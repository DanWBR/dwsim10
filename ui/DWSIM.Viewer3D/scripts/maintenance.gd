## Maintenance: every so often a team comes out of a control room with its tools, walks round the
## plant to a unit (the ones in alarm or in error first), sets cones and tape round the place of the
## job and works there for a while: welding (a blue arc that flickers, sparks and the crackle of the
## arc, with a second person on fire watch holding an extinguisher), grinding (a fan of orange
## sparks and the whine of the disc), hammering (the swing and the clank of each blow) or tightening
## flange bolts (the clicks of a ratchet). When the job is done, the team takes the cones away and
## goes back in. The jobs go into the log of the Rounds panel. The sounds are made here.
class_name PlantMaintenance
extends Node3D

const MAX_JOBS := 2
const SPEED := 1.2
const TASKS := ["welding", "grinding", "hammering", "bolting"]
const WHAT := {
	"welding": "welding a pipe support",
	"grinding": "grinding a weld",
	"hammering": "straightening a bracket",
	"bolting": "tightening the flange bolts"}
const ANIM := {
	"welding": ["crouch", 1.0], "grinding": ["holding-right", 1.0],
	"hammering": ["attack-melee-right", 0.45], "bolting": ["interact-right", 0.55]}

var main: Node3D
var _jobs: Array = []
var _next := 8.0
var _count := 0
var _rng := RandomNumberGenerator.new()
var _streams := {}
var _dot: GradientTexture2D


func _init(owner_main: Node3D) -> void:
	main = owner_main
	_rng.seed = 23


func _ready() -> void:
	_dot = PlantLife._radial(Color(1, 1, 1, 1))
	_streams["welding"] = _arc()
	_streams["grinding"] = _grinder()
	_streams["hammering"] = _clank()
	_streams["bolting"] = _ratchet()


## Every team goes back in: the scene changed and the paths with it.
func clear() -> void:
	for j in _jobs:
		_drop(j)
	_jobs.clear()
	_next = 8.0


func _drop(j: Dictionary) -> void:
	for p in j["people"]:
		if is_instance_valid(p["node"]):
			p["node"].queue_free()
	for k in ["site", "fx", "bubble"]:
		if is_instance_valid(j[k]):
			j[k].queue_free()


# --- a job -----------------------------------------------------------------------------------

