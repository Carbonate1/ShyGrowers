using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

public sealed class SpecialOrder
{
	public const double LIFETIME_SECONDS = 60;

	private readonly string[] plantNames;
	private double secondsRemaining;

	public IReadOnlyList<string> PlantNames => plantNames;
	public float Reward { get; }
	public int SecondsRemaining => Math.Max(0, (int)Math.Ceiling(secondsRemaining));

	public bool AdvanceTime(double delta)
	{
		secondsRemaining -= delta;
		return secondsRemaining <= 0;
	}

	public SpecialOrder(IEnumerable<string> plantNames, float reward)
	{
        secondsRemaining = LIFETIME_SECONDS;
        
		ArgumentNullException.ThrowIfNull(plantNames);
		this.plantNames = plantNames.ToArray();
		ArgumentOutOfRangeException.ThrowIfLessThan(this.plantNames.Length, 1);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(this.plantNames.Length, 3);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(reward, 0);

		foreach (string plantName in this.plantNames)
		{
			if (!SeedCatalog.Offers.Any(offer => offer.Name == plantName))
			{
				throw new ArgumentException($"Unknown plant '{plantName}' in special order.", nameof(plantNames));
			}
		}

		Reward = reward;
	}

	public bool TryGetItems(
		IReadOnlyList<InventoryItem> inventoryItems,
		out IReadOnlyList<InventoryItem> requiredItems)
	{
		ArgumentNullException.ThrowIfNull(inventoryItems);
		List<InventoryItem> selectedItems = new(plantNames.Length);
		bool[] itemSelected = new bool[inventoryItems.Count];

		foreach (string plantName in plantNames)
		{
			bool found = false;
			for (int index = 0; index < inventoryItems.Count; index++)
			{
				if (!itemSelected[index] && inventoryItems[index].name == plantName)
				{
					itemSelected[index] = true;
					selectedItems.Add(inventoryItems[index]);
					found = true;
					break;
				}
			}

			if (!found)
			{
				requiredItems = Array.Empty<InventoryItem>();
				return false;
			}
		}

		requiredItems = selectedItems;
		return true;
	}
}
