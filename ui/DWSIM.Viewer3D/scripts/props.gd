## The site around the equipment. Concrete foundations under the equipment and caged ladders up
## tall columns and vessels are always drawn here. The rest of the site (a chain-link fence with a
## gate and an access road, a control room with parked vehicles, lamp posts, fire hydrants and
## extinguishers, a storage corner, and woods outside the fence) is a list of objects of the site
## (auto_items, in the form of the site.add message): until the user opens the build bar the window
## draws that list by itself, made from the bounds of the plant; when the user opens it, the list
## goes to DWSIM as the adopted site (site.adopt) and from then on the site is drawn from its
## objects (site_objects.gd). The people are the rounds of crew.gd. O shows or hides all of it.
class_name PlantProps
extends Node3D

var main: Node3D
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

## The doors of the control rooms, where the rounds start and end: [world position, outward direction].
var doors: Array = []


func refresh() -> void:
	for c in get_children():
		c.queue_free()
	doors.clear()
	_foundations_and_ladders()
	if not main.site_adopted:
		for it in auto_items():
			var node := Node3D.new()
			add_child(node)
			var d: Dictionary = it.duplicate()
			d["o"] = [float(it.get("x", 0.0)), float(it.get("y", 0.0)), float(it.get("z", 0.0))]
			if it.has("points"):
				d["o"] = [float(it["points"][0][0]), float(it["points"][0][1]), float(it.get("z", 0.0))]
			node.position = SceneBuilder.vec(d["o"])
			SiteObjects.build(node, d)
			if str(it["kind"]) == "controlroom":
				doors.append([node.transform * ControlRoomModel.DOOR, node.transform.basis * Vector3(0, 0, 1)])
	for id in main.entries.keys():
		var e: Dictionary = main.entries[id]
		if str(e["data"].get("site", "")) == "controlroom":
			var n: Node3D = e["node"]
			doors.append([n.transform * Vector3(n.get_meta("door", ControlRoomModel.DOOR)), n.transform.basis * Vector3(0, 0, 1)])


func _foundations_and_ladders() -> void:
	for id in main.entries.keys():
		var e: Dictionary = main.entries[id]
		var d: Dictionary = e["data"]
		if e["kind"] != "item" or bool(d.get("badge", false)) or bool(d.get("inst", false)) or d.has("site"):
			continue
		# the imported plant model has the item's own base and ladders
		if d.get("model") is Dictionary and main.model_replaces():
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


# --- the site the window makes by itself --------------------------------------------------------

## An object of the site at a place of the window (Godot frame, m), turned by yaw degrees, as site.add takes it.
static func _point(kind: String, at: Vector3, yaw: float, variant := "") -> Dictionary:
	var d := {"kind": kind, "x": roundf(at.x * 1000.0), "y": roundf(-at.z * 1000.0), "z": roundf(at.y * 1000.0),
		"rot": int(fposmod(roundf(yaw), 360.0))}
	if variant != "":
		d["variant"] = variant
	return d


## A line or an area through places of the window (Godot frame, m).
static func _shape(kind: String, pts: Array, y: float) -> Dictionary:
	var list := []
	for p in pts:
		list.append([roundf(p.x * 1000.0), roundf(-p.z * 1000.0)])
	return {"kind": kind, "points": list, "z": roundf(y * 1000.0)}


