namespace ADV.Commands.Game;

public class CameraCorrectLightActive : CommandBase
{
	public override string[] ArgsLabel => new string[1] { "Active" };

	public override string[] ArgsDefault => new string[1] { bool.TrueString };

	public override void Do()
	{
		base.Do();
		base.scenario.advScene.correctLightAngle.enabled = bool.Parse(args[0]);
	}
}
