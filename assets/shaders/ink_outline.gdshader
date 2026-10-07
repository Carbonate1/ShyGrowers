shader_type spatial;
render_mode unshaded, fog_disabled, depth_draw_never, depth_test_disabled, cull_disabled;

uniform sampler2D depth_texture : hint_depth_texture, filter_nearest;
uniform sampler2D normal_texture : hint_normal_roughness_texture, filter_nearest;

uniform vec4 ink_color : source_color = vec4(0.2, 0.12, 0.09, 1.0);
uniform float ink_strength : hint_range(0.0, 1.0) = 0.55;
uniform float thickness : hint_range(0.5, 4.0) = 1.2;
uniform float depth_threshold = 0.04;
uniform float normal_threshold : hint_range(0.0, 2.0) = 0.55;
uniform float fade_distance = 8.0;

void vertex() {
	POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

float view_depth(vec2 uv, mat4 inv_projection) {
	float raw = texture(depth_texture, uv).r;
	vec4 view = inv_projection * vec4(uv * 2.0 - 1.0, raw, 1.0);
	return -view.z / view.w;
}

void fragment() {
	vec2 texel = thickness / VIEWPORT_SIZE;
	float center_depth = view_depth(SCREEN_UV, INV_PROJECTION_MATRIX);
	vec3 center_normal = texture(normal_texture, SCREEN_UV).xyz * 2.0 - 1.0;

	vec2 offsets[4] = vec2[](vec2(1.0, 0.0), vec2(-1.0, 0.0), vec2(0.0, 1.0), vec2(0.0, -1.0));
	float depth_edge = 0.0;
	float normal_edge = 0.0;
	for (int i = 0; i < 4; i++) {
		vec2 uv = SCREEN_UV + offsets[i] * texel;
		depth_edge = max(depth_edge, (view_depth(uv, INV_PROJECTION_MATRIX) - center_depth) / center_depth);
		vec3 neighbour_normal = texture(normal_texture, uv).xyz * 2.0 - 1.0;
		normal_edge = max(normal_edge, 1.0 - dot(center_normal, neighbour_normal));
	}

	float edge = max(
		smoothstep(depth_threshold, depth_threshold * 2.0, depth_edge),
		smoothstep(normal_threshold, normal_threshold + 0.25, normal_edge));
	edge *= 1.0 - smoothstep(fade_distance * 0.5, fade_distance, center_depth);

	ALBEDO = ink_color.rgb;
	ALPHA = edge * ink_strength;
}
