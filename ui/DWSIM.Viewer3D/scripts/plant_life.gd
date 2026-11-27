## What makes the plant look and sound alive: smoke from the stacks of fired heaters and a glow at
## their burners, the flame and smoke of flares, the steam plume of cooling towers, and the hum of
## pumps, the whine of compressors and the roar of burners and flares, heard around the middle of
## the view. The sounds are made here (no recordings): a few seconds of each, looped.
class_name PlantLife
extends Node3D

const MAX_VOICES := 14

var main: Node3D
var _effects_root: Node3D
var _voices_root: Node3D
var _listener: AudioListener3D
var _streams := {}
var _smoke_tex: GradientTexture2D
var _flame_tex: GradientTexture2D


func _init(owner_main: Node3D) -> void:
	main = owner_main


func _ready() -> void:
	_effects_root = Node3D.new()
	add_child(_effects_root)
	_voices_root = Node3D.new()
	add_child(_voices_root)
	_listener = AudioListener3D.new()
	add_child(_listener)
	_listener.make_current()
	_smoke_tex = _radial(Color(1, 1, 1, 0.8))
	_flame_tex = _radial(Color(1, 1, 1, 1))
	_streams["hum"] = _hum()
	_streams["whine"] = _whine()
	_streams["roar"] = _roar()


static func _radial(c: Color) -> GradientTexture2D:
	var g := Gradient.new()
	g.set_color(0, c)
	g.set_color(1, Color(c.r, c.g, c.b, 0.0))
	var t := GradientTexture2D.new()
	t.gradient = g
	t.fill = GradientTexture2D.FILL_RADIAL
	t.fill_from = Vector2(0.5, 0.5)
	t.fill_to = Vector2(0.5, 0.0)
	t.width = 64
	t.height = 64
	return t


# --- effects ---------------------------------------------------------------------------------

## Builds the effects and the voices again for the scene the window has now.
func refresh() -> void:
	for c in _effects_root.get_children():
		c.queue_free()
	for c in _voices_root.get_children():
		c.queue_free()
	for id in main.entries.keys():
		var e: Dictionary = main.entries[id]
		if e["kind"] != "item" or bool(e["data"].get("badge", false)):
			continue
		var node: Node3D = e["node"]
		var style := str(e["data"].get("style", ""))
		var top := _top(node)
		match style:
			"fired":
				_effects_root.add_child(_smoke(top, 0.6, Color(0.35, 0.35, 0.37), 9.0))
				_effects_root.add_child(_glow(main._node_bounds(node).get_center() - Vector3(0, main._node_bounds(node).size.y * 0.35, 0), 1.2))
				_voice(node, "roar", -10.0)
			"flare":
				_effects_root.add_child(_flame(top))
				_effects_root.add_child(_smoke(top + Vector3(0, 3.0, 0), 1.2, Color(0.2, 0.2, 0.22), 10.0))
				_effects_root.add_child(_glow(top + Vector3(0, 2.0, 0), 4.0))
				_voice(node, "roar", -2.0)
			"coolingtower":
				_effects_root.add_child(_smoke(top, 3.0, Color(0.95, 0.96, 0.98), 12.0))
		match str(e["data"].get("type", "")):
			"Pump":
				_voice(node, "hum", -14.0)
			"Compressor", "Expander":
				_voice(node, "whine", -12.0)


## The top of an item: the middle of the top face of its highest solid (a stack, a flare tip).
func _top(node: Node3D) -> Vector3:
	var best := Vector3.ZERO
	var best_y := -INF
	for mi in node.find_children("*", "MeshInstance3D", true, false):
		if mi.has_meta("flow"):
			continue
		var b: AABB = mi.global_transform * mi.get_aabb()
		if b.end.y > best_y:
			best_y = b.end.y
			best = Vector3(b.get_center().x, b.end.y, b.get_center().z)
	return best


func _billboard(tex: Texture2D, additive: bool) -> QuadMesh:
	var q := QuadMesh.new()
	var m := StandardMaterial3D.new()
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	m.blend_mode = BaseMaterial3D.BLEND_MODE_ADD if additive else BaseMaterial3D.BLEND_MODE_MIX
	m.billboard_mode = BaseMaterial3D.BILLBOARD_PARTICLES
	m.vertex_color_use_as_albedo = true
	m.albedo_texture = tex
	m.depth_draw_mode = BaseMaterial3D.DEPTH_DRAW_DISABLED
	q.material = m
	return q


