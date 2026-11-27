## The build bar of the site, in the manner of SimCity 3000 (U opens and closes it): a row of
## categories at the bottom of the window; a category opens a grid of the objects in it, each with
## a picture made in the window, its name and its estimated cost.
##  - A point object (a building, a tree, a car): a ghost of it follows the mouse on a 1 m grid,
##    green where it fits and red where it would stand on the equipment or on another object; R
##    turns it a quarter (Shift+R back) and a click builds it.
##  - A line (a road, a fence, a wall, a hedge, a row of trees): press and drag for a straight run,
##    or click point after point and finish with a double click or Enter; Backspace takes the last
##    point back and Shift holds the run to steps of 45 degrees.
##  - An area (paving, gravel, a lawn, woods, a parking lot, a bund): drag a rectangle, or click
##    the corners of an outline and finish with a double click or Enter.
## Lines and areas show as they are drawn, with their length or area and their cost, red where they
## would cross the equipment. The tool stays until you press Esc or click the right button.
## Demolish is the bulldozer: a click takes away one object of the site, a dragged rectangle all
## those it touches; the equipment stays. Everything goes to DWSIM (site.add, site.remove), which
## keeps it with the flowsheet and sends the scene back. The first time the bar opens, the site the
## window drew by itself becomes objects (site.adopt), so the user can change it like the rest.
class_name SiteBuilder
extends Node

const GRID := 1.0
const THUMB := Vector2i(112, 84)
const DRAG_PIXELS := 6.0

var main: Node3D
var bar: PanelContainer
var flyout: PanelContainer
var kind := ""
var rot := 0
var variant := ""
var demolishing := false
var _catalog: Array = []
var _category := ""
var _grid: GridContainer
var _hint: Label
var _cat_buttons := {}
var _demolish_button: Button
var _cash: Label
var _budget_button: Button
## The budget panel: the cost of the site by category, as DWSIM counts it.
var budget_panel: PanelContainer
var _budget_grid: GridContainer
var _budget: Dictionary = {}
var _ghost: Node3D
var _ok := true
var _ok_mat: StandardMaterial3D
var _bad_mat: StandardMaterial3D
var _adopt_sent := false
var _thumbs := {}
var _queue: Array = []
var _stage: SubViewport
var _stage_root: Node3D
var _stage_cam: Camera3D
var _busy := false
# drawing a line or an area, and the bulldozer's rectangle
var _pts: Array = []
var _cursor = null
var _pressed := false
var _dragging := false
var _press_at := Vector2.ZERO
var _start := Vector3.ZERO
var _shown := ""
var _rect: MeshInstance3D


func _init(owner_main: Node3D) -> void:
	main = owner_main


func _ready() -> void:
	_ok_mat = _ghost_mat(Color(0.25, 0.85, 0.35, 0.45))
	_bad_mat = _ghost_mat(Color(0.95, 0.2, 0.15, 0.45))
	_rect = MeshInstance3D.new()
	_rect.mesh = BoxMesh.new()
	_rect.material_override = _ghost_mat(Color(0.95, 0.3, 0.1, 0.3))
	_rect.visible = false
	add_child(_rect)
	_ring = MeshInstance3D.new()
	var rm := StandardMaterial3D.new()
	rm.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	rm.vertex_color_use_as_albedo = true
	rm.no_depth_test = true
	_ring.material_override = rm
	_ring.visible = false
	add_child(_ring)
	# the stage the pictures of the objects are taken on: a world of its own, out of sight
	_stage = SubViewport.new()
	_stage.size = THUMB
	_stage.own_world_3d = true
	_stage.transparent_bg = true
	_stage.render_target_update_mode = SubViewport.UPDATE_DISABLED
	add_child(_stage)
	var env := WorldEnvironment.new()
	var e := Environment.new()
	e.background_mode = Environment.BG_CLEAR_COLOR
	e.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	e.ambient_light_color = Color(0.75, 0.75, 0.75)
	e.ambient_light_energy = 0.8
	env.environment = e
	_stage.add_child(env)
	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-50, 35, 0)
	sun.light_energy = 1.2
	_stage.add_child(sun)
	_stage_cam = Camera3D.new()
	_stage_cam.projection = Camera3D.PROJECTION_ORTHOGONAL
	_stage.add_child(_stage_cam)
	_stage_root = Node3D.new()
	_stage.add_child(_stage_root)


static func _ghost_mat(c: Color) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = c
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	m.no_depth_test = false
	return m


## The catalog of the site from DWSIM's welcome.
func set_catalog(entries: Array) -> void:
	_catalog = entries
	SiteObjects.set_catalog(entries)
	for c in _cat_buttons.keys():
		(_cat_buttons[c] as Button).visible = c == "Terrain" or _catalog.any(func(x): return str(x.get("cat", "")) == c)
	if _category != "":
		_fill(_category)


