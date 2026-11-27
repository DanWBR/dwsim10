## The relief of the ground round the plant: a grid of heights, one every metre, that the user
## shapes with the terrain brushes of the build bar (raise, lower, level, smooth, pond, drain), and
## the water of the ponds. DWSIM keeps it with the flowsheet (Plant3DTerrain): x0, y0 and cell in mm
## in DWSIM's frame, nx by ny heights as 16-bit centimetres, and a byte per cell with the depth of
## the water in decimetres (0 where it is dry), both in base 64. The platform of the plant (the
## equipment, the pad and the gravel round it) stays flat. The ground turns from grass to bare
## earth as it gets steeper. The objects of the site sit on the relief (site_objects.gd asks
## height_at).
class_name SiteTerrain
extends Node3D

const CELL := 1.0
const CHUNK := 32
const MARGIN := 110.0
const MIN_H := -30.0
const MAX_H := 60.0

var main: Node3D
var x0 := 0.0  # Godot x of cell column 0, m
var y0 := 0.0  # DWSIM y of cell row 0, m (Godot z = -y)
var nx := 0
var ny := 0
var h := PackedFloat32Array()  # m, above the ground of the plant
var w := PackedFloat32Array()  # depth of water, m
var locked := Rect2()  # the platform of the plant, Godot x and z
var _chunks := {}
var _ground_mat: StandardMaterial3D
var _water_mat: StandardMaterial3D


func _init(owner_main: Node3D) -> void:
	main = owner_main


func _ready() -> void:
	_ground_mat = StandardMaterial3D.new()
	_ground_mat.albedo_texture = load("res://assets/textures/grass_albedo.jpg")
	_ground_mat.uv1_triplanar = true
	_ground_mat.uv1_world_triplanar = true
	_ground_mat.uv1_scale = Vector3.ONE / 3.0
	_ground_mat.vertex_color_use_as_albedo = true
	_ground_mat.roughness = 0.95
	_water_mat = StandardMaterial3D.new()
	_water_mat.albedo_color = Color(0.16, 0.36, 0.46, 0.82)
	_water_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_water_mat.metallic = 0.3
	_water_mat.roughness = 0.06
	_water_mat.cull_mode = BaseMaterial3D.CULL_DISABLED


func has_grid() -> bool:
	return nx > 1 and ny > 1


func ground_y() -> float:
	return main._bounds().position.y


# --- reading and writing -----------------------------------------------------------------------

## The relief DWSIM sent with the scene; null clears it. The mesh is built again unless rebuild is
## false (while the scene loads, before the plant's bounds are known).
func load_from(t, rebuild := true) -> void:
	if not (t is Dictionary):
		nx = 0
		ny = 0
		h = PackedFloat32Array()
		w = PackedFloat32Array()
		if rebuild:
			rebuild_all()
		return
	var cell := float(t.get("cell", 1000.0)) / 1000.0
	nx = int(t.get("nx", 0))
	ny = int(t.get("ny", 0))
	x0 = float(t.get("x0", 0.0)) / 1000.0
	y0 = float(t.get("y0", 0.0)) / 1000.0
	var hb := Marshalls.base64_to_raw(str(t.get("h", "")))
	h.resize(nx * ny)
	w.resize(nx * ny)
	h.fill(0.0)
	w.fill(0.0)
	if hb.size() == 2 * nx * ny:
		for i in nx * ny:
			h[i] = hb.decode_s16(i * 2) / 100.0
	if t.has("w") and t["w"] != null:
		var wb := Marshalls.base64_to_raw(str(t["w"]))
		if wb.size() == nx * ny:
			for i in nx * ny:
				w[i] = wb[i] / 10.0
	if absf(cell - CELL) > 0.001:
		push_warning("terrain cell %.2f m read as %.2f m" % [cell, CELL])
	if rebuild:
		rebuild_all()


## The relief as the terrain message carries it.
func to_dict() -> Dictionary:
	var hb := PackedByteArray()
	hb.resize(2 * nx * ny)
	var wb := PackedByteArray()
	wb.resize(nx * ny)
	for i in nx * ny:
		hb.encode_s16(i * 2, int(clampf(roundf(h[i] * 100.0), -32768, 32767)))
		wb[i] = int(clampf(roundf(w[i] * 10.0), 0, 255))
	return {"x0": roundf(x0 * 1000.0), "y0": roundf(y0 * 1000.0), "cell": CELL * 1000.0, "nx": nx, "ny": ny,
		"h": Marshalls.raw_to_base64(hb), "w": Marshalls.raw_to_base64(wb)}


