## Editing the plant layout in the window: drag items over the ground (on a 0.5 m grid), turn them
## by quarter turns, raise or lower them, nudge them with the arrow keys, type their place in the
## panel, align several, and undo. Every change goes to DWSIM as one "place" command, which keeps
## the place in the flowsheet's layout (the same one the SolidCraft model uses) and sends back the
## items and the lines routed again. Ctrl+Z and Ctrl+Y undo and redo in DWSIM's own 3D history.
class_name LayoutEditor
extends Node

## The grid the drag and the arrow keys snap to, m; Alt drags freely.
const SNAP := 0.5
const FINE := 0.1

var main: Node3D

var panel: PanelContainer
var _x: SpinBox
var _y: SpinBox
var _z: SpinBox
var _multi: HBoxContainer
var _tag: LineEdit
var _updating := false

var _pending_id := ""
var _press := Vector2.ZERO
var _dragging := false
var _plane_y := 0.0
var _grab := Vector3.ZERO
var _starts := {}  # id -> [node position, label position]


func _init(owner_main: Node3D) -> void:
	main = owner_main


## The items of the selection that can be placed (instruments follow their measuring point).
func movable() -> Array:
	var ids := []
	for id in main.selected:
		var e: Dictionary = main.entries.get(id, {})
		# an item the plant model stands for stays where the model puts it
		if e.get("kind", "") == "item" and not bool(e["data"].get("inst", false)) and not (e["data"].get("model") is Dictionary and main.model_replaces()):
			ids.append(id)
	return ids


# --- commands to DWSIM -----------------------------------------------------------------------

## The place of an item as DWSIM keeps it: mm, Z up, Y to the back.
static func dwsim_place(id: String, pos: Vector3, rot: int) -> Dictionary:
	return {"id": id, "x": roundf(pos.x * 1000.0), "y": roundf(-pos.z * 1000.0), "z": roundf(pos.y * 1000.0), "rot": rot}


func _rot(id: String) -> int:
	return int(main.entries[id]["data"].get("rot", 0))


func _send(items: Array) -> void:
	if items.is_empty():
		return
	main.link.send({"type": "place", "items": items})


## Moves the movable items of the selection by an offset (Godot frame, m) and turns them.
func shift(offset: Vector3, quarter_turns := 0) -> void:
	var items := []
	for id in movable():
		var node: Node3D = main.entries[id]["node"]
		items.append(dwsim_place(id, node.position + offset, posmod(_rot(id) + 90 * quarter_turns, 360)))
	_send(items)


func set_axis(axis: String, value_m: float) -> void:
	var ids := movable()
	if ids.is_empty():
		return
	var id: String = ids[0]
	var p: Vector3 = (main.entries[id]["node"] as Node3D).position
	match axis:
		"x": p.x = value_m
		"y": p.z = -value_m
		"z": p.y = value_m
	_send([dwsim_place(id, p, _rot(id))])


## Lines the selection up with its first item along one axis of the plot plan ("x", "y" or "z").
func align(axis: String) -> void:
	var ids := movable()
	if ids.size() < 2:
		return
	var ref: Vector3 = (main.entries[ids[0]]["node"] as Node3D).position
	var items := []
	for id in ids:
		var p: Vector3 = (main.entries[id]["node"] as Node3D).position
		match axis:
			"x": p.x = ref.x
			"y": p.z = ref.z
			"z": p.y = ref.y
		items.append(dwsim_place(id, p, _rot(id)))
	_send(items)


func follow_drawing() -> void:
	var ids := movable()
	if not ids.is_empty():
		main.link.send({"type": "layout.reset", "ids": ids})


# --- mouse and keys --------------------------------------------------------------------------

