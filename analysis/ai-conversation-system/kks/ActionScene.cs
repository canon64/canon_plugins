using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ADV;
using ActionGame;
using ActionGame.Chara;
using ActionGame.Communication;
using ActionGame.Esthetic;
using ActionGame.H;
using ActionGame.MapObject;
using ActionGame.Point;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Illusion;
using Illusion.Component;
using Illusion.CustomAttributes;
using Illusion.Extensions;
using Illusion.Game;
using Illusion.Game.Elements.EasyLoader;
using Manager;
using SaveData;
using StrayTech;
using UniRx;
using UniRx.Triggers;
using UnityEngine;

public sealed class ActionScene : SingletonInitializer<ActionScene>
{
	public enum Regulate
	{
		Move = 1
	}

	public enum ADVReserveType
	{
		MapWarning
	}

	public class ADVParam : SceneParameter
	{
		private class WarningADV
		{
			public Vector3 pos { get; private set; }

			public Quaternion rot { get; private set; }

			public WarningADV(Vector3 pos, Quaternion rot)
			{
				this.pos = pos;
				this.rot = rot;
			}
		}

		public bool isInVisibleChara = true;

		private WarningADV warningADV;

		public ADVParam(ActionScene scene)
			: base(scene)
		{
		}

		public override void Init(Data data)
		{
			warningADV = null;
			ADVScene aDVScene = SceneParameter.advScene;
			ActionScene actionScene = base.mono as ActionScene;
			TextScenario scenario = aDVScene.Scenario;
			scenario.LoadBundleName = data.bundleName;
			scenario.LoadAssetName = data.assetName;
			if (!actionScene.isBasePositionIgnore)
			{
				aDVScene.Stand.SetPositionAndRotation(data.position, data.rotation);
			}
			scenario.heroineList = data.heroineList;
			scenario.transferList = data.transferList;
			if (!data.heroineList.IsNullOrEmpty())
			{
				scenario.currentChara = new ADV.CharaData(data.heroineList[0], scenario, data.motionReserverList.SafeGet(0), data.isParentChara);
				if (data.isParentChara)
				{
					scenario.commandController.AddChara(0, scenario.currentChara);
				}
			}
			if (data.camera != null)
			{
				scenario.BackCamera.transform.SetPositionAndRotation(data.camera.position, data.camera.rotation);
				scenario.BackCamera.fieldOfView = Camera.main.fieldOfView;
			}
			foreach (KeyValuePair<string, Func<string>> aDVVariable in actionScene.Cycle.ADVVariables)
			{
				scenario.Vars[aDVVariable.Key] = new ValData(ValData.Convert(aDVVariable.Value(), typeof(string)));
			}
			aDVScene.Map = actionScene.Map;
			float fadeInTime = data.fadeInTime;
			if (fadeInTime > 0f)
			{
				aDVScene.fadeTime = fadeInTime;
			}
			else
			{
				isInVisibleChara = false;
			}
			Program.Transfer transfer = data.transferList.Find((Program.Transfer p) => p.param.Command == Command.Open);
			if (transfer != null && transfer.param.Args[1] == "Warning")
			{
				warningADV = new WarningADV(data.camera.position, data.camera.rotation);
			}
		}

		public override void Release()
		{
			ADVScene aDVScene = SceneParameter.advScene;
			ActionScene actScene = base.mono as ActionScene;
			if (isInVisibleChara)
			{
				List<ChaControl> visibleList = actScene.VisibleList;
				if (visibleList != null)
				{
					visibleList.RemoveAll((ChaControl p) => p == null);
					visibleList.ForEach(delegate(ChaControl p)
					{
						p.GetComponent<Base>().chaCtrl.visibleAll = true;
					});
				}
			}
			if (warningADV != null)
			{
				Camera.main.transform.SetPositionAndRotation(warningADV.pos, warningADV.rot);
			}
			bool isPlayerVisible = true;
			MapInfo.Param info = actScene.Map.Info;
			if (!info.isGate || info.is2D || !actScene.Cycle.isAction)
			{
				isPlayerVisible = false;
			}
			ActionGame.Chara.Player player = actScene.Player;
			if (player != null)
			{
				if (info.isGate)
				{
					player.mapNo = actScene.Map.no;
				}
				player.SetActive(isPlayerVisible);
			}
			actScene.npcList.ForEach(delegate(NPC p)
			{
				p.SetActive(isPlayerVisible && actScene.Map.no == p.mapNo);
			});
			aDVScene.gameObject.SetActive(value: false);
		}

		public override void WaitEndProc()
		{
			ActionScene actionScene = base.mono as ActionScene;
			if (isInVisibleChara)
			{
				actionScene.VisibleList.ForEach(delegate(ChaControl p)
				{
					p.GetComponent<Base>().chaCtrl.visibleAll = false;
				});
				actionScene.Player.chaCtrl.visibleAll = false;
			}
		}
	}

	[EnumFlags]
	[SerializeField]
	private Regulate _regulate;

	[SerializeField]
	private ActionMap _map;

	[SerializeField]
	private Cycle _cycle;

	[SerializeField]
	private SmoothDampFollowTargetPublices _follow;

	private Transform _playerListener;

	[SerializeField]
	private ADVScene advScene;

	[SerializeField]
	private Camera nowCamera;

	private Transform _cameraTransform;

	[SerializeField]
	private CameraStateDefinitionChange cameraStateDefinitionChange;

	[SerializeField]
	private Minimap _miniMap;

	[SerializeField]
	private ActionChangeUI _actionChangeUI;

	[SerializeField]
	private ActionMenu _actionMenu;

	[SerializeField]
	private ActionMoveUI _actionMoveUI;

	[SerializeField]
	private ActionMoveGuideUI _actionMoveGuideUI;

	[SerializeField]
	private TwoShot _twoShot;

	private LoadSEMesh SEMesh;

	private MinimapVisible[] minimapVisible;

	[SerializeField]
	private ActionControl _actCtrl;

	[SerializeField]
	private ParamUI _paramUI;

	[SerializeField]
	private GameObject informationH;

	[SerializeField]
	private ShortcutKey _systemShortcutKey;

	[SerializeField]
	private ShortcutKey _actionShortcutKey;

	private Heroine hSceneOtherHeroine;

	private const int FixGuideNo = -13;

	public Regulate regulate => _regulate;

	public List<ChaControl> VisibleList { get; } = new List<ChaControl>();


	public ActionMap Map => _map;

	public Cycle Cycle => _cycle;

	public CharacterPosSet PosSet { get; } = new CharacterPosSet();


	public SmoothDampFollowTargetPublices follow => _follow;

	public ActionGame.Chara.Player Player { get; private set; }

	private Transform playerListener
	{
		get
		{
			if (_playerListener == null)
			{
				Transform t = new GameObject("PlayerListener").transform;
				t.SetParent(base.transform, worldPositionStays: false);
				(from _ in t.UpdateAsObservable()
					where Player != null
					select _).Subscribe(delegate
				{
					t.SetPositionAndRotation(Player.standPos, Player.rotation);
				});
				_playerListener = t;
			}
			return _playerListener;
		}
	}

	public ADVScene AdvScene => advScene;

	public Transform cameraTransform
	{
		get
		{
			if (!(_cameraTransform != null))
			{
				return _cameraTransform = nowCamera.transform;
			}
			return _cameraTransform;
		}
	}

	public CameraStateDefinitionChange CameraState => cameraStateDefinitionChange;

	public bool isCursorLock => _isCursorLock;

	private bool _isCursorLock { get; set; }

	public List<NPC> npcList { get; private set; } = new List<NPC>();


	public Fix fixChara { get; private set; }

	public Dictionary<int, CharaInfo.Param> charaInfoDic => _charaInfoDic;

	private Dictionary<int, CharaInfo.Param> _charaInfoDic { get; set; }

	public Dictionary<int, TalkLookNeck.Param> talkLookNeckDic => _talkLookNeckDic;

	private Dictionary<int, TalkLookNeck.Param> _talkLookNeckDic { get; set; }

	public Dictionary<string, TalkLookBody.Param> talkLookBodyDic => _talkLookBodyDic;

	private Dictionary<string, TalkLookBody.Param> _talkLookBodyDic { get; set; }

	public Dictionary<int, Topic.Param> topicDic => _topicDic;

	private Dictionary<int, Topic.Param> _topicDic { get; set; }

	public Dictionary<int, int> stepUpDic => _stepUpDic;

	private Dictionary<int, int> _stepUpDic { get; set; }

	public Dictionary<int, ShopInfo.Param> shopInfoTable => _shopInfoTable;

	private Dictionary<int, ShopInfo.Param> _shopInfoTable { get; set; }

	public Dictionary<int, PrayInfo.Param> prayInfoTable => _prayInfoTable;

	private Dictionary<int, PrayInfo.Param> _prayInfoTable { get; set; }

	public Dictionary<int, List<string>> monologueInfoTable => _monologueInfoTable;

	private Dictionary<int, List<string>> _monologueInfoTable { get; set; }

	public Minimap miniMap => _miniMap;

	public ActionChangeUI actionChangeUI => _actionChangeUI;

	public bool actionMenuVisible => _actionMenu.Visible;

	public TwoShot twoShot => _twoShot;

	public bool IsInChargeBGM => _isInChargeBGM;

	private bool _isInChargeBGM { get; set; }

	public bool MiniMapAndCameraActive
	{
		get
		{
			if (MonoBehaviourSingleton<CameraSystem>.Instance.enabled)
			{
				return miniMap.isVisible;
			}
			return false;
		}
		set
		{
			if (value)
			{
				miniMap.Open();
			}
			else
			{
				miniMap.Close();
			}
			if (twoShot.isStart)
			{
				MonoBehaviourSingleton<CameraSystem>.Instance.enabled = twoShot.camCtrl;
			}
			else
			{
				MonoBehaviourSingleton<CameraSystem>.Instance.enabled = value;
			}
			paramUI.active = value;
			_actionChangeUI.isVisible = value;
		}
	}

	public bool isEventNow { get; private set; }

	public ActionControl actCtrl => _actCtrl;

	public ParamUI paramUI => _paramUI;

	private GameObject nowInformationH { get; set; }

	private bool shortcutKey
	{
		set
		{
			_systemShortcutKey.enabled = value;
			_actionShortcutKey.enabled = value;
		}
	}

	public float lesbianDistance
	{
		get
		{
			if (Map.no == 36)
			{
				return 2f;
			}
			return 1.5f;
		}
	}

	public bool isBasePositionIgnore { get; set; }

	public bool isPenetration { get; set; }

	public NPC otherPauseNPC { get; private set; }

	private static string[] GuideLayerNames { get; } = new string[2] { "FixGuide", "FixGuide_Lovers" };


