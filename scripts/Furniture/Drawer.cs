using Godot;
using Godot.Collections;

[GlobalClass]
public partial class Drawer : Node3D
{
	[Signal] public delegate void OpenedEventHandler();
	[Signal] public delegate void ClosedEventHandler();
	[Signal] public delegate void ContentsChangedEventHandler();
	[Signal] public delegate void SlotClickedEventHandler(int slot, MouseButton button);

	[Export] public AnimationPlayer Animator { get; set; }
	[Export] public StringName Animation { get; set; } = "ELV_Drawer_RootAction";
	[Export] public Vector2 OpenSection { get; set; } = new(1.25f, 2.92f);
	[Export] public Vector2 CloseSection { get; set; } = new(4.08f, 5.83f);

	[ExportGroup("Storage")]
	[Export] public Node3D Slots { get; set; }
	[Export] public Vector3 SlotSize { get; set; } = new(0.1f, 0.09f, 0.19f);
	[Export] public float StoreDuration { get; set; } = 0.35f;
	[Export] public Array<ItemData> Items { get; set; } = new();

	public bool IsOpen { get; private set; }

	private ItemPickup[] stored;

	public override void _Ready()
	{
		int capacity = Slots.GetChildCount();
		Items = Items.Duplicate();
		while (Items.Count < capacity)
		{
			Items.Add(null);
		}

		stored = new ItemPickup[capacity];
		for (int i = 0; i < capacity; i++)
		{
			stored[i] = Place(i, Items[i], null);

			if (Slots.GetChild(i) is CollisionObject3D area)
			{
				int slot = i;
				area.InputRayPickable = false;
				area.InputEvent += (_, @event, _, _, _) => OnSlotInput(slot, @event);
			}
		}
	}

	public void Open() => Slide(true);
	public void Close() => Slide(false);
	public void Toggle() => Slide(!IsOpen);

	public void ClickSlot(int slot, MouseButton button)
	{
		EmitSignal(SignalName.SlotClicked, slot, (int)button);
	}

	public ItemData Swap(int slot, ItemData item, Transform3D from, out Transform3D takenFrom)
	{
		ItemData taken = Items[slot];
		takenFrom = stored[slot]?.GlobalTransform ?? Slots.GetChild<Node3D>(slot).GlobalTransform;

		stored[slot]?.QueueFree();
		Items[slot] = item;
		stored[slot] = Place(slot, item, from);

		EmitSignal(SignalName.ContentsChanged);
		return taken;
	}

	private ItemPickup Place(int slot, ItemData item, Transform3D? from)
	{
		if (item is null || !item.IsPhysical)
		{
			return null;
		}

		ItemPickup pickup = item.MakePickup();
		pickup.Hold();
		Slots.GetChild<Node3D>(slot).AddChild(pickup);
		Transform3D rest = pickup.FitInside(SlotSize);

		if (from.HasValue)
		{
			pickup.GlobalTransform = from.Value;
			Transform3D start = pickup.Transform;
			CreateTween()
				.SetTrans(Tween.TransitionType.Back)
				.SetEase(Tween.EaseType.Out)
				.TweenMethod(Callable.From<float>(t =>
				{
					if (IsInstanceValid(pickup))
					{
						pickup.Transform = start.InterpolateWith(rest, t);
					}
				}), 0f, 1f, StoreDuration);
		}
		else
		{
			pickup.Transform = rest;
		}

		return pickup;
	}

	private void Slide(bool open)
	{
		if (IsOpen == open)
		{
			return;
		}

		IsOpen = open;
		Vector2 section = open ? OpenSection : CloseSection;
		Vector2 interrupted = open ? CloseSection : OpenSection;

		double start = section.X;
		if (Animator.IsPlaying())
		{
			double progress = Mathf.InverseLerp(interrupted.X, interrupted.Y, Animator.CurrentAnimationPosition);
			start = Mathf.Lerp(section.X, section.Y, 1.0 - Mathf.Clamp(progress, 0.0, 1.0));
		}

		Animator.PlaySection(Animation, section.X, section.Y);
		Animator.Seek(start, true);

		foreach (Node slot in Slots.GetChildren())
		{
			if (slot is CollisionObject3D area)
			{
				area.InputRayPickable = open;
			}
		}

		EmitSignal(open ? SignalName.Opened : SignalName.Closed);
	}

	private void OnSlotInput(int slot, InputEvent @event)
	{
		if (@event is InputEventMouseButton { Pressed: true } click &&
			click.ButtonIndex is MouseButton.Left or MouseButton.Right)
		{
			ClickSlot(slot, click.ButtonIndex);
		}
	}

	private void OnHandleInputEvent(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx)
	{
		if (@event is InputEventMouseButton { Pressed: true } click &&
			click.ButtonIndex is MouseButton.Left or MouseButton.Right)
		{
			Toggle();
		}
	}
}
