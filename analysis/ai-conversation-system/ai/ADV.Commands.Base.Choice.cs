using System.Linq;
using AIProject.UI;
using Manager;

namespace ADV.Commands.Base;

public class Choice : CommandBase
{
	public class ChoiceData
	{
		public readonly string text;

		public readonly string jump;

		public ChoiceData(string text, string jump)
		{
			this.text = text;
			this.jump = jump;
		}
	}

	private ChoiceData choice;

	private string labelTitleBK = string.Empty;

	public override string[] ArgsLabel => new string[2] { "Visible", "Case" };

	public override string[] ArgsDefault => new string[2]
	{
		bool.TrueString,
		"text,tag"
	};

	public override void Do()
	{
		base.Do();
		int count = 0;
		if (!bool.Parse(args[count++]))
		{
			base.scenario.captionSystem.Clear();
		}
		CommCommandList.CommandInfo[] options = (from arg in args.Skip(count)
			where !args.IsNullOrEmpty()
			select arg.Split(',') into ss
			where ss.Length >= 2
			select new ChoiceData(base.scenario.ReplaceText(ss[0]), base.scenario.ReplaceVars(ss[1])) into choice
			select new CommCommandList.CommandInfo(choice.text, null, delegate
			{
				this.choice = choice;
			})).ToArray();
		labelTitleBK = base.scenario.ChoiceON(string.Empty, options);
		base.scenario.isSkip = base.scenario.isSkip && Config.GameData.ChoicesSkip;
		base.scenario.isAuto = base.scenario.isAuto && Config.GameData.ChoicesAuto;
	}

	public override bool Process()
	{
		base.Process();
		return choice != null;
	}

	public override void Result(bool processEnd)
	{
		base.Result(processEnd);
		if (processEnd)
		{
			base.scenario.ChoiceOFF(labelTitleBK);
			base.scenario.TextLogCall(new Text.Data(string.Empty, choice.text), null);
			base.scenario.SearchTagJumpOrOpenFile(choice.jump, base.localLine);
			base.scenario.captions.CanvasGroupON();
		}
	}
}
