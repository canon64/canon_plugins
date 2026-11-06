using Manager;

namespace ADV.Commands.MapScene;

public class ChangeADVFixedAngleCamera : CommandBase
{
	public override string[] ArgsLabel => new string[2] { "No", "AttitudeID" };

	public override string[] ArgsDefault => new string[2] { "0", "0" };

	public override void Do()
	{
		base.Do();
		int cnt = 0;
		CharaData chara = this.GetChara(ref cnt);
		if (chara != null)
		{
			int attitudeID = int.Parse(args[cnt++]);
			Manager.ADV.ChangeADVFixedAngleCamera(chara.data.actor, attitudeID);
		}
	}
}
