using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public enum Biome
{
	Winter,
	Spring,
	Summer,
	Autumn,
	Night,
	Desert,
	Space,
	Ocean,
	Any,
	None
}

[GlobalClass]
public partial class PlanterBox : Node3D
{
	[Signal] public delegate void PlotClickedEventHandler(PlanterBox planterBox, int plotIndex, MouseButton button);

	private sealed record PlantPlacement(Plant Plant, PlantData Seeds, int FirstPlot, int PlotCount);
	public const int PLOT_COUNT = 6;

	[Export] public float LookAwaySpeed { get; set; } = 3f;
	[Export] public VisibleOnScreenNotifier3D Watcher { get; set; }
	[Export] public Biome GrowingBiome { get; set; } = Biome.Any;

	[ExportGroup("Sounds")]
	[Export] public AudioStreamPlayer3D PlantedSound { get; set; }
	[Export] public AudioStreamPlayer3D NeedsCareSound { get; set; }
	[Export] public AudioStreamPlayer3D ReadySound { get; set; }
	[Export] public AudioStreamPlayer3D MistakeSound { get; set; }

	public bool IsWatched => Watcher is null || Watcher.IsOnScreen();
	public float TimeScale => IsWatched ? 1f : LookAwaySpeed;

	private readonly List<Plot> plots = new();
	private readonly List<PlantPlacement> placements = new();

	public bool HasAtLeastOnePlant => plots.Any();

	public override void _Ready()
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(LookAwaySpeed, 0);
		Node plotsNode = GetNodeOrNull<Node>("Plots")
			?? throw new InvalidOperationException("PlanterBox scene is missing its 'Plots' node.");

		foreach (Node child in plotsNode.FindChildren("*", nameof(Area3D)))
		{
			if (child is Plot plot)
			{
				plots.Add(plot);
			}
		}

		plots.Sort((left, right) => ToLocal(left.GlobalPosition).X.CompareTo(ToLocal(right.GlobalPosition).X));
		if (plots.Count != PLOT_COUNT)
		{
			throw new InvalidOperationException(
				$"PlanterBox must contain exactly {PLOT_COUNT} Plot nodes; found {plots.Count}.");
		}

