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
		string jump = Utils.ProbabilityCalclator.DetermineFromDict(args.Select((string s) => s.Split(',')).ToDictionary((string[] v) => base.scenario.ReplaceVars(v[1]), delegate(string[] v)
		{
			int.TryParse(base.scenario.ReplaceVars(v[0]), out var result);
			return result;
		}));
		base.scenario.SearchTagJumpOrOpenFile(jump, base.localLine);
	}
}
