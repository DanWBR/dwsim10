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

## The models of the point kinds, "kind" or "kind:variant": path, and the size to scale them to
## (its length, or its height when the third value is true); 0 keeps the model's own size.
const MODELS := {
	"office:e": [KENNEY + "commercial/building-e.glb", 18.0, false],
	"office:k": [KENNEY + "commercial/building-k.glb", 18.0, false],
	"office:c": [KENNEY + "commercial/building-c.glb", 14.0, false],
	"factory:b": [KENNEY + "industrial/building-b.glb", 24.0, false],
	"factory:e": [KENNEY + "industrial/building-e.glb", 24.0, false],
	"factory:m": [KENNEY + "industrial/building-m.glb", 24.0, false],
	"plantoffice:a": [KENNEY + "industrial/building-a.glb", 14.0, false],
	"plantoffice:o": [KENNEY + "industrial/building-o.glb", 14.0, false],
	"storehouse:i": [KENNEY + "industrial/building-i.glb", 16.0, false],
	"storehouse:j": [KENNEY + "industrial/building-j.glb", 16.0, false],
	"watertower": [KENNEY + "industrial/water-tower.glb", 18.0, true],
	"solar": [KENNEY + "industrial/solar-panel-landscape-group.glb", 10.0, false],
	"windturbine": [KENNEY + "industrial/windmill.glb", 30.0, true],
	"container:a": [KENNEY + "industrial/shipping-container-a.glb", 6.1, false],
	"container:b": [KENNEY + "industrial/shipping-container-b.glb", 6.1, false],
	"container:c": [KENNEY + "industrial/shipping-container-c.glb", 6.1, false],
	"dumpster": [KENNEY + "roads/dumpster.glb", 2.0, false],
	"parasol:a": [KENNEY + "commercial/detail-parasol-a.glb", 2.5, false],
	"parasol:b": [KENNEY + "commercial/detail-parasol-b.glb", 2.5, false],
	"tree:oak": [KENNEY + "nature/tree_oak.glb", 7.0, true],
	"tree:default": [KENNEY + "nature/tree_default.glb", 7.0, true],
	"tree:detailed": [KENNEY + "nature/tree_detailed.glb", 7.0, true],
	"tree:fat": [KENNEY + "nature/tree_fat.glb", 7.0, true],
	"tree:tall": [KENNEY + "nature/tree_tall.glb", 7.0, true],
	"tree:simple": [KENNEY + "nature/tree_simple.glb", 7.0, true],
	"tree:plateau": [KENNEY + "nature/tree_plateau.glb", 7.0, true],
	"tree:thin": [KENNEY + "nature/tree_thin.glb", 7.0, true],
	"tree:cone": [KENNEY + "nature/tree_cone.glb", 7.0, true],
	"tree:oak_dark": [KENNEY + "nature/tree_oak_dark.glb", 7.0, true],
	"tree:default_dark": [KENNEY + "nature/tree_default_dark.glb", 7.0, true],
	"tree:detailed_dark": [KENNEY + "nature/tree_detailed_dark.glb", 7.0, true],
	"tree:fat_dark": [KENNEY + "nature/tree_fat_darkh.glb", 7.0, true],
	"tree:tall_dark": [KENNEY + "nature/tree_tall_dark.glb", 7.0, true],
	"tree:simple_dark": [KENNEY + "nature/tree_simple_dark.glb", 7.0, true],
	"tree:plateau_dark": [KENNEY + "nature/tree_plateau_dark.glb", 7.0, true],
	"tree:thin_dark": [KENNEY + "nature/tree_thin_dark.glb", 7.0, true],
	"tree:cone_dark": [KENNEY + "nature/tree_cone_dark.glb", 7.0, true],
	"autumntree:default": [KENNEY + "nature/tree_default_fall.glb", 7.0, true],
	"autumntree:oak": [KENNEY + "nature/tree_oak_fall.glb", 7.0, true],
	"autumntree:detailed": [KENNEY + "nature/tree_detailed_fall.glb", 7.0, true],
	"autumntree:fat": [KENNEY + "nature/tree_fat_fall.glb", 7.0, true],
	"autumntree:tall": [KENNEY + "nature/tree_tall_fall.glb", 7.0, true],
	"autumntree:simple": [KENNEY + "nature/tree_simple_fall.glb", 7.0, true],
	"autumntree:plateau": [KENNEY + "nature/tree_plateau_fall.glb", 7.0, true],
	"autumntree:thin": [KENNEY + "nature/tree_thin_fall.glb", 7.0, true],
	"autumntree:cone": [KENNEY + "nature/tree_cone_fall.glb", 7.0, true],
	"smalltree:small": [KENNEY + "nature/tree_small.glb", 3.5, true],
	"smalltree:small_dark": [KENNEY + "nature/tree_small_dark.glb", 3.5, true],
	"smalltree:small_fall": [KENNEY + "nature/tree_small_fall.glb", 3.5, true],
	"smalltree:pine_a": [KENNEY + "nature/tree_pineSmallA.glb", 3.5, true],
	"smalltree:pine_b": [KENNEY + "nature/tree_pineSmallB.glb", 3.5, true],
	"smalltree:pine_c": [KENNEY + "nature/tree_pineSmallC.glb", 3.5, true],
	"smalltree:pine_d": [KENNEY + "nature/tree_pineSmallD.glb", 3.5, true],
	"smalltree:ground_a": [KENNEY + "nature/tree_pineGroundA.glb", 2.5, true],
	"smalltree:ground_b": [KENNEY + "nature/tree_pineGroundB.glb", 2.5, true],
	"pine:a": [KENNEY + "nature/tree_pineDefaultA.glb", 9.0, true],
	"pine:b": [KENNEY + "nature/tree_pineDefaultB.glb", 9.0, true],
	"pine:round": [KENNEY + "nature/tree_pineRoundA.glb", 8.0, true],
	"pine:round_b": [KENNEY + "nature/tree_pineRoundB.glb", 8.0, true],
	"pine:round_c": [KENNEY + "nature/tree_pineRoundC.glb", 8.0, true],
	"pine:round_d": [KENNEY + "nature/tree_pineRoundD.glb", 8.0, true],
	"pine:round_e": [KENNEY + "nature/tree_pineRoundE.glb", 8.0, true],
	"pine:round_f": [KENNEY + "nature/tree_pineRoundF.glb", 8.0, true],
	"pine:tall": [KENNEY + "nature/tree_pineTallA.glb", 11.0, true],
	"pine:tall_b": [KENNEY + "nature/tree_pineTallB.glb", 11.0, true],
	"pine:tall_c": [KENNEY + "nature/tree_pineTallC.glb", 11.0, true],
	"pine:tall_d": [KENNEY + "nature/tree_pineTallD.glb", 11.0, true],
	"palm:a": [KENNEY + "nature/tree_palm.glb", 8.0, true],
	"palm:tall": [KENNEY + "nature/tree_palmTall.glb", 10.0, true],
	"palm:detailed": [KENNEY + "nature/tree_palmDetailedTall.glb", 9.0, true],
	"palm:bend": [KENNEY + "nature/tree_palmBend.glb", 8.0, true],
	"palm:short": [KENNEY + "nature/tree_palmShort.glb", 5.0, true],
	"palm:detailed_short": [KENNEY + "nature/tree_palmDetailedShort.glb", 5.0, true],
	"rock:a": [KENNEY + "nature/rock_largeA.glb", 1.5, false],
	"rock:b": [KENNEY + "nature/rock_largeB.glb", 1.5, false],
	"rock:c": [KENNEY + "nature/rock_largeC.glb", 1.5, false],
	"rock:d": [KENNEY + "nature/rock_largeD.glb", 1.5, false],
	"rock:e": [KENNEY + "nature/rock_largeE.glb", 1.5, false],
	"rock:f": [KENNEY + "nature/rock_largeF.glb", 1.5, false],
	"stone:a": [KENNEY + "nature/rock_smallA.glb", 0.6, false],
	"stone:b": [KENNEY + "nature/rock_smallB.glb", 0.6, false],
	"stone:c": [KENNEY + "nature/rock_smallC.glb", 0.6, false],
	"stone:d": [KENNEY + "nature/rock_smallD.glb", 0.6, false],
	"stone:e": [KENNEY + "nature/rock_smallE.glb", 0.6, false],
	"stone:f": [KENNEY + "nature/rock_smallF.glb", 0.6, false],
	"stone:g": [KENNEY + "nature/rock_smallG.glb", 0.6, false],
	"stone:h": [KENNEY + "nature/rock_smallH.glb", 0.6, false],
	"stone:i": [KENNEY + "nature/rock_smallI.glb", 0.6, false],
	"stone:flat_a": [KENNEY + "nature/rock_smallFlatA.glb", 0.7, false],
	"stone:flat_b": [KENNEY + "nature/rock_smallFlatB.glb", 0.7, false],
	"stone:flat_c": [KENNEY + "nature/rock_smallFlatC.glb", 0.7, false],
	"stone:top_a": [KENNEY + "nature/rock_smallTopA.glb", 0.6, false],
	"stone:top_b": [KENNEY + "nature/rock_smallTopB.glb", 0.6, false],
	"tallrock:a": [KENNEY + "nature/rock_tallA.glb", 3.5, true],
	"tallrock:b": [KENNEY + "nature/rock_tallB.glb", 3.5, true],
	"tallrock:c": [KENNEY + "nature/rock_tallC.glb", 3.5, true],
	"tallrock:d": [KENNEY + "nature/rock_tallD.glb", 3.5, true],
	"tallrock:e": [KENNEY + "nature/rock_tallE.glb", 3.5, true],
	"tallrock:f": [KENNEY + "nature/rock_tallF.glb", 3.5, true],
	"tallrock:g": [KENNEY + "nature/rock_tallG.glb", 3.5, true],
	"tallrock:h": [KENNEY + "nature/rock_tallH.glb", 3.5, true],
	"tallrock:i": [KENNEY + "nature/rock_tallI.glb", 3.5, true],
	"tallrock:j": [KENNEY + "nature/rock_tallJ.glb", 3.5, true],
	"bush:a": [KENNEY + "nature/plant_bush.glb", 1.5, false],
	"bush:large": [KENNEY + "nature/plant_bushLarge.glb", 2.2, false],
	"bush:detailed": [KENNEY + "nature/plant_bushDetailed.glb", 1.6, false],
	"flowers:red": [KENNEY + "nature/flower_redA.glb", 1.0, false],
	"flowers:yellow": [KENNEY + "nature/flower_yellowA.glb", 1.0, false],
	"flowers:purple": [KENNEY + "nature/flower_purpleA.glb", 1.0, false],
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

## The looks of the kinds that have several: the build bar deals one at each placement, and an
## object saved without one gets one from its id.
const VARIANTS := {
	"office": ["e", "k", "c"], "factory": ["b", "e", "m"], "plantoffice": ["a", "o"], "storehouse": ["i", "j"],
	"container": ["a", "b", "c"], "parasol": ["a", "b"], "bush": ["a", "large", "detailed"],
	"flowers": ["red", "yellow", "purple"], "truck": ["", "box"],
	"tree": ["oak", "default", "detailed", "fat", "tall", "simple", "plateau", "thin", "cone", "oak_dark", "default_dark", "detailed_dark", "fat_dark", "tall_dark", "simple_dark", "plateau_dark", "thin_dark", "cone_dark"], "autumntree": ["default", "oak", "detailed", "fat", "tall", "simple", "plateau", "thin", "cone"], "smalltree": ["small", "small_dark", "small_fall", "pine_a", "pine_b", "pine_c", "pine_d", "ground_a", "ground_b"], "pine": ["a", "b", "round", "round_b", "round_c", "round_d", "round_e", "round_f", "tall", "tall_b", "tall_c", "tall_d"], "palm": ["a", "tall", "detailed", "bend", "short", "detailed_short"], "rock": ["a", "b", "c", "d", "e", "f"], "stone": ["a", "b", "c", "d", "e", "f", "g", "h", "i", "flat_a", "flat_b", "flat_c", "top_a", "top_b"], "tallrock": ["a", "b", "c", "d", "e", "f", "g", "h", "i", "j"],
}

## Plants and rocks: each one a little bigger or smaller than the next.
const VARIED := ["tree", "autumntree", "smalltree", "pine", "palm", "bush", "flowers", "rock", "stone", "tallrock"]

## The signs over the doors of the buildings made here.
const SIGNS := {"admin": "ADMINISTRATION", "workshop": "MAINTENANCE WORKSHOP", "warehouse": "WAREHOUSE",
	"substation": "SUBSTATION", "canteen": "CANTEEN", "guardhouse": "GATE"}

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
## The relief of the ground (terrain.gd); the objects sit on it. Null while there is none.
static var terrain = null
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
	"office": ["point", 18, 12, 10], "factory": ["point", 24, 18, 10], "plantoffice": ["point", 14, 10, 6],
	"storehouse": ["point", 16, 12, 7], "pine": ["point", 4, 4, 9], "palm": ["point", 4, 4, 8], "bush": ["point", 1.5, 1.5, 1.2],
	"flowers": ["point", 1, 1, 0.4], "container": ["point", 6.1, 2.44, 2.6], "dumpster": ["point", 2, 1.2, 1.3],
	"parasol": ["point", 2.5, 2.5, 2.5], "watertower": ["point", 6, 6, 18], "solar": ["point", 10, 6, 2],
	"windturbine": ["point", 8, 8, 30], "powerline": ["line", 2, 0, 12],
	"autumntree": ["point", 4, 4, 7], "smalltree": ["point", 2, 2, 3.5], "stone": ["point", 0.6, 0.6, 0.3],
	"tallrock": ["point", 2, 2, 3.5],
}


