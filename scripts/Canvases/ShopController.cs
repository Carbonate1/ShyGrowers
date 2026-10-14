using Godot;
using System;
using System.Collections.Generic;

public partial class ShopController : Node
{
	private const int STARTING_COINS = 3;
	private const double MARKET_UPDATE_INTERVAL = 3;

	[Export] public Shop Shop { get; set; }
	[Export] public Inventory Inventory { get; set; }
	[Export] public SeedStock Seeds { get; set; }
	[Export] public UnlockProgress Unlocks { get; set; }

	public float CurrentCoins => coins;

	private PauseMenu pauseMenu;
	private readonly Dictionary<string, PlantData> plantDataByName = new();
	private readonly Dictionary<string, SeedOffer> seedOffers = new();
	private readonly Dictionary<string, UnlockOffer> unlockOffers = new();
	private readonly Dictionary<string, int> seedCounts = new();
	private readonly Dictionary<string, float> marketValues = new();
	private readonly Random random = new();
	private double marketTimeElapsed;
	private float coins;

	public override void _Ready()
	{
		coins = STARTING_COINS;

		foreach (SeedOffer offer in SeedCatalog.Offers)
		{
			PlantData plantData = GD.Load<PlantData>(offer.PlantDataPath)
				?? throw new InvalidOperationException($"Could not load plant data for offer '{offer.Name}'.");
			plantDataByName.Add(offer.Name, plantData);
			seedOffers.Add(offer.Name, offer);
			marketValues.Add(offer.Name, 1f);
		}
		foreach (UnlockOffer offer in UnlockCatalog.Offers)
		{
			unlockOffers.Add(offer.Name, offer);
		}

		Shop.Hide();
		Inventory.Hide();
		pauseMenu = new PauseMenu();
		AddChild(pauseMenu);
		Shop.CloseRequested += CloseShop;
		Shop.BuySeedRequested += BuySeed;
		Shop.SellPlantRequested += SellPlant;
		Shop.UnlockRequested += Unlock;
		Inventory.CloseRequested += CloseInventory;
		Inventory.ItemsChanged += RefreshShop;
		Seeds.Changed += RefreshShop;
		RefreshShop();
	}

	public override void _ExitTree()
	{
		if (Shop is not null)
		{
			Shop.CloseRequested -= CloseShop;
			Shop.BuySeedRequested -= BuySeed;
			Shop.SellPlantRequested -= SellPlant;
			Shop.UnlockRequested -= Unlock;
		}

		if (Inventory is not null)
		{
			Inventory.CloseRequested -= CloseInventory;
			Inventory.ItemsChanged -= RefreshShop;
		}

		if (Seeds is not null)
		{
			Seeds.Changed -= RefreshShop;
		}
	}

	public override void _Process(double delta)
	{
		marketTimeElapsed += delta;
		if (marketTimeElapsed < MARKET_UPDATE_INTERVAL)
		{
			return;
		}

		marketTimeElapsed = 0;
		UpdateMarket();
		if (Shop.Visible)
		{
			RefreshShop();
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false } keyEvent)
		{
			return;
		}

		switch (keyEvent.Keycode)
		{
			case Key.I:
				ToggleInventory();
				GetViewport().SetInputAsHandled();
				break;
			case Key.S:
				ToggleShop();
				GetViewport().SetInputAsHandled();
				break;
			case Key.Escape:
				if (Shop.Visible)
				{
					CloseShop();
				}
				else if (Inventory.Visible)
				{
					CloseInventory();
				}
				else
				{
					GetTree().Paused = true;
					pauseMenu.Show();
				}
				GetViewport().SetInputAsHandled();
				break;
		}
	}

	private void BuySeed(string offerName)
	{
		if (!Unlocks.IsPlantUnlocked(offerName))
		{
			return;
		}

		if (!plantDataByName.TryGetValue(offerName, out PlantData plantData))
		{
			GD.PushWarning($"Unknown seed offer '{offerName}'.");
			return;
		}

		if (!seedOffers.TryGetValue(offerName, out SeedOffer offer))
		{
			return;
		}

		float buyPrice = offer.Price * marketValues[offerName];
		if (coins < buyPrice)
		{
			return;
		}

		coins -= buyPrice;
		Seeds.Add(plantData, 1);
		RefreshShop();
	}

	private void Unlock(string offerName)
	{
		if (!unlockOffers.TryGetValue(offerName, out UnlockOffer offer))
		{
			GD.PushWarning($"Unknown unlock offer '{offerName}'.");
			return;
		}

		if (coins < offer.Price)
		{
			return;
		}

		bool wasUnlocked = offer.Type == UnlockType.Plant
			? Unlocks.UnlockPlant(offer.Name)
			: Unlocks.UnlockPlanter(offer.PlanterBiome);
		if (!wasUnlocked)
		{
			return;
		}

		if (offer.AssociatedPlantToUnlock is not null)
		{
			Unlocks.UnlockPlant(offer.AssociatedPlantToUnlock);
		}

		coins -= offer.Price;
		if (!specialOrdersStarted)
		{
			StartSpecialOrders();
		}
		RefreshShop();
	}

	private void SellPlant(InventoryItem item)
	{
		ArgumentNullException.ThrowIfNull(item);
		float salePrice = item.currentSellingPrice;
		if (!Inventory.RemoveItem(item))
		{
			return;
		}

		coins += salePrice * marketValues[item.name];
		RefreshShop();
	}

	private void ToggleInventory()
	{
		if (Inventory.Visible)
		{
			CloseInventory();
		}
		else
		{
			Shop.Hide();
			Inventory.Show();
		}
	}

	private void ToggleShop()
	{
		if (Shop.Visible)
		{
			CloseShop();
		}
		else
		{
			Inventory.Hide();
			Shop.Show();
			RefreshShop();
		}
	}

	private void CloseShop()
	{
		Shop.Hide();
	}

	private void CloseInventory()
	{
		Inventory.Hide();
	}

	private void RefreshShop()
	{
		seedCounts.Clear();
		foreach (KeyValuePair<string, PlantData> entry in plantDataByName)
		{
			seedCounts.Add(entry.Key, Seeds.Count(entry.Value));
		}

		Shop.SetContents(
			coins,
			seedCounts,
			Inventory.Items,
			marketValues,
			Unlocks);
	}

	private void UpdateMarket()
	{
		foreach (string offerName in new List<string>(marketValues.Keys))
		{
			float change = random.Next(0, 11) / 100f;
			float direction = random.Next(0, 2) == 0 ? -1f : 1f;
			marketValues[offerName] = Mathf.Clamp(marketValues[offerName] + change * direction, 0.5f, 2f);
		}
	}
}
