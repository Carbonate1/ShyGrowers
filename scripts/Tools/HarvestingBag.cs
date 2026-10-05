public partial class HarvestingBag : ToolItem
{
	public override ToolUseResult UseOnPlant(Plant plant)
	{
		return plant.IsHarvestable
			? ToolUseResult.HarvestPlant
			: ToolUseResult.NoEffect;
	}
}
