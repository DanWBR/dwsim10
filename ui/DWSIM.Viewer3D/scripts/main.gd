## DWSIM 3D: shows the plant model of a DWSIM flowsheet and keeps it in step with DWSIM.
##
## Started by DWSIM with "-- --dwsim-port=N --dwsim-token=T", it connects back and draws the scene
## DWSIM sends. Started with "-- --dwsim-scene=file.json" it draws a saved scene on its own, and
## with "--capture-dir=folder --views=IsoSE,Plan" it saves those views as PNG files and quits.
##
## Mouse: left click selects (Ctrl adds), double click frames the item, right drag turns the view,
## middle drag (or Shift + right drag) moves it, the wheel zooms. Keys: see SHORTCUTS.
extends Node3D

## The width of the panels at the sides; the Rounds panel at the bottom is wider.
const PANEL_W := 280.0
const FONT_SIZE := 12

const SHORTCUTS := [
	["1-4", "Isometric views"],
	["5", "Plan"],
	["6 / 7", "Front / right side"],
	["Q / E", "Turn 90 degrees"],
	["F", "Fit the plant"],
	["L", "All labels (else the selected ones)"],
	["I", "Insulation"],
	["R", "Pipe rack"],
	["G", "Grid"],
	["P", "Equipment palette"],
	["C", "Connect nozzles"],
	["Del", "Delete the selection"],
	["Drag", "Move on a 0.5 m grid (Alt: free)"],
	["Arrows", "Nudge 0.5 m (Shift: 0.1 m)"],
	["PgUp / PgDn", "Raise / lower 0.5 m"],
	["T / Shift+T", "Turn the selection"],
	["Ctrl+Z / Y", "Undo / redo"],
	["Double click", "Open the editor (Enter too)"],
	["Shift+F", "Frame the selection"],
	["A", "Animation on / off"],
	["V", "Values on the plant"],
	["B", "Flyby of the plant (Esc stops)"],
	["O", "The site around the plant"],
	["U", "Build bar of the site"],
	["K", "Rounds and maintenance log"],
	["N", "Noon / night"],
	["W", "Next weather"],
	["M", "Sound on / off"],
	["Esc", "Clear the selection"],
	["F1", "This card"],
]
const KEY_VIEWS := {KEY_1: "IsoSE", KEY_2: "IsoSW", KEY_3: "IsoNW", KEY_4: "IsoNE", KEY_5: "Plan", KEY_6: "Front", KEY_7: "Right"}

var link: DwsimLink
var rig: CameraRig
var builder := SceneBuilder.new()
var editor: LayoutEditor
var tools: BuildTools
var site_builder: SiteBuilder
## The look and sound of building and demolishing (effects.gd).
var effects: SiteEffects
## Whether the site is made of objects the user can change (DWSIM's scene says it).
var site_adopted := false
var site_root: Node3D
## The relief of the ground (terrain.gd).
var terrain: SiteTerrain
## The heights the window last sent to DWSIM, so their echo in the scene is not loaded again.
var _terrain_sent := ""
var title_panel: PanelContainer
var _pending_select := ""
var animate := true
var surroundings: PlantEnvironment
var life: PlantLife
var alarms := {}
var errors := {}
var _alarm_mat: StandardMaterial3D
var _error_mat: StandardMaterial3D
var values_root: Node3D
var flyby: Flyby
var props: PlantProps
var crew: PlantCrew
var maintenance: PlantMaintenance
var flyby_record := false
## Seconds per unit given on the command line (a recording); 0 takes the panel's setting.
var flyby_hold := 0.0
var _flyby_asked := false
var _hud_before := {}
var world_env: Environment

var world: Node3D
var items_root: Node3D
var lines_root: Node3D
var insulation_root: Node3D
var rack_root: Node3D
var labels_root: Node3D
var ground: MeshInstance3D
var grid: MeshInstance3D
var sun: DirectionalLight3D

## id -> {node, data, kind ("item" or "line"), label}
var entries := {}
var scene_name := ""
## How many full scenes the window has drawn (a flowsheet of streams alone has no items, but has a scene).
var scenes_loaded := 0
var warnings: Array = []
var selected: Array = []

var hud: CanvasLayer
var title_label: Label
var status_label: Label
var info_panel: PanelContainer
var info_text: RichTextLabel
var card: PanelContainer
var toast: Label
var _toast_time := 0.0

var _highlight: StandardMaterial3D
var _hover_mat: StandardMaterial3D
var _hover_id := ""
var _hover_box: MeshInstance3D
var _hover_tip: Label
var _mouse := Vector2(-1, -1)
var _mouse_dirty := false
var _drag_button := MOUSE_BUTTON_NONE
var _drag_from := Vector2.ZERO
var _press_at := Vector2.ZERO
var _fitted := false


