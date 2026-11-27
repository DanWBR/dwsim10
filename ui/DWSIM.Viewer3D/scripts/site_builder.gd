## The build bar of the site, in the manner of SimCity 3000 (U opens and closes it): a row of
## categories at the bottom of the window; a category opens a grid of the objects in it, each with
## a picture made in the window, its name and its estimated cost. When you pick a point object, a
## ghost of it follows the mouse on a 1 m grid, green where it fits and red where it would stand on
## the equipment or on another object; R turns it a quarter (Shift+R back), a click builds it, and
## the tool stays until you press Esc or click the right button. The objects go to DWSIM
## (site.add), which keeps them with the flowsheet and sends the scene back.
## The first time the bar opens, the site the window drew by itself becomes objects (site.adopt),
## so the user can move, turn or demolish them like the rest.
class_name SiteBuilder
extends Node

const GRID := 1.0
const THUMB := Vector2i(112, 84)

var main: Node3D
var bar: PanelContainer
var flyout: PanelContainer
var kind := ""
var rot := 0
var _catalog: Array = []
var _category := ""
var _grid: GridContainer
var _hint: Label
var _cat_buttons := {}
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


func _init(owner_main: Node3D) -> void:
	main = owner_main


func _ready() -> void:
	_ok_mat = _ghost_mat(Color(0.25, 0.85, 0.35, 0.45))
	_bad_mat = _ghost_mat(Color(0.95, 0.2, 0.15, 0.45))
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
		(_cat_buttons[c] as Button).visible = _catalog.any(func(x): return str(x.get("cat", "")) == c)
	if _category != "":
		_fill(_category)


# --- the bar ---------------------------------------------------------------------------------

const CATEGORIES := ["Buildings", "Roads and paving", "Fences and barriers", "Nature", "Vehicles", "Safety", "Utilities"]
const SHORT := {"Buildings": "Buildings", "Roads and paving": "Roads", "Fences and barriers": "Fences",
	"Nature": "Nature", "Vehicles": "Vehicles", "Safety": "Safety", "Utilities": "Utilities"}


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
		row.add_child(b)
		_cat_buttons[c] = b
	_hint = Label.new()
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
	_update_hint()
	return bar


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
		ch.queue_free()
	for e in _catalog:
		if str(e.get("cat", "")) != c:
			continue
		var k := str(e.get("kind", ""))
		var shape := str(e.get("shape", "point"))
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
		if shape == "point":
			b.pressed.connect(func(): start(k))
		else:
			b.disabled = true
			b.tooltip_text = "Roads, fences and areas are drawn by dragging the mouse; that tool comes in the next step."
		_grid.add_child(b)


func _update_hint() -> void:
	if _hint == null:
		return
	if kind != "":
		_hint.text = "Building: %s. Click to build, R turns it, Esc or the right button stops." % str(SiteObjects.entry(kind).get("name", kind))
	else:
		_hint.text = "Pick a category and an object to build. Drag a built object to move it, T turns it, Del demolishes it. U closes the bar."


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


# --- placing -----------------------------------------------------------------------------------

func start(k: String) -> void:
	stop()
	kind = k
	rot = 0
	_ghost = Node3D.new()
	main.add_child(_ghost)
	SiteObjects.build(_ghost, {"site": k, "id": "ghost", "o": [0, 0, 0], "rot": 0})
	for n in _ghost.find_children("*", "", true, false):
		for gname in ["lamp_head", "lamp_light", "night_glow"]:
			if n.is_in_group(gname):
				n.remove_from_group(gname)
		if n is Light3D:
			(n as Light3D).visible = false
		if n is GeometryInstance3D:
			(n as GeometryInstance3D).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	_ghost.visible = false
	_paint(true)
	_update_hint()


func stop() -> void:
	kind = ""
	if _ghost != null and is_instance_valid(_ghost):
		_ghost.queue_free()
	_ghost = null
	_update_hint()


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


func _ground(screen: Vector2):
	var cam: Camera3D = main.rig.camera
	var dir := cam.project_ray_normal(screen)
	if absf(dir.y) < 0.05:
		return null
	return Plane(Vector3.UP, _ground_y()).intersects_ray(cam.project_ray_origin(screen), dir)


## The half sizes (x, z, m) of the footprint of a point kind turned by rot degrees.
static func half_extent(k: String, turn: float, scale := 1.0) -> Vector2:
	var e := SiteObjects.entry(k)
	var w := float(e.get("w", 1.0)) * scale
	var d := float(e.get("d", 1.0)) * scale
	var a := deg_to_rad(turn)
	return Vector2(absf(cos(a)) * w + absf(sin(a)) * d, absf(sin(a)) * w + absf(cos(a)) * d) / 2.0


## Whether a point object fits at a place: clear of the equipment (with 1 m round it) and of the other point objects.
func fits(k: String, at: Vector3, turn: float) -> bool:
	var h := half_extent(k, turn)
	var mine := Rect2(Vector2(at.x - h.x, at.z - h.y), h * 2.0)
	for id in main.entries.keys():
		var e: Dictionary = main.entries[id]
		if e["kind"] != "item" or not is_instance_valid(e["node"]):
			continue
		var d: Dictionary = e["data"]
		var other: Rect2
		if d.has("site"):
			if str(d.get("shape", "point")) != "point":
				continue
			var n: Node3D = e["node"]
			var oh := half_extent(str(d["site"]), float(d.get("rot", 0)), float(d.get("scale", 1.0)))
			other = Rect2(Vector2(n.position.x - oh.x, n.position.z - oh.y), oh * 2.0)
		else:
			if bool(d.get("badge", false)):
				continue
			var b: AABB = main._node_bounds(e["node"]).grow(1.0)
			other = Rect2(Vector2(b.position.x, b.position.z), Vector2(b.size.x, b.size.z))
		if mine.grow(-0.02).intersects(other):
			return false
	return true


func _snap(g: Vector3) -> Vector3:
	var step := GRID if float(SiteObjects.entry(kind).get("w", 1.0)) >= 1.0 else GRID / 2.0
	return Vector3(snappedf(g.x, step), _ground_y(), snappedf(g.z, step))


## The input of the bar: U, and while placing the mouse, R, Esc and the right button. True when it took the event.
func handle(event: InputEvent) -> bool:
	if event is InputEventKey and event.pressed and not event.echo:
		var key := (event as InputEventKey).keycode
		if key == KEY_U and not (event as InputEventKey).ctrl_pressed:
			toggle()
			return true
		if kind == "":
			return false
		match key:
			KEY_ESCAPE:
				stop()
				return true
			KEY_R:
				rot = int(fposmod(rot + (-90 if (event as InputEventKey).shift_pressed else 90), 360))
				if _ghost != null:
					_ghost.rotation.y = deg_to_rad(rot)
					_paint(fits(kind, _ghost.position, rot))
				return true
		return false
	if kind == "":
		return false
	if event is InputEventMouseMotion:
		var g = _ground((event as InputEventMouseMotion).position)
		if _ghost != null:
			_ghost.visible = g != null
			if g != null:
				_ghost.position = _snap(g)
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
		main.link.send({"type": "site.add", "kind": kind, "x": roundf(at.x * 1000.0), "y": roundf(-at.z * 1000.0),
			"z": roundf(at.y * 1000.0), "rot": rot})
		return true
	return false
