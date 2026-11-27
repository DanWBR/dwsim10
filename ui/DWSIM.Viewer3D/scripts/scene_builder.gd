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
	m.set_meta("kind", "cladding" if str(color).to_lower() == CLADDING else ("metal" if metal else "painted"))
	_detail_on(m, detail)
	_materials[key] = m
	return m


## The surface detail of the materials: the scratches and dents of painted steel, the grain of bare
## metal, the ribs of the cladding on insulation (CC0 maps, see assets/CREDITS.md), over the colour
## each part already has. Off at the Low quality.
var detail := true
var _maps := {}


func _map(name: String) -> Texture2D:
	if not _maps.has(name):
		_maps[name] = load("res://assets/textures/%s.jpg" % name)
	return _maps[name]


func _detail_on(m: StandardMaterial3D, on: bool) -> void:
	var kind := str(m.get_meta("kind", "painted"))
	m.normal_enabled = on
	m.uv1_triplanar = on
	m.uv1_world_triplanar = on
	if not on:
		m.normal_texture = null
		m.roughness_texture = null
		return
	match kind:
		"cladding":
			m.normal_texture = _map("cladding_normal")
			m.normal_scale = 0.8
			m.roughness_texture = null
			m.uv1_scale = Vector3.ONE / 1.2
		"metal":
			m.normal_texture = _map("metal_normal")
			m.normal_scale = 0.5
			m.roughness_texture = _map("metal_roughness")
			m.uv1_scale = Vector3.ONE / 1.5
		_:
			m.normal_texture = _map("painted_normal")
			m.normal_scale = 0.45
			m.roughness_texture = _map("painted_roughness")
			m.uv1_scale = Vector3.ONE / 2.0
	m.roughness_texture_channel = BaseMaterial3D.TEXTURE_CHANNEL_RED


func set_detail(on: bool) -> void:
	detail = on
	for m in _materials.values():
		if m.has_meta("kind"):
			_detail_on(m, on)


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
	if str(it.get("site", "")) == "controlroom":
		# a building of the site: drawn here, turned by its own "rot"
		ControlRoomModel.build(node)
		node.rotation.y = deg_to_rad(float(it.get("rot", 0)))
		node.set_meta("door", ControlRoomModel.DOOR)
		return node
	if bool(it.get("badge", false)):
		_badge(node, it)
		return node
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


## The objects without a body in the plant model: a controller is a giant old computer on the
## ground, an energy stream a spinning bolt with dashed links to the nozzles it joins, and the
## other logical blocks spinning spheres floating over their place.
func _badge(node: Node3D, it: Dictionary) -> void:
	var type := str(it.get("type", ""))
	if type.begins_with("Controller_"):
		_computer(node)
	elif bool(it.get("energy", false)):
		_bolt(node, it.get("links", []))
	else:
		_orb(node, type)


func _glow(c: Color, energy := 0.35, metal := 0.0) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = c
	m.emission_enabled = true
	m.emission = c
	m.emission_energy_multiplier = energy
	m.metallic = metal
	m.roughness = 0.35
	return m


## Something that turns about its own Y axis (main.gd turns the "spin" group).
static func spinning(n: Node3D, speed: float) -> Node3D:
	n.add_to_group("spin")
	n.set_meta("spin", speed)
	return n


func _pole(node: Node3D, color: Color) -> void:
	var h := node.position.y
	if h > 0.5:
		var pole := cylinder(Vector3(0, -h, 0), Vector3(0, -0.45, 0), 0.025, material(color.to_html(false), false), 8)
		if pole != null:
			node.add_child(pole)


