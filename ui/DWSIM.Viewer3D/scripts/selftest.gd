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
	_run()


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
	await _shot("0_start")

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


func _shot(name: String) -> void:
	var png: PackedByteArray = await main._capture("", 1400, 900)
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


func _mouse_move(at: Vector2) -> void:
	var e := InputEventMouseMotion.new()
	e.position = at
	e.global_position = at
	e.button_mask = MOUSE_BUTTON_MASK_LEFT
	Input.parse_input_event(e)


func _key(code: Key, ctrl := false) -> void:
	for pressed in [true, false]:
		var e := InputEventKey.new()
		e.keycode = code
		e.physical_keycode = code
		e.ctrl_pressed = ctrl
		e.pressed = pressed
		Input.parse_input_event(e)