func _ready() -> void:
	get_window().title = "DWSIM 3D"
	_build_world()
	terrain = SiteTerrain.new(self)
	world.add_child(terrain)
	SiteObjects.terrain = terrain
	_build_hud()
	editor = LayoutEditor.new(self)
	add_child(editor)
	var edit_panel := editor.build_panel(_panel_style())
	_glass(edit_panel)
	hud.add_child(edit_panel)
	flyby = Flyby.new(self)
	add_child(flyby)
	flyby.build_card(hud)
	flyby.finished.connect(_on_flyby_finished)
	for arg in OS.get_cmdline_user_args():
		if arg == "--flyby-record":
			flyby_record = true
		elif arg.begins_with("--flyby-hold="):
			flyby_hold = float(arg.get_slice("=", 1))
	if flyby_record:
		get_window().title = "DWSIM 3D - recording a flyby"
	else:
		# the project starts at 1920 x 1080, the size of the flyby videos (Movie Maker records at the
		# size the window starts with); the window people work in opens smaller
		var screen := DisplayServer.screen_get_usable_rect(get_window().current_screen)
		var size := Vector2i(mini(1400, screen.size.x - 80), mini(900, screen.size.y - 80))
		get_window().size = size
		get_window().position = screen.position + (screen.size - size) / 2
	surroundings = PlantEnvironment.new(self, world_env, sun)
	add_child(surroundings)
	var env_panel := surroundings.build_panel(_panel_style())
	_glass(env_panel)
	env_panel.name = "environment_panel"
	hud.add_child(env_panel)
	crew = PlantCrew.new(self)
	add_child(crew)
	maintenance = PlantMaintenance.new(self)
	add_child(maintenance)
	var rounds := crew.build_panel(_panel_style())
	_glass(rounds, PANEL_W * 1.6)
	hud.add_child(rounds)
	tools = BuildTools.new(self)
	add_child(tools)
	var palette := tools.build_panel(_panel_style())
	_glass(palette)
	hud.add_child(palette)
	site_builder = SiteBuilder.new(self)
	add_child(site_builder)
	effects = SiteEffects.new(self)
	add_child(effects)
	var site_bar := site_builder.build_bar(_panel_style())
	_glass(site_bar, 0.0)
	hud.add_child(site_bar)
	_glass(site_builder.flyout, 0.0)
	hud.add_child(site_builder.flyout)
	_glass(site_builder.budget_panel)
	hud.add_child(site_builder.budget_panel)
	rig = CameraRig.new()
	add_child(rig)
	link = DwsimLink.new()
	add_child(link)
	link.message_received.connect(_on_message)
	link.state_changed.connect(_on_state)
	_highlight = StandardMaterial3D.new()
	_highlight.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_highlight.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_highlight.albedo_color = Color(0.24, 0.5, 0.84, 0.38)
	_highlight.no_depth_test = false
	_hover_mat = StandardMaterial3D.new()
	_hover_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_hover_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_hover_mat.albedo_color = Color(1.0, 0.78, 0.25, 0.22)
	_alarm_mat = StandardMaterial3D.new()
	_alarm_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_alarm_mat.albedo_color = Color(1.0, 0.12, 0.08, 0.55)
	# an alarm shines, and blooms
	_alarm_mat.emission_enabled = true
	_alarm_mat.emission = Color(1.0, 0.1, 0.06)
	_alarm_mat.emission_energy_multiplier = 3.0
	_error_mat = StandardMaterial3D.new()
	_error_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_error_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_error_mat.albedo_color = Color(1.0, 0.55, 0.05, 0.3)
	life = PlantLife.new(self)
	add_child(life)
	props = PlantProps.new(self)
	add_child(props)
	_hover_box = MeshInstance3D.new()
	var box_mat := StandardMaterial3D.new()
	box_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	box_mat.no_depth_test = true
	box_mat.albedo_color = Color(0.95, 0.62, 0.1)
	box_mat.render_priority = 5
	_hover_box.material_override = box_mat
	_hover_box.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	_hover_box.visible = false
	add_child(_hover_box)
	_run_offline()
	var test_item := ""
	var test_out := ""
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--selftest-item="):
			test_item = arg.get_slice("=", 1)
		elif arg.begins_with("--selftest-out="):
			test_out = arg.get_slice("=", 1)
	# the same, asked through the environment, for a window that DWSIM starts itself
	if test_item == "":
		test_item = OS.get_environment("DWSIM_VIEWER3D_SELFTEST")
		test_out = OS.get_environment("DWSIM_VIEWER3D_SELFTEST_OUT")
	if test_item != "" and test_out != "":
		add_child(SelfTest.new(self, test_item, test_out))
	# "--bake-site": the 3D view of a sample, made once and saved with it (see SiteBake)
	if "--bake-site" in OS.get_cmdline_user_args():
		var bake_out := ""
		for arg in OS.get_cmdline_user_args():
			if arg.begins_with("--bake-out="):
				bake_out = arg.get_slice("=", 1)
		add_child(SiteBake.new(self, bake_out))


# --- world -----------------------------------------------------------------------------------

func _build_world() -> void:
	var env := Environment.new()
	var sky_mat := ProceduralSkyMaterial.new()
	sky_mat.sky_top_color = Color(0.36, 0.55, 0.80)
	sky_mat.sky_horizon_color = Color(0.74, 0.80, 0.86)
	sky_mat.ground_horizon_color = Color(0.74, 0.80, 0.86)
	sky_mat.ground_bottom_color = Color(0.45, 0.47, 0.48)
	var sky := Sky.new()
	sky.sky_material = sky_mat
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.ambient_light_energy = 0.8
	env.reflected_light_source = Environment.REFLECTION_SOURCE_SKY
	env.tonemap_mode = Environment.TONE_MAPPER_ACES
	env.ssao_enabled = true
	env.ssao_radius = 1.5
	env.ssao_intensity = 1.6
	# bloom: only what is brighter than the lit scene (lamps, flames, the arc, alarms) glows; the
	# scene is drawn in HDR and the glow takes the light above the threshold before tone mapping
	env.glow_enabled = true
	env.glow_intensity = 0.9
	env.glow_strength = 1.0
	env.glow_bloom = 0.0
	env.glow_blend_mode = Environment.GLOW_BLEND_MODE_SCREEN
	env.glow_hdr_threshold = 1.3
	env.glow_hdr_scale = 2.0
	env.glow_hdr_luminance_cap = 16.0
	for lv in 7:
		env.set_glow_level(lv, [0.0, 1.0, 1.0, 0.9, 0.7, 0.4, 0.0][lv])
	var we := WorldEnvironment.new()
	we.environment = env
	world_env = env
	add_child(we)

	sun = DirectionalLight3D.new()
	sun.light_energy = 1.25
	sun.shadow_enabled = true
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_PARALLEL_4_SPLITS
	sun.directional_shadow_max_distance = 600.0
	sun.rotation_degrees = Vector3(-50.0, -35.0, 0.0)
	add_child(sun)

	world = Node3D.new()
	add_child(world)
	for n in ["items", "site", "lines", "insulation", "rack", "labels", "values"]:
		var node := Node3D.new()
		node.name = n
		world.add_child(node)
	items_root = world.get_node("items")
	site_root = world.get_node("site")
	lines_root = world.get_node("lines")
	insulation_root = world.get_node("insulation")
	rack_root = world.get_node("rack")
	labels_root = world.get_node("labels")
	values_root = world.get_node("values")
	values_root.visible = false

	ground = MeshInstance3D.new()
	ground.mesh = PlaneMesh.new()
	var gm := StandardMaterial3D.new()
	gm.albedo_color = Color(0.62, 0.63, 0.62)
	gm.roughness = 0.95
	ground.material_override = gm
	add_child(ground)

	grid = MeshInstance3D.new()
	var lm := StandardMaterial3D.new()
	lm.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	lm.albedo_color = Color(0.45, 0.47, 0.5, 0.55)
	lm.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	grid.material_override = lm
	grid.visible = false
	add_child(grid)


