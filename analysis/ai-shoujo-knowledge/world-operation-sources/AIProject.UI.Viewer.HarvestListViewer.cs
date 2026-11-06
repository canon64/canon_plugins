using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AIProject.SaveData;
using AIProject.Scene;
using Manager;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.UI;
using UnityEx;

namespace AIProject.UI.Viewer;

public class HarvestListViewer : MonoBehaviour
{
	[SerializeField]
	private ItemListUI _itemListUI;

	[SerializeField]
	private Text _infoText;

	[SerializeField]
	private Button _allGetButton;

	[SerializeField]
	private Button _getButton;

	[SerializeField]
	private Image _cursor;

	public ICollection<StuffItem> AddFailedList { get; } = new List<StuffItem>();

	public bool initialized { get; private set; }

	public ItemListUI itemListUI
	{
		[CompilerGenerated]
		get
		{
			return _itemListUI;
		}
	}

	private ReactiveCollection<StuffItem> harvestList { get; } = new ReactiveCollection<StuffItem>();

	private int capacity
	{
		[CompilerGenerated]
		get
		{
			return Singleton<Map>.Instance.Player.PlayerData.InventorySlotMax;
		}
	}

	private List<StuffItem> storage
	{
		[CompilerGenerated]
		get
		{
			return Singleton<Map>.Instance.Player.PlayerData.ItemList;
		}
	}

	private BoolReactiveProperty inStorage { get; } = new BoolReactiveProperty();

	private CompositeDisposable disposables { get; } = new CompositeDisposable();

	public event Action<IReadOnlyCollection<StuffItem>> GetItemAction;

	public event Action AllGetListAction;

	public event Action<IReadOnlyCollection<StuffItem>> SyncListAction;

	public void Refresh()
	{
		_itemListUI.ForceSetNonSelect();
	}

	public void AddList(StuffItem item)
	{
		harvestList.AddItem(item);
		_itemListUI.AddItemNode(_itemListUI.SearchNotUsedIndex, item);
	}

	public void ClearList()
	{
		harvestList.Clear();
		_itemListUI.ClearItems();
	}

	public bool InStorageCheck(StuffItem item)
	{
		List<StuffItem> self = storage;
		int num = capacity;
		int? num2 = item?.Count;
		int possible;
		bool result = self.CanAddItem(num, item, num2.HasValue ? num2.Value : 0, out possible);
		if (item != null)
		{
			storage.CanAddItem(capacity, null, 0, out possible);
		}
		inStorage.Value = possible > 0;
		return result;
	}

	private IEnumerator Start()
	{
		while (!Singleton<Manager.Resources>.IsInstance())
		{
			yield return null;
		}
		while (!Singleton<Game>.IsInstance() || Singleton<Game>.Instance.Environment == null)
		{
			yield return null;
		}
		if (!_itemListUI.isOptionNode)
		{
			string bundle = Singleton<Manager.Resources>.Instance.DefinePack.ABPaths.MapScenePrefab;
			GameObject gameObject = CommonLib.LoadAsset<GameObject>(bundle, "ItemOption_new", clone: false, string.Empty);
			if (gameObject == null)
			{
				yield break;
			}
			if (!MapScene.AssetBundlePaths.Exists((UnityEx.ValueTuple<string, string> x) => x.Item1 == bundle))
			{
				MapScene.AssetBundlePaths.Add(new UnityEx.ValueTuple<string, string>(bundle, string.Empty));
			}
			_itemListUI.SetOptionNode(gameObject);
		}
		while (!Singleton<Map>.IsInstance() || Singleton<Map>.Instance.Player == null || Singleton<Map>.Instance.Player.PlayerData == null)
		{
			yield return null;
		}
		_itemListUI.CurrentChanged += delegate(int _, ItemNodeUI option)
		{
			bool flag = option != null;
			StuffItem stuffItem = (flag ? option.Item : null);
			flag &= InStorageCheck(stuffItem);
			if (stuffItem != null)
			{
				flag |= Singleton<Manager.Resources>.Instance.GameInfo.GetItem(stuffItem.CategoryID, stuffItem.ID) == null;
			}
			_getButton.interactable = flag;
		};
		inStorage.Subscribe(delegate(bool success)
		{
			_infoText.enabled = !success;
		}).AddTo(disposables);
		_allGetButton.OnClickAsObservable().Subscribe(delegate
		{
			Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_S);
			if (this.GetItemAction != null)
			{
				this.GetItemAction(harvestList);
			}
			foreach (StuffItem item2 in harvestList.ToList())
			{
				if (!AddFailedList.Contains(item2))
				{
					harvestList.Remove(item2);
					_itemListUI.RemoveItemNode(_itemListUI.SearchIndex(item2));
				}
			}
			_itemListUI.Refresh();
			if (_itemListUI.SearchIndex(_itemListUI.CurrentOption) == -1)
			{
				_itemListUI.ForceSetNonSelect();
			}
			AddFailedList.Clear();
			if (this.SyncListAction != null)
			{
				this.SyncListAction(harvestList);
			}
		}).AddTo(disposables);
		(from _ in _getButton.OnClickAsObservable()
			where _itemListUI.CurrentOption != null
			select _).Subscribe(delegate
		{
			Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_S);
			StuffItem item = _itemListUI.CurrentOption.Item;
			if (this.GetItemAction != null)
			{
				this.GetItemAction((IReadOnlyCollection<StuffItem>)(object)new StuffItem[1] { item });
			}
			if (!AddFailedList.Contains(item))
			{
				harvestList.Remove(item);
				_itemListUI.RemoveItemNode(_itemListUI.CurrentID);
				_itemListUI.ForceSetNonSelect();
			}
			AddFailedList.Clear();
			if (this.SyncListAction != null)
			{
				this.SyncListAction(harvestList);
			}
		}).AddTo(disposables);
		(from _ in harvestList.ObserveRemove()
			where !harvestList.Any()
			select _).Subscribe(delegate
		{
			if (this.AllGetListAction != null)
			{
				this.AllGetListAction();
			}
		});
		this.UpdateAsObservable().Subscribe(delegate
		{
			_allGetButton.interactable = harvestList.Any();
		}).AddTo(disposables);
		initialized = true;
	}

	private void OnDestroy()
	{
		disposables.Clear();
	}
}
