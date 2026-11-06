using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ADV;
using ActionGame;
using ActionGame.Chara;
using Manager;
using SaveData;
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;
using ksPlug.Character;
using ksPlug.HSession;
using ksPlug.Inputs;
using ksPlug.Menu;
using ksPlug.Shared;
using ksPlug.Story;
using ksPlug.Tools;

namespace ksPlug;

public class Dealer : MonoBehaviour
{
	public enum Context
	{
		Default,
		H,
		Event,
		Esthetic,
		EstResult
	}

	public enum GoToMyHouseCase
	{
		ByGame,
		ByCallHome,
		ByGetNews,
		ByEndDay
	}

	public enum MenuCtx
	{
		NotInMenu,
		SaveLoad,
		Booking,
		Album,
		Config
	}

	public enum MapAskAction
	{
		Toilets,
		Shower,
		Onanism,
		Love
	}

	public class StoryHInfo
	{
		public bool hStartOnMap;

		public bool isHomeH;

		public int mapIdAtStart;

		public Heroine mainGirlSavAtStart;

		public Heroine secGirlSavAtStart;

		public List<Heroine> girlsToMigrateInH = new List<Heroine>();

		public bool hasInvGirlsToMigate;

		public bool setByPrevInfo;

		public Map.AmbTyp ambAtStart;

		public Map.AmbTyp ambAtChange;

		public int numOfAmbChange;

		public bool endDayByAmbChng;

		public float hTime;

		public bool inGoToMyHouse;

		public bool AmbChangedInH => numOfAmbChange > 0;

		public bool UpdateTimerAtEnd
		{
			get
			{
				if (hStartOnMap && !AmbChangedInH)
				{
					return !endDayByAmbChng;
				}
				return false;
			}
		}

		public bool UpdateCycleAtEnd
		{
			get
			{
				if (hStartOnMap && AmbChangedInH && !endDayByAmbChng)
				{
					return ambAtChange > ambAtStart;
				}
				return false;
			}
		}

		public bool ConfirmNpcCall
		{
			get
			{
				if (hStartOnMap && !AmbChangedInH)
				{
					return !UpdateCycleAtEnd;
				}
				return false;
			}
		}

		public bool EndDay
		{
			get
			{
				if (hStartOnMap)
				{
					return endDayByAmbChng;
				}
				return false;
			}
		}

		public bool IsSexAtHome
		{
			get
			{
				if (isHomeH)
				{
					return mapIdAtStart == Core.dealer.MapID;
				}
				return false;
			}
		}
	}

	public class Menus
	{
		public BaseMenu config;

		public BaseMenu jukbox;

		public BaseMenu diary;

		public BaseMenu dress;
	}

	private const string cmpName = "Dealer";

	public static readonly List<Context> FreeCamPossibleCtxs = new List<Context>
	{
		Context.Event,
		Context.EstResult
	};

	public Mouse.MouseInfo mInfos = new Mouse.MouseInfo();

	public List<Mover> ChaMovers = new List<Mover>();

	public List<ChaCore> ChaCoresInH = new List<ChaCore>();

	public List<ChaCore> ChaCoresInEst = new List<ChaCore>();

	public List<Heroine> HeroinesBeforeTrspass = new List<Heroine>();

	public bool ecchiEndByTresspass;

	public float[] tresspassTimes = new float[2];

	public Menus DMenus = new Menus();

	public Events.Info eventInfo = new Events.Info();

	public Heroine girlInEsthetic;

	public ChaControl EventCha;

	public GoToMyHouseCase goToMyHouseCase;

	private Type cycleTyp = (Type)4;

	private List<NPC> npcs = new List<NPC>();

	private List<Heroine> heroinesOnMap = new List<Heroine>();

	public FreeCam freeCam;

	private List<Texture2D[]> CursorTxts = new List<Texture2D[]>();

	private int mapNo = -1;

	private bool inGuestSwap;

	private bool inGuestKiss;

	private bool waitForBindersDestroy;

	private bool tweakNpcStat;

	private bool allowSavTweaks = true;

	public bool justLoadSave;

	private string scenario = string.Empty;

