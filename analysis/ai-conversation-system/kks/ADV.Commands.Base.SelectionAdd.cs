namespace ADV.Commands.Base;

public class SelectionAdd : CommandBase
{
	public override string[] ArgsLabel => new string[2] { "Jump", "Text" };

	public override string[] ArgsDefault => new string[2]
	{
		string.Empty,
		string.Empty
	};

	public override void Do()
	{
		base.Do();
		int num = 0;
		TextScenario.ChoiceData item = new TextScenario.ChoiceData(base.scenario.ReplaceVars(args[num++]), base.scenario.ReplaceText(args[num++], useOnceTagConvert: true));
		base.scenario.choiceList.Add(item);
	}
}
