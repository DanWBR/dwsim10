## The rounds: every so often an operator or an engineer comes out of a control room, walks round
## the plant on a path just outside the equipment, stops at two to four units, looks them over and
## writes a note on a clipboard (the values the window has for the unit, or the alarm or the error
## it is in), and goes back in. Units in alarm are visited first. The notes appear over the head of
## whoever writes them and in the Rounds panel (K). At most four people are out at a time.
class_name PlantCrew
extends Node3D

const OPERATORS := ["character-worker"]
const ENGINEERS := ["character-male-a", "character-female-a", "character-male-b", "character-female-b", "character-male-c", "character-female-c"]
const MAX_OUT := 4
const SPEED := 1.3
const LOG_LINES := 7

var main: Node3D
var panel: PanelContainer
var _lines: VBoxContainer
var _people: Array = []
var _next := 3.0
var _rng := RandomNumberGenerator.new()
var _count := {"Operator": 0, "Engineer": 0}
var _log: Array = []


func _init(owner_main: Node3D) -> void:
	main = owner_main
	_rng.seed = 7


func build_panel(style: StyleBox) -> PanelContainer:
	panel = PanelContainer.new()
	panel.add_theme_stylebox_override("panel", style)
	panel.name = "rounds_panel"
	var vb := VBoxContainer.new()
	panel.add_child(vb)
	var title := Label.new()
	title.text = "Rounds and maintenance"
	title.add_theme_color_override("font_color", Color(0.11, 0.15, 0.2))
	vb.add_child(title)
	_lines = VBoxContainer.new()
	vb.add_child(_lines)
	panel.visible = false
	return panel


## Everyone goes back in: the scene changed and the paths with it.
func clear() -> void:
	for p in _people:
		if is_instance_valid(p["node"]):
			p["node"].queue_free()
	_people.clear()
	_next = 3.0


# --- the path round the plant ----------------------------------------------------------------

## The units the rounds visit: equipment with a body, not badges, instruments or buildings.
func _units() -> Array:
	var ids := []
	for id in main.entries.keys():
		var e: Dictionary = main.entries[id]
		var d: Dictionary = e["data"]
		if e["kind"] == "item" and not bool(d.get("badge", false)) and not bool(d.get("inst", false)) and not d.has("site"):
			ids.append(id)
	return ids


## The rectangle the path follows: the equipment grown by 2.5 m (Godot frame, x and z).
func _ring() -> Rect2:
	var box := AABB()
	var first := true
	for id in _units():
		var b: AABB = main._node_bounds(main.entries[id]["node"])
		box = b if first else box.merge(b)
		first = false
	return Rect2(box.position.x - 2.5, box.position.z - 2.5, box.size.x + 5.0, box.size.z + 5.0)


static func _corners(r: Rect2) -> Array:
	return [Vector2(r.position.x, r.position.y), Vector2(r.end.x, r.position.y), Vector2(r.end.x, r.end.y), Vector2(r.position.x, r.end.y)]


## The distance along the path (from the first corner, clockwise in x, z) of the point nearest to p.
static func _along(r: Rect2, p: Vector2) -> float:
	var c := _corners(r)
	var best := INF
	var best_s := 0.0
	var s0 := 0.0
	for k in 4:
		var a: Vector2 = c[k]
		var b: Vector2 = c[(k + 1) % 4]
		var q := Geometry2D.get_closest_point_to_segment(p, a, b)
		var d := q.distance_to(p)
		if d < best:
			best = d
			best_s = s0 + a.distance_to(q)
		s0 += a.distance_to(b)
	return best_s


static func _point(r: Rect2, s: float) -> Vector2:
	var c := _corners(r)
	var per := 2.0 * (r.size.x + r.size.y)
	s = fposmod(s, per)
	for k in 4:
		var a: Vector2 = c[k]
		var b: Vector2 = c[(k + 1) % 4]
		var l := a.distance_to(b)
		if s <= l:
			return a.lerp(b, s / maxf(l, 0.001))
		s -= l
	return c[0]


