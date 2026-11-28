## The review of the plant model's groups (ModelAnalysis.cs): the model drawn island by island,
## coloured by what each group is and whether it stands for a flowsheet object, a list of the
## equipment with the objects the matching proposes, and the tools that change the groups. Every
## change goes to DWSIM, which keeps it in the flowsheet (one step of Ctrl+Z) and sends the groups
## back.
##
## Mouse: a click picks a group, Ctrl+click adds groups, Alt+click picks single islands (to split
## them off), a double click frames the group.
extends Node3D

signal loaded

const ROLES := ["equipment", "piping", "structure", "scenery", "hidden"]
const COLORS := {
	"chosen": Color(0.30, 0.72, 0.36),
	"proposed": Color(0.95, 0.78, 0.22),
	"free": Color(0.66, 0.67, 0.70),
	"piping": Color(0.36, 0.56, 0.90),
	"structure": Color(0.80, 0.76, 0.66),
	"scenery": Color(0.58, 0.52, 0.44),
	"selected": Color(1.0, 0.45, 0.12),
}

var main: Node
var active := false
var loading := false
## Group id to its data (id, name, tag, role, kind, islands, object, suggested, score, why, user).
var groups := {}
## The equipment group ids in the order of the list.
var order: Array = []
var units: Array = []
## The pipes of the model that join two groups whose objects no stream joins: {a, b, from, to, fromTag, toTag}.
var links: Array = []
var island_group := {}
var nodes := {}
var selected_groups: Array = []
var selected_islands: Array = []
var show_pipes := true
var show_structure := true
var show_scenery := true
var panel: PanelContainer

var _glb := ""
var _wanted := ""
var _thread: Thread
var _root: Node3D
var _faces := {}
var _mats := {}
var _list: ItemList
var _detail: RichTextLabel
var _assign: OptionButton
var _role: OptionButton
var _hint: Label
var _links: ItemList
var _press := Vector2.ZERO
var _updating := false


func _ready() -> void:
	for k in COLORS.keys():
		var m := StandardMaterial3D.new()
		m.albedo_color = COLORS[k]
		m.roughness = 0.75
		m.cull_mode = BaseMaterial3D.CULL_DISABLED
		if k == "selected":
			m.emission_enabled = true
			m.emission = COLORS[k] * 0.35
		_mats[k] = m


# --- entering and leaving ----------------------------------------------------------------------

func toggle() -> void:
	if active:
		leave()
	else:
		enter()


func enter() -> void:
	var info: Dictionary = main.plant_model.info
	if info.is_empty():
		main._show_toast("Import a plant model first.")
		return
	active = true
	panel.visible = true
	main.card.visible = false
	main.plant_model.visible = false
	var path = info.get("islands")
	_load(str(path) if path != null else "")
	if main.link != null and main.link.state == "connected":
		main.link.send({"type": "model.groups"})
	if _root != null:
		_root.visible = true
	main.refit_ground()


func leave() -> void:
	active = false
	panel.visible = false
	main.card.visible = true
	main.plant_model.visible = main.plant_model.show_model
	if _root != null:
		_root.visible = false
	main.refit_ground()


func has_view() -> bool:
	return active and _root != null


func meshes() -> Array:
	if not has_view():
		return []
	return _root.find_children("*", "MeshInstance3D", true, false)


# --- the islands -------------------------------------------------------------------------------

func _load(path: String) -> void:
	_wanted = path
	if path == "" or (path == _glb and _root != null) or loading:
		return
	loading = true
	_thread = Thread.new()
	_thread.start(_read.bind(path))


func _read(path: String) -> void:
	var doc := GLTFDocument.new()
	var state := GLTFState.new()
	var node: Node = null
	if doc.append_from_file(path, state) == OK:
		node = doc.generate_scene(state)
	call_deferred("_done", path, node)


func _done(path: String, node: Node) -> void:
	_thread.wait_to_finish()
	_thread = null
	loading = false
	if path != _wanted:
		if node != null:
			node.free()
		_load(_wanted)
		return
	if _root != null:
		_root.queue_free()
	_root = node as Node3D
	_glb = path
	nodes.clear()
	_faces.clear()
	if _root != null:
		add_child(_root)
		for mi in _root.find_children("island_*", "MeshInstance3D", true, false):
			var n := str(mi.name)
			nodes[int(n.substr(7))] = mi
		_root.visible = active
	_recolor()
	if active:
		main.refit_ground()
	loaded.emit()


