using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using AIChara;
using AIProject;
using AIProject.Animal;
using AIProject.Definitions;
using AIProject.MiniGames.Fishing;
using AIProject.Player;
using AIProject.SaveData;
using AIProject.Scene;
using ConfigScene;
using IllusionUtility.GetUtility;
using PlaceholderSoftware.WetStuff;
using ReMotion;
using Sound;
using UniRx;
using UnityEngine;
using UnityEngine.AI;
using UnityEx;
using UnityEx.Misc;

namespace Manager;

public class Map : Singleton<Map>
{
	[Serializable]
	public class VisibleObject
	{
		public string nameCollider = string.Empty;

		public Collider collider;

		public float delay;

		public bool isVisible = true;

		public GameObject vanishObj;
	}

	private Dictionary<int, Actor> _actorTable = new Dictionary<int, Actor>();

	private Dictionary<int, AgentActor> _agentTable = new Dictionary<int, AgentActor>();

	private List<int> _agentKeys = new List<int>();

	private Transform _enviroGroupRoot;

	[SerializeField]
	private Transform _mapRoot;

	[SerializeField]
	private GameObject _waterObject;

	[SerializeField]
	private PointManager _pointAgent;

	private GameObject _mapDataEffect;

	private List<int> _agentVanishAreaList;

	private GameObject _agentPrefab;

	private GameObject _tutorialSearchPointObject;

	private List<int> _removeRegIDCache = new List<int>();

	private List<ICommandable> _appendCommandables = new List<ICommandable>();

	private List<VisibleObject> lstMapVanish = new List<VisibleObject>();

	public EnvironmentSimulator Simulator { get; private set; }

	public ReadOnlyDictionary<int, Actor> ActorTable { get; set; }

	public List<Actor> Actors { get; private set; } = new List<Actor>();


	public PlayerActor Player { get; private set; }

	public ReadOnlyDictionary<int, AgentActor> AgentTable { get; private set; }

	public MerchantActor Merchant { get; private set; }

	public AgentActor TutorialAgent { get; set; }

	public static bool TutorialMode
	{
		get
		{
			int tutorialProgress = GetTutorialProgress();
			return tutorialProgress < 16;
		}
	}

	public Dictionary<int, UnityEx.ValueTuple<GameObject, EnvArea3DSE, EnvLineArea3DSE>> EnviroSETable { get; private set; }

	public Dictionary<int, System.Tuple<Transform, AudioReverbZone[]>> ReverbTable { get; private set; }

	public Transform EnviroGroupRoot
	{
		get
		{
			if (_enviroGroupRoot != null)
			{
				return _enviroGroupRoot;
			}
			_enviroGroupRoot = new GameObject("Enviro Group Root").transform;
			_enviroGroupRoot.SetParent(_mapRoot, worldPositionStays: false);
			return _enviroGroupRoot;
		}
	}

	public Dictionary<int, Transform> EnviroRootElement { get; private set; }

	public Dictionary<int, UnityEx.ValueTuple<bool, List<Env3DSEPoint>>> HousingEnvSEPointTable { get; } = new Dictionary<int, UnityEx.ValueTuple<bool, List<Env3DSEPoint>>>();


	public Dictionary<int, Dictionary<bool, ForcedHideObject[]>> AreaOpenObjectTable { get; private set; } = new Dictionary<int, Dictionary<bool, ForcedHideObject[]>>();


	public Dictionary<int, Dictionary<int, Dictionary<bool, Dictionary<int, UnityEx.ValueTuple<GameObject, Material, float, Color>[]>>>> TimeRelationObjectTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<bool, Dictionary<int, UnityEx.ValueTuple<GameObject, Material, float, Color>[]>>>>();


	public EnvironmentProfile EnvironmentProfile
	{
		[CompilerGenerated]
		get
		{
			return Simulator?.EnvironmentProfile;
		}
	}

	public Transform MapRoot
	{
		get
		{
			return _mapRoot;
		}
		set
		{
			_mapRoot = value;
			_pointAgent = value.GetComponentInChildren<PointManager>(includeInactive: true);
		}
	}

	public GameObject WaterObject
	{
		get
		{
			return _waterObject;
		}
		set
		{
			_waterObject = value;
		}
	}

	public AQUAS_Reflection[] WaterRefrections { get; set; }

	public Transform PlayerStartPoint { get; set; }

	public Transform ActorRoot { get; set; }

	public NavMeshSurface NavMeshSurface { get; set; }

	public GameObject NavMeshSrc { get; set; }

	public GameObject ChunkSrc { get; set; }

	public PointManager PointAgent
	{
		[CompilerGenerated]
		get
		{
			return _pointAgent;
		}
	}

	public int MapID { get; private set; }

	public int AccessDeviceID { get; set; } = -1;


	public Dictionary<int, Dictionary<int, Transform>> EventStartPointDic { get; set; } = new Dictionary<int, Dictionary<int, Transform>>();


	public Dictionary<int, string> ChangedCharaFiles { get; set; } = new Dictionary<int, string>();


	public Dictionary<int, Transform> HousingPointTable { get; set; } = new Dictionary<int, Transform>();


	public int HousingID { get; set; }

	public int HousingAreaID { get; set; }

	public Dictionary<int, List<Transform>> HousingRecoveryPointTable { get; set; } = new Dictionary<int, List<Transform>>();


	public Dictionary<int, Dictionary<int, List<WarpPoint>>> WarpPointDic { get; set; } = new Dictionary<int, Dictionary<int, List<WarpPoint>>>();


	public FishingManager FishingSystem { get; set; }

	public Dictionary<int, Chunk> ChunkTable { get; } = new Dictionary<int, Chunk>();


	public Dictionary<int, GameObject> MapGroupObjList { get; private set; } = new Dictionary<int, GameObject>();


	public Dictionary<int, UnityEx.ValueTuple<Desire.ActionType, Desire.ActionType, bool>> AgentModeCache { get; set; } = new Dictionary<int, UnityEx.ValueTuple<Desire.ActionType, Desire.ActionType, bool>>();


	public bool IsHour7After
	{
		[CompilerGenerated]
		get
		{
			return Simulator.Now.Hour >= 7;
		}
	}

	public List<TimeLinkedLightObject> TimeLinkedLightObjectList { get; private set; } = new List<TimeLinkedLightObject>();


	public bool UpdateTexSetting { get; set; } = true;


	public GameObject DemoLightObject { get; private set; }

	public GameObject TutorialLockAreaObject { get; private set; }

	public StoryPointEffect StoryPointEffect { get; private set; }

	public bool IsWarpProc { get; private set; }

	public List<VisibleObject> LstMapVanish
	{
		[CompilerGenerated]
		get
		{
			return lstMapVanish;
		}
	}

	protected override void Awake()
	{
		if (CheckInstance())
		{
			ActorTable = new ReadOnlyDictionary<int, Actor>(_actorTable);
			AgentTable = new ReadOnlyDictionary<int, AgentActor>(_agentTable);
		}
	}

	private void Start()
	{
	}

	private void FixedUpdate()
	{
		LayerMask areaDetectionLayer = Singleton<Resources>.Instance.DefinePack.MapDefines.AreaDetectionLayer;
		foreach (Actor actor in Actors)
		{
			if (Physics.Raycast(actor.Position + Vector3.up * 5f, Vector3.down, out var hitInfo, 1000f, areaDetectionLayer))
			{
				bool flag = false;
				foreach (KeyValuePair<int, Chunk> item in ChunkTable)
				{
					MapArea[] mapAreas = item.Value.MapAreas;
					foreach (MapArea mapArea in mapAreas)
					{
						if (flag = mapArea.ContainsCollider(hitInfo.collider))
						{
							actor.MapArea = mapArea;
							actor.ChunkID = mapArea.ChunkID;
							actor.AreaID = mapArea.AreaID;
							break;
						}
					}
					if (flag)
					{
						break;
					}
				}
				if (!flag)
				{
					actor.MapArea = null;
				}
			}
			else
			{
				actor.MapArea = null;
			}
		}
		if (Player != null)
		{
			AreaTypeUpdate(Player);
			if (Player.MapArea != null)
			{
				Player.PlayerData.AreaID = Player.MapArea.AreaID;
			}
		}
		if (Merchant != null)
		{
			AreaTypeUpdate(Merchant);
		}
		if (_agentTable.IsNullOrEmpty())
		{
			return;
		}
		foreach (KeyValuePair<int, AgentActor> item2 in _agentTable)
		{
			if (!(item2.Value == null))
			{
				AreaTypeUpdate(item2.Value);
				if (!(item2.Value.MapArea == null))
				{
					item2.Value.AgentData.AreaID = item2.Value.MapArea.AreaID;
				}
			}
		}
	}

	private void Update()
	{
		GraphicSystem graphicData = Config.GraphicData;
		if (graphicData != null)
		{
			bool selfShadow = graphicData.SelfShadow;
			int qualityLevel = QualitySettings.GetQualityLevel();
			int num = qualityLevel / 2 * 2 + ((!selfShadow) ? 1 : 0);
			if (num != qualityLevel)
			{
				QualitySettings.SetQualityLevel(num);
			}
			if (UpdateTexSetting)
			{
				byte mapGraphicQuality = graphicData.MapGraphicQuality;
				if (QualitySettings.masterTextureLimit != mapGraphicQuality)
				{
					QualitySettings.masterTextureLimit = mapGraphicQuality;
				}
			}
		}
		PlayerActor player = Player;
		if (player == null)
		{
			return;
		}
		if (Simulator != null)
		{
			DecalSettings settings = player.CameraControl.WetDecal.Settings;
			float currentVelocity = 0f;
			switch (Simulator.Weather)
			{
			case Weather.Rain:
				settings.Saturation = Mathf.SmoothDamp(settings.Saturation, 1f, ref currentVelocity, 0.5f);
				break;
			case Weather.Storm:
				settings.Saturation = Mathf.SmoothDamp(settings.Saturation, 1f, ref currentVelocity, 0.3f);
				break;
			default:
				settings.Saturation = Mathf.SmoothDamp(settings.Saturation, 0f, ref currentVelocity, 1f);
				break;
			}
		}
		if (Singleton<Game>.IsInstance() && Singleton<Game>.Instance.Environment != null)
		{
			foreach (KeyValuePair<int, List<AIProject.SaveData.Environment.PlantInfo>> item in Singleton<Game>.Instance.Environment.FarmlandTable)
			{
				foreach (AIProject.SaveData.Environment.PlantInfo item2 in item.Value)
				{
					item2?.AddTimer(Time.unscaledDeltaTime);
				}
			}
		}
		if (Singleton<Game>.IsInstance() && Singleton<Game>.Instance.Environment != null)
		{
			Dictionary<int, RecyclingData> recyclingDataTable = Singleton<Game>.Instance.Environment.RecyclingDataTable;
			if (!recyclingDataTable.IsNullOrEmpty())
			{
				foreach (KeyValuePair<int, RecyclingData> item3 in recyclingDataTable)
				{
					RecyclingData value = item3.Value;
					if (value != null && value.CreateCountEnabled)
					{
						value.CreateCounter += Time.unscaledDeltaTime;
					}
				}
			}
		}
		if (Singleton<Game>.IsInstance() && Singleton<Game>.Instance.Environment != null)
		{
			Dictionary<int, AIProject.SaveData.Environment.SearchActionInfo> searchActionLockTable = Singleton<Game>.Instance.Environment.SearchActionLockTable;
			foreach (KeyValuePair<int, AIProject.SaveData.Environment.SearchActionInfo> item4 in searchActionLockTable)
			{
				AIProject.SaveData.Environment.SearchActionInfo value2 = item4.Value;
				if (value2.Count != 0)
				{
					value2.ElapsedTime += Time.unscaledDeltaTime;
					if (value2.ElapsedTime > EnvironmentProfile.SearchCoolTimeDuration)
					{
						value2.ElapsedTime = 0f;
						value2.Count = 0;
					}
				}
			}
		}
		float talkLockDuration = Singleton<Resources>.Instance.AgentProfile.TalkLockDuration;
		float effectiveDynamicBoneDistance = Singleton<Resources>.Instance.LocomotionProfile.EffectiveDynamicBoneDistance;
		float charaVisibleDistance = Singleton<Resources>.Instance.LocomotionProfile.CharaVisibleDistance;
		Transform cameraTransform = player.CameraControl.transform;
		foreach (int agentKey in _agentKeys)
		{
			if (!_agentTable.TryGetValue(agentKey, out var value3))
			{
				continue;
			}
			if (value3.AgentData.LockTalk)
			{
				value3.AgentData.TalkElapsedTime += Time.unscaledDeltaTime;
				if (value3.AgentData.TalkElapsedTime > talkLockDuration)
				{
					value3.AgentData.LockTalk = false;
					value3.AgentData.TalkElapsedTime = 0f;
					value3.AgentData.TalkMotivation = value3.AgentData.StatsTable[5];
				}
			}
			value3.AgentData.CallCTCount += Time.unscaledDeltaTime;
			if (value3.AgentData.IsPlayerForBirthdayEvent && !player.IsBirthday(null))
			{
				value3.AgentData.IsPlayerForBirthdayEvent = false;
			}
			Dictionary<int, AIProject.SaveData.Environment.SearchActionInfo> searchActionLockTable2 = value3.AgentData.SearchActionLockTable;
			foreach (KeyValuePair<int, AIProject.SaveData.Environment.SearchActionInfo> item5 in searchActionLockTable2)
			{
				AIProject.SaveData.Environment.SearchActionInfo value4 = item5.Value;
				if (value4.Count != 0)
				{
					value4.ElapsedTime += Time.deltaTime;
					if (value4.ElapsedTime > EnvironmentProfile.SearchCoolTimeDuration)
					{
						value4.ElapsedTime = 0f;
						value4.Count = 0;
					}
				}
			}
			StatusProfile statusProfile = Singleton<Resources>.Instance.StatusProfile;
			if (value3.AgentData.ColdLockInfo.Lock)
			{
				SickLockInfo coldLockInfo = value3.AgentData.ColdLockInfo;
				coldLockInfo.ElapsedTime += Time.deltaTime;
				if (coldLockInfo.ElapsedTime > statusProfile.ColdLockDuration)
				{
					coldLockInfo.Lock = false;
					coldLockInfo.ElapsedTime = 0f;
				}
			}
			if (value3.AgentData.HeatStrokeLockInfo.Lock)
			{
				SickLockInfo heatStrokeLockInfo = value3.AgentData.HeatStrokeLockInfo;
				heatStrokeLockInfo.ElapsedTime += Time.deltaTime;
				if (heatStrokeLockInfo.ElapsedTime > statusProfile.HeatStrokeLockDuration)
				{
					heatStrokeLockInfo.Lock = false;
					heatStrokeLockInfo.ElapsedTime = 0f;
				}
			}
			if (value3.enabled && value3.Controller.enabled && value3.AnimationAgent.enabled)
			{
				if (!_agentVanishAreaList.IsNullOrEmpty())
				{
					value3.Visible = !_agentVanishAreaList.Contains(value3.AreaID);
				}
				else
				{
					value3.Visible = true;
				}
				value3.ChaControl.visibleAll = value3.Visible && value3.IsVisible && value3.IsVisibleDistanceAll(cameraTransform);
				if (!(player.PlayerController.State is Sex))
				{
					bool flag = value3.ChaControl.fileGameInfo.flavorState[2] >= Singleton<Resources>.Instance.StatusProfile.LampEquipableBorder;
					bool flag2 = Simulator.TimeZone == AIProject.TimeZone.Night;
					value3.ChaControl.ShowExtraAccessory(ChaControlDefine.ExtraAccessoryParts.Waist, flag && flag2);
				}
				ItemScrounge itemScrounge = value3.AgentData.ItemScrounge;
				itemScrounge.AddTimer(Time.deltaTime);
				if (itemScrounge.isEnd)
				{
					itemScrounge.Reset();
					FlavorSkill.Type[] source = new FlavorSkill.Type[3]
					{
						FlavorSkill.Type.Reliability,
						FlavorSkill.Type.Sociability,
						FlavorSkill.Type.Reason
					};
					int num2 = (int)source.Shuffle().First();
					value3.AgentData.SetFlavorSkill(num2, value3.ChaControl.fileGameInfo.flavorState[num2] - 20);
					FlavorSkill.Type[] source2 = new FlavorSkill.Type[3]
					{
						FlavorSkill.Type.Darkness,
						FlavorSkill.Type.Wariness,
						FlavorSkill.Type.Instinct
					};
					int num3 = (int)source2.Shuffle().First();
					value3.AgentData.SetFlavorSkill(num3, value3.ChaControl.fileGameInfo.flavorState[num3] + 20);
					int num4 = 1;
					value3.SetStatus(num4, value3.AgentData.StatsTable[num4] - 30f);
					int id = 7;
					value3.SetStatus(id, value3.ChaControl.fileGameInfo.morality - 5);
				}
			}
		}
		MerchantActor merchant = Merchant;
		if (merchant != null && merchant.enabled && merchant.Controller.enabled && merchant.AnimationMerchant.enabled)
		{
			merchant.ChaControl.visibleAll = merchant.IsVisible && merchant.IsVisibleDistanceAll(cameraTransform);
		}
	}

	public IEnumerator LoadMap(int id)
	{
		MapID = id;
		AssetBundleInfo mapInfo = Singleton<Resources>.Instance.Map.MapList[id];
		yield return Singleton<Scene>.Instance.LoadBaseSceneCoroutine(new Scene.Data
		{
			assetBundleName = mapInfo.assetbundle,
			levelName = mapInfo.asset,
			fadeType = Scene.Data.FadeType.None
		});
	}

	public IEnumerator LoadMap(string assetBundleName, string assetName)
	{
		yield return Singleton<Scene>.Instance.LoadBaseSceneCoroutine(new Scene.Data
		{
			assetBundleName = assetBundleName,
			levelName = assetName,
			fadeType = Scene.Data.FadeType.None
		});
	}

	public IEnumerator LoadEnvironment()
	{
		Resources resourceManager = Singleton<Resources>.Instance;
		string mapScenePrefabAdd = resourceManager.DefinePack.ABPaths.MapScenePrefabAdd05;
		string add = resourceManager.DefinePack.ABManifests.Add05;
		GameObject original = CommonLib.LoadAsset<GameObject>(mapScenePrefabAdd, "MapSimulation", clone: false, add);
		MapScene.AddAssetBundlePath(mapScenePrefabAdd, add);
		GameObject gameObject = UnityEngine.Object.Instantiate(original);
		gameObject.transform.SetParent(_mapRoot);
		Simulator = gameObject.GetComponent<EnvironmentSimulator>();
		Simulator.EnabledTimeProgression = false;
		string mapScenePrefab = resourceManager.DefinePack.ABPaths.MapScenePrefab;
		string text = "abdata";
		GameObject original2 = CommonLib.LoadAsset<GameObject>(mapScenePrefab, "EnviroSkyGroup", clone: false, text);
		MapScene.AddAssetBundlePath(mapScenePrefab, text);
		GameObject gameObject2 = UnityEngine.Object.Instantiate(original2, Vector3.zero, Quaternion.identity, Simulator.transform);
		EnviroSky enviroSky = EnviroSky.instance;
		Simulator.EnviroSky = enviroSky;
		Simulator.DefaultEnviroZone = gameObject2.GetComponentInChildren<EnviroZone>(includeInactive: true);
		Simulator.EnviroEvents = gameObject2.GetComponentInChildren<EnviroEvents>(includeInactive: true);
		Simulator.SkyDomeObject = gameObject2;
		Simulator.MapID = MapID;
		Simulator.OnEnableChangeTimeAsObservable().Subscribe(delegate(bool x)
		{
			Simulator.SetTimeProgress(x);
		});
		yield return null;
	}

	public IEnumerator LoadDemoMap()
	{
		DefinePack.AssetBundlePathsDefine define = Singleton<Resources>.Instance.DefinePack.ABPaths;
		yield return Singleton<Scene>.Instance.LoadBaseSceneCoroutine(new Scene.Data
		{
			assetBundleName = "map/scene/demo_data.unity3d",
			levelName = "demo_data",
			fadeType = Scene.Data.FadeType.None
		});
	}

	public IEnumerator LoadDemoEnvironment(string assetName)
	{
		GameObject original = CommonLib.LoadAsset<GameObject>("map/demo_data.unity3d", assetName, clone: false, string.Empty);
		GameObject gameObject2 = (DemoLightObject = UnityEngine.Object.Instantiate(original));
		gameObject2.SetActive(value: true);
		EnviroSky enviroSky = EnviroSky.instance;
		enviroSky.Weather.weatherPresets.Clear();
		enviroSky.Weather.WeatherPrefabs.Clear();
		EnviroZone component = EnviroSky.instance.GetComponent<EnviroZone>();
		component.zoneWeather.Clear();
		component.enabled = false;
		yield return null;
	}

	public IEnumerator LoadNavMeshSource()
	{
		if (!(NavMeshSurface == null))
		{
			AssetBundleInfo assetBundleInfo = Singleton<Resources>.Instance.Map.NavMeshSourceList[MapID];
			GameObject original = CommonLib.LoadAsset<GameObject>(assetBundleInfo.assetbundle, assetBundleInfo.asset, clone: false, assetBundleInfo.manifest);
			NavMeshSrc = UnityEngine.Object.Instantiate(original, NavMeshSurface.transform, worldPositionStays: false);
		}
		yield break;
	}

