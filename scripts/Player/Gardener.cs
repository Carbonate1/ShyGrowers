using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class Gardener : Node
{
	[Signal] public delegate void PlantHarvestedEventHandler(int total);
	[Signal] public delegate void GardenEmptyEventHandler(int total);

	[Export] public Hands Hands { get; set; }
	[Export] public SeedStock Seeds { get; set; }

	public int HarvestCount { get; private set; }

	private readonly List<Plot> plots = new();
	private bool gameOver;

	public override void _Ready()
	{
		gameOver = false;

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
		Hand hand = Hands.GetHand(button);
		ItemData held = hand.Item;
		Plant plant = plot.Plant;

		if (hand.IsBusy)
		{
			return;
		}

		if (plant is null)
		{
			AttemptPlantSeed(held, plot);
			return;
		}

		if (held is null)
		{
			AttemptHarvest(plant, plot, button);
			return;
		}

		if (held.DigsUpPlants)
		{
			plot.Clear();
			CheckIfGameOver();
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
				plant.CompleteCare(held.Care);
			}
		});
	}

	private void AttemptPlantSeed(ItemData held, Plot plot)
	{
		if (held?.PlantScene is not null && Seeds.Count(held) > 0)
		{
			if (plot.PlantSeed(held))
			{
				plot.Plant.StageChanged += _ => CheckIfGameOver();
				Seeds.Use(held);
			}
		}
	}

	private void AttemptHarvest(
		Plant plant,
		Plot plot,
		MouseButton button)
	{
		if (!plant.IsHarvestable)
		{
			return;
		}

		ItemData sown = plot.SownWith;
		ItemData produce = plant.Produce;
		int seedYield = plant.GetSeedYield();
		if (!plot.Box.RemovePlant(plant))
		{
			return;
		}

		plant.QueueFree();
		Hands.GetHand(button).Item = produce;

		HarvestCount++;
		EmitSignal(SignalName.PlantHarvested, HarvestCount);
		Seeds.Add(sown, seedYield);
		CheckIfGameOver();
	}

	private void CheckIfGameOver()
	{
		if (gameOver || Seeds.Total > 0 || plots.Any(plot => plot.Plant is { IsAlive: true }))
		{
			return;
		}

		gameOver = true;
		EmitSignal(SignalName.GardenEmpty, HarvestCount);
	}
}
