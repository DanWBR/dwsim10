## A self-test of the layout editing, run with "-- --selftest-item=<id> --selftest-out=<folder>":
## once DWSIM has sent the scene, it drags the item with the mouse, turns it, raises it twice and
## undoes the last step, with events fed to the window itself (Input.parse_input_event), so it
## touches neither the real mouse nor the keyboard. It saves a picture after each step; DWSIM's
## side checks the layout it ends with.
class_name SelfTest
extends Node

var main: Node3D
var item_id := ""
var out_dir := ""


func _init(owner_main: Node3D, id: String, folder: String) -> void:
	main = owner_main
	item_id = id
	out_dir = folder


func _ready() -> void:
	DirAccess.make_dir_recursive_absolute(out_dir)
	if item_id == "build":
		_run_build()
	elif item_id.begins_with("edit:"):
		_run_edit(item_id.substr(5))
	else:
		_run()


## The editor self-test ("edit:<tag>"): selects the item with that tag and asks DWSIM for its
## editor with a double click, as the user would.
func _run_edit(tag: String) -> void:
	OS.low_processor_usage_mode = false
	var id := ""
	for i in 600:
		for k in main.entries.keys():
			if str(main.entries[k]["data"].get("tag", "")) == tag:
				id = k
		if id != "":
			break
		await _wait(0.1)
	if id == "":
		push_error("selftest: no item tagged " + tag)
		return
	await _wait(1.0)
	_key(KEY_F1)
	await _wait(0.3)
	var node: Node3D = main.entries[id]["node"]
	var at: Vector2 = main.rig.camera.unproject_position(main._node_bounds(node).get_center())
	_mouse_move(at, 0)
	_mouse_button(at, true)
	_mouse_button(at, false)
	var e := InputEventMouseButton.new()
	e.button_index = MOUSE_BUTTON_LEFT
	e.pressed = true
	e.double_click = true
	e.position = at
	e.global_position = at
	Input.parse_input_event(e)
	await _wait(3.0)
	await _shot("edit_" + tag, false)


## The building self-test ("--selftest-item=build"): add a cooler from the palette, give it a feed
## and a product from its nozzles, rename it, delete it and undo the delete.
func _run_build() -> void:
	OS.low_processor_usage_mode = false
	for i in 300:
		if not main.entries.is_empty():
			break
		await _wait(0.1)
	await _wait(1.0)
	var before: Array = main.entries.keys()

	# the palette button, then the ground 8 m past the east end of the plant
	var button: Button = main.tools.palette_panel.find_child("add_Cooler", true, false)
	if button == null:
		push_error("selftest: no Cooler in the palette")
		return
	await _click(button.get_global_rect().get_center())
	var box: AABB = main._bounds()
	# south of the plant, which the isometric view shows at the bottom, clear of the panels
	var spot := Vector3(box.get_center().x, 0.0, box.end.z + 8.0)
	var cam: Camera3D = main.rig.camera
	var at := cam.unproject_position(spot)
	_mouse_move(at, 0)
	await get_tree().process_frame
	await _click(at)
	await _wait(2.5)
	var cooler := ""
	for id in main.entries.keys():
		if not before.has(id) and str(main.entries[id]["data"].get("type", "")) == "Cooler":
			cooler = id
	if cooler == "":
		push_error("selftest: the cooler did not come")
		return
	await _shot("build_1_added", false)

	# the connect tool: outlet to the ground (a product), then inlet to the ground (a feed); the
	# selection and the card go away first, so their panels cover none of the nozzles
	_key(KEY_ESCAPE)
	_key(KEY_F1)
	_key(KEY_F)
	await _wait(0.5)
	cam = main.rig.camera
	_key(KEY_C)
	await _wait(0.5)
	for kind in ["out", "in"]:
		var port := _port(cooler, kind)
		if port == null:
			push_error("selftest: no free " + kind + " nozzle")
			return
		var p_at := cam.unproject_position(port.global_position)
		await _click(p_at)
		var away := port.global_position + Vector3(6.0 if kind == "out" else -6.0, 0, 0)
		away.y = 0.0
		var g_at := cam.unproject_position(away)
		_mouse_move(g_at, 0)
		await _click(g_at)
		await _wait(2.5)
	await _shot("build_2_connected", false)
	_key(KEY_ESCAPE)
	await _wait(0.3)

	# rename through the panel, delete with the Delete key, and undo the delete
	main._set_selection([cooler], true)
	await _wait(0.5)
	main.editor._tag.text = "E-901"
	main.editor._tag.text_submitted.emit("E-901")
	await _wait(2.0)
	main._set_selection([cooler], true)
	_key(KEY_DELETE)
	await _wait(2.0)
	_key(KEY_Z, true)
	await _wait(2.5)
	await _shot("build_3_final")
	main.link.send({"type": "select", "ids": [cooler]})


