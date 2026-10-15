using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Gardener : Node
{
	private const float MAX_LOOK_AWAY_SPEED = 3f;
	private const float LOOK_AWAY_SPEED_CHANGE_PER_BOX = 1.5f/8;
	private const float INITIAL_COIN_THRESHOLD = 10f;
	public const int TIME_TO_MEET_THRESHOLD = 120;

	[Signal] public delegate void PlantHarvestedEventHandler(int total);
	[Signal] public delegate void GardenEmptyEventHandler(int total);

	[Export] public Hands Hands { get; set; }
	[Export] public SeedStock Seeds { get; set; }
	[Export] public Inventory Inventory { get; set; }
	[Export] public PlantSelectionMenu PlantSelectionMenu { get; set; }
	[Export] public UnlockProgress Unlocks { get; set; }
	[Export] public ShopController ShopController { get; set; }

	public int HarvestCount { get; private set; }
	public float CoinThreshold;
	public float CurrentCoins => ShopController.CurrentCoins;
	public uint ThresholdsCompleted => thresholdsCompleted;

	public readonly Timer endlessTimer = new();
	private readonly List<PlanterBox> planterBoxes = new();
	private readonly Dictionary<string, PlantData> plantDataByName = new();
	private bool gameOver;
	private uint thresholdsCompleted;

	public override void _Ready()
	{
		ArgumentNullException.ThrowIfNull(Seeds);
		ArgumentNullException.ThrowIfNull(PlantSelectionMenu);
		ArgumentNullException.ThrowIfNull(Unlocks);
		ArgumentNullException.ThrowIfNull(ShopController);

		foreach (SeedOffer offer in SeedCatalog.Offers)
		{
			plantDataByName.Add(
				offer.Name,
				GD.Load<PlantData>(offer.PlantDataPath)
					?? throw new InvalidOperationException($"Could not load plant data for offer '{offer.Name}'."));
		}
		
		foreach (Node node in GetTree().GetNodesInGroup("planter_boxes"))
		{
			if (node is PlanterBox planterBox)
			{
				planterBoxes.Add(planterBox);
				planterBox.PlotClicked += OnPlotClicked;
			}
		}

		CoinThreshold = INITIAL_COIN_THRESHOLD;
		gameOver = false;

		AddChild(endlessTimer);
		endlessTimer.WaitTime = TIME_TO_MEET_THRESHOLD;
		endlessTimer.OneShot = true;
		endlessTimer.Start();
		endlessTimer.Timeout += CheckIfGameOver;
		thresholdsCompleted = 0;
	}

    public override void _Process(double delta)
    {
		int numUsedPlanterBoxes = GetNumUsedPlanterBoxes();
		float lookAwaySpeed = Math.Clamp(
			MAX_LOOK_AWAY_SPEED - LOOK_AWAY_SPEED_CHANGE_PER_BOX * (numUsedPlanterBoxes - 1),
			1,
			MAX_LOOK_AWAY_SPEED
		);

		foreach (PlanterBox planterBox in planterBoxes)
		{
			planterBox.UpdateLookAwaySpeed(lookAwaySpeed);
		}
    }

	public override void _ExitTree()
	{
		foreach (PlanterBox planterBox in planterBoxes)
		{
			planterBox.PlotClicked -= OnPlotClicked;
		}
	}

	private void OnPlotClicked(PlanterBox planterBox, int plotIndex, MouseButton button)
	{
		if (!Unlocks.IsPlanterUnlocked(planterBox.GrowingBiome))
		{
			return;
		}

		Hand hand = Hands.GetHand(button);
		ItemData held = hand.Item;
		Plant plant = planterBox.GetPlantAtPlot(plotIndex);

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

	private void OpenPlantSelection(PlanterBox planterBox, int plotIndex)
	{
		Dictionary<string, int> seedCounts = plantDataByName.ToDictionary(
			entry => entry.Key,
			entry => Seeds.Count(entry.Value));
		PlantSelectionMenu.Open(
			planterBox,
			plotIndex,
			seedCounts,
			Unlocks.IsPlantUnlocked,
			TryConsumeSeed);
	}

	private bool TryConsumeSeed(string offerName)
	{
		if (!Unlocks.IsPlantUnlocked(offerName) ||
			!plantDataByName.TryGetValue(offerName, out PlantData plantData) ||
			Seeds.Count(plantData) <= 0)
		{
			return false;
		}

		Seeds.Use(plantData);
		return true;
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
	}

	private void AttemptRemovePlant(Plant plant, PlanterBox planterBox)
	{
		if (planterBox.RemovePlant(plant))
		{
			plant.QueueFree();
		}
	}

	private void CheckIfGameOver()
	{
		if (!gameOver && CurrentCoins < CoinThreshold)
		{
			gameOver = true;
			EmitSignal(SignalName.GardenEmpty, HarvestCount);
		}
		else
		{
			IncreaseCoinThreshold();
		}
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

	private void IncreaseCoinThreshold()
	{
		thresholdsCompleted++;
		CoinThreshold = CoinThreshold * 1.1f + INITIAL_COIN_THRESHOLD;
		endlessTimer.Start();
	}
}
