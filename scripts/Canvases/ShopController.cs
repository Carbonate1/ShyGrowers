using Godot;
using System;
using System.Collections.Generic;

public partial class ShopController : Node
{
	private const double MARKET_UPDATE_INTERVAL = 3;

	[Export] public Shop Shop { get; set; }
	[Export] public Inventory Inventory { get; set; }
	[Export] public SeedStock Seeds { get; set; }
	[Export] public float StartingCoins { get; set; }

	private readonly Dictionary<string, ItemData> seedItems = new();
	private readonly Dictionary<string, SeedOffer> seedOffers = new();
	private readonly Dictionary<string, int> seedCounts = new();
	private readonly Dictionary<string, float> marketValues = new();
	private readonly Random random = new();
	private double marketTimeElapsed;
	private float coins;

	public override void _Ready()
	{
		ArgumentOutOfRangeException.ThrowIfNegative(StartingCoins);
		coins = StartingCoins;

		foreach (SeedOffer offer in SeedCatalog.Offers)
		{
			ItemData seed = GD.Load<ItemData>($"res://resources/items/{offer.Id.ToLowerInvariant()}_seeds.tres")
				?? throw new InvalidOperationException($"Could not load seed data for offer '{offer.Id}'.");
			seedItems.Add(offer.Id, seed);
			seedOffers.Add(offer.Id, offer);
			marketValues.Add(offer.Id, 1f);
		}

		Shop.Hide();
		Inventory.Hide();
		Shop.CloseRequested += () => Shop.Hide();
		Shop.BuySeedRequested += BuySeed;
		Shop.SellPlantRequested += SellPlant;
		Inventory.CloseRequested += () => Inventory.Hide();
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
			case Key.Escape when Shop.Visible || Inventory.Visible:
				if (Shop.Visible)
				{
					CloseShop();
				}
				else
				{
					CloseInventory();
				}
				GetViewport().SetInputAsHandled();
				break;
		}
	}

	private void BuySeed(string offerId)
	{
		if (!seedItems.TryGetValue(offerId, out ItemData seed))
		{
			GD.PushWarning($"Unknown seed offer '{offerId}'.");
			return;
		}

		if (!seedOffers.TryGetValue(offerId, out SeedOffer offer) || coins < offer.Price)
		{
			return;
		}

		coins -= offer.Price;
		Seeds.Add(seed, 1);
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

		coins += salePrice;
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
		foreach (KeyValuePair<string, ItemData> entry in seedItems)
		{
			seedCounts.Add(entry.Key, Seeds.Count(entry.Value));
		}

		Shop.SetContents(coins, seedCounts, Inventory.Items, marketValues);
	}

	private void UpdateMarket()
	{
		foreach (string offerId in new List<string>(marketValues.Keys))
		{
			float change = random.Next(0, 11) / 100f;
			float direction = random.Next(0, 2) == 0 ? -1f : 1f;
			marketValues[offerId] = Mathf.Clamp(marketValues[offerId] + change * direction, 0.5f, 2f);
		}
	}
}