## The look of an object: its own variant, or one picked from its id for an object saved without one.
static func variant_of(it: Dictionary, kind: String) -> String:
	var v := str(it.get("variant", ""))
	if v == "" and VARIANTS.has(kind):
		var list: Array = VARIANTS[kind]
		v = list[absi(_seed(it)) % list.size()]
	return v


static func entry(kind: String) -> Dictionary:
	if _catalog.has(kind):
		return _catalog[kind]
	if FALLBACK.has(kind):
		var f: Array = FALLBACK[kind]
		return {"kind": kind, "shape": f[0], "w": f[1], "d": f[2], "h": f[3]}
	return {}


## The estimated cost of one object, USD, as DWSIM counts it (SiteCatalog.CostOf): each, per metre
## of a line or per square metre of an area; a point object times its scale squared.
static func cost_of(it: Dictionary) -> float:
	var kind := str(it.get("site", it.get("kind", "")))
	var e := entry(kind)
	var cost := float(e.get("cost", 0.0))
	var pts: Array = it.get("points", [])
	match str(e.get("shape", "point")):
		"line":
			var l := 0.0
			for i in range(1, pts.size()):
				l += Vector2(float(pts[i][0]) - float(pts[i - 1][0]), float(pts[i][1]) - float(pts[i - 1][1])).length()
			return cost * l / 1000.0
		"area":
			var a := 0.0
			for i in pts.size():
				var p = pts[i]
				var q = pts[(i + 1) % pts.size()]
				a += float(p[0]) * float(q[1]) - float(q[0]) * float(p[1])
			return cost * absf(a) / 2.0 / 1.0e6
	var s := float(it.get("scale", 1.0))
	return cost * s * s


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


