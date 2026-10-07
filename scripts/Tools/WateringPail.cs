public partial class WateringPail : ToolItem
{
	public override ToolUseResult UseOnPlant(Plant plant)
	{
		return plant.CompleteCare(Care.Watering)
			? ToolUseResult.CareCompleted
			: ToolUseResult.NoEffect;
	}
}
