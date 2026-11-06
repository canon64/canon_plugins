using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AIProject.Player;
using AIProject.SaveData;
using AIProject.Scene;
using AIProject.UI.Viewer;
using Illusion.Extensions;
using Manager;
using ReMotion;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using UnityEx;

namespace AIProject.UI;

public class CraftUI : MenuUIBehaviour
{
	public class CreateItem
	{
		public StuffItem item { get; set; }

		public RecipeDataInfo info { get; set; }
	}

	[Header("CraftUI Setting")]
	[SerializeField]
	private CanvasGroup _canvasGroup;

	[SerializeField]
	private Button _closeButton;

	[Header("Craft Viewer")]
	[SerializeField]
	private CraftTagSelectionUI _craftTagSelection;

	private CraftViewer[] _craftVieweres;

	[Header("Recipe Viewer")]
	[SerializeField]
	private RectTransform _recipeViewerLayout;

	[SerializeField]
	private RecipeViewer _recipeViewer;

	[Header("InfoUI")]
	[SerializeField]
	private RectTransform _itemInfoLayout;

	[SerializeField]
	private CraftItemInfoUI _itemInfoUI;

	private IDisposable _fadeDisposable;

	public PlaySE playSE { get; } = new PlaySE(use: false);

	public SystemMenuUI Observer { get; set; }

	public Action OnClose { get; set; }

	public Action OnClosedEvent { get; set; }

	public IReadOnlyCollection<IReadOnlyCollection<StuffItem>> checkStorages
	{
		[CompilerGenerated]
		get
		{
			return _checkStorages;
		}
	}

	private IReadOnlyCollection<List<StuffItem>> _checkStorages { get; set; }

	private int _id { get; set; }

	private int _selectedIndexOf { get; set; }

	private MenuUIBehaviour[] MenuUIList { get; set; }

	private bool initialized { get; set; }

	private CraftViewer currentViewer { get; set; }

	private CreateItem createItem { get; } = new CreateItem();

	public static bool CreateCooking(CreateItem createItem, List<StuffItem> itemList, float pheromone, bool chef)
	{
		if (createItem == null)
		{
			return false;
		}
		int num = 1;
		StuffItem stuffItem = new StuffItem(createItem.item);
		List<StuffItem> itemListInPantry = Singleton<Game>.Instance.Environment.ItemListInPantry;
		int pantryCapacity = Singleton<Manager.Resources>.Instance.DefinePack.ItemBoxCapacityDefines.PantryCapacity;
		if (!itemListInPantry.CanAddItem(pantryCapacity, stuffItem, num))
		{
			return false;
		}
		RecipeDataInfo info = createItem.info;
		RecipeDataInfo.NeedData[] needList = info.NeedList;
		foreach (RecipeDataInfo.NeedData needData in needList)
		{
			StuffItem.RemoveStorages(new StuffItem(needData.CategoryID, needData.ID, needData.Sum * num), (IReadOnlyCollection<List<StuffItem>>)(object)new List<StuffItem>[2] { itemList, itemListInPantry });
		}
		float num2 = 50f;
		StatusProfile statusProfile = Singleton<Manager.Resources>.Instance.StatusProfile;
		float t = Mathf.InverseLerp(statusProfile.FlavorCookSuccessBoostMinMax.min, statusProfile.FlavorCookSuccessBoostMinMax.max, pheromone);
		float num3 = statusProfile.FlavorCookSuccessBoost.Lerp(t);
		float num4 = 0f;
		if (chef)
		{
			num4 += (float)statusProfile.ChefCookSuccessBoost;
		}
		float num5 = num2 + num3 + num4;
		float num6 = UnityEngine.Random.Range(0f, 100f);
		if (num6 <= num5)
		{
			itemListInPantry.AddItem(stuffItem, info.CreateSum * num);
		}
		else
		{
			stuffItem.CategoryID = 7;
			stuffItem.ID = 12;
			itemListInPantry.AddItem(stuffItem, info.CreateSum * num);
		}
		return true;
	}

