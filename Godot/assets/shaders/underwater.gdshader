shader_type canvas_item;

uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;
uniform sampler2D foam_noise : hint_default_black, filter_linear_mipmap, repeat_enable;
uniform vec4 tint : source_color = vec4(0.32, 0.62, 0.78, 1.0);
uniform float tint_strength : hint_range(0.0, 1.0) = 0.3;
uniform float wave_strength = 0.0035;
uniform float wave_scale = 16.0;
uniform float wave_speed = 1.1;
uniform float caustic_scale = 0.8;
uniform float caustic_strength = 0.05;
uniform float vignette_strength : hint_range(0.0, 1.0) = 0.35;

void fragment() {
	float t = TIME * wave_speed;
	vec2 uv = SCREEN_UV;
	vec2 sway = vec2(sin(uv.y * wave_scale + t), cos(uv.x * wave_scale * 0.8 + t * 1.3)) * wave_strength;
	vec3 scene = textureLod(screen_texture, uv + sway, 0.0).rgb;

	vec2 p = uv * vec2(1.78, 1.0) * caustic_scale;
	float drift = texture(foam_noise, p * 0.5 + vec2(t * 0.01, -t * 0.008)).r;
	float shimmer = texture(foam_noise, p + (drift - 0.5) * 0.15 + vec2(-t * 0.015, t * 0.01)).r;
	float caustic = smoothstep(0.88, 0.97, 1.0 - abs(shimmer * 2.0 - 1.0)) * caustic_strength * (1.0 - uv.y);

	vec3 color = mix(scene, scene * tint.rgb * 1.7, tint_strength) + vec3(caustic);
	float vignette = smoothstep(0.85, 0.3, length(uv - 0.5));
	color *= mix(1.0 - vignette_strength, 1.0, vignette);

	COLOR = vec4(color, COLOR.a);
}
