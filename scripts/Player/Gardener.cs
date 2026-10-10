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

	private readonly List<PlanterBox> planterBoxes = new();
	private bool gameOver;
	private bool inventoryOpen;

	public override void _Ready()
	{
		gameOver = false;
		inventoryOpen = false;
		Inventory.Hide();
		Inventory.CloseRequested += CloseInventory;
		
		foreach (Node node in GetTree().GetNodesInGroup("planter_boxes"))
		{
			if (node is PlanterBox planterBox)
			{
				planterBoxes.Add(planterBox);
				planterBox.PlotClicked += OnPlotClicked;
			}
		}
	}

	public override void _ExitTree()
	{
		if (Inventory is not null)
		{
			Inventory.CloseRequested -= CloseInventory;
		}

		foreach (PlanterBox planterBox in planterBoxes)
		{
			planterBox.PlotClicked -= OnPlotClicked;
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

	private void OnPlotClicked(PlanterBox planterBox, int plotIndex, MouseButton button)
	{
		Hand hand = Hands.GetHand(button);
		ItemData held = hand.Item;
		Plant plant = planterBox.GetPlantAtPlot(plotIndex);

		if (hand.IsBusy)
		{
			return;
		}

		if (plant is null)
		{
			AttemptPlantSeed(held, planterBox, plotIndex);
			return;
		}

		if (held is null)
		{
			AttemptHarvest(plant, planterBox, plotIndex);
			return;
		}

		if (held.DigsUpPlants)
		{
			AttemptRemovePlant(plant, planterBox);
			return;
		}

		if (held.Care == Care.None)
		{
			return;
		}

		hand.UseOn(planterBox.GetPlotGlobalPosition(plotIndex), () =>
		{
			if (held.CareEffect is not null)
			{
				Node3D effect = held.CareEffect.Instantiate<Node3D>();
				planterBox.AddChild(effect);
				effect.GlobalPosition = planterBox.GetPlotGlobalPosition(plotIndex);
			}

			if (planterBox.GetPlantAtPlot(plotIndex) == plant)
			{
				plant.CompleteCare(held.Care);
			}
		});
	}

	private void AttemptPlantSeed(ItemData held, PlanterBox planterBox, int plotIndex)
	{
		if (held?.PlantScene is null || Seeds.Count(held) <= 0)
		{
			return;
		}

		Plant plant = held.PlantScene.Instantiate<Plant>();
		if (!planterBox.TryPlant(plant, plotIndex, held))
		{
			plant.Free();
			return;
		}

		plant.StageChanged += _ => CheckIfGameOver();
		Seeds.Use(held);
	}

	private void AttemptHarvest(Plant plant, PlanterBox planterBox, int plotIndex)
	{
		if (!plant.IsHarvestable)
		{
			return;
		}

		ItemData sown = planterBox.GetSeedsAtPlot(plotIndex);
		float saleMultiplier = planterBox.GetAdjacentPlantSaleBonuses(plant);
		var harvestedItem = new InventoryItem(plant, saleMultiplier);
		int seedYield = plant.GetSeedYield();
		if (!planterBox.RemovePlant(plant))
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

	private void AttemptRemovePlant(Plant plant, PlanterBox planterBox)
	{
		if (planterBox.RemovePlant(plant))
		{
			plant.QueueFree();
		}
		CheckIfGameOver();
	}

	private void CloseInventory()
	{
		inventoryOpen = false;
		Inventory.Hide();
	}

	private void CheckIfGameOver()
	{
		if (gameOver || Seeds.Total > 0 || planterBoxes.Any(box =>
			Enumerable.Range(0, PlanterBox.PLOT_COUNT)
				.Any(index => box.GetPlantAtPlot(index) is { IsAlive: true })))
		{
			return;
		}

		gameOver = true;
		EmitSignal(SignalName.GardenEmpty, HarvestCount);
	}
}