func _start() -> void:
	var crew: PlantCrew = main.crew
	var doors: Array = main.props.doors if main.props != null else []
	if crew == null or doors.is_empty():
		return
	var busy := _jobs.map(func(j): return j["id"])
	var units: Array = crew._units().filter(func(u): return not busy.has(u))
	if units.is_empty():
		return
	var id := ""
	for u in units:
		if main.alarms.has(u) or main.errors.has(u):
			id = u
			break
	if id == "":
		id = units[_rng.randi() % units.size()]
	var task: String = TASKS[_rng.randi() % TASKS.size()]
	_count += 1

	var door: Array = doors[_rng.randi() % doors.size()]
	var y: float = main._bounds().position.y
	var at: Vector3 = door[0]
	at.y = y
	var outside: Vector3 = at + (door[1] as Vector3).normalized() * 2.0
	var r := crew._ring()
	var b: AABB = (main._node_bounds(main.entries[id]["node"]) as AABB).grow(1.0)
	var s0 := PlantCrew._along(r, Vector2(outside.x, outside.z))
	var s1 := PlantCrew._along(r, Vector2(b.get_center().x, b.get_center().z))
	var gate: Vector2 = PlantCrew._point(r, s1)
	var stand := Vector3(clampf(gate.x, b.position.x, b.end.x), y, clampf(gate.y, b.position.z, b.end.z))
	var face := Vector3(b.get_center().x, y, b.get_center().z)
	var fwd := (face - stand).normalized() if face.distance_to(stand) > 0.01 else Vector3.FORWARD
	var side := fwd.cross(Vector3.UP).normalized()
	var dur := _rng.randf_range(30.0, 50.0)

	var job := {"id": id, "task": task, "people": [], "t": 0.0, "dur": dur, "working": false,
		"stand": stand, "fwd": fwd, "side": side, "on": false, "flip": 0.0, "last": 0.0,
		"name": "Maintenance %d" % _count, "tag": str(main.entries[id]["data"].get("tag", id))}
	job["site"] = _barrier(stand, fwd, side)
	job["fx"] = _effects(task, fwd)
	job["bubble"] = _bubble()

	# the one who does the job, and on a hot job a second one on fire watch, a step to the side
	var team := [[stand, _tool(task), task]]
	if task == "welding":
		team.append([stand + side * 1.3 - fwd * 0.6, _extinguisher(), "firewatch"])
	var delay := 0.0
	for m in team:
		var n := PlantPeople.make("character-worker", _rng)
		if n == null:
			continue
		add_child(n)
		n.position = at
		n.visible = false
		PlantPeople.hold(n, m[1])
		PlantPeople.dress(n, m[2])
		var steps := [{"wait": delay}, {"walk": outside}]
		for p in PlantCrew._walk_along(r, s0, s1):
			steps.append({"walk": Vector3(p.x, y, p.y)})
		steps.append({"walk": m[0]})
		steps.append({"work": face})
		steps.append({"walk": Vector3(gate.x, y, gate.y)})
		for p in PlantCrew._walk_along(r, s1, s0):
			steps.append({"walk": Vector3(p.x, y, p.y)})
		steps.append({"walk": outside})
		steps.append({"walk": at})
		steps.append({"enter": true})
		job["people"].append({"node": n, "steps": steps, "i": 0, "t": 0.0, "anim": "",
			"lead": job["people"].is_empty()})
		delay += 1.4
	(job["site"] as Node3D).visible = false
	_jobs.append(job)


## Cones round the place of the job, three sides of red and white tape between them; the side
## towards the unit stays open.
func _barrier(stand: Vector3, fwd: Vector3, side: Vector3) -> Node3D:
	var site := Node3D.new()
	add_child(site)
	var c := [stand + side * 1.7 + fwd * 0.4, stand + side * 1.7 - fwd * 1.8,
		stand - side * 1.7 - fwd * 1.8, stand - side * 1.7 + fwd * 0.4]
	var path := "res://assets/models/kenney/vehicles/cone.glb"
	var ps: PackedScene = load(path) if ResourceLoader.exists(path) else null
	for p in c:
		if ps == null:
			break
		var n: Node3D = ps.instantiate()
		site.add_child(n)
		var h := PlantProps._bounds_of(n).size.y
		n.scale = Vector3.ONE * (0.7 / h if h > 0.0 else 1.0)
		n.global_position = p
	var tape := StandardMaterial3D.new()
	tape.albedo_color = Color(0.85, 0.12, 0.1)
	for i in 3:
		var a: Vector3 = c[i]
		var e: Vector3 = c[i + 1]
		var m := BoxMesh.new()
		m.size = Vector3(0.015, 0.06, a.distance_to(e))
		var mi := MeshInstance3D.new()
		mi.mesh = m
		mi.material_override = tape
		site.add_child(mi)
		mi.global_position = (a + e) / 2.0 + Vector3(0, 0.55, 0)
		mi.look_at(e + Vector3(0, 0.55, 0), Vector3.UP)
	return site