## The height of the ground at a place of the world above the plant's ground, m.
static func ground_at(x: float, z: float) -> float:
	if terrain == null or not terrain.has_grid():
		return 0.0
	return terrain.height_at(x, z)


## Whether the ground rises or sinks under the points (world), with a margin.
static func _bumpy(node: Node3D, pts: PackedVector3Array, margin: float) -> bool:
	if terrain == null or not terrain.has_grid() or pts.is_empty():
		return false
	var r := Rect2(Vector2(pts[0].x, pts[0].z), Vector2.ZERO)
	for p in pts:
		r = r.expand(Vector2(p.x, p.z))
	r.position += Vector2(node.position.x, node.position.z)
	return terrain.bumpy(r.grow(margin))


## The points (relative to the node, which stands on the plant's ground) raised onto the relief.
static func _lift(node: Node3D, pts: PackedVector3Array) -> PackedVector3Array:
	var out := PackedVector3Array()
	for p in pts:
		out.append(Vector3(p.x, ground_at(node.position.x + p.x, node.position.z + p.z), p.z))
	return out


## The points of a line split every 2 m and raised onto the relief, so the line follows the slopes.
static func _drape(node: Node3D, pts: PackedVector3Array) -> PackedVector3Array:
	var out := PackedVector3Array()
	for i in pts.size():
		if i > 0:
			var a := pts[i - 1]
			var b := pts[i]
			var n := maxi(1, int(ceil(a.distance_to(b) / 2.0)))
			for k in range(1, n):
				out.append(a.lerp(b, float(k) / n))
		out.append(pts[i])
	return _lift(node, out)


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
	node.position.y += ground_at(node.position.x, node.position.z)
	var body := Node3D.new()
	if kind in VARIED:
		scale *= 0.85 + 0.3 * float(absi(_seed(it)) % 1000) / 1000.0
	body.scale = Vector3.ONE * scale
	node.add_child(body)
	var look := kind + ":" + variant_of(it, kind)
	if kind == "controlroom":
		ControlRoomModel.build(body)
		node.set_meta("door", ControlRoomModel.DOOR * scale)
	elif MODELS.has(look):
		_model(body, MODELS[look])
	elif MODELS.has(kind):
		_model(body, MODELS[kind])
		if kind == "windturbine":
			# the rotor turns about its hub while the plant runs
			for b in body.find_children("blades", "", true, false):
				b.add_to_group("spin")
				b.set_meta("spin", 1.6)
				b.set_meta("spin_axis", Vector3.RIGHT)
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
	if "/nature/" in str(spec[0]):
		_tone_node(n, "/nature/rock_" in str(spec[0]))
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
	_details(parent, kind, w, d, h)


