using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using ADV;
using AIProject.Definitions;
using AIProject.Player;
using AIProject.SaveData;
using AIProject.UI;
using ConfigScene;
using Housing;
using Manager;
using ReMotion;
using Sirenix.OdinInspector;
using UniRx;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using UnityEx;

namespace AIProject.Scene;

public class MapScene : Singleton<MapScene>, ISystemCommand
{
	[Header("Root")]
	[SerializeField]
	[FormerlySerializedAs("Map")]
	[Indent(1)]
	private Transform _mapRoot;

	[SerializeField]
	[FormerlySerializedAs("Entity")]
	[Indent(1)]
	private Transform _actorRoot;

	[SerializeField]
	private NavMeshSurface _navMeshSurface;

	[SerializeField]
	private Transform _startPositionPoint;

	[SerializeField]
	private Transform _warpPoint;

	private List<AIProject.UI.ICommandData> _systemCommands = new List<AIProject.UI.ICommandData>();

	private bool _enabledCommandArea;

	private bool[] _charasEntry;

	public static List<UnityEx.ValueTuple<string, string>> AssetBundlePaths { get; private set; } = new List<UnityEx.ValueTuple<string, string>>();

	public NavMeshSurface NavMeshSurface
	{
		[CompilerGenerated]
		get
		{
			return _navMeshSurface;
		}
	}

	public Transform WarpPoint
	{
		[CompilerGenerated]
		get
		{
			return _warpPoint;
		}
	}

	public bool EnabledInput { get; set; }

	public bool IsLoading { get; private set; }

	public bool IsCursorLock { get; set; }

	public bool isLoadEnd { get; private set; }

	public void OnUpdateInput()
	{
		Manager.Input input = Singleton<Manager.Input>.Instance;
		foreach (AIProject.UI.ICommandData systemCommand in _systemCommands)
		{
			systemCommand.Invoke(input);
		}
	}

	public static void AddAssetBundlePath(AssetBundleInfo info)
	{
		if (!AssetBundlePaths.Exists((UnityEx.ValueTuple<string, string> x) => x.Item1 == info.assetbundle && x.Item2 == info.manifest))
		{
			AssetBundlePaths.Add(new UnityEx.ValueTuple<string, string>(info.assetbundle, info.manifest));
		}
	}

	public static void AddAssetBundlePath(string assetBundle, string manifest)
	{
		if (!AssetBundlePaths.Exists((UnityEx.ValueTuple<string, string> x) => x.Item1 == assetBundle && x.Item2 == manifest))
		{
			AssetBundlePaths.Add(new UnityEx.ValueTuple<string, string>(assetBundle, manifest));
		}
	}

	public static void AddAssetBundlePath(string assetBundle)
	{
		if (!AssetBundlePaths.Exists((UnityEx.ValueTuple<string, string> x) => x.Item1 == assetBundle))
		{
			AssetBundlePaths.Add(new UnityEx.ValueTuple<string, string>(assetBundle, string.Empty));
		}
	}

	public static bool EqualsSequence(bool[] a, bool[] b)
	{
		if (a.Length != b.Length)
		{
			return false;
		}
		for (int i = 0; i < a.Length; i++)
		{
			if (a[i] != b[i])
			{
				return false;
			}
		}
		return true;
	}