	public static CreateItem CreateCheck(IReadOnlyDictionary<int, RecipeDataInfo[]> targetTable, IReadOnlyCollection<IReadOnlyCollection<StuffItem>> storages)
	{
		Dictionary<int, RecipeDataInfo[]> dictionary = (from v in targetTable
			select new
			{
				Key = v.Key,
				Value = Possible(storages, v.Value)
			} into v
			where v.Value.Any()
			select v).ToDictionary(v => v.Key, v => v.Value);
		if (dictionary.Any())
		{
			KeyValuePair<int, RecipeDataInfo[]> keyValuePair = dictionary.Shuffle().First();
			StuffItemInfo stuffItemInfo = Singleton<Manager.Resources>.Instance.GameInfo.FindItemInfo(keyValuePair.Key);
			CreateItem createItem = new CreateItem();
			createItem.info = IEnumerableExtensions.Shuffle(keyValuePair.Value).First();
			createItem.item = new StuffItem(stuffItemInfo.CategoryID, stuffItemInfo.ID, 0);
			return createItem;
		}
		return null;
	}

	private static RecipeDataInfo[] Possible(IReadOnlyCollection<IReadOnlyCollection<StuffItem>> storages, RecipeDataInfo[] info)
	{
		return info.Where((RecipeDataInfo x) => x.NeedList.All((RecipeDataInfo.NeedData need) => storages.SelectMany((IReadOnlyCollection<StuffItem> storage) => storage).FindItems(new StuffItem(need.CategoryID, need.ID, 0)).Sum((StuffItem p) => p.Count) / need.Sum > 0)).ToArray();
	}

