using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AIProject.SaveData;
using AIProject.Scene;
using AIProject.UI.Viewer;
using Manager;
using ReMotion;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEx;

namespace AIProject.UI;

public class FarmlandUI : MenuUIBehaviour
{
	[SerializeField]
	private CanvasGroup _canvasGroup;

	[SerializeField]
	private InventoryFacadeViewer _inventoryUI;

	[SerializeField]
	private HarvestListViewer _harvestListViewer;

	[SerializeField]
	private PlantUI _plantUI;

	[SerializeField]
	private PlantInfoUI _plantInfoUI;

	[SerializeField]
	private Button _plantButton;

	[SerializeField]
	private Button _closeButton;

	[SerializeField]
	private Button _allPlantButton;

	private IDisposable _fadeDisposable;

	private MenuUIBehaviour[] _menuUIList;

	public PlaySE playSE { get; } = new PlaySE(use: false);

	public List<AIProject.SaveData.Environment.PlantInfo> currentPlant
	{
		get
		{
			return _currentPlant;
		}
		set
		{
			_currentPlant = value;
		}
	}

	private List<AIProject.SaveData.Environment.PlantInfo> _currentPlant { get; set; }

	private MenuUIBehaviour[] MenuUIList => this.GetCache(ref _menuUIList, () => new MenuUIBehaviour[6] { this, _plantUI, _plantInfoUI, _inventoryUI.categoryUI, _inventoryUI.itemListUI, _harvestListViewer.itemListUI }.Where((MenuUIBehaviour p) => p != null).ToArray());

	private void CursorOFF()
	{
		_inventoryUI.cursor.enabled = false;
	}

	protected override void Start()
	{
		OnActiveChangedAsObservable().Subscribe(delegate(bool x)
		{
			SetActiveControl(x);
		});
		StartCoroutine(BindingUI());
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
		actionIDDownCommand = new ActionIDDownCommand();
		actionIDDownCommand.ActionID = ActionID.MouseRight;
		ActionIDDownCommand actionIDDownCommand4 = actionIDDownCommand;
		actionIDDownCommand4.TriggerEvent.AddListener(delegate
		{
			OnInputCancel();
		});
		_actionCommands.Add(actionIDDownCommand4);
		base.Start();
	}

