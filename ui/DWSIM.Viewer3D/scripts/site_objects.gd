## How each object of the site looks (the kinds of the catalog DWSIM sends, SiteCatalog.cs): a
## point object stands at its node and turns with it; a line follows its points and an area fills
## its outline, both given in the DWSIM frame (mm) and drawn relative to the node's place. The
## models are CC0 (Kenney, Poly Haven; see assets/CREDITS.md); the rest is made here. Used for the
## objects in the scene, for the site the window draws by itself before it is adopted, for the
## ghost of the build tool and for the pictures of the build bar.
class_name SiteObjects
extends RefCounted

const KENNEY := "res://assets/models/kenney/"
const HAVEN := "res://assets/models/polyhaven/"
const MM := 0.001

## The models of the point kinds: path, and the size to scale them to (its length, or its height
## when the third value is true); 0 keeps the model's own size.
const MODELS := {
	"suv": [KENNEY + "vehicles/suv.glb", 4.7, false],
	"delivery": [KENNEY + "vehicles/delivery.glb", 5.6, false],
	"truck": [KENNEY + "vehicles/truck-flat.glb", 7.0, false],
	"truck:box": [KENNEY + "vehicles/truck.glb", 6.5, false],
	"firetruck": [KENNEY + "vehicles/firetruck.glb", 8.0, false],
	"loader": [KENNEY + "vehicles/tractor-shovel.glb", 6.0, false],
	"cone": [KENNEY + "vehicles/cone.glb", 0.7, true],
	"hydrant": [HAVEN + "fire_hydrant/fire_hydrant.gltf", 0.0, false],
	"extinguisher": [HAVEN + "korean_fire_extinguisher_01/korean_fire_extinguisher_01.gltf", 0.0, false],
	"barrel": [HAVEN + "Barrel_01/Barrel_01.gltf", 0.0, false],
	"crate": [HAVEN + "wooden_crate_01/wooden_crate_01.gltf", 0.0, false],
	"lpgtank": [HAVEN + "small_lpg_tank/small_lpg_tank.gltf", 0.0, false],
	"utilitybox": [HAVEN + "utility_box_01/utility_box_01.gltf", 0.0, false],
	"barrier": [HAVEN + "concrete_road_barrier/concrete_road_barrier.gltf", 0.0, false],
}

## Walls, roof and size (w, d, h, m) of the buildings that are made here.
const BUILDINGS := {
	"admin": [Color(0.86, 0.85, 0.8), Color(0.35, 0.37, 0.4)],
	"workshop": [Color(0.62, 0.66, 0.7), Color(0.3, 0.45, 0.62)],
	"warehouse": [Color(0.72, 0.74, 0.76), Color(0.55, 0.57, 0.6)],
	"substation": [Color(0.8, 0.78, 0.7), Color(0.4, 0.4, 0.42)],
	"canteen": [Color(0.9, 0.84, 0.72), Color(0.62, 0.32, 0.22)],
	"guardhouse": [Color(0.88, 0.88, 0.86), Color(0.25, 0.3, 0.38)],
}

## How far above the ground each kind of area lies, so overlapping areas do not flicker.
const AREA_LIFT := {"grass": 0.014, "woods": 0.016, "gravel": 0.018, "concrete": 0.022, "asphalt": 0.026, "parking": 0.026, "bund": 0.022}

static var _scenes := {}
static var _mats := {}
static var _catalog := {}


## The catalog the window got from DWSIM: kind -> entry (name, cat, shape, w, d, h, cost, unit).
static func set_catalog(entries: Array) -> void:
	_catalog.clear()
	for e in entries:
		if e is Dictionary:
			_catalog[str(e.get("kind", ""))] = e


