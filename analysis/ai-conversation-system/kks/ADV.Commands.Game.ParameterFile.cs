using Manager;

namespace ADV.Commands.Game;

public class ParameterFile : CommandBase
{
	public override string[] ArgsLabel => new string[1] { "Asset" };

	public override string[] ArgsDefault => new string[1] { string.Empty };

	public override void Do()
	{
		base.Do();
		if (SingletonInitializer<Manager.Game>.instance.scenarioParameterDic.TryGetValue(args[0], out var value))
		{
			base.scenario.CommandPacks.InsertRange(base.scenario.CurrentLine + 1, value);
		}
	}
}
