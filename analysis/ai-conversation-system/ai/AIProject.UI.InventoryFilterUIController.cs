using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AIProject.SaveData;
using AIProject.Scene;
using AIProject.UI.Viewer;
using Manager;
using ReMotion;
using UniRx;
using UnityEngine;
using UnityEx;

namespace AIProject.UI;

public abstract class InventoryFilterUIController : MenuUIBehaviour
{
	[Serializable]
	public class InventoryUI : InventoryFacadeViewer
	{
		public InventoryFilterUIController owner { get; private set; }

		public void SetOwner(InventoryFilterUIController owner)
		{
			this.owner = owner;
			base.viewer.SetParentMenuUI(owner);
			base.viewer.setFocusLevel = delegate(int level)
			{
				owner.SetFocusLevel(level);
			};
			base.categoryUI.OnCancel.AddListener(delegate
			{
				owner.OnInputCancel();
			});
			base.itemListUI.OnCancel.AddListener(delegate
			{
				owner.OnInputCancel();
			});
		}

		public override void ItemListNodeCreate()
		{
			SetItemList(owner.itemList());
			SetItemList_System(owner.itemList_System?.Invoke() ?? new List<StuffItem>());
			base.ItemListNodeCreate();
			base.viewer.sortUI.SetDefault();
			base.viewer.sortUI.Close();
			base.viewer.sorter.isOn = true;
			Refresh();
		}

		public override void ItemListNodeFilter(int category, bool isSort)
		{
			base.ItemListNodeFilter(category, isSort);
			owner._itemInfoUI.Close();
		}
	}

	[SerializeField]
	private CanvasGroup _canvasGroup;

	[SerializeField]
	protected ItemInfoUI _itemInfoUI;

	[SerializeField]
	protected InventoryUI _inventoryUI;

	private MenuUIBehaviour[] _menuUIList;

	private IDisposable _fadeDisposable;

	private IDisposable _emptyTextUpdateDisposable;

	public PlaySE playSE { get; } = new PlaySE(use: false);


	public virtual Func<int> capacity { get; set; }

	public Func<List<StuffItem>> itemList { get; set; }

	public Func<List<StuffItem>> itemList_System { get; set; }

	public SystemMenuUI Observer { get; set; }

	public Action OnClose { get; set; }

	protected ItemListUI itemListUI
	{
		[CompilerGenerated]
		get
		{
			return _inventoryUI.viewer.itemListUI;
		}
	}

	protected MenuUIBehaviour[] MenuUIList => this.GetCache(ref _menuUIList, () => new MenuUIBehaviour[2] { this, _itemInfoUI }.Concat(_inventoryUI.viewer.MenuUIList).ToArray());

	public void SetItemFilter(InventoryFacadeViewer.ItemFilter[] itemFilters)
	{
		_inventoryUI.SetItemFilter(itemFilters);
	}

	public void CountViewerVisible(bool isVisible)
	{
		_itemInfoUI.isCountViewerVisible = isVisible;
	}

	public void EmptyTextAutoVisible(bool isVisible)
	{
		_inventoryUI.viewer.isAutoEmptyText = isVisible;
	}

	public void DoubleClickAction(Action<InventoryFacadeViewer.DoubleClickData> action)
	{
		_inventoryUI.ItemNodeOnDoubleClick = delegate(InventoryFacadeViewer.DoubleClickData x)
		{
			action?.Invoke(x);
			_itemInfoUI.OnSubmitForce();
		};
	}

	protected abstract void ItemInfoEvent();

	private IEnumerator LoadViewer()
	{
		yield return _inventoryUI.Initialize();
		_inventoryUI.SetOwner(this);
		ItemInfoEvent();
	}

	protected override void OnBeforeStart()
	{
		StartCoroutine(LoadViewer());
		OnActiveChangedAsObservable().Subscribe(delegate(bool x)
		{
			SetActiveControl(x);
		});
		ActionIDDownCommand actionIDDownCommand = new ActionIDDownCommand();
		actionIDDownCommand.ActionID = ActionID.Submit;
		ActionIDDownCommand actionIDDownCommand2 = actionIDDownCommand;
		actionIDDownCommand2.TriggerEvent.AddListener(delegate
		{
			OnInputSubmit();
		});
		_actionCommands.Add(actionIDDownCommand2);
		actionIDDownCommand = new ActionIDDownCommand();
		actionIDDownCommand.ActionID = ActionID.Cancel;
		ActionIDDownCommand actionIDDownCommand3 = actionIDDownCommand;
		actionIDDownCommand3.TriggerEvent.AddListener(delegate
		{
			OnInputCancel();
		});
		_actionCommands.Add(actionIDDownCommand3);
	}

	protected void SelectItem(StuffItem info)
	{
		_itemInfoUI.Open(info);
	}

	protected new void SetFocusLevel(int level)
	{
		_itemInfoUI.EnabledInput = level == _itemInfoUI.FocusLevel;
	}