## Shapes and sizes (w, d, h, m) of the kinds, for when the window runs without DWSIM's catalog
## (offline pictures): the same as SiteCatalog.cs.
const FALLBACK := {
	"controlroom": ["point", 12, 7, 3.8], "admin": ["point", 20, 12, 8], "workshop": ["point", 16, 10, 6],
	"warehouse": ["point", 24, 16, 8], "substation": ["point", 12, 8, 4], "canteen": ["point", 14, 10, 4],
	"guardhouse": ["point", 4, 4, 3], "road": ["line", 7, 0, 0], "path": ["line", 1.5, 0, 0],
	"asphalt": ["area", 0, 0, 0], "concrete": ["area", 0, 0, 0], "gravel": ["area", 0, 0, 0], "parking": ["area", 0, 0, 0],
	"fence": ["line", 0.1, 0, 2.4], "wall": ["line", 0.25, 0, 2.5], "gate": ["point", 8, 0.3, 2.4],
	"barrier": ["point", 2, 0.6, 0.8], "bund": ["area", 0, 0, 0], "tree": ["point", 4, 4, 7], "treerow": ["line", 4, 0, 7],
	"hedge": ["line", 1, 0, 1.2], "woods": ["area", 0, 0, 0], "grass": ["area", 0, 0, 0], "rock": ["point", 1.5, 1.5, 1],
	"suv": ["point", 4.8, 2, 1.8], "delivery": ["point", 5.5, 2.1, 2.4], "truck": ["point", 8, 2.5, 3],
	"firetruck": ["point", 9, 2.5, 3.2], "loader": ["point", 6, 2.6, 3.2], "hydrant": ["point", 0.5, 0.5, 0.9],
	"extinguisher": ["point", 0.4, 0.4, 0.7], "shower": ["point", 1.2, 1.2, 2.4], "assembly": ["point", 1, 0.2, 2.5],
	"windsock": ["point", 1, 1, 8], "cone": ["point", 0.4, 0.4, 0.7], "lamp": ["point", 0.4, 0.4, 9],
	"flag": ["point", 0.3, 0.3, 10], "lpgtank": ["point", 3, 1.2, 1.4], "utilitybox": ["point", 1, 0.5, 1.5],
	"barrel": ["point", 0.6, 0.6, 0.9], "crate": ["point", 1.2, 1.2, 1.2],
}


static func entry(kind: String) -> Dictionary:
	if _catalog.has(kind):
		return _catalog[kind]
	if FALLBACK.has(kind):
		var f: Array = FALLBACK[kind]
		return {"kind": kind, "shape": f[0], "w": f[1], "d": f[2], "h": f[3]}
	return {}


static func shape_of(kind: String) -> String:
	return str(entry(kind).get("shape", "point"))


# --- materials and models ---------------------------------------------------------------------

static func _mat(key: String) -> StandardMaterial3D:
	if _mats.has(key):
		return _mats[key]
	var m := StandardMaterial3D.new()
	match key:
		"concrete", "asphalt", "path", "wall", "bund":
			m.albedo_texture = load("res://assets/textures/concrete_albedo.jpg")
			m.uv1_triplanar = true
			m.uv1_world_triplanar = true
			m.uv1_scale = Vector3.ONE / 3.0
			m.albedo_color = {"concrete": Color(0.82, 0.8, 0.76), "asphalt": Color(0.3, 0.3, 0.31), "path": Color(0.9, 0.87, 0.8),
				"wall": Color(0.78, 0.77, 0.74), "bund": Color(0.74, 0.73, 0.7)}[key]
			m.roughness = 0.9
		"gravel":
			m.albedo_texture = load("res://assets/textures/gravel_albedo.jpg")
			m.uv1_triplanar = true
			m.uv1_world_triplanar = true
			m.uv1_scale = Vector3.ONE / 2.0
		"grass", "woods":
			m.albedo_texture = load("res://assets/textures/grass_albedo.jpg")
			m.uv1_triplanar = true
			m.uv1_world_triplanar = true
			m.uv1_scale = Vector3.ONE / 3.0
			m.albedo_color = Color(1, 1, 1) if key == "grass" else Color(0.62, 0.6, 0.45)
		"steel":
			m.albedo_color = Color(0.55, 0.57, 0.6)
			m.metallic = 0.7
			m.roughness = 0.4
		"yellow":
			m.albedo_color = Color(0.92, 0.75, 0.12)
			m.metallic = 0.3
		"white":
			m.albedo_color = Color(0.95, 0.95, 0.93)
			m.roughness = 0.6
		"bark":
			m.albedo_color = Color(0.33, 0.24, 0.16)
		"leaves":
			m.vertex_color_use_as_albedo = true
			m.roughness = 0.9
		"hedge":
			m.albedo_color = Color(0.2, 0.38, 0.14)
			m.roughness = 0.95
		"rock":
			m.albedo_color = Color(0.5, 0.49, 0.46)
			m.roughness = 0.95
		"glass":
			m.albedo_color = Color(0.2, 0.3, 0.38)
			m.metallic = 0.6
			m.roughness = 0.1
		"fence":
			m.albedo_texture = load("res://assets/textures/fence_albedo.png")
			m.normal_enabled = true
			m.normal_texture = load("res://assets/textures/fence_normal.jpg")
			m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
			m.albedo_color = Color(0.75, 0.77, 0.8, 1.0)
			m.cull_mode = BaseMaterial3D.CULL_DISABLED
			m.metallic = 0.6
			m.roughness = 0.5
		"green":
			m.albedo_color = Color(0.1, 0.55, 0.3)
		"orange":
			m.albedo_color = Color(0.95, 0.45, 0.08)
		"red":
			m.albedo_color = Color(0.8, 0.12, 0.1)
		_:
			m.albedo_color = Color.from_string(key, Color(0.7, 0.7, 0.7))
	_mats[key] = m
	return m