func _exit_tree() -> void:
	if _thread != null:
		_thread.wait_to_finish()


# --- the groups from DWSIM ---------------------------------------------------------------------

func on_groups(m: Dictionary) -> void:
	groups.clear()
	island_group.clear()
	for g in m.get("groups", []):
		if g is Dictionary:
			groups[str(g["id"])] = g
			for i in g.get("islands", []):
				island_group[int(i)] = str(g["id"])
	units = m.get("units", [])
	links = m.get("links", [])
	selected_groups = selected_groups.filter(func(id): return groups.has(id))
	selected_islands = selected_islands.filter(func(i): return island_group.has(i))
	order = groups.keys().filter(func(id): return str(groups[id].get("role", "")) == "equipment")
	order.sort_custom(func(a, b):
		var ra := _rank(groups[a])
		var rb := _rank(groups[b])
		if ra != rb:
			return ra < rb
		return _label_of(groups[a]) < _label_of(groups[b]))
	_fill_units()
	_refresh_list()
	_refresh_links()
	_recolor()
	_show_detail()


func _rank(g: Dictionary) -> int:
	if g.get("object") != null:
		return 2
	if g.get("suggested") != null:
		return 0
	return 1


func _state(g: Dictionary) -> String:
	match str(g.get("role", "")):
		"equipment":
			if g.get("object") != null:
				return "chosen"
			if g.get("suggested") != null:
				return "proposed"
			return "free"
		"piping":
			return "piping"
		"structure":
			return "structure"
		"hidden":
			return "hidden"
	return "scenery"


func _label_of(g: Dictionary) -> String:
	var t = g.get("tag")
	if t != null and str(t) != "":
		return str(t)
	return str(g.get("name", g.get("id", "")))


func _unit_tag(name) -> String:
	if name == null:
		return ""
	for u in units:
		if str(u.get("name", "")) == str(name):
			return str(u.get("tag", name))
	return str(name)


func _recolor() -> void:
	for i in nodes.keys():
		var mi: MeshInstance3D = nodes[i]
		var gid: String = island_group.get(i, "")
		var g: Dictionary = groups.get(gid, {})
		var state := _state(g) if not g.is_empty() else "scenery"
		var shown := state != "hidden"
		if state == "piping":
			shown = show_pipes
		elif state == "structure":
			shown = show_structure
		elif state == "scenery":
			shown = show_scenery
		mi.visible = shown
		var picked := selected_islands.has(i) or selected_islands.is_empty() and selected_groups.has(gid)
		mi.material_override = _mats["selected"] if picked else _mats.get(state, _mats["scenery"])


# --- picking -----------------------------------------------------------------------------------

## The island under a point of the screen, or -1: the boxes the ray crosses, nearest first, then
## their triangles.
func pick(screen: Vector2) -> int:
	var cam: Camera3D = main.rig.camera
	var from := cam.project_ray_origin(screen)
	var dir := cam.project_ray_normal(screen)
	var hits := []
	for i in nodes.keys():
		var mi: MeshInstance3D = nodes[i]
		if not mi.visible:
			continue
		var box: AABB = mi.global_transform * mi.get_aabb()
		var h = box.intersects_ray(from, dir)
		if h == null and box.has_point(from):
			h = from
		if h != null:
			hits.append([(h - from).dot(dir), i])
	hits.sort_custom(func(a, b): return a[0] < b[0])
	var best := INF
	var best_i := -1
	for hit in hits:
		if hit[0] > best:
			break
		var i: int = hit[1]
		var faces: PackedVector3Array = _world_faces(i)
		for k in range(0, faces.size(), 3):
			var p = Geometry3D.ray_intersects_triangle(from, dir, faces[k], faces[k + 1], faces[k + 2])
			if p != null:
				var t: float = (p - from).dot(dir)
				if t < best:
					best = t
					best_i = i
	return best_i


