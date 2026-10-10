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
		AdditionalSeason
	}

	[Export] public Type BonusType { get; set; } = Type.None;
	[Export] public Season BonusSeason { get; set; } = Season.None;
	[Export] public float Multiplier { get; set; } = 1f;
}