static func _scene(path: String) -> PackedScene:
	if not _scenes.has(path):
		_scenes[path] = load(path) if ResourceLoader.exists(path) else null
	return _scenes[path]


static func _box(parent: Node3D, size: Vector3, at: Vector3, mat: Material) -> MeshInstance3D:
	var m := BoxMesh.new()
	m.size = size
	var mi := MeshInstance3D.new()
	mi.mesh = m
	mi.material_override = mat
	mi.position = at
	parent.add_child(mi)
	return mi


static func _rod(parent: Node3D, r: float, h: float, at: Vector3, mat: Material) -> MeshInstance3D:
	var c := CylinderMesh.new()
	c.top_radius = r
	c.bottom_radius = r
	c.height = h
	c.radial_segments = 8
	var mi := MeshInstance3D.new()
	mi.mesh = c
	mi.material_override = mat
	mi.position = at
	parent.add_child(mi)
	return mi


## The points of a line or an area, relative to the node (Godot frame, m, on the ground).
static func local_points(it: Dictionary) -> PackedVector3Array:
	var o = it.get("o", [0, 0, 0])
	var ox := float(o[0])
	var oy := float(o[1])
	var pts := PackedVector3Array()
	for p in it.get("points", []):
		pts.append(Vector3((float(p[0]) - ox) * MM, 0.0, -(float(p[1]) - oy) * MM))
	return pts


static func _seed(it: Dictionary) -> int:
	var key := str(it.get("id", ""))
	if key == "":
		key = str(it.get("o", [])) + str(it.get("points", []))
	return hash(key)


# --- building an object ------------------------------------------------------------------------

## Draws the object of the site under its node (already at its place); a point object is turned
## by its "rot".
static func build(node: Node3D, it: Dictionary) -> void:
	var kind := str(it.get("site", it.get("kind", "")))
	var shape := str(it.get("shape", shape_of(kind)))
	var scale := float(it.get("scale", 1.0))
	node.set_meta("site", kind)
	if shape == "line":
		_line(node, kind, local_points(it), it)
		return
	if shape == "area":
		_area(node, kind, local_points(it), it)
		return
	node.rotation.y = deg_to_rad(float(it.get("rot", 0)))
	var body := Node3D.new()
	body.scale = Vector3.ONE * scale
	node.add_child(body)
	if kind == "controlroom":
		ControlRoomModel.build(body)
		node.set_meta("door", ControlRoomModel.DOOR * scale)
	elif MODELS.has(kind + ":" + str(it.get("variant", ""))):
		_model(body, MODELS[kind + ":" + str(it.get("variant", ""))])
	elif MODELS.has(kind):
		_model(body, MODELS[kind])
	elif BUILDINGS.has(kind):
		_building(body, kind)
	else:
		match kind:
			"tree": _tree(body, RandomNumberGenerator.new(), _seed(it))
			"lamp": _lamp(body)
			"gate": _gate(body, float(entry(kind).get("w", 8.0)))
			"rock": _rock(body)
			"flag": _flag(body)
			"windsock": _windsock(body)
			"shower": _shower(body)
			"assembly": _assembly(body)
			_:
				var e := entry(kind)
				var s := Vector3(float(e.get("w", 1.0)), float(e.get("h", 1.0)), float(e.get("d", 1.0)))
				_box(body, s, Vector3(0, s.y / 2.0, 0), _mat("#b0b4b8"))


