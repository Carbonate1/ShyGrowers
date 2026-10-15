using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed record SeedOffer(string Name, string PlantDataPath, string ScenePath, int Price);
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
			1),
		new SeedOffer(
			"Rose",
			"res://resources/plants/rose.tres",
			"res://scenes/Plants/rose.tscn",
			1),
		new SeedOffer(
			"Frostbloom",
			"res://resources/plants/frostbloom.tres",
			"res://scenes/Plants/frostbloom.tscn",
			1),
		new SeedOffer(
			"Ambi Rose",
			"res://resources/plants/ambi_rose.tres",
			"res://scenes/Plants/ambi_rose.tscn",
			1),
		new SeedOffer(
			"Azure Queen",
			"res://resources/plants/azure_queen.tres",
			"res://scenes/Plants/azure_queen.tscn",
			1),
		new SeedOffer(
			"Neighbouring Root",
			"res://resources/plants/neighbouring_root.tres",
			"res://scenes/Plants/neighbouring_root.tscn",
			1),
	});
};

public partial class Shop : CanvasLayer
{
	private Label coinLabel;
	private VBoxContainer buyingItems;
	private VBoxContainer sellingItems;
	private readonly Dictionary<string, Label> marketValueLabels = new();
	private readonly Dictionary<InventoryItem, Button> sellButtons = new();
	private IReadOnlyDictionary<string, int> seedStock = new Dictionary<string, int>();
	private IReadOnlyDictionary<string, float> marketValue = new Dictionary<string, float>();
	private IReadOnlyList<InventoryItem> inventoryItems = Array.Empty<InventoryItem>();
	private float coins;

	public event Action CloseRequested;
	public event Action<string> BuySeedRequested;
	public event Action<InventoryItem> SellPlantRequested;

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
			OffsetLeft = -450,
			OffsetTop = -260,
			OffsetRight = 450,
			OffsetBottom = 260,
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
		HBoxContainer sections = new HBoxContainer
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		content.AddChild(sections);

		VBoxContainer buySection = CreateSection("Buy seeds");
		buyingItems = new VBoxContainer();
		buySection.AddChild(buyingItems);
		sections.AddChild(buySection);

		VBoxContainer sellSection = CreateSection("Sell plants");
		sellingItems = new VBoxContainer();
		sellSection.AddChild(sellingItems);
		sections.AddChild(sellSection);

		VBoxContainer ordersSection = CreateSection("Special orders");
		sections.AddChild(ordersSection);
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
			entry.Value.Text = $"Sell - {entry.Key.currentSellingPrice:0.00}";
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
		IReadOnlyDictionary<string, float> currentMarketValues)
	{
		coins = currentCoins;
		seedStock = currentSeedStock;
		inventoryItems = currentInventoryItems;
		marketValue = currentMarketValues;
		UpdateMarketValueLabels();
		Refresh();
	}

	private static VBoxContainer CreateSection(string title)
	{
		VBoxContainer section = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		section.AddChild(new Label
		{
			Text = title,
			HorizontalAlignment = HorizontalAlignment.Center
		});
		section.AddChild(new HSeparator());
		return section;
	}

	private void Refresh()
	{
		if (coinLabel is null)
		{
			return;
		}

		coinLabel.Text = $"Coins: {coins:0.00}";
		ClearChildren(buyingItems);
		ClearChildren(sellingItems);
		sellButtons.Clear();

		foreach (SeedOffer offer in SeedCatalog.Offers)
		{
			seedStock.TryGetValue(offer.Name, out int count);
			HBoxContainer row = new HBoxContainer();
			buyingItems.AddChild(row);
			row.AddChild(new Label
			{
				Text = $"{offer.Name} Seeds ({count})",
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				VerticalAlignment = VerticalAlignment.Center
			});

			Button buyButton = new Button { Text = $"Buy - {offer.Price} coin" };
			buyButton.Disabled = coins < offer.Price;
			buyButton.Pressed += () => BuySeedRequested?.Invoke(offer.Name);
			row.AddChild(buyButton);
		}

		if (inventoryItems.Count == 0)
		{
			sellingItems.AddChild(new Label
			{
				Text = "No plants to sell.",
				HorizontalAlignment = HorizontalAlignment.Center
			});
			return;
		}

		foreach (InventoryItem item in inventoryItems.ToArray())
		{
			HBoxContainer row = new HBoxContainer();
			sellingItems.AddChild(row);
			row.AddChild(new Label
			{
				Text = item.name,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				VerticalAlignment = VerticalAlignment.Center
			});

			Button sellButton = new Button { Text = $"Sell - {item.currentSellingPrice:0.00}" };
			sellButton.Pressed += () => SellPlantRequested?.Invoke(item);
			sellButtons.Add(item, sellButton);
			row.AddChild(sellButton);
		}
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
