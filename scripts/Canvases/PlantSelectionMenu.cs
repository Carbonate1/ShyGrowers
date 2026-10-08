using Godot;
using System;
using System.Collections.Generic;

public partial class PlantSelectionMenu : CanvasLayer
{
	private const string TULIP_SCENE_PATH = "res://scenes/Plants/tulip.tscn";
	private static readonly PlantOption[] PlantOptions =
	{
		new("Tulip", TULIP_SCENE_PATH)
	};

	private sealed record PlantOption(string DisplayName, string ScenePath);

	private PlantBox pendingPlantBox;
	private int pendingPlotIdx;
	private Control menuRoot;

	public bool IsOpen => menuRoot is not null;

	public override void _Ready()
	{
		Layer = 10;
		Hide();
	}

	public void Open(PlantBox plantBox, int plotIdx)
	{
		ArgumentNullException.ThrowIfNull(plantBox);
		if (IsOpen)
		{
			return;
		}

		pendingPlantBox = plantBox;
		pendingPlotIdx = plotIdx;

		var backdrop = new ColorRect
		{
			Color = new Color(0, 0, 0, 0.6f),
			MouseFilter = Control.MouseFilterEnum.Stop,
			AnchorRight = 1,
			AnchorBottom = 1
		};
		backdrop.GuiInput += OnBackdropInput;

		var menu = new PanelContainer
		{
			AnchorLeft = 0.5f,
			AnchorTop = 0.5f,
			AnchorRight = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -180,
			OffsetTop = -120,
			OffsetRight = 180,
			OffsetBottom = 120,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		var options = new VBoxContainer();
		options.AddChild(new Label
		{
			Text = "Choose a plant",
			HorizontalAlignment = HorizontalAlignment.Center
		});

		foreach (PlantOption option in PlantOptions)
		{
			var button = new Button { Text = option.DisplayName };
			button.Pressed += () => SelectPlant(option);
			Plant plant = LoadPlant(option);
			button.Disabled = !IsPlantableHere(plant, pendingPlantBox, pendingPlotIdx);
			plant.Free();
			options.AddChild(button);
		}

		var cancelButton = new Button { Text = "Cancel" };
		cancelButton.Pressed += Close;
		options.AddChild(cancelButton);
		menu.AddChild(options);
		backdrop.AddChild(menu);

		menuRoot = backdrop;
		AddChild(menuRoot);
		Show();
	}

	public void Close()
	{
		menuRoot?.QueueFree();
		menuRoot = null;
		pendingPlantBox = null;
		Hide();
	}

	private static bool IsPlantableHere(Plant option, PlantBox plantBox, int plotIdx)
	{
		if (plotIdx < 0 || plotIdx + option.plantSize > PlantBox.PLOT_COUNT)
		{
			return false;
		}

		for (int occupiedPlotIdx = plotIdx; occupiedPlotIdx < plotIdx + option.plantSize; occupiedPlotIdx++)
		{
			if (plantBox.GetPlantAtPlot(occupiedPlotIdx) is not null)
			{
				return false;
			}
		}

		if (plantBox.season == Season.Any ||
			option.growingSeason == plantBox.season)
		{
			return true;
		}

		List<Plant> adjPlants = plantBox.GetAdjacentPlants(plotIdx, option.plantSize);
		foreach (Plant plant in adjPlants)
		{
			if (plant.adjBonus.season == option.growingSeason)
			{
				return true;
			}
		}

		return true;
	}

	private void OnBackdropInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton { Pressed: true })
		{
			GetViewport().SetInputAsHandled();
		}
	}

	private void SelectPlant(PlantOption option)
	{
		Plant plant = LoadPlant(option);
		if (!pendingPlantBox.TryPlant(plant, pendingPlotIdx))
		{
			plant.Free();
			GD.PushWarning($"There is not enough room to plant {option.DisplayName} in this plot.");
			return;
		}

		pendingPlantBox.AddChild(plant);
		plant.Position = pendingPlantBox.GetPlantPosition(pendingPlotIdx, plant.plantSize);
		Close();
	}

	private static Plant LoadPlant(PlantOption option)
	{
		PackedScene plantScene = GD.Load<PackedScene>(option.ScenePath);
		if (plantScene is null)
		{
			throw new InvalidOperationException($"Could not load plant scene at '{option.ScenePath}'.");
		}

		return plantScene.Instantiate<Plant>();
	}
}
