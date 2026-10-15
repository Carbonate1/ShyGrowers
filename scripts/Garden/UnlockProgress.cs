using Godot;
using System;
using System.Collections.Generic;

public partial class UnlockProgress : Node
{
	private readonly HashSet<string> unlockedPlants = new(StringComparer.Ordinal)
	{
		"Marigold"
	};
	private readonly HashSet<Biome> unlockedPlanters = new()
	{
		Biome.Autumn
	};

	public bool IsPlantUnlocked(string plantName)
	{
		return unlockedPlants.Contains(plantName);
	}

	public bool IsPlanterUnlocked(Biome biome)
	{
		return unlockedPlanters.Contains(biome);
	}

	public bool UnlockPlant(string plantName)
	{
		return unlockedPlants.Add(plantName);
	}

	public bool UnlockPlanter(Biome biome)
	{
		return unlockedPlanters.Add(biome);
	}
}
