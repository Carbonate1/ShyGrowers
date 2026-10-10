using Godot;
using System;
using System.Collections.Generic;

public partial class Inventory : CanvasLayer
{
	private readonly List<InventoryItem> items = new();
	private readonly List<Label> priceLabels = new();
	private readonly Dictionary<InventoryItem, HBoxContainer> itemRows = new();
	private VBoxContainer itemList;
	private Label emptyMessage;

	public event Action CloseRequested;

	public IReadOnlyList<InventoryItem> Items => items;

	public override void _Ready()
	{
		VBoxContainer content = CreateContent();
		CreateHeader(content);
		CreateItemList(content);
		AddExistingItems();
	}

	private VBoxContainer CreateContent()
	{
		var backdrop = new ColorRect
		{
			Color = new Color(0, 0, 0, 0.65f),
			MouseFilter = Control.MouseFilterEnum.Stop,
			AnchorRight = 1,
			AnchorBottom = 1
		};
		AddChild(backdrop);

		var panel = new PanelContainer
		{
			AnchorLeft = 0.5f,
			AnchorTop = 0.5f,
			AnchorRight = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -240,
			OffsetTop = -200,
			OffsetRight = 240,
			OffsetBottom = 200,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		AddChild(panel);

		var content = new VBoxContainer();
		panel.AddChild(content);
		return content;
	}

	private void CreateHeader(VBoxContainer content)
	{
		var header = new HBoxContainer();
		content.AddChild(header);
		header.AddChild(new Label
		{
			Text = "Inventory",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Center
		});

		var closeButton = new Button { Text = "Close" };
		closeButton.Pressed += () => CloseRequested?.Invoke();
		header.AddChild(closeButton);
	}

	private void CreateItemList(VBoxContainer content)
	{
		emptyMessage = new Label
		{
			Text = "No harvested plants yet.",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		content.AddChild(emptyMessage);

		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		content.AddChild(scroll);

		itemList = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		scroll.AddChild(itemList);
	}

	private void AddExistingItems()
	{
		emptyMessage.Visible = items.Count == 0;
		foreach (InventoryItem item in items)
		{
			AddItemRow(item);
		}
	}

	public override void _Process(double delta)
	{
		for (int i = 0; i < items.Count; i++)
		{
			items[i].Decay(delta);
			priceLabels[i].Text = $"Value: {items[i].currentSellingPrice:0.00}";
		}
	}

	public void AddItem(InventoryItem item)
	{
		ArgumentNullException.ThrowIfNull(item);
		items.Add(item);
		if (itemList is not null)
		{
			emptyMessage.Visible = false;
			AddItemRow(item);
		}
	}

	private void AddItemRow(InventoryItem item)
	{
		var row = new HBoxContainer();
		itemList.AddChild(row);

		var icon = new TextureRect
		{
			Texture = item.texture,
			CustomMinimumSize = new Vector2(48, 48),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
		};
		row.AddChild(icon);
		row.AddChild(new Label
		{
			Text = item.name,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Center
		});

		var priceLabel = new Label
		{
			Text = $"Value: {item.currentSellingPrice:0.00}",
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center
		};
		priceLabels.Add(priceLabel);
		row.AddChild(priceLabel);

		itemRows.Add(item, row);
	}

	public bool RemoveItem(InventoryItem item)
	{
		int itemIndex = items.IndexOf(item);
		if (itemIndex < 0)
		{
			return false;
		}

		items.RemoveAt(itemIndex);
		priceLabels.RemoveAt(itemIndex);
		HBoxContainer row = itemRows[item];
		itemRows.Remove(item);
		itemList.RemoveChild(row);
		row.QueueFree();
		emptyMessage.Visible = items.Count == 0;
		return true;
	}
}
