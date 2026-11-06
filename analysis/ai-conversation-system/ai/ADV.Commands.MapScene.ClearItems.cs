namespace ADV.Commands.MapScene;

public class ClearItems : CommandBase
{
	public override string[] ArgsLabel => new string[1] { "No" };

	public override string[] ArgsDefault => new string[1] { "0" };

	public override void Do()
	{
		base.Do();
		int cnt = 0;
		this.GetChara(ref cnt)?.data.actor.ClearItems();
	}
}