## Sparks, a light and the sound of the job, moved to the tip of the tool while it works.
func _effects(task: String, fwd: Vector3) -> Node3D:
	var fx := Node3D.new()
	add_child(fx)
	fx.visible = false
	var hot := task == "welding" or task == "grinding" or task == "hammering"
	if hot:
		var p := GPUParticles3D.new()
		p.name = "sparks"
		p.local_coords = false
		var pm := ParticleProcessMaterial.new()
		pm.gravity = Vector3(0, -9.8, 0)
		pm.scale_min = 0.5
		pm.scale_max = 1.0
		var g := Gradient.new()
		g.set_color(0, Color(1, 1, 1, 1))
		g.set_color(1, Color(1, 1, 1, 0))
		var gt := GradientTexture1D.new()
		gt.gradient = g
		pm.color_ramp = gt
		match task:
			"welding":
				p.amount = 60
				p.lifetime = 0.5
				pm.direction = Vector3(0, 1, 0)
				pm.spread = 70.0
				pm.initial_velocity_min = 1.0
				pm.initial_velocity_max = 2.6
				pm.color = Color(1.0, 0.92, 0.7)
			"grinding":
				p.amount = 140
				p.lifetime = 0.45
				pm.direction = (-fwd + Vector3(0, 0.6, 0)).normalized()
				pm.spread = 22.0
				pm.initial_velocity_min = 3.5
				pm.initial_velocity_max = 6.0
				pm.color = Color(1.0, 0.62, 0.2)
			_:
				p.amount = 16
				p.lifetime = 0.3
				p.one_shot = true
				p.explosiveness = 1.0
				pm.direction = Vector3(0, 1, 0)
				pm.spread = 80.0
				pm.initial_velocity_min = 1.0
				pm.initial_velocity_max = 2.5
				pm.color = Color(1.0, 0.75, 0.35)
		p.process_material = pm
		var q := QuadMesh.new()
		q.size = Vector2(0.07, 0.07)
		var m := StandardMaterial3D.new()
		m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		m.blend_mode = BaseMaterial3D.BLEND_MODE_ADD
		m.billboard_mode = BaseMaterial3D.BILLBOARD_PARTICLES
		m.vertex_color_use_as_albedo = true
		m.albedo_texture = _dot
		q.material = m
		p.draw_pass_1 = q
		p.emitting = false
		fx.add_child(p)
	if task == "welding" or task == "grinding":
		var l := OmniLight3D.new()
		l.name = "light"
		l.light_color = Color(0.7, 0.82, 1.0) if task == "welding" else Color(1.0, 0.65, 0.3)
		l.omni_range = 6.0 if task == "welding" else 3.0
		l.light_energy = 0.0
		l.shadow_enabled = false
		fx.add_child(l)
	if task == "welding":
		var s := SphereMesh.new()
		s.radius = 0.06
		s.height = 0.12
		var glow := StandardMaterial3D.new()
		glow.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		glow.albedo_color = Color(0.85, 0.92, 1.0)
		var mi := MeshInstance3D.new()
		mi.name = "arc"
		mi.mesh = s
		mi.material_override = glow
		mi.visible = false
		fx.add_child(mi)
	var a := AudioStreamPlayer3D.new()
	a.name = "sound"
	a.stream = _streams[task]
	a.unit_size = 10.0
	a.max_distance = 160.0
	a.attenuation_model = AudioStreamPlayer3D.ATTENUATION_INVERSE_DISTANCE
	fx.add_child(a)
	return fx


func _bubble() -> Label3D:
	var bubble := Label3D.new()
	bubble.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	bubble.fixed_size = true
	bubble.pixel_size = 0.0008
	bubble.font_size = 22
	bubble.outline_size = 8
	bubble.modulate = Color(0.45, 0.22, 0.0)
	bubble.outline_modulate = Color(1, 1, 1, 0.92)
	bubble.no_depth_test = true
	bubble.render_priority = 11
	bubble.visible = false
	add_child(bubble)
	return bubble


# --- tools (in metres, the grip at the origin, along +z) ------------------------------------

static func _mat(c: Color, metal := 0.0) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = c
	m.metallic = metal
	m.roughness = 0.5
	return m


static func _part(parent: Node3D, mesh: Mesh, at: Vector3, mat: Material, rot := Vector3.ZERO) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	mi.material_override = mat
	mi.position = at
	mi.rotation = rot
	parent.add_child(mi)
	return mi


