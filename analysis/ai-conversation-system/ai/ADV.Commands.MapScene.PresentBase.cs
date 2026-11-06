using AIChara;
using AIProject;
using Manager;

namespace ADV.Commands.MapScene;

public abstract class PresentBase : CommandBase
{
	protected abstract Resources.GameInfoTables.AdvPresentItemInfo GetAdvPresentItemInfo(ChaControl chaCtrl);

	protected void SetPresentInfo(ref int cnt)
	{
		CharaData chara = this.GetChara(ref cnt);
		if (chara == null)
		{
			return;
		}
		Resources.GameInfoTables.AdvPresentItemInfo advPresentItemInfo = GetAdvPresentItemInfo(chara.chaCtrl);
		if (advPresentItemInfo == null)
		{
			return;
		}
		int eventItemID = advPresentItemInfo.eventItemID;
		string name = advPresentItemInfo.itemInfo.Name;
		int nameHash = advPresentItemInfo.itemInfo.nameHash;
		PlayState autoPlayState = chara.GetAutoPlayState();
		for (int i = 0; i < autoPlayState.ItemInfoCount; i++)
		{
			if (Singleton<Resources>.Instance.Map.EventItemList.TryGetValue(eventItemID, out var value))
			{
				chara.data.actor.LoadEventItem(eventItemID, autoPlayState.GetItemInfo(i), value);
			}
		}
		base.scenario.Vars["Item"] = new ValData(name);
		base.scenario.Vars[string.Format("{0}.{1}", "Item", "Hash")] = new ValData(nameHash);
	}
}
