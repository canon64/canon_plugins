using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using ADV;
using ActionGame.Chara;
using ActionGame.MapObject;
using ActionGame.Place;
using ActionGame.Point;
using Cysharp.Threading.Tasks;
using Illusion;
using Illusion.Extensions;
using Illusion.Game;
using Manager;
using StrayTech;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ActionGame;

public class ActionMap : BaseMap
{
	public class NavigationInfo
	{
		public float distance { get; }

		public int[] IDs { get; }

		public NavigationInfo(float distance, int[] IDs)
		{
			this.distance = distance;
			this.IDs = IDs;
		}
	}

	[SerializeField]
	private int _gateLinkID = -1;

	[SerializeField]
	private Cycle.Type _nowCycle;

	private SunLightInfo _sunLightInfo;

	private MapSkyInfo _skyInfo;

	private GameObject _gateObject;

	private HashSet<int> _noIntrusionInfo;

	public static string GATE_OBJECT_ASSET_NAME { get; } = "GatePoint";

	public static string GATE_ROOT_NAME { get; } = "GateRoot";

	private static string MAP_OBJECT_GROUP { get; } = "MapObjectGroup";

	private static string MAP_MOB_ROOT { get; } = "MapMobGroup";

	public MapInfo.Param Info => base.infoDic[base.no];

	public int gateLinkID
	{
		get
		{
			return _gateLinkID;
		}
		set
		{
			_gateLinkID = value;
		}
	}

	public Cycle.Type nowCycle
	{
		get
		{
			if (actScene != null)
			{
				_nowCycle = actScene.Cycle.nowType;
			}
			return _nowCycle;
		}
		set
		{
			_nowCycle = value;
		}
	}

	public IReadOnlyDictionary<int, List<GateInfo>> calcGateDic { get; private set; }

	public Dictionary<int, GateInfo> gateInfoDic { get; private set; } = new Dictionary<int, GateInfo>();

	public GateGroup gateGroup { get; private set; }

	public MapVisibleList mapVisibleList { get; private set; }

	public MapVisibleState mapVisibleState { get; private set; }

	public Transform mapObjectGroup { get; private set; }

	public Kind[] mapObjects { get; private set; }

	public GameObject mapMobGroup { get; private set; }

	public bool IsMobVisible
	{
		get
		{
			return isMobVisible;
		}
		set
		{
			isMobVisible = value;
		}
	}

	private bool isMobVisible { get; set; } = true;

	public SunLightInfo sunLightInfo
	{
		get
		{
			if (!(base.mapRoot == null))
			{
				return base.mapRoot.GetComponentCache(ref _sunLightInfo);
			}
			return null;
		}
	}

	public MapSkyInfo skyInfo
	{
		get
		{
			if (!(base.mapRoot == null))
			{
				return base.mapRoot.GetComponentCache(ref _skyInfo);
			}
			return null;
		}
	}

	public Transform gateParent { get; private set; }

	public Transform actionPointRoot { get; private set; }

	public Transform datePointRoot { get; private set; }

	public Transform areaStatePointRoot { get; private set; }

	private GameObject gateObject
	{
		get
		{
			if (!(_gateObject != null))
			{
				return _gateObject = Get();
			}
			return _gateObject;
			static GameObject Get()
			{
				GameObject asset = AssetBundleManager.LoadAsset("map/object/00.unity3d", GATE_OBJECT_ASSET_NAME, typeof(GameObject)).GetAsset<GameObject>();
				AssetBundleManager.UnloadAssetBundle("map/object/00.unity3d", isUnloadForceRefCount: false);
				return asset;
			}
		}
	}

	public Transform warpPointTransform
	{
		get
		{
			if (base.mapRoot == null)
			{
				return null;
			}
			foreach (Transform item in base.mapRoot.transform)
			{
				if (item.CompareTag("Action/WarpPoint"))
				{
					return item;
				}
			}
			return null;
		}
	}

	public Vector3 warpPoint
	{
		get
		{
			Transform transform = warpPointTransform;
			if (!(transform == null))
			{
				return transform.position;
			}
			return Vector3.zero;
		}
	}

