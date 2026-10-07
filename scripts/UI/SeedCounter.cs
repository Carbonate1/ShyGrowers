using Godot;

[GlobalClass]
public partial class SeedCounter : Label
{
	[Export] public ItemData Seeds { get; set; }

	public void ShowCount(int count)
	{
		Text = $"{Seeds?.DisplayName}  x{count}";
		Modulate = count > 0 ? Colors.White : new Color(1f, 1f, 1f, 0.45f);
	}
}