	private IEnumerator BindingUI()
	{
		yield return StartCoroutine(_inventoryUI.Initialize());
		_inventoryUI.ItemNodeOnDoubleClick = delegate(InventoryFacadeViewer.DoubleClickData x)
		{
			Planting(x.ID, x.node);
		};
		Observable.FromEvent(delegate(Action<PlantIcon> h)
		{
			_plantUI.IconChanged += h;
		}, delegate(Action<PlantIcon> h)
		{
			_plantUI.IconChanged -= h;
		}).Subscribe(delegate(PlantIcon icon)
		{
			playSE.Play(SoundPack.SystemSE.OK_S);
			bool flag = icon.info != null;
			_inventoryUI.Visible = !flag;
			_harvestListViewer.ClearList();
			if (flag)
			{
				_plantInfoUI.Open(icon);
				if (icon.info.isEnd)
				{
					_plantInfoUI.ItemCancelInteractable(interactable: false);
				}
				else
				{
					StuffItemInfo itemInfo = icon.itemInfo;
					if (itemInfo != null)
					{
						StuffItem item = new StuffItem(itemInfo.CategoryID, itemInfo.ID, 1);
						bool interactable = _inventoryUI.itemList.CanAddItem(_inventoryUI.slotCounter.y, item);
						_plantInfoUI.ItemCancelInteractable(interactable);
					}
				}
			}
			else
			{
				_plantInfoUI.Close();
			}
		}).AddTo(this);
		_plantUI.AllGet.Subscribe(delegate
		{
			foreach (AIProject.SaveData.Environment.PlantInfo item2 in currentPlant.Where(delegate(AIProject.SaveData.Environment.PlantInfo item)
			{
				bool? flag2 = item?.isEnd;
				return flag2.HasValue && flag2.Value;
			}))
			{
				foreach (StuffItem item3 in item2.result.Where((StuffItem item) => item.Count > 0))
				{
					if (Singleton<Manager.Resources>.Instance.GameInfo.GetItem(item3.CategoryID, item3.ID) != null)
					{
						_inventoryUI.AddItem(item3);
					}
				}
			}
			for (int num = 0; num < currentPlant.Count; num++)
			{
				bool? flag = currentPlant[num]?.isEnd;
				if (flag.HasValue && flag.Value && !currentPlant[num].result.Any((StuffItem item) => item.Count > 0))
				{
					_plantUI.plantIcons[num].info = null;
					currentPlant[num] = null;
				}
				if (currentPlant[num] == null && num == _plantUI.currentIndex)
				{
					_inventoryUI.Visible = true;
					_plantInfoUI.Close();
				}
			}
			_inventoryUI.Refresh();
			_plantUI.Refresh();
		});
		_harvestListViewer.GetItemAction += delegate(IReadOnlyCollection<StuffItem> collection)
		{
			foreach (StuffItem item4 in collection)
			{
				if (Singleton<Manager.Resources>.Instance.GameInfo.GetItem(item4.CategoryID, item4.ID) != null && !_inventoryUI.AddItem(item4))
				{
					_harvestListViewer.AddFailedList.Add(item4);
				}
			}
			_inventoryUI.Refresh();
		};
		_plantInfoUI.OnComplete.Subscribe(delegate
		{
			_plantInfoUI.ItemCancelInteractable(interactable: false);
			bool flag = true;
			foreach (StuffItem item5 in _plantInfoUI.currentIcon.info.result.Where((StuffItem item) => item.Count > 0))
			{
				flag = false;
				_harvestListViewer.AddList(item5);
			}
			if (flag)
			{
				_harvestListViewer.AddList(StuffItem.CreateSystemItem(1));
			}
			_harvestListViewer.InStorageCheck(null);
		}).AddTo(this);
		_harvestListViewer.AllGetListAction += delegate
		{
			_plantUI.currentIcon.info = null;
			currentPlant[_plantUI.currentIndex] = null;
			_inventoryUI.Visible = true;
			_plantInfoUI.Close();
		};
		_plantInfoUI.OnSubmit.AddListener(delegate
		{
			playSE.Play(SoundPack.SystemSE.OK_S);
			ConfirmScene.Sentence = "栽培を中止しますか？";
			ConfirmScene.BackAlpha = 0f;
			ConfirmScene.Offset = new Vector2(0f, -420f);
			ConfirmScene.OnClickedYes = delegate
			{
				PlantIcon currentIcon = _plantInfoUI.currentIcon;
				bool? flag = currentIcon.info?.isEnd;
				if (!flag.HasValue || flag.Value)
				{
					Debug.LogError("栽培キャンセル失敗(時間経過)");
					_plantInfoUI.Close();
				}
				else
				{
					StuffItemInfo itemInfo = currentIcon.itemInfo;
					if (itemInfo != null)
					{
						bool interactable = _inventoryUI.AddItem(new StuffItem(itemInfo.CategoryID, itemInfo.ID, 1));
						_plantInfoUI.ItemCancelInteractable(interactable);
					}
					else
					{
						Debug.LogError("栽培キャンセル失敗(アイテム情報がない)");
					}
					currentPlant[_plantUI.currentIndex] = null;
					currentIcon.info = null;
					_plantInfoUI.Close();
					_inventoryUI.Refresh();
					_inventoryUI.Visible = true;
					playSE.Play(SoundPack.SystemSE.OK_L);
				}
			};
			ConfirmScene.OnClickedNo = delegate
			{
				playSE.Play(SoundPack.SystemSE.Cancel);
			};
			Singleton<Game>.Instance.LoadDialog();
		});
		_inventoryUI.itemListUI.CurrentChanged += delegate(int _, ItemNodeUI option)
		{
			_plantButton.interactable = option != null;
		};
		_plantButton.OnClickAsObservable().Subscribe(delegate
		{
			Planting(_inventoryUI.itemListUI.CurrentID, _inventoryUI.itemListUI.CurrentOption);
		});
		_closeButton.OnClickAsObservable().Subscribe(delegate
		{
			OnInputCancel();
		});
		_inventoryUI.itemListUI.CurrentChanged += delegate(int _, ItemNodeUI option)
		{
			_allPlantButton.interactable = option != null;
		};
		_allPlantButton.OnClickAsObservable().Subscribe(delegate
		{
			PlantingForAll(_inventoryUI.itemListUI.CurrentID, _inventoryUI.itemListUI.CurrentOption);
		});
		_inventoryUI.categoryUI.OnSubmit.AddListener(delegate
		{
			if (_inventoryUI.categoryUI.useCursor)
			{
				_inventoryUI.categoryUI.SelectedButton.onClick.Invoke();
			}
		});
		_inventoryUI.categoryUI.OnCancel.AddListener(delegate
		{
			OnInputCancel();
		});
		_inventoryUI.categoryUI.OnEntered = delegate
		{
			CursorOFF();
			SetFocusLevel(_inventoryUI.categoryUI.FocusLevel);
		};
		Observable.Merge(new IObservable<PointerEventData>[2]
		{
			_inventoryUI.categoryUI.leftButton.OnPointerEnterAsObservable(),
			_inventoryUI.categoryUI.rightButton.OnPointerEnterAsObservable()
		}).Subscribe(delegate
		{
			_inventoryUI.categoryUI.OnEntered();
		}).AddTo(this);
		_inventoryUI.itemListUI.OnEntered.Where((int _) => IsActiveControl).Subscribe(delegate
		{
			if (_inventoryUI.itemListUI.ItemVisibles.Any())
			{
				CursorOFF();
				SetFocusLevel(_inventoryUI.itemListUI.FocusLevel);
			}
		});
		_inventoryUI.itemListUI.OnCancel.AddListener(delegate
		{
			OnInputCancel();
		});
		_harvestListViewer.itemListUI.OnEntered.Where((int _) => IsActiveControl).Subscribe(delegate
		{
			if (_harvestListViewer.itemListUI.ItemVisibles.Any())
			{
				CursorOFF();
				SetFocusLevel(_harvestListViewer.itemListUI.FocusLevel);
			}
		});
		_harvestListViewer.itemListUI.OnCancel.AddListener(delegate
		{
			OnInputCancel();
		});
	}