	public IEnumerator LoadElements()
	{
		DefinePack.AssetBundlePathsDefine define = Singleton<Resources>.Instance.DefinePack.ABPaths;
		PointManager pointAgent = _mapRoot.GetComponentInChildren<PointManager>(includeInactive: true);
		Transform apRoot = new GameObject("ActionPointRoot").transform;
		apRoot.SetParent(pointAgent.transform, worldPositionStays: false);
		pointAgent.ActionPointRoot = apRoot;
		AssetBundleInfo info = Singleton<Resources>.Instance.Map.ActionPointGroupTable[MapID];
		GameObject gameObject = CommonLib.LoadAsset<GameObject>(info.assetbundle, info.asset, clone: false, info.manifest);
		if (gameObject != null)
		{
			MapScene.AddAssetBundlePath(info);
			UnityEngine.Object.Instantiate(gameObject, apRoot, worldPositionStays: true);
		}
		Transform bpRoot = new GameObject("BasePointRoot").transform;
		bpRoot.SetParent(pointAgent.transform);
		pointAgent.BasePointRoot = bpRoot;
		if (Singleton<Resources>.Instance.Map.BasePointGroupTable.TryGetValue(MapID, out var value))
		{
			GameObject gameObject2 = CommonLib.LoadAsset<GameObject>(value.assetbundle, value.asset, clone: false, value.manifest);
			MapScene.AddAssetBundlePath(value);
			if (gameObject2 != null)
			{
				UnityEngine.Object.Instantiate(gameObject2, bpRoot, worldPositionStays: true);
			}
		}
		Transform dpRoot = new GameObject("DevicePointRoot").transform;
		dpRoot.SetParent(pointAgent.transform, worldPositionStays: false);
		pointAgent.DevicePointRoot = dpRoot;
		if (Singleton<Resources>.Instance.Map.DevicePointGroupTable.TryGetValue(MapID, out var value2))
		{
			GameObject gameObject3 = CommonLib.LoadAsset<GameObject>(value2.assetbundle, value2.asset, clone: false, value2.manifest);
			MapScene.AddAssetBundlePath(value2);
			if (gameObject3 != null)
			{
				UnityEngine.Object.Instantiate(gameObject3, dpRoot, worldPositionStays: true);
			}
		}
		Transform hpRoot = new GameObject("FarmPointRoot").transform;
		hpRoot.SetParent(pointAgent.transform, worldPositionStays: false);
		pointAgent.HarvestPointRoot = hpRoot;
		if (Singleton<Resources>.Instance.Map.HarvestPointGroupTable.TryGetValue(MapID, out var value3))
		{
			GameObject gameObject4 = CommonLib.LoadAsset<GameObject>(value3.assetbundle, value3.asset, clone: false, value3.manifest);
			MapScene.AddAssetBundlePath(value3);
			if (gameObject4 != null)
			{
				UnityEngine.Object.Instantiate(gameObject4, hpRoot, worldPositionStays: true);
			}
		}
		Transform shipRoot = new GameObject("ShipPointRoot").transform;
		shipRoot.SetParent(pointAgent.transform, worldPositionStays: false);
		pointAgent.ShipPointRoot = shipRoot;
		if (Singleton<Resources>.Instance.Map.ShipPointGroupTable.TryGetValue(MapID, out var value4))
		{
			GameObject gameObject5 = CommonLib.LoadAsset<GameObject>(value4.assetbundle, value4.asset, clone: false, value4.manifest);
			MapScene.AddAssetBundlePath(value4);
			if (gameObject5 != null)
			{
				UnityEngine.Object.Instantiate(gameObject5, shipRoot, worldPositionStays: true);
			}
		}
		Transform lpRoot = new GameObject("LightSwitchRoot").transform;
		lpRoot.SetParent(pointAgent.transform, worldPositionStays: false);
		pointAgent.LightSwitchPointRoot = lpRoot;
		if (Singleton<Resources>.Instance.Map.LightSwitchPointGroupTable.TryGetValue(MapID, out var value5) && !value5.IsNullOrEmpty())
		{
			foreach (KeyValuePair<int, AssetBundleInfo> item in value5)
			{
				AssetBundleInfo value6 = item.Value;
				GameObject gameObject6 = CommonLib.LoadAsset<GameObject>(value6.assetbundle, value6.asset, clone: false, value6.manifest);
				MapScene.AddAssetBundlePath(value6);
				if (gameObject6 != null)
				{
					UnityEngine.Object.Instantiate(gameObject6, lpRoot, worldPositionStays: true);
				}
			}
		}
		AssetBundleInfo info2 = Singleton<Resources>.Instance.Map.ChunkList[MapID];
		GameObject original = CommonLib.LoadAsset<GameObject>(info2.assetbundle, info2.asset, clone: false, info2.manifest);
		MapScene.AddAssetBundlePath(info2);
		GameObject gameObject8 = (ChunkSrc = UnityEngine.Object.Instantiate(original));
		GameObject gameObject9 = gameObject8;
		gameObject9.transform.SetParent(_mapRoot, worldPositionStays: false);
		if (Singleton<Resources>.Instance.Map.SEMeshList.TryGetValue(MapID, out var value7))
		{
			GameObject original2 = CommonLib.LoadAsset<GameObject>(value7.assetbundle, value7.asset, clone: false, value7.manifest);
			MapScene.AddAssetBundlePath(value7);
			GameObject gameObject10 = UnityEngine.Object.Instantiate(original2);
			gameObject10.transform.SetParent(gameObject9.transform, worldPositionStays: false);
		}
		else
		{
			Debug.LogWarning($"マップ：{MapID} のSEメッシュがない");
		}
		Singleton<Resources>.Instance.HSceneTable.SetHmesh(gameObject9.transform);
		AssetBundleInfo info3 = Singleton<Resources>.Instance.Map.CameraColliderList[MapID];
		GameObject original3 = CommonLib.LoadAsset<GameObject>(info3.assetbundle, info3.asset, clone: false, info3.manifest ?? string.Empty);
		MapScene.AddAssetBundlePath(info3);
		GameObject gameObject11 = UnityEngine.Object.Instantiate(original3);
		gameObject11.transform.SetParent(gameObject9.transform, worldPositionStays: false);
		_mapDataEffect = GameObject.Find("searchpoint_effect");
		yield return null;
	}

	public IEnumerator LoadHousingObj(int mapID = 0)
	{
		yield return Singleton<Housing>.Instance.LoadHousing(mapID);
	}

	public IEnumerator LoadMerchantPoint()
	{
		PointManager pointAgent = _mapRoot.GetComponentInChildren<PointManager>(includeInactive: true);
		Transform mpRoot = new GameObject("MerchantPointRoot").transform;
		mpRoot.SetParent(pointAgent.transform, worldPositionStays: false);
		pointAgent.MerchantPointRoot = mpRoot;
		int chunkID = MapID;
		Dictionary<int, AssetBundleInfo> infos = Singleton<Resources>.Instance.Map.MerchantPointGroupTable[chunkID];
		foreach (KeyValuePair<int, AssetBundleInfo> item in infos)
		{
			GameObject gameObject = CommonLib.LoadAsset<GameObject>(item.Value.assetbundle, item.Value.asset, clone: false, item.Value.manifest);
			if (!(gameObject == null))
			{
				MapScene.AddAssetBundlePath(item.Value);
				UnityEngine.Object.Instantiate(gameObject, mpRoot, worldPositionStays: true);
			}
		}
		yield return null;
	}

	public IEnumerator LoadEventPoints()
	{
		PointManager pointAgent = _mapRoot.GetComponentInChildren<PointManager>(includeInactive: true);
		Transform epRoot = new GameObject("Event Point Root").transform;
		epRoot.SetParent(pointAgent.transform, worldPositionStays: false);
		pointAgent.EventPointRoot = epRoot;
		if (Singleton<Resources>.Instance.Map.EventPointGroupTable.TryGetValue(MapID, out var elementTable) && !elementTable.IsNullOrEmpty())
		{
			foreach (KeyValuePair<int, AssetBundleInfo> item in elementTable)
			{
				AssetBundleInfo value = item.Value;
				GameObject original = CommonLib.LoadAsset<GameObject>(value.assetbundle, value.asset, clone: false, value.manifest);
				MapScene.AddAssetBundlePath(value);
				UnityEngine.Object.Instantiate(original, epRoot, worldPositionStays: true);
			}
		}
		RefreshEventPointActive();
		yield return null;
	}

	public IEnumerator LoadStoryPoints()
	{
		PointManager pointAgent = _mapRoot.GetComponentInChildren<PointManager>(includeInactive: true);
		if (pointAgent == null)
		{
			yield break;
		}
		Transform spRoot = new GameObject("Story Point Root").transform;
		spRoot.SetParent(pointAgent.transform, worldPositionStays: false);
		pointAgent.StoryPointRoot = spRoot;
		if (Singleton<Resources>.Instance.Map.StoryPointGroupTable.TryGetValue(MapID, out var elementTable) && !elementTable.IsNullOrEmpty())
		{
			foreach (KeyValuePair<int, AssetBundleInfo> item in elementTable)
			{
				AssetBundleInfo value = item.Value;
				GameObject original = CommonLib.LoadAsset<GameObject>(value.assetbundle, value.asset, clone: false, value.manifest);
				MapScene.AddAssetBundlePath(value);
				UnityEngine.Object.Instantiate(original, spRoot, worldPositionStays: true);
			}
		}
		yield return null;
	}

	public IEnumerator LoadEnviroObject()
	{
		if (!Singleton<Resources>.IsInstance())
		{
			yield break;
		}
		if (!EnviroRootElement.IsNullOrEmpty())
		{
			foreach (KeyValuePair<int, Transform> item in EnviroRootElement)
			{
				Transform value = item.Value;
				if (!(value == null) && value.gameObject.activeSelf)
				{
					value.gameObject.SetActive(value: false);
				}
			}
		}
		yield return LoadEnviroSEObject();
		yield return LoadReverbObject();
	}

	private Transform GetEnviroRootElement(int areaID)
	{
		if (EnviroRootElement == null)
		{
			EnviroRootElement = new Dictionary<int, Transform>();
		}
		Transform value = null;
		if (!EnviroRootElement.TryGetValue(areaID, out value) || value == null)
		{
			value = (EnviroRootElement[areaID] = new GameObject(string.Format("Enviro Root Element {0}", areaID.ToString("00"))).transform);
			value.SetParent(EnviroGroupRoot, worldPositionStays: false);
		}
		return value;
	}

	public bool ActiveEnvAreaID(int areaID)
	{
		if (MapID < 0 || areaID < 0)
		{
			return false;
		}
		if (!Singleton<Resources>.IsInstance())
		{
			return false;
		}
		MapArea mapArea = ((!(Player != null)) ? null : Player.MapArea);
		if (mapArea == null)
		{
			return false;
		}
		int areaID2 = mapArea.AreaID;
		if (areaID == areaID2)
		{
			return true;
		}
		Dictionary<int, Dictionary<int, int[]>> enviroSEAdjacentInfoTable = Singleton<Resources>.Instance.Sound.EnviroSEAdjacentInfoTable;
		if (enviroSEAdjacentInfoTable.IsNullOrEmpty())
		{
			return false;
		}
		if (enviroSEAdjacentInfoTable.TryGetValue(MapID, out var value) && !value.IsNullOrEmpty() && value.TryGetValue(areaID2, out var value2) && !value2.IsNullOrEmpty())
		{
			return value2.Contains(areaID);
		}
		return false;
	}

