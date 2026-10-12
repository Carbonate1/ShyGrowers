using Godot;

[GlobalClass]
public partial class PlantData : Resource
{
	[Export] public int PlantSize { get; set; } = 1;
	[Export] public string DisplayName { get; set; } = "DISPLAY_NAME";
	[Export] public int OptimalSaleValue { get; set; } = 7;
	[Export] public float GrowthTime { get; set; } = 10.0f;
	[Export] public float HarvestableTime { get; set; } = 10.0f;
	[Export] public float DecayTime { get; set; } = 5.0f;
	[Export] public float DecayRate { get; set; } = 0.01f;
	[Export] public float InventoryDecayRate { get; set; } = 0.05f;
	[Export] public Season GrowingSeason { get; set; } = Season.Any;
	[Export] public AdjacencyBonus Adjacency { get; set; } = new();
}