func _world_faces(i: int) -> PackedVector3Array:
	if _faces.has(i):
		return _faces[i]
	var mi: MeshInstance3D = nodes[i]
	var local := mi.mesh.get_faces()
	var xf := mi.global_transform
	var out := PackedVector3Array()
	out.resize(local.size())
	for k in local.size():
		out[k] = xf * local[k]
	_faces[i] = out
	return out


## The window's input goes here first while the review is open: clicks pick groups and islands,
## the rest (turning, panning, zooming) goes on to the view.
func handle(event: InputEvent) -> bool:
	if not active or not (event is InputEventMouseButton):
		return false
	var mb := event as InputEventMouseButton
	if mb.button_index != MOUSE_BUTTON_LEFT:
		return false
	if mb.pressed:
		_press = mb.position
		if mb.double_click:
			var i := pick(mb.position)
			if i >= 0 and island_group.has(i):
				frame(island_group[i])
		return true
	if mb.position.distance_to(_press) > 4.0:
		return true
	var i := pick(mb.position)
	var gid: String = island_group.get(i, "") if i >= 0 else ""
	if mb.alt_pressed and i >= 0:
		# single islands of one group, to split them off
		if not selected_groups.has(gid):
			selected_groups = [gid]
			selected_islands = []
		if selected_islands.has(i):
			selected_islands.erase(i)
		else:
			selected_islands.append(i)
	elif mb.ctrl_pressed and gid != "":
		selected_islands = []
		if selected_groups.has(gid):
			selected_groups.erase(gid)
		else:
			selected_groups.append(gid)
	else:
		selected_islands = []
		selected_groups = [gid] if gid != "" else []
	_after_selection()
	return true


func select(ids: Array) -> void:
	selected_groups = ids.filter(func(id): return groups.has(str(id))).map(func(id): return str(id))
	selected_islands = []
	_after_selection()


func _after_selection() -> void:
	_recolor()
	_updating = true
	_list.deselect_all()
	for k in order.size():
		if selected_groups.has(order[k]):
			_list.select(k, false)
			_list.ensure_current_is_visible()
	_updating = false
	_show_detail()


func frame(gid: String) -> void:
	var pts := PackedVector3Array()
	for i in groups.get(gid, {}).get("islands", []):
		if nodes.has(int(i)):
			var mi: MeshInstance3D = nodes[int(i)]
			var b: AABB = mi.global_transform * mi.get_aabb()
			for k in 8:
				pts.append(b.get_endpoint(k))
	if not pts.is_empty():
		main.rig.fit_points(pts, main._aspect())


# --- the panel ---------------------------------------------------------------------------------