static func _model(parent: Node3D, spec: Array) -> void:
	var ps := _scene(spec[0])
	if ps == null:
		_box(parent, Vector3(1, 1, 1), Vector3(0, 0.5, 0), _mat("#b0b4b8"))
		return
	var n: Node3D = ps.instantiate()
	parent.add_child(n)
	var size: float = spec[1]
	if size > 0.0:
		var b := _bounds_of(n)
		var have := b.size.y if spec[2] else maxf(b.size.x, b.size.z)
		if have > 0.0:
			n.scale = Vector3.ONE * (size / have)


## The bounds of a node's meshes in the node's own frame (it need not be in the tree).
static func _bounds_of(n: Node3D) -> AABB:
	var box := AABB()
	var first := true
	for mi in n.find_children("*", "MeshInstance3D", true, false):
		var t := Transform3D.IDENTITY
		var p: Node = mi
		while p != null and p != n:
			if p is Node3D:
				t = (p as Node3D).transform * t
			p = p.get_parent()
		var b: AABB = t * (mi as MeshInstance3D).get_aabb()
		box = b if first else box.merge(b)
		first = false
	return box


static func _building(parent: Node3D, kind: String) -> void:
	var e := entry(kind)
	var w := float(e.get("w", 10.0))
	var d := float(e.get("d", 8.0))
	var h := float(e.get("h", 4.0))
	var colors: Array = BUILDINGS[kind]
	var walls := StandardMaterial3D.new()
	walls.albedo_texture = load("res://assets/textures/concrete_albedo.jpg")
	walls.albedo_color = colors[0]
	walls.uv1_triplanar = true
	walls.uv1_scale = Vector3.ONE / 3.0
	_box(parent, Vector3(w, h, d), Vector3(0, h / 2.0, 0), walls)
	var roof := _mat("#" + colors[1].to_html(false))
	roof.metallic = 0.4
	_box(parent, Vector3(w + 0.4, 0.25, d + 0.4), Vector3(0, h + 0.12, 0), roof)
	_box(parent, Vector3(w + 1.2, 0.15, d + 1.2), Vector3(0, 0.075, 0), _mat("#8c8c88"))
	# rows of windows that light at night, and a door
	var glass := StandardMaterial3D.new()
	glass.albedo_color = Color(0.2, 0.3, 0.38)
	glass.metallic = 0.6
	glass.roughness = 0.1
	glass.emission_enabled = true
	glass.emission = Color(1.0, 0.85, 0.55)
	glass.emission_energy_multiplier = 0.0
	glass.set_meta("night_glow", 1.4)
	var floors := maxi(1, int(h / 3.5))
	var per := maxi(1, int((w - 2.0) / 2.8))
	for f in floors:
		for i in per:
			var x := -w / 2.0 + 1.6 + i * (w - 3.2) / maxf(1.0, per - 1.0) if per > 1 else 0.0
			if kind == "warehouse" and f == 0:
				continue
			_box(parent, Vector3(1.4, 1.0, 0.06), Vector3(x, 1.6 + f * 3.3, d / 2.0 + 0.01), glass).add_to_group("night_glow")
	if kind == "warehouse":
		_box(parent, Vector3(4.5, 4.5, 0.08), Vector3(0, 2.25, d / 2.0 + 0.02), _mat("#9aa0a6"))
	else:
		_box(parent, Vector3(1.1, 2.2, 0.06), Vector3(w / 2.0 - 1.4, 1.1, d / 2.0 + 0.01), _mat("#4c535c"))
	if kind == "substation":
		for i in 3:
			_box(parent, Vector3(1.6, 2.0, 1.4), Vector3(-w / 2.0 + 2.0 + i * 2.6, 1.0, d / 2.0 + 1.8), _mat("#7d8a7a"))


