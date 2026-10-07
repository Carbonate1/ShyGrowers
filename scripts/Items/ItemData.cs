using Godot;

[GlobalClass]
public partial class ItemData : Resource
{
	[Export] public string DisplayName { get; set; } = "";
	[Export] public Texture2D Icon { get; set; }
	[Export(PropertyHint.File, "*.tscn")] public string Pickup { get; set; } = "";

	[ExportGroup("Gardening")]
	[Export] public Care Care { get; set; }
	[Export] public PackedScene CareEffect { get; set; }
	[Export] public PackedScene PlantScene { get; set; }
	[Export] public bool DigsUpPlants { get; set; }

	[ExportGroup("Shop")]
	[Export] public int SellValue { get; set; }

	public bool IsPhysical => !string.IsNullOrEmpty(Pickup);

	public ItemPickup MakePickup()
	{
		return GD.Load<PackedScene>(Pickup).Instantiate<ItemPickup>();
	}
}
