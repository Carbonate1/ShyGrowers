using Godot;

public enum Care
{
	None,
	Water,
	Fertilizer,
	Weed
}

[GlobalClass]
public partial class Plant : Node3D
{
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
	[Export] public ItemData Produce { get; set; }
	[Export] public Vector2I SeedYield { get; set; } = new(1, 2);
	[Export] public Care FirstNeed { get; set; } = Care.Water;

	[ExportGroup("Nodes")]
	[Export] public Node3D Stages { get; set; }
	[Export] public Label3D NeedLabel { get; set; }

	public Stage CurrentStage { get; private set; }
	public Care CareNeeded { get; private set; }
	public float TimeScale { get; set; } = 1f;
	public bool IsHarvestable => CurrentStage == Stage.Harvestable;
	public bool IsAlive => CurrentStage < Stage.Decaying;

	private float stageTime;

	private float StageLength => CurrentStage switch
	{
		Stage.Harvestable => HarvestableTime,
		Stage.Decaying => DecayTime,
		_ => GrowthTime
	};

	public override void _Ready()
	{
		CareNeeded = FirstNeed;
		ShowStage();
	}

	public override void _Process(double delta)
	{
		if (CareNeeded != Care.None || CurrentStage == Stage.Dead)
		{
			return;
		}

		stageTime += (float)delta * TimeScale;
		if (stageTime < StageLength)
		{
			return;
		}

		if (CurrentStage < Stage.Harvestable)
		{
			CareNeeded = (Care)GD.RandRange(1, 3);
			UpdateLabel();
		}
		else
		{
			Advance();
		}
	}

	public bool GiveCare(Care care)
	{
		if (care == Care.None || care != CareNeeded)
		{
			return false;
		}

		CareNeeded = Care.None;
		Advance();
		return true;
	}

	public int RollSeeds()
	{
		return GD.RandRange(SeedYield.X, SeedYield.Y);
	}

	private void Advance()
	{
		CurrentStage++;
		stageTime = 0f;
		ShowStage();
		EmitSignal(SignalName.StageChanged, this);
	}

	private void ShowStage()
	{
		foreach (Node child in Stages.GetChildren())
		{
			if (child is Node3D stage)
			{
				stage.Visible = stage.Name.ToString() == CurrentStage.ToString();
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
			Care.Water => "thirsty",
			Care.Fertilizer => "hungry",
			Care.Weed => "weedy",
			_ => CurrentStage switch
			{
				Stage.Harvestable => "ready!",
				Stage.Decaying => "wilting...",
				Stage.Dead => "gone",
				_ => ""
			}
		};
	}
}
