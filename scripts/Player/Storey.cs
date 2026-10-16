using Godot;
using Godot.Collections;

[GlobalClass]
public partial class Storey : Node3D
{
	[Export] public Environment Environment { get; set; }
	[Export] public Array<Node3D> Lights { get; set; } = new();
	[Export] public CanvasItem Overlay { get; set; }
	[Export] public float FadeTime { get; set; } = 0.6f;

	private bool? active;
	private Tween fade;

	public void SetActive(bool value)
	{
		if (active == value)
		{
			return;
		}

		active = value;
		foreach (Node3D light in Lights)
		{
			light.Visible = value;
		}

		if (Overlay is null)
		{
			return;
		}

		fade?.Kill();
		Overlay.Show();
		fade = CreateTween();
		fade.TweenProperty(Overlay, "modulate:a", value ? 1f : 0f, FadeTime);
		if (!value)
		{
			fade.TweenCallback(Callable.From(Overlay.Hide));
		}
	}
}
