namespace ADV.Commands.MapScene;

public class CharaSetting : CommandBase
{
	public override string[] ArgsLabel => new string[2] { "No", "Active" };

	public override string[] ArgsDefault => new string[2]
	{
		"0",
		bool.TrueString
	};

	public override void Do()
	{
		base.Do();
		int cnt = 0;
		CharaData chara = this.GetChara(ref cnt);
		if (chara != null)
		{
			if (bool.Parse(args[cnt++]))
			{
				chara.backup.Repair();
			}
			else
			{
				chara.backup.Set();
			}
		}
	}
}