	private void StartFadingStream(IConnectableObservable<TimeInterval<float>> stream)
	{
		LoadingPanel loadingPanel = Singleton<Manager.Scene>.Instance.loadingPanel;
		float startAlpha = loadingPanel.CanvasGroup.alpha;
		Manager.Map map = Singleton<Manager.Map>.Instance;
		foreach (KeyValuePair<int, AgentActor> item in map.AgentTable)
		{
			if (item.Value.AgentData.CarryingItem != null && item.Value.Mode != Desire.ActionType.SearchEatSpot && item.Value.Mode != Desire.ActionType.EndTaskEat && item.Value.Mode != Desire.ActionType.EndTaskEatThere)
			{
				item.Value.AgentData.CarryingItem = null;
			}
			item.Value.EnableBehavior();
			item.Value.BehaviorResources.ChangeMode(item.Value.Mode);
		}
		PlayerActor player = map.Player;
		string prevPlayerStateFromCharaCreate = Game.PrevPlayerStateFromCharaCreate;
		if (!prevPlayerStateFromCharaCreate.IsNullOrEmpty())
		{
			player.PlayerController.ChangeState(prevPlayerStateFromCharaCreate);
			player.CurrentDevicePoint = Singleton<Manager.Map>.Instance.PointAgent.DevicePointDic[Game.PrevAccessDeviceID];
		}
		else
		{
			ReadOnlyDictionary<int, AgentActor> agentTable = map.AgentTable;
			int? partnerID = player.PlayerData.PartnerID;
			if (agentTable.TryGetValue((!partnerID.HasValue) ? (-1) : partnerID.Value, out var value))
			{
				player.Partner = value;
				value.Partner = player;
				if (player.PlayerData.IsOnbu)
				{
					player.PlayerController.ChangeState("Onbu");
				}
				else
				{
					value.BehaviorResources.ChangeMode(Desire.ActionType.Date);
					value.Mode = Desire.ActionType.Date;
					player.Mode = Desire.ActionType.Date;
					player.PlayerController.ChangeState("Normal");
				}
			}
			else
			{
				player.PlayerController.ChangeState("Normal");
			}
		}
		Game.PrevPlayerStateFromCharaCreate = null;
		_enabledCommandArea = player.PlayerController.CommandArea.enabled;
		player.PlayerController.CommandArea.enabled = false;
		player.PlayerController.CommandArea.InitCommandStates();
		player.PlayerController.CommandArea.RefreshCommands();
		if (Singleton<Game>.Instance.Environment.TutorialProgress == 0)
		{
			FadeItem blackoutPanel = MapUIContainer.FadeCanvas.GetPanel(FadeCanvas.PanelType.Blackout);
			blackoutPanel.CanvasGroup.alpha = 1f;
			((IObservable<TimeInterval<float>>)stream).Subscribe((Action<TimeInterval<float>>)delegate(TimeInterval<float> x)
			{
				Fade(loadingPanel.CanvasGroup, startAlpha, 0f, x.Value);
			}, (System.Action)delegate
			{
				loadingPanel.Stop();
				blackoutPanel = MapUIContainer.FadeCanvas.GetPanel(FadeCanvas.PanelType.Blackout);
				blackoutPanel.CanvasGroup.alpha = 1f;
				OnLoaded();
				blackoutPanel = MapUIContainer.FadeCanvas.GetPanel(FadeCanvas.PanelType.Blackout);
				blackoutPanel.CanvasGroup.alpha = 1f;
			});
			stream.Connect();
		}
		else
		{
			((IObservable<TimeInterval<float>>)stream).Subscribe((Action<TimeInterval<float>>)delegate(TimeInterval<float> x)
			{
				Fade(loadingPanel.CanvasGroup, startAlpha, 0f, x.Value);
			}, (System.Action)delegate
			{
				loadingPanel.Stop();
				OnLoaded();
			});
			stream.Connect();
		}
	}

	private void Fade(FadeItem panel, float start, float destination, float value)
	{
		panel.CanvasGroup.alpha = Mathf.Lerp(start, destination, value);
	}

	private void Fade(CanvasGroup canvasGroup, float start, float end, float value)
	{
		canvasGroup.alpha = Mathf.Lerp(start, end, value);
	}

	private void OnLoaded()
	{
		Debug.Log(string.Format("ーーーーー{0}ーーーーー", "OnLoaded"));
		Singleton<Manager.Scene>.Instance.loadingPanel.gameObject.SetActive(value: false);
		Singleton<Manager.Map>.Instance.StartSubscribe();
		Singleton<AnimalManager>.Instance.StartSubscribe();
		int tutorialProgress = Manager.Map.GetTutorialProgress();
		if (tutorialProgress != 0)
		{
			Singleton<SoundPlayer>.Instance.StartAllSubscribe();
			Singleton<SoundPlayer>.Instance.PlayActiveAll = true;
		}
		Singleton<Manager.Map>.Instance.Player.PlayerController.CommandArea.enabled = _enabledCommandArea;
		Manager.Map map = Singleton<Manager.Map>.Instance;
		MapUIContainer.SetMarkerTargetCameraTransform(map.Player.CameraControl.transform);
		Singleton<Manager.Input>.Instance.ReserveState(Manager.Input.ValidType.Action);
		Singleton<Manager.Input>.Instance.SetupState();
		bool tutorialMode = Manager.Map.TutorialMode;
		map.InitSearchActorTargetsAll();
		if (map.TutorialAgent != null)
		{
			map.TutorialAgent.ChangeFirstTutorialBehavior();
		}
		if (tutorialMode)
		{
			if (tutorialProgress < 14)
			{
				map.CreateTutorialLockArea();
				Singleton<MapUIContainer>.Instance.MinimapUI.MinimapLockAreaInit();
			}
			map.SetActiveMapEffect(active: false);
			if (tutorialProgress == 3)
			{
				map.CreateTutorialSearchPoint();
			}
		}
		if (!Singleton<Game>.Instance.WorldData.Cleared)
		{
		}
		if (Singleton<Game>.Instance.Environment.TutorialProgress == 0)
		{
			PlayerActor player = map.Player;
			player.PlayerController.ChangeState("OpeningWakeUp");
		}
		IsLoading = false;
		if (!tutorialMode)
		{
			map.Simulator.EnabledTimeProgression = true;
		}
		IObservable<long> source = from _ in Observable.EveryUpdate().TakeUntilDestroy(base.gameObject)
			where base.isActiveAndEnabled
			select _;
		source.Select((long _) => MapUIContainer.AnyUIActive()).DistinctUntilChanged().Subscribe(delegate(bool isOn)
		{
			IsCursorLock = !isOn;
		});
		IObservable<bool> source2 = source.Select((long _) => IsCursorLock);
		(from isOn in source2.DistinctUntilChanged()
			where !isOn
			select isOn).Subscribe(delegate
		{
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
		});
		source2.Where((bool isOn) => isOn).Subscribe((Action<bool>)delegate
		{
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}, (System.Action)delegate
		{
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
			Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
		});
		IsCursorLock = true;
		InitShortCutEvents();
		Manager.Map.RefreshStoryUI();
		if (Game.IsFreeMode && Game.IsFirstGame)
		{
			SaveProfile();
		}
		(from _ in Observable.Interval(TimeSpan.FromMinutes(10.0)).ObserveOnMainThread().TakeUntilDestroy(this)
			where base.isActiveAndEnabled
			select _).Subscribe(delegate
		{
			if (!Singleton<CraftScene>.IsInstance())
			{
				Debug.Log("progressed 10 minutes");
				SaveProfile(isAuto: true);
				Debug.Log("End Saving");
			}
		});
		Debug.Log(string.Format("ーーーーー{0}ーーーーー", "OnLoaded"));
	}