	public void PlayerCreate()
	{
		if (!(Player != null))
		{
			Player = ActionGame.Chara.Player.Create(base.transform, Game.Player);
			Player.LoadStart();
		}
	}

	public void PlayerDelete()
	{
		if (!(Player == null))
		{
			UnityEngine.Object.Destroy(Player.gameObject);
			Player = null;
		}
	}

	public void SetCursorLock(bool isLock)
	{
		_isCursorLock = isLock;
	}

	public void FixCharaDelete()
	{
		if (fixChara != null)
		{
			UnityEngine.Object.Destroy(fixChara.gameObject);
			fixChara = null;
		}
	}

	public LoadSEMesh GetSEMeshVisible()
	{
		if (SEMesh == null)
		{
			SEMesh = GetComponentInChildren<LoadSEMesh>(includeInactive: true);
		}
		return SEMesh;
	}

	public MinimapVisible[] GetMinimapVisible()
	{
		if (minimapVisible == null)
		{
			minimapVisible = GetComponentsInChildren<MinimapVisible>(includeInactive: true);
		}
		return minimapVisible;
	}

	private void DisplayHInformation(float time)
	{
		time *= 0.5f;
		if (nowInformationH != null)
		{
			UnityEngine.Object.Destroy(nowInformationH);
		}
		nowInformationH = UnityEngine.Object.Instantiate(informationH, base.transform, worldPositionStays: false);
		nowInformationH.GetComponent<CanvasGroup>().DOFade(1f, time).SetEase(Ease.OutExpo)
			.SetLoops(2, LoopType.Yoyo)
			.OnComplete(delegate
			{
				UnityEngine.Object.Destroy(nowInformationH);
			});
		Canvas canvas = nowInformationH.GetComponent<Canvas>();
		canvas.enabled = true;
		nowInformationH.UpdateAsObservable().Subscribe(delegate
		{
			canvas.sortingOrder = Scene.sceneFadeCanvas.canvas.sortingOrder + 1;
		});
	}

	public void ShortcutKeyEnable(bool isOn)
	{
		shortcutKey = isOn;
	}

	public void ShortcutKeyEnable_Action(bool isOn)
	{
		_actionShortcutKey.enabled = isOn;
	}

	protected override void Initialize()
	{
		ParameterList.Add(new ADVParam(this));
		this.OnDestroyAsObservable().Subscribe(delegate
		{
			Manager.Character.loading.Clear();
			ParameterList.Remove(this);
		});
	}

	private IEnumerator Start()
	{
		base.enabled = false;
		Game.Player.whreLiveSelection = Game.Player.schoolClass;
		bool isLoad = false;
		SaveLoad.onChange = delegate
		{
			isLoad = false;
			_isCursorLock = false;
			WorldData saveData3 = Game.saveData;
			saveData3.area = Map.no;
			if (SingletonInitializer<HomeMenu>.instance.isEnd)
			{
				saveData3.saveTiming = 2;
				saveData3.timer = Cycle.timer;
				Game.Player.moveData.Set(Player);
				Game.HeroineList.ForEach(delegate(Heroine h)
				{
					h.moveData.Set(h.charaBase);
				});
				saveData3.mapTopic.isRefresh = false;
			}
			else
			{
				saveData3.timer = float.MinValue;
			}
		};
		SaveLoad.onResponse = delegate(SaveLoad.Mode mode, SaveLoad.Answer answer)
		{
			switch (mode)
			{
			case SaveLoad.Mode.Load:
				if (SaveLoad.answer == SaveLoad.Answer.Yes)
				{
					isLoad = true;
					Game.isAutoSavePossible = false;
					WorldData saveData2 = Game.saveData;
					HashSet<int> tutorialHash = Game.globalData.tutorialHash;
					int item = 10;
					if (saveData2.isSuiteRoomEvent)
					{
						tutorialHash.Add(item);
					}
					else
					{
						tutorialHash.Remove(item);
					}
					if (saveData2.saveTiming > 0 || Cycle.isAction)
					{
						Cycle.Refresh();
					}
					KoikatsuPointUI.Clear();
					_cycle.SetWeek(saveData2.week);
				}
				break;
			case SaveLoad.Mode.Save:
				Game.HeroineList.ForEach(delegate(Heroine h)
				{
					if (h.moveData.isAlive && h.chaCtrl != null)
					{
						h.charFile.status.coordinateType = h.chaCtrl.chaFile.status.coordinateType;
					}
				});
				break;
			}
		};
		SaveLoad.onFinish = delegate
		{
			if (!isLoad)
			{
				Game.Player.moveData.Set(null);
				Game.HeroineList.ForEach(delegate(Heroine h)
				{
					h.moveData.Set(null);
				});
				WorldData saveData = Game.saveData;
				saveData.timer = float.MinValue;
				saveData.saveTiming = 0;
				saveData.mapTopic.isRefresh = true;
			}
		};
		this.OnDestroyAsObservable().Subscribe(delegate
		{
			SaveLoad.onChange = null;
			SaveLoad.onFinish = null;
			SaveLoad.onResponse = null;
		});
		Cycle.Initialize();
		_actCtrl.Init();
		_actCtrl.Refresh();
		Manager.Sound.Listener = cameraTransform;
		Manager.Character.BeginLoadAssetBundle(_resources: true);
		PlayerCreate();
		_actionMoveUI.Initialize(this);
		_actionMoveGuideUI.Initialize(this);
		_actionMenu.Initialize(this);
		PosSet.Initialize();
		_charaInfoDic = new Dictionary<int, CharaInfo.Param>();
		List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath("action/list/chara/", subdirCheck: true);
		assetBundleNameListFromPath.Sort();
		assetBundleNameListFromPath.ForEach(delegate(string file)
		{
			foreach (CharaInfo item2 in from p in AssetBundleManager.LoadAllAsset(file, typeof(CharaInfo)).GetAllAssets<CharaInfo>()
				orderby p.name
				select p)
			{
				foreach (CharaInfo.Param item3 in item2.param)
				{
					_charaInfoDic[item3.Personality] = item3;
				}
			}
			AssetBundleManager.UnloadAssetBundle(file, isUnloadForceRefCount: false);
		});
		_talkLookNeckDic = new Dictionary<int, TalkLookNeck.Param>();
		List<string> assetBundleNameListFromPath2 = CommonLib.GetAssetBundleNameListFromPath("action/list/talklookneck/", subdirCheck: true);
		assetBundleNameListFromPath2.Sort();
		assetBundleNameListFromPath2.ForEach(delegate(string file)
		{
			foreach (TalkLookNeck item4 in from p in AssetBundleManager.LoadAllAsset(file, typeof(TalkLookNeck)).GetAllAssets<TalkLookNeck>()
				orderby p.name
				select p)
			{
				foreach (TalkLookNeck.Param item5 in item4.param)
				{
					_talkLookNeckDic[Animator.StringToHash(item5.StateName)] = item5;
				}
			}
			AssetBundleManager.UnloadAssetBundle(file, isUnloadForceRefCount: false);
		});
		_talkLookBodyDic = new Dictionary<string, TalkLookBody.Param>();
		List<string> assetBundleNameListFromPath3 = CommonLib.GetAssetBundleNameListFromPath("action/list/talklookbody/", subdirCheck: true);
		assetBundleNameListFromPath3.Sort();
		assetBundleNameListFromPath3.ForEach(delegate(string file)
		{
			foreach (TalkLookBody item6 in from p in AssetBundleManager.LoadAllAsset(file, typeof(TalkLookBody)).GetAllAssets<TalkLookBody>()
				orderby p.name
				select p)
			{
				foreach (TalkLookBody.Param item7 in item6.param)
				{
					_talkLookBodyDic[item7.StateName] = item7;
				}
			}
			AssetBundleManager.UnloadAssetBundle(file, isUnloadForceRefCount: false);
		});
		_topicDic = new Dictionary<int, Topic.Param>();
		List<string> assetBundleNameListFromPath4 = CommonLib.GetAssetBundleNameListFromPath("action/list/topic/", subdirCheck: true);
		assetBundleNameListFromPath4.Sort();
		assetBundleNameListFromPath4.ForEach(delegate(string file)
		{
			foreach (Topic item8 in from p in AssetBundleManager.LoadAllAsset(file, typeof(Topic)).GetAllAssets<Topic>()
				orderby p.name
				select p)
			{
				foreach (Topic.Param item9 in item8.param)
				{
					_topicDic[item9.No] = item9;
				}
			}
			AssetBundleManager.UnloadAssetBundle(file, isUnloadForceRefCount: false);
		});
		_stepUpDic = new Dictionary<int, int>();
		List<string> assetBundleNameListFromPath5 = CommonLib.GetAssetBundleNameListFromPath("action/list/stepup/", subdirCheck: true);
		assetBundleNameListFromPath5.Sort();
		assetBundleNameListFromPath5.ForEach(delegate(string file)
		{
			foreach (StepUpData item10 in from p in AssetBundleManager.LoadAllAsset(file, typeof(StepUpData)).GetAllAssets<StepUpData>()
				orderby p.name
				select p)
			{
				foreach (StepUpData.Param item11 in item10.param)
				{
					_stepUpDic[item11.no] = item11.limit;
				}
			}
			AssetBundleManager.UnloadAssetBundle(file, isUnloadForceRefCount: false);
		});
		bool isH3 = Game.isAddH;
		Dictionary<int, ShopInfo.Param> table2 = new Dictionary<int, ShopInfo.Param>();
		List<string> assetBundleNameListFromPath6 = CommonLib.GetAssetBundleNameListFromPath("action/list/shopinfo/", subdirCheck: true);
		assetBundleNameListFromPath6.Sort();
		assetBundleNameListFromPath6.ForEach(delegate(string file)
		{
			foreach (ShopInfo item12 in from p in AssetBundleManager.LoadAllAsset(file, typeof(ShopInfo)).GetAllAssets<ShopInfo>()
				orderby p.name
				select p)
			{
				foreach (ShopInfo.Param item13 in item12.param)
				{
					if (isH3 || item13.Type != 1)
					{
						table2[item13.ID] = item13;
					}
				}
			}
			AssetBundleManager.UnloadAssetBundle(file, isUnloadForceRefCount: false);
		});
		_shopInfoTable = table2.OrderBy((KeyValuePair<int, ShopInfo.Param> x) => x.Value.Sort).ToDictionary((KeyValuePair<int, ShopInfo.Param> v) => v.Key, (KeyValuePair<int, ShopInfo.Param> v) => v.Value);
		bool isH2 = Game.isAddH;
		_prayInfoTable = new Dictionary<int, PrayInfo.Param>();
		List<string> assetBundleNameListFromPath7 = CommonLib.GetAssetBundleNameListFromPath("action/list/prayinfo/", subdirCheck: true);
		assetBundleNameListFromPath7.Sort();
		assetBundleNameListFromPath7.ForEach(delegate(string file)
		{
			foreach (PrayInfo item14 in from p in AssetBundleManager.LoadAllAsset(file, typeof(PrayInfo)).GetAllAssets<PrayInfo>()
				orderby p.name
				select p)
			{
				foreach (PrayInfo.Param item15 in item14.param)
				{
					if (isH2 || item15.Type != 1)
					{
						_prayInfoTable[item15.ID] = item15;
					}
				}
			}
			AssetBundleManager.UnloadAssetBundle(file, isUnloadForceRefCount: false);
		});
		bool isH = Game.isAddH;
		Dictionary<int, MonologueInfo.Param> table = new Dictionary<int, MonologueInfo.Param>();
		List<string> assetBundleNameListFromPath8 = CommonLib.GetAssetBundleNameListFromPath("action/list/monologue/", subdirCheck: true);
		assetBundleNameListFromPath8.Sort();
		assetBundleNameListFromPath8.ForEach(delegate(string file)
		{
			foreach (MonologueInfo item16 in from p in AssetBundleManager.LoadAllAsset(file, typeof(MonologueInfo)).GetAllAssets<MonologueInfo>()
				orderby p.name
				select p)
			{
				foreach (MonologueInfo.Param item17 in item16.param)
				{
					if (isH || !item17.isH)
					{
						table[item17.ID] = item17;
					}
				}
			}
			AssetBundleManager.UnloadAssetBundle(file, isUnloadForceRefCount: false);
		});
		_monologueInfoTable = table.ToLookup((KeyValuePair<int, MonologueInfo.Param> v) => v.Value.isH ? 1 : 0, (KeyValuePair<int, MonologueInfo.Param> v) => v.Value.Text).ToDictionary((IGrouping<int, string> v) => v.Key, (IGrouping<int, string> v) => v.ToList());
		(from _ in this.UpdateAsObservable()
			select Player != null && Player.isActionNow_Origin).DistinctUntilChanged().Subscribe(delegate(bool isAction)
		{
			_actionMenu.Interactable = !isAction;
		});
		(from isOn in (from _ in this.UpdateAsObservable()
				select ActionInput.isCursorLock && !SingletonInitializer<TalkScene>.instance.isActive && Scene.AddSceneName.IsNullOrEmpty() && !Scene.IsOverlap && MiniMapAndCameraActive && !Scene.IsNowLoadingFade && !_twoShot.isStart && !CapFileViewer.isActive && !ShopMenu.isActive && !PrayMenu.isActive).DistinctUntilChanged()
			where isOn
			select isOn).Subscribe(delegate
		{
			_isCursorLock = !_isCursorLock;
		});
		(from _ in this.UpdateAsObservable()
			select !Cycle.isAction || Program.isADVProcessing || SingletonInitializer<TalkScene>.instance.isActive || !Scene.AddSceneName.IsNullOrEmpty() || Scene.IsOverlap || _twoShot.isStart || !_twoShot.isEnd || CapFileViewer.isActive || ShopMenu.isActive || PrayMenu.isActive).DistinctUntilChanged().Subscribe(delegate(bool isOn)
		{
			_isCursorLock = !isOn;
		});
		(from _ in this.UpdateAsObservable()
			where _twoShot.isStart
			select _).Subscribe(delegate
		{
			if (!Scene.IsOverlap)
			{
				_isCursorLock = true;
			}
			else
			{
				_isCursorLock = false;
			}
		});
		IObservable<bool> source = from _ in this.UpdateAsObservable()
			select _isCursorLock;
		source.Select((bool isOn) => !isOn && Cycle.isAction && !Program.isADVProcessing && !SingletonInitializer<TalkScene>.instance.isActive && Scene.AddSceneName.IsNullOrEmpty() && !Scene.IsOverlap && !_twoShot.isStart && _twoShot.isEnd && !CapFileViewer.isActive && !ShopMenu.isActive && !PrayMenu.isActive && !CallMenu.isActive).DistinctUntilChanged().Subscribe(delegate(bool visible)
		{
			_actionMenu.Visible = visible;
		});
		source.DistinctUntilChanged().Subscribe(delegate(bool isOn)
		{
			if (!isOn)
			{
				Cursor.lockState = CursorLockMode.None;
				Cursor.visible = true;
			}
		});
		source.Where((bool isOn) => isOn).Subscribe((Action<bool>)delegate
		{
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}, (Action)delegate
		{
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
			Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
		});
		CameraEffector cameraEffector = nowCamera.GetComponent<CameraEffector>();
		CameraEffectorConfig cameraEffectorConfig = cameraEffector.config;
		(from _ in this.UpdateAsObservable()
			where base.enabled
			where isUpdateCheck()
			select _).Subscribe(delegate
		{
			Base action = GetAction();
			if (action != null)
			{
				_SceneEvent(action).Forget();
			}
			cameraEffectorConfig.SetDOF(enable: true);
			switch (cameraStateDefinitionChange.Mode)
			{
			case CameraMode.FPS:
				cameraEffector.SetDOFTargetCamera();
				break;
			case CameraMode.TPS:
			case CameraMode.Other:
				cameraEffector.SetDOFTarget(Player.tpsDofTarget);
				break;
			}
		});
		yield return new WaitUntil(() => Player != null && Player.initialized);
		(from _ in this.UpdateAsObservable()
			where _isCursorLock && playerListener != null && Manager.Sound.Listener != playerListener
			select _).Subscribe(delegate
		{
			Manager.Sound.Listener = playerListener;
		});
		_paramUI.Init();
		base.enabled = true;
		Cycle.Change(Cycle.nowType);
		Base GetAction()
		{
			if (!ActionInput.isAction)
			{
				return null;
			}
			if (!_twoShot.isEnd)
			{
				return null;
			}
			if (MonoBehaviourSingleton<CameraSystem>.Instance != null && MonoBehaviourSingleton<CameraSystem>.Instance.SystemStatus == CameraSystem.CameraSystemStatus.Transitioning)
			{
				return null;
			}
			if (_cycle.isActionEnd)
			{
				return null;
			}
			if (Player.isActionPointHit)
			{
				return null;
			}
			if (Player.isActionNow)
			{
				return null;
			}
			if (Scene.IsOverlap)
			{
				return null;
			}
			if (!Scene.AddSceneName.IsNullOrEmpty())
			{
				return null;
			}
			Base actionTarget = Player.actionTarget;
			if (actionTarget == null)
			{
				return null;
			}
			if (actionTarget is NPC && Player.isGateHit)
			{
				return null;
			}
			if (!actionTarget.isAction)
			{
				return null;
			}
			return actionTarget;
		}
		bool isUpdateCheck()
		{
			if (Player == null)
			{
				return false;
			}
			bool flag = ((Cycle.isAction && !Program.isADVProcessing && !SingletonInitializer<TalkScene>.instance.isActive && Scene.AddSceneName.IsNullOrEmpty() && !Scene.IsOverlap && !_twoShot.isStart && !CapFileViewer.isActive && !ShopMenu.isActive && !PrayMenu.isActive) ? true : false);
			MiniMapAndCameraActive = flag;
			if (flag && !isEventNow)
			{
				return !Game.IsRegulate(isAddSceneCheck: false);
			}
			return false;
		}
	}

