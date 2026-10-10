using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class Gardener : Node
{
	[Signal] public delegate void PlantHarvestedEventHandler(int total);
	[Signal] public delegate void GardenEmptyEventHandler(int total);

	[Export] public Hands Hands { get; set; }
	[Export] public SeedStock Seeds { get; set; }
	[Export] public Inventory Inventory { get; set; }

	public int HarvestCount { get; private set; }

	private readonly List<Plot> plots = new();
	private bool gameOver;
	private bool inventoryOpen;

	public override void _Ready()
	{
		gameOver = false;
		inventoryOpen = false;
		Inventory.Hide();
		Inventory.CloseRequested += CloseInventory;
		
		foreach (Node node in GetTree().GetNodesInGroup("plots"))
		{
			if (node is Plot plot)
			{
				plots.Add(plot);
				plot.Clicked += OnPlotClicked;
			}
		}
	}

	public override void _ExitTree()
	{
		if (Inventory is not null)
		{
			Inventory.CloseRequested -= CloseInventory;
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false } keyEvent)
		{
			return;
		}

		if (keyEvent.Keycode == Key.I)
		{
			inventoryOpen = !inventoryOpen;
			Inventory.Visible = inventoryOpen;
			GetViewport().SetInputAsHandled();
		}
		else if (keyEvent.Keycode == Key.Escape && inventoryOpen)
		{
			CloseInventory();
			GetViewport().SetInputAsHandled();
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
			AttemptHarvest(plant, plot);
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

	private void AttemptHarvest(Plant plant, Plot plot)
	{
		if (!plant.IsHarvestable)
		{
			return;
		}

		ItemData sown = plot.SownWith;
		float saleMultiplier = plot.Box.GetAdjacentPlantSaleBonuses(plant);
		var harvestedItem = new InventoryItem(plant, saleMultiplier);
		int seedYield = plant.GetSeedYield();
		if (!plot.Box.RemovePlant(plant))
		{
			return;
		}

		plant.QueueFree();
		Inventory.AddItem(harvestedItem);

		HarvestCount++;
		EmitSignal(SignalName.PlantHarvested, HarvestCount);
		Seeds.Add(sown, seedYield);
		CheckIfGameOver();
	}

	private void CloseInventory()
	{
		inventoryOpen = false;
		Inventory.Hide();
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
