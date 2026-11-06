namespace ADV.Commands.Game;

public class CharaActive : CommandBase
{
	public override string[] ArgsLabel => new string[3] { "No", "isActive", "Stand" };

	public override string[] ArgsDefault => new string[3]
	{
		int.MaxValue.ToString(),
		bool.TrueString,
		string.Empty
	};

	public override void Do()
	{
		base.Do();
		int num = 0;
		int no = int.Parse(args[num++]);
		CharaData chara = base.scenario.commandController.GetChara(no);
		bool isVisible = bool.Parse(args[num++]);
		if (0 == 0)
		{
			chara.data.isVisible = isVisible;
		}
	}
}