static func _rod(r: float, l: float) -> CylinderMesh:
	var c := CylinderMesh.new()
	c.top_radius = r
	c.bottom_radius = r
	c.height = l
	c.radial_segments = 10
	c.rings = 1
	return c


static func _cube(s: Vector3) -> BoxMesh:
	var b := BoxMesh.new()
	b.size = s
	return b


## The tool of a task with a "tip" marker where its sparks come from.
func _tool(task: String) -> Node3D:
	var t := Node3D.new()
	var steel := _mat(Color(0.55, 0.57, 0.6), 0.8)
	var along := Vector3(PI / 2.0, 0, 0)
	var tip := Marker3D.new()
	tip.name = "tip"
	match task:
		"welding":
			_part(t, _rod(0.015, 0.3), Vector3(0, 0, 0.12), _mat(Color(0.15, 0.15, 0.17)), along)
			_part(t, _rod(0.006, 0.08), Vector3(0, -0.02, 0.3), steel, Vector3(PI / 3.0, 0, 0))
			tip.position = Vector3(0, -0.05, 0.34)
		"grinding":
			_part(t, _rod(0.035, 0.26), Vector3(0, 0, 0.1), _mat(Color(0.1, 0.35, 0.55)), along)
			_part(t, _rod(0.07, 0.008), Vector3(0, -0.05, 0.22), _mat(Color(0.3, 0.3, 0.32), 0.3))
			tip.position = Vector3(0, -0.1, 0.24)
		"hammering":
			_part(t, _rod(0.014, 0.36), Vector3(0, 0, 0.15), _mat(Color(0.5, 0.33, 0.18)), along)
			_part(t, _cube(Vector3(0.05, 0.05, 0.14)), Vector3(0, 0, 0.33), steel, Vector3(0, PI / 2.0, 0))
			tip.position = Vector3(0, 0, 0.36)
		_:
			_part(t, _cube(Vector3(0.025, 0.012, 0.28)), Vector3(0, 0, 0.12), steel)
			_part(t, _rod(0.025, 0.015), Vector3(0, 0, 0.27), steel)
			tip.position = Vector3(0, 0, 0.27)
	t.add_child(tip)
	return t


func _extinguisher() -> Node3D:
	var t := Node3D.new()
	var red := _mat(Color(0.78, 0.08, 0.06), 0.2)
	_part(t, _rod(0.075, 0.42), Vector3(0, -0.24, 0), red)
	_part(t, _rod(0.02, 0.06), Vector3(0, -0.01, 0), _mat(Color(0.15, 0.15, 0.15)))
	_part(t, _rod(0.008, 0.3), Vector3(0.06, -0.12, 0.02), _mat(Color(0.08, 0.08, 0.08)), Vector3(0, 0, 0.3))
	return t


# --- running the jobs ------------------------------------------------------------------------

func _process(delta: float) -> void:
	if not main.animate:
		return
	_next -= delta
	if _next <= 0.0:
		_next = _rng.randf_range(35.0, 70.0)
		if _jobs.size() < MAX_JOBS:
			_start()
	var env: PlantEnvironment = main.surroundings
	var gain: float = env.volume if env != null and env.sound_on else 0.0
	for j in _jobs.duplicate():
		for p in j["people"]:
			_step(j, p, delta)
		_work(j, delta, gain)
		if j["people"].all(func(p): return p["done"] if p.has("done") else false):
			_drop(j)
			_jobs.erase(j)


func _play(p: Dictionary, anim: String, speed := 1.0) -> void:
	if p["anim"] == anim:
		return
	p["anim"] = anim
	PlantPeople.play(p["node"], anim, speed)