	private void InitShortCutEvents()
	{
		KeyCodeDownCommand keyCodeDownCommand = new KeyCodeDownCommand();
		keyCodeDownCommand.KeyCode = KeyCode.Escape;
		KeyCodeDownCommand keyCodeDownCommand2 = keyCodeDownCommand;
		keyCodeDownCommand2.TriggerEvent.AddListener(delegate
		{
			if (!UnityEngine.Input.GetKey(KeyCode.F2) && !UnityEngine.Input.GetKey(KeyCode.F3))
			{
				GameUtil.GameEnd();
			}
		});
		_systemCommands.Add(keyCodeDownCommand2);
		keyCodeDownCommand = new KeyCodeDownCommand();
		keyCodeDownCommand.KeyCode = KeyCode.Mouse2;
		KeyCodeDownCommand keyCodeDownCommand3 = keyCodeDownCommand;
		keyCodeDownCommand3.TriggerEvent.AddListener(delegate
		{
			if (!Singleton<Manager.Map>.Instance.IsWarpProc && !UnityEngine.Input.GetKey(KeyCode.Mouse1))
			{
				PlayerActor player = Singleton<Manager.Map>.Instance.Player;
				PlayerController playerController = player.PlayerController;
				if (playerController.State is Normal || playerController.State is Houchi)
				{
					playerController.ChangeState("Menu");
				}
			}
		});
		_systemCommands.Add(keyCodeDownCommand3);
		keyCodeDownCommand = new KeyCodeDownCommand();
		keyCodeDownCommand.KeyCode = KeyCode.M;
		KeyCodeDownCommand keyCodeDownCommand4 = keyCodeDownCommand;
		keyCodeDownCommand4.TriggerEvent.AddListener(delegate
		{
			if (!MapUIContainer.FadeCanvas.IsFading && !Singleton<Manager.Map>.Instance.IsWarpProc)
			{
				PlayerActor player = Singleton<Manager.Map>.Instance.Player;
				IState state = player.PlayerController.State;
				if (state is Normal || state is Houchi)
				{
					if (Singleton<MapUIContainer>.Instance.MinimapUI.VisibleMode == 0)
					{
						player.Controller.ChangeState("WMap");
					}
					else if (Singleton<MapUIContainer>.Instance.MinimapUI.VisibleMode == 2)
					{
						if (Config.GameData.MiniMap)
						{
							Singleton<MapUIContainer>.Instance.MinimapUI.OpenMiniMap();
						}
						else
						{
							player.Controller.ChangeState("WMap");
						}
					}
				}
			}
		});
		_systemCommands.Add(keyCodeDownCommand4);
		keyCodeDownCommand = new KeyCodeDownCommand();
		keyCodeDownCommand.KeyCode = KeyCode.Space;
		KeyCodeDownCommand keyCodeDownCommand5 = keyCodeDownCommand;
		keyCodeDownCommand5.TriggerEvent.AddListener(delegate
		{
			if (!Manager.Map.TutorialMode && !Singleton<Manager.Map>.Instance.IsWarpProc)
			{
				PlayerActor player = Singleton<Manager.Map>.Instance.Player;
				IState state = player.PlayerController.State;
				if (state is Normal || state is Houchi || state is Onbu)
				{
					player.CallProc();
				}
			}
		});
		_systemCommands.Add(keyCodeDownCommand5);
		keyCodeDownCommand = new KeyCodeDownCommand();
		keyCodeDownCommand.KeyCode = KeyCode.F1;
		KeyCodeDownCommand keyCodeDownCommand6 = keyCodeDownCommand;
		keyCodeDownCommand6.TriggerEvent.AddListener(delegate
		{
			if (!Singleton<Manager.Map>.Instance.IsWarpProc && !UnityEngine.Input.GetKey(KeyCode.Mouse1))
			{
				PlayerActor player = Singleton<Manager.Map>.Instance.Player;
				IState state = player.PlayerController.State;
				if (state is Normal || state is Houchi)
				{
					player.Controller.ChangeState("Menu");
				}
			}
		});
		_systemCommands.Add(keyCodeDownCommand6);
		keyCodeDownCommand = new KeyCodeDownCommand();
		keyCodeDownCommand.KeyCode = KeyCode.F2;
		KeyCodeDownCommand keyCodeDownCommand7 = keyCodeDownCommand;
		keyCodeDownCommand7.TriggerEvent.AddListener(delegate
		{
			if (!UnityEngine.Input.GetKey(KeyCode.Escape) && !UnityEngine.Input.GetKey(KeyCode.F3) && !Singleton<Manager.Map>.Instance.IsWarpProc)
			{
				PlayerActor player = Manager.Map.GetPlayer();
				IState state = player?.PlayerController.State;
				if (!player.CameraControl.CinemachineBrain.IsBlending)
				{
					int index = 0;
					System.Action closedEvent = null;
					if (state is Normal || state is Houchi || state is Onbu || state is Communication)
					{
						Singleton<Game>.Instance.LoadShortcut(index, closedEvent);
					}
					else if (state is Fishing)
					{
						index = 3;
						float timeScale = Time.timeScale;
						Time.timeScale = 0f;
						closedEvent = delegate
						{
							Time.timeScale = timeScale;
						};
						Singleton<Game>.Instance.LoadShortcut(index, closedEvent);
					}
					else if (state is Photo)
					{
						index = 5;
						float timeScale2 = Time.timeScale;
						closedEvent = delegate
						{
							Time.timeScale = timeScale2;
						};
						Singleton<Game>.Instance.LoadShortcut(index, closedEvent);
					}
					else if (state is Sex)
					{
						index = 2;
						float timeScale3 = Time.timeScale;
						Time.timeScale = 0f;
						closedEvent = delegate
						{
							Time.timeScale = timeScale3;
						};
						Singleton<Game>.Instance.LoadShortcut(index, closedEvent);
					}
				}
			}
		});
		_systemCommands.Add(keyCodeDownCommand7);
		keyCodeDownCommand = new KeyCodeDownCommand();
		keyCodeDownCommand.KeyCode = KeyCode.F3;
		KeyCodeDownCommand keyCodeDownCommand8 = keyCodeDownCommand;
		keyCodeDownCommand8.TriggerEvent.AddListener(delegate
		{
			if (!Singleton<Manager.Map>.Instance.IsWarpProc)
			{
				PlayerActor player = Singleton<Manager.Map>.Instance.Player;
				IState state = player.PlayerController.State;
				if (state is Normal || state is Houchi || state is Onbu)
				{
					if (_charasEntry == null || _charasEntry.Length != Config.GraphicData.CharasEntry.Length)
					{
						_charasEntry = new bool[Config.GraphicData.CharasEntry.Length];
					}
					for (int i = 0; i < _charasEntry.Length; i++)
					{
						_charasEntry[i] = Config.GraphicData.CharasEntry[i];
					}
					ConfigWindow.UnLoadAction = delegate
					{
						if (!EqualsSequence(_charasEntry, Config.GraphicData.CharasEntry))
						{
							player.PlayerController.ChangeState("Idle");
							Singleton<Manager.Map>.Instance.ApplyConfig(null, delegate
							{
								player.PlayerController.ChangeState("Normal");
							});
						}
					};
					ConfigWindow.TitleChangeAction = delegate
					{
						ConfigWindow.UnLoadAction = null;
						Singleton<Game>.Instance.Dialog.TimeScale = 1f;
					};
					Singleton<Game>.Instance.LoadConfig();
				}
			}
		});
		_systemCommands.Add(keyCodeDownCommand8);
		keyCodeDownCommand = new KeyCodeDownCommand();
		keyCodeDownCommand.KeyCode = KeyCode.F11;
		KeyCodeDownCommand keyCodeDownCommand9 = keyCodeDownCommand;
		keyCodeDownCommand9.TriggerEvent.AddListener(delegate
		{
			CaptureSS();
		});
		_systemCommands.Add(keyCodeDownCommand9);
		keyCodeDownCommand = new KeyCodeDownCommand();
		keyCodeDownCommand.KeyCode = KeyCode.K;
		KeyCodeDownCommand keyCodeDownCommand10 = keyCodeDownCommand;
		keyCodeDownCommand10.TriggerEvent.AddListener(delegate
		{
			Config.GraphicData.FaceLight = !Config.GraphicData.FaceLight;
		});
		_systemCommands.Add(keyCodeDownCommand10);
		keyCodeDownCommand = new KeyCodeDownCommand();
		keyCodeDownCommand.KeyCode = KeyCode.L;
		KeyCodeDownCommand keyCodeDownCommand11 = keyCodeDownCommand;
		keyCodeDownCommand11.TriggerEvent.AddListener(delegate
		{
			Config.GraphicData.AmbientLight = !Config.GraphicData.AmbientLight;
		});
		_systemCommands.Add(keyCodeDownCommand11);
	}

