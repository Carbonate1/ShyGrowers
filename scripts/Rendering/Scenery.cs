using Godot;
using Godot.Collections;

[GlobalClass]
public partial class Scenery : MeshInstance3D
{
	[Export] public Array<Texture2D> Views { get; set; } = new();
	[Export] public float HoldTime { get; set; } = 90f;
	[Export] public float FadeTime { get; set; } = 6f;

	private ShaderMaterial material;
	private int current;

	public override void _Ready()
	{
		material = (ShaderMaterial)MaterialOverride.Duplicate();
		MaterialOverride = material;
		Settle(0);

		if (Views.Count > 1)
		{
			Wait();
		}
	}

	private void Settle(int index)
	{
		current = index;
		material.SetShaderParameter("view", Views[index]);
		material.SetShaderParameter("next_view", Views[index]);
		material.SetShaderParameter("blend", 0f);
	}

	private void Wait()
	{
		CreateTween().TweenCallback(Callable.From(Advance)).SetDelay(HoldTime);
	}

	private void Advance()
	{
		int next = (current + 1) % Views.Count;
		material.SetShaderParameter("next_view", Views[next]);

		Tween tween = CreateTween();
		tween.TweenMethod(Callable.From<float>(t => material.SetShaderParameter("blend", t)), 0f, 1f, FadeTime);
		tween.TweenCallback(Callable.From(() => Settle(next)));
		tween.TweenCallback(Callable.From(Wait));
	}
}
