## A flyby of the plant: a perspective camera that opens on the whole plant, flies in an arc to
## each unit in process order (the order DWSIM sends), closes in and turns slowly around it while
## a card shows its tag, its kind and its main values, and ends on the whole plant again. B plays it
## in the window; DWSIM records it with Godot's Movie Maker (see FlybyRecorder.cs).
class_name Flyby
extends Node3D

signal finished

const FOV := 40.0
const ELEVATION := 24.0

var main: Node3D
var active := false
var hold := 4.0

var _cam: Camera3D
var _shots: Array = []
var _i := 0
var _t := 0.0
var _card: PanelContainer
var _title: Label
var _sub: Label
var _step: Label
var _rows: GridContainer
var _shadow_reach := 0.0
var _labels_before := true
var _values_before := false


func _init(owner_main: Node3D) -> void:
	main = owner_main


func _ready() -> void:
	_cam = Camera3D.new()
	_cam.fov = FOV
	_cam.near = 0.1
	_cam.far = 6000.0
	add_child(_cam)


## The direction from a point towards a camera at an azimuth and elevation (DWSIM frame, degrees).
static func toward(az_deg: float, el_deg: float) -> Vector3:
	var az := deg_to_rad(az_deg)
	var el := deg_to_rad(el_deg)
	return CameraRig.g(Vector3(cos(el) * sin(az), -cos(el) * cos(az), sin(el)))


func build_card(parent: Node) -> void:
	_card = PanelContainer.new()
	var st := StyleBoxFlat.new()
	st.bg_color = Color(0.05, 0.07, 0.1, 0.82)
	st.set_corner_radius_all(10)
	st.set_content_margin_all(18)
	_card.add_theme_stylebox_override("panel", st)
	var vb := VBoxContainer.new()
	vb.add_theme_constant_override("separation", 4)
	_card.add_child(vb)
	_step = _text(vb, 14, Color(0.55, 0.75, 0.95))
	_title = _text(vb, 32, Color(1, 1, 1))
	_sub = _text(vb, 17, Color(0.75, 0.8, 0.86))
	var gap := Control.new()
	gap.custom_minimum_size = Vector2(0, 6)
	vb.add_child(gap)
	_rows = GridContainer.new()
	_rows.columns = 2
	_rows.add_theme_constant_override("h_separation", 22)
	vb.add_child(_rows)
	_card.visible = false
	parent.add_child(_card)


func _text(parent: Control, size: int, color: Color) -> Label:
	var l := Label.new()
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", color)
	parent.add_child(l)
	return l


# --- the tour --------------------------------------------------------------------------------

func start(ids: Array, hold_s: float) -> void:
	hold = maxf(1.0, hold_s)
	var units := []
	for id in ids:
		var s := str(id)
		if main.entries.has(s) and main.entries[s]["kind"] == "item":
			units.append(s)
	var box: AABB = main._bounds()
	var c := box.get_center()
	var r := maxf(box.size.length() * 0.5, 10.0)
	var far := r * 2.1 / tan(deg_to_rad(FOV) * 0.5) * 0.55
	_shots.clear()
	var at := c + toward(40.0, 32.0) * far
	_shots.append({"kind": "orbit", "c": c, "d": far, "el": 32.0, "az0": 40.0, "az1": 58.0, "dur": 3.0, "id": ""})
	at = c + toward(58.0, 32.0) * far
	var look := c
	var n := units.size()
	var boxes := {}
	for id in main.entries.keys():
		if main.entries[id]["kind"] == "item":
			boxes[id] = (main._node_bounds(main.entries[id]["node"]) as AABB).grow(0.2)
	var last_az := 58.0
	for k in n:
		var id: String = units[k]
		var b: AABB = main._node_bounds(main.entries[id]["node"])
		var uc := b.get_center()
		var ur := maxf(b.size.length() * 0.5, 1.5)
		var d := maxf(ur / tan(deg_to_rad(FOV) * 0.5) * 1.5, 8.0)
		var view := _clear_view(id, uc, d, boxes, last_az)
		var az: float = view.x
		var el: float = view.y
		var to := uc + toward(az, el) * d
		var span := at.distance_to(to)
		_shots.append({"kind": "move", "p0": at, "p1": to, "l0": look, "l1": uc,
			"ctrl": (at + to) * 0.5 + Vector3(0, maxf(6.0, span * 0.35), 0),
			"dur": clampf(span / 18.0, 1.8, 4.5), "id": ""})
		_shots.append({"kind": "orbit", "c": uc, "d": d, "el": el, "az0": az, "az1": az + 20.0,
			"dur": hold, "id": id, "k": k + 1, "n": n})
		at = uc + toward(az + 20.0, el) * d
		look = uc
		last_az = az + 20.0
	var end := c + toward(60.0, 34.0) * far
	_shots.append({"kind": "move", "p0": at, "p1": end, "l0": look, "l1": c,
		"ctrl": (at + end) * 0.5 + Vector3(0, r * 0.6, 0), "dur": 3.5, "id": ""})
	_shots.append({"kind": "orbit", "c": c, "d": far, "el": 34.0, "az0": 60.0, "az1": 75.0, "dur": 3.0, "id": ""})
	_i = 0
	_t = 0.0
	_shadow_reach = main.sun.directional_shadow_max_distance
	main.sun.directional_shadow_max_distance = maxf(150.0, r * 3.0)
	_labels_before = main.labels_root.visible
	_values_before = main.values_root.visible
	main.values_root.visible = false
	_cam.make_current()
	active = true
	_frame(0.0)