# --- the bar ---------------------------------------------------------------------------------

const CATEGORIES := ["Buildings", "Roads and paving", "Fences and barriers", "Nature", "Vehicles", "Safety", "Utilities", "Terrain"]
const SHORT := {"Buildings": "Buildings", "Roads and paving": "Roads", "Fences and barriers": "Fences",
	"Nature": "Nature", "Vehicles": "Vehicles", "Safety": "Safety", "Utilities": "Utilities", "Terrain": "Terrain",
	"Earthworks": "Earthworks (m3)"}

## The brushes of the terrain: name and what it does.
const BRUSHES := [["raise", "Raise", "Builds the ground up"], ["lower", "Lower", "Digs the ground down"],
	["level", "Level", "Brings the ground to the height where you press"], ["smooth", "Smooth", "Evens out bumps and steps"],
	["pond", "Pond", "Digs a bowl and fills it with water up to the height where you press"], ["drain", "Drain", "Takes the water away"]]
const SIZES := [["Small", 4.0], ["Medium", 8.0], ["Large", 14.0]]

## The brush of the terrain in hand ("" when none), its radius, and where it is.
var brush := ""
var brush_radius := 8.0
var _ring: MeshInstance3D
var _painting := false
var _target := 0.0


func build_bar(style: StyleBox) -> PanelContainer:
	bar = PanelContainer.new()
	bar.add_theme_stylebox_override("panel", style)
	bar.name = "site_bar"
	var vb := VBoxContainer.new()
	bar.add_child(vb)
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 4)
	vb.add_child(row)
	for c in CATEGORIES:
		var b := Button.new()
		b.text = SHORT[c]
		b.toggle_mode = true
		b.focus_mode = Control.FOCUS_NONE
		b.tooltip_text = c
		b.pressed.connect(func(): _open_category(c))
		b.pressed.connect(_tick)
		row.add_child(b)
		_cat_buttons[c] = b
	row.add_child(VSeparator.new())
	_demolish_button = Button.new()
	_demolish_button.text = "Demolish"
	_demolish_button.toggle_mode = true
	_demolish_button.focus_mode = Control.FOCUS_NONE
	_demolish_button.tooltip_text = "The bulldozer: click an object of the site, or drag a rectangle over several"
	_demolish_button.pressed.connect(func():
		if demolishing:
			stop()
		else:
			start_demolish())
	_demolish_button.pressed.connect(_tick)
	row.add_child(_demolish_button)
	row.add_child(VSeparator.new())
	# the cost of the site so far, as SimCity shows the city's funds
	_cash = Label.new()
	_cash.add_theme_font_size_override("font_size", 13)
	_cash.add_theme_color_override("font_color", Color(0.1, 0.36, 0.2))
	_cash.tooltip_text = "The estimated cost of the site (USD, 2026)"
	_cash.mouse_filter = Control.MOUSE_FILTER_PASS
	row.add_child(_cash)
	_budget_button = Button.new()
	_budget_button.text = "Budget"
	_budget_button.toggle_mode = true
	_budget_button.focus_mode = Control.FOCUS_NONE
	_budget_button.pressed.connect(func(): budget_panel.visible = _budget_button.button_pressed)
	_budget_button.pressed.connect(_tick)
	row.add_child(_budget_button)
	_hint = Label.new()
	# the hint wraps at the width of the buttons, so a long one does not stretch the bar
	_hint.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_hint.custom_minimum_size = Vector2(1, 0)
	_hint.add_theme_font_size_override("font_size", 11)
	_hint.add_theme_color_override("font_color", Color(0.27, 0.32, 0.38))
	vb.add_child(_hint)
	bar.visible = false

	flyout = PanelContainer.new()
	flyout.add_theme_stylebox_override("panel", style.duplicate())
	flyout.name = "site_flyout"
	var scroll := ScrollContainer.new()
	scroll.custom_minimum_size = Vector2(0, 250)
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	flyout.add_child(scroll)
	_grid = GridContainer.new()
	_grid.columns = 5
	_grid.add_theme_constant_override("h_separation", 6)
	_grid.add_theme_constant_override("v_separation", 6)
	scroll.add_child(_grid)
	flyout.visible = false

	budget_panel = PanelContainer.new()
	budget_panel.add_theme_stylebox_override("panel", style.duplicate())
	budget_panel.name = "site_budget"
	var bv := VBoxContainer.new()
	budget_panel.add_child(bv)
	var title := Label.new()
	title.text = "Site budget"
	title.add_theme_font_size_override("font_size", 14)
	bv.add_child(title)
	_budget_grid = GridContainer.new()
	_budget_grid.columns = 3
	_budget_grid.add_theme_constant_override("h_separation", 14)
	bv.add_child(_budget_grid)
	var note := Label.new()
	note.text = "Rough estimates in US dollars of 2026, for planning; not a quote."
	note.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	note.custom_minimum_size = Vector2(240, 0)
	note.add_theme_font_size_override("font_size", 10)
	note.add_theme_color_override("font_color", Color(0.4, 0.45, 0.5))
	bv.add_child(note)
	budget_panel.visible = false
	set_budget({})
	_update_hint()
	return bar