	private IReadOnlyDictionary<int, Dictionary<int, List<NavigationInfo>>> navDic { get; set; }

	private IReadOnlyDictionary<int, List<GameObject>> bestPlaceAreas { get; set; }

	private HashSet<int> noIntrusionInfo
	{
		get
		{
			if (_noIntrusionInfo == null)
			{
				return _noIntrusionInfo = (Game.isAddH ? new HashSet<int>() : new HashSet<int>(from x in base.infoDic.Values
					where x.isH
					select x.No));
			}
			return _noIntrusionInfo;
		}
	}

	private ActionScene actScene { get; set; }

	private CancellationTokenSource mapSoundCancellation { get; set; }

	public BGM bgmID { get; private set; }

	public static SunLightInfo.Info.Type CycleToType(Cycle.Type cycle)
	{
		switch (cycle)
		{
		case Cycle.Type.WakeUp:
		case Cycle.Type.Morning:
			return SunLightInfo.Info.Type.Morning;
		case Cycle.Type.Daytime:
			return SunLightInfo.Info.Type.DayTime;
		case Cycle.Type.Evening:
			return SunLightInfo.Info.Type.Evening;
		case Cycle.Type.Night:
		case Cycle.Type.GotoMyHouse:
		case Cycle.Type.MyHouse:
			return SunLightInfo.Info.Type.Night;
		default:
			return SunLightInfo.Info.Type.DayTime;
		}
	}

	public static void CameraColorMask(Camera camera, bool enabled)
	{
		if (!(camera == null))
		{
			CameraEffectorColorMask componentInChildren = camera.GetComponentInChildren<CameraEffectorColorMask>();
			if (!(componentInChildren == null))
			{
				componentInChildren.Enabled = enabled;
			}
		}
	}

	public static void SetCameraClearFlags(Camera camera, bool isSky)
	{
		if (!(camera == null))
		{
			camera.clearFlags = (isSky ? CameraClearFlags.Skybox : CameraClearFlags.Depth);
		}
	}

	public static bool IsSpecialSecretBeach()
	{
		Cycle.Week week = (Cycle.Week)Game.saveData.week;
		if (week == Cycle.Week.Tuesday || week == Cycle.Week.Friday)
		{
			return true;
		}
		return false;
	}

	public void UpdateCameraFog()
	{
		SingletonInitializer<Game>.instance.cameraEffector.config.SetFog(RenderSettings.fog);
	}

	public bool IsPop(Base chara)
	{
		base.infoDic.TryGetValue(base.no, out var value);
		if (value != null && value.isGate && chara.isPopOK)
		{
			return base.no == chara.mapNo;
		}
		return false;
	}

	public override async UniTask ChangeAsync(int no, FadeCanvas.Fade fadeType = FadeCanvas.Fade.InOut, bool isForce = false)
	{
		if ((base.mapRoot != null && base.no == no && !isForce) || !base.infoDic.TryGetValue(no, out var value))
		{
			return;
		}
		base.isMapLoading = true;
		base.prevNo = base.no;
		base.no = no;
		if (actScene != null)
		{
			actScene.npcList.ForEach(delegate(NPC p)
			{
				p.SetActive(active: false);
			});
			if (actScene.fixChara != null)
			{
				actScene.fixChara.SetActive(active: false);
			}
			Player player = actScene.Player;
			if (player != null)
			{
				player.SetActive(active: false);
			}
		}
		await Manager.Scene.LoadBaseSceneAsync(new Manager.Scene.Data
		{
			bundleName = value.AssetBundleName,
			levelName = value.AssetName,
			fadeType = fadeType,
			onFadeIn = FadeIn,
			onLoad = delegate
			{
				OnLoad(fadeType);
			}
		});
	}

	private async UniTask FadeIn()
	{
		await UniTask.Yield();
		if (!(actScene == null))
		{
			ChangeCameraMapState();
		}
	}

	private async UniTask FadeOut()
	{
		await UniTask.Yield();
	}

	private void OnLoad(FadeCanvas.Fade fadeType)
	{
		if (fadeType == FadeCanvas.Fade.None)
		{
			ChangeCameraMapState();
		}
	}

