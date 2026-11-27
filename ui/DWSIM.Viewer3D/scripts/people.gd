## The people of the plant (Kenney Mini Characters): loads them at a person's height, gives each
## one a skin tone, and puts a tool in the right hand. The tones go from very light to very dark
## and are dealt from a shuffled bag, so every group of seven people has one of each.
class_name PlantPeople
extends RefCounted

const PEOPLE := "res://assets/models/kenney/people/"
const HEIGHT := 1.75
const TONES := [
	Color(0.98, 0.86, 0.76), Color(0.95, 0.77, 0.63), Color(0.86, 0.65, 0.49),
	Color(0.73, 0.52, 0.36), Color(0.59, 0.4, 0.26), Color(0.44, 0.29, 0.19), Color(0.31, 0.2, 0.13)]
## The skin swatches of the Kenney colormap, in pixels: four swatches of 64 px side by side.
const SKIN := Rect2i(0, 256, 256, 128)

static var _scenes := {}
static var _materials := {}
static var _bag: Array = []


## A person of the model, scaled to HEIGHT, with the next skin tone of the bag. The scale is in
## the "k" meta (metres of the model per metre of the plant), for what is hung on the person.
static func make(model: String, rng: RandomNumberGenerator) -> Node3D:
	var path := PEOPLE + model + ".glb"
	if not _scenes.has(path):
		_scenes[path] = load(path) if ResourceLoader.exists(path) else null
	var ps: PackedScene = _scenes[path]
	if ps == null:
		return null
	var n: Node3D = ps.instantiate()
	# the visible body is 0.67 tall in the model (its mesh bounds are larger)
	var h := 0.67
	var k := HEIGHT / h
	n.scale = Vector3.ONE * k
	n.set_meta("k", k)
	if _bag.is_empty():
		for i in TONES.size():
			_bag.append(i)
		for i in range(_bag.size() - 1, 0, -1):
			var j := rng.randi_range(0, i)
			var t = _bag[i]
			_bag[i] = _bag[j]
			_bag[j] = t
	var tone: int = _bag.pop_back()
	for m in n.find_children("*", "MeshInstance3D", true, false):
		var mi := m as MeshInstance3D
		for s in mi.mesh.get_surface_count():
			var mat := mi.get_active_material(s) as StandardMaterial3D
			if mat != null and mat.albedo_texture != null:
				mi.set_surface_override_material(s, _toned(mat, tone))
	return n


## The material with the skin swatches of its colour map painted in a tone; the light and shade
## of each swatch stay as they were.
static func _toned(base: StandardMaterial3D, tone: int) -> StandardMaterial3D:
	var key := "%d:%d" % [base.get_instance_id(), tone]
	if _materials.has(key):
		return _materials[key]
	var src := base.albedo_texture.get_image()
	if src == null:
		return base
	var img := src.duplicate() as Image
	if img.is_compressed():
		img.decompress()
	img.clear_mipmaps()
	img.convert(Image.FORMAT_RGBA8)
	var f := img.get_width() / 512.0
	var area := Rect2i(Vector2i(Vector2(SKIN.position) * f), Vector2i(Vector2(SKIN.size) * f))
	var swatch := int(64 * f)
	var to: Color = TONES[tone]
	for sx in range(area.position.x, area.end.x, swatch):
		var ref := img.get_pixel(sx + 1, area.position.y + 1).get_luminance()
		for x in range(sx, mini(sx + swatch, area.end.x)):
			for y in range(area.position.y, area.end.y):
				var c := img.get_pixel(x, y)
				var shade := clampf(c.get_luminance() / maxf(ref, 0.01), 0.0, 1.3)
				img.set_pixel(x, y, Color(to.r * shade, to.g * shade, to.b * shade, c.a))
	img.generate_mipmaps()
	var mat := base.duplicate() as StandardMaterial3D
	mat.albedo_texture = ImageTexture.create_from_image(img)
	_materials[key] = mat
	return mat


## Hangs a tool (built in metres, its grip at its origin) in the right hand of the person.
static func hold(person: Node3D, tool: Node3D) -> void:
	var sk: Skeleton3D = null
	for s in person.find_children("*", "Skeleton3D", true, false):
		sk = s
	if sk == null:
		return
	var at := BoneAttachment3D.new()
	at.bone_name = "arm-right"
	sk.add_child(at)
	# the hand is at the end of the arm, which points along -x in the model's rest pose
	tool.position = Vector3(-0.25, -0.02, 0.0)
	tool.scale = Vector3.ONE / float(person.get_meta("k", 1.0))
	at.add_child(tool)


