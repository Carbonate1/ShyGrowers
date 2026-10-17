using Godot;
using System.Collections.Generic;

public partial class ToonShading : Node
{
	[Export] public Shader Surface { get; set; }
	[Export] public Shader DoubleSidedSurface { get; set; }
	[Export] public Shader Sprite { get; set; }

	private readonly Dictionary<Material, ShaderMaterial> surfaceMaterials = new();
	private readonly Dictionary<Texture2D, ShaderMaterial> spriteMaterials = new();

	public override void _Ready()
	{
		GetTree().NodeAdded += OnNodeAdded;
		ApplyRecursive(GetParent());
	}

	public override void _ExitTree()
	{
		GetTree().NodeAdded -= OnNodeAdded;
	}

	private void OnNodeAdded(Node node)
	{
		if (GetParent().IsAncestorOf(node))
		{
			Apply(node);
		}
	}

	private void ApplyRecursive(Node node)
	{
		Apply(node);
		foreach (Node child in node.GetChildren())
		{
			ApplyRecursive(child);
		}
	}

	private void Apply(Node node)
	{
		if (node is MeshInstance3D mesh)
		{
			ApplyToMesh(mesh);
		}
		else if (node is Sprite3D sprite)
		{
			ApplyToSprite(sprite);
		}
	}

	private void ApplyToMesh(MeshInstance3D mesh)
	{
		if (mesh.MaterialOverride is not null || mesh.Mesh is null)
		{
			return;
		}

		for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
		{
			if (mesh.GetActiveMaterial(i) is BaseMaterial3D source &&
				source.Transparency == BaseMaterial3D.TransparencyEnum.Disabled &&
				source.ShadingMode != BaseMaterial3D.ShadingModeEnum.Unshaded)
			{
				mesh.SetSurfaceOverrideMaterial(i, ToonVersionOf(source));
			}
		}
	}

	private void ApplyToSprite(Sprite3D sprite)
	{
		if (sprite.MaterialOverride is not null || sprite.Texture is null)
		{
			return;
		}

		if (!spriteMaterials.TryGetValue(sprite.Texture, out ShaderMaterial material))
		{
			material = new ShaderMaterial { Shader = Sprite };
			material.SetShaderParameter("sprite_texture", sprite.Texture);
			spriteMaterials[sprite.Texture] = material;
		}

		sprite.MaterialOverride = material;
	}

	private ShaderMaterial ToonVersionOf(BaseMaterial3D source)
	{
		if (surfaceMaterials.TryGetValue(source, out ShaderMaterial toon))
		{
			return toon;
		}

		toon = new ShaderMaterial
		{
			Shader = source.CullMode == BaseMaterial3D.CullModeEnum.Disabled ? DoubleSidedSurface : Surface,
			ResourceName = source.ResourceName
		};

		toon.SetShaderParameter("albedo_color", source.AlbedoColor);
		toon.SetShaderParameter("use_vertex_color", source.VertexColorUseAsAlbedo);
		toon.SetShaderParameter("uv_scale", source.Uv1Scale);
		toon.SetShaderParameter("uv_offset", source.Uv1Offset);
		toon.SetShaderParameter("roughness", source.Roughness);
		toon.SetShaderParameter("metallic", source.Metallic);
		toon.SetShaderParameter("specular", source.MetallicSpecular);
		SetTexture(toon, "albedo_texture", source.AlbedoTexture);

		if (source.NormalEnabled && source.NormalTexture is not null)
		{
			toon.SetShaderParameter("use_normal_texture", true);
			toon.SetShaderParameter("normal_scale", source.NormalScale);
			SetTexture(toon, "normal_texture", source.NormalTexture);
		}

		if (source is OrmMaterial3D orm)
		{
			SetTexture(toon, "roughness_texture", orm.OrmTexture);
			SetTexture(toon, "metallic_texture", orm.OrmTexture);
			toon.SetShaderParameter("roughness_channel", ChannelMask(BaseMaterial3D.TextureChannel.Green));
			toon.SetShaderParameter("metallic_channel", ChannelMask(BaseMaterial3D.TextureChannel.Blue));
		}
		else
		{
			SetTexture(toon, "roughness_texture", source.RoughnessTexture);
			SetTexture(toon, "metallic_texture", source.MetallicTexture);
			toon.SetShaderParameter("roughness_channel", ChannelMask(source.RoughnessTextureChannel));
			toon.SetShaderParameter("metallic_channel", ChannelMask(source.MetallicTextureChannel));
		}

		if (source.EmissionEnabled)
		{
			toon.SetShaderParameter("emission", source.Emission);
			toon.SetShaderParameter("emission_energy", source.EmissionEnergyMultiplier);
			SetTexture(toon, "emission_texture", source.EmissionTexture);
		}

		surfaceMaterials[source] = toon;
		return toon;
	}

	private static void SetTexture(ShaderMaterial material, string parameter, Texture2D texture)
	{
		if (texture is not null)
		{
			material.SetShaderParameter(parameter, texture);
		}
	}

	private static Vector4 ChannelMask(BaseMaterial3D.TextureChannel channel)
	{
		return channel switch
		{
			BaseMaterial3D.TextureChannel.Red => new Vector4(1f, 0f, 0f, 0f),
			BaseMaterial3D.TextureChannel.Green => new Vector4(0f, 1f, 0f, 0f),
			BaseMaterial3D.TextureChannel.Blue => new Vector4(0f, 0f, 1f, 0f),
			BaseMaterial3D.TextureChannel.Alpha => new Vector4(0f, 0f, 0f, 1f),
			_ => new Vector4(0.333f, 0.333f, 0.333f, 0f)
		};
	}
}
