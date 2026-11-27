## A parallel-projection camera with the fixed views of the DWSIM plant designer: four isometric
## views, the plan and the elevations, turned in quarter turns. Azimuth and elevation follow
## OrbitCamera.cs (DWSIM frame: X right, Y to the back, Z up; azimuth 0 looks from the front).
class_name CameraRig
extends Node3D

const ISO_ELEVATION := 35.264389682754654  # atan(1/sqrt 2), degrees

const VIEWS := {
	"IsoSE": [45.0, ISO_ELEVATION],
	"IsoSW": [-45.0, ISO_ELEVATION],
	"IsoNW": [-135.0, ISO_ELEVATION],
	"IsoNE": [135.0, ISO_ELEVATION],
	"Plan": [0.0, 90.0],
	"Front": [0.0, 0.0],
	"Back": [180.0, 0.0],
	"Left": [-90.0, 0.0],
	"Right": [90.0, 0.0],
}

var azimuth := 45.0
var elevation := ISO_ELEVATION
## The plant point at the centre of the screen (Godot frame, m).
var target := Vector3.ZERO
## The height of the view, m.
var view_size := 40.0
## How far the camera stands back; set from the size of the plant so shadows reach it.
var distance := 200.0

var camera: Camera3D

var _from_az := 0.0
var _from_el := 0.0
var _to_az := 0.0
var _to_el := 0.0
var _anim := -1.0
const ANIM_TIME := 0.35


func _ready() -> void:
	camera = Camera3D.new()
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.near = 0.05
	add_child(camera)
	camera.make_current()
	apply()


## DWSIM frame to Godot frame (both right handed).
static func g(v: Vector3) -> Vector3:
	return Vector3(v.x, v.z, -v.y)


func eye() -> Vector3:
	var t := deg_to_rad(azimuth)
	var p := deg_to_rad(elevation)
	return g(Vector3(cos(p) * sin(t), -cos(p) * cos(t), sin(p)))


func right() -> Vector3:
	var t := deg_to_rad(azimuth)
	return g(Vector3(cos(t), sin(t), 0.0))


func up() -> Vector3:
	return eye().cross(right())


func apply() -> void:
	if camera == null:
		return
	camera.size = max(view_size, 0.5)
	camera.far = distance * 3.0 + 100.0
	camera.global_transform = Transform3D(Basis(right(), up(), eye()), target + eye() * distance)


func set_view(name: String, animate := true) -> void:
	if not VIEWS.has(name):
		return
	var v: Array = VIEWS[name]
	# the shortest way round to the new azimuth
	var to_az: float = v[0]
	while to_az - azimuth > 180.0:
		to_az -= 360.0
	while to_az - azimuth < -180.0:
		to_az += 360.0
	animate_to(to_az, v[1], animate)


func turn(quarters: int) -> void:
	var base := _to_az if _anim >= 0.0 else azimuth
	var el := _to_el if _anim >= 0.0 else elevation
	animate_to(base + 90.0 * quarters, el, true)


func animate_to(az: float, el: float, animate := true) -> void:
	if not animate:
		azimuth = az
		elevation = el
		_anim = -1.0
		apply()
		return
	_from_az = azimuth
	_from_el = elevation
	_to_az = az
	_to_el = el
	_anim = 0.0


func orbit(dx: float, dy: float) -> void:
	_anim = -1.0
	azimuth += dx * 0.3
	elevation = clamp(elevation + dy * 0.3, 0.0, 90.0)
	apply()


## Moves the view by a mouse drag of (dx, dy) pixels in a viewport of the given height.
func pan(dx: float, dy: float, viewport_height: float) -> void:
	var k := view_size / maxf(viewport_height, 1.0)
	target -= right() * dx * k - up() * dy * k
	apply()


## Zooms by a factor, keeping the plant point under the mouse where it is.
func zoom_at(factor: float, mouse: Vector2, viewport_size: Vector2) -> void:
	var h: float = max(viewport_size.y, 1.0)
	var off := (mouse - viewport_size / 2.0) / h
	var before := view_size
	view_size = clamp(view_size * factor, 0.5, 20000.0)
	target += (right() * off.x - up() * off.y) * (before - view_size)
	apply()


## Fits a set of points (Godot frame) into the view, centred on the screen.
func fit_points(points: PackedVector3Array, aspect: float) -> void:
	if points.is_empty():
		return
	var r := right()
	var u := up()
	var e := eye()
	var lo := Vector3(INF, INF, INF)
	var hi := Vector3(-INF, -INF, -INF)
	for p in points:
		var q := Vector3(p.dot(r), p.dot(u), p.dot(e))
		lo = lo.min(q)
		hi = hi.max(q)
	var mid := (lo + hi) / 2.0
	target = r * mid.x + u * mid.y + e * mid.z
	view_size = maxf(hi.y - lo.y, (hi.x - lo.x) / maxf(aspect, 0.1)) * 1.08 + 0.5
	var span := (hi - lo).length()
	distance = maxf(100.0, span * 2.0)
	apply()


## Fits a box (Godot frame) into the view.
func fit(box: AABB, aspect: float) -> void:
	target = box.get_center()
	var r := right()
	var u := up()
	var w := 0.0
	var h := 0.0
	for i in 8:
		var d := box.get_endpoint(i) - target
		w = max(w, abs(d.dot(r)))
		h = max(h, abs(d.dot(u)))
	view_size = max(2.0 * h, 2.0 * w / max(aspect, 0.1)) * 1.12 + 1.0
	distance = max(100.0, box.size.length() * 2.0)
	apply()


func _process(delta: float) -> void:
	if _anim < 0.0:
		return
	_anim = min(1.0, _anim + delta / ANIM_TIME)
	var s := _anim * _anim * (3.0 - 2.0 * _anim)
	azimuth = lerp(_from_az, _to_az, s)
	elevation = lerp(_from_el, _to_el, s)
	apply()
	if _anim >= 1.0:
		_anim = -1.0
