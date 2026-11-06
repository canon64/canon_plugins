using System.Collections.Generic;
using AIProject;
using AIProject.SaveData;
using Manager;
using UnityEngine;

namespace ADV.Commands.MapScene;

internal static class Extension
{
	public static bool AddItemVars(this TextScenario self, StuffItem Item)
	{
		Dictionary<string, ValData> vars = self.Vars;
		string text = "Item";
		StuffItemInfo stuffItemInfo = null;
		if (Item != null && Singleton<Manager.Resources>.IsInstance())
		{
			stuffItemInfo = Singleton<Manager.Resources>.Instance.GameInfo.GetItem(Item.CategoryID, Item.ID);
		}
		vars[text] = new ValData(stuffItemInfo?.Name ?? string.Empty);
		string key = $"{text}.Hash";
		int? num = stuffItemInfo?.nameHash;
		vars[key] = new ValData((!num.HasValue) ? (-1) : num.Value);
		if (Item == null)
		{
			return false;
		}
		vars[string.Format("{0}.{1}", text, "CategoryID")] = new ValData(Item.CategoryID);
		vars[string.Format("{0}.{1}", text, "ID")] = new ValData(Item.ID);
		vars[string.Format("{0}.{1}", text, "Count")] = new ValData(Item.Count);
		return true;
	}

	public static bool AddItemVars(this TextScenario self, StuffItemInfo Item, int Count)
	{
		Dictionary<string, ValData> vars = self.Vars;
		string text = "Item";
		vars[text] = new ValData(Item?.Name ?? string.Empty);
		string key = $"{text}.Hash";
		int? num = Item?.nameHash;
		vars[key] = new ValData((!num.HasValue) ? (-1) : num.Value);
		if (Item == null)
		{
			return false;
		}
		vars[string.Format("{0}.{1}", text, "CategoryID")] = new ValData(Item.CategoryID);
		vars[string.Format("{0}.{1}", text, "ID")] = new ValData(Item.ID);
		vars[string.Format("{0}.{1}", text, "Count")] = new ValData(Count);
		return true;
	}

	public static CharaData GetChara(this CommandBase self, ref int cnt)
	{
		int num = cnt++;
		if (self.args.IsNullOrEmpty(num))
		{
			Debug.LogError(string.Format("Arg none:{0}[{1}]", "args", num));
			return null;
		}
		CharaData result = null;
		if (!int.TryParse(self.args[num], out var result2) || (result = self.scenario.commandController.GetChara(result2)) == null)
		{
			Debug.LogError(string.Format("Chara none:{0}({1}[{2}]:{3})", result2, "args", num, self.args[num]));
		}
		return result;
	}
}
