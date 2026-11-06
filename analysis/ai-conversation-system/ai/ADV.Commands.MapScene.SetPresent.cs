using AIChara;
using Manager;

namespace ADV.Commands.MapScene;

public class SetPresent : PresentBase
{
	public override string[] ArgsLabel => new string[1] { "No" };

	public override string[] ArgsDefault => new string[1] { "0" };

	protected override Resources.GameInfoTables.AdvPresentItemInfo GetAdvPresentItemInfo(ChaControl chaCtrl)
	{
		return Singleton<Resources>.Instance.GameInfo.GetAdvPresentInfo(chaCtrl);
	}

	public override void Do()
	{
		base.Do();
		int cnt = 0;
		SetPresentInfo(ref cnt);
	}
}
