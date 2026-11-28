## Converts a model file Godot reads at run time (FBX) into a GLB file for the model import of
## DWSIM (ModelImporter.cs), which reads glTF itself. DWSIM runs the window headless with
## --convert-model=<in> --convert-out=<out>; the window writes the file and quits with 0, or quits
## with 1 and the reason on stderr.
class_name ModelConvert
extends RefCounted


## The paths of a conversion on the command line, or an empty array when the window was not started for one.
static func requested() -> PackedStringArray:
	var src := ""
	var out := ""
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--convert-model="):
			src = arg.substr("--convert-model=".length())
		elif arg.begins_with("--convert-out="):
			out = arg.substr("--convert-out=".length())
	if src == "" or out == "":
		return PackedStringArray()
	return PackedStringArray([src, out])


## Converts the file; 0 when the GLB file was written.
static func run(src: String, out: String) -> int:
	if not FileAccess.file_exists(src):
		printerr("model convert: file not found: ", src)
		return 1
	var ext := src.get_extension().to_lower()
	var doc: GLTFDocument
	var state: GLTFState
	if ext == "fbx":
		doc = FBXDocument.new()
		state = FBXState.new()
	elif ext == "gltf" or ext == "glb":
		doc = GLTFDocument.new()
		state = GLTFState.new()
	else:
		printerr("model convert: the window does not read .", ext, " files")
		return 1
	var err := doc.append_from_file(src, state)
	if err != OK:
		printerr("model convert: could not read ", src, " (error ", err, ")")
		return 1
	# the scene keeps the meshes, materials and textures that were read; writing it again with the
	# glTF writer embeds the textures in the GLB file
	var root := doc.generate_scene(state)
	if root == null:
		printerr("model convert: the file does not have a scene")
		return 1
	var writer := GLTFDocument.new()
	writer.image_format = "PNG"
	var out_state := GLTFState.new()
	err = writer.append_from_scene(root, out_state)
	if err == OK:
		err = writer.write_to_filesystem(out_state, out)
	root.free()
	if err != OK or not FileAccess.file_exists(out):
		printerr("model convert: could not write ", out, " (error ", err, ")")
		return 1
	return 0
