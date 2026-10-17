using Godot;
using System;

[GlobalClass]
public partial class RoomCamera : Camera3D
{
	[Signal] public delegate void ViewChangedEventHandler(Node3D view);

	[Export] public Node3D Viewpoints { get; set; }
	[Export] public UnlockProgress Unlocks { get; set; }
	[Export] public int StartingFloor { get; set; }
	[Export] public int StartingView { get; set; }
	[Export] public float TurnDuration { get; set; } = 0.8f;
	[Export] public float ClimbDuration { get; set; } = 1.4f;
	[Export(PropertyHint.Range, "0,10,0.1,suffix:°")] public float MouseSway { get; set; } = 2.5f;
	[Export] public AudioStreamPlayer ClimbSound { get; set; }

	public Node3D CurrentView { get; private set; }

	private int floorIndex;
	private int[] viewIndices;
	private Transform3D anchor;
	private Vector2 sway;
	private Tween turnTween;

	private Storey Floor => Viewpoints.GetChild<Storey>(floorIndex);

	public override void _Ready()
	{
		ArgumentNullException.ThrowIfNull(Unlocks);

		floorIndex = StartingFloor;
		viewIndices = new int[Viewpoints.GetChildCount()];
		viewIndices[floorIndex] = StartingView;
		if (!IsViewUnlocked(Floor.Views[viewIndices[floorIndex]]))
		{
			viewIndices[floorIndex] = FindNextUnlockedView(viewIndices[floorIndex], 1);
		}
		CurrentView = Floor.Views[viewIndices[floorIndex]];

		anchor = CurrentView.GlobalTransform;
		GlobalTransform = anchor;
		Arrive();
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
		else if (@event.IsActionPressed("switch_floor"))
		{
			Climb(1);
		}
	}

	public void Turn(int direction)
	{
		if (direction == 0) // No direction given; return
		{
			return;
		}

		int currentViewIndex = viewIndices[floorIndex];
		int nextViewIndex = FindNextUnlockedView(currentViewIndex, direction);
		if (nextViewIndex == currentViewIndex) // No unlocked views other than this one; return
		{
			return;
		}

		viewIndices[floorIndex] = nextViewIndex;
		MoveTo(TurnDuration);
	}

	public void Climb(int direction)
	{
		int nextFloorIndex = FindNextUnlockedFloor(floorIndex, direction);
		if (nextFloorIndex == floorIndex)
		{
			return;
		}

		floorIndex = nextFloorIndex;
		ClimbSound?.Play();
		MoveTo(ClimbDuration);
	}

	private int FindNextUnlockedFloor(int fromIndex, int direction)
	{
		int floorCount = Viewpoints.GetChildCount();
		for (int offset = 1; offset <= floorCount; offset++)
		{
			int candidateIndex = Mathf.PosMod(fromIndex + direction * offset, floorCount);
			Storey candidateFloor = Viewpoints.GetChild<Storey>(candidateIndex);
			foreach (Node3D view in candidateFloor.Views)
			{
				if (IsViewUnlocked(view))
				{
					return candidateIndex;
				}
			}
		}

		return fromIndex;
	}

	private int FindNextUnlockedView(int fromIndex, int direction)
	{
		int viewCount = Floor.Views.Count;
		for (int offset = 1; offset <= viewCount; offset++)
		{
			int candidateIndex = Mathf.PosMod(fromIndex + direction * offset, viewCount);
			if (IsViewUnlocked(Floor.Views[candidateIndex]))
			{
				return candidateIndex;
			}
		}

		return fromIndex;
	}

	private bool IsViewUnlocked(Node3D view)
	{
		// Biome name is different from the node name
		if (string.Equals(view.Name.ToString(), "Nocturnal"))
		{
			return Unlocks.IsPlanterUnlocked(Biome.Night);
		}

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

	private void MoveTo(float duration)
	{
		CurrentView = Floor.Views[viewIndices[floorIndex]];

		Transform3D from = anchor;
		Transform3D to = CurrentView.GlobalTransform;

		turnTween?.Kill();
		turnTween = CreateTween()
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);
		turnTween.TweenMethod(Callable.From<float>(t => anchor = from.InterpolateWith(to, t)), 0f, 1f, duration);
		turnTween.Parallel().TweenCallback(Callable.From(Arrive)).SetDelay(duration * 0.5f);

		EmitSignal(SignalName.ViewChanged, CurrentView);
	}

	private void Arrive()
	{
		foreach (Storey storey in Viewpoints.GetChildren())
		{
			storey.SetActive(storey == Floor);
		}

		Environment = Floor.Environment;
	}
}
