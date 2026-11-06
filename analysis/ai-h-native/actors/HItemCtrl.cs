using System;
using System.Collections.Generic;
using Illusion;
using IllusionUtility.GetUtility;
using Manager;
using UnityEngine;
using UnityEx;

public class HItemCtrl
{
	public class ChildInfo
	{
		public GameObject objChild;

		public Transform transChild;

		public Transform oldParent;
	}

	public class Item
	{
		public string itemName;

		public GameObject objItem;

		public Transform transItem;

		public Animator animItem;

		public List<ChildInfo> lstChild = new List<ChildInfo>();
	}

	public class ParentInfo
	{
		public bool isParentMode;

		public int numToWhomParent;

		public string nameParent;

		public string nameSelf;

		public bool isParentScale;
	}

	public class ListItem
	{
		public string Name;

		public int itemkind;

		public int itemID;

		public string nameManifest;

		public string pathAssetObject;

		public string nameObject;

		public string pathAssetAnimatorBase;

		public string nameAnimatorBase;

		public string pathAssetAnimator;

		public string nameAnimator;

		public List<ParentInfo> lstParent = new List<ParentInfo>();
	}

	private List<Item> lstItem = new List<Item>();

	private List<Dictionary<int, List<ListItem>>>[] lstParent = new List<Dictionary<int, List<ListItem>>>[6]
	{
		new List<Dictionary<int, List<ListItem>>>(),
		new List<Dictionary<int, List<ListItem>>>(),
		new List<Dictionary<int, List<ListItem>>>(),
		new List<Dictionary<int, List<ListItem>>>(),
		new List<Dictionary<int, List<ListItem>>>(),
		new List<Dictionary<int, List<ListItem>>>()
	};

	private List<UnityEx.ValueTuple<Transform, Transform, bool>> itemObj = new List<UnityEx.ValueTuple<Transform, Transform, bool>>();

	private Transform hitemPlace;

	private List<UnityEx.ValueTuple<string, RuntimeAnimatorController>> BaseRacs = new List<UnityEx.ValueTuple<string, RuntimeAnimatorController>>();

	private RuntimeAnimatorController[] rac = new RuntimeAnimatorController[2];

	public void HItemInit(Transform _hitemPlace)
	{
		lstParent = Singleton<Manager.Resources>.Instance.HSceneTable.lstHItemObjInfo;
		BaseRacs = Singleton<Manager.Resources>.Instance.HSceneTable.lstHItemBase;
		hitemPlace = _hitemPlace;
	}

