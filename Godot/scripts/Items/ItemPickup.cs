using Godot;

[GlobalClass]
public partial class ItemPickup : RigidBody3D
{
	[Signal] public delegate void ClickedEventHandler(ItemPickup pickup, MouseButton button);

	[Export] public ItemData Item { get; set; }
	[Export] public Marker3D Grip { get; set; }
	[Export] public Marker3D Stow { get; set; }
	[Export] public Marker3D Aim { get; set; }
	[Export] public GpuParticles3D UseEffect { get; set; }

	[ExportGroup("Sounds")]
	[Export] public AudioStreamPlayer3D UseSound { get; set; }
	[Export] public AudioStreamPlayer3D LandSound { get; set; }
	[Export] public float LandSpeed { get; set; } = 0.6f;

	public Transform3D GripTransform => Grip?.Transform ?? Transform3D.Identity;

	public override void _Ready()
	{
		if (LandSound is null)
		{
			return;
		}

		ContactMonitor = true;
		MaxContactsReported = 1;
		BodyEntered += OnBodyEntered;
	}

	public void Hold()
	{
		ProcessMode = ProcessModeEnum.Disabled;
		DisableMode = DisableModeEnum.Remove;
		InputRayPickable = false;
		RemoveFromGroup("pickups");

		foreach (Node node in FindChildren("*", "GeometryInstance3D", true, false))
		{
			((GeometryInstance3D)node).CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		}

		foreach (Node node in FindChildren("*", "GPUParticles3D", true, false))
		{
			node.ProcessMode = ProcessModeEnum.Always;
		}

		foreach (Node node in FindChildren("*", "AudioStreamPlayer3D", true, false))
		{
			node.ProcessMode = ProcessModeEnum.Always;
		}
	}

	public void Use()
	{
		UseSound?.Play();

		if (UseEffect is null)
		{
			return;
		}

		UseEffect.Restart();

		GpuParticles3D splash = UseEffect.GetNodeOrNull<GpuParticles3D>(UseEffect.SubEmitter);
		if (splash is null || RenderingServer.GetRenderingDevice() is null)
		{
			return;
		}

		GetTree().CreateTimer(0.05).Timeout += () =>
		{
			if (IsInstanceValid(splash))
			{
				splash.Emitting = true;
			}
		};
		UseEffect.Connect(GpuParticles3D.SignalName.Finished, Callable.From(() => splash.Emitting = false), (uint)ConnectFlags.OneShot);
	}

	public Transform3D FitInside(Vector3 size)
	{
		Transform3D stow = (Stow?.Transform ?? Transform3D.Identity).Orthonormalized().AffineInverse();
		Aabb bounds = stow * VisualBounds();

		Vector3 fit = size / bounds.Size;
		float scale = Mathf.Min(1f, Mathf.Min(fit.X, Mathf.Min(fit.Y, fit.Z)));
		Vector3 anchor = new(bounds.GetCenter().X, bounds.Position.Y, bounds.GetCenter().Z);

		return new Transform3D(Basis.FromScale(Vector3.One * scale), -anchor * scale) * stow;
	}

	public override void _InputEvent(Camera3D camera, InputEvent @event, Vector3 eventPosition, Vector3 normal, int shapeIdx)
	{
		if (@event is InputEventMouseButton { Pressed: true } click &&
			click.ButtonIndex is MouseButton.Left or MouseButton.Right)
		{
			EmitSignal(SignalName.Clicked, this, (int)click.ButtonIndex);
		}
	}

	private void OnBodyEntered(Node body)
	{
		if (LinearVelocity.Length() > LandSpeed)
		{
			LandSound.Play();
		}
	}

	private Aabb VisualBounds()
	{
		Aabb bounds = new();
		bool first = true;

		foreach (Node node in FindChildren("*", "VisualInstance3D", true, false))
		{
			if (node is not (MeshInstance3D or SpriteBase3D))
			{
				continue;
			}

			VisualInstance3D visual = (VisualInstance3D)node;
			Aabb box = RelativeTransform(visual) * visual.GetAabb();
			bounds = first ? box : bounds.Merge(box);
			first = false;
		}

		return bounds;
	}

	private Transform3D RelativeTransform(Node3D node)
	{
		Transform3D result = Transform3D.Identity;
		for (Node3D current = node; current is not null && current != this; current = current.GetParent() as Node3D)
		{
			result = current.Transform * result;
		}
		return result;
	}
}
