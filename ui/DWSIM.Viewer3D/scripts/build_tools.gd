## Building the flowsheet in the window: the equipment palette (the catalog DWSIM sends in its
## welcome), the place tool, which drops new equipment where the user clicks on the ground, and the
## connect tool, which joins the nozzles of two items with a new stream, or a nozzle to the battery
## limit with a feed or a product stream. Delete removes the selection. Everything goes to DWSIM,
## which changes the flowsheet (the 2D drawing follows) and sends the scene back.
class_name BuildTools
extends Node

enum Tool { SELECT, PLACE, CONNECT }

const PORT_RADIUS := 0.22
const PICK_PIXELS := 14.0

var main: Node3D
var tool := Tool.SELECT
var place_type := ""
var place_name := ""
var place_external := ""

var palette_panel: PanelContainer
var _palette_box: VBoxContainer
var _tool_label: Label
var _ghost: MeshInstance3D
var _ports_root: Node3D
var _wire: MeshInstance3D
var _start: Dictionary = {}  # the first nozzle of a connection: id, k, i, p
var _mat_out: StandardMaterial3D
var _mat_in: StandardMaterial3D
var _mat_used: StandardMaterial3D
var _mat_hot: StandardMaterial3D
var _mat_energy: StandardMaterial3D


func _init(owner_main: Node3D) -> void:
	main = owner_main


func _ready() -> void:
	_mat_out = _flat(Color(0.2, 0.72, 0.35))
	_mat_in = _flat(Color(0.2, 0.5, 0.92))
	_mat_used = _flat(Color(0.55, 0.57, 0.6))
	_mat_hot = _flat(Color(1.0, 0.75, 0.15))
	_mat_energy = _flat(Color(0.98, 0.55, 0.1))
	_ports_root = Node3D.new()
	_ports_root.visible = false
	add_child(_ports_root)
	_ghost = MeshInstance3D.new()
	var gm := StandardMaterial3D.new()
	gm.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	gm.albedo_color = Color(0.24, 0.5, 0.84, 0.35)
	gm.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	var box := BoxMesh.new()
	box.size = Vector3(2, 2, 2)
	_ghost.mesh = box
	_ghost.material_override = gm
	_ghost.visible = false
	add_child(_ghost)
	_wire = MeshInstance3D.new()
	var wm := _flat(Color(1.0, 0.75, 0.15))
	wm.no_depth_test = true
	_wire.material_override = wm
	add_child(_wire)


func _flat(c: Color) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	m.albedo_color = c
	m.no_depth_test = true
	m.render_priority = 6
	return m


# --- the palette -----------------------------------------------------------------------------

func build_panel(style: StyleBox) -> PanelContainer:
	palette_panel = PanelContainer.new()
	palette_panel.add_theme_stylebox_override("panel", style)
	var outer := VBoxContainer.new()
	palette_panel.add_child(outer)
	var tools := HBoxContainer.new()
	outer.add_child(tools)
	_button(tools, "Select", func(): set_tool(Tool.SELECT))
	_button(tools, "Connect", func(): set_tool(Tool.CONNECT))
	_button(tools, "Delete", delete_selection)
	_tool_label = Label.new()
	_tool_label.add_theme_color_override("font_color", Color(0.27, 0.32, 0.38))
	_tool_label.add_theme_font_size_override("font_size", 11)
	_tool_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_tool_label.custom_minimum_size = Vector2(200, 0)
	outer.add_child(_tool_label)
	var scroll := ScrollContainer.new()
	scroll.custom_minimum_size = Vector2(200, 340)
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	# the list shows once DWSIM sends its catalog
	scroll.visible = false
	outer.add_child(scroll)
	_palette_box = VBoxContainer.new()
	_palette_box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(_palette_box)
	_update_label()
	return palette_panel


