## Turns the scene DWSIM sends (SceneBuilder.cs) into Godot nodes: one node per item at its origin
## with its solids under it, the lines with their elbows, flanges and insulation, and the rack.
## Lengths come in mm in the DWSIM frame (Z up) and go out in m in the Godot frame (Y up).
class_name SceneBuilder
extends RefCounted

const MM := 0.001
const CLADDING := "#c4cad1"
const STEEL := "#6b7075"

var _materials := {}


static func g(v: Vector3) -> Vector3:
	return Vector3(v.x, v.z, -v.y)


static func vec(a) -> Vector3:
	if a is Array and a.size() >= 3:
		return g(Vector3(float(a[0]), float(a[1]), float(a[2])) * MM)
	return Vector3.ZERO


## A direction (unitless) from DWSIM to Godot.
static func dir(a) -> Vector3:
	if a is Array and a.size() >= 3:
		var d := g(Vector3(float(a[0]), float(a[1]), float(a[2])))
		return d.normalized() if d.length() > 1e-9 else Vector3.UP
	return Vector3.UP


func material(color, metal: bool) -> StandardMaterial3D:
	var key := str(color) + ("m" if metal else "")
	if _materials.has(key):
		return _materials[key]
	var m := StandardMaterial3D.new()
	m.albedo_color = Color.from_string(str(color), Color(0.6, 0.62, 0.65))
	m.metallic = 0.65 if metal else 0.0
	m.roughness = 0.38 if metal else 0.72
	if metal:
		m.metallic_specular = 0.6
	_materials[key] = m
	return m


## The basis that turns the mesh's Y axis onto a direction.
static func basis_along(d: Vector3) -> Basis:
	var n := d.normalized()
	if n.dot(Vector3.UP) > 0.99999:
		return Basis.IDENTITY
	if n.dot(Vector3.UP) < -0.99999:
		return Basis(Vector3.RIGHT, PI)
	return Basis(Quaternion(Vector3.UP, n))


func cylinder(a: Vector3, b: Vector3, r: float, mat: Material, segments := 24) -> MeshInstance3D:
	var d := b - a
	var l := d.length()
	if l < 1e-6 or r <= 0.0:
		return null
	var mesh := CylinderMesh.new()
	mesh.top_radius = r
	mesh.bottom_radius = r
	mesh.height = l
	mesh.radial_segments = segments
	mesh.rings = 1
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	mi.material_override = mat
	mi.transform = Transform3D(basis_along(d), a + d * 0.5)
	return mi


func sphere(at: Vector3, r: float, mat: Material, segments := 24) -> MeshInstance3D:
	if r <= 0.0:
		return null
	var mesh := SphereMesh.new()
	mesh.radius = r
	mesh.height = 2.0 * r
	mesh.radial_segments = segments
	mesh.rings = max(4, segments / 2)
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	mi.material_override = mat
	mi.position = at
	return mi


## One solid of an item, around the item's origin.
func part(p: Dictionary) -> MeshInstance3D:
	var mat := material(p.get("c", "#999999"), bool(p.get("m", false)))
	var at := vec(p.get("p"))
	match str(p.get("k", "")):
		"Cylinder":
			var a := dir(p.get("a"))
			return cylinder(at, at + a * float(p.get("h", 0)) * MM, float(p.get("d", 0)) * MM / 2.0, mat, 32)
		"Box":
			var s: Array = p.get("s", [0, 0, 0])
			var mesh := BoxMesh.new()
			mesh.size = Vector3(float(s[0]), float(s[2]), float(s[1])) * MM
			var mi := MeshInstance3D.new()
			mi.mesh = mesh
			mi.material_override = mat
			mi.position = at
			return mi
		"Sphere":
			return sphere(at, float(p.get("d", 0)) * MM / 2.0, mat, 32)
		"Torus":
			var big := float(p.get("R", 0)) * MM
			var small := float(p.get("r", 0)) * MM
			if small <= 0.0 or big <= small:
				return null
			var t := TorusMesh.new()
			t.inner_radius = big - small
			t.outer_radius = big + small
			t.rings = 48
			t.ring_segments = 12
			var mi := MeshInstance3D.new()
			mi.mesh = t
			mi.material_override = mat
			mi.position = at
			return mi
		"Head":
			# a dish of the vessel's diameter and the head's depth, bulging along the axis
			var r := float(p.get("d", 0)) * MM / 2.0
			var depth := float(p.get("h", 0)) * MM
			if r <= 0.0 or depth <= 0.0:
				return null
			var mesh := SphereMesh.new()
			mesh.radius = r
			mesh.height = r
			mesh.is_hemisphere = true
			mesh.radial_segments = 32
			mesh.rings = 12
			var mi := MeshInstance3D.new()
			mi.mesh = mesh
			mi.material_override = mat
			mi.transform = Transform3D(basis_along(dir(p.get("a"))) * Basis.from_scale(Vector3(1.0, depth / r, 1.0)), at)
			return mi
	return null


