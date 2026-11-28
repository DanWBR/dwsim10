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
	elif item_id == "site":
		_run_site()
	elif item_id.begins_with("edit:"):
		_run_edit(item_id.substr(5))
	elif item_id.begins_with("review:"):
		_run_review(item_id.substr(7))
	else:
		_run()


## The review self-test ("review:<tag>"): in the review of the plant model, frames the group
## with that tag, clicks its largest island, then Alt+clicks it, as the user would, and writes what
## each click picked to review.txt, with a picture after each.
func _run_review(tag: String) -> void:
	OS.low_processor_usage_mode = false
	var review = main.model_review
	var gid := ""
	for i in 600:
		if not review.loading and not review.groups.is_empty():
			for k in review.groups.keys():
				if str(review.groups[k].get("tag", "")) == tag:
					gid = k
		if gid != "":
			break
		await _wait(0.1)
	var log := []
	if gid == "":
		log.append("no group tagged " + tag)
	else:
		if not review.active:
			review.enter()
		review.select([])
		review.frame(gid)
		await _wait(1.0)
		var best := -1
		var area := 0.0
		for i in review.groups[gid].get("islands", []):
			if review.nodes.has(int(i)):
				var b: AABB = review.nodes[int(i)].get_aabb()
				var a := b.size.x * b.size.y + b.size.y * b.size.z + b.size.x * b.size.z
				if a > area:
					area = a
					best = int(i)
		var mi: MeshInstance3D = review.nodes[best]
		var at: Vector2 = main.rig.camera.unproject_position((mi.global_transform * mi.get_aabb()).get_center())
		var under: int = review.pick(at)
		log.append("target %s island %d, the pick under its centre: island %d of group %s" % [gid, best, under, review.island_group.get(under, "-")])
		await _click(at)
		await _wait(0.5)
		log.append("click selects: %s" % str(review.selected_groups))
		await _shot("review_click", false)
		var e := InputEventMouseButton.new()
		e.button_index = MOUSE_BUTTON_LEFT
		e.alt_pressed = true
		e.position = at
		e.global_position = at
		e.pressed = true
		e.button_mask = MOUSE_BUTTON_MASK_LEFT
		Input.parse_input_event(e)
		await get_tree().process_frame
		var r := e.duplicate()
		r.pressed = false
		r.button_mask = 0
		Input.parse_input_event(r)
		await _wait(0.5)
		log.append("alt+click picks islands: %s of group %s" % [str(review.selected_islands), str(review.selected_groups)])
		await _shot("review_alt", false)
	var f := FileAccess.open(out_dir.path_join("review.txt"), FileAccess.WRITE)
	if f != null:
		f.store_string("\n".join(log) + "\n")
		f.close()
	get_tree().quit()


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