	private void ChangeCameraMapState()
	{
		if (actScene == null)
		{
			return;
		}
		if (MonoBehaviourSingleton<CameraSystem>.Instance != null)
		{
			CameraStateDefinitionChange cameraState = actScene.CameraState;
			if (cameraState != null && cameraState.Mode == CameraMode.Other)
			{
				MonoBehaviourSingleton<CameraSystem>.Instance.UnregisterCameraState(MonoBehaviourSingleton<CameraSystem>.Instance.CurrentCameraStateDefinition);
			}
		}
		int num = base.no;
		if (num == 28 || (uint)(num - 30) <= 1u)
		{
			if (Manager.Config.ActData.ToiletTPS)
			{
				actScene.CameraState.ModeChangeForce(CameraMode.FPS);
			}
		}
		else
		{
			actScene.CameraState.ModeChangePrev();
		}
	}

	public void SetCycleToSunLight()
	{
		SetCycleToSunLight(Camera.main);
	}

	public void SetCycleToSunLight(Camera camera)
	{
		SunLightInfo.Info.Type type = CycleToType(nowCycle);
		if (!(sunLightInfo == null) && !sunLightInfo.Set(type, camera))
		{
			sunLightInfo.Set(null, camera);
		}
		UpdateCameraFog();
		if (skyInfo != null)
		{
			skyInfo.Change((int)type);
		}
	}

	public void LoadPlayerActionPoint()
	{
		if (SingletonInitializer<PlayerAction>.initialized)
		{
			GameObject root = SingletonInitializer<PlayerAction>.instance.LoadPoint();
			if (actScene != null)
			{
				actScene.miniMap.TopicIconUpdate(root);
			}
		}
	}

	public void ReloadPoint()
	{
		CreateGate(base.no);
		CreateAreaStatePoint(base.no);
		LoadPlayerActionPoint();
	}

	public void SettingMap()
	{
		int num = base.no;
		if (num == 9)
		{
			if (IsSpecialSecretBeach())
			{
				mapVisibleState.Set(1);
			}
			else
			{
				mapVisibleState.Set(0);
			}
		}
	}

	public NavigationInfo SearchRoute(int firstMapNo, int lastMapNo, Vector3? pos = null, HashSet<int> _not = null)
	{
		return GatePositionDistanceCheck(FindNavigateList(firstMapNo, lastMapNo), pos, _not);
	}

	public void PlayerMapWarp(int mapNo, Action act = null)
	{
		Player player = actScene.Player;
		int mapNo2 = player.mapNo;
		GateInfo gateInfo = NearGate(mapNo2, mapNo, player.position);
		if (gateInfo != null)
		{
			_gateLinkID = gateInfo.linkID;
		}
		else
		{
			NavigationInfo navigationInfo = SearchRoute(mapNo2, mapNo, player.position);
			if (navigationInfo != null && !((IReadOnlyCollection<int>)(object)navigationInfo.IDs).IsNullOrEmpty())
			{
				_gateLinkID = gateInfoDic[navigationInfo.IDs.Last()].linkID;
			}
		}
		act?.Invoke();
		player.mapNo = mapNo;
		Change(mapNo);
	}

	public GateInfo NearGate(int s, int e, Vector3? pos = null)
	{
		NavigationInfo navigationInfo = SearchRoute(s, e, pos);
		if (navigationInfo != null && !((IReadOnlyCollection<int>)(object)navigationInfo.IDs).IsNullOrEmpty())
		{
			return gateInfoDic[navigationInfo.IDs.Last()];
		}
		return null;
	}

	public void PlayerMapNearWarp(Base target)
	{
		Player player = actScene.Player;
		GateInfo gateInfo = NearGate(target.mapNo, player.mapNo);
		bool num = gateInfo != null;
		if (gateInfo == null)
		{
			gateInfo = calcGateDic[player.mapNo].Shuffle().First();
		}
		GateInfo gateInfo2 = gateInfoDic[gateInfo.linkID];
		target.mapNo = gateInfo.mapNo;
		Vector3 pos;
		Vector3 ang;
		if (num)
		{
			pos = gateInfo2.pos;
			ang = gateInfo2.ang;
		}
		else
		{
			pos = gateInfo.pos;
			ang = gateInfo.ang;
		}
		target.SetPositionAndRotation(pos, Quaternion.Euler(ang));
		target.SetActive(IsPop(target));
	}