func _orb(node: Node3D, type: String) -> void:
	var color := Color(0.55, 0.36, 0.85)
	if type in ["OT_EnergyRecycle", "NodeEn", "EnergyMixer"]:
		color = Color(0.95, 0.75, 0.15)
	elif type in ["Switch", "Input"]:
		color = Color(0.15, 0.65, 0.65)
	var spin := spinning(Node3D.new(), 1.4)
	node.add_child(spin)
	var ball := sphere(Vector3.ZERO, 0.3, _glow(color, 0.3, 0.2), 24)
	spin.add_child(ball)
	var ring_mat := _glow(color.lightened(0.35), 0.5, 0.6)
	for tilt in [Vector3(deg_to_rad(20), 0, 0), Vector3(deg_to_rad(90), 0, deg_to_rad(25))]:
		var t := TorusMesh.new()
		t.inner_radius = 0.40
		t.outer_radius = 0.45
		t.rings = 32
		t.ring_segments = 6
		var mi := MeshInstance3D.new()
		mi.mesh = t
		mi.material_override = ring_mat
		mi.rotation = tilt
		spin.add_child(mi)
	_pole(node, Color(0.54, 0.57, 0.61))


func _box(parent: Node3D, size: Vector3, at: Vector3, mat: Material) -> MeshInstance3D:
	var b := BoxMesh.new()
	b.size = size
	var mi := MeshInstance3D.new()
	mi.mesh = b
	mi.material_override = mat
	mi.position = at
	parent.add_child(mi)
	return mi


## A controller: a mainframe cabinet of the old days, with a green screen, rows of blinking
## lamps and two tape reels that turn.
func _computer(node: Node3D) -> void:
	var beige := material("#d8cfb6", false)
	var dark := material("#2c3035", false)
	_box(node, Vector3(1.5, 0.08, 0.9), Vector3(0, 0.04, 0), dark)
	_box(node, Vector3(1.4, 2.1, 0.8), Vector3(0, 1.13, 0), beige)
	_box(node, Vector3(1.2, 1.55, 0.02), Vector3(0, 1.25, 0.41), dark)
	_box(node, Vector3(0.5, 0.3, 0.03), Vector3(0, 1.12, 0.425), _glow(Color(0.22, 0.83, 0.33), 1.2))
	for side in [-0.3, 0.3]:
		var pivot := Node3D.new()
		pivot.position = Vector3(side, 1.67, 0.44)
		pivot.rotation = Vector3(deg_to_rad(90), 0, 0)
		node.add_child(pivot)
		var reel := spinning(MeshInstance3D.new(), 2.5 if side < 0 else -2.0) as MeshInstance3D
		var c := CylinderMesh.new()
		c.top_radius = 0.2
		c.bottom_radius = 0.2
		c.height = 0.04
		c.radial_segments = 24
		reel.mesh = c
		reel.material_override = material("#9aa0a6", true)
		pivot.add_child(reel)
		_box(reel, Vector3(0.38, 0.05, 0.04), Vector3.ZERO, dark)
		_box(reel, Vector3(0.04, 0.05, 0.38), Vector3.ZERO, dark)
	var lamps := [Color(0.95, 0.2, 0.15), Color(1.0, 0.7, 0.1), Color(0.2, 0.9, 0.3)]
	for row in 3:
		for col in 9:
			var k := row * 9 + col
			var lamp := _box(node, Vector3(0.05, 0.05, 0.02), Vector3(-0.44 + col * 0.11, 0.62 + row * 0.1, 0.425), _glow(lamps[(k * 7) % 3], 1.5))
			lamp.add_to_group("blink")
			lamp.set_meta("rate", 0.6 + float((k * 13) % 7) * 0.25)
			lamp.set_meta("phase", float((k * 37) % 100) / 100.0)


## The outline of a lightning bolt, about 0.9 m tall, in the XY plane.
const BOLT := [Vector2(0.10, 0.45), Vector2(-0.20, 0.02), Vector2(0.00, 0.02), Vector2(-0.12, -0.45), Vector2(0.22, 0.07), Vector2(0.02, 0.07)]