## The build bar self-test ("site"): opens the bar (the window's own site becomes objects), picks
## categories and objects with clicks, builds two trees (the second turned with R), a car and an
## administration building, checks that the ghost turns red over the equipment, deletes a tree with
## Delete and undoes it, draws a road, gravel, a parking lot and a fence, tries a road across the
## equipment, demolishes a car and the drums with the bulldozer, and writes what the site holds to
## selftest_site.json.
func _run_site() -> void:
	OS.low_processor_usage_mode = false
	for i in 600:
		if not main.entries.is_empty():
			break
		await _wait(0.1)
	await _wait(1.0)
	_key(KEY_ESCAPE)
	_key(KEY_F1)
	await _shot("site_0_before")
	_key(KEY_U)
	for i in 100:
		if main.site_adopted:
			break
		await _wait(0.1)
	await _wait(1.0)
	await _shot("site_1_adopted")
	var cam: Camera3D = main.rig.camera
	var box: AABB = main._bounds()
	var y := box.position.y
	var c := box.get_center()
	var sb: SiteBuilder = main.site_builder

	await _click((sb._cat_buttons["Nature"] as Button).get_global_rect().get_center())
	await _wait(3.0)
	await _shot("site_2_nature", false)
	await _click((sb._grid.get_node("site_tree") as Button).get_global_rect().get_center())
	await _wait(0.3)
	var spot := Vector3(c.x - 8.0, y, box.end.z + 10.0)
	_mouse_move(cam.unproject_position(spot), 0)
	await _wait(0.5)
	await _shot("site_3_ghost", false)
	await _click(cam.unproject_position(spot))
	await _wait(1.5)
	_key(KEY_R)
	var spot2 := Vector3(c.x - 14.0, y, box.end.z + 10.0)
	_mouse_move(cam.unproject_position(spot2), 0)
	await _wait(0.3)
	await _click(cam.unproject_position(spot2))
	await _wait(1.5)
	# over the equipment the ghost turns red and the click builds nothing
	_mouse_move(cam.unproject_position(c), 0)
	await _wait(0.3)
	var red: bool = not sb._ok
	await _shot("site_4_red", false)
	await _click(cam.unproject_position(c))
	await _wait(1.0)
	_key(KEY_ESCAPE)

	await _click((sb._cat_buttons["Vehicles"] as Button).get_global_rect().get_center())
	await _wait(2.0)
	await _click((sb._grid.get_node("site_suv") as Button).get_global_rect().get_center())
	var spot3 := Vector3(c.x - 20.0, y, box.end.z + 10.0)
	_mouse_move(cam.unproject_position(spot3), 0)
	await _wait(0.3)
	await _click(cam.unproject_position(spot3))
	await _wait(1.5)
	_key(KEY_ESCAPE)
	await _click((sb._cat_buttons["Buildings"] as Button).get_global_rect().get_center())
	await _wait(3.0)
	await _shot("site_5_buildings", false)
	await _click((sb._grid.get_node("site_admin") as Button).get_global_rect().get_center())
	var spot4 := Vector3(box.end.x + 30.0, y, c.z)
	_mouse_move(cam.unproject_position(spot4), 0)
	await _wait(0.3)
	await _click(cam.unproject_position(spot4))
	_mouse_move(Vector2(700, 20), 0)
	# the building rises out of a cloud of dust
	for i in 40:
		if main.effects.events.any(func(e): return str(e).begins_with("built site:admin")):
			break
		await get_tree().process_frame
	await _close_up(spot4, 45.0, 0.25, "site_6a_dust")
	await _wait(1.5)
	_key(KEY_ESCAPE)
	await _wait(0.5)
	await _shot("site_6_built")

	# lines and areas: a road dragged in one run, a fence clicked point by point and finished with
	# Enter, gravel dragged as a rectangle, a parking lot clicked corner by corner and finished with a
	# double click, and a road across the equipment, which is refused
	await _click((sb._cat_buttons["Roads and paving"] as Button).get_global_rect().get_center())
	await _wait(3.0)
	await _click((sb._grid.get_node("site_road") as Button).get_global_rect().get_center())
	var r0 := Vector3(box.position.x - 25.0, y, box.end.z + 25.0)
	var r1 := Vector3(box.end.x + 25.0, y, box.end.z + 25.0)
	await _drag(cam.unproject_position(r0), cam.unproject_position(r1))
	await _wait(1.5)
	await _drag(cam.unproject_position(Vector3(box.position.x - 5.0, y, c.z)), cam.unproject_position(Vector3(box.end.x + 5.0, y, c.z)))
	await _wait(1.0)
	await _click((sb._grid.get_node("site_gravel") as Button).get_global_rect().get_center())
	await _drag(cam.unproject_position(Vector3(box.position.x - 30.0, y, c.z - 10.0)), cam.unproject_position(Vector3(box.position.x - 20.0, y, c.z + 5.0)))
	await _wait(1.5)
	await _click((sb._grid.get_node("site_parking") as Button).get_global_rect().get_center())
	var lot := [Vector3(c.x + 5.0, y, box.end.z + 30.0), Vector3(c.x + 25.0, y, box.end.z + 30.0),
		Vector3(c.x + 25.0, y, box.end.z + 40.0), Vector3(c.x + 5.0, y, box.end.z + 40.0)]
	for i in 3:
		_mouse_move(cam.unproject_position(lot[i]), 0)
		await _click(cam.unproject_position(lot[i]))
		await _wait(0.2)
	_mouse_move(cam.unproject_position(lot[3]), 0)
	await _wait(0.3)
	await _shot("site_8_drawing", false)
	_double_click(cam.unproject_position(lot[3]))
	await _wait(1.5)
	await _click((sb._cat_buttons["Fences and barriers"] as Button).get_global_rect().get_center())
	await _wait(3.0)
	await _click((sb._grid.get_node("site_fence") as Button).get_global_rect().get_center())
	for q in [Vector3(box.end.x + 25.0, y, c.z - 10.0), Vector3(box.end.x + 25.0, y, c.z + 10.0), Vector3(box.end.x + 35.0, y, c.z + 10.0)]:
		_mouse_move(cam.unproject_position(q), 0)
		await _click(cam.unproject_position(q))
		await _wait(0.2)
	_key(KEY_ENTER)
	await _wait(1.5)
	_key(KEY_ESCAPE)
	_key(KEY_ESCAPE)
	await _wait(0.3)
	await _shot("site_9_drawn")
	await _click(sb._budget_button.get_global_rect().get_center())
	await _wait(0.5)
	await _shot("site_9b_budget")
	await _click(sb._budget_button.get_global_rect().get_center())

	# the bulldozer: a click on the car built above, and a rectangle over the drums of the storage corner
	await _click(sb._demolish_button.get_global_rect().get_center())
	await _wait(0.3)
	await _click(cam.unproject_position(spot3 + Vector3(0, 0.8, 0)))
	for i in 40:
		if main.effects.events.any(func(e): return str(e).begins_with("demolished site:suv")):
			break
		await get_tree().process_frame
	await _close_up(spot3, 22.0, 0.35, "site_10a_rubble")
	await _wait(1.5)
	var drums := AABB()
	var first := true
	for id in main.entries.keys():
		if str(main.entries[id]["data"].get("site", "")) == "barrel":
			var at: Vector3 = (main.entries[id]["node"] as Node3D).position
			drums = AABB(at, Vector3.ZERO) if first else drums.expand(at)
			first = false
	drums = drums.grow(0.6)
	await _drag(cam.unproject_position(Vector3(drums.position.x, y, drums.position.z)), cam.unproject_position(Vector3(drums.end.x, y, drums.end.z)))
	await _wait(1.5)
	_key(KEY_ESCAPE)
	await _shot("site_10_demolished")
	var shapes := {}
	for id in main.entries.keys():
		var d: Dictionary = main.entries[id]["data"]
		var k := str(d.get("site", ""))
		if k in ["road", "fence", "gravel", "parking"]:
			if not shapes.has(k):
				shapes[k] = []
			shapes[k].append((d.get("points", []) as Array).size())

	# the terrain: a hill in the woods to the east with a tree on top, a pond in the woods to the
	# west, the hill smoothed, and a try at the middle of the plant, which stays flat
	await _click((sb._cat_buttons["Terrain"] as Button).get_global_rect().get_center())
	await _wait(0.5)
	await _click((sb._grid.get_node("size_large") as Button).get_global_rect().get_center())
	await _wait(0.3)
	await _click((sb._grid.get_node("terrain_raise") as Button).get_global_rect().get_center())
	var hill := Vector3(box.end.x + 40.0, y, c.z + 30.0)
	await _hold(cam.unproject_position(hill), 2.0)
	await _wait(1.5)
	await _click((sb._grid.get_node("terrain_smooth") as Button).get_global_rect().get_center())
	await _hold(cam.unproject_position(hill + Vector3(0, main.terrain.height_at(hill.x, hill.z), 0)), 0.6)
	await _wait(1.0)
	await _click((sb._grid.get_node("terrain_raise") as Button).get_global_rect().get_center())
	await _hold(cam.unproject_position(c), 0.8)
	await _wait(1.0)
	await _click((sb._grid.get_node("terrain_pond") as Button).get_global_rect().get_center())
	var pond := Vector3(box.position.x - 40.0, y, c.z + 20.0)
	await _hold(cam.unproject_position(pond), 2.0)
	await _wait(1.5)
	_key(KEY_ESCAPE)
	await _click((sb._cat_buttons["Nature"] as Button).get_global_rect().get_center())
	await _wait(2.0)
	await _click((sb._grid.get_node("site_tree") as Button).get_global_rect().get_center())
	var top := hill + Vector3(0, main.terrain.height_at(hill.x, hill.z), 0)
	_mouse_move(cam.unproject_position(top), 0)
	await _wait(0.3)
	await _click(cam.unproject_position(top))
	await _wait(2.0)
	_key(KEY_ESCAPE)
	var tree_y := -1.0
	for id in main.entries.keys():
		var n: Node3D = main.entries[id]["node"]
		if str(main.entries[id]["data"].get("site", "")) == "tree" and Vector2(n.position.x, n.position.z).distance_to(Vector2(hill.x, hill.z)) < 3.0:
			tree_y = n.position.y - y
	var terrain_summary := {"hill": main.terrain.height_at(hill.x, hill.z), "middle": main.terrain.height_at(c.x, c.z),
		"pond_depth": 0.0, "tree_on_hill": tree_y}
	var t: SiteTerrain = main.terrain
	var pi := int((pond.x - t.x0) / t.CELL)
	var pj := int((-pond.z - t.y0) / t.CELL)
	if t.has_grid():
		terrain_summary["pond_depth"] = t.w[pj * t.nx + pi]
	sb.close()
	main.hud.visible = false
	var keep: float = main.rig.view_size
	main.rig.view_size *= 2.2
	main.rig.apply()
	await _wait(1.0)
	await _shot("site_12_terrain", false)
	main.rig.view_size = keep
	main.rig.apply()
	main.hud.visible = true
	sb.open()

	# Delete takes a tree away, Ctrl+Z brings it back
	var tree := ""
	for id in main.entries.keys():
		if str(main.entries[id]["data"].get("site", "")) == "tree":
			tree = id
	main._set_selection([tree], true)
	_key(KEY_DELETE)
	await _wait(2.0)
	var after_delete: bool = main.entries.has(tree)
	_key(KEY_Z, true)
	await _wait(2.0)
	var counts := {}
	for id in main.entries.keys():
		var k := str(main.entries[id]["data"].get("site", ""))
		if k != "":
			counts[k] = int(counts.get(k, 0)) + 1
	var rots := []
	for id in main.entries.keys():
		if str(main.entries[id]["data"].get("site", "")) == "tree":
			rots.append(int(main.entries[id]["data"].get("rot", 0)))
	var f := FileAccess.open(out_dir.path_join("selftest_site.json"), FileAccess.WRITE)
	f.store_string(JSON.stringify({"adopted": main.site_adopted, "counts": counts, "red_over_equipment": red,
		"deleted": not after_delete, "back_after_undo": main.entries.has(tree), "tree_rots": rots, "shapes": shapes,
		"cash": sb._cash.text, "budget_total": float(sb._budget.get("total", -1.0)), "terrain": terrain_summary,
		"effects": main.effects.events}))
	f.close()
	await _shot("site_7_done")
	# the whole site from further away, without the panels
	sb.close()
	main.hud.visible = false
	main.rig.view_size *= 2.0
	main.rig.apply()
	await _wait(1.0)
	await _shot("site_11_overview", false)
	main.hud.visible = true


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