## The budget DWSIM sent with the scene: {total, currency, items: [{cat, count, cost}]}.
func set_budget(b) -> void:
	_budget = b if b is Dictionary else {}
	var total := float(_budget.get("total", 0.0))
	_cash.text = "Site: " + money(total)
	for ch in _budget_grid.get_children():
		# out of the grid at once, so the panel shrinks to the new rows
		_budget_grid.remove_child(ch)
		ch.queue_free()
	var rows: Array = _budget.get("items", [])
	for r in rows:
		_cell(SHORT.get(str(r.get("cat", "")), str(r.get("cat", ""))), false)
		_cell("%d" % int(r.get("count", 0)), true)
		_cell(money(float(r.get("cost", 0.0))), true)
	_cell("Total", false, true)
	_cell("%d" % rows.reduce(func(a, r): return a + int(r.get("count", 0)), 0), true, true)
	_cell(money(total), true, true)
	# what building the site emits, from the concrete, steel, asphalt and earthworks it takes
	var co2 := float(_budget.get("co2e_kg", 0.0))
	if co2 > 0.0:
		_cell("Embodied CO2e", false)
		_cell("", true)
		_cell("%.0f t" % (co2 / 1000.0) if co2 >= 10000.0 else "%.1f t" % (co2 / 1000.0), true)
	budget_panel.reset_size.call_deferred()


func _cell(text: String, right: bool, bold := false) -> void:
	var l := Label.new()
	l.text = text
	l.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT if right else HORIZONTAL_ALIGNMENT_LEFT
	l.add_theme_font_size_override("font_size", 13 if bold else 12)
	if bold:
		l.add_theme_color_override("font_color", Color(0.05, 0.08, 0.12))
	_budget_grid.add_child(l)


func toggle() -> void:
	if bar.visible:
		close()
	else:
		open()


func open() -> void:
	bar.visible = true
	_update_hint()
	if not main.site_adopted and not _adopt_sent and main.link != null and main.link.state == "connected":
		# the site the window drew by itself becomes objects the user can change
		_adopt_sent = true
		main.link.send({"type": "site.adopt", "items": main.props.auto_items()})


func close() -> void:
	stop()
	bar.visible = false
	flyout.visible = false
	budget_panel.visible = false
	_budget_button.button_pressed = false
	_category = ""
	for b in _cat_buttons.values():
		(b as Button).button_pressed = false


func _open_category(c: String) -> void:
	if _category == c and flyout.visible:
		_category = ""
		flyout.visible = false
	else:
		_category = c
		flyout.visible = true
		_fill(c)
	for k in _cat_buttons.keys():
		(_cat_buttons[k] as Button).button_pressed = k == _category


static func money(v: float) -> String:
	if v >= 1.0e6:
		return "$%.1fM" % (v / 1.0e6)
	if v >= 1.0e3:
		return "$%dk" % int(roundf(v / 1.0e3))
	return "$%d" % int(v)


func _fill(c: String) -> void:
	for ch in _grid.get_children():
		# out of the grid at once, so the new tiles keep their names
		_grid.remove_child(ch)
		ch.queue_free()
	if c == "Terrain":
		_fill_terrain()
		return
	for e in _catalog:
		if str(e.get("cat", "")) != c:
			continue
		var k := str(e.get("kind", ""))
		var unit := str(e.get("unit", "each"))
		var b := Button.new()
		b.name = "site_" + k
		b.focus_mode = Control.FOCUS_NONE
		b.custom_minimum_size = Vector2(118, 124)
		b.text = "%s\n%s%s" % [str(e.get("name", k)), money(float(e.get("cost", 0.0))), "" if unit == "each" else "/" + unit]
		b.add_theme_font_size_override("font_size", 11)
		b.vertical_icon_alignment = VERTICAL_ALIGNMENT_TOP
		b.icon_alignment = HORIZONTAL_ALIGNMENT_CENTER
		b.expand_icon = true
		b.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		if _thumbs.has(k):
			b.icon = _thumbs[k]
		else:
			_queue.append(k)
		b.pressed.connect(func(): start(k))
		b.pressed.connect(_tick)
		_grid.add_child(b)


func _shape() -> String:
	return SiteObjects.shape_of(kind) if kind != "" else ""


func _tick() -> void:
	if main.effects != null:
		main.effects.click()


