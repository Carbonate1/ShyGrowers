shader_type spatial;
render_mode unshaded, fog_disabled;

uniform sampler2D view : source_color, filter_linear_mipmap;
uniform sampler2D next_view : source_color, filter_linear_mipmap;
uniform float blend : hint_range(0.0, 1.0) = 0.0;
uniform float inset : hint_range(0.0, 0.2) = 0.06;
uniform float brightness = 1.0;

void fragment() {
	vec2 uv = mix(vec2(inset), vec2(1.0 - inset), UV);
	vec3 color = mix(texture(view, uv).rgb, texture(next_view, uv).rgb, blend);
	ALBEDO = color * brightness;
}