static func tree_meshes() -> Array:
	var tm := CylinderMesh.new()
	tm.top_radius = 0.14
	tm.bottom_radius = 0.22
	tm.height = 1.0
	tm.radial_segments = 6
	var cm := SphereMesh.new()
	cm.radius = 1.0
	cm.height = 2.0
	cm.radial_segments = 10
	cm.rings = 6
	return [tm, cm]


## Trees at the given places, in two multimeshes (trunks and crowns), seeded so they look the same each time.
static func _forest(parent: Node3D, spots: PackedVector3Array, rng: RandomNumberGenerator) -> void:
	if spots.is_empty():
		return
	var meshes := tree_meshes()
	var trunks := MultiMesh.new()
	trunks.transform_format = MultiMesh.TRANSFORM_3D
	trunks.mesh = meshes[0]
	trunks.instance_count = spots.size()
	var crowns := MultiMesh.new()
	crowns.transform_format = MultiMesh.TRANSFORM_3D
	crowns.use_colors = true
	crowns.mesh = meshes[1]
	crowns.instance_count = spots.size() * 2
	for i in spots.size():
		var p: Vector3 = spots[i]
		var h := rng.randf_range(3.0, 6.5)
		var r := h * rng.randf_range(0.35, 0.5)
		trunks.set_instance_transform(i, Transform3D(Basis.from_scale(Vector3(1, h * 0.55, 1)), p + Vector3(0, h * 0.275, 0)))
		for k in 2:
			var off := Vector3(rng.randf_range(-0.4, 0.4), h * (0.62 + 0.18 * k), rng.randf_range(-0.4, 0.4)) * Vector3(r, 1, r)
			var sc := r * (1.0 - 0.25 * k)
			crowns.set_instance_transform(i * 2 + k, Transform3D(Basis.from_scale(Vector3(sc, sc * 0.85, sc)), p + off))
			crowns.set_instance_color(i * 2 + k, Color(0.2, 0.42, 0.16).lerp(Color(0.36, 0.52, 0.2), rng.randf()))
	var a := MultiMeshInstance3D.new()
	a.multimesh = trunks
	a.material_override = _mat("bark")
	parent.add_child(a)
	var b := MultiMeshInstance3D.new()
	b.multimesh = crowns
	b.material_override = _mat("leaves")
	parent.add_child(b)


static func _tree(parent: Node3D, rng: RandomNumberGenerator, seed_value: int) -> void:
	rng.seed = seed_value
	var meshes := tree_meshes()
	var h := rng.randf_range(4.5, 7.0)
	var r := h * rng.randf_range(0.35, 0.45)
	var trunk := MeshInstance3D.new()
	trunk.mesh = meshes[0]
	trunk.material_override = _mat("bark")
	trunk.transform = Transform3D(Basis.from_scale(Vector3(1, h * 0.55, 1)), Vector3(0, h * 0.275, 0))
	parent.add_child(trunk)
	for k in 2:
		var crown := MeshInstance3D.new()
		crown.mesh = meshes[1]
		var m := StandardMaterial3D.new()
		m.albedo_color = Color(0.2, 0.42, 0.16).lerp(Color(0.36, 0.52, 0.2), rng.randf())
		m.roughness = 0.9
		crown.material_override = m
		var sc := r * (1.0 - 0.25 * k)
		crown.transform = Transform3D(Basis.from_scale(Vector3(sc, sc * 0.85, sc)), Vector3(0, h * (0.62 + 0.18 * k), 0))
		parent.add_child(crown)


