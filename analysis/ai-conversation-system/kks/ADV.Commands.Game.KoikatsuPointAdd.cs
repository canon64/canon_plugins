using ActionGame;
using Manager;

namespace ADV.Commands.Game;

public class KoikatsuPointAdd : CommandBase
{
	public override string[] ArgsLabel => new string[1] { "Num" };

	public override string[] ArgsDefault => new string[1] { "0" };

	private bool isChanged { get; set; }

	public override void Do()
	{
		base.Do();
		Manager.Game.Player.koikatsuPoint += int.Parse(args[0]);
		isChanged = KoikatsuPointUI.isChanged;
	}

	public override bool Process()
	{
		base.Process();
		if (isChanged)
		{
			if (KoikatsuPointUI.isActive)
			{
				isChanged = false;
			}
			return false;
		}
		return !KoikatsuPointUI.isActive;
	}
}
