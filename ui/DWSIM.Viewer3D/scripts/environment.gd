## The surroundings of the plant: the sun and the moon by the hour, the sky, the ground (a concrete
## pad under the plant, a gravel band around it, grass beyond), the lamps of the site and aircraft warning
## lights that come on at night, the weather (cloud, rain, storm, snow, fog, with wind), the
## ambient sounds that go with the hour and the weather, and the quality of the picture.
## The settings are kept between runs (user://environment.cfg).
class_name PlantEnvironment
extends Node3D

enum Weather { CLEAR, CLOUDY, RAIN, STORM, SNOW, FOG }
const WEATHER_NAMES := ["Clear", "Cloudy", "Rain", "Storm", "Snow", "Fog"]
enum Quality { LOW, MEDIUM, HIGH }
const QUALITY_NAMES := ["Low", "Medium", "High"]

## Southern latitude of a Brazilian plant, degrees; the plot plan's Y is north.
const LATITUDE := -22.9
const SETTINGS := "user://environment.cfg"

var main: Node3D
var env: Environment
var sun: DirectionalLight3D
var moon: DirectionalLight3D
var flash: DirectionalLight3D
var sky_mat: PhysicalSkyMaterial

var hour := 10.0
## Hours of plant time per second of real time; 0 keeps the hour.
var cycle_speed := 0.0
var weather := Weather.CLEAR
var wind := 3.0
var quality := Quality.MEDIUM
## Whether the window asks the system for HDR output, on a display that has it.
var hdr_display := false
## Whether every item shows its tag, or only the selected ones (L, or All labels in the panel).
var all_labels := false
var _labels_box: CheckBox
var sound_on := true
var volume := 0.6
## Seconds the flyby stays on each unit (B in the window, and the recorded videos).
var flyby_hold := 4.0

var pad: MeshInstance3D
var gravel: MeshInstance3D
var _ground_mat: StandardMaterial3D
var _pad_mat: StandardMaterial3D
var _gravel_mat: StandardMaterial3D
var _beacons_root: Node3D
var _rain: GPUParticles3D
var _snow: GPUParticles3D
var _players := {}
var _next_flash := 8.0
var _flash_time := -1.0
var _night := false
var _panel: PanelContainer
var _time_slider: HSlider
var _time_label: Label
var _updating := false


func _init(owner_main: Node3D, environment: Environment, sun_light: DirectionalLight3D) -> void:
	main = owner_main
	env = environment
	sun = sun_light


func _ready() -> void:
	sky_mat = PhysicalSkyMaterial.new()
	sky_mat.night_sky = _stars()
	sky_mat.ground_color = Color(0.32, 0.33, 0.3)
	sky_mat.energy_multiplier = 1.0
	env.sky.sky_material = sky_mat
	env.ambient_light_sky_contribution = 1.0

	moon = DirectionalLight3D.new()
	moon.light_color = Color(0.62, 0.7, 0.95)
	moon.shadow_enabled = true
	moon.directional_shadow_mode = DirectionalLight3D.SHADOW_PARALLEL_2_SPLITS
	moon.sky_mode = DirectionalLight3D.SKY_MODE_LIGHT_ONLY
	add_child(moon)
	flash = DirectionalLight3D.new()
	flash.light_color = Color(0.85, 0.88, 1.0)
	flash.light_energy = 0.0
	flash.sky_mode = DirectionalLight3D.SKY_MODE_LIGHT_ONLY
	flash.rotation_degrees = Vector3(-70, 30, 0)
	add_child(flash)

	_ground_mat = _textured("grass", 6.0)
	_pad_mat = _textured("concrete", 3.0)
	_gravel_mat = _textured("gravel", 2.5)
	main.ground.material_override = _ground_mat
	pad = MeshInstance3D.new()
	pad.mesh = PlaneMesh.new()
	pad.material_override = _pad_mat
	add_child(pad)
	gravel = MeshInstance3D.new()
	gravel.mesh = PlaneMesh.new()
	gravel.material_override = _gravel_mat
	add_child(gravel)
	_beacons_root = Node3D.new()
	add_child(_beacons_root)
	_rain = _particles(true)
	_snow = _particles(false)
	for layer in ["wind", "birds_day", "crickets_night", "rain", "storm"]:
		var p := AudioStreamPlayer.new()
		var stream = load("res://assets/sounds/%s.ogg" % layer)
		if stream is AudioStreamOggVorbis:
			stream.loop = true
		p.stream = stream
		p.volume_db = -80.0
		add_child(p)
		_players[layer] = p
	_load_settings()
	_apply_args()
	set_quality(quality)
	set_hdr_display(hdr_display)
	_apply()


