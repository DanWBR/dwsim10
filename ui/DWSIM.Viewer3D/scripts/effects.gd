## What building and demolishing look and sound like, in the manner of SimCity 3000. A new object
## (of the site or of the plant) rises out of the ground with a small bounce, a cloud of dust and a
## thud; a demolished one sinks under the sound of a bulldozer, with dust and rubble and a crunch;
## the terrain brushes rumble like earthmoving machines and throw up dust where they work; and the
## buttons of the build bar click. The sounds are made here (no recordings) and follow the sound
## setting of the window. Changes of many objects at once (a scene that loads, the site adopted)
## pass quietly.
class_name SiteEffects
extends Node3D

const MAX_AT_ONCE := 6

var main: Node3D
## What happened, for the self-test: "built <id>", "demolished <id>".
var events: Array = []
var _streams := {}
var _dust: GradientTexture2D
var _ui: AudioStreamPlayer
var _rumble: AudioStreamPlayer3D
var _brush_dust: GPUParticles3D


func _init(owner_main: Node3D) -> void:
	main = owner_main


func _ready() -> void:
	_dust = PlantLife._radial(Color(1, 1, 1, 0.9))
	_streams["thud"] = _thud()
	_streams["click"] = _click()
	_streams["dozer"] = _dozer()
	_streams["crunch"] = _crunch()
	_ui = AudioStreamPlayer.new()
	_ui.stream = _streams["click"]
	add_child(_ui)
	_rumble = AudioStreamPlayer3D.new()
	_rumble.stream = _streams["dozer"]
	_rumble.unit_size = 14.0
	_rumble.max_distance = 260.0
	add_child(_rumble)
	_brush_dust = _dust_cloud(Vector3(4, 0.5, 4), 24, false)
	_brush_dust.emitting = false
	add_child(_brush_dust)


func _gain() -> float:
	var env: PlantEnvironment = main.surroundings
	return env.volume if env != null and env.sound_on else 0.0


func _play_at(kind: String, at: Vector3, db := 0.0) -> void:
	var g := _gain()
	if g <= 0.01:
		return
	var p := AudioStreamPlayer3D.new()
	p.stream = _streams[kind]
	p.unit_size = 16.0
	p.max_distance = 300.0
	p.volume_db = db + linear_to_db(g)
	p.position = at
	add_child(p)
	p.play()
	p.finished.connect(p.queue_free)


## A soft click for a button of the build bar.
func click() -> void:
	var g := _gain()
	if g <= 0.01:
		return
	_ui.volume_db = -14.0 + linear_to_db(g)
	_ui.play()


# --- building and demolishing ------------------------------------------------------------------

## Whether a change is small enough to play: a few objects, not a scene that loads.
static func quiet(count: int) -> bool:
	return count > MAX_AT_ONCE


## A new object: it rises out of the ground, with dust and a thud.
func built(id: String) -> void:
	if not main.entries.has(id):
		return
	var e: Dictionary = main.entries[id]
	var node: Node3D = e["node"]
	if node == null or not is_instance_valid(node):
		return
	var b: AABB = main._node_bounds(node)
	var full := node.scale
	node.scale = Vector3(full.x, full.y * 0.02, full.z)
	var tw := create_tween()
	tw.tween_property(node, "scale", full, 0.55).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	var foot := Vector3(maxf(b.size.x, 1.0), 0.5, maxf(b.size.z, 1.0))
	var at := Vector3(b.get_center().x, b.position.y, b.get_center().z)
	_burst(_dust_cloud(foot, clampi(int(foot.x * foot.z), 16, 72), true), at + Vector3(0, 0.8, 0))
	_play_at("thud", at, -2.0)
	events.append("built " + id)


## An object taken away: it sinks into the ground under the bulldozer, with dust, rubble and a crunch.
## The node is the window's; it is freed here when it is gone.
func demolished(id: String, node: Node3D) -> void:
	if node == null or not is_instance_valid(node):
		return
	var b: AABB = main._node_bounds(node)
	var t := node.global_transform
	node.get_parent().remove_child(node)
	add_child(node)
	node.global_transform = t
	var tw := create_tween()
	tw.tween_property(node, "scale", Vector3(node.scale.x, node.scale.y * 0.02, node.scale.z), 0.4).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)
	tw.tween_callback(node.queue_free)
	var foot := Vector3(maxf(b.size.x, 1.0), 0.5, maxf(b.size.z, 1.0))
	var at := Vector3(b.get_center().x, b.position.y, b.get_center().z)
	_burst(_dust_cloud(foot, clampi(int(foot.x * foot.z), 20, 90), true), at + Vector3(0, 0.8, 0))
	_burst(_rubble(foot), at + Vector3(0, minf(b.size.y, 3.0) * 0.5, 0))
	_play_at("crunch", at, -3.0)
	_play_at("dozer", at, -8.0)
	events.append("demolished " + id)