## The azimuth and elevation of a close-up that no other item hides: of twelve azimuths at two
## elevations, the one whose sight line (and the end of the turn) crosses the fewest other
## items, the nearest to the last azimuth among those.
func _clear_view(id: String, centre: Vector3, d: float, boxes: Dictionary, last_az: float) -> Vector2:
	var best := Vector2(last_az, ELEVATION)
	var best_score := INF
	for el in [ELEVATION, 40.0]:
		for i in 12:
			var az := last_az + 30.0 * i
			var hidden := 0
			for turn in [0.0, 20.0]:
				var eye := centre + toward(az + turn, el) * d
				for other in boxes.keys():
					if other != id and (boxes[other] as AABB).intersects_segment(eye, centre):
						hidden += 1
			var turn_cost := absf(wrapf(az - last_az, -180.0, 180.0)) / 180.0
			var score := hidden * 10.0 + turn_cost + (0.5 if el != ELEVATION else 0.0)
			if score < best_score:
				best_score = score
				best = Vector2(wrapf(az, 0.0, 360.0), el)
	return best


func stop() -> void:
	if not active:
		return
	active = false
	_card.visible = false
	main.sun.directional_shadow_max_distance = _shadow_reach
	main.labels_root.visible = _labels_before
	main.values_root.visible = _values_before
	main.refresh_labels()
	main.rig.camera.make_current()
	finished.emit()


func _process(delta: float) -> void:
	if not active:
		return
	_t += delta
	while active and _t >= float(_shots[_i]["dur"]):
		_t -= float(_shots[_i]["dur"])
		_i += 1
		if _i >= _shots.size():
			stop()
			return
	_frame(_t)


func _frame(t: float) -> void:
	var s: Dictionary = _shots[_i]
	var dur := float(s["dur"])
	var k := clampf(t / dur, 0.0, 1.0)
	var pos: Vector3
	var look: Vector3
	if s["kind"] == "move":
		var e := k * k * (3.0 - 2.0 * k)
		var p0: Vector3 = s["p0"]
		var p1: Vector3 = s["p1"]
		var q: Vector3 = s["ctrl"]
		pos = p0 * (1.0 - e) * (1.0 - e) + q * 2.0 * e * (1.0 - e) + p1 * e * e
		look = (s["l0"] as Vector3).lerp(s["l1"], e)
	else:
		var az := lerpf(float(s["az0"]), float(s["az1"]), k)
		look = s["c"]
		pos = look + toward(az, float(s["el"])) * float(s["d"])
	_cam.look_at_from_position(pos, look, Vector3.UP)
	_show_card(s, t, dur)


func _show_card(s: Dictionary, t: float, dur: float) -> void:
	var id := str(s.get("id", ""))
	# only the tag of the unit the camera is on
	var current = main.entries[id].get("label") if id != "" and main.entries.has(id) else null
	for label in main.labels_root.get_children():
		label.visible = label == current
	if id == "" or not main.entries.has(id):
		_card.visible = false
		return
	if not _card.visible or _title.get_meta("id", "") != id:
		var d: Dictionary = main.entries[id]["data"]
		_title.text = str(d.get("tag", ""))
		_title.set_meta("id", id)
		_sub.text = str(d.get("desc", ""))
		_step.text = "Unit %d of %d, in process order" % [int(s["k"]), int(s["n"])]
		for c in _rows.get_children():
			c.queue_free()
		var props = d.get("props", {})
		var shown := 0
		if props is Dictionary:
			for key in props.keys():
				if str(key).begins_with("Composition"):
					continue
				_text(_rows, 18, Color(0.7, 0.76, 0.83)).text = str(key)
				_text(_rows, 18, Color(1, 1, 1)).text = str(props[key])
				shown += 1
				if shown == 8:
					break
		_card.size = Vector2.ZERO
		_card.visible = true
	_card.modulate.a = clampf(minf(t / 0.6, (dur - t) / 0.5), 0.0, 1.0)
	var vs: Vector2 = main.get_viewport().get_visible_rect().size
	_card.position = Vector2(40, vs.y - _card.size.y - 40)
