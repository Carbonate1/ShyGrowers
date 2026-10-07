using Godot;

[GlobalClass]
public partial class Hands : Node
{
	[Signal] public delegate void ChangedEventHandler();

	[Export] public ItemData Left { get; set; }
	[Export] public ItemData Right { get; set; }

	public ItemData Get(MouseButton button)
	{
		return button == MouseButton.Right ? Right : Left;
	}

	public void Set(MouseButton button, ItemData item)
	{
		if (button == MouseButton.Right)
		{
			Right = item;
		}
		else
		{
			Left = item;
		}

		EmitSignal(SignalName.Changed);
	}
}
