using System;
using System.Collections.Generic;
using AIProject;
using AIProject.SaveData;
using Manager;

namespace ADV.Commands.MapScene;

public class SetItemScrounge : CommandBase
{
	public override string[] ArgsLabel => new string[1] { "No" };

	public override string[] ArgsDefault => new string[1] { "0" };

	public override void Do()
	{
		base.Do();
		int cnt = 0;
		CharaData chara = this.GetChara(ref cnt);
		if (chara != null)
		{
			Tuple<StuffItemInfo, int> advScroungeInfo = Singleton<Resources>.Instance.GameInfo.GetAdvScroungeInfo(chara.chaCtrl);
			StuffItemInfo item = advScroungeInfo.Item1;
			int item2 = advScroungeInfo.Item2;
			base.scenario.AddItemVars(item, item2);
			chara.data.agentActor.AgentData.ItemScrounge.Set((IReadOnlyCollection<StuffItem>)(object)new StuffItem[1]
			{
				new StuffItem(item.CategoryID, item.ID, item2)
			});
		}
	}
}
