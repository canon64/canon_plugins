using System.Collections.Generic;

namespace ADV.Commands.EventCG;

public class Release : CommandBase
{
	public override string[] ArgsLabel => new string[1] { "isMotionContinue" };

	public override string[] ArgsDefault => new string[1] { bool.FalseString };

	public override void Do()
	{
		base.Do();
		if (!Common.Release(base.scenario))
		{
			return;
		}
		foreach (KeyValuePair<int, CharaData> character in base.scenario.commandController.Characters)
		{
			character.Value.backup.Repair();
		}
		base.scenario.commandController.useCorrectCamera = true;
	}
}