## A name over the door, on the front of a building.
static func _sign(parent: Node3D, text: String, at: Vector3, size := 48) -> void:
	var label := Label3D.new()
	label.text = text
	label.font_size = size
	label.pixel_size = 0.01
	label.modulate = Color(0.12, 0.17, 0.26)
	label.outline_size = 3
	label.outline_modulate = Color(1, 1, 1, 0.8)
	label.position = at
	parent.add_child(label)


## What makes each building made here look like what it is.
static func _details(parent: Node3D, kind: String, w: float, d: float, h: float) -> void:
	var front := d / 2.0
	if SIGNS.has(kind):
		_sign(parent, SIGNS[kind], Vector3(0, h - 0.6 if kind != "guardhouse" else h - 0.5, front + 0.05), 48 if w > 6.0 else 28)
	match kind:
		"admin":
			# an entrance canopy and units on the roof
			_box(parent, Vector3(4.0, 0.2, 2.5), Vector3(0, 3.2, front + 1.25), _mat("#5c636d"))
			for sx in [-1.0, 1.0]:
				_rod(parent, 0.08, 3.2, Vector3(sx * 1.8, 1.6, front + 2.3), _mat("steel"))
			for i in 3:
				_box(parent, Vector3(1.6, 1.0, 1.2), Vector3(-w / 4.0 + i * w / 4.0, h + 0.75, -d / 4.0), _mat("#a7adb3"))
		"workshop":
			_box(parent, Vector3(4.5, 4.2, 0.1), Vector3(-w / 2.0 + 3.5, 2.1, front + 0.03), _mat("#3f6c96"))
			for i in 3:
				_box(parent, Vector3(1.2, 0.15, d * 0.7), Vector3(-w / 2.0 + 3.0 + i * (w - 6.0) / 2.0, h + 0.3, 0), _mat("glass"))
		"warehouse":
			_box(parent, Vector3(w * 0.8, 1.2, 2.5), Vector3(0, 0.6, front + 1.25), _mat("concrete"))
			for sx in [-1.0, 1.0]:
				_box(parent, Vector3(4.0, 4.0, 0.1), Vector3(sx * w / 4.0, 3.2, front + 0.03), _mat("#9aa0a6"))
		"substation":
			# a fenced yard of transformers in front, with a high voltage sign
			var yard := 6.0
			var a := Vector3(-w / 2.0, 0, front + 0.3)
			var b := Vector3(w / 2.0, 0, front + 0.3)
			var c := Vector3(w / 2.0, 0, front + yard)
			var e := Vector3(-w / 2.0, 0, front + yard)
			_fence(parent, b, c, 2.2)
			_fence(parent, c, e, 2.2)
			_fence(parent, e, a, 2.2)
			for sx in [-1.0, 1.0]:
				var at := Vector3(sx * w / 4.0, 0, front + yard / 2.0)
				_box(parent, Vector3(2.2, 2.4, 1.8), at + Vector3(0, 1.2, 0), _mat("#6f7d6c"))
				for k in 5:
					_box(parent, Vector3(0.08, 1.8, 1.9), at + Vector3(-1.2 + k * 0.6, 1.1, 0), _mat("#5e6a5c"))
				for k in 3:
					_rod(parent, 0.08, 0.7, at + Vector3(-0.6 + k * 0.6, 2.75, 0), _mat("#c9c2a8"))
			_box(parent, Vector3(0.6, 0.6, 0.02), Vector3(0, 1.6, front + yard + 0.05), _mat("yellow"))
		"canteen":
			_box(parent, Vector3(w * 0.7, 0.12, 2.0), Vector3(0, 2.8, front + 1.0), _mat("green"))
			for i in 2:
				_model_at(parent, MODELS["parasol:a"], Vector3(-3.0 + i * 6.0, 0, front + 4.5))
		"guardhouse":
			for side in [Vector3(w / 2.0 + 0.01, 0, 0), Vector3(-w / 2.0 - 0.01, 0, 0)]:
				var g := _box(parent, Vector3(0.06, 1.0, d * 0.7), side + Vector3(0, 1.7, 0), _mat("glass"))
				g.add_to_group("night_glow")