## The tiles of the terrain: the brushes, and the sizes of the brush.
func _fill_terrain() -> void:
	for b in BRUSHES:
		var t := Button.new()
		t.name = "terrain_" + b[0]
		t.focus_mode = Control.FOCUS_NONE
		t.custom_minimum_size = Vector2(118, 60)
		t.text = b[1]
		t.tooltip_text = b[2]
		t.toggle_mode = true
		t.button_pressed = brush == b[0]
		var k: String = b[0]
		t.pressed.connect(func(): start_brush(k))
		t.pressed.connect(_tick)
		_grid.add_child(t)
	for s in SIZES:
		var t := Button.new()
		t.name = "size_" + str(s[0]).to_lower()
		t.focus_mode = Control.FOCUS_NONE
		t.custom_minimum_size = Vector2(118, 40)
		t.text = "%s brush (%d m)" % [s[0], int(s[1])]
		t.toggle_mode = true
		t.button_pressed = absf(brush_radius - float(s[1])) < 0.1
		var r: float = s[1]
		t.pressed.connect(func():
			brush_radius = r
			_fill("Terrain"))
		_grid.add_child(t)


func _update_hint() -> void:
	if _hint == null:
		return
	if bar != null:
		bar.reset_size.call_deferred()
	var name := str(SiteObjects.entry(kind).get("name", kind))
	if brush != "":
		var what := ""
		for b in BRUSHES:
			if b[0] == brush:
				what = b[2]
		_hint.text = "Terrain, %s: %s. Hold the left button and move over the ground; the plant's platform stays flat. Esc stops." % [brush, what.to_lower()]
	elif demolishing:
		_hint.text = "Demolish: click an object of the site, or drag a rectangle over several. The equipment stays. Esc stops."
	elif kind == "":
		_hint.text = "Pick a category and an object to build. Drag a built object to move it, T turns it, Del demolishes it. U closes the bar."
	elif _shape() == "point":
		_hint.text = "Building: %s, about %s. Click to build, R turns it%s, Esc or the right button stops." % [name,
			money(float(SiteObjects.entry(kind).get("cost", 0.0))), ", V changes its look" if SiteObjects.VARIANTS.has(kind) else ""]
	else:
		var pts := _drawn()
		var line := _shape() == "line"
		var size := _length(pts) if line else _area(pts)
		var cost := size * float(SiteObjects.entry(kind).get("cost", 0.0))
		var what := ("%s: %d m, about %s." % [name, int(roundf(size)), money(cost)]) if line else ("%s: %d m2, about %s." % [name, int(roundf(size)), money(cost)])
		if pts.size() < 2:
			what = "%s: %s." % [name, "press and drag for a straight run, or click point after point" if line else "drag a rectangle, or click the corners of an outline"]
		if _dragging:
			_hint.text = what + " Release to build."
		elif _pts.size() > 0:
			_hint.text = what + " Click to add a point, double click or Enter to finish, Backspace takes a point back, Esc cancels."
		else:
			_hint.text = what + " Esc or the right button stops."


# --- pictures of the objects ---------------------------------------------------------------------

## A sample of an object for its picture: lines 12 m long, areas 10 m square.
static func sample(k: String) -> Dictionary:
	var shape := SiteObjects.shape_of(k)
	var d := {"site": k, "id": "sample:" + k, "o": [0.0, 0.0, 0.0], "rot": 30, "shape": shape}
	if shape == "line":
		d["points"] = [[0.0, 0.0], [12000.0, 0.0]]
		d["rot"] = 0
	elif shape == "area":
		d["points"] = [[0.0, 0.0], [10000.0, 0.0], [10000.0, 10000.0], [0.0, 10000.0]]
		d["rot"] = 0
	return d


func _process(_delta: float) -> void:
	if not _busy and not _queue.is_empty():
		_shoot(_queue.pop_front())


func _shoot(k: String) -> void:
	if _thumbs.has(k):
		return
	_busy = true
	for c in _stage_root.get_children():
		c.queue_free()
	var node := Node3D.new()
	_stage_root.add_child(node)
	SiteObjects.build(node, sample(k))
	for l in node.find_children("*", "Light3D", true, false):
		(l as Light3D).visible = false
	var b := SiteObjects._bounds_of(node)
	if node.rotation.y != 0.0:
		b = Transform3D(Basis(Vector3.UP, node.rotation.y), Vector3.ZERO) * b
	var c := b.get_center()
	var size := maxf(b.size.length(), 0.5)
	_stage_cam.size = size * 0.95
	var dir := Vector3(1.0, 0.85, 1.25).normalized()
	_stage_cam.look_at_from_position(c + dir * size * 3.0, c, Vector3.UP)
	_stage_cam.near = 0.05
	_stage_cam.far = size * 8.0
	var low := OS.low_processor_usage_mode
	OS.low_processor_usage_mode = false
	_stage.render_target_update_mode = SubViewport.UPDATE_ONCE
	await RenderingServer.frame_post_draw
	await RenderingServer.frame_post_draw
	OS.low_processor_usage_mode = low
	var img := _stage.get_texture().get_image()
	if img != null and not img.is_empty():
		var tex := ImageTexture.create_from_image(img)
		_thumbs[k] = tex
		if _grid != null:
			var btn := _grid.get_node_or_null("site_" + k) as Button
			if btn != null:
				btn.icon = tex
	_busy = false