	private void SetActiveControl(bool isActive)
	{
		Manager.Input instance = Singleton<Manager.Input>.Instance;
		IEnumerator coroutine;
		if (isActive)
		{
			Time.timeScale = 0f;
			Singleton<Map>.Instance.Player.SetScheduledInteractionState(isEnabled: false);
			Singleton<Map>.Instance.Player.ReleaseInteraction();
			instance.FocusLevel = 0;
			instance.MenuElements = MenuUIList;
			coroutine = DoOpen();
		}
		else
		{
			instance.ClearMenuElements();
			instance.FocusLevel = -1;
			coroutine = DoClose();
		}
		if (_fadeDisposable != null)
		{
			_fadeDisposable.Dispose();
		}
		_fadeDisposable = Observable.FromCoroutine(() => coroutine).Subscribe(delegate
		{
		}, delegate(Exception ex)
		{
			Debug.LogException(ex);
		});
	}

	private void Close()
	{
		_inventoryUI.viewer.cursor.enabled = false;
		_inventoryUI.viewer.itemListUI.ClearItems();
		_inventoryUI.viewer.sortUI.playSE.use = false;
		_inventoryUI.viewer.sortUI.SetDefault();
		_inventoryUI.viewer.sortUI.Close();
		_inventoryUI.viewer.sortUI.playSE.use = true;
		_itemInfoUI.Close();
		if (_emptyTextUpdateDisposable != null)
		{
			_emptyTextUpdateDisposable.Dispose();
		}
		_emptyTextUpdateDisposable = null;
		_inventoryUI.viewer.isAutoEmptyText = false;
		_inventoryUI.viewer.emptyText.enabled = false;
		_inventoryUI.ItemNodeOnDoubleClick = null;
		OnClose?.Invoke();
		playSE.Play(SoundPack.SystemSE.Cancel);
	}

	private IEnumerator DoOpen()
	{
		if (Observer != null)
		{
			Observer.OnClose = delegate
			{
				Close();
			};
		}
		Singleton<Manager.Input>.Instance.ReserveState(Manager.Input.ValidType.UI);
		Singleton<Manager.Input>.Instance.SetupState();
		_inventoryUI.viewer.slotCounter.y = capacity();
		if (!itemListUI.isOptionNode)
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
			itemListUI.SetOptionNode(gameObject);
		}
		_canvasGroup.blocksRaycasts = false;
		while (!_inventoryUI.viewer.categoryInitialized || !_inventoryUI.viewer.sortUIInitialized)
		{
			yield return null;
		}
		_inventoryUI.ItemListNodeCreate();
		if (_emptyTextUpdateDisposable != null)
		{
			_emptyTextUpdateDisposable.Dispose();
		}
		_emptyTextUpdateDisposable = null;
		if (_inventoryUI.viewer.isAutoEmptyText)
		{
			_emptyTextUpdateDisposable = (from _ in Observable.EveryUpdate().TakeUntilDestroy(base.gameObject)
				where base.isActiveAndEnabled
				select _).Subscribe(delegate
			{
				_inventoryUI.viewer.emptyText.enabled = !_inventoryUI.itemListUI.optionTable.Any((KeyValuePair<int, ItemNodeUI> v) => v.Value.Visible);
			});
		}
		float startAlpha = _canvasGroup.alpha;
		IObservable<TimeInterval<float>> fadeInStream = ObservableEasing.Linear(0.3f, ignoreTimeScale: true).FrameTimeInterval(ignoreTimeScale: true).Do(delegate(TimeInterval<float> x)
		{
			_canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, x.Value);
		});
		yield return fadeInStream.ToYieldInstruction();
		_canvasGroup.blocksRaycasts = true;
		base.EnabledInput = true;
		_inventoryUI.viewer.SetFocusLevel(Singleton<Manager.Input>.Instance.FocusLevel);
		if (Observer != null)
		{
			Observer.ActiveCloseButton = true;
		}
		playSE.use = true;
	}

	private IEnumerator DoClose()
	{
		if (Observer != null)
		{
			Observer.ActiveCloseButton = false;
		}
		ItemListUI obj = itemListUI;
		bool enabledInput = (base.EnabledInput = false);
		obj.EnabledInput = enabledInput;
		PlayerActor player = Singleton<Map>.Instance.Player;
		Singleton<Manager.Input>.Instance.ReserveState(Manager.Input.ValidType.Action);
		_canvasGroup.blocksRaycasts = false;
		float startAlpha = _canvasGroup.alpha;
		IObservable<TimeInterval<float>> fadeOutStream = ObservableEasing.Linear(0.3f, ignoreTimeScale: true).FrameTimeInterval(ignoreTimeScale: true).Do(delegate(TimeInterval<float> x)
		{
			_canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, x.Value);
		});
		yield return fadeOutStream.ToYieldInstruction();
		player.SetScheduledInteractionState(isEnabled: true);
		player.ReleaseInteraction();
		Singleton<Manager.Input>.Instance.SetupState();
		playSE.use = false;
	}

	private void OnInputSubmit()
	{
	}

	private void OnInputCancel()
	{
		Close();
	}
}
