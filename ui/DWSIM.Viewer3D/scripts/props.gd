## The site around the equipment, made from the plant's bounds: concrete foundations under the
## equipment, ladders with safety cages on tall columns and vessels, a chain-link fence with a gate
## and an access road, a control room with parked vehicles, fire hydrants and extinguishers, a
## storage corner, and trees outside the fence. The people are the rounds of crew.gd.
## The models are CC0 (Kenney, Poly Haven; see assets/CREDITS.md); the rest is made here.
## O shows or hides all of it.
class_name PlantProps
extends Node3D

const KENNEY := "res://assets/models/kenney/"
const HAVEN := "res://assets/models/polyhaven/"

var main: Node3D
var _scenes := {}
var _concrete: StandardMaterial3D
var _steel: StandardMaterial3D
var _rng := RandomNumberGenerator.new()


func _init(owner_main: Node3D) -> void:
	main = owner_main


func _ready() -> void:
	_concrete = StandardMaterial3D.new()
	_concrete.albedo_texture = load("res://assets/textures/concrete_albedo.jpg")
	_concrete.normal_enabled = true
	_concrete.normal_texture = load("res://assets/textures/concrete_normal.jpg")
	_concrete.albedo_color = Color(0.82, 0.8, 0.76)
	_concrete.uv1_triplanar = true
	_concrete.uv1_world_triplanar = true
	_concrete.uv1_scale = Vector3.ONE / 2.0
	_steel = StandardMaterial3D.new()
	_steel.albedo_color = Color(0.85, 0.72, 0.15)  # safety yellow
	_steel.metallic = 0.4
	_steel.roughness = 0.5


func _scene(path: String) -> PackedScene:
	if not _scenes.has(path):
		_scenes[path] = load(path) if ResourceLoader.exists(path) else null
	return _scenes[path]


## An instance of a model, scaled so its largest horizontal size (or its height) is a given length.
func _model(path: String, at: Vector3, yaw_deg: float, size := 0.0, by_height := false) -> Node3D:
	var ps := _scene(path)
	if ps == null:
		return null
	var n: Node3D = ps.instantiate()
	add_child(n)
	if size > 0.0:
		var b := _bounds_of(n)
		var have := b.size.y if by_height else maxf(b.size.x, b.size.z)
		if have > 0.0:
			n.scale = Vector3.ONE * (size / have)
	n.position = at
	n.rotation.y = deg_to_rad(yaw_deg)
	return n


static func _bounds_of(n: Node3D) -> AABB:
	var box := AABB()
	var first := true
	for mi in n.find_children("*", "MeshInstance3D", true, false):
		var b: AABB = (mi as MeshInstance3D).get_aabb()
		var t: Transform3D = n.global_transform.affine_inverse() * (mi as Node3D).global_transform
		b = t * b
		box = b if first else box.merge(b)
		first = false
	return box


func _box(size: Vector3, at: Vector3, mat: Material) -> MeshInstance3D:
	var m := BoxMesh.new()
	m.size = size
	var mi := MeshInstance3D.new()
	mi.mesh = m
	mi.material_override = mat
	mi.position = at
	add_child(mi)
	return mi


# --- building the site -----------------------------------------------------------------------

func refresh() -> void:
	for c in get_children():
		c.queue_free()
	_rng.seed = 2026
	var box: AABB = main._bounds()
	var ground := box.position.y
	_foundations_and_ladders()
	var x0 := box.position.x - 15.0
	var x1 := box.end.x + 15.0
	var z0 := box.position.z - 15.0
	var z1 := box.end.z + 15.0
	var gate_x := (x0 + x1) * 0.5
	_fence(x0, x1, z0, z1, gate_x, ground)
	_road(gate_x, z1, ground)
	_control_room(x0, z1, gate_x, ground)
	_safety(box, ground)
	_trees(x0, x1, z0, z1, gate_x, ground)


func _foundations_and_ladders() -> void:
	for id in main.entries.keys():
		var e: Dictionary = main.entries[id]
		var d: Dictionary = e["data"]
		if e["kind"] != "item" or bool(d.get("badge", false)) or bool(d.get("inst", false)) or d.has("site"):
			continue
		var b: AABB = main._node_bounds(e["node"])
		if b.size.x < 0.3 or b.size.z < 0.3:
			continue
		# a plinth under the item, as high as the gap below it
		var h := maxf(0.25, b.position.y - main._bounds().position.y + 0.2)
		_box(Vector3(b.size.x + 0.5, h, b.size.z + 0.5), Vector3(b.get_center().x, main._bounds().position.y + h / 2.0 - 0.05, b.get_center().z), _concrete)
		var round_item := absf(b.size.x - b.size.z) < 0.35 * maxf(b.size.x, b.size.z)
		if b.size.y > 6.0 and round_item:
			_ladder(b)