static func bolt_mesh(depth := 0.1) -> ArrayMesh:
	var pts := PackedVector2Array(BOLT)
	var tri := Geometry2D.triangulate_polygon(pts)
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var h := depth / 2.0
	for i in range(0, tri.size(), 3):
		for j in [0, 1, 2]:
			var p: Vector2 = pts[tri[i + j]]
			st.add_vertex(Vector3(p.x, p.y, h))
		for j in [2, 1, 0]:
			var p: Vector2 = pts[tri[i + j]]
			st.add_vertex(Vector3(p.x, p.y, -h))
	var n := pts.size()
	for i in n:
		var a: Vector2 = pts[i]
		var b: Vector2 = pts[(i + 1) % n]
		for v in [Vector3(a.x, a.y, h), Vector3(b.x, b.y, h), Vector3(b.x, b.y, -h), Vector3(a.x, a.y, h), Vector3(b.x, b.y, -h), Vector3(a.x, a.y, -h)]:
			st.add_vertex(v)
	st.generate_normals()
	return st.commit()


## An energy stream: a golden bolt turning over its place, with dashed links to its nozzles.
func _bolt(node: Node3D, links: Array) -> void:
	var gold := _glow(Color(1.0, 0.78, 0.2), 0.9, 0.5)
	gold.cull_mode = BaseMaterial3D.CULL_DISABLED
	var bolt := spinning(MeshInstance3D.new(), 1.6) as MeshInstance3D
	bolt.mesh = bolt_mesh()
	bolt.material_override = gold
	bolt.scale = Vector3.ONE * 1.6
	node.add_child(bolt)
	var dash_mat := material("#f0a030", false)
	for end in links:
		var to := vec(end) - node.position
		var length := to.length()
		if length < 0.6:
			continue
		var dirn := to / length
		var k := 0.5
		while k < length:
			var c := cylinder(dirn * k, dirn * minf(k + 0.3, length), 0.022, dash_mat, 6)
			if c != null:
				node.add_child(c)
			k += 0.5


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
	_flow(node, pts, r, Color.from_string(str(ln.get("c", "#888888")), Color.GRAY), float(ln.get("v", 0.0)))
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


var _flow_shader: Shader = preload("res://shaders/flow.gdshader")
var _flow_mats := {}


## The pace of the dashes along a line, m/s on the screen, from the velocity of its stream.
static func flow_speed(v: float) -> float:
	return 0.0 if v <= 0.0 else 0.4 + minf(4.0, v) * 0.6


## Dashes that run along the line in the direction of flow (the "flow" group, which the window
## shows while the animation is on).
func _flow(node: Node3D, pts: Array[Vector3], r: float, color: Color, v: float) -> void:
	var key := color.to_html(false)
	if not _flow_mats.has(key):
		var m := ShaderMaterial.new()
		m.shader = _flow_shader
		m.set_shader_parameter("color", color.lightened(0.35))
		_flow_mats[key] = m
	var start := 0.0
	for i in range(pts.size() - 1):
		var a: Vector3 = pts[i]
		var b: Vector3 = pts[i + 1]
		var l := a.distance_to(b)
		if l < 1e-4:
			continue
		var c := CylinderMesh.new()
		c.top_radius = r * 1.15
		c.bottom_radius = r * 1.15
		c.height = l
		c.radial_segments = 12
		c.rings = 1
		c.cap_top = false
		c.cap_bottom = false
		var mi := MeshInstance3D.new()
		mi.mesh = c
		mi.material_override = _flow_mats[key]
		mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		mi.transform = Transform3D(basis_along(b - a), a + (b - a) * 0.5)
		mi.set_instance_shader_parameter("seg_len", l)
		mi.set_instance_shader_parameter("start", start)
		mi.set_instance_shader_parameter("speed", flow_speed(v))
		mi.add_to_group("flow")
		mi.set_meta("flow", true)
		node.add_child(mi)
		start += l


## A new velocity for the dashes of a line already built.
static func set_flow(node: Node3D, v: float) -> void:
	for mi in node.get_children():
		if mi.has_meta("flow"):
			(mi as GeometryInstance3D).set_instance_shader_parameter("speed", flow_speed(v))


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
