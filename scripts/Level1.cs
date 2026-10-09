using Godot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public partial class Level1 : Node2D
{
	private const int BOX_COUNT = 4;
	private const string BOX_VIEW_SCENE_PATH = "res://scenes/plant_box.tscn";
	private const string INVENTORY_SCENE_PATH = "res://scenes/Canvases/inventory.tscn";
	private const string SHOP_SCENE_PATH = "res://scenes/Canvases/shop.tscn";
	private const string PLANT_SELECTION_MENU_SCENE_PATH = "res://scenes/Canvases/plant_selection_menu.tscn";
	private const int BOX_DISTANCE = 1200; // in pixels
	private const float STARTING_COINS = 10;

	// Member variables

	// Private
	private readonly PlantBox[] boxViews = new PlantBox[BOX_COUNT];
	private readonly ToolItem[] handItems = new ToolItem[2];
	private readonly HashSet<ToolItem> connectedToolItems = new();
	private readonly Dictionary<string, int> seedStock = new();
	private PlantSelectionMenu plantSelectionMenu;
	private Inventory inventoryLayer;
	private Shop shopLayer;
	private Camera2D camera;
	private int currentPlantBoxIdx;
	private float coins;
	private bool showingInventory;
	private bool showingShop;

	// Properties
	public PlantBox CurrentPlantBox => boxViews[currentPlantBoxIdx];
	public ToolItem LeftHandItem => GetHandItem(MouseButton.Left);
	public ToolItem RightHandItem => GetHandItem(MouseButton.Right);

	// Events
	public event Action<PlantBox> CurrentPlantBoxChanged;

	public override void _EnterTree()
	{
		GetTree().NodeAdded += OnNodeAdded;
	}

	public override void _ExitTree()
	{
		GetTree().NodeAdded -= OnNodeAdded;
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		coins = STARTING_COINS;
		camera = GetNode<Camera2D>("Camera");
		foreach (SeedOffer offer in SeedCatalog.Offers)
		{
			if (offer.Id == "Tulip")
			{
				seedStock.Add(offer.Id, 5);
			}
			else
			{
				seedStock.Add(offer.Id, 0);
			}
		}
		currentPlantBoxIdx = 0;
		showingInventory = false;
		showingShop = false;

		LoadPlantBoxes();
		UpdatePlayerOrientation();
		LoadInventory();
		LoadShop();
		LoadPlantSelectionMenu();

		CurrentPlantBoxChanged?.Invoke(CurrentPlantBox);
	}

	private void LoadPlantBoxes()
	{
		PackedScene boxViewScene = GD.Load<PackedScene>(BOX_VIEW_SCENE_PATH);
		if (boxViewScene is null)
		{
			throw new InvalidOperationException($"Could not load box view scene at '{BOX_VIEW_SCENE_PATH}'.");
		}

		for (int i = 0; i < boxViews.Length; i++)
		{
			PlantBox boxViewInstance = boxViewScene.Instantiate<PlantBox>();
			boxViewInstance.Initialize(newBoxIdx: i, season: Season.Any);
			boxViewInstance.Position = new Vector2(i * BOX_DISTANCE, 0);
			boxViews[i] = boxViewInstance;
			boxViewInstance.PlantClicked += OnPlantClicked;
			boxViewInstance.EmptyPlotClicked += OnEmptyPlotClicked;
			AddChild(boxViewInstance);
		}
	}

	private void LoadInventory()
	{
		PackedScene inventoryScene = GD.Load<PackedScene>(INVENTORY_SCENE_PATH);
		if (inventoryScene is null)
		{
			throw new InvalidOperationException($"Could not load inventory scene at '{INVENTORY_SCENE_PATH}'.");
		}

		inventoryLayer = inventoryScene.Instantiate<Inventory>();
		inventoryLayer.CloseRequested += CloseInventory;
		AddChild(inventoryLayer);
		inventoryLayer.Hide();
	}

	private void LoadShop()
	{
		PackedScene shopScene = GD.Load<PackedScene>(SHOP_SCENE_PATH);
		if (shopScene is null)
		{
			throw new InvalidOperationException($"Could not load shop scene at '{SHOP_SCENE_PATH}'.");
		}

		shopLayer = shopScene.Instantiate<Shop>();
		shopLayer.CloseRequested += CloseShop;
		shopLayer.BuySeedRequested += BuySeed;
		shopLayer.SellPlantRequested += SellPlant;
		AddChild(shopLayer);
		shopLayer.Hide();
	}

	private void LoadPlantSelectionMenu()
	{
		PackedScene plantSelectionMenuScene = GD.Load<PackedScene>(PLANT_SELECTION_MENU_SCENE_PATH);
		if (plantSelectionMenuScene is null)
		{
			throw new InvalidOperationException(
				$"Could not load plant selection menu scene at '{PLANT_SELECTION_MENU_SCENE_PATH}'.");
		}

		plantSelectionMenu = plantSelectionMenuScene.Instantiate<PlantSelectionMenu>();
		AddChild(plantSelectionMenu);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (plantSelectionMenu.IsOpen)
		{
			return;
		}

		if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
		{
			return;
		}

		if (showingInventory && keyEvent.Keycode != Key.Q)
		{
			return;
		}
		if (showingShop && keyEvent.Keycode != Key.E)
		{
			return;
		}

		switch (keyEvent.Keycode)
		{
			case Key.A:
				SelectPlantBox(currentPlantBoxIdx - 1);
				break;
			case Key.D:
				SelectPlantBox(currentPlantBoxIdx + 1);
				break;
			case Key.Q:
				ToggleInventory();
				break;
			case Key.E:
				ToggleShop();
				break;
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
			seedPacket.PlantSelectionRequested += OpenPlantSelectionMenu;
		}
	}

	private void OnPlantClicked(Plant plant, MouseButton button)
	{
		if (plantSelectionMenu.IsOpen)
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
					if (RemovePlantFromBox(plant))
					{
						float saleMultiplier = boxViews[plant.owningPlantBoxIdx].GetAdjacentPlantSaleBonuses(plant);
						InventoryItem harvestedItem = new(plant, saleMultiplier);
						inventoryLayer.AddItem(harvestedItem);
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
		if (plantSelectionMenu.IsOpen)
		{
			return;
		}

		ToolItem item = GetHandItem(button);
		if (item is SeedPacket)
		{
			item.UseOnEmptyPlot(plantBox, plotIdx);
		}
	}

	private void ToggleInventory()
	{
		if (showingInventory)
		{
			CloseInventory();
		}
		else
		{
			OpenInventory();
		}
	}

	private void OpenInventory()
	{
		if (showingShop)
		{
			CloseShop();
		}

		inventoryLayer.Show();
		showingInventory = true;
	}

	private void CloseInventory()
	{
		inventoryLayer.Hide();
		showingInventory = false;
	}

	private void ToggleShop()
	{
		if (showingShop)
		{
			CloseShop();
		}
		else
		{
			OpenShop();
		}
	}

	private void OpenShop()
	{
		if (showingInventory)
		{
			CloseInventory();
		}

		RefreshShop();
		shopLayer.Show();
		showingShop = true;
	}

	private void CloseShop()
	{
		shopLayer.Hide();
		showingShop = false;
	}

	private void RefreshShop()
	{
		shopLayer.SetContents(coins, seedStock, inventoryLayer.Items);
	}

	private void BuySeed(string seedId)
	{
		SeedOffer offer = null;
		foreach (SeedOffer availableOffer in SeedCatalog.Offers)
		{
			if (availableOffer.Id == seedId)
			{
				offer = availableOffer;
				break;
			}
		}

		if (offer is null || coins < offer.Price)
		{
			return;
		}

		coins -= offer.Price;
		seedStock[offer.Id]++;
		RefreshShop();
	}

	private void SellPlant(InventoryItem item)
	{
		if (!inventoryLayer.RemoveItem(item))
		{
			return;
		}

		coins += item.currentSellingPrice;
		RefreshShop();
	}

	private void OpenPlantSelectionMenu(PlantBox plantBox, int plotIdx)
	{
		if (showingInventory || showingShop)
		{
			return;
		}

		plantSelectionMenu.Open(plantBox, plotIdx, seedStock, TryConsumeSeed);
	}

	private bool TryConsumeSeed(string seedId)
	{
		if (!seedStock.TryGetValue(seedId, out int count) || count <= 0)
		{
			return false;
		}

		seedStock[seedId] = count - 1;
		return true;
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
