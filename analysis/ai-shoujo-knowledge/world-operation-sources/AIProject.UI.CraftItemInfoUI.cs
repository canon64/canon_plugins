using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using AIProject.ColorDefine;
using AIProject.SaveData;
using AIProject.Scene;
using AIProject.UI.Viewer;
using Manager;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.UI;
using UnityEx;

namespace AIProject.UI;

public class CraftItemInfoUI : ItemInfoUI
{
	[SerializeField]
	private CraftUI _craftUI;

	[SerializeField]
	private RectTransform _warningViewerLayout;

	[SerializeField]
	private WarningViewer _warningViewer;

	private IDisposable _storageDisposable;

	private StuffItem _item;

	public int createSum
	{
		set
		{
			_createSum.SetValueAndForceNotify(value);
		}
	}

	private string itemName { get; set; } = string.Empty;

	private IntReactiveProperty _createSum { get; } = new IntReactiveProperty(0);

	private BoolReactiveProperty _active { get; } = new BoolReactiveProperty(initialValue: false);

	public IReadOnlyCollection<StuffItem> targetStorage { get; private set; }

	private IReadOnlyCollection<IReadOnlyCollection<StuffItem>> checkStorages
	{
		[CompilerGenerated]
		get
		{
			return _craftUI.checkStorages;
		}
	}

	public static IEnumerator Load(CraftUI craftUI, Transform viewerParent, Action<CraftItemInfoUI> onComplete)
	{
		while (!Singleton<Manager.Resources>.IsInstance())
		{
			yield return null;
		}
		string bundle = Singleton<Manager.Resources>.Instance.DefinePack.ABPaths.MapScenePrefab;
		GameObject asset = CommonLib.LoadAsset<GameObject>(bundle, "CraftItemInfoUI", clone: false, string.Empty);
		if (!MapScene.AssetBundlePaths.Exists((UnityEx.ValueTuple<string, string> x) => x.Item1 == bundle))
		{
			MapScene.AssetBundlePaths.Add(new UnityEx.ValueTuple<string, string>(bundle, string.Empty));
		}
		CraftItemInfoUI infoUI = UnityEngine.Object.Instantiate(asset, viewerParent, worldPositionStays: false).GetComponent<CraftItemInfoUI>();
		infoUI._craftUI = craftUI;
		onComplete.Call(infoUI);
	}

	public override void Refresh(int count)
	{
		base.Refresh(count);
		_active.Value = count > 0;
	}

	public override void Refresh(StuffItem item)
	{
		_item = item;
		StuffItemInfo item2 = Singleton<Manager.Resources>.Instance.GameInfo.GetItem(item.CategoryID, item.ID);
		itemName = item2.Name;
		_createSum.SetValueAndForceNotify(0);
		_flavorText.text = item2.Explanation;
		Refresh(item.Count);
	}

	public override void Close()
	{
		_item = null;
		Refresh(0);
		base.Close();
	}

	protected override void Start()
	{
		base.Start();
		if (_infoLayout != null)
		{
			_infoLayout.SetActive(value: true);
		}
		_active.Value = base.isOpen;
		_active.Subscribe(delegate(bool active)
		{
			base.isCountViewerVisible = active;
		});
		_active.Subscribe(delegate(bool active)
		{
			_submitButton.gameObject.SetActive(active);
		});
		_createSum.SubscribeToText(_itemName, delegate(int i)
		{
			string arg = string.Empty;
			if (i > 0)
			{
				arg = $" x {i}";
			}
			string text2 = itemName;
			if (text2.IsNullOrEmpty())
			{
				arg = string.Empty;
			}
			return $"{text2}{arg}";
		});
		_storageDisposable = Observable.FromCoroutine((CancellationToken _) => LoadStorageType()).Subscribe();
		Text text = _submitButton.GetComponentInChildren<Text>();
		Color baseTextColor = text.color;
		_submitButton.OnPointerEnterAsObservable().Subscribe(delegate
		{
			text.color = Define.Get(Colors.Orange);
		});
		_submitButton.OnPointerExitAsObservable().Subscribe(delegate
		{
			text.color = baseTextColor;
		});
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (_storageDisposable != null)
		{
			_storageDisposable.Dispose();
		}
		_storageDisposable = null;
		_active.Dispose();
	}

	private IEnumerator LoadStorageType()
	{
		yield return LoadWarningViewer();
		while (!Singleton<Map>.IsInstance() || Singleton<Map>.Instance.Player == null || Singleton<Map>.Instance.Player.PlayerData == null)
		{
			yield return null;
		}
		while (!Singleton<Game>.IsInstance() || Singleton<Game>.Instance.Environment == null)
		{
			yield return null;
		}
		while (!Singleton<Manager.Resources>.IsInstance() || Singleton<Manager.Resources>.Instance.DefinePack == null || Singleton<Manager.Resources>.Instance.DefinePack.ItemBoxCapacityDefines == null)
		{
			yield return null;
		}
		_warningViewer.msgID = 1;
		_countViewer.Counter.Where((int _) => _item != null).Subscribe(delegate(int count)
		{
			EmptySlotCheck(count);
		});
	}

	private IEnumerator LoadWarningViewer()
	{
		if (!(_warningViewer != null))
		{
			yield return WarningViewer.Load(_warningViewerLayout, delegate(WarningViewer warningViewer)
			{
				_warningViewer = warningViewer;
			});
		}
	}

	private void EmptySlotCheck(int count)
	{
		count *= _createSum.Value;
		targetStorage = null;
		bool flag = false;
		if (count <= 0)
		{
			flag = true;
		}
		else
		{
			foreach (IReadOnlyCollection<StuffItem> checkStorage in checkStorages)
			{
				int capacity;
				if (checkStorage == Singleton<Map>.Instance.Player.PlayerData.ItemList)
				{
					capacity = Singleton<Map>.Instance.Player.PlayerData.InventorySlotMax;
				}
				else
				{
					if (checkStorage != Singleton<Game>.Instance.Environment.ItemListInStorage)
					{
						if (checkStorage != Singleton<Game>.Instance.Environment.ItemListInPantry)
						{
							Debug.LogError("StorageType not found");
						}
						continue;
					}
					capacity = Singleton<Manager.Resources>.Instance.DefinePack.ItemBoxCapacityDefines.StorageCapacity;
				}
				if (IsStrageIn(capacity, count, checkStorage))
				{
					targetStorage = checkStorage;
					flag = true;
					break;
				}
			}
		}
		_warningViewer.visible = !flag;
		_submitButton.interactable = flag;
	}

	private bool IsStrageIn(int capacity, int count, IReadOnlyCollection<StuffItem> storage)
	{
		int itemSlotMax = Singleton<Manager.Resources>.Instance.DefinePack.MapDefines.ItemSlotMax;
		StuffItem[] array = storage.FindItems(_item);
		foreach (StuffItem stuffItem in array)
		{
			int num = itemSlotMax - stuffItem.Count;
			int a = count - num;
			count = Mathf.Max(a, 0);
			if (count <= 0)
			{
				break;
			}
		}
		int num2 = count / itemSlotMax;
		num2 += ((count % itemSlotMax > 0) ? 1 : 0);
		bool flag = capacity - storage.Count - num2 < 0;
		return !flag;
	}

	private new void SetFocusLevel(int level)
	{
		Singleton<Manager.Input>.Instance.FocusLevel = level;
	}
}