	public List<NavigationInfo> FindNavigateList(int mapNo, int lastMapNo)
	{
		if (mapNo == lastMapNo)
		{
			return new List<NavigationInfo>();
		}
		return navDic[mapNo][lastMapNo];
	}

	public NavigationInfo GatePositionDistanceCheck(List<NavigationInfo> routes, Vector3? position, HashSet<int> _not)
	{
		return GatePositionDistanceCheckList(routes, position, _not).FirstOrDefault();
	}

	public List<NavigationInfo> GatePositionDistanceCheckList(List<NavigationInfo> routes, Vector3? position, HashSet<int> _not)
	{
		if (!routes.Any())
		{
			return routes;
		}
		List<NavigationInfo> list = new List<NavigationInfo>();
		List<NavigationInfo> list2 = ((_not != null) ? routes.Where((NavigationInfo v) => !v.IDs.Any((int id) => _not.Contains(id))).ToList() : routes);
		if (list2.IsNullOrEmpty())
		{
			list2 = routes;
		}
		foreach (NavigationInfo item in list2)
		{
			float num = item.distance;
			if (position.HasValue)
			{
				num += (gateInfoDic[item.IDs[0]].pos - position.Value).magnitude;
			}
			list.Add(new NavigationInfo(num, item.IDs));
		}
		return list.OrderBy((NavigationInfo p) => p.distance).ToList();
	}

	public Vector3[] MapCalcPosition(int mapNo, int gateID, Vector3 pos, int? prevID)
	{
		List<GateInfo> list = calcGateDic[mapNo];
		GateInfo gateInfo = ((!prevID.HasValue) ? null : list.Find((GateInfo gate) => gate.ID == prevID.Value));
		if (gateInfo != null && gateInfo.calc.TryGetValue(gateID, out var value))
		{
			int num = Illusion.Utils.Math.MinDistanceRouteIndex(value, pos);
			if (num != -1)
			{
				return value.Skip(num).ToArray();
			}
		}
		foreach (GateInfo item in list)
		{
			if (item.calc.TryGetValue(gateID, out var value2))
			{
				int num2 = Illusion.Utils.Math.MinDistanceRouteIndex(value2, pos);
				if (num2 != -1)
				{
					return value2.Skip(num2).ToArray();
				}
			}
		}
		return null;
	}

	protected override void Awake()
	{
		base.Awake();
	}

	protected override void Start()
	{
		base.Start();
		actScene = GetComponent<ActionScene>();
		LoadCalcGate();
		LoadNavigationInfo();
		LoadBestPlaceArea();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		mapSoundCancellation?.Cancel();
		mapSoundCancellation?.Dispose();
		mapSoundCancellation = null;
	}