# --- settings --------------------------------------------------------------------------------

func _load_settings() -> void:
	var cfg := ConfigFile.new()
	if cfg.load(SETTINGS) != OK:
		if RenderingServer.get_current_rendering_method() == "gl_compatibility":
			quality = Quality.LOW
		return
	hour = float(cfg.get_value("env", "hour", hour))
	cycle_speed = float(cfg.get_value("env", "cycle_speed", cycle_speed))
	weather = int(cfg.get_value("env", "weather", weather))
	quality = int(cfg.get_value("env", "quality", quality))
	sound_on = bool(cfg.get_value("env", "sound", sound_on))
	volume = float(cfg.get_value("env", "volume", volume))
	flyby_hold = clampf(float(cfg.get_value("env", "flyby_hold", flyby_hold)), 1.0, 30.0)
	hdr_display = bool(cfg.get_value("env", "hdr_display", hdr_display))
	all_labels = bool(cfg.get_value("env", "all_labels", all_labels))


func save_settings() -> void:
	var cfg := ConfigFile.new()
	cfg.set_value("env", "hour", hour)
	cfg.set_value("env", "cycle_speed", cycle_speed)
	cfg.set_value("env", "weather", weather)
	cfg.set_value("env", "quality", quality)
	cfg.set_value("env", "sound", sound_on)
	cfg.set_value("env", "volume", volume)
	cfg.set_value("env", "flyby_hold", flyby_hold)
	cfg.set_value("env", "hdr_display", hdr_display)
	cfg.set_value("env", "all_labels", all_labels)
	cfg.save(SETTINGS)


## "--time=21.5 --weather=rain --quality=high" (for pictures) win over the saved settings.
func _apply_args() -> void:
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--time="):
			hour = clampf(float(arg.get_slice("=", 1)), 0.0, 24.0)
			cycle_speed = 0.0
		elif arg.begins_with("--weather="):
			var w := WEATHER_NAMES.find(arg.get_slice("=", 1).capitalize())
			if w >= 0:
				weather = w
		elif arg.begins_with("--quality="):
			var q := QUALITY_NAMES.find(arg.get_slice("=", 1).capitalize())
			if q >= 0:
				quality = q
		elif arg == "--mute":
			sound_on = false
		elif arg == "--all-labels":
			all_labels = true


# --- materials and pieces --------------------------------------------------------------------

func _textured(name: String, tile_m: float) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_texture = load("res://assets/textures/%s_albedo.jpg" % name)
	m.normal_enabled = true
	m.normal_texture = load("res://assets/textures/%s_normal.jpg" % name)
	m.roughness_texture = load("res://assets/textures/%s_roughness.jpg" % name)
	m.roughness_texture_channel = BaseMaterial3D.TEXTURE_CHANNEL_RED
	m.uv1_triplanar = true
	m.uv1_world_triplanar = true
	m.uv1_scale = Vector3.ONE / tile_m
	m.texture_filter = BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC
	m.set_meta("albedo", m.albedo_color)
	return m