# --- the tools -------------------------------------------------------------------------------------

func start(k: String) -> void:
	stop()
	kind = k
	rot = 0
	variant = ""
	if _shape() == "point":
		_deal()
	_update_hint()


## A look for the next object of a kind that has several (V picks the next one), and its ghost.
func _deal(step := 0) -> void:
	var list: Array = SiteObjects.VARIANTS.get(kind, [])
	if list.is_empty():
		variant = ""
	elif step == 0:
		variant = list[randi() % list.size()]
	else:
		variant = list[(list.find(variant) + step) % list.size()]
	var at := Vector3.ZERO
	var shown := false
	if _ghost != null and is_instance_valid(_ghost):
		at = _ghost.position
		shown = _ghost.visible
	_drop_ghost()
	_ghost = _make_ghost({"site": kind, "id": "ghost", "o": [0, 0, 0], "rot": 0, "variant": variant})
	_ghost.position = at
	_ghost.rotation.y = deg_to_rad(rot)
	_ghost.visible = shown
	_paint(fits(kind, at, rot) if shown else true)


func start_demolish() -> void:
	stop()
	demolishing = true
	_demolish_button.button_pressed = true
	_update_hint()


## Takes a brush of the terrain in hand.
func start_brush(k: String) -> void:
	stop()
	brush = k
	_fill("Terrain")
	_update_hint()


func stop() -> void:
	kind = ""
	if _painting:
		_painting = false
		main.effects.earthwork(false)
		main.send_terrain()
	brush = ""
	if _ring != null:
		_ring.visible = false
	demolishing = false
	if _demolish_button != null:
		_demolish_button.button_pressed = false
	_drop_ghost()
	_pts.clear()
	_cursor = null
	_pressed = false
	_dragging = false
	_shown = ""
	_rect.visible = false
	_update_hint()


func _drop_ghost() -> void:
	if _ghost != null and is_instance_valid(_ghost):
		_ghost.queue_free()
	_ghost = null


## A ghost of an object: drawn like the object, see-through, without lights or shadows.
func _make_ghost(it: Dictionary) -> Node3D:
	var g := Node3D.new()
	main.add_child(g)
	SiteObjects.build(g, it)
	for n in g.find_children("*", "", true, false):
		for gname in ["lamp_head", "lamp_light", "night_glow"]:
			if n.is_in_group(gname):
				n.remove_from_group(gname)
		if n is Light3D:
			(n as Light3D).visible = false
		if n is GeometryInstance3D:
			(n as GeometryInstance3D).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	return g


func _paint(ok: bool) -> void:
	_ok = ok
	if _ghost == null:
		return
	var m := _ok_mat if ok else _bad_mat
	for n in _ghost.find_children("*", "", true, false):
		if n is GeometryInstance3D:
			(n as GeometryInstance3D).material_override = m


func _ground_y() -> float:
	return main._bounds().position.y


## Where the mouse points on the ground, relief included; the point carries the plant's ground height
## (the objects add the relief themselves).
func _ground(screen: Vector2):
	var cam: Camera3D = main.rig.camera
	var g = main.terrain.ray(cam.project_ray_origin(screen), cam.project_ray_normal(screen))
	if g == null:
		return null
	return Vector3(g.x, _ground_y(), g.z)


## The half sizes (x, z, m) of the footprint of a point kind turned by rot degrees.
static func half_extent(k: String, turn: float, scale := 1.0) -> Vector2:
	var e := SiteObjects.entry(k)
	var w := float(e.get("w", 1.0)) * scale
	var d := float(e.get("d", 1.0)) * scale
	var a := deg_to_rad(turn)
	return Vector2(absf(cos(a)) * w + absf(sin(a)) * d, absf(sin(a)) * w + absf(cos(a)) * d) / 2.0


## The footprints (x, z) of the equipment, with 1 m round each.
func _equipment() -> Array:
	var out := []
	for id in main.entries.keys():
		var e: Dictionary = main.entries[id]
		var d: Dictionary = e["data"]
		if e["kind"] != "item" or not is_instance_valid(e["node"]) or d.has("site") or bool(d.get("badge", false)):
			continue
		var b: AABB = main._node_bounds(e["node"]).grow(1.0)
		out.append(Rect2(Vector2(b.position.x, b.position.z), Vector2(b.size.x, b.size.z)))
	return out


