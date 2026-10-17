shader_type spatial;
render_mode cull_disabled;

#include "res://assets/shaders/cozy_toon_light.gdshaderinc"

uniform sampler2D sprite_texture : source_color, filter_nearest;
uniform float alpha_cut : hint_range(0.0, 1.0) = 0.5;
uniform float roughness : hint_range(0.0, 1.0) = 0.85;

void fragment() {
	vec4 texel = texture(sprite_texture, UV) * COLOR;
	ALBEDO = texel.rgb;
	ALPHA = texel.a;
	ALPHA_SCISSOR_THRESHOLD = alpha_cut;
	ROUGHNESS = roughness;
}