## A sky of small stars for the night (the physical sky shows it when the sun is down).
func _stars() -> ImageTexture:
	var img := Image.create_empty(1024, 512, false, Image.FORMAT_RGB8)
	img.fill(Color(0.004, 0.006, 0.014))
	var rng := RandomNumberGenerator.new()
	rng.seed = 7
	for i in 1800:
		var b := rng.randf_range(0.25, 1.0)
		var c := Color(b, b, b * rng.randf_range(0.9, 1.1))
		img.set_pixel(rng.randi_range(0, 1023), rng.randi_range(0, 300), c)
	return ImageTexture.create_from_image(img)


func _particles(rain: bool) -> GPUParticles3D:
	var p := GPUParticles3D.new()
	var pm := ParticleProcessMaterial.new()
	pm.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_BOX
	pm.direction = Vector3(0, -1, 0)
	pm.spread = 3.0 if rain else 15.0
	pm.initial_velocity_min = 14.0 if rain else 0.8
	pm.initial_velocity_max = 18.0 if rain else 1.6
	pm.gravity = Vector3(0, -9.8, 0) if rain else Vector3(0, -0.6, 0)
	pm.particle_flag_align_y = rain
	if not rain:
		pm.turbulence_enabled = true
		pm.turbulence_noise_strength = 0.6
	p.process_material = pm
	var mesh: Mesh
	var mat := StandardMaterial3D.new()
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	if rain:
		var box := BoxMesh.new()
		box.size = Vector3(0.012, 0.7, 0.012)
		mesh = box
		mat.albedo_color = Color(0.78, 0.84, 0.92, 0.45)
	else:
		var s := SphereMesh.new()
		s.radius = 0.035
		s.height = 0.07
		s.radial_segments = 6
		s.rings = 3
		mesh = s
		mat.albedo_color = Color(1, 1, 1, 0.95)
	mesh.surface_set_material(0, mat)
	p.draw_pass_1 = mesh
	p.lifetime = 2.6 if rain else 14.0
	p.preprocess = p.lifetime
	p.amount = 6000 if rain else 3000
	p.visibility_aabb = AABB(Vector3(-400, -100, -400), Vector3(800, 200, 800))
	p.emitting = false
	p.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(p)
	return p


## The pad, the gravel band and the warning lights, for the plant's bounds.
func fit(box: AABB) -> void:
	var c := box.get_center()
	var y := box.position.y
	(pad.mesh as PlaneMesh).size = Vector2(box.size.x + 8.0, box.size.z + 8.0)
	pad.position = Vector3(c.x, y + 0.012, c.z)
	(gravel.mesh as PlaneMesh).size = Vector2(box.size.x + 30.0, box.size.z + 30.0)
	gravel.position = Vector3(c.x, y + 0.006, c.z)
	_place_beacons()
	_apply()


var _lamps_on := false


## Lights the lamps and the windows of the site at night, and puts them out by day; called again
## whenever the site is built anew.
func relight() -> void:
	for n in get_tree().get_nodes_in_group("lamp_light"):
		(n as SpotLight3D).light_energy = 3.5 if _lamps_on else 0.0
	# the lamp posts are objects of the site (site_objects.gd)
	for head in get_tree().get_nodes_in_group("lamp_head"):
		if head.has_meta("lamp"):
			(head.get_meta("lamp") as StandardMaterial3D).emission_energy_multiplier = 8.0 if _night else 0.0
	_beacons_root.visible = _night
	for n in get_tree().get_nodes_in_group("night_glow"):
		var gm := (n as MeshInstance3D).material_override as StandardMaterial3D
		if gm != null:
			gm.emission_energy_multiplier = 1.8 * float(gm.get_meta("night_glow", 1.5)) if _night else 0.0