func _burst(p: GPUParticles3D, at: Vector3) -> void:
	p.position = at
	add_child(p)
	p.restart()
	p.emitting = true
	get_tree().create_timer(p.lifetime + 1.0).timeout.connect(p.queue_free)


## A cloud of dust over a footprint (x, z, m).
func _dust_cloud(foot: Vector3, amount: int, once: bool) -> GPUParticles3D:
	var p := GPUParticles3D.new()
	p.amount = amount
	p.lifetime = 1.6
	p.one_shot = once
	p.explosiveness = 0.85 if once else 0.0
	p.local_coords = false
	var pm := ParticleProcessMaterial.new()
	pm.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_BOX
	pm.emission_box_extents = Vector3(foot.x / 2.0, 0.2, foot.z / 2.0)
	pm.direction = Vector3(0, 1, 0)
	pm.spread = 70.0
	pm.initial_velocity_min = 0.6
	pm.initial_velocity_max = 2.2
	pm.gravity = Vector3(0, -0.4, 0)
	pm.damping_min = 1.0
	pm.damping_max = 2.0
	pm.scale_min = 0.8
	pm.scale_max = 1.6
	var grow := Curve.new()
	grow.add_point(Vector2(0, 0.4))
	grow.add_point(Vector2(1, 1.6))
	var gt := CurveTexture.new()
	gt.curve = grow
	pm.scale_curve = gt
	var g := Gradient.new()
	g.set_color(0, Color(0.6, 0.52, 0.42, 0.9))
	g.add_point(0.4, Color(0.66, 0.59, 0.5, 0.7))
	g.set_color(g.get_point_count() - 1, Color(0.7, 0.64, 0.56, 0.0))
	var ramp := GradientTexture1D.new()
	ramp.gradient = g
	pm.color_ramp = ramp
	p.process_material = pm
	# puffs as big as a quarter of the footprint, between 1.5 and 5 m
	var puff := clampf(maxf(foot.x, foot.z) / 4.0, 1.5, 5.0)
	var q := QuadMesh.new()
	q.size = Vector2(puff, puff)
	var m := StandardMaterial3D.new()
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	m.billboard_mode = BaseMaterial3D.BILLBOARD_PARTICLES
	m.vertex_color_use_as_albedo = true
	m.albedo_texture = _dust
	# soft where a puff meets a wall or the ground
	m.proximity_fade_enabled = true
	m.proximity_fade_distance = 1.2
	q.material = m
	p.draw_pass_1 = q
	p.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	return p


## Chunks of rubble thrown up and falling back.
func _rubble(foot: Vector3) -> GPUParticles3D:
	var p := GPUParticles3D.new()
	p.amount = clampi(int(foot.x * foot.z / 2.0), 10, 48)
	p.lifetime = 1.2
	p.one_shot = true
	p.explosiveness = 0.9
	p.local_coords = false
	var pm := ParticleProcessMaterial.new()
	pm.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_BOX
	pm.emission_box_extents = Vector3(foot.x / 2.0, 0.5, foot.z / 2.0)
	pm.direction = Vector3(0, 1, 0)
	pm.spread = 40.0
	pm.initial_velocity_min = 2.0
	pm.initial_velocity_max = 4.5
	pm.gravity = Vector3(0, -9.8, 0)
	pm.angular_velocity_min = -360.0
	pm.angular_velocity_max = 360.0
	pm.scale_min = 0.6
	pm.scale_max = 1.4
	p.process_material = pm
	var box := BoxMesh.new()
	box.size = Vector3(0.22, 0.16, 0.18)
	var m := StandardMaterial3D.new()
	m.albedo_color = Color(0.55, 0.53, 0.5)
	m.roughness = 0.95
	box.material = m
	p.draw_pass_1 = box
	return p


# --- the terrain brushes -------------------------------------------------------------------------