## Whether a point object fits at a place: clear of the equipment (with 1 m round it) and of the other point objects.
func fits(k: String, at: Vector3, turn: float) -> bool:
	var h := half_extent(k, turn)
	var mine := Rect2(Vector2(at.x - h.x, at.z - h.y), h * 2.0).grow(-0.02)
	for r in _equipment():
		if mine.intersects(r):
			return false
	for id in main.entries.keys():
		var e: Dictionary = main.entries[id]
		var d: Dictionary = e["data"]
		if e["kind"] != "item" or not d.has("site") or str(d.get("shape", "point")) != "point" or not is_instance_valid(e["node"]):
			continue
		var n: Node3D = e["node"]
		var oh := half_extent(str(d["site"]), float(d.get("rot", 0)), float(d.get("scale", 1.0)))
		if mine.intersects(Rect2(Vector2(n.position.x - oh.x, n.position.z - oh.y), oh * 2.0)):
			return false
	return true


## Whether a line (or, closed, an area) keeps clear of the equipment.
func shape_fits(pts: Array, closed: bool) -> bool:
	var flat := PackedVector2Array()
	for p in pts:
		flat.append(Vector2(p.x, p.z))
	for r in _equipment():
		var corners := [r.position, Vector2(r.end.x, r.position.y), r.end, Vector2(r.position.x, r.end.y)]
		for p in flat:
			if r.has_point(p):
				return false
		if closed and flat.size() >= 3:
			for c in corners:
				if Geometry2D.is_point_in_polygon(c, flat):
					return false
		var n := flat.size() if closed else flat.size() - 1
		for i in n:
			var a := flat[i]
			var b := flat[(i + 1) % flat.size()]
			for k in 4:
				if Geometry2D.segment_intersects_segment(a, b, corners[k], corners[(k + 1) % 4]) != null:
					return false
	return true


func _snap(g: Vector3) -> Vector3:
	var step := GRID
	if _shape() == "point" and float(SiteObjects.entry(kind).get("w", 1.0)) < 1.0:
		step = GRID / 2.0
	return Vector3(snappedf(g.x, step), _ground_y(), snappedf(g.z, step))


## Holds a run from the last point to steps of 45 degrees (Shift).
static func _steady(from: Vector3, to: Vector3) -> Vector3:
	var d := to - from
	var a := snappedf(atan2(d.z, d.x), PI / 4.0)
	var l := Vector2(d.x, d.z).length()
	return from + Vector3(cos(a) * l, 0.0, sin(a) * l)


## The points drawn so far and the cursor: the whole run while dragging, the rectangle for an area.
func _drawn() -> Array:
	if _dragging and _cursor != null:
		if _shape() == "area":
			var a: Vector3 = _start
			var b: Vector3 = _cursor
			return [a, Vector3(b.x, a.y, a.z), b, Vector3(a.x, a.y, b.z)]
		return [_start, _cursor]
	var pts := _pts.duplicate()
	if _cursor != null and not pts.is_empty() and (_cursor as Vector3).distance_to(pts[-1]) > 0.01:
		pts.append(_cursor)
	return pts


static func _length(pts: Array) -> float:
	var s := 0.0
	for i in range(1, pts.size()):
		s += (pts[i] as Vector3).distance_to(pts[i - 1])
	return s


static func _area(pts: Array) -> float:
	if pts.size() < 3:
		return 0.0
	var s := 0.0
	for i in pts.size():
		var a: Vector3 = pts[i]
		var b: Vector3 = pts[(i + 1) % pts.size()]
		s += a.x * b.z - b.x * a.z
	return absf(s) / 2.0


static func _to_dwsim(p: Vector3) -> Array:
	return [roundf(p.x * 1000.0), roundf(-p.z * 1000.0)]


## Draws the line or the area as it stands, green or red; rebuilt only when the points change.
func _show_shape() -> void:
	var pts := _drawn()
	var key := str(pts)
	if key == _shown:
		return
	_shown = key
	_drop_ghost()
	var line := _shape() == "line"
	if pts.size() >= 2 and (line or pts.size() >= 3):
		var list := []
		for p in pts:
			list.append(_to_dwsim(p))
		_ghost = _make_ghost({"site": kind, "id": "ghost", "o": [list[0][0], list[0][1], roundf(_ground_y() * 1000.0)],
			"points": list, "shape": _shape()})
		_ghost.position = pts[0]
		_paint(shape_fits(pts, not line))
	_update_hint()


func _finish() -> void:
	var pts := _drawn() if _dragging else _pts.duplicate()
	var line := _shape() == "line"
	var size := _length(pts) if line else _area(pts)
	_pts.clear()
	_dragging = false
	_pressed = false
	if (line and (pts.size() < 2 or size < 1.0)) or (not line and (pts.size() < 3 or size < 1.0)):
		_shown = ""
		_drop_ghost()
		_update_hint()
		return
	if not shape_fits(pts, not line):
		main._show_toast("It would cross the equipment.")
		_shown = ""
		_drop_ghost()
		_update_hint()
		return
	var list := []
	for p in pts:
		list.append(_to_dwsim(p))
	main.link.send({"type": "site.add", "kind": kind, "points": list, "z": roundf(_ground_y() * 1000.0)})
	_shown = ""
	_drop_ghost()
	_update_hint()


