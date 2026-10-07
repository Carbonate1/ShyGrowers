using Godot;

[GlobalClass]
public partial class SceneButton : Button
{
	[Export(PropertyHint.File, "*.tscn")] public string Scene { get; set; } = "";

	public override void _Pressed()
	{
		if (string.IsNullOrEmpty(Scene))
		{
			GetTree().ReloadCurrentScene();
		}
		else
		{
			GetTree().ChangeSceneToFile(Scene);
		}
	}
}