	public void SetID(int id)
	{
		_id = id;
		List<List<StuffItem>> list = new List<List<StuffItem>>();
		list.Add(Singleton<Map>.Instance.Player.PlayerData.ItemList);
		list.Add(Singleton<Game>.Instance.Environment.ItemListInStorage);
		if (_id == 2)
		{
			list.Add(Singleton<Game>.Instance.Environment.ItemListInPantry);
		}
		_checkStorages = list;
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
		_closeButton.OnClickAsObservable().Subscribe(delegate
		{
			OnInputCancel();
		});
		while (!Singleton<Manager.Resources>.IsInstance() || !Singleton<Manager.Resources>.Instance.GameInfo.recipe.initialized)
		{
			yield return null;
		}
		while (!Singleton<Map>.IsInstance() || Singleton<Map>.Instance.Player == null || Singleton<Map>.Instance.Player.PlayerData == null)
		{
			yield return null;
		}
		while (!Singleton<Game>.IsInstance() || Singleton<Game>.Instance.Environment == null)
		{
			yield return null;
		}
		if (_recipeViewer == null)
		{
			yield return RecipeViewer.Load(this, _recipeViewerLayout, delegate(RecipeViewer recipeViewer)
			{
				_recipeViewer = recipeViewer;
			});
		}
		while (!_recipeViewer.initialized)
		{
			yield return null;
		}
		if (_itemInfoUI == null)
		{
			yield return CraftItemInfoUI.Load(this, _itemInfoLayout, delegate(CraftItemInfoUI itemInfoUI)
			{
				_itemInfoUI = itemInfoUI;
			});
		}
		UITrigger.TriggerEvent triggerEvent = new UITrigger.TriggerEvent();
		_recipeViewer.GetOrAddComponent<PointerEnterTrigger>().Triggers.Add(triggerEvent);
		triggerEvent.AddListener(delegate
		{
			SetFocusLevel(_recipeViewer.itemListUI.FocusLevel);
		});
		_recipeViewer.itemListUI.CurrentChanged += delegate(int index, RecipeItemTitleNodeUI option)
		{
			if (!(option == null))
			{
				_itemInfoUI.createSum = option.data.CreateSum;
				StuffItem item2 = createItem.item;
				StuffItemInfo item3 = Singleton<Manager.Resources>.Instance.GameInfo.GetItem(item2.CategoryID, item2.ID);
				int num3 = option.MaxCount;
				if (!item3.isTrash)
				{
					if (Singleton<Map>.Instance.Player.PlayerData.FirstCreatedItemTable.Contains(item3.nameHash))
					{
						num3 = 0;
					}
					num3 = Mathf.Min(num3, 1);
				}
				_itemInfoUI.Refresh(num3);
				_itemInfoUI.isCountViewerVisible = num3 > 0 && item3.isTrash;
				createItem.info = option.data;
			}
		};
		CraftGroupUI[] array = _craftTagSelection.group;
		_craftVieweres = array.Select((CraftGroupUI p) => p.viewer).ToArray();
		CraftViewer[] craftVieweres = _craftVieweres;
		foreach (CraftViewer item in craftVieweres)
		{
			while (!item.initialized)
			{
				yield return null;
			}
			UITrigger.TriggerEvent onEnter = new UITrigger.TriggerEvent();
			item.GetOrAddComponent<PointerEnterTrigger>().Triggers.Add(onEnter);
			onEnter.AddListener(delegate
			{
				SetFocusLevel(item.itemListUI.FocusLevel);
			});
			item.itemListUI.CurrentChanged += delegate(int index, CraftItemNodeUI option)
			{
				if (!(option == null))
				{
					if (option.isUnknown)
					{
						_itemInfoUI.Close();
					}
					else
					{
						createItem.item = new StuffItem(option.CategoryID, option.ID, 0);
						_itemInfoUI.Open(createItem.item);
					}
					_recipeViewer.itemListUI.ClearItems();
					_recipeViewer.itemListUI.AddItemNode(index, option.data);
				}
			};
		}
		_craftTagSelection.Selection = delegate(int sel)
		{
			_itemInfoUI.Close();
			_recipeViewer.itemListUI.ClearItems();
			if (IsActiveControl)
			{
				for (int i = 0; i < array.Length; i++)
				{
					bool flag = i == sel;
					if (flag && currentViewer != array[i].viewer)
					{
						currentViewer = array[i].viewer;
						currentViewer.Refresh(this);
					}
					array[i].SetActive(flag);
				}
				currentViewer.itemListUI.ForceSetNonSelect();
				playSE.Play(SoundPack.SystemSE.OK_S);
			}
		};
		_itemInfoUI.OnSubmit += delegate
		{
			ConfirmScene.Sentence = _itemInfoUI.ConfirmLabel;
			ConfirmScene.OnClickedYes = delegate
			{
				int count = _itemInfoUI.Count;
				RecipeDataInfo.NeedData[] needList = createItem.info.NeedList;
				foreach (RecipeDataInfo.NeedData needData in needList)
				{
					StuffItem.RemoveStorages(new StuffItem(needData.CategoryID, needData.ID, needData.Sum * count), _checkStorages);
				}
				StuffItem item2 = createItem.item;
				foreach (List<StuffItem> checkStorage in _checkStorages)
				{
					if (checkStorage == _itemInfoUI.targetStorage)
					{
						checkStorage.AddItem(new StuffItem(item2), createItem.info.CreateSum * count);
						break;
					}
				}
				StuffItemInfo item3 = Singleton<Manager.Resources>.Instance.GameInfo.GetItem(item2.CategoryID, item2.ID);
				bool isTrash = item3.isTrash;
				if (!isTrash)
				{
					Singleton<Map>.Instance.Player.PlayerData.FirstCreatedItemTable.Add(item3.nameHash);
				}
				RecipeItemListUI itemListUI = _recipeViewer.itemListUI;
				RecipeItemTitleNodeUI currentOption = itemListUI.CurrentOption;
				currentOption.Refresh();
				_itemInfoUI.Refresh(isTrash ? currentOption.MaxCount : 0);
				if (!currentOption.isSuccess)
				{
					itemListUI.ForceSetNonSelect();
				}
				currentViewer.itemListUI.Refresh();
				playSE.Play(SoundPack.SystemSE.Craft);
			};
			ConfirmScene.OnClickedNo = delegate
			{
				playSE.Play(SoundPack.SystemSE.Cancel);
			};
			Singleton<Game>.Instance.LoadDialog();
		};
		CraftViewer[] craftVieweres2 = _craftVieweres;
		foreach (CraftViewer craftViewer in craftVieweres2)
		{
			craftViewer.itemListUI.OnCancel.AddListener(delegate
			{
				OnInputCancel();
			});
		}
		_recipeViewer.itemListUI.OnCancel.AddListener(delegate
		{
			OnInputCancel();
		});
		_itemInfoUI.OnCancel += delegate
		{
			OnInputCancel();
		};
		List<MenuUIBehaviour> list = new List<MenuUIBehaviour>();
		list.Add(this);
		list.Add(_recipeViewer.itemListUI);
		list.Add(_itemInfoUI);
		list.AddRange(_craftVieweres.Select((CraftViewer p) => p.itemListUI));
		MenuUIList = list.ToArray();
		initialized = true;
	}