# --- input ---------------------------------------------------------------------------------------

## The input of the bar: U, and while a tool is on the mouse and its keys. True when it took the event.
func handle(event: InputEvent) -> bool:
	if event is InputEventKey and event.pressed and not event.echo:
		var ke := event as InputEventKey
		if ke.keycode == KEY_U and not ke.ctrl_pressed:
			toggle()
			return true
		if brush != "" and ke.keycode == KEY_ESCAPE:
			stop()
			return true
		if kind == "" and not demolishing:
			return false
		match ke.keycode:
			KEY_ESCAPE:
				if not _pts.is_empty() or _dragging:
					_pts.clear()
					_dragging = false
					_pressed = false
					_shown = ""
					_drop_ghost()
					_update_hint()
				else:
					stop()
				return true
			KEY_ENTER, KEY_KP_ENTER:
				if _shape() == "line" or _shape() == "area":
					_finish()
					return true
			KEY_BACKSPACE:
				if not _pts.is_empty():
					_pts.pop_back()
					_show_shape()
					return true
			KEY_V:
				if _shape() == "point" and SiteObjects.VARIANTS.has(kind):
					_deal(1)
					return true
			KEY_R:
				if _shape() == "point":
					rot = int(fposmod(rot + (-90 if ke.shift_pressed else 90), 360))
					if _ghost != null:
						_ghost.rotation.y = deg_to_rad(rot)
						_paint(fits(kind, _ghost.position, rot))
					return true
		return false
	if brush != "":
		return _sculpt(event)
	if demolishing:
		return _demolish(event)
	if kind == "":
		return false
	if _shape() == "point":
		return _place(event)
	return _draw(event)


func _place(event: InputEvent) -> bool:
	if event is InputEventMouseMotion:
		var g = _ground((event as InputEventMouseMotion).position)
		if _ghost != null:
			_ghost.visible = g != null
			if g != null:
				_ghost.position = _snap(g)
				_ghost.position.y += SiteObjects.ground_at(_ghost.position.x, _ghost.position.z)
				_ghost.rotation.y = deg_to_rad(rot)
				_paint(fits(kind, _ghost.position, rot))
		return false
	if event is InputEventMouseButton:
		var mb := event as InputEventMouseButton
		if not mb.pressed:
			return mb.button_index == MOUSE_BUTTON_LEFT
		if mb.button_index == MOUSE_BUTTON_RIGHT:
			stop()
			return true
		if mb.button_index != MOUSE_BUTTON_LEFT:
			return false
		var g = _ground(mb.position)
		if g == null:
			return true
		var at := _snap(g)
		if not fits(kind, at, rot):
			main._show_toast("There is no room for it there.")
			return true
		var add := {"type": "site.add", "kind": kind, "x": roundf(at.x * 1000.0), "y": roundf(-at.z * 1000.0),
			"z": roundf(at.y * 1000.0), "rot": rot}
		if variant != "":
			add["variant"] = variant
		main.link.send(add)
		# the next one may look different
		_deal()
		return true
	return false


func _draw(event: InputEvent) -> bool:
	if event is InputEventMouseMotion:
		var mm := event as InputEventMouseMotion
		var g = _ground(mm.position)
		if g == null:
			return false
		var p := _snap(g)
		var anchor = _start if _dragging or (_pressed and _pts.size() <= 1) else (_pts[-1] if not _pts.is_empty() else null)
		if mm.shift_pressed and anchor != null and _shape() == "line":
			p = _snap(_steady(anchor, p))
		_cursor = p
		if _pressed and not _dragging and mm.position.distance_to(_press_at) > DRAG_PIXELS and _pts.size() == 1:
			_dragging = true
			_pts.clear()
		_show_shape()
		return false
	if event is InputEventMouseButton:
		var mb := event as InputEventMouseButton
		if mb.button_index == MOUSE_BUTTON_RIGHT and mb.pressed:
			if not _pts.is_empty() or _dragging:
				_pts.clear()
				_dragging = false
				_pressed = false
				_shown = ""
				_drop_ghost()
				_update_hint()
			else:
				stop()
			return true
		if mb.button_index != MOUSE_BUTTON_LEFT:
			return false
		var g = _ground(mb.position)
		if mb.pressed:
			if g == null:
				return true
			var p := _snap(g)
			if mb.double_click:
				if _pts.is_empty() or p.distance_to(_pts[-1]) > 0.01:
					_pts.append(p)
				_finish()
				return true
			if _pts.is_empty():
				_start = p
				_press_at = mb.position
				_pressed = true
				_pts.append(p)
			elif p.distance_to(_pts[-1]) > 0.01:
				_pts.append(p)
			_cursor = p
			_show_shape()
			return true
		# released: a drag builds at once; a click goes on adding points
		_pressed = false
		if _dragging:
			_finish()
		return true
	return false


