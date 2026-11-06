using System.Collections.Generic;
using AIProject;
using AIProject.SaveData;
using Manager;
using UnityEngine;

namespace ADV.Commands.MapScene;

public class InventoryCheck : CommandBase
{
	public override string[] ArgsLabel => new string[3] { "True", "False", "Type" };

	public override string[] ArgsDefault => new string[3]
	{
		string.Empty,
		string.Empty,
		"0"
	};

	public override void Do()
	{
		base.Do();
		int num = int.Parse(args[2]);
		List<StuffItem> self;
		int capacity;
		switch (num)
		{
		case 0:
			self = Singleton<Map>.Instance.Player.PlayerData.ItemList;
			capacity = Singleton<Map>.Instance.Player.PlayerData.InventorySlotMax;
			break;
		case 1:
			self = Singleton<Manager.Game>.Instance.Environment.ItemListInStorage;
			capacity = Singleton<Manager.Resources>.Instance.DefinePack.ItemBoxCapacityDefines.StorageCapacity;
			break;
		case 2:
			self = Singleton<Manager.Game>.Instance.Environment.ItemListInPantry;
			capacity = Singleton<Manager.Resources>.Instance.DefinePack.ItemBoxCapacityDefines.PantryCapacity;
			break;
		default:
			Debug.LogError(string.Format("{0}:{1}", "type", num));
			return;
		}
		bool flag = self.CanAddItem(capacity, null, 1);
		string text = ((!flag) ? args[1] : args[0]);
		if (text.IsNullOrEmpty())
		{
			Debug.LogWarning(string.Format("IF[tag:Empty]{0}:{1}", "answer", flag));
		}
		else
		{
			base.scenario.SearchTagJumpOrOpenFile(text, base.localLine);
		}
	}
}
