namespace ADV.Commands.Base;

public class Clear : CommandBase
{
	public override string[] ArgsLabel => null;

	public override string[] ArgsDefault => null;

	public override void Do()
	{
		base.Do();
		base.scenario.textController.Clear();
	}
}