static func _model_at(parent: Node3D, spec: Array, at: Vector3) -> void:
	var holder := Node3D.new()
	holder.position = at
	parent.add_child(holder)
	_model(holder, spec)


## A power line: a pole at each point of the line and every 35 m between, and three wires from
## pole to pole at the height of the cross arm.
static func _power_line(parent: Node3D, pts: PackedVector3Array) -> void:
	var poles := PackedVector3Array()
	var dirs := []
	for i in range(1, pts.size()):
		var a := pts[i - 1]
		var b := pts[i]
		var n := maxi(1, int(ceil(a.distance_to(b) / 35.0)))
		for k in n:
			poles.append(a.lerp(b, float(k) / n))
			dirs.append((b - a).normalized())
	poles.append(pts[-1])
	dirs.append((pts[-1] - pts[-2]).normalized() if pts.size() > 1 else Vector3.RIGHT)
	var spec := [KENNEY + "roads/electricity-pole.glb", 12.0, true]
	var arm := 11.2
	for i in poles.size():
		var holder := Node3D.new()
		holder.position = poles[i]
		# the cross arm across the line
		holder.basis = Basis.looking_at(dirs[i], Vector3.UP)
		parent.add_child(holder)
		_model(holder, spec)
	var wire := _mat("#2b2d30")
	for i in range(1, poles.size()):
		var across := Vector3.UP.cross(dirs[i - 1]).normalized()
		var across2 := Vector3.UP.cross(dirs[i]).normalized()
		for off in [-1.1, 0.0, 1.1]:
			var a: Vector3 = poles[i - 1] + across * off + Vector3(0, arm, 0)
			var b: Vector3 = poles[i] + across2 * off + Vector3(0, arm, 0)
			var r := _rod(parent, 0.02, a.distance_to(b), (a + b) / 2.0, wire)
			r.basis = SceneBuilder.basis_along((b - a).normalized())


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