	public bool OnTitle
	{
		get
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			Scene curScene = Core.CurScene;
			return ((Scene)(ref curScene)).name == "Title";
		}
	}

	public bool InActScene => Singleton<ActionScene>.IsInstance();

	public bool InProcess => InChaSwap;

	public bool InConfig => GameMenuCtx == MenuCtx.Config;

	public bool InCharaMaker
	{
		get
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			Scene curScene = Core.CurScene;
			return ((Scene)(ref curScene)).name == "CustomScene";
		}
	}

	public bool InFreeHSel
	{
		get
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			Scene curScene = Core.CurScene;
			return ((Scene)(ref curScene)).name == "FreeH";
		}
	}

	public bool NotInMenu => GameMenuCtx == MenuCtx.NotInMenu;

	public bool CursorOverUIS
	{
		get
		{
			if (DMenus != null)
			{
				if ((!Object.op_Implicit((Object)(object)DMenus.config) || !DMenus.config.IsCursorOver()) && (!Object.op_Implicit((Object)(object)DMenus.jukbox) || !DMenus.jukbox.IsCursorOver()) && (!Object.op_Implicit((Object)(object)DMenus.diary) || !DMenus.diary.IsCursorOver()))
				{
					if (Object.op_Implicit((Object)(object)DMenus.dress))
					{
						return DMenus.dress.IsCursorOver();
					}
					return false;
				}
				return true;
			}
			return false;
		}
	}

	public bool IsEventCtx
	{
		get
		{
			if (Ctx != Context.Event)
			{
				return Ctx == Context.EstResult;
			}
			return true;
		}
	}

	public bool InChaSwap => inGuestSwap;

	public bool InKissAI => inGuestKiss;

	public int MapID
	{
		get
		{
			if (!((Object)(object)ActMap != (Object)null))
			{
				return -1;
			}
			return ((BaseMap)ActMap).no;
		}
	}

	public Type NowCycle
	{
		get
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			if (!((Object)(object)ActMap != (Object)null))
			{
				return (Type)4;
			}
			return ActMap.nowCycle;
		}
	}

	public Player Player
	{
		get
		{
			if (!((Object)(object)SingletonInitializer<ActionScene>.instance != (Object)null) || !((Object)(object)SingletonInitializer<ActionScene>.instance.Player != (Object)null))
			{
				return null;
			}
			return SingletonInitializer<ActionScene>.instance.Player;
		}
	}

	public ChaControl MainGirlCha
	{
		get
		{
			if (!InH)
			{
				if (!IsEventCtx || eventInfo.chas.Count <= 0)
				{
					if (!((Object)(object)EstCtrl != (Object)null) || !((Object)(object)EstCtrl.Cha != (Object)null))
					{
						return null;
					}
					return EstCtrl.Cha;
				}
				return eventInfo.chas[0];
			}
			return HCtrl.MainGirl.Cha;
		}
	}

	public ActionScene ActScene => Singleton<ActionScene>.Instance;

	public bool PlayerIsChase
	{
		get
		{
			if ((Object)(object)ActScene != (Object)null && (Object)(object)ActScene.Player != (Object)null)
			{
				return (Object)(object)ActScene.Player.chaser != (Object)null;
			}
			return false;
		}
	}

	public bool InH => (Object)(object)HCtrl != (Object)null;

	public HAnims.Context HCtx
	{
		get
		{
			if (!InH || HCtrl.CurAnim == null)
			{
				return HAnims.Context.Undefined;
			}
			return HCtrl.CurAnim.ctx;
		}
	}

	public HAnims.State HState
	{
		get
		{
			if (!InH)
			{
				return HAnims.State.Undefined;
			}
			return HCtrl.CurState;
		}
	}

	public bool InMoversCustomMove
	{
		get
		{
			if (InH)
			{
				return (Object)(object)HCtrl.moverInCustomMove != (Object)null;
			}
			return false;
		}
	}

	public bool InHFPV
	{
		get
		{
			if (InH)
			{
				return (Object)(object)HCtrl.FpvCtrl != (Object)null;
			}
			return false;
		}
	}

	public bool BoyIsVisInH
	{
		get
		{
			if (InH && HCtrl.CurAnim != null && HCtrl.CurAnim.ctx != HAnims.Context.Tease && HCtrl.CurAnim.ctx != HAnims.Context.Peep)
			{
				return HCtrl.CurAnim.ctx != HAnims.Context.Undefined;
			}
			return false;
		}
	}

	public ChaControl MainBoyCha
	{
		get
		{
			if (!InH)
			{
				return null;
			}
			return HCtrl.MainBoy.Cha;
		}
	}

	public ActionMap ActMap { get; private set; }

	public bool InStory { get; private set; }

	public Context Ctx { get; private set; }

	public MenuCtx GameMenuCtx { get; private set; }

	public Messenger Msgn { get; private set; }

	public EstheticCtrl EstCtrl { get; private set; }

	public HCtrl HCtrl { get; private set; }

	public bool GirlHasThink { get; private set; }

	public int FreeHtimeZone { get; private set; }

	public int FreeHexpAtStart { get; private set; }

	public ChaFileControl PlayerChaFile { get; private set; }

	public Base NpcBaseInH { get; private set; }

	public CtrlHome CtrlHome { get; private set; }

	public StoryHInfo StoryHInf { get; private set; }

	public FreeCam FreeCam => freeCam;

	public static ChaTools.ColDefinition ColDefUnderCursor { get; private set; }

	public bool HasFreeCam => (Object)(object)freeCam != (Object)null;

	public bool InFreeCam
	{
		get
		{
			if (HasFreeCam)
			{
				return !freeCam.isDisabled;
			}
			return false;
		}
	}

	public bool InKissOrGrab { get; private set; }

	public string Scenario => scenario;

	public bool InWaitForResultGauge { get; private set; }

	public void SetInKissOrGrab()
	{
		((MonoBehaviour)this).StartCoroutine(IE_SetInKissOrGrab());
	}

	private IEnumerator IE_SetInKissOrGrab()
	{
		InKissOrGrab = true;
		yield return (object)new WaitUntil((Func<bool>)(() => !Keys.LmbHold));
		InKissOrGrab = false;
	}

	private void Start()
	{
		Ctx = Context.Default;
		GameMenuCtx = MenuCtx.NotInMenu;
		CursorTxts = GameAssets.GetCursorTextures();
		Msgn = ((Component)this).gameObject.AddComponent<Messenger>();
		HExtraAnims.GetExtraAnimInfoOnStart();
		Statistic.InitDictionaries();
		Map.InitMapInfoDictionary();
		DealTool.UnlockAllEcchiPoses();
		Binders.ReadBinderUserPref();
		Binders.ReadAll();
		Music.ListBgms();
		SizeOffsets.ReadAll();
		DMenus.config = BaseMenu.Create(((Component)this).gameObject, BaseMenu.Type.Config);
		DMenus.jukbox = BaseMenu.Create(((Component)this).gameObject, BaseMenu.Type.Jukebox);
		DMenus.diary = BaseMenu.Create(((Component)this).gameObject, BaseMenu.Type.Diary);
		DebugInfos.InConsole("Start", "Dealer");
	}

	private void Update()
	{
		//IL_031e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0324: Unknown result type (might be due to invalid IL or missing references)
		//IL_032f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0334: Unknown result type (might be due to invalid IL or missing references)
		//IL_0338: Unknown result type (might be due to invalid IL or missing references)
		//IL_033d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0359: Unknown result type (might be due to invalid IL or missing references)
		//IL_035f: Invalid comparison between Unknown and I4
		//IL_038b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0391: Invalid comparison between Unknown and I4
		//IL_03d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03df: Invalid comparison between Unknown and I4
		//IL_0378: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e8: Invalid comparison between Unknown and I4
		//IL_03f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f1: Invalid comparison between Unknown and I4
		//IL_063d: Unknown result type (might be due to invalid IL or missing references)
		if (InCharaMaker)
		{
			return;
		}
		if (InStory && (Object)(object)ActMap == (Object)null)
		{
			ActMap = Singleton<ActionScene>.Instance.Map;
		}
		if (InStory && Object.op_Implicit((Object)(object)Singleton<ActionScene>.Instance) && (Object)(object)Singleton<ActionScene>.Instance.AdvScene != (Object)null && Object.op_Implicit((Object)(object)Singleton<ActionScene>.Instance.AdvScene.Scenario))
		{
			TextScenario val = Singleton<ActionScene>.Instance.AdvScene.Scenario;
			if (!string.IsNullOrEmpty(val.LoadAssetName) && scenario != val.LoadAssetName)
			{
				scenario = val.LoadAssetName;
				if (scenario == "penetration_heroine")
				{
					HScene instance = Singleton<HScene>.Instance;
					if ((Object)(object)instance != (Object)null)
					{
						List<Heroine> fieldValue = instance.GetFieldValue<List<Heroine>>("heroineList");
						ChaCore[] array = Object.FindObjectsOfType<ChaCore>();
						foreach (ChaCore chaCore in array)
						{
							if (!((ChaInfo)chaCore.Cha).visibleAll || (Object)(object)chaCore.Npc != (Object)null || (Object)(object)chaCore.FixNpc != (Object)null)
							{
								continue;
							}
							foreach (Heroine item in fieldValue)
							{
								if (((CharaData)item).parameter.fullname == ((ChaInfo)chaCore.Cha).fileParam.fullname)
								{
									GManage.GetAndApplySnapshot((CharaData)(object)item, chaCore.Cha);
								}
							}
						}
					}
				}
				if (eventInfo != null && eventInfo.saves.Count != 0 && Statistic.GetDaily(eventInfo.saves[0]) != null)
				{
					Statistic.AddActivityToDaily(eventInfo.saves[0], scenario);
				}
				DebugInfos.InConsole(" Scenario changed to " + val.LoadAssetName);
				if (Core.debug)
				{
					Msgn.ReceiveMessage(Messenger.Type.TopCorner_R, " Scenario is " + val.LoadAssetName, 5f);
				}
			}
		}
		if (InFreeHSel && !Scene.IsOverlap && !Scene.IsNowLoading && !Scene.IsFadeNow)
		{
			FreeHScene val2 = Object.FindObjectOfType<FreeHScene>();
			if ((Object)(object)val2 != (Object)null)
			{
				Member fieldValue2 = val2.GetFieldValue<Member>("member");
				if (fieldValue2 != null)
				{
					FreeHtimeZone = ((ReactiveProperty<int>)(object)fieldValue2.resultTimeZone).Value;
					FreeHexpAtStart = ((ReactiveProperty<int>)(object)fieldValue2.resultStage1).Value;
				}
			}
		}
		if (Ctx == Context.H && (Object)(object)ActMap == (Object)null && Object.op_Implicit((Object)(object)Singleton<ActionMap>.Instance))
		{
			ActMap = Singleton<ActionMap>.Instance;
			((MonoBehaviour)this).StartCoroutine(IE_GetPlayerAppearence(freeH: true));
		}
		if ((Object)(object)ActMap != (Object)null)
		{
			if (mapNo != MapID)
			{
				mapNo = MapID;
				DebugInfos.InConsole("Map changed to " + Map.GetName(MapID), "Dealer");
			}
			if (cycleTyp != NowCycle)
			{
				Type cycle = cycleTyp;
				cycleTyp = NowCycle;
				if (InStory && !InH)
				{
					if ((int)cycleTyp == 5 && (StoryHInf == null || !StoryHInf.inGoToMyHouse))
					{
						((MonoBehaviour)this).StartCoroutine(IE_AfterGoToMyHouse(Map.AmbianceType(cycle)));
					}
					if ((int)cycleTyp == 1)
					{
						foreach (Statistic.Daily item2 in Statistic.AllDailies())
						{
							Statistic.RemoveNegEventsFromSave(item2.save);
						}
						((MonoBehaviour)this).StartCoroutine(IE_SearchMorningNPC());
					}
					if ((int)cycleTyp == 2 || (int)cycleTyp == 3 || (int)cycleTyp == 4)
					{
						DealTool.DealerCycleChange();
					}
					if ((int)cycleTyp == 0)
					{
						if (justLoadSave)
						{
							justLoadSave = false;
						}
						else
						{
							DealCha.DeleteNpcsExtraComp();
							((MonoBehaviour)this).StartCoroutine(IE_AtWakeUp(applyPregDay: true));
						}
					}
					if (StoryHInf != null)
					{
						StoryHInf = null;
					}
				}
				GManage.ClearChaSnapshot();
				DebugInfos.InConsole("Cycle changed to " + ((object)System.Runtime.CompilerServices.Unsafe.As<Type, Type>(ref cycleTyp)/*cast due to .constrained prefix*/).ToString(), "Dealer");
			}
		}
		if ((Ctx == Context.Event || Ctx == Context.Esthetic || Ctx == Context.EstResult) && eventInfo != null && eventInfo.saves.Count != 0)
		{
			Statistic.SetDailyEvent(eventInfo.saves[0], new List<Statistic.DailyEvent> { Statistic.DailyEvent.timeTogether });
		}
		if (InStory && Object.op_Implicit((Object)(object)SingletonInitializer<ActionScene>.instance) && (Object)(object)SingletonInitializer<ActionScene>.instance.Player != (Object)null && Ctx == Context.Default)
		{
			Player player = SingletonInitializer<ActionScene>.instance.Player;
			if ((Object)(object)player.chaser != (Object)null && ((Base)player.chaser).heroine != null)
			{
				Statistic.SetDailyEvent(((Base)player.chaser).heroine, new List<Statistic.DailyEvent> { Statistic.DailyEvent.timeTogether });
			}
		}
		if (Ctx == Context.H && (Object)(object)HCtrl != (Object)null && (Object)(object)HCtrl.HFlag != (Object)null && HCtrl.HFlag.isHSceneEnd)
		{
			HManage.EndH();
			DebugInfos.InConsoleCompact("!!! Ending H scene from HFlag end scene rather than HProc", "Dealer");
			SetContext(Context.Default);
		}
		if ((Object)(object)freeCam != (Object)null && Ctx != Context.Event && !CtrlHome.IsActive)
		{
			Object.Destroy((Object)(object)freeCam);
		}
		if (Ctx != Context.H && Ctx != Context.Esthetic && Ctx != Context.EstResult)
		{
			if ((Object)(object)EventCha != (Object)null && Ctx != Context.Event)
			{
				SetContext(Context.Event);
			}
			if ((Object)(object)EventCha == (Object)null && Ctx == Context.Event)
			{
				SetContext(Context.Default);
			}
			if ((Object)(object)Singleton<HomeMenu>.Instance != (Object)null && Singleton<HomeMenu>.Instance.visible && (Object)(object)CtrlHome == (Object)null)
			{
				CtrlHome = ((Component)this).gameObject.AddComponent<CtrlHome>();
			}
			if ((int)NowCycle == 0 && Map.RoomIDS.Contains(MapID) && allowSavTweaks && Core.debug && Keys.ShiftMod && Keys.AltMod && Keys.CtrlMod && Input.GetKeyDown((KeyCode)8))
			{
				DealTool.CorrectNamesAndTweakRelations();
			}
			SetGameMenuCTx();
		}
	}

	private void LateUpdate()
	{
		if (InCharaMaker)
		{
			return;
		}
		if (mInfos != null)
		{
			Mouse.FeedMouseInfo(ref mInfos);
		}
		if (IsEventCtx)
		{
			if (Keys.EnterOrResetFPV && (Object)(object)MainGirlCha != (Object)null)
			{
				if ((Object)(object)freeCam == (Object)null)
				{
					AddFreeCam(FreeCam.GetTargFromBodyPart(MainGirlCha, ChaTools.Part.Head));
				}
				else
				{
					Object.Destroy((Object)(object)freeCam);
				}
			}
			if (mInfos.lmbShortPress && (Object)(object)MainGirlCha != (Object)null)
			{
				ChaTools.ColDefinition colDefinition = ChaTools.SearchColDefUnderCursor();
				if ((Object)(object)colDefinition != (Object)null && Events.DoTouch(colDefinition))
				{
					IncrFavAndLewd(eventInfo.saves[0]);
				}
			}
			if (InKissOrGrab && (Object)(object)MainGirlCha != (Object)null)
			{
				Statistic.AddLewdness(eventInfo.saves[0], mInfos.dragLenght);
			}
		}
		DealCha.Inputs();
		if (Ctx == Context.H && !Scene.IsNowLoading && !Scene.IsOverlap)
		{
			HManage.DealerInputs();
			HManage.PublicTouchIcons();
		}
		if (Ctx != Context.H && ChaCoresInH.Count != 0)
		{
			ChaCoresInH.Clear();
		}
		if (Ctx != Context.H && !InCharaMaker)
		{
			((MonoBehaviour)this).StartCoroutine(IE_TrackColDefUnderCurs());
		}
		if ((Object)(object)DMenus.config != (Object)null)
		{
			if ((Scene.IsOverlap || Scene.IsNowLoading || Scene.IsFadeNow) && DMenus.config.ShowMenu)
			{
				DMenus.config.ShowMenu = false;
			}
			if (((!InCharaMaker && !InFreeHSel && !Scene.IsOverlap && !Scene.IsNowLoading && !Scene.IsFadeNow && NotInMenu && GameCursor.isDraw) || DMenus.config.ShowMenu) && Keys.ConfigMenu)
			{
				DMenus.config.ShowMenu = !DMenus.config.ShowMenu;
			}
		}
		if ((Object)(object)DMenus.jukbox != (Object)null)
		{
			if ((Scene.IsOverlap || Scene.IsNowLoading || Scene.IsFadeNow) && DMenus.jukbox.ShowMenu)
			{
				DMenus.jukbox.ShowMenu = false;
			}
			if (((!Scene.IsOverlap && !Scene.IsNowLoading && !Scene.IsFadeNow && GameCursor.isDraw && NotInMenu && !DMenus.config.ShowMenu) || DMenus.jukbox.ShowMenu) && Keys.MusicMenu)
			{
				DMenus.jukbox.ShowMenu = !DMenus.jukbox.ShowMenu;
			}
		}
		if ((Object)(object)DMenus.diary != (Object)null)
		{
			if ((Scene.IsNowLoading || Scene.IsFadeNow || OnTitle) && DMenus.diary.ShowMenu)
			{
				DMenus.diary.ShowMenu = false;
			}
			if (((InStory && !InCharaMaker && NotInMenu && !Scene.IsOverlap && !Scene.IsNowLoading && !Scene.IsFadeNow && GameCursor.isDraw) || DMenus.diary.ShowMenu) && Keys.DiaryMenu)
			{
				DMenus.diary.ShowMenu = !DMenus.diary.ShowMenu;
			}
		}
		if (InStory && ((CtrlHome.IsActive && (Object)(object)freeCam != (Object)null) || (Ctx == Context.Default && MapID == 32 && PlayerIsChase) || (Ctx == Context.Event && MapID == 32)) && Keys.DressMenu)
		{
			if ((Object)(object)DMenus.dress == (Object)null)
			{
				DMenus.dress = BaseMenu.Create(((Component)this).gameObject, BaseMenu.Type.Dress);
			}
			DMenus.dress.ShowMenu = !DMenus.dress.ShowMenu;
		}
		DealTool.ParseInteractiveOptions();
		if (Ctx == Context.Default && InStory)
		{
			DealTool.ParseRefreshableOptions();
		}
	}

	private void OnGUI()
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		if (Core.debug)
		{
			Event current = Event.current;
			if (current.isKey && Input.GetKeyUp(current.keyCode))
			{
				Console.WriteLine(" catched key press " + ((object)current.keyCode/*cast due to .constrained prefix*/).ToString(), "Dealer");
			}
		}
	}

	public void AddFreeCam(Transform target)
	{
		freeCam = FreeCam.Create(target);
	}

	private IEnumerator IE_TrackColDefUnderCurs()
	{
		yield return (object)new WaitForEndOfFrame();
		ColDefUnderCursor = ChaTools.SearchColDefUnderCursor();
	}

	public void SetOrCreateDressMenuInH(ChaControl menuTargetCha)
	{
		if ((Object)(object)DMenus.dress == (Object)null)
		{
			DMenus.dress = BaseMenu.Create(((Component)this).gameObject, BaseMenu.Type.Dress);
		}
		MenuDress.SetChaInH(menuTargetCha);
		DMenus.dress.ShowMenu = true;
	}

	public static void IncrFavAndLewd(Heroine her = null, ChaControl cha = null)
	{
		if (her == null && (Object)(object)cha != (Object)null)
		{
			her = ChaCore.TryToGetSaveFromChaCore(((Component)cha).gameObject);
		}
		if (her != null)
		{
			Statistic.AddFavor(her, 2f);
			Statistic.AddLewdness(her, 3f);
		}
	}

	private IEnumerator IE_GetPlayerAppearence(bool freeH = false)
	{
		if (!freeH)
		{
			yield return (object)new WaitUntil((Func<bool>)(() => Object.op_Implicit((Object)(object)Singleton<ActionScene>.Instance) && (Object)(object)Singleton<ActionScene>.Instance.Player != (Object)null && (Object)(object)((Base)Singleton<ActionScene>.Instance.Player).chaCtrl != (Object)null));
		}
		if (freeH)
		{
			yield return (object)new WaitUntil((Func<bool>)(() => Object.op_Implicit((Object)(object)Singleton<HFlag>.Instance) && Singleton<HFlag>.Instance.player != null && (Object)(object)((CharaData)Singleton<HFlag>.Instance.player).chaCtrl != (Object)null));
		}
		PlayerChaFile = new ChaFileControl();
		((ChaFile)PlayerChaFile).CopyAll((ChaFile)(object)(freeH ? ((ChaInfo)((CharaData)Singleton<HFlag>.Instance.player).chaCtrl).chaFile : ((ChaInfo)((Base)Singleton<ActionScene>.Instance.Player).chaCtrl).chaFile));
		if (Object.op_Implicit((Object)(object)Singleton<ActionScene>.Instance))
		{
			DebugInfos.InConsole(" Stored from ActionScene", "Dealer.GetPlayerAppearence");
		}
		else if (Object.op_Implicit((Object)(object)Singleton<HFlag>.Instance))
		{
			DebugInfos.InConsole(" Stored from HFlag", "Dealer.GetPlayerAppearence");
		}
	}

	private void SetGameMenuCTx()
	{
		MenuCtx gameMenuCtx = GameMenuCtx;
		GameMenuCtx = MenuCtx.NotInMenu;
		if (Scene.IsOverlapFirst((IOverlap)(object)SingletonInitializer<ConfigScene>.instance))
		{
			GameMenuCtx = MenuCtx.Config;
		}
		if ((Object)(object)Singleton<ClassRoomSelectScene>.Instance != (Object)null)
		{
			GameMenuCtx = MenuCtx.Booking;
		}
		if (Scene.Overlaps.Count != 0)
		{
			foreach (IOverlap overlap in Scene.Overlaps)
			{
				if ((Object)(object)overlap.canvas != (Object)null && ((Object)overlap.canvas).name.Contains("SaveLoad"))
				{
					GameMenuCtx = MenuCtx.SaveLoad;
					break;
				}
				if ((Object)(object)overlap.canvas != (Object)null && ((Object)overlap.canvas).name.Contains("CapFileViewer"))
				{
					GameMenuCtx = MenuCtx.Album;
					break;
				}
			}
		}
		if (gameMenuCtx != GameMenuCtx)
		{
			DebugInfos.InConsole("Game menu context changed to " + GameMenuCtx);
		}
	}

	public void WaitAndSaveBinderUserPrefs()
	{
		if (!waitForBindersDestroy)
		{
			((MonoBehaviour)this).StartCoroutine(IE_WaitAndSaveBinderUserPrefs());
		}
	}

	private IEnumerator IE_WaitAndSaveBinderUserPrefs()
	{
		waitForBindersDestroy = true;
		yield return (object)new WaitUntil((Func<bool>)(() => Object.FindObjectsOfType<Binders.Binder>().Length == 0));
		Settings.SaveBinderEdits(Binders.BinderUserPrefs);
		waitForBindersDestroy = false;
	}

	public IEnumerator IE_WaitReturnOnMapAfterH()
	{
		bool hasResult = (Object)(object)HCtrl != (Object)null && HCtrl.HasResult;
		if (hasResult)
		{
			yield return (object)new WaitUntil((Func<bool>)(() => !Core.dealer.InWaitForResultGauge || ecchiEndByTresspass));
		}
		else
		{
			yield return (object)new WaitUntil((Func<bool>)(() => (Object)(object)Core.dealer.HCtrl == (Object)null || ecchiEndByTresspass));
		}
		bool num = ecchiEndByTresspass;
		DebugInfos.InConsole(" Waited until H end ", "Dealer.IE_WaitReturnOnMapAfterH");
		if (num)
		{
			yield break;
		}
		DebugInfos.InConsole(" Apply Time Result and Ambiance ", "Dealer.IE_WaitReturnOnMapAfterH");
		if (Settings.AhegaoStay == Settings.AhegPersist.No && Settings.AhegaoCount == Settings.AhegCount.EcchiSession)
		{
			DealTool.ResetDailiesAhegao();
		}
		if (StoryHInf != null && StoryHInf.mainGirlSavAtStart != null && (!Settings.TimePassesInH || (!StoryHInf.UpdateCycleAtEnd && !StoryHInf.EndDay)))
		{
			ChaCore chaCore = ChaCore.FindChaCoreNpcFromSave(StoryHInf.mainGirlSavAtStart, considerFix: true);
			if ((Object)(object)chaCore != (Object)null && (Object)(object)chaCore.CtrlNpc != (Object)null)
			{
				chaCore.CtrlNpc.ChaSnapshot = GManage.GetAndApplySnapshot((CharaData)(object)StoryHInf.mainGirlSavAtStart, chaCore.Cha, rmvKey: true);
			}
			if (StoryHInf.secGirlSavAtStart != null)
			{
				ChaCore chaCore2 = ChaCore.FindChaCoreNpcFromSave(StoryHInf.secGirlSavAtStart, considerFix: true);
				if ((Object)(object)chaCore2 != (Object)null && (Object)(object)chaCore2.CtrlNpc != (Object)null)
				{
					chaCore2.CtrlNpc.ChaSnapshot = GManage.GetAndApplySnapshot((CharaData)(object)StoryHInf.secGirlSavAtStart, chaCore2.Cha, rmvKey: true);
				}
			}
		}
		if (Settings.TimePassesInH && StoryHInf != null)
		{
			if (hasResult)
			{
				yield return (object)new WaitUntil((Func<bool>)(() => (Object)(object)Singleton<HResultGauge>.Instance == (Object)null));
				yield return (object)new WaitForSeconds(Time.deltaTime);
			}
			else
			{
				yield return (object)new WaitForSeconds(Time.deltaTime);
			}
			yield return (object)new WaitUntil((Func<bool>)(() => !Scene.IsOverlap && !Scene.IsNowLoading && !Scene.IsFadeNow));
			if (StoryHInf.UpdateTimerAtEnd)
			{
				float num2 = StoryHInf.hTime / 60f / (float)Settings.HPeriodDurStory;
				if (Singleton<Cycle>.Instance.timer / (float)Cycle.TIME_LIMIT + num2 <= 1f)
				{
					Singleton<Cycle>.Instance.AddTimer(num2);
				}
			}
			if (StoryHInf.UpdateCycleAtEnd)
			{
				Singleton<Cycle>.Instance.Change(Map.GetMapCycleFromAmb(StoryHInf.ambAtChange));
			}
			if (StoryHInf.EndDay)
			{
				if (StoryHInf.mainGirlSavAtStart != null)
				{
					Singleton<Cycle>.Instance.SetWithHeroine(StoryHInf.mainGirlSavAtStart, 0);
				}
				Map.AmbTyp ambAtChange = StoryHInf.ambAtChange;
				Core.dealer.goToMyHouseCase = GoToMyHouseCase.ByEndDay;
				StoryHInf.inGoToMyHouse = true;
				Singleton<Cycle>.Instance.Change((Type)5);
				((MonoBehaviour)this).StartCoroutine(IE_AfterGoToMyHouse(ambAtChange));
			}
		}
		yield return (object)new WaitForSeconds(5f);
		GManage.ClearChaSnapshot();
	}

	public void WakeUpWithGirl()
	{
		((MonoBehaviour)this).StartCoroutine(IE_WakeUpWithGirl());
	}

	private IEnumerator IE_WakeUpWithGirl()
	{
		Type cyclAtStart = NowCycle;
		Heroine withGirl = Singleton<Cycle>.Instance.withHeroine;
		yield return (object)new WaitUntil((Func<bool>)(() => cyclAtStart != NowCycle || InH));
		if (InH)
		{
			int whreLiveSelection = Game.Player.whreLiveSelection;
			List<Heroine> list = new List<Heroine>();
			foreach (Heroine heroine in Game.HeroineList)
			{
				if (heroine != withGirl && Statistic.IsStayingWithPlayer(heroine, whreLiveSelection))
				{
					list.Add(heroine);
				}
			}
			if (StoryHInf != null)
			{
				StoryHInf.girlsToMigrateInH = list;
				StoryHInf.isHomeH = true;
			}
		}
		yield return (object)new WaitForSeconds(Time.deltaTime);
	}

	private IEnumerator IE_AfterGoToMyHouse(Map.AmbTyp targetAmb)
	{
		if (goToMyHouseCase == GoToMyHouseCase.ByGetNews)
		{
			foreach (Heroine heroine in Game.HeroineList)
			{
				Statistic.RemoveStayWithPlayerPlace(heroine, includeDaily: true);
			}
		}
		bool isWithGirl = Singleton<Cycle>.Instance.withHeroine != null;
		yield return (object)new WaitForSeconds(isWithGirl ? 2f : Time.deltaTime);
		yield return (object)new WaitUntil((Func<bool>)(() => !Scene.IsOverlap && !Scene.IsNowLoading && !Scene.IsNowLoadingFade && !Scene.IsFadeNow));
		yield return (object)new WaitForSeconds(Time.deltaTime * 1f);
		if ((Object)(object)Core.dealer.ActMap.sunLightInfo != (Object)null)
		{
			Core.dealer.ActMap.sunLightInfo.Set((Type?)Map.SunlightType(targetAmb), Camera.main);
		}
		if (Object.op_Implicit((Object)(object)Singleton<HomeMenu>.Instance) && Singleton<HomeMenu>.Instance.visible)
		{
			Singleton<HomeMenu>.Instance.End();
		}
		if (!isWithGirl)
		{
			isWithGirl = Singleton<Cycle>.Instance.withHeroine != null;
		}
		if (isWithGirl)
		{
			Heroine withGirl = Singleton<Cycle>.Instance.withHeroine;
			int playerStayPlace = Game.Player.whreLiveSelection;
			List<Heroine> girlsAtHome = new List<Heroine>();
			foreach (Heroine heroine2 in Game.HeroineList)
			{
				if (heroine2 != withGirl && Statistic.IsStayingWithPlayer(heroine2, playerStayPlace))
				{
					girlsAtHome.Add(heroine2);
				}
			}
			Type cycleAtCall = cycleTyp;
			yield return (object)new WaitUntil((Func<bool>)(() => (Core.dealer.Ctx == Context.H && (Object)(object)Core.dealer.HCtrl != (Object)null && Core.dealer.HCtrl.IsInit && !Scene.IsNowLoading) || cycleAtCall != cycleTyp));
			if (cycleAtCall == cycleTyp && Core.dealer.Ctx == Context.H)
			{
				if (!Statistic.IsStayingWithPlayer(withGirl, playerStayPlace))
				{
					Statistic.ManagePlaceWithPlayer(playerStayPlace);
					Statistic.SetStayWithPlayerPlace(withGirl, playerStayPlace);
				}
				if (StoryHInf != null)
				{
					StoryHInf.girlsToMigrateInH = girlsAtHome;
					StoryHInf.isHomeH = true;
				}
				yield return (object)new WaitForSeconds(Time.deltaTime * 2f);
				Map.ambiance = targetAmb;
			}
		}
		goToMyHouseCase = GoToMyHouseCase.ByGame;
	}

	public void WaitEstheticEnd()
	{
		((MonoBehaviour)this).StartCoroutine(IE_WaitEstEnd(eventInfo.chas[0]));
	}

	public void RetainMainNpcBaseIntoH(Base charaBase)
	{
		NpcBaseInH = charaBase;
		DebugInfos.InConsole(" NpcBaseInH = " + ChaCore.Name(((ChaInfo)charaBase.chaCtrl).fileParam, firstNameFirst: false), "Dealer.SetNpcAtHStart");
	}

	public void WaitResultGauge()
	{
		InWaitForResultGauge = true;
		((MonoBehaviour)this).StartCoroutine(IE_WaitResultGauge());
	}

	private IEnumerator IE_WaitResultGauge()
	{
		yield return (object)new WaitUntil((Func<bool>)(() => (Object)(object)Singleton<HResultGauge>.Instance != (Object)null || Singleton<Cycle>.Instance.timerVisible || ecchiEndByTresspass));
		if (ecchiEndByTresspass)
		{
			yield return (object)new WaitUntil((Func<bool>)(() => Core.dealer.Ctx == Context.H));
		}
		InWaitForResultGauge = false;
	}

	private IEnumerator IE_WaitEstEnd(ChaControl cha)
	{
		yield return (object)new WaitForSeconds(Time.deltaTime);
		yield return (object)new WaitUntil((Func<bool>)(() => (Object)(object)cha == (Object)null));
		DebugInfos.InConsole("IE_WaitEstEnd ", "Dealer");
		SetContext(Context.Default);
		ActionScene actionScene = Singleton<ActionScene>.Instance;
		yield return (object)new WaitUntil((Func<bool>)(() => ((Base)actionScene.Player.chaser).chaCtrl.GetActiveTop()));
		yield return (object)new WaitForSeconds(Time.deltaTime);
		Heroine heroine = ((Base)actionScene.Player.chaser).heroine;
		Statistic.GetDaily(heroine);
		ChaControl chaCtrl = ((Base)actionScene.Player.chaser).chaCtrl;
		((ChaInfo)chaCtrl).fileCustom.body.sunburnId = ((ChaFile)((CharaData)heroine).charFile).custom.body.sunburnId;
		((ChaInfo)chaCtrl).fileCustom.body.sunburnColor = ((ChaFile)((CharaData)heroine).charFile).custom.body.sunburnColor;
		ChaTools.ApplySkinColor(chaCtrl, ((ChaFile)((CharaData)heroine).charFile).custom.body.skinMainColor);
		ChaCoresInEst.Clear();
	}

	public static void SetSunburnAfterEstOnNpc(ChaControl npcCha, Heroine her)
	{
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		((ChaInfo)npcCha).fileCustom.body.sunburnId = ((ChaFile)((CharaData)her).charFile).custom.body.sunburnId;
		((ChaInfo)npcCha).fileCustom.body.sunburnColor = ((ChaFile)((CharaData)her).charFile).custom.body.sunburnColor;
		ChaTools.ApplySkinColor(npcCha, ((ChaFile)((CharaData)her).charFile).custom.body.skinMainColor);
	}

	public void AtWakeUpAfterLoad()
	{
		((MonoBehaviour)this).StartCoroutine(IE_AtWakeUp(applyPregDay: false));
	}

	private IEnumerator IE_AtWakeUp(bool applyPregDay)
	{
		yield return (object)new WaitUntil((Func<bool>)(() => !Scene.IsNowLoading && !Scene.IsNowLoadingFade));
		Statistic.FeedDailyDictionary(Game.HeroineList, applyPregDay);
	}

	private IEnumerator IE_SearchMorningNPC()
	{
		npcs.Clear();
		heroinesOnMap.Clear();
		yield return (object)new WaitUntil((Func<bool>)(() => !Scene.IsNowLoading && !Scene.IsNowLoadingFade));
		foreach (NPC npc in SingletonInitializer<ActionScene>.instance.npcList)
		{
			DebugInfos.InConsoleCompact("Adding NPC to Dealer List " + ChaCore.Name(((ChaInfo)((Base)npc).chaCtrl).fileParam, firstNameFirst: false), "Dealer");
			npcs.Add(npc);
			heroinesOnMap.Add(((Base)npc).heroine);
		}
		if ((Object)(object)SingletonInitializer<ActionScene>.instance.fixChara != (Object)null)
		{
			ChaControl chaCtrl = ((CharaData)((Base)SingletonInitializer<ActionScene>.instance.fixChara).heroine).chaCtrl;
			if ((Object)(object)chaCtrl != (Object)null)
			{
				chaCtrl.ChangeCoordinateTypeAndReload((CoordinateType)0, true);
				chaCtrl.SetClothesStateAll((byte)0);
			}
			heroinesOnMap.Add(((Base)SingletonInitializer<ActionScene>.instance.fixChara).heroine);
		}
		if (Core.debug)
		{
			World.EditKoikatsuPoint(50);
			if (tweakNpcStat)
			{
				Statistic.TweakNpcsRelations(heroinesOnMap, new List<Statistic.ExpTweak>
				{
					Statistic.ExpTweak.FirstLove,
					Statistic.ExpTweak.Tied,
					Statistic.ExpTweak.Newbie
				}, maxLewdness: true);
			}
		}
		DealCha.AddNpcsExtraComp();
	}

	public void LaunchGirlHasThink()
	{
		((MonoBehaviour)this).StartCoroutine(IE_GirlHasThink());
	}

	private IEnumerator IE_GirlHasThink()
	{
		GirlHasThink = true;
		yield return (object)new WaitForSeconds(Time.deltaTime * 2f);
		GirlHasThink = false;
	}

	public IEnumerator IE_InGuestSwap(AIGuest ai)
	{
		inGuestSwap = true;
		yield return (object)new WaitUntil((Func<bool>)(() => !ai.InSwap));
		inGuestSwap = false;
	}

	public IEnumerator IE_InGuestKiss(AIGuest ai)
	{
		inGuestKiss = true;
		yield return (object)new WaitUntil((Func<bool>)(() => !ai.InKiss));
		inGuestKiss = false;
	}

	public void SetContext(Context ctx)
	{
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		Ctx = ctx;
		if (Ctx == Context.H)
		{
			HCtrl = HCtrl.Create(((Component)this).gameObject);
			if (InStory)
			{
				StoryHInfo storyHInfo = new StoryHInfo();
				if (ecchiEndByTresspass && StoryHInf != null)
				{
					storyHInfo.endDayByAmbChng = StoryHInf.endDayByAmbChng;
					storyHInfo.numOfAmbChange = StoryHInf.numOfAmbChange;
					storyHInfo.ambAtStart = StoryHInf.ambAtStart;
					storyHInfo.mapIdAtStart = StoryHInf.mapIdAtStart;
					storyHInfo.ambAtChange = StoryHInf.ambAtChange;
					storyHInfo.setByPrevInfo = true;
				}
				StoryHInf = storyHInfo;
				StoryHInf.hStartOnMap = Map.CyclesOnMap.Contains(NowCycle);
				if (!storyHInfo.setByPrevInfo)
				{
					StoryHInf.mapIdAtStart = MapID;
				}
				DebugInfos.InConsole("Creating StoryHInfos, cur cycle is " + ((object)NowCycle/*cast due to .constrained prefix*/).ToString() + " , hStartOnMap : " + StoryHInf.hStartOnMap);
				if (ecchiEndByTresspass)
				{
					HCtrl.SetHTimeAtStart(tresspassTimes[0], tresspassTimes[1]);
				}
				else
				{
					HCtrl.SetHTimeAtStart();
				}
			}
		}
		else if ((Object)(object)HCtrl != (Object)null)
		{
			Object.Destroy((Object)(object)HCtrl);
			ChaMovers.Clear();
			Map.Reset();
		}
		if (Ctx == Context.Esthetic)
		{
			EstCtrl = EstheticCtrl.Create(((Component)this).gameObject);
		}
		else if ((Object)(object)EstCtrl != (Object)null)
		{
			Object.Destroy((Object)(object)EstCtrl);
		}
		if (Ctx == Context.Event && eventInfo != null && eventInfo.saves.Count != 0)
		{
			Statistic.SetDailyEvent(eventInfo.saves[0], new List<Statistic.DailyEvent> { Statistic.DailyEvent.talkEvent });
		}
		DebugInfos.InConsole(" Context set to " + Ctx, "Dealer");
	}

	public static void SetInStory(bool isInStory)
	{
		if (Object.op_Implicit((Object)(object)Core.dealer))
		{
			Core.dealer.InStory = isInStory;
			if (isInStory)
			{
				((MonoBehaviour)Core.dealer).StartCoroutine(Core.dealer.IE_GetPlayerAppearence());
			}
			DebugInfos.InConsole(Core.dealer.InStory ? " Enter Story Mode" : " Exit Story Mode", "Dealer");
		}
	}

	public static void SetCursorTxt(int id)
	{
		if (!Keys.LmbHold)
		{
			Singleton<GameCursor>.Instance.SetCursorTexture(0, Core.dealer.CursorTxts[id], true);
		}
	}

	public static void ParentToDealer(List<Transform> childs, bool destroyAfterScene)
	{
		foreach (Transform child in childs)
		{
			child.SetParent(((Component)Core.dealer).transform, true);
			if (destroyAfterScene && Core.dealer.Ctx == Context.H)
			{
				DestroyAfterH.Create(((Component)child).gameObject);
			}
			if (destroyAfterScene && Core.dealer.Ctx == Context.Esthetic)
			{
				DestroyAfterEst.Create(((Component)child).gameObject);
			}
		}
	}
}
