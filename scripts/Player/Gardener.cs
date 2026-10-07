using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class Gardener : Node
{
	[Signal] public delegate void PlantHarvestedEventHandler(int total);
	[Signal] public delegate void GardenEmptyEventHandler(int total);

	[Export] public Hands Hands { get; set; }
	[Export] public Hand LeftHand { get; set; }
	[Export] public Hand RightHand { get; set; }
	[Export] public SeedStock Seeds { get; set; }

	public int HarvestCount { get; private set; }

	private readonly List<Plot> plots = new();
	private bool over;

	public override void _Ready()
	{
		foreach (Node node in GetTree().GetNodesInGroup("plots"))
		{
			if (node is Plot plot)
			{
				plots.Add(plot);
				plot.Clicked += OnPlotClicked;
			}
		}
	}

	private void OnPlotClicked(Plot plot, MouseButton button)
	{
		ItemData held = Hands.Get(button);
		Plant plant = plot.Plant;
		Hand hand = button == MouseButton.Right ? RightHand : LeftHand;

		if (hand.IsBusy)
		{
			return;
		}

		if (plant is null)
		{
			if (held?.PlantScene is not null && Seeds.Count(held) > 0)
			{
				plot.Sow(held);
				plot.Plant.StageChanged += _ => CheckIfEmpty();
				Seeds.Use(held);
			}
			return;
		}

		if (held is null)
		{
			if (plant.IsHarvestable)
			{
				ItemData sown = plot.SownWith;
				Hands.Set(button, plant.Produce);
				plot.Clear();

				HarvestCount++;
				EmitSignal(SignalName.PlantHarvested, HarvestCount);
				Seeds.Add(sown, plant.RollSeeds());
				CheckIfEmpty();
			}
			return;
		}

		if (held.DigsUpPlants)
		{
			plot.Clear();
			CheckIfEmpty();
			return;
		}

		if (held.Care == Care.None)
		{
			return;
		}

		hand.UseOn(plot.GlobalPosition, () =>
		{
			if (held.CareEffect is not null)
			{
				plot.AddChild(held.CareEffect.Instantiate());
			}

			if (plot.Plant == plant)
			{
				plant.GiveCare(held.Care);
			}
		});
	}

	private void CheckIfEmpty()
	{
		if (over || Seeds.Total > 0 || plots.Any(plot => plot.Plant is { IsAlive: true }))
		{
			return;
		}

		over = true;
		EmitSignal(SignalName.GardenEmpty, HarvestCount);
	}
}