## A flat grid round the plant, made the first time a brush touches the ground.
func ensure_grid() -> void:
	if has_grid():
		return
	var box: AABB = main._bounds()
	x0 = floorf(box.position.x - MARGIN)
	var gx1 := ceilf(box.end.x + MARGIN)
	# DWSIM y = -Godot z
	y0 = floorf(-box.end.z - MARGIN)
	var gy1 := ceilf(-box.position.z + MARGIN)
	nx = int((gx1 - x0) / CELL) + 1
	ny = int((gy1 - y0) / CELL) + 1
	h.resize(nx * ny)
	w.resize(nx * ny)
	h.fill(0.0)
	w.fill(0.0)


func update_lock() -> void:
	var box: AABB = main._bounds().grow(15.0)
	locked = Rect2(Vector2(box.position.x, box.position.z), Vector2(box.size.x, box.size.z))


# --- heights -----------------------------------------------------------------------------------

func _cell_xz(i: int, j: int) -> Vector2:
	return Vector2(x0 + i * CELL, -(y0 + j * CELL))


## The height of the ground above the plant's ground at a place (Godot x, z), bilinear; 0 off the grid.
func height_at(gx: float, gz: float) -> float:
	if not has_grid():
		return 0.0
	var fi := (gx - x0) / CELL
	var fj := (-gz - y0) / CELL
	if fi < 0.0 or fj < 0.0 or fi > nx - 1 or fj > ny - 1:
		return 0.0
	var i := mini(int(fi), nx - 2)
	var j := mini(int(fj), ny - 2)
	var u := fi - i
	var v := fj - j
	var a := h[j * nx + i]
	var b := h[j * nx + i + 1]
	var c := h[(j + 1) * nx + i]
	var d := h[(j + 1) * nx + i + 1]
	return lerpf(lerpf(a, b, u), lerpf(c, d, u), v)


## Whether any of the relief rises or sinks inside a rectangle (Godot x, z).
func bumpy(r: Rect2) -> bool:
	if not has_grid():
		return false
	var i0 := clampi(int((r.position.x - x0) / CELL), 0, nx - 1)
	var i1 := clampi(int(ceil((r.end.x - x0) / CELL)), 0, nx - 1)
	var j0 := clampi(int((-r.end.y - y0) / CELL), 0, ny - 1)
	var j1 := clampi(int(ceil((-r.position.y - y0) / CELL)), 0, ny - 1)
	for j in range(j0, j1 + 1):
		for i in range(i0, i1 + 1):
			if absf(h[j * nx + i]) > 0.01:
				return true
	return false


## Where a ray of the camera meets the ground, relief included.
func ray(from: Vector3, dir: Vector3):
	if absf(dir.y) < 0.05:
		return null
	var y := ground_y()
	var p = Plane(Vector3.UP, y).intersects_ray(from, dir)
	if p == null:
		return null
	for k in 6:
		var q = Plane(Vector3.UP, y + height_at(p.x, p.z)).intersects_ray(from, dir)
		if q == null:
			break
		p = q
	return p


# --- brushes ----------------------------------------------------------------------------------

## One step of a brush at a place (Godot x, z), radius r m, over dt s. Kinds: raise, lower, level
## (towards the height "target"), smooth, pond (a bowl of water whose surface is "target"), drain.
## Returns the chunks it changed.
func brush(kind: String, at: Vector3, r: float, dt: float, target := 0.0) -> Array:
	ensure_grid()
	update_lock()
	var touched := {}
	var i0 := clampi(int((at.x - r - x0) / CELL), 0, nx - 1)
	var i1 := clampi(int(ceil((at.x + r - x0) / CELL)), 0, nx - 1)
	var j0 := clampi(int((-at.z - r - y0) / CELL), 0, ny - 1)
	var j1 := clampi(int(ceil((-at.z + r - y0) / CELL)), 0, ny - 1)
	var old := h.duplicate()
	for j in range(j0, j1 + 1):
		for i in range(i0, i1 + 1):
			var p := _cell_xz(i, j)
			var dist := p.distance_to(Vector2(at.x, at.z))
			if dist > r:
				continue
			if locked.grow(1.0).has_point(p):
				continue
			var f := smoothstep(0.0, 1.0, 1.0 - dist / r)
			var k := j * nx + i
			match kind:
				"raise":
					h[k] = minf(h[k] + 2.0 * dt * f, MAX_H)
				"lower":
					h[k] = maxf(h[k] - 2.0 * dt * f, MIN_H)
				"level":
					h[k] += (target - h[k]) * minf(1.0, 5.0 * dt * f)
				"smooth":
					var s := 0.0
					var n := 0
					for o in [[-1, 0], [1, 0], [0, -1], [0, 1]]:
						var ii: int = i + o[0]
						var jj: int = j + o[1]
						if ii >= 0 and jj >= 0 and ii < nx and jj < ny:
							s += old[jj * nx + ii]
							n += 1
					h[k] += (s / maxf(n, 1) - h[k]) * minf(1.0, 6.0 * dt * f)
				"pond":
					# dig a bowl under the surface and fill it to the surface
					var bed := target - 1.6 * f
					if h[k] > bed:
						h[k] = maxf(bed, h[k] - 3.0 * dt)
					w[k] = maxf(0.0, target - h[k]) if f > 0.15 else w[k]
				"drain":
					w[k] = 0.0
			touched[Vector2i(i / CHUNK, j / CHUNK)] = true
	return touched.keys()


