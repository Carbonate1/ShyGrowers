using Godot;
using System;
using System.Collections.Generic;

public enum Season
{
	Winter,
	Spring,
	Summer,
	Fall,
	Any,
	None
}

[GlobalClass]
public partial class PlanterBox : Node3D
{
	private sealed record PlantPlacement(Plant Plant, int FirstPlot, int PlotCount);

	public const int PLOT_COUNT = 6;

	[Export] public float LookAwaySpeed { get; set; } = 3f;
	[Export] public VisibleOnScreenNotifier3D Watcher { get; set; }
	[Export] public Season GrowingSeason { get; set; } = Season.Any;

	public bool IsWatched => Watcher is null || Watcher.IsOnScreen();
	public float TimeScale => IsWatched ? 1f : LookAwaySpeed;

	private readonly List<Plot> plots = new();
	private readonly List<PlantPlacement> placements = new();

	public override void _Ready()
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(LookAwaySpeed, 0);
		Node plotsNode = GetNodeOrNull<Node>("Plots")
			?? throw new InvalidOperationException("PlanterBox scene is missing its 'Plots' node.");

		foreach (Node child in plotsNode.GetChildren())
		{
			if (child is Plot plot)
			{
				plots.Add(plot);
			}
		}

		plots.Sort((left, right) => left.Position.X.CompareTo(right.Position.X));
		if (plots.Count != PLOT_COUNT)
		{
			throw new InvalidOperationException(
				$"PlanterBox must contain exactly {PLOT_COUNT} Plot nodes; found {plots.Count}.");
		}

		for (int i = 0; i < plots.Count; i++)
		{
			plots[i].Initialize(this, i);
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
		return plots[plotIndex].Plant;
	}

	public bool CanPlant(Plant plant, int firstPlot)
	{
		ArgumentNullException.ThrowIfNull(plant);
		ArgumentOutOfRangeException.ThrowIfLessThan(plant.PlantSize, 1);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(plant.PlantSize, PLOT_COUNT);
		if (firstPlot < 0 || firstPlot + plant.PlantSize > PLOT_COUNT)
		{
			return false;
		}

		for (int index = firstPlot; index < firstPlot + plant.PlantSize; index++)
		{
			if (plots[index].Plant is not null)
			{
				return false;
			}
		}

		if (GrowingSeason is Season.Any or Season.None ||
			plant.GrowingSeason is Season.Any or Season.None ||
			GrowingSeason == plant.GrowingSeason)
		{
			return true;
		}

		foreach (Plant neighbor in GetAdjacentPlants(firstPlot, plant.PlantSize))
		{
			if (neighbor.Adjacency.BonusType == AdjacencyBonus.Type.AdditionalSeason &&
				neighbor.Adjacency.BonusSeason == plant.GrowingSeason)
			{
				return true;
			}
		}

		return false;
	}

	public bool TryPlant(Plant plant, int firstPlot, ItemData seeds = null)
	{
		ArgumentNullException.ThrowIfNull(plant);
		if (plant.GetParent() is not null ||
			placements.Exists(placement => placement.Plant == plant) ||
			!CanPlant(plant, firstPlot))
		{
			return false;
		}

		int plotCount = plant.PlantSize;
		AddChild(plant);
		plant.Position = GetPlantPosition(firstPlot, plotCount);
		placements.Add(new PlantPlacement(plant, firstPlot, plotCount));

		for (int index = firstPlot; index < firstPlot + plotCount; index++)
		{
			plots[index].SetPlant(plant, seeds);
		}

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

		PlantPlacement placement = placements[placementIndex];
		placements.RemoveAt(placementIndex);
		for (int index = placement.FirstPlot; index < placement.FirstPlot + placement.PlotCount; index++)
		{
			plots[index].SetPlant(null, null);
		}

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
		var adjacentPlants = new List<Plant>(2);
		int leftIndex = firstPlot - 1;
		int rightIndex = firstPlot + plotCount;

		if (leftIndex >= 0 && plots[leftIndex].Plant is { } leftPlant)
		{
			adjacentPlants.Add(leftPlant);
		}

		if (rightIndex < PLOT_COUNT && plots[rightIndex].Plant is { } rightPlant)
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
			AdjacencyBonus bonus = neighbor.Adjacency;
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
			if (neighbor.Adjacency.BonusType == AdjacencyBonus.Type.SaleValue)
			{
				multiplier *= ValidateMultiplier(neighbor.Adjacency);
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
