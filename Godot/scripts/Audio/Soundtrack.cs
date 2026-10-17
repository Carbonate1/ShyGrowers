using Godot;

[GlobalClass]
public partial class Soundtrack : Node
{
	[Export] public AudioStream Track { get; set; }

	public override void _Ready()
	{
		GetNode<Jukebox>("/root/Music").PlayTrack(Track);
	}
}