static var _trees := []

## Natural tones for the bright colours of the Nature Kit, by material name.
const TONES := {"leafsGreen": Color(0.3, 0.5, 0.17), "leafsDark": Color(0.17, 0.35, 0.16), "grass": Color(0.34, 0.52, 0.19),
	"woodBark": Color(0.42, 0.3, 0.2), "woodBarkDark": Color(0.33, 0.24, 0.17), "dirt": Color(0.47, 0.38, 0.28),
	"leafsFall": Color(0.82, 0.42, 0.14), "woodBirch": Color(0.85, 0.83, 0.77), "stone": Color(0.58, 0.58, 0.56)}
## On stones and rocks the earth is stone and the grass is moss.
const ROCK_TONES := {"dirt": Color(0.5, 0.49, 0.47), "grass": Color(0.33, 0.43, 0.2), "stone": Color(0.6, 0.6, 0.58)}
static var _toned := {}


## The material in a natural tone, for a Nature Kit material that has one; the same material otherwise.
static func _tone(m: Material, rocky := false) -> Material:
	var tones: Dictionary = ROCK_TONES if rocky and m != null and ROCK_TONES.has(m.resource_name) else TONES
	if m == null or not tones.has(m.resource_name):
		return m
	var key := ("rock:" if tones == ROCK_TONES else "") + m.resource_name
	if not _toned.has(key):
		var t := (m as BaseMaterial3D).duplicate() as BaseMaterial3D
		t.albedo_color = tones[m.resource_name]
		t.roughness = 0.9 if tones != ROCK_TONES else 0.95
		_toned[key] = t
	return _toned[key]