## The ground and the grid under the plant, and the shadow reach, from the plant's bounds.
func _fit_ground() -> void:
	var box := _bounds()
	var c := box.get_center()
	var size: float = max(box.size.x, box.size.z) + 40.0
	var plane := ground.mesh as PlaneMesh
	plane.size = Vector2(4000.0, 4000.0)
	ground.position = Vector3(c.x, box.position.y - 0.005, c.z)
	var step := 5.0
	var half := ceilf(size / 2.0 / step) * step
	var im := ImmediateMesh.new()
	im.surface_begin(Mesh.PRIMITIVE_LINES)
	var k := -half
	while k <= half + 0.001:
		im.surface_add_vertex(Vector3(c.x + k, 0, c.z - half))
		im.surface_add_vertex(Vector3(c.x + k, 0, c.z + half))
		im.surface_add_vertex(Vector3(c.x - half, 0, c.z + k))
		im.surface_add_vertex(Vector3(c.x + half, 0, c.z + k))
		k += step
	im.surface_end()
	grid.mesh = im
	grid.position = Vector3(0, box.position.y + 0.002, 0)
	sun.directional_shadow_max_distance = rig.distance + box.size.length() * 1.5
	if surroundings != null:
		surroundings.fit(box)


## The corners of every solid of the plant, for a tight fit of the view.
func _corners() -> PackedVector3Array:
	var pts := PackedVector3Array()
	for root in [items_root, lines_root, rack_root]:
		for mi in root.find_children("*", "MeshInstance3D", true, false):
			var b: AABB = mi.global_transform * mi.get_aabb()
			for i in 8:
				pts.append(b.get_endpoint(i))
	if pts.is_empty():
		var b := _bounds()
		for i in 8:
			pts.append(b.get_endpoint(i))
	return pts


func _bounds() -> AABB:
	var box := AABB()
	var first := true
	for root in [items_root, lines_root, rack_root]:
		for mi in root.find_children("*", "MeshInstance3D", true, false):
			var b: AABB = mi.global_transform * mi.get_aabb()
			if first:
				box = b
				first = false
			else:
				box = box.merge(b)
	if first:
		box = AABB(Vector3(-10, 0, -10), Vector3(20, 5, 20))
	return box


# --- scene from DWSIM ------------------------------------------------------------------------

func _on_message(m: Dictionary) -> void:
	match str(m.get("type", "")):
		"welcome":
			scene_name = str(m.get("name", ""))
			tools.set_palette(m.get("palette", []))
			site_builder.set_catalog(m.get("site", []))
			_update_title()
		"scene.full":
			_load_scene(m.get("scene", {}))
		"scene.delta":
			_apply_delta(m)
		"select":
			_set_selection(m.get("ids", []), false)
		"flyby.plan":
			_start_flyby(m.get("ids", []))
		"values":
			_apply_values(m)
		"capture":
			_capture_for_link(m)
		"added":
			# what the build bar builds is not selected: the tool stays for the next one
			if str(m.get("for", "")) != "site.add":
				_pending_select = str(m.get("id", ""))
		"error":
			_show_toast("DWSIM: " + str(m.get("message", "")))
		"bye":
			get_tree().quit()


func _on_state(state: String) -> void:
	_update_title()
	if state == "closed":
		# DWSIM went away: nothing left to show
		get_tree().quit()


func _load_scene(scene: Dictionary) -> void:
	for id in entries.keys():
		_remove_entry(id)
	entries.clear()
	scenes_loaded += 1
	scene_name = str(scene.get("name", scene_name))
	_read_site(scene.get("site"))
	# the relief first, so the objects of the site sit on it
	terrain.load_from(scene.get("terrain"), false)
	for it in scene.get("items", []):
		if it is Dictionary:
			_add_item(it)
	_set_lines(scene.get("lines", []))
	_set_rack(scene.get("rack", null))
	warnings = scene.get("warnings", [])
	terrain.rebuild_all()
	_after_change()
	if not _fitted:
		_fitted = true
		rig.fit_points(_corners(), _aspect())
		_fit_ground()
	if flyby_record and not _flyby_asked:
		_flyby_asked = true
		_ask_flyby()


func _apply_delta(m: Dictionary) -> void:
	var removed: Array = m.get("remove", [])
	var fresh := []
	for it in m.get("upsert", []):
		if it is Dictionary and not entries.has(str(it.get("id", ""))):
			fresh.append(str(it.get("id", "")))
	# a few objects built or taken away play their effects; a change of many passes quietly
	var loud := effects != null and not SiteEffects.quiet(fresh.size() + removed.size())
	for id in removed:
		var key := str(id)
		if loud and entries.has(key) and _shows_effects(entries[key]):
			var node: Node3D = entries[key]["node"]
			entries[key]["node"] = null
			effects.demolished(key, node)
		_remove_entry(key)
		entries.erase(key)
	for it in m.get("upsert", []):
		if it is Dictionary:
			var id := str(it.get("id", ""))
			_remove_entry(id)
			_add_item(it)
	if m.has("lines"):
		_set_lines(m["lines"])
	if m.has("rack"):
		_set_rack(m["rack"])
	if m.has("name"):
		scene_name = str(m["name"])
	if m.has("warnings"):
		warnings = m["warnings"]
	if loud:
		for id in fresh:
			if entries.has(id) and _shows_effects(entries[id]):
				effects.built(id)
	if m.has("site"):
		_read_site(m["site"])
	if m.has("terrain"):
		var t = m["terrain"]
		var sent: bool = t is Dictionary and str(t.get("h", "")) == _terrain_sent and _terrain_sent != ""
		if not sent:
			terrain.load_from(t)
			redrape()
	_after_change()


## Whether an item rises and falls with effects: the objects of the site and the equipment, not the
## badges of logical blocks, the instruments or the lines.
func _shows_effects(e: Dictionary) -> bool:
	if e.get("kind") != "item" or e.get("node") == null or not is_instance_valid(e["node"]):
		return false
	var d: Dictionary = e["data"]
	return not bool(d.get("badge", false)) and not bool(d.get("inst", false))


## Draws the objects of the site again, on the relief as it is now.
func redrape() -> void:
	for id in entries.keys():
		var d: Dictionary = entries[id]["data"]
		if entries[id]["kind"] == "item" and d.has("site"):
			_remove_entry(id)
			_add_item(d)


## Sends the relief to DWSIM after a stroke of a brush, and puts the site back on it.
func send_terrain() -> void:
	var t := terrain.to_dict()
	_terrain_sent = str(t["h"])
	link.send({"type": "terrain", "terrain": t})
	redrape()
	if props != null:
		props.refresh()
	if surroundings != null:
		surroundings.relight()


func _read_site(site) -> void:
	site_adopted = site is Dictionary and bool(site.get("adopted", false))
	if site_builder != null:
		site_builder.set_budget(site.get("budget", {}) if site is Dictionary else {})


