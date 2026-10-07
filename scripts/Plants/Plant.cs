using Godot;
using System;
using System.Collections.Generic;

public sealed record PlantData(
	int GrowthTime, // Time between each growth stage
	int OptimalSaleValue, // Value of the plant when sold at its optimal time
	int HarvestableTime, // Time the plant remains harvestable (in both harvestable & decaying state)
	int DecayTime, // Time for the harvested plant's value to decay in the inventory
	double OutOfViewTimeMultiplier
);

public enum Care
{
	None,
	Watering,
	Fertilizing,
	Weeding
}

public abstract partial class Plant : Node2D
{
	public enum State
	{
		Planted,
		Growing1,
		Growing2,
		Harvestable,
		Decaying,
		Dead,
		Harvested
	}

	// Member variables
	// Const
	private const float DECAY_RATE = 0.05F;

	// Private
	private readonly PlantData plantData;
	private State plantState;
	private Area2D stageArea;
	private readonly Dictionary<State, CollisionShape2D> stageShapes = new();
	private readonly Dictionary<State, Sprite2D> stageSprites = new();
	private double configuredStageDuration;
	private int owningPlantBoxIdx = -1;
	private int playerOrientationIdx = -1;
	private PlantBox owningPlantBox;

	// Protected
	protected Timer stageTimer;
	protected bool decayStarted;
	protected float saleValue; // TODO: Set this up to reflected optimal value * customer willingness * time spent decaying

	// Public
	public Care careNeeded;
	public int plantSize; // Indicates how many plots this plant occupies in a PlantBox
	public Season growingSeason;
	public AdjacencyBonus adjBonus;

	// Properties
	protected State currentState
	{
		get => plantState;
		set
		{
			plantState = value;
			UpdateGrowthStage();
		}
	}

	public bool IsHarvestable => currentState == State.Harvestable;
	public bool IsGrowing => 	currentState == State.Planted || 
								currentState == State.Growing1 || 
								currentState == State.Growing2 || 
								currentState == State.Harvestable;
	public string PlantTypeName => GetType().Name;
	public Texture2D HarvestableTexture => stageSprites[State.Harvestable].Texture;
	public float GetSaleValue => saleValue;
	public int DecayTime => plantData.DecayTime;
	

	// Events
	public event Action<Plant, Care> careCompleted;
	public event Action<Plant, MouseButton> clicked;

	protected State NextState()
	{
		return currentState switch
		{
			State.Planted => State.Growing1,
			State.Growing1 => State.Growing2,
			State.Growing2 => State.Harvestable,
			State.Harvestable => State.Decaying,
			State.Decaying => State.Dead,

			// The following states can not be transitioned from, but are included for completeness
			State.Dead => State.Dead,
			State.Harvested => State.Harvested,
			_ => throw new ArgumentOutOfRangeException()
		};
	}

	protected Plant(
		PlantData plantData,
		int plantSize,
		Season growingSeason,
		AdjacencyBonus adjBonus)
	{
		ArgumentNullException.ThrowIfNull(plantData);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(plantData.OutOfViewTimeMultiplier, 0);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(plantData.GrowthTime, 0);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(plantData.HarvestableTime, 0);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(plantData.DecayTime, 0);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(plantData.OptimalSaleValue, 0);
		ArgumentOutOfRangeException.ThrowIfLessThan(plantSize, 1);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(plantSize, PlantBox.PLOT_COUNT);

		this.growingSeason = growingSeason;
		this.plantData = plantData;
		this.plantSize = plantSize;
		this.adjBonus = adjBonus;
		saleValue = plantData.OptimalSaleValue;
		currentState = State.Planted;
		careNeeded = Care.None;
		decayStarted = false;
		AddChild(stageTimer = new Timer
        {
            OneShot = true
        });

	}

	private void OnStageAreaInputEvent(
		Node viewport,
		InputEvent @event,
		long shapeIdx)
	{
		if (@event is InputEventMouseButton mouseButtonEvent &&
			mouseButtonEvent.Pressed &&
			mouseButtonEvent.ButtonIndex is MouseButton.Left or MouseButton.Right
		   )
		{
			clicked?.Invoke(this, mouseButtonEvent.ButtonIndex);
			GetViewport().SetInputAsHandled();
		}
	}

	private void UpdateGrowthStage()
	{
		foreach (KeyValuePair<State, CollisionShape2D> stage in stageShapes)
		{
			bool isCurrentStage = stage.Key == plantState;
			stage.Value.Visible = isCurrentStage;
			stage.Value.Disabled = !isCurrentStage;
			stageSprites[stage.Key].Visible = isCurrentStage;
		}
	}

	// Used to display time remaining and adjust timer speed when the plant is out of view.
	public double GetTimerDuration(int duration)
	{
		int multiplier = owningPlantBox is null
			? 1
			: owningPlantBox.GetAdjacentPlantTimeBonuses(this);
		duration *= multiplier;

		return owningPlantBoxIdx != playerOrientationIdx
			? duration / plantData.OutOfViewTimeMultiplier
			: duration;
	}

	internal void SetOwningPlantBox(PlantBox plantBox)
	{
		owningPlantBox = plantBox;
		UpdateTimerDuration();
	}

