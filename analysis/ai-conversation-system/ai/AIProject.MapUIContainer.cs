using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ADV;
using AIProject.Animal;
using AIProject.ColorDefine;
using AIProject.DebugUtil;
using AIProject.Definitions;
using AIProject.SaveData;
using AIProject.Scene;
using AIProject.UI;
using AIProject.UI.Recycling;
using Cinemachine;
using Illusion.Extensions;
using Manager;
using ReMotion;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using UnityEx;

namespace AIProject;

public class MapUIContainer : Singleton<MapUIContainer>
{
	[SerializeField]
	private CanvasGroup _canvasGroup;

	[SerializeField]
	private Canvas _hudCanvas;

	[SerializeField]
	private Canvas _commandCanvas;

	[Header("Progression")]
	[SerializeField]
	private FadeCanvas _fadeCanvas;

	[SerializeField]
	private NotifyMessageList _notifyPanel;

	[SerializeField]
	[Header("HUD")]
	private CanvasGroup _centerPointerCanvasGroup;

	[SerializeField]
	private MiniMapControler _minimapUI;

	[SerializeField]
	private CanvasGroup _minimapCanvasGroup;

	[SerializeField]
	private GameObject _MiniMapRenderTex;

	[SerializeField]
	private GameObject _MiniMapObject;

	[Header("Command")]
	[SerializeField]
	private SystemMenuUI _systemMenuUI;

	[SerializeField]
	private GameLog _gameLogUI;

	[SerializeField]
	private CommCommandList _commandList;

	[SerializeField]
	private CommCommandList _choiceUI;

	[SerializeField]
	private CommandLabel _commandLabel;

	private ClosetUI _dressRoomUI;

	private ClosetUI _closetUI;

	private CharaEntryUI _charaEntryUI;

	private CharaChangeUI _charaChangeUI;

	private PlayerChangeUI _playerChangeUI;

	private CharaLookEditUI _charaLookEditUI;

	private PlayerLookEditUI _playerLookEditUI;

	private CharaMigrateUI _charaMigrateUI;

	[SerializeField]
	private ItemBoxUI _itemBoxUI;

	[SerializeField]
	private FishingUI _fishingUI;

	private ResultMessageUI _resultMessageUI;

	private WarningMessageUI _warningMessageUI;

	private StorySupportUI _storySupportUI;

	private RequestUI _requestUI;

	private PhotoShotUI _photoShotUI;

	private TutorialUI _tutorialUI;

	private EventDialogUI _eventDialogUI;

	[SerializeField]
	private AllAreaMapUI _AllAreaMapUI;

	[SerializeField]
	private ShopUI _ShopUI;

	[SerializeField]
	private ScroungeUI _ScroungeUI;

	[SerializeField]
	private RefrigeratorUI _RefrigeratorUI;

	[SerializeField]
	private CraftUI _CraftUI;

	[SerializeField]
	private CraftUI _CookingUI;

	[SerializeField]
	private CraftUI _PetCraftUI;

	[SerializeField]
	private CraftUI _MedicineCraftUI;

	[SerializeField]
	private FarmlandUI _FarmlandUI;

	[SerializeField]
	private ChickenCoopUI _ChickenCoopUI;

	[SerializeField]
	private PetHomeUI _petHomeUI;

	[SerializeField]
	private JukeBoxUI _jukeBoxUI;

	[SerializeField]
	private SpendMoneyUI _spendMoneyUI;

	[SerializeField]
	private AnimalNicknameOutput _nicknameUI;

	[SerializeField]
	private RecyclingUI _recyclingUI;

	private IDisposable _hudActivateSubscriber;

	private IDisposable _storySupprtUIActivateSubscriber;

	[Header("Debug")]
	[SerializeField]
	private Canvas _debugUIRoot;

	[SerializeField]
	private Canvas _debugBackgroundUIRoot;

	private ADVScene _advScene;

	private readonly ADVData _advData = new ADVData();

	private bool _activeCanvas = true;

	public CanvasGroup CanvasGroup
	{
		[CompilerGenerated]
		get
		{
			return _canvasGroup;
		}
	}

