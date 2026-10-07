using Godot;

[GlobalClass]
public partial class Hand : Node3D
{
	[Export] public Hands Hands { get; set; }
	[Export] public MouseButton Button { get; set; } = MouseButton.Left;
	[Export] public StringName TossAction { get; set; } = "toss_left";
	[Export] public Node3D World { get; set; }
	[Export] public Drawer Drawer { get; set; }

	[ExportGroup("Feel")]
	[Export] public float TossSpeed { get; set; } = 2.6f;
	[Export] public float TossLift { get; set; } = 1.4f;
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
		Hands.Changed += Refresh;
		if (Drawer is not null)
		{
			Drawer.SlotClicked += OnDrawerSlotClicked;
		}
		Refresh();
	}

	public override void _ExitTree()
	{
		GetTree().NodeAdded -= Watch;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed(TossAction))
		{
			Toss();
		}
		else if (@event is InputEventMouseButton { Pressed: true } click && click.ButtonIndex == Button && model is not null)
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

	public void Toss()
	{
		if (model is null || IsBusy)
		{
			return;
		}

		Vector3 forward = -GetParent<Node3D>().GlobalBasis.Z;
		ItemPickup thrown = Drop(shown, model.GlobalTransform.Orthonormalized());
		thrown.LinearVelocity = forward * TossSpeed + Vector3.Up * TossLift;
		thrown.AngularVelocity = new Vector3((float)GD.RandRange(-4.0, 4.0), (float)GD.RandRange(-4.0, 4.0), (float)GD.RandRange(-4.0, 4.0));

		Hands.Set(Button, null);
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

		ItemData held = Hands.Get(Button);
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
		Hands.Set(Button, pickup.Item);
	}

	private void OnDrawerSlotClicked(int slot, MouseButton button)
	{
		if (button != Button || IsBusy)
		{
			return;
		}

		Transform3D from = model?.GlobalTransform ?? GlobalTransform;
		ItemData taken = Drawer.Swap(slot, Hands.Get(Button), from, out Transform3D takenFrom);

		incoming = takenFrom;
		Hands.Set(Button, taken);
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
		ItemData item = Hands.Get(Button);
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