## A blinking red light on top of each item taller than 15 m, lit at night.
func _place_beacons() -> void:
	for c in _beacons_root.get_children():
		c.queue_free()
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.9, 0.1, 0.08)
	mat.emission_enabled = true
	mat.emission = Color(1.0, 0.12, 0.08)
	mat.emission_energy_multiplier = 10.0
	for id in main.entries.keys():
		var e: Dictionary = main.entries[id]
		if e["kind"] != "item" or bool(e["data"].get("badge", false)):
			continue
		var b: AABB = main._node_bounds(e["node"])
		if b.size.y < 15.0:
			continue
		var s := MeshInstance3D.new()
		var sm := SphereMesh.new()
		sm.radius = 0.18
		sm.height = 0.36
		s.mesh = sm
		s.material_override = mat
		s.position = Vector3(b.get_center().x, b.end.y + 0.25, b.get_center().z)
		s.add_to_group("blink")
		s.set_meta("rate", 0.5)
		s.set_meta("phase", 0.0)
		_beacons_root.add_child(s)


# --- the hour and the weather ----------------------------------------------------------------

func set_hour(h: float) -> void:
	hour = fposmod(h, 24.0)
	_apply()


func set_weather(w: int) -> void:
	weather = w
	_apply()
	save_settings()


## Shows the tags of all items, or only of the selected ones.
func set_all_labels(on: bool) -> void:
	all_labels = on
	if _labels_box != null:
		_labels_box.set_pressed_no_signal(on)
	main.refresh_labels()


## Whether the system can show this window in HDR (Godot asks the display driver).
func hdr_supported() -> bool:
	return DisplayServer.has_feature(DisplayServer.FEATURE_HDR_OUTPUT) and DisplayServer.window_is_hdr_output_supported()


## Asks for HDR output, or goes back to the usual range; the choice is kept even where the display
## cannot do it, for the next display that can.
func set_hdr_display(on: bool) -> void:
	hdr_display = on
	if hdr_supported():
		main.get_window().hdr_output_requested = on


func set_quality(q: int) -> void:
	quality = q
	var vp := main.get_viewport()
	match q:
		Quality.LOW:
			vp.msaa_3d = Viewport.MSAA_DISABLED
			env.ssao_enabled = false
			env.ssil_enabled = false
			env.glow_enabled = false
			RenderingServer.directional_shadow_atlas_set_size(2048, true)
		Quality.MEDIUM:
			vp.msaa_3d = Viewport.MSAA_2X
			env.ssao_enabled = true
			env.ssil_enabled = false
			env.glow_enabled = true
			RenderingServer.directional_shadow_atlas_set_size(4096, true)
		Quality.HIGH:
			vp.msaa_3d = Viewport.MSAA_4X
			env.ssao_enabled = true
			env.ssil_enabled = true
			env.glow_enabled = true
			RenderingServer.directional_shadow_atlas_set_size(8192, true)
	if main.builder != null:
		main.builder.set_detail(q != Quality.LOW)
	var k := 0.35 if q == Quality.LOW else (1.0 if q == Quality.MEDIUM else 1.5)
	_rain.amount = int(6000 * k)
	_snow.amount = int(3000 * k)
	_apply()


## The sun's elevation and azimuth (degrees, azimuth from north towards east) at an hour, at the
## equinox, for the plant's latitude.
static func sun_angles(h: float) -> Vector2:
	var lat := deg_to_rad(LATITUDE)
	var hour_angle := deg_to_rad((h - 12.0) * 15.0)
	var el := asin(cos(lat) * cos(hour_angle))
	var az := atan2(sin(hour_angle), cos(hour_angle) * sin(lat))
	return Vector2(rad_to_deg(el), fposmod(rad_to_deg(az) + 180.0, 360.0))


## A light shining from elevation and azimuth (DWSIM frame: Y north, X east, Z up).
static func aim(light: DirectionalLight3D, el_deg: float, az_deg: float) -> void:
	var el := deg_to_rad(el_deg)
	var az := deg_to_rad(az_deg)
	var to_light := CameraRig.g(Vector3(cos(el) * sin(az), cos(el) * cos(az), sin(el)))
	light.basis = Basis.looking_at(-to_light, Vector3.UP if absf(to_light.y) < 0.99 else Vector3.FORWARD)


