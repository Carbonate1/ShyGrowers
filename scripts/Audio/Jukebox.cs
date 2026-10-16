using Godot;

[GlobalClass]
public partial class Jukebox : AudioStreamPlayer
{
	[Export] public float FadeTime { get; set; } = 1.5f;
	[Export] public float SilentDb { get; set; } = -40f;

	private float volume;
	private Tween fade;

	public override void _Ready()
	{
		volume = VolumeDb;
	}

	public void PlayTrack(AudioStream track)
	{
		if (track == Stream && Playing)
		{
			return;
		}

		fade?.Kill();
		fade = CreateTween();
		if (Playing)
		{
			fade.TweenProperty(this, "volume_db", SilentDb, FadeTime);
		}

		fade.TweenCallback(Callable.From(() =>
		{
			Stream = track;
			VolumeDb = SilentDb;
			Play();
		}));
		fade.TweenProperty(this, "volume_db", volume, FadeTime);
	}
}