## A lamp post facing +z: the head and the light lean that way. Lit at night by the environment
## (groups lamp_head and lamp_light).
static func _lamp(parent: Node3D) -> void:
	var pole := MeshInstance3D.new()
	var cm := CylinderMesh.new()
	cm.top_radius = 0.06
	cm.bottom_radius = 0.09
	cm.height = 9.0
	pole.mesh = cm
	pole.material_override = _mat("steel")
	pole.position = Vector3(0, 4.5, 0)
	parent.add_child(pole)
	var hm := StandardMaterial3D.new()
	hm.albedo_color = Color(0.3, 0.31, 0.33)
	hm.emission_enabled = true
	hm.emission = Color(1.0, 0.86, 0.62)
	hm.emission_energy_multiplier = 0.0
	var head := _box(parent, Vector3(0.7, 0.15, 0.35), Vector3(0, 9.0, 0.6), hm)
	head.set_meta("lamp", hm)
	head.add_to_group("lamp_head")
	var light := SpotLight3D.new()
	light.light_color = Color(1.0, 0.86, 0.62)
	light.spot_range = 30.0
	light.spot_angle = 58.0
	light.light_energy = 0.0
	light.shadow_enabled = false
	light.position = head.position + Vector3(0, -0.1, 0)
	var inward := Vector3(0, 0, 1)
	light.basis = Basis.looking_at((Vector3.DOWN + inward * 0.35).normalized(), inward)
	light.add_to_group("lamp_light")
	parent.add_child(light)


static func _gate(parent: Node3D, w: float) -> void:
	for sx in [-1.0, 1.0]:
		_box(parent, Vector3(0.3, 1.3, 0.3), Vector3(sx * w / 2.0, 0.65, 0), _mat("steel"))
	_box(parent, Vector3(w, 0.12, 0.12), Vector3(0, 1.0, 0), _mat("yellow"))
	for i in int(w / 1.0):
		if i % 2 == 0:
			_box(parent, Vector3(0.5, 0.13, 0.13), Vector3(-w / 2.0 + 0.5 + i, 1.0, 0), _mat("red"))


static func _rock(parent: Node3D) -> void:
	var s := SphereMesh.new()
	s.radius = 0.75
	s.height = 1.0
	s.radial_segments = 7
	s.rings = 4
	var mi := MeshInstance3D.new()
	mi.mesh = s
	mi.material_override = _mat("rock")
	mi.position = Vector3(0, 0.3, 0)
	parent.add_child(mi)


static func _flag(parent: Node3D) -> void:
	_rod(parent, 0.05, 10.0, Vector3(0, 5.0, 0), _mat("white"))
	_box(parent, Vector3(1.8, 1.1, 0.02), Vector3(0.95, 9.3, 0), _mat("#2f6db5"))


static func _windsock(parent: Node3D) -> void:
	_rod(parent, 0.05, 8.0, Vector3(0, 4.0, 0), _mat("steel"))
	var c := CylinderMesh.new()
	c.top_radius = 0.3
	c.bottom_radius = 0.12
	c.height = 1.6
	var mi := MeshInstance3D.new()
	mi.mesh = c
	mi.material_override = _mat("orange")
	mi.rotation.z = PI / 2.0
	mi.position = Vector3(0.85, 7.8, 0)
	parent.add_child(mi)


static func _shower(parent: Node3D) -> void:
	for sx in [-0.5, 0.5]:
		_rod(parent, 0.04, 2.4, Vector3(sx, 1.2, 0), _mat("green"))
	_box(parent, Vector3(1.1, 0.06, 0.06), Vector3(0, 2.4, 0), _mat("green"))
	_rod(parent, 0.18, 0.08, Vector3(0, 2.3, 0.2), _mat("green"))
	_box(parent, Vector3(0.5, 0.5, 0.06), Vector3(0, 1.6, 0.06), _mat("#e8e8e8"))


static func _assembly(parent: Node3D) -> void:
	_rod(parent, 0.05, 2.5, Vector3(0, 1.25, 0), _mat("steel"))
	_box(parent, Vector3(0.9, 0.9, 0.04), Vector3(0, 2.1, 0.04), _mat("green"))
	_box(parent, Vector3(0.5, 0.08, 0.05), Vector3(0, 2.1, 0.07), _mat("white"))


# --- lines -------------------------------------------------------------------------------------