func _apply() -> void:
	if sky_mat == null:
		return
	var a := sun_angles(hour)
	var el := a.x
	var cloud: float = [0.0, 0.55, 0.7, 0.85, 0.6, 0.5][weather]
	var day := smoothstep(-6.0, 12.0, el)
	_night = el < -2.0

	aim(sun, maxf(el, 0.5), a.y)
	var warm := Color(1.0, 0.62, 0.35).lerp(Color(1.0, 0.97, 0.92), smoothstep(2.0, 30.0, el))
	sun.light_color = warm
	sun.light_energy = smoothstep(-1.0, 10.0, el) * 1.3 * (1.0 - 0.75 * cloud)
	sun.visible = el > -1.0
	sun.shadow_enabled = quality != Quality.LOW or el > 5.0
	aim(moon, 45.0, fposmod(a.y + 180.0, 360.0))
	moon.light_energy = (1.0 - day) * 0.22 * (1.0 - 0.6 * cloud)
	moon.visible = moon.light_energy > 0.01

	sky_mat.turbidity = lerpf(10.0, 40.0, cloud)
	sky_mat.mie_coefficient = lerpf(0.005, 0.05, cloud)
	sky_mat.rayleigh_coefficient = lerpf(2.0, 0.6, cloud)
	sky_mat.energy_multiplier = lerpf(1.0, 0.55, cloud)
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.ambient_light_color = Color(0.32, 0.38, 0.55)
	env.ambient_light_sky_contribution = lerpf(0.55, 1.0, day)
	env.ambient_light_energy = lerpf(0.25, 0.9, day) * lerpf(1.0, 0.8, cloud)
	# ACES keeps the sunlit concrete near white at 0.9; the night takes more exposure
	env.tonemap_exposure = lerpf(1.3, 0.9, day)

	env.fog_light_color = Color(0.62, 0.66, 0.7).lerp(Color(0.05, 0.06, 0.09), 1.0 - day)
	env.fog_sky_affect = 0.5
	env.volumetric_fog_enabled = false
	_fog()

	var raining := weather == Weather.RAIN or weather == Weather.STORM
	_rain.emitting = raining
	_snow.emitting = weather == Weather.SNOW
	wind = [2.0, 5.0, 6.0, 14.0, 4.0, 1.0][weather]
	(_rain.process_material as ParticleProcessMaterial).direction = Vector3(wind * 0.03, -1, 0).normalized()
	(_snow.process_material as ParticleProcessMaterial).gravity = Vector3(wind * 0.08, -0.6, 0)

	# wet ground is darker and shinier; snow covers it
	for m in [_ground_mat, _pad_mat, _gravel_mat]:
		var base: Color = Color.WHITE
		if raining:
			m.albedo_color = base * 0.62
			m.roughness = 0.35
		elif weather == Weather.SNOW:
			m.albedo_color = base.lerp(Color(2.2, 2.25, 2.4), 0.75)
			m.roughness = 0.8
		else:
			m.albedo_color = base
			m.roughness = 1.0

	_lamps_on = _night or (weather == Weather.FOG and el < 8.0)
	relight()
	if _time_label != null:
		_time_label.text = "%02d:%02d" % [int(hour), int(fmod(hour, 1.0) * 60.0)]


## How much the weather veils the plant: a fraction of the light lost on the way to the camera.
## The camera of the fixed views stands far back, so the density comes from that distance, and
## the veil is the same whatever the zoom.
func _fog() -> void:
	var veil: float = [0.0, 0.0, 0.18, 0.3, 0.22, 0.62][weather]
	env.fog_enabled = veil > 0.0
	if veil > 0.0:
		env.fog_density = -log(1.0 - veil) / maxf(main.rig.distance, 1.0)


var _last_size := -1.0


## Drops and flakes a couple of pixels across at any zoom.
func _scale_particles() -> void:
	var size: float = main.rig.view_size
	if absf(size - _last_size) < _last_size * 0.1:
		return
	_last_size = size
	var k := size / 900.0
	var drop := _rain.draw_pass_1 as BoxMesh
	drop.size = Vector3(maxf(0.02, 1.6 * k), maxf(0.8, 18.0 * k), maxf(0.02, 1.6 * k))
	var flake := _snow.draw_pass_1 as SphereMesh
	flake.radius = maxf(0.04, 3.6 * k)
	flake.height = flake.radius * 2.0
	_fog()