## The points to walk from one place on the path to another, the shorter way round.
static func _walk_along(r: Rect2, s0: float, s1: float) -> Array:
	var per := 2.0 * (r.size.x + r.size.y)
	var fwd := fposmod(s1 - s0, per)
	var dir := 1.0 if fwd <= per / 2.0 else -1.0
	var span := fwd if dir > 0.0 else per - fwd
	var pts := []
	var c := _corners(r)
	# the corners passed on the way
	var corner_s := [0.0, r.size.x, r.size.x + r.size.y, 2.0 * r.size.x + r.size.y]
	var stops := []
	for k in 4:
		var d := fposmod((corner_s[k] - s0) * dir, per)
		if d > 0.01 and d < span:
			stops.append([d, c[k]])
	stops.sort_custom(func(a, b): return a[0] < b[0])
	for st in stops:
		pts.append(st[1])
	pts.append(_point(r, s1))
	return pts


# --- people ----------------------------------------------------------------------------------

func _spawn() -> void:
	var doors: Array = main.props.doors if main.props != null else []
	var units := _units()
	if doors.is_empty() or units.is_empty():
		return
	var engineer := _rng.randf() < 0.45
	var role := "Engineer" if engineer else "Operator"
	_count[role] += 1
	var model: String = (ENGINEERS[_rng.randi() % ENGINEERS.size()] if engineer else OPERATORS[0])
	var n := PlantPeople.make(model, _rng)
	if n == null:
		return
	var door: Array = doors[_rng.randi() % doors.size()]
	var at: Vector3 = door[0]
	var out: Vector3 = door[1]
	var y: float = main._bounds().position.y
	at.y = y
	add_child(n)
	PlantPeople.dress(n, "engineer" if engineer else "operator")
	var k: float = n.get_meta("k")
	n.position = at
	var clip := MeshInstance3D.new()
	var cm := BoxMesh.new()
	cm.size = Vector3(0.24, 0.32, 0.02)
	clip.mesh = cm
	var cmat := StandardMaterial3D.new()
	cmat.albedo_color = Color(0.62, 0.48, 0.3)
	clip.material_override = cmat
	clip.scale = Vector3.ONE / k
	clip.position = Vector3(0.1, 1.12, 0.3) / k
	clip.rotation.x = deg_to_rad(-35.0)
	clip.visible = false
	n.add_child(clip)
	var bubble := Label3D.new()
	bubble.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	bubble.fixed_size = true
	bubble.pixel_size = 0.0009
	bubble.font_size = 32
	bubble.outline_size = 6
	bubble.outline_render_priority = 10
	bubble.modulate = Color(0.08, 0.2, 0.4)
	bubble.outline_modulate = Color(1, 1, 1, 0.92)
	bubble.no_depth_test = true
	bubble.render_priority = 11
	bubble.visible = false
	add_child(bubble)

	# the units of this round: alarms first, then two or three others
	var picks := []
	for id in main.alarms.keys():
		if units.has(id) and picks.size() < 2:
			picks.append(id)
	var rest := units.filter(func(u): return not picks.has(u))
	var want := _rng.randi_range(2, 4)
	while picks.size() < want and not rest.is_empty():
		picks.append(rest.pop_at(_rng.randi() % rest.size()))

	var r := _ring()
	var steps := []
	var outside := at + out.normalized() * 2.0
	steps.append({"walk": outside})
	var s := _along(r, Vector2(outside.x, outside.z))
	var s_door := s
	for id in picks:
		var b: AABB = (main._node_bounds(main.entries[id]["node"]) as AABB).grow(1.0)
		var s1 := _along(r, Vector2(b.get_center().x, b.get_center().z))
		for p in _walk_along(r, s, s1):
			steps.append({"walk": Vector3(p.x, y, p.y)})
		var gate: Vector2 = _point(r, s1)
		# the nearest point of the unit's footprint, a metre out from it
		var stand := Vector3(clampf(gate.x, b.position.x, b.end.x), y, clampf(gate.y, b.position.z, b.end.z))
		steps.append({"walk": stand})
		steps.append({"look": id, "face": b.get_center(), "dur": _rng.randf_range(5.0, 8.0)})
		steps.append({"walk": Vector3(gate.x, y, gate.y)})
		s = s1
	for p in _walk_along(r, s, s_door):
		steps.append({"walk": Vector3(p.x, y, p.y)})
	steps.append({"walk": outside})
	steps.append({"walk": at})
	steps.append({"enter": true})
	_people.append({"node": n, "clip": clip, "bubble": bubble, "steps": steps, "i": 0, "t": 0.0,
		"name": "%s %d" % [role, _count[role]], "anim": ""})


