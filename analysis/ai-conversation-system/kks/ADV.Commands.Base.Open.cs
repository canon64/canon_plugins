namespace ADV.Commands.Base;

public class Open : CommandBase
{
	public override string[] ArgsLabel => new string[5] { "Bundle", "Asset", "isAdd", "isClearCheck", "isNext" };

	public override string[] ArgsDefault => new string[5]
	{
		string.Empty,
		string.Empty,
		bool.FalseString,
		bool.TrueString,
		bool.TrueString
	};

	public override void Do()
	{
		base.Do();
		int num = 0;
		string text = args[num++];
		string text2 = args[num++];
		bool flag = bool.Parse(args[num++]);
		bool isClearCheck = bool.Parse(args[num++]);
		bool isNext = bool.Parse(args[num++]);
		if (text.IsNullOrEmpty())
		{
			text = base.scenario.LoadBundleName;
		}
		if (!AssetBundleCheck.IsFile(text))
		{
			text = Program.ScenarioBundle(text);
		}
		base.scenario.Vars["BundleFile"] = new ValData(text);
		base.scenario.Vars["AssetFile"] = new ValData(text2);
		base.scenario.LoadFile(text, text2, !flag, isClearCheck, isNext);
	}
}