static func _segments(pts: PackedVector3Array) -> Array:
	var out := []
	for i in range(1, pts.size()):
		if pts[i - 1].distance_to(pts[i]) > 0.05:
			out.append([pts[i - 1], pts[i]])
	return out


static func _strip(parent: Node3D, a: Vector3, b: Vector3, w: float, h: float, lift: float, mat: Material) -> MeshInstance3D:
	var dir := (b - a).normalized()
	var mi := _box(parent, Vector3(w, h, a.distance_to(b)), Vector3.ZERO, mat)
	mi.transform = Transform3D(Basis.looking_at(dir, Vector3.UP), (a + b) / 2.0 + Vector3(0, lift + h / 2.0, 0))
	return mi


static func _line(node: Node3D, kind: String, pts: PackedVector3Array, it: Dictionary) -> void:
	var e := entry(kind)
	var w := float(e.get("w", 1.0))
	var segs := _segments(pts)
	match kind:
		"road", "path":
			var mat := _mat("asphalt" if kind == "road" else "path")
			var lift := 0.03 if kind == "road" else 0.028
			for s in segs:
				_strip(node, s[0], s[1], w, 0.02, lift, mat).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			# round joints so the bends are not notched
			for i in range(1, pts.size() - 1):
				var c := CylinderMesh.new()
				c.top_radius = w / 2.0
				c.bottom_radius = w / 2.0
				c.height = 0.02
				c.radial_segments = 20
				var mi := MeshInstance3D.new()
				mi.mesh = c
				mi.material_override = mat
				mi.position = pts[i] + Vector3(0, lift + 0.01, 0)
				mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
				node.add_child(mi)
			if kind == "road":
				for s in segs:
					var a: Vector3 = s[0]
					var b: Vector3 = s[1]
					var n := int(a.distance_to(b) / 6.0)
					for k in range(1, n):
						var p := a.lerp(b, float(k) / n)
						var q := a.lerp(b, minf(1.0, (float(k) + 0.4) / n))
						_strip(node, p, q, 0.15, 0.005, lift + 0.02, _mat("white"))
		"fence":
			for s in segs:
				_fence(node, s[0], s[1], 2.2)
		"wall":
			for s in segs:
				_strip(node, s[0], s[1], w, 2.5, 0.0, _mat("wall"))
		"hedge":
			for s in segs:
				_strip(node, s[0], s[1], w, 1.2, 0.0, _mat("hedge"))
		"treerow":
			var spots := PackedVector3Array()
			for s in segs:
				var a: Vector3 = s[0]
				var b: Vector3 = s[1]
				var n := maxi(1, int(a.distance_to(b) / 6.0))
				for k in n + (1 if s == segs[-1] else 0):
					spots.append(a.lerp(b, float(k) / n))
			var rng := RandomNumberGenerator.new()
			rng.seed = _seed(it)
			_forest(node, spots, rng)
			# a thin strip on the ground, so the row can be picked
			for s in segs:
				_strip(node, s[0], s[1], 1.5, 0.01, 0.015, _mat("woods"))
		_:
			for s in segs:
				_strip(node, s[0], s[1], maxf(w, 0.3), maxf(float(e.get("h", 0.2)), 0.05), 0.0, _mat("#b0b4b8"))


## A chain-link fence from a to b: a panel of mesh, a top rail and posts every 3 m.
static func _fence(parent: Node3D, a: Vector3, b: Vector3, height: float) -> void:
	var length := a.distance_to(b)
	var q := QuadMesh.new()
	q.size = Vector2(length, height)
	var panel := MeshInstance3D.new()
	panel.mesh = q
	var m := _mat("fence").duplicate() as StandardMaterial3D
	m.uv1_scale = Vector3(length / 2.5, height / 2.5, 1)
	panel.material_override = m
	var dir := (b - a).normalized()
	panel.transform = Transform3D(Basis(dir, Vector3.UP, dir.cross(Vector3.UP)), (a + b) / 2.0 + Vector3(0, height / 2.0, 0))
	parent.add_child(panel)
	var rail := _rod(parent, 0.035, length, Vector3.ZERO, _mat("steel"))
	rail.transform = Transform3D(SceneBuilder.basis_along(dir), (a + b) / 2.0 + Vector3(0, height, 0))
	var posts := int(ceil(length / 3.0))
	for i in posts + 1:
		_rod(parent, 0.06, height + 0.1, a.lerp(b, float(i) / posts) + Vector3(0, (height + 0.1) / 2.0, 0), _mat("steel"))


