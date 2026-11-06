using UnityEngine;

namespace ADV.Commands.Game;

public class CameraCorrectLightAngle : CommandBase
{
	public override string[] ArgsLabel => new string[3] { "isLocal", "X", "Y" };

	public override string[] ArgsDefault => new string[2]
	{
		bool.TrueString,
		string.Empty
	};

	public override void Do()
	{
		base.Do();
		int cnt = 0;
		bool num = bool.Parse(args[cnt++]);
		if (!base.scenario.commandController.GetV3Dic(args[cnt], out var pos))
		{
			CommandBase.CountAddV3(args, ref cnt, ref pos);
		}
		if (num)
		{
			base.scenario.advScene.correctLightAngle.lightTrans.localRotation = Quaternion.Euler(pos.x, pos.y, 0f);
		}
		else
		{
			base.scenario.advScene.correctLightAngle.lightTrans.rotation = Quaternion.Euler(pos.x, pos.y, 0f);
		}
	}
}
