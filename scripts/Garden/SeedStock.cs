using Godot;
using Godot.Collections;
using System.Linq;

[GlobalClass]
public partial class SeedStock : Node
{
	[Signal] public delegate void ChangedEventHandler();

	[Export] public Dictionary<ItemData, int> Seeds { get; set; } = new();

	public int Total => Seeds.Values.Sum();

	public override void _Ready()
	{
		Seeds = Seeds.Duplicate();
	}

	public int Count(ItemData seeds)
	{
		return seeds is not null && Seeds.TryGetValue(seeds, out int count) ? count : 0;
	}

	public void Use(ItemData seeds)
	{
		if (Count(seeds) <= 0)
		{
			return;
		}

		Seeds[seeds] = Count(seeds) - 1;
		EmitSignal(SignalName.Changed);
	}

	public void Add(ItemData seeds, int amount)
	{
		if (seeds is null || amount <= 0)
		{
			return;
		}

		Seeds[seeds] = Count(seeds) + amount;
		EmitSignal(SignalName.Changed);
	}
}
