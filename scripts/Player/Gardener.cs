using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Gardener : Node
{
	private const float MAX_LOOK_AWAY_SPEED = 3f;
	private const float LOOK_AWAY_SPEED_CHANGE_PER_BOX = 1.5f/8;

	[Signal] public delegate void PlantHarvestedEventHandler(int total);
	[Signal] public delegate void GardenEmptyEventHandler(int total);

	[Export] public Hands Hands { get; set; }
	[Export] public SeedStock Seeds { get; set; }
	[Export] public Inventory Inventory { get; set; }
	[Export] public PlantSelectionMenu PlantSelectionMenu { get; set; }

	public int HarvestCount { get; private set; }

	private readonly List<PlanterBox> planterBoxes = new();
	private readonly Dictionary<string, PlantData> plantDataByName = new();
	private bool gameOver;

	public override void _Ready()
	{
		ArgumentNullException.ThrowIfNull(Seeds);
		ArgumentNullException.ThrowIfNull(PlantSelectionMenu);
		gameOver = false;
		foreach (SeedOffer offer in SeedCatalog.Offers)
		{
			plantDataByName.Add(
				offer.Name,
				GD.Load<PlantData>(offer.PlantDataPath)
					?? throw new InvalidOperationException($"Could not load plant data for offer '{offer.Name}'."));
		}
		PlantSelectionMenu.PlantPlanted += OnPlantPlanted;
		
		foreach (Node node in GetTree().GetNodesInGroup("planter_boxes"))
		{
			if (node is PlanterBox planterBox)
			{
				planterBoxes.Add(planterBox);
				planterBox.PlotClicked += OnPlotClicked;
			}
		}
	}

    public override void _Process(double delta)
    {
		int numUsedPlanterBoxes = GetNumUsedPlanterBoxes();
		float lookAwaySpeed = Math.Clamp(
			MAX_LOOK_AWAY_SPEED - LOOK_AWAY_SPEED_CHANGE_PER_BOX * (numUsedPlanterBoxes - 1),
			1,
			3
		);

		foreach (PlanterBox planterBox in planterBoxes)
		{
			planterBox.UpdateLookAwaySpeed(MAX_LOOK_AWAY_SPEED - LOOK_AWAY_SPEED_CHANGE_PER_BOX * numUsedPlanterBoxes);
		}
    }

	public override void _ExitTree()
	{
		foreach (PlanterBox planterBox in planterBoxes)
		{
			planterBox.PlotClicked -= OnPlotClicked;
		}
		if (PlantSelectionMenu is not null)
		{
			PlantSelectionMenu.PlantPlanted -= OnPlantPlanted;
		}
	}

	private void OnPlotClicked(PlanterBox planterBox, int plotIndex, MouseButton button)
	{
		Plant plant = planterBox.GetPlantAtPlot(plotIndex);
		Hand hand = Hands.GetHand(button);
		if (!CanHelp(hand.Item, plant) && CanHelp(Hands.OtherHand(hand).Item, plant))
		{
			hand = Hands.OtherHand(hand);
		}

		ItemData held = hand.Item;

		if (hand.IsBusy)
		{
			return;
		}

		if (plant is null)
		{
			if (held?.OpensPlantSelection == true)
			{
				OpenPlantSelection(planterBox, plotIndex);
			}
			return;
		}

		if (held is null)
		{
			return;
		}

		if (held.Harvests)
		{
			if (plant.IsHarvestable)
			{
				hand.UseOn(planterBox.GetPlotGlobalPosition(plotIndex), () =>
				{
					if (planterBox.GetPlantAtPlot(plotIndex) == plant)
					{
						AttemptHarvest(plant, planterBox, plotIndex);
					}
				});
			}
			return;
		}

		if (held.DigsUpPlants)
		{
			hand.UseOn(planterBox.GetPlotGlobalPosition(plotIndex), () => AttemptRemovePlant(plant, planterBox));
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

	private void OpenPlantSelection(PlanterBox planterBox, int plotIndex)
	{
		Dictionary<string, int> seedCounts = plantDataByName.ToDictionary(
			entry => entry.Key,
			entry => Seeds.Count(entry.Value));
		PlantSelectionMenu.Open(planterBox, plotIndex, seedCounts, TryConsumeSeed);
	}

	private bool TryConsumeSeed(string offerName)
	{
		if (!plantDataByName.TryGetValue(offerName, out PlantData plantData) || Seeds.Count(plantData) <= 0)
		{
			return false;
		}

		Seeds.Use(plantData);
		return true;
	}

	private static bool CanHelp(ItemData item, Plant plant)
	{
		if (item is null)
		{
			return false;
		}

		if (plant is null)
		{
			return item.OpensPlantSelection;
		}

		if (item.Harvests)
		{
			return plant.IsHarvestable;
		}

		return item.Care != Care.None && item.Care == plant.CareNeeded;
	}

	private void OnPlantPlanted(Plant plant)
	{
		plant.StageChanged += _ => CheckIfGameOver();
	}

	private void AttemptHarvest(Plant plant, PlanterBox planterBox, int plotIndex)
	{
		if (!plant.IsHarvestable)
		{
			return;
		}

		PlantData sown = planterBox.GetSeedsAtPlot(plotIndex);
		float saleMultiplier = planterBox.GetAdjacentPlantSaleBonuses(plant);
		InventoryItem harvestedItem = new InventoryItem(plant, saleMultiplier);
		bool grown = plant.IsAlive;
		int seedYield = grown ? plant.GetSeedYield() : 0;
		if (!planterBox.RemovePlant(plant))
		{
			return;
		}

		plant.QueueFree();
		Inventory.AddItem(harvestedItem);

		if (grown)
		{
			HarvestCount++;
			EmitSignal(SignalName.PlantHarvested, HarvestCount);
		}
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

	private int GetNumUsedPlanterBoxes()
	{
		int numUsedPlanterBoxes = 0;
        foreach (PlanterBox planterBox in planterBoxes)
		{
			if (planterBox.HasAtLeastOnePlant)
			{
				numUsedPlanterBoxes += 1;
			}
		}

		return numUsedPlanterBoxes;
	}
}