func _smoke(at: Vector3, size: float, color: Color, rise: float) -> GPUParticles3D:
	var p := GPUParticles3D.new()
	p.position = at
	var pm := ParticleProcessMaterial.new()
	pm.direction = Vector3(0, 1, 0)
	pm.spread = 12.0
	pm.initial_velocity_min = rise * 0.25
	pm.initial_velocity_max = rise * 0.4
	pm.gravity = Vector3(0.6, 0.15, 0)
	pm.scale_min = size
	pm.scale_max = size * 1.6
	var grow := Curve.new()
	grow.add_point(Vector2(0, 0.4))
	grow.add_point(Vector2(1, 3.0))
	var ct := CurveTexture.new()
	ct.curve = grow
	pm.scale_curve = ct
	var ramp := Gradient.new()
	ramp.set_color(0, Color(color.r, color.g, color.b, 0.55))
	ramp.set_color(1, Color(color.r, color.g, color.b, 0.0))
	var gt := GradientTexture1D.new()
	gt.gradient = ramp
	pm.color_ramp = gt
	p.process_material = pm
	p.draw_pass_1 = _billboard(_smoke_tex, false)
	p.amount = 60
	p.lifetime = 7.0
	p.preprocess = 7.0
	p.visibility_aabb = AABB(Vector3(-40, -5, -40), Vector3(80, 80, 80))
	p.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	p.set_meta("drift", true)
	return p


func _flame(at: Vector3) -> GPUParticles3D:
	var p := GPUParticles3D.new()
	p.position = at
	var pm := ParticleProcessMaterial.new()
	pm.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_SPHERE
	pm.emission_sphere_radius = 0.3
	pm.direction = Vector3(0, 1, 0)
	pm.spread = 8.0
	pm.initial_velocity_min = 4.0
	pm.initial_velocity_max = 7.0
	pm.gravity = Vector3(0.8, 2.0, 0)
	pm.scale_min = 1.4
	pm.scale_max = 2.4
	var shrink := Curve.new()
	shrink.add_point(Vector2(0, 1.0))
	shrink.add_point(Vector2(1, 0.3))
	var ct := CurveTexture.new()
	ct.curve = shrink
	pm.scale_curve = ct
	var ramp := Gradient.new()
	ramp.set_color(0, Color(1.0, 0.95, 0.6, 1.0))
	ramp.add_point(0.4, Color(1.0, 0.55, 0.12, 0.9))
	ramp.set_color(ramp.get_point_count() - 1, Color(0.6, 0.12, 0.05, 0.0))
	var gt := GradientTexture1D.new()
	gt.gradient = ramp
	pm.color_ramp = gt
	p.process_material = pm
	p.draw_pass_1 = _billboard(_flame_tex, true)
	p.amount = 90
	p.lifetime = 0.9
	p.preprocess = 1.0
	p.visibility_aabb = AABB(Vector3(-15, -2, -15), Vector3(30, 30, 30))
	p.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	p.set_meta("drift", true)
	return p


## A warm light that flickers (burners, flames).
func _glow(at: Vector3, energy: float) -> OmniLight3D:
	var l := OmniLight3D.new()
	l.position = at
	l.light_color = Color(1.0, 0.55, 0.2)
	l.light_energy = energy
	l.omni_range = 6.0 + energy * 5.0
	l.shadow_enabled = false
	l.set_meta("flicker", energy)
	return l


func _process(_delta: float) -> void:
	var t := Time.get_ticks_msec() / 1000.0
	var wind: float = main.surroundings.wind if main.surroundings != null else 2.0
	for c in _effects_root.get_children():
		if c is GPUParticles3D:
			(c as GPUParticles3D).emitting = main.animate
			if c.has_meta("drift"):
				var pm := (c as GPUParticles3D).process_material as ParticleProcessMaterial
				pm.gravity = Vector3(wind * 0.25, pm.gravity.y, 0)
		elif c is OmniLight3D and c.has_meta("flicker"):
			var e: float = c.get_meta("flicker")
			(c as OmniLight3D).light_energy = e * (0.85 + 0.15 * sin(t * 13.0 + c.position.x) * sin(t * 7.3)) if main.animate else e
	# the ear is over the middle of the view, turned as the camera is
	var rig: CameraRig = main.rig
	_listener.global_transform = Transform3D(rig.camera.global_basis, rig.target + Vector3(0, 12, 0))
	_mix()


# --- sounds ----------------------------------------------------------------------------------