	private IEnumerator Load()
	{
		Debug.Log(string.Format("ーーーーー{0}ーーーーー", "Load"));
		Game gameMgr = Singleton<Game>.Instance;
		WorldData worldData = Singleton<Game>.Instance.WorldData;
		bool isFreeMode = worldData.FreeMode;
		bool isFirstGame = Game.IsFirstGame;
		if (Singleton<MapUIContainer>.IsInstance())
		{
			StorySupportUI storySupportUI = MapUIContainer.StorySupportUI;
			if (isFreeMode)
			{
				worldData.Cleared = true;
				if (worldData.Environment.TutorialProgress <= 28)
				{
					worldData.Environment.TutorialProgress = 29;
				}
			}
			else if (worldData.Environment.TutorialProgress == 28)
			{
				worldData.Environment.TutorialProgress = 29;
			}
			if (worldData.Environment.TutorialProgress == 29 && worldData.MapID != 0)
			{
				worldData.Environment.TutorialProgress = 30;
			}
			storySupportUI.SetIndexClose(gameMgr.WorldData.Environment.TutorialProgress);
		}
		Manager.Map mapManager = Singleton<Manager.Map>.Instance;
		mapManager.MapRoot = _mapRoot;
		mapManager.NavMeshSurface = _navMeshSurface;
		mapManager.InitializeDefaultState();
		yield return mapManager.LoadMap(worldData.MapID);
		if (isFreeMode)
		{
			for (int i = -1; i <= 2; i++)
			{
				mapManager.SetBaseOpenState(0, i, openState: true);
			}
		}
		yield return mapManager.LoadEnvironment();
		yield return mapManager.LoadNavMeshSource();
		yield return mapManager.LoadElements();
		yield return mapManager.LoadMerchantPoint();
		yield return mapManager.LoadEventPoints();
		yield return mapManager.LoadStoryPoints();
		yield return mapManager.LoadHousingObj(worldData.MapID);
		mapManager.SetVanishList();
		mapManager.BuildNavMesh();
		yield return mapManager.LoadAnimalPoint();
		yield return mapManager.SetupPoint();
		AnimalManager animalManager = Singleton<AnimalManager>.Instance;
		yield return animalManager.SetupPointsAsync(mapManager.PointAgent);
		if (Singleton<SoundPlayer>.IsInstance())
		{
			Singleton<SoundPlayer>.Instance.RefreshJukeBoxTable(mapManager);
		}
		WorldData data = gameMgr.WorldData;
		if (data.AgentTable.IsNullOrEmpty())
		{
			int agentMax = Singleton<Manager.Resources>.Instance.DefinePack.MapDefines.AgentMax;
			_ = Singleton<Manager.Resources>.Instance.DefinePack.MapDefines.AgentDefaultNum;
			for (int j = 0; j < agentMax; j++)
			{
				AgentData agentData = new AgentData();
				data.AgentTable[j] = agentData;
				AgentData agentData2 = agentData;
				agentData2.OpenState = j < 1;
				agentData2.PlayEnterScene = j < 1;
			}
		}
		yield return null;
		AIProject.SaveData.SaveData profile = gameMgr.Data;
		mapManager.ActorRoot = _actorRoot;
		bool existsBackup = gameMgr.ExistsBackup(worldData.WorldID);
		yield return mapManager.ApplyProfile(gameMgr.WorldData, existsBackup);
		mapManager.InitActionPoints();
		AQUAS_Reflection[] reflections = mapManager.WaterRefrections;
		if (reflections != null)
		{
			AQUAS_Reflection[] array = reflections;
			foreach (AQUAS_Reflection aQUAS_Reflection in array)
			{
				aQUAS_Reflection.enabled = false;
			}
		}
		mapManager.InitCommandable();
		mapManager.AddAppendTargetPoints(Singleton<Manager.Housing>.Instance.ActionPoints);
		mapManager.AddRuntimeFarmPoints(Singleton<Manager.Housing>.Instance.FarmPoints);
		mapManager.AddPetHomePoints(Singleton<Manager.Housing>.Instance.PetHomePoints);
		mapManager.AddJukePoints(Singleton<Manager.Housing>.Instance.JukePoints);
		mapManager.AddRuntimeCraftPoints(Singleton<Manager.Housing>.Instance.CraftPoints);
		mapManager.AddRuntimeLightSwitchPoints(Singleton<Manager.Housing>.Instance.LightSwitchPoints);
		mapManager.AddAppendHPoints(Singleton<Manager.Housing>.Instance.HPoints);
		ActionPoint[] actionPoints = Singleton<Manager.Housing>.Instance.ActionPoints;
		foreach (ActionPoint actionPoint in actionPoints)
		{
			OffMeshLink[] componentsInChildren = actionPoint.GetComponentsInChildren<OffMeshLink>();
			OffMeshLink[] array2 = componentsInChildren;
			foreach (OffMeshLink offMeshLink in array2)
			{
				offMeshLink.UpdatePositions();
			}
		}
		mapManager.LoadAgentTargetActionPoint();
		mapManager.LoadAgentTargetActor();
		mapManager.PointAgent.CreateHousingWaypoint();
		yield return mapManager.LoadAnimals(gameMgr.WorldData, existsBackup);
		mapManager.RefreshAreaOpenLinkedObject();
		mapManager.RefreshTimeOpenLinkedObject();
		SoundData[] sounds = Config.SoundData.Sounds;
		for (int n = 0; n < sounds.Length; n++)
		{
			sounds[n]?.Refresh();
		}
		yield return mapManager.LoadEnviroObject();
		int mapID = mapManager.MapID;
		int? num = mapManager.Player.MapArea?.AreaID;
		mapManager.ResettingEnviroAreaElement(mapID, num.HasValue ? num.Value : 0);
		int mapID2 = mapManager.MapID;
		int? num2 = mapManager.Player.MapArea?.AreaID;
		mapManager.RefreshHousingEnv3DSEPoints(mapID2, num2.HasValue ? num2.Value : 0);
		yield return new WaitForSecondsRealtime(1f);
		WaitForSeconds waitTime = new WaitForSeconds(0.5f);
		while (!WaitCompletionAgents())
		{
			yield return waitTime;
		}
		foreach (AgentActor value in Singleton<Manager.Map>.Instance.AgentTable.Values)
		{
			value.ActivateNavMeshAgent();
		}
		yield return null;
		foreach (AgentActor agent in Singleton<Manager.Map>.Instance.AgentTable.Values)
		{
			if (agent.AgentData.ReservedWaypointIDList.IsNullOrEmpty())
			{
				yield return agent.CalculateWaypoints();
				continue;
			}
			agent.LoadWaypoints();
			yield return null;
		}
		if (!mapManager.IsHour7After)
		{
			foreach (AgentActor value2 in mapManager.AgentTable.Values)
			{
				value2.SetAdvEventLimitationResetReserve();
			}
		}
		mapManager.Player.ActivateNavMeshAgent();
		mapManager.Merchant.ActivateNavMeshAgent();
		mapManager.Merchant.FirstAppear();
		Debug.Log(string.Format("ーーーーーStart→{0}ーーーーー", "InitializeMinimap"));
		MapUIContainer.InitializeMinimap();
		Debug.Log(string.Format("ーーーーーEnd→{0}ーーーーー", "InitializeMinimap"));
		animalManager.StartAllAnimalCreate();
		yield return Singleton<Manager.Resources>.Instance.HSceneTable.LoadHObj();
		if (Singleton<AnimalManager>.IsInstance())
		{
			Singleton<AnimalManager>.Instance.ActiveMapScene = true;
		}
		GC.Collect();
		Debug.Log(string.Format("ーーーーー{0}ーーーーー", "Load"));
		isLoadEnd = true;
	}