## The site made from the bounds of the plant: the objects the window draws until the user adopts them.
## With campus, the site the samples are baked with (--bake-site): the same fence, gate, control room,
## vehicles, hydrants and lamps, and in front of the gate, on both sides of a longer road, the
## buildings of a plant (see _campus), with the woods kept clear of them.
func auto_items(campus := false) -> Array:
	_rng.seed = 2026
	var box: AABB = main._bounds()
	var y := box.position.y
	var x0 := box.position.x - 15.0
	var x1 := box.end.x + 15.0
	var z0 := box.position.z - 15.0
	var z1 := box.end.z + 15.0
	var gate_x := (x0 + x1) * 0.5
	var gap := 8.0
	var out := []
	# the fence round the plant, open at the gate, and the road out of it
	out.append(_shape("fence", [Vector3(gate_x + gap / 2.0, y, z1), Vector3(x1, y, z1), Vector3(x1, y, z0),
		Vector3(x0, y, z0), Vector3(x0, y, z1), Vector3(gate_x - gap / 2.0, y, z1)], y))
	out.append(_point("gate", Vector3(gate_x, y, z1 + 0.3), 0.0))
	out.append(_shape("road", [Vector3(gate_x, y, z1 - 2.0), Vector3(gate_x, y, z1 + (110.0 if campus else 70.0))], y))
	for i in 5:
		out.append(_point("cone", Vector3(gate_x - 3.0 + 1.5 * i, y, z1 + 3.0), 0.0))
	out.append(_point("barrier", Vector3(gate_x - 6.0, y, z1 + 1.5), 0.0))
	out.append(_point("barrier", Vector3(gate_x + 6.0, y, z1 + 1.5), 0.0))
	# the control room in the corner, when the user has not placed one
	var w := ControlRoomModel.WIDTH
	var d := ControlRoomModel.DEPTH
	var at := Vector3(x0 + 3.0 + w / 2.0, y, z1 - 2.5 - d / 2.0)
	var placed := false
	for id in main.entries.keys():
		if str(main.entries[id]["data"].get("site", "")) == "controlroom":
			placed = true
	if not placed:
		out.append(_point("controlroom", at, 0.0))
	out.append(_point("utilitybox", at + Vector3(-w / 2.0 - 0.8, 0, 1.0), 90.0))
	# parked vehicles along the road, the fire truck by the gate, a loader
	var cars := [["suv", ""], ["delivery", ""], ["truck", "box"], ["suv", ""], ["truck", ""]]
	for i in cars.size():
		out.append(_point(cars[i][0], Vector3(gate_x + 8.0 + i * 3.6, y, z1 - 5.0), 0.0, cars[i][1]))
	out.append(_point("firetruck", Vector3(gate_x - 9.0, y, z1 + 10.0), 90.0))
	out.append(_point("loader", Vector3(at.x + w / 2.0 + 6.0, y, at.z - 1.0), 200.0))
	# the storage corner
	var sx := at.x + w / 2.0 + 2.5
	for i in 6:
		out.append(_point("barrel", Vector3(sx + (i % 3) * 0.75, y, at.z + 2.0 + (i / 3) * 0.75), _rng.randf_range(0, 360)))
	for i in 3:
		out.append(_point("crate", Vector3(sx + 3.0 + i * 1.1, y, at.z + 2.4), _rng.randf_range(-10, 10)))
	for i in 4:
		out.append(_point("lpgtank", Vector3(sx + i * 0.6, y, at.z + 4.2), 0.0))
	# hydrants round the plant and an extinguisher by each pump and compressor
	var g := box.grow(4.5)
	for p in [Vector3(g.position.x, y, g.position.z), Vector3(g.end.x, y, g.position.z), Vector3(g.position.x, y, g.end.z),
			Vector3(g.end.x, y, g.end.z), Vector3(g.get_center().x, y, g.position.z), Vector3(g.get_center().x, y, g.end.z)]:
		out.append(_point("hydrant", p, _rng.randf_range(0, 360)))
	for id in main.entries.keys():
		var e: Dictionary = main.entries[id]
		if str(e["data"].get("type", "")) in ["Pump", "Compressor", "Expander"]:
			var b: AABB = main._node_bounds(e["node"])
			out.append(_point("extinguisher", Vector3(b.end.x + 0.9, y, b.end.z + 0.6), 0.0))
	# lamp posts round the plant, facing in
	var lx0 := box.position.x - 7.0
	var lx1 := box.end.x + 7.0
	var lz0 := box.position.z - 7.0
	var lz1 := box.end.z + 7.0
	var spots := []
	var nx := maxi(1, int(ceil((lx1 - lx0) / 28.0)))
	var nz := maxi(1, int(ceil((lz1 - lz0) / 28.0)))
	for i in nx + 1:
		var x := lerpf(lx0, lx1, float(i) / nx)
		spots.append([Vector3(x, y, lz0), Vector3(0, 0, 1)])
		spots.append([Vector3(x, y, lz1), Vector3(0, 0, -1)])
	for j in range(1, nz):
		var z := lerpf(lz0, lz1, float(j) / nz)
		spots.append([Vector3(lx0, y, z), Vector3(1, 0, 0)])
		spots.append([Vector3(lx1, y, z), Vector3(-1, 0, 0)])
	for s in spots.slice(0, 24):
		var inward: Vector3 = s[1]
		out.append(_point("lamp", s[0], rad_to_deg(atan2(inward.x, inward.z))))
	# woods on the grass outside the fence, clear of the road (and of the buildings in front)
	var o := 75.0 if campus else 45.0
	var c := 5.0
	var rects := [
		[x0 - o, z0 - o, x0 - c, z1 + o], [x1 + c, z0 - o, x1 + o, z1 + o], [x0 - c, z0 - o, x1 + c, z0 - c],
		[x0 - c, z1 + c, gate_x - 7.0, z1 + o], [gate_x + 7.0, z1 + c, x1 + c, z1 + o]]
	if campus:
		# the buildings take the front, along the road
		var keep := []
		for r in rects:
			for piece in _cut(r, [gate_x - 64.0, z1 + 4.0, gate_x + 64.0, z1 + 50.0]):
				keep.append_array(_cut(piece, [gate_x - 7.0, z1, gate_x + 7.0, z1 + 115.0]))
		rects = keep
	for r in rects:
		if r[2] - r[0] > 4.0 and r[3] - r[1] > 4.0:
			out.append(_shape("woods", [Vector3(r[0], y, r[1]), Vector3(r[2], y, r[1]), Vector3(r[2], y, r[3]), Vector3(r[0], y, r[3])], y))
	if campus:
		out.append_array(_campus(box, gate_x, x0, x1, z0, z1, y))
	return out


