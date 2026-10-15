using Godot;
using System;
using System.Collections.Generic;

public partial class PlantOverview : CanvasLayer
{
	private const float MAX_GROWTH_TIME = 15;
	private const float MAX_DECAY_RATE = 0.2f;

	private static readonly IReadOnlyDictionary<Biome, string> BiomeNodeNames =
		new Dictionary<Biome, string>
		{
			[Biome.Winter] = "Winter",
			[Biome.Spring] = "Spring",
			[Biome.Summer] = "Summer",
			[Biome.Autumn] = "Autumn",
			[Biome.Night] = "Nighttime",
			[Biome.Desert] = "Desert",
			[Biome.Space] = "Space",
			[Biome.Ocean] = "Underwater"
		};

	private Node2D biomeIcons;
	private Node2D decayBar;
	private Node2D growthBar;
	private Node2D plantSprites;
	private Label plantSize;
	private Label displayName;
	private Label optimalSaleValue;
	private Label adjLabel;

	public override void _Ready()
	{
		Layer = 11;
		biomeIcons = GetRequiredNode<Node2D>("BiomeIcons");
		decayBar = GetRequiredNode<Node2D>("DecayBar");
		growthBar = GetRequiredNode<Node2D>("GrowthBar");
		plantSprites = GetRequiredNode<Node2D>("PlantSprite");
		plantSize = GetRequiredNode<Label>("Size");
		displayName = GetRequiredNode<Label>("Name");
		optimalSaleValue = GetRequiredNode<Label>("OptimalSaleValue");
		adjLabel = GetRequiredNode<Label>("AdjacencyLabel");

		HideAllSprites(biomeIcons);
		HideAllSprites(decayBar);
		HideAllSprites(growthBar);
		HideAllSprites(plantSprites);
		Hide();
	}

	public void ShowOverview(PlantData data)
	{
		ArgumentNullException.ThrowIfNull(data);
		ArgumentNullException.ThrowIfNull(data.GrowingBiomes);

		displayName.Text = data.DisplayName;
		plantSize.Text = data.PlantSize.ToString();
		ShowStatbar(growthBar, data.GrowthTime / MAX_GROWTH_TIME);
		ShowStatbar(decayBar, data.DecayRate / MAX_DECAY_RATE);
		ShowGrowingBiomes(data.GrowingBiomes);
		ShowOptimalSaleValue(data.OptimalSaleValue);
		ShowAdjacencyBonus(data.Adjacency);
		ShowPlantSprite(data.DisplayName);
		Show();
	}

	public void HideOverview()
	{
		Hide();
	}

	private T GetRequiredNode<T>(string path) where T : Node
	{
		return GetNodeOrNull<T>(path)
			?? throw new InvalidOperationException($"Plant overview scene is missing its '{path}' node.");
	}

	private static void HideAllSprites(Node2D container)
	{
		foreach (Node child in container.GetChildren())
		{
			if (child is CanvasItem sprite)
			{
				sprite.Visible = false;
			}
		}
	}

	private static void ShowStatbar(Node2D statbar, float value)
	{
		int score = Mathf.Clamp(Mathf.RoundToInt(value * 10), 0, 10);
		string spriteName = $"{score}-10";
		ShowSprite(statbar, spriteName);
	}

	private void ShowGrowingBiomes(Godot.Collections.Array<Biome> growingBiomes)
	{
		bool acceptsAnyBiome = growingBiomes.Contains(Biome.Any) || growingBiomes.Contains(Biome.None);
		foreach ((Biome biome, string nodeName) in BiomeNodeNames)
		{
			SetSpriteVisible(biomeIcons, nodeName, acceptsAnyBiome || growingBiomes.Contains(biome));
		}
	}

	private void ShowPlantSprite(string displayName)
	{
		ShowSprite(plantSprites, displayName);
	}

	private void ShowOptimalSaleValue(int value)
	{
		optimalSaleValue.Text = $"{value}";
	}

	private void ShowAdjacencyBonus(AdjacencyBonus adjacency)
	{
		switch (adjacency.BonusType)
		{
			case AdjacencyBonus.Type.None:
				adjLabel.Text = "No adjacency bonus for this plant!";
				break;
			case AdjacencyBonus.Type.AdditionalBiome:
				adjLabel.Text = "Plants belonging to any biome can be placed next to this plant";
				break;
			case AdjacencyBonus.Type.DecayTime:
				adjLabel.Text = $"Adjacent plants' decay at a slower rate \n\t\t({adjacency.Multiplier}x slower)";
				break;
			case AdjacencyBonus.Type.GrowthTime:
				adjLabel.Text = $"Adjacent plants grow at a faster rate \n\t\t({adjacency.Multiplier}x faster)";
				break;
			case AdjacencyBonus.Type.SaleValue:
				adjLabel.Text = $"Harvested adjacent plants can be sold for a higher rate \n\t\t({adjacency.Multiplier}x more)";
				break;
		}
	}

	private static void ShowSprite(Node2D container, string spriteName)
	{
		bool found = false;
		foreach (Node child in container.GetChildren())
		{
			if (child is not CanvasItem sprite)
			{
				continue;
			}

			sprite.Visible = child.Name == spriteName;
			found |= sprite.Visible;
		}

		if (!found)
		{
			throw new InvalidOperationException(
				$"Plant overview node '{container.Name}' has no sprite named '{spriteName}'.");
		}
	}

	private static void SetSpriteVisible(Node2D container, string spriteName, bool visible)
	{
		Node sprite = container.GetNodeOrNull<Node>(spriteName)
			?? throw new InvalidOperationException(
				$"Plant overview node '{container.Name}' has no sprite named '{spriteName}'.");
		if (sprite is not CanvasItem canvasItem)
		{
			throw new InvalidOperationException(
				$"Plant overview node '{container.Name}/{spriteName}' is not a CanvasItem.");
		}

		canvasItem.Visible = visible;
	}
}
