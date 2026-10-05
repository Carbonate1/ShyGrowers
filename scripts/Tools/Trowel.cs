public partial class Trowel : ToolItem
{
	public override ToolUseResult UseOnPlant(Plant plant)
	{
		return ToolUseResult.DiscardPlant;
	}
}