func build_panel(style: StyleBox) -> PanelContainer:
	panel = PanelContainer.new()
	panel.add_theme_stylebox_override("panel", style)
	panel.visible = false
	var v := VBoxContainer.new()
	v.custom_minimum_size = Vector2(340, 0)
	panel.add_child(v)
	var title := Label.new()
	title.text = "Plant model review"
	title.add_theme_font_size_override("font_size", 14)
	title.add_theme_color_override("font_color", Color(0.11, 0.15, 0.2))
	v.add_child(title)
	var f := HBoxContainer.new()
	v.add_child(f)
	f.add_child(_check("pipes", show_pipes, func(on):
		show_pipes = on
		_recolor()))
	f.add_child(_check("structure", show_structure, func(on):
		show_structure = on
		_recolor()))
	f.add_child(_check("civil", show_scenery, func(on):
		show_scenery = on
		_recolor()))
	_list = ItemList.new()
	_list.custom_minimum_size = Vector2(320, 250)
	_list.select_mode = ItemList.SELECT_MULTI
	_list.focus_mode = Control.FOCUS_NONE
	_list.add_theme_font_size_override("font_size", 12)
	_list.multi_selected.connect(func(_index, _on):
		if _updating:
			return
		selected_groups = []
		for k in _list.get_selected_items():
			selected_groups.append(order[k])
		selected_islands = []
		_recolor()
		_show_detail())
	_list.item_activated.connect(func(k): frame(order[k]))
	v.add_child(_list)
	_detail = RichTextLabel.new()
	_detail.bbcode_enabled = true
	_detail.fit_content = true
	_detail.scroll_active = false
	_detail.custom_minimum_size = Vector2(320, 0)
	_detail.add_theme_font_size_override("normal_font_size", 12)
	_detail.add_theme_font_size_override("bold_font_size", 12)
	_detail.add_theme_color_override("default_color", Color(0.11, 0.15, 0.2))
	v.add_child(_detail)
	var r1 := HBoxContainer.new()
	v.add_child(r1)
	r1.add_child(_button("Accept", "The group stands for the object the matching proposes.", func(): _each("model.assign", {"accept": true})))
	r1.add_child(_button("Clear", "The group stops standing for a flowsheet object.", func(): _each("model.assign", {"object": null})))
	r1.add_child(_button("Create in flowsheet", "Adds a flowsheet object of the group's kind, where the group stands, and the group stands for it.", func(): _each("model.create", {})))
	var r2 := HBoxContainer.new()
	v.add_child(r2)
	_assign = OptionButton.new()
	_assign.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_assign.focus_mode = Control.FOCUS_NONE
	r2.add_child(_assign)
	r2.add_child(_button("Assign", "The selected group stands for the object chosen here (instead of the one it had).", func():
		if _assign.selected < 0 or _assign.selected >= units.size():
			return
		_one("model.assign", {"object": str(units[_assign.selected].get("name", ""))})))
	r2.add_child(_button("Add", "The selected group stands for the object chosen here too, as when the flowsheet draws one exchanger as two objects.", func():
		if _assign.selected < 0 or _assign.selected >= units.size():
			return
		_one("model.assign", {"object": str(units[_assign.selected].get("name", "")), "add": true})))
	var r3 := HBoxContainer.new()
	v.add_child(r3)
	_role = OptionButton.new()
	for r in ROLES:
		_role.add_item(r)
	_role.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_role.focus_mode = Control.FOCUS_NONE
	r3.add_child(_role)
	r3.add_child(_button("Set role", "What the selected groups are: equipment, piping, structure, civil (scenery) or hidden.", func():
		_each("model.role", {"role": ROLES[_role.selected]})))
	var r4 := HBoxContainer.new()
	v.add_child(r4)
	r4.add_child(_button("Join", "The selected groups become one.", func():
		if selected_groups.size() >= 2:
			_send({"type": "model.merge", "groups": selected_groups.duplicate()})))
	r4.add_child(_button("Split", "The islands picked with Alt+click become a group of their own.", func():
		if selected_groups.size() == 1 and not selected_islands.is_empty():
			_send({"type": "model.split", "group": selected_groups[0], "islands": selected_islands.duplicate()})
			selected_islands = []))
	r4.add_child(_button("Close", "Back to the plant.", func(): leave()))
	var lt := Label.new()
	lt.text = "Missing connections (a pipe of the model, no stream)"
	lt.add_theme_font_size_override("font_size", 12)
	lt.add_theme_color_override("font_color", Color(0.11, 0.15, 0.2))
	v.add_child(lt)
	_links = ItemList.new()
	_links.custom_minimum_size = Vector2(320, 70)
	_links.focus_mode = Control.FOCUS_NONE
	_links.add_theme_font_size_override("font_size", 12)
	v.add_child(_links)
	var r5 := HBoxContainer.new()
	v.add_child(r5)
	r5.add_child(_button("Connect ->", "Adds a stream from the first object to the second.", func(): _connect(true)))
	r5.add_child(_button("Connect <-", "Adds a stream from the second object to the first.", func(): _connect(false)))
	_hint = Label.new()
	_hint.text = "Click: a group. Ctrl+click: more groups. Alt+click: single islands. Double click: frame."
	_hint.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_hint.custom_minimum_size = Vector2(320, 0)
	_hint.add_theme_font_size_override("font_size", 11)
	_hint.add_theme_color_override("font_color", Color(0.27, 0.32, 0.38))
	v.add_child(_hint)
	return panel


func _button(text: String, tip: String, pressed: Callable) -> Button:
	var b := Button.new()
	b.text = text
	b.tooltip_text = tip
	b.focus_mode = Control.FOCUS_NONE
	b.pressed.connect(pressed)
	return b


func _check(text: String, on: bool, toggled: Callable) -> CheckBox:
	var c := CheckBox.new()
	c.text = text
	c.button_pressed = on
	c.focus_mode = Control.FOCUS_NONE
	c.add_theme_color_override("font_color", Color(0.11, 0.15, 0.2))
	c.toggled.connect(toggled)
	return c


