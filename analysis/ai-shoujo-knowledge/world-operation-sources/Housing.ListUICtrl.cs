using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AIProject;
using Battlehub.UIControls;
using Housing.Command;
using Illusion.Extensions;
using Manager;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Housing;

[Serializable]
public class ListUICtrl : UIDerived
{
	[SerializeField]
	private CanvasGroup canvasGroup;

	[SerializeField]
	[Header("一覧関係")]
	private VirtualizingTreeView virtualizingTreeView;

	[SerializeField]
	[Header("操作関係")]
	private Button buttonDelete;

	[SerializeField]
	private Button buttonDuplicate;

	[SerializeField]
	private Button buttonFolder;

	public bool Visible
	{
		get
		{
			return canvasGroup.alpha != 0f;
		}
		set
		{
			canvasGroup.Enable(value);
			if (!value)
			{
				virtualizingTreeView.SelectedIndex = -1;
			}
		}
	}

	public bool isOnFuncSkip { get; set; }

	public VirtualizingTreeView VirtualizingTreeView
	{
		[CompilerGenerated]
		get
		{
			return virtualizingTreeView;
		}
	}

	public override void Init(UICtrl _uiCtrl, bool _tutorial)
	{
		base.Init(_uiCtrl, _tutorial);
		virtualizingTreeView.ItemDataBinding += OnItemDataBinding;
		virtualizingTreeView.SelectionChanged += OnSelectionChanged;
		virtualizingTreeView.ItemsRemoving += OnItemsRemoving;
		virtualizingTreeView.ItemsRemoved += OnItemsRemoved;
		virtualizingTreeView.ItemExpanding += OnItemExpanding;
		virtualizingTreeView.ItemCollapsed += OnItemCollapsed;
		virtualizingTreeView.ItemBeginDrag += OnItemBeginDrag;
		virtualizingTreeView.ItemDrop += OnItemDrop;
		virtualizingTreeView.ItemBeginDrop += OnItemBeginDrop;
		virtualizingTreeView.ItemEndDrag += OnItemEndDrag;
		virtualizingTreeView.AutoExpand = true;
		buttonDelete.OnClickAsObservable().Subscribe(delegate
		{
			virtualizingTreeView.RemoveSelectedItems();
		});
		buttonDuplicate.OnClickAsObservable().Subscribe(delegate
		{
			Duplicate();
		});
		buttonFolder.OnClickAsObservable().Subscribe(delegate
		{
			Singleton<UndoRedoManager>.Instance.Do(new AddFolderCommand());
		});
		Selection instance = Singleton<Selection>.Instance;
		instance.onSelectFunc = (Action<ObjectCtrl[]>)Delegate.Combine(instance.onSelectFunc, new Action<ObjectCtrl[]>(OnSelectFunc));
		buttonDelete.interactable = false;
		buttonDuplicate.interactable = false;
	}

	public override void UpdateUI()
	{
		List<ObjectCtrl> objectCtrls = Singleton<Manager.Housing>.Instance.ObjectCtrls;
		virtualizingTreeView.Items = objectCtrls;
	}

	public void AddList(ObjectCtrl _objectCtrl)
	{
		virtualizingTreeView.Add(_objectCtrl);
	}

	public void RemoveList(ObjectCtrl _objectCtrl)
	{
		virtualizingTreeView.RemoveChild(null, _objectCtrl);
	}

	public void Duplicate()
	{
		IEnumerable selectedItems = virtualizingTreeView.SelectedItems;
		if (selectedItems != null)
		{
			ObjectCtrl[] array = (from v in selectedItems.OfType<ObjectCtrl>()
				where v != null
				select v).ToArray();
			if (!array.IsNullOrEmpty())
			{
				Singleton<UndoRedoManager>.Instance.Do(new DuplicateCommand(array));
			}
		}
	}

	public void UpdateList(bool _select = true)
	{
		virtualizingTreeView.SetItems(Singleton<Manager.Housing>.Instance.ObjectCtrls, _select);
	}

	public void RefreshList()
	{
		virtualizingTreeView.Refresh();
	}

	public void ClearList()
	{
		virtualizingTreeView.Items = null;
	}

	public void Select(ObjectCtrl _objectCtrl)
	{
		ExpandedLoop(_objectCtrl?.Parent);
		virtualizingTreeView.SelectedItem = _objectCtrl;
	}

	private void ExpandedLoop(ObjectCtrl _objectCtrl)
	{
		if (_objectCtrl != null)
		{
			TreeViewItemContainerData treeViewItemContainerData = (TreeViewItemContainerData)virtualizingTreeView.GetItemContainerData(_objectCtrl);
			if (treeViewItemContainerData == null)
			{
				ExpandedLoop(_objectCtrl.Parent);
			}
			else if (treeViewItemContainerData.IsExpanded)
			{
				return;
			}
			virtualizingTreeView.Expand(_objectCtrl);
		}
	}

	private void OnSelectFunc(ObjectCtrl[] _objectCtrls)
	{
		bool flag = _objectCtrls.IsNullOrEmpty();
		buttonDelete.interactable = !flag;
		buttonDuplicate.interactable = !flag;
	}

