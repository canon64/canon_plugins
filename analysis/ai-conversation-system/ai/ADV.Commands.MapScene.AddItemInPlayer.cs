using System.Collections.Generic;
using AIProject;
using AIProject.SaveData;
using Manager;
using UnityEngine;

namespace ADV.Commands.MapScene;

public class AddItemInPlayer : CommandBase
{
	public override string[] ArgsLabel => new string[2] { "ItemHash", "Num" };

	public override string[] ArgsDefault => new string[2] { "-1", "1" };

	public override void Do()
	{
		base.Do();
		int num = 0;
		int num2 = int.Parse(args[num++]);
		StuffItemInfo stuffItemInfo = Singleton<Manager.Resources>.Instance.GameInfo.FindItemInfo(num2);
		if (stuffItemInfo == null)
		{
			Debug.LogError($"Item none:{num2}");
			return;
		}
		if (!int.TryParse(args[num++], out var result))
		{
			Debug.LogError(string.Format("Num none:{0}[{1}]:{2}", "args", num - 1, args[num - 1]));
			result = 1;
		}
		StuffItem addItem = new StuffItem(stuffItemInfo.CategoryID, stuffItemInfo.ID, result);
		List<StuffItem> itemList = Singleton<Map>.Instance.Player.PlayerData.ItemList;
		int inventorySlotMax = Singleton<Map>.Instance.Player.PlayerData.InventorySlotMax;
		if (AddItem(itemList, inventorySlotMax, addItem, result))
		{
			Debug.Log($"AddItem(Inventory):{stuffItemInfo.Name}x{result}");
			return;
		}
		itemList = Singleton<Manager.Game>.Instance.Environment.ItemListInStorage;
		inventorySlotMax = Singleton<Manager.Resources>.Instance.DefinePack.ItemBoxCapacityDefines.StorageCapacity;
		if (AddItem(itemList, inventorySlotMax, addItem, result))
		{
			Debug.Log($"AddItem(ItemBox):{stuffItemInfo.Name}x{result}");
		}
		else
		{
			Debug.LogError($"AddItem(Failed):{stuffItemInfo.Name}x{result}");
		}
	}

	private bool AddItem(List<StuffItem> itemList, int capacity, StuffItem addItem, int num)
	{
		if (!itemList.CanAddItem(capacity, addItem, num))
		{
			return false;
		}
		itemList.AddItem(addItem);
		return true;
	}
}