	private async UniTask _SceneEvent(Base chara)
	{
		if (Cycle.timer >= (float)Cycle.EVENT_LIMIT || chara == null || isEventNow)
		{
			return;
		}
		CancellationToken token = this.GetCancellationTokenOnDestroy();
		_isInChargeBGM = true;
		Player.isActionNow = true;
		isEventNow = true;
		Player.isLesMotionPlay = false;
		Player.isOnanismMotionPlay = false;
		shortcutKey = false;
		Cycle.Type nowCycle = Cycle.nowType;
		await UniTask.Yield(PlayerLoopTiming.Update, token);
		await UniTask.WaitUntil(() => Scene.AddSceneName.IsNullOrEmpty(), PlayerLoopTiming.Update, token);
		if (chara is NPC nPC)
		{
			if (nPC != null)
			{
				int onanismID = nPC.GetOnanismID(isMotion: false);
				if (onanismID != -1)
				{
					Player.ChaserEnd();
					await ChangeHSpecial(new List<int> { onanismID }, false, nPC.cachedTransform, null, token, nPC);
				}
				else
				{
					onanismID = nPC.GetLesbianID(isMotion: false);
					if (onanismID != -1)
					{
						Player.ChaserEnd();
						await ChangeHSpecial(new List<int> { onanismID }, false, nPC.cachedTransform, null, token, nPC.AI.target, nPC);
					}
					else if (Player.mapNo == nPC.mapNo)
					{
						await _SceneEventNPC(nPC, token);
					}
				}
			}
		}
		else if (chara is Fix fix)
		{
			await _SceneEventFix(fix, token);
		}
		shortcutKey = true;
		_isInChargeBGM = false;
		isEventNow = false;
		Player.isActionNow = false;
		if (Cycle.isAction && nowCycle == Cycle.nowType)
		{
			Map.PlayBGM();
		}
	}