func _after_change() -> void:
	# the items were built again: the hover marks a node that may be gone
	_hover_id = ""
	_hover_box.visible = false
	_hover_tip.visible = false
	_mouse_dirty = true
	_fit_ground()
	if _pending_select != "" and entries.has(_pending_select):
		_set_selection([_pending_select], true)
		_pending_select = ""
	else:
		_set_selection(selected.filter(func(id): return entries.has(id)), false)
	if tools != null:
		tools.refresh_ports()
	if life != null:
		life.refresh()
	if props != null:
		props.refresh()
	if surroundings != null:
		surroundings.relight()
	if crew != null:
		crew.clear()
	if maintenance != null:
		maintenance.clear()
	_value_tags()
	_update_title()


func _add_item(it: Dictionary) -> void:
	var id := str(it.get("id", ""))
	var node := builder.item(it)
	var on_site := it.has("site")
	(site_root if on_site else items_root).add_child(node)
	if on_site and str(it.get("cat", "")) != "Buildings":
		# a tree, a road or a car has no label
		entries[id] = {"node": node, "data": it, "kind": "item", "label": null}
		return
	var label := Label3D.new()
	label.text = str(it.get("tag", ""))
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	label.fixed_size = true
	# dark text with a white border; below 32 px the letters of the distance-field font go thin and
	# the border swallows them
	label.pixel_size = 0.0011
	label.font_size = 32
	label.outline_size = 6
	label.modulate = Color(0.08, 0.1, 0.13)
	label.outline_modulate = Color(1, 1, 1, 0.9)
	label.no_depth_test = true
	label.render_priority = 10
	label.outline_render_priority = 9
	var b := _node_bounds(node)
	label.position = Vector3(b.get_center().x, b.end.y + 0.6, b.get_center().z)
	labels_root.add_child(label)
	entries[id] = {"node": node, "data": it, "kind": "item", "label": label}
	label.visible = _label_shown(id)


func _remove_entry(id: String) -> void:
	if not entries.has(id):
		return
	var e: Dictionary = entries[id]
	if e.get("node") != null and is_instance_valid(e["node"]):
		e["node"].queue_free()
	if e.get("label") != null and is_instance_valid(e["label"]):
		e["label"].queue_free()


func _set_lines(lines: Array) -> void:
	for id in entries.keys():
		if entries[id]["kind"] == "line":
			_remove_entry(id)
			entries.erase(id)
	for c in insulation_root.get_children():
		c.queue_free()
	var k := 0
	for ln in lines:
		if not (ln is Dictionary):
			continue
		var node := builder.line(ln, insulation_root)
		lines_root.add_child(node)
		var id = ln.get("id")
		var key := str(id) if id != null else "line#" + str(k)
		entries[key] = {"node": node, "data": ln, "kind": "line", "label": null}
		k += 1


func _set_rack(k) -> void:
	for c in rack_root.get_children():
		c.queue_free()
	if k is Dictionary:
		if k.has("x0"):
			rack_root.add_child(builder.rack(k))
		if k.has("supports"):
			rack_root.add_child(builder.supports(k["supports"]))


func _node_bounds(node: Node3D) -> AABB:
	var box := AABB()
	var first := true
	for mi in node.find_children("*", "MeshInstance3D", true, false):
		var b: AABB = node.transform * (mi.transform * mi.get_aabb())
		if first:
			box = b
			first = false
		else:
			box = box.merge(b)
	return box


# --- selection -------------------------------------------------------------------------------

func _set_selection(ids: Array, tell_dwsim: bool) -> void:
	for id in selected:
		_tint(id, false)
	selected = []
	for id in ids:
		var s := str(id)
		if entries.has(s) and not selected.has(s):
			selected.append(s)
			_tint(s, true)
	_show_info()
	refresh_labels()
	if tell_dwsim:
		var real := selected.filter(func(id): return not str(id).begins_with("line#"))
		link.send({"type": "select", "ids": real})


## The tags of the items: all of them when the panel says so (L), else only those of the selection,
## so that the tags do not crowd the plant.
func refresh_labels() -> void:
	for id in entries.keys():
		var label = entries[id].get("label")
		if label != null and is_instance_valid(label):
			label.visible = _label_shown(id)


func _label_shown(id: String) -> bool:
	var all := surroundings != null and surroundings.all_labels
	return all or selected.has(id)


func _tint(id: String, on: bool) -> void:
	if not entries.has(id):
		return
	var node = entries[id]["node"]
	if node == null or not is_instance_valid(node):
		return
	var mat: Material = _overlay_for(id, on)
	for mi in node.find_children("*", "MeshInstance3D", true, false):
		mi.material_overlay = mat


## What tints an object: a blinking red for an alarm, the selection, the hover, a steady orange for
## an object with an error.
func _overlay_for(id: String, selected_on: bool) -> Material:
	if alarms.has(id) and fmod(Time.get_ticks_msec() / 1000.0, 1.0) < 0.5:
		return _alarm_mat
	if selected_on or selected.has(id):
		return _highlight
	if id == _hover_id:
		return _hover_mat
	if errors.has(id):
		return _error_mat
	return null


# --- hover: what is under the mouse, its limits and its size ----------------------------------