func _process(delta: float) -> void:
	if cycle_speed > 0.0:
		hour = fposmod(hour + cycle_speed * delta, 24.0)
		_apply()
		if _time_slider != null:
			_updating = true
			_time_slider.value = hour
			_updating = false
	# the weather follows the view
	var t: Vector3 = main.rig.target
	var span: float = main.rig.view_size * 1.3 + 20.0
	for p in [_rain, _snow]:
		p.position = Vector3(t.x, t.y + 28.0, t.z)
		(p.process_material as ParticleProcessMaterial).emission_box_extents = Vector3(span, 6.0, span)
	_scale_particles()
	_storm(delta)
	_sounds(delta)


func _storm(delta: float) -> void:
	if weather != Weather.STORM:
		flash.light_energy = 0.0
		return
	_next_flash -= delta
	if _next_flash <= 0.0:
		_next_flash = randf_range(6.0, 16.0)
		_flash_time = 0.0
	if _flash_time >= 0.0:
		_flash_time += delta
		# two quick strokes
		var on := (_flash_time < 0.08) or (_flash_time > 0.16 and _flash_time < 0.24)
		flash.light_energy = 3.0 if on else 0.0
		if _flash_time > 0.3:
			_flash_time = -1.0


func _sounds(delta: float) -> void:
	var el := sun_angles(hour).x
	var day := smoothstep(-4.0, 6.0, el)
	var target := {
		"wind": clampf(wind / 14.0, 0.15, 1.0),
		"birds_day": day * (1.0 if weather in [Weather.CLEAR, Weather.CLOUDY] else 0.0),
		"crickets_night": (1.0 - day) * (1.0 if weather in [Weather.CLEAR, Weather.CLOUDY, Weather.FOG] else 0.0),
		"rain": 1.0 if weather == Weather.RAIN else 0.0,
		"storm": 1.0 if weather == Weather.STORM else 0.0,
	}
	for layer in _players.keys():
		var p: AudioStreamPlayer = _players[layer]
		if p.stream == null:
			continue
		var want: float = target[layer] * volume if sound_on else 0.0
		var now := db_to_linear(p.volume_db)
		now = move_toward(now, want, delta * 0.4)
		p.volume_db = linear_to_db(maxf(now, 0.0001))
		if now > 0.001 and not p.playing:
			p.play()
			print_verbose("sound: %s starts" % layer)
		elif now <= 0.001 and p.playing:
			p.stop()
			print_verbose("sound: %s stops" % layer)


# --- the panel -------------------------------------------------------------------------------