func _send(m: Dictionary) -> void:
	if main.link == null or main.link.state != "connected":
		main._show_toast("Open the 3D window from DWSIM to change the plant model.")
		return
	main.link.send(m)


## The same change for each selected group, one message each.
func _each(type: String, fields: Dictionary) -> void:
	for gid in selected_groups:
		var m := fields.duplicate()
		m["type"] = type
		m["group"] = gid
		_send(m)


func _one(type: String, fields: Dictionary) -> void:
	if selected_groups.size() != 1:
		main._show_toast("Select one group.")
		return
	var m := fields.duplicate()
	m["type"] = type
	m["group"] = selected_groups[0]
	_send(m)


func _fill_units() -> void:
	_assign.clear()
	for u in units:
		_assign.add_item("%s  (%s)" % [str(u.get("tag", "")), str(u.get("kind", ""))])


func _refresh_list() -> void:
	_list.clear()
	for id in order:
		var g: Dictionary = groups[id]
		var text := _label_of(g)
		if g.get("kind") != null:
			text += "  " + str(g["kind"])
		if g.get("object") != null:
			text += "  =  " + _objects_text(g, "object", "also")
		if g.get("suggested") != null or g.get("suggestedAlso") != null:
			text += "  ->  %s (%d %%)" % [_objects_text(g, "suggested", "suggestedAlso"), int(round(float(g.get("score", 0)) * 100.0))]
		var k := _list.add_item(text)
		_list.set_item_custom_fg_color(k, COLORS[_state(g)].lightened(0.2))


## The tags of a group's object and the others of the same tag ("E-121, E-121 (gas)").
func _objects_text(g: Dictionary, first: String, more: String) -> String:
	var tags := []
	if g.get(first) != null:
		tags.append(_unit_tag(g[first]))
	for o in g.get(more, []):
		tags.append(_unit_tag(o))
	return ", ".join(tags)


func _refresh_links() -> void:
	if _links == null:
		return
	_links.clear()
	for l in links:
		_links.add_item("%s  -  %s" % [str(l.get("fromTag", "")), str(l.get("toTag", ""))])


## A stream between the two objects of a missing connection, the way the user says it flows.
func _connect(forward: bool) -> void:
	var sel := _links.get_selected_items()
	if sel.is_empty():
		main._show_toast("Pick a connection in the list.")
		return
	var l: Dictionary = links[sel[0]]
	var a := str(l.get("from", ""))
	var b := str(l.get("to", ""))
	_send({"type": "connect", "from": a if forward else b, "to": b if forward else a})
	_send({"type": "model.groups"})


func _show_detail() -> void:
	if selected_groups.is_empty():
		var n_chosen := order.filter(func(id): return groups[id].get("object") != null).size()
		var n_prop := order.filter(func(id): return groups[id].get("object") == null and groups[id].get("suggested") != null).size()
		_detail.text = "%d equipment groups: %d chosen, %d proposed." % [order.size(), n_chosen, n_prop]
		return
	var g: Dictionary = groups.get(selected_groups[0], {})
	if g.is_empty():
		_detail.text = ""
		return
	var t := "[b]%s[/b]  %s, %s" % [_label_of(g), str(g.get("role", "")), str(g.get("kind", "-"))]
	var n_isl: int = g.get("islands", []).size()
	t += "\n%.1f m long, %.2f m across, %d %s" % [float(g.get("length", 0)), float(g.get("diameter", 0)), n_isl, "island" if n_isl == 1 else "islands"]
	if g.get("object") != null:
		t += "\nStands for [b]%s[/b]" % _objects_text(g, "object", "also")
	if g.get("suggested") != null or g.get("suggestedAlso") != null:
		t += "\nProposed: %s, %d %% sure (%s)" % [_objects_text(g, "suggested", "suggestedAlso"), int(round(float(g.get("score", 0)) * 100.0)), str(g.get("why", ""))]
	if selected_groups.size() > 1:
		t += "\n%d groups selected" % selected_groups.size()
	if not selected_islands.is_empty():
		t += "\n%d %s picked to split off" % [selected_islands.size(), "island" if selected_islands.size() == 1 else "islands"]
	_detail.text = t
