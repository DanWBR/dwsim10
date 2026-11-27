## A control room: a single-storey block with a light roof, a row of windows that light up at
## night, a door and its name over it, centred on its parent's origin with its front to +Z. The
## operators and engineers of the rounds (crew.gd) come out of its door and go back in.
class_name ControlRoomModel
extends RefCounted

const WIDTH := 12.0
const DEPTH := 7.0
## The door, in the building's own frame (on the ground, at the front).
const DOOR := Vector3(WIDTH / 2.0 - 1.2, 0.0, DEPTH / 2.0)


static func _box(parent: Node3D, size: Vector3, at: Vector3, mat: Material) -> MeshInstance3D:
	var m := BoxMesh.new()
	m.size = size
	var mi := MeshInstance3D.new()
	mi.mesh = m
	mi.material_override = mat
	mi.position = at
	parent.add_child(mi)
	return mi


static func build(parent: Node3D) -> void:
	var walls := StandardMaterial3D.new()
	walls.albedo_texture = load("res://assets/textures/concrete_albedo.jpg")
	walls.albedo_color = Color(0.93, 0.9, 0.82)
	walls.uv1_triplanar = true
	walls.uv1_scale = Vector3.ONE / 2.0
	_box(parent, Vector3(WIDTH, 3.6, DEPTH), Vector3(0, 1.8, 0), walls)
	var roof := StandardMaterial3D.new()
	roof.albedo_color = Color(0.66, 0.68, 0.7)
	roof.metallic = 0.5
	_box(parent, Vector3(WIDTH + 0.6, 0.25, DEPTH + 0.6), Vector3(0, 3.72, 0), roof)
	var plinth := StandardMaterial3D.new()
	plinth.albedo_color = Color(0.55, 0.55, 0.53)
	_box(parent, Vector3(WIDTH + 1.6, 0.15, DEPTH + 1.6), Vector3(0, 0.075, 0), plinth)
	var glass := StandardMaterial3D.new()
	glass.albedo_color = Color(0.2, 0.3, 0.38)
	glass.metallic = 0.6
	glass.roughness = 0.1
	glass.emission_enabled = true
	glass.emission = Color(1.0, 0.85, 0.55)
	glass.emission_energy_multiplier = 0.0
	glass.set_meta("night_glow", 1.6)
	for i in 4:
		_box(parent, Vector3(1.8, 1.1, 0.06), Vector3(-WIDTH / 2.0 + 1.8 + i * 2.8, 2.0, DEPTH / 2.0 + 0.01), glass).add_to_group("night_glow")
	var door := StandardMaterial3D.new()
	door.albedo_color = Color(0.3, 0.33, 0.37)
	_box(parent, Vector3(1.1, 2.2, 0.06), DOOR + Vector3(0, 1.1, 0.01), door)
	var label := Label3D.new()
	label.text = "CONTROL ROOM"
	label.font_size = 64
	label.pixel_size = 0.01
	label.modulate = Color(0.15, 0.2, 0.3)
	label.position = Vector3(0, 3.2, DEPTH / 2.0 + 0.05)
	parent.add_child(label)
