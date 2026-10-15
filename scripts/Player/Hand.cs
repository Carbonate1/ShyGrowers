using Godot;

[GlobalClass]
public partial class Hand : Node3D
{
	[Export] public MouseButton Button { get; set; } = MouseButton.Left;
	[Export] public Node3D World { get; set; }
	[Export] public Drawer Drawer { get; set; }
	[Export] public ItemData Item
	{
		get => item;
		set
		{
			if (item == value)
			{
				return;
			}

			item = value;
			if (IsInsideTree())
			{
				Refresh();
				EmitSignal(SignalName.ItemChanged);
			}
		}
	}

	[Signal] public delegate void ItemChangedEventHandler();

	[ExportGroup("Feel")]
	[Export] public float Stiffness { get; set; } = 80f;
	[Export] public float Damping { get; set; } = 11f;
	[Export(PropertyHint.Range, "0,2,0.05")] public float Inertia { get; set; } = 0.7f;
	[Export] public float SwingStrength { get; set; } = 7f;
	[Export] public float PullSpeed { get; set; } = 12f;

	[ExportGroup("Using On Plots")]
	[Export] public Vector3 ReachOffset { get; set; } = new(0f, 0.3f, 0.1f);
	[Export(PropertyHint.Range, "-180,180,1,suffix:°")] public float ReachYaw { get; set; } = 90f;
	[Export] public float ReachTime { get; set; } = 0.35f;
	[Export] public float ImpactDelay { get; set; } = 0.3f;
	[Export] public float UseTime { get; set; } = 0.6f;

	public bool IsBusy { get; private set; }

	private ItemData shown;
	private ItemData item;
	private ItemPickup model;
	private Transform3D? incoming;
	private Transform3D rest;
	private Basis? lastBasis;
	private Vector3 tilt;
	private Vector3 tiltVelocity;
	private float time;
	private Tween useTween;

	public override void _Ready()
	{
		rest = Transform;

		foreach (Node node in GetTree().GetNodesInGroup("pickups"))
		{
			Watch(node);
		}

		GetTree().NodeAdded += Watch;
		if (Drawer is not null)
		{
			Drawer.SlotClicked += OnDrawerSlotClicked;
		}
		Refresh();
	}

	public override void _ExitTree()
	{
		GetTree().NodeAdded -= Watch;
		if (Drawer is not null)
		{
			Drawer.SlotClicked -= OnDrawerSlotClicked;
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton { Pressed: true } click && click.ButtonIndex == Button && model is not null)
		{
			tiltVelocity += new Vector3(-SwingStrength, 0f, SwingStrength * 0.25f * Mathf.Sign(rest.Origin.X));
		}
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		time += dt;

		Basis cameraBasis = GetParent<Node3D>().GlobalBasis;
		tilt -= ((lastBasis ?? cameraBasis).Inverse() * cameraBasis).GetEuler() * Inertia;
		lastBasis = cameraBasis;

		tiltVelocity += (-tilt * Stiffness - tiltVelocity * Damping) * dt;
		tilt = (tilt + tiltVelocity * dt).Clamp(-Vector3.One * 0.6f, Vector3.One * 0.6f);

		Vector3 bob = new(0f, Mathf.Sin(time * 1.7f + rest.Origin.X * 3f) * 0.004f, 0f);
		Transform = rest * new Transform3D(Basis.FromEuler(tilt), bob);

		if (model is not null && !IsBusy)
		{
			Transform3D held = model.GripTransform.AffineInverse();
			model.Transform = model.Transform.InterpolateWith(held, 1f - Mathf.Exp(-PullSpeed * dt));
		}
	}

	public void UseOn(Vector3 target, System.Action landed)
	{
		if (IsBusy)
		{
			return;
		}

		if (model?.Aim is null)
		{
			model?.Use();
			landed();
			return;
		}

		Vector3 eye = GetParent<Node3D>().GlobalPosition;
		Vector3 forward = new Vector3(target.X - eye.X, 0f, target.Z - eye.Z).Normalized();
		Basis facing = Basis.LookingAt(forward, Vector3.Up) * new Basis(Vector3.Up, Mathf.DegToRad(ReachYaw));
		Transform3D goal = new Transform3D(facing, target + facing * ReachOffset) * model.Aim.Transform.AffineInverse();

		ItemPickup used = model;
		Transform3D start = used.GlobalTransform;
		IsBusy = true;

		useTween = CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		useTween.TweenMethod(Callable.From<float>(t => used.GlobalTransform = start.InterpolateWith(goal, t)), 0f, 1f, ReachTime);
		useTween.TweenCallback(Callable.From(used.Use));
		useTween.TweenInterval(ImpactDelay);
		useTween.TweenCallback(Callable.From(landed));
		useTween.TweenInterval(UseTime);
		useTween.TweenCallback(Callable.From(() => IsBusy = false));
	}

	private void Watch(Node node)
	{
		if (node is ItemPickup pickup && pickup.IsInGroup("pickups"))
		{
			pickup.Clicked += OnPickupClicked;
		}
	}

	private void OnPickupClicked(ItemPickup pickup, MouseButton button)
	{
		if (button != Button || IsBusy || pickup.IsQueuedForDeletion())
		{
			return;
		}

		ItemData held = Item;
		if (held is not null && !held.IsPhysical)
		{
			return;
		}

		if (held is not null)
		{
			ItemPickup swapped = Drop(held, pickup.GlobalTransform);
			swapped.LinearVelocity = Vector3.Up * 1.2f;
		}

		incoming = pickup.GlobalTransform;
		pickup.QueueFree();
		Item = pickup.Item;
	}

	private void OnDrawerSlotClicked(int slot, MouseButton button)
	{
		if (button != Button || IsBusy || Drawer.Items[slot] is null)
		{
			return;
		}

		Transform3D from = model?.GlobalTransform ?? GlobalTransform;
		ItemData taken = Drawer.Swap(slot, Item, from, out Transform3D takenFrom);

		incoming = takenFrom;
		Item = taken;
	}

	private ItemPickup Drop(ItemData item, Transform3D where)
	{
		ItemPickup pickup = item.MakePickup();
		World.AddChild(pickup);
		pickup.GlobalTransform = where;
		return pickup;
	}

	private void Refresh()
	{
		ItemData item = Item;
		if (item == shown && !incoming.HasValue)
		{
			return;
		}

		shown = item;
		model?.QueueFree();
		model = null;
		useTween?.Kill();
		IsBusy = false;

		if (item is not null && item.IsPhysical)
		{
			model = item.MakePickup();
			model.Hold();
			AddChild(model);

			Transform3D held = model.GripTransform.AffineInverse();
			model.Transform = incoming.HasValue
				? GlobalTransform.AffineInverse() * incoming.Value
				: held.Translated(new Vector3(0f, -0.3f, 0.1f));
		}

		incoming = null;
	}
}