	private IEnumerator LoadEnviroSEObject()
	{
		if (!EnviroSETable.IsNullOrEmpty())
		{
			foreach (KeyValuePair<int, UnityEx.ValueTuple<GameObject, EnvArea3DSE, EnvLineArea3DSE>> item2 in EnviroSETable)
			{
				GameObject item = item2.Value.Item1;
				if (item != null)
				{
					UnityEngine.Object.Destroy(item);
				}
			}
			EnviroSETable.Clear();
		}
		else if (EnviroSETable == null)
		{
			EnviroSETable = new Dictionary<int, UnityEx.ValueTuple<GameObject, EnvArea3DSE, EnvLineArea3DSE>>();
		}
		int chunkID = MapID;
		Dictionary<int, Dictionary<int, AssetBundleInfo>> enviroInfoTable = Singleton<Resources>.Instance.Sound.EnviroSEPrefabInfoTable;
		if (!enviroInfoTable.TryGetValue(chunkID, out var areaTable) || areaTable.IsNullOrEmpty())
		{
			yield break;
		}
		foreach (KeyValuePair<int, AssetBundleInfo> item3 in areaTable)
		{
			AssetBundleInfo value = item3.Value;
			GameObject gameObject = CommonLib.LoadAsset<GameObject>(value.assetbundle, value.asset, clone: false, value.manifest);
			if (!(gameObject == null))
			{
				MapScene.AddAssetBundlePath(value);
				Transform enviroRootElement = GetEnviroRootElement(item3.Key);
				GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject, enviroRootElement, worldPositionStays: true);
				EnvArea3DSE component = gameObject2.GetComponent<EnvArea3DSE>();
				EnvLineArea3DSE component2 = gameObject2.GetComponent<EnvLineArea3DSE>();
				if (component != null || component2 != null)
				{
					EnviroSETable[item3.Key] = new UnityEx.ValueTuple<GameObject, EnvArea3DSE, EnvLineArea3DSE>(gameObject2, component, component2);
				}
				else if (gameObject2 != null)
				{
					UnityEngine.Object.Destroy(gameObject2);
				}
			}
		}
		yield return null;
	}

	private IEnumerator LoadReverbObject()
	{
		if (!ReverbTable.IsNullOrEmpty())
		{
			foreach (KeyValuePair<int, System.Tuple<Transform, AudioReverbZone[]>> item2 in ReverbTable)
			{
				Transform item = item2.Value.Item1;
				if (!(item == null) && !(item.gameObject == null))
				{
					UnityEngine.Object.Destroy(item.gameObject);
				}
			}
			ReverbTable.Clear();
		}
		else if (ReverbTable == null)
		{
			ReverbTable = new Dictionary<int, System.Tuple<Transform, AudioReverbZone[]>>();
		}
		int chunkID = MapID;
		Dictionary<int, Dictionary<int, AssetBundleInfo>> reverbInfoTable = Singleton<Resources>.Instance.Sound.ReverbPrefabInfoTable;
		if (!reverbInfoTable.TryGetValue(chunkID, out var areaTable) || areaTable.IsNullOrEmpty())
		{
			yield break;
		}
		foreach (KeyValuePair<int, AssetBundleInfo> item3 in areaTable)
		{
			AssetBundleInfo value = item3.Value;
			GameObject gameObject = CommonLib.LoadAsset<GameObject>(value.assetbundle, value.asset, clone: false, value.manifest);
			if (!(gameObject == null))
			{
				MapScene.AddAssetBundlePath(value);
				Transform enviroRootElement = GetEnviroRootElement(item3.Key);
				GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject, enviroRootElement, worldPositionStays: true);
				AudioReverbZone[] componentsInChildren = gameObject2.GetComponentsInChildren<AudioReverbZone>(includeInactive: true);
				if (!componentsInChildren.IsNullOrEmpty())
				{
					ReverbTable[item3.Key] = new System.Tuple<Transform, AudioReverbZone[]>(gameObject2.transform, componentsInChildren);
				}
				else
				{
					UnityEngine.Object.Destroy(gameObject2);
				}
			}
		}
		yield return null;
	}

	public IEnumerator LoadAnimalPoint()
	{
		Transform apRoot = new GameObject("AnimalPointRoot").transform;
		apRoot.SetParent(_mapRoot, worldPositionStays: false);
		PointManager pointAgent = _mapRoot.GetComponentInChildren<PointManager>(includeInactive: true);
		pointAgent.AnimalPointRoot = apRoot;
		int chunkID = MapID;
		Dictionary<int, Dictionary<int, AssetBundleInfo>> assetInfoTable = Singleton<Resources>.Instance.AnimalTable.AnimalPointAssetTable;
		if (assetInfoTable.TryGetValue(chunkID, out var table))
		{
			foreach (KeyValuePair<int, AssetBundleInfo> item in table)
			{
				AssetBundleInfo value = item.Value;
				GameObject gameObject = CommonLib.LoadAsset<GameObject>(value.assetbundle, value.asset, clone: false, value.manifest);
				if (!(gameObject == null))
				{
					MapScene.AddAssetBundlePath(value);
					UnityEngine.Object.Instantiate(gameObject, apRoot, worldPositionStays: true);
				}
			}
		}
		yield return null;
	}

	public void StartSubscribe()
	{
		(from _ in Simulator.OnWeatherChangedAsObservable().TakeUntilDestroy(base.gameObject)
			where base.isActiveAndEnabled
			select _).Do(delegate(Weather weather)
		{
			RefreshWeather(weather);
		}).OnErrorRetry(delegate(Exception ex)
		{
			Debug.LogException(ex);
			if (Debug.isDebugBuild)
			{
				Debug.Log(string.Format("再購読します: {0}", "OnWeatherChangedAsObservable"));
			}
		}).Subscribe();
		(from _ in Simulator.OnTimeZoneChangedAsObservable().TakeUntilDestroy(base.gameObject)
			where base.isActiveAndEnabled
			select _).Do(delegate(AIProject.TimeZone timeZone)
		{
			RefreshTimeZone(timeZone);
		}).OnErrorRetry(delegate(Exception ex)
		{
			Debug.LogException(ex);
			if (Debug.isDebugBuild)
			{
				Debug.Log(string.Format("再購読します: {0}", "OnTimeZoneChangedAsObservable"));
			}
		}).Subscribe();
		(from _ in Simulator.OnDayAsObservable().TakeUntilDestroy(base.gameObject)
			where base.isActiveAndEnabled
			select _).Do(delegate(TimeSpan timeSpan)
		{
			OnElapsedDay(timeSpan);
		}).OnErrorRetry(delegate(Exception ex)
		{
			Debug.LogException(ex);
			if (Debug.isDebugBuild)
			{
				Debug.Log(string.Format("再購読します: {0}", "OnDayAsObservable"));
			}
		}).Subscribe();
		(from _ in Simulator.OnMinuteAsObservable().TakeUntilDestroy(Simulator.gameObject)
			where base.isActiveAndEnabled
			select _).Do(delegate(TimeSpan timeSpan)
		{
			OnElapsedMinute(timeSpan);
		}).OnErrorRetry(delegate(Exception ex)
		{
			Debug.LogException(ex);
			if (Debug.isDebugBuild)
			{
				Debug.Log(string.Format("再購読します: {0} -> OnElapsedMinute", "OnMinuteAsObservable"));
			}
		}).Subscribe();
		(from _ in Simulator.OnMinuteAsObservable().TakeUntilDestroy(Simulator.gameObject)
			where base.isActiveAndEnabled
			select _).Do(delegate(TimeSpan timeSpan)
		{
			Simulator.OnMinuteUpdate(timeSpan);
		}).OnErrorRetry(delegate(Exception ex)
		{
			Debug.LogException(ex);
			if (Debug.isDebugBuild)
			{
				Debug.Log(string.Format("再購読します：{0} -> Simulator.OnMinuteUpdate", "OnMinuteAsObservable"));
			}
		}).Subscribe();
		(from _ in Simulator.OnSecondAsObservable().TakeUntilDestroy(base.gameObject)
			where base.isActiveAndEnabled
			select _).OnErrorRetry(delegate(Exception ex)
		{
			Debug.LogException(ex);
			if (Debug.isDebugBuild)
			{
				Debug.Log(string.Format("再購読します: {0}", "OnSecondAsObservable"));
			}
		}).Subscribe(delegate(TimeSpan timeSpan)
		{
			OnElapsedSecond(timeSpan);
		}, delegate(Exception ex)
		{
			Debug.LogException(ex);
		}, delegate
		{
		});
		(from _ in Simulator.OnMapLightTimeZoneChangedAsObservable().TakeUntilDestroy(Simulator)
			where Simulator.isActiveAndEnabled
			select _).Do(delegate
		{
			RefreshActiveTimeRelationObjects();
		}).OnErrorRetry(delegate(Exception ex)
		{
			Debug.LogException(ex);
			if (Debug.isDebugBuild)
			{
				Debug.Log(string.Format("再購読します：{0}", "OnMapLightTimeZoneChangedAsObservable"));
			}
		}).Subscribe();
		Player.OnMapAreaChangedAsObservable().Do(delegate(int areaID)
		{
			OnMapAreaChanged(areaID);
			ResettingEnviroAreaElement(MapID, areaID);
			RefreshHousingEnv3DSEPoints(MapID, areaID);
		}).Subscribe();
	}

	private void OnMapAreaChanged(int areaID)
	{
		int mapID = MapID;
		if (Singleton<Resources>.Instance.Map.MapGroupHiddenAreaList.TryGetValue(mapID, out var value) && value.TryGetValue(areaID, out var value2))
		{
			foreach (KeyValuePair<int, GameObject> mapGroupObj in MapGroupObjList)
			{
				if (!(mapGroupObj.Value == null))
				{
					mapGroupObj.Value.SetActive(!value2.Contains(mapGroupObj.Key));
				}
			}
			HousingPointTable[0].gameObject.SetActive(!value2.Contains(100));
			HousingPointTable[1].gameObject.SetActive(!value2.Contains(101));
			HousingPointTable[2].gameObject.SetActive(!value2.Contains(102));
		}
		if (Singleton<Resources>.Instance.Map.AgentHiddenAreaList.TryGetValue(mapID, out var value3))
		{
			if (!value3.TryGetValue(areaID, out _agentVanishAreaList))
			{
				_agentVanishAreaList = null;
			}
		}
		else
		{
			_agentVanishAreaList = null;
		}
		ActorCameraControl cameraControl = Player.CameraControl;
		if (Singleton<Resources>.Instance.PlayerProfile.DisableWaterVFXAreaList.TryGetValue(mapID, out var value4))
		{
			bool flag = !value4.Contains(areaID);
			cameraControl.UnderWaterFX.enabled = flag;
			cameraControl.UnderWaterBlurFX.enabled = flag;
		}
		else
		{
			cameraControl.UnderWaterFX.enabled = true;
			cameraControl.UnderWaterBlurFX.enabled = true;
		}
	}

	public void SetAgentOpenState(int id, bool openState)
	{
		if (Singleton<Game>.IsInstance())
		{
			Dictionary<int, AgentData> dictionary = Singleton<Game>.Instance.WorldData?.AgentTable;
			if (dictionary != null && dictionary.TryGetValue(id, out var value) && value.OpenState != openState)
			{
				value.OpenState = openState;
				PlayerActor player = Player;
				player.PlayerController.CommandArea.UpdateCollision(player);
			}
		}
	}

	public bool GetBasePointOpenState(int id, out bool flag)
	{
		Dictionary<int, Dictionary<int, bool>> dictionary = Singleton<Game>.Instance.WorldData?.Environment?.BasePointOpenState;
		if (!dictionary.TryGetValue(MapID, out var value))
		{
			Dictionary<int, bool> dictionary2 = new Dictionary<int, bool>();
			dictionary[MapID] = dictionary2;
			value = dictionary2;
			flag = false;
			return false;
		}
		return value.TryGetValue(id, out flag);
	}

	public bool SetBaseOpenState(int id, bool openState)
	{
		if (Singleton<Game>.IsInstance())
		{
			Dictionary<int, Dictionary<int, bool>> dictionary = Singleton<Game>.Instance.WorldData?.Environment?.BasePointOpenState;
			if (dictionary != null)
			{
				if (!dictionary.TryGetValue(MapID, out var value))
				{
					Dictionary<int, bool> dictionary2 = new Dictionary<int, bool>();
					dictionary[MapID] = dictionary2;
					value = dictionary2;
				}
				value.TryGetValue(id, out var value2);
				if (value2 != openState)
				{
					value[id] = openState;
					return true;
				}
			}
		}
		return false;
	}

	public bool SetBaseOpenState(int mapID, int id, bool openState)
	{
		if (Singleton<Game>.IsInstance())
		{
			Dictionary<int, Dictionary<int, bool>> dictionary = Singleton<Game>.Instance.WorldData?.Environment?.BasePointOpenState;
			if (dictionary != null)
			{
				if (!dictionary.TryGetValue(mapID, out var value))
				{
					Dictionary<int, bool> dictionary3 = (dictionary[mapID] = new Dictionary<int, bool>());
					value = dictionary3;
				}
				value.TryGetValue(id, out var value2);
				if (value2 != openState)
				{
					value[id] = openState;
					return true;
				}
			}
		}
		return false;
	}

	public void InitializeDefaultState()
	{
		InitializeDefaultAreaOpenID();
		InitializeDefaultTimeRelationStateID();
		RefreshTutorialOpenState();
	}

	public void InitializeDefaultAreaOpenID()
	{
		if (!Singleton<Resources>.IsInstance() || !Singleton<Game>.IsInstance())
		{
			return;
		}
		bool isFreeMode = Game.IsFreeMode;
		Dictionary<int, bool> dictionary = Singleton<Game>.Instance.Environment?.AreaOpenState;
		Dictionary<int, string> areaOpenIDTable = Singleton<Resources>.Instance.Map.AreaOpenIDTable;
		if (dictionary == null || areaOpenIDTable == null)
		{
			return;
		}
		foreach (KeyValuePair<int, string> item in areaOpenIDTable)
		{
			if (isFreeMode)
			{
				dictionary[item.Key] = true;
			}
			else if (!dictionary.ContainsKey(item.Key))
			{
				dictionary[item.Key] = false;
			}
		}
	}

	public void InitializeDefaultTimeRelationStateID()
	{
		if (!Singleton<Resources>.IsInstance() || !Singleton<Game>.IsInstance())
		{
			return;
		}
		bool isFreeMode = Game.IsFreeMode;
		Dictionary<int, bool> dictionary = Singleton<Game>.Instance.Environment?.TimeObjOpenState;
		Dictionary<int, string> timeRelationObjectIDTable = Singleton<Resources>.Instance.Map.TimeRelationObjectIDTable;
		if (dictionary == null || timeRelationObjectIDTable == null)
		{
			return;
		}
		foreach (KeyValuePair<int, string> item in timeRelationObjectIDTable)
		{
			if (isFreeMode)
			{
				dictionary[item.Key] = true;
			}
			else if (!dictionary.ContainsKey(item.Key))
			{
				dictionary[item.Key] = false;
			}
		}
	}

	public void SetOpenAreaState(int id, bool active)
	{
		bool flag = false;
		if (Singleton<Game>.IsInstance())
		{
			Dictionary<int, bool> dictionary = Singleton<Game>.Instance.Environment.AreaOpenState;
			if (dictionary == null)
			{
				Dictionary<int, bool> dictionary2 = new Dictionary<int, bool>();
				Singleton<Game>.Instance.Environment.AreaOpenState = dictionary2;
				dictionary = dictionary2;
			}
			flag = !dictionary.TryGetValue(id, out var value) || value != active;
			dictionary[id] = active;
		}
		if (!AreaOpenObjectTable.IsNullOrEmpty() && AreaOpenObjectTable.TryGetValue(id, out var value2) && !value2.IsNullOrEmpty())
		{
			foreach (KeyValuePair<bool, ForcedHideObject[]> item in value2)
			{
				if (item.Value.IsNullOrEmpty())
				{
					continue;
				}
				bool key = item.Key;
				ForcedHideObject[] value3 = item.Value;
				foreach (ForcedHideObject forcedHideObject in value3)
				{
					if (!(forcedHideObject == null))
					{
						bool active2 = ((!key) ? (!active) : active);
						forcedHideObject.SetActive(active2);
					}
				}
			}
		}
		if (PointAgent != null)
		{
			EventPoint[] eventPoints = PointAgent.EventPoints;
			if (!eventPoints.IsNullOrEmpty())
			{
				EventPoint[] array = eventPoints;
				foreach (EventPoint eventPoint in array)
				{
					if (!(eventPoint == null) && eventPoint.OpenAreaID >= 0 && eventPoint.OpenAreaID == id)
					{
						eventPoint.SetActive(!active);
					}
				}
			}
		}
		if (!AreaOpenLinkedObject.Table.IsNullOrEmpty() && AreaOpenLinkedObject.Table.TryGetValue(id, out var value4) && !value4.IsNullOrEmpty())
		{
			foreach (GameObject item2 in value4)
			{
				if (!(item2 == null) && item2.activeSelf != active)
				{
					item2.SetActive(active);
				}
			}
		}
		if (flag)
		{
			if (Singleton<MapUIContainer>.IsInstance() && Singleton<MapUIContainer>.Instance.MinimapUI != null)
			{
				Singleton<MapUIContainer>.Instance.MinimapUI.RoadNaviMesh.Reflesh();
			}
			if (Merchant != null && active)
			{
				Merchant.SetOpenAreaID(this);
			}
		}
	}

	public bool GetOpenAreaState(int id)
	{
		if (!Singleton<Game>.IsInstance())
		{
			return false;
		}
		bool? flag = Singleton<Game>.Instance.Environment?.AreaOpenState?.IsNullOrEmpty();
		if (!flag.HasValue || flag.Value)
		{
			return false;
		}
		bool value;
		return Singleton<Game>.Instance.Environment.AreaOpenState.TryGetValue(id, out value) && value;
	}

	public void RefreshAreaOpenObject()
	{
		if (AreaOpenObjectTable.IsNullOrEmpty())
		{
			return;
		}
		int num;
		if (Singleton<Game>.IsInstance())
		{
			bool? flag = Singleton<Game>.Instance.Environment?.AreaOpenState?.IsNullOrEmpty();
			num = ((!flag.HasValue || flag.Value) ? 1 : 0);
		}
		else
		{
			num = 1;
		}
		bool flag2 = (byte)num != 0;
		bool isFreeMode = Game.IsFreeMode;
		if (flag2)
		{
			foreach (KeyValuePair<int, Dictionary<bool, ForcedHideObject[]>> item in AreaOpenObjectTable)
			{
				if (!item.Value.IsNullOrEmpty())
				{
					foreach (KeyValuePair<bool, ForcedHideObject[]> item2 in item.Value)
					{
						if (!item2.Value.IsNullOrEmpty())
						{
							bool flag3 = item2.Key;
							if (isFreeMode)
							{
								flag3 = !flag3;
							}
							ForcedHideObject[] value = item2.Value;
							foreach (ForcedHideObject forcedHideObject in value)
							{
								if (!(forcedHideObject == null))
								{
									forcedHideObject.SetActive(!flag3);
								}
							}
						}
					}
				}
			}
			return;
		}
		Dictionary<int, bool> areaOpenState = Singleton<Game>.Instance.Environment.AreaOpenState;
		foreach (KeyValuePair<int, Dictionary<bool, ForcedHideObject[]>> item3 in AreaOpenObjectTable)
		{
			if (item3.Value.IsNullOrEmpty())
			{
				continue;
			}
			int key = item3.Key;
			foreach (KeyValuePair<bool, ForcedHideObject[]> item4 in item3.Value)
			{
				if (item4.Value.IsNullOrEmpty())
				{
					continue;
				}
				bool key2 = item4.Key;
				ForcedHideObject[] value2 = item4.Value;
				foreach (ForcedHideObject forcedHideObject2 in value2)
				{
					if (!(forcedHideObject2 == null))
					{
						if (!areaOpenState.TryGetValue(key, out var value3))
						{
							bool flag5 = (areaOpenState[key] = isFreeMode);
							value3 = flag5;
						}
						if (!key2)
						{
							value3 = !value3;
						}
						forcedHideObject2.SetActive(value3);
					}
				}
			}
		}
	}

	public void RefreshEventPointActive()
	{
		if (PointAgent == null)
		{
			return;
		}
		EventPoint[] array = PointAgent.EventPoints;
		if (array.IsNullOrEmpty() && PointAgent.EventPointRoot != null)
		{
			array = PointAgent.EventPointRoot.GetComponentsInChildren<EventPoint>(includeInactive: true);
		}
		if (array.IsNullOrEmpty())
		{
			return;
		}
		int num;
		if (Singleton<Game>.IsInstance())
		{
			bool? flag = Singleton<Game>.Instance.Environment?.AreaOpenState?.IsNullOrEmpty();
			num = ((!flag.HasValue || flag.Value) ? 1 : 0);
		}
		else
		{
			num = 1;
		}
		bool flag2 = (byte)num != 0;
		bool isFreeMode = Game.IsFreeMode;
		if (flag2)
		{
			EventPoint[] array2 = array;
			foreach (EventPoint eventPoint in array2)
			{
				if (!(eventPoint == null) && eventPoint.OpenAreaID >= 0 && !eventPoint.gameObject.activeSelf)
				{
					eventPoint.gameObject.SetActive(!isFreeMode);
				}
			}
			return;
		}
		Dictionary<int, bool> areaOpenState = Singleton<Game>.Instance.Environment.AreaOpenState;
		EventPoint[] array3 = array;
		foreach (EventPoint eventPoint2 in array3)
		{
			if (!(eventPoint2 == null) && eventPoint2.OpenAreaID >= 0)
			{
				int openAreaID = eventPoint2.OpenAreaID;
				if (!areaOpenState.TryGetValue(openAreaID, out var value))
				{
					value = false;
				}
				if (isFreeMode)
				{
					value = true;
				}
				if (eventPoint2.gameObject.activeSelf == value)
				{
					eventPoint2.gameObject.SetActive(!value);
				}
			}
		}
	}

	public void RefreshAreaOpenLinkedObject()
	{
		if (AreaOpenLinkedObject.Table.IsNullOrEmpty())
		{
			return;
		}
		int num;
		if (Singleton<Game>.IsInstance())
		{
			bool? flag = Singleton<Game>.Instance.Environment?.AreaOpenState?.IsNullOrEmpty();
			num = ((!flag.HasValue || flag.Value) ? 1 : 0);
		}
		else
		{
			num = 1;
		}
		bool flag2 = (byte)num != 0;
		bool isFreeMode = Game.IsFreeMode;
		if (flag2)
		{
			foreach (KeyValuePair<int, List<GameObject>> item in AreaOpenLinkedObject.Table)
			{
				if (!item.Value.IsNullOrEmpty())
				{
					foreach (GameObject item2 in item.Value)
					{
						if (!(item2 == null) && item2.activeSelf)
						{
							item2.SetActive(isFreeMode);
						}
					}
				}
			}
			return;
		}
		Dictionary<int, bool> areaOpenState = Singleton<Game>.Instance.Environment.AreaOpenState;
		foreach (KeyValuePair<int, List<GameObject>> item3 in AreaOpenLinkedObject.Table)
		{
			if (item3.Value.IsNullOrEmpty() || !areaOpenState.TryGetValue(item3.Key, out var value))
			{
				continue;
			}
			foreach (GameObject item4 in item3.Value)
			{
				if (!(item4 == null) && item4.activeSelf != value)
				{
					item4.SetActive(value);
				}
			}
		}
	}

	public void RefreshTimeOpenLinkedObject()
	{
		if (TimeOpenLinkedObject.Table.IsNullOrEmpty())
		{
			return;
		}
		if (Singleton<Game>.IsInstance())
		{
			bool? flag = Singleton<Game>.Instance.Environment?.TimeObjOpenState?.IsNullOrEmpty();
			if (flag.HasValue && !flag.Value)
			{
				Dictionary<int, bool> timeObjOpenState = Singleton<Game>.Instance.Environment.TimeObjOpenState;
				{
					foreach (KeyValuePair<int, List<TimeOpenLinkedObject>> item in TimeOpenLinkedObject.Table)
					{
						if (item.Value.IsNullOrEmpty())
						{
							continue;
						}
						if (!timeObjOpenState.TryGetValue(item.Key, out var value))
						{
							value = false;
						}
						foreach (TimeOpenLinkedObject item2 in item.Value)
						{
							if (!(item2 == null) && !(item2.gameObject == null))
							{
								item2.SetActive(item2.EnableFlag == value);
							}
						}
					}
					return;
				}
			}
		}
		foreach (KeyValuePair<int, List<TimeOpenLinkedObject>> item3 in TimeOpenLinkedObject.Table)
		{
			if (item3.Value.IsNullOrEmpty())
			{
				continue;
			}
			foreach (TimeOpenLinkedObject item4 in item3.Value)
			{
				if (!(item4 == null) && !(item4.gameObject == null))
				{
					item4.SetActive(!item4.EnableFlag);
				}
			}
		}
	}

	public void SetTimeRelationAreaOpenState(int areaID, bool active)
	{
		if (!Singleton<Game>.IsInstance())
		{
			return;
		}
		bool flag = false;
		Dictionary<int, bool> dictionary = Singleton<Game>.Instance.Environment.TimeObjOpenState;
		if (dictionary == null)
		{
			Dictionary<int, bool> dictionary2 = new Dictionary<int, bool>();
			Singleton<Game>.Instance.Environment.TimeObjOpenState = dictionary2;
			dictionary = dictionary2;
		}
		flag = !dictionary.TryGetValue(areaID, out var value) || value != active;
		if (flag)
		{
			dictionary[areaID] = active;
		}
		if (!TimeOpenLinkedObject.Table.IsNullOrEmpty() && TimeOpenLinkedObject.Table.TryGetValue(areaID, out var value2) && !value2.IsNullOrEmpty())
		{
			foreach (TimeOpenLinkedObject item in value2)
			{
				if (!(item == null) && !(item.gameObject == null))
				{
					item.SetActive(item.EnableFlag == active);
				}
			}
		}
		if (flag)
		{
			RefreshActiveTimeRelationObjects();
		}
	}

	public bool CanSleepInTime()
	{
		DateTime now = Simulator.Now;
		PlayerProfile playerProfile = Singleton<Resources>.Instance.PlayerProfile;
		EnvironmentSimulator.DateTimeThreshold[] canSleepTime = playerProfile.CanSleepTime;
		for (int i = 0; i < canSleepTime.Length; i++)
		{
			EnvironmentSimulator.DateTimeThreshold dateTimeThreshold = canSleepTime[i];
			if (dateTimeThreshold.min.Time <= now && dateTimeThreshold.max.Time > now)
			{
				return true;
			}
		}
		return false;
	}

	public bool GetTimeObjOpenState(int id)
	{
		if (!Singleton<Game>.IsInstance())
		{
			return false;
		}
		Dictionary<int, bool> dictionary = Singleton<Game>.Instance.Environment?.TimeObjOpenState;
		if (dictionary == null)
		{
			return false;
		}
		bool value;
		return dictionary.TryGetValue(id, out value) && value;
	}

	public void RefreshTutorialOpenState()
	{
		if (!Singleton<Game>.IsInstance() || !Singleton<Resources>.IsInstance())
		{
			return;
		}
		bool isFirstGame = Game.IsFirstGame;
		bool isFreeMode = Game.IsFreeMode;
		WorldData worldData = Singleton<Game>.Instance.WorldData;
		if (worldData == null)
		{
			return;
		}
		Dictionary<int, bool> dictionary = worldData.TutorialOpenStateTable;
		if (dictionary == null)
		{
			dictionary = (worldData.TutorialOpenStateTable = new Dictionary<int, bool>());
		}
		ReadOnlyDictionary<int, UnityEx.ValueTuple<string, GameObject[]>> tutorialPrefabTable = Singleton<Resources>.Instance.PopupInfo.TutorialPrefabTable;
		if (tutorialPrefabTable.IsNullOrEmpty())
		{
			return;
		}
		foreach (int key in tutorialPrefabTable.Keys)
		{
			if (isFreeMode && isFirstGame)
			{
				dictionary[key] = true;
			}
			else if (!dictionary.ContainsKey(key))
			{
				dictionary[key] = isFreeMode;
			}
		}
	}

	public bool CheckAvailableMapArea(int mapAreaID)
	{
		if (MapID != 0)
		{
			return true;
		}
		if (!Singleton<Resources>.IsInstance())
		{
			return false;
		}
		if (!Singleton<Game>.IsInstance())
		{
			return false;
		}
		Dictionary<int, bool> dictionary = Singleton<Game>.Instance.Environment?.AreaOpenState;
		if (dictionary == null)
		{
			return false;
		}
		Dictionary<int, int[]> areaOpenStateMapAreaLinkerTable = Singleton<Resources>.Instance.Map.AreaOpenStateMapAreaLinkerTable;
		if (!areaOpenStateMapAreaLinkerTable.TryGetValue(mapAreaID, out var value))
		{
			return false;
		}
		if (value.IsNullOrEmpty())
		{
			return true;
		}
		bool flag = true;
		int[] array = value;
		foreach (int key in array)
		{
			flag = dictionary.TryGetValue(key, out var value2) && flag && value2;
		}
		return flag;
	}

	public void ResettingEnviroSEActive(int mapID, int areaID)
	{
		if (mapID < 0 || areaID < 0 || !Singleton<Resources>.IsInstance())
		{
			return;
		}
		Resources.SoundTable sound = Singleton<Resources>.Instance.Sound;
		if (sound == null || EnviroSETable.IsNullOrEmpty())
		{
			return;
		}
		Dictionary<int, int[]> value = null;
		int[] value2 = null;
		if (sound.EnviroSEAdjacentInfoTable.TryGetValue(mapID, out value) && value.TryGetValue(areaID, out value2) && !value2.IsNullOrEmpty())
		{
			List<int> list = ListPool<int>.Get();
			foreach (KeyValuePair<int, UnityEx.ValueTuple<GameObject, EnvArea3DSE, EnvLineArea3DSE>> item2 in EnviroSETable)
			{
				list.Add(item2.Key);
			}
			if (list.Contains(areaID))
			{
				list.Remove(areaID);
			}
			if (EnviroSETable.TryGetValue(areaID, out var value3) && value3.Item1 != null && !value3.Item1.activeSelf)
			{
				value3.Item1.SetActive(value: true);
			}
			int[] array = value2;
			foreach (int num in array)
			{
				if (EnviroSETable.TryGetValue(num, out var value4) && value4.Item1 != null && !value4.Item1.activeSelf)
				{
					value4.Item1.SetActive(value: true);
				}
				if (list.Contains(num))
				{
					list.Remove(num);
				}
			}
			foreach (int item3 in list)
			{
				if (EnviroSETable.TryGetValue(item3, out var value5) && value5.Item1 != null && value5.Item1.activeSelf)
				{
					value5.Item1.SetActive(value: false);
				}
			}
			ListPool<int>.Release(list);
			return;
		}
		foreach (KeyValuePair<int, UnityEx.ValueTuple<GameObject, EnvArea3DSE, EnvLineArea3DSE>> item4 in EnviroSETable)
		{
			GameObject item = item4.Value.Item1;
			if (!(item == null) && item.activeSelf)
			{
				item.SetActive(value: false);
			}
		}
	}

	public void ResettingEnviroAreaElement(int mapID, int areaID)
	{
		if (mapID < 0 || areaID < 0 || !Singleton<Resources>.IsInstance())
		{
			return;
		}
		Dictionary<int, Dictionary<int, int[]>> dictionary = Singleton<Resources>.Instance.Sound?.EnviroSEAdjacentInfoTable;
		if (dictionary.IsNullOrEmpty() || EnviroRootElement.IsNullOrEmpty())
		{
			return;
		}
		Dictionary<int, int[]> value = null;
		int[] value2 = null;
		if (dictionary.TryGetValue(mapID, out value) && !value.IsNullOrEmpty() && value.TryGetValue(areaID, out value2) && !value2.IsNullOrEmpty())
		{
			List<int> list = ListPool<int>.Get();
			list.AddRange(value2);
			if (!list.Contains(areaID))
			{
				list.Add(areaID);
			}
			List<int> list2 = ListPool<int>.Get();
			list2.AddRange(EnviroRootElement.Keys);
			list2.Remove(areaID);
			foreach (int item in list)
			{
				if (EnviroRootElement.TryGetValue(item, out var value3) && value3 != null && !value3.gameObject.activeSelf)
				{
					value3.gameObject.SetActive(value: true);
				}
				if (list2.Contains(item))
				{
					list2.Remove(item);
				}
			}
			foreach (int item2 in list2)
			{
				if (EnviroRootElement.TryGetValue(item2, out var value4) && value4 != null && value4.gameObject.activeSelf)
				{
					value4.gameObject.SetActive(value: false);
				}
			}
			ListPool<int>.Release(list);
			ListPool<int>.Release(list2);
			return;
		}
		foreach (KeyValuePair<int, Transform> item3 in EnviroRootElement)
		{
			Transform value5 = item3.Value;
			if (!(value5 == null) && !(value5.gameObject == null))
			{
				bool flag = areaID == item3.Key;
				if (value5.gameObject.activeSelf != flag)
				{
					value5.gameObject.SetActive(flag);
				}
			}
		}
	}

	public void RefreshHousingEnv3DSEPoints(int mapID, int areaID)
	{
		if (mapID < 0 || areaID < 0 || !Singleton<Resources>.IsInstance())
		{
			return;
		}
		Dictionary<int, Dictionary<int, int[]>> dictionary = Singleton<Resources>.Instance.Sound?.EnviroSEAdjacentInfoTable;
		if (dictionary.IsNullOrEmpty() || HousingEnvSEPointTable.IsNullOrEmpty())
		{
			return;
		}
		Dictionary<int, int[]> value = null;
		int[] value2 = null;
		if (dictionary.TryGetValue(mapID, out value) && !value.IsNullOrEmpty() && value.TryGetValue(areaID, out value2) && !value2.IsNullOrEmpty())
		{
			List<int> list = ListPool<int>.Get();
			list.AddRange(value2);
			if (!list.Contains(areaID))
			{
				list.Add(areaID);
			}
			List<int> list2 = ListPool<int>.Get();
			list2.AddRange(HousingEnvSEPointTable.Keys);
			list2.Remove(areaID);
			foreach (int item in list)
			{
				if (HousingEnvSEPointTable.TryGetValue(item, out var value3))
				{
					value3.Item1 = true;
					if (!value3.Item2.IsNullOrEmpty())
					{
						foreach (Env3DSEPoint item2 in value3.Item2)
						{
							if (!(item2 == null))
							{
								item2.enabled = true;
							}
						}
					}
					HousingEnvSEPointTable[item] = value3;
				}
				if (list2.Contains(item))
				{
					list2.Remove(item);
				}
			}
			foreach (int item3 in list2)
			{
				if (!HousingEnvSEPointTable.TryGetValue(item3, out var value4))
				{
					continue;
				}
				value4.Item1 = false;
				if (value4.Item2.IsNullOrEmpty())
				{
					continue;
				}
				foreach (Env3DSEPoint item4 in value4.Item2)
				{
					if (!(item4 == null))
					{
						item4.enabled = false;
					}
				}
			}
			ListPool<int>.Release(list);
			ListPool<int>.Release(list2);
		}
		else
		{
			if (HousingEnvSEPointTable.IsNullOrEmpty())
			{
				return;
			}
			List<int> list3 = HousingEnvSEPointTable.Keys.ToList();
			foreach (int item5 in list3)
			{
				UnityEx.ValueTuple<bool, List<Env3DSEPoint>> value5 = HousingEnvSEPointTable[item5];
				value5.Item1 = item5 == areaID;
				if (!value5.Item2.IsNullOrEmpty())
				{
					foreach (Env3DSEPoint item6 in value5.Item2)
					{
						if (item6 != null && item6.enabled != value5.Item1)
						{
							item6.enabled = value5.Item1;
						}
					}
				}
				HousingEnvSEPointTable[item5] = value5;
			}
		}
	}

	public static StoryPoint GetStoryPoint(int id)
	{
		if (Singleton<Map>.IsInstance() && Singleton<Map>.Instance._pointAgent != null)
		{
			StoryPoint value = null;
			Singleton<Map>.Instance._pointAgent.StoryPointTable.TryGetValue(id, out value);
			return value;
		}
		return null;
	}

	public static bool SetTutorialProgress(int number)
	{
		if (!Singleton<Game>.IsInstance())
		{
			return false;
		}
		AIProject.SaveData.Environment environment = Singleton<Game>.Instance.Environment;
		if (environment == null)
		{
			return false;
		}
		int tutorialProgress = environment.TutorialProgress;
		environment.TutorialProgress = Mathf.Max(tutorialProgress, number);
		return tutorialProgress < number;
	}

	public static bool ForcedSetTutorialProgress(int number)
	{
		if (!Singleton<Game>.IsInstance())
		{
			return false;
		}
		AIProject.SaveData.Environment environment = Singleton<Game>.Instance.Environment;
		if (environment == null)
		{
			return false;
		}
		environment.TutorialProgress = number;
		return true;
	}

	public static bool ForcedSetTutorialProgressAndUIUpdate(int number)
	{
		if (!Singleton<Game>.IsInstance())
		{
			return false;
		}
		AIProject.SaveData.Environment environment = Singleton<Game>.Instance.Environment;
		if (environment == null)
		{
			return false;
		}
		environment.TutorialProgress = number;
		MapUIContainer.StorySupportUI.Open(number);
		return true;
	}

	public static void SetTutorialProgressAndUIUpdate(int number)
	{
		if (SetTutorialProgress(number))
		{
			MapUIContainer.OpenStorySupportUI(number);
		}
	}

	public static void RefreshStoryUI()
	{
		if (Singleton<Game>.IsInstance())
		{
			AIProject.SaveData.Environment environment = Singleton<Game>.Instance.Environment;
			if (environment != null)
			{
				MapUIContainer.OpenStorySupportUI(environment.TutorialProgress);
			}
		}
	}

	public void CheckTutorialState(PlayerActor player)
	{
		if (player == null || !Singleton<Resources>.IsInstance())
		{
			return;
		}
		Resources resources = Singleton<Resources>.Instance;
		if (TutorialMode)
		{
			if (ChangeTutorialUI(player))
			{
				return;
			}
			switch ((Popup.StorySupport.Type)GetTutorialProgress())
			{
			case Popup.StorySupport.Type.GetDriftwood:
			{
				ItemIDKeyPair driftwoodID = resources.CommonDefine.ItemIDDefine.DriftwoodID;
				if (player.PlayerData.ItemList.Exists((StuffItem x) => x.CategoryID == driftwoodID.categoryID && x.ID == driftwoodID.itemID && 0 < x.Count))
				{
					Singleton<Map>.Instance.DestroyTutorialSearchPoint();
					SetTutorialProgress(4);
					player.AddTutorialUI(Popup.Tutorial.Type.Craft, groupDisplay: false);
				}
				break;
			}
			case Popup.StorySupport.Type.CraftFishingRod:
			{
				bool flag3 = false;
				FishingDefinePack.ItemIDPair fishingRodID = resources.FishingDefinePack.IDInfo.FishingRod;
				if (player.PlayerData.ItemList.Exists((StuffItem x) => x.CategoryID == fishingRodID.CategoryID && x.ID == fishingRodID.ItemID && 0 < x.Count))
				{
					SetTutorialProgress(5);
					player.AddTutorialUI(Popup.Tutorial.Type.Equipment, groupDisplay: false);
				}
				break;
			}
			case Popup.StorySupport.Type.EquippedFishingRod:
			{
				int iD = player.PlayerData.EquipedFishingItem.ID;
				if (iD == resources.CommonDefine.ItemIDDefine.RodID.itemID)
				{
					SetTutorialProgress(6);
				}
				break;
			}
			case Popup.StorySupport.Type.FishingGetFish:
			{
				List<FishingDefinePack.ItemIDPair> fishList = resources.FishingDefinePack.IDInfo.FishList;
				bool flag2 = false;
				if (fishList.IsNullOrEmpty())
				{
					break;
				}
				foreach (FishingDefinePack.ItemIDPair data in fishList)
				{
					if (player.PlayerData.ItemList.Exists((StuffItem x) => x.CategoryID == data.CategoryID && x.ID == data.ItemID && 0 < x.Count))
					{
						flag2 = true;
						break;
					}
				}
				if (flag2)
				{
					SetTutorialProgress(7);
				}
				break;
			}
			case Popup.StorySupport.Type.ArrangeKitchen:
			{
				bool flag = false;
				FishingDefinePack fishingDefinePack = resources?.FishingDefinePack;
				List<StuffItem> list = player?.PlayerData?.ItemList;
				if (fishingDefinePack != null && list != null)
				{
					FishingDefinePack.ItemIDPair idinfo = fishingDefinePack.IDInfo.GrilledFish;
					flag = list.Exists((StuffItem x) => x.CategoryID == idinfo.CategoryID && x.ID == idinfo.ItemID && 0 < x.Count);
					if (flag)
					{
						SetTutorialProgress(13);
					}
				}
				if (flag)
				{
					break;
				}
				CommonDefine commonDefine = ((!Singleton<Resources>.IsInstance()) ? null : Singleton<Resources>.Instance.CommonDefine);
				if (!(commonDefine != null) || !(_pointAgent != null) || commonDefine.Tutorial.KitchenPointIDList.IsNullOrEmpty())
				{
					break;
				}
				int[] kitchenPointIDList = commonDefine.Tutorial.KitchenPointIDList;
				foreach (int kitchenID in kitchenPointIDList)
				{
					if (_pointAgent.AppendActionPoints.Exists((ActionPoint x) => x.ID == kitchenID))
					{
						SetTutorialProgress(12);
						break;
					}
				}
				break;
			}
			case Popup.StorySupport.Type.CookFish:
			{
				FishingDefinePack.ItemIDPair idinfo2 = resources.FishingDefinePack.IDInfo.GrilledFish;
				List<StuffItem> itemList = player.PlayerData.ItemList;
				if (itemList.Exists((StuffItem x) => x.CategoryID == idinfo2.CategoryID && x.ID == idinfo2.ItemID && 0 < x.Count))
				{
					SetTutorialProgress(13);
				}
				break;
			}
			}
		}
		if (ChangeTutorialUI(player) || MapID != 0 || GetTutorialProgress() == MapUIContainer.StorySupportUI.Index)
		{
			return;
		}
		bool flag4 = Singleton<MapScene>.IsInstance() && Singleton<MapScene>.Instance.IsLoading;
		bool flag5 = !flag4 && Singleton<Scene>.IsInstance() && Singleton<Scene>.Instance.IsNowLoadingFade;
		if (flag4)
		{
			Observable.EveryUpdate().TakeUntilDestroy(Singleton<MapScene>.Instance.gameObject).TakeUntilDestroy(player.gameObject)
				.SkipWhile((long _) => Singleton<MapScene>.IsInstance() && Singleton<MapScene>.Instance.IsLoading)
				.SkipWhile((long _) => player.CameraControl.CinemachineBrain.IsBlending || player.Animation.PlayingInLocoAnimation)
				.Take(1)
				.DelayFrame(15)
				.Subscribe(delegate
				{
					RefreshStoryUI();
				});
		}
		else if (flag5)
		{
			Observable.EveryUpdate().TakeUntilDestroy(Singleton<MapScene>.Instance.gameObject).TakeUntilDestroy(player.gameObject)
				.SkipWhile((long _) => Singleton<Scene>.IsInstance() && Singleton<Scene>.Instance.IsNowLoadingFade)
				.SkipWhile((long _) => player.CameraControl.CinemachineBrain.IsBlending || player.Animation.PlayingInLocoAnimation)
				.Take(1)
				.DelayFrame(15)
				.Subscribe(delegate
				{
					RefreshStoryUI();
				});
		}
		else
		{
			Observable.EveryUpdate().TakeUntilDestroy(player).Skip(1)
				.SkipWhile((long _) => player.CameraControl.CinemachineBrain.IsBlending || player.Animation.PlayingInLocoAnimation)
				.Take(1)
				.DelayFrame(15)
				.Subscribe(delegate
				{
					RefreshStoryUI();
				});
		}
	}

	private bool ChangeTutorialUI(PlayerActor player)
	{
		float delayTime = ((!Singleton<Resources>.IsInstance()) ? 1f : Singleton<Resources>.Instance.CommonDefine.Tutorial.UIDisplayDelayTime);
		if (!player.TutorialIndexList.IsNullOrEmpty())
		{
			UnityEx.ValueTuple<Popup.Tutorial.Type, bool> valueTuple = player.TutorialIndexList.Pop();
			MapUIContainer.TutorialUI.SetCondition(valueTuple.Item1, valueTuple.Item2);
			EventPoint.SetCurrentPlayerStateName();
			player.PlayerController.ChangeState("Idle");
			bool flag = Singleton<MapScene>.IsInstance() && Singleton<MapScene>.Instance.IsLoading;
			bool flag2 = !flag && Singleton<Scene>.IsInstance() && Singleton<Scene>.Instance.IsNowLoadingFade;
			if (flag)
			{
				Observable.EveryUpdate().TakeUntilDestroy(Singleton<MapScene>.Instance.gameObject).TakeUntilDestroy(player.gameObject)
					.SkipWhile((long _) => Singleton<MapScene>.IsInstance() && Singleton<MapScene>.Instance.IsLoading)
					.Take(1)
					.Subscribe(delegate
					{
						DelayChangeTutorialUI(player, delayTime);
					});
			}
			else if (flag2)
			{
				Observable.EveryUpdate().TakeUntilDestroy(Singleton<Scene>.Instance).TakeUntilDestroy(player.gameObject)
					.SkipWhile((long _) => Singleton<Scene>.IsInstance() && Singleton<Scene>.Instance.IsNowLoadingFade)
					.Take(1)
					.Subscribe(delegate
					{
						DelayChangeTutorialUI(player, delayTime);
					});
			}
			else
			{
				DelayChangeTutorialUI(player, delayTime);
			}
			return true;
		}
		return false;
	}

	private void DelayChangeTutorialUI(PlayerActor player, float delayTime)
	{
		if (GetTutorialProgress() != MapUIContainer.StorySupportUI.Index)
		{
			MapUIContainer.StorySupportUI.OpenedAction = delegate
			{
				(from _ in Observable.Timer(TimeSpan.FromSeconds(delayTime)).TakeUntilDestroy(player.gameObject)
					where Singleton<Map>.IsInstance()
					select _).Subscribe(delegate
				{
					player.PlayerController.ChangeState("Tutorial");
				});
				MapUIContainer.StorySupportUI.OpenedAction = null;
			};
			Observable.EveryUpdate().TakeUntilDestroy(player.gameObject).Skip(1)
				.SkipWhile((long _) => player.Animation.PlayingInLocoAnimation || player.CameraControl.CinemachineBrain.IsBlending)
				.Take(1)
				.Subscribe(delegate
				{
					RefreshStoryUI();
				});
		}
		else
		{
			Observable.EveryUpdate().Skip(1).SkipWhile((long _) => player.Animation.PlayingInLocoAnimation || player.CameraControl.CinemachineBrain.IsBlending)
				.Take(1)
				.Delay(TimeSpan.FromSeconds(delayTime))
				.Subscribe(delegate
				{
					player.PlayerController.ChangeState("Tutorial");
				});
		}
	}

	public void CheckStoryProgress()
	{
		if (MapID != 0)
		{
			return;
		}
		int tutorialProgress = GetTutorialProgress();
		Popup.StorySupport.Type type = (Popup.StorySupport.Type)tutorialProgress;
		switch (type)
		{
		case Popup.StorySupport.Type.GrowGirls1:
		{
			EventPoint eventPoint2 = EventPoint.Get(1, 0);
			if (eventPoint2 != null && eventPoint2.LabelIndex == 1)
			{
				ForcedSetTutorialProgressAndUIUpdate(17);
			}
			break;
		}
		case Popup.StorySupport.Type.GrowGirls2:
		{
			EventPoint eventPoint3 = EventPoint.Get(1, 1);
			if (eventPoint3 != null && eventPoint3.LabelIndex == 1)
			{
				ForcedSetTutorialProgressAndUIUpdate(20);
			}
			break;
		}
		case Popup.StorySupport.Type.GrowGirls3:
		case Popup.StorySupport.Type.ExamineStoryPoint3:
		case Popup.StorySupport.Type.RepairGenerator:
		case Popup.StorySupport.Type.ExamineStoryPoint4:
		{
			EventPoint eventPoint = EventPoint.Get(1, 2);
			if (!(eventPoint != null))
			{
				break;
			}
			switch (eventPoint.LabelIndex)
			{
			case 0:
				if (type != Popup.StorySupport.Type.GrowGirls3)
				{
					ForcedSetTutorialProgressAndUIUpdate(22);
				}
				break;
			case 1:
				if (type != Popup.StorySupport.Type.ExamineStoryPoint3)
				{
					ForcedSetTutorialProgressAndUIUpdate(23);
				}
				break;
			case 2:
				if (type != Popup.StorySupport.Type.RepairGenerator)
				{
					ForcedSetTutorialProgressAndUIUpdate(24);
				}
				break;
			case 3:
				if (type != Popup.StorySupport.Type.ExamineStoryPoint4)
				{
					ForcedSetTutorialProgressAndUIUpdate(25);
				}
				break;
			}
			break;
		}
		}
		bool flag = false;
		if (Player != null)
		{
			flag = Player.Mode == Desire.ActionType.Date;
		}
		type = (Popup.StorySupport.Type)GetTutorialProgress();
		if (!flag)
		{
			switch (type)
			{
			case Popup.StorySupport.Type.GrowGirls1:
			case Popup.StorySupport.Type.ExamineStoryPoint1:
				EventPoint.SetTargetID(1, 0);
				break;
			case Popup.StorySupport.Type.ExamineNextStoryPoint1:
			case Popup.StorySupport.Type.GrowGirls2:
			case Popup.StorySupport.Type.ExamineStoryPoint2:
				EventPoint.SetTargetID(1, 1);
				break;
			case Popup.StorySupport.Type.ExamineNextStoryPoint2:
			case Popup.StorySupport.Type.GrowGirls3:
			case Popup.StorySupport.Type.ExamineStoryPoint3:
			case Popup.StorySupport.Type.ExamineStoryPoint4:
				EventPoint.SetTargetID(1, 2);
				break;
			case Popup.StorySupport.Type.RepairGenerator:
				EventPoint.SetTargetID(1, 3);
				break;
			case Popup.StorySupport.Type.ExamineNextStoryPoint3:
			case Popup.StorySupport.Type.RepairShip:
				EventPoint.SetTargetID(1, 6);
				break;
			default:
				EventPoint.SetTargetID(-1, -1);
				break;
			}
		}
		else if (type == Popup.StorySupport.Type.ExamineStoryPoint4)
		{
			EventPoint.SetTargetID(1, 2);
		}
		else
		{
			EventPoint.SetTargetID(-1, -1);
		}
	}

	public static int GetTutorialProgress()
	{
		if (Singleton<Game>.IsInstance())
		{
			return Singleton<Game>.Instance.Environment?.TutorialProgress ?? (-1);
		}
		return -1;
	}

	public static int GetAgentTotalFlavorAdditionAmount()
	{
		if (!Singleton<Map>.IsInstance())
		{
			return 0;
		}
		if (Singleton<Map>.Instance.AgentTable.IsNullOrEmpty())
		{
			return 0;
		}
		int num = 0;
		foreach (KeyValuePair<int, AgentActor> item in Singleton<Map>.Instance.AgentTable)
		{
			AgentActor value = item.Value;
			if (!(value == null))
			{
				AgentData agentData = value.AgentData;
				if (agentData != null && agentData.OpenState)
				{
					num += agentData.FlavorAdditionAmount;
				}
			}
		}
		return num;
	}

	public static int GetTotalAgentFlavorAdditionAmount()
	{
		if (!Singleton<Map>.IsInstance() || !Singleton<Game>.IsInstance())
		{
			return 0;
		}
		return Singleton<Game>.Instance.Environment?.TotalAgentFlavorAdditionAmount ?? 0;
	}

	public IEnumerator SetupPoint()
	{
		_pointAgent.SetActive(isActive: true);
		yield return _pointAgent.Load();
		if (Debug.isDebugBuild)
		{
			Debug.Log("ポイント配置完了");
		}
	}

	public IEnumerator ApplyProfile(WorldData profile, bool existsBackup)
	{
		Debug.Log("ーーーーーキャラ読み込みーーーーー");
		yield return RefreshAsync();
		yield return LoadPlayerAsync(profile.PlayerData, existsBackup);
		yield return LoadMerchantAsync(profile.MerchantData, existsBackup);
		yield return LoadAgents(profile, existsBackup);
		RefreshAgentStatus();
		RefreshAnimalStatus();
		Debug.Log("ーーーーーキャラ読み込みーーーーー");
		yield return null;
		Simulator.EnabledSky = true;
		Simulator.EnviroSky.AssignAndStart(Player.Locomotor.gameObject, Player.CameraControl.CameraComponent);
		Simulator.DefaultEnviroZone.enabled = true;
		yield return null;
		Debug.Log("ーーーーー環境反映ーーーーー");
		AIProject.SaveData.Environment env = profile.Environment;
		Simulator.Now = env.Time.DateTime;
		Simulator.InitializeEnviroParticle();
		Simulator.RefreshWeather(env.Weather, forced: true);
		Simulator.SetTemperatureValue(env.TemperatureValue);
		Debug.Log("ーーーーー環境反映ーーーーー");
		yield return null;
		RefreshActiveTimeRelationObjects();
	}

	private void LoadPlayer(PlayerData playerInfo)
	{
		DefinePack definePack = Singleton<Resources>.Instance.DefinePack;
		string bundlePath = definePack.ABPaths.ActorPrefab;
		string manifest = definePack.ABManifests.Default;
		GameObject original = CommonLib.LoadAsset<GameObject>(bundlePath, "Player", clone: false, manifest);
		if (!MapScene.AssetBundlePaths.Exists((UnityEx.ValueTuple<string, string> x) => x.Item1 == bundlePath && x.Item2 == manifest))
		{
			MapScene.AssetBundlePaths.Add(new UnityEx.ValueTuple<string, string>(bundlePath, manifest));
		}
		GameObject gameObject = UnityEngine.Object.Instantiate(original);
		gameObject.transform.Initiate(ActorRoot);
		PlayerActor player = gameObject.GetComponent<PlayerActor>();
		player.ID = -99;
		Vector3 pos = playerInfo.Position;
		(from _ in Observable.EveryUpdate()
			where player.NavMeshAgent.enabled
			select _).TakeWhile((long _) => !player.NavMeshAgent.isOnNavMesh).Subscribe(delegate
		{
			player.NavMeshAgent.Warp(pos);
		});
		player.Rotation = playerInfo.Rotation;
		player.PlayerData = playerInfo;
		if (player.PlayerData.CharaFileNames.IsNullOrEmpty())
		{
			player.PlayerData.CharaFileNames[0] = "charaF_20170613163526688";
			player.PlayerData.CharaFileNames[1] = "charaF_20170613163526688";
		}
		player.Relocate();
		gameObject.SetActiveSafe(isActive: true);
		RegisterPlayer(player);
		RegisterActor(-99, player);
	}

	private IEnumerator LoadPlayerAsync(PlayerData playerInfo, bool existsBackup)
	{
		DefinePack define = Singleton<Resources>.Instance.DefinePack;
		string bundlePath = define.ABPaths.ActorPrefab;
		string manifest = define.ABManifests.Default;
		GameObject playPrefab = CommonLib.LoadAsset<GameObject>(bundlePath, "Player", clone: false, manifest);
		if (!MapScene.AssetBundlePaths.Exists((UnityEx.ValueTuple<string, string> x) => x.Item1 == bundlePath && x.Item2 == manifest))
		{
			MapScene.AssetBundlePaths.Add(new UnityEx.ValueTuple<string, string>(bundlePath, manifest));
		}
		GameObject obj = UnityEngine.Object.Instantiate(playPrefab);
		obj.transform.Initiate(ActorRoot);
		PlayerActor player = obj.GetComponent<PlayerActor>();
		player.ID = -99;
		bool addTutorialItems = false;
		if (existsBackup)
		{
			Vector3 pos = playerInfo.Position;
			player.NavMeshAgent.Warp(pos);
			(from _ in Observable.EveryUpdate()
				where player.NavMeshAgent.enabled
				select _).TakeWhile((long _) => !player.NavMeshAgent.isOnNavMesh).Subscribe(delegate
			{
				player.NavMeshAgent.Warp(pos);
			});
			player.Rotation = playerInfo.Rotation;
		}
		else
		{
			StoryPoint storyPoint = GetStoryPoint(0);
			if (TutorialMode && storyPoint != null)
			{
				player.MapArea = storyPoint.OwnerArea;
				Transform transform = storyPoint.transform.FindLoop("player_point")?.transform;
				PlayerActor playerActor = player;
				Vector3? vector = transform?.position;
				playerActor.Position = ((!vector.HasValue) ? storyPoint.Position : vector.Value);
				PlayerActor playerActor2 = player;
				Quaternion? quaternion = transform?.rotation;
				playerActor2.Rotation = ((!quaternion.HasValue) ? storyPoint.Rotation : quaternion.Value);
			}
			else
			{
				player.Position = PlayerStartPoint.position;
				player.Rotation = PlayerStartPoint.rotation;
				addTutorialItems = true;
			}
		}
		player.PlayerData = playerInfo;
		playerInfo.param.Bind(player);
		if (player.PlayerData.CharaFileNames.IsNullOrEmpty())
		{
			player.PlayerData.CharaFileNames[0] = "charaF_20170613163526688";
			player.PlayerData.CharaFileNames[1] = "charaF_20170613163526688";
		}
		player.Relocate();
		obj.SetActiveSafe(isActive: true);
		yield return player.LoadAsync();
		RegisterPlayer(player);
		RegisterActor(-99, player);
		if (addTutorialItems)
		{
			AddTutorialItems();
		}
	}

	private IEnumerator LoadAgents(WorldData profile, bool existsBackup)
	{
		DefinePack define = Singleton<Resources>.Instance.DefinePack;
		AgentProfile agentProfile = Singleton<Resources>.Instance.AgentProfile;
		string bundlePath = define.ABPaths.ActorPrefab;
		string manifest = define.ABManifests.Default;
		GameObject agentPrefab = CommonLib.LoadAsset<GameObject>(bundlePath, "Agent", clone: false, manifest);
		if (!MapScene.AssetBundlePaths.Exists((UnityEx.ValueTuple<string, string> x) => x.Item1 == bundlePath && x.Item2 == manifest))
		{
			MapScene.AssetBundlePaths.Add(new UnityEx.ValueTuple<string, string>(bundlePath, manifest));
		}
		int[] chunkKeys = ChunkTable.Keys.ToArray();
		int maxCharaNum = Config.GraphicData.MaxCharaNum;
		bool[] charasEnter = Config.GraphicData.CharasEntry;
		ChaFileControl chaFile = new ChaFileControl();
		bool isFreeMode = profile.FreeMode;
		bool isFirstGame = !existsBackup;
		for (int i = 0; i < profile.AgentTable.Count; i++)
		{
			AgentData data = profile.AgentTable[i];
			if (isFreeMode)
			{
				data.OpenState = true;
				data.PlayEnterScene = true;
			}
			if (!charasEnter[i] && (!TutorialMode || i != 0))
			{
				if (i == 0 && isFreeMode && isFirstGame)
				{
					FirstAgentDataPositionSetting(data);
				}
			}
			else
			{
				if (!data.OpenState || data.CharaFileName.IsNullOrEmpty() || data.MapID != MapID || !chaFile.LoadCharaFile(data.CharaFileName, 1))
				{
					continue;
				}
				GameObject agentObj = UnityEngine.Object.Instantiate(agentPrefab, ActorRoot);
				agentObj.name = string.Format("Heroine_{0}", i.ToString("00"));
				AgentActor agent = agentObj.GetComponent<AgentActor>();
				agent.ID = i;
				agent.AgentData = data;
				data.param.Bind(agent);
				RegisterActor(i, agent);
				RegisterAgent(i, agent);
				if (existsBackup)
				{
					if (!TutorialMode)
					{
						if (agentProfile.BlackListInSaveAndLoad.Contains(data.ModeType))
						{
							data.ModeType = Desire.ActionType.Normal;
						}
						if (data.ModeType == Desire.ActionType.Date || data.ModeType == Desire.ActionType.Onbu)
						{
							data.ModeType = Desire.ActionType.Normal;
						}
						if (data.PrevMapID.HasValue)
						{
							data.ModeType = Desire.ActionType.Normal;
						}
					}
					agent.Mode = data.ModeType;
					agent.PrevMode = data.PrevMode;
					if (i == 0 && TutorialMode)
					{
						AgentActor agentActor = agent;
						Desire.ActionType actionType2 = (data.ModeType = Desire.ActionType.Idle);
						actionType2 = actionType2;
						agent.PrevActionMode = actionType2;
						actionType2 = actionType2;
						agent.PrevMode = actionType2;
						agentActor.Mode = actionType2;
						agent.PrevActionMode = Desire.ActionType.Idle;
						agent.TutorialType = data.TutorialModeType;
						agent.TutorialMode = true;
						TutorialAgent = agent;
						agent.CreateTutorialBehaviorResources();
					}
					Vector3 pos;
					Quaternion rotation;
					if (data.PrevMapID.HasValue)
					{
						if (MapID == 0)
						{
							DevicePoint devicePoint = PointAgent.DevicePointDic[i];
							pos = devicePoint.RecoverPoints[0].position;
							rotation = devicePoint.RecoverPoints[0].rotation;
						}
						else
						{
							DevicePoint devicePoint2 = PointAgent.DevicePoints[0];
							pos = devicePoint2.RecoverPoints[i].position;
							rotation = devicePoint2.RecoverPoints[i].rotation;
						}
						data.PrevMapID = null;
					}
					else
					{
						pos = data.Position;
						rotation = data.Rotation;
					}
					agent.NavMeshAgent.Warp(pos);
					(from _ in Observable.EveryUpdate().TakeUntilDestroy(agent.gameObject)
						where agent.NavMeshAgent.enabled
						select _).TakeWhile((long _) => !agent.NavMeshAgent.isOnNavMesh).Subscribe(delegate
					{
						agent.NavMeshAgent.Warp(pos);
					});
					agent.Rotation = rotation;
					ChunkTable.TryGetValue(data.ChunkID, out var value);
					MapArea[] mapAreas = value.MapAreas;
					foreach (MapArea mapArea in mapAreas)
					{
						if (mapArea.AreaID == data.AreaID)
						{
							agent.MapArea = mapArea;
						}
					}
				}
				else
				{
					StoryPoint storyPoint = GetStoryPoint(0);
					if (TutorialMode && i == 0 && storyPoint != null)
					{
						AgentActor agentActor2 = agent;
						Desire.ActionType actionType2 = (data.ModeType = Desire.ActionType.Idle);
						actionType2 = actionType2;
						agent.PrevActionMode = actionType2;
						actionType2 = actionType2;
						agent.PrevMode = actionType2;
						agentActor2.Mode = actionType2;
						AgentActor agentActor3 = agent;
						AIProject.Definitions.Tutorial.ActionType tutorialType = (data.TutorialModeType = AIProject.Definitions.Tutorial.ActionType.Idle);
						agentActor3.TutorialType = tutorialType;
						agent.MapArea = storyPoint.OwnerArea;
						Transform transform = storyPoint.transform.FindLoop("heroine_point").transform;
						AgentActor agentActor4 = agent;
						Vector3? vector = transform?.position;
						agentActor4.Position = ((!vector.HasValue) ? (storyPoint.Position + Player.Rotation * storyPoint.Forward * 3f) : vector.Value);
						AgentActor agentActor5 = agent;
						Quaternion? quaternion = transform?.rotation;
						agentActor5.Rotation = ((!quaternion.HasValue) ? storyPoint.Rotation : quaternion.Value);
						agent.TutorialMode = true;
						TutorialAgent = agent;
						agent.CreateTutorialBehaviorResources();
					}
					else if (i == 0)
					{
						if (!FirstAgentPositionSetting(agent))
						{
							AgentPositionSetting(agent);
						}
					}
					else
					{
						AgentPositionSetting(agent);
					}
				}
				agent.Relocate();
				agentObj.SetActiveSafe(isActive: true);
				yield return agent.LoadAsync();
				if (!existsBackup)
				{
					agent.SetDefaultImmoral();
					foreach (KeyValuePair<int, float> item in agent.ChaControl.fileGameInfo.desireDefVal)
					{
						agent.AgentData.DesireTable[item.Key] = item.Value;
					}
				}
				yield return null;
			}
		}
		yield return null;
	}

	private bool FirstAgentPositionSetting(AgentActor agent)
	{
		StoryPoint storyPoint = GetStoryPoint(3);
		if (storyPoint == null || agent == null)
		{
			return false;
		}
		if (agent.NavMeshAgent.isActiveAndEnabled)
		{
			agent.NavMeshAgent.Warp(storyPoint.Position);
		}
		else
		{
			agent.Position = storyPoint.Position;
		}
		agent.Rotation = storyPoint.Rotation;
		agent.MapArea = storyPoint.OwnerArea;
		return true;
	}

	private bool FirstAgentDataPositionSetting(AgentData data)
	{
		if (data == null)
		{
			return false;
		}
		StoryPoint storyPoint = GetStoryPoint(3);
		if (storyPoint == null)
		{
			return false;
		}
		data.Position = storyPoint.Position;
		data.Rotation = storyPoint.Rotation;
		return true;
	}

	private void AgentPositionSetting(AgentActor agent)
	{
		int key = 0;
		ChunkTable.TryGetValue(key, out var value);
		List<Waypoint> waypoints = value.MapAreas[0].Waypoints;
		List<Waypoint> list = waypoints.FindAll(delegate(Waypoint x)
		{
			Vector3 position = Player.Position;
			float num = Vector3.Distance(x.transform.position, position);
			return num > 50f;
		});
		if (Debug.isDebugBuild)
		{
			Debug.Log($"近距離で除外されたウェイポイントの数 = {waypoints.Count - list.Count}");
		}
		Waypoint element = list.GetElement(UnityEngine.Random.Range(0, list.Count));
		agent.Position = element.transform.position;
		agent.MapArea = value.MapAreas[0];
	}

	public void LoadAgentTargetActionPoint()
	{
		foreach (KeyValuePair<int, AgentActor> agent in _agentTable)
		{
			ActionPoint actionPoint = PointAgent.ActionPoints.FirstOrDefault((ActionPoint x) => x.RegisterID == agent.Value.AgentData.CurrentActionPointID);
			if (actionPoint == null)
			{
				actionPoint = PointAgent.AppendActionPoints.Find((ActionPoint x) => x.RegisterID == agent.Value.AgentData.CurrentActionPointID);
			}
			if (actionPoint != null && !actionPoint.AgentEventType.Contains(AIProject.EventType.Move) && !actionPoint.AgentEventType.Contains(AIProject.EventType.DoorOpen))
			{
				agent.Value.TargetInSightActionPoint = actionPoint;
			}
		}
	}

	public void LoadAgentTargetActor()
	{
		foreach (KeyValuePair<int, AgentActor> item in _agentTable)
		{
			if (_actorTable.TryGetValue(item.Value.AgentData.ActionTargetID, out var value))
			{
				item.Value.TargetInSightActor = value;
			}
		}
	}

	public void RemovePlayer(PlayerActor player)
	{
		UnregisterActor(player.ID);
	}

	public AgentActor AddAgent(int id, AgentData agentData)
	{
		if (_agentPrefab == null)
		{
			DefinePack definePack = Singleton<Resources>.Instance.DefinePack;
			string bundlePath = definePack.ABPaths.ActorPrefab;
			string manifest = definePack.ABManifests.Default;
			_agentPrefab = CommonLib.LoadAsset<GameObject>(bundlePath, "Agent", clone: false, manifest);
			if (!MapScene.AssetBundlePaths.Exists((UnityEx.ValueTuple<string, string> x) => x.Item1 == bundlePath && x.Item2 == manifest))
			{
				MapScene.AssetBundlePaths.Add(new UnityEx.ValueTuple<string, string>(bundlePath, manifest));
			}
		}
		int[] array = ChunkTable.Keys.ToArray();
		GameObject gameObject = UnityEngine.Object.Instantiate(_agentPrefab, ActorRoot);
		gameObject.name = string.Format("Heroine_{0}", id.ToString("00"));
		AgentActor component = gameObject.GetComponent<AgentActor>();
		component.ID = id;
		component.AgentData = agentData;
		if (Singleton<Resources>.Instance.AgentProfile.DefaultAreaIDTable.TryGetValue(id, out var value))
		{
			int areaID = (agentData.AreaID = value);
			component.AreaID = areaID;
			foreach (KeyValuePair<int, Chunk> item in ChunkTable)
			{
				MapArea[] mapAreas = item.Value.MapAreas;
				foreach (MapArea mapArea in mapAreas)
				{
					if (mapArea.AreaID == value)
					{
						component.MapArea = mapArea;
					}
				}
			}
		}
		agentData.param.Bind(component);
		RegisterActor(id, component);
		RegisterAgent(id, component);
		if (Singleton<AnimalManager>.IsInstance())
		{
			Singleton<AnimalManager>.Instance.AddTargetAnimals(component);
		}
		component.Relocate();
		gameObject.SetActiveSafe(isActive: true);
		component.Load();
		return component;
	}

	public void RemoveAgent(AgentActor agent)
	{
		agent.DisableBehavior();
		agent.BehaviorResources.DisableAllBehaviors();
		foreach (KeyValuePair<int, Dictionary<int, UnityEx.ValueTuple<AudioSource, FadePlayer>>> item in agent.Animation.ActionLoopSETable)
		{
			foreach (KeyValuePair<int, UnityEx.ValueTuple<AudioSource, FadePlayer>> item2 in item.Value)
			{
				if (!(item2.Value.Item2 == null))
				{
					item2.Value.Item2.Stop(0f);
				}
			}
		}
		agent.Animation.ActionLoopSETable.Clear();
		Player.PlayerController.CommandArea.RemoveCommandableObject(agent);
		UnregisterAgent(agent.ID);
		UnregisterActor(agent.ID);
		foreach (KeyValuePair<int, AgentActor> item3 in AgentTable)
		{
			if (!(item3.Value == agent))
			{
				item3.Value.RemoveActor(agent);
			}
		}
		Singleton<Character>.Instance.DeleteChara(agent.ChaControl);
		UnityEngine.Object.Destroy(agent.gameObject);
	}

	private IEnumerator LoadMerchantAsync(MerchantData merchantData, bool existsBackup)
	{
		DefinePack define = Singleton<Resources>.Instance.DefinePack;
		string bundlePath = define.ABPaths.ActorPrefab;
		string manifest = define.ABManifests.Default;
		GameObject merchantPrefab = CommonLib.LoadAsset<GameObject>(bundlePath, "Merchant", clone: false, manifest);
		if (!MapScene.AssetBundlePaths.Exists((UnityEx.ValueTuple<string, string> x) => x.Item1 == bundlePath && x.Item2 == manifest))
		{
			MapScene.AssetBundlePaths.Add(new UnityEx.ValueTuple<string, string>(bundlePath, manifest));
		}
		GameObject obj = UnityEngine.Object.Instantiate(merchantPrefab, ActorRoot);
		obj.name = "Merchant";
		MerchantActor merchant = obj.GetComponent<MerchantActor>();
		merchant.ID = -90;
		if (merchantData == null)
		{
			merchantData = new MerchantData();
		}
		merchant.SetMerchantPoints(_pointAgent.MerchantPoints);
		merchant.OpenAreaID = 0;
		if (existsBackup)
		{
			merchant.DataUpdate(merchantData);
		}
		else if (merchant.StartPoint != null)
		{
			Transform basePoint = merchant.StartPoint.GetBasePoint(AIProject.Definitions.Merchant.EventType.Wait);
			merchant.Position = basePoint.position;
			merchant.Rotation = basePoint.rotation;
			merchant.TargetInSightMerchantPoint = merchant.StartPoint;
		}
		else
		{
			int[] source = ChunkTable.Keys.ToArray();
			int element = source.GetElement(0);
			ChunkTable.TryGetValue(element, out var value);
			List<Waypoint> waypoints = value.MapAreas[0].Waypoints;
			List<Waypoint> list = waypoints.FindAll(delegate(Waypoint x)
			{
				Vector3 position = Player.Position;
				float num = Vector3.Distance(x.transform.position, position);
				return 50f < num;
			});
			Waypoint element2 = list.GetElement(UnityEngine.Random.Range(0, list.Count));
			merchant.Position = element2.transform.position;
		}
		merchant.Relocate();
		merchant.SetActiveSafe(isActive: true);
		merchant.MerchantData = merchantData;
		merchantData.param.Bind(merchant);
		yield return merchant.LoadAsync();
		merchant.SetOpenAreaID(this);
		if (!merchant.MerchantData.Unlock && !TutorialMode)
		{
			merchant.MerchantData.Unlock = true;
		}
		RegisterMerchant(merchant);
		RegisterActor(-90, merchant);
		if (!existsBackup)
		{
			yield break;
		}
		merchant.ChangeMap(MapID);
		if (MapID == 0 && merchantData.ModeType == AIProject.Definitions.Merchant.ActionType.Absent)
		{
			merchant.Hide();
			if (merchant.ExitPoint != null)
			{
				merchant.ExitPoint.HideItemObjects();
			}
		}
	}

	public IEnumerator LoadAnimals(WorldData profile, bool existsBackup)
	{
		if (_pointAgent == null || profile == null || !Singleton<AnimalManager>.IsInstance())
		{
			yield break;
		}
		AIProject.SaveData.Environment envData = profile.Environment;
		if (envData == null)
		{
			yield break;
		}
		yield return null;
		Dictionary<int, AIProject.SaveData.Environment.PetHomeInfo> homePetDataTable = envData.PetHomeStateTable;
		IReadOnlyDictionary<int, PetHomePoint> petPointTable = _pointAgent.PetHomePointTable;
		if (homePetDataTable.IsNullOrEmpty() && petPointTable != null && petPointTable.Count != 0)
		{
			yield break;
		}
		foreach (KeyValuePair<int, AIProject.SaveData.Environment.PetHomeInfo> pair in homePetDataTable)
		{
			AIProject.SaveData.AnimalData data = pair.Value?.AnimalData;
			if (data == null)
			{
				continue;
			}
			int registerID = pair.Key;
			if (!petPointTable.TryGetValue(registerID, out var habitatPoint) || habitatPoint == null)
			{
				continue;
			}
			IPetAnimal petAnimal = null;
			int rootPointID = 0;
			if (!data.InitAnimalTypeID || data.AnimalTypeID < 0)
			{
				data.AnimalTypeID = AIProject.Animal.AnimalData.GetAnimalTypeID(data.AnimalType);
				data.InitAnimalTypeID = true;
			}
			switch (habitatPoint.Kind)
			{
			case PetHomePoint.HomeKind.PetMat:
				petAnimal = Singleton<AnimalManager>.Instance.CreateBase(data.AnimalTypeID, (int)data.BreedingType) as IPetAnimal;
				break;
			case PetHomePoint.HomeKind.FishTank:
			{
				Dictionary<int, Dictionary<int, FishInfo>> dictionary = ((!Singleton<Resources>.IsInstance()) ? null : Singleton<Resources>.Instance.Fishing.FishInfoTable);
				if (!dictionary.IsNullOrEmpty() && dictionary.TryGetValue(data.ItemCategoryID, out var value) && !value.IsNullOrEmpty() && value.TryGetValue(data.ItemID, out var value2))
				{
					petAnimal = Singleton<AnimalManager>.Instance.CreateBase(data.AnimalTypeID, (int)data.BreedingType) as IPetAnimal;
					rootPointID = value2.TankPointID;
				}
				break;
			}
			}
			if (petAnimal != null)
			{
				data.AnimalID = petAnimal.AnimalID;
				petAnimal.AnimalData = data;
				IGroundPet groundPet = petAnimal as IGroundPet;
				if (groundPet != null)
				{
					groundPet.ChaseActor = pair.Value.ChaseActor;
				}
				habitatPoint.SetUser(petAnimal);
				habitatPoint.SetRootPoint(rootPointID, petAnimal);
				if (groundPet != null && groundPet.ChaseActor)
				{
					groundPet.Position = data.Position;
					groundPet.Rotation = data.Rotation;
				}
				yield return null;
			}
		}
	}

	private void AddTutorialItems()
	{
		List<StuffItem> itemList = Player.PlayerData.ItemList;
		FishingDefinePack.ItemIDPair fishingRodID = Singleton<Resources>.Instance.FishingDefinePack.IDInfo.FishingRod;
		if (!itemList.Exists((StuffItem x) => x.CategoryID == fishingRodID.CategoryID && x.ID == fishingRodID.ItemID && 0 < x.Count))
		{
			itemList.AddItem(new StuffItem(fishingRodID.CategoryID, fishingRodID.ItemID, 1));
		}
	}

	public void CreateTutorialLockArea()
	{
		string mapScenePrefab = Singleton<Resources>.Instance.DefinePack.ABPaths.MapScenePrefab;
		string assetName = "p_ai_mi_tutorialmesh00";
		GameObject original = CommonLib.LoadAsset<GameObject>(mapScenePrefab, assetName, clone: false, string.Empty);
		GameObject gameObject = UnityEngine.Object.Instantiate(original);
		gameObject.transform.SetParent(_mapRoot, worldPositionStays: false);
		gameObject.transform.localPosition = Vector3.zero;
		gameObject.transform.localRotation = Quaternion.identity;
		gameObject.transform.localScale = Vector3.one;
		TutorialLockAreaObject = gameObject;
		MapScene.AddAssetBundlePath(mapScenePrefab, string.Empty);
	}

	public void DestroyTutorialLockArea()
	{
		if (TutorialLockAreaObject != null)
		{
			UnityEngine.Object.Destroy(TutorialLockAreaObject);
			TutorialLockAreaObject = null;
		}
	}

	public void CreateTutorialSearchPoint()
	{
		if (!(_tutorialSearchPointObject != null))
		{
			string mapScenePrefab = Singleton<Resources>.Instance.DefinePack.ABPaths.MapScenePrefab;
			string assetName = "TutorialSearchPoint_00";
			string @default = Singleton<Resources>.Instance.DefinePack.ABManifests.Default;
			GameObject gameObject = CommonLib.LoadAsset<GameObject>(mapScenePrefab, assetName, clone: false, @default);
			if (!(gameObject == null))
			{
				GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject);
				gameObject2.transform.SetParent(_mapRoot, worldPositionStays: false);
				_tutorialSearchPointObject = gameObject2;
				MapScene.AddAssetBundlePath(mapScenePrefab, @default);
			}
		}
	}

	public void DestroyTutorialSearchPoint()
	{
		if (_tutorialSearchPointObject != null)
		{
			UnityEngine.Object.Destroy(_tutorialSearchPointObject);
			_tutorialSearchPointObject = null;
		}
	}

	public void CreateStoryPointEffect()
	{
		if (StoryPointEffect != null)
		{
			return;
		}
		string mapScenePrefab = Singleton<Resources>.Instance.DefinePack.ABPaths.MapScenePrefab;
		string assetName = "StoryPointEffect";
		string @default = Singleton<Resources>.Instance.DefinePack.ABManifests.Default;
		GameObject gameObject = CommonLib.LoadAsset<GameObject>(mapScenePrefab, assetName, clone: false, @default);
		if (!(gameObject == null))
		{
			GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject);
			gameObject2.transform.SetParent(_pointAgent.transform, worldPositionStays: false);
			StoryPointEffect = gameObject2.GetComponent<StoryPointEffect>();
			if (StoryPointEffect == null)
			{
				UnityEngine.Object.Destroy(gameObject2);
			}
			else
			{
				StoryPointEffect.Switch = true;
			}
			MapScene.AddAssetBundlePath(mapScenePrefab, @default);
		}
	}

	public void DestroyStoryPointEffect()
	{
		if (!(StoryPointEffect == null))
		{
			UnityEngine.Object.Destroy(StoryPointEffect.gameObject);
			StoryPointEffect = null;
		}
	}

	public void RegisterPlayer(PlayerActor player)
	{
		if (!(Player == player))
		{
			Player = player;
		}
	}

	public void RegisterActor(int id, Actor actor)
	{
		if (_actorTable.TryGetValue(id, out var _))
		{
		}
		_actorTable[id] = actor;
		if (!Actors.Contains(actor))
		{
			Actors.Add(actor);
		}
	}

	public void UnregisterActor(int id)
	{
		if (_actorTable.ContainsKey(id))
		{
			_actorTable.Remove(id);
		}
	}

	public void RegisterAgent(int id, AgentActor agent)
	{
		if (_agentTable.TryGetValue(id, out var _))
		{
		}
		_agentTable[id] = agent;
		if (!_agentKeys.Contains(id))
		{
			_agentKeys.Add(id);
		}
	}

	public void UnregisterAgent(int id)
	{
		if (_agentKeys.Contains(id))
		{
			_agentKeys.Remove(id);
		}
		if (_agentTable.ContainsKey(id))
		{
			_agentTable.Remove(id);
		}
	}

	public void RegisterMerchant(MerchantActor merchant)
	{
		if (!(Merchant == merchant))
		{
			Merchant = merchant;
		}
	}

	public void ApplyConfig(System.Action onCompleteIn, System.Action onCompleteOut)
	{
		if (TutorialMode)
		{
			onCompleteOut?.Invoke();
			return;
		}
		MapUIContainer.StartFade(FadeCanvas.PanelType.Blackout, FadeType.In, 1f, ignoreTimeScale: true).Subscribe((Action<Unit>)delegate
		{
		}, (System.Action)delegate
		{
			onCompleteIn?.Invoke();
			bool[] charasEntry = Config.GraphicData.CharasEntry;
			Dictionary<int, AgentData> agentTable = Singleton<Game>.Instance.WorldData.AgentTable;
			ReadOnlyDictionary<int, AgentActor> agentTable2 = Singleton<Map>.Instance.AgentTable;
			ChaFileControl chaFileControl = new ChaFileControl();
			foreach (KeyValuePair<int, AgentData> item in Singleton<Game>.Instance.WorldData.AgentTable)
			{
				string charaFileName = item.Value.CharaFileName;
				AgentActor value;
				if (charasEntry[item.Key] && !charaFileName.IsNullOrEmpty() && chaFileControl.LoadCharaFile(charaFileName, 1) && item.Value.MapID == MapID)
				{
					if (!agentTable2.ContainsKey(item.Key))
					{
						AgentActor agentActor = Singleton<Map>.Instance.AddAgent(item.Key, item.Value);
						agentActor.RefreshWalkStatus(PointAgent.Waypoints);
						Singleton<Map>.Instance.InitSearchActorTargets(agentActor);
						Player.PlayerController.CommandArea.AddCommandableObject(agentActor);
						foreach (KeyValuePair<int, AgentActor> item2 in Singleton<Map>.Instance.AgentTable)
						{
							if (!(item2.Value == agentActor))
							{
								item2.Value.AddActor(agentActor);
							}
						}
						agentActor.ActivateNavMeshAgent();
						Transform transform = ((MapID != 0) ? PointAgent.DevicePointDic[0].RecoverPoints[item.Key].transform : PointAgent.DevicePointDic[item.Key].RecoverPoints[0].transform);
						agentActor.NavMeshAgent.Warp(transform.position);
						agentActor.Rotation = transform.rotation;
						agentActor.EnableBehavior();
						agentActor.ChangeBehavior(Desire.ActionType.Normal);
					}
				}
				else if (agentTable2.TryGetValue(item.Key, out value))
				{
					value.DisableBehavior();
					value.ClearItems();
					value.ClearParticles();
					Actor.BehaviorSchedule schedule = value.Schedule;
					schedule.enabled = false;
					value.Schedule = schedule;
					value.TargetInSightActor = null;
					if (value.CurrentPoint != null)
					{
						value.CurrentPoint.SetActiveMapItemObjs(active: true);
						value.CurrentPoint.ReleaseSlot(value);
						value.CurrentPoint = null;
					}
					value.TargetInSightActionPoint = null;
					if (value.Partner != null)
					{
						if (value.Partner is PlayerActor)
						{
							PlayerActor playerActor = value.Partner as PlayerActor;
							value.ActivateHoldingHands(0, enabled: false);
							if (playerActor.PlayerController.State is Normal || playerActor.PlayerController.State is Houchi || playerActor.PlayerController.State is Onbu)
							{
								playerActor.PlayerController.ChangeState("Normal");
								playerActor.Mode = Desire.ActionType.Normal;
								playerActor.Partner = null;
								playerActor.ActivateTransfer();
							}
							else if (playerActor.PlayerController.State is Menu)
							{
								playerActor.PlayerController.ChangeState("Menu");
								playerActor.PlayerController.PrevStateName = "Normal";
								playerActor.Mode = Desire.ActionType.Normal;
								playerActor.Partner = null;
								playerActor.ActivateTransfer();
							}
						}
						else if (value.Partner is AgentActor)
						{
							AgentActor agentActor2 = value.Partner as AgentActor;
							value.StopLesbianSequence();
							agentActor2.StopLesbianSequence();
							agentActor2.Animation.EndIgnoreEvent();
							Singleton<Game>.Instance.GetExpression(agentActor2.ChaControl.fileParam.personality, "標準")?.Change(agentActor2.ChaControl);
							agentActor2.Animation.ResetDefaultAnimatorController();
							agentActor2.ChangeBehavior(Desire.ActionType.Normal);
						}
						else if (value.Partner is MerchantActor)
						{
							MerchantActor merchantActor = value.Partner as MerchantActor;
							value.StopLesbianSequence();
							merchantActor.ResetState();
							merchantActor.ChangeBehavior(merchantActor.LastNormalMode);
						}
						value.Partner = null;
					}
					if (value.CommandPartner != null)
					{
						if (value.CommandPartner is AgentActor)
						{
							AgentActor agentActor3 = value.CommandPartner as AgentActor;
							agentActor3.ChangeBehavior(Desire.ActionType.Normal);
						}
						else if (value.CommandPartner is MerchantActor)
						{
							MerchantActor merchantActor2 = value.CommandPartner as MerchantActor;
							merchantActor2.ChangeBehavior(merchantActor2.LastNormalMode);
						}
						value.CommandPartner = null;
					}
					value.ChaControl.chaFile.SaveCharaFile(value.ChaControl.chaFile.charaFileName);
					RemoveAgent(value);
				}
			}
			MapUIContainer.StartFade(FadeCanvas.PanelType.Blackout, FadeType.Out, 1f, ignoreTimeScale: true).Subscribe((Action<Unit>)delegate
			{
			}, (System.Action)delegate
			{
				onCompleteOut?.Invoke();
			});
		});
	}

	public void ReleaseComponents()
	{
		if (Player != null)
		{
			UnityEngine.Object.Destroy(Player.gameObject);
			Player = null;
		}
		foreach (KeyValuePair<int, AgentActor> item in _agentTable)
		{
			if (!(item.Value == null))
			{
				UnityEngine.Object.Destroy(item.Value.gameObject);
			}
		}
		_agentTable.Clear();
		if (Merchant != null)
		{
			UnityEngine.Object.Destroy(Merchant.gameObject);
			Merchant = null;
		}
		_actorTable.Clear();
		_agentKeys.Clear();
		if (Singleton<Character>.IsInstance())
		{
			Singleton<Character>.Instance.DeleteCharaAll();
			Singleton<Character>.Instance.EndLoadAssetBundle();
		}
		if (Singleton<Housing>.IsInstance())
		{
			Singleton<Housing>.Instance.Release();
		}
		HousingPointTable.Clear();
		ChunkTable.Clear();
	}

	private void ReleaseAgents()
	{
		int[] array = _agentKeys.ToArray();
		int[] array2 = array;
		foreach (int key in array2)
		{
			if (_agentTable.TryGetValue(key, out var value))
			{
				RemoveAgent(value);
			}
		}
	}

	private void ReleaseMap()
	{
		Singleton<Housing>.Instance.DeleteRoot();
		Singleton<Housing>.Instance.Release();
		HousingPointTable.Clear();
		if (NavMeshSrc != null)
		{
			UnityEngine.Object.Destroy(NavMeshSrc.gameObject);
			NavMeshSrc = null;
		}
		if (ChunkSrc != null)
		{
			UnityEngine.Object.Destroy(ChunkSrc.gameObject);
			ChunkSrc = null;
		}
		_pointAgent.Release();
	}

	public void InitSearchActorTargetsAll()
	{
		foreach (KeyValuePair<int, AgentActor> item in AgentTable)
		{
			InitSearchActorTargets(item.Value);
		}
	}

	public void InitSearchActorTargets(AgentActor agent)
	{
		agent.AddActor(Player);
		agent.AddActor(Merchant);
		foreach (KeyValuePair<int, AgentActor> item in AgentTable)
		{
			if (!(item.Value == agent))
			{
				agent.AddActor(item.Value);
			}
		}
	}

	private void AreaTypeUpdate(Actor actor)
	{
		if (!(actor == null))
		{
			LayerMask roofLayer = Singleton<Resources>.Instance.DefinePack.MapDefines.RoofLayer;
			actor.AreaType = (Physics.Raycast(actor.Position, Vector3.up, out var _, 1000f, roofLayer) ? MapArea.AreaType.Indoor : MapArea.AreaType.Normal);
		}
	}

	private void OnElapsedDay(TimeSpan timeSpan)
	{
		if (Merchant != null)
		{
			Merchant.OnDayUpdated(timeSpan);
		}
		List<int> agentKeys = _agentKeys;
		foreach (int item in agentKeys)
		{
			if (_agentTable.TryGetValue(item, out var value))
			{
				value.OnDayUpdated(timeSpan);
			}
		}
	}

	private void OnElapsedMinute(TimeSpan timeSpan)
	{
		Player.OnMinuteUpdated(timeSpan);
		if (Merchant != null)
		{
			Merchant.OnMinuteUpdated(timeSpan);
		}
		List<int> agentKeys = _agentKeys;
		foreach (int item in agentKeys)
		{
			if (_agentTable.TryGetValue(item, out var value))
			{
				value.OnMinuteUpdated(timeSpan);
			}
		}
	}

	private void OnElapsedSecond(TimeSpan timeSpan)
	{
		List<int> agentKeys = _agentKeys;
		foreach (int item in agentKeys)
		{
			if (_agentTable.TryGetValue(item, out var value))
			{
				value.OnSecondUpdated(timeSpan);
			}
		}
	}

	public void RefreshWeather(Weather weather)
	{
		foreach (KeyValuePair<int, AgentActor> item in _agentTable)
		{
			AgentActor value = item.Value;
			RefreshAgentLocomotionStatus(value);
		}
	}

	public void RefreshPlayerLocomotionStatus()
	{
	}

	public void RefreshAgentLocomotionStatus(AgentActor agent)
	{
		if (!agent.NavMeshAgent.enabled || agent.NavMeshAgent.isStopped)
		{
			return;
		}
		bool flag = false;
		AnimatorStateInfo currentAnimatorStateInfo = agent.Animation.Animator.GetCurrentAnimatorStateInfo(0);
		foreach (KeyValuePair<int, PlayState> item in Singleton<Resources>.Instance.Animation.AgentLocomotionStateTable)
		{
			if (flag)
			{
				continue;
			}
			PlayState.Info[] stateInfos = item.Value.MainStateInfo.InStateInfo.StateInfos;
			for (int i = 0; i < stateInfos.Length; i++)
			{
				PlayState.Info info = stateInfos[i];
				if (!flag && currentAnimatorStateInfo.IsName(info.stateName))
				{
					flag = true;
					agent.ActivateTransfer();
					agent.AbortCalc();
					agent.AbortPatrol();
					agent.ClearWalkPath();
					agent.StartPatrol();
				}
			}
		}
	}

	public void RefreshTimeZone(AIProject.TimeZone timeZone)
	{
		int[] array = AgentTable.Keys.ToArray();
		int[] array2 = array;
		foreach (int key in array2)
		{
			if (!AgentTable.TryGetValue(key, out var value))
			{
				continue;
			}
			AgentData agentData = value.AgentData;
			if (!agentData.SickState.Enabled && agentData.SickState.UsedMedicine)
			{
				agentData.SickState.UsedMedicine = false;
			}
			if (!agentData.StatsTable.TryGetValue(1, out var value2))
			{
				float num = 50f;
				agentData.StatsTable[1] = num;
				value2 = num;
			}
			Dictionary<int, Threshold> value3;
			Threshold value4;
			if (value.ChaControl.fileGameInfo.phase < 3)
			{
				agentData.StatsTable[5] = Singleton<Resources>.Instance.DefinePack.MapDefines.DefaultMotivation + value.ChaControl.fileGameInfo.motivation;
			}
			else if (Singleton<Resources>.Instance.Action.PersonalityMotivation.TryGetValue(value.ChaControl.fileParam.personality, out value3) && value3.TryGetValue(value.ChaControl.fileGameInfo.phase, out value4))
			{
				float num2 = value4.Lerp(value2) + (float)value.ChaControl.fileGameInfo.motivation;
				if (value.ChaControl.fileGameInfo.normalSkill.ContainsValue(9))
				{
					num2 += Singleton<Resources>.Instance.StatusProfile.GWifeMotivationBuff;
				}
				if (value.ChaControl.fileGameInfo.normalSkill.ContainsValue(33))
				{
					num2 += Singleton<Resources>.Instance.StatusProfile.ActiveBuffMotivation;
				}
				agentData.StatsTable[5] = num2;
			}
			if (timeZone == AIProject.TimeZone.Morning)
			{
				agentData.Greeted = false;
			}
			RefreshAgentLocomotionStatus(value);
		}
	}

	private IEnumerator RefreshAsync()
	{
		Waypoint[] waypoints = _pointAgent.Waypoints;
		BasePoint[] basePoints = _pointAgent.BasePoints;
		DevicePoint[] devicePoints = _pointAgent.DevicePoints;
		ActionPoint[] actionPoints = _pointAgent.ActionPoints;
		FarmPoint[] farmPoints = _pointAgent.FarmPoints;
		ShipPoint[] shipPoints = _pointAgent.ShipPoints;
		MerchantPoint[] merchantPoints = _pointAgent.MerchantPoints;
		EventPoint[] eventPoints = _pointAgent.EventPoints;
		StoryPoint[] storyPoints = _pointAgent.StoryPoints;
		AnimalActionPoint[] animalActionPoints = _pointAgent.AnimalActionPoints;
		List<IConnectableObservable<Unit>> streams = ListPool<IConnectableObservable<Unit>>.Get();
		int[] chunkKeys = ChunkTable.Keys.ToArray();
		int[] array = chunkKeys;
		foreach (int key in array)
		{
			if (ChunkTable.TryGetValue(key, out var chunk))
			{
				IConnectableObservable<Unit> connectableObservable = Observable.FromCoroutine((CancellationToken _) => chunk.Load(waypoints, basePoints, devicePoints, farmPoints, shipPoints, actionPoints, merchantPoints, eventPoints, storyPoints, animalActionPoints)).PublishLast();
				connectableObservable.Connect();
				streams.Add(connectableObservable);
			}
		}
		yield return streams.WhenAll().ToYieldInstruction();
		ListPool<IConnectableObservable<Unit>>.Release(streams);
		int ct = 0;
		Waypoint[] array2 = waypoints;
		foreach (Waypoint point8 in array2)
		{
			if (!(point8.OwnerArea != null))
			{
				point8.SetActiveSafe(isActive: false);
				if (ct++ > 16)
				{
					ct %= 16;
					yield return null;
				}
			}
		}
		int sleepCount2 = 0;
		BasePoint[] array3 = basePoints;
		foreach (BasePoint point7 in array3)
		{
			if (!(point7.OwnerArea != null))
			{
				point7.SetActiveSafe(isActive: false);
				if (sleepCount2++ > 100)
				{
					sleepCount2 = 0;
					yield return null;
				}
			}
		}
		ActionPoint[] array4 = actionPoints;
		foreach (ActionPoint point6 in array4)
		{
			if (!(point6.OwnerArea != null))
			{
				point6.SetActiveSafe(isActive: false);
				if (sleepCount2++ > 100)
				{
					sleepCount2 = 0;
					yield return null;
				}
			}
		}
		MerchantPoint[] array5 = merchantPoints;
		foreach (MerchantPoint point5 in array5)
		{
			if (!(point5.OwnerArea != null))
			{
				point5.SetActiveSafe(isActive: false);
				if (sleepCount2++ > 100)
				{
					sleepCount2 = 0;
					yield return null;
				}
			}
		}
		EventPoint[] array6 = eventPoints;
		foreach (EventPoint point4 in array6)
		{
			if (!(point4.OwnerArea != null))
			{
				point4.SetActiveSafe(isActive: false);
				if (sleepCount2++ > 100)
				{
					sleepCount2 = 0;
					yield return null;
				}
			}
		}
		StoryPoint[] array7 = storyPoints;
		foreach (StoryPoint point3 in array7)
		{
			if (!(point3.OwnerArea != null))
			{
				point3.SetActiveSafe(isActive: false);
				if (sleepCount2++ > 100)
				{
					sleepCount2 = 0;
					yield return null;
				}
			}
		}
		AnimalActionPoint[] array8 = animalActionPoints;
		foreach (AnimalActionPoint point2 in array8)
		{
			if (!(point2.OwnerArea != null))
			{
				point2.SetActiveSafe(isActive: false);
				if (sleepCount2++ > 100)
				{
					sleepCount2 = 0;
					yield return null;
				}
			}
		}
		yield return null;
		sleepCount2 = 0;
		Waypoint[] array9 = waypoints;
		foreach (Waypoint point in array9)
		{
			if (!(point.OwnerArea == null))
			{
				point.RefreshExistence();
				point.RefilterToActionPoint(actionPoints);
				point.RefilterToActionPoint(merchantPoints);
				point.RefilterToActionPoint(eventPoints);
				point.RefilterToActionPoint(animalActionPoints);
				if (sleepCount2++ > 100)
				{
					sleepCount2 = 0;
					yield return null;
				}
			}
		}
	}

	public void InitActionPoints()
	{
		ActionPoint[] actionPoints = _pointAgent.ActionPoints;
		ActionPoint[] array = actionPoints;
		foreach (ActionPoint actionPoint in array)
		{
			actionPoint.Init();
		}
	}

	private void RefreshAgentStatus()
	{
		Waypoint[] waypoints = _pointAgent.Waypoints;
		foreach (AgentActor value in _agentTable.Values)
		{
			value.RefreshWalkStatus(waypoints);
		}
	}

	private void RefreshAnimalStatus()
	{
		if (Singleton<AnimalManager>.IsInstance())
		{
			Singleton<AnimalManager>.Instance.RefreshStates(this);
		}
	}

	public void RefreshActiveTimeRelationObjects()
	{
		if (Simulator == null)
		{
			return;
		}
		string text = "_EmissionColor";
		string keyword = "_EMISSION";
		AIProject.TimeZone mapLightTimeZone = Simulator.MapLightTimeZone;
		Dictionary<int, bool> dictionary = null;
		if (Singleton<Game>.IsInstance())
		{
			dictionary = Singleton<Game>.Instance.Environment?.TimeObjOpenState;
		}
		int num = mapLightTimeZone switch
		{
			AIProject.TimeZone.Morning => 0, 
			AIProject.TimeZone.Day => 1, 
			AIProject.TimeZone.Night => 2, 
			_ => -1, 
		};
		Dictionary<int, GameObject> dictionary2 = DictionaryPool<int, GameObject>.Get();
		Dictionary<int, GameObject> dictionary3 = DictionaryPool<int, GameObject>.Get();
		foreach (KeyValuePair<int, Dictionary<int, Dictionary<bool, Dictionary<int, UnityEx.ValueTuple<GameObject, Material, float, Color>[]>>>> item5 in TimeRelationObjectTable)
		{
			if (item5.Value.IsNullOrEmpty())
			{
				continue;
			}
			int key = item5.Key;
			foreach (KeyValuePair<int, Dictionary<bool, Dictionary<int, UnityEx.ValueTuple<GameObject, Material, float, Color>[]>>> item6 in item5.Value)
			{
				if (item6.Value.IsNullOrEmpty())
				{
					continue;
				}
				int key2 = item6.Key;
				foreach (KeyValuePair<bool, Dictionary<int, UnityEx.ValueTuple<GameObject, Material, float, Color>[]>> item7 in item6.Value)
				{
					if (item7.Value.IsNullOrEmpty())
					{
						continue;
					}
					bool key3 = item7.Key;
					foreach (KeyValuePair<int, UnityEx.ValueTuple<GameObject, Material, float, Color>[]> item8 in item7.Value)
					{
						if (item8.Value.IsNullOrEmpty())
						{
							continue;
						}
						int key4 = item8.Key;
						bool flag = key == num;
						if (key3 && flag)
						{
							bool value;
							if (dictionary == null)
							{
								flag = false;
							}
							else if (!dictionary.TryGetValue(key4, out value))
							{
								value = false;
							}
							else
							{
								flag = value;
							}
						}
						switch (key2)
						{
						case 0:
						{
							UnityEx.ValueTuple<GameObject, Material, float, Color>[] value3 = item8.Value;
							for (int j = 0; j < value3.Length; j++)
							{
								UnityEx.ValueTuple<GameObject, Material, float, Color> valueTuple2 = value3[j];
								if (valueTuple2.Item1 == null)
								{
									continue;
								}
								int instanceID2 = valueTuple2.Item1.GetInstanceID();
								if (flag)
								{
									if (dictionary3.ContainsKey(instanceID2))
									{
										dictionary3.Remove(instanceID2);
									}
									if (!dictionary2.ContainsKey(instanceID2))
									{
										dictionary2[instanceID2] = valueTuple2.Item1;
									}
								}
								else if (!dictionary2.ContainsKey(instanceID2) && !dictionary3.ContainsKey(instanceID2))
								{
									dictionary3[instanceID2] = valueTuple2.Item1;
								}
							}
							break;
						}
						case 1:
						{
							if (key != num)
							{
								break;
							}
							UnityEx.ValueTuple<GameObject, Material, float, Color>[] value2 = item8.Value;
							for (int i = 0; i < value2.Length; i++)
							{
								UnityEx.ValueTuple<GameObject, Material, float, Color> valueTuple = value2[i];
								if (valueTuple.Item1 == null || valueTuple.Item2 == null)
								{
									continue;
								}
								GameObject item = valueTuple.Item1;
								Material item2 = valueTuple.Item2;
								int instanceID = valueTuple.Item1.GetInstanceID();
								if (!valueTuple.Item2.HasProperty(text))
								{
									continue;
								}
								if (flag)
								{
									Color item3 = valueTuple.Item4;
									float item4 = valueTuple.Item3;
									item4 = Mathf.Sign(item4) * Mathf.Pow(item4, 1.5f);
									Color color = Color.white * item4 + item3;
									if (0f <= color.r && 0f <= color.g && 0f <= color.b)
									{
										if (!item2.IsKeywordEnabled(keyword))
										{
											item2.EnableKeyword(keyword);
										}
										item2.SetVector(text, color);
									}
									else if (item2.IsKeywordEnabled(keyword))
									{
										item2.DisableKeyword(keyword);
									}
								}
								else if (item2.IsKeywordEnabled(keyword))
								{
									item2.DisableKeyword(keyword);
								}
							}
							break;
						}
						}
					}
				}
			}
		}
		foreach (KeyValuePair<int, GameObject> item9 in dictionary2)
		{
			if (item9.Value != null && !item9.Value.activeSelf)
			{
				item9.Value.SetActive(value: true);
			}
		}
		foreach (KeyValuePair<int, GameObject> item10 in dictionary3)
		{
			if (item10.Value != null && item10.Value.activeSelf)
			{
				item10.Value.SetActive(value: false);
			}
		}
		DictionaryPool<int, GameObject>.Release(dictionary2);
		DictionaryPool<int, GameObject>.Release(dictionary3);
		if (TimeLinkedLightObjectList.IsNullOrEmpty())
		{
			return;
		}
		List<TimeLinkedLightObject> timeLinkedLightObjectList = TimeLinkedLightObjectList;
		timeLinkedLightObjectList.RemoveAll((TimeLinkedLightObject x) => x == null);
		foreach (TimeLinkedLightObject item11 in timeLinkedLightObjectList)
		{
			item11.Refresh(mapLightTimeZone);
		}
	}

	public void SetActiveMapEffect(bool active)
	{
		if (!(_mapDataEffect == null) && _mapDataEffect.activeSelf != active)
		{
			_mapDataEffect.SetActive(active);
		}
	}

	public static PlayerActor GetPlayer()
	{
		if (!Singleton<Map>.IsInstance())
		{
			return null;
		}
		Map map = Singleton<Map>.Instance;
		PlayerActor player = map.Player;
		return (!(player != null)) ? null : player;
	}

	public static MerchantActor GetMerchant()
	{
		if (!Singleton<Map>.IsInstance())
		{
			return null;
		}
		Map map = Singleton<Map>.Instance;
		MerchantActor merchant = map.Merchant;
		return (!(merchant != null)) ? null : merchant;
	}

	public static Camera GetCameraComponent()
	{
		if (!Singleton<Map>.IsInstance())
		{
			return null;
		}
		Map map = Singleton<Map>.Instance;
		PlayerActor player = map.Player;
		if (player == null)
		{
			return null;
		}
		ActorCameraControl cameraControl = player.CameraControl;
		if (cameraControl == null)
		{
			return null;
		}
		Camera cameraComponent = cameraControl.CameraComponent;
		return (!(cameraComponent != null)) ? null : cameraComponent;
	}

	public static Camera GetCameraComponent(PlayerActor player)
	{
		if (player == null)
		{
			return null;
		}
		ActorCameraControl cameraControl = player.CameraControl;
		if (cameraControl == null)
		{
			return null;
		}
		Camera cameraComponent = cameraControl.CameraComponent;
		return (!(cameraComponent != null)) ? null : cameraComponent;
	}

	public static CommandArea GetCommandArea()
	{
		if (!Singleton<Map>.IsInstance())
		{
			return null;
		}
		Map map = Singleton<Map>.Instance;
		PlayerActor player = map.Player;
		if (player == null)
		{
			return null;
		}
		PlayerController playerController = player.PlayerController;
		if (playerController == null)
		{
			return null;
		}
		CommandArea commandArea = playerController.CommandArea;
		return (!(commandArea != null)) ? null : commandArea;
	}

	public static CommandArea GetCommandArea(PlayerActor player)
	{
		if (player == null)
		{
			return null;
		}
		PlayerController playerController = player.PlayerController;
		if (playerController == null)
		{
			return null;
		}
		CommandArea commandArea = playerController.CommandArea;
		return (!(commandArea != null)) ? null : commandArea;
	}

	public static ActorCameraControl GetCameraControl()
	{
		if (!Singleton<Map>.IsInstance())
		{
			return null;
		}
		Map map = Singleton<Map>.Instance;
		PlayerActor player = map.Player;
		if (player == null)
		{
			return null;
		}
		ActorCameraControl cameraControl = player.CameraControl;
		return (!(cameraControl != null)) ? null : cameraControl;
	}

	public static ActorCameraControl GetCameraControl(PlayerActor player)
	{
		if (player == null)
		{
			return null;
		}
		ActorCameraControl cameraControl = player.CameraControl;
		return (!(cameraControl != null)) ? null : cameraControl;
	}

	public static bool FadeStart(float time = -1f)
	{
		if (!Singleton<Map>.IsInstance())
		{
			return false;
		}
		Map map = Singleton<Map>.Instance;
		PlayerActor player = map.Player;
		if (player == null)
		{
			return false;
		}
		ActorCameraControl cameraControl = player.CameraControl;
		if (cameraControl == null)
		{
			return false;
		}
		CrossFade crossFade = cameraControl.CrossFade;
		if (crossFade == null)
		{
			return false;
		}
		crossFade.FadeStart(time);
		return true;
	}

	public void BuildNavMesh()
	{
		NavMeshSurface.BuildNavMesh();
	}

	public IEnumerator RebuildNavMeshAsync()
	{
		if (NavMeshSurface == null)
		{
			Debug.LogException(new NullReferenceException());
		}
		else
		{
			yield return NavMeshSurface.UpdateNavMesh(NavMeshSurface.navMeshData);
		}
	}

	public void SetVisibleAll(bool active)
	{
		foreach (KeyValuePair<int, AgentActor> item in AgentTable)
		{
			item.Value.IsVisible = active;
		}
	}

	public void SetVisibleAll(bool active, AgentActor exAgent)
	{
		foreach (KeyValuePair<int, AgentActor> item in AgentTable)
		{
			if (!(item.Value == exAgent))
			{
				item.Value.IsVisible = active;
			}
		}
	}

	public void EnableEntity()
	{
		PlayerActor player = Player;
		player.CameraControl.enabled = true;
		player.ChaControl.visibleAll = true;
		player.ActivateNavMeshAgent();
		foreach (AgentActor value in AgentTable.Values)
		{
			UnityEx.ValueTuple<Desire.ActionType, Desire.ActionType, bool> valueTuple = AgentModeCache[value.ID];
			if (valueTuple.Item3)
			{
				value.ActivateNavMeshAgent();
				value.NavMeshAgent.Warp(value.Position);
			}
			else if (value.EventKey == AIProject.EventType.Move)
			{
				value.ActivateNavMeshAgent();
				value.SetDefaultStateHousingItem();
				if (value.CurrentPoint != null)
				{
					OffMeshLink component = value.CurrentPoint.GetComponent<OffMeshLink>();
					if (component != null)
					{
						Transform endTransform = component.endTransform;
						value.NavMeshAgent.Warp(endTransform.position);
						value.Rotation = endTransform.rotation;
					}
					value.CurrentPoint.RemoveBookingUser(value);
					value.CurrentPoint.SetActiveMapItemObjs(active: true);
					value.CurrentPoint.ReleaseSlot(value);
					value.CurrentPoint = null;
				}
				value.EventKey = (AIProject.EventType)0;
				value.TargetInSightActionPoint = null;
				value.Animation.ResetDefaultAnimatorController();
				valueTuple.Item1 = Desire.ActionType.Normal;
				valueTuple.Item2 = Desire.ActionType.Normal;
			}
			else if (value.EventKey == AIProject.EventType.DoorOpen)
			{
				value.ActivateNavMeshAgent();
				value.SetDefaultStateHousingItem();
				if (value.CurrentPoint != null)
				{
					DoorPoint doorPoint = value.CurrentPoint as DoorPoint;
					if (doorPoint != null)
					{
						if (doorPoint.OpenState == DoorPoint.OpenPattern.Close)
						{
							if (doorPoint.OpenType == DoorPoint.OpenTypeState.Right || doorPoint.OpenType == DoorPoint.OpenTypeState.Right90)
							{
								doorPoint.SetOpenState(DoorPoint.OpenPattern.OpenRight, isSelf: true);
							}
							else
							{
								doorPoint.SetOpenState(DoorPoint.OpenPattern.OpenLeft, isSelf: true);
							}
						}
						doorPoint.RemoveBookingUser(value);
					}
					value.CurrentPoint.SetActiveMapItemObjs(active: true);
					value.CurrentPoint.ReleaseSlot(value);
					value.CurrentPoint = null;
				}
				value.EventKey = (AIProject.EventType)0;
				value.TargetInSightActionPoint = null;
				value.Animation.ResetDefaultAnimatorController();
				valueTuple.Item1 = Desire.ActionType.Normal;
				valueTuple.Item2 = Desire.ActionType.Normal;
			}
			else if (value.EventKey == AIProject.EventType.Toilet || value.EventKey == AIProject.EventType.DressIn || value.EventKey == AIProject.EventType.Bath || value.EventKey == AIProject.EventType.DressOut || value.AgentData.PlayedDressIn || valueTuple.Item1 == Desire.ActionType.FoundPeeping)
			{
				value.ActivateNavMeshAgent();
				value.SetDefaultStateHousingItem();
				if (value.CurrentPoint != null)
				{
					value.CurrentPoint.SetActiveMapItemObjs(active: true);
					value.CurrentPoint.ReleaseSlot(value);
					value.CurrentPoint = null;
				}
				value.ChaControl.ChangeNowCoordinate(reload: true);
				value.AgentData.BathCoordinateFileName = null;
				value.ChaControl.SetClothesState(0, 0);
				value.ChaControl.SetClothesState(1, 0);
				value.ChaControl.SetClothesState(2, 0);
				value.ChaControl.SetClothesState(3, 0);
				value.AgentData.PlayedDressIn = false;
				value.EventKey = (AIProject.EventType)0;
				value.TargetInSightActionPoint = null;
				value.Animation.ResetDefaultAnimatorController();
				valueTuple.Item1 = Desire.ActionType.Normal;
				valueTuple.Item2 = Desire.ActionType.Normal;
			}
			else if (valueTuple.Item1 == Desire.ActionType.Encounter)
			{
				value.ActivateNavMeshAgent();
				value.EventKey = (AIProject.EventType)0;
				value.TargetInSightActionPoint = null;
				value.Animation.ResetDefaultAnimatorController();
				valueTuple.Item1 = Desire.ActionType.Normal;
				valueTuple.Item2 = Desire.ActionType.Normal;
			}
			value.Controller.enabled = true;
			value.ChaControl.visibleAll = true;
			value.AnimationAgent.enabled = true;
			value.SetActiveOnEquipedItem(active: false);
			value.EnableBehavior();
			if (valueTuple.Item1 == Desire.ActionType.Normal && valueTuple.Item2 == Desire.ActionType.Normal)
			{
				value.ResetActionFlag();
				if (value.EnabledSchedule)
				{
					value.EnabledSchedule = false;
				}
			}
			value.Mode = valueTuple.Item1;
			value.BehaviorResources.ChangeMode(valueTuple.Item2);
			value.AnimationAgent.EnableItems();
			value.AnimationAgent.EnableParticleRenderer();
		}
		MerchantActor merchant = Singleton<Map>.Instance.Merchant;
		merchant.EnableEntity();
		AnimalBase.CreateDisplay = true;
		AnimalManager animalManager = Singleton<AnimalManager>.Instance;
		for (int i = 0; i < animalManager.Animals.Count; i++)
		{
			AnimalBase animalBase = animalManager.Animals[i];
			if (!(animalBase == null))
			{
				animalBase.BodyEnabled = true;
				animalBase.enabled = true;
			}
		}
		animalManager.SettingAnimalPointBehavior();
	}

	public void DisableEntity()
	{
		PlayerActor player = Player;
		player.CameraControl.enabled = false;
		player.ChaControl.visibleAll = false;
		foreach (AgentActor value in AgentTable.Values)
		{
			Desire.ActionType mode = value.BehaviorResources.Mode;
			bool flag = value.NavMeshAgent.enabled;
			AgentModeCache[value.ID] = new UnityEx.ValueTuple<Desire.ActionType, Desire.ActionType, bool>(value.Mode, mode, flag);
			value.SetActiveOnEquipedItem(active: false);
			value.Controller.enabled = false;
			if (flag)
			{
				value.NavMeshAgent.enabled = false;
			}
			value.AnimationAgent.enabled = false;
			value.ChaControl.visibleAll = false;
			if (mode == Desire.ActionType.EndTaskMasturbation || mode == Desire.ActionType.EndTaskLesbianH || mode == Desire.ActionType.EndTaskLesbianMerchantH)
			{
				value.BehaviorResources.ChangeMode(Desire.ActionType.Idle);
			}
			value.DisableBehavior();
			value.AnimationAgent.DisableItems();
			value.AnimationAgent.DisableParticleRenderer();
		}
		MerchantActor merchant = Singleton<Map>.Instance.Merchant;
		merchant.DisableEntity();
		AnimalBase.CreateDisplay = false;
		AnimalManager animalManager = Singleton<AnimalManager>.Instance;
		for (int i = 0; i < animalManager.Animals.Count; i++)
		{
			AnimalBase animalBase = animalManager.Animals[i];
			if (!(animalBase == null))
			{
				animalBase.enabled = false;
				animalBase.BodyEnabled = false;
			}
		}
		animalManager.ClearAnimalPointBehavior();
	}

	public void EnableEntity(Actor exActor)
	{
		ReadOnlyDictionary<int, AgentActor> agentTable = Singleton<Map>.Instance.AgentTable;
		if (agentTable.IsNullOrEmpty())
		{
			return;
		}
		foreach (KeyValuePair<int, AgentActor> item in agentTable)
		{
			AgentActor value = item.Value;
			if (!(value == null) && !(value == exActor))
			{
				value.EnableEntity();
			}
		}
		if (Merchant != null && Merchant != exActor)
		{
			Merchant.EnableEntity();
		}
	}

	public void DisableEntity(Actor exActor)
	{
		ReadOnlyDictionary<int, AgentActor> agentTable = Singleton<Map>.Instance.AgentTable;
		if (agentTable.IsNullOrEmpty())
		{
			return;
		}
		foreach (KeyValuePair<int, AgentActor> item in agentTable)
		{
			AgentActor value = item.Value;
			if (!(value == null) && !(value == exActor))
			{
				value.DisableEntity();
			}
		}
		if (Merchant != null && Merchant != exActor)
		{
			Merchant.DisableEntity();
		}
	}

	public void ResetAgentsInHousingArea()
	{
		List<AgentActor> list = ListPool<AgentActor>.Get();
		foreach (KeyValuePair<int, AgentActor> item in _agentTable)
		{
			if (item.Value.AreaID == HousingAreaID)
			{
				list.Add(item.Value);
				Actor commandPartner = item.Value.CommandPartner;
				if (commandPartner != null && commandPartner.GetType() == typeof(AgentActor) && !list.Contains(commandPartner))
				{
					list.Add(commandPartner as AgentActor);
				}
			}
		}
		if (HousingRecoveryPointTable.TryGetValue(HousingID, out var value))
		{
			int num = 0;
			foreach (AgentActor item2 in list)
			{
				Transform element = value.GetElement(num++);
				if (!(element == null))
				{
					item2.ActivateNavMeshAgent();
					item2.ClearItems();
					item2.ClearParticles();
					item2.SetActiveOnEquipedItem(active: false);
					item2.AgentData.CarryingItem = null;
					item2.ChaControl.ChangeNowCoordinate(reload: true);
					item2.AgentData.BathCoordinateFileName = null;
					item2.ChaControl.SetClothesState(0, 0);
					item2.ChaControl.SetClothesState(1, 0);
					item2.ChaControl.SetClothesState(2, 0);
					item2.ChaControl.SetClothesState(3, 0);
					item2.ChaControl.SetClothesState(5, 0);
					item2.AgentData.PlayedDressIn = false;
					item2.SetDefaultStateHousingItem();
					if (item2.CurrentPoint != null)
					{
						item2.CurrentPoint.SetActiveMapItemObjs(active: true);
						item2.CurrentPoint.ReleaseSlot(item2);
						item2.CurrentPoint = null;
					}
					item2.EventKey = (AIProject.EventType)0;
					item2.TargetInSightActionPoint = null;
					item2.CommandPartner = null;
					item2.TargetInSightActor = null;
					item2.Animation.ResetDefaultAnimatorController();
					item2.ChangeBehavior(Desire.ActionType.Normal);
					item2.NavMeshAgent.Warp(element.position);
					item2.Rotation = Quaternion.Euler(0f, element.rotation.eulerAngles.y, 0f);
				}
			}
		}
		ListPool<AgentActor>.Release(list);
	}

	public void InitCommandable()
	{
		ICommandable[] componentsInChildren = _pointAgent.GetComponentsInChildren<ICommandable>(includeInactive: true);
		Player.PlayerController.CommandArea.SetCommandableObjects(componentsInChildren);
		foreach (int agentKey in _agentKeys)
		{
			if (AgentTable.TryGetValue(agentKey, out var value))
			{
				AddCommandable(value);
			}
		}
		AddCommandable(Merchant);
		AnimalManager animalManager = Singleton<AnimalManager>.Instance;
		foreach (AnimalBase animal in animalManager.Animals)
		{
			if (animal != null && animal.IsCommandable)
			{
				AddCommandable(animal);
			}
		}
	}

	public void AddCommandable(ICommandable command)
	{
		Player.PlayerController.CommandArea.AddCommandableObject(command);
	}

	private int UnusedRegID()
	{
		int i;
		for (i = 10000; Singleton<Game>.Instance.Environment.RegIDList.Contains(i); i++)
		{
		}
		return i;
	}

	public int RegisterRuntimePoint(Point pt)
	{
		if (pt == null)
		{
			return -1;
		}
		int num2 = ((pt.RegisterID != -1) ? pt.RegisterID : (pt.RegisterID = UnusedRegID()));
		AIProject.SaveData.Environment environment = Singleton<Game>.Instance.Environment;
		environment.RegIDList.Add(num2);
		if (pt is SearchActionPoint)
		{
			if (!environment.SearchActionLockTable.TryGetValue(num2, out var _))
			{
				environment.SearchActionLockTable[num2] = new AIProject.SaveData.Environment.SearchActionInfo();
			}
		}
		else if (pt is FarmPoint)
		{
			FarmPoint farmPoint = pt as FarmPoint;
			if (!environment.FarmlandTable.TryGetValue(num2, out var value2))
			{
				List<AIProject.SaveData.Environment.PlantInfo> list = new List<AIProject.SaveData.Environment.PlantInfo>();
				environment.FarmlandTable[num2] = list;
				value2 = list;
				if (farmPoint.Kind == FarmPoint.FarmKind.Plant && value2.Count != farmPoint.HarvestSections.Length)
				{
					value2.Clear();
					for (int i = 0; i < farmPoint.HarvestSections.Length; i++)
					{
						value2.Add(null);
					}
				}
			}
			FarmSection[] harvestSections = farmPoint.HarvestSections;
			foreach (FarmSection farmSection in harvestSections)
			{
				farmSection.HarvestID = num2;
			}
		}
		return num2;
	}

	public void UnregisterRuntimePoint(Point pt, bool removeEntry = true)
	{
		if (pt == null)
		{
			return;
		}
		int registerID = pt.RegisterID;
		AIProject.SaveData.Environment environment = Singleton<Game>.Instance.Environment;
		if (removeEntry)
		{
			if (environment.RegIDList.Remove(registerID))
			{
				environment.SearchActionLockTable.Remove(registerID);
				environment.FarmlandTable.Remove(registerID);
			}
		}
		else if (!_removeRegIDCache.Contains(registerID))
		{
			_removeRegIDCache.Add(registerID);
		}
	}

	public void RemoveRegIDCache(int regID)
	{
		if (_removeRegIDCache.Contains(regID))
		{
			_removeRegIDCache.Remove(regID);
		}
	}

	public void SeqRemoveCacheRegID()
	{
		AIProject.SaveData.Environment environment = Singleton<Game>.Instance.Environment;
		foreach (int item in _removeRegIDCache)
		{
			if (environment.RegIDList.Remove(item))
			{
				environment.SearchActionLockTable.Remove(item);
				environment.FarmlandTable.Remove(item);
			}
		}
		_removeRegIDCache.Clear();
	}

	public void AddAppendTargetPoints(ActionPoint[] actionPoints)
	{
		_pointAgent.AppendActionPoints.Clear();
		_pointAgent.AppendActionPoints.AddRange(actionPoints);
		int[] array = ChunkTable.Keys.ToArray();
		int[] array2 = array;
		foreach (int key in array2)
		{
			if (ChunkTable.TryGetValue(key, out var value))
			{
				value.LoadAppendActionPoints(actionPoints);
			}
		}
		Singleton<Map>.Instance.WarpPointDic.Clear();
		foreach (ActionPoint actionPoint in actionPoints)
		{
			actionPoint.Init();
			AddAppendCommandable(actionPoint);
			CheckWarpPoint(actionPoint);
		}
		foreach (int agentKey in _agentKeys)
		{
			if (AgentTable.TryGetValue(agentKey, out var value2))
			{
				value2.AgentController.SearchArea.AddPoints(actionPoints);
			}
		}
	}

	private void CheckWarpPoint(ActionPoint pt)
	{
		WarpPoint warpPoint = pt as WarpPoint;
		if (warpPoint == null)
		{
			return;
		}
		MapArea ownerArea = warpPoint.OwnerArea;
		if (!(ownerArea == null))
		{
			int chunkID = ownerArea.ChunkID;
			if (!Singleton<Map>.Instance.WarpPointDic.TryGetValue(chunkID, out var value))
			{
				Dictionary<int, List<WarpPoint>> dictionary = new Dictionary<int, List<WarpPoint>>();
				Singleton<Map>.Instance.WarpPointDic[chunkID] = dictionary;
				value = dictionary;
			}
			if (!value.TryGetValue(warpPoint.TableID, out var value2))
			{
				List<WarpPoint> list = new List<WarpPoint>();
				value[warpPoint.TableID] = list;
				value2 = list;
			}
			if (!value2.Contains(warpPoint))
			{
				value2.Add(warpPoint);
			}
		}
	}

	public void RemoveAppendActionPoint(ActionPoint point)
	{
		_pointAgent.AppendActionPoints.Remove(point);
		int[] array = ChunkTable.Keys.ToArray();
		int[] array2 = array;
		foreach (int key in array2)
		{
			if (ChunkTable.TryGetValue(key, out var value))
			{
				MapArea[] mapAreas = value.MapAreas;
				foreach (MapArea mapArea in mapAreas)
				{
					mapArea.AppendActionPoints.Remove(point);
				}
				value.AppendActionPoints.Remove(point);
			}
		}
	}

	public void RemoveAgentSearchActionPoint(ActionPoint actionPoint)
	{
		foreach (int agentKey in _agentKeys)
		{
			if (AgentTable.TryGetValue(agentKey, out var value))
			{
				value.AgentController.SearchArea.RemovePoint(actionPoint);
				value.RemoveActionPoint(actionPoint);
			}
		}
	}

	public void AddRuntimeFarmPoints(FarmPoint[] farmPoints)
	{
		int[] array = ChunkTable.Keys.ToArray();
		int[] array2 = array;
		foreach (int key in array2)
		{
			if (ChunkTable.TryGetValue(key, out var value))
			{
				value.LoadAppendFarmPoints(farmPoints);
			}
		}
		if (_pointAgent != null)
		{
			_pointAgent.AddRuntimeFarmPoints(farmPoints);
		}
		foreach (FarmPoint farmPoint in farmPoints)
		{
			AddAppendCommandable(farmPoint);
			farmPoint.SetChickenWayPoint();
			farmPoint.CreateChicken();
		}
	}

	public void RemoveRuntimeFarmPoint(FarmPoint point)
	{
		if (point != null)
		{
			point.DestroyChicken();
		}
		int[] array = ChunkTable.Keys.ToArray();
		int[] array2 = array;
		foreach (int key in array2)
		{
			if (ChunkTable.TryGetValue(key, out var value))
			{
				MapArea[] mapAreas = value.MapAreas;
				foreach (MapArea mapArea in mapAreas)
				{
					mapArea.AppendFarmPoints.Remove(point);
				}
				value.AppendFarmPoints.Remove(point);
			}
		}
		if (_pointAgent != null)
		{
			_pointAgent.RemoveRuntimeFarmPoint(point);
		}
	}

	public void AddPetHomePoints(PetHomePoint[] petHomePoints)
	{
		int[] array = ChunkTable.Keys.ToArray();
		int[] array2 = array;
		foreach (int key in array2)
		{
			if (ChunkTable.TryGetValue(key, out var value) && !(value == null))
			{
				value.LoadAppendPetHomePoints(petHomePoints);
			}
		}
		foreach (PetHomePoint command in petHomePoints)
		{
			AddAppendCommandable(command);
		}
		if (PointAgent != null)
		{
			PointAgent.AddPetHomePoints(petHomePoints);
		}
		Dictionary<int, AIProject.SaveData.Environment.PetHomeInfo> dictionary = ((!Singleton<Game>.IsInstance()) ? null : Singleton<Game>.Instance.Environment?.PetHomeStateTable);
		if (dictionary == null)
		{
			return;
		}
		foreach (PetHomePoint petHomePoint in petHomePoints)
		{
			if (!(petHomePoint == null))
			{
				int registerID = petHomePoint.RegisterID;
				if (!dictionary.TryGetValue(registerID, out var value2) || value2 == null)
				{
					AIProject.SaveData.Environment.PetHomeInfo petHomeInfo2 = (dictionary[registerID] = new AIProject.SaveData.Environment.PetHomeInfo());
					value2 = (petHomePoint.SaveData = petHomeInfo2);
					value2.HousingID = HousingID;
					value2.AnimalData = null;
					value2.ChaseActor = false;
				}
				else
				{
					petHomePoint.SaveData = value2;
				}
			}
		}
	}

	public void RemovePetHomePoint(PetHomePoint petHomePoint)
	{
		if (petHomePoint == null)
		{
			return;
		}
		int[] array = ChunkTable.Keys.ToArray();
		int[] array2 = array;
		foreach (int key in array2)
		{
			if (!ChunkTable.TryGetValue(key, out var value) || value == null)
			{
				continue;
			}
			if (!value.MapAreas.IsNullOrEmpty())
			{
				MapArea[] mapAreas = value.MapAreas;
				foreach (MapArea mapArea in mapAreas)
				{
					mapArea.AppendPetHomePoints.Remove(petHomePoint);
				}
			}
			value.AppendPetHomePoints.Remove(petHomePoint);
		}
		if (PointAgent != null)
		{
			PointAgent.RemovePetHomePoint(petHomePoint);
		}
		Dictionary<int, AIProject.SaveData.Environment.PetHomeInfo> dictionary = ((!Singleton<Game>.IsInstance()) ? null : Singleton<Game>.Instance.Environment?.PetHomeStateTable);
		if (!dictionary.IsNullOrEmpty())
		{
			dictionary.Remove(petHomePoint.RegisterID);
		}
	}

	public void AddJukePoints(JukePoint[] jukePoints)
	{
		int[] array = ChunkTable.Keys.ToArray();
		int[] array2 = array;
		foreach (int key in array2)
		{
			if (ChunkTable.TryGetValue(key, out var value) && !(value == null))
			{
				value.LoadAppendJukePoints(jukePoints);
			}
		}
		foreach (JukePoint jukePoint in jukePoints)
		{
			if (!(jukePoint == null))
			{
				jukePoint.SetAreaID();
				AddAppendCommandable(jukePoint);
			}
		}
		if (_pointAgent != null)
		{
			_pointAgent.AddJukePoints(jukePoints);
		}
	}

	public void RemoveJukePoint(JukePoint point)
	{
		int[] array = ChunkTable.Keys.ToArray();
		if (!array.IsNullOrEmpty())
		{
			int[] array2 = array;
			foreach (int key in array2)
			{
				if (ChunkTable.TryGetValue(key, out var value) && !(value == null))
				{
					MapArea[] mapAreas = value.MapAreas;
					foreach (MapArea mapArea in mapAreas)
					{
						mapArea.AppendJukePoints.Remove(point);
					}
					value.AppendJukePoints.Remove(point);
				}
			}
		}
		if (_pointAgent != null)
		{
			_pointAgent.RemoveJukePoint(point);
		}
		Dictionary<int, string> value2 = null;
		if (Singleton<Game>.IsInstance())
		{
			if (MapID == 0)
			{
				value2 = Singleton<Game>.Instance.Environment?.JukeBoxAudioNameTable;
			}
			else
			{
				Dictionary<int, Dictionary<int, string>> dictionary = Singleton<Game>.Instance.Environment?.AnotherJukeBoxAudioNameTable;
				if (dictionary != null && (!dictionary.TryGetValue(MapID, out value2) || value2 == null))
				{
					Dictionary<int, string> dictionary2 = new Dictionary<int, string>();
					dictionary[MapID] = dictionary2;
					value2 = dictionary2;
				}
			}
		}
		if (value2.IsNullOrEmpty() || !(point != null))
		{
			return;
		}
		int areaID = point.AreaID;
		bool flag = true;
		if (_pointAgent != null)
		{
			IReadOnlyDictionary<int, List<JukePoint>> jukePointTable = _pointAgent.JukePointTable;
			if (jukePointTable != null && 0 < jukePointTable.Count)
			{
				List<JukePoint> value3 = null;
				if (jukePointTable.TryGetValue(areaID, out value3) && !value3.IsNullOrEmpty())
				{
					value3.RemoveAll((JukePoint x) => x == null);
					flag = value3.Count == 0 || !value3.Exists((JukePoint x) => x != point);
				}
			}
		}
		if (flag && value2.ContainsKey(areaID))
		{
			value2.Remove(areaID);
		}
	}

	public void AddRuntimeCraftPoints(CraftPoint[] craftPoints)
	{
		List<int> list = ChunkTable.Keys.ToList();
		if (!list.IsNullOrEmpty())
		{
			foreach (int item in list)
			{
				if (ChunkTable.TryGetValue(item, out var value) && !(value == null))
				{
					value.LoadAppendCraftPoints(craftPoints);
				}
			}
		}
		foreach (CraftPoint craftPoint in craftPoints)
		{
			AddAppendCommandable(craftPoint);
			if (!(craftPoint != null) || craftPoint.Kind != CraftPoint.CraftKind.Recycling)
			{
				continue;
			}
			Dictionary<int, RecyclingData> dictionary = ((!Singleton<Game>.IsInstance()) ? null : Singleton<Game>.Instance.Environment)?.RecyclingDataTable;
			if (dictionary != null)
			{
				RecyclingData value2 = null;
				if (!dictionary.TryGetValue(craftPoint.RegisterID, out value2) || value2 == null)
				{
					dictionary[craftPoint.RegisterID] = new RecyclingData();
				}
			}
		}
	}

	public void RemoveCraftPoint(CraftPoint point)
	{
		List<int> list = ChunkTable.Keys.ToList();
		foreach (int item in list)
		{
			if (ChunkTable.TryGetValue(item, out var value) && !(value == null))
			{
				MapArea[] mapAreas = value.MapAreas;
				foreach (MapArea mapArea in mapAreas)
				{
					mapArea.AppendCraftPoints.Remove(point);
				}
				value.AppendCraftPoints.Remove(point);
			}
		}
		if (point != null && point.Kind == CraftPoint.CraftKind.Recycling)
		{
			Dictionary<int, RecyclingData> dictionary = ((!Singleton<Game>.IsInstance()) ? null : Singleton<Game>.Instance.Environment)?.RecyclingDataTable;
			if (dictionary != null && dictionary.ContainsKey(point.RegisterID))
			{
				dictionary.Remove(point.RegisterID);
			}
		}
	}

	public void AddRuntimeLightSwitchPoints(LightSwitchPoint[] lightSwitchPoints)
	{
		List<int> list = ChunkTable.Keys.ToList();
		if (!list.IsNullOrEmpty())
		{
			foreach (int item in list)
			{
				if (ChunkTable.TryGetValue(item, out var value) && !(value == null))
				{
					value.LoadAppendLightSwitchPoints(lightSwitchPoints);
				}
			}
		}
		if (!lightSwitchPoints.IsNullOrEmpty())
		{
			foreach (LightSwitchPoint lightSwitchPoint in lightSwitchPoints)
			{
				AddAppendCommandable(lightSwitchPoint);
				lightSwitchPoint.Switch(lightSwitchPoint.IsSwitch());
			}
		}
	}

	public void RemoveRuntimeLightSwitchPoint(LightSwitchPoint point)
	{
		List<int> list = ChunkTable.Keys.ToList();
		if (!list.IsNullOrEmpty())
		{
			foreach (int item in list)
			{
				if (ChunkTable.TryGetValue(item, out var value) && !(value == null))
				{
					MapArea[] mapAreas = value.MapAreas;
					foreach (MapArea mapArea in mapAreas)
					{
						mapArea.AppendLightSwitchPoints.Remove(point);
					}
					value.AppendLightSwitchPoints.Remove(point);
				}
			}
		}
		Dictionary<int, bool> dictionary = ((!Singleton<Game>.IsInstance()) ? null : Singleton<Game>.Instance.Environment?.LightObjectSwitchStateTable);
		if (!dictionary.IsNullOrEmpty() && dictionary.ContainsKey(point.RegisterID))
		{
			dictionary.Remove(point.RegisterID);
		}
	}

	public void AddAppendCommandable(ICommandable command)
	{
		_appendCommandables.Add(command);
		Player.PlayerController.CommandArea.AddCommandableObject(command);
	}

	public void RemoveAppendCommandable(ICommandable command)
	{
		_appendCommandables.Remove(command);
		Player.PlayerController.CommandArea.RemoveCommandableObject(command);
	}

	public void ClearAppendCommands()
	{
		if (_appendCommandables.IsNullOrEmpty())
		{
			return;
		}
		foreach (ICommandable appendCommandable in _appendCommandables)
		{
			Player.PlayerController.CommandArea.RemoveCommandableObject(appendCommandable);
			if (!(appendCommandable is ActionPoint))
			{
				continue;
			}
			ActionPoint ap = appendCommandable as ActionPoint;
			foreach (int agentKey in _agentKeys)
			{
				if (AgentTable.TryGetValue(agentKey, out var value))
				{
					value.AgentController.SearchArea.RemovePoint(ap);
				}
			}
		}
		_appendCommandables.Clear();
	}

	public void AddAppendHPoints(HPoint[] hPoints)
	{
		_pointAgent.AppendHPoints.Clear();
		_pointAgent.AppendHPoints.AddRange(hPoints);
		int[] array = ChunkTable.Keys.ToArray();
		int[] array2 = array;
		foreach (int key in array2)
		{
			if (ChunkTable.TryGetValue(key, out var value))
			{
				value.LoadAppendHPoints(hPoints);
			}
		}
	}

	public List<UnityEx.ValueTuple<int, List<StuffItem>>> GetInventoryList()
	{
		List<UnityEx.ValueTuple<int, List<StuffItem>>> list = ListPool<UnityEx.ValueTuple<int, List<StuffItem>>>.Get();
		WorldData worldData = ((!Singleton<Game>.IsInstance()) ? null : Singleton<Game>.Instance.WorldData);
		AIProject.SaveData.Environment environment = worldData?.Environment;
		DefinePack definePack = ((!Singleton<Resources>.IsInstance()) ? null : Singleton<Resources>.Instance.DefinePack);
		if (worldData != null && environment != null)
		{
			for (int i = 0; i < 2; i++)
			{
				int num = 0;
				List<StuffItem> list2 = null;
				if (i != 0)
				{
					if (i != 1)
					{
						continue;
					}
					list2 = environment.ItemListInStorage;
					num = ((definePack != null) ? definePack.ItemBoxCapacityDefines.StorageCapacity : 0);
				}
				else
				{
					PlayerData playerData = worldData.PlayerData;
					list2 = playerData.ItemList;
					num = playerData.InventorySlotMax;
				}
				if (0 < num && list2 != null)
				{
					list.Add(new UnityEx.ValueTuple<int, List<StuffItem>>(num, list2));
				}
			}
		}
		return list;
	}

	public void ReturnInventoryList(List<UnityEx.ValueTuple<int, List<StuffItem>>> list)
	{
		if (list != null)
		{
			list.Clear();
			ListPool<UnityEx.ValueTuple<int, List<StuffItem>>>.Release(list);
		}
	}

	public void SendItemListToList(int slotMax, List<StuffItem> sender, List<StuffItem> receiver)
	{
		if (slotMax <= 0 || sender.IsNullOrEmpty() || receiver == null)
		{
			return;
		}
		for (int i = 0; i < sender.Count; i++)
		{
			StuffItem element = sender.GetElement(i);
			if (element == null || element.Count <= 0)
			{
				sender.RemoveAt(i);
				i--;
				continue;
			}
			StuffItem stuffItem = new StuffItem(element);
			int possible = 0;
			receiver.CanAddItem(slotMax, stuffItem, out possible);
			if (0 < possible)
			{
				possible = Mathf.Min(stuffItem.Count, possible);
				receiver.AddItem(stuffItem, possible, slotMax);
			}
			element.Count -= possible;
			if (element.Count <= 0)
			{
				sender.RemoveAt(i);
				i--;
			}
		}
	}

	public void WarpToBasePoint(BasePoint basePoint, System.Action onEndFadeIn = null, System.Action onCompleted = null)
	{
		if (basePoint == null)
		{
			return;
		}
		IsWarpProc = true;
		MapUIContainer.StartFade(FadeCanvas.PanelType.Blackout, FadeType.In, 1f, ignoreTimeScale: true).Subscribe((Action<Unit>)delegate
		{
		}, (System.Action)delegate
		{
			onEndFadeIn?.Invoke();
			Transform warpPt = basePoint.WarpPoint;
			PlayerActor player = Player;
			player.NavMeshAgent.Warp(warpPt.position);
			Quaternion rotation = warpPt.rotation;
			player.Rotation = rotation;
			Quaternion quaternion = rotation;
			player.CameraControl.XAxisValue = quaternion.eulerAngles.y;
			player.CameraControl.YAxisValue = 0.6f;
			Actor partner = player.Partner;
			if (partner != null)
			{
				AgentActor agentActor = partner as AgentActor;
				if (agentActor != null && agentActor.EventKey == AIProject.EventType.Move)
				{
					agentActor.Animation.StopAllAnimCoroutine();
				}
				Observable.EveryLateUpdate().Skip(2).Take(2)
					.Subscribe(delegate
					{
						float shapeBodyValue = partner.ChaControl.GetShapeBodyValue(0);
						Vector3 newPosition = player.Position + player.Rotation * Singleton<Resources>.Instance.AgentProfile.GetOffsetInParty(shapeBodyValue);
						partner.NavMeshAgent.Warp(newPosition);
						partner.Rotation = warpPt.rotation;
					});
			}
			MapUIContainer.StartFade(FadeCanvas.PanelType.Blackout, FadeType.Out, 1f, ignoreTimeScale: false).Subscribe((Action<Unit>)delegate
			{
			}, (System.Action)delegate
			{
				IsWarpProc = false;
				onCompleted?.Invoke();
			});
		});
	}

	public void WarpToPairPoint(WarpPoint warpPoint, System.Action onEndFadeIn = null, System.Action onCompleted = null)
	{
		if (warpPoint == null)
		{
			return;
		}
		IsWarpProc = true;
		MapUIContainer.StartFade(FadeCanvas.PanelType.Blackout, FadeType.In, 1f, ignoreTimeScale: true).Subscribe((Action<Unit>)delegate
		{
		}, (System.Action)delegate
		{
			onEndFadeIn?.Invoke();
			Transform warpTrans = warpPoint.transform;
			PlayerActor player = Player;
			player.NavMeshWarp(warpTrans, 3);
			player.CameraControl.XAxisValue = warpTrans.eulerAngles.y;
			player.CameraControl.YAxisValue = 0.6f;
			Actor partner = player.Partner;
			if (partner != null)
			{
				AgentActor agentActor = partner as AgentActor;
				if (agentActor != null && agentActor.EventKey == AIProject.EventType.Move)
				{
					agentActor.Animation.StopAllAnimCoroutine();
				}
				Observable.EveryLateUpdate().Skip(2).Take(2)
					.Subscribe(delegate
					{
						float shapeBodyValue = partner.ChaControl.GetShapeBodyValue(0);
						Vector3 position = player.Position + player.Rotation * Singleton<Resources>.Instance.AgentProfile.GetOffsetInParty(shapeBodyValue);
						partner.NavMeshWarp(position, warpTrans.rotation, 0);
					});
			}
			MapUIContainer.StartFade(FadeCanvas.PanelType.Blackout, FadeType.Out, 1f, ignoreTimeScale: false).Subscribe((Action<Unit>)delegate
			{
			}, (System.Action)delegate
			{
				IsWarpProc = false;
				onCompleted?.Invoke();
			});
		});
	}

	public void ChangeMap(int mapID, System.Action onEndFadeIn = null, System.Action onCompleted = null)
	{
		Singleton<Game>.Instance.WorldData.MapID = mapID;
		Observable.FromCoroutine(() => ChangeMapCoroutine(mapID, onEndFadeIn, onCompleted)).Subscribe();
	}

	private IEnumerator ChangeMapCoroutine(int mapID, System.Action onEndFadeIn = null, System.Action onCompleted = null)
	{
		Singleton<Scene>.Instance.loadingPanel.CanvasGroup.alpha = 0f;
		Singleton<Scene>.Instance.loadingPanel.gameObject.SetActive(value: true);
		Singleton<Scene>.Instance.loadingPanel.Play();
		yield return ObservableEasing.Linear(1f, ignoreTimeScale: true).FrameTimeInterval(ignoreTimeScale: true).Do(delegate(TimeInterval<float> x)
		{
			Singleton<Scene>.Instance.loadingPanel.CanvasGroup.alpha = x.Value;
		})
			.ToYieldInstruction();
		onEndFadeIn?.Invoke();
		if (Singleton<AnimalManager>.IsInstance())
		{
			Singleton<AnimalManager>.Instance.ReleaseAnimal();
		}
		ReleaseMap();
		ReleaseAgents();
		Player.DisableEntity();
		Merchant.StopBehavior();
		Merchant.DisableEntity();
		Merchant.DeactivateNavMeshElement();
		yield return LoadMap(mapID);
		yield return LoadNavMeshSource();
		yield return LoadElements();
		yield return LoadMerchantPoint();
		yield return LoadEventPoints();
		yield return LoadStoryPoints();
		yield return LoadHousingObj(mapID);
		SetVanishList();
		BuildNavMesh();
		_pointAgent.CreateHousingWaypoint();
		yield return LoadAnimalPoint();
		yield return SetupPoint();
		AnimalManager animalManager = Singleton<AnimalManager>.Instance;
		yield return animalManager.SetupPointsAsync(_pointAgent);
		if (Singleton<SoundPlayer>.IsInstance())
		{
			Singleton<SoundPlayer>.Instance.RefreshJukeBoxTable(this);
		}
		yield return RefreshAsync();
		WorldData worldData = Singleton<Game>.Instance.WorldData;
		bool existsBackup = Singleton<Game>.Instance.ExistsBackup(worldData.WorldID);
		yield return LoadAgents(worldData, existsBackup);
		RefreshAgentStatus();
		RefreshAnimalStatus();
		yield return null;
		RefreshActiveTimeRelationObjects();
		InitActionPoints();
		Player.PlayerController.CommandArea.InitCommandStates();
		InitCommandable();
		AddAppendTargetPoints(Singleton<Housing>.Instance.ActionPoints);
		AddRuntimeFarmPoints(Singleton<Housing>.Instance.FarmPoints);
		AddPetHomePoints(Singleton<Housing>.Instance.PetHomePoints);
		AddJukePoints(Singleton<Housing>.Instance.JukePoints);
		AddRuntimeCraftPoints(Singleton<Housing>.Instance.CraftPoints);
		AddRuntimeLightSwitchPoints(Singleton<Housing>.Instance.LightSwitchPoints);
		AddAppendHPoints(Singleton<Housing>.Instance.HPoints);
		ActionPoint[] actionPoints = Singleton<Housing>.Instance.ActionPoints;
		foreach (ActionPoint actionPoint in actionPoints)
		{
			OffMeshLink[] componentsInChildren = actionPoint.GetComponentsInChildren<OffMeshLink>();
			OffMeshLink[] array = componentsInChildren;
			foreach (OffMeshLink offMeshLink in array)
			{
				offMeshLink.UpdatePositions();
			}
		}
		LoadAgentTargetActionPoint();
		LoadAgentTargetActor();
		WorldData worldData2 = Singleton<Game>.Instance.WorldData;
		bool existsBackup2 = Singleton<Game>.Instance.ExistsBackup(worldData2.WorldID);
		yield return LoadAnimals(worldData2, existsBackup2);
		RefreshAreaOpenLinkedObject();
		RefreshTimeOpenLinkedObject();
		SoundData[] sounds = Config.SoundData.Sounds;
		for (int k = 0; k < sounds.Length; k++)
		{
			sounds[k]?.Refresh();
		}
		yield return LoadEnviroObject();
		Map map = this;
		int? num = Player.MapArea?.AreaID;
		map.ResettingEnviroAreaElement(mapID, num.HasValue ? num.Value : 0);
		Map map2 = this;
		int? num2 = Player.MapArea?.AreaID;
		map2.RefreshHousingEnv3DSEPoints(mapID, num2.HasValue ? num2.Value : 0);
		yield return new WaitForSecondsRealtime(1f);
		WaitForSeconds waitTime = new WaitForSeconds(0.5f);
		while (!WaitCompletionAgents())
		{
			yield return waitTime;
		}
		_agentVanishAreaList = null;
		foreach (AgentActor value in _agentTable.Values)
		{
			value.ActivateNavMeshAgent();
		}
		yield return null;
		foreach (AgentActor agent in _agentTable.Values)
		{
			yield return agent.CalculateWaypoints();
		}
		Player.ActivateNavMeshAgent();
		Merchant.SetMerchantPoints(_pointAgent.MerchantPoints);
		Merchant.ChangeMap(mapID);
		MapUIContainer.InitializeMinimap();
		animalManager.StartAllAnimalCreate();
		yield return Singleton<Resources>.Instance.HSceneTable.LoadHObj();
		if (Singleton<AnimalManager>.IsInstance())
		{
			Singleton<AnimalManager>.Instance.ActiveMapScene = true;
		}
		GC.Collect();
		Merchant.EnableEntity();
		Merchant.FirstAppear();
		Player.EnableEntity();
		Transform startPoint = _pointAgent.ShipPoints[0].StartPointFromMigrate;
		Player.NavMeshAgent.Warp(startPoint.position);
		Player.Rotation = startPoint.rotation;
		InitSearchActorTargetsAll();
		foreach (KeyValuePair<int, AgentActor> item in _agentTable)
		{
			if (item.Value.AgentData.CarryingItem != null && item.Value.Mode != Desire.ActionType.SearchEatSpot && item.Value.Mode != Desire.ActionType.EndTaskEat && item.Value.Mode != Desire.ActionType.EndTaskEatThere)
			{
				item.Value.AgentData.CarryingItem = null;
			}
			item.Value.EnableBehavior();
			item.Value.BehaviorResources.ChangeMode(item.Value.Mode);
		}
		Player.PlayerController.CommandArea.InitCommandStates();
		Player.PlayerController.CommandArea.RefreshCommands();
		yield return ObservableEasing.Linear(1f, ignoreTimeScale: true).FrameTimeInterval(ignoreTimeScale: true).Do(delegate(TimeInterval<float> x)
		{
			Singleton<Scene>.Instance.loadingPanel.CanvasGroup.alpha = 1f - x.Value;
		})
			.ToYieldInstruction();
		Singleton<Scene>.Instance.loadingPanel.Stop();
		Singleton<Scene>.Instance.loadingPanel.gameObject.SetActive(value: false);
		while (Time.timeScale == 0f)
		{
			yield return null;
		}
		if (Singleton<Game>.IsInstance())
		{
			AIProject.SaveData.Environment environment = Singleton<Game>.Instance.Environment;
			if (environment != null)
			{
				int num3 = 29;
				if (environment.TutorialProgress == num3)
				{
					ForcedSetTutorialProgressAndUIUpdate(num3 + 1);
				}
			}
		}
		onCompleted?.Invoke();
	}

	public bool WaitCompletionAgents()
	{
		foreach (AgentActor value in _agentTable.Values)
		{
			if (!value.IsInit)
			{
				return false;
			}
		}
		return true;
	}

	public void SetVanishList()
	{
		Dictionary<int, List<List<Resources.MapTables.VisibleObjectInfo>>> vanishList = Singleton<Resources>.Instance.Map.VanishList;
		if (!Singleton<Resources>.Instance.Map.VanishList.TryGetValue(MapID, out var value))
		{
			return;
		}
		for (int i = 0; i < value.Count; i++)
		{
			int index = i;
			for (int j = 0; j < value[index].Count; j++)
			{
				int index2 = j;
				lstMapVanish.Add(new VisibleObject
				{
					nameCollider = value[index][index2].nameCollider,
					collider = GameObject.Find(value[index][index2].nameCollider)?.GetComponentInChildren<Collider>(),
					vanishObj = GameObject.Find(value[index][index2].VanishObjName)
				});
			}
		}
	}
}
