using Godot;

[GlobalClass]
public partial class RoomCamera : Camera3D
{
	[Signal] public delegate void ViewChangedEventHandler(Node3D view);

	[Export] public Node3D Viewpoints { get; set; }
	[Export] public UnlockProgress Unlocks { get; set; }
	[Export] public int StartingView { get; set; }
	[Export] public float TurnDuration { get; set; } = 0.8f;
	[Export(PropertyHint.Range, "0,10,0.1,suffix:°")] public float MouseSway { get; set; } = 2.5f;

	public Node3D CurrentView { get; private set; }

	private int viewIndex;
	private Transform3D anchor;
	private Vector2 sway;
	private Tween turnTween;

	public override void _Ready()
	{
		ArgumentNullException.ThrowIfNull(Unlocks);

		viewIndex = StartingView;
		if (!IsViewUnlocked(Viewpoints.GetChild<Node3D>(viewIndex)))
		{
			viewIndex = FindNextUnlockedView(viewIndex, 1);
		}

		CurrentView = Viewpoints.GetChild<Node3D>(viewIndex);
		anchor = CurrentView.GlobalTransform;
		GlobalTransform = anchor;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("look_left"))
		{
			Turn(-1);
		}
		else if (@event.IsActionPressed("look_right"))
		{
			Turn(1);
		}
	}

	public void Turn(int direction)
	{
		if (direction == 0) // No direction given; return
		{
			return;
		}

		int nextViewIndex = FindNextUnlockedView(viewIndex, direction);
		if (nextViewIndex == viewIndex) // No unlocked views other than this one; return
		{
			return;
		}

		viewIndex = nextViewIndex;
		CurrentView = Viewpoints.GetChild<Node3D>(viewIndex);

		Transform3D from = anchor;
		Transform3D to = CurrentView.GlobalTransform;

		turnTween?.Kill();
		turnTween = CreateTween()
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);
		turnTween.TweenMethod(Callable.From<float>(t => anchor = from.InterpolateWith(to, t)), 0f, 1f, TurnDuration);

		EmitSignal(SignalName.ViewChanged, CurrentView);
	}

	private int FindNextUnlockedView(int fromIndex, int direction)
	{
		int viewCount = Viewpoints.GetChildCount();
		for (int offset = 1; offset <= viewCount; offset++)
		{
			int candidateIndex = Mathf.PosMod(fromIndex + direction * offset, viewCount);
			if (IsViewUnlocked(Viewpoints.GetChild<Node3D>(candidateIndex)))
			{
				return candidateIndex;
			}
		}

		return fromIndex;
	}

	private bool IsViewUnlocked(Node3D view)
	{
		if (!Enum.TryParse(view.Name.ToString(), true, out Biome biome) ||
			biome is Biome.Any or Biome.None)
		{
			return true;
		}

		return Unlocks.IsPlanterUnlocked(biome);
	}

	public override void _Process(double delta)
	{
		Vector2 screenSize = GetViewport().GetVisibleRect().Size;
		Vector2 mouse = GetViewport().GetMousePosition() / screenSize * 2f - Vector2.One;
		mouse = mouse.Clamp(-Vector2.One, Vector2.One);

		sway = sway.Lerp(mouse, 1f - Mathf.Exp(-3f * (float)delta));
		Vector3 tilt = new Vector3(-sway.Y, -sway.X, 0f) * Mathf.DegToRad(MouseSway);
		GlobalTransform = anchor * new Transform3D(Basis.FromEuler(tilt), Vector3.Zero);
	}
}