# --- the terrain -----------------------------------------------------------------------------------

func _sculpt(event: InputEvent) -> bool:
	var cam: Camera3D = main.rig.camera
	if event is InputEventMouseMotion:
		var at := (event as InputEventMouseMotion).position
		var g = main.terrain.ray(cam.project_ray_origin(at), cam.project_ray_normal(at))
		_cursor = g
		_draw_ring()
		return false
	if event is InputEventMouseButton:
		var mb := event as InputEventMouseButton
		if mb.button_index == MOUSE_BUTTON_RIGHT and mb.pressed:
			stop()
			return true
		if mb.button_index != MOUSE_BUTTON_LEFT:
			return false
		if mb.pressed:
			var g = main.terrain.ray(cam.project_ray_origin(mb.position), cam.project_ray_normal(mb.position))
			if g != null:
				_cursor = g
				_painting = true
				_target = main.terrain.height_at(g.x, g.z)
			return true
		if _painting:
			_painting = false
			main.effects.earthwork(false)
			main.send_terrain()
		return true
	return false


## Brushes the ground while the left button is held.
func _physics_process(delta: float) -> void:
	if _painting and _cursor != null and brush != "":
		var changed: Array = main.terrain.brush(brush, _cursor, brush_radius, delta, _target)
		main.terrain.rebuild_chunks(changed)
		_draw_ring()
		var at: Vector3 = _cursor
		main.effects.earthwork(true, Vector3(at.x, main.terrain.ground_y() + main.terrain.height_at(at.x, at.z), at.z), brush_radius)


## The ring of the brush on the ground: white, or red over the platform of the plant.
func _draw_ring() -> void:
	if _cursor == null:
		_ring.visible = false
		return
	var t: SiteTerrain = main.terrain
	t.update_lock()
	var c: Vector3 = _cursor
	var red := t.locked.has_point(Vector2(c.x, c.z))
	var im := ImmediateMesh.new()
	im.surface_begin(Mesh.PRIMITIVE_LINE_STRIP)
	var y := t.ground_y()
	for i in 49:
		var a := TAU * i / 48.0
		var p := Vector3(c.x + cos(a) * brush_radius, 0, c.z + sin(a) * brush_radius)
		p.y = y + t.height_at(p.x, p.z) + 0.15
		im.surface_set_color(Color(0.95, 0.25, 0.2) if red else Color(1, 1, 1))
		im.surface_add_vertex(p)
	im.surface_end()
	_ring.mesh = im
	_ring.visible = true


func _demolish(event: InputEvent) -> bool:
	if event is InputEventMouseMotion:
		var mm := event as InputEventMouseMotion
		if _pressed:
			if not _dragging and mm.position.distance_to(_press_at) > DRAG_PIXELS:
				_dragging = true
			var g = _ground(mm.position)
			if _dragging and g != null:
				_cursor = g
				var a: Vector3 = _start
				var b: Vector3 = g
				(_rect.mesh as BoxMesh).size = Vector3(maxf(absf(b.x - a.x), 0.1), 0.3, maxf(absf(b.z - a.z), 0.1))
				_rect.position = Vector3((a.x + b.x) / 2.0, _ground_y() + 0.15, (a.z + b.z) / 2.0)
				_rect.visible = true
		return false
	if event is InputEventMouseButton:
		var mb := event as InputEventMouseButton
		if mb.button_index == MOUSE_BUTTON_RIGHT and mb.pressed:
			stop()
			return true
		if mb.button_index != MOUSE_BUTTON_LEFT:
			return false
		if mb.pressed:
			var g = _ground(mb.position)
			_pressed = g != null
			_dragging = false
			_press_at = mb.position
			if g != null:
				_start = g
			return true
		_rect.visible = false
		if not _pressed:
			return true
		_pressed = false
		if _dragging and _cursor != null:
			_dragging = false
			var a: Vector3 = _start
			var b: Vector3 = _cursor
			main.link.send({"type": "site.remove", "rect": [roundf(a.x * 1000.0), roundf(-a.z * 1000.0), roundf(b.x * 1000.0), roundf(-b.z * 1000.0)]})
			return true
		var id: String = main._pick(mb.position)
		if id == "":
			return true
		if (main.entries[id]["data"] as Dictionary).has("site"):
			main.link.send({"type": "site.remove", "ids": [id]})
		else:
			main._show_toast("The bulldozer leaves the equipment alone; Del takes it out of the flowsheet.")
		return true
	return false
