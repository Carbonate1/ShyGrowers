using Godot;

[GlobalClass]
public partial class Plot : Area3D
{
	[Signal] public delegate void ClickedEventHandler(Plot plot, MouseButton button);

	public Plant Plant { get; private set; }
	public ItemData SownWith { get; private set; }

	private PlanterBox box;

	public override void _Ready()
	{
		Node parent = GetParent();
		while (parent is not null && parent is not PlanterBox)
		{
			parent = parent.GetParent();
		}

		box = parent as PlanterBox;
	}

	public override void _Process(double delta)
	{
		if (Plant is not null && box is not null)
		{
			Plant.TimeScale = box.TimeScale;
		}
	}

	public override void _InputEvent(Camera3D camera, InputEvent @event, Vector3 eventPosition, Vector3 normal, int shapeIdx)
	{
		if (@event is InputEventMouseButton { Pressed: true } click &&
			click.ButtonIndex is MouseButton.Left or MouseButton.Right)
		{
			EmitSignal(SignalName.Clicked, this, (int)click.ButtonIndex);
		}
	}

	public void Sow(ItemData seeds)
	{
		SownWith = seeds;
		Plant = seeds.PlantScene.Instantiate<Plant>();
		AddChild(Plant);
	}

	public void Clear()
	{
		Plant?.QueueFree();
		Plant = null;
		SownWith = null;
	}
}
