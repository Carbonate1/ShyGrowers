global uniform float toon_bands;
global uniform float toon_band_softness;
global uniform float toon_strength;
global uniform float toon_wrap;
global uniform float toon_specular;
global uniform float toon_rim;
global uniform vec4 toon_rim_color;

float toon_ramp(float x) {
	float bands = max(toon_bands, 1.0);
	float scaled = x * bands;
	float step_floor = floor(scaled);
	float blend = smoothstep(0.5 - toon_band_softness, 0.5 + toon_band_softness, scaled - step_floor);
	return clamp((step_floor + blend) / bands, 0.0, 1.0);
}

void light() {
	float wrapped = clamp((dot(NORMAL, LIGHT) + toon_wrap) / (1.0 + toon_wrap), 0.0, 1.0);
	float lit = wrapped * ATTENUATION;
	float shade = mix(lit, toon_ramp(lit), toon_strength);
	DIFFUSE_LIGHT += LIGHT_COLOR * shade / PI;

	vec3 half_vector = normalize(VIEW + LIGHT);
	float glossiness = exp2(10.0 * (1.0 - ROUGHNESS) + 1.0);
	float highlight = pow(max(dot(NORMAL, half_vector), 0.0), glossiness) * lit;
	highlight = smoothstep(0.5 - toon_band_softness, 0.5 + toon_band_softness, highlight);
	SPECULAR_LIGHT += LIGHT_COLOR * highlight * toon_specular * SPECULAR_AMOUNT * (1.0 - ROUGHNESS * 0.7) / PI;

	float rim = 1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	rim = smoothstep(0.62, 0.7, rim) * lit;
	SPECULAR_LIGHT += LIGHT_COLOR * toon_rim_color.rgb * rim * toon_rim / PI;
}
