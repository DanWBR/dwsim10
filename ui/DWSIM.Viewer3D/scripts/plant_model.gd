## The 3D model of the plant that DWSIM imported (ModelImporter.cs), drawn as scenery under the
## plant DWSIM builds. The scene names the model's view file in DWSIM's cache, cut in tiles of one
## mesh per material; the window reads it at run time in a thread, so a plant of a million
## triangles does not stop the window while it loads. The panel rows import a model, take it
## away and show or hide the model and the equipment.
extends Node3D

signal loaded

## The view file shown now, and the one the scene asks for.
var glb := ""
var _wanted := ""
var loading := false
## What the scene says of the model: name, sha, glb, found.
var info := {}
var status := ""
var show_model := true
var show_equipment := true
var main: Node
var _thread: Thread
var _root: Node3D
var _remove_button: Button
var _match_button: Button
var _review_button: Button
var _model_box: CheckBox
var _equipment_box: CheckBox
var _dialog: FileDialog

const FILTERS := ["*.dwf, *.dwfx, *.ifc, *.step, *.stp, *.igs, *.iges, *.glb, *.gltf, *.fbx, *.obj, *.stl, *.ply, *.3dm, *.fcstd ; Plant models",
	"*.dwf, *.dwfx ; 3D DWF (Navisworks, AutoCAD)", "*.ifc ; IFC", "*.step, *.stp ; STEP (through SolidCraft)", "*.igs, *.iges ; IGES (through SolidCraft)",
	"*.glb, *.gltf ; glTF", "*.fbx ; FBX", "*.obj ; OBJ", "*.stl ; STL", "*.ply ; PLY", "*.3dm ; Rhino (through SolidCraft)", "*.fcstd ; FreeCAD (through SolidCraft)"]


func has_model() -> bool:
	return _root != null


## The model the scene names (or null): loads it when it is another file, takes it away when there is none.
func set_info(i) -> void:
	info = i if i is Dictionary else {}
	var g = info.get("glb")
	var want: String = str(g) if g != null else ""
	_wanted = want
	_refresh_rows()
	if want == glb and not loading:
		return
	if want == "":
		if not loading:
			_clear()
			glb = ""
			loaded.emit()
		return
	if not loading:
		_start(want)


func _start(path: String) -> void:
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
		if _wanted != "":
			_start(_wanted)
		else:
			_clear()
			glb = ""
			loaded.emit()
		return
	_clear()
	glb = path
	if node == null:
		status = "The 3D window could not read the plant model's view file."
	else:
		_root = node as Node3D
		add_child(node)
	visible = show_model
	_refresh_rows()
	loaded.emit()


func _clear() -> void:
	if _root != null:
		_root.queue_free()
		_root = null


## The meshes of the model, for the bounds of the view.
func meshes() -> Array:
	if _root == null or not visible:
		return []
	return _root.find_children("*", "MeshInstance3D", true, false)


func _exit_tree() -> void:
	if _thread != null:
		_thread.wait_to_finish()


# --- the rows in the environment panel ---------------------------------------------------------

func add_rows(g: GridContainer, owner_main: Node) -> void:
	main = owner_main
	_label(g, "Plant model")
	var row := HBoxContainer.new()
	row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	g.add_child(row)
	var imp := Button.new()
	imp.text = "Import..."
	imp.focus_mode = Control.FOCUS_NONE
	imp.tooltip_text = "Imports a 3D model of the plant (3D DWF, glTF, FBX, OBJ, STL, PLY) and shows it round the equipment. The model stays in its own file; the flowsheet keeps a reference to it."
	imp.pressed.connect(_ask_file)
	row.add_child(imp)
	_remove_button = Button.new()
	_remove_button.text = "Remove"
	_remove_button.focus_mode = Control.FOCUS_NONE
	_remove_button.tooltip_text = "The flowsheet stops using the plant model (Ctrl+Z brings it back)."
	_remove_button.pressed.connect(func():
		if main.link != null:
			main.link.send({"type": "model.remove"}))
	row.add_child(_remove_button)
	_match_button = Button.new()
	_match_button.text = "Match"
	_match_button.focus_mode = Control.FOCUS_NONE
	_match_button.tooltip_text = "Proposes again which flowsheet object each equipment of the model stands for (after the flowsheet changed). The choices already made stay."
	_match_button.pressed.connect(func():
		if main.link != null:
			main.link.send({"type": "model.suggest"}))
	row.add_child(_match_button)
	_review_button = Button.new()
	_review_button.text = "Review"
	_review_button.focus_mode = Control.FOCUS_NONE
	_review_button.tooltip_text = "Shows the model's groups coloured by what they are, with the equipment list and the tools to choose, join, split and create flowsheet objects."
	_review_button.pressed.connect(func(): main.model_review.toggle())
	row.add_child(_review_button)
	_label(g, "Show")
	var srow := HBoxContainer.new()
	srow.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	g.add_child(srow)
	_model_box = _check("model", show_model, "Shows the imported plant model. The equipment it stands for shows as the model; without it, as DWSIM draws it.", func(on):
		show_model = on
		visible = on
		main.apply_model_replacement()
		if main.props != null:
			main.props.refresh()
		main.refit_ground())
	srow.add_child(_model_box)
	_equipment_box = _check("equipment", show_equipment, "Shows the equipment and the lines DWSIM builds from the flowsheet.", func(on):
		show_equipment = on
		main.set_equipment_visible(on))
	srow.add_child(_equipment_box)
	_refresh_rows()


func _refresh_rows() -> void:
	if _remove_button == null:
		return
	var any := not info.is_empty()
	_remove_button.disabled = not any
	_match_button.disabled = not any
	_review_button.disabled = not any
	_model_box.disabled = not any


func _check(text: String, on: bool, tip: String, toggled: Callable) -> CheckBox:
	var c := CheckBox.new()
	c.text = text
	c.button_pressed = on
	c.focus_mode = Control.FOCUS_NONE
	c.tooltip_text = tip
	c.add_theme_color_override("font_color", Color(0.11, 0.15, 0.2))
	c.toggled.connect(toggled)
	return c


func _label(parent: Control, text: String) -> void:
	var l := Label.new()
	l.text = text
	l.add_theme_color_override("font_color", Color(0.27, 0.32, 0.38))
	parent.add_child(l)


func _ask_file() -> void:
	if main.link == null or main.link.state != "connected":
		main._show_toast("Open the 3D window from DWSIM to import a plant model.")
		return
	if _dialog == null:
		_dialog = FileDialog.new()
		_dialog.use_native_dialog = true
		_dialog.access = FileDialog.ACCESS_FILESYSTEM
		_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILE
		_dialog.title = "Import a 3D model of the plant"
		_dialog.filters = PackedStringArray(FILTERS)
		_dialog.file_selected.connect(func(path: String):
			main.link.send({"type": "model.import", "path": path}))
		add_child(_dialog)
	_dialog.popup_centered(Vector2i(900, 600))


## What the matching found, for the status line: "61 equipment, 57 proposed".
func counts() -> String:
	if info.is_empty() or int(info.get("equipment", 0)) == 0:
		return ""
	var t := "%d equipment, %d proposed" % [int(info.get("equipment", 0)), int(info.get("proposed", 0))]
	if int(info.get("chosen", 0)) > 0:
		t += ", %d chosen" % int(info.get("chosen", 0))
	return t


## A message of DWSIM about an import: the status line says how it goes.
func on_status(m: Dictionary) -> void:
	var state := str(m.get("state", ""))
	status = str(m.get("message", ""))
	if state == "failed":
		main._show_toast(status)
	main._update_title()