	protected override void Reserve(UnityEngine.SceneManagement.Scene oldScene, UnityEngine.SceneManagement.Scene newScene)
	{
		base.Reserve(oldScene, newScene);
		if (base._isLock || base.mapRoot == null || base.isMapLoading)
		{
			return;
		}
		MapInfo.Param info = Info;
		Camera main = Camera.main;
		SetCycleToSunLight(main);
		SetCameraClearFlags(main, info.isSky);
		mapVisibleList = base.mapRoot.GetComponent<MapVisibleList>();
		mapVisibleState = base.mapRoot.GetComponent<MapVisibleState>();
		mapObjectGroup = base.mapRoot.transform.Find(MAP_OBJECT_GROUP);
		mapObjects = ((mapObjectGroup == null) ? null : mapObjectGroup.GetComponentsInChildren<Kind>(includeInactive: true));
		bool flag = true;
		switch (base.no)
		{
		case 14:
		case 28:
		case 30:
		case 31:
		{
			List<Tuple<OffMeshLink, NavMeshObstacle>> list = new List<Tuple<OffMeshLink, NavMeshObstacle>>();
			foreach (GameObject item in from p in base.mapRoot.Children()
				where p.name.IndexOf("DoorCloseEvent") != -1
				select p)
			{
				Door component = item.GetComponent<Door>();
				if (component == null)
				{
					continue;
				}
				NavMeshObstacle obstacle = component.GetObstacle();
				if (!(obstacle == null))
				{
					OffMeshLink component2 = item.GetComponent<OffMeshLink>();
					if (!(component2 == null))
					{
						list.Add(Tuple.Create(component2, obstacle));
					}
				}
			}
			list.Select((Tuple<OffMeshLink, NavMeshObstacle> p) => from _ in p.Item1.UpdateAsObservable().DistinctUntilChanged((Unit _) => p.Item2.enabled)
				select p).Merge().Subscribe(delegate(Tuple<OffMeshLink, NavMeshObstacle> item)
			{
				if (!(item.Item2 == null))
				{
					float carvingTimeToStationary = item.Item2.carvingTimeToStationary;
					Observable.Return(Unit.Default).Delay(TimeSpan.FromSeconds(carvingTimeToStationary + 0.1f)).Subscribe(delegate
					{
						list.ForEach(delegate(Tuple<OffMeshLink, NavMeshObstacle> p)
						{
							if (!(p.Item1 == null))
							{
								p.Item1.enabled = false;
								p.Item1.enabled = true;
							}
						});
					});
				}
			})
				.AddTo(base.mapRoot);
			break;
		}
		case 16:
			flag = false;
			break;
		}
		SettingMap();
		gateGroup = base.mapRoot.GetComponent<GateGroup>();
		if (gateGroup == null)
		{
			return;
		}
		int mapNo = info.No;
		CreateGate(mapNo);
		CreateActionPoint(mapNo);
		CreateDatePoint(mapNo);
		CreateAreaStatePoint(mapNo);
		CharaSetting(info);
		foreach (KeyValuePair<int, List<GameObject>> bestPlaceArea in bestPlaceAreas)
		{
			int key = bestPlaceArea.Key;
			foreach (GameObject item2 in bestPlaceArea.Value)
			{
				item2.SetActiveIfDifferent(key == base.no);
			}
		}
		LoadPlayerActionPoint();
		if (actScene != null)
		{
			MinimapVisible[] minimapVisible = actScene.GetMinimapVisible();
			if (minimapVisible != null)
			{
				MinimapVisible[] array = minimapVisible;
				for (int num = 0; num < array.Length; num++)
				{
					array[num].ChangeVisible(base.no);
				}
			}
			LoadSEMesh sEMeshVisible = actScene.GetSEMeshVisible();
			if (sEMeshVisible != null)
			{
				foreach (SEMesh item3 in sEMeshVisible.Mesh)
				{
					item3.ChangeVisible(base.no);
				}
			}
		}
		mapSoundCancellation?.Cancel();
		mapSoundCancellation?.Dispose();
		mapSoundCancellation = new CancellationTokenSource();
		if (flag)
		{
			UniTask.Void(async delegate(CancellationToken token)
			{
				await UniTask.WaitWhile(() => Manager.Scene.IsNowLoadingFade, PlayerLoopTiming.Update, token);
				token.ThrowIfCancellationRequested();
				ResetSoundComponents(isSkipEnv: false);
			}, mapSoundCancellation.Token);
		}
		Illusion.Game.Utils.Sound.InitMapSoundInfo();
	}