## A caged ladder up the east side of a tall round item.
func _ladder(b: AABB) -> void:
	var r := b.size.x * 0.5
	var x := b.get_center().x + r + 0.25
	var zc := b.get_center().z
	var y0 := b.position.y + 0.3
	var y1 := b.end.y - 0.4
	var h := y1 - y0
	for side in [-0.22, 0.22]:
		var rail := MeshInstance3D.new()
		var c := CylinderMesh.new()
		c.top_radius = 0.025
		c.bottom_radius = 0.025
		c.height = h
		c.radial_segments = 6
		rail.mesh = c
		rail.material_override = _steel
		rail.position = Vector3(x, y0 + h / 2.0, zc + side)
		add_child(rail)
	var rungs := MultiMesh.new()
	rungs.transform_format = MultiMesh.TRANSFORM_3D
	var rung := BoxMesh.new()
	rung.size = Vector3(0.03, 0.03, 0.44)
	rungs.mesh = rung
	rungs.instance_count = int(h / 0.3)
	for i in rungs.instance_count:
		rungs.set_instance_transform(i, Transform3D(Basis.IDENTITY, Vector3(x, y0 + 0.3 * i, zc)))
	var rmi := MultiMeshInstance3D.new()
	rmi.multimesh = rungs
	rmi.material_override = _steel
	add_child(rmi)
	if h > 3.0:
		var hoops := MultiMesh.new()
		hoops.transform_format = MultiMesh.TRANSFORM_3D
		var t := TorusMesh.new()
		t.inner_radius = 0.36
		t.outer_radius = 0.4
		t.rings = 16
		t.ring_segments = 4
		hoops.mesh = t
		hoops.instance_count = int((h - 2.4) / 0.9)
		for i in hoops.instance_count:
			hoops.set_instance_transform(i, Transform3D(Basis.IDENTITY, Vector3(x + 0.2, y0 + 2.4 + 0.9 * i, zc)))
		var hmi := MultiMeshInstance3D.new()
		hmi.multimesh = hoops
		hmi.material_override = _steel
		add_child(hmi)


func _fence(x0: float, x1: float, z0: float, z1: float, gate_x: float, y: float) -> void:
	var mesh_mat := StandardMaterial3D.new()
	mesh_mat.albedo_texture = load("res://assets/textures/fence_albedo.png")
	mesh_mat.normal_enabled = true
	mesh_mat.normal_texture = load("res://assets/textures/fence_normal.jpg")
	# blended, not cut: far away the wires blur into a grey veil instead of vanishing
	mesh_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mesh_mat.albedo_color = Color(0.75, 0.77, 0.8, 1.0)
	mesh_mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	mesh_mat.metallic = 0.6
	mesh_mat.roughness = 0.5
	mesh_mat.uv1_scale = Vector3(1, 1, 1)
	var post_mat := StandardMaterial3D.new()
	post_mat.albedo_color = Color(0.55, 0.57, 0.6)
	post_mat.metallic = 0.7
	post_mat.roughness = 0.4
	var height := 2.2
	var gap := 8.0
	var sides := [[Vector3(x0, y, z0), Vector3(x1, y, z0)], [Vector3(x1, y, z0), Vector3(x1, y, z1)],
		[Vector3(x0, y, z0), Vector3(x0, y, z1)],
		[Vector3(x0, y, z1), Vector3(gate_x - gap / 2.0, y, z1)], [Vector3(gate_x + gap / 2.0, y, z1), Vector3(x1, y, z1)]]
	for s in sides:
		var a: Vector3 = s[0]
		var b: Vector3 = s[1]
		var length := a.distance_to(b)
		if length < 0.5:
			continue
		var q := QuadMesh.new()
		q.size = Vector2(length, height)
		var panel := MeshInstance3D.new()
		panel.mesh = q
		var m := mesh_mat.duplicate() as StandardMaterial3D
		m.uv1_scale = Vector3(length / 2.5, height / 2.5, 1)
		panel.material_override = m
		panel.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
		var mid := (a + b) / 2.0 + Vector3(0, height / 2.0, 0)
		var dir := (b - a).normalized()
		panel.transform = Transform3D(Basis(dir, Vector3.UP, dir.cross(Vector3.UP)), mid)
		add_child(panel)
		# the top rail
		var rail := MeshInstance3D.new()
		var rc := CylinderMesh.new()
		rc.top_radius = 0.035
		rc.bottom_radius = 0.035
		rc.height = length
		rc.radial_segments = 6
		rail.mesh = rc
		rail.material_override = post_mat
		rail.transform = Transform3D(SceneBuilder.basis_along(dir), (a + b) / 2.0 + Vector3(0, height, 0))
		add_child(rail)
		var posts := int(ceil(length / 3.0))
		for i in posts + 1:
			var p := a.lerp(b, float(i) / posts)
			var c := CylinderMesh.new()
			c.top_radius = 0.06
			c.bottom_radius = 0.06
			c.height = height + 0.1
			c.radial_segments = 6
			var post := MeshInstance3D.new()
			post.mesh = c
			post.material_override = post_mat
			post.position = p + Vector3(0, (height + 0.1) / 2.0, 0)
			add_child(post)
	# the gate barrier, raised
	_box(Vector3(gap, 0.12, 0.12), Vector3(gate_x, y + 1.0, z1 + 0.3), _steel)


