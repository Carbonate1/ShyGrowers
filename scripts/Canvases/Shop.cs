using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed record SeedOffer(
	string Name,
	string PlantDataPath,
	string ScenePath,
	int Price
);

public static class SeedCatalog
{
	public static IReadOnlyList<SeedOffer> Offers { get; } = Array.AsReadOnly(new[]
	{
		new SeedOffer(
			"Marigold",
			"res://resources/plants/marigold.tres",
			"res://scenes/Plants/marigold.tscn",
			1),
		new SeedOffer(
			"Tulip",
			"res://resources/plants/tulip.tres",
			"res://scenes/Plants/tulip.tscn",
			2),
		new SeedOffer(
			"Rose",
			"res://resources/plants/rose.tres",
			"res://scenes/Plants/rose.tscn",
			2),
		new SeedOffer(
			"Frostbloom",
			"res://resources/plants/frostbloom.tres",
			"res://scenes/Plants/frostbloom.tscn",
			3),
		new SeedOffer(
			"Ambi Rose",
			"res://resources/plants/ambi_rose.tres",
			"res://scenes/Plants/ambi_rose.tscn",
			4),
		new SeedOffer(
			"Azure Queen",
			"res://resources/plants/azure_queen.tres",
			"res://scenes/Plants/azure_queen.tscn",
			6),
		new SeedOffer(
			"Neighbouring Root",
			"res://resources/plants/neighbouring_root.tres",
			"res://scenes/Plants/neighbouring_root.tscn",
			4),
	});
};

public enum UnlockType
{
	Plant,
	Planter
}

public sealed record UnlockOffer(
	string Name,
	int Price,
	UnlockType Type,
	Biome PlanterBiome = Biome.None,
	string AssociatedPlantToUnlock = null
);

public static class UnlockCatalog
{
	public static IReadOnlyList<UnlockOffer> Offers { get; } = Array.AsReadOnly(new[]
	{
		new UnlockOffer("Spring Planter + Tulip", 8, UnlockType.Planter, Biome.Spring, "Tulip"),
		new UnlockOffer("Summer Planter + Rose", 10, UnlockType.Planter, Biome.Summer, "Rose"),
		new UnlockOffer("Winter Planter + Frostbloom", 12, UnlockType.Planter, Biome.Winter, "Frostbloom"),
		new UnlockOffer("Ambi Rose", 15, UnlockType.Plant),
		new UnlockOffer("Azure Queen", 20, UnlockType.Plant),
		new UnlockOffer("Neighbouring Root", 25, UnlockType.Plant)
	});
}

public partial class Shop : CanvasLayer
{
	private const int OFFSET_X = 450;
	private const int OFFSET_Y = 260;

	private Label coinLabel;
	private TabContainer shopTabs;
	private VBoxContainer buyingItemsTab;
	private VBoxContainer sellingItemsTab;
	private VBoxContainer unlockablesTab;
	private VBoxContainer specialOrdersTab;
	private readonly Dictionary<string, Label> marketValueLabels = new();
	private readonly Dictionary<InventoryItem, Button> sellButtons = new();
	private readonly Dictionary<InventoryItem, Label> sellValueLabels = new();
	private IReadOnlyDictionary<string, int> seedStock = new Dictionary<string, int>();
	private IReadOnlyDictionary<string, float> marketValue = new Dictionary<string, float>();
	private IReadOnlyList<InventoryItem> inventoryItems = Array.Empty<InventoryItem>();
	private IReadOnlyList<SpecialOrder> specialOrders = Array.Empty<SpecialOrder>();
	private UnlockProgress unlocks;
	private float coins;

	public event Action CloseRequested;
	public event Action<string> BuySeedRequested;
	public event Action<InventoryItem> SellPlantRequested;
	public event Action<string> UnlockRequested;
	public event Action<SpecialOrder> RedeemSpecialOrderRequested;

	public override void _Ready()
	{
		VBoxContainer content = CreateShopContent();
		CreateHeader(content);
		CreateSections(content);
		CreateMarketValueDisplay(content);

		UpdateMarketValueLabels();
		Refresh();
	}