	public bool LoadItem(int _mode, int _id, GameObject _boneMale, GameObject _boneFemale, GameObject _boneMale1, GameObject _boneFemale1)
	{
		ReleaseItem();
		List<ListItem> list = null;
		itemObj.Clear();
		for (int i = 0; i < lstParent[_mode].Count; i++)
		{
			if (!lstParent[_mode][i].ContainsKey(_id))
			{
				continue;
			}
			list = lstParent[_mode][i][_id];
			foreach (ListItem item2 in list)
			{
				Item item = new Item();
				bool flag = false;
				if (GlobalMethod.AssetFileExist(item2.pathAssetObject, item2.nameObject, item2.nameManifest))
				{
					item.itemName = item2.Name;
					item.objItem = CommonLib.LoadAsset<GameObject>(item2.pathAssetObject, item2.nameObject, clone: true, item2.nameManifest);
					item.transItem = item.objItem.transform;
					Singleton<HSceneManager>.Instance.hashUseAssetBundle.Add(item2.pathAssetObject);
					LoadAnimation(item, item2);
					lstItem.Add(item);
				}
			}
		}
		if (list == null)
		{
			return false;
		}
		for (int j = 0; j < list.Count && lstItem.Count > j; j++)
		{
			if (lstItem[j].objItem == null)
			{
				continue;
			}
			foreach (ParentInfo item3 in list[j].lstParent)
			{
				GameObject gameObject = null;
				if (item3.numToWhomParent == 0)
				{
					if (_boneMale != null)
					{
						gameObject = _boneMale.transform.FindLoop(item3.nameParent);
					}
				}
				else if (item3.numToWhomParent == 1)
				{
					if (_boneFemale != null)
					{
						gameObject = _boneFemale.transform.FindLoop(item3.nameParent);
					}
				}
				else if (item3.numToWhomParent == 2)
				{
					if (_boneMale1 != null)
					{
						gameObject = _boneMale1.transform.FindLoop(item3.nameParent);
					}
				}
				else if (item3.numToWhomParent == 3)
				{
					if (_boneFemale1 != null)
					{
						gameObject = _boneFemale1.transform.FindLoop(item3.nameParent);
					}
				}
				else
				{
					int num = item3.numToWhomParent - 4;
					if (lstItem.Count > num && (bool)lstItem[num].objItem)
					{
						gameObject = lstItem[num].transItem.FindLoop(item3.nameParent);
					}
				}
				GameObject gameObject2 = null;
				gameObject2 = ((!(item3.nameSelf != string.Empty)) ? lstItem[j].objItem : lstItem[j].transItem.FindLoop(item3.nameSelf));
				if (!(gameObject == null) && !(gameObject2 == null))
				{
					ChildInfo childInfo = new ChildInfo();
					childInfo.objChild = gameObject2;
					childInfo.transChild = gameObject2.transform;
					childInfo.oldParent = gameObject2.transform.parent;
					lstItem[j].lstChild.Add(childInfo);
					if (item3.isParentMode)
					{
						childInfo.transChild.SetParent(gameObject.transform, worldPositionStays: false);
						childInfo.transChild.localPosition = Vector3.zero;
						childInfo.transChild.localRotation = Quaternion.identity;
					}
					else
					{
						childInfo.transChild.SetParent(hitemPlace, worldPositionStays: false);
						childInfo.transChild.position = gameObject.transform.position;
						childInfo.transChild.rotation = gameObject.transform.rotation;
					}
					if (!item3.isParentScale)
					{
						itemObj.Add(new UnityEx.ValueTuple<Transform, Transform, bool>(childInfo.transChild, gameObject.transform, item3.isParentMode));
					}
				}
			}
		}
		GC.Collect();
		return true;
	}

	public void ParentScaleReject()
	{
		if (itemObj.Count <= 0)
		{
			return;
		}
		for (int i = 0; i < itemObj.Count; i++)
		{
			if (itemObj[i].Item3)
			{
				Vector3 lossyScale = itemObj[i].Item1.lossyScale;
				Vector3 localScale = itemObj[i].Item1.localScale;
				itemObj[i].Item1.localScale = new Vector3(localScale.x / lossyScale.x, localScale.y / lossyScale.y, localScale.z / lossyScale.z);
			}
			else
			{
				itemObj[i].Item1.position = itemObj[i].Item2.position;
				itemObj[i].Item1.rotation = itemObj[i].Item2.rotation;
			}
		}
	}

	public bool ReleaseItem()
	{
		for (int i = 0; i < lstItem.Count; i++)
		{
			if (lstItem[i].objItem == null)
			{
				continue;
			}
			for (int j = 0; j < lstItem[i].lstChild.Count; j++)
			{
				ChildInfo childInfo = lstItem[i].lstChild[j];
				if ((bool)childInfo.objChild && (bool)childInfo.oldParent)
				{
					childInfo.transChild.SetParent(childInfo.oldParent, worldPositionStays: false);
				}
			}
			UnityEngine.Object.Destroy(lstItem[i].objItem);
			lstItem[i].objItem = null;
			lstItem[i].animItem = null;
		}
		lstItem.Clear();
		itemObj.Clear();
		return true;
	}

	public bool setTransform(Transform _transform)
	{
		if (_transform == null)
		{
			return false;
		}
		foreach (Item item in lstItem)
		{
			if (!(item.objItem == null) && !(item.transItem.parent != null))
			{
				item.transItem.position = _transform.position;
				item.transItem.rotation = _transform.rotation;
			}
		}
		return true;
	}

	public bool setTransform(Vector3 pos, Vector3 rot)
	{
		foreach (Item item in lstItem)
		{
			if (!(item.objItem == null) && !(item.transItem.parent != null))
			{
				item.transItem.position = pos;
				item.transItem.rotation = Quaternion.Euler(rot);
			}
		}
		return true;
	}

