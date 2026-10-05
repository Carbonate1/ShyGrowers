using System;

public partial class SeedPacket : ToolItem
{
	public event Action<PlantBox, int> PlantSelectionRequested;

	public override bool UseOnEmptyPlot(PlantBox plantBox, int plotIndex)
	{
		ArgumentNullException.ThrowIfNull(plantBox);
		PlantSelectionRequested?.Invoke(plantBox, plotIndex);
		return true;
	}
}
