using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Linq;

[GlobalClass]
public partial class Storey : Node3D
{
	[Export] public Environment Environment { get; set; }
	[Export] public Array<Node3D> Lights { get; set; } = new();
	[Export] public CanvasItem Overlay { get; set; }
	[Export] public AudioStreamPlayer Ambience { get; set; }
	[Export] public float FadeTime { get; set; } = 0.6f;
	[Export] public float SilentDb { get; set; } = -40f;

	public IReadOnlyList<Marker3D> Views => GetChildren().OfType<Marker3D>().ToList();

	private bool? active;
	private float ambienceVolume;
	private Tween fade;
	private Tween hush;

	public override void _Ready()
	{
		if (Ambience is not null)
		{
			ambienceVolume = Ambience.VolumeDb;
		}
	}

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

		if (Overlay is not null)
		{
			FadeOverlay(value);
		}

		if (Ambience is not null)
		{
			FadeAmbience(value);
		}
	}

	private void FadeOverlay(bool value)
	{
		fade?.Kill();
		Overlay.Show();
		fade = CreateTween();
		fade.TweenProperty(Overlay, "modulate:a", value ? 1f : 0f, FadeTime);
		if (!value)
		{
			fade.TweenCallback(Callable.From(Overlay.Hide));
		}
	}

	private void FadeAmbience(bool value)
	{
		hush?.Kill();
		hush = CreateTween();
		if (value)
		{
			if (!Ambience.Playing)
			{
				Ambience.VolumeDb = SilentDb;
				Ambience.Play();
			}
			hush.TweenProperty(Ambience, "volume_db", ambienceVolume, FadeTime);
		}
		else
		{
			hush.TweenProperty(Ambience, "volume_db", SilentDb, FadeTime);
			hush.TweenCallback(Callable.From(Ambience.Stop));
		}
	}
}
