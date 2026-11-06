using System.Collections.Generic;
using AIProject;
using AIProject.SaveData;
using UnityEngine;

namespace ADV.Commands.MapScene;

public class AddItem : ItemListControlBase
{
	public override string[] ArgsLabel => new string[3] { "No", "ItemHash", "Num" };

	public override string[] ArgsDefault => new string[3] { "-1", "-1", "1" };

	protected override void ItemListProc(StuffItemInfo itemInfo, List<StuffItem> itemList, StuffItem stuffItem)
	{
		Debug.Log($"AddItem:{itemInfo.Name}x{stuffItem.Count}");
		itemList.AddItem(stuffItem);
	}

	public override void Do()
	{
		base.Do();
		int cnt = 0;
		SetItem(ref cnt);
	}
}
