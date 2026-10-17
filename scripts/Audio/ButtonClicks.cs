using Godot;

[GlobalClass]
public partial class ButtonClicks : AudioStreamPlayer
{
	public override void _Ready()
	{
		GetTree().NodeAdded += OnNodeAdded;
	}

	public override void _ExitTree()
	{
		GetTree().NodeAdded -= OnNodeAdded;
	}

	private void OnNodeAdded(Node node)
	{
		if (node is BaseButton button)
		{
			button.Pressed += () => Play();
		}
	}
}