func _port(id: String, kind: String) -> Node3D:
	for mi in main.tools._ports_root.get_children():
		var p: Dictionary = mi.get_meta("port")
		if p["id"] == id and p["k"] == kind and not p["used"]:
			return mi
	return null


func _click(at: Vector2) -> void:
	_mouse_button(at, true)
	await get_tree().process_frame
	_mouse_button(at, false)
	await get_tree().process_frame


func _wait(seconds: float) -> void:
	await get_tree().create_timer(seconds).timeout


func _run() -> void:
	OS.low_processor_usage_mode = false
	for i in 300:
		if main.entries.has(item_id):
			break
		await _wait(0.1)
	if not main.entries.has(item_id):
		push_error("selftest: the item never came")
		return
	await _wait(1.0)
	# the mouse over the item, no button: the item's limits and size show
	var hn: Node3D = main.entries[item_id]["node"]
	_mouse_move(main.rig.camera.unproject_position(hn.global_position + Vector3(0, 0.5, 0)), 0)
	await _wait(0.5)
	await _shot("0_hover", false)

	# drag the item 6 m along X on the ground (the pixels for 6 m come from the camera)
	var node: Node3D = main.entries[item_id]["node"]
	var cam: Camera3D = main.rig.camera
	var from := cam.unproject_position(node.global_position + Vector3(0, 0.5, 0))
	var to := cam.unproject_position(node.global_position + Vector3(6.0, 0.5, 0))
	_mouse_button(from, true)
	for k in range(1, 9):
		_mouse_move(from.lerp(to, k / 8.0))
		await get_tree().process_frame
	_mouse_button(to, false)
	await _wait(2.0)
	await _shot("1_dragged")

	_key(KEY_T)
	await _wait(1.5)
	_key(KEY_PAGEUP)
	await _wait(1.5)
	_key(KEY_PAGEUP)
	await _wait(1.5)
	await _shot("2_turned_raised")

	_key(KEY_Z, true)
	await _wait(2.0)
	await _shot("3_undone")
	main.link.send({"type": "select", "ids": [item_id]})


func _shot(name: String, fit := true) -> void:
	var png: PackedByteArray = await main._capture("", 1400, 900, fit)
	var f := FileAccess.open(out_dir.path_join("selftest_" + name + ".png"), FileAccess.WRITE)
	if f != null:
		f.store_buffer(png)
		f.close()


func _mouse_button(at: Vector2, pressed: bool) -> void:
	var e := InputEventMouseButton.new()
	e.button_index = MOUSE_BUTTON_LEFT
	e.pressed = pressed
	e.position = at
	e.global_position = at
	e.button_mask = MOUSE_BUTTON_MASK_LEFT if pressed else 0
	Input.parse_input_event(e)


func _mouse_move(at: Vector2, mask: int = MOUSE_BUTTON_MASK_LEFT) -> void:
	var e := InputEventMouseMotion.new()
	e.position = at
	e.global_position = at
	e.button_mask = mask
	Input.parse_input_event(e)


func _key(code: Key, ctrl := false) -> void:
	for pressed in [true, false]:
		var e := InputEventKey.new()
		e.keycode = code
		e.physical_keycode = code
		e.ctrl_pressed = ctrl
		e.pressed = pressed
		Input.parse_input_event(e)