	public static FadeCanvas FadeCanvas
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._fadeCanvas;
		}
	}

	public static NotifyMessageList NotifyPanel
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._notifyPanel;
		}
	}

	public MiniMapControler MinimapUI
	{
		[CompilerGenerated]
		get
		{
			return _minimapUI;
		}
	}

	public static GameObject MiniMapRenderTex
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._MiniMapRenderTex;
		}
	}

	public static SystemMenuUI SystemMenuUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._systemMenuUI;
		}
	}

	public static GameLog GameLogUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._gameLogUI;
		}
	}

	public static CommCommandList CommandList
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._commandList;
		}
	}

	public static CommCommandList ChoiceUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._choiceUI;
		}
	}

	public static CommandLabel CommandLabel
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._commandLabel;
		}
	}

	public static ClosetUI DressRoomUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._dressRoomUI;
		}
	}

	public static ClosetUI ClosetUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._closetUI;
		}
	}

	public static CharaEntryUI CharaEntryUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._charaEntryUI;
		}
	}

	public static CharaChangeUI CharaChangeUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._charaChangeUI;
		}
	}

	public static PlayerChangeUI PlayerChangeUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._playerChangeUI;
		}
	}

	public static CharaLookEditUI CharaLookEditUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._charaLookEditUI;
		}
	}

	public static PlayerLookEditUI PlayerLookEditUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._playerLookEditUI;
		}
	}

	public static CharaMigrateUI CharaMigrateUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._charaMigrateUI;
		}
	}

	public static ItemBoxUI ItemBoxUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._itemBoxUI;
		}
	}

	public static FishingUI FishingUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._fishingUI;
		}
	}

	public static ResultMessageUI ResultMessageUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._resultMessageUI;
		}
	}

	public static WarningMessageUI WarningMessageUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._warningMessageUI;
		}
	}

	public static StorySupportUI StorySupportUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._storySupportUI;
		}
	}

	public static RequestUI RequestUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._requestUI;
		}
	}

	public static PhotoShotUI PhotoShotUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._photoShotUI;
		}
	}

	public static TutorialUI TutorialUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._tutorialUI;
		}
	}

	public static EventDialogUI EventDialogUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._eventDialogUI;
		}
	}

	public static AllAreaMapUI AllAreaMapUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._AllAreaMapUI;
		}
	}

	public static ShopUI ShopUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._ShopUI;
		}
	}

	public static ScroungeUI ScroungeUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._ScroungeUI;
		}
	}

	public static RefrigeratorUI RefrigeratorUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._RefrigeratorUI;
		}
	}

	public static CraftUI CraftUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._CraftUI;
		}
	}

	public static CraftUI CookingUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._CookingUI;
		}
	}

	public static CraftUI PetCraftUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._PetCraftUI;
		}
	}

	public static CraftUI MedicineCraftUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._MedicineCraftUI;
		}
	}

	public static FarmlandUI FarmlandUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._FarmlandUI;
		}
	}

	public static ChickenCoopUI ChickenCoopUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._ChickenCoopUI;
		}
	}

	public static PetHomeUI PetHomeUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._petHomeUI;
		}
	}

	public static JukeBoxUI JukeBoxUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._jukeBoxUI;
		}
	}

	public static SpendMoneyUI SpendMoneyUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._spendMoneyUI;
		}
	}

	public static AnimalNicknameOutput NicknameUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._nicknameUI;
		}
	}

	public static RecyclingUI RecyclingUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<MapUIContainer>.Instance._recyclingUI;
		}
	}

	public ADVScene advScene
	{
		[CompilerGenerated]
		get
		{
			return _advScene;
		}
	}

	public ADVData advData
	{
		[CompilerGenerated]
		get
		{
			return _advData;
		}
	}

	public static string InfoName
	{
		[CompilerGenerated]
		get
		{
			return "system";
		}
	}

	public static string ItemGetText
	{
		[CompilerGenerated]
		get
		{
			return " を入手しました。";
		}
	}

	public static string ItemGetEmptyText
	{
		[CompilerGenerated]
		get
		{
			return "何も入手できませんでした。";
		}
	}

	public void OpenADV(OpenData openData, IPack pack)
	{
		_advData.openData = openData;
		_advData.pack = pack;
		_advScene.gameObject.SetActive(value: true);
	}

	public void CloseADV()
	{
		_advScene.Scenario.captions.EndADV(delegate
		{
			_advScene.gameObject.SetActive(value: false);
		});
	}

	private void Reset()
	{
		_systemMenuUI = GetComponentInChildren<SystemMenuUI>();
		_commandLabel = GetComponentInChildren<CommandLabel>();
		_notifyPanel = GetComponentInChildren<NotifyMessageList>();
		_itemBoxUI = GetComponentInChildren<ItemBoxUI>();
		_AllAreaMapUI = GetComponentInChildren<AllAreaMapUI>();
		_ShopUI = GetComponentInChildren<ShopUI>();
		_ScroungeUI = GetComponentInChildren<ScroungeUI>();
		_RefrigeratorUI = GetComponentInChildren<RefrigeratorUI>();
		CraftUI[] componentsInChildren = GetComponentsInChildren<CraftUI>();
		CraftUI[] array = componentsInChildren;
		foreach (CraftUI craftUI in array)
		{
			if (craftUI.name.StartsWith("CraftUI"))
			{
				_CraftUI = craftUI;
			}
			else if (craftUI.name.StartsWith("CookingUI"))
			{
				_CookingUI = craftUI;
			}
			else if (craftUI.name.StartsWith("PetCraftUI"))
			{
				_PetCraftUI = craftUI;
			}
			else if (craftUI.name.StartsWith("MedicineCraftUI"))
			{
				_MedicineCraftUI = craftUI;
			}
		}
		_FarmlandUI = GetComponentInChildren<FarmlandUI>();
		_ChickenCoopUI = GetComponentInChildren<ChickenCoopUI>();
		if (_MiniMapRenderTex == null)
		{
			_MiniMapRenderTex = _minimapCanvasGroup.GetComponentInChildren<RawImage>(includeInactive: true).gameObject;
		}
	}

	public static void SwitchSystemMenu()
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._systemMenuUI.IsActiveControl = !Singleton<MapUIContainer>.Instance._systemMenuUI.IsActiveControl;
		}
	}

	public static void InitializeMinimap()
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			if (Singleton<MapUIContainer>.Instance._MiniMapRenderTex == null)
			{
				Singleton<MapUIContainer>.Instance._MiniMapRenderTex = Singleton<MapUIContainer>.Instance._minimapCanvasGroup.GetComponentInChildren<RawImage>(includeInactive: true).gameObject;
			}
			Singleton<MapUIContainer>.Instance.MinimapUI.Init();
		}
	}

	public static void SetVisibleHUD(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			float from = Singleton<MapUIContainer>.Instance._minimapCanvasGroup.alpha;
			float fromStoryUIAlpha = Singleton<MapUIContainer>.Instance._storySupportUI.CanvasAlpha;
			float fromPointerAlpha = Singleton<MapUIContainer>.Instance._centerPointerCanvasGroup.alpha;
			float duration = 0.3f;
			int to = (active ? 1 : 0);
			Singleton<MapUIContainer>.Instance._hudActivateSubscriber?.Dispose();
			Singleton<MapUIContainer>.Instance._hudActivateSubscriber = ObservableEasing.EaseOutQuint(duration, ignoreTimeScale: true).FrameTimeInterval(ignoreTimeScale: true).Subscribe(delegate(TimeInterval<float> x)
			{
				Singleton<MapUIContainer>.Instance._minimapCanvasGroup.alpha = Mathf.Lerp(from, to, x.Value);
				Singleton<MapUIContainer>.Instance._centerPointerCanvasGroup.alpha = Mathf.Lerp(fromPointerAlpha, to, x.Value);
			}, delegate(Exception ex)
			{
				Debug.LogException(ex);
			});
			Singleton<MapUIContainer>.Instance._storySupprtUIActivateSubscriber?.Dispose();
			Singleton<MapUIContainer>.Instance._storySupprtUIActivateSubscriber = ObservableEasing.EaseOutQuint(duration, ignoreTimeScale: true).FrameTimeInterval(ignoreTimeScale: true).Subscribe(delegate(TimeInterval<float> x)
			{
				Singleton<MapUIContainer>.Instance._storySupportUI.CanvasAlpha = Mathf.Lerp(fromStoryUIAlpha, to, x.Value);
			}, delegate(Exception ex)
			{
				Debug.LogException(ex);
			});
		}
	}

	public static void SetVisibleHUDExceptStoryUI(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			float fromMiniMapUIAlpha = Singleton<MapUIContainer>.Instance._minimapCanvasGroup.alpha;
			float fromPointerAlpha = Singleton<MapUIContainer>.Instance._centerPointerCanvasGroup.alpha;
			float to = ((!active) ? 0f : 1f);
			Singleton<MapUIContainer>.Instance._hudActivateSubscriber?.Dispose();
			Singleton<MapUIContainer>.Instance._hudActivateSubscriber = ObservableEasing.EaseOutQuint(0.3f, ignoreTimeScale: true).FrameTimeInterval(ignoreTimeScale: true).Subscribe(delegate(TimeInterval<float> x)
			{
				Singleton<MapUIContainer>.Instance._minimapCanvasGroup.alpha = Mathf.Lerp(fromMiniMapUIAlpha, to, x.Value);
				Singleton<MapUIContainer>.Instance._centerPointerCanvasGroup.alpha = Mathf.Lerp(fromPointerAlpha, to, x.Value);
			}, delegate(Exception ex)
			{
				Debug.LogException(ex);
			});
		}
	}

	public static void ChangeActiveCanvas()
	{
		bool flag = (Singleton<MapUIContainer>.Instance._activeCanvas = !Singleton<MapUIContainer>.Instance._activeCanvas);
		Singleton<MapUIContainer>.Instance._canvasGroup.alpha = (flag ? 1 : 0);
	}

	public static void AddNotify(string message)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._notifyPanel.AddMessage(message);
		}
	}

	public static string ToHtmlStringRGB(Color color)
	{
		return $"#{ColorUtility.ToHtmlStringRGB(color)}";
	}

	public static string CharaNameColor(Actor actor)
	{
		if (actor == null || actor.CharaName.IsNullOrEmpty())
		{
			return string.Empty;
		}
		Color? actorColor = GetActorColor(actor.ID);
		return actorColor.HasValue ? actor.CharaName.Coloring(ToHtmlStringRGB(actorColor.Value)) : actor.CharaName;
	}

	public static Color? GetActorColor(int actorID)
	{
		Color? result = null;
		switch (actorID)
		{
		case 0:
			result = new Color32(233, 67, 33, byte.MaxValue);
			break;
		case 1:
			result = Define.Get(Colors.Blue);
			break;
		case 2:
			result = new Color32(250, 239, 0, byte.MaxValue);
			break;
		case 3:
			result = new Color32(byte.MaxValue, 0, byte.MaxValue, byte.MaxValue);
			break;
		case -99:
			result = Define.Get(Colors.LightGreen);
			break;
		case -90:
			result = Define.Get(Colors.Green);
			break;
		default:
			Debug.LogError(string.Format("{0} none:{1}", "CharaNameColor", actorID));
			break;
		}
		return result;
	}

	public static string GetItemColor(StuffItemInfo info)
	{
		if (info == null)
		{
			return string.Empty;
		}
		Color color;
		switch (info.Grade)
		{
		case Grade._1:
			color = Define.Get(Colors.White);
			break;
		case Grade._2:
			color = Define.Get(Colors.Blue);
			break;
		case Grade._3:
			color = Define.Get(Colors.Orange);
			break;
		default:
			color = Define.Get(Colors.White);
			Debug.LogError(string.Format("{0}:{1}", "GetItemColor", info.Grade));
			break;
		}
		return info.Name.Coloring(ToHtmlStringRGB(color));
	}

	private static string GetItemMessage(StuffItemInfo info, int count)
	{
		return $"{GetItemColor(info)} x {count}{ItemGetText}";
	}

	public static void AddSystemItemLog(StuffItemInfo info, int count, bool isNotify)
	{
		AddSystemLog(GetItemMessage(info, count), isNotify);
	}

	public static void AddSystemLog(string message, bool isNotify)
	{
		AddLog(InfoName.Coloring(ToHtmlStringRGB(Define.Get(Colors.Green))), message);
		if (isNotify)
		{
			AddNotify(message);
		}
	}

	public static void AddItemLog(Actor actor, StuffItemInfo info, int count, bool isNotify)
	{
		string itemMessage = GetItemMessage(info, count);
		AddLog(actor, itemMessage);
		if (isNotify)
		{
			AddNotify(itemMessage);
		}
	}

	public static void AddLog(Actor actor, string message, IReadOnlyCollection<TextScenario.IVoice[]> voices = null)
	{
		AddLog(CharaNameColor(actor), message, voices);
	}

	public static void AddLog(string name, string message, IReadOnlyCollection<TextScenario.IVoice[]> voices = null)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			if (!name.IsNullOrEmpty())
			{
				name += " :";
			}
			Singleton<MapUIContainer>.Instance._gameLogUI.AddLog(name, message, voices);
		}
	}

	public static void PushResultMessage(string message)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._resultMessageUI.ShowMessage(message);
		}
	}

	public static void PushWarningMessage(Popup.Warning.Type type)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._warningMessageUI.ShowMessage(type);
		}
	}

	public static void PushMessageUI(Popup.Warning.Type type, int colorID, int poseID, System.Action onComplete = null)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._warningMessageUI.ShowMessage(type, colorID, poseID, onComplete);
		}
	}

	public static void PushMessageUI(string mes, int colorID, int posID, System.Action onComplete = null)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._warningMessageUI.ShowMessage(mes, colorID, posID, onComplete);
		}
	}

	public static void OpenRequestUI(Popup.Request.Type type)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._requestUI.Open(type);
		}
	}

	public static void CloseRequestUI()
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._requestUI.IsActiveControl = false;
		}
	}

	public static void OpenStorySupportUI(Popup.StorySupport.Type type)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._storySupportUI.Open(type);
		}
	}

	public static void OpenStorySupportUI(int id)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._storySupportUI.Open(id);
		}
	}

	public static void CloseStorySupportUI()
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._storySupportUI.Close();
		}
	}

	public static void SetActiveSystemMenuUI(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._systemMenuUI.IsActiveControl = active;
		}
	}

	public static void SetActiveCommandList(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._commandList.IsActiveControl = active;
		}
	}

	public static void SetActiveCommandList(bool active, string title)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._commandList.Label.text = title;
			Singleton<MapUIContainer>.Instance._commandList.IsActiveControl = active;
		}
	}

	public static void SetActiveChoiceUI(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._choiceUI.IsActiveControl = active;
		}
	}

	public static void SetActiveChoiceUI(bool active, string title = "")
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._choiceUI.Label.text = title;
			Singleton<MapUIContainer>.Instance._choiceUI.IsActiveControl = active;
		}
	}

	public static void SetActiveItemBoxUI(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._itemBoxUI.IsActiveControl = active;
		}
	}

	public static void SetActiveDressRoomUI(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._dressRoomUI.IsActiveControl = active;
		}
	}

	public static void SetActiveClosetUI(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._closetUI.IsActiveControl = active;
		}
	}

	public static void SetActiveCharaEntryUI(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._charaEntryUI.IsActiveControl = active;
		}
	}

	public static void SetActiveCharaChangeUI(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._charaChangeUI.IsActiveControl = active;
		}
	}

	public static void SetActivePlayerChangeUI(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._playerChangeUI.IsActiveControl = active;
		}
	}

	public static void SetActiveCharaLookEditUI(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._charaLookEditUI.IsActiveControl = active;
		}
	}

	public static void SetActivePlayerLookEditUI(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._playerLookEditUI.IsActiveControl = active;
		}
	}

	public static void SetActiveCharaMigrationUI(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._charaMigrateUI.IsActiveControl = active;
		}
	}

	public static void SetActiveFishingUI(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._fishingUI.IsActiveControl = active;
		}
	}

	public static void SetActiveShopUI(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._ShopUI.IsActiveControl = active;
		}
	}

	public static void SetActiveScroungeUI(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._ScroungeUI.IsActiveControl = active;
		}
	}

	public static void SetActiveRefrigeratorUI(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._RefrigeratorUI.IsActiveControl = active;
		}
	}

	public static void SetActiveCraftUI(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._CraftUI.IsActiveControl = active;
		}
	}

	public static void SetActiveCookingUI(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._CookingUI.IsActiveControl = active;
		}
	}

	public static void SetActivePetCraftUI(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._PetCraftUI.IsActiveControl = active;
		}
	}

	public static void SetActiveMedicineCraftUI(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._MedicineCraftUI.IsActiveControl = active;
		}
	}

	public static void SetActiveFarmlandUI(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._FarmlandUI.IsActiveControl = active;
		}
	}

	public static void SetActiveChickenCoopUI(bool active, ChickenCoopUI.Mode mode)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._ChickenCoopUI.SetMode(mode);
			Singleton<MapUIContainer>.Instance._ChickenCoopUI.IsActiveControl = active;
		}
	}

	public static void SetActivePetHomeUI(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._petHomeUI.IsActiveControl = active;
		}
	}

	public static void SetActiveJukeBoxUI(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._jukeBoxUI.IsActiveControl = active;
		}
	}

	public static void SetVisibledSpendMoneyUI(bool visibled)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._spendMoneyUI.Visibled = visibled;
		}
	}

	public static void SetActiveRecyclingUI(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._recyclingUI.IsActiveControl = active;
		}
	}

	public static void OpenTutorialUI(int id, bool groupDisplay = false)
	{
		if (!Singleton<MapUIContainer>.IsInstance())
		{
			return;
		}
		if (Singleton<Game>.IsInstance())
		{
			Dictionary<int, bool> dictionary = Singleton<Game>.Instance.WorldData?.TutorialOpenStateTable;
			if (dictionary != null)
			{
				dictionary[id] = true;
			}
		}
		TutorialUI tutorialUI = Singleton<MapUIContainer>.Instance._tutorialUI;
		tutorialUI.SetCondition(id, groupDisplay);
		tutorialUI.IsActiveControl = true;
	}

	public static bool OpenOnceTutorial(int id, bool groupDisplay = false)
	{
		if (!Singleton<MapUIContainer>.IsInstance())
		{
			return false;
		}
		TutorialUI tutorialUI = Singleton<MapUIContainer>.Instance._tutorialUI;
		if (tutorialUI == null || tutorialUI.IsActiveControl)
		{
			return false;
		}
		if (!Singleton<Game>.IsInstance())
		{
			return false;
		}
		WorldData worldData = Singleton<Game>.Instance.WorldData;
		if (worldData == null)
		{
			return false;
		}
		Dictionary<int, bool> tutorialOpenStateTable = worldData.TutorialOpenStateTable;
		if (tutorialOpenStateTable == null)
		{
			return false;
		}
		if (!tutorialOpenStateTable.TryGetValue(id, out var value) || !value)
		{
			tutorialOpenStateTable[id] = true;
			tutorialUI.SetCondition(id, groupDisplay);
			tutorialUI.IsActiveControl = true;
			return true;
		}
		return false;
	}

	public static bool GetTutorialOpenState(int id)
	{
		if (!Singleton<Game>.IsInstance())
		{
			return false;
		}
		WorldData worldData = Singleton<Game>.Instance.WorldData;
		if (worldData == null)
		{
			return false;
		}
		Dictionary<int, bool> tutorialOpenStateTable = worldData.TutorialOpenStateTable;
		if (tutorialOpenStateTable == null)
		{
			return false;
		}
		if (!tutorialOpenStateTable.TryGetValue(id, out var value))
		{
			value = (tutorialOpenStateTable[id] = false);
		}
		return value;
	}

	public static void SetActiveTutorialUI(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._tutorialUI.IsActiveControl = active;
		}
	}

	public static IEnumerator DrawOnceTutorialUI(int tutorialID, ActorCameraControl camera = null)
	{
		if (!Singleton<MapUIContainer>.IsInstance() || GetTutorialOpenState(tutorialID))
		{
			yield break;
		}
		yield return null;
		if (camera != null)
		{
			CinemachineBrain brain = camera.CinemachineBrain;
			CrossFade fade = camera.CrossFade;
			while (true)
			{
				bool? flag = brain?.IsBlending;
				if ((!flag.HasValue || !flag.Value) && (!(fade != null) || fade.isEnd))
				{
					break;
				}
				yield return null;
			}
		}
		bool standby = OpenOnceTutorial(tutorialID);
		if (standby)
		{
			TutorialUI.ClosedEvent = delegate
			{
				standby = false;
			};
			while (standby)
			{
				yield return null;
			}
		}
	}

	public static void SetActiveEventDialogUI(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._eventDialogUI.IsActiveControl = active;
		}
	}

	public static void SetActivePhotoShotUI(bool active)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._photoShotUI.IsActiveControl = active;
		}
	}

	public static void SetSystemNotify(string message)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._systemMenuUI.SetNotify(message);
		}
	}

	public static void SetInventoryStock(StuffItem presentingItem, Action<bool> onCompleted)
	{
		if (!Singleton<MapUIContainer>.IsInstance())
		{
		}
	}

	public static void RefreshCommands(int id, CommCommandList.CommandInfo[] options)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._commandList.ID = id;
			Singleton<MapUIContainer>.Instance._commandList.Options = options;
		}
	}

	public static void RefreshChoices(int id, CommCommandList.CommandInfo[] options)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._choiceUI.ID = id;
			Singleton<MapUIContainer>.Instance._choiceUI.Options = options;
		}
	}

	public static void SetCommandLabelAcception(CommandLabel.AcceptionState acception)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._commandLabel.Acception = acception;
		}
	}

	public static void ReserveSystemMenuMode(SystemMenuUI.MenuMode mode)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance._systemMenuUI.ReserveMove(mode);
		}
	}

	public static IObservable<Unit> StartFade(FadeCanvas.PanelType panelType, FadeType fadeType, float duration, bool ignoreTimeScale)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			return Singleton<MapUIContainer>.Instance._fadeCanvas.StartFade(panelType, fadeType, duration, ignoreTimeScale);
		}
		return null;
	}

	public static void SetMarkerTargetCameraTransform(Transform cameraTransform)
	{
		if (Singleton<MapUIContainer>.IsInstance())
		{
			Singleton<MapUIContainer>.Instance.GetComponentInChildren<MarkerOutput>().CameraTransform = cameraTransform;
		}
	}

	private void InitiateDebugOutput()
	{
	}

	private T LoadAsset<T>(string assetBundlePath, string assetName, string manifest = "") where T : UnityEngine.Object
	{
		T val = CommonLib.LoadAsset<T>(assetBundlePath, assetName, clone: false, manifest);
		if (val == null)
		{
			return (T)null;
		}
		if (!MapScene.AssetBundlePaths.Exists((UnityEx.ValueTuple<string, string> x) => x.Item1 == assetBundlePath && x.Item2 == manifest))
		{
			MapScene.AssetBundlePaths.Add(new UnityEx.ValueTuple<string, string>(assetBundlePath, manifest));
		}
		return val;
	}

	public static bool AnyUIActive()
	{
		if (!Singleton<MapUIContainer>.IsInstance())
		{
			return false;
		}
		return Singleton<MapUIContainer>.Instance._commandList.IsActiveControl || Singleton<MapUIContainer>.Instance._choiceUI.IsActiveControl || Singleton<MapUIContainer>.Instance._itemBoxUI.IsActiveControl || Singleton<MapUIContainer>.Instance._charaEntryUI.IsActiveControl || Singleton<MapUIContainer>.Instance._charaChangeUI.IsActiveControl || Singleton<MapUIContainer>.Instance._playerChangeUI.IsActiveControl || Singleton<MapUIContainer>.Instance._charaLookEditUI.IsActiveControl || Singleton<MapUIContainer>.Instance._playerLookEditUI.IsActiveControl || Singleton<MapUIContainer>.Instance._charaMigrateUI.IsActiveControl || Singleton<MapUIContainer>.Instance._dressRoomUI.IsActiveControl || Singleton<MapUIContainer>.Instance._closetUI.IsActiveControl || Singleton<MapUIContainer>.Instance._RefrigeratorUI.IsActiveControl || Singleton<MapUIContainer>.Instance._CookingUI.IsActiveControl || Singleton<MapUIContainer>.Instance._MedicineCraftUI.IsActiveControl || Singleton<MapUIContainer>.Instance._PetCraftUI.IsActiveControl || Singleton<MapUIContainer>.Instance._FarmlandUI.IsActiveControl || Singleton<MapUIContainer>.Instance._ChickenCoopUI.IsActiveControl || Singleton<MapUIContainer>.Instance._petHomeUI.IsActiveControl || Singleton<MapUIContainer>.Instance._jukeBoxUI.IsActiveControl || Singleton<MapUIContainer>.Instance._systemMenuUI.IsActiveControl || Singleton<MapUIContainer>.Instance._requestUI.IsActiveControl || Singleton<MapUIContainer>.Instance._tutorialUI.IsActiveControl || Singleton<MapUIContainer>.Instance._eventDialogUI.IsActiveControl || Singleton<MapUIContainer>.Instance._recyclingUI.IsActiveControl || Singleton<Manager.ADV>.Instance.Captions.Active || Singleton<MapUIContainer>.Instance._minimapUI.AllAreaMap.activeSelf || Singleton<Manager.Resources>.Instance.HSceneTable.HSceneUISet.activeSelf || (Singleton<Manager.Housing>.IsInstance() && Singleton<Manager.Housing>.Instance.IsCraft) || (Singleton<Game>.IsInstance() && Singleton<Game>.Instance.MapShortcutUI != null) || (Singleton<Game>.IsInstance() && Singleton<Game>.Instance.Config != null) || (Singleton<Game>.IsInstance() && Singleton<Game>.Instance.Dialog != null) || (Singleton<Game>.IsInstance() && Singleton<Game>.Instance.ExitScene != null);
	}

	private void Start()
	{
		DefinePack.AssetBundlePathsDefine aBPaths = Singleton<Manager.Resources>.Instance.DefinePack.ABPaths;
		DefinePack.AssetBundleManifestsDefine aBManifests = Singleton<Manager.Resources>.Instance.DefinePack.ABManifests;
		Transform parent = _hudCanvas.transform;
		Debug.Log("通知UI生成");
		GameObject original = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "NotifyLog", aBManifests.Default);
		_notifyPanel = UnityEngine.Object.Instantiate(original, parent, worldPositionStays: false).GetComponent<NotifyMessageList>();
		Debug.Log("リザルトUI生成");
		GameObject original2 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "ResultMessageUI", aBManifests.Default);
		_resultMessageUI = UnityEngine.Object.Instantiate(original2, parent, worldPositionStays: false).GetComponent<ResultMessageUI>();
		Debug.Log("ストーリーサポートUI生成");
		GameObject original3 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "StorySupportUI", aBManifests.Default);
		_storySupportUI = UnityEngine.Object.Instantiate(original3, parent, worldPositionStays: false).GetComponent<StorySupportUI>();
		Debug.Log("ワーニングUI生成");
		GameObject original4 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "WarningMessageUI", aBManifests.Default);
		_warningMessageUI = UnityEngine.Object.Instantiate(original4, parent, worldPositionStays: false).GetComponent<WarningMessageUI>();
		Debug.Log("点UI生成");
		GameObject original5 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "CenterPointer", aBManifests.Default);
		_centerPointerCanvasGroup = UnityEngine.Object.Instantiate(original5, parent, worldPositionStays: false).GetComponent<CanvasGroup>();
		Debug.Log("ミニマップ生成");
		GameObject gameObject = UnityEngine.Object.Instantiate(_MiniMapObject);
		_minimapUI = gameObject.GetComponentInChildren<MiniMapControler>();
		GameObject original6 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "MiniMapUI", aBManifests.Default);
		_minimapCanvasGroup = UnityEngine.Object.Instantiate(original6, parent, worldPositionStays: false).GetComponent<CanvasGroup>();
		Transform parent2 = _commandCanvas.transform;
		GameObject original7 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "NicknameUI", aBManifests.Default);
		_nicknameUI = UnityEngine.Object.Instantiate(original7, parent2, worldPositionStays: false).GetComponent<AnimalNicknameOutput>();
		Debug.Log("コマンドラベル生成");
		GameObject original8 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "CommandLabel", aBManifests.Default);
		_commandLabel = UnityEngine.Object.Instantiate(original8, parent2, worldPositionStays: false).GetComponent<CommandLabel>();
		Debug.Log("ADVWindow生成");
		GameObject original9 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "ADVWindow", aBManifests.Default);
		_advScene = UnityEngine.Object.Instantiate(original9, parent2, worldPositionStays: false).GetComponentInChildren<ADVScene>(includeInactive: true);
		ParameterList.Add(new ADVParames(_advData));
		Debug.Log("ADV選択肢UI生成");
		GameObject original10 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "ChoiceUI", aBManifests.Default);
		_choiceUI = UnityEngine.Object.Instantiate(original10, parent2, worldPositionStays: false).GetComponent<CommCommandList>();
		Debug.Log("コマンドリスト生成");
		GameObject original11 = LoadAsset<GameObject>(aBPaths.MapScenePrefabAdd05, "CommandList", aBManifests.Add05);
		_commandList = UnityEngine.Object.Instantiate(original11, parent2, worldPositionStays: false).GetComponent<CommCommandList>();
		Debug.Log("メニューUI生成");
		GameObject original12 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "MenuUI", aBManifests.Default);
		_systemMenuUI = UnityEngine.Object.Instantiate(original12, parent2, worldPositionStays: false).GetComponent<SystemMenuUI>();
		Debug.Log("ゲームログUI生成");
		GameObject original13 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "GameLog", aBManifests.Default);
		_gameLogUI = UnityEngine.Object.Instantiate(original13, parent2, worldPositionStays: false).GetComponent<GameLog>();
		Debug.Log("アイテムボックスUI生成");
		GameObject original14 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "ItemBoxUI", aBManifests.Default);
		_itemBoxUI = UnityEngine.Object.Instantiate(original14, parent2, worldPositionStays: false).GetComponent<ItemBoxUI>();
		Debug.Log("脱衣所UIの作成");
		GameObject original15 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "DressRoomUI", aBManifests.Default);
		_dressRoomUI = UnityEngine.Object.Instantiate(original15, parent2, worldPositionStays: false).GetComponent<ClosetUI>();
		Debug.Log("衣装棚UIの作成");
		GameObject original16 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "ClosetUI", aBManifests.Default);
		_closetUI = UnityEngine.Object.Instantiate(original16, parent2, worldPositionStays: false).GetComponent<ClosetUI>();
		Debug.Log("登場キャラ選択UI生成");
		GameObject original17 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "CharaEntryUI", aBManifests.Default);
		_charaEntryUI = UnityEngine.Object.Instantiate(original17, parent2, worldPositionStays: false).GetComponent<CharaEntryUI>();
		Debug.Log("女の子編集UI生成");
		GameObject original18 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "CharaChangeUI", aBManifests.Default);
		_charaChangeUI = UnityEngine.Object.Instantiate(original18, parent2, worldPositionStays: false).GetComponent<CharaChangeUI>();
		Debug.Log("主人公編集UI生成");
		GameObject original19 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "PlayerChangeUI", aBManifests.Default);
		_playerChangeUI = UnityEngine.Object.Instantiate(original19, parent2, worldPositionStays: false).GetComponent<PlayerChangeUI>();
		Debug.Log("女の子容姿変更UI生成");
		GameObject original20 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "CharaLookEditUI", aBManifests.Default);
		_charaLookEditUI = UnityEngine.Object.Instantiate(original20, parent2, worldPositionStays: false).GetComponent<CharaLookEditUI>();
		Debug.Log("主人公容姿変更UI生成");
		GameObject original21 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "PlayerLookEditUI", aBManifests.Default);
		_playerLookEditUI = UnityEngine.Object.Instantiate(original21, parent2, worldPositionStays: false).GetComponent<PlayerLookEditUI>();
		Debug.Log("女の子島変更UI生成");
		GameObject original22 = LoadAsset<GameObject>(aBPaths.MapScenePrefabAdd05, "CharaMigrateUI", aBManifests.Add05);
		_charaMigrateUI = UnityEngine.Object.Instantiate(original22, parent2, worldPositionStays: false).GetComponent<CharaMigrateUI>();
		Debug.Log("釣りUI生成");
		GameObject gameObject2 = LoadAsset<GameObject>(aBPaths.MapScenePrefabAdd05, "FishingUI", aBManifests.Add05);
		_fishingUI = ((!(gameObject2 == null)) ? UnityEngine.Object.Instantiate(gameObject2, parent2, worldPositionStays: false).GetComponent<FishingUI>() : null);
		GameObject gameObject3 = LoadAsset<GameObject>(aBPaths.MapScenePrefabAdd05, "RequestUI", aBManifests.Add05);
		_requestUI = ((!(gameObject3 == null)) ? UnityEngine.Object.Instantiate(gameObject3, parent2, worldPositionStays: false).GetComponent<RequestUI>() : null);
		GameObject original23 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "PhotoShotUI", aBManifests.Default);
		_photoShotUI = UnityEngine.Object.Instantiate(original23, parent2, worldPositionStays: false).GetComponent<PhotoShotUI>();
		GameObject original24 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "SpendMoneyUI", aBManifests.Default);
		_spendMoneyUI = UnityEngine.Object.Instantiate(original24, parent2, worldPositionStays: false).GetComponent<SpendMoneyUI>();
		GameObject original25 = LoadAsset<GameObject>(aBPaths.MapScenePrefabAdd12, "AllAreaMap", aBManifests.Add12);
		_AllAreaMapUI = UnityEngine.Object.Instantiate(original25, parent2, worldPositionStays: false).GetComponent<AllAreaMapUI>();
		GameObject original26 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "ShopUI", aBManifests.Default);
		_ShopUI = UnityEngine.Object.Instantiate(original26, parent2, worldPositionStays: false).GetComponent<ShopUI>();
		GameObject original27 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "ScroungeUI", aBManifests.Default);
		_ScroungeUI = UnityEngine.Object.Instantiate(original27, parent2, worldPositionStays: false).GetComponent<ScroungeUI>();
		GameObject original28 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "RefrigeratorUI", aBManifests.Default);
		_RefrigeratorUI = UnityEngine.Object.Instantiate(original28, parent2, worldPositionStays: false).GetComponent<RefrigeratorUI>();
		GameObject original29 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "CraftUI", aBManifests.Default);
		_CraftUI = UnityEngine.Object.Instantiate(original29, parent2, worldPositionStays: false).GetComponent<CraftUI>();
		GameObject original30 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "CookingUI", aBManifests.Default);
		_CookingUI = UnityEngine.Object.Instantiate(original30, parent2, worldPositionStays: false).GetComponent<CraftUI>();
		GameObject original31 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "PetCraftUI", aBManifests.Default);
		_PetCraftUI = UnityEngine.Object.Instantiate(original31, parent2, worldPositionStays: false).GetComponent<CraftUI>();
		GameObject original32 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "MedicineCraftUI", aBManifests.Default);
		_MedicineCraftUI = UnityEngine.Object.Instantiate(original32, parent2, worldPositionStays: false).GetComponent<CraftUI>();
		GameObject gameObject4 = LoadAsset<GameObject>(aBPaths.MapScenePrefabAdd12, "RecyclingUI", aBManifests.Add12);
		if (gameObject4 != null)
		{
			_recyclingUI = UnityEngine.Object.Instantiate(gameObject4, parent2, worldPositionStays: false).GetComponent<RecyclingUI>();
		}
		else
		{
			_recyclingUI = null;
		}
		GameObject original33 = LoadAsset<GameObject>(aBPaths.MapScenePrefabAdd07, "FarmlandUI", aBManifests.Add07);
		_FarmlandUI = UnityEngine.Object.Instantiate(original33, parent2, worldPositionStays: false).GetComponent<FarmlandUI>();
		GameObject original34 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "ChickenCoopUI", aBManifests.Default);
		_ChickenCoopUI = UnityEngine.Object.Instantiate(original34, parent2, worldPositionStays: false).GetComponent<ChickenCoopUI>();
		GameObject original35 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "PetHomeUI", aBManifests.Default);
		_petHomeUI = UnityEngine.Object.Instantiate(original35, parent2, worldPositionStays: false).GetComponent<PetHomeUI>();
		GameObject original36 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "JukeBoxUI", aBManifests.Default);
		_jukeBoxUI = UnityEngine.Object.Instantiate(original36, parent2, worldPositionStays: false).GetComponent<JukeBoxUI>();
		GameObject original37 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "EventDialogUI", aBManifests.Default);
		_eventDialogUI = UnityEngine.Object.Instantiate(original37, parent2, worldPositionStays: false).GetComponent<EventDialogUI>();
		GameObject original38 = LoadAsset<GameObject>(aBPaths.MapScenePrefab, "TutorialUI", aBManifests.Default);
		_tutorialUI = UnityEngine.Object.Instantiate(original38, parent2, worldPositionStays: false).GetComponent<TutorialUI>();
	}

	private void Update()
	{
		if (_centerPointerCanvasGroup != null)
		{
			_centerPointerCanvasGroup.gameObject.SetActiveIfDifferent(Config.GameData.CenterPointer);
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		ParameterList.Remove(_advData);
	}
}
