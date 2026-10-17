using Godot;

public partial class TitleMenu : Control
{
	private void OnQuitPressed()
	{
		GetTree().Quit();
	}
}
