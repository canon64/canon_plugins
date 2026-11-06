namespace ADV.Commands.MapScene;

public class SendCommandData : CommandBase
{
	private string name;

	public override string[] ArgsLabel => new string[1] { "Name" };

	public override string[] ArgsDefault => new string[1] { string.Empty };

	public override void ConvertBeforeArgsProc()
	{
		base.ConvertBeforeArgsProc();
		int num = 0;
		name = args[num++];
	}

	public override void Do()
	{
		base.Do();
		foreach (CommandData command in base.scenario.package.commandList)
		{
			if (command.key == name)
			{
				command.ReceiveADV(base.scenario);
				break;
			}
		}
	}
}