func build_panel(style: StyleBox) -> PanelContainer:
	_panel = PanelContainer.new()
	_panel.add_theme_stylebox_override("panel", style)
	var g := GridContainer.new()
	g.columns = 2
	_panel.add_child(g)
	_label(g, "Hour")
	var row := HBoxContainer.new()
	row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	g.add_child(row)
	_time_slider = HSlider.new()
	_time_slider.min_value = 0.0
	_time_slider.max_value = 24.0
	_time_slider.step = 0.25
	_time_slider.value = hour
	_time_slider.custom_minimum_size = Vector2(90, 0)
	_time_slider.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_time_slider.focus_mode = Control.FOCUS_NONE
	_time_slider.value_changed.connect(func(v):
		if not _updating:
			set_hour(v)
			save_settings())
	row.add_child(_time_slider)
	_time_label = Label.new()
	_time_label.add_theme_color_override("font_color", Color(0.11, 0.15, 0.2))
	row.add_child(_time_label)
	_label(g, "Day cycle")
	var cyc := CheckBox.new()
	cyc.button_pressed = cycle_speed > 0.0
	cyc.focus_mode = Control.FOCUS_NONE
	cyc.text = "24 h in 4 min"
	cyc.add_theme_color_override("font_color", Color(0.11, 0.15, 0.2))
	cyc.toggled.connect(func(on):
		cycle_speed = 0.1 if on else 0.0
		save_settings())
	g.add_child(cyc)
	_label(g, "Weather")
	g.add_child(_options(WEATHER_NAMES, weather, func(i): set_weather(i)))
	_label(g, "Quality")
	g.add_child(_options(QUALITY_NAMES, quality, func(i):
		set_quality(i)
		save_settings()))
	_label(g, "Labels")
	_labels_box = CheckBox.new()
	_labels_box.focus_mode = Control.FOCUS_NONE
	_labels_box.button_pressed = all_labels
	_labels_box.text = "all (L)"
	_labels_box.add_theme_color_override("font_color", Color(0.11, 0.15, 0.2))
	_labels_box.tooltip_text = "Shows the tag of every item. When it is off, only the selected items show their tags."
	_labels_box.toggled.connect(func(on):
		set_all_labels(on)
		save_settings())
	g.add_child(_labels_box)
	_label(g, "HDR display")
	var hdr := CheckBox.new()
	hdr.focus_mode = Control.FOCUS_NONE
	hdr.button_pressed = hdr_display and hdr_supported()
	hdr.disabled = not hdr_supported()
	hdr.text = "on" if hdr_supported() else "not on this display"
	hdr.add_theme_color_override("font_color", Color(0.11, 0.15, 0.2))
	hdr.tooltip_text = "Sends the picture to the display in high dynamic range, so lamps, flames and the sun can be brighter than white."
	hdr.toggled.connect(func(on):
		set_hdr_display(on)
		save_settings())
	g.add_child(hdr)
	_label(g, "Sound")
	var srow := HBoxContainer.new()
	srow.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	g.add_child(srow)
	var snd := CheckBox.new()
	snd.button_pressed = sound_on
	snd.focus_mode = Control.FOCUS_NONE
	snd.toggled.connect(func(on):
		sound_on = on
		save_settings())
	srow.add_child(snd)
	var vol := HSlider.new()
	vol.min_value = 0.0
	vol.max_value = 1.0
	vol.step = 0.05
	vol.value = volume
	vol.custom_minimum_size = Vector2(80, 0)
	vol.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	vol.focus_mode = Control.FOCUS_NONE
	vol.value_changed.connect(func(v):
		volume = v
		save_settings())
	srow.add_child(vol)
	_label(g, "Flyby stop")
	var frow := HBoxContainer.new()
	frow.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	g.add_child(frow)
	var hold := SpinBox.new()
	hold.min_value = 1.0
	hold.max_value = 30.0
	hold.step = 0.5
	hold.value = flyby_hold
	hold.suffix = "s per unit"
	hold.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	hold.value_changed.connect(func(v):
		flyby_hold = v
		save_settings())
	frow.add_child(hold)
	_apply()
	return _panel


func _label(parent: Control, text: String) -> void:
	var l := Label.new()
	l.text = text
	l.add_theme_color_override("font_color", Color(0.27, 0.32, 0.38))
	parent.add_child(l)


func _options(names: Array, selected: int, picked: Callable) -> OptionButton:
	var o := OptionButton.new()
	o.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	for n in names:
		o.add_item(n)
	o.select(selected)
	o.focus_mode = Control.FOCUS_NONE
	o.item_selected.connect(picked)
	return o


## N jumps between noon and night, W steps through the weather, M turns the sound off and on.
func handle_key(k: int) -> bool:
	match k:
		KEY_N:
			set_hour(22.0 if not _night else 12.0)
			if _time_slider != null:
				_updating = true
				_time_slider.value = hour
				_updating = false
			save_settings()
			return true
		KEY_W:
			set_weather((weather + 1) % WEATHER_NAMES.size())
			return true
		KEY_M:
			sound_on = not sound_on
			save_settings()
			return true
	return false
