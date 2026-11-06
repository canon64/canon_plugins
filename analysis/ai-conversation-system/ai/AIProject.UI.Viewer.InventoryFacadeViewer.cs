using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AIProject.SaveData;
using Illusion.Game.Extensions;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AIProject.UI.Viewer;

[Serializable]
public class InventoryFacadeViewer
{
	[Serializable]
	public class ItemFilter
	{
		[SerializeField]
		private int _category;

		[SerializeField]
		private int[] _IDs = new int[0];

		public int category
		{
			[CompilerGenerated]
			get
			{
				return _category;
			}
		}

		public int[] IDs
		{
			[CompilerGenerated]
			get
			{
				return _IDs;
			}
		}

		public ItemFilter()
		{
		}

		public ItemFilter(int category)
		{
			_category = category;
		}

		public ItemFilter(int category, int[] IDs)
		{
			_category = category;
			_IDs = IDs;
		}
	}

	public struct DoubleClickData
	{
		public int ID { get; }

		public ItemNodeUI node { get; }

		public DoubleClickData(int ID, ItemNodeUI node)
		{
			this.ID = ID;
			this.node = node;
		}
	}

	[SerializeField]
	private CanvasGroup _canvasGroup;

	[SerializeField]
	private RectTransform _parent;

	[SerializeField]
	private Sprite _iconSprite;

	[SerializeField]
	private string _iconText;

	[SerializeField]
	private InventoryViewer _viewer;

	[SerializeField]
	private Transform _sortUIPanel;

	[SerializeField]
	private bool _sorterVisible = true;

	[SerializeField]
	private bool _counterVisible = true;

	[Header("Filter Setting")]
	[SerializeField]
	private ItemFilter[] _itemFilter = new ItemFilter[0];

	public bool sorterVisible
	{
		set
		{
			_sorterVisible = value;
		}
	}

	public bool counterVisible
	{
		set
		{
			_counterVisible = value;
		}
	}

	public Action<DoubleClickData> ItemNodeOnDoubleClick { get; set; }

	public InventoryViewer viewer
	{
		[CompilerGenerated]
		get
		{
			return _viewer;
		}
	}

	public ItemFilterCategoryUI categoryUI
	{
		[CompilerGenerated]
		get
		{
			return _viewer.categoryUI;
		}
	}

	public ItemListUI itemListUI
	{
		[CompilerGenerated]
		get
		{
			return _viewer.itemListUI;
		}
	}

	public Image cursor
	{
		[CompilerGenerated]
		get
		{
			return _viewer.cursor;
		}
	}

	public ConditionalTextXtoYViewer slotCounter
	{
		[CompilerGenerated]
		get
		{
			return _viewer.slotCounter;
		}
	}

	public bool initialized { get; private set; }

	public List<StuffItem> itemList { get; private set; }

	public List<StuffItem> itemList_System { get; private set; } = new List<StuffItem>();


	public bool Visible
	{
		get
		{
			return visible.Value;
		}
		set
		{
			visible.Value = value;
		}
	}

	public CanvasGroup canvasGroup
	{
		[CompilerGenerated]
		get
		{
			return _canvasGroup;
		}
	}

	private BoolReactiveProperty visible { get; } = new BoolReactiveProperty(initialValue: true);


	public void SetItemList(List<StuffItem> itemList)
	{
		this.itemList = itemList;
	}

	public void SetItemList_System(List<StuffItem> itemList_System)
	{
		this.itemList_System = itemList_System;
	}

	public void SetItemFilter(ItemFilter[] itemFilter)
	{
		_itemFilter = itemFilter;
	}

	public void SetParent(RectTransform parent)
	{
		_parent = parent;
	}