static func _tone_node(n: Node, rocky := false) -> void:
	for mi in n.find_children("*", "MeshInstance3D", true, false):
		var m := mi as MeshInstance3D
		for i in m.mesh.get_surface_count():
			var t := _tone(m.mesh.surface_get_material(i), rocky)
			if t != m.mesh.surface_get_material(i):
				m.set_surface_override_material(i, t)


## The meshes of the Nature Kit trees for woods and rows: [mesh, its transform in the model, height].
static func nature_trees() -> Array:
	if not _trees.is_empty():
		return _trees
	for v in ["oak", "default", "detailed", "fat", "tall", "simple", "plateau", "thin", "oak_dark", "default_dark",
			"detailed_dark", "pineDefaultA", "pineRoundA", "pineRoundC", "pineTallB"]:
		var ps := _scene(KENNEY + "nature/tree_" + v + ".glb")
		if ps == null:
			continue
		var n: Node3D = ps.instantiate()
		var found := n.find_children("*", "MeshInstance3D", true, false)
		if not found.is_empty():
			var mi := found[0] as MeshInstance3D
			var t := Transform3D.IDENTITY
			var p: Node = mi
			while p != null and p != n:
				if p is Node3D:
					t = (p as Node3D).transform * t
				p = p.get_parent()
			var mesh := mi.mesh.duplicate() as Mesh
			for i in mesh.get_surface_count():
				mesh.surface_set_material(i, _tone(mesh.surface_get_material(i)))
			_trees.append([mesh, t, maxf((t * mi.get_aabb()).size.y, 0.01)])
		n.free()
	return _trees


