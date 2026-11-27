// Frosted glass for the panels of the window: the plant behind a panel shows through, blurred and
// lightened by the panel's own colour, so the text stays readable over any background. It goes on
// the PanelContainer only; the controls inside it draw normally.
shader_type canvas_item;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
// mip level of the screen copy: higher blurs more
uniform float blur = 3.0;
// how much of the panel's colour covers the blurred plant (0 = clear glass, 1 = opaque)
uniform float tint = 0.42;

// the glass never gets darker than this, so the dark text stays readable over a dark plant at night
uniform float min_light = 0.7;

void fragment() {
	vec3 behind = textureLod(screen_tex, SCREEN_UV, blur).rgb;
	vec3 c = mix(behind, COLOR.rgb, tint);
	float light = dot(c, vec3(0.299, 0.587, 0.114));
	c = mix(c, COLOR.rgb, clamp((min_light - light) / min_light, 0.0, 1.0));
	COLOR = vec4(c, COLOR.a);
}
