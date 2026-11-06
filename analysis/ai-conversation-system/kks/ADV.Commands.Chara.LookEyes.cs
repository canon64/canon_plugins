namespace ADV.Commands.Chara;

public class LookEyes : CommandBase
{
	public override string[] ArgsLabel => new string[2] { "No", "Ptn" };

	public override string[] ArgsDefault => new string[2]
	{
		int.MaxValue.ToString(),
		"0"
	};

	public override void Do()
	{
		base.Do();
		int num = 0;
		int no = int.Parse(args[num++]);
		int ptn = int.Parse(args[num++]);
		base.scenario.commandController.GetChara(no).chaCtrl.ChangeLookEyesPtn(ptn);
	}
}