## A picture of a place from close by, without the panels, a moment after something happened there.
func _close_up(at: Vector3, size: float, after: float, name: String) -> void:
	var rig: CameraRig = main.rig
	var keep_target := rig.target
	var keep_size := rig.view_size
	main.hud.visible = false
	rig.target = at
	rig.view_size = size
	rig.apply()
	await _wait(after)
	await _shot(name, false)
	rig.target = keep_target
	rig.view_size = keep_size
	rig.apply()
	main.hud.visible = true


## Presses the left button at one place, moves with it held and lets go at another.
func _drag(from: Vector2, to: Vector2) -> void:
	_mouse_move(from, 0)
	await get_tree().process_frame
	_mouse_button(from, true)
	await get_tree().process_frame
	for i in range(1, 11):
		_mouse_move(from.lerp(to, i / 10.0))
		await get_tree().process_frame
	_mouse_button(to, false)
	await get_tree().process_frame


## Presses the left button at a place and holds it for a while (a brush of the terrain).
func _hold(at: Vector2, seconds: float) -> void:
	_mouse_move(at, 0)
	await get_tree().process_frame
	_mouse_button(at, true)
	await _wait(seconds)
	_mouse_button(at, false)
	await get_tree().process_frame


func _double_click(at: Vector2) -> void:
	var e := InputEventMouseButton.new()
	e.button_index = MOUSE_BUTTON_LEFT
	e.pressed = true
	e.double_click = true
	e.position = at
	e.global_position = at
	e.button_mask = MOUSE_BUTTON_MASK_LEFT
	Input.parse_input_event(e)
	_mouse_button(at, false)


func _key(code: Key, ctrl := false) -> void:
	for pressed in [true, false]:
		var e := InputEventKey.new()
		e.keycode = code
		e.physical_keycode = code
		e.ctrl_pressed = ctrl
		e.pressed = pressed
		Input.parse_input_event(e)
