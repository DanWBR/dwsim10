class_name SiteBake
extends Node

## "-- --bake-site [--bake-out=<folder>]": prepares the 3D view of a flowsheet for the samples. Once
## DWSIM has sent the scene, the window turns the site it draws by itself, together with the
## buildings of a plant in front of the gate (props.gd, auto_items(true)), into objects of the
## flowsheet (site.adopt). When DWSIM sends the adopted site back, the window saves a picture of
## the plant and its site in the folder (IsoSE.png, with --capture-zoom and --capture-clean if
## given), writes bake.txt (ok, or what went wrong) and quits. A flowsheet whose site was already
## adopted keeps it: the window only takes the picture.

var main: Node
var out_dir := ""


func _init(owner_main: Node, folder: String) -> void:
	main = owner_main
	out_dir = folder


func _ready() -> void:
	_run()


func _run() -> void:
	# the link up and the scene drawn
	if not await _wait(func(): return main.link != null and main.link.state == "connected" and main.scenes_loaded > 0, 90.0):
		_done("no scene from DWSIM")
		return
	await get_tree().create_timer(1.0).timeout
	if not main.site_adopted:
		main.link.send({"type": "site.adopt", "items": main.props.auto_items(true)})
		if not await _wait(func(): return main.site_adopted, 30.0):
			_done("DWSIM did not adopt the site")
			return
	# let the site be built and the lamps and trees settle before the picture
	await get_tree().create_timer(3.0).timeout
	if out_dir != "":
		DirAccess.make_dir_recursive_absolute(out_dir)
		var png: PackedByteArray = await main._capture("IsoSE", 1600, 1000)
		var f := FileAccess.open(out_dir.path_join("IsoSE.png"), FileAccess.WRITE)
		if f != null:
			f.store_buffer(png)
			f.close()
	_done("ok")


## Waits until the test is true, for at most the given seconds.
func _wait(test: Callable, seconds: float) -> bool:
	var t := 0.0
	while not test.call():
		if t > seconds:
			return false
		await get_tree().create_timer(0.25).timeout
		t += 0.25
	return true


func _done(status: String) -> void:
	if out_dir != "":
		DirAccess.make_dir_recursive_absolute(out_dir)
		var f := FileAccess.open(out_dir.path_join("bake.txt"), FileAccess.WRITE)
		if f != null:
			f.store_string(status + "\n")
			f.close()
	get_tree().quit(0 if status == "ok" else 1)