func _road(gate_x: float, z1: float, y: float) -> void:
	var asphalt := _concrete.duplicate() as StandardMaterial3D
	asphalt.albedo_color = Color(0.32, 0.32, 0.33)
	asphalt.roughness = 0.9
	var road := _box(Vector3(7.0, 0.04, 70.0), Vector3(gate_x, y + 0.02, z1 + 35.0), asphalt)
	road.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	for i in 5:
		_model(KENNEY + "vehicles/cone.glb", Vector3(gate_x - 3.0 + 1.5 * i, y, z1 + 3.0), 0.0, 0.7, true)
	_model(HAVEN + "concrete_road_barrier/concrete_road_barrier.gltf", Vector3(gate_x - 6.0, y, z1 + 1.5), 0.0)
	_model(HAVEN + "concrete_road_barrier/concrete_road_barrier.gltf", Vector3(gate_x + 6.0, y, z1 + 1.5), 0.0)


## The doors of the control rooms, where the rounds start and end: [world position, outward direction].
var doors: Array = []


func _control_room(x0: float, z1: float, gate_x: float, y: float) -> void:
	var w := ControlRoomModel.WIDTH
	var d := ControlRoomModel.DEPTH
	var at := Vector3(x0 + 3.0 + w / 2.0, y, z1 - 2.5 - d / 2.0)
	doors.clear()
	for id in main.entries.keys():
		var e: Dictionary = main.entries[id]
		if str(e["data"].get("site", "")) == "controlroom":
			var n: Node3D = e["node"]
			doors.append([n.transform * ControlRoomModel.DOOR, n.transform.basis * Vector3(0, 0, 1)])
	if doors.is_empty():
		# none placed in the window: one in the corner of the site
		var room := Node3D.new()
		room.position = at
		add_child(room)
		ControlRoomModel.build(room)
		doors.append([at + ControlRoomModel.DOOR, Vector3(0, 0, 1)])
	_model(HAVEN + "utility_box_01/utility_box_01.gltf", at + Vector3(-w / 2.0 - 0.8, 0, 1.0), 90.0)
	# parked vehicles along the road, and the fire truck by the gate
	var cars := ["suv", "delivery", "truck", "suv", "truck-flat"]
	var lengths := [4.7, 5.6, 6.5, 4.7, 7.0]
	for i in cars.size():
		var x := gate_x + 8.0 + i * 3.6
		_model(KENNEY + "vehicles/%s.glb" % cars[i], Vector3(x, y, z1 - 5.0), 0.0, lengths[i])
	_model(KENNEY + "vehicles/firetruck.glb", Vector3(gate_x - 9.0, y, z1 + 10.0), 90.0, 8.0)
	_model(KENNEY + "vehicles/tractor-shovel.glb", Vector3(at.x + w / 2.0 + 6.0, y, at.z - 1.0), 200.0, 6.0)
	# the storage corner
	var sx := at.x + w / 2.0 + 2.5
	for i in 6:
		_model(HAVEN + "Barrel_01/Barrel_01.gltf", Vector3(sx + (i % 3) * 0.75, y, at.z + 2.0 + (i / 3) * 0.75), _rng.randf_range(0, 360))
	for i in 3:
		_model(HAVEN + "wooden_crate_01/wooden_crate_01.gltf", Vector3(sx + 3.0 + i * 1.1, y, at.z + 2.4), _rng.randf_range(-10, 10))
	for i in 4:
		_model(HAVEN + "small_lpg_tank/small_lpg_tank.gltf", Vector3(sx + i * 0.6, y, at.z + 4.2), 0.0)


