using Godot;

[GlobalClass]
public partial class AdjacencyBonus : Resource
{
	public enum Type
	{
		None,
		GrowthTime,
		DecayTime,
		SaleValue,
		AdditionalBiome
	}

	[Export] public Type BonusType { get; set; } = Type.None;
	[Export] public Biome BonusBiome { get; set; } = Biome.None;
	[Export] public float Multiplier { get; set; } = 1f;
}
