using Godot;

[GlobalClass]
public partial class PlantData : Resource
{
	[Export] public int PlantSize { get; set; } = 1;
	[Export] public string DisplayName { get; set; } = "DISPLAY_NAME";
	[Export] public int OptimalSaleValue { get; set; } = 5;
	[Export] public float GrowthTime { get; set; } = 20.0f;
	[Export] public float HarvestableTime { get; set; } = 40.0f;
	[Export] public float DecayTime { get; set; } = 20.0f;
	[Export] public float DecayRate { get; set; } = 0.01f;
	[Export] public float InventoryDecayRate { get; set; } = 0.001f;
	[Export] public Godot.Collections.Array<Biome> GrowingBiomes { get; set; } = new() {Biome.Any};
	[Export] public AdjacencyBonus Adjacency { get; set; } = new();
	[Export] public bool Unlocked { get; set; } = false;
}