# --- the mesh --------------------------------------------------------------------------------------

func rebuild_all() -> void:
	for c in _chunks.values():
		for n in c:
			if is_instance_valid(n):
				n.queue_free()
	_chunks.clear()
	if not has_grid():
		return
	update_lock()
	for cj in int(ceil(float(ny - 1) / CHUNK)):
		for ci in int(ceil(float(nx - 1) / CHUNK)):
			rebuild_chunk(Vector2i(ci, cj))


func rebuild_chunks(list: Array) -> void:
	for c in list:
		rebuild_chunk(c)


func _vertex(i: int, j: int, y: float) -> Vector3:
	var p := _cell_xz(i, j)
	return Vector3(p.x, y + h[j * nx + i] + 0.004, p.y)


func rebuild_chunk(c: Vector2i) -> void:
	if _chunks.has(c):
		for n in _chunks[c]:
			if is_instance_valid(n):
				n.queue_free()
	_chunks.erase(c)
	var i0 := c.x * CHUNK
	var j0 := c.y * CHUNK
	var i1 := mini(i0 + CHUNK, nx - 1)
	var j1 := mini(j0 + CHUNK, ny - 1)
	if i1 <= i0 or j1 <= j0:
		return
	var y := ground_y()
	# a chunk that is all flat and dry is left to the ground plane under it
	var flat := true
	for j in range(j0, j1 + 1):
		for i in range(i0, i1 + 1):
			if absf(h[j * nx + i]) > 0.005 or w[j * nx + i] > 0.0:
				flat = false
	if flat:
		return
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var grass := Color(1.0, 1.0, 1.0)
	var earth := Color(0.78, 0.62, 0.45)
	for j in range(j0, j1):
		for i in range(i0, i1):
			var q := [Vector2i(i, j), Vector2i(i + 1, j), Vector2i(i + 1, j + 1), Vector2i(i, j + 1)]
			var v := []
			for g in q:
				var vert := _vertex(g.x, g.y, y)
				# the slope at the corner, from the heights round it
				var hx := h[g.y * nx + mini(g.x + 1, nx - 1)] - h[g.y * nx + maxi(g.x - 1, 0)]
				var hz := h[mini(g.y + 1, ny - 1) * nx + g.x] - h[maxi(g.y - 1, 0) * nx + g.x]
				var n := Vector3(-hx / (2.0 * CELL), 1.0, hz / (2.0 * CELL)).normalized()
				var steep := clampf((1.0 - n.y) * 6.0, 0.0, 1.0)
				var wet := w[g.y * nx + g.x] > 0.0
				v.append([vert, n, grass.lerp(earth, steep).darkened(0.25 if wet else 0.0)])
			# two triangles, wound so they face up (Godot's front faces turn clockwise)
			for t in [[0, 2, 1], [0, 3, 2]]:
				for k in t:
					st.set_normal(v[k][1])
					st.set_color(v[k][2])
					st.add_vertex(v[k][0])
	var mi := MeshInstance3D.new()
	mi.mesh = st.commit()
	mi.material_override = _ground_mat
	add_child(mi)
	var nodes := [mi]
	# the water: a level sheet over the wet cells
	var ws := SurfaceTool.new()
	ws.begin(Mesh.PRIMITIVE_TRIANGLES)
	var any := false
	for j in range(j0, j1):
		for i in range(i0, i1):
			var k := j * nx + i
			if w[k] <= 0.0:
				continue
			any = true
			var top := y + h[k] + w[k]
			var a := _cell_xz(i, j)
			var b := _cell_xz(i + 1, j + 1)
			var corners := [Vector3(a.x, top, a.y), Vector3(b.x, top, a.y), Vector3(b.x, top, b.y), Vector3(a.x, top, b.y)]
			for t in [[0, 2, 1], [0, 3, 2]]:
				for kk in t:
					ws.set_normal(Vector3.UP)
					ws.add_vertex(corners[kk])
	if any:
		var wm := MeshInstance3D.new()
		wm.mesh = ws.commit()
		wm.material_override = _water_mat
		wm.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		add_child(wm)
		nodes.append(wm)
	_chunks[c] = nodes