	public virtual IEnumerator Initialize()
	{
		CanvasGroupSetting();
		if (_viewer == null)
		{
			yield return InventoryViewer.Load(_parent, delegate(InventoryViewer viewer)
			{
				_viewer = viewer;
			});
		}
		if (_viewer != null)
		{
			_viewer.ChangeTitleIcon(_iconSprite);
			_viewer.ChangeTitleText(_iconText);
			if (_sorterVisible && _sortUIPanel != null)
			{
				ItemSortUI itemSortUI = _sortUIPanel.GetComponentInChildren<ItemSortUI>();
				if (itemSortUI != null)
				{
					_viewer.SortUIBind(itemSortUI);
				}
				else
				{
					yield return _viewer.LoadSortUI(delegate(ItemSortUI sortUI)
					{
						sortUI.transform.SetParent(_sortUIPanel, worldPositionStays: false);
					});
				}
			}
			yield return _viewer.CategoryButtonAddEvent(delegate(int i)
			{
				ItemListNodeFilter(i, isSort: false);
			});
		}
		_viewer.slotCounter.visible = _counterVisible;
		_viewer.categoryUI.OnSubmit.AddListener(delegate
		{
			if (_viewer.categoryUI.SelectedButton != null)
			{
				_viewer.categoryUI.SelectedButton.onClick.Invoke();
			}
			else if (_viewer.cursor.enabled && _sorterVisible)
			{
				Vector3 position = _viewer.cursor.transform.position;
				if (position == _viewer.sorter.transform.position)
				{
					_viewer.sorter.isOn = !_viewer.sorter.isOn;
				}
				else if (position == _viewer.sortButton.transform.position)
				{
					_viewer.sortButton.onClick.Invoke();
				}
			}
		});
		if (_viewer.sortUI != null)
		{
			_viewer.sortUI.OnCancel.AddListener(delegate
			{
				if (_viewer.itemListUI.ItemVisibles.Any())
				{
					_viewer.SetFocusLevel(_viewer.itemListUI.FocusLevel);
				}
				else
				{
					_viewer.SetFocusLevel(_viewer.categoryUI.FocusLevel);
					_viewer.categoryUI.SelectedID = 0;
					_viewer.categoryUI.useCursor = true;
				}
			});
			_viewer.sortButton.OnClickAsObservable().Subscribe(delegate
			{
				if (!_viewer.sortUI.isOpen)
				{
					_viewer.sortUI.Open();
					_viewer.SetFocusLevel(_viewer.sortUI.FocusLevel);
				}
				else
				{
					_viewer.sortUI.Close();
				}
			});
			_viewer.sortUI.TypeChanged += delegate(int type)
			{
				_viewer.SortType = type;
			};
		}
		_viewer.itemListUI.OnSubmit.AddListener(delegate
		{
			_viewer.itemListUI.CurrentID = _viewer.itemListUI.SelectedID;
		});
		if (!_sorterVisible)
		{
			_viewer.sorter.isOn = true;
			_viewer.sorter.gameObject.SetActive(value: false);
			_viewer.sortButton.gameObject.SetActive(value: false);
		}
		_viewer.categoryUI.OnEntered = delegate
		{
			_viewer.SetFocusLevel(_viewer.categoryUI.FocusLevel);
		};
		Observable.Merge(new IObservable<PointerEventData>[2]
		{
			_viewer.categoryUI.leftButton.OnPointerEnterAsObservable(),
			_viewer.categoryUI.rightButton.OnPointerEnterAsObservable()
		}).Subscribe(delegate
		{
			_viewer.categoryUI.OnEntered();
		}).AddTo(_viewer);
		_viewer.itemListUI.OnEntered.Where((int _) => _viewer.IsActiveControl).Subscribe(delegate
		{
			if (_viewer.itemListUI.ItemVisibles.Any())
			{
				_viewer.SetFocusLevel(_viewer.itemListUI.FocusLevel);
			}
		});
		if (_viewer.sortUI != null)
		{
			_viewer.sortUI.OnEntered += delegate
			{
				if (_viewer.IsActiveControl && _viewer.sortUI.isOpen)
				{
					_viewer.SetFocusLevel(_viewer.sortUI.FocusLevel);
				}
			};
			_viewer.sorter.OnPointerEnterAsObservable().Subscribe(delegate
			{
				_viewer.SetCursorFocus(_viewer.sorter);
			}).AddTo(_viewer);
			_viewer.sorter.OnPointerExitAsObservable().Subscribe(delegate
			{
				_viewer.cursor.enabled = false;
			}).AddTo(_viewer);
		}
		initialized = true;
	}