## An item: a node at its origin with its solids and its label.
func item(it: Dictionary) -> Node3D:
	var node := Node3D.new()
	node.name = "item"
	node.position = vec(it.get("o"))
	for p in it.get("parts", []):
		if p is Dictionary:
			var mi := part(p)
			if mi != null:
				node.add_child(mi)
	if node.get_child_count() == 0:
		# a point node (a recycle block): a small marker, so it can still be picked
		var mi := sphere(Vector3(0, 0.3, 0), 0.25, material("#9aa3ad", false), 12)
		node.add_child(mi)
	return node


## A line: the pipe, its elbows and flanges; the insulation goes to its own group.
func line(ln: Dictionary, insulation: Node3D) -> Node3D:
	var node := Node3D.new()
	node.name = "line"
	var mat := material(ln.get("c", "#888888"), true)
	var r := float(ln.get("od", 0)) * MM / 2.0
	var pts: Array[Vector3] = []
	for v in ln.get("path", []):
		pts.append(vec(v))
	for i in range(pts.size() - 1):
		var c := cylinder(pts[i], pts[i + 1], r, mat, 16)
		if c != null:
			node.add_child(c)
	for i in range(1, pts.size() - 1):
		var s := sphere(pts[i], r, mat, 16)
		if s != null:
			node.add_child(s)
	var flange := material(STEEL, true)
	for f in ln.get("fl", []):
		if f is Array and f.size() == 2:
			var face := vec(f[0])
			var d := dir(f[1])
			var t := maxf(30.0, 0.25 * float(ln.get("od", 0))) * MM
			var c := cylinder(face - d * t / 2.0, face + d * t / 2.0, r * 1.7, flange, 24)
			if c != null:
				node.add_child(c)
	var ins := float(ln.get("ins", 0)) * MM
	if ins > 0.0 and insulation != null:
		var clad := material(CLADDING, true)
		for i in range(pts.size() - 1):
			var c := cylinder(pts[i], pts[i + 1], r + ins, clad, 16)
			if c != null:
				insulation.add_child(c)
		for i in range(1, pts.size() - 1):
			var s := sphere(pts[i], r + ins, clad, 16)
			if s != null:
				insulation.add_child(s)
	return node


## The single-tier rack: columns at each bay, cross beams and the two side beams.
func rack(k: Dictionary) -> Node3D:
	var node := Node3D.new()
	node.name = "rack"
	var steel := material(STEEL, true)
	var x0 := float(k.get("x0", 0))
	var x1 := float(k.get("x1", 0))
	var ya := float(k.get("y0", 0))
	var yb := ya + float(k.get("w", 0))
	var zb := float(k.get("top", 0)) - float(k.get("beam", 0)) / 2.0
	var bay_len := float(k.get("bay", 6000))
	var bays := maxi(1, ceili((x1 - x0) / bay_len))
	var bay := (x1 - x0) / bays
	var half := 0.075
	var p := func(x: float, y: float, z: float) -> Vector3: return g(Vector3(x, y, z) * MM)
	for i in range(bays + 1):
		var x := x0 + i * bay
		for c in [cylinder(p.call(x, ya, 0), p.call(x, ya, zb), half, steel, 6),
				cylinder(p.call(x, yb, 0), p.call(x, yb, zb), half, steel, 6),
				cylinder(p.call(x, ya, zb), p.call(x, yb, zb), half, steel, 6)]:
			if c != null:
				node.add_child(c)
	for c in [cylinder(p.call(x0, ya, zb), p.call(x1, ya, zb), half, steel, 6),
			cylinder(p.call(x0, yb, zb), p.call(x1, yb, zb), half, steel, 6)]:
		if c != null:
			node.add_child(c)
	return node