func _voice(node: Node3D, kind: String, db: float) -> void:
	var p := AudioStreamPlayer3D.new()
	p.stream = _streams[kind]
	p.position = main._node_bounds(node).get_center()
	p.unit_size = 14.0
	p.max_distance = 220.0
	p.attenuation_model = AudioStreamPlayer3D.ATTENUATION_INVERSE_DISTANCE
	p.set_meta("db", db)
	p.volume_db = -80.0
	_voices_root.add_child(p)


## Only the voices nearest the middle of the view play, at the volume of the sound setting.
func _mix() -> void:
	var env: PlantEnvironment = main.surroundings
	var on := env != null and env.sound_on
	var gain: float = env.volume if env != null else 0.0
	var centre: Vector3 = main.rig.target
	var voices := _voices_root.get_children()
	voices.sort_custom(func(a, b): return a.position.distance_squared_to(centre) < b.position.distance_squared_to(centre))
	var playing := 0
	for i in voices.size():
		var p: AudioStreamPlayer3D = voices[i]
		if p.playing:
			playing += 1
		var want := on and i < MAX_VOICES and gain > 0.01
		if want:
			p.volume_db = float(p.get_meta("db")) + linear_to_db(gain)
			if not p.playing:
				p.play(randf() * 0.9)
		elif p.playing:
			p.stop()
	if playing != _last_playing:
		_last_playing = playing
		print_verbose("plant sounds: %d of %d voices playing" % [playing, voices.size()])


var _last_playing := -1


static func _wav(samples: PackedFloat32Array, rate: int) -> AudioStreamWAV:
	var data := PackedByteArray()
	data.resize(samples.size() * 2)
	for i in samples.size():
		data.encode_s16(i * 2, int(clampf(samples[i], -1.0, 1.0) * 32767.0))
	var w := AudioStreamWAV.new()
	w.format = AudioStreamWAV.FORMAT_16_BITS
	w.mix_rate = rate
	w.stereo = false
	w.data = data
	w.loop_mode = AudioStreamWAV.LOOP_FORWARD
	w.loop_begin = 0
	w.loop_end = samples.size()
	return w


## A motor's hum: the mains frequency and its harmonics, a whole number of cycles in one second.
static func _hum() -> AudioStreamWAV:
	var rate := 22050
	var s := PackedFloat32Array()
	s.resize(rate)
	var rng := RandomNumberGenerator.new()
	rng.seed = 11
	for i in rate:
		var t := float(i) / rate
		s[i] = 0.32 * sin(TAU * 60.0 * t) + 0.22 * sin(TAU * 120.0 * t) + 0.12 * sin(TAU * 180.0 * t) \
			+ 0.08 * sin(TAU * 360.0 * t) + 0.04 * rng.randf_range(-1.0, 1.0)
	return _wav(s, rate)


## A compressor: the motor's hum, the tone of the blades and a hiss of gas.
static func _whine() -> AudioStreamWAV:
	var rate := 22050
	var s := PackedFloat32Array()
	s.resize(rate)
	var rng := RandomNumberGenerator.new()
	rng.seed = 12
	var hiss := 0.0
	for i in rate:
		var t := float(i) / rate
		hiss = 0.7 * hiss + 0.3 * rng.randf_range(-1.0, 1.0)
		s[i] = 0.22 * sin(TAU * 60.0 * t) + 0.12 * sin(TAU * 120.0 * t) \
			+ 0.16 * sin(TAU * 1180.0 * t) * (0.8 + 0.2 * sin(TAU * 4.0 * t)) + 0.05 * sin(TAU * 2360.0 * t) + 0.18 * hiss
	return _wav(s, rate)


## A burner's roar: low, rumbling noise, two seconds whose end fades into their start.
static func _roar() -> AudioStreamWAV:
	var rate := 22050
	var n := rate * 2
	var fade := rate / 4
	var raw := PackedFloat32Array()
	raw.resize(n + fade)
	var rng := RandomNumberGenerator.new()
	rng.seed = 13
	var b1 := 0.0
	var b2 := 0.0
	for i in raw.size():
		b1 = 0.985 * b1 + 0.015 * rng.randf_range(-1.0, 1.0)
		b2 = 0.8 * b2 + 0.2 * rng.randf_range(-1.0, 1.0)
		raw[i] = 5.0 * b1 + 0.12 * b2
	var s := PackedFloat32Array()
	s.resize(n)
	for i in n:
		s[i] = raw[i]
	for i in fade:
		var k := float(i) / fade
		s[i] = raw[i] * k + raw[n + i] * (1.0 - k)
	return _wav(s, rate)