## Fills the palette from DWSIM's catalog: {group, name, objectType} entries.
func set_palette(entries: Array) -> void:
	(_palette_box.get_parent() as Control).visible = not entries.is_empty()
	for c in _palette_box.get_children():
		c.queue_free()
	var group := ""
	for e in entries:
		if not (e is Dictionary):
			continue
		if str(e.get("group", "")) != group:
			group = str(e.get("group", ""))
			var h := Label.new()
			h.text = group
			h.add_theme_color_override("font_color", Color(0.27, 0.32, 0.38))
			h.add_theme_font_size_override("font_size", 12)
			_palette_box.add_child(h)
		var type := str(e.get("objectType", ""))
		var name := str(e.get("name", type))
		var external := str(e.get("external", ""))
		var b := _button(_palette_box, name, func(): start_place(type, name, external))
		b.alignment = HORIZONTAL_ALIGNMENT_LEFT
		b.name = ("ext_" + external if external != "" else "add_" + type).validate_node_name()


func _button(parent: Control, text: String, pressed: Callable) -> Button:
	var b := Button.new()
	b.text = text
	b.focus_mode = Control.FOCUS_NONE
	b.pressed.connect(pressed)
	parent.add_child(b)
	return b


# --- tools -----------------------------------------------------------------------------------

func set_tool(t: Tool) -> void:
	tool = t
	_start = {}
	_wire.mesh = null
	_ghost.visible = false
	_ports_root.visible = t == Tool.CONNECT
	if t == Tool.CONNECT:
		refresh_ports()
	_update_label()


func start_place(type: String, name: String, external := "") -> void:
	place_type = type
	place_name = name
	place_external = external
	set_tool(Tool.PLACE)


func _update_label() -> void:
	if _tool_label == null:
		return
	match tool:
		Tool.PLACE:
			_tool_label.text = "Click on the ground to place the %s (Esc cancels)." % place_name
		Tool.CONNECT:
			if _start.is_empty():
				_tool_label.text = "Click a nozzle (green: outlet, blue: inlet, orange: energy), then another nozzle, or the ground for a feed or a product stream."
			else:
				_tool_label.text = "Now click the other nozzle, or the ground for a %s stream (Esc cancels)." % ("product" if str(_start["k"]).ends_with("out") else "feed")
		_:
			_tool_label.text = "Pick equipment to add, or Connect to join nozzles."


func delete_selection() -> void:
	var ids := []
	for id in main.selected:
		if not str(id).begins_with("line#"):
			ids.append(id)
	if not ids.is_empty():
		main.link.send({"type": "delete", "ids": ids})


## The nozzles of every item, coloured by kind; refreshed with every new scene while connecting.
func refresh_ports() -> void:
	for c in _ports_root.get_children():
		c.queue_free()
	if tool != Tool.CONNECT:
		return
	for id in main.entries.keys():
		var e: Dictionary = main.entries[id]
		if e["kind"] != "item":
			continue
		for p in e["data"].get("ports", []):
			var mi := MeshInstance3D.new()
			var s := SphereMesh.new()
			s.radius = PORT_RADIUS
			s.height = PORT_RADIUS * 2.0
			s.radial_segments = 12
			s.rings = 6
			mi.mesh = s
			var k := str(p.get("k"))
			mi.material_override = _mat_used if bool(p.get("used", false)) else (_mat_energy if k.begins_with("e") else (_mat_out if k == "out" else _mat_in))
			mi.position = SceneBuilder.vec(p.get("p"))
			mi.set_meta("port", {"id": id, "k": str(p.get("k")), "i": int(p.get("i", 0)), "used": bool(p.get("used", false))})
			_ports_root.add_child(mi)


func _port_at(screen: Vector2) -> MeshInstance3D:
	var cam: Camera3D = main.rig.camera
	var best: MeshInstance3D = null
	var best_d := PICK_PIXELS
	for mi in _ports_root.get_children():
		var d := cam.unproject_position(mi.global_position).distance_to(screen)
		if d < best_d:
			best_d = d
			best = mi
	return best


func _ground(screen: Vector2):
	var cam: Camera3D = main.rig.camera
	var dir := cam.project_ray_normal(screen)
	if absf(dir.y) < 0.05:
		return null
	return Plane(Vector3.UP, 0.0).intersects_ray(cam.project_ray_origin(screen), dir)