	private VBoxContainer CreateShopContent()
	{
		ColorRect backdrop = new ColorRect
		{
			Color = new Color(0, 0, 0, 0.65f),
			MouseFilter = Control.MouseFilterEnum.Stop,
			AnchorRight = 1,
			AnchorBottom = 1
		};
		AddChild(backdrop);

		PanelContainer panel = new PanelContainer
		{
			AnchorLeft = 0.5f,
			AnchorTop = 0.5f,
			AnchorRight = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -OFFSET_X,
			OffsetTop = -OFFSET_Y,
			OffsetRight = OFFSET_X,
			OffsetBottom = OFFSET_Y,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		AddChild(panel);

		VBoxContainer content = new VBoxContainer();
		panel.AddChild(content);
		return content;
	}

	private void CreateHeader(VBoxContainer content)
	{
		HBoxContainer header = new HBoxContainer();
		content.AddChild(header);
		header.AddChild(new Label
		{
			Text = "Shop",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Center
		});
		coinLabel = new Label { VerticalAlignment = VerticalAlignment.Center };
		header.AddChild(coinLabel);

		Button closeButton = new Button { Text = "Close" };
		closeButton.Pressed += () => CloseRequested?.Invoke();
		header.AddChild(closeButton);
	}

	private void CreateSections(VBoxContainer content)
	{
		shopTabs = new TabContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		content.AddChild(shopTabs);

		buyingItemsTab = CreateTab("Buy seeds");
		sellingItemsTab = CreateTab("Sell plants");
		unlockablesTab = CreateTab("Unlocks");
		specialOrdersTab = CreateTab("Special orders");
	}

	private void CreateMarketValueDisplay(VBoxContainer content)
	{
		VBoxContainer marketSection = new VBoxContainer();
		content.AddChild(marketSection);
		marketSection.AddChild(new HSeparator());
		HBoxContainer marketValues = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		marketSection.AddChild(marketValues);
		foreach (SeedOffer offer in SeedCatalog.Offers)
		{
			Label label = new Label
			{
				HorizontalAlignment = HorizontalAlignment.Center
			};
			marketValueLabels.Add(offer.Name, label);
			marketValues.AddChild(label);
		}
	}

	public override void _Process(double delta)
	{
		foreach (KeyValuePair<InventoryItem, Button> entry in sellButtons)
		{
			InventoryItem item = entry.Key;
			float multiplier = GetMarketMultiplier(item.name);
			sellValueLabels[item].Text =
				$"Current value: {item.currentSellingPrice:0.00} ({multiplier:0.00}x)";
			entry.Value.Text = $"Sell - {item.currentSellingPrice * multiplier:0.00}";
		}
	}

	private void UpdateMarketValueLabels()
	{
		foreach (KeyValuePair<string, Label> entry in marketValueLabels)
		{
			if (marketValue.TryGetValue(entry.Key, out float value))
			{
				entry.Value.Text = $"{entry.Key}: {value:0.00}x";
			}
		}
	}

	public void SetContents(
		float currentCoins,
		IReadOnlyDictionary<string, int> currentSeedStock,
		IReadOnlyList<InventoryItem> currentInventoryItems,
		IReadOnlyDictionary<string, float> currentMarketValues,
		UnlockProgress currentUnlocks,
		IReadOnlyList<SpecialOrder> currentSpecialOrders)
	{
		ArgumentNullException.ThrowIfNull(currentUnlocks);
		ArgumentNullException.ThrowIfNull(currentSpecialOrders);
		
		coins = currentCoins;
		seedStock = currentSeedStock;
		inventoryItems = currentInventoryItems;
		marketValue = currentMarketValues;
		unlocks = currentUnlocks;
		specialOrders = currentSpecialOrders;
		UpdateMarketValueLabels();
		Refresh();
	}

	private VBoxContainer CreateTab(string title)
	{
		VBoxContainer page = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		shopTabs.AddChild(page);
		shopTabs.SetTabTitle(shopTabs.GetTabCount() - 1, title);
		return page;
	}

	private float GetMarketMultiplier(string offerName)
	{
		return marketValue.TryGetValue(offerName, out float value) ? value : 1f;
	}

	private void Refresh()
	{
		if (coinLabel is null || unlocks is null)
		{
			return;
		}

		coinLabel.Text = $"Coins: {coins:0.00}";
		ClearChildren(buyingItemsTab);
		ClearChildren(sellingItemsTab);
		ClearChildren(unlockablesTab);
		ClearChildren(specialOrdersTab);
		sellButtons.Clear();
		sellValueLabels.Clear();

		RefreshSeedOffers();
		RefreshUnlockOffers();
		RefreshSpecialOrders();

		if (inventoryItems.Count == 0)
		{
			sellingItemsTab.AddChild(new Label
			{
				Text = "No plants to sell.",
				HorizontalAlignment = HorizontalAlignment.Center
			});
			return;
		}
		RefreshSellingItems();
	}

	private void RefreshSeedOffers()
	{
		foreach (SeedOffer offer in SeedCatalog.Offers)
		{
			seedStock.TryGetValue(offer.Name, out int count);
			float buyPrice = offer.Price * GetMarketMultiplier(offer.Name);
			HBoxContainer row = new HBoxContainer();
			buyingItemsTab.AddChild(row);
			row.AddChild(new Label
			{
				Text = unlocks.IsPlantUnlocked(offer.Name)
					? $"{offer.Name} Seeds ({count})"
					: $"{offer.Name} Seeds (locked)",
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				VerticalAlignment = VerticalAlignment.Center
			});

			Button buyButton = new Button
			{
				Text = unlocks.IsPlantUnlocked(offer.Name)
					? $"Buy - {buyPrice:0.00} coin"
					: "Locked"
			};
			buyButton.Disabled = !unlocks.IsPlantUnlocked(offer.Name) || coins < buyPrice;
			buyButton.Pressed += () => BuySeedRequested?.Invoke(offer.Name);
			row.AddChild(buyButton);
		}
	}

	private void RefreshUnlockOffers()
	{
		foreach (UnlockOffer offer in UnlockCatalog.Offers)
		{
			HBoxContainer row = new HBoxContainer();
			unlockablesTab.AddChild(row);
			row.AddChild(new Label
			{
				Text = offer.Name,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				VerticalAlignment = VerticalAlignment.Center
			});

			bool isUnlocked = IsUnlocked(offer);
			Button unlockButton = new Button
			{
				Text = isUnlocked ? "Unlocked" : $"Unlock - {offer.Price} coins",
				Disabled = isUnlocked || coins < offer.Price
			};
			unlockButton.Pressed += () => UnlockRequested?.Invoke(offer.Name);
			row.AddChild(unlockButton);
		}
	}

	private void RefreshSellingItems()
	{
		foreach (InventoryItem item in inventoryItems.ToArray())
		{
			HBoxContainer row = new HBoxContainer();
			sellingItemsTab.AddChild(row);
			row.AddChild(new Label
			{
				Text = item.name,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				VerticalAlignment = VerticalAlignment.Center
			});

			float multiplier = GetMarketMultiplier(item.name);
			Label valueLabel = new Label
			{
				Text = $"Current value: {item.currentSellingPrice:0.00} ({multiplier:0.00}x)",
				VerticalAlignment = VerticalAlignment.Center
			};
			row.AddChild(valueLabel);

			Button sellButton = new Button
			{
				Text = $"Sell - {item.currentSellingPrice * multiplier:0.00}"
			};
			sellButton.Pressed += () => SellPlantRequested?.Invoke(item);
			sellButtons.Add(item, sellButton);
			sellValueLabels.Add(item, valueLabel);
			row.AddChild(sellButton);
		}
	}

	private void RefreshSpecialOrders()
	{
		if (specialOrders.Count == 0)
		{
			specialOrdersTab.AddChild(new Label
			{
				Text = "No special orders available.",
				HorizontalAlignment = HorizontalAlignment.Center
			});
			return;
		}

		foreach (SpecialOrder order in specialOrders)
		{
			HBoxContainer row = new HBoxContainer();
			specialOrdersTab.AddChild(row);
			row.AddChild(new Label
			{
				Text = string.Join(", ", order.PlantNames
					.GroupBy(name => name)
					.Select(group => group.Count() == 1
						? group.Key
						: $"{group.Key} x{group.Count()}")),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				VerticalAlignment = VerticalAlignment.Center
			});

			bool canRedeem = order.TryGetItems(inventoryItems, out _);
			Button redeemButton = new Button
			{
				Text = $"Redeem for {order.Reward:0.00} coins",
				Disabled = !canRedeem
			};
			redeemButton.Pressed += () => RedeemSpecialOrderRequested?.Invoke(order);
			row.AddChild(redeemButton);
		}
	}

	private bool IsUnlocked(UnlockOffer offer)
	{
		return offer.Type == UnlockType.Plant
			? unlocks.IsPlantUnlocked(offer.Name)
			: unlocks.IsPlanterUnlocked(offer.PlanterBiome);
	}

	private static void ClearChildren(Node parent)
	{
		foreach (Node child in parent.GetChildren())
		{
			parent.RemoveChild(child);
			child.QueueFree();
		}
	}
}