	private void SetActiveControl(bool isActive)
	{
		Manager.Input instance = Singleton<Manager.Input>.Instance;
		IEnumerator coroutine;
		if (isActive)
		{
			MapUIContainer.SetVisibleHUD(active: false);
			Time.timeScale = 0f;
			Singleton<Map>.Instance.Player.SetScheduledInteractionState(isEnabled: false);
			Singleton<Map>.Instance.Player.ReleaseInteraction();
			instance.FocusLevel = 0;
			instance.MenuElements = MenuUIList;
			coroutine = DoOpen();
		}
		else
		{
			MapUIContainer.SetVisibleHUD(active: true);
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
		Time.timeScale = 1f;
		IsActiveControl = false;
		MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.InvokeAcception);
		playSE.Play(SoundPack.SystemSE.Cancel);
	}

	private IEnumerator DoOpen()
	{
		yield return MapUIContainer.DrawOnceTutorialUI(15);
		Singleton<Manager.Input>.Instance.ReserveState(Manager.Input.ValidType.UI);
		Singleton<Manager.Input>.Instance.SetupState();
		while (Singleton<Map>.Instance.Player == null)
		{
			yield return null;
		}
		_inventoryUI.SetItemList(Singleton<Map>.Instance.Player.PlayerData.ItemList);
		_inventoryUI.slotCounter.y = Singleton<Map>.Instance.Player.PlayerData.InventorySlotMax;
		if (!_inventoryUI.itemListUI.isOptionNode)
		{
			string bundle = Singleton<Manager.Resources>.Instance.DefinePack.ABPaths.MapScenePrefab;
			GameObject gameObject = CommonLib.LoadAsset<GameObject>(bundle, "ItemOption_ItemBox", clone: false, string.Empty);
			if (gameObject == null)
			{
				yield break;
			}
			if (!MapScene.AssetBundlePaths.Exists((UnityEx.ValueTuple<string, string> x) => x.Item1 == bundle))
			{
				MapScene.AssetBundlePaths.Add(new UnityEx.ValueTuple<string, string>(bundle, string.Empty));
			}
			_inventoryUI.itemListUI.SetOptionNode(gameObject);
		}
		_canvasGroup.blocksRaycasts = false;
		while (!_inventoryUI.initialized)
		{
			yield return null;
		}
		_inventoryUI.ItemListNodeCreate();
		_inventoryUI.Refresh();
		_harvestListViewer.Refresh();
		while (_currentPlant == null)
		{
			yield return null;
		}
		_plantUI.Open(currentPlant);
		float startAlpha = _canvasGroup.alpha;
		IObservable<TimeInterval<float>> fadeInStream = ObservableEasing.Linear(0.3f, ignoreTimeScale: true).FrameTimeInterval(ignoreTimeScale: true).Do(delegate(TimeInterval<float> x)
		{
			_canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, x.Value);
		});
		yield return fadeInStream.ToYieldInstruction();
		_canvasGroup.blocksRaycasts = true;
		Singleton<Manager.Input>.Instance.FocusLevel = 0;
		List<MenuUIBehaviour> onInputs = new List<MenuUIBehaviour>();
		List<MenuUIBehaviour> offInputs = new List<MenuUIBehaviour>();
		if (_inventoryUI.itemListUI.ItemVisibles.Any())
		{
			onInputs.Add(_inventoryUI.itemListUI);
			onInputs.Add(_inventoryUI.categoryUI);
		}
		else
		{
			offInputs.Add(_inventoryUI.itemListUI);
			onInputs.Add(_inventoryUI.categoryUI);
		}
		onInputs.ForEach(delegate(MenuUIBehaviour p)
		{
			p.EnabledInput = true;
		});
		offInputs.ForEach(delegate(MenuUIBehaviour p)
		{
			p.EnabledInput = false;
		});
		base.EnabledInput = true;
		playSE.use = true;
	}

	private IEnumerator DoClose()
	{
		base.EnabledInput = false;
		_inventoryUI.itemListUI.EnabledInput = false;
		PlayerActor player = Singleton<Map>.Instance.Player;
		player.CameraControl.Mode = CameraMode.Normal;
		Singleton<Manager.Input>.Instance.ReserveState(Manager.Input.ValidType.Action);
		_canvasGroup.blocksRaycasts = false;
		float startAlpha = _canvasGroup.alpha;
		IObservable<TimeInterval<float>> fadeOutStream = ObservableEasing.Linear(0.3f, ignoreTimeScale: true).FrameTimeInterval(ignoreTimeScale: true).Do(delegate(TimeInterval<float> x)
		{
			_canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, x.Value);
		});
		yield return fadeOutStream.ToYieldInstruction();
		_plantInfoUI.Close();
		_inventoryUI.cursor.enabled = false;
		_inventoryUI.itemListUI.ClearItems();
		player.Controller.ChangeState("Normal");
		player.SetScheduledInteractionState(isEnabled: true);
		player.ReleaseInteraction();
		Singleton<Manager.Input>.Instance.SetupState();
		playSE.use = false;
	}

	private new void SetFocusLevel(int level)
	{
		_inventoryUI.viewer.SetFocusLevel(level);
		base.EnabledInput = level == base.FocusLevel;
		_plantUI.EnabledInput = level == _plantUI.FocusLevel;
		_plantInfoUI.EnabledInput = level == _plantInfoUI.FocusLevel;
		_harvestListViewer.itemListUI.EnabledInput = level == _harvestListViewer.itemListUI.FocusLevel;
	}

	private void Planting(int currentID, ItemNodeUI currentOption)
	{
		StuffItem item = currentOption.Item;
		StuffItem stuffItem = _inventoryUI.itemList.Find((StuffItem x) => x == item);
		stuffItem.Count--;
		if (stuffItem.Count <= 0)
		{
			_inventoryUI.itemList.Remove(stuffItem);
			_inventoryUI.itemListUI.RemoveItemNode(currentID);
			_inventoryUI.itemListUI.ForceSetNonSelect();
		}
		_inventoryUI.itemListUI.Refresh();
		_plantUI.SetPlantItem(item);
		_plantUI.Refresh();
		bool interactable = _inventoryUI.itemList.CanAddItem(_inventoryUI.slotCounter.y, item);
		_plantInfoUI.ItemCancelInteractable(interactable);
	}

	private void PlantingForAll(int currentID, ItemNodeUI currentOption)
	{
		int emptySum = _plantUI.GetEmptySum();
		if (emptySum == 0)
		{
			return;
		}
		StuffItem item = currentOption.Item;
		StuffItem stuffItem = _inventoryUI.itemList.Find((StuffItem x) => x == item);
		int num = 0;
		while (stuffItem.Count > 0)
		{
			stuffItem.Count--;
			if (++num >= emptySum)
			{
				break;
			}
		}
		if (stuffItem.Count <= 0)
		{
			_inventoryUI.itemList.Remove(stuffItem);
			_inventoryUI.itemListUI.RemoveItemNode(currentID);
			_inventoryUI.itemListUI.ForceSetNonSelect();
		}
		_inventoryUI.itemListUI.Refresh();
		_plantUI.SetPlantItemForAll(item, num);
		_plantUI.Refresh();
		bool interactable = _inventoryUI.itemList.CanAddItem(_inventoryUI.slotCounter.y, item);
		_plantInfoUI.ItemCancelInteractable(interactable);
	}

	private void OnInputSubmit()
	{
	}

	private void OnInputCancel()
	{
		Close();
	}
}
