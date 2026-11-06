using System.Collections.Generic;
using System.Linq;

namespace ADV.Commands.Base;

public class Switch : CommandBase
{
	private const string defaultKey = "default";

	private string key;

	private Dictionary<string, string> answers;

	public override string[] ArgsLabel => new string[2] { "Key", "CaseTag" };

	public override string[] ArgsDefault => new string[2] { "a", "Case,Tag" };

	public override void ConvertBeforeArgsProc()
	{
		base.ConvertBeforeArgsProc();
		int count = 0;
		key = args[count++];
		answers = (from s in args.Skip(count)
			select s.Split(',') into array
			select (array.Length == 1) ? new string[2]
			{
				"default",
				array[0]
			} : array).ToDictionary((string[] v) => v[0], (string[] v) => v[1]);
	}

	public override void Do()
	{
		base.Do();
		string text = base.scenario.Vars[key].o.ToString();
		if (!answers.TryGetValue(text, out var value))
		{
			text = "default";
			value = answers[text];
		}
		base.scenario.SearchTagJumpOrOpenFile(value, base.localLine);
	}
}
