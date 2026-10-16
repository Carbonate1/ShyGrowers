shader_type spatial;
render_mode cull_disabled;

#include "res://assets/shaders/cozy_toon_light.gdshaderinc"

uniform sampler2D foam_noise : hint_default_black, filter_linear_mipmap, repeat_enable;
uniform vec4 deep_color : source_color = vec4(0.01, 0.09, 0.18, 1.0);
uniform vec4 shallow_color : source_color = vec4(0.04, 0.56, 0.76, 1.0);
uniform vec4 foam_color : source_color = vec4(0.9, 0.97, 1.0, 1.0);
uniform float pattern_scale = 0.4;
uniform float flow_speed = 1.0;
uniform float warp_strength = 0.1;
uniform float chop_amount : hint_range(0.0, 1.0) = 0.2;
uniform float glow_start : hint_range(0.0, 1.0) = 0.6;
uniform float foam_threshold : hint_range(0.0, 1.0) = 0.92;
uniform float foam_softness : hint_range(0.0, 0.3) = 0.05;
uniform float lace_threshold : hint_range(0.0, 1.0) = 0.96;
uniform float lace_strength : hint_range(0.0, 1.0) = 0.6;
uniform float clarity : hint_range(0.0, 1.0) = 0.92;

varying vec3 world_position;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float t = TIME * flow_speed;
	vec2 uv = world_position.xz * pattern_scale;
	float flow = texture(foam_noise, uv * 0.5 + vec2(t * 0.01, 0.0)).r;
	vec2 warped = uv + (flow - 0.5) * warp_strength;
	float swell = texture(foam_noise, warped + vec2(0.02, 0.013) * t).r;
	float chop = texture(foam_noise, warped * 1.7 + vec2(-0.017, 0.011) * t).r;

	float ridge = 1.0 - abs((swell + (chop - 0.5) * chop_amount) * 2.0 - 1.0);
	float body = smoothstep(glow_start, foam_threshold, ridge);
	float foam = smoothstep(foam_threshold, foam_threshold + foam_softness, ridge) * mix(0.3, 1.0, smoothstep(0.35, 0.6, flow));
	float lace = smoothstep(lace_threshold, lace_threshold + 0.05, 1.0 - abs(chop * 2.0 - 1.0));
	foam = clamp(foam + lace * body * lace_strength, 0.0, 1.0);

	vec3 water = mix(deep_color.rgb, shallow_color.rgb, clamp(body * 0.8 + (swell - 0.5) * 0.35 - 0.05, 0.0, 1.0));

	ALBEDO = mix(water, foam_color.rgb, foam);
	NORMAL_MAP = normalize(vec3((swell - chop) * 0.6, (flow - swell) * 0.6, 1.0)) * 0.5 + 0.5;
	ROUGHNESS = mix(0.06, 0.55, foam);
	SPECULAR = 0.6;
	ALPHA = mix(clarity, 1.0, foam);
}