func _set_hover(id: String) -> void:
	if id == _hover_id:
		return
	var old := _hover_id
	_hover_id = id
	if old != "" and not selected.has(old):
		_tint(old, false)
	if id == "" or not entries.has(id):
		_hover_id = ""
		_hover_box.visible = false
		_hover_tip.visible = false
		return
	if not selected.has(id):
		_tint(id, false)
	var node = entries[id]["node"]
	var box := AABB()
	var first := true
	for mi in node.find_children("*", "MeshInstance3D", true, false):
		var b: AABB = mi.global_transform * mi.get_aabb()
		box = b if first else box.merge(b)
		first = false
	if first:
		_hover_box.visible = false
		return
	var im := ImmediateMesh.new()
	im.surface_begin(Mesh.PRIMITIVE_LINES)
	for e in [[0, 1], [1, 3], [3, 2], [2, 0], [4, 5], [5, 7], [7, 6], [6, 4], [0, 4], [1, 5], [2, 6], [3, 7]]:
		im.surface_add_vertex(box.get_endpoint(e[0]))
		im.surface_add_vertex(box.get_endpoint(e[1]))
	im.surface_end()
	_hover_box.mesh = im
	_hover_box.visible = true
	var d: Dictionary = entries[id]["data"]
	var size := "%.1f x %.1f x %.1f m" % [box.size.x, box.size.z, box.size.y] if entries[id]["kind"] == "item" else ""
	if d.has("site"):
		# an object of the site: its estimated cost instead of its bounds
		size = "about " + SiteBuilder.money(SiteObjects.cost_of(d))
	_hover_tip.text = str(d.get("tag", "")) + ("
" + size if size != "" else "")
	_hover_tip.size = Vector2.ZERO
	_hover_tip.visible = true


func _update_hover() -> void:
	if not _mouse_dirty:
		return
	_mouse_dirty = false
	var busy := _drag_button != MOUSE_BUTTON_NONE or (editor != null and editor._dragging) 		or (site_builder != null and site_builder.kind != "")
	_set_hover("" if busy or _mouse.x < 0 else _pick(_mouse))
	if _hover_tip.visible:
		_hover_tip.position = _mouse + Vector2(16, 14)


func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_MOUSE_EXIT and _hover_tip != null:
		_mouse = Vector2(-1, -1)
		_mouse_dirty = true


func _pick(screen: Vector2) -> String:
	var cam := rig.camera
	var from := cam.project_ray_origin(screen)
	var dirv := cam.project_ray_normal(screen)
	var best := INF
	var best_id := ""
	for id in entries.keys():
		var node = entries[id]["node"]
		if node == null or not is_instance_valid(node):
			continue
		for mi in node.find_children("*", "MeshInstance3D", true, false):
			var box: AABB = mi.global_transform * mi.get_aabb()
			var hit = box.intersects_ray(from, dirv)
			if hit != null:
				var t: float = (hit - from).dot(dirv)
				if t < best:
					best = t
					best_id = id
	# a line of an item's own (a column's overhead line, a pipe of a network) stands for its item
	if entries.has(best_id) and entries[best_id]["kind"] == "line":
		var owner = entries[best_id]["data"].get("owner")
		if owner != null and entries.has(str(owner)):
			return str(owner)
	return best_id


# --- input -----------------------------------------------------------------------------------

func _unhandled_input(event: InputEvent) -> void:
	if flyby != null and flyby.active:
		# during a flyby only B and Esc count: they stop it
		if event is InputEventKey and event.pressed and (event.keycode == KEY_B or event.keycode == KEY_ESCAPE):
			flyby.stop()
		return
	if event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_B and not event.ctrl_pressed:
		_ask_flyby()
		return
	if event is InputEventMouseMotion:
		_mouse = (event as InputEventMouseMotion).position
		_mouse_dirty = true
	if site_builder != null and site_builder.handle(event):
		return
	if tools != null and tools.handle(event):
		return
	if editor != null and editor.handle(event):
		return
	if event is InputEventMouseButton:
		var mb := event as InputEventMouseButton
		if mb.pressed:
			match mb.button_index:
				MOUSE_BUTTON_WHEEL_UP:
					rig.zoom_at(0.85, mb.position, _viewport_size())
				MOUSE_BUTTON_WHEEL_DOWN:
					rig.zoom_at(1.0 / 0.85, mb.position, _viewport_size())
				MOUSE_BUTTON_LEFT:
					_press_at = mb.position
					if mb.double_click:
						var id := _pick(mb.position)
						if id != "":
							_edit(id)
						else:
							_fit_all()
				MOUSE_BUTTON_RIGHT, MOUSE_BUTTON_MIDDLE:
					_drag_button = mb.button_index
					_drag_from = mb.position
		else:
			if mb.button_index == MOUSE_BUTTON_LEFT and mb.position.distance_to(_press_at) < 4.0 and not mb.double_click:
				var id := _pick(mb.position)
				if mb.ctrl_pressed and id != "":
					var ids := selected.duplicate()
					if ids.has(id):
						ids.erase(id)
					else:
						ids.append(id)
					_set_selection(ids, true)
				else:
					_set_selection([id] if id != "" else [], true)
			if mb.button_index == _drag_button:
				_drag_button = MOUSE_BUTTON_NONE
	elif event is InputEventMouseMotion and _drag_button != MOUSE_BUTTON_NONE:
		var mm := event as InputEventMouseMotion
		var d := mm.position - _drag_from
		_drag_from = mm.position
		if _drag_button == MOUSE_BUTTON_MIDDLE or mm.shift_pressed:
			rig.pan(d.x, d.y, _viewport_size().y)
		else:
			rig.orbit(d.x, d.y)
	elif event is InputEventKey and event.pressed and not event.echo:
		var k := (event as InputEventKey).keycode
		if surroundings != null and surroundings.handle_key(k):
			return
		if KEY_VIEWS.has(k):
			rig.set_view(KEY_VIEWS[k])
		else:
			match k:
				KEY_Q: rig.turn(-1)
				KEY_E: rig.turn(1)
				KEY_F:
					if (event as InputEventKey).shift_pressed and not selected.is_empty():
						_focus(selected[0])
					else:
						_fit_all()
				KEY_ENTER, KEY_KP_ENTER:
					if not selected.is_empty():
						_edit(selected[0])
				KEY_A:
					animate = not animate
				KEY_V:
					values_root.visible = not values_root.visible
				KEY_O:
					props.visible = not props.visible
					crew.visible = props.visible
					maintenance.visible = props.visible
					site_root.visible = props.visible
				KEY_K:
					crew.panel.visible = not crew.panel.visible
				KEY_L:
					surroundings.set_all_labels(not surroundings.all_labels)
					surroundings.save_settings()
				KEY_I: insulation_root.visible = not insulation_root.visible
				KEY_R: rack_root.visible = not rack_root.visible
				KEY_G: grid.visible = not grid.visible
				KEY_F1: card.visible = not card.visible
				KEY_ESCAPE: _set_selection([], true)
				KEY_EQUAL, KEY_KP_ADD: rig.zoom_at(0.8, _viewport_size() / 2.0, _viewport_size())
				KEY_MINUS, KEY_KP_SUBTRACT: rig.zoom_at(1.25, _viewport_size() / 2.0, _viewport_size())


## New values from DWSIM while a dynamic simulation runs: the properties merge into what the window
## has, the dashes take the new velocities, and the alarms and errors tint their objects.
func _apply_values(m: Dictionary) -> void:
	for group in ["items", "lines"]:
		var got = m.get(group, {})
		if not (got is Dictionary):
			continue
		for id in got.keys():
			if not entries.has(id):
				continue
			var e: Dictionary = entries[id]
			var v: Dictionary = got[id]
			var props: Dictionary = e["data"].get("props", {})
			var fresh = v.get("props", {})
			if fresh is Dictionary:
				for k in fresh.keys():
					props[k] = fresh[k]
			e["data"]["props"] = props
			if v.has("v"):
				e["data"]["v"] = v["v"]
				SceneBuilder.set_flow(e["node"], float(v["v"]))
	var before := alarms.keys() + errors.keys()
	alarms.clear()
	for id in m.get("alarms", []):
		alarms[str(id)] = true
	errors.clear()
	for id in m.get("errors", []):
		errors[str(id)] = true
	for id in before + alarms.keys() + errors.keys():
		_tint(str(id), selected.has(str(id)))
	_show_info()
	_value_tags()


## Small labels with the main values: temperature, pressure and flow on the lines, the first
## values of each item under its tag (V shows them).
func _value_tags() -> void:
	for c in values_root.get_children():
		c.queue_free()
	for id in entries.keys():
		var e: Dictionary = entries[id]
		var props = e["data"].get("props", {})
		if not (props is Dictionary) or props.is_empty():
			continue
		var text := ""
		var at := Vector3.ZERO
		if e["kind"] == "line":
			var parts := []
			for k in ["Temperature (C)", "Pressure (bar)", "Mass flow (kg/h)"]:
				if props.has(k):
					parts.append(str(props[k]) + " " + k.get_slice("(", 1).trim_suffix(")"))
			text = "  ".join(parts)
			var path: Array = e["data"].get("path", [])
			if path.size() < 2:
				continue
			var mid := int(path.size() / 2)
			at = (SceneBuilder.vec(path[mid - 1]) + SceneBuilder.vec(path[mid])) / 2.0 + Vector3(0, 0.6, 0)
		else:
			var lines := []
			for k in props.keys():
				lines.append("%s: %s" % [k, props[k]])
				if lines.size() == 2:
					break
			text = "\n".join(lines)
			var b := _node_bounds(e["node"])
			at = Vector3(b.get_center().x, b.end.y + 0.1, b.get_center().z)
		if text == "":
			continue
		var l := Label3D.new()
		l.text = text
		l.billboard = BaseMaterial3D.BILLBOARD_ENABLED
		l.fixed_size = true
		l.pixel_size = 0.0009
		l.font_size = 32
		l.outline_size = 6
		l.modulate = Color(0.05, 0.3, 0.55)
		l.outline_modulate = Color(1, 1, 1, 0.9)
		l.no_depth_test = true
		l.render_priority = 9
		l.outline_render_priority = 8
		l.vertical_alignment = VERTICAL_ALIGNMENT_TOP
		l.position = at
		values_root.add_child(l)


## Asks DWSIM for the order of the flyby; without DWSIM the units go west to east.
func _ask_flyby() -> void:
	if link.state == "connected":
		link.send({"type": "flyby"})
		return
	var ids := []
	for id in entries.keys():
		var d: Dictionary = entries[id]["data"]
		if entries[id]["kind"] == "item" and not bool(d.get("badge", false)) and not bool(d.get("inst", false)) and not d.has("site"):
			ids.append(id)
	ids.sort_custom(func(a, b): return float(entries[a]["data"]["o"][0]) < float(entries[b]["data"]["o"][0]))
	_start_flyby(ids)


func _start_flyby(ids: Array) -> void:
	if ids.is_empty():
		_show_toast("There is no unit to fly over.")
		if flyby_record:
			get_tree().quit()
		return
	_hud_before.clear()
	for c in hud.get_children():
		if c == title_panel or c == flyby._card:
			continue
		if c is CanvasItem:
			_hud_before[c] = (c as CanvasItem).visible
			(c as CanvasItem).visible = false
	_mouse = Vector2(-1, -1)
	_set_hover("")
	flyby.start(ids, flyby_hold if flyby_hold > 0.0 else surroundings.flyby_hold)


func _on_flyby_finished() -> void:
	for c in _hud_before.keys():
		if is_instance_valid(c):
			(c as CanvasItem).visible = _hud_before[c]
	_hud_before.clear()
	if flyby_record:
		get_tree().quit()


## Asks DWSIM to open the editor of an object in a floating window.
func _edit(id: String) -> void:
	if id.begins_with("line#"):
		_show_toast("This line belongs to its item; open the item's editor.")
		return
	link.send({"type": "edit", "id": id})


func _fit_all() -> void:
	rig.fit_points(_corners(), _aspect())


func _focus(id: String) -> void:
	var node = entries[id]["node"]
	var box := AABB()
	var first := true
	for mi in node.find_children("*", "MeshInstance3D", true, false):
		var b: AABB = mi.global_transform * mi.get_aabb()
		box = b if first else box.merge(b)
		first = false
	if not first:
		var keep := rig.distance
		rig.fit(box.grow(1.0), _aspect())
		rig.distance = max(keep, rig.distance)
		rig.apply()


func _viewport_size() -> Vector2:
	return get_viewport().get_visible_rect().size


func _aspect() -> float:
	var s := _viewport_size()
	return s.x / max(s.y, 1.0)


# --- heads-up display ------------------------------------------------------------------------

func _panel_style(alpha := 1.0) -> StyleBoxFlat:
	var s := StyleBoxFlat.new()
	s.bg_color = Color(0.97, 0.98, 0.99, alpha)
	s.border_color = Color(1, 1, 1, alpha)
	s.set_border_width_all(1)
	s.set_corner_radius_all(8)
	s.content_margin_left = 10
	s.content_margin_right = 10
	s.content_margin_top = 8
	s.content_margin_bottom = 8
	return s


var _glass_material: ShaderMaterial
var _theme: Theme

## A panel of the window: the same width as the others, the shared light theme and a frosted,
## translucent background (shaders/glass.gdshader) through which the plant shows.
func _glass(panel: Control, width := PANEL_W) -> void:
	if _glass_material == null:
		_glass_material = ShaderMaterial.new()
		_glass_material.shader = load("res://shaders/glass.gdshader")
	panel.material = _glass_material
	panel.theme = _light_theme()
	panel.custom_minimum_size.x = width


## Light input fields and buttons for the panels (Godot's own theme is dark), at the panels' font size.
func _light_theme() -> Theme:
	if _theme != null:
		return _theme
	var t := Theme.new()
	_theme = t
	t.default_font_size = FONT_SIZE
	t.set_color("font_color", "Label", Color(0.11, 0.15, 0.2))
	var dark := Color(0.11, 0.15, 0.2)
	var box := func(bg: Color, border: Color) -> StyleBoxFlat:
		var sb := StyleBoxFlat.new()
		sb.bg_color = bg
		sb.border_color = border
		sb.set_border_width_all(1)
		sb.set_corner_radius_all(4)
		sb.content_margin_left = 8
		sb.content_margin_right = 8
		sb.content_margin_top = 3
		sb.content_margin_bottom = 3
		return sb
	t.set_stylebox("normal", "LineEdit", box.call(Color(1, 1, 1), Color(0.78, 0.81, 0.85)))
	t.set_stylebox("focus", "LineEdit", box.call(Color(1, 1, 1), Color(0.24, 0.5, 0.84)))
	t.set_color("font_color", "LineEdit", dark)
	t.set_color("caret_color", "LineEdit", dark)
	t.set_stylebox("normal", "Button", box.call(Color(0.95, 0.96, 0.97), Color(0.78, 0.81, 0.85)))
	t.set_stylebox("hover", "Button", box.call(Color(0.9, 0.93, 0.97), Color(0.24, 0.5, 0.84)))
	t.set_stylebox("pressed", "Button", box.call(Color(0.84, 0.89, 0.96), Color(0.24, 0.5, 0.84)))
	t.set_stylebox("hover_pressed", "Button", box.call(Color(0.8, 0.86, 0.95), Color(0.24, 0.5, 0.84)))
	t.set_stylebox("disabled", "Button", box.call(Color(0.93, 0.94, 0.95, 0.6), Color(0.85, 0.87, 0.9)))
	t.set_color("font_disabled_color", "Button", Color(0.55, 0.58, 0.62))
	for c in ["font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color"]:
		t.set_color(c, "Button", dark)
	return t


func _build_hud() -> void:
	hud = CanvasLayer.new()
	add_child(hud)

	var top := PanelContainer.new()
	title_panel = top
	top.add_theme_stylebox_override("panel", _panel_style())
	_glass(top)
	top.position = Vector2(12, 12)
	var vb := VBoxContainer.new()
	top.add_child(vb)
	title_label = Label.new()
	title_label.add_theme_font_size_override("font_size", 14)
	title_label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	title_label.custom_minimum_size = Vector2(PANEL_W - 20, 0)
	title_label.add_theme_color_override("font_color", Color(0.11, 0.15, 0.2))
	vb.add_child(title_label)
	status_label = Label.new()
	status_label.add_theme_color_override("font_color", Color(0.27, 0.32, 0.38))
	status_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	status_label.custom_minimum_size = Vector2(PANEL_W - 20, 0)
	vb.add_child(status_label)
	hud.add_child(top)

	info_panel = PanelContainer.new()
	info_panel.add_theme_stylebox_override("panel", _panel_style())
	_glass(info_panel)
	info_panel.visible = false
	info_text = RichTextLabel.new()
	info_text.bbcode_enabled = true
	info_text.fit_content = true
	info_text.scroll_active = false
	info_text.custom_minimum_size = Vector2(PANEL_W - 20, 0)
	info_text.add_theme_font_size_override("normal_font_size", FONT_SIZE)
	info_text.add_theme_font_size_override("bold_font_size", FONT_SIZE)
	info_text.add_theme_color_override("default_color", Color(0.11, 0.15, 0.2))
	info_panel.add_child(info_text)
	hud.add_child(info_panel)

	card = PanelContainer.new()
	card.add_theme_stylebox_override("panel", _panel_style())
	_glass(card)
	var gridc := GridContainer.new()
	gridc.columns = 2
	gridc.add_theme_constant_override("h_separation", 12)
	gridc.add_theme_constant_override("v_separation", 1)
	for row in SHORTCUTS:
		for i in 2:
			var l := Label.new()
			l.text = row[i]
			l.add_theme_font_size_override("font_size", 11)
			l.add_theme_color_override("font_color", Color(0.11, 0.15, 0.2) if i == 0 else Color(0.27, 0.32, 0.38))
			if i == 1:
				l.size_flags_horizontal = Control.SIZE_EXPAND_FILL
				l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
				l.custom_minimum_size = Vector2(150, 0)
			gridc.add_child(l)
	card.add_child(gridc)
	hud.add_child(card)

	_hover_tip = Label.new()
	var tip_style := _panel_style(0.9)
	tip_style.set_content_margin_all(5)
	_hover_tip.add_theme_stylebox_override("normal", tip_style)
	_hover_tip.add_theme_color_override("font_color", Color(0.11, 0.15, 0.2))
	_hover_tip.add_theme_font_size_override("font_size", FONT_SIZE)
	_hover_tip.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_hover_tip.visible = false
	hud.add_child(_hover_tip)

	toast = Label.new()
	toast.add_theme_color_override("font_color", Color(0.6, 0.1, 0.1))
	toast.visible = false
	hud.add_child(toast)
	_update_title()


func _process(delta: float) -> void:
	var size := _viewport_size()
	card.position = Vector2(size.x - card.size.x - 12, 12)
	info_panel.position = Vector2(size.x - info_panel.size.x - 12, card.position.y + (card.size.y + 12 if card.visible else 0))
	toast.position = Vector2(14, size.y - 34)
	_update_hover()
	# the dashes of the flow are drawn by a shader, which the low-power mode does not redraw
	OS.low_processor_usage_mode = not animate
	get_tree().call_group("flow", "set_visible", animate)
	for id in alarms.keys():
		_tint(id, selected.has(id))
	if animate:
		var t := Time.get_ticks_msec() / 1000.0
		for n in get_tree().get_nodes_in_group("spin"):
			(n as Node3D).rotate_object_local(n.get_meta("spin_axis", Vector3.UP), float(n.get_meta("spin", 1.0)) * delta)
		for n in get_tree().get_nodes_in_group("blink"):
			(n as Node3D).visible = fmod(t * float(n.get_meta("rate", 1.0)) + float(n.get_meta("phase", 0.0)), 1.0) < 0.6
	if crew != null and crew.panel != null:
		var above := 12.0
		if site_builder != null and site_builder.bar.visible:
			var sb := site_builder.bar
			sb.position = Vector2((size.x - sb.size.x) / 2.0, size.y - sb.size.y - 12)
			above = sb.size.y + 24.0
			var fo := site_builder.flyout
			fo.position = Vector2((size.x - fo.size.x) / 2.0, size.y - above - fo.size.y)
			if fo.visible:
				above += fo.size.y + 12.0
		crew.panel.position = Vector2((size.x - crew.panel.size.x) / 2.0, size.y - crew.panel.size.y - above)
	var ep := hud.get_node_or_null("environment_panel") as Control
	if ep != null:
		ep.position = Vector2(12, size.y - ep.size.y - 12)
	if tools != null and tools.palette_panel != null:
		tools.palette_panel.position = Vector2(12, title_panel.position.y + title_panel.size.y + 12)
	if editor != null and editor.panel != null:
		editor.panel.position = Vector2(size.x - editor.panel.size.x - 12, size.y - editor.panel.size.y - 12)
	if site_builder != null and site_builder.budget_panel != null:
		var bp := site_builder.budget_panel
		var below := 12.0
		if editor != null and editor.panel != null and editor.panel.visible:
			below += editor.panel.size.y + 12.0
		bp.position = Vector2(size.x - bp.size.x - 12, size.y - bp.size.y - below)
	if _toast_time > 0.0:
		_toast_time -= delta
		if _toast_time <= 0.0:
			toast.visible = false


func _update_title() -> void:
	if title_label == null:
		return
	title_label.text = scene_name if scene_name != "" else "DWSIM 3D"
	var n_items := 0
	var n_lines := 0
	for e in entries.values():
		if e["kind"] == "item":
			# the objects of the site are not items of the plant
			if not (e["data"] as Dictionary).has("site"):
				n_items += 1
		else:
			n_lines += 1
	var state := link.state if link != null else "offline"
	var text := "%d items, %d lines" % [n_items, n_lines]
	if state != "connected":
		text += "  (" + state + ")"
	if warnings.size() > 0:
		text += "  -  %d warnings" % warnings.size()
	status_label.text = text


func _show_info() -> void:
	if editor != null:
		editor.refresh()
	if selected.is_empty():
		info_panel.visible = false
		return
	var e: Dictionary = entries[selected[0]]
	var d: Dictionary = e["data"]
	var t := "[b]%s[/b]\n[color=#5d6b7a]%s[/color]\n" % [_esc(str(d.get("tag", ""))), _esc(str(d.get("desc", "")))]
	var props = d.get("props", {})
	if props is Dictionary:
		t += "[table=2]"
		for k in props.keys():
			t += "[cell][color=#5d6b7a]%s[/color]  [/cell][cell]%s[/cell]" % [_esc(str(k)), _esc(str(props[k]))]
		t += "[/table]"
	if selected.size() > 1:
		t += "\n[color=#5d6b7a]and %d more selected[/color]" % (selected.size() - 1)
	info_text.text = t
	info_panel.size = Vector2.ZERO
	info_panel.visible = true


static func _esc(s: String) -> String:
	return s.replace("[", "[lb]")


func _show_toast(text: String) -> void:
	toast.text = text
	toast.visible = true
	_toast_time = 6.0


# --- pictures --------------------------------------------------------------------------------

## A picture of a view at a size, as PNG bytes.
func _capture(view: String, width: int, height: int, fit := true) -> PackedByteArray:
	# the low-power mode draws only when something changes, and a window that is not on screen
	# may not be drawn at all: draw on every frame while the picture is taken, then force a draw
	var low := OS.low_processor_usage_mode
	OS.low_processor_usage_mode = false
	if fit and width > 0 and height > 0:
		get_window().size = Vector2i(width, height)
	# "--capture-clean": the pictures without the panels
	if "--capture-clean" in OS.get_cmdline_user_args():
		hud.visible = false
	for i in 3:
		await get_tree().process_frame
	if view != "":
		rig.set_view(view, false)
	if fit:
		rig.fit_points(_corners(), _aspect())
		_fit_ground()
		# "--capture-zoom=2" takes in twice as much (the site around the plant, say)
		for arg in OS.get_cmdline_user_args():
			if arg.begins_with("--capture-zoom="):
				rig.view_size *= maxf(0.1, float(arg.get_slice("=", 1)))
				rig.apply()
			# "--capture-focus=<tag>" centres the pictures on that unit
			elif arg.begins_with("--capture-focus="):
				var tag := arg.get_slice("=", 1)
				for id in entries.keys():
					if str(entries[id]["data"].get("tag", "")) == tag:
						rig.target = _node_bounds(entries[id]["node"]).get_center()
						rig.apply()
	for i in 3:
		await get_tree().process_frame
	RenderingServer.force_draw(false)
	var img := get_viewport().get_texture().get_image()
	OS.low_processor_usage_mode = low
	return img.save_png_to_buffer()


func _capture_for_link(m: Dictionary) -> void:
	var view := str(m.get("view", ""))
	var png: PackedByteArray = await _capture(view, int(m.get("width", 0)), int(m.get("height", 0)))
	link.send({"type": "capture.result", "ref": m.get("ref"), "view": view, "png": Marshalls.raw_to_base64(png)})


## Without DWSIM: draws a saved scene and, when asked, saves pictures of it and quits.
func _run_offline() -> void:
	var scene_file := ""
	var capture_dir := ""
	var views := PackedStringArray()
	var size := Vector2i(1400, 900)
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--dwsim-scene="):
			scene_file = arg.get_slice("=", 1)
		elif arg.begins_with("--capture-dir="):
			capture_dir = arg.get_slice("=", 1)
		elif arg.begins_with("--views="):
			views = arg.get_slice("=", 1).split(",", false)
		elif arg.begins_with("--size="):
			var wh := arg.get_slice("=", 1).split("x")
			if wh.size() == 2:
				size = Vector2i(int(wh[0]), int(wh[1]))
	if scene_file == "":
		return
	var text := FileAccess.get_file_as_string(scene_file)
	var parsed = JSON.parse_string(text)
	if not (parsed is Dictionary):
		push_error("not a scene: " + scene_file)
		get_tree().quit(2)
		return
	var scene: Dictionary = parsed.get("scene", parsed) if parsed.get("type", "") == "scene.full" else parsed
	_load_scene(scene)
	# more messages, as DWSIM would send them (values, a selection), from "--dwsim-messages=file"
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--dwsim-messages="):
			var more = JSON.parse_string(FileAccess.get_file_as_string(arg.get_slice("=", 1)))
			if more is Array:
				for msg in more:
					if msg is Dictionary:
						_on_message(msg)
	for arg in OS.get_cmdline_user_args():
		if arg == "--rounds":
			crew.panel.visible = true
	if capture_dir == "":
		return
	# "--capture-after=<s>": let the plant live a while first (the rounds, the flow)
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--capture-after="):
			OS.low_processor_usage_mode = false
			await get_tree().create_timer(float(arg.get_slice("=", 1))).timeout
	if views.is_empty():
		views = PackedStringArray(["IsoSE"])
	DirAccess.make_dir_recursive_absolute(capture_dir)
	for v in views:
		var png: PackedByteArray = await _capture(v, size.x, size.y)
		var f := FileAccess.open(capture_dir.path_join(v + ".png"), FileAccess.WRITE)
		if f != null:
			f.store_buffer(png)
			f.close()
	get_tree().quit()
