using Godot;

[GlobalClass]
public partial class BaggedFlower : RigidBody3D
{
	[Export] public Sprite3D Sprite { get; set; }
	[Export] public float PopTime { get; set; } = 0.3f;
	[Export] public float PopRise { get; set; } = 0.2f;

	public void ShowIn(Node3D display, Texture2D texture)
	{
		Sprite.Texture = texture;
		Sprite.Reparent(display, false);
		Sprite.Transform = Transform;
	}

	public void Pop(Vector3 outOf)
	{
		Sprite3D sprite = Sprite;
		Sprite = null;
		QueueFree();

		Tween tween = sprite.CreateTween().SetParallel().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
		tween.TweenProperty(sprite, "position", outOf + Vector3.Up * PopRise, PopTime);
		tween.TweenProperty(sprite, "scale", Vector3.Zero, PopTime);
		tween.Chain().TweenCallback(Callable.From(sprite.QueueFree));
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Sprite is not null)
		{
			Sprite.Transform = Transform;
		}
	}
}