	private bool WaitCompletionAgents()
	{
		foreach (AgentActor value in Singleton<Manager.Map>.Instance.AgentTable.Values)
		{
			if (!value.IsInit)
			{
				return false;
			}
		}
		return true;
	}

	public void SaveProfile(bool isAuto = false)
	{
		if (!Singleton<Game>.IsInstance())
		{
			return;
		}
		string worldSaveDataFile = Path.WorldSaveDataFile;
		WorldData worldData = Singleton<Game>.Instance.WorldData;
		worldData.SaveTime = DateTime.Now;
		worldData.SaveTimeString = worldData.SaveTime.ToString("o");
		worldData.Environment.SetSimulation(Singleton<Manager.Map>.Instance.Simulator);
		PlayerActor player = Singleton<Manager.Map>.Instance.Player;
		worldData.PlayerData.IsOnbu = player.PlayerController.State != null && player.PlayerController.PrevStateName == "Onbu";
		if (player.AgentPartner != null)
		{
			worldData.PlayerData.PartnerID = player.AgentPartner.ID;
		}
		else
		{
			worldData.PlayerData.PartnerID = -1;
		}
		foreach (AgentActor value2 in Singleton<Manager.Map>.Instance.AgentTable.Values)
		{
			value2.ChaControl.chaFile.SaveCharaFile(value2.ChaControl.chaFile.charaFileName);
			if (value2.CurrentPoint != null)
			{
				value2.AgentData.CurrentActionPointID = value2.CurrentPoint.RegisterID;
			}
			else
			{
				value2.AgentData.CurrentActionPointID = -1;
			}
			if (value2.TargetInSightActor != null)
			{
				value2.AgentData.ActionTargetID = value2.TargetInSightActor.ID;
			}
			else
			{
				value2.AgentData.ActionTargetID = -1;
			}
			value2.AgentData.ScheduleEnabled = value2.Schedule.enabled;
			value2.AgentData.ScheduleElapsedTime = value2.Schedule.elapsedTime;
			value2.AgentData.ScheduleDuration = value2.Schedule.duration;
			value2.AgentData.ReservedWaypointIDList.Clear();
			Waypoint[] reservedWaypoints = value2.GetReservedWaypoints();
			foreach (Waypoint waypoint in reservedWaypoints)
			{
				value2.AgentData.ReservedWaypointIDList.Add(waypoint.ID);
			}
			value2.AgentData.WalkRouteWaypointIDList.Clear();
			foreach (Waypoint item in value2.WalkRoute)
			{
				if (!(item == null))
				{
					value2.AgentData.WalkRouteWaypointIDList.Add(item.ID);
				}
			}
		}
		if (Singleton<AnimalManager>.IsInstance())
		{
			Singleton<AnimalManager>.Instance.SetupSaveDataWildAnimals();
		}
		AIProject.SaveData.SaveData data = Singleton<Game>.Instance.Data;
		WorldData value;
		if (isAuto)
		{
			if (data.AutoData == null)
			{
				data.AutoData = new WorldData();
			}
			data.AutoData.Copy(worldData);
		}
		else if (data.WorldList.TryGetValue(worldData.WorldID, out value))
		{
			value.Copy(worldData);
		}
		else
		{
			Debug.LogWarning($"ワールドID = [ {worldData.WorldID} ] のデータがなかったので作成");
			WorldData worldData2 = new WorldData();
			data.WorldList[worldData.WorldID] = worldData2;
			value = worldData2;
			value.Copy(worldData);
		}
		Singleton<Game>.Instance.SaveProfile(worldSaveDataFile);
		Debug.Log("保存完了: MapScene side");
		GC.Collect();
	}