	private void CreateGate(int mapNo)
	{
		if (!calcGateDic.TryGetValue(mapNo, out var value))
		{
			return;
		}
		if (gateParent != null)
		{
			UnityEngine.Object.Destroy(gateParent.gameObject);
		}
		Transform transform = (gateParent = new GameObject(GATE_ROOT_NAME).transform);
		Transform transform3 = transform;
		transform3.SetParent(gateGroup.transform, worldPositionStays: false);
		List<Gate> gateList = gateGroup.gateList;
		gateList.Clear();
		foreach (GateInfo item in value)
		{
			Gate component = UnityEngine.Object.Instantiate(gateObject, transform3, worldPositionStays: false).GetComponent<Gate>();
			component.SetData(item);
			bool isActive;
			switch (component.mapNo)
			{
			case 9:
				isActive = Game.Player.isSecretBeachOpen;
				if (mapObjectGroup != null)
				{
					mapObjectGroup.Find("gate_close").SafeProcObject(delegate(Transform x)
					{
						x.gameObject.SetActive(!isActive);
					});
					mapObjectGroup.Find("gate_open").SafeProcObject(delegate(Transform x)
					{
						x.gameObject.SetActive(isActive);
					});
				}
				break;
			case 36:
				isActive = Game.Player.isSuiteRoomOpen;
				if (mapObjectGroup != null)
				{
					mapObjectGroup.Find("move_partion00").SafeProcObject(delegate(Transform x)
					{
						x.gameObject.SetActive(!isActive);
					});
				}
				break;
			default:
				isActive = true;
				break;
			}
			component.gameObject.SetActive(isActive);
			gateList.Add(component);
		}
		if (noIntrusionInfo.Any())
		{
			foreach (Gate item2 in gateList)
			{
				if (noIntrusionInfo.Contains(item2.mapNo))
				{
					item2.playerHitBox.gameObject.SetActive(value: false);
				}
			}
		}
		if (actScene != null)
		{
			actScene.miniMap.GateIconUpdate(gateGroup);
		}
	}

