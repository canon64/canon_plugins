namespace ADV.Commands.MapScene;

public class SendCommandDataList : CommandBase
{
	public override string[] ArgsLabel => null;

	public override string[] ArgsDefault => null;

	public override void Do()
	{
		base.Do();
		foreach (CommandData command in base.scenario.package.commandList)
		{
			command.ReceiveADV(base.scenario);
		}
	}
}