	internal void SetOwningPlantBoxIdx(int newPlantBoxIdx)
	{
		owningPlantBoxIdx = newPlantBoxIdx;
		UpdateTimerDuration();
	}

	internal void SetPlayerOrientationIdx(int newPlayerOrientationIdx)
	{
		if (playerOrientationIdx == newPlayerOrientationIdx)
		{
			return;
		}

		playerOrientationIdx = newPlayerOrientationIdx;
		UpdateTimerDuration();
	}

	protected void UpdateTimerDuration()
	{
		int baseStageDuration = currentState switch
		{
			State.Planted or State.Growing1 or State.Growing2 => plantData.GrowthTime,
			State.Harvestable or State.Decaying => plantData.HarvestableTime,
			_ => 0
		};

		if (baseStageDuration == 0)
		{
			return;
		}

		double newDuration = GetTimerDuration(baseStageDuration);
		if (Math.Abs(configuredStageDuration - newDuration) < 0.01)
		{
			return;
		}

		bool wasRunning = !stageTimer.IsStopped() && stageTimer.TimeLeft > 0;
		double remainingFraction = wasRunning && configuredStageDuration > 0
			? Math.Clamp(stageTimer.TimeLeft / configuredStageDuration, 0, 1)
			: 1;

		stageTimer.Stop();
		stageTimer.WaitTime = newDuration;
		if (wasRunning)
		{
			stageTimer.Start(newDuration * remainingFraction);
		}

		configuredStageDuration = newDuration;
	}

	protected void SelectCareNeeded()
	{
		Random random = new Random();
		int careNeededIndex = random.Next(1, Enum.GetValues(typeof(Care)).Length);
		careNeeded = (Care)careNeededIndex;
	}

	public bool CompleteCare(Care completedCare)
	{
		if (completedCare == Care.None ||
			careNeeded != completedCare ||
			currentState is not (State.Planted or State.Growing1 or State.Growing2))
		{
			return false;
		}

		careNeeded = Care.None;
		currentState = NextState();
		decayStarted = currentState == State.Harvestable;
		StartStageTimer(currentState == State.Harvestable
			? plantData.HarvestableTime
			: plantData.GrowthTime);
		careCompleted?.Invoke(this, completedCare);
		return true;
	}

	private void StartStageTimer(int duration)
	{
		SetStageTimerDuration(duration);
		stageTimer.Start();
	}

	protected void SetStageTimerDuration(int duration)
	{
		configuredStageDuration = GetTimerDuration(duration);
		stageTimer.WaitTime = configuredStageDuration;
	}

	protected void TimeCheck(int harvestableTime)
	{
		if (careNeeded != Care.None || stageTimer.TimeLeft > 0)
		{
			return;
		}

		if (currentState is State.Planted or State.Growing1 or State.Growing2)
		{
			SelectCareNeeded();
		}
		else if (currentState == State.Harvestable)
		{
			if (!decayStarted)
			{
				StartStageTimer(harvestableTime);
				decayStarted = true;
			} 
			else
			{
				currentState = NextState();
				StartStageTimer(harvestableTime);
			}
		}
		else if (currentState == State.Decaying)
		{
			currentState = NextState();
		}
	}

	protected void UpdateInfoDisplay()
	{
		Label infoLabel = GetNodeOrNull<Label>("InfoLabel");
		if (infoLabel is null)
		{
			return;
		}

		if (!stageTimer.IsStopped() && stageTimer.TimeLeft > 0)
		{
			infoLabel.Text = $"{Math.Ceiling(stageTimer.TimeLeft)}s";
		}
		else
		{
			infoLabel.Text = $"{careNeeded}";
		}

		infoLabel.Text += $"\nSale value = {saleValue}";
	}

	public override void _Ready()
	{
		stageArea = GetNodeOrNull<Area2D>("Area")
			?? throw new InvalidOperationException("Plant scene is missing its 'Area' node.");
		stageArea.CollisionLayer = 1;
		stageArea.CollisionMask = 0;
		stageArea.InputPickable = true;
		stageArea.InputEvent += OnStageAreaInputEvent;

		foreach (State stage in Enum.GetValues<State>())
		{
			if (stage == State.Harvested)
			{
				continue;
			}

			string stageName = stage.ToString();
			CollisionShape2D shape = stageArea.GetNodeOrNull<CollisionShape2D>(stageName)
				?? throw new InvalidOperationException(
				   $"Plant scene is missing the '{stageName}' collision shape.");
			Sprite2D sprite = stageArea.GetNodeOrNull<Sprite2D>($"{stageName}/Sprite")
				?? throw new InvalidOperationException(
				   $"Plant scene is missing the '{stageName}/Sprite' node.");

			stageShapes.Add(stage, shape);
			stageSprites.Add(stage, sprite);
		}

		UpdateGrowthStage();
	}

	private void UpdateSaleValue(double delta)
	{
		if ((currentState != State.Dead && careNeeded != Care.None) ||
			(currentState == State.Decaying))
		{
			saleValue -= DECAY_RATE * (float)delta;
		}

		if (saleValue <= 0.0)
		{
			currentState = State.Dead;
			saleValue = 0;
		}
	}

	public override void _Process(double delta)
	{
		TimeCheck(plantData.HarvestableTime);
		UpdateTimerDuration();
		UpdateInfoDisplay();
		UpdateSaleValue(delta);
	}
}