	private void OnItemDataBinding(object sender, VirtualizingTreeViewItemDataBindingArgs e)
	{
		if (e.Item is ObjectCtrl objectCtrl)
		{
			Text componentInChildren = e.ItemPresenter.GetComponentInChildren<Text>(includeInactive: true);
			componentInChildren.text = objectCtrl.Name;
			OCItem oCItem = objectCtrl as OCItem;
			componentInChildren.color = ((oCItem == null) ? Color.white : ((!oCItem.IsOverlapNow) ? Color.white : Color.red));
			OCFolder oCFolder = objectCtrl as OCFolder;
			e.HasChildren = oCFolder != null && oCFolder.Child.Count > 0;
			e.CanBeParent = oCFolder != null;
		}
	}

	private void OnSelectionChanged(object sender, SelectionChangedArgs e)
	{
		if (!e.OldItems.IsNullOrEmpty())
		{
			object[] oldItems = e.OldItems;
			foreach (object obj in oldItems)
			{
				(obj as ObjectCtrl)?.OnDeselected();
			}
		}
		ObjectCtrl[] array = (from v in e.NewItems
			select v as ObjectCtrl into v
			where v != null
			select v).ToArray();
		ObjectCtrl[] array2 = array;
		foreach (ObjectCtrl objectCtrl in array2)
		{
			objectCtrl.OnSelected();
		}
		if (Singleton<Selection>.IsInstance())
		{
			Singleton<Selection>.Instance.SetSelectObjects(array);
		}
	}

	private void OnItemsRemoving(object sender, ItemsCancelArgs e)
	{
		HashSet<ObjectCtrl> hashSet = new HashSet<ObjectCtrl>(e.Items.Select((object v) => v as ObjectCtrl));
		foreach (ObjectCtrl item in e.Items.Select((object v) => v as ObjectCtrl))
		{
			CheckParent(hashSet, item);
		}
		bool isMessage = false;
		e.Items = ((IEnumerable<object>)hashSet.Where(delegate(ObjectCtrl v)
		{
			bool flag = v.OnRemoving();
			isMessage |= !flag;
			return flag;
		})).ToList();
		if (isMessage)
		{
			MapUIContainer.PushMessageUI("アイテムがいっぱいです", 2, 1, delegate
			{
			});
		}
	}

	private void OnItemsRemoved(object sender, ItemsRemovedArgs e)
	{
		HashSet<ObjectCtrl> hashSet = new HashSet<ObjectCtrl>(e.Items.Select((object v) => v as ObjectCtrl));
		foreach (ObjectCtrl item in e.Items.Select((object v) => v as ObjectCtrl))
		{
			CheckParent(hashSet, item);
		}
		DeleteCommand deleteCommand = new DeleteCommand(hashSet.ToArray());
		if (!isOnFuncSkip)
		{
			Singleton<UndoRedoManager>.Instance.Do(deleteCommand);
		}
		else
		{
			deleteCommand.Do();
		}
		isOnFuncSkip = false;
		ObjectCtrl[] obj = virtualizingTreeView.SelectedItems?.OfType<ObjectCtrl>().ToArray();
		if (Singleton<Selection>.IsInstance())
		{
			Singleton<Selection>.Instance.onSelectFunc?.Invoke(obj);
		}
		RefreshList();
	}

	private void CheckParent(HashSet<ObjectCtrl> _objects, ObjectCtrl _objectCtrl)
	{
		if (_objectCtrl != null && _objectCtrl.Parent != null)
		{
			if (_objects.Contains(_objectCtrl.Parent))
			{
				_objects.Remove(_objectCtrl);
				RemoveTargetChild(_objects, _objectCtrl as OCFolder);
			}
			else
			{
				CheckParent(_objects, _objectCtrl.Parent);
			}
		}
	}

	private void RemoveTargetChild(HashSet<ObjectCtrl> _objects, OCFolder _ocf)
	{
		if (_ocf == null)
		{
			return;
		}
		foreach (KeyValuePair<IObjectInfo, ObjectCtrl> item in _ocf.Child)
		{
			_objects.Remove(item.Value);
			RemoveTargetChild(_objects, item.Value as OCFolder);
		}
	}

	private void OnItemExpanding(object sender, VirtualizingItemExpandingArgs e)
	{
		if (!(e.Item is OCFolder oCFolder))
		{
			return;
		}
		if (oCFolder.Child.Count > 0)
		{
			List<ObjectCtrl> source = (List<ObjectCtrl>)(e.Children = oCFolder.ChildObjectCtrls);
			e.ChildrenExpand = source.Select((ObjectCtrl v) => v.ObjectInfo is OIFolder && ((OIFolder)v.ObjectInfo).Expand);
		}
		oCFolder.OIFolder.Expand = true;
	}

	private void OnItemCollapsed(object sender, VirtualizingItemCollapsedArgs e)
	{
		if (e.Item is OCFolder oCFolder)
		{
			oCFolder.OIFolder.Expand = false;
		}
	}

	private void OnItemBeginDrag(object sender, ItemArgs e)
	{
	}

	private void OnItemDrop(object sender, ItemDropArgs args)
	{
		if (args.DropTarget != null)
		{
			Singleton<UndoRedoManager>.Instance.Do(new ListDropCommand(args));
		}
	}

	private void OnItemBeginDrop(object sender, ItemDropCancelArgs e)
	{
	}

	private void OnItemEndDrag(object sender, ItemArgs e)
	{
	}

	private List<IObjectInfo> ChildrenOf(ObjectCtrl parent)
	{
		if (parent is OCFolder oCFolder)
		{
			return oCFolder.OIFolder.Child;
		}
		if (parent != null)
		{
			return parent.CraftInfo.ObjectInfos;
		}
		return Singleton<Manager.Housing>.Instance.CraftInfo.ObjectInfos;
	}
}
