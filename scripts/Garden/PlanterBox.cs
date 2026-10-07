using Godot;

[GlobalClass]
public partial class PlanterBox : Node3D
{
	[Export] public float LookAwaySpeed { get; set; } = 3f;
	[Export] public VisibleOnScreenNotifier3D Watcher { get; set; }

	public bool IsWatched => Watcher is null || Watcher.IsOnScreen();
	public float TimeScale => IsWatched ? 1f : LookAwaySpeed;
}