	private void SetActiveControl(bool isActive)
	{
		Manager.Input instance = Singleton<Manager.Input>.Instance;
		IEnumerator coroutine;
		if (isActive)
		{
			MapUIContainer.SetVisibleHUD(active: false);
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
		if (Singleton<Map>.Instance.Player.Controller.State is AIProject.Player.Craft)
		{
			Time.timeScale = 1f;
			MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.InvokeAcception);
		}
		IsActiveControl = false;
		if (Observer != null)
		{
			Observer.ChangeBackground(-1);
		}
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
		_canvasGroup.blocksRaycasts = false;
		while (!initialized)
		{
			yield return null;
		}
		CraftViewer[] craftVieweres = _craftVieweres;
		foreach (CraftViewer item in craftVieweres)
		{
			while (!item.initialized)
			{
				yield return null;
			}
		}
		foreach (Toggle item2 in _craftTagSelection)
		{
			item2.isOn = false;
		}
		float startAlpha = _canvasGroup.alpha;
		IObservable<TimeInterval<float>> fadeInStream = ObservableEasing.Linear(0.3f, ignoreTimeScale: true).FrameTimeInterval(ignoreTimeScale: true).Do(delegate(TimeInterval<float> x)
		{
			_canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, x.Value);
		});
		yield return fadeInStream.ToYieldInstruction();
		_canvasGroup.blocksRaycasts = true;
		Singleton<Manager.Input>.Instance.FocusLevel = 0;
		MenuUIBehaviour[] menuUIList = MenuUIList;
		foreach (MenuUIBehaviour menuUIBehaviour in menuUIList)
		{
			menuUIBehaviour.EnabledInput = true;
		}
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
		MenuUIBehaviour[] menuUIList = MenuUIList;
		foreach (MenuUIBehaviour menuUIBehaviour in menuUIList)
		{
			menuUIBehaviour.EnabledInput = false;
		}
		_canvasGroup.blocksRaycasts = false;
		float startAlpha = _canvasGroup.alpha;
		IObservable<TimeInterval<float>> fadeOutStream = ObservableEasing.Linear(0.3f, ignoreTimeScale: true).FrameTimeInterval(ignoreTimeScale: true).Do(delegate(TimeInterval<float> x)
		{
			_canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, x.Value);
		});
		yield return fadeOutStream.ToYieldInstruction();
		_itemInfoUI.Close();
		CraftViewer[] craftVieweres = _craftVieweres;
		foreach (CraftViewer craftViewer in craftVieweres)
		{
			craftViewer.itemListUI.ForceSetNonSelect();
		}
		_recipeViewer.itemListUI.ClearItems();
		_craftTagSelection[0].isOn = true;
		CraftViewer[] craftVieweres2 = _craftVieweres;
		foreach (CraftViewer craftViewer2 in craftVieweres2)
		{
			craftViewer2.itemListUI.ClearItems();
		}
		if (currentViewer != null)
		{
			currentViewer.Visible = false;
		}
		currentViewer = null;
		Singleton<Manager.Input>.Instance.SetupState();
		if (OnClosedEvent != null)
		{
			Action onClosedEvent = OnClosedEvent;
			OnClosedEvent = null;
			onClosedEvent();
		}
		playSE.use = false;
	}

	private new void SetFocusLevel(int level)
	{
		Singleton<Manager.Input>.Instance.FocusLevel = level;
	}

	private void OnInputSubmit()
	{
	}

	private void OnInputCancel()
	{
		Close();
	}
}
