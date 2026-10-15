using Godot;
using System;

[GlobalClass]
public partial class Plot : Area3D
{
	[Signal] public delegate void ClickedEventHandler(int plotIndex, MouseButton button);

	public int Index { get; private set; } = -1;

	public override void _InputEvent(
		Camera3D camera,
		InputEvent @event,
		Vector3 eventPosition,
		Vector3 normal,
		int shapeIdx)
	{
		if (@event is InputEventMouseButton { Pressed: true } click &&
			click.ButtonIndex is MouseButton.Left or MouseButton.Right)
		{
			EmitSignal(SignalName.Clicked, Index, (int)click.ButtonIndex);
		}
	}

	internal void Initialize(int index)
	{
		Index = index;
	}
}