## Handles the input that edits the layout; true when it took the event.
func handle(event: InputEvent) -> bool:
	if event is InputEventMouseButton:
		var mb := event as InputEventMouseButton
		if mb.button_index != MOUSE_BUTTON_LEFT:
			return false
		if mb.pressed and not mb.double_click:
			_pending_id = main._pick(mb.position)
			_press = mb.position
			_dragging = false
			if _pending_id != "" and main.entries[_pending_id]["kind"] != "item":
				_pending_id = ""
			return false
		if not mb.pressed and _dragging:
			_finish_drag()
			return true
		_pending_id = ""
		return false
	if event is InputEventMouseMotion and (event.button_mask & MOUSE_BUTTON_MASK_LEFT) != 0 and _pending_id != "":
		var mm := event as InputEventMouseMotion
		if not _dragging:
			if mm.position.distance_to(_press) < 5.0 or not _begin_drag():
				return false
		_drag_to(mm.position, mm.alt_pressed)
		return true
	if event is InputEventKey and event.pressed:
		var k := event as InputEventKey
		var step := FINE if k.shift_pressed else SNAP
		if k.ctrl_pressed or k.meta_pressed:
			match k.keycode:
				KEY_Z:
					main.link.send({"type": "redo" if k.shift_pressed else "undo"})
					return true
				KEY_Y:
					main.link.send({"type": "redo"})
					return true
			return false
		match k.keycode:
			KEY_T:
				shift(Vector3.ZERO, -1 if k.shift_pressed else 1)
				return true
			KEY_PAGEUP:
				shift(Vector3(0, step, 0))
				return true
			KEY_PAGEDOWN:
				shift(Vector3(0, -step, 0))
				return true
			KEY_LEFT, KEY_RIGHT, KEY_UP, KEY_DOWN:
				if movable().is_empty():
					return false
				shift(_ground_step(k.keycode) * step)
				return true
	return false


## The plot plan axis closest to a screen direction, so the arrows move the items the way they look.
func _ground_step(key: int) -> Vector3:
	var r: Vector3 = main.rig.right()
	var u: Vector3 = main.rig.up()
	var d: Vector3
	match key:
		KEY_RIGHT: d = r
		KEY_LEFT: d = -r
		KEY_UP: d = u
		_: d = -u
	d.y = 0.0
	if d.length() < 1e-6:
		# looking straight along a horizontal axis: up and down mean away and towards the viewer
		d = -main.rig.eye() * (1.0 if key == KEY_UP else -1.0)
		d.y = 0.0
	if absf(d.x) >= absf(d.z):
		return Vector3(signf(d.x), 0, 0)
	return Vector3(0, 0, signf(d.z))


func _begin_drag() -> bool:
	if not main.selected.has(_pending_id):
		main._set_selection([_pending_id], true)
	var ids := movable()
	if not ids.has(_pending_id):
		return false
	var node: Node3D = main.entries[_pending_id]["node"]
	_plane_y = node.position.y
	var hit = _ground_hit(_press)
	if hit == null:
		return false
	_grab = hit
	_starts.clear()
	for id in ids:
		var e: Dictionary = main.entries[id]
		var label: Node3D = e.get("label")
		_starts[id] = [(e["node"] as Node3D).position, label.position if label != null else Vector3.ZERO]
	_dragging = true
	return true


func _ground_hit(screen: Vector2):
	var cam: Camera3D = main.rig.camera
	var dir := cam.project_ray_normal(screen)
	if absf(dir.y) < 0.05:
		return null  # a side view: the ground is edge on
	return Plane(Vector3.UP, _plane_y).intersects_ray(cam.project_ray_origin(screen), dir)


func _drag_to(screen: Vector2, free: bool) -> void:
	var hit = _ground_hit(screen)
	if hit == null:
		return
	var d: Vector3 = hit - _grab
	d.y = 0.0
	if not free:
		d.x = snappedf(d.x, SNAP)
		d.z = snappedf(d.z, SNAP)
	for id in _starts.keys():
		var e: Dictionary = main.entries.get(id, {})
		if e.is_empty():
			continue
		(e["node"] as Node3D).position = _starts[id][0] + d
		var label: Node3D = e.get("label")
		if label != null:
			label.position = _starts[id][1] + d
	refresh()


