using System;
using AIProject.SaveData;
using AIProject.Scene;
using Manager;
using UniRx;
using UnityEngine;

namespace AIProject;

public class FarmSection : MonoBehaviour
{
	private AIProject.SaveData.Environment.PlantInfo _plantInfo;

	private PlantItem _plantItem;

	public int HarvestID { get; set; }

	public int SectionID { get; set; }

	private void Start()
	{
		(from _ in Observable.EveryUpdate().TakeUntilDestroy(this)
			where base.isActiveAndEnabled
			select _).Do(delegate
		{
			OnUpdate();
		}, delegate(Exception ex)
		{
			Debug.LogException(ex);
		}).OnErrorRetry().Subscribe(delegate
		{
		});
	}

	private void OnUpdate()
	{
		if (!Singleton<Game>.Instance.Environment.FarmlandTable.TryGetValue(HarvestID, out var value))
		{
			return;
		}
		AIProject.SaveData.Environment.PlantInfo element = value.GetElement(SectionID);
		if (element == null)
		{
			if (_plantInfo != null)
			{
				_plantInfo = null;
				if (_plantItem != null)
				{
					UnityEngine.Object.Destroy(_plantItem.gameObject);
					_plantItem = null;
				}
			}
			return;
		}
		if (_plantInfo != element)
		{
			_plantInfo = element;
			StuffItemInfo seedInfo = Singleton<Manager.Resources>.Instance.GameInfo.FindItemInfo(element.nameHash);
			ItemInfo itemInfo = Singleton<Manager.Resources>.Instance.Map.PlantIvyFilterList.Find((ItemInfo x) => x.CategoryID == seedInfo.CategoryID && x.ItemID == seedInfo.ID);
			if (Singleton<Manager.Resources>.Instance.Map.PlantItemList.TryGetValue(itemInfo.ObjID, out var value2))
			{
				GameObject gameObject = CommonLib.LoadAsset<GameObject>(value2.assetbundle, value2.asset, clone: false, value2.manifest);
				MapScene.AddAssetBundlePath(value2.assetbundle, value2.manifest);
				if (gameObject != null)
				{
					if (_plantItem != null)
					{
						UnityEngine.Object.Destroy(_plantItem.gameObject);
						_plantItem = null;
					}
					_plantItem = UnityEngine.Object.Instantiate(gameObject, base.transform, worldPositionStays: false).GetComponent<PlantItem>();
					_plantItem.ChangeState(0);
				}
			}
		}
		if (_plantItem != null)
		{
			float num = element.timer / (float)element.timeLimit;
			if (num < 0.5f)
			{
				_plantItem.ChangeState(0);
			}
			else if (num < 1f)
			{
				_plantItem.ChangeState(1);
			}
			else
			{
				_plantItem.ChangeState(2);
			}
		}
	}
}
