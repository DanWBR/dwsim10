// Bright dashes that run along a pipe at a pace that follows the velocity of its stream. The
// mesh is a cylinder from the start of a straight piece of the line to its end, along its local Y
// (from -seg_len/2 to +seg_len/2); "start" is the length of line before the piece, so the dashes
// carry on across the elbows.
shader_type spatial;
render_mode unshaded, cull_back, depth_draw_never, blend_mix;

uniform vec4 color : source_color = vec4(0.6, 0.85, 1.0, 1.0);
uniform float spacing = 1.8;
uniform float dash = 0.5;
instance uniform float seg_len = 1.0;
instance uniform float speed = 1.0;
instance uniform float start = 0.0;

varying float along;

void vertex() {
	along = VERTEX.y + seg_len * 0.5;
}

void fragment() {
	float s = start + along;
	float phase = fract((s - TIME * speed) / spacing);
	float edge = dash / spacing;
	float a = smoothstep(0.0, 0.06, phase) * (1.0 - smoothstep(edge - 0.06, edge, phase));
	ALBEDO = color.rgb;
	ALPHA = a * 0.45;
}
