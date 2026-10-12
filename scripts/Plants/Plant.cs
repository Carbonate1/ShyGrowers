using Godot;
using System;

public enum Care
{
	None,
	Watering,
	Fertilizing,
	Weeding
}

[GlobalClass]
public partial class Plant : Node3D
{
	private const float MINIMUM_SALE_VALUE = 1f;
	private const float WRONG_CARE_PUNISHMENT = 0.3f;
	private const float QUICK_CARE_BONUS = 0.5f;
	private const float TIME_FOR_QUICK_CARE_BONUS = 5f;

	
	public enum Stage
	{
		Planted,
		Growing1,
		Growing2,
		Harvestable,
		Decaying,
		Dead
	}

	[Signal] public delegate void StageChangedEventHandler(Plant plant);

	[Export] public PlantData Data { get; set; }
	[Export] public Vector2I SeedYield { get; set; } = new(1, 2);

	[ExportGroup("Nodes")]
	[Export] public Node3D Stages { get; set; }
	[Export] public Label3D NeedLabel { get; set; }

	public Stage CurrentStage { get; private set; }
	public Care CareNeeded { get; private set; }
	public float TimeScale { get; internal set; } = 1f;
	public bool IsHarvestable => CurrentStage is Stage.Harvestable or Stage.Decaying;
	public bool IsGrowing => CurrentStage <= Stage.Harvestable;
	public bool IsAlive => IsGrowing || CurrentStage == Stage.Decaying;
	public float GetSaleValue => saleValue;
	public Texture2D HarvestableTexture =>
		Stages?.GetNodeOrNull<Sprite3D>(nameof(Stage.Harvestable))?.Texture;

	private double timeElapsed;
	private float saleValue;
	private bool decayingSaleValueApplied;
	private readonly Random random = new();
	private readonly Timer waitingForCare = new(); // If care is given before the timer stops, the plant's sale value incrases
												   // Start timer when care is selected

	public override void _Ready()
	{
		ValidateConfiguration();

		AddChild(waitingForCare);
		waitingForCare.OneShot = true;
		waitingForCare.WaitTime = TIME_FOR_QUICK_CARE_BONUS;

		saleValue = Data.OptimalSaleValue;
		CareNeeded = Care.Watering;
		waitingForCare.Start();
		
		ShowStage();
	}

	public override void _Process(double delta)
	{
		if (CurrentStage != Stage.Dead && CareNeeded == Care.None)
		{
			timeElapsed += delta * TimeScale;
			if (timeElapsed >= StageDuration)
			{
				timeElapsed = 0;
				if (CurrentStage is Stage.Planted or Stage.Growing1 or Stage.Growing2)
				{
					CareNeeded = SelectCare();
					UpdateLabel();
				}
				else
				{
					Advance();
				}
			}
		}

		UpdateSaleValue(delta * TimeScale);
		UpdateLabel();
	}

	private Care SelectCare()
	{
		waitingForCare.Start();
		return (Care)random.Next((int)Care.Watering, (int)Care.Weeding + 1);
	}
	public bool CompleteCare(Care completedCare)
	{
		if (completedCare == Care.None ||
			CurrentStage is not (Stage.Planted or Stage.Growing1 or Stage.Growing2))
		{
			return false;
		}

		if (completedCare != CareNeeded)
		{
			saleValue -= WRONG_CARE_PUNISHMENT;
			return false;
		}

		CareNeeded = Care.None;
		timeElapsed = 0;
		
		if (!waitingForCare.IsStopped())
		{
			saleValue += QUICK_CARE_BONUS;
		}
		Advance();
		return true;
	}

	public int GetSeedYield()
	{
		return GD.RandRange(SeedYield.X, SeedYield.Y);
	}

	private double StageDuration => CurrentStage switch
	{
		Stage.Harvestable => Data.HarvestableTime,
		Stage.Decaying => Data.DecayTime,
		_ => Data.GrowthTime
	};

	private void ValidateConfiguration()
	{
		ArgumentNullException.ThrowIfNull(Data);
		ArgumentException.ThrowIfNullOrWhiteSpace(Data.DisplayName);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(Data.GrowthTime, 0);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(Data.HarvestableTime, 0);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(Data.DecayTime, 0);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(Data.DecayRate, 0);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(Data.OptimalSaleValue, 0);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(Data.InventoryDecayRate, 0);
		ArgumentOutOfRangeException.ThrowIfLessThan(Data.PlantSize, 1);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(Data.PlantSize, PlanterBox.PLOT_COUNT);
		ArgumentNullException.ThrowIfNull(Data.Adjacency);
	}

	private void UpdateSaleValue(double delta)
	{
		if (CurrentStage == Stage.Dead)
		{
			return;
		}

		if (CareNeeded != Care.None || CurrentStage == Stage.Decaying)
		{
			saleValue -= Data.DecayRate * (float)delta;
		}

		if (CurrentStage == Stage.Decaying && !decayingSaleValueApplied)
		{
			saleValue -= 1f;
			decayingSaleValueApplied = true;
		}

		if (saleValue <= MINIMUM_SALE_VALUE)
		{
			saleValue = MINIMUM_SALE_VALUE;
			SetStage(Stage.Dead);
		}
	}

	private void Advance()
	{
		Stage nextStage = CurrentStage switch
		{
			Stage.Planted => Stage.Growing1,
			Stage.Growing1 => Stage.Growing2,
			Stage.Growing2 => Stage.Harvestable,
			Stage.Harvestable => Stage.Decaying,
			Stage.Decaying => Stage.Dead,
			_ => Stage.Dead
		};

		SetStage(nextStage);
	}

	private void SetStage(Stage stage)
	{
		if (CurrentStage == stage)
		{
			return;
		}

		CurrentStage = stage;
		timeElapsed = 0;
		ShowStage();
		EmitSignal(SignalName.StageChanged, this);
	}

	private void ShowStage()
	{
		if (Stages is not null)
		{
			foreach (Node child in Stages.GetChildren())
			{
				if (child is Node3D stage)
				{
					stage.Visible = stage.Name == CurrentStage.ToString();
				}
			}
		}

		UpdateLabel();
	}

	private void UpdateLabel()
	{
		if (NeedLabel is null)
		{
			return;
		}

		NeedLabel.Text = CareNeeded switch
		{
			Care.Watering => "thirsty",
			Care.Fertilizing => "hungry",
			Care.Weeding => "weedy",
			_ => CurrentStage switch
			{
				Stage.Harvestable => "ready!",
				Stage.Decaying => "wilting...",
				Stage.Dead => "gone",
				_ => ""
			}
		}  	+ $"\nGrowing time: {Math.Round(timeElapsed, 1)}/{StageDuration}"
			+ $"\nSale Value: {Math.Round(saleValue, 2)}";
	}
}
