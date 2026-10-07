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
};

public partial class PlantBox : Area2D
{
	public const int PLOT_COUNT = 6;

	public Season season;

	private sealed record PlantPlacement(Plant Plant, int FirstPlot, int PlotCount);
	private readonly List<PlantPlacement> placements = new();
	private readonly Dictionary<ulong, int> plotIndicesByShapeID = new();
	
	private int boxIdx = -1;
	private int playerOrientationIdx = -1;

	public event Action<Plant, MouseButton> PlantClicked;
	public event Action<PlantBox, int, MouseButton> EmptyPlotClicked;

	public void Initialize(int newBoxIdx, Season season)
	{
		InitializeBoxIdx(newBoxIdx);
		InitializeSeason(season);
	}

	private void InitializeBoxIdx(int newBoxIdx)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(newBoxIdx);
		if (boxIdx != -1)
		{
			throw new InvalidOperationException("PlantBox index can only be initialized once.");
		}

		boxIdx = newBoxIdx;
	}

	private void InitializeSeason(Season season)
	{
		this.season = season;
	}

	public Plant GetPlantAtPlot(int plotIdx)
	{
		ValidatePlotIdx(plotIdx);

		foreach (PlantPlacement placement in placements)
		{
			if (plotIdx >= placement.FirstPlot &&
				plotIdx < placement.FirstPlot + placement.PlotCount)
			{
				return placement.Plant;
			}
		}

		return null;
	}

	public bool TryPlant(Plant plant, int requestedPlotIdx)
	{
		ArgumentNullException.ThrowIfNull(plant);
		ArgumentOutOfRangeException.ThrowIfLessThan(plant.plantSize, 1);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(plant.plantSize, PLOT_COUNT);

		if (requestedPlotIdx < 0 || requestedPlotIdx + plant.plantSize > PLOT_COUNT)
		{
			return false;
		}

		foreach (PlantPlacement placement in placements)
		{
			bool overlaps = requestedPlotIdx < placement.FirstPlot + placement.PlotCount &&
				placement.FirstPlot < requestedPlotIdx + plant.plantSize;
			if (overlaps || placement.Plant == plant)
			{
				return false;
			}
		}

		if (boxIdx < 0)
		{
			throw new InvalidOperationException("PlantBox must be assigned an index before planting.");
		}

		plant.SetOwningPlantBoxIdx(boxIdx);
		plant.SetPlayerOrientationIdx(playerOrientationIdx);
		plant.clicked += OnPlantClicked;
		placements.Add(new PlantPlacement(plant, requestedPlotIdx, plant.plantSize));
		return true;
	}

	private void OnPlantClicked(Plant plant, MouseButton button)
	{
		PlantClicked?.Invoke(plant, button);
	}

	public void SetPlayerOrientationIdx(int newPlayerOrientationIdx)
	{
		playerOrientationIdx = newPlayerOrientationIdx;

		foreach (PlantPlacement placement in placements)
		{
			placement.Plant.SetPlayerOrientationIdx(newPlayerOrientationIdx);
		}
	}

	public bool RemovePlant(Plant plant)
	{
		ArgumentNullException.ThrowIfNull(plant);

		int placementIdx = placements.FindIndex(placement => placement.Plant == plant);
		if (placementIdx < 0)
		{
			return false;
		}

		placements[placementIdx].Plant.clicked -= OnPlantClicked;
		placements.RemoveAt(placementIdx);
		return true;
	}

	private static void ValidatePlotIdx(int plotIdx)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(plotIdx);
		ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(plotIdx, PLOT_COUNT);
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		CollisionLayer = 1;
		CollisionMask = 0;
		InputPickable = true;
		InputEvent += OnPlotInputEvent;

		for (int plotIdx = 0; plotIdx < PLOT_COUNT; plotIdx++)
		{
			string plotAreaName = "Plot" + (plotIdx+1) + "Area";
			CollisionShape2D plotShape = GetNodeOrNull<CollisionShape2D>(plotAreaName);
			if (plotShape is null)
			{
				throw new InvalidOperationException($"PlantBox scene is missing the collision shape '{plotAreaName}'.");
			}

			plotIndicesByShapeID.Add(plotShape.GetInstanceId(), plotIdx);
		}
	}

	private void OnPlotInputEvent(
		Node viewport,
		InputEvent inputEvent,
		long shapeIdx
	)
	{
		if (inputEvent is not InputEventMouseButton mouseEvent ||
			!mouseEvent.Pressed ||
			mouseEvent.ButtonIndex is not (MouseButton.Left or MouseButton.Right))
		{
			return;
		}

		uint shapeOwnerID = ShapeFindOwner((int)shapeIdx);
		if (ShapeOwnerGetOwner(shapeOwnerID) is not CollisionShape2D plotShape ||
			!plotIndicesByShapeID.TryGetValue(plotShape.GetInstanceId(), out int plotIdx))
		{
			return;
		}

		if (GetPlantAtPlot(plotIdx) is null)
		{
			EmptyPlotClicked?.Invoke(this, plotIdx, mouseEvent.ButtonIndex);
			GetViewport().SetInputAsHandled();
		}
	}

	public Vector2 GetPlantPosition(int firstPlot, int plotCount)
	{
		ValidatePlotIdx(firstPlot);
		ArgumentOutOfRangeException.ThrowIfLessThan(plotCount, 1);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(firstPlot + plotCount, PLOT_COUNT);

		int centerPlotIdx = firstPlot + (plotCount - 1) / 2;
		string plotAreaName = "Plot" + (centerPlotIdx + 1) + "Area";
		CollisionShape2D plotShape = GetNodeOrNull<CollisionShape2D>(plotAreaName);
		if (plotShape is null)
		{
			throw new InvalidOperationException($"PlantBox scene is missing the collision shape '{plotAreaName}'.");
		}

		return ToLocal(plotShape.GlobalPosition);
	}

	public List<Plant> GetAdjacentPlants(int plotIdx, int plotCount)
	{
		List<Plant> adjPlants = new List<Plant>();

		foreach (PlantPlacement placement in placements)
		{
			if (placement.FirstPlot + placement.PlotCount == plotIdx ||
				placement.FirstPlot == plotIdx + plotCount)
			{
				adjPlants.Add(placement.Plant);
			}
		}

		if (adjPlants.Count == 0)
		{
			adjPlants = null;
		}
		return adjPlants;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
