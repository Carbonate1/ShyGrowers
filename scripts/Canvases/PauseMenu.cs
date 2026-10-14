using Godot;

public partial class PauseMenu : CanvasLayer
{
	private const string GARDEN_SCENE = "res://scenes/garden.tscn";
	private const int LAYER = 10;
	private const int MIN_SIZE_X = 420;
	private const int FONT_SIZE = 48;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		Layer = LAYER;
		CreateMenu();
		Hide();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (Visible && @event is InputEventKey { Keycode: Key.Escape, Pressed: true, Echo: false })
		{
			Resume();
			GetViewport().SetInputAsHandled();
		}
	}

	private void CreateMenu()
	{
		ColorRect backdrop = new()
		{
			Color = new Color(0, 0, 0, 0.65f),
			MouseFilter = Control.MouseFilterEnum.Stop,
			AnchorRight = 1,
			AnchorBottom = 1
		};
		AddChild(backdrop);

		CenterContainer center = new();
		center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddChild(center);

		PanelContainer panel = new()
		{
			CustomMinimumSize = new Vector2(MIN_SIZE_X, 0),
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		center.AddChild(panel);

		VBoxContainer content = new();
		panel.AddChild(content);

		Label title = new()
		{
			Text = "Paused",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", FONT_SIZE);
		content.AddChild(title);

		Button resumeButton = new() { Text = "Resume" };
		resumeButton.Pressed += Resume;
		content.AddChild(resumeButton);

		SceneButton restartButton = new()
		{
			Text = "Restart",
			Scene = GARDEN_SCENE
		};
		content.AddChild(restartButton);
	}

	private void Resume()
	{
		GetTree().Paused = false;
		Hide();
	}
}
