using System.Collections.Generic;
using System.Linq;
using Illusion;

namespace ADV.Commands.Base;

public class Probs : CommandBase
{
	public override string[] ArgsLabel => new string[1] { "ProbTag" };

	public override string[] ArgsDefault => new string[1] { "Prob,Tag" };

	public override void Do()
	{
		base.Do();
		Dictionary<string, int> targetDict = args.Select((string s) => s.Split(',')).ToDictionary((string[] v) => base.scenario.ReplaceVars(v[1]), delegate(string[] v)
		{
			int.TryParse(base.scenario.ReplaceVars(v[0]), out var result);
			return result;
		});
		string jump = Illusion.Utils.ProbabilityCalclator.DetermineFromDict(targetDict);
		base.scenario.SearchTagJumpOrOpenFile(jump, base.localLine);
	}
}