	private void CaptureSS()
	{
		Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.Photo);
		Singleton<Manager.Map>.Instance.Player.CameraControl.ScreenShot.Capture(string.Empty);
	}

	protected override void Awake()
	{
		if (CheckInstance())
		{
			base.Awake();
		}
	}

	private IEnumerator Start()
	{
		Debug.Log(string.Format("Begin {0}", "MapScene"));
		Manager.Resources rm = Singleton<Manager.Resources>.Instance;
		QualitySettings.shadowDistance = rm.DefinePack.MapDefines.ShadowDistance;
		Singleton<Manager.Scene>.Instance.loadingPanel.gameObject.SetActive(value: true);
		Singleton<Manager.Scene>.Instance.loadingPanel.CanvasGroup.alpha = 1f;
		Singleton<Manager.Scene>.Instance.loadingPanel.Play();
		Singleton<Manager.Map>.Instance.PlayerStartPoint = _startPositionPoint;
		IsLoading = true;
		rm.SetupMap();
		DefinePack definePack = Singleton<Manager.Resources>.Instance.DefinePack;
		GameObject uiSource = CommonLib.LoadAsset<GameObject>(definePack.ABPaths.MapScenePrefabAdd07, "MapUI", clone: false, definePack.ABManifests.Add07);
		GameObject uiInstance = UnityEngine.Object.Instantiate(uiSource);
		uiInstance.transform.SetParent(base.transform, worldPositionStays: false);
		IConnectableObservable<TimeInterval<float>> fadeOutProgression = ObservableEasing.EaseOutQuint(1f, ignoreTimeScale: true).FrameTimeInterval(ignoreTimeScale: true).Publish();
		yield return rm.LoadMapResourceStream.ToYieldInstruction();
		IConnectableObservable<Unit> stream = Observable.FromCoroutine(() => Load()).Publish();
		stream.Connect();
		Singleton<Manager.Input>.Instance.SystemElements.RemoveAll((ISystemCommand x) => x is MapScene);
		Singleton<Manager.Input>.Instance.SystemElements.Add(this);
		Singleton<Manager.Input>.Instance.ActionElements.RemoveAll((IActionCommand x) => x is CommandLabel);
		Singleton<Manager.Input>.Instance.ActionElements.Add(MapUIContainer.CommandLabel);
		fadeOutProgression.Subscribe(delegate
		{
		}, delegate
		{
		}, delegate
		{
			EnabledInput = true;
			MapUIContainer.CommandLabel.EnabledInput = true;
			Singleton<Manager.Input>.Instance.ActionElements.Add(Singleton<Manager.Map>.Instance.Player.CameraControl);
			ActorCameraControl cameraControl = Singleton<Manager.Map>.Instance.Player.CameraControl;
			cameraControl.EnabledInput = true;
			Singleton<Manager.Sound>.Instance.Listener = cameraControl.transform;
		});
		Observable.WhenAll(stream).Subscribe(delegate
		{
		}, delegate(Exception ex)
		{
			Debug.LogException(ex);
		}, delegate
		{
			if (Singleton<Game>.Instance.Environment.TutorialProgress != 0)
			{
				Singleton<SoundPlayer>.Instance.PlayMapBGM();
				Singleton<SoundPlayer>.Instance.PlayWideEnvSE(_quickChange: true);
				FadeItem panel = MapUIContainer.FadeCanvas.GetPanel(FadeCanvas.PanelType.Blackout);
				panel.CanvasGroup.alpha = 0f;
			}
			StartFadingStream(fadeOutProgression);
		});
		Singleton<MapUIContainer>.Instance.advScene.Scenario.TextLog += delegate(string name, string text, IReadOnlyCollection<TextScenario.IVoice[]> voices)
		{
			MapUIContainer.AddLog(name, text, voices);
		};
		Singleton<MapUIContainer>.Instance.advScene.Scenario.VisibleLog += delegate
		{
			MapUIContainer.GameLogUI.IsActiveControl = true;
			MapUIContainer.GameLogUI.OnClosed = delegate
			{
				Singleton<MapUIContainer>.Instance.advScene.Scenario.captionSystem.Visible = true;
			};
			Singleton<MapUIContainer>.Instance.advScene.Scenario.captionSystem.Visible = false;
		};
	}

	private void Update()
	{
		if (Singleton<Game>.IsInstance())
		{
			Game game = Singleton<Game>.Instance;
			AIProject.SaveData.Environment environment = game.Environment;
			if (environment != null)
			{
				environment.TotalPlayTime = environment.TotalPlayTime.TimeSpan + TimeSpan.FromSeconds(Time.unscaledDeltaTime);
			}
		}
	}

	private void OnDisable()
	{
		if (Singleton<Manager.Input>.IsInstance())
		{
			Singleton<Manager.Input>.Instance.SystemElements.Remove(this);
			if (Singleton<MapUIContainer>.IsInstance())
			{
				Singleton<Manager.Input>.Instance.ActionElements.Remove(MapUIContainer.CommandLabel);
			}
		}
	}

	protected override void OnDestroy()
	{
		if (Singleton<MapScene>.Instance != this)
		{
			return;
		}
		base.OnDestroy();
		if (Singleton<Manager.Scene>.IsInstance())
		{
			if (Singleton<Manager.Map>.IsInstance())
			{
				Singleton<Manager.Map>.Instance.ReleaseComponents();
			}
			if (Singleton<Manager.Resources>.IsInstance())
			{
				Singleton<Manager.Resources>.Instance.ReleaseMapResources();
				Singleton<Manager.Resources>.Instance.EndLoadAssetBundle(forceRemove: true);
			}
			if (Singleton<SoundPlayer>.IsInstance())
			{
				Singleton<SoundPlayer>.Instance.Release();
			}
			if (Singleton<AnimalManager>.IsInstance())
			{
				Singleton<AnimalManager>.Instance.ActiveMapScene = false;
			}
		}
	}

	public void PushResult(int id, string message)
	{
	}
}