## What is left of the rectangle r ([x0, z0, x1, z1]) outside the rectangle hole: up to four pieces.
static func _cut(r: Array, hole: Array) -> Array:
	if hole[0] >= r[2] or hole[2] <= r[0] or hole[1] >= r[3] or hole[3] <= r[1]:
		return [r]
	var out := []
	if hole[1] > r[1]:
		out.append([r[0], r[1], r[2], hole[1]])
	if hole[3] < r[3]:
		out.append([r[0], hole[3], r[2], r[3]])
	var za: float = maxf(r[1], hole[1])
	var zb: float = minf(r[3], hole[3])
	if hole[0] > r[0]:
		out.append([r[0], za, hole[0], zb])
	if hole[2] < r[2]:
		out.append([hole[2], za, r[2], zb])
	return out


## The buildings of a plant in front of the gate, on both sides of the road (the road runs along +z
## from the gate at gate_x, 7 m wide): a guardhouse by the gate; on the left the administration building with
## flags, a parking lot, the canteen and a lawn with the assembly point; on the right the
## maintenance workshop, the warehouse with containers and the substation, whose power line runs
## out along the road. Lamp posts and rows of trees line the road beyond, and the plant gets safety
## showers and a windsock.
func _campus(box: AABB, gate_x: float, x0: float, x1: float, z0: float, z1: float, y: float) -> Array:
	var out := []
	var gx := gate_x
	# left of the road
	out.append(_point("guardhouse", Vector3(gx + 7.0, y, z1 + 5.0), 0.0))
	out.append(_point("admin", Vector3(gx - 21.5, y, z1 + 16.0), 0.0))
	for i in 3:
		out.append(_point("flag", Vector3(gx - 6.5, y, z1 + 12.0 + i * 2.5), 0.0))
	out.append(_shape("parking", [Vector3(gx - 50.0, y, z1 + 9.0), Vector3(gx - 33.0, y, z1 + 9.0),
		Vector3(gx - 33.0, y, z1 + 25.0), Vector3(gx - 50.0, y, z1 + 25.0)], y))
	var cars := [["suv", ""], ["suv", ""], ["delivery", ""], ["suv", ""], ["suv", ""]]
	for i in cars.size():
		out.append(_point(cars[i][0], Vector3(gx - 47.5 + i * 3.0, y, z1 + 12.5 + (i % 2) * 9.0), 90.0, cars[i][1]))
	out.append(_shape("asphalt", [Vector3(gx - 50.0, y, z1 + 25.5), Vector3(gx - 3.5, y, z1 + 25.5),
		Vector3(gx - 3.5, y, z1 + 29.0), Vector3(gx - 50.0, y, z1 + 29.0)], y))
	out.append(_point("canteen", Vector3(gx - 18.0, y, z1 + 37.0), 0.0))
	out.append(_point("parasol", Vector3(gx - 30.0, y, z1 + 34.0), 0.0))
	out.append(_point("parasol", Vector3(gx - 30.0, y, z1 + 39.0), 0.0))
	out.append(_shape("grass", [Vector3(gx - 56.0, y, z1 + 31.0), Vector3(gx - 35.0, y, z1 + 31.0),
		Vector3(gx - 35.0, y, z1 + 46.0), Vector3(gx - 56.0, y, z1 + 46.0)], y))
	out.append(_point("assembly", Vector3(gx - 45.5, y, z1 + 38.5), 0.0))
	for p in [Vector3(gx - 54.0, y, z1 + 33.0), Vector3(gx - 54.0, y, z1 + 44.0), Vector3(gx - 37.0, y, z1 + 44.0)]:
		out.append(_point("tree", p, _rng.randf_range(0, 360)))
	# right of the road
	out.append(_point("workshop", Vector3(gx + 18.0, y, z1 + 14.0), 0.0))
	out.append(_point("dumpster", Vector3(gx + 28.5, y, z1 + 10.5), 90.0))
	out.append(_point("dumpster", Vector3(gx + 28.5, y, z1 + 13.0), 90.0))
	out.append(_point("warehouse", Vector3(gx + 22.0, y, z1 + 34.0), 0.0))
	out.append(_shape("gravel", [Vector3(gx + 35.0, y, z1 + 25.0), Vector3(gx + 50.0, y, z1 + 25.0),
		Vector3(gx + 50.0, y, z1 + 43.0), Vector3(gx + 35.0, y, z1 + 43.0)], y))
	for i in 3:
		out.append(_point("container", Vector3(gx + 39.0 + i * 3.5, y, z1 + 31.0), 90.0))
	out.append(_point("substation", Vector3(gx + 44.0, y, z1 + 14.0), 0.0))
	out.append(_shape("powerline", [Vector3(gx + 51.0, y, z1 + 14.0), Vector3(gx + 58.0, y, z1 + 14.0),
		Vector3(gx + 58.0, y, z1 + 115.0)], y))
	# along the road beyond the buildings
	var z := z1 + 8.0
	while z <= z1 + 104.0:
		out.append(_point("lamp", Vector3(gx - 5.0, y, z), 90.0))
		out.append(_point("lamp", Vector3(gx + 5.0, y, z + 12.0), 270.0))
		z += 24.0
	out.append(_shape("treerow", [Vector3(gx - 7.0, y, z1 + 52.0), Vector3(gx - 7.0, y, z1 + 108.0)], y))
	out.append(_shape("treerow", [Vector3(gx + 7.0, y, z1 + 52.0), Vector3(gx + 7.0, y, z1 + 108.0)], y))
	# on the plant: safety showers at the middle of the sides, a windsock at the back corner
	var g := box.grow(4.5)
	out.append(_point("shower", Vector3(g.position.x, y, g.get_center().z), 90.0))
	out.append(_point("shower", Vector3(g.end.x, y, g.get_center().z), 270.0))
	out.append(_point("windsock", Vector3(x1 - 4.0, y, z0 + 4.0), 0.0))
	return out