func _safety(box: AABB, y: float) -> void:
	var g := box.grow(4.5)
	var spots := [Vector3(g.position.x, y, g.position.z), Vector3(g.end.x, y, g.position.z),
		Vector3(g.position.x, y, g.end.z), Vector3(g.end.x, y, g.end.z),
		Vector3(g.get_center().x, y, g.position.z), Vector3(g.get_center().x, y, g.end.z)]
	for p in spots:
		_model(HAVEN + "fire_hydrant/fire_hydrant.gltf", p, _rng.randf_range(0, 360))
	for id in main.entries.keys():
		var e: Dictionary = main.entries[id]
		if str(e["data"].get("type", "")) in ["Pump", "Compressor", "Expander"]:
			var b: AABB = main._node_bounds(e["node"])
			_model(HAVEN + "korean_fire_extinguisher_01/korean_fire_extinguisher_01.gltf", Vector3(b.end.x + 0.9, y, b.end.z + 0.6), 0.0)


# --- trees -----------------------------------------------------------------------------------

## Clumps of trees on the grass outside the fence, clear of the road.
func _trees(x0: float, x1: float, z0: float, z1: float, gate_x: float, y: float) -> void:
	var spots: Array = []
	var tries := 0
	while spots.size() < 90 and tries < 2000:
		tries += 1
		var p := Vector3(_rng.randf_range(x0 - 45.0, x1 + 45.0), y, _rng.randf_range(z0 - 45.0, z1 + 45.0))
		var inside := p.x > x0 - 5.0 and p.x < x1 + 5.0 and p.z > z0 - 5.0 and p.z < z1 + 5.0
		var on_road := absf(p.x - gate_x) < 7.0 and p.z > z1
		if inside or on_road:
			continue
		spots.append(p)
	var trunks := MultiMesh.new()
	trunks.transform_format = MultiMesh.TRANSFORM_3D
	var tm := CylinderMesh.new()
	tm.top_radius = 0.14
	tm.bottom_radius = 0.22
	tm.height = 1.0
	tm.radial_segments = 6
	trunks.mesh = tm
	trunks.instance_count = spots.size()
	var crowns := MultiMesh.new()
	crowns.transform_format = MultiMesh.TRANSFORM_3D
	crowns.use_colors = true
	var cm := SphereMesh.new()
	cm.radius = 1.0
	cm.height = 2.0
	cm.radial_segments = 10
	cm.rings = 6
	crowns.mesh = cm
	crowns.instance_count = spots.size() * 2
	for i in spots.size():
		var p: Vector3 = spots[i]
		var h := _rng.randf_range(3.0, 6.5)
		var r := h * _rng.randf_range(0.35, 0.5)
		trunks.set_instance_transform(i, Transform3D(Basis.from_scale(Vector3(1, h * 0.55, 1)), p + Vector3(0, h * 0.275, 0)))
		for k in 2:
			var off := Vector3(_rng.randf_range(-0.4, 0.4), h * (0.62 + 0.18 * k), _rng.randf_range(-0.4, 0.4)) * Vector3(r, 1, r)
			var sc := r * (1.0 - 0.25 * k)
			crowns.set_instance_transform(i * 2 + k, Transform3D(Basis.from_scale(Vector3(sc, sc * 0.85, sc)), p + off))
			crowns.set_instance_color(i * 2 + k, Color(0.2, 0.42, 0.16).lerp(Color(0.36, 0.52, 0.2), _rng.randf()))
	var bark := StandardMaterial3D.new()
	bark.albedo_color = Color(0.33, 0.24, 0.16)
	var leaves := StandardMaterial3D.new()
	leaves.vertex_color_use_as_albedo = true
	leaves.roughness = 0.9
	var a := MultiMeshInstance3D.new()
	a.multimesh = trunks
	a.material_override = bark
	add_child(a)
	var b := MultiMeshInstance3D.new()
	b.multimesh = crowns
	b.material_override = leaves
	add_child(b)
