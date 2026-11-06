using Illusion;

namespace ADV.Commands.Base;

public class Prob : CommandBase
{
	public override string[] ArgsLabel => new string[3] { "Prob", "True", "False" };

	public override string[] ArgsDefault => new string[3] { "100", "tagA", "tagB" };

	public override void Do()
	{
		base.Do();
		int num = 0;
		float percent = float.Parse(args[num++]);
		string text = args[num++];
		string text2 = args[num++];
		string jump = (Utils.ProbabilityCalclator.DetectFromPercent(percent) ? text : text2);
		base.scenario.SearchTagJumpOrOpenFile(jump, base.localLine);
	}
}