## Handles the input of the place and connect tools, and Delete; true when it took the event.
func handle(event: InputEvent) -> bool:
	if event is InputEventKey and event.pressed and not event.echo:
		var k := (event as InputEventKey).keycode
		match k:
			KEY_ESCAPE:
				if tool != Tool.SELECT:
					set_tool(Tool.SELECT)
					return true
			KEY_C:
				set_tool(Tool.SELECT if tool == Tool.CONNECT else Tool.CONNECT)
				return true
			KEY_P:
				palette_panel.visible = not palette_panel.visible
				return true
			KEY_DELETE:
				delete_selection()
				return true
		return false
	if tool == Tool.SELECT:
		return false
	if event is InputEventMouseMotion:
		var at := (event as InputEventMouseMotion).position
		if tool == Tool.PLACE:
			var g = _ground(at)
			_ghost.visible = g != null
			if g != null:
				_ghost.position = Vector3(snappedf(g.x, 0.5), 1.0, snappedf(g.z, 0.5))
		elif tool == Tool.CONNECT:
			for mi in _ports_root.get_children():
				mi.scale = Vector3.ONE
			var hot := _port_at(at)
			if hot != null:
				hot.scale = Vector3.ONE * 1.6
			_draw_wire(at)
		return false
	if event is InputEventMouseButton and event.pressed:
		var mb := event as InputEventMouseButton
		if mb.button_index == MOUSE_BUTTON_RIGHT:
			set_tool(Tool.SELECT)
			return true
		if mb.button_index != MOUSE_BUTTON_LEFT:
			return false
		if tool == Tool.PLACE:
			var g = _ground(mb.position)
			if g != null:
				var add := {"type": "add", "objectType": place_type,
					"x": roundf(snappedf(g.x, 0.5) * 1000.0), "y": roundf(-snappedf(g.z, 0.5) * 1000.0), "z": 0, "rot": 0}
				if place_external != "":
					add["external"] = place_external
				main.link.send(add)
				if not mb.shift_pressed:
					set_tool(Tool.SELECT)
			return true
		if tool == Tool.CONNECT:
			_click_connect(mb.position)
			return true
	if event is InputEventMouseButton and not event.pressed and tool != Tool.SELECT:
		return (event as InputEventMouseButton).button_index == MOUSE_BUTTON_LEFT
	return false


func _click_connect(at: Vector2) -> void:
	var hit := _port_at(at)
	if _start.is_empty():
		if hit == null:
			main._show_toast("Click a nozzle to start a connection.")
			return
		var p: Dictionary = hit.get_meta("port")
		if p["used"]:
			main._show_toast("That nozzle already carries a stream.")
			return
		_start = p.duplicate()
		_start["at"] = hit.global_position
		_update_label()
		return
	var m := {"type": "connect"}
	var energy := str(_start["k"]).begins_with("e")
	var outward := str(_start["k"]).ends_with("out")
	if energy:
		m["energy"] = true
	if hit == null:
		if _ground(at) == null:
			return
		# the ground: a product stream from an outlet, a feed stream to an inlet
		if outward:
			m["from"] = _start["id"]
			m["fromPort"] = _start["i"]
		else:
			m["to"] = _start["id"]
			m["toPort"] = _start["i"]
	else:
		var p: Dictionary = hit.get_meta("port")
		var pk := str(p["k"])
		if p["used"] or p["id"] == _start["id"] or pk.begins_with("e") != energy or pk.ends_with("out") == outward:
			main._show_toast("Join an outlet to a free inlet of another item, material to material, energy to energy.")
			return
		var out_p: Dictionary = _start if outward else p
		var in_p: Dictionary = p if outward else _start
		m["from"] = out_p["id"]
		m["fromPort"] = out_p["i"]
		m["to"] = in_p["id"]
		m["toPort"] = in_p["i"]
	main.link.send(m)
	_start = {}
	_wire.mesh = null
	_update_label()


func _draw_wire(at: Vector2) -> void:
	if _start.is_empty():
		_wire.mesh = null
		return
	var end = null
	var hot := _port_at(at)
	if hot != null:
		end = hot.global_position
	else:
		end = _ground(at)
	if end == null:
		return
	var im := ImmediateMesh.new()
	im.surface_begin(Mesh.PRIMITIVE_LINES)
	im.surface_add_vertex(_start["at"])
	im.surface_add_vertex(end)
	im.surface_end()
	_wire.mesh = im
