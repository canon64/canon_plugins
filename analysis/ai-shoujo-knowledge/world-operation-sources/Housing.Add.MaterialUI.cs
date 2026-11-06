using System;
using System.Runtime.CompilerServices;
using AIProject;
using AIProject.SaveData;
using Illusion.Extensions;
using Manager;
using UnityEngine;
using UnityEngine.UI;

namespace Housing.Add;

public class MaterialUI : MonoBehaviour
{
	[Serializable]
	public class MaterialInfo
	{
		public GameObject gameObject;

		public Text textName;

		public Text textNum;

		public Text textNeed;

		public bool Active
		{
			set
			{
				gameObject.SetActiveIfDifferent(value);
			}
		}

		public bool Set(string _name, int _num, int _need)
		{
			bool flag = _num < _need;
			gameObject.SetActiveIfDifferent(active: true);
			textName.text = _name;
			textNum.text = ((!flag) ? _num.ToString() : $"<color=red>{_num}</color>");
			textNeed.text = $"/{_need}";
			return !flag;
		}
	}

	[SerializeField]
	private MaterialInfo[] materialInfos;

	public MaterialInfo[] MaterialInfos
	{
		[CompilerGenerated]
		get
		{
			return materialInfos;
		}
	}

	public bool UpdateUI(Manager.Housing.LoadInfo _loadInfo)
	{
		bool flag = true;
		if (_loadInfo.requiredMaterials.IsNullOrEmpty())
		{
			MaterialInfo[] array = materialInfos;
			foreach (MaterialInfo materialInfo in array)
			{
				materialInfo.Active = false;
			}
			return true;
		}
		for (int j = 0; j < materialInfos.Length; j++)
		{
			Manager.Housing.RequiredMaterial requiredMaterial = _loadInfo.requiredMaterials.SafeGet(j);
			if (requiredMaterial == null)
			{
				materialInfos[j].Active = false;
				continue;
			}
			StuffItemInfo item = Singleton<Manager.Resources>.Instance.GameInfo.GetItem(requiredMaterial.category, requiredMaterial.no);
			if (item == null)
			{
				materialInfos[j].Active = false;
				continue;
			}
			int num = 0;
			StuffItem item2 = new StuffItem(requiredMaterial.category, requiredMaterial.no, 0);
			StuffItem stuffItem = Singleton<Map>.Instance.Player.PlayerData.ItemList.FindItem(item2);
			if (stuffItem != null)
			{
				num += stuffItem.Count;
			}
			stuffItem = Singleton<Game>.Instance.Environment.ItemListInStorage.FindItem(item2);
			if (stuffItem != null)
			{
				num += stuffItem.Count;
			}
			flag &= materialInfos[j].Set(item.Name, num, requiredMaterial.num);
			materialInfos[j].Active = true;
		}
		return flag;
	}
}