## Trees at the given places, seeded so they look the same each time: Nature Kit trees in one
## multimesh per model, or, without them, trunks and crowns made here.
static func _forest(parent: Node3D, spots: PackedVector3Array, rng: RandomNumberGenerator) -> void:
	if spots.is_empty():
		return
	var kinds := nature_trees()
	if not kinds.is_empty():
		var groups := []
		for k in kinds.size():
			groups.append([])
		for p in spots:
			var k := rng.randi() % kinds.size()
			var h := rng.randf_range(5.0, 9.0)
			var s: float = h / float(kinds[k][2])
			var t := Transform3D(Basis(Vector3.UP, rng.randf_range(0.0, TAU)).scaled(Vector3.ONE * s), p) * (kinds[k][1] as Transform3D)
			groups[k].append(t)
		for k in kinds.size():
			if groups[k].is_empty():
				continue
			var mm := MultiMesh.new()
			mm.transform_format = MultiMesh.TRANSFORM_3D
			mm.mesh = kinds[k][0]
			mm.instance_count = groups[k].size()
			for i in groups[k].size():
				mm.set_instance_transform(i, groups[k][i])
			var mmi := MultiMeshInstance3D.new()
			mmi.multimesh = mm
			parent.add_child(mmi)
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
	# on the relief: the corners raised onto it, and the runs split so they follow it
	var corners := pts
	if _bumpy(node, pts, w + 2.0):
		corners = _lift(node, pts)
		pts = _drape(node, pts)
	var segs := _segments(pts)
	match kind:
		"road", "path":
			var mat := _mat("asphalt" if kind == "road" else "path")
			var lift := 0.03 if kind == "road" else 0.028
			for s in segs:
				_strip(node, s[0], s[1], w, 0.02, lift, mat).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			# round joints so the bends are not notched
			for i in range(1, corners.size() - 1):
				var c := CylinderMesh.new()
				c.top_radius = w / 2.0
				c.bottom_radius = w / 2.0
				c.height = 0.02
				c.radial_segments = 20
				var mi := MeshInstance3D.new()
				mi.mesh = c
				mi.material_override = mat
				mi.position = corners[i] + Vector3(0, lift + 0.01, 0)
				mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
				node.add_child(mi)
			if kind == "road":
				for s in _segments(corners):
					var a: Vector3 = s[0]
					var b: Vector3 = s[1]
					var n := int(a.distance_to(b) / 6.0)
					for k in range(1, n):
						var p := a.lerp(b, float(k) / n)
						var q := a.lerp(b, minf(1.0, (float(k) + 0.4) / n))
						p.y = ground_at(node.position.x + p.x, node.position.z + p.z) if corners != pts else p.y
						q.y = ground_at(node.position.x + q.x, node.position.z + q.z) if corners != pts else q.y
						_strip(node, p, q, 0.15, 0.005, lift + 0.02, _mat("white"))
		"fence":
			for s in segs:
				_fence(node, s[0], s[1], 2.2)
		"powerline":
			_power_line(node, corners)
		"wall":
			for s in segs:
				_strip(node, s[0], s[1], w, 2.5, 0.0, _mat("wall"))
		"hedge":
			for s in segs:
				_strip(node, s[0], s[1], w, 1.2, 0.0, _mat("hedge"))
		"treerow":
			var spots := PackedVector3Array()
			var runs := _segments(corners)
			for s in runs:
				var a: Vector3 = s[0]
				var b: Vector3 = s[1]
				var n := maxi(1, int(a.distance_to(b) / 6.0))
				for k in n + (1 if s == runs[-1] else 0):
					spots.append(a.lerp(b, float(k) / n))
			spots = _lift(node, spots) if corners != pts else spots
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
	var mat := _mat(key if key in ["concrete", "asphalt", "gravel", "grass", "woods", "bund"] else "#9aa0a6")
	var on_relief := _bumpy(node, pts, 1.0)
	var flat := PackedVector2Array()
	for p in pts:
		flat.append(Vector2(p.x, p.z))
	if on_relief:
		_draped_polygon(node, flat, lift, mat)
	else:
		_polygon(node, pts, lift, mat)
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
			if on_relief:
				spots = _lift(node, spots)
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
						if on_relief:
							var lifted := _lift(node, PackedVector3Array([p0, p1]))
							p0 = lifted[0]
							p1 = lifted[1]
						_strip(node, p0, p1, 0.12, 0.005, lift + 0.01, _mat("white"))
					s += 2.5
				row += 11.0
		"bund":
			var ring := pts.duplicate()
			ring.append(pts[0])
			var walls := _drape(node, ring) if on_relief else ring
			for s in _segments(walls):
				_strip(node, s[0], s[1], 0.25, 1.0, 0.0, _mat("bund"))


## An area laid over the relief: the cells of the ground grid whose middle lies inside the outline,
## each a quad at the heights of its corners.
static func _draped_polygon(node: Node3D, flat: PackedVector2Array, lift: float, mat: Material) -> void:
	var cell: float = terrain.CELL
	var box := Rect2(flat[0], Vector2.ZERO)
	for p in flat:
		box = box.expand(p)
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var ox := node.position.x
	var oz := node.position.z
	var x := floorf((box.position.x + ox) / cell) * cell - ox
	var any := false
	while x < box.end.x:
		var z := floorf((box.position.y + oz) / cell) * cell - oz
		while z < box.end.y:
			if Geometry2D.is_point_in_polygon(Vector2(x + cell / 2.0, z + cell / 2.0), flat):
				any = true
				var c := [Vector3(x, 0, z), Vector3(x + cell, 0, z), Vector3(x + cell, 0, z + cell), Vector3(x, 0, z + cell)]
				for k in 4:
					c[k].y = ground_at(ox + c[k].x, oz + c[k].z) + lift
				for t in [[0, 1, 2], [0, 2, 3]]:
					for k in t:
						st.set_normal(Vector3.UP)
						st.add_vertex(c[k])
			z += cell
		x += cell
	if not any:
		return
	var mi := MeshInstance3D.new()
	mi.mesh = st.commit()
	mi.material_override = mat
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	node.add_child(mi)


static func _signed_area(flat: PackedVector2Array) -> float:
	var s := 0.0
	for i in flat.size():
		var a := flat[i]
		var b := flat[(i + 1) % flat.size()]
		s += a.x * b.y - b.x * a.y
	return s / 2.0