## The personal protective equipment of a role, hung on the bones of the person, in the model's
## own units (the head bone is at the neck; the head is 0.34 wide, 0.33 tall and 0.33 deep, its
## face at +0.16; the eyes are 0.14 up). Everyone on the plant wears a hard hat, safety glasses,
## gloves and a high-visibility vest: the worker model has its own yellow hard hat, and engineers
## get a white one. The welder wears a welding mask over the hard hat, a leather apron and leather
## gloves; whoever grinds wears a face shield over the glasses; whoever grinds or hammers wears ear
## muffs. Roles: engineer, operator, welding, grinding, hammering, bolting, firewatch.
static func dress(person: Node3D, role: String) -> void:
	var sk: Skeleton3D = null
	for s in person.find_children("*", "Skeleton3D", true, false):
		sk = s
	if sk == null:
		return
	var head := _bone(sk, "head")
	var torso := _bone(sk, "torso")
	if role == "engineer":
		var hat := _flat(Color(0.95, 0.95, 0.93), 0.2)
		var dome := SphereMesh.new()
		dome.is_hemisphere = true
		dome.radius = 0.215
		dome.height = 0.16
		var shell := MeshInstance3D.new()
		shell.mesh = dome
		shell.material_override = hat
		shell.position = Vector3(0, 0.3, -0.005)
		head.add_child(shell)
		var brim := CylinderMesh.new()
		brim.top_radius = 0.245
		brim.bottom_radius = 0.245
		brim.height = 0.02
		var rim := MeshInstance3D.new()
		rim.mesh = brim
		rim.material_override = hat
		rim.position = Vector3(0, 0.3, 0.03)
		head.add_child(rim)
	if role == "welding":
		var mask := _flat(Color(0.14, 0.15, 0.17), 0.2)
		_box(head, Vector3(0.36, 0.3, 0.04), Vector3(0, 0.15, 0.19), mask)
		_box(head, Vector3(0.2, 0.06, 0.012), Vector3(0, 0.18, 0.212), _flat(Color(0.1, 0.45, 0.2), 0.6))
	else:
		var lens := StandardMaterial3D.new()
		lens.albedo_color = Color(0.25, 0.3, 0.34, 0.7)
		lens.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		lens.metallic_specular = 0.9
		lens.roughness = 0.1
		_box(head, Vector3(0.35, 0.055, 0.02), Vector3(0, 0.14, 0.175), lens)
	if role == "grinding":
		var shield := StandardMaterial3D.new()
		shield.albedo_color = Color(0.75, 0.85, 0.9, 0.3)
		shield.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		shield.metallic_specular = 0.9
		shield.roughness = 0.05
		_box(head, Vector3(0.38, 0.27, 0.012), Vector3(0, 0.13, 0.215), shield)
	if role == "grinding" or role == "hammering":
		var muff := _flat(Color(0.8, 0.1, 0.08), 0.3)
		for sx in [-1.0, 1.0]:
			_box(head, Vector3(0.04, 0.09, 0.09), Vector3(sx * 0.185, 0.12, -0.01), muff)
	# the vest: a shell round the chest with two reflective bands
	var vest := _flat(Color(1.0, 0.45, 0.05) if role == "operator" else Color(0.85, 0.95, 0.1), 0.0)
	var band := _flat(Color(0.85, 0.87, 0.9), 0.8)
	_box(torso, Vector3(0.255, 0.15, 0.235), Vector3(0, 0.085, 0.0), vest)
	for y in [0.04, 0.1]:
		_box(torso, Vector3(0.26, 0.016, 0.24), Vector3(0, y, 0.0), band)
	if role == "welding":
		_box(torso, Vector3(0.2, 0.2, 0.015), Vector3(0, 0.03, 0.125), _flat(Color(0.45, 0.3, 0.18), 0.0))
	var glove := _flat(Color(0.55, 0.38, 0.22) if role == "welding" else Color(0.9, 0.75, 0.2), 0.0)
	for side in [["arm-right", -1.0], ["arm-left", 1.0]]:
		var arm := _bone(sk, side[0])
		_box(arm, Vector3(0.07, 0.125, 0.125), Vector3(float(side[1]) * 0.25, 0.0, 0.0), glove)


static func _bone(sk: Skeleton3D, name: String) -> BoneAttachment3D:
	var at := BoneAttachment3D.new()
	at.bone_name = name
	sk.add_child(at)
	# the attachment carries the bone's pose; the gear is placed from the bone's rest origin
	return at


static func _flat(c: Color, metal: float) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = c
	m.metallic = metal
	m.roughness = 0.45
	return m


static func _box(parent: Node3D, size: Vector3, at: Vector3, mat: Material) -> void:
	var b := BoxMesh.new()
	b.size = size
	var mi := MeshInstance3D.new()
	mi.mesh = b
	mi.material_override = mat
	mi.position = at
	parent.add_child(mi)


## Plays an animation of the person in a loop (idle if the model doesn't have it).
static func play(person: Node3D, anim: String, speed := 1.0) -> void:
	for ap in person.find_children("*", "AnimationPlayer", true, false):
		var player := ap as AnimationPlayer
		var name := anim if player.has_animation(anim) else "idle"
		if player.has_animation(name):
			player.get_animation(name).loop_mode = Animation.LOOP_LINEAR
			player.play(name)
			player.speed_scale = speed


## Where the person's animation is, from 0 to 1 (for sounds in step with a swing).
static func phase(person: Node3D) -> float:
	for ap in person.find_children("*", "AnimationPlayer", true, false):
		var player := ap as AnimationPlayer
		if player.current_animation != "":
			return player.current_animation_position / maxf(player.current_animation_length, 0.001)
	return 0.0
