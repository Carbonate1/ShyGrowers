using Godot;

[GlobalClass]
public partial class ItemSlot : Button
{
	[Signal] public delegate void ClickedEventHandler(ItemSlot slot, MouseButton button);

	[Export] public string EmptyText { get; set; } = "empty";

	public ItemData Item { get; private set; }

	public override void _Ready()
	{
		SetItem(null);
	}

	public void SetItem(ItemData item)
	{
		Item = item;
		Text = item?.DisplayName ?? EmptyText;
		Icon = item?.Icon;
		Modulate = item is null ? new Color(1f, 1f, 1f, 0.6f) : Colors.White;
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton { Pressed: true } click &&
			click.ButtonIndex is MouseButton.Left or MouseButton.Right)
		{
			EmitSignal(SignalName.Clicked, this, (int)click.ButtonIndex);
		}
	}
}