	private void CreateActionPoint(int mapNo)
	{
		Transform transform = (actionPointRoot = new GameObject("ActionPoints").transform);
		Transform transform3 = transform;
		transform3.SetParent(base.mapRoot.transform, worldPositionStays: false);
		foreach (string item in from s in CommonLib.GetAssetBundleNameListFromPath("map/actionpoint/", subdirCheck: true)
			orderby s
			select s)
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(item);
			if (int.TryParse(fileNameWithoutExtension, out var result) && result == mapNo)
			{
				CommonLib.LoadAsset<GameObject>(item, fileNameWithoutExtension, clone: true).transform.SetParent(transform3, worldPositionStays: false);
				AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: true);
			}
		}
		if (actScene != null)
		{
			actScene.miniMap.SetActionIcon(transform3);
		}
	}

	private void CreateDatePoint(int mapNo)
	{
		Transform transform = (datePointRoot = new GameObject("DatePoints").transform);
		Transform transform3 = transform;
		transform3.SetParent(base.mapRoot.transform, worldPositionStays: false);
		foreach (string item in from s in CommonLib.GetAssetBundleNameListFromPath("map/datepoint", subdirCheck: true)
			orderby s
			select s)
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(item);
			if (int.TryParse(fileNameWithoutExtension, out var result) && result == mapNo)
			{
				CommonLib.LoadAsset<GameObject>(item, fileNameWithoutExtension, clone: true).transform.SetParent(transform3, worldPositionStays: false);
				AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: true);
			}
		}
		if (actScene != null)
		{
			actScene.miniMap.SetDateIcon(transform3);
		}
	}

	private void CreateAreaStatePoint(int mapNo)
	{
		if (areaStatePointRoot != null)
		{
			UnityEngine.Object.Destroy(areaStatePointRoot.gameObject);
		}
		Transform transform = (areaStatePointRoot = new GameObject("AreaStatePoints").transform);
		Transform transform3 = transform;
		transform3.SetParent(base.mapRoot.transform, worldPositionStays: false);
		foreach (string item in from s in CommonLib.GetAssetBundleNameListFromPath("map/areastatepoint", subdirCheck: true)
			orderby s
			select s)
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(item);
			if (int.TryParse(fileNameWithoutExtension, out var result) && result == mapNo)
			{
				CommonLib.LoadAsset<GameObject>(item, fileNameWithoutExtension, clone: true).transform.SetParent(transform3, worldPositionStays: false);
				AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: true);
			}
		}
	}

	private void CharaSetting(MapInfo.Param info)
	{
		if (actScene == null)
		{
			return;
		}
		Player player = actScene.Player;
		player.isLesMotionPlay = true;
		player.isOnanismMotionPlay = true;
		Gate gate = gateGroup.gateList.Find((Gate p) => p.ID == _gateLinkID);
		Transform playerTrans;
		if (gate != null)
		{
			playerTrans = gate.playerTrans;
		}
		else
		{
			player.HitGateReset();
			playerTrans = warpPointTransform;
		}
		if (playerTrans != null)
		{
			player.SetPositionAndRotation(playerTrans);
			actScene.CameraState.SetAngle(playerTrans.eulerAngles);
			NPC chaser = player.chaser;
			if (chaser != null && chaser.mapNo == player.mapNo)
			{
				float num = 0.5f;
				Vector3 vector = player.rotation * Vector3.right * num;
				chaser.position = playerTrans.position + vector;
			}
		}
		if (!Program.isADVProcessing && !Manager.Scene.NowSceneNames.Contains("H"))
		{
			actScene.npcList.ForEach(delegate(NPC p)
			{
				p.SetActive(IsPop(p));
			});
			actScene.fixChara.SafeProcObject(delegate(Fix x)
			{
				x.SetActive(IsPop(x));
			});
			player.SetActive(IsPop(player));
		}
	}

	private void LoadCalcGate()
	{
		Dictionary<int, List<GateInfo>> dictionary = new Dictionary<int, List<GateInfo>>();
		foreach (string item2 in from s in CommonLib.GetAssetBundleNameListFromPath("map/list/calcgateinfo/", subdirCheck: true)
			orderby s
			select s)
		{
			ExcelData[] allAssets = AssetBundleManager.LoadAllAsset(item2, typeof(ExcelData)).GetAllAssets<ExcelData>();
			foreach (ExcelData excelData in allAssets)
			{
				if (!int.TryParse(excelData.name, out var result))
				{
					continue;
				}
				List<GateInfo> list = GateInfo.Create(excelData.list);
				list.ForEach(delegate(GateInfo info)
				{
					gateInfoDic[info.ID] = info;
				});
				if (!dictionary.TryGetValue(result, out var value))
				{
					value = (dictionary[result] = new List<GateInfo>());
				}
				foreach (GateInfo item in list)
				{
					value.RemoveAll((GateInfo x) => x.ID == item.ID);
				}
				value.AddRange(list);
			}
			AssetBundleManager.UnloadAssetBundle(item2, isUnloadForceRefCount: false);
		}
		calcGateDic = dictionary;
	}

	private void LoadNavigationInfo()
	{
		Dictionary<int, Dictionary<int, List<NavigationInfo>>> dictionary = new Dictionary<int, Dictionary<int, List<NavigationInfo>>>();
		foreach (string item in from s in CommonLib.GetAssetBundleNameListFromPath("map/list/navigationinfo/", subdirCheck: true)
			orderby s
			select s)
		{
			ActionGame.NavigationInfo[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(ActionGame.NavigationInfo)).GetAllAssets<ActionGame.NavigationInfo>();
			foreach (ActionGame.NavigationInfo obj in allAssets)
			{
				int key = int.Parse(obj.name);
				if (!dictionary.TryGetValue(key, out var value))
				{
					value = (dictionary[key] = new Dictionary<int, List<NavigationInfo>>());
				}
				foreach (ActionGame.NavigationInfo.Param item2 in obj.param)
				{
					int lastMapID = item2.lastMapID;
					if (!value.TryGetValue(lastMapID, out var value2))
					{
						value2 = (value[lastMapID] = new List<NavigationInfo>());
					}
					value2.AddRange(item2.infos.Select((ActionGame.NavigationInfo.Info info) => new NavigationInfo(info.distance, info.IDs)));
				}
			}
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: false);
		}
		navDic = dictionary;
	}

	private void LoadBestPlaceArea()
	{
		Dictionary<int, List<GameObject>> dictionary = new Dictionary<int, List<GameObject>>();
		if (!(actScene == null))
		{
			string text = (from s in CommonLib.GetAssetBundleNameListFromPath("map/placearea/", subdirCheck: true)
				orderby s descending
				select s).FirstOrDefault();
			if (!text.IsNullOrEmpty())
			{
				Transform transform = new GameObject("PlaceAreaRoot").transform;
				transform.SetParent(actScene.transform, worldPositionStays: false);
				GameObject[] allAssets = AssetBundleManager.LoadAllAsset(text, typeof(GameObject)).GetAllAssets<GameObject>();
				foreach (GameObject gameObject in allAssets)
				{
					Area component = gameObject.GetComponent<Area>();
					if (!(component == null))
					{
						int mapNo = component.mapNo;
						if (!dictionary.TryGetValue(mapNo, out var value))
						{
							value = (dictionary[mapNo] = new List<GameObject>());
						}
						GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject, transform, worldPositionStays: false);
						gameObject2.name = gameObject.name;
						value.Add(gameObject2);
					}
				}
				AssetBundleManager.UnloadAssetBundle(text, isUnloadForceRefCount: false);
			}
		}
		bestPlaceAreas = dictionary;
	}

	public void PlayBGM()
	{
		ResetSoundComponents(isSkipEnv: true);
	}

	private void ResetSoundComponents(bool isSkipEnv)
	{
		if (!Illusion.Game.Utils.Sound.FootStepDefaultData.ContainsKey(base.no))
		{
			Illusion.Game.Utils.Sound.FootStepAreaDefaultType = FootStepSE.Rock;
		}
		else
		{
			Illusion.Game.Utils.Sound.FootStepAreaDefaultType = Illusion.Game.Utils.Sound.FootStepDefaultData[base.no];
		}
		int[] array = Illusion.Game.Utils.Sound.FootStepAreaTypes.Keys.ToArray();
		foreach (int key in array)
		{
			Illusion.Game.Utils.Sound.FootStepAreaTypes[key] = Illusion.Game.Utils.Sound.FootStepAreaDefaultType;
		}
		FootStepSEArea[] componentsInChildren = base.mapRoot.GetComponentsInChildren<FootStepSEArea>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].ForceUpdate();
		}
		BGMArea.PlayInfo value;
		bool flag = Illusion.Game.Utils.Sound.MapBGMTable.TryGetValue(base.no, out value);
		if (actScene != null && actScene.Cycle.isAction)
		{
			if (!actScene.IsInChargeBGM)
			{
				BGM bGM = BGM.MapMoveMorning;
				bool flag2 = false;
				if (flag)
				{
					if (value.BGMID > -1)
					{
						bGM = (BGM)value.BGMID;
						flag2 = true;
					}
				}
				else
				{
					switch (nowCycle)
					{
					case Cycle.Type.Morning:
						bGM = BGM.MapMoveMorning;
						flag2 = true;
						break;
					case Cycle.Type.Daytime:
						bGM = BGM.MapMoveNoon;
						flag2 = true;
						break;
					case Cycle.Type.Evening:
						bGM = BGM.MapMoveEvening;
						flag2 = true;
						break;
					case Cycle.Type.Night:
						bGM = BGM.MapMoveNight;
						flag2 = true;
						break;
					}
				}
				if (actScene.Player.chaser != null)
				{
					bGM = BGM.Follow;
					flag2 = true;
				}
				foreach (NPC npc in actScene.npcList)
				{
					if (npc.mapNo == base.no && (npc.isOnanism || npc.isLesbian))
					{
						bGM = BGM.HScenePeep;
						flag2 = true;
						break;
					}
				}
				if (flag2)
				{
					bgmAction(bGM);
				}
			}
			if (flag)
			{
				if (value.EnableVolumeModification)
				{
					volumeAction(value.Volume);
				}
			}
			else
			{
				volumeAction(1f);
			}
		}
		if (!isSkipEnv && Illusion.Game.Utils.Sound.EnvAreaTable.TryGetValue($"EnvArea_{base.no:00}", out var value2))
		{
			UnityEngine.Object.Instantiate(value2, base.mapRoot.transform);
		}
		void bgmAction(BGM bgmID)
		{
			GameObject obj = new GameObject("BGM");
			obj.transform.SetParent(base.mapRoot.transform, worldPositionStays: false);
			obj.AddComponent<BGMArea>().BGM = bgmID;
			this.bgmID = bgmID;
		}
		void volumeAction(float volume)
		{
			GameObject obj = new GameObject("BGMVolume");
			obj.transform.SetParent(base.mapRoot.transform, worldPositionStays: false);
			obj.AddComponent<BGMVolume>().Volume = volume;
		}
	}
}