	private async UniTask _SceneEventFix(Fix fix, CancellationToken token)
	{
		Heroine heroine = fix.heroine;
		bool introSkip = false;
		ResultEnum result = ResultEnum.None;
		ResultInfo resultInfo = null;
		bool isOtherInitialized = false;
		IKMotion ikMotion = null;
		if (0 == 0)
		{
			TalkScene talk = SingletonInitializer<TalkScene>.instance;
			talk.otherInitialize = delegate
			{
				isOtherInitialized = true;
				MiniMapAndCameraActive = false;
				npcList.ForEach(delegate(NPC p)
				{
					p.SetActive(active: false);
				});
				npcList.ForEach(delegate(NPC p)
				{
					p.move.isStop = true;
				});
				npcList.ForEach(delegate(NPC p)
				{
					p.Pause(isPause: true);
				});
				fix.SetActive(active: false);
				Player.SetActive(active: false);
			};
			NecessaryInfo necessaryInfo = new NecessaryInfo(_state: (!fix.isBodyForTarget) ? 1 : 0, _mapNo: Player.GetCommunicationMap(), _cycleType: Cycle.nowType, _isOtherPeople: false, _isChase: false, _isChasePossible: !Map.Info.is2D, _isHPossible: true, _isNotice: false, _isAttack: false, _isHAttack: false, _exposure: false, _femaleOnly: Player.isChaseWarning, _introSkip: introSkip, _placeNames: new string[0]);
			Player.SetCommunicationMap(null);
			shortcutKey = true;
			await talk.Setup(heroine, necessaryInfo).WithCancellation(token);
			await UniTask.WaitWhile(() => talk.isActive, PlayerLoopTiming.Update, token);
			resultInfo = talk.resultInfo;
			ikMotion = talk.resultIK;
		}
		else
		{
			resultInfo = new ResultInfo
			{
				result = result
			};
		}
		await UniTask.WaitWhile(() => resultInfo == null, PlayerLoopTiming.Update, token);
		int advNo = -1;
		switch (resultInfo.result)
		{
		case ResultEnum.H:
		case ResultEnum.ByTheWay:
		{
			Base @base = fix;
			Kind kind = null;
			KindGroup component = Map.mapRoot.GetComponent<KindGroup>();
			List<Kind> list = new List<Kind>();
			if (component != null && !((IReadOnlyCollection<Kind>)(object)component.kinds).IsNullOrEmpty())
			{
				list.AddRange(component.kinds);
			}
			if (!((IReadOnlyCollection<Kind>)(object)Map.mapObjects).IsNullOrEmpty())
			{
				list.AddRange(Map.mapObjects);
			}
			if (list.Any())
			{
				float num = float.MaxValue;
				foreach (Kind item in list)
				{
					float sqrMagnitude = (item.transform.position - @base.position).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						kind = item;
					}
				}
				float radius = @base.GetComponent<CapsuleCollider>().radius;
				if (num > radius * radius)
				{
					kind = null;
				}
			}
			await ChangeH_Fix(kind, token, fix);
			break;
		}
		case ResultEnum.GoHome:
			Cycle.SetWithHeroine(heroine, 0);
			Cycle.Change(Cycle.Type.GotoMyHouse);
			break;
		}
		if (advNo != -1)
		{
			await OpenADV(heroine, new ADV.CharaData.MotionReserver
			{
				ikMotion = ikMotion
			}, token, advNo);
		}
		ResultEnum result2 = resultInfo.result;
		if (result2 == ResultEnum.H || result2 == ResultEnum.ByTheWay)
		{
			return;
		}
		if (heroine.chaCtrl != null && heroine.chaCtrl.hiPoly)
		{
			_ = heroine.chaCtrl.fileStatus.coordinateType;
			Manager.Character.DeleteChara(heroine.chaCtrl);
			await UniTask.WaitUntil(() => heroine.chaCtrl == null, PlayerLoopTiming.Update, token);
		}
		heroine.SetRoot(fix.gameObject);
		if (resultInfo.result == ResultEnum.GoHome)
		{
			Cycle.ActionEnd();
			return;
		}
		if (advNo != -1)
		{
			result2 = resultInfo.result;
			_ = result2 - 4;
			_ = 3;
		}
		Player.SetActive(active: true);
		npcList.ForEach(delegate(NPC p)
		{
			p.SetActive(p.mapNo == Map.no);
		});
		npcList.ForEach(delegate(NPC p)
		{
			p.move.isStop = false;
		});
		npcList.ForEach(delegate(NPC p)
		{
			p.Pause(isPause: false);
		});
		if (fix.mapNo == Map.no)
		{
			fix.SetActive(active: true);
		}
		if (isOtherInitialized)
		{
			CameraState.SetAngle(Player.eulerAngles);
		}
		if (advNo != -1)
		{
			await UniTask.WaitUntil(() => MiniMapAndCameraActive, PlayerLoopTiming.Update, token);
			await Scene.sceneFadeCanvas.StartFadeAysnc(FadeCanvas.Fade.Out).WithCancellation(token);
		}
	}

	private async UniTask _SceneEventNPC(NPC npc, CancellationToken token)
	{
		Heroine heroine = npc.heroine;
		npc.move.isStop = true;
		npc.move.velocity = Vector3.zero;
		bool isAttack = npc.AI.ActionNoCheck(8, 28);
		bool isHAttack = npc.AI.ActionNoCheck(5, 29);
		bool flag = false;
		bool introSkip = false;
		ResultEnum result = ResultEnum.None;
		switch (npc.AI.actionNo)
		{
		case 28:
			introSkip = true;
			break;
		case 29:
			flag = true;
			result = ResultEnum.ByTheWay;
			heroine.talkEvent.Add(4);
			heroine.talkEvent.Add(5);
			break;
		case 30:
			npc.AI.HResponseAction();
			return;
		}
		npc.Pause(isPause: true);
		bool isAgentEnabled = npc.agent.enabled;
		npc.agentEnabled = false;
		bool isNotice = npc.isNotice;
		bool isInChase = Player.chaser == npc;
		ResultInfo resultInfo = null;
		bool isOtherInitialized = false;
		IKMotion ikMotion = null;
		if (!flag)
		{
			TalkScene talk = SingletonInitializer<TalkScene>.instance;
			talk.otherInitialize = delegate
			{
				isOtherInitialized = true;
				MiniMapAndCameraActive = false;
				npcList.ForEach(delegate(NPC p)
				{
					p.SetActive(active: false);
				});
				npcList.ForEach(delegate(NPC p)
				{
					p.move.isStop = true;
				});
				npcList.ForEach(delegate(NPC p)
				{
					p.Pause(isPause: true);
				});
				if (fixChara != null)
				{
					fixChara.SetActive(active: false);
				}
				Player.SetActive(active: false);
			};
			int state = ((npc.actIconNo == NPC.ActIcon.Sleep) ? 2 : ((!npc.isBodyForTarget) ? 1 : 0));
			string[] placeNames = npc.PlaceNames;
			NecessaryInfo necessaryInfo = new NecessaryInfo(Player.GetCommunicationMap(), Cycle.nowType, npc.isOtherPeople, isInChase, !Map.Info.is2D, _isHPossible: true, isNotice, state, isAttack, isHAttack, npc.isEasyPlace, Player.isChaseWarning, introSkip, placeNames);
			Player.SetCommunicationMap(null);
			shortcutKey = true;
			await talk.Setup(heroine, necessaryInfo).WithCancellation(token);
			await UniTask.WaitWhile(() => talk.isActive, PlayerLoopTiming.Update, token);
			resultInfo = talk.resultInfo;
			ikMotion = talk.resultIK;
		}
		else
		{
			resultInfo = new ResultInfo
			{
				result = result
			};
		}
		await UniTask.WaitWhile(() => resultInfo == null, PlayerLoopTiming.Update, token);
		npc.agentEnabled = isAgentEnabled;
		ResultEnum result2 = resultInfo.result;
		if ((uint)(result2 - 3) <= 4u)
		{
			npc.heroine.chaCtrl.RandomChangeOfClothesLowPolyEnd();
		}
		int advNo = -1;
		switch (resultInfo.result)
		{
		case ResultEnum.Chase:
			Player.ChaserSet(npc);
			break;
		case ResultEnum.H:
		case ResultEnum.ByTheWay:
		{
			Base @base = ((!isInChase) ? ((Base)npc) : ((Base)Player));
			Kind kind = null;
			KindGroup component = Map.mapRoot.GetComponent<KindGroup>();
			List<Kind> list = new List<Kind>();
			if (component != null && !((IReadOnlyCollection<Kind>)(object)component.kinds).IsNullOrEmpty())
			{
				list.AddRange(component.kinds);
			}
			if (!((IReadOnlyCollection<Kind>)(object)Map.mapObjects).IsNullOrEmpty())
			{
				list.AddRange(Map.mapObjects);
			}
			if (list.Any())
			{
				float num = float.MaxValue;
				foreach (Kind item in list)
				{
					float sqrMagnitude = (item.transform.position - @base.position).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						kind = item;
					}
				}
				float radius = @base.GetComponent<CapsuleCollider>().radius;
				if (num > radius * radius)
				{
					kind = null;
				}
			}
			await ChangeH_NPC(kind, isInChase, resultInfo.result == ResultEnum.ByTheWay, token, npc);
			break;
		}
		case ResultEnum.Tourism:
			switch (heroine.relation)
			{
			case 0:
				advNo = 30;
				break;
			case 1:
				advNo = 31;
				break;
			case 2:
			case 3:
				advNo = 32;
				break;
			}
			break;
		case ResultEnum.Play:
			switch (heroine.relation)
			{
			case 0:
				advNo = 36;
				break;
			case 1:
				advNo = 37;
				break;
			case 2:
			case 3:
				advNo = 38;
				break;
			}
			break;
		case ResultEnum.Experience:
			switch (heroine.relation)
			{
			case 0:
				advNo = 42;
				break;
			case 1:
				advNo = 43;
				break;
			case 2:
			case 3:
				advNo = 44;
				break;
			}
			break;
		case ResultEnum.Meal:
			switch (heroine.relation)
			{
			case 0:
				advNo = 48;
				break;
			case 1:
				advNo = 49;
				break;
			case 2:
			case 3:
				advNo = 50;
				break;
			}
			break;
		case ResultEnum.Confess:
			advNo = 2;
			break;
		case ResultEnum.GoHome:
			Cycle.SetWithHeroine(heroine, 0);
			Cycle.Change(Cycle.Type.GotoMyHouse);
			break;
		}
		if (advNo != -1)
		{
			await OpenADV(heroine, new ADV.CharaData.MotionReserver
			{
				ikMotion = ikMotion
			}, token, advNo);
		}
		if (resultInfo.result != ResultEnum.Chase && isInChase && !Player.isChaseMaintain)
		{
			Player.ChaserEnd();
		}
		Player.isChaseMaintain = false;
		result2 = resultInfo.result;
		if (result2 != ResultEnum.H && result2 != ResultEnum.ByTheWay)
		{
			int hiPolyCoordinates = -1;
			if (heroine.chaCtrl != null && heroine.chaCtrl.hiPoly)
			{
				hiPolyCoordinates = heroine.chaCtrl.fileStatus.coordinateType;
				Manager.Character.DeleteChara(heroine.chaCtrl);
				await UniTask.WaitUntil(() => heroine.chaCtrl == null, PlayerLoopTiming.Update, token);
			}
			if (hiPolyCoordinates != -1)
			{
				npc.heroine.isDresses = true;
				heroine.coordinates = hiPolyCoordinates;
			}
			heroine.SetRoot(npc.gameObject);
			if (resultInfo.result == ResultEnum.GoHome)
			{
				Cycle.ActionEnd();
			}
			else
			{
				bool flag2 = false;
				bool flag3 = false;
				if (advNo != -1)
				{
					result2 = resultInfo.result;
					if ((uint)(result2 - 4) <= 3u)
					{
						flag2 = true;
						flag3 = npc.mapNo != Player.mapNo;
					}
				}
				if (isAttack || isHAttack)
				{
					flag2 = true;
				}
				if (flag3)
				{
					npc.mapNo = Player.mapNo;
				}
				if (Player.chaser != null)
				{
					Player.chaser.mapNo = Player.mapNo;
				}
				Player.SetActive(active: true);
				npc.move.isStop = false;
				SetDefenseSpeak();
				npcList.ForEach(delegate(NPC p)
				{
					p.SetActive(p.mapNo == Map.no);
				});
				npcList.ForEach(delegate(NPC p)
				{
					p.move.isStop = false;
				});
				npcList.ForEach(delegate(NPC p)
				{
					p.Pause(isPause: false);
				});
				if (fixChara != null && fixChara.mapNo == Map.no)
				{
					fixChara.SetActive(active: true);
				}
				if (flag2)
				{
					npc.AI.NextActionNoTarget();
				}
				if (flag3)
				{
					Vector3 position = Player.position;
					Vector3 forward = Vector3.forward;
					Vector3 vector = Player.rotation * forward;
					npc.SetPositionAndRotation(position + vector, Player.eulerAngles);
				}
				if (isOtherInitialized)
				{
					CameraState.SetAngle(Player.eulerAngles);
				}
				if (advNo != -1)
				{
					await UniTask.WaitUntil(() => MiniMapAndCameraActive, PlayerLoopTiming.Update, token);
					await Scene.sceneFadeCanvas.StartFadeAysnc(FadeCanvas.Fade.Out).WithCancellation(token);
				}
			}
		}
		SetDefenseSpeak();
		void SetDefenseSpeak()
		{
			npc.isDefenseSpeak = true;
		}
	}

	private async UniTask OpenADV(Heroine heroine, ADV.CharaData.MotionReserver motionReserver, CancellationToken token, params int[] advNos)
	{
		for (int i = 0; i < advNos.Length; i++)
		{
			int advNo = advNos[i];
			await Scene.sceneFadeCanvas.StartFadeAysnc(FadeCanvas.Fade.In).WithCancellation(token);
			List<Program.Transfer> list = Program.Transfer.NewList();
			Program.SetParam(Game.Player, heroine, list);
			list.Add(Program.Transfer.Create(true, Command.CameraSetFov, Program.BASE_FOV));
			string text = Program.FindADVBundleFilePath(advNo, heroine);
			list.Add(Program.Transfer.Open(text, advNo.ToString(), bool.TrueString));
			bool isOpenADV = false;
			await Program.Open(new Data
			{
				fadeInTime = 0f,
				camera = null,
				heroineList = new List<Heroine> { heroine },
				scene = this,
				transferList = list,
				isParentChara = true,
				motionReserverList = new List<ADV.CharaData.MotionReserver> { motionReserver }
			}, token, new Program.OpenDataProc
			{
				onLoad = delegate
				{
					isOpenADV = true;
				}
			});
			await UniTask.WaitUntil(() => isOpenADV, PlayerLoopTiming.Update, token);
			await Program.Wait(string.Empty, token);
			heroine.talkEvent.Add(advNo);
		}
	}

	public async UniTask ChangeHEvent(Vector3 position, Quaternion rotation, CancellationToken token, int appoint = -1, bool isKokanForceInsert = false, params (int no, Heroine heroine)[] heroines)
	{
		shortcutKey = false;
		for (int i = 0; i < heroines.Length; i++)
		{
			(int no, Heroine heroine) tuple = heroines[i];
			tuple.heroine.talkEvent.Add(4);
			tuple.heroine.talkEvent.Add(5);
		}
		OpenHData orAddComponent = Scene.commonSpace.GetOrAddComponent<OpenHData>();
		orAddComponent.data = new OpenHData.Data
		{
			position = position,
			rotation = rotation,
			maleCoordinateType = heroines.First().no,
			lstFemaleCoordinateType = heroines.Select(((int no, Heroine heroine) p) => p.no).ToList(),
			lstFemale = heroines.Select(((int no, Heroine heroine) p) => p.heroine).ToList(),
			state = 0,
			appoint = appoint,
			isKokanForceInsert = isKokanForceInsert
		};
		await ChangeHCommon(orAddComponent, isEvent: true, token);
	}

	public async UniTask ChangeHEventWithActionPoint(Vector3 position, Quaternion rotation, CancellationToken token, int appoint = -1, params Base[] targets)
	{
		shortcutKey = false;
		OpenHData orAddComponent = Scene.commonSpace.GetOrAddComponent<OpenHData>();
		foreach (ChaControl item in from p in targets
			select p.charaData.chaCtrl into chaCtrl
			where chaCtrl != null && chaCtrl.hiPoly
			select chaCtrl)
		{
			Manager.Character.DeleteChara(item);
		}
		Base[] array = targets;
		foreach (Base @base in array)
		{
			@base.charaData.SetRoot(@base.gameObject);
		}
		Player.SetActive(active: false);
		npcList.ForEach(delegate(NPC p)
		{
			p.SetActive(active: false);
		});
		npcList.ForEach(delegate(NPC p)
		{
			p.Pause(isPause: true);
		});
		if (fixChara != null)
		{
			fixChara.SetActive(active: false);
		}
		orAddComponent.data = new OpenHData.Data
		{
			position = position,
			rotation = rotation,
			lstFemale = targets.Select((Base p) => p.heroine).ToList(),
			maleCoordinateType = Player.chaCtrl.fileStatus.coordinateType,
			lstFemaleCoordinateType = targets.Select((Base p) => p.chaCtrl.fileStatus.coordinateType).ToList(),
			state = 0,
			appoint = appoint
		};
		await ChangeHCommon(orAddComponent, isEvent: false, token);
		array = targets;
		foreach (Base base2 in array)
		{
			base2.charaData.SetRoot(base2.gameObject);
		}
	}

	public async UniTask ChangeH_ActionPoint(Kind kind, bool isLoadPeepLoom, CancellationToken token, params Base[] targets)
	{
		await ChangeH(kind, isKindPlayer: true, isLoadPeepLoom, isInvited: false, token, targets);
	}

	public async UniTask ChangeH_Fix(Kind kind, CancellationToken token, params Base[] targets)
	{
		await ChangeH(kind, isKindPlayer: false, isLoadPeepLoom: false, isInvited: false, token, targets);
	}

	public async UniTask ChangeH_NPC(Kind kind, bool isKindPlayer, bool isInvited, CancellationToken token, params Base[] targets)
	{
		await ChangeH(kind, isKindPlayer, isLoadPeepLoom: false, isInvited, token, targets);
	}

	private async UniTask ChangeH(Kind kind, bool isKindPlayer, bool isLoadPeepLoom, bool isInvited, CancellationToken token, params Base[] targets)
	{
		shortcutKey = false;
		foreach (ChaControl item in from p in targets
			select p.charaData.chaCtrl into chaCtrl
			where chaCtrl != null && chaCtrl.hiPoly
			select chaCtrl)
		{
			Manager.Character.DeleteChara(item);
		}
		Base[] array = targets;
		foreach (Base @base in array)
		{
			@base.charaData.SetRoot(@base.gameObject);
		}
		bool flag = false;
		bool isEasyPlace = false;
		NPC nPC = targets.OfType<NPC>().FirstOrDefault();
		if (nPC != null)
		{
			isInvited |= nPC.AI.actionNo == 5;
			flag = nPC.isOnanismInSight;
			if (flag && (nPC.heroine.HExperience <= Heroine.HExperienceKind.不慣れ || nPC.heroine.lewdness < 80))
			{
				flag = false;
			}
			isEasyPlace = nPC.isEasyPlace;
		}
		Player.SetActive(active: false);
		npcList.ForEach(delegate(NPC p)
		{
			p.SetActive(active: false);
		});
		npcList.ForEach(delegate(NPC p)
		{
			p.Pause(isPause: true);
		});
		if (fixChara != null)
		{
			fixChara.SetActive(active: false);
		}
		Base base2 = targets[0];
		Base base3 = (isKindPlayer ? Player : base2);
		Vector3 vector = base3.position;
		Quaternion rotation = base3.rotation;
		if (kind != null)
		{
			Collider component = kind.GetComponent<Collider>();
			if (component != null)
			{
				vector = component.ClosestPointOnBounds(base3.position);
				vector.y = base3.position.y;
			}
			if (kind.category == 3)
			{
				rotation = Quaternion.LookRotation((vector - base3.position).normalized);
			}
		}
		OpenHData orAddComponent = Scene.commonSpace.GetOrAddComponent<OpenHData>();
		orAddComponent.data = new OpenHData.Data
		{
			position = vector,
			rotation = rotation,
			kind = kind,
			lstFemale = targets.Select((Base p) => p.heroine).ToList(),
			maleCoordinateType = Player.chaCtrl.fileStatus.coordinateType,
			lstFemaleCoordinateType = targets.Select((Base p) => p.chaCtrl.fileStatus.coordinateType).ToList(),
			state = base2.state,
			isInvited = isInvited,
			isFound = flag,
			isEasyPlace = isEasyPlace,
			isLoadPeepLoom = isLoadPeepLoom
		};
		await ChangeHCommon(orAddComponent, isEvent: false, token);
		array = targets;
		foreach (Base base4 in array)
		{
			base4.charaData.SetRoot(base4.gameObject);
		}
	}

	public async UniTask ChangeHSpecial(ActionPoint point, Kind kind, CancellationToken token, params Base[] targets)
	{
		await ChangeHSpecial(point.HType, point.IsLoadPeepLoom, point.transform, delegate(OpenHData.Data data)
		{
			data.kind = kind;
			data.isLoadPeepLoom = point.IsLoadPeepLoom;
			data.offsetPos = point.HOffsetPos;
			data.offsetAngle = point.HOffsetAngle;
			data.visibleChange = point.VisibleChange;
		}, token, targets);
	}

	public async UniTask ChangeHSpecial(List<int> peepCategory, bool isLoadPeepLoom, Transform point, Action<OpenHData.Data> action, CancellationToken token, params Base[] targets)
	{
		await ChangeHSpecial(peepCategory, isLoadPeepLoom, point.position, point.rotation, action, token, targets);
	}

	public async UniTask ChangeHSpecial(List<int> peepCategory, bool isLoadPeepLoom, Vector3 position, Quaternion rotation, Action<OpenHData.Data> action, CancellationToken token, params Base[] targets)
	{
		shortcutKey = false;
		if (isLoadPeepLoom)
		{
			_isInChargeBGM = true;
		}
		foreach (ChaControl item in from p in targets
			select p.charaData.chaCtrl into chaCtrl
			where chaCtrl != null && chaCtrl.hiPoly
			select chaCtrl)
		{
			Manager.Character.DeleteChara(item);
		}
		Base[] array = targets;
		foreach (Base @base in array)
		{
			@base.charaData.SetRoot(@base.gameObject);
		}
		bool isInvited = false;
		bool flag = false;
		bool isEasyPlace = false;
		NPC nPC = targets.OfType<NPC>().FirstOrDefault();
		if (nPC != null)
		{
			isInvited = nPC.AI.actionNo == 5;
			flag = nPC.isOnanismInSight;
			if (flag && (nPC.heroine.HExperience <= Heroine.HExperienceKind.不慣れ || nPC.heroine.lewdness < 80))
			{
				flag = false;
			}
			isEasyPlace = nPC.isEasyPlace;
		}
		Player.SetActive(active: false);
		npcList.ForEach(delegate(NPC p)
		{
			p.SetActive(active: false);
		});
		npcList.ForEach(delegate(NPC p)
		{
			p.Pause(isPause: true);
		});
		if (fixChara != null)
		{
			fixChara.SetActive(active: false);
		}
		OpenHData orAddComponent = Scene.commonSpace.GetOrAddComponent<OpenHData>();
		orAddComponent.data = new OpenHData.Data
		{
			position = position,
			rotation = rotation,
			lstFemale = targets.Select((Base p) => p.heroine).ToList(),
			maleCoordinateType = Player.chaCtrl.fileStatus.coordinateType,
			lstFemaleCoordinateType = targets.Select((Base p) => p.chaCtrl.fileStatus.coordinateType).ToList(),
			peepCategory = peepCategory,
			isInvited = isInvited,
			isFound = flag,
			isEasyPlace = isEasyPlace,
			isLoadPeepLoom = isLoadPeepLoom
		};
		action?.Invoke(orAddComponent.data as OpenHData.Data);
		await ChangeHCommon(orAddComponent, isEvent: false, token);
		array = targets;
		foreach (Base base2 in array)
		{
			base2.charaData.SetRoot(base2.gameObject);
		}
	}

	private async UniTask ChangeHCommon(OpenHData openData, bool isEvent, CancellationToken token)
	{
		isPenetration = false;
		hSceneOtherHeroine = null;
		Scene.sceneFadeCanvas.DefaultColor();
		OpenHData.Data data = openData.data as OpenHData.Data;
		data.camera = new OpenData.CameraData
		{
			position = cameraTransform.position,
			rotation = cameraTransform.rotation
		};
		data.map = Map.mapRoot;
		data.player = Game.Player;
		(Vector3, Quaternion)? backupPlayerPos = null;
		(Vector3, Quaternion)? backupChaserPos = null;
		if (data.isDarkness)
		{
			backupPlayerPos = (Player.position, Player.rotation);
			if (Player.chaser != null)
			{
				backupChaserPos = (Player.chaser.position, Player.chaser.rotation);
			}
		}
		List<NPC> hNPCList = data.lstFemale.Select((Heroine p) => p.charaBase).OfType<NPC>().ToList();
		Fix hFixChara = data.lstFemale.Select((Heroine p) => p.charaBase).OfType<Fix>().FirstOrDefault();
		openData.isLoad = false;
		openData.isAsync = true;
		openData.isFade = false;
		openData.fadeType = FadeCanvas.Fade.In;
		int[] array = data.peepCategory.ToArray();
		bool isNotPeep = true;
		if (array.Any())
		{
			isNotPeep = array.Except(Enumerable.Range(2000, 5)).Any();
			if (isNotPeep)
			{
				isNotPeep = array.Except(Enumerable.Range(1010, 8)).Any();
				if (isNotPeep)
				{
					isNotPeep = array.Except(Enumerable.Range(1100, 3)).Any();
					if (!isEvent && !isNotPeep)
					{
						foreach (NPC item in hNPCList)
						{
							item.AI.SetLesbianDesire(AI.LesbianResult.Success);
						}
					}
				}
			}
			else if (array.Contains(2000))
			{
				hNPCList.ForEach(delegate(NPC p)
				{
					p.chaCtrl.RandomChangeOfClothesLowPolyEnd();
				});
			}
		}
		bool isSneakH = array.Any((int x) => x == 1002 || x == 1003);
		int mapNo = Map.no;
		string sceneName = "H";
		await UniTask.WaitUntil(() => Scene.NowSceneNames.Contains(sceneName), PlayerLoopTiming.Update, token);
		shortcutKey = true;
		MiniMapAndCameraActive = false;
		nowCamera.gameObject.SetActive(value: false);
		await UniTask.WaitWhile(() => Scene.NowSceneNames.Contains(sceneName), PlayerLoopTiming.Update, token);
		await UniTask.WaitWhile(() => Scene.IsNowLoading, PlayerLoopTiming.Update, token);
		Player.charaData.SetRoot(Player.gameObject);
		if (mapNo != Map.no)
		{
			nowCamera.gameObject.SetActive(value: true);
			Map.Change(mapNo, FadeCanvas.Fade.In);
		}
		await UniTask.WaitWhile(() => Map.isMapLoading, PlayerLoopTiming.Update, token);
		if (backupPlayerPos.HasValue)
		{
			Player.SetPositionAndRotation(backupPlayerPos.Value.Item1, backupPlayerPos.Value.Item2);
			if (data.isDarkness)
			{
				CameraState.SetAngle(Vector3.zero);
			}
		}
		if (backupChaserPos.HasValue)
		{
			Player.chaser.SetPositionAndRotation(backupChaserPos.Value.Item1, backupChaserPos.Value.Item2);
		}
		if (!nowCamera.gameObject.activeSelf)
		{
			nowCamera.gameObject.SetActive(value: true);
		}
		await UniTask.WaitUntil(() => Scene.AddSceneName.IsNullOrEmpty(), PlayerLoopTiming.Update, token);
		float time = 0f;
		while (nowCamera != Camera.main && time < 5f)
		{
			time += Time.unscaledDeltaTime;
			await UniTask.Yield(PlayerLoopTiming.Update, token);
		}
		if (isPenetration && !isNotPeep)
		{
			isNotPeep = !Map.Info.isWarning;
		}
		if (!isEvent)
		{
			if (!isNotPeep)
			{
				_isInChargeBGM = false;
				List<GateInfo> list = Map.calcGateDic[Map.no];
				int no = Map.no;
				GateInfo gateInfo = ((no != 8) ? GetSideMap(list) : GetSideMap(list.Where((GateInfo item) => item.mapNo != 9)));
				if (gateInfo == null)
				{
					gateInfo = list.First();
				}
				Map.gateLinkID = gateInfo.linkID;
				Player.mapNo = gateInfo.mapNo;
				if (Player.chaser != null)
				{
					Player.chaser.mapNo = Player.mapNo;
				}
				Map.Change(gateInfo.mapNo, FadeCanvas.Fade.None);
			}
			await UniTask.WaitWhile(() => Map.isMapLoading);
			Player.SetActive(active: true);
			npcList.ForEach(delegate(NPC p)
			{
				p.charaData.SetRoot(p.gameObject);
			});
			npcList.ForEach(delegate(NPC p)
			{
				p.SetActive(p.mapNo == Map.no);
			});
			npcList.ForEach(delegate(NPC p)
			{
				p.move.isStop = false;
			});
			npcList.ForEach(delegate(NPC p)
			{
				p.Pause(isPause: false);
			});
			foreach (NPC item2 in hNPCList.Where((NPC p) => p.AI.actionNo != 23))
			{
				item2.AI.NextActionNoTarget();
			}
			NPC otherNPC = ((hSceneOtherHeroine == null) ? null : (hSceneOtherHeroine.charaBase as NPC));
			if (otherNPC != null && !hNPCList.Contains(otherNPC))
			{
				otherNPC.AI.NextActionNoTarget();
			}
			if (isNotPeep && !data.isDarkness && !isSneakH)
			{
				Base @base = hFixChara;
				if (@base == null)
				{
					@base = hNPCList.SafeGet(0);
				}
				if (@base != null)
				{
					foreach (NPC item3 in from p in GetOtherNPC(10f, @base)
						where p != otherNPC
						where !hNPCList.Contains(p)
						select p)
					{
						if (item3.AI.isArrival && item3.AI.ActionNoCheck(1, 2, 48))
						{
							continue;
						}
						Heroine.HExperienceKind hExperience = item3.heroine.HExperience;
						if (hExperience == Heroine.HExperienceKind.淫乱)
						{
							actCtrl.AddDesire(30, item3.heroine, 50);
							if (actCtrl.GetDesire(30, item3.heroine) >= 100)
							{
								if (Player.chaser == item3)
								{
									Player.ChaserEnd();
								}
								item3.AI.HesitantlyWait();
							}
							actCtrl.AddDesire(5, item3.heroine, 10);
							actCtrl.AddDesire(4, item3.heroine, 10);
							if (item3.heroine.parameter.attribute.likeGirls)
							{
								actCtrl.AddDesire(26, item3.heroine, 10);
							}
						}
						else
						{
							if (Player.chaser == item3)
							{
								Player.ChaserEnd();
							}
							item3.AI.EscapeAction(isAnger: false);
							actCtrl.AddDesire(5, item3.heroine, 5);
							actCtrl.AddDesire(4, item3.heroine, 5);
							if (item3.heroine.parameter.attribute.likeGirls)
							{
								actCtrl.AddDesire(26, item3.heroine, 5);
							}
						}
					}
				}
			}
			if (fixChara != null && fixChara.mapNo == Map.no)
			{
				fixChara.SetActive(active: true);
			}
			await UniTask.WaitUntil(() => MiniMapAndCameraActive, PlayerLoopTiming.Update, token);
		}
		Manager.Sound.Listener = cameraTransform;
		if (!isEvent)
		{
			await Scene.sceneFadeCanvas.StartFadeAysnc(FadeCanvas.Fade.Out).WithCancellation(token);
		}
		GateInfo GetSideMap(IEnumerable<GateInfo> iter)
		{
			MapInfo.Param value;
			return (from p in iter
				where Map.infoDic.TryGetValue(p.mapNo, out value) && !value.isWarning
				orderby (p.pos - Player.position).sqrMagnitude
				select p).FirstOrDefault();
		}
	}

	public async UniTask ChangeEsthetic(ActionPoint point, Action<OpenEstheticData.Data> action, bool isFadeout, CancellationToken token, params Base[] targets)
	{
		shortcutKey = false;
		foreach (ChaControl item in from p in targets
			select p.charaData.chaCtrl into chaCtrl
			where chaCtrl != null && chaCtrl.hiPoly
			select chaCtrl)
		{
			Manager.Character.DeleteChara(item);
		}
		Base[] array = targets;
		foreach (Base @base in array)
		{
			@base.charaData.SetRoot(@base.gameObject);
		}
		targets.OfType<NPC>().FirstOrDefault();
		Player.SetActive(active: false);
		npcList.ForEach(delegate(NPC p)
		{
			p.SetActive(active: false);
		});
		npcList.ForEach(delegate(NPC p)
		{
			p.Pause(isPause: true);
		});
		if (fixChara != null)
		{
			fixChara.SetActive(active: false);
		}
		OpenEstheticData orAddComponent = Scene.commonSpace.GetOrAddComponent<OpenEstheticData>();
		Transform transform = point.transform;
		orAddComponent.data = new OpenEstheticData.Data
		{
			position = transform.position,
			rotation = transform.rotation,
			offsetPos = point.HOffsetPos,
			offsetAngle = point.HOffsetAngle,
			isLoadMap = point.IsLoadPeepLoom,
			visibleChange = point.VisibleChange,
			lstFemale = targets.Select((Base p) => p.heroine).ToList(),
			state = ((point.HType != null && point.HType.Any()) ? point.HType[0] : 0)
		};
		action?.Invoke(orAddComponent.data as OpenEstheticData.Data);
		if (point.IsLoadPeepLoom)
		{
			UnityEngine.Object.Destroy(point.gameObject);
		}
		isPenetration = false;
		hSceneOtherHeroine = null;
		OpenEstheticData.Data data = orAddComponent.data as OpenEstheticData.Data;
		data.camera = new OpenData.CameraData
		{
			position = cameraTransform.position,
			rotation = cameraTransform.rotation
		};
		(Vector3, Quaternion)? backupPlayerPos = null;
		(Vector3, Quaternion)? backupChaserPos = null;
		List<NPC> hNPCList = data.lstFemale.Select((Heroine p) => p.charaBase).OfType<NPC>().ToList();
		orAddComponent.isLoad = false;
		orAddComponent.isAsync = true;
		orAddComponent.isFade = false;
		orAddComponent.fadeType = FadeCanvas.Fade.In;
		int mapNo = Map.no;
		string sceneName = "EstheticScene";
		await UniTask.WaitUntil(() => Scene.NowSceneNames.Contains(sceneName), PlayerLoopTiming.Update, token);
		shortcutKey = true;
		MiniMapAndCameraActive = false;
		nowCamera.gameObject.SetActive(value: false);
		await UniTask.WaitWhile(() => Scene.NowSceneNames.Contains(sceneName), PlayerLoopTiming.Update, token);
		await UniTask.WaitWhile(() => Scene.IsNowLoading, PlayerLoopTiming.Update, token);
		Player.charaData.SetRoot(Player.gameObject);
		if (mapNo != Map.no)
		{
			nowCamera.gameObject.SetActive(value: true);
			Map.Change(mapNo, FadeCanvas.Fade.In);
		}
		await UniTask.WaitWhile(() => Map.isMapLoading, PlayerLoopTiming.Update, token);
		if (backupPlayerPos.HasValue)
		{
			Player.SetPositionAndRotation(backupPlayerPos.Value.Item1, backupPlayerPos.Value.Item2);
		}
		if (backupChaserPos.HasValue)
		{
			Player.chaser.SetPositionAndRotation(backupChaserPos.Value.Item1, backupChaserPos.Value.Item2);
		}
		if (!nowCamera.gameObject.activeSelf)
		{
			nowCamera.gameObject.SetActive(value: true);
		}
		await UniTask.WaitUntil(() => Scene.AddSceneName.IsNullOrEmpty(), PlayerLoopTiming.Update, token);
		await UniTask.WaitWhile(() => nowCamera != Camera.main, PlayerLoopTiming.Update, token).TimeoutWithoutException(TimeSpan.FromSeconds(5.0));
		Player.SetActive(active: true);
		npcList.ForEach(delegate(NPC p)
		{
			p.charaData.SetRoot(p.gameObject);
		});
		npcList.ForEach(delegate(NPC p)
		{
			p.SetActive(p.mapNo == Map.no);
		});
		npcList.ForEach(delegate(NPC p)
		{
			p.move.isStop = false;
		});
		npcList.ForEach(delegate(NPC p)
		{
			p.Pause(isPause: false);
		});
		foreach (NPC item2 in hNPCList.Where((NPC p) => p.AI.actionNo != 23))
		{
			item2.AI.NextActionNoTarget();
		}
		if (fixChara != null && fixChara.mapNo == Map.no)
		{
			fixChara.SetActive(active: true);
		}
		await UniTask.WaitUntil(() => MiniMapAndCameraActive, PlayerLoopTiming.Update, token);
		Manager.Sound.Listener = cameraTransform;
		if (isFadeout)
		{
			await Scene.sceneFadeCanvas.StartFadeAysnc(FadeCanvas.Fade.Out);
		}
		array = targets;
		foreach (Base base2 in array)
		{
			base2.charaData.SetRoot(base2.gameObject);
		}
	}

	public void SceneEvent(NPC npc)
	{
		SceneEventAsync(npc).Forget();
	}

	public async UniTask SceneEventAsync(NPC npc)
	{
		await _SceneEvent(npc);
	}

	public void OtherAIPause(NPC npc, bool stop)
	{
		Func<NPC, List<AI>> func = (NPC my) => (from p in npcList
			where p != my
			select p into item
			select item.AI).ToList();
		if (otherPauseNPC != null && npc != null)
		{
			func(otherPauseNPC).ForEach(delegate(AI item)
			{
				item.Start();
			});
		}
		if (stop)
		{
			func(npc).ForEach(delegate(AI item)
			{
				item.Pause();
			});
		}
		else
		{
			func(npc).ForEach(delegate(AI item)
			{
				item.Start();
			});
		}
		otherPauseNPC = npc;
	}

	public void SetAllCharaActive(bool isActive)
	{
		PlayerCreate();
		Player.ChaserEnd();
		if (isActive)
		{
			Player.isLesMotionPlay = true;
			Player.isOnanismMotionPlay = true;
			Player.isActionNow = false;
			SaveData.CharaData.MoveData moveData = Player.charaData.moveData;
			if (moveData.isAlive)
			{
				Player.mapNo = moveData.mapNo;
				Player.SetPositionAndRotation(moveData.position, moveData.angle);
				CameraState.SetAngle(moveData.angle);
				Player.charaData.moveData.Set(null);
			}
			else
			{
				Player.mapNo = Map.no;
				Transform warpPointTransform = Map.warpPointTransform;
				if (warpPointTransform != null)
				{
					Player.SetPositionAndRotation(warpPointTransform);
					CameraState.SetAngle(warpPointTransform.eulerAngles);
				}
				else
				{
					Player.position = Map.warpPoint;
				}
			}
			Player.ItemClear();
			Player.isPopOK = true;
			Player.move.animParamSpeed = 0f;
			Player.SetActive(Map.IsPop(Player));
			var source = npcList.Select((NPC npc) => new
			{
				npc = npc,
				isLoad = (npc.heroine.moveData.resultInfo != null)
			}).ToArray();
			foreach (NPC npc4 in npcList)
			{
				npc4.isDefenseSpeak = false;
				npc4.isPopOK = true;
				npc4.ItemClear();
				npc4.AI.FirstAction();
			}
			source = source.OrderBy(x =>
			{
				int actionNo = x.npc.AI.actionNo;
				return actionNo switch
				{
					47 => 0, 
					48 => 2, 
					49 => 9, 
					50 => 16, 
					51 => 17, 
					52 => 22, 
					53 => 26, 
					54 => 4, 
					55 => 31, 
					56 => 32, 
					_ => actionNo, 
				};
			}).ToArray();
			var array = source;
			foreach (var obj in array)
			{
				NPC npc2 = obj.npc;
				if (obj.isLoad)
				{
					npc2.AI.LoadAfter();
				}
				npc2.ReStart();
				switch (npc2.AI.actionNo)
				{
				case 23:
					Player.ChaserSet(npc2);
					break;
				case 25:
					npc2.isLoadShyPlay = true;
					break;
				}
				npc2.charaData.moveData.Set(null);
			}
			foreach (var item in source.Where(x => x.isLoad))
			{
				NPC npc3 = item.npc;
				if (npc3.isActive)
				{
					int i = npc3.AI.actionNo;
					if ((uint)(i - 26) <= 1u || i == 53)
					{
						npc3.PlayAnimation();
					}
				}
			}
			if (fixChara != null)
			{
				fixChara.isPopOK = true;
				fixChara.ItemClear();
				fixChara.SetActive(Map.IsPop(fixChara));
				fixChara.charaData.moveData.Set(null);
			}
			Map.PlayBGM();
			return;
		}
		Player.isActionNow = true;
		Player.isPopOK = false;
		Player.SetActive(active: false);
		npcList.ForEach(delegate(NPC npc)
		{
			npc.isPopOK = false;
			npc.Stop();
			if (npc.chaCtrl != null)
			{
				npc.chaCtrl.RandomChangeOfClothesLowPolyEnd();
			}
		});
		if (fixChara != null)
		{
			fixChara.isPopOK = false;
			fixChara.SetActive(active: false);
		}
	}

	public Heroine GetHSceneOtherHeroine(Heroine target)
	{
		if (target.fixCharaID < 0)
		{
			return null;
		}
		Base chara = target.charaBase;
		if (chara == null)
		{
			return null;
		}
		int mapNo = chara.mapNo;
		if (Map.no != mapNo)
		{
			return null;
		}
		float distance = 10f;
		return hSceneOtherHeroine = (from p in (from p in npcList
				where p != chara
				where p.mapNo == mapNo
				where p.heroine.HExperience >= Heroine.HExperienceKind.慣れ
				where !p.AI.isArrival || !p.AI.ActionNoCheck(1, 2, 48)
				select new
				{
					heroine = p.heroine,
					sqrDis = (p.position - chara.position).sqrMagnitude
				} into p
				orderby p.sqrDis
				select p).TakeWhile(p => p.sqrDis < distance * distance)
			select p.heroine).FirstOrDefault(delegate(Heroine heroine)
		{
			float num = ((heroine.HExperience == Heroine.HExperienceKind.慣れ) ? 20 : 40);
			num += (float)actCtrl.GetDesire(5, heroine) * 0.5f;
			num += (float)heroine.lewdness * 0.5f;
			if (heroine.parameter.attribute.bitch)
			{
				num += 10f;
			}
			return Illusion.Utils.ProbabilityCalclator.DetectFromPercent(num);
		});
	}

	public NPC[] GetOtherNPC(float distance, Base chara)
	{
		if (chara == null)
		{
			return new NPC[0];
		}
		return (from p in (from p in npcList
				where p != chara
				where p.mapNo == chara.mapNo
				select new
				{
					chara = p,
					sqrDis = (p.position - chara.position).sqrMagnitude
				} into p
				orderby p.sqrDis
				select p).TakeWhile(p => p.sqrDis < distance * distance)
			select p.chara).ToArray();
	}

	public async UniTask NPCLoadAll(bool isShuffle)
	{
		Manager.Character.enableCharaLoadGCClear = false;
		foreach (KeyValuePair<int, List<WaitPoint>> item4 in PosSet.waitPointDic)
		{
			item4.Value.ForEach(delegate(WaitPoint p)
			{
				p.UnReserve();
			});
		}
		Game.HeroineList.ForEach(delegate(Heroine p)
		{
			p.PopInit();
		});
		List<NPC> reloadList = new List<NPC>();
		List<Tuple<NPC, Heroine>> replaceList = new List<Tuple<NPC, Heroine>>();
		if (!isShuffle)
		{
			reloadList.AddRange(npcList);
		}
		else
		{
			Game.HeroineList.ForEach(delegate(Heroine item)
			{
				item.SetRoot(null);
			});
			List<NPC> list = npcList.ToList();
			List<Heroine> list2 = new List<Heroine>();
			foreach (Heroine item3 in (from p in Game.HeroineList.Shuffle()
				where p.fixCharaID == 0
				select p).Take(Manager.Config.EtcData.MaxCharaNum))
			{
				int num = list.FindIndex((NPC p) => p.charaData == item3);
				if (num != -1)
				{
					reloadList.Add(list[num]);
					list.RemoveAt(num);
				}
				else
				{
					list2.Add(item3);
				}
			}
			int j;
			for (j = 0; j < list.Count && j < list2.Count; j++)
			{
				replaceList.Add(Tuple.Create(list[j], list2[j]));
			}
			if (list.Count != list2.Count)
			{
				if (list.Count > list2.Count)
				{
					for (; j < list.Count; j++)
					{
						NPC nPC = list[j];
						npcList.Remove(nPC);
						UnityEngine.Object.Destroy(nPC.gameObject);
					}
				}
				else if (list.Count < list2.Count)
				{
					npcList.AddRange(from heroine in list2.Skip(j)
						select NPC.Create(base.transform, heroine));
				}
			}
		}
		otherPauseNPC = null;
		npcList.ForEach(delegate(NPC npc)
		{
			npc.AI.Reset(isResult: true);
		});
		npcList.ForEach(delegate(NPC npc)
		{
			npc.LoadStart();
		});
		Scene.DrawImageAndProgress(0f);
		int count = npcList.Count + 1;
		foreach (var item2 in npcList.Select((NPC p, int i) => new { p, i }))
		{
			Scene.DrawImageAndProgress((float)(item2.i + 1) / (float)count);
			await UniTask.WaitUntil(() => item2.p.initialized);
			if (reloadList.Contains(item2.p))
			{
				item2.p.charaData.SetRoot(item2.p.gameObject);
				continue;
			}
			foreach (Tuple<NPC, Heroine> item5 in replaceList)
			{
				if (item5.Item1 == item2.p)
				{
					item5.Item1.Replace(item5.Item2);
					break;
				}
			}
		}
		_actCtrl.NextTime();
		await FixCharaSetting();
		AsyncOperation ao = Manager.Character.EndLoadAssetBundle();
		if (ao != null)
		{
			await UniTask.WaitUntil(() => ao.isDone);
		}
		Scene.DrawImageAndProgress(1f);
		await UniTask.Yield();
		Scene.DrawImageAndProgress();
		Manager.Character.enableCharaLoadGCClear = true;
	}

	private async UniTask FixCharaSetting()
	{
		Heroine fixHeroine = Game.HeroineList.Shuffle().FirstOrDefault((Heroine x) => x.fixCharaID < 0);
		if (fixChara != null && !fixChara.isCharaLoad)
		{
			FixCharaDelete();
		}
		if (await FixCharaSetPosition(fixHeroine))
		{
			fixChara.SetActivate();
		}
		else if (fixChara != null && fixChara.isCharaLoad)
		{
			fixChara.SetStandby();
		}
		else
		{
			FixCharaDelete();
		}
	}

	private async UniTask<bool> FixCharaSetPosition(Heroine fixHeroine, int? mapNo = null)
	{
		int fixCharaID = fixHeroine.fixCharaID;
		if (fixCharaID == -13)
		{
			Cycle.Type nowType = Cycle.nowType;
			string layerName;
			if ((uint)(nowType - 1) <= 1u)
			{
				layerName = GuideLayerNames[0];
				Game.saveData.guideSetPositionMaps.Clear();
			}
			else
			{
				if (!fixHeroine.isFriend)
				{
					return false;
				}
				layerName = GuideLayerNames[1];
			}
			WaitPoint wp = null;
			int layerIndex = -1;
			int motionIndex = -1;
			SaveData.CharaData.MoveData moveData = fixHeroine.moveData;
			bool flag = false;
			if (moveData.isAlive && moveData.waitPointID != int.MinValue)
			{
				foreach (WaitPoint item2 in PosSet.waitPointDic.Values.SelectMany((List<WaitPoint> x) => x))
				{
					if (item2.ID == moveData.waitPointID)
					{
						WaitPoint.Parameter parameter = item2.parameterList.SafeGet(moveData.paramIndex);
						if (parameter != null && !(parameter.layer != layerName) && parameter.motionList.SafeGet(moveData.motionIndex) != null)
						{
							wp = item2;
							layerIndex = moveData.paramIndex;
							motionIndex = moveData.motionIndex;
							flag = true;
							break;
						}
					}
				}
			}
			if (!flag)
			{
				List<(WaitPoint, int, int)> list = new List<(WaitPoint, int, int)>();
				int? num = null;
				if (layerName == GuideLayerNames[0])
				{
					Cycle.Week week = (Cycle.Week)Game.saveData.week;
					num = ((week != Cycle.Week.Wednesday && week != Cycle.Week.Saturday) ? new int?(3) : new int?(1));
				}
				foreach (WaitPoint item3 in PosSet.waitPointDic.Values.SelectMany((List<WaitPoint> x) => x).Shuffle())
				{
					int num2 = item3.parameterList.FindIndex((WaitPoint.Parameter p) => p.layer == layerName);
					if (num2 != -1)
					{
						int item = item3.parameterList[num2].motionList.Select((WaitPoint.Parameter.Motion _, int n) => n).Shuffle().First();
						list.Add((item3, num2, item));
					}
				}
				foreach (var item4 in list.Shuffle())
				{
					int mapNo2 = item4.Item1.MapNo;
					if (mapNo.HasValue)
					{
						if (mapNo2 != mapNo)
						{
							continue;
						}
					}
					else if (num.HasValue)
					{
						if (mapNo2 != num)
						{
							continue;
						}
					}
					else
					{
						fixCharaID = fixHeroine.fixCharaID;
						if (fixCharaID == -13 && Game.saveData.guideSetPositionMaps.Contains(mapNo2))
						{
							continue;
						}
					}
					(wp, layerIndex, motionIndex) = item4;
					break;
				}
			}
			if (wp == null || layerIndex == -1)
			{
				return false;
			}
			fixCharaID = fixHeroine.fixCharaID;
			if (fixCharaID == -13)
			{
				Game.saveData.guideSetPositionMaps.Add(wp.MapNo);
			}
			if (fixChara != null)
			{
				fixChara.Replace(fixHeroine);
			}
			else
			{
				fixChara = Fix.Create(base.transform, fixHeroine);
			}
			fixChara.LoadStart();
			await UniTask.WaitUntil(() => fixChara.initialized);
			wp.Reserve(fixChara);
			wp.SetWait(layerIndex, motionIndex);
			return true;
		}
		return false;
	}

	public async UniTask NPCLoadAll()
	{
		Manager.Character.enableCharaLoadGCClear = false;
		foreach (KeyValuePair<int, List<WaitPoint>> item2 in PosSet.waitPointDic)
		{
			item2.Value.ForEach(delegate(WaitPoint p)
			{
				p.UnReserve();
			});
		}
		Game.HeroineList.ForEach(delegate(Heroine p)
		{
			p.SetRoot(null);
		});
		foreach (NPC npc in npcList)
		{
			UnityEngine.Object.Destroy(npc.gameObject);
		}
		npcList.Clear();
		IEnumerable<Heroine> source = Game.HeroineList.Where((Heroine p) => p.fixCharaID == 0 && p.moveData.isAlive);
		npcList.AddRange(source.Select((Heroine heroine) => NPC.Create(base.transform, heroine)));
		otherPauseNPC = null;
		npcList.ForEach(delegate(NPC npc)
		{
			npc.AI.Reset(isResult: true);
		});
		npcList.ForEach(delegate(NPC npc)
		{
			npc.LoadStart();
		});
		foreach (NPC item in npcList)
		{
			await UniTask.WaitUntil(() => item.initialized);
			item.charaData.SetRoot(item.gameObject);
		}
		await FixCharaSetting();
		AsyncOperation ao = Manager.Character.EndLoadAssetBundle();
		if (ao != null)
		{
			await UniTask.WaitUntil(() => ao.isDone);
		}
		Scene.DrawImageAndProgress(1f);
		await UniTask.Yield();
		Scene.DrawImageAndProgress();
		Manager.Character.enableCharaLoadGCClear = true;
	}

	public void ShortcutKeyRegulate(bool enable)
	{
		foreach (ShortcutKey.Proc proc in _actionShortcutKey.procList)
		{
			KeyCode keyCode = proc.keyCode;
			if (keyCode == KeyCode.F3)
			{
				proc.enabled = enable;
			}
		}
	}

	public void OpenMapMoveAllMenu()
	{
		if (IsShortcutKey(string.Empty))
		{
			Illusion.Game.Utils.Sound.Play(SystemSE.window_o);
			MapSelectMenuScene.CreateMapMove(Map);
		}
	}

	public void OpenStatus()
	{
		if (IsShortcutKey(string.Empty))
		{
			Illusion.Game.Utils.Sound.Play(SystemSE.window_o);
			HeroineStatusScene.Load();
		}
	}

	public void NextCycle()
	{
		if (IsShortcutKey(string.Empty))
		{
			Cycle.ActionEnd();
		}
	}

	private bool IsShortcutKey(string levelName)
	{
		if (isEventNow)
		{
			return false;
		}
		if (Scene.IsNowLoadingFade)
		{
			return false;
		}
		if (_cycle.isActionEnd)
		{
			return false;
		}
		if (Program.isADVProcessing)
		{
			return false;
		}
		if (ShortcutKey.IsReglate(levelName))
		{
			return false;
		}
		if (!Scene.AddSceneName.IsNullOrEmpty())
		{
			return false;
		}
		if (Scene.IsOverlap)
		{
			return false;
		}
		if (Player != null && Player.isActionNow_Origin)
		{
			return false;
		}
		return true;
	}

	private void OpenMenu(string assetBundleName, string levelName, Action onLoad)
	{
		Scene.LoadReserve(new Scene.Data
		{
			bundleName = assetBundleName,
			levelName = levelName,
			isAdd = true,
			onLoad = onLoad
		}, isLoadingImageDraw: false);
	}
}