func _play(p: Dictionary, anim: String) -> void:
	if p["anim"] == anim:
		return
	p["anim"] = anim
	for ap in (p["node"] as Node).find_children("*", "AnimationPlayer", true, false):
		var player := ap as AnimationPlayer
		var name := anim if player.has_animation(anim) else "idle"
		if player.has_animation(name):
			player.get_animation(name).loop_mode = Animation.LOOP_LINEAR
			player.play(name)


func _process(delta: float) -> void:
	if not main.animate:
		return
	_next -= delta
	if _next <= 0.0:
		_next = _rng.randf_range(20.0, 45.0)
		if _people.size() < MAX_OUT:
			_spawn()
	for p in _people.duplicate():
		_step(p, delta)


func _step(p: Dictionary, delta: float) -> void:
	if not is_instance_valid(p["node"]):
		_people.erase(p)
		return
	var n: Node3D = p["node"]
	var st: Dictionary = p["steps"][p["i"]]
	if st.has("walk"):
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
	elif st.has("look"):
		var id: String = st["look"]
		if p["t"] == 0.0:
			var f: Vector3 = st["face"]
			n.rotation.y = atan2(f.x - n.position.x, f.z - n.position.z)
			_play(p, "holding-right")
			p["clip"].visible = true
			var note := _note(id)
			p["bubble"].text = p["name"] + "\n" + note
			p["bubble"].visible = true
			_write(p["name"], note)
		p["t"] += delta
		p["bubble"].position = n.position + Vector3(0, 2.4, 0)
		if p["t"] >= float(st["dur"]):
			p["t"] = 0.0
			p["clip"].visible = false
			p["bubble"].visible = false
			p["i"] += 1
	else:
		p["bubble"].queue_free()
		n.queue_free()
		_people.erase(p)


# --- notes -----------------------------------------------------------------------------------

## What the round notes at a unit: its alarm or its error first, else its main values.
func _note(id: String) -> String:
	if not main.entries.has(id):
		return id
	var d: Dictionary = main.entries[id]["data"]
	var tag := str(d.get("tag", id))
	if main.alarms.has(id):
		return "%s: alarm, reported to the control room" % tag
	if main.errors.has(id):
		return "%s: calculation error, to check" % tag
	var props = d.get("props", {})
	var parts := []
	if props is Dictionary:
		for pair in [["Operating temperature (C)", "%s C"], ["Operating pressure (bar)", "%s bar"], ["Power (kW)", "%s kW"], ["Duty (kW)", "%s kW"]]:
			if props.has(pair[0]) and parts.size() < 2:
				parts.append(pair[1] % str(props[pair[0]]))
	return "%s: %s" % [tag, (", ".join(parts) + ", normal") if not parts.is_empty() else "looked over, normal"]


func _write(who: String, note: String) -> void:
	var hour: float = main.surroundings.hour if main.surroundings != null else 0.0
	var line := "%02d:%02d  %s  -  %s" % [int(hour), int(fmod(hour, 1.0) * 60.0), who, note]
	_log.append(line)
	while _log.size() > LOG_LINES:
		_log.pop_front()
	if _lines == null:
		return
	for c in _lines.get_children():
		c.queue_free()
	for l in _log:
		var lab := Label.new()
		lab.text = l
		lab.add_theme_font_size_override("font_size", 11)
		lab.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		lab.custom_minimum_size = Vector2(main.PANEL_W * 1.6 - 20.0, 0)
		lab.add_theme_color_override("font_color", Color(0.2, 0.26, 0.33))
		_lines.add_child(lab)
	panel.size = Vector2.ZERO