## The rumble and the dust of a terrain brush at work (on), or quiet (off).
func earthwork(on: bool, at = null, radius := 8.0) -> void:
	if not on or at == null:
		_brush_dust.emitting = false
		if _rumble.playing:
			_rumble.stop()
		return
	_brush_dust.position = at
	var pm := _brush_dust.process_material as ParticleProcessMaterial
	pm.emission_box_extents = Vector3(radius * 0.6, 0.2, radius * 0.6)
	_brush_dust.emitting = true
	_rumble.position = at
	var g := _gain()
	if g > 0.01:
		_rumble.volume_db = -6.0 + linear_to_db(g)
		if not _rumble.playing:
			_rumble.play()
	elif _rumble.playing:
		_rumble.stop()


# --- the sounds (no recordings) -----------------------------------------------------------------

## A heavy thud: a low tone falling from 90 to 45 Hz that dies away, a knock and a puff of noise; once.
static func _thud() -> AudioStreamWAV:
	var rate := 22050
	var n := int(rate * 0.6)
	var s := PackedFloat32Array()
	s.resize(n)
	var rng := RandomNumberGenerator.new()
	rng.seed = 41
	var phase := 0.0
	var lp := 0.0
	for i in n:
		var t := float(i) / rate
		var f := lerpf(90.0, 45.0, minf(1.0, t / 0.25))
		phase += TAU * f / rate
		lp = 0.9 * lp + 0.1 * rng.randf_range(-1.0, 1.0)
		s[i] = 0.75 * sin(phase) * exp(-t * 7.0) + 0.25 * sin(TAU * 180.0 * t) * exp(-t * 30.0) + 1.6 * lp * exp(-t * 18.0)
	var w := PlantLife._wav(s, rate)
	w.loop_mode = AudioStreamWAV.LOOP_DISABLED
	return w


## A short soft tick; once.
static func _click() -> AudioStreamWAV:
	var rate := 22050
	var n := int(rate * 0.05)
	var s := PackedFloat32Array()
	s.resize(n)
	for i in n:
		var t := float(i) / rate
		s[i] = 0.5 * sin(TAU * 2400.0 * t) * exp(-t * 160.0) + 0.3 * sin(TAU * 1200.0 * t) * exp(-t * 90.0)
	var w := PlantLife._wav(s, rate)
	w.loop_mode = AudioStreamWAV.LOOP_DISABLED
	return w


## A diesel engine of a bulldozer or a loader: the firing of the cylinders on a low drone, the
## rattle of the tracks and gravel under them; two seconds that loop.
static func _dozer() -> AudioStreamWAV:
	var rate := 22050
	var n := rate * 2
	var s := PackedFloat32Array()
	s.resize(n)
	var rng := RandomNumberGenerator.new()
	rng.seed = 42
	var b := 0.0
	var crunch := 0.0
	for i in n:
		var t := float(i) / rate
		# 12 firings a second, whole numbers of cycles in the loop so it joins cleanly
		var fire := 0.55 + 0.45 * maxf(0.0, sin(TAU * 12.0 * t))
		b = 0.85 * b + 0.15 * rng.randf_range(-1.0, 1.0)
		if rng.randf() < 0.0015:
			crunch = rng.randf_range(0.3, 0.6)
		crunch *= 0.995
		s[i] = 0.72 * (fire * (0.38 * sin(TAU * 38.0 * t) + 0.22 * sin(TAU * 76.0 * t) + 0.12 * sin(TAU * 114.0 * t)) \
			+ 0.35 * b * fire + crunch * rng.randf_range(-1.0, 1.0))
	return PlantLife._wav(s, rate)


## Concrete and steel crushed: a run of cracks and scrapes that dies away; once.
static func _crunch() -> AudioStreamWAV:
	var rate := 22050
	var n := int(rate * 0.9)
	var s := PackedFloat32Array()
	s.resize(n)
	var rng := RandomNumberGenerator.new()
	rng.seed = 43
	var hit := 0.0
	var lp := 0.0
	for i in n:
		var t := float(i) / rate
		if rng.randf() < 0.004 * exp(-t * 3.0) + 0.0004:
			hit = rng.randf_range(0.5, 1.0)
		hit *= 0.985
		lp = 0.7 * lp + 0.3 * rng.randf_range(-1.0, 1.0)
		s[i] = (hit * rng.randf_range(-1.0, 1.0) + 0.4 * lp * exp(-t * 2.5)) * 0.8
	var w := PlantLife._wav(s, rate)
	w.loop_mode = AudioStreamWAV.LOOP_DISABLED
	return w