	public void Refresh()
	{
		_viewer.itemListUI.Refresh();
		_viewer.slotCounter.x = itemList.Count;
	}

	public virtual void ItemListNodeCreate()
	{
		Dictionary<int, int[]> table = _itemFilter.ToLookup((ItemFilter v) => v.category, (ItemFilter v) => v.IDs).ToDictionary((IGrouping<int, int[]> v) => v.Key, (IGrouping<int, int[]> v) => v.SelectMany((int[] x) => x).ToArray());
		IEnumerable<StuffItem> source = itemList_System.Concat(itemList);
		List<StuffItem> viewList;
		if (!table.Any() || (table.Count == 1 && table.First().Key == 0))
		{
			viewList = source.ToList();
		}
		else
		{
			viewList = source.Where(delegate(StuffItem item)
			{
				if (item.CategoryID <= 0)
				{
					return true;
				}
				int[] value;
				return table.TryGetValue(item.CategoryID, out value) && (!value.Any() || value.Contains(item.ID));
			}).ToList();
		}
		_viewer.itemListUI.ClearItems();
		_viewer.categoryUI.Filter(table.Keys.ToArray());
		foreach (var item in Enumerable.Range(0, viewList.Count).Select((int i, int index) => new
		{
			item = viewList[i],
			index = index
		}))
		{
			ItemListAddNode(item.index, item.item);
		}
		int num = _viewer.categoryUI.Visibles.FirstOrDefault();
		_viewer.categoryUI.SetSelectAndCategory(num);
		ItemListNodeFilter(num, isSort: true);
	}

	public virtual ItemNodeUI ItemListAddNode(int index, StuffItem item)
	{
		ItemNodeUI node = _viewer.itemListUI.AddItemNode(index, item);
		if (node != null && ItemNodeOnDoubleClick != null)
		{
			node.OnClick.AsObservable().DoubleInterval(250f, isHot: false).Subscribe(delegate
			{
				ItemNodeOnDoubleClick?.Invoke(new DoubleClickData(index, node));
			})
				.AddTo(node);
		}
		return node;
	}

	public virtual void ItemListNodeFilter(int category, bool isSort)
	{
		_viewer.itemListUI.Filter(category);
		_viewer.itemListUI.ForceSetNonSelect();
		if (isSort)
		{
			_viewer.SortItemList();
		}
	}

	public bool AddItem(StuffItem item)
	{
		int num = item.Count;
		if (!itemList.CanAddItem(slotCounter.y, item, num, out var possible))
		{
			num = possible;
		}
		if (num <= 0)
		{
			return false;
		}
		itemList.AddItem(new StuffItem(item), num);
		item.Count -= num;
		List<StuffItem> list = itemList.FindItems(item).ToList();
		foreach (ItemNodeUI item2 in itemListUI)
		{
			list.Remove(item2.Item);
		}
		foreach (StuffItem item3 in list)
		{
			ItemListAddNode(itemListUI.SearchNotUsedIndex, item3);
		}
		ItemListNodeFilter(categoryUI.CategoryID, isSort: true);
		return item.Count <= 0;
	}

	protected virtual void CanvasGroupSetting()
	{
		if (_canvasGroup == null)
		{
			visible.Dispose();
			return;
		}
		visible.Value = !Mathf.Approximately(_canvasGroup.alpha, 0f);
		visible.Subscribe(delegate(bool isOn)
		{
			_canvasGroup.alpha = (isOn ? 1 : 0);
			_canvasGroup.blocksRaycasts = isOn;
		});
	}
}
