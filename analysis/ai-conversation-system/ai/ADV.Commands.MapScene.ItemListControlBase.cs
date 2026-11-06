using System.Collections.Generic;
using AIProject;
using AIProject.SaveData;
using Manager;
using UnityEngine;

namespace ADV.Commands.MapScene;

public abstract class ItemListControlBase : CommandBase
{
	protected abstract void ItemListProc(StuffItemInfo itemInfo, List<StuffItem> itemList, StuffItem stuffItem);

	protected void SetItem(ref int cnt)
	{
		CharaData chara = this.GetChara(ref cnt);
		if (chara == null)
		{
			return;
		}
		int num = int.Parse(args[cnt++]);
		StuffItemInfo stuffItemInfo = Singleton<Manager.Resources>.Instance.GameInfo.FindItemInfo(num);
		if (stuffItemInfo == null)
		{
			Debug.LogError($"Item none:{num}");
			return;
		}
		if (!int.TryParse(args[cnt++], out var result))
		{
			Debug.LogError(string.Format("Num none:{0}[{1}]:{2}", "args", cnt - 1, args[cnt - 1]));
			result = 1;
		}
		ItemListProc(stuffItemInfo, chara.data.characterInfo.ItemList, new StuffItem(stuffItemInfo.CategoryID, stuffItemInfo.ID, result));
	}
}