func _finish_drag() -> void:
	_dragging = false
	_pending_id = ""
	var items := []
	for id in _starts.keys():
		var e: Dictionary = main.entries.get(id, {})
		if e.is_empty():
			continue
		var p: Vector3 = (e["node"] as Node3D).position
		if p.distance_to(_starts[id][0]) > 1e-4:
			items.append(dwsim_place(id, p, _rot(id)))
	_starts.clear()
	_send(items)


# --- the panel ---------------------------------------------------------------------------------

func build_panel(style: StyleBox) -> PanelContainer:
	panel = PanelContainer.new()
	panel.add_theme_stylebox_override("panel", style)
	panel.visible = false
	var vb := VBoxContainer.new()
	panel.add_child(vb)
	var title := Label.new()
	title.text = "Place (m)"
	title.add_theme_color_override("font_color", Color(0.11, 0.15, 0.2))
	vb.add_child(title)
	var g := GridContainer.new()
	g.columns = 2
	vb.add_child(g)
	var tl := Label.new()
	tl.text = "Tag"
	tl.add_theme_color_override("font_color", Color(0.27, 0.32, 0.38))
	g.add_child(tl)
	_tag = LineEdit.new()
	_tag.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_tag.text_submitted.connect(func(t):
		var ids := movable()
		if not ids.is_empty() and t.strip_edges() != "":
			main.link.send({"type": "rename", "id": ids[0], "tag": t.strip_edges()})
		_tag.release_focus())
	g.add_child(_tag)
	_x = _spin(g, "X", -100000.0, 100000.0, func(v): set_axis("x", v))
	_y = _spin(g, "Y", -100000.0, 100000.0, func(v): set_axis("y", v))
	_z = _spin(g, "Elevation", -50.0, 300.0, func(v): set_axis("z", v))
	var turn := HBoxContainer.new()
	vb.add_child(turn)
	_button(turn, "Turn -90", func(): shift(Vector3.ZERO, -1))
	_button(turn, "Turn +90", func(): shift(Vector3.ZERO, 1))
	_button(turn, "Follow drawing", follow_drawing)
	_multi = HBoxContainer.new()
	vb.add_child(_multi)
	_button(_multi, "Align X", func(): align("x"))
	_button(_multi, "Align Y", func(): align("y"))
	_button(_multi, "Align elevation", func(): align("z"))
	return panel


func _spin(parent: Control, text: String, lo: float, hi: float, changed: Callable) -> SpinBox:
	var l := Label.new()
	l.text = text
	l.add_theme_color_override("font_color", Color(0.27, 0.32, 0.38))
	parent.add_child(l)
	var s := SpinBox.new()
	s.min_value = lo
	s.max_value = hi
	s.step = 0.1
	s.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	s.value_changed.connect(func(v):
		if not _updating:
			changed.call(v))
	parent.add_child(s)
	return s


func _button(parent: Control, text: String, pressed: Callable) -> void:
	var b := Button.new()
	b.text = text
	b.focus_mode = Control.FOCUS_NONE
	b.pressed.connect(pressed)
	parent.add_child(b)


## Shows the place of the first movable item of the selection; hides the panel without one.
func refresh() -> void:
	if panel == null:
		return
	var ids := movable()
	panel.visible = not ids.is_empty()
	if ids.is_empty():
		return
	var p: Vector3 = (main.entries[ids[0]]["node"] as Node3D).position
	_updating = true
	_x.set_value_no_signal(snappedf(p.x, 0.01))
	_y.set_value_no_signal(snappedf(-p.z, 0.01))
	_z.set_value_no_signal(snappedf(p.y, 0.01))
	if not _tag.has_focus():
		_tag.text = str(main.entries[ids[0]]["data"].get("tag", ""))
	_updating = false
	_multi.visible = ids.size() > 1
	panel.size = Vector2.ZERO  # shrink to the rows that show
