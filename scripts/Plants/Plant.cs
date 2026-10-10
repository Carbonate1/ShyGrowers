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
	private const float DECAY_RATE = 0.05f;
	private const float MINIMUM_SALE_VALUE = 1f;

	
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

	[Export] public float GrowthTime { get; set; } = 10f;
	[Export] public float HarvestableTime { get; set; } = 10f;
	[Export] public float DecayTime { get; set; } = 5f;
	[Export] public int OptimalSaleValue { get; set; } = 7;
	[Export] public float InventoryDecayRate { get; set; } = 0.05f;
	[Export] public Vector2I SeedYield { get; set; } = new(1, 2);
	[Export] public int PlantSize { get; set; } = 1;
	[Export] public Season GrowingSeason { get; set; } = Season.Any;
	[Export] public AdjacencyBonus Adjacency { get; set; } = new();

	[ExportGroup("Nodes")]
	[Export] public Node3D Stages { get; set; }
	[Export] public Label3D NeedLabel { get; set; }

	public Stage CurrentStage { get; private set; }
	public Care CareNeeded { get; private set; }
	public float TimeScale { get; internal set; } = 1f;
	public bool IsHarvestable => CurrentStage is Stage.Harvestable or Stage.Decaying;
	public bool IsGrowing => CurrentStage <= Stage.Harvestable;
	public bool IsAlive => IsGrowing || CurrentStage == Stage.Decaying;
	public string PlantTypeName => GetType().Name;
	public float GetSaleValue => saleValue;
	public Texture2D HarvestableTexture =>
		Stages?.GetNodeOrNull<Sprite3D>(nameof(Stage.Harvestable))?.Texture;

	private double timeElapsed;
	private float saleValue;
	private bool decayingSaleValueApplied;
	private readonly Random random = new();

	public override void _Ready()
	{
		ValidateConfiguration();
		saleValue = OptimalSaleValue;
		CareNeeded = SelectCare();
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
		return (Care)random.Next((int)Care.Watering, (int)Care.Weeding + 1);
	}
	public bool CompleteCare(Care completedCare)
	{
		if (completedCare == Care.None ||
			completedCare != CareNeeded ||
			CurrentStage is not (Stage.Planted or Stage.Growing1 or Stage.Growing2))
		{
			return false;
		}

		CareNeeded = Care.None;
		timeElapsed = 0;
		Advance();
		return true;
	}

	public int GetSeedYield()
	{
		return GD.RandRange(SeedYield.X, SeedYield.Y);
	}

	private double StageDuration => CurrentStage switch
	{
		Stage.Harvestable => HarvestableTime,
		Stage.Decaying => DecayTime,
		_ => GrowthTime
	};

	private void ValidateConfiguration()
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(GrowthTime, 0);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(HarvestableTime, 0);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(DecayTime, 0);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(OptimalSaleValue, 0);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(InventoryDecayRate, 0);
		ArgumentOutOfRangeException.ThrowIfLessThan(PlantSize, 1);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(PlantSize, PlanterBox.PLOT_COUNT);
		ArgumentNullException.ThrowIfNull(Adjacency);
	}

	private void UpdateSaleValue(double delta)
	{
		if (CurrentStage == Stage.Dead)
		{
			return;
		}

		if (CareNeeded != Care.None || CurrentStage == Stage.Decaying)
		{
			saleValue -= DECAY_RATE * (float)delta;
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