func _step(j: Dictionary, p: Dictionary, delta: float) -> void:
	var n: Node3D = p["node"]
	if p.has("done") or not is_instance_valid(n):
		p["done"] = true
		return
	var st: Dictionary = p["steps"][p["i"]]
	if st.has("wait"):
		p["t"] += delta
		if p["t"] >= float(st["wait"]):
			p["t"] = 0.0
			p["i"] += 1
			n.visible = true
	elif st.has("walk"):
		_play(p, "walk")
		var to: Vector3 = st["walk"]
		var d := to - n.position
		d.y = 0.0
		var move := SPEED * delta
		if d.length() <= move:
			n.position = Vector3(to.x, n.position.y, to.z)
			p["i"] += 1
		else:
			n.position += d.normalized() * move
			n.rotation.y = atan2(d.x, d.z)
	elif st.has("work"):
		var f: Vector3 = st["work"]
		n.rotation.y = atan2(f.x - n.position.x, f.z - n.position.z)
		if p["lead"]:
			if not j["working"]:
				j["working"] = true
				j["t"] = 0.0
				(j["site"] as Node3D).visible = true
				(j["fx"] as Node3D).visible = true
				var bubble: Label3D = j["bubble"]
				bubble.text = "%s\n%s: %s" % [j["name"], j["tag"], j["task"]]
				bubble.visible = true
				main.crew._write(j["name"], "%s: %s" % [j["tag"], WHAT[j["task"]]])
			var a: Array = ANIM[j["task"]]
			_play(p, a[0], a[1])
			if j["t"] >= float(j["dur"]):
				j["working"] = false
				(j["site"] as Node3D).visible = false
				(j["fx"] as Node3D).visible = false
				(j["bubble"] as Label3D).visible = false
				main.crew._write(j["name"], "%s: %s, done" % [j["tag"], WHAT[j["task"]]])
				p["i"] += 1
		else:
			_play(p, "holding-right")
			# the fire watch stays until the job is done
			var lead: Dictionary = j["people"][0]
			if int(lead["i"]) > int(p["i"]) or lead.has("done"):
				p["i"] += 1
	else:
		n.queue_free()
		p["done"] = true


## The job while it goes on: the arc or the disc comes and goes in bursts, the hammer clanks at
## each blow, and the sparks, the light and the sound follow the tip of the tool.
func _work(j: Dictionary, delta: float, gain: float) -> void:
	var fx: Node3D = j["fx"]
	var sound: AudioStreamPlayer3D = fx.get_node("sound")
	if not j["working"]:
		if sound.playing:
			sound.stop()
		return
	j["t"] += delta
	var lead: Node3D = j["people"][0]["node"]
	var tip := lead.find_child("tip", true, false) as Node3D
	var at: Vector3 = tip.global_position if tip != null else lead.global_position + Vector3(0, 0.8, 0)
	fx.global_position = at
	(j["bubble"] as Label3D).position = lead.position + Vector3(0, 2.4, 0)
	var task: String = j["task"]
	var sparks := fx.get_node_or_null("sparks") as GPUParticles3D
	var light := fx.get_node_or_null("light") as OmniLight3D
	var arc := fx.get_node_or_null("arc") as MeshInstance3D
	if task == "welding" or task == "grinding":
		j["flip"] -= delta
		if j["flip"] <= 0.0:
			j["on"] = not j["on"]
			j["flip"] = _rng.randf_range(3.0, 6.0) if j["on"] else _rng.randf_range(1.2, 2.5)
		var on: bool = j["on"]
		sparks.emitting = on
		if arc != null:
			arc.visible = on and _rng.randf() > 0.15
		if light != null:
			light.light_energy = (_rng.randf_range(2.5, 6.0) if task == "welding" else _rng.randf_range(0.8, 1.4)) if on else 0.0
		_sound(sound, on, gain, -4.0 if task == "welding" else -6.0)
	elif task == "hammering":
		var ph := PlantPeople.phase(lead)
		if j["last"] < 0.5 and ph >= 0.5:
			sparks.restart()
			if gain > 0.01:
				sound.volume_db = -2.0 + linear_to_db(gain)
				sound.play()
		j["last"] = ph
	else:
		_sound(sound, true, gain, -8.0)


