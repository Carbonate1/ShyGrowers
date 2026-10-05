using Godot;
using System;

public enum ToolUseResult
{
	NoEffect,
	CareCompleted,
	DiscardPlant,
	HarvestPlant
}

public partial class ToolItem : Area2D
{
	[Export]
	public Care CareType { get; set; }

	public event Action<ToolItem, MouseButton> Clicked;

	public ToolItem()
	{
		CollisionLayer = 1;
		CollisionMask = 0;
		InputPickable = true;

		AddChild(new CollisionShape2D
		{
			Shape = new CircleShape2D { Radius = 24 }
		});

		InputEvent += OnInputEvent;
	}

	public void SetHeld(bool isHeld)
	{
		Visible = !isHeld;
		InputPickable = !isHeld;
	}

	public virtual bool UseOnEmptyPlot(PlantBox plantBox, int plotIndex)
	{
		return false;
	}

	public virtual ToolUseResult UseOnPlant(Plant plant)
	{
		ArgumentNullException.ThrowIfNull(plant);
		return ToolUseResult.NoEffect;
	}

	private void OnInputEvent(Node viewport, InputEvent @event, long shapeIdx)
	{
		if (@event is InputEventMouseButton mouseButtonEvent &&
			mouseButtonEvent.Pressed &&
			mouseButtonEvent.ButtonIndex is MouseButton.Left or MouseButton.Right)
		{
			Clicked?.Invoke(this, mouseButtonEvent.ButtonIndex);
			GetViewport().SetInputAsHandled();
		}
	}
}
