using Godot;
using System;

[GlobalClass]
public partial class Plot : Area3D
{
	[Signal] public delegate void ClickedEventHandler(Plot plot, MouseButton button);

	public Plant Plant { get; private set; }
	public ItemData SownWith { get; private set; }
	public PlanterBox Box { get; private set; }
	public int Index { get; private set; } = -1;

	public bool IsOccupied => Plant is not null;

	public override void _Ready()
	{
		Node parent = GetParent();
		while (parent is not null && parent is not PlanterBox)
		{
			parent = parent.GetParent();
		}

		Box = parent as PlanterBox
			?? throw new InvalidOperationException("Plot must be a child of a PlanterBox.");
	}

	public override void _InputEvent(
		Camera3D camera,
		InputEvent @event,
		Vector3 eventPosition,
		Vector3 normal,
		int shapeIdx)
	{
		if (@event is InputEventMouseButton { Pressed: true } click &&
			click.ButtonIndex is MouseButton.Left or MouseButton.Right)
		{
			EmitSignal(SignalName.Clicked, this, (int)click.ButtonIndex);
		}
	}

	public bool PlantSeed(ItemData seeds)
	{
		ArgumentNullException.ThrowIfNull(seeds);
		if (seeds.PlantScene is null || IsOccupied)
		{
			return false;
		}

		Plant plant = seeds.PlantScene.Instantiate<Plant>();
		if (Box.TryPlant(plant, Index, seeds))
		{
			return true;
		}

		plant.Free();
		return false;
	}

	public void Clear()
	{
		Plant plant = Plant;
		if (plant is null || !Box.RemovePlant(plant))
		{
			return;
		}

		plant.QueueFree();
	}

	internal void Initialize(PlanterBox box, int index)
	{
		Box = box;
		Index = index;
	}

	internal void SetPlant(Plant plant, ItemData seeds)
	{
		Plant = plant;
		SownWith = seeds;
	}
}
