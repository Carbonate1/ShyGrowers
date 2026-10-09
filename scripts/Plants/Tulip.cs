using Godot;

public partial class Tulip : Plant
{
	// Consts
	const int GROWTH_TIME = 10; // in seconds
	const int OPTIMAL_SALE_VALUE = 7; // in coins
	const int HARVESTABLE_TIME = 10; // in seconds
	const float DECAY_TIME = 0.05f; // in seconds
	const double OUT_OF_VIEW_TIME_MULTIPLIER = 2.0;
	static readonly PlantData PLANT_DATA = new(
		GrowthTime: GROWTH_TIME,
		OptimalSaleValue: OPTIMAL_SALE_VALUE,
		HarvestableTime: HARVESTABLE_TIME,
		InventoryDecayRate: DECAY_TIME,
		OutOfViewTimeMultiplier: OUT_OF_VIEW_TIME_MULTIPLIER
	);
	static readonly AdjacencyBonus adjacencyBonus = new AdjacencyBonus();

	public override bool IsHarvestable => 	currentState == State.Harvestable || 
											currentState == State.Decaying ||
											currentState == State.Dead;

	public Tulip() : base(
		plantData: PLANT_DATA, 
		plantSize: 1, 
		growingSeason: Season.Spring,
		adjBonus: adjacencyBonus)
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

	protected override void UpdateSaleValue(double delta)
	{
		if (currentState == State.Dead)
		{
			saleValue = 1;
			return;
		}

		if (saleValue <= 1)
		{
			currentState = State.Dead;
			saleValue = 1;
		}

		if ((currentState != State.Dead && careNeeded != Care.None) ||
			(currentState == State.Decaying))
		{
			saleValue -= owningPlantBoxIdx != playerOrientationIdx
			? DECAY_RATE * (float)delta * (float)PLANT_DATA.OutOfViewTimeMultiplier
			: DECAY_RATE * (float)delta;
			
		}

		// When the plant hits its decaying stage, sale value drops a substantial amount
		if (currentState == State.Decaying &&!decayingSaleValueApplied)
		{
			saleValue -= 1;
			decayingSaleValueApplied = true;
		}
	}
}