# --- areas -------------------------------------------------------------------------------------

## A flat polygon on the ground (local x, z), lifted a little.
static func _polygon(parent: Node3D, pts: PackedVector3Array, lift: float, mat: Material) -> MeshInstance3D:
	var flat := PackedVector2Array()
	for p in pts:
		flat.append(Vector2(p.x, p.z))
	var tris := Geometry2D.triangulate_polygon(flat)
	if tris.is_empty():
		return null
	var verts := PackedVector3Array()
	var normals := PackedVector3Array()
	for i in tris:
		verts.append(Vector3(flat[i].x, lift, flat[i].y))
		normals.append(Vector3.UP)
	# triangulate_polygon follows the winding of the outline; the faces must look up, and the front
	# of a face in Godot is the side its corners turn clockwise on
	var first := (verts[1] - verts[0]).cross(verts[2] - verts[0])
	if first.y > 0.0:
		for k in range(0, verts.size(), 3):
			var t := verts[k + 1]
			verts[k + 1] = verts[k + 2]
			verts[k + 2] = t
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = verts
	arrays[Mesh.ARRAY_NORMAL] = normals
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	mi.material_override = mat
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	parent.add_child(mi)
	return mi


static func _area(node: Node3D, kind: String, pts: PackedVector3Array, it: Dictionary) -> void:
	if pts.size() < 3:
		return
	var lift: float = AREA_LIFT.get(kind, 0.02)
	var key: String = {"parking": "asphalt"}.get(kind, kind)
	_polygon(node, pts, lift, _mat(key if key in ["concrete", "asphalt", "gravel", "grass", "woods", "bund"] else "#9aa0a6"))
	var flat := PackedVector2Array()
	for p in pts:
		flat.append(Vector2(p.x, p.z))
	match kind:
		"woods":
			var rng := RandomNumberGenerator.new()
			rng.seed = _seed(it)
			var box := Rect2(flat[0], Vector2.ZERO)
			for p in flat:
				box = box.expand(p)
			var area := absf(_signed_area(flat))
			var want := clampi(int(area / 130.0), 1, 4000)
			var spots := PackedVector3Array()
			var tries := 0
			while spots.size() < want and tries < want * 20:
				tries += 1
				var p := Vector2(rng.randf_range(box.position.x, box.end.x), rng.randf_range(box.position.y, box.end.y))
				if Geometry2D.is_point_in_polygon(p, flat):
					spots.append(Vector3(p.x, 0, p.y))
			_forest(node, spots, rng)
		"parking":
			# stalls 2.5 m wide and 5 m deep along the first side, in rows 6 m apart
			var a := pts[0]
			var b := pts[1]
			var along := (b - a).normalized()
			var across := Vector3.UP.cross(along).normalized()
			var box := Rect2(flat[0], Vector2.ZERO)
			for p in flat:
				box = box.expand(p)
			var reach := box.size.length()
			var row := 0.0
			while row < reach:
				var s := -reach
				while s < reach:
					var p0 := a + along * s + across * row
					var p1 := p0 + across * 5.0
					if Geometry2D.is_point_in_polygon(Vector2(p0.x, p0.z), flat) and Geometry2D.is_point_in_polygon(Vector2(p1.x, p1.z), flat):
						_strip(node, p0, p1, 0.12, 0.005, lift + 0.01, _mat("white"))
					s += 2.5
				row += 11.0
		"bund":
			for i in pts.size():
				_strip(node, pts[i], pts[(i + 1) % pts.size()], 0.25, 1.0, 0.0, _mat("bund"))


static func _signed_area(flat: PackedVector2Array) -> float:
	var s := 0.0
	for i in flat.size():
		var a := flat[i]
		var b := flat[(i + 1) % flat.size()]
		s += a.x * b.y - b.x * a.y
	return s / 2.0