		for (int i = 0; i < plots.Count; i++)
		{
			plots[i].Initialize(i);
			plots[i].Clicked += OnPlotClicked;
		}
	}

	public override void _ExitTree()
	{
		foreach (Plot plot in plots)
		{
			plot.Clicked -= OnPlotClicked;
		}
	}

	public override void _Process(double delta)
	{
		foreach (PlantPlacement placement in placements)
		{
			placement.Plant.TimeScale = TimeScale * GetAdjacentPlantTimeBonuses(placement.Plant);
		}
	}

	public Plant GetPlantAtPlot(int plotIndex)
	{
		ValidatePlotIndex(plotIndex);
		return FindPlacementAtPlot(plotIndex)?.Plant;
	}

	public PlantData GetSeedsAtPlot(int plotIndex)
	{
		ValidatePlotIndex(plotIndex);
		return FindPlacementAtPlot(plotIndex)?.Seeds;
	}

	public Vector3 GetPlotGlobalPosition(int plotIndex)
	{
		ValidatePlotIndex(plotIndex);
		return plots[plotIndex].GlobalPosition;
	}

	public bool CanPlant(Plant plant, int firstPlot)
	{
		ArgumentNullException.ThrowIfNull(plant);
		ArgumentOutOfRangeException.ThrowIfLessThan(plant.Data.PlantSize, 1);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(plant.Data.PlantSize, PLOT_COUNT);
		if (firstPlot < 0 || firstPlot + plant.Data.PlantSize > PLOT_COUNT)
		{
			return false;
		}

		for (int index = firstPlot; index < firstPlot + plant.Data.PlantSize; index++)
		{
			if (GetPlantAtPlot(index) is not null || !SameBed(firstPlot, index))
			{
				return false;
			}
		}


		if (GrowingBiome is Biome.Any or Biome.None ||
			plant.Data.GrowingBiomes.Contains(Biome.Any) || 
			plant.Data.GrowingBiomes.Contains(Biome.None) ||
			plant.Data.GrowingBiomes.Contains(GrowingBiome))
		{
			return true;
		}

		foreach (Plant neighbor in GetAdjacentPlants(firstPlot, plant.Data.PlantSize))
		{
			if (neighbor.Data.Adjacency.BonusType == AdjacencyBonus.Type.AdditionalBiome &&
				(neighbor.Data.Adjacency.BonusBiome == Biome.Any ||
				plant.Data.GrowingBiomes.Contains(neighbor.Data.Adjacency.BonusBiome)))
			{
				return true;
			}
		}

		return false;
	}

	public bool TryPlant(Plant plant, int firstPlot, PlantData seeds = null)
	{
		ArgumentNullException.ThrowIfNull(plant);
		if (plant.GetParent() is not null ||
			placements.Exists(placement => placement.Plant == plant) ||
			!CanPlant(plant, firstPlot))
		{
			return false;
		}

		int plotCount = plant.Data.PlantSize;
		AddChild(plant);
		plant.Position = GetPlantPosition(firstPlot, plotCount);
		placements.Add(new PlantPlacement(plant, seeds, firstPlot, plotCount));

		plant.CareRequested += _ => NeedsCareSound?.Play();
		plant.WrongCareGiven += _ => MistakeSound?.Play();
		plant.StageChanged += OnPlantStageChanged;
		PlantedSound?.Play();

		return true;
	}

	public bool RemovePlant(Plant plant)
	{
		ArgumentNullException.ThrowIfNull(plant);
		int placementIndex = placements.FindIndex(placement => placement.Plant == plant);
		if (placementIndex < 0)
		{
			return false;
		}

		placements.RemoveAt(placementIndex);
		return true;
	}

	public Vector3 GetPlantPosition(int firstPlot, int plotCount)
	{
		ValidatePlacement(firstPlot, plotCount);
		Vector3 center = Vector3.Zero;
		for (int index = firstPlot; index < firstPlot + plotCount; index++)
		{
			center += plots[index].GlobalPosition;
		}

		return ToLocal(center / plotCount);
	}

	public IReadOnlyList<Plant> GetAdjacentPlants(int firstPlot, int plotCount)
	{
		ValidatePlacement(firstPlot, plotCount);
		List<Plant> adjacentPlants = new List<Plant>(2);
		int leftIndex = firstPlot - 1;
		int rightIndex = firstPlot + plotCount;

		if (leftIndex >= 0 && SameBed(leftIndex, firstPlot) && GetPlantAtPlot(leftIndex) is { } leftPlant)
		{
			adjacentPlants.Add(leftPlant);
		}

		if (rightIndex < PLOT_COUNT && SameBed(rightIndex, rightIndex - 1) && GetPlantAtPlot(rightIndex) is { } rightPlant)
		{
			adjacentPlants.Add(rightPlant);
		}

		return adjacentPlants;
	}

	public float GetAdjacentPlantTimeBonuses(Plant plant)
	{
		PlantPlacement placement = FindPlacement(plant);
		float multiplier = 1f;
		foreach (Plant neighbor in GetAdjacentPlants(placement.FirstPlot, placement.PlotCount))
		{
			AdjacencyBonus bonus = neighbor.Data.Adjacency;
			if ((bonus.BonusType == AdjacencyBonus.Type.GrowthTime && plant.IsGrowing) ||
				(bonus.BonusType == AdjacencyBonus.Type.DecayTime && !plant.IsGrowing))
			{
				multiplier *= ValidateMultiplier(bonus);
			}
		}

		return multiplier;
	}

	public float GetAdjacentPlantSaleBonuses(Plant plant)
	{
		PlantPlacement placement = FindPlacement(plant);
		float multiplier = 1f;
		foreach (Plant neighbor in GetAdjacentPlants(placement.FirstPlot, placement.PlotCount))
		{
			if (neighbor.Data.Adjacency.BonusType == AdjacencyBonus.Type.SaleValue)
			{
				multiplier *= ValidateMultiplier(neighbor.Data.Adjacency);
			}
		}

		return multiplier;
	}

	private PlantPlacement FindPlacement(Plant plant)
	{
		ArgumentNullException.ThrowIfNull(plant);
		return placements.Find(placement => placement.Plant == plant)
			?? throw new ArgumentException("The plant is not placed in this planter box.", nameof(plant));
	}

	private PlantPlacement FindPlacementAtPlot(int plotIndex)
	{
		return placements.Find(placement =>
			plotIndex >= placement.FirstPlot &&
			plotIndex < placement.FirstPlot + placement.PlotCount);
	}

	private void OnPlantStageChanged(Plant plant)
	{
		if (plant.CurrentStage == Plant.Stage.Harvestable)
		{
			ReadySound?.Play();
		}
	}

	private bool SameBed(int left, int right)
	{
		return plots[left].GetParent() == plots[right].GetParent();
	}

	private void OnPlotClicked(int plotIndex, MouseButton button)
	{
		EmitSignal(SignalName.PlotClicked, this, plotIndex, (int)button);
	}

	public void UpdateLookAwaySpeed(float newVal)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(newVal, 1);
	}

	private static float ValidateMultiplier(AdjacencyBonus bonus)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(bonus.Multiplier, 0);
		return bonus.Multiplier;
	}

	private static void ValidatePlotIndex(int plotIndex)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(plotIndex);
		ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(plotIndex, PLOT_COUNT);
	}

	private static void ValidatePlacement(int firstPlot, int plotCount)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(firstPlot);
		ArgumentOutOfRangeException.ThrowIfLessThan(plotCount, 1);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(firstPlot + plotCount, PLOT_COUNT);
	}
}
