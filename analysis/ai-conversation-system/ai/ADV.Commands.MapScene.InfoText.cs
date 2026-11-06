using AIProject;

namespace ADV.Commands.MapScene;

public class InfoText : CommandBase
{
	public override string[] ArgsLabel => new string[1] { "Text" };

	public override string[] ArgsDefault => new string[1] { string.Empty };

	public override void Do()
	{
		base.Do();
		MapUIContainer.WarningMessageUI.isFadeInForOutWait = true;
		MapUIContainer.PushMessageUI(args[0], 0, 1);
	}

	public override bool Process()
	{
		base.Process();
		return false;
	}

	public override void Result(bool processEnd)
	{
		base.Result(processEnd);
		MapUIContainer.WarningMessageUI.isFadeInForOutWait = false;
	}
}
