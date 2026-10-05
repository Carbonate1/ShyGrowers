using Godot;

public partial class Tulip : Plant
{
	// Consts
	const int GROWTH_TIME = 10; // in seconds
	const int OPTIMAL_SALE_VALUE = 7; // in coins
	const int HARVESTABLE_TIME = 10; // in seconds
	const int DECAY_TIME = 5; // in seconds
	const double OUT_OF_VIEW_TIME_MULTIPLIER = 2.0;

	public Tulip() : base(
		new PlantData(
			GrowthTime: GROWTH_TIME,
			OptimalSaleValue: OPTIMAL_SALE_VALUE,
			HarvestableTime: HARVESTABLE_TIME,
			DecayTime: DECAY_TIME,
			OutOfViewTimeMultiplier: OUT_OF_VIEW_TIME_MULTIPLIER
		),
		plantSize: 1)
	{
		SetStageTimerDuration(GROWTH_TIME);
	}

	public override void _Ready()
	{
		base._Ready();
		stageTimer.Start();
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
	}
}