	public void syncPlay(AnimatorStateInfo _ai)
	{
		foreach (Item item in lstItem)
		{
			if (!(item.animItem == null))
			{
				item.animItem.Play(_ai.shortNameHash, 0, _ai.normalizedTime);
			}
		}
	}

	public void syncPlay(int _nameHash, float _fnormalizedTime)
	{
		foreach (Item item in lstItem)
		{
			if (!(item.animItem == null))
			{
				item.animItem.Play(_nameHash, 0, _fnormalizedTime);
			}
		}
	}

	public void Update()
	{
		foreach (Item item in lstItem)
		{
			if (!(item.animItem == null))
			{
				item.animItem.Update(0f);
			}
		}
	}

	public void setPlay(string _strAnmName)
	{
		foreach (Item item in lstItem)
		{
			if (!(item.animItem == null))
			{
				item.animItem.Play(_strAnmName, 0);
			}
		}
	}

	public void setPlay(string _strAnmName, float normalizeTime)
	{
		foreach (Item item in lstItem)
		{
			if (!(item.animItem == null))
			{
				item.animItem.Play(_strAnmName, 0, normalizeTime);
			}
		}
	}

	public bool setPlay(string _strAnmName, int _nObj)
	{
		if (lstItem.Count <= _nObj)
		{
			return false;
		}
		if (lstItem[_nObj].animItem == null)
		{
			return false;
		}
		lstItem[_nObj].animItem.Play(_strAnmName, 0);
		return true;
	}

	public void setAnimatorParamTrigger(string _strAnmName)
	{
		foreach (Item item in lstItem)
		{
			if (!(item.animItem == null))
			{
				item.animItem.SetTrigger(_strAnmName);
			}
		}
	}

	public void setAnimatorParamResetTrigger(string _strAnmName)
	{
		foreach (Item item in lstItem)
		{
			if (!(item.animItem == null))
			{
				item.animItem.ResetTrigger(_strAnmName);
			}
		}
	}

	public void setAnimatorParamBool(string _strAnmName, bool _bFlag)
	{
		foreach (Item item in lstItem)
		{
			if (!(item.animItem == null))
			{
				item.animItem.SetBool(_strAnmName, _bFlag);
			}
		}
	}

	public void setAnimatorParamFloat(string _strAnmName, float _fValue)
	{
		foreach (Item item in lstItem)
		{
			if (!(item.animItem == null))
			{
				item.animItem.SetFloat(_strAnmName, _fValue);
			}
		}
	}

	public GameObject GetItem()
	{
		if (lstItem.Count < 1)
		{
			return null;
		}
		return lstItem[0].objItem;
	}

	public List<Item> GetItems()
	{
		return lstItem;
	}

	public List<Dictionary<int, List<ListItem>>>[] GetListItemInfos()
	{
		return lstParent;
	}

	private bool LoadAnimation(Item _item, ListItem _info)
	{
		if (_item.objItem == null)
		{
			return false;
		}
		_item.animItem = _item.objItem.GetComponent<Animator>();
		if (_item.animItem == null)
		{
			_item.animItem = _item.objItem.GetComponentInChildren<Animator>();
			if (_item.animItem == null)
			{
				return false;
			}
		}
		if (_info.pathAssetAnimator.IsNullOrEmpty() || _info.nameAnimator.IsNullOrEmpty())
		{
			_item.animItem = null;
			return false;
		}
		foreach (UnityEx.ValueTuple<string, RuntimeAnimatorController> baseRac in BaseRacs)
		{
			if (!(baseRac.Item1 != _info.nameAnimatorBase))
			{
				rac[0] = baseRac.Item2;
			}
		}
		_item.animItem.runtimeAnimatorController = rac[0];
		if (_item.animItem.runtimeAnimatorController == null)
		{
			_item.animItem = null;
		}
		rac[1] = CommonLib.LoadAsset<RuntimeAnimatorController>(_info.pathAssetAnimator, _info.nameAnimator, clone: false, string.Empty);
		_item.animItem.runtimeAnimatorController = Illusion.Utils.Animator.SetupAnimatorOverrideController(_item.animItem.runtimeAnimatorController, rac[1]);
		return true;
	}
}
