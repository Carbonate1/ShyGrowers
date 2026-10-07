#include "res://assets/shaders/cozy_toon_light.gdshaderinc"

uniform vec4 albedo_color : source_color = vec4(1.0);
uniform sampler2D albedo_texture : source_color, hint_default_white, filter_linear_mipmap_anisotropic, repeat_enable;
uniform bool use_vertex_color = false;

uniform bool use_normal_texture = false;
uniform sampler2D normal_texture : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float normal_scale = 1.0;

uniform float roughness : hint_range(0.0, 1.0) = 1.0;
uniform sampler2D roughness_texture : hint_default_white, filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec4 roughness_channel = vec4(1.0, 0.0, 0.0, 0.0);

uniform float metallic : hint_range(0.0, 1.0) = 0.0;
uniform sampler2D metallic_texture : hint_default_white, filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec4 metallic_channel = vec4(1.0, 0.0, 0.0, 0.0);
uniform float specular : hint_range(0.0, 1.0) = 0.5;

uniform vec4 emission : source_color = vec4(0.0, 0.0, 0.0, 1.0);
uniform float emission_energy = 1.0;
uniform sampler2D emission_texture : source_color, hint_default_white, filter_linear_mipmap_anisotropic, repeat_enable;

uniform vec3 uv_scale = vec3(1.0);
uniform vec3 uv_offset = vec3(0.0);

void vertex() {
	UV = UV * uv_scale.xy + uv_offset.xy;
}

void fragment() {
	vec4 albedo = albedo_color * texture(albedo_texture, UV);
	if (use_vertex_color) {
		albedo *= COLOR;
	}
	ALBEDO = albedo.rgb;

	if (use_normal_texture) {
		NORMAL_MAP = texture(normal_texture, UV).rgb;
		NORMAL_MAP_DEPTH = normal_scale;
	}

	ROUGHNESS = roughness * dot(texture(roughness_texture, UV), roughness_channel);
	METALLIC = metallic * dot(texture(metallic_texture, UV), metallic_channel);
	SPECULAR = specular;
	EMISSION = emission.rgb * emission_energy * texture(emission_texture, UV).rgb;
}
