using Godot;

public partial class Fertilizer : ToolItem
{
	public override ToolUseResult UseOnPlant(Plant plant)
	{
		return plant.CompleteCare(Care.Fertilizer)
			? ToolUseResult.CareCompleted
			: ToolUseResult.NoEffect;
	}
}