func _sound(p: AudioStreamPlayer3D, on: bool, gain: float, db: float) -> void:
	if on and gain > 0.01:
		p.volume_db = db + linear_to_db(gain)
		if not p.playing:
			p.play(_rng.randf() * 0.5)
	elif p.playing:
		p.stop()


# --- the sounds (no recordings) ---------------------------------------------------------------

## An electric arc: a hiss that flutters, sharp pops and the buzz of the welding set.
static func _arc() -> AudioStreamWAV:
	var rate := 22050
	var n := rate * 2
	var s := PackedFloat32Array()
	s.resize(n)
	var rng := RandomNumberGenerator.new()
	rng.seed = 31
	var prev := 0.0
	var pop := 0.0
	var flutter := 0.5
	for i in n:
		var t := float(i) / rate
		var w := rng.randf_range(-1.0, 1.0)
		var hiss := w - prev
		prev = w
		if rng.randf() < 0.0025:
			pop = rng.randf_range(0.4, 0.9) * (1.0 if rng.randf() < 0.5 else -1.0)
		pop *= 0.93
		flutter = 0.999 * flutter + 0.001 * rng.randf()
		s[i] = 0.22 * hiss * (0.5 + flutter) + pop * rng.randf() + 0.05 * sin(TAU * 100.0 * t)
	return PlantLife._wav(s, rate)


## An angle grinder on steel: the whine of the disc, its harmonic and the rasp of the cut.
static func _grinder() -> AudioStreamWAV:
	var rate := 22050
	var s := PackedFloat32Array()
	s.resize(rate)
	var rng := RandomNumberGenerator.new()
	rng.seed = 32
	var b := 0.0
	for i in rate:
		var t := float(i) / rate
		b = 0.6 * b + 0.4 * rng.randf_range(-1.0, 1.0)
		s[i] = 0.16 * sin(TAU * 1700.0 * t) + 0.07 * sin(TAU * 3400.0 * t) + 0.05 * sin(TAU * 850.0 * t) \
			+ 0.3 * b * (0.75 + 0.25 * sin(TAU * 6.0 * t))
	return PlantLife._wav(s, rate)


## One blow of a hammer on steel: a click and a few ringing tones that die away; played once.
static func _clank() -> AudioStreamWAV:
	var rate := 22050
	var n := int(rate * 0.6)
	var s := PackedFloat32Array()
	s.resize(n)
	var rng := RandomNumberGenerator.new()
	rng.seed = 33
	var tones := [[820.0, 0.35, 9.0], [1310.0, 0.25, 12.0], [2150.0, 0.18, 16.0], [3470.0, 0.1, 22.0]]
	for i in n:
		var t := float(i) / rate
		var v := 0.0
		for tn in tones:
			v += tn[1] * sin(TAU * tn[0] * t) * exp(-tn[2] * t)
		v += 0.7 * rng.randf_range(-1.0, 1.0) * exp(-t * 400.0)
		s[i] = v
	var w := PlantLife._wav(s, rate)
	w.loop_mode = AudioStreamWAV.LOOP_DISABLED
	return w


## A ratchet: quick clicks while the wrench turns, then a pause while it goes back.
static func _ratchet() -> AudioStreamWAV:
	var rate := 22050
	var s := PackedFloat32Array()
	s.resize(rate)
	var rng := RandomNumberGenerator.new()
	rng.seed = 34
	var click_every := rate / 11
	for i in rate:
		var t := float(i) / rate
		var v := 0.0
		if t < 0.6:
			var since := float(i % click_every) / rate
			v = (0.6 * rng.randf_range(-1.0, 1.0) + 0.5 * sin(TAU * 2900.0 * since)) * exp(-since * 260.0)
		s[i] = v
	return PlantLife._wav(s, rate)
