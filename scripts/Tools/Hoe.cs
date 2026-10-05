public partial class Hoe : ToolItem
{
	public override ToolUseResult UseOnPlant(Plant plant)
	{
		return plant.CompleteCare(Care.Weed)
			? ToolUseResult.CareCompleted
			: ToolUseResult.NoEffect;
	}
}
