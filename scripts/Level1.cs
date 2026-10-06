using Godot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public partial class Level1 : Node2D
{
	private const int BOX_COUNT = 4;
	private const string BOX_VIEW_SCENE_PATH = "res://scenes/plant_box.tscn";
	private const int BOX_DISTANCE = 1200; // in pixels
	private static readonly PlantOption[] PlantOptions =
	{
		new("Tulip", "res://scenes/Plants/tulip.tscn")
	};

	private readonly PlantBox[] boxViews = new PlantBox[BOX_COUNT];
	private readonly ToolItem[] handItems = new ToolItem[2];
	private readonly HashSet<ToolItem> connectedToolItems = new();
	private readonly Dictionary<string, int> inventory = new();
	private int currentPlantBoxIdx;
	private CanvasLayer plantMenuLayer;
	private PlantBox pendingPlantBox;
	private int pendingPlotIdx;

	public PlantBox CurrentPlantBox => boxViews[currentPlantBoxIdx];
	public ToolItem LeftHandItem => GetHandItem(MouseButton.Left);
	public ToolItem RightHandItem => GetHandItem(MouseButton.Right);
	public IReadOnlyDictionary<string, int> Inventory =>
		new ReadOnlyDictionary<string, int>(inventory);
	public event Action<PlantBox> CurrentPlantBoxChanged;

	private Camera2D camera;

	public override void _EnterTree()
	{
		GetTree().NodeAdded += OnNodeAdded;
	}

	public override void _ExitTree()
	{
		GetTree().NodeAdded -= OnNodeAdded;
	}

	private sealed record PlantOption(string DisplayName, string ScenePath);
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		camera = GetNode<Camera2D>("Camera");
		PackedScene boxViewScene = GD.Load<PackedScene>(BOX_VIEW_SCENE_PATH);
		if (boxViewScene is null)
		{
			throw new InvalidOperationException($"Could not load box view scene at '{BOX_VIEW_SCENE_PATH}'.");
		}

		for (int i = 0; i < boxViews.Length; i++)
		{
			PlantBox boxViewInstance = boxViewScene.Instantiate<PlantBox>();
			boxViewInstance.Initialize(newBoxIdx: i, season: PlantBox.Season.Any);
			boxViewInstance.Position = new Vector2(i * BOX_DISTANCE, 0);
			boxViews[i] = boxViewInstance;
			boxViewInstance.PlantClicked += OnPlantClicked;
			boxViewInstance.EmptyPlotClicked += OnEmptyPlotClicked;
			AddChild(boxViewInstance);
		}

		currentPlantBoxIdx = 0;
		UpdatePlayerOrientation();
		CurrentPlantBoxChanged?.Invoke(CurrentPlantBox);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (plantMenuLayer is not null)
		{
			return;
		}

		if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
		{
			return;
		}

		if (keyEvent.Keycode == Key.A)
		{
			SelectPlantBox(currentPlantBoxIdx - 1);
		}
		else if (keyEvent.Keycode == Key.D)
		{
			SelectPlantBox(currentPlantBoxIdx + 1);
		}
	}

	private void SelectPlantBox(int idx)
	{
		currentPlantBoxIdx = (idx + boxViews.Length) % boxViews.Length;
		UpdatePlayerOrientation();
		CurrentPlantBoxChanged?.Invoke(CurrentPlantBox);

		camera.Position = new Vector2(currentPlantBoxIdx * BOX_DISTANCE, 0);

	}

	private void UpdatePlayerOrientation()
	{
		foreach (PlantBox boxView in boxViews)
		{
			boxView.SetPlayerOrientationIdx(currentPlantBoxIdx);
		}
	}

	private void OnNodeAdded(Node node)
	{
		if (node is ToolItem toolItem)
		{
			ConnectToolItem(toolItem);
		}
	}

	private void ConnectToolItem(ToolItem toolItem)
	{
		if (!connectedToolItems.Add(toolItem))
		{
			return;
		}

		toolItem.Clicked += OnToolItemClicked;
		if (toolItem is SeedPacket seedPacket)
		{
			seedPacket.PlantSelectionRequested += ShowPlantMenu;
		}
	}

	private void OnPlantClicked(Plant plant, MouseButton button)
	{
		if (plantMenuLayer is not null)
		{
			return;
		}

		ToolItem item = GetHandItem(button);
		if (item is not null)
		{
			switch (item.UseOnPlant(plant))
			{
				case ToolUseResult.DiscardPlant:
					RemovePlantFromBox(plant);
					break;
				case ToolUseResult.HarvestPlant:
					string plantTypeName = plant.PlantTypeName;
					if (RemovePlantFromBox(plant))
					{
						inventory.TryGetValue(plantTypeName, out int count);
						inventory[plantTypeName] = count + 1;
					}
					break;
			}
		}
	}

	private bool RemovePlantFromBox(Plant plant)
	{
		foreach (PlantBox boxView in boxViews)
		{
			if (!boxView.RemovePlant(plant))
			{
				continue;
			}

			plant.QueueFree();
			return true;
		}

		return false;
	}

	private void OnEmptyPlotClicked(
		PlantBox plantBox, 
		int plotIdx, 
		MouseButton button)
	{
		if (plantMenuLayer is not null)
		{
			return;
		}

		ToolItem item = GetHandItem(button);
		if (item is SeedPacket)
		{
			item.UseOnEmptyPlot(plantBox, plotIdx);
		}
	}

	private void ShowPlantMenu(PlantBox plantBox, int plotIndex)
	{
		if (plantMenuLayer is not null)
		{
			return;
		}

		pendingPlantBox = plantBox;
		pendingPlotIdx = plotIdx;
		plantMenuLayer = new CanvasLayer { Layer = 10 };
		var backdrop = new ColorRect
		{
			Color = new Color(0, 0, 0, 0.6f),
			MouseFilter = Control.MouseFilterEnum.Stop,
			AnchorRight = 1,
			AnchorBottom = 1
		};
		backdrop.GuiInput += OnPlantMenuBackdropInput;
		plantMenuLayer.AddChild(backdrop);

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
			options.AddChild(button);
		}

		var cancelButton = new Button { Text = "Cancel" };
		cancelButton.Pressed += ClosePlantMenu;
		options.AddChild(cancelButton);
		menu.AddChild(options);
		backdrop.AddChild(menu);
		AddChild(plantMenuLayer);
	}

	private void OnPlantMenuBackdropInput(InputEvent @event)
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
		ClosePlantMenu();
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

	private void ClosePlantMenu()
	{
		plantMenuLayer?.QueueFree();
		plantMenuLayer = null;
		pendingPlantBox = null;
	}

	private void OnToolItemClicked(ToolItem clickedItem, MouseButton button)
	{
		ToolItem currentHandItem = GetHandItem(button);
		if (clickedItem == currentHandItem || IsHeld(clickedItem))
		{
			return;
		}

		if (currentHandItem is not null)
		{
			currentHandItem.GlobalPosition = clickedItem.GlobalPosition;
		}

		EquipHand(clickedItem, button);
	}

	private void EquipHand(ToolItem item, MouseButton button)
	{
		int handIdx = GetHandIdx(button);
		int otherHandIdx = 1 - handIdx;
		if (item is not null && item == handItems[otherHandIdx])
		{
			throw new InvalidOperationException("An item cannot be held in both hands.");
		}

		ToolItem previousItem = handItems[handIdx];
		if (previousItem == item)
		{
			return;
		}

		previousItem?.SetHeld(false);
		handItems[handIdx] = item;
		item?.SetHeld(true);
	}

	private ToolItem GetHandItem(MouseButton button)
	{
		return handItems[GetHandIdx(button)];
	}

	private static int GetHandIdx(MouseButton button)
	{
		switch (button)
		{
			case MouseButton.Left:
				return 0;
			case MouseButton.Right:
				return 1;
			default:
				throw new ArgumentOutOfRangeException(nameof(button));
		}
	}

	private bool IsHeld(ToolItem item)
	{
		return handItems[0] == item || handItems[1] == item;
	}
}
