using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ActionGame;
using ActionGame.Chara;
using ActionGame.H;
using ActionGame.MapObject;
using ActionGame.Point;
using ChaCustom;
using Cysharp.Threading.Tasks;
using H;
using HSceneUtility;
using Illusion;
using Illusion.Component;
using Illusion.CustomAttributes;
using Illusion.Extensions;
using Illusion.Game;
using IllusionUtility.GetUtility;
using Manager;
using RootMotion.FinalIK;
using SaveData;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityStandardAssets.ImageEffects;

public class HSceneProc : MonoBehaviour
{
	public enum EExperience
	{
		funare,
		nare
	}

	[Serializable]
	public class PathName
	{
		public string assetpath = "";

		public string file = "";
	}

	[Serializable]
	public class Category
	{
		public int category;

		public string fileMove = "";
	}

	[Serializable]
	public class MaleParameter
	{
		public PathName path = new PathName();

		public List<int> lstIdLayer = new List<int>();

		public bool isHitObject;

		public string fileHit = "";

		public string fileHitObject = "";

		public string fileLookAt = "";

		public string fileMotionNeck = "";

		public string fileMetaballCtrl = "";

		public string fileCondomCtrl = "";
	}

	[Serializable]
	public class FemaleParameter
	{
		public PathName path = new PathName();

		public List<int> lstIdLayer = new List<int>();

		public List<int> lstFrontAndBehind = new List<int>();

		public bool isHitObject;

		public string fileHit = "";

		public string fileHitObject = "";

		public bool isYure;

		public string fileYure = "";

		public string fileDynamicBoneRef = "";

		public string fileMotionNeck = "";

		public string fileSe = "";

		public string fileSiruPaste = "";

		public string fileAibuEnableMotion = "";

		public string fileReaction = "";

		public string fileItem = "";

		public bool isAnal;

		public string nameCamera = "";
	}

	[Serializable]
	public class AnimationListInfo
	{
		public int id;

		public HFlag.EMode mode = HFlag.EMode.none;

		public string nameAnimation = "";

		public List<Category> lstCategory = new List<Category>();

		public int posture;

		public int kindHoushi;

		public int isExperience;

		public PathName pathMapObjectNull = new PathName();

		public int useDesk;

		public int useChair;

		public PathName pathMaleBase = new PathName();

		public MaleParameter paramMale = new MaleParameter();

		public PathName pathFemaleBase = new PathName();

		public FemaleParameter paramFemale = new FemaleParameter();

		public PathName pathFemaleBase1 = new PathName();

		public FemaleParameter paramFemale1 = new FemaleParameter();

		public int numCtrl = -1;

		public int sysTaii;

		public string nameCameraIdle = "";

		public string nameCameraKiss = "";

		public List<int> lstAibuSpecialItem = new List<int>();

		public bool isFemaleInitiative;

		public int houshiLoopActionW = -1;

		public int houshiLoopActionS = -1;

		public bool isSplash;

		public int numMainVoiceID = -1;

		public int numSubVoiceID = -1;

		public int numVoiceKindID = -1;

		public int numMaleSon = -1;

		public int numFemaleUpperCloth = -1;

		public int numFemaleLowerCloth = -1;

		public int numFemaleUpperCloth1 = -1;

		public int numFemaleLowerCloth1 = -1;

		public bool isRelease;

		public int stateRestriction;

		public int[] mainHoushi3PShortVoicePtns = new int[4];

		public int[] subHoushi3PShortVoicePtns = new int[4];
	}

	[Serializable]
	public class MapObjectNullInfo
	{
		public GameObject obj;

		public Vector3 pos;

		public Quaternion rot;

		public int layer;
	}

	[Serializable]
	public class MapObjectNull
	{
		public List<MapObjectNullInfo> lstDesk = new List<MapObjectNullInfo>();

		public List<MapObjectNullInfo> lstChair = new List<MapObjectNullInfo>();

		public List<MapObjectNullInfo> lstOther = new List<MapObjectNullInfo>();
	}

	public class MaleConfig
	{
		public bool visibleSimple;

		public Color color;

		public bool cloth;

		public bool accessoryMain;

		public bool accessorySub;

		public bool shoes;
	}

	public class LightData
	{
		public Light light;

		public Color initColor = Color.white;

		public Vector2 initRot = Vector2.zero;

		public float initIntensity = 1f;

		public Vector2 calcRot = Vector2.zero;
	}

	public class MapDependent
	{
		public int socks = -1;

		public int shoes = -1;

		public int glove = -1;
	}

	public struct HExpBase
	{
		public float common;

		public float bitch;
	}

	public struct HExpBonus
	{
		public float min;

		public float max;
	}

	public enum VoicePlayShuffleKind
	{
		PoseChange
	}

	public class BoneOffSet
	{
		public Vector3 pos = Vector3.zero;

		public Vector3 rot = Vector3.zero;

		public Vector3 scl = Vector3.zero;
	}

	public int finishADV;

	public HFlag flags;

	public CrossFade crossfade;

	public HSprite sprite;

	public ParentObjectCtrl parentObjectBaseFemale;

	public ParentObjectCtrl parentObjectBaseMale;

	public ParentObjectCtrl parentObjectBaseFemale1;

	public ParentObjectCtrl parentObjectFemale;

	public ParentObjectCtrl parentObjectMale;

	public ParentObjectCtrl parentObjectFemale1;

	public HandCtrl hand;

	public HandCtrl hand1;

	public Lookat_dan lookDan;

	public Lookat_dan lookDan1;

	public YureCtrl yure;

	public YureCtrl yure1;

	public YureCtrl yureMale;

	public GameObject objMetaball;

	public Transform trsMainCam;

	public HitCollisionEnableCtrl hitcollisionFemale;

	public HitCollisionEnableCtrl hitcollisionFemale1;

	public HitCollisionEnableCtrl hitcollisionMale;

	public HitReaction hitreaction;

	public HitReaction hitreaction1;

	public SiruPasteCtrl siru;

	public SiruPasteCtrl siru1;

	public FaceListCtrl face;

	public FaceListCtrl face1;

	public HVoiceCtrl voice;

	public HParticleCtrl particle;

	public HParticleCtrl particle1;

	public HMotionEyeNeckFemale eyeneckFemale;

	public HMotionEyeNeckFemale eyeneckFemale1;

	public HMotionEyeNeckMale eyeneckMale;

	public HMotionEyeNeckMale eyeneckMale1;

	public AutoRely rely;

	public HSeCtrl se;

	public DynamicBoneReferenceCtrl dynamicCtrl;

	public DynamicBoneReferenceCtrl dynamicCtrl1;

	public AnimatorLayerCtrl alCtrl;

	public AnimatorLayerCtrl alCtrl1;

	public GameObject objMoveAxis;

	public HSceneGuideObject guideObject;

	public PhysicsRaycaster raycaster;

	public string nameMap = "教室1-1";

	public Cycle.Type defType;

	public OpenHData.Data dataH;

	public clothesFileControl clothCustomCtrl;

	public Light lightCamera;

	public HScene.AddParameter addParameter = new HScene.AddParameter();

	public GameScreenShot gss;

	public int countKiss;

	public bool myRoom;

	public bool spHpoint;

	public bool changeTaii = true;

	public bool actYukaTaii;

	public bool Orgasm;

	[Header("---< アニメーション変更 >---")]
	public List<int> categorys = new List<int>();

	public HFlag.EMode modeChange;

	public int idList;

	[Button("ChangeAnimatorTest", "アニメーション変更", new object[] { })]
	public int ChangeAnimatorButton;

	[Space]
	[Button("ChangeCloth", "服全部消す", new object[] { })]
	public int ChangeClothButton;

	[Space]
	[Button("MapObjectDelete", "椅子とか？消す", new object[] { })]
	public int MapObjectDeleteButton;

	[Header("時間帯")]
	[Button("SetDayTime", "昼間に設定", new object[] { })]
	public int SetDayTimeButton;

	[Button("SetEvening", "夕方に設定", new object[] { })]
	public int SetEveningButton;

	[Button("SetNight", "夜に設定", new object[] { })]
	public int SetNightButton;

	[Space]
	[Label("取得したいマップオブジェクトの名前")]
	public string nameMapObject;

	[Button("GetMapObjectKind", "マップオブジェクトの取得", new object[] { })]
	public int GetMapObjectKindButton;

	private List<ChaControl> lstFemale = new List<ChaControl>();

	private List<MotionIK> lstMotionIK = new List<MotionIK>();

	private ChaControl male;

	private ChaControl male1;

	[SerializeField]
	private ObiCtrl ctrlObi;

	private ItemObject item = new ItemObject();

	private ActionMap map;

	private Kind kindMap;

	private List<int> useCategorys = new List<int>();

	private Vector3 nowHpointDataPos = Vector3.zero;

	private string nowHpointData = "HPointBase";

	private List<HPointData> closeHpointData = new List<HPointData>();

	private bool useInitPosPoint = true;

	private bool nowInitPoint = true;

	private bool closeInitPoint;

	private Vector3 HpointJudgePos = Vector3.zero;

	private List<AnimationListInfo>[] lstAnimInfo = new List<AnimationListInfo>[8];

	private List<AnimationListInfo>[] lstUseAnimInfo = new List<AnimationListInfo>[8];

	private List<HActionBase> lstProc = new List<HActionBase>();

	private Dictionary<int, List<Collider>> dicCharaCollder = new Dictionary<int, List<Collider>>();

	[SerializeField]
	private MapObjectNull mapObjectNull = new MapObjectNull();

	private MaleConfig maleConfig = new MaleConfig();

	private List<bool> lstOldFemaleVisible = new List<bool>();

	private List<bool> lstOldMaleVisible = new List<bool>();

	private Vector3 initPos = Vector3.zero;

	private Quaternion initRot = Quaternion.identity;

	private Vector3 initKindOffsetPos = Vector3.zero;

	private Vector3 initKindOffsetRot = Vector3.zero;

	private List<int> lstInitCategory = new List<int>();

	private bool isInvited;

	private Vector3 loadLocalPositon = Vector3.zero;

	private Vector3 loadLocalRotation = Vector3.zero;

	private LightData lightData = new LightData();

	private bool isEasyPlace;

	private int appointAction = -1;

	private List<int> lstCoordinateType = new List<int> { 0 };

	private List<List<int>> clothStates;

	private List<List<bool>> accessoryStates;

	private int maleCoordinateType;

	private bool isFirstPlayMasturbation;

	private bool isMasturbationInitPoint;

	private int hScenePlayCount;

	private ShuffleRand[] voicePlayShuffle = new ShuffleRand[5];

	private int progressAdd = 7;

	private int nFreeSecret;

	private Dictionary<ShapeBodyInfoFemale.CorrectKeyName, BoneOffSet> backUPBoneOffset = new Dictionary<ShapeBodyInfoFemale.CorrectKeyName, BoneOffSet>();

	private DepthOfField dof;

	private Transform dofFocalTrs;

	private RuntimeAnimatorController changeRac;

	private bool bChangePoint = true;

	public List<HScene.HObjOffSetData> offSetData = new List<HScene.HObjOffSetData>();

	private Dictionary<int, HExpBase> dicHexpBase = new Dictionary<int, HExpBase>();

	private Dictionary<int, HExpBonus> dicHexpBonus = new Dictionary<int, HExpBonus>();

	private Dictionary<int, Dictionary<int, int>> dicExpAddTaii = new Dictionary<int, Dictionary<int, int>>();

	private const string expAddTaiiListName = "AddTaii_ab";

	private Dictionary<int, Dictionary<int, List<int>>> dicShopAddTaii = new Dictionary<int, Dictionary<int, List<int>>>();

	private const string shopAddTaiiListName = "shopaddtaii";

	private const string ishigakiGateClose = "gate_close";

	private const string ishigakiGateOpen = "gate_open";

	private const string ishigakiGateClose_Col = "gate_close_col";

	private const string ishigakiGateOpen_Col = "gate_open_col";

	private int openIdleState = Animator.StringToHash("open_Idle");

	private int closeIdleState = Animator.StringToHash("close_Idle");

	private const string suiteRoomPartition = "move_partion00";

	public bool isEnd { get; private set; }

	public void OnBack()
	{
		Scene.Unload();
	}

	public void OnTitle()
	{
		Illusion.Game.Utils.Scene.ReturnTitleForget();
	}

	private IEnumerator Start()
	{
		ActionScene actscene = SingletonInitializer<ActionScene>.instance;
		flags.isDebug = false;
		flags.isUseImmediatelyFinishButton = false;
		base.enabled = false;
		if (actscene == null)
		{
			map = base.gameObject.AddComponent<ActionMap>();
		}
		else
		{
			map = actscene.GetComponent<ActionMap>();
		}
		yield return new WaitUntil(() => map.infoDic != null);
		Character.enableCorrectHandSize = false;
		lstInitCategory.Clear();
		int freeHMapNo = -1;
		int freeHTimeZone = -1;
		int freeHStatus = -1;
		int freeHStage1 = -1;
		int freeHStage2 = -1;
		bool isLoadPeepMap = false;
		bool flag;
		if (dataH != null)
		{
			flags.lstHeroine = dataH.lstFemale;
			flags.player = dataH.player;
			flags.newHeroine = dataH.newHeroione;
			initPos = dataH.position;
			initRot = dataH.rotation;
			flags.isFreeH = dataH.isFreeH;
			isInvited = dataH.isInvited;
			flags.isMasturbationFound = dataH.isFound;
			freeHMapNo = dataH.mapNoFreeH;
			freeHTimeZone = dataH.timezoneFreeH;
			nFreeSecret = dataH.SecretBeachStateFreeH;
			freeHStatus = dataH.statusFreeH;
			freeHStage1 = dataH.stageFreeH1;
			freeHStage2 = dataH.stageFreeH2;
			isEasyPlace = dataH.isEasyPlace;
			isLoadPeepMap = dataH.isLoadPeepLoom;
			appointAction = dataH.appoint;
			isFirstPlayMasturbation = dataH.isFirstPlayMasturbation;
			isMasturbationInitPoint = dataH.isMasturbationInitPoint;
			lstCoordinateType.Clear();
			lstCoordinateType.AddRange(dataH.lstFemaleCoordinateType);
			clothStates = dataH.clothStates;
			accessoryStates = dataH.accessoryStates;
			maleCoordinateType = dataH.maleCoordinateType;
			hScenePlayCount = dataH.hScenePlayCount;
			categorys.Clear();
			if (dataH.peepCategory.Any((int c) => MathfEx.IsRange(1100, c, 1199, isEqual: true)))
			{
				categorys.AddRange(dataH.peepCategory);
			}
			else if (dataH.kind != null)
			{
				kindMap = dataH.kind;
				categorys.AddRange(kindMap.categoryes);
			}
			else if (dataH.peepCategory.Any())
			{
				categorys.AddRange(dataH.peepCategory);
			}
			else if (appointAction != -1)
			{
				if (MathfEx.IsRange(0, appointAction, 4, isEqual: true) || appointAction == 7 || appointAction == 10)
				{
					categorys.Add(0);
				}
				else if (MathfEx.IsRange(5, appointAction, 6, isEqual: true))
				{
					categorys.Add(4);
				}
				else if (appointAction == 8)
				{
					categorys.Add(1205);
				}
				else if (appointAction == 9)
				{
					categorys.Add(1202);
				}
			}
			else if (dataH.state != -1)
			{
				bool num = isOnaniState(dataH.state);
				flag = isOnani(flags.lstHeroine[0]);
				if (!num || flag)
				{
					int[] array = new int[13]
					{
						0, 4, 0, 9, 7, 8, 1010, 1013, 1014, 1015,
						0, 1016, 1017
					};
					categorys.Add(array[dataH.state]);
				}
				else
				{
					int[] array2 = new int[13]
					{
						0, 4, 0, 9, 7, 8, 0, 0, 0, 0,
						0, 0, 0
					};
					categorys.Add(array2[dataH.state]);
				}
			}
			else
			{
				GlobalMethod.DebugLog("カテゴリーが取得できなかった", 1);
				categorys.Add(1);
			}
		}
		flag = categorys[0] >= 1010 && categorys[0] < 1100;
		bool isWaitPointPoisitionSet = false;
		if (flags.lstHeroine.Count != 0 && (bool)flags.lstHeroine[0].charaBase && flags.lstHeroine[0].charaBase.isArrival && map.no == flags.lstHeroine[0].charaBase.mapNo && appointAction == -1 && hScenePlayCount == 0 && !flag)
		{
			WaitPoint wp = flags.lstHeroine[0].charaBase.wpData.wp;
			initPos = wp.transform.TransformPoint(wp.offsetHPos);
			initRot = wp.transform.rotation * Quaternion.Euler(wp.offsetHAngle);
			if ((bool)map.mapObjectGroup)
			{
				wp.kindList.ForEach(delegate(WaitPoint.KindMover kind)
				{
					Kind[] mapObjects = map.mapObjects;
					foreach (Kind kind2 in mapObjects)
					{
						if (kind.name == kind2.name)
						{
							kind2.transform.Translate(wp.offsetHPos);
							kind2.transform.rotation *= Quaternion.Euler(wp.offsetHAngle);
						}
					}
				});
			}
			isWaitPointPoisitionSet = true;
		}
		else if (dataH != null)
		{
			GameObject gameObject = new GameObject();
			gameObject.transform.SetPositionAndRotation(initPos, initRot);
			initPos = gameObject.transform.TransformPoint(dataH.offsetPos);
			initRot = gameObject.transform.rotation * Quaternion.Euler(dataH.offsetAngle);
			UnityEngine.Object.Destroy(gameObject);
		}
		lstInitCategory.AddRange(categorys);
		dof = flags.ctrlCamera.GetComponent<DepthOfField>();
		dofFocalTrs = ((dof != null) ? dof.focalTransform : null);
		if (flags.isFreeH)
		{
			map.Change(freeHMapNo, FadeCanvas.Fade.None);
			yield return new WaitUntil(() => map.mapRoot != null);
			GameObject gameObject2 = map.mapRoot.transform.FindLoop("h_free");
			if ((bool)gameObject2)
			{
				initPos = gameObject2.transform.position;
				initRot = gameObject2.transform.rotation;
			}
			if (freeHTimeZone != -1)
			{
				map.sunLightInfo.Set((SunLightInfo.Info.Type)(0 + freeHTimeZone), flags.ctrlCamera.thisCmaera);
				map.UpdateCameraFog();
				if (map.skyInfo != null)
				{
					SunLightInfo.Info.Type type = (SunLightInfo.Info.Type)(0 + freeHTimeZone);
					map.skyInfo.Change((int)type);
				}
			}
			if (dof != null)
			{
				dof.focalTransform = dofFocalTrs;
			}
			if (freeHStage1 != -1)
			{
				SetState(flags.lstHeroine[0], freeHStage1);
			}
			if (flags.lstHeroine.Count > 1)
			{
				SetState(flags.lstHeroine[1], freeHStage2);
			}
			switch (freeHStatus)
			{
			case 0:
				flags.lstHeroine[0].SetMenstruationsDay(0);
				break;
			default:
				flags.lstHeroine[0].SetMenstruationsDay(4);
				break;
			case -1:
				break;
			}
			MapChangeObject();
		}
		else if ((bool)actscene)
		{
			actscene.Map.SetCycleToSunLight();
			if (dof != null)
			{
				dof.focalTransform = dofFocalTrs;
			}
			MapChangeObject();
		}
		int startMapNo = -1;
		string startObjName = "";
		if (map.no == 10 || map.no == 18 || map.no == 22)
		{
			startObjName = "h_free";
		}
		bool myRoom = actscene != null && actscene.Cycle.withHeroine != null && (actscene.Cycle.withHeroineSituation == 0 || actscene.Cycle.withHeroineSituation == 1);
		if (appointAction == 0)
		{
			List<GameObject> list = GlobalMethod.LoadAllFolder<GameObject>("h/common/", $"HPoint_{(map ? map.no : 10)}");
			if (list.Count != 0)
			{
				GameObject obj = UnityEngine.Object.Instantiate(list[list.Count - 1]);
				GameObject gameObject3 = obj.transform.FindLoop("yuka00");
				if ((bool)gameObject3)
				{
					initPos = gameObject3.transform.position;
					initRot = gameObject3.transform.rotation;
					startObjName = "";
				}
				UnityEngine.Object.Destroy(obj);
			}
		}
		else if (myRoom)
		{
			int num2 = (map ? map.no : 10);
			List<GameObject> list2 = GlobalMethod.LoadAllFolder<GameObject>("h/common/", $"HPoint_{num2}");
			if (list2.Count != 0)
			{
				GameObject obj2 = UnityEngine.Object.Instantiate(list2[list2.Count - 1]);
				GameObject gameObject4 = obj2.transform.FindLoop("yuka00");
				if ((bool)gameObject4)
				{
					initPos = gameObject4.transform.position;
					initRot = gameObject4.transform.rotation;
					startObjName = "";
				}
				UnityEngine.Object.Destroy(obj2);
			}
		}
		if (categorys.Any((int c) => c >= 2000) || categorys.Contains(1302))
		{
			LoadSpecialMapStartPosition(out startMapNo, out startObjName);
		}
		else if (isLoadPeepMap)
		{
			if (map.no == 13 || map.no == 28 || map.no == 30 || map.no == 31)
			{
				startMapNo = 34;
				startObjName = "h_nozoki";
			}
			else if (map.no == 14)
			{
				startMapNo = 15;
				startObjName = "h_nozoki";
			}
			else if (map.no == 32)
			{
				startMapNo = 27;
				startObjName = "h_free";
			}
		}
		else if (!flags.isFreeH)
		{
			LoadSpecialMapStartPosition(out startMapNo, out startObjName);
		}
		else
		{
			startMapNo = freeHMapNo;
			startObjName = "h_free";
		}
		if (flags.isFreeH)
		{
			if (categorys.Any((int c) => MathfEx.IsRange(1010, c, 1099, isEqual: true) || MathfEx.IsRange(1100, c, 1199, isEqual: true)))
			{
				bool isLesbian = categorys.Any((int c) => MathfEx.IsRange(1100, c, 1199, isEqual: true));
				List<GameObject> list3 = GlobalMethod.LoadAllFolder<GameObject>("h/common/", "HPoint_Add_" + map.no);
				if (list3.Count != 0)
				{
					GameObject objPointFree = UnityEngine.Object.Instantiate(list3[list3.Count - 1]);
					HPointData[] datas = objPointFree.GetComponentsInChildren<HPointData>(includeInactive: true);
					yield return null;
					HPointData[] array3 = datas;
					foreach (HPointData hPointData in array3)
					{
						if (hPointData.category.Any((int c) => (!isLesbian) ? (c == 1012) : (c == 1102)))
						{
							hPointData.MoveOffset();
							initPos = hPointData.transform.position;
							initRot = hPointData.transform.rotation;
							startObjName = "";
							hPointData.ObjectOffset();
							hPointData.GetKindObject(map.mapObjectGroup);
							hPointData.BackupObjectOffset();
							offSetData.Clear();
							hPointData.GetOffsetData(ref offSetData);
							break;
						}
					}
					UnityEngine.Object.Destroy(objPointFree);
				}
			}
			else if (categorys.Any((int c) => MathfEx.IsRange(3000, c, 3099, isEqual: true)))
			{
				GlobalMethod.HPointAppointNullDatail detail = new GlobalMethod.HPointAppointNullDatail
				{
					mode = HFlag.EMode.houshi3P,
					idMap = (map ? map.no : 0),
					kindGet = 0,
					nameGetNull = "3p_yuka"
				};
				offSetData.Clear();
				GlobalMethod.HPointAppointNullGetData hPointAppointNull = GlobalMethod.GetHPointAppointNull(detail, ref offSetData, map.mapObjectGroup);
				initPos = hPointAppointNull.pos;
				initRot = hPointAppointNull.rot;
				startObjName = "";
			}
		}
		if (startMapNo != -1)
		{
			map.Change(startMapNo, FadeCanvas.Fade.None);
			yield return new WaitUntil(() => !map.isMapLoading);
			flags.ctrlCamera.VisibleFroceVanish(_visible: true);
			flags.ctrlCamera.CleraVanishCollider();
			if (dof != null)
			{
				dof.focalTransform = dofFocalTrs;
			}
		}
		if (!startObjName.IsNullOrEmpty())
		{
			GameObject gameObject5 = map.mapRoot.transform.FindLoop(startObjName);
			if ((bool)gameObject5)
			{
				initPos = gameObject5.transform.position;
				initRot = gameObject5.transform.rotation;
			}
		}
		lightData.light = lightCamera;
		lightData.initColor = lightCamera.color;
		lightData.initRot = new Vector2(lightCamera.transform.localEulerAngles.x, lightCamera.transform.localEulerAngles.y);
		lightData.initIntensity = lightCamera.intensity;
		GetMapObject();
		AllDoorOpenClose(_isOpen: false);
		if (dataH != null && dataH.visibleChange != null)
		{
			dataH.visibleChange.Change(map.mapRoot);
			foreach (string unVisible in dataH.visibleChange.unVisibleList)
			{
				GameObject gameObject6 = map.mapRoot.transform.FindLoop(unVisible);
				if (!(gameObject6 == null))
				{
					if (unVisible.Contains("isu") || unVisible.Contains("chair"))
					{
						mapObjectNull.lstChair.Add(new MapObjectNullInfo
						{
							obj = gameObject6,
							pos = gameObject6.transform.position,
							rot = gameObject6.transform.rotation,
							layer = gameObject6.layer
						});
					}
					else if (unVisible.Contains("desk"))
					{
						mapObjectNull.lstDesk.Add(new MapObjectNullInfo
						{
							obj = gameObject6,
							pos = gameObject6.transform.position,
							rot = gameObject6.transform.rotation,
							layer = gameObject6.layer
						});
					}
					else
					{
						mapObjectNull.lstOther.Add(new MapObjectNullInfo
						{
							obj = gameObject6,
							pos = gameObject6.transform.position,
							rot = gameObject6.transform.rotation,
							layer = gameObject6.layer
						});
					}
				}
			}
		}
		if (flags.lstHeroine.IsNullOrEmpty())
		{
			flags.lstHeroine = new List<Heroine>(new Heroine[1]
			{
				new Heroine(isRandomize: false)
			});
		}
		flags.player = flags.player ?? new SaveData.Player(isRandomize: false);
		Scene.DrawImageAndProgress(0f);
		GameObject parent = Scene.GetRootGameObjects("H")[0];
		yield return CreateCharacter(parent, flags.lstHeroine, flags.player);
		yield return null;
		trsMainCam.SetParent(parent.transform, worldPositionStays: false);
		ChaControl female = lstFemale[0];
		ChaControl female2 = ((lstFemale.Count > 1) ? lstFemale[1] : null);
		Heroine heroine = flags.lstHeroine[0];
		Heroine heroine2 = ((flags.lstHeroine.Count > 1) ? flags.lstHeroine[1] : null);
		female.transform.SetPositionAndRotation(initPos, initRot);
		if ((bool)female2)
		{
			female2.transform.SetPositionAndRotation(initPos, initRot);
		}
		male.transform.SetPositionAndRotation(initPos, initRot);
		if ((bool)male1)
		{
			male1.transform.SetPositionAndRotation(initPos, initRot);
		}
		SetCharacterPositon(!isWaitPointPoisitionSet);
		nowHpointDataPos = female.transform.position;
		if ((bool)objMoveAxis)
		{
			objMoveAxis.transform.SetPositionAndRotation(female.transform.position, female.transform.rotation);
		}
		HpointJudgePos = initPos;
		for (int num4 = 0; num4 < lstFemale.Count; num4++)
		{
			LoadCharaTagLayer(num4);
		}
		flags.mode = HFlag.EMode.none;
		flags.experience = ((heroine.HExperience == Heroine.HExperienceKind.慣れ || heroine.HExperience == Heroine.HExperienceKind.淫乱) ? EExperience.nare : EExperience.funare);
		useInitPosPoint = lstInitCategory.Any((int c) => c == 0 || c == 1 || c == 8 || c == 1302);
		nowInitPoint = true;
		GetCloseCategory();
		if (!flags.isFreeH || categorys.Any((int c) => MathfEx.IsRange(3000, c, 3099, isEqual: true)))
		{
			offSetData.Clear();
			InitOffset();
		}
		LoadAddTaii();
		LoadShopAddTaii();
		CreateListAnimationFileName();
		yield return null;
		lstMotionIK.Add(new MotionIK(female));
		lstMotionIK.Add(new MotionIK(male));
		if ((bool)female2)
		{
			lstMotionIK.Add(new MotionIK(female2));
		}
		if ((bool)male1)
		{
			lstMotionIK.Add(new MotionIK(male1));
		}
		lstMotionIK.ForEach(delegate(MotionIK motionIK)
		{
			motionIK.SetPartners(lstMotionIK);
		});
		lookDan.SetMale(male);
		lookDan1.SetMale(male1);
		hitreaction.ik = female.animBody.GetComponent<FullBodyBipedIK>();
		hitreaction1.ik = (female2 ? female2.animBody.GetComponent<FullBodyBipedIK>() : null);
		flags.SetCheetItem();
		HandCtrl.LoadInitList(flags);
		yield return null;
		hand.LoadInit();
		yield return null;
		hand.Init(female, male.chaFile.custom.body.skinMainColor, 0);
		hand.hitReaction = hitreaction;
		if (female2 != null)
		{
			hand1.LoadInit();
			yield return null;
			hand1.Init(female2, male.chaFile.custom.body.skinMainColor, 1);
			hand1.hitReaction = hitreaction1;
		}
		else
		{
			hand1.enabled = false;
		}
		int progressIndex = 2;
		Scene.DrawImageAndProgress((float)(lstFemale.Count + progressIndex++) / (float)(lstFemale.Count + progressAdd));
		yield return null;
		sprite.Init(lstFemale, heroine, male, rely, lstUseAnimInfo, categorys, voice);
		sprite.SetLightData(lightData);
		sprite.localMoveInitAction = InitLocalPosition;
		sprite.InitSpriteButton();
		lookDan.SetFemale(new ChaControl[2] { female, female2 });
		lookDan1.SetFemale(new ChaControl[1] { female });
		yure = new YureCtrl();
		yure.Init(female);
		yure1 = new YureCtrl();
		yure1.Init(female2);
		hand.yure = yure;
		hand1.yure = yure1;
		yureMale = new YureCtrl();
		yureMale.Init(male);
		ctrlObi = new ObiCtrl(flags);
		siru.Init(female);
		siru1.Init(female2);
		yield return face.LoadText(heroine.ChaName);
		if (heroine2 != null)
		{
			yield return face1.LoadText(heroine2.ChaName);
		}
		HParticleCtrl.InitLoad();
		particle.Init(female.objBodyBone, flags);
		particle.Load(male.objBodyBone, 0);
		particle1.Init(female2 ? female2.objBodyBone : null, flags);
		particle1.Load(male1 ? male1.objBodyBone : null, 0);
		ctrlObi.SetParticle(particle);
		Scene.DrawImageAndProgress((float)(lstFemale.Count + progressIndex++) / (float)(lstFemale.Count + progressAdd));
		yield return null;
		yield return voice.Init(heroine.ChaName, (heroine2 != null) ? heroine2.ChaName : "");
		Scene.DrawImageAndProgress((float)(lstFemale.Count + progressIndex++) / (float)(lstFemale.Count + progressAdd));
		yield return null;
		HMotionEyeNeck.LoadEyeNeckPtn();
		eyeneckFemale.Init(female, male, heroine);
		eyeneckFemale.SetPartner(male.objBodyBone);
		if ((bool)female2)
		{
			eyeneckFemale1.Init(female2, male, heroine2);
			eyeneckFemale1.SetPartner(male.objBodyBone);
			eyeneckFemale.SetFemalePartner(female2.objBodyBone);
			eyeneckFemale1.SetFemalePartner(female.objBodyBone);
		}
		eyeneckMale.Init(male, female);
		eyeneckMale.SetPartner(female.objBodyBone);
		eyeneckMale1.Init(male1, female);
		eyeneckMale1.SetPartner(female.objBodyBone);
		dynamicCtrl.Init(female);
		dynamicCtrl1.Init(female2);
		alCtrl.Init(female.animBody);
		if ((bool)female2)
		{
			alCtrl1.Init(female2.animBody);
		}
		else if ((bool)male1)
		{
			alCtrl1.Init(male1.animBody);
		}
		HFlag.DeliveryMember membar = new HFlag.DeliveryMember
		{
			chaFemale = female,
			chaFemale1 = female2,
			chaMale = male,
			chaMale1 = male1,
			ctrlFlag = flags,
			fade = crossfade,
			sprite = sprite,
			lstMotionIK = lstMotionIK,
			hand = hand,
			hand1 = hand1,
			yure = yure,
			yure1 = yure1,
			yureMale = yureMale,
			item = item,
			hitcolFemale = hitcollisionFemale,
			hitcolFemale1 = hitcollisionFemale1,
			hitcolMale = hitcollisionMale,
			obi = ctrlObi,
			parentObjectFemale = parentObjectFemale,
			parentObjectFemale1 = parentObjectFemale1,
			parentObjectMale = parentObjectMale,
			voice = voice,
			se = se,
			particle = particle,
			alCtrl = alCtrl,
			alCtrl1 = alCtrl1
		};
		lstProc.Add(new HAibu(membar));
		lstProc.Add(new HHoushi(membar));
		(lstProc[1] as HHoushi).SetRely(rely);
		lstProc.Add(new HSonyu(membar));
		lstProc.Add(new HMasturbation(membar));
		lstProc.Add(new HPeeping(membar));
		(lstProc[4] as HPeeping).SetMapObject(map.mapRoot);
		lstProc.Add(new HLesbian(membar));
		lstProc.Add(new H3PHoushi(membar));
		(lstProc[6] as H3PHoushi).SetRely(rely);
		lstProc.Add(new H3PSonyu(membar));
		lstProc.Add(new H3PDarkHoushi(membar));
		(lstProc[8] as H3PDarkHoushi).SetRely(rely);
		lstProc.Add(new H3PDarkSonyu(membar));
		ParentObjectCtrl.LoadInit();
		parentObjectBaseFemale.SetDicBodyObject(female.objBodyBone);
		parentObjectFemale.SetDicBodyObject(female.objBodyBone);
		parentObjectBaseFemale.Init(female.objBodyBone, female, 1, _isAllVisible: true);
		parentObjectFemale.Init(female.objBodyBone, female, 1, _isAllVisible: false);
		if ((bool)female2)
		{
			parentObjectBaseFemale1.SetDicBodyObject(female2.objBodyBone);
			parentObjectFemale1.SetDicBodyObject(female2.objBodyBone);
			parentObjectBaseFemale1.Init(female2.objBodyBone, female2, 1, _isAllVisible: true);
			parentObjectFemale1.Init(female2.objBodyBone, female2, 1, _isAllVisible: false);
		}
		parentObjectBaseMale.SetDicBodyObject(male.objBodyBone);
		parentObjectMale.SetDicBodyObject(male.objBodyBone);
		parentObjectBaseMale.Init(male.objBodyBone, male, 0, _isAllVisible: true);
		parentObjectMale.Init(male.objBodyBone, male, 0, _isAllVisible: false);
		hand.InitHitReactionCollider();
		if (female2 != null)
		{
			hand1.InitHitReactionCollider();
		}
		Scene.DrawImageAndProgress((float)(lstFemale.Count + progressIndex++) / (float)(lstFemale.Count + progressAdd));
		yield return null;
		ChangeAnimator(LoadAnimationListInfo(-1, appointAction, bFirst: true), _isForceCameraReset: true);
		changeTaii = false;
		Scene.DrawImageAndProgress((float)(lstFemale.Count + progressIndex) / (float)(lstFemale.Count + progressAdd));
		yield return null;
		sprite.InitPointMenuAndHelp(categorys);
		flags.transVoiceMouth[0] = (female.GetReferenceInfo(ChaReference.RefObjKey.a_n_mouth) ? female.GetReferenceInfo(ChaReference.RefObjKey.a_n_mouth).transform : null);
		if ((bool)female2)
		{
			flags.transVoiceMouth[1] = (female2.GetReferenceInfo(ChaReference.RefObjKey.a_n_mouth) ? female2.GetReferenceInfo(ChaReference.RefObjKey.a_n_mouth).transform : null);
		}
		voice.voiceTrans[0] = flags.transVoiceMouth[0];
		voice.voiceTrans[1] = flags.transVoiceMouth[1];
		if (flags.isFreeH || (actscene != null && actscene.Cycle.withHeroine != null))
		{
			LoadMapDependent(map.no);
		}
		else
		{
			for (int num5 = 0; num5 < flags.lstHeroine.Count; num5++)
			{
				if (flags.lstHeroine != null)
				{
					lstFemale[num5].SetClothesState(4, flags.lstHeroine[num5].chaCtrl.fileStatus.clothesState[4]);
					lstFemale[num5].SetClothesState(6, flags.lstHeroine[num5].chaCtrl.fileStatus.clothesState[6]);
					lstFemale[num5].SetClothesState(7, flags.lstHeroine[num5].chaCtrl.fileStatus.clothesState[7]);
					lstFemale[num5].SetClothesState(8, flags.lstHeroine[num5].chaCtrl.fileStatus.clothesState[8]);
				}
			}
			male.SetClothesState(4, flags.player.chaCtrl.fileStatus.clothesState[4]);
			male.SetClothesState(6, flags.player.chaCtrl.fileStatus.clothesState[6]);
			male.SetClothesState(7, flags.player.chaCtrl.fileStatus.clothesState[7]);
			male.SetClothesState(8, flags.player.chaCtrl.fileStatus.clothesState[8]);
		}
		if (map.Info.State == 2)
		{
			flags.lstHeroine.ForEach(delegate(Heroine h)
			{
				h.chaCtrl.fileStatus.shoesType = 1;
			});
			male.fileStatus.shoesType = 1;
			if ((bool)male1)
			{
				male1.fileStatus.shoesType = 1;
			}
		}
		female.SetAccessoryStateCategory(1, show: false);
		if ((bool)female2)
		{
			female2.SetAccessoryStateCategory(1, show: false);
		}
		if (map.no == 15)
		{
			female.SetAccessoryStateAll(show: false);
			if ((bool)female2)
			{
				female2.SetAccessoryStateAll(show: false);
			}
		}
		female.hideMoz = false;
		if ((bool)female2)
		{
			female2.hideMoz = false;
		}
		flags.isInsertOK[0] = (dataH != null && dataH.isKokanForceInsert) || flags.IsNamaInsertOK();
		flags.isInsertOK[1] = flags.lstHeroine.Count > 1 && HFlag.NamaInsertCheck(flags.lstHeroine[1]);
		int b = (int)(heroine.hAreaExps[3] + heroine.countAnalH);
		b = Mathf.Min(100, b);
		GlobalMethod.RatioRand ratioRand = new GlobalMethod.RatioRand();
		ratioRand.Add(0, b);
		if (100 - b != 0)
		{
			ratioRand.Add(1, 100 - b);
		}
		flags.isAnalInsertOK = ratioRand.Random() == 0 || heroine.denial.anal;
		flags.voice.MemberInit();
		flags.isCondom = false;
		if (appointAction == 4 && !flags.isInsertOK[0])
		{
			sprite.CondomClick();
		}
		bool flag2 = categorys.Any((int c) => MathfEx.IsRange(2000, c, 2999, isEqual: true));
		Illusion.Game.Utils.Sound.Play(new Illusion.Game.Utils.Sound.SettingBGM(flag2 ? BGM.HScenePeep : BGM.HScene));
		if (MathfEx.IsRange(0, appointAction, 6, isEqual: true) || myRoom)
		{
			flags.voice.playVoices[0] = -1;
		}
		else if (flags.mode == HFlag.EMode.masturbation)
		{
			flags.voice.playVoices[0] = 400;
		}
		else if (categorys.Any((int c) => c == 1305))
		{
			flags.voice.playVoices[0] = 0;
		}
		else if (categorys.Any((int c) => (c >= 1000 && c < 2000) || c == 12))
		{
			if (flags.mode == HFlag.EMode.aibu)
			{
				flags.voice.playVoices[0] = 144;
			}
			else if (flags.mode == HFlag.EMode.houshi)
			{
				flags.voice.playVoices[0] = 210;
			}
			else if (flags.mode == HFlag.EMode.sonyu)
			{
				flags.voice.playVoices[0] = 337 + (flags.nowAnimationInfo.isFemaleInitiative ? 38 : 0);
			}
		}
		else if (categorys.Any((int c) => c < 2000))
		{
			flags.voice.playVoices[0] = 0;
		}
		if (heroine.hCount == 0 && !flag2 && !flags.isFreeH && !female2)
		{
			sprite.SetHelpSprite();
		}
		flags.ctrlCamera.LoadVanishData("h/list/", "map_col_" + map.no.ToString("00"), map.mapRoot);
		bool secretOpen = Game.Player.isSecretBeachOpen;
		bool suiteRoomOpen = Game.Player.isSuiteRoomOpen;
		if (flags.isFreeH)
		{
			secretOpen = true;
			suiteRoomOpen = true;
		}
		flags.ctrlCamera.SetSecretOpen(secretOpen);
		flags.ctrlCamera.SetSuiteRoomOpen(suiteRoomOpen);
		Manager.Sound.Listener = flags.ctrlCamera.transform;
		if (isEasyPlace)
		{
			SingletonInitializer<Game>.instance.rankSaveData.easyPlaceHCount++;
		}
		if (flags.mode == HFlag.EMode.masturbation && !flags.isFreeH)
		{
			SingletonInitializer<Game>.instance.rankSaveData.peepOnanismCount++;
		}
		SetShortcutKey();
		sprite.InitTrespassingHelp();
		AnimatorStateInfo animatorStateInfo = female.getAnimatorStateInfo(0);
		eyeneckFemale.Proc(animatorStateInfo, flags.voice.eyenecks[0], flags.ctrlCamera.thisCmaera.gameObject, Manager.Config.HData.FemaleEyesCamera, Manager.Config.HData.FemaleNeckCamera);
		if ((bool)female2)
		{
			eyeneckFemale1.Proc(animatorStateInfo, flags.voice.eyenecks[1], flags.ctrlCamera.thisCmaera.gameObject, Manager.Config.HData.FemaleEyesCamera1, Manager.Config.HData.FemaleNeckCamera1);
		}
		eyeneckMale.Proc(animatorStateInfo, flags.ctrlCamera.thisCmaera.gameObject);
		if ((bool)male1)
		{
			eyeneckMale1.Proc(animatorStateInfo, flags.ctrlCamera.thisCmaera.gameObject);
		}
		Scene.DrawImageAndProgress(1f);
		yield return null;
		Scene.DrawImageAndProgress();
		female.ResetDynamicBoneAll();
		if ((bool)female2)
		{
			female2.ResetDynamicBoneAll();
		}
		male.ResetDynamicBoneAll();
		if ((bool)male1)
		{
			male1.ResetDynamicBoneAll();
		}
		SetConfig(_force: true);
		for (int num6 = 0; num6 < voicePlayShuffle.Length; num6++)
		{
			voicePlayShuffle[num6] = new ShuffleRand();
			voicePlayShuffle[num6].Init(2);
		}
		StartCoroutine(Scene.sceneFadeCanvas.StartFadeAysnc(FadeCanvas.Fade.Out).ToCoroutine());
		while (Scene.IsFadeNow)
		{
			yield return null;
		}
		if (dataH != null && !dataH.peepCategory.Any((int c) => MathfEx.IsRange(2000, c, 2999, isEqual: true) || MathfEx.IsRange(1010, c, 1099, isEqual: true) || MathfEx.IsRange(1100, c, 1199, isEqual: true)) && Tutorial.Open(Tutorial.Category.H))
		{
			yield return new WaitWhile(() => Tutorial.active);
			if (Scene.isReturnTitle || ExitDialog.isGameEnd)
			{
				yield break;
			}
		}
		base.enabled = true;
	}

	private void Update()
	{
		SetConfig();
		ChaControl female = lstFemale[0];
		ChaControl female2 = ((lstFemale.Count > 1) ? lstFemale[1] : null);
		face.SafeProc(delegate(FaceListCtrl f)
		{
			f.OpenCtrl(female);
		});
		face1.SafeProc(delegate(FaceListCtrl f)
		{
			f.OpenCtrl(female2);
		});
		float num = flags.gaugeFemale * 0.01f;
		if (flags.rateNip < num)
		{
			flags.rateNip = num;
		}
		float rate = Mathf.Lerp(0f, Manager.Config.HData.nipMax, flags.rateNip);
		female.ChangeNipRate(rate);
		if ((bool)female2)
		{
			female2.ChangeNipRate(rate);
		}
		if (flags.isHSceneEnd)
		{
			switch (flags.numEnd)
			{
			case 0:
				EndProc();
				flags.numEnd = 1;
				break;
			case 2:
				NewHeroineEndProc();
				flags.numEnd = 3;
				break;
			case 1:
			case 3:
				break;
			}
			return;
		}
		AnimatorStateInfo animatorStateInfo = female.getAnimatorStateInfo(0);
		eyeneckFemale.Proc(animatorStateInfo, flags.voice.eyenecks[0], flags.ctrlCamera.thisCmaera.gameObject, Manager.Config.HData.FemaleEyesCamera, Manager.Config.HData.FemaleNeckCamera);
		if ((bool)female2)
		{
			eyeneckFemale1.Proc(animatorStateInfo, flags.voice.eyenecks[1], flags.ctrlCamera.thisCmaera.gameObject, Manager.Config.HData.FemaleEyesCamera1, Manager.Config.HData.FemaleNeckCamera1);
		}
		eyeneckMale.Proc(animatorStateInfo, flags.ctrlCamera.thisCmaera.gameObject);
		if ((bool)male1)
		{
			eyeneckMale1.Proc(animatorStateInfo, flags.ctrlCamera.thisCmaera.gameObject);
		}
		if (Scene.NowSceneNames[0] != "HProc")
		{
			return;
		}
		if (sprite.GetFadeKindProc() != HSprite.FadeKindProc.OutEnd)
		{
			voice.Proc(animatorStateInfo, lstFemale);
		}
		int mode = (int)flags.mode;
		if (mode >= 0 && mode < lstProc.Count)
		{
			lstProc[mode].Proc();
		}
		ctrlObi.Proc(animatorStateInfo, flags.isInsideFinish);
		siru.Proc(animatorStateInfo);
		siru1.Proc(animatorStateInfo);
		se.Proc(animatorStateInfo, lstFemale.ToArray());
		if (flags.selectAnimationListInfo != null && !flags.voiceWait)
		{
			if (!CategoryCheck())
			{
				MovePointByChangeAnim();
			}
			ChangeAnimator(flags.selectAnimationListInfo);
			flags.selectAnimationListInfo = null;
		}
		ShortCut();
		if (flags.click == HFlag.ClickKind.pointmove)
		{
			GotoPointMoveScene();
		}
		if (clothCustomCtrl != null && clothCustomCtrl.gameObject.activeSelf)
		{
			GlobalMethod.SetCameraMoveFlag(flags.ctrlCamera, _bPlay: false);
		}
		flags.click = HFlag.ClickKind.none;
	}

	private void LateUpdate()
	{
		if (Scene.NowSceneNames[0] != "HProc")
		{
			if ((bool)dynamicCtrl)
			{
				dynamicCtrl.Proc();
			}
			if ((bool)dynamicCtrl1)
			{
				dynamicCtrl1.Proc();
			}
			for (int i = 0; i < lstFemale.Count; i++)
			{
				lstFemale[i].SetPosition(lstFemale[i].gameObject.transform.InverseTransformVector(guideObject.amountTotal.position));
				lstFemale[i].SetRotation(guideObject.amountTotal.rotation);
			}
			if (lstFemale.Count != 0 && (bool)lstFemale[0])
			{
				item.SetTransform(lstFemale[0].objTop.transform);
			}
			if ((bool)male)
			{
				male.SetPosition(male.gameObject.transform.InverseTransformVector(guideObject.amountTotal.position));
				male.SetRotation(guideObject.amountTotal.rotation);
			}
			if ((bool)male1)
			{
				male1.SetPosition(male1.gameObject.transform.InverseTransformVector(guideObject.amountTotal.position));
				male1.SetRotation(guideObject.amountTotal.rotation);
			}
			return;
		}
		int mode = (int)flags.mode;
		if (mode >= 0 && mode < lstProc.Count)
		{
			lstProc[mode].LateProc();
		}
		if (item != null)
		{
			item.LateUpdate();
		}
		dynamicCtrl.Proc();
		dynamicCtrl1.Proc();
		for (int j = 0; j < lstFemale.Count; j++)
		{
			lstFemale[j].SetPosition(lstFemale[j].gameObject.transform.InverseTransformVector(guideObject.amountTotal.position));
			lstFemale[j].SetRotation(guideObject.amountTotal.rotation);
		}
		if (lstFemale.Count != 0 && (bool)lstFemale[0])
		{
			item.SetTransform(lstFemale[0].objTop.transform);
		}
		if ((bool)male)
		{
			male.SetPosition(male.gameObject.transform.InverseTransformVector(guideObject.amountTotal.position));
			male.SetRotation(guideObject.amountTotal.rotation);
		}
		if ((bool)male1)
		{
			male1.SetPosition(male1.gameObject.transform.InverseTransformVector(guideObject.amountTotal.position));
			male1.SetRotation(guideObject.amountTotal.rotation);
		}
		if (flags.nowAnimationInfo != null && (flags.nowAnimationInfo.mode == HFlag.EMode.sonyu || flags.nowAnimationInfo.mode == HFlag.EMode.sonyu3P) && guideObject.isDrag)
		{
			GomuRigidAwake();
		}
		LateShortCut();
	}

	private void OnDestroy()
	{
		if ((bool)map && (bool)map.mapRoot)
		{
			map.mapRoot.SetActive(value: true);
		}
		if (SingletonInitializer<Voice>.initialized)
		{
			Voice.StopAll();
		}
		if (SingletonInitializer<Manager.Sound>.initialized)
		{
			Manager.Sound.Stop(Manager.Sound.Type.GameSE3D);
		}
		ctrlObi.EndProcSolver();
		ctrlObi.EndProc();
		HMotionEyeNeck.EndProc();
		ParentObjectCtrl.EndProc();
		AnimatorLayerCtrl.EndProc();
	}

	private IEnumerator CreateCharacter(GameObject parent, List<Heroine> heroineList, SaveData.Player player)
	{
		int count = heroineList.Count + progressAdd;
		int person = 0;
		foreach (Heroine heroine in heroineList)
		{
			ChaFileControl chaFileControl = new ChaFileControl();
			ChaFile.CopyChaFile(chaFileControl, heroine.chaCtrl ? heroine.chaCtrl.chaFile : heroine.charFile);
			ChaControl chara = Character.CreateFemale(parent, 0, chaFileControl);
			heroine.SetRoot(chara.gameObject);
			heroine.lewdness = Mathf.Clamp(heroine.lewdness, 0, 100);
			if (!flags.isFreeH)
			{
				if (lstInitCategory.Contains(1307))
				{
					chara.ChangeCoordinateType(ChaFileDefine.CoordinateType.Swim);
				}
				else if (lstInitCategory.Contains(1306))
				{
					chara.ChangeCoordinateType(ChaFileDefine.CoordinateType.Bathing);
				}
				else
				{
					chara.ChangeCoordinateType((ChaFileDefine.CoordinateType)lstCoordinateType[person]);
				}
			}
			else
			{
				chara.ChangeCoordinateType((ChaFileDefine.CoordinateType)lstCoordinateType[person]);
			}
			Character.loading.Load(0, chara);
			yield return new WaitUntil(() => Character.loading.IsEnd(chara));
			if (clothStates.Count > person)
			{
				for (int num = 0; num < clothStates[person].Count; num++)
				{
					chara.SetClothesState(num, (byte)clothStates[person][num]);
				}
			}
			if (accessoryStates.Count > person)
			{
				for (int num2 = 0; num2 < accessoryStates[person].Count; num2++)
				{
					chara.SetAccessoryState(num2, accessoryStates[person][num2]);
				}
			}
			lstFemale.Add(chara);
			Scene.DrawImageAndProgress((float)lstFemale.Count / (float)count);
			person++;
		}
		ChaFileControl chaFileControl2 = new ChaFileControl();
		ChaFile.CopyChaFile(chaFileControl2, player.chaCtrl ? player.chaCtrl.chaFile : player.charFile);
		male = Character.CreateMale(0, parent, 0, chaFileControl2);
		player.SetRoot(male.gameObject);
		if (!flags.isFreeH)
		{
			if (lstInitCategory.Contains(1307))
			{
				male.ChangeCoordinateType(ChaFileDefine.CoordinateType.Swim);
			}
			else if (lstInitCategory.Contains(1306))
			{
				male.ChangeCoordinateType(ChaFileDefine.CoordinateType.Bathing);
			}
			else
			{
				male.ChangeCoordinateType((ChaFileDefine.CoordinateType)maleCoordinateType);
			}
		}
		else
		{
			male.ChangeCoordinateType((ChaFileDefine.CoordinateType)maleCoordinateType);
		}
		Character.loading.Load(0, male);
		yield return new WaitUntil(() => Character.loading.IsEnd(male));
		male.visibleAll = false;
		Scene.DrawImageAndProgress((float)(lstFemale.Count + 1) / (float)count);
	}

	public IEnumerator CreateFemaleCharacterAppoint(Heroine _heroine, int _coordinateType)
	{
		GameObject parent = Scene.GetRootGameObjects("H")[0];
		ChaFileControl chaFileControl = new ChaFileControl();
		ChaFile.CopyChaFile(chaFileControl, _heroine.chaCtrl ? _heroine.chaCtrl.chaFile : _heroine.charFile);
		ChaControl chara = Character.CreateFemale(parent, 0, chaFileControl);
		_heroine.SetRoot(chara.gameObject);
		_heroine.lewdness = Mathf.Clamp(_heroine.lewdness, 0, 100);
		chara.ChangeCoordinateType((ChaFileDefine.CoordinateType)_coordinateType);
		Character.loading.Load(0, chara);
		yield return new WaitUntil(() => Character.loading.IsEnd(chara));
		lstFemale.Add(chara);
	}

	private void ChangeAnimator(AnimationListInfo _nextAinmInfo, bool _isForceCameraReset = false)
	{
		changeRac = null;
		HFlag.EMode mode = flags.nowAnimationInfo.mode;
		if (_nextAinmInfo == null)
		{
			return;
		}
		flags.mode = _nextAinmInfo.mode;
		ChaControl chaControl = lstFemale[0];
		ChaControl chaControl2 = ((lstFemale.Count > 1) ? lstFemale[1] : null);
		int num = 0;
		if (_nextAinmInfo.mode == HFlag.EMode.houshi3P || _nextAinmInfo.mode == HFlag.EMode.sonyu3P)
		{
			lstMotionIK.ForEach(delegate(MotionIK motionIK)
			{
				motionIK.Release();
			});
			lstMotionIK.Clear();
			if (_nextAinmInfo.id % 2 == 0)
			{
				lstMotionIK.Add(new MotionIK(chaControl));
				lstMotionIK.Add(new MotionIK(male));
				if ((bool)chaControl2)
				{
					lstMotionIK.Add(new MotionIK(chaControl2));
				}
			}
			else
			{
				if ((bool)chaControl2)
				{
					lstMotionIK.Add(new MotionIK(chaControl2));
				}
				lstMotionIK.Add(new MotionIK(male));
				lstMotionIK.Add(new MotionIK(chaControl));
				num = 1;
			}
			lstMotionIK.ForEach(delegate(MotionIK motionIK)
			{
				motionIK.SetPartners(lstMotionIK);
				motionIK.Reset();
			});
		}
		FemaleParameter paramFemale = _nextAinmInfo.paramFemale;
		FemaleParameter paramFemale2 = flags.nowAnimationInfo.paramFemale;
		if (paramFemale2.lstIdLayer.Count != 0 && paramFemale2.lstIdLayer[0] != -1)
		{
			chaControl.setLayerWeight(0f, paramFemale2.lstIdLayer[0]);
		}
		if (flags.mode != mode || flags.mode == HFlag.EMode.lesbian || flags.mode == HFlag.EMode.sonyu3P || flags.mode == HFlag.EMode.houshi3P || flags.mode == HFlag.EMode.houshi3PMMF || (!_nextAinmInfo.pathFemaleBase.assetpath.IsNullOrEmpty() && paramFemale.path.assetpath.IsNullOrEmpty()))
		{
			chaControl.LoadAnimation(_nextAinmInfo.pathFemaleBase.assetpath, _nextAinmInfo.pathFemaleBase.file);
		}
		if (paramFemale.lstIdLayer[0] != -1)
		{
			chaControl.setLayerWeight(1f, paramFemale.lstIdLayer[0]);
		}
		PathName path = paramFemale.path;
		if (!path.assetpath.IsNullOrEmpty())
		{
			Animator animBody = chaControl.animBody;
			changeRac = CommonLib.LoadAsset<RuntimeAnimatorController>(path.assetpath, path.file);
			if (changeRac != null)
			{
				animBody.runtimeAnimatorController = Illusion.Utils.Animator.SetupAnimatorOverrideController(animBody.runtimeAnimatorController, changeRac);
			}
			AssetBundleManager.UnloadAssetBundle(path.assetpath, isUnloadForceRefCount: true);
		}
		else if (flags.mode != HFlag.EMode.lesbian && flags.mode != HFlag.EMode.sonyu3P && flags.mode != HFlag.EMode.houshi3P)
		{
			GlobalMethod.DebugLog(string.Concat("女の", flags.mode, "の", _nextAinmInfo.id.ToString(), "番に", flags.experience, "なんてないよ"), 2);
		}
		if (paramFemale.isHitObject)
		{
			chaControl.LoadHitObject(1);
			hitcollisionFemale.flags = flags;
			hitcollisionFemale.Init(chaControl, chaControl.objHitHead);
			if (flags.testSiruKind == HFlag.TestSiruKind.メタボール)
			{
				hitcollisionFemale.LoadText("h/list/", paramFemale.fileHit);
			}
			else
			{
				hitcollisionFemale.LoadExcel(paramFemale.fileHit);
			}
		}
		else
		{
			chaControl.ReleaseHitObject();
			hitcollisionFemale.Release();
		}
		if (!lstMotionIK[(num != 0) ? 2 : 0].LoadData(GlobalMethod.LoadAllFolderInOneFile<TextAsset>("h/list/", path.file)))
		{
			lstMotionIK[(num != 0) ? 2 : 0].Release();
		}
		alCtrl.Load("h/list/", path.file);
		FemaleParameter paramFemale3 = _nextAinmInfo.paramFemale1;
		if ((bool)chaControl2)
		{
			paramFemale2 = flags.nowAnimationInfo.paramFemale1;
			if (paramFemale2.lstIdLayer.Count != 0 && paramFemale2.lstIdLayer[0] != -1)
			{
				chaControl2.setLayerWeight(0f, paramFemale2.lstIdLayer[0]);
			}
			if (flags.mode != mode || flags.mode == HFlag.EMode.lesbian || flags.mode == HFlag.EMode.sonyu3P || flags.mode == HFlag.EMode.houshi3P || (!_nextAinmInfo.pathFemaleBase.assetpath.IsNullOrEmpty() && paramFemale3.path.assetpath.IsNullOrEmpty()))
			{
				chaControl2.LoadAnimation(_nextAinmInfo.pathFemaleBase1.assetpath, _nextAinmInfo.pathFemaleBase1.file);
			}
			if (paramFemale3.lstIdLayer[0] != -1)
			{
				chaControl2.setLayerWeight(1f, paramFemale3.lstIdLayer[0]);
			}
			changeRac = null;
			path = paramFemale3.path;
			if (!path.assetpath.IsNullOrEmpty())
			{
				Animator animBody2 = chaControl2.animBody;
				changeRac = CommonLib.LoadAsset<RuntimeAnimatorController>(path.assetpath, path.file);
				if (changeRac != null)
				{
					animBody2.runtimeAnimatorController = Illusion.Utils.Animator.SetupAnimatorOverrideController(animBody2.runtimeAnimatorController, changeRac);
				}
				AssetBundleManager.UnloadAssetBundle(path.assetpath, isUnloadForceRefCount: true);
			}
			else if (flags.mode != HFlag.EMode.lesbian && flags.mode != HFlag.EMode.sonyu3P && flags.mode != HFlag.EMode.houshi3P)
			{
				GlobalMethod.DebugLog(string.Concat("女の", flags.mode, "の", _nextAinmInfo.id.ToString(), "番に", flags.experience, "なんてないよ"), 2);
			}
			if (paramFemale3.isHitObject)
			{
				chaControl2.LoadHitObject(1);
				hitcollisionFemale1.flags = flags;
				hitcollisionFemale1.Init(chaControl2, chaControl2.objHitHead);
				if (flags.testSiruKind == HFlag.TestSiruKind.メタボール)
				{
					hitcollisionFemale1.LoadText("h/list/", paramFemale3.fileHit);
				}
				else
				{
					hitcollisionFemale1.LoadExcel(paramFemale3.fileHit);
				}
			}
			else
			{
				chaControl2.ReleaseHitObject();
				hitcollisionFemale1.Release();
			}
			if (!lstMotionIK[(num == 0) ? 2 : 0].LoadData(GlobalMethod.LoadAllFolderInOneFile<TextAsset>("h/list/", path.file)))
			{
				lstMotionIK[(num == 0) ? 2 : 0].Release();
			}
			alCtrl1.Load("h/list/", path.file);
		}
		MaleParameter paramMale = _nextAinmInfo.paramMale;
		path = paramMale.path;
		if (path.assetpath != "")
		{
			MaleParameter paramMale2 = flags.nowAnimationInfo.paramMale;
			if (paramMale2.lstIdLayer.Any() && paramMale2.lstIdLayer[0] != -1)
			{
				male.setLayerWeight(0f, paramMale2.lstIdLayer[0]);
			}
			male.chaFile.status.visibleBodyAlways = true;
			male.visibleAll = true;
			if (flags.mode != mode || flags.mode == HFlag.EMode.sonyu3P || flags.mode == HFlag.EMode.houshi3P || flags.mode == HFlag.EMode.houshi3PMMF)
			{
				male.LoadAnimation(_nextAinmInfo.pathMaleBase.assetpath, _nextAinmInfo.pathMaleBase.file);
			}
			if (paramMale.lstIdLayer[0] != -1)
			{
				male.setLayerWeight(1f, paramMale.lstIdLayer[0]);
			}
			changeRac = null;
			if (!path.assetpath.IsNullOrEmpty())
			{
				Animator animBody3 = male.animBody;
				changeRac = CommonLib.LoadAsset<RuntimeAnimatorController>(path.assetpath, path.file);
				if (changeRac != null)
				{
					animBody3.runtimeAnimatorController = Illusion.Utils.Animator.SetupAnimatorOverrideController(animBody3.runtimeAnimatorController, changeRac);
				}
				AssetBundleManager.UnloadAssetBundle(path.assetpath, isUnloadForceRefCount: true);
			}
			else if (flags.mode != HFlag.EMode.lesbian && flags.mode != HFlag.EMode.sonyu3P && flags.mode != HFlag.EMode.houshi3P)
			{
				GlobalMethod.DebugLog(string.Concat("男の", flags.mode, "の", _nextAinmInfo.id.ToString(), "番に", flags.experience, "なんてないよ"), 2);
			}
			if (!lstMotionIK[1].LoadData(GlobalMethod.LoadAllFolderInOneFile<TextAsset>("h/list/", path.file)))
			{
				lstMotionIK[1].Release();
			}
			if (paramMale.isHitObject)
			{
				male.LoadHitObject(1);
				hitcollisionMale.flags = flags;
				hitcollisionMale.Init(male, male.objHitHead);
				if (flags.testSiruKind == HFlag.TestSiruKind.メタボール)
				{
					hitcollisionMale.LoadText("h/list/", paramMale.fileHit);
				}
				else
				{
					hitcollisionMale.LoadExcel(paramMale.fileHit);
				}
			}
		}
		else if ((bool)male.objBodyBone)
		{
			male.visibleAll = false;
			male.chaFile.status.visibleBodyAlways = true;
			male.chaFile.status.visibleHeadAlways = true;
			male.ReleaseHitObject();
			hitcollisionMale.Release();
		}
		if ((bool)male1)
		{
			paramFemale2 = flags.nowAnimationInfo.paramFemale1;
			if (paramFemale2.lstIdLayer.Count != 0 && paramFemale2.lstIdLayer[0] != -1)
			{
				male1.setLayerWeight(0f, paramFemale2.lstIdLayer[0]);
			}
			male1.LoadAnimation(_nextAinmInfo.pathFemaleBase1.assetpath, _nextAinmInfo.pathFemaleBase1.file);
			if (paramFemale3.lstIdLayer[0] != -1)
			{
				male1.setLayerWeight(1f, paramFemale3.lstIdLayer[0]);
			}
			changeRac = null;
			path = paramFemale3.path;
			if (!path.assetpath.IsNullOrEmpty())
			{
				Animator animBody4 = male1.animBody;
				changeRac = CommonLib.LoadAsset<RuntimeAnimatorController>(path.assetpath, path.file);
				if (changeRac != null)
				{
					animBody4.runtimeAnimatorController = Illusion.Utils.Animator.SetupAnimatorOverrideController(animBody4.runtimeAnimatorController, changeRac);
				}
				AssetBundleManager.UnloadAssetBundle(path.assetpath, isUnloadForceRefCount: true);
			}
			else
			{
				GlobalMethod.DebugLog(string.Concat("おっさんの", flags.mode, "の", _nextAinmInfo.id.ToString(), "番に", flags.experience, "なんてないよ"), 2);
			}
			if (paramFemale3.isHitObject)
			{
				male1.LoadHitObject(1);
				hitcollisionFemale1.flags = flags;
				hitcollisionFemale1.Init(male1, male1.objHitHead);
				if (flags.testSiruKind == HFlag.TestSiruKind.メタボール)
				{
					hitcollisionFemale1.LoadText("h/list/", paramFemale3.fileHit);
				}
				else
				{
					hitcollisionFemale1.LoadExcel(paramFemale3.fileHit);
				}
			}
			else
			{
				male1.ReleaseHitObject();
			}
			if (!lstMotionIK[2].LoadData(GlobalMethod.LoadAllFolderInOneFile<TextAsset>("h/list/", path.file)))
			{
				lstMotionIK[2].Release();
			}
			alCtrl1.Load("h/list/", path.file);
		}
		lstMotionIK.Where((MotionIK motionIK) => motionIK.ik != null).ToList().ForEach(delegate(MotionIK motionIK)
		{
			motionIK.Calc("Idle");
		});
		lookDan.LoadList("h/list/", paramMale.fileLookAt);
		if (_nextAinmInfo.mode == HFlag.EMode.houshi3PMMF || _nextAinmInfo.mode == HFlag.EMode.sonyu3PMMF)
		{
			lookDan1.LoadList("h/list/", paramFemale3.fileDynamicBoneRef);
		}
		else
		{
			lookDan1.Release();
		}
		yure.LoadAllExcel("h/list/", paramFemale.fileYure);
		yure1.LoadAllExcel("h/list/", paramFemale3.fileYure);
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("yure_").Append(paramMale.path.file);
		yureMale.LoadAllExcel("h/list/", stringBuilder.ToString());
		hand.LoadMotionEnableState(paramFemale.fileAibuEnableMotion);
		hand.LoadReactionList(paramFemale.fileReaction);
		if (chaControl2 != null)
		{
			hand1.LoadMotionEnableState(paramFemale3.fileAibuEnableMotion);
			hand1.LoadReactionList(paramFemale3.fileReaction);
		}
		item.LoadItem(paramFemale.fileItem, male.objBodyBone, lstFemale.ToArray(), null, flags.hashAssetBundle);
		List<ChaControl> list = new List<ChaControl> { male, male1 };
		if (Singleton<ObiFluidManager>.IsInstance())
		{
			Singleton<ObiFluidManager>.Instance.LoadCondomInfo(_nextAinmInfo.paramMale.fileCondomCtrl);
		}
		ctrlObi.Clear();
		ctrlObi.NullEmitterDel();
		ctrlObi.SetChara(list, lstFemale, chaControl2 != null || male1 != null);
		ctrlObi.InitSetting(_nextAinmInfo.paramMale.fileMetaballCtrl, item.GetItem(0));
		siru.Load("h/list/", paramFemale.fileSiruPaste);
		siru1.Load("h/list/", paramFemale3.fileSiruPaste);
		parentObjectFemale.LoadExcel(paramFemale.fileHitObject);
		parentObjectFemale1.LoadExcel(paramFemale3.fileHitObject);
		parentObjectMale.LoadExcel(paramMale.fileHitObject);
		eyeneckFemale.Load(paramFemale.fileMotionNeck);
		if (chaControl2 != null)
		{
			eyeneckFemale1.Load(paramFemale3.fileMotionNeck);
		}
		eyeneckMale.Load(paramMale.fileMotionNeck);
		if ((bool)male1)
		{
			eyeneckMale1.Load(paramFemale3.fileMotionNeck);
		}
		se.Load("h/list/", paramFemale.fileSe, male.objBodyBone, chaControl.objBodyBone, map.mapRoot);
		dynamicCtrl.Load("h/list/", paramFemale.fileDynamicBoneRef);
		if (!male1)
		{
			dynamicCtrl1.Load("h/list/", paramFemale3.fileDynamicBoneRef);
		}
		male.chaFile.status.visibleSon = _nextAinmInfo.numMaleSon == 1;
		if ((bool)male1)
		{
			male1.chaFile.status.visibleSon = _nextAinmInfo.numFemaleUpperCloth1 == 1;
		}
		foreach (int item in hand.GetUseItemNumber())
		{
			hand.DetachItemByUseItem(item);
		}
		hand.ForceFinish(_isCameraMoveStop: false);
		int num2 = ((flags.click == HFlag.ClickKind.insert) ? 1 : ((flags.click == HFlag.ClickKind.insert_voice) ? 2 : 0));
		if (flags.mode == HFlag.EMode.houshi || flags.mode == HFlag.EMode.houshi3P || flags.mode == HFlag.EMode.houshi3PMMF)
		{
			lstProc[(int)flags.mode].MotionChange(num2);
		}
		else if (flags.mode == HFlag.EMode.peeping)
		{
			lstProc[(int)flags.mode].MotionChange(_nextAinmInfo.numCtrl);
		}
		else if (flags.mode == HFlag.EMode.lesbian || flags.mode == HFlag.EMode.masturbation)
		{
			lstProc[(int)flags.mode].MotionChange(1);
		}
		else
		{
			lstProc[(int)flags.mode].MotionChange(0);
		}
		SetLocalPosition(_nextAinmInfo);
		string strfile = paramFemale.nameCamera;
		if (flags.mode == HFlag.EMode.houshi && num2 == 0)
		{
			strfile = _nextAinmInfo.nameCameraIdle;
		}
		if (_isForceCameraReset || Manager.Config.HData.HInitCamera)
		{
			GlobalMethod.LoadCamera(flags.ctrlCamera, "h/list/", strfile);
		}
		else
		{
			GlobalMethod.LoadResetCamera(flags.ctrlCamera, "h/list/", strfile);
		}
		bChangePoint = false;
		flags.SpeedRelationClear();
		SetMapObject(_nextAinmInfo.useChair, _nextAinmInfo.useDesk, _nextAinmInfo.pathMapObjectNull.assetpath, _nextAinmInfo.pathMapObjectNull.file);
		if (_nextAinmInfo.mode != HFlag.EMode.masturbation && _nextAinmInfo.mode != HFlag.EMode.lesbian)
		{
			if (flags.nowAnimationInfo.mode == _nextAinmInfo.mode)
			{
				flags.voice.playVoices[0] = -1;
				if (_nextAinmInfo.mode == HFlag.EMode.aibu)
				{
					flags.voice.playVoices[0] = 9;
				}
				else if (_nextAinmInfo.mode == HFlag.EMode.sonyu)
				{
					flags.voice.playVoices[0] = 10;
				}
				else if (_nextAinmInfo.mode == HFlag.EMode.sonyu3P)
				{
					flags.voice.playVoices[voicePlayShuffle[0].Get()] = 21;
				}
			}
			else
			{
				flags.voice.playVoices[0] = ((_nextAinmInfo.mode == HFlag.EMode.aibu) ? 100 : ((_nextAinmInfo.mode == HFlag.EMode.houshi) ? 200 : (300 + (_nextAinmInfo.isFemaleInitiative ? 38 : 0))));
			}
		}
		bool num3 = (_nextAinmInfo.mode == HFlag.EMode.houshi || _nextAinmInfo.mode == HFlag.EMode.houshi3P || _nextAinmInfo.mode == HFlag.EMode.houshi3PMMF) && _nextAinmInfo.mode == flags.nowAnimationInfo.mode;
		SetBoneOffset(flags.nowAnimationInfo, _nextAinmInfo);
		flags.nowAnimationInfo = _nextAinmInfo;
		if (!num3)
		{
			sprite.MainSpriteChange(_nextAinmInfo);
		}
		sprite.InitHeroine(flags.lstHeroine);
		SetClothStateStartMotion(0);
		SetClothStateStartMotion(1);
		dynamicCtrl.Proc();
		dynamicCtrl1.Proc();
		for (int num4 = 0; num4 < lstFemale.Count; num4++)
		{
			lstFemale[num4].getDynamicBoneBust(ChaInfo.DynamicBoneKind.BreastL).InitLocalPosition();
			lstFemale[num4].getDynamicBoneBust(ChaInfo.DynamicBoneKind.BreastR).InitLocalPosition();
		}
		sprite.CloseMotionSubMenu();
		GlobalMethod.HGlobalSaveData((int)_nextAinmInfo.mode, _nextAinmInfo.id);
		Manager.Sound.Stop(Manager.Sound.Type.GameSE3D);
		if (!actYukaTaii)
		{
			foreach (Category item2 in _nextAinmInfo.lstCategory)
			{
				actYukaTaii = item2.category == 0 || item2.category == 3000;
				if (actYukaTaii)
				{
					break;
				}
			}
		}
		if (!changeTaii)
		{
			changeTaii = true;
		}
		if (flags.mode == HFlag.EMode.masturbation)
		{
			bool rotorMasturbation = false;
			foreach (Category item3 in _nextAinmInfo.lstCategory)
			{
				if (item3.category == 1017)
				{
					rotorMasturbation = true;
					break;
				}
			}
			(lstProc[(int)flags.mode] as HMasturbation).SetRotorMasturbation(rotorMasturbation);
		}
		Resources.UnloadUnusedAssets();
	}

	private void CreateListAnimationFileName(bool _isAnimListCreate = true)
	{
		if (_isAnimListCreate)
		{
			CreateAllAnimationList();
		}
		WorldData saveData = Game.saveData;
		Dictionary<int, HashSet<int>> playHList = Game.globalData.playHList;
		bool flag = categorys.Any((int c) => MathfEx.IsRange(1010, c, 1099, isEqual: true) || MathfEx.IsRange(1100, c, 1199, isEqual: true));
		bool flag2 = categorys.Any((int c) => MathfEx.IsRange(3000, c, 3099, isEqual: true));
		for (int num = 0; num < lstAnimInfo.Length; num++)
		{
			lstUseAnimInfo[num] = new List<AnimationListInfo>();
			if (!playHList.TryGetValue(num, out var value))
			{
				value = new HashSet<int>();
			}
			if (flags.isFreeH && value.Count == 0 && !flag && !flag2)
			{
				continue;
			}
			for (int num2 = 0; num2 < lstAnimInfo[num].Count; num2++)
			{
				if (flag)
				{
					if (!lstAnimInfo[num][num2].lstCategory.Any((Category c) => categorys.Contains(c.category)))
					{
						continue;
					}
				}
				else if (!lstAnimInfo[num][num2].lstCategory.Any((Category c) => useCategorys.Contains(c.category)))
				{
					continue;
				}
				if (!flags.isFreeH)
				{
					bool num3 = lstAnimInfo[num][num2].isRelease && !CheckExpAddTaii(num, lstAnimInfo[num][num2].id, flags.lstHeroine[0].hExp);
					bool flag3 = !CheckShopAdd(new HashSet<int>(saveData.player.buyNumTable.Keys), num, lstAnimInfo[num][num2].id);
					if (num3 || flag3 || (lstAnimInfo[num][num2].isExperience != 2 && lstAnimInfo[num][num2].isExperience > (int)flags.experience))
					{
						continue;
					}
				}
				else if (lstAnimInfo[num][num2].stateRestriction > (int)flags.lstHeroine[0].HExperience || (!value.Contains(lstAnimInfo[num][num2].id) && !flag && !flag2))
				{
					continue;
				}
				lstUseAnimInfo[num].Add(lstAnimInfo[num][num2]);
			}
		}
	}

	private void CreateAllAnimationList()
	{
		string value = "AnimationInfo_";
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < lstAnimInfo.Length; i++)
		{
			lstAnimInfo[i] = new List<AnimationListInfo>();
			stringBuilder.Clear();
			stringBuilder.Append(value).AppendFormat("{0:00}", i);
			foreach (string item in HFlag.lstABName)
			{
				if (!AssetBundleCheck.IsFile(item, stringBuilder.ToString()) || (!AssetBundleCheck.IsSimulation && !HFlag.BundleCheck(item, stringBuilder.ToString())))
				{
					continue;
				}
				AnimationInfoData animationInfoData = CommonLib.LoadAsset<AnimationInfoData>(item, stringBuilder.ToString(), clone: false, "", check: false);
				AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: true);
				if (animationInfoData == null)
				{
					continue;
				}
				foreach (AnimationInfoData.Param param in animationInfoData.param)
				{
					AnimationListInfo animationListInfo = lstAnimInfo[i].Find((AnimationListInfo l) => l.id == param.id);
					if (animationListInfo == null)
					{
						lstAnimInfo[i].Add(new AnimationListInfo());
						animationListInfo = lstAnimInfo[i][lstAnimInfo[i].Count - 1];
					}
					animationListInfo.nameAnimation = param.nameAnimation;
					animationListInfo.id = param.id;
					animationListInfo.mode = (HFlag.EMode)i;
					animationListInfo.lstCategory.Clear();
					animationListInfo.lstCategory.AddRange(param.lstCategory);
					animationListInfo.posture = param.posture;
					animationListInfo.kindHoushi = param.kindHoushi;
					animationListInfo.isExperience = param.isExperience;
					animationListInfo.pathMapObjectNull.assetpath = param.pathMapObjectNull.assetpath;
					animationListInfo.pathMapObjectNull.file = param.pathMapObjectNull.file;
					animationListInfo.useDesk = param.useDesk;
					animationListInfo.useChair = param.useChair;
					animationListInfo.pathMaleBase.assetpath = param.pathMaleBase.assetpath;
					animationListInfo.pathMaleBase.file = param.pathMaleBase.file;
					MaleParameter paramMale = animationListInfo.paramMale;
					MaleParameter paramMale2 = param.paramMale;
					paramMale.lstIdLayer.Clear();
					paramMale.lstIdLayer.AddRange(paramMale2.lstIdLayer);
					paramMale.path.assetpath = paramMale2.path.assetpath;
					paramMale.path.file = paramMale2.path.file;
					paramMale.isHitObject = paramMale2.isHitObject;
					paramMale.fileHit = paramMale2.fileHit;
					paramMale.fileHitObject = paramMale2.fileHitObject;
					paramMale.fileLookAt = paramMale2.fileLookAt;
					paramMale.fileMotionNeck = paramMale2.fileMotionNeck;
					paramMale.fileMetaballCtrl = paramMale2.fileMetaballCtrl;
					paramMale.fileCondomCtrl = paramMale2.fileCondomCtrl;
					animationListInfo.pathFemaleBase.assetpath = param.pathFemaleBase.assetpath;
					animationListInfo.pathFemaleBase.file = param.pathFemaleBase.file;
					FemaleParameter paramFemale = animationListInfo.paramFemale;
					FemaleParameter paramFemale2 = param.paramFemale;
					paramFemale.lstIdLayer.Clear();
					paramFemale.lstIdLayer.AddRange(paramFemale2.lstIdLayer);
					paramFemale.lstFrontAndBehind.Clear();
					paramFemale.lstFrontAndBehind.AddRange(paramFemale2.lstFrontAndBehind);
					paramFemale.path.assetpath = paramFemale2.path.assetpath;
					paramFemale.path.file = paramFemale2.path.file;
					paramFemale.isHitObject = paramFemale2.isHitObject;
					paramFemale.fileHit = paramFemale2.fileHit;
					paramFemale.fileHitObject = paramFemale2.fileHitObject;
					paramFemale.isYure = paramFemale2.isYure;
					paramFemale.fileYure = paramFemale2.fileYure;
					paramFemale.fileDynamicBoneRef = paramFemale2.fileDynamicBoneRef;
					paramFemale.fileMotionNeck = paramFemale2.fileMotionNeck;
					paramFemale.fileSe = paramFemale2.fileSe;
					paramFemale.fileSiruPaste = paramFemale2.fileSiruPaste;
					paramFemale.fileAibuEnableMotion = paramFemale2.fileAibuEnableMotion;
					paramFemale.fileReaction = paramFemale2.fileReaction;
					paramFemale.fileItem = paramFemale2.fileItem;
					paramFemale.isAnal = paramFemale2.isAnal;
					paramFemale.nameCamera = paramFemale2.nameCamera;
					animationListInfo.numCtrl = param.numCtrl;
					animationListInfo.sysTaii = param.sysTaii;
					animationListInfo.nameCameraIdle = param.nameCameraIdle;
					animationListInfo.nameCameraKiss = param.nameCameraKiss;
					animationListInfo.lstAibuSpecialItem.Clear();
					animationListInfo.lstAibuSpecialItem.AddRange(param.lstAibuSpecialItem);
					animationListInfo.isFemaleInitiative = param.isFemaleInitiative;
					animationListInfo.houshiLoopActionW = param.houshiLoopActionW;
					animationListInfo.houshiLoopActionS = param.houshiLoopActionS;
					animationListInfo.isSplash = param.isSplash;
					animationListInfo.numMainVoiceID = param.numMainVoiceID;
					animationListInfo.numSubVoiceID = param.numSubVoiceID;
					animationListInfo.numVoiceKindID = param.numVoiceKindID;
					animationListInfo.numMaleSon = param.numMaleSon;
					animationListInfo.numFemaleUpperCloth = param.numFemaleUpperCloth;
					animationListInfo.numFemaleLowerCloth = param.numFemaleLowerCloth;
					animationListInfo.isRelease = param.isRelease;
					animationListInfo.stateRestriction = param.stateRestriction;
					if (param.exsitSecondInfo)
					{
						animationListInfo.pathFemaleBase1.assetpath = param.pathSecondChar.assetpath;
						animationListInfo.pathFemaleBase1.file = param.pathSecondChar.file;
						FemaleParameter paramFemale3 = animationListInfo.paramFemale1;
						paramFemale2 = param.paramSecondChar;
						paramFemale3.lstIdLayer.Clear();
						paramFemale3.lstIdLayer.AddRange(paramFemale2.lstIdLayer);
						paramFemale3.lstFrontAndBehind.Clear();
						paramFemale3.lstFrontAndBehind.AddRange(paramFemale2.lstFrontAndBehind);
						paramFemale3.path.assetpath = paramFemale2.path.assetpath;
						paramFemale3.path.file = paramFemale2.path.file;
						paramFemale3.isHitObject = paramFemale2.isHitObject;
						paramFemale3.fileHit = paramFemale2.fileHit;
						paramFemale3.fileHitObject = paramFemale2.fileHitObject;
						paramFemale3.isYure = paramFemale2.isYure;
						paramFemale3.fileYure = paramFemale2.fileYure;
						paramFemale3.fileDynamicBoneRef = paramFemale2.fileDynamicBoneRef;
						paramFemale3.fileMotionNeck = paramFemale2.fileMotionNeck;
						paramFemale3.fileSe = paramFemale2.fileSe;
						paramFemale3.fileSiruPaste = paramFemale2.fileSiruPaste;
						paramFemale3.fileAibuEnableMotion = paramFemale2.fileAibuEnableMotion;
						paramFemale3.fileReaction = paramFemale2.fileReaction;
						paramFemale3.fileItem = paramFemale2.fileItem;
						paramFemale3.isAnal = paramFemale2.isAnal;
						animationListInfo.numFemaleUpperCloth1 = param.numFemaleUpperCloth1;
						animationListInfo.numFemaleLowerCloth1 = param.numFemaleLowerCloth1;
						animationListInfo.mainHoushi3PShortVoicePtns = new int[4];
						for (int num = 0; num < param.mainHoushi3PShortVoicePtns.Length; num++)
						{
							animationListInfo.mainHoushi3PShortVoicePtns[num] = param.mainHoushi3PShortVoicePtns[num];
						}
						animationListInfo.subHoushi3PShortVoicePtns = new int[4];
						for (int num2 = 0; num2 < param.subHoushi3PShortVoicePtns.Length; num2++)
						{
							animationListInfo.subHoushi3PShortVoicePtns[num2] = param.subHoushi3PShortVoicePtns[num2];
						}
					}
				}
			}
		}
	}

	private void LoadLocalMoveList(string _file, out Vector3 _pos, out Vector3 _rot)
	{
		_pos = Vector3.zero;
		_rot = Vector3.zero;
		foreach (string item in HFlag.lstABName)
		{
			if (!AssetBundleCheck.IsFile(item, _file) || (!AssetBundleCheck.IsSimulation && !HFlag.BundleCheck(item, _file)))
			{
				continue;
			}
			MoveOffsetData moveOffsetData = CommonLib.LoadAsset<MoveOffsetData>(item, _file, clone: false, "", check: false);
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: true);
			if (moveOffsetData == null)
			{
				continue;
			}
			foreach (MoveOffsetData.Param item2 in moveOffsetData.param)
			{
				_pos = item2.pos;
				_rot = item2.rot;
			}
		}
	}

	private void LoadCharaTagLayer(int _id)
	{
		ChaControl chaControl = lstFemale[_id];
		if (!dicCharaCollder.ContainsKey(_id))
		{
			dicCharaCollder.Add(_id, new List<Collider>());
		}
		List<Collider> list = dicCharaCollder[_id];
		list.Clear();
		string text = "h_bone_tag_layer";
		Dictionary<int, ChangeTagLayerData.Param> dictionary = new Dictionary<int, ChangeTagLayerData.Param>();
		foreach (string item in HFlag.lstABName)
		{
			if (!AssetBundleCheck.IsFile(item, text) || (!AssetBundleCheck.IsSimulation && !HFlag.BundleCheck(item, text)))
			{
				continue;
			}
			ChangeTagLayerData changeTagLayerData = CommonLib.LoadAsset<ChangeTagLayerData>(item, text, clone: false, "", check: false);
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: true);
			if (changeTagLayerData == null)
			{
				continue;
			}
			foreach (ChangeTagLayerData.Param item2 in changeTagLayerData.param)
			{
				if (!dictionary.ContainsKey(item2.ID))
				{
					dictionary.Add(item2.ID, new ChangeTagLayerData.Param());
				}
				dictionary[item2.ID].ID = item2.ID;
				dictionary[item2.ID].objName = item2.objName;
				dictionary[item2.ID].Tag = item2.Tag;
				dictionary[item2.ID].Layer = item2.Layer;
			}
		}
		Dictionary<string, GameObject> dictionary2 = new Dictionary<string, GameObject>();
		Transform[] componentsInChildren = chaControl.objBodyBone.GetComponentsInChildren<Transform>();
		foreach (Transform transform in componentsInChildren)
		{
			if (!dictionary2.ContainsKey(transform.name))
			{
				dictionary2.Add(transform.name, transform.gameObject);
			}
		}
		foreach (ChangeTagLayerData.Param value in dictionary.Values)
		{
			GameObject gameObject = (dictionary2.ContainsKey(value.objName) ? dictionary2[value.objName] : null);
			if (!(gameObject == null))
			{
				gameObject.tag = value.Tag;
				gameObject.layer = LayerMask.NameToLayer(value.Layer);
				Collider component = gameObject.GetComponent<Collider>();
				if (!(component == null))
				{
					component.enabled = true;
					list.Add(component);
				}
			}
		}
	}

	private void ShortCut()
	{
		if (sprite.isFade || sprite.GetFadeKindProc() == HSprite.FadeKindProc.OutEnd)
		{
			return;
		}
		if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
		{
			if (Input.GetKeyDown(KeyCode.Alpha2))
			{
				Manager.Config.HData.FemaleEyesCamera = !Manager.Config.HData.FemaleEyesCamera;
			}
			if (Input.GetKeyDown(KeyCode.Alpha3))
			{
				Manager.Config.HData.FemaleNeckCamera = !Manager.Config.HData.FemaleNeckCamera;
			}
			if (Input.GetKeyDown(KeyCode.Alpha5))
			{
				byte lv = (byte)((!new bool[5]
				{
					lstFemale[0].GetSiruFlags(ChaFileDefine.SiruParts.SiruKao) != 0,
					lstFemale[0].GetSiruFlags(ChaFileDefine.SiruParts.SiruFrontUp) != 0,
					lstFemale[0].GetSiruFlags(ChaFileDefine.SiruParts.SiruFrontDown) != 0,
					lstFemale[0].GetSiruFlags(ChaFileDefine.SiruParts.SiruBackUp) != 0,
					lstFemale[0].GetSiruFlags(ChaFileDefine.SiruParts.SiruBackDown) != 0
				}.Any((bool v) => v)) ? 2u : 0u);
				for (ChaFileDefine.SiruParts siruParts = ChaFileDefine.SiruParts.SiruKao; siruParts < (ChaFileDefine.SiruParts)5; siruParts++)
				{
					lstFemale[0].SetSiruFlags(siruParts, lv);
				}
			}
			return;
		}
		if (Input.GetKeyDown(KeyCode.Alpha2))
		{
			Manager.Config.HData.FemaleEyesCamera1 = !Manager.Config.HData.FemaleEyesCamera1;
		}
		if (Input.GetKeyDown(KeyCode.Alpha3))
		{
			Manager.Config.HData.FemaleNeckCamera1 = !Manager.Config.HData.FemaleNeckCamera1;
		}
		if (Input.GetKeyDown(KeyCode.Alpha5) && lstFemale.Count > 1)
		{
			byte lv2 = (byte)((!new bool[5]
			{
				lstFemale[1].GetSiruFlags(ChaFileDefine.SiruParts.SiruKao) != 0,
				lstFemale[1].GetSiruFlags(ChaFileDefine.SiruParts.SiruFrontUp) != 0,
				lstFemale[1].GetSiruFlags(ChaFileDefine.SiruParts.SiruFrontDown) != 0,
				lstFemale[1].GetSiruFlags(ChaFileDefine.SiruParts.SiruBackUp) != 0,
				lstFemale[1].GetSiruFlags(ChaFileDefine.SiruParts.SiruBackDown) != 0
			}.Any((bool v) => v)) ? 2u : 0u);
			for (ChaFileDefine.SiruParts siruParts2 = ChaFileDefine.SiruParts.SiruKao; siruParts2 < (ChaFileDefine.SiruParts)5; siruParts2++)
			{
				lstFemale[1].SetSiruFlags(siruParts2, lv2);
			}
		}
	}

	private void LateShortCut()
	{
		if (!sprite.isFade && sprite.GetFadeKindProc() != HSprite.FadeKindProc.OutEnd)
		{
			GlobalMethod.CameraKeyCtrl(flags.ctrlCamera, lstFemale.ToArray());
		}
	}

	private void GotoPointMoveScene()
	{
		if (hand.IsAction() || flags.voiceWait || categorys.Any((int c) => MathfEx.IsRange(2000, c, 2999, isEqual: true)) || (flags.mode == HFlag.EMode.masturbation && !flags.isFreeH))
		{
			return;
		}
		lstOldFemaleVisible.Clear();
		for (int num = 0; num < lstFemale.Count; num++)
		{
			lstOldFemaleVisible.Add(lstFemale[num].visibleAll);
			lstFemale[num].visibleAll = false;
		}
		lstOldMaleVisible.Clear();
		lstOldMaleVisible.Add(male.visibleAll);
		male.visibleAll = false;
		if ((bool)male1)
		{
			lstOldMaleVisible.Add(male1.visibleAll);
			male1.visibleAll = false;
		}
		item.SetVisible(_visible: false);
		hand.SceneChangeItemEnable(_enable: false);
		GameObject commonSpace = Scene.commonSpace;
		if (commonSpace != null)
		{
			DeliveryHPointData deliveryHPointData = commonSpace.AddComponent<DeliveryHPointData>();
			deliveryHPointData.actionSelect = ChangeCategory;
			deliveryHPointData.actionBack = CancelForHPointMove;
			deliveryHPointData.IDMap = map.no;
			deliveryHPointData.cam = flags.ctrlCamera;
			deliveryHPointData.lstCategory = useCategorys;
			deliveryHPointData.initPos = initPos;
			deliveryHPointData.initRot = initRot;
			deliveryHPointData.isFreeH = flags.isFreeH;
			deliveryHPointData.status = flags.lstHeroine[0].HExperience;
			deliveryHPointData.lstAnimInfo = lstAnimInfo;
			deliveryHPointData.isDebug = false;
			deliveryHPointData.flags = flags;
			deliveryHPointData.lstInitCategory = lstInitCategory;
			deliveryHPointData.usePointInit = lstInitCategory.Any((int c) => c == 0 || c == 1 || c == 8 || c == 1302);
			deliveryHPointData.initOffsetPos = initKindOffsetPos;
			deliveryHPointData.initOffsetRot = initKindOffsetRot;
			ActionScene instance = SingletonInitializer<ActionScene>.instance;
			bool flag = instance != null && instance.Cycle.withHeroine != null && (instance.Cycle.withHeroineSituation == 0 || instance.Cycle.withHeroineSituation == 1);
			deliveryHPointData.usePointInit &= appointAction != 0 && appointAction != 8 && appointAction != 9 && !flag;
			if (instance != null && instance.isPenetration && isMasturbationInitPoint)
			{
				deliveryHPointData.usePointInit = false;
			}
			deliveryHPointData.femalePos = initPos;
		}
		sprite.gameObject.SetActive(value: false);
		Singleton<GameCursor>.Instance.SetCursorTexture(-1);
		UndoMapObject();
		raycaster.enabled = false;
		guideObject.gameObject.SetActive(value: false);
		ctrlObi.Clear();
		AllDoorOpenClose(_isOpen: true);
		flags.ctrlCamera.GetComponent<CameraEffectorConfig>().useDOF = false;
		flags.ctrlCamera.GetComponent<CameraEffector>().dof.enabled = false;
		crossfade.FadeStart();
		Scene.LoadReserve(new Scene.Data
		{
			levelName = "HPointMove",
			isAdd = true,
			isFade = false
		}, isLoadingImageDraw: false);
	}

	private void BackToCharaCollder(int _id)
	{
		if (!dicCharaCollder.TryGetValue(_id, out var value))
		{
			return;
		}
		foreach (Collider item in value)
		{
			if (item != null)
			{
				item.enabled = false;
			}
		}
	}

	private bool GetMapObject()
	{
		UndoMapObject();
		mapObjectNull.lstChair.Clear();
		mapObjectNull.lstDesk.Clear();
		mapObjectNull.lstOther.Clear();
		if (!kindMap)
		{
			return false;
		}
		List<GameObject> list = new List<GameObject>();
		list.Add(kindMap.gameObject);
		list.AddRange(kindMap.groups.Select((Transform t) => t.gameObject));
		list.AddRange(kindMap.targets.Select((Transform t) => t.gameObject));
		foreach (GameObject item in list)
		{
			item.SetActive(value: false);
			MeshCollider component = item.GetComponent<MeshCollider>();
			if ((bool)component)
			{
				component.isTrigger = false;
				component.convex = false;
			}
			if (item.name.Contains("isu") || item.name.Contains("chair"))
			{
				mapObjectNull.lstChair.Add(new MapObjectNullInfo
				{
					obj = item,
					pos = item.transform.position,
					rot = item.transform.rotation,
					layer = item.layer
				});
			}
			else if (item.name.Contains("desk"))
			{
				mapObjectNull.lstDesk.Add(new MapObjectNullInfo
				{
					obj = item,
					pos = item.transform.position,
					rot = item.transform.rotation,
					layer = item.layer
				});
			}
			else
			{
				mapObjectNull.lstOther.Add(new MapObjectNullInfo
				{
					obj = item,
					pos = item.transform.position,
					rot = item.transform.rotation,
					layer = item.layer
				});
			}
		}
		return true;
	}

	private bool GetMapObject(HPointData _data)
	{
		UndoMapObject();
		mapObjectNull.lstChair.Clear();
		mapObjectNull.lstDesk.Clear();
		mapObjectNull.lstOther.Clear();
		if (!_data)
		{
			return false;
		}
		List<GameObject> list = new List<GameObject>();
		if ((bool)_data.selfTransform)
		{
			list.Add(_data.selfTransform.gameObject);
		}
		list.AddRange(_data.objGroups.Select((Transform t) => t.gameObject));
		list.AddRange(_data.objTargets.Select((Transform t) => t.gameObject));
		foreach (GameObject item in list)
		{
			item.SetActive(value: false);
			MeshCollider component = item.GetComponent<MeshCollider>();
			if (component != null)
			{
				component.isTrigger = false;
				component.convex = false;
			}
			if (item.name.Contains("isu") || item.name.Contains("chair"))
			{
				mapObjectNull.lstChair.Add(new MapObjectNullInfo
				{
					obj = item,
					pos = item.transform.position,
					rot = item.transform.rotation,
					layer = item.layer
				});
			}
			else if (item.name.Contains("desk"))
			{
				mapObjectNull.lstDesk.Add(new MapObjectNullInfo
				{
					obj = item,
					pos = item.transform.position,
					rot = item.transform.rotation,
					layer = item.layer
				});
			}
			else
			{
				mapObjectNull.lstOther.Add(new MapObjectNullInfo
				{
					obj = item,
					pos = item.transform.position,
					rot = item.transform.rotation,
					layer = item.layer
				});
			}
		}
		return true;
	}

	private bool SetMapObject(int _useChair, int _useDesk, string _pathAssetNullObject, string _fileNullObject)
	{
		flags.ctrlCamera.lstIgnoreObj.Clear();
		Transform transform = lstFemale[0].transform;
		for (int i = 0; i < mapObjectNull.lstChair.Count; i++)
		{
			MapObjectNullInfo mapObjectNullInfo = mapObjectNull.lstChair[i];
			if ((bool)mapObjectNullInfo.obj)
			{
				mapObjectNullInfo.obj.SetActive(_useChair > i);
				mapObjectNullInfo.obj.transform.SetPositionAndRotation(transform.position, transform.rotation);
				if (_useChair > i || !flags.ctrlCamera.lstIgnoreObj.Contains(mapObjectNullInfo.obj))
				{
					flags.ctrlCamera.lstIgnoreObj.Add(mapObjectNullInfo.obj);
				}
			}
		}
		for (int j = 0; j < mapObjectNull.lstDesk.Count; j++)
		{
			MapObjectNullInfo mapObjectNullInfo2 = mapObjectNull.lstDesk[j];
			if ((bool)mapObjectNullInfo2.obj)
			{
				mapObjectNullInfo2.obj.SetActive(_useDesk > j);
				mapObjectNullInfo2.obj.transform.SetPositionAndRotation(transform.position, transform.rotation);
				if (_useDesk > j || !flags.ctrlCamera.lstIgnoreObj.Contains(mapObjectNullInfo2.obj))
				{
					flags.ctrlCamera.lstIgnoreObj.Add(mapObjectNullInfo2.obj);
				}
			}
		}
		for (int k = 0; k < mapObjectNull.lstOther.Count; k++)
		{
			MapObjectNullInfo mapObjectNullInfo3 = mapObjectNull.lstOther[k];
			if ((bool)mapObjectNullInfo3.obj)
			{
				mapObjectNullInfo3.obj.SetActive(value: false);
				if (!flags.ctrlCamera.lstIgnoreObj.Contains(mapObjectNullInfo3.obj))
				{
					flags.ctrlCamera.lstIgnoreObj.Add(mapObjectNullInfo3.obj);
				}
			}
		}
		foreach (HScene.HObjOffSetData offSetDatum in offSetData)
		{
			if (!(offSetDatum.trs == null))
			{
				if (offSetDatum.UsePos)
				{
					offSetDatum.trs.position = offSetDatum.offsetPos + offSetDatum.backupPos;
				}
				if (offSetDatum.UseAng)
				{
					offSetDatum.trs.Rotate(offSetDatum.offsetAng);
				}
				offSetDatum.trs.gameObject.SetActive(value: true);
			}
		}
		if (_pathAssetNullObject.IsNullOrEmpty() || _fileNullObject.IsNullOrEmpty())
		{
			return false;
		}
		GameObject gameObject = CommonLib.LoadAsset<GameObject>(_pathAssetNullObject, _fileNullObject);
		flags.hashAssetBundle.Add(_pathAssetNullObject);
		if (!gameObject)
		{
			return false;
		}
		gameObject.transform.SetPositionAndRotation(transform.position, transform.rotation);
		HSceneSpriteObjectCategory component = gameObject.GetComponent<HSceneSpriteObjectCategory>();
		if (!component)
		{
			return false;
		}
		for (int l = 0; l < _useChair && l < mapObjectNull.lstChair.Count; l++)
		{
			MapObjectNullInfo mapObjectNullInfo4 = mapObjectNull.lstChair[l];
			Transform transform2 = component.GetObject(l).transform;
			mapObjectNullInfo4.obj.transform.SetPositionAndRotation(transform2.position, transform2.rotation);
		}
		for (int m = 0; m < _useDesk && m < mapObjectNull.lstDesk.Count; m++)
		{
			MapObjectNullInfo mapObjectNullInfo5 = mapObjectNull.lstDesk[m];
			Transform transform3 = component.GetObject(m).transform;
			mapObjectNullInfo5.obj.transform.SetPositionAndRotation(transform3.position, transform3.rotation);
		}
		return true;
	}

	private bool SetCharacterPositon(bool _set)
	{
		initKindOffsetPos = Vector3.zero;
		initKindOffsetRot = Vector3.zero;
		if (!kindMap || !_set)
		{
			return true;
		}
		Transform transform = kindMap.transform;
		initPos = transform.position;
		initRot = transform.rotation;
		initKindOffsetPos = kindMap.offsetPos;
		initKindOffsetRot = kindMap.offsetAngle;
		kindMap.MoveOffset();
		for (int i = 0; i < lstFemale.Count; i++)
		{
			lstFemale[i].transform.SetPositionAndRotation(transform.position, transform.rotation);
		}
		male.transform.SetPositionAndRotation(transform.position, transform.rotation);
		if ((bool)male1)
		{
			male1.transform.SetPositionAndRotation(transform.position, transform.rotation);
		}
		kindMap.ResetPosition();
		return true;
	}

	private void UndoMapObject()
	{
		foreach (MapObjectNullInfo item in mapObjectNull.lstDesk)
		{
			if ((bool)item.obj)
			{
				item.obj.SetActive(value: true);
				item.obj.transform.SetPositionAndRotation(item.pos, item.rot);
				MeshCollider component = item.obj.GetComponent<MeshCollider>();
				if ((bool)component)
				{
					component.convex = true;
					component.isTrigger = true;
				}
			}
		}
		foreach (MapObjectNullInfo item2 in mapObjectNull.lstChair)
		{
			if ((bool)item2.obj)
			{
				item2.obj.SetActive(value: true);
				item2.obj.transform.SetPositionAndRotation(item2.pos, item2.rot);
				MeshCollider component2 = item2.obj.GetComponent<MeshCollider>();
				if ((bool)component2)
				{
					component2.convex = true;
					component2.isTrigger = true;
				}
			}
		}
		foreach (MapObjectNullInfo item3 in mapObjectNull.lstOther)
		{
			if ((bool)item3.obj)
			{
				item3.obj.SetActive(value: true);
				item3.obj.transform.SetPositionAndRotation(item3.pos, item3.rot);
				MeshCollider component3 = item3.obj.GetComponent<MeshCollider>();
				if ((bool)component3)
				{
					component3.convex = true;
					component3.isTrigger = true;
				}
			}
		}
		foreach (HScene.HObjOffSetData offSetDatum in offSetData)
		{
			if (!(offSetDatum.trs == null))
			{
				offSetDatum.trs.position = offSetDatum.backupPos;
				offSetDatum.trs.Rotate(offSetDatum.backupAng);
				offSetDatum.trs.gameObject.SetActive(value: true);
			}
		}
	}

	public void ChangeCategory(HPointData _data, int _category)
	{
		ReturnVisibleForHPointMove();
		if (_data == null)
		{
			return;
		}
		flags.ctrlCamera.lstIgnoreObj.Clear();
		HpointJudgePos = _data.transform.position;
		_data.MoveOffset();
		string text = _data.name;
		Transform transform = _data.transform;
		if (nowHpointDataPos != transform.position)
		{
			bChangePoint = true;
		}
		nowHpointData = text;
		ChaControl chaControl = lstFemale[0];
		for (int i = 0; i < lstFemale.Count; i++)
		{
			lstFemale[i].transform.SetPositionAndRotation(transform.position, transform.rotation);
		}
		male.transform.SetPositionAndRotation(transform.position, transform.rotation);
		if ((bool)male1)
		{
			male1.transform.SetPositionAndRotation(transform.position, transform.rotation);
		}
		nowHpointDataPos = chaControl.transform.position;
		if ((bool)objMoveAxis)
		{
			objMoveAxis.transform.SetPositionAndRotation(chaControl.transform.position, chaControl.transform.rotation);
		}
		_data.ResetPosition();
		_data.GetKindObject(map.mapObjectGroup);
		GetMapObject(_data);
		_data.BackupObjectOffset();
		offSetData.Clear();
		_data.GetOffsetData(ref offSetData);
		if ((flags.mode != HFlag.EMode.lesbian && !categorys.SymmetricExcept(_data.category).Any()) || (flags.mode == HFlag.EMode.lesbian && flags.nowAnimationInfo.lstCategory.Any((Category c) => c.category == _category)))
		{
			SetLocalPosition(flags.nowAnimationInfo);
			SetMapObject(flags.nowAnimationInfo.useChair, flags.nowAnimationInfo.useDesk, flags.nowAnimationInfo.pathMapObjectNull.assetpath, flags.nowAnimationInfo.pathMapObjectNull.file);
			int num = ((flags.click == HFlag.ClickKind.insert) ? 1 : ((flags.click == HFlag.ClickKind.insert_voice) ? 2 : 0));
			string strfile = flags.nowAnimationInfo.paramFemale.nameCamera;
			if (flags.mode == HFlag.EMode.houshi && num == 0)
			{
				strfile = flags.nowAnimationInfo.nameCameraIdle;
			}
			if (Manager.Config.HData.HInitCamera)
			{
				GlobalMethod.LoadCamera(flags.ctrlCamera, "h/list/", strfile);
			}
			else
			{
				GlobalMethod.LoadResetCamera(flags.ctrlCamera, "h/list/", strfile);
			}
			bChangePoint = false;
			CreateListAnimationFileName(_isAnimListCreate: false);
		}
		else
		{
			bool flag = categorys.Count > 0 && ((categorys[0] != 12 && categorys[0] < 1000) || (categorys[0] >= 3000 && categorys[0] < 4000));
			if (flags.isFreeH || categorys.Count != 1 || flag)
			{
				categorys.Clear();
				categorys.AddRange(_data.category);
			}
			flags.mode = HFlag.EMode.none;
			CreateListAnimationFileName(_isAnimListCreate: false);
			ChangeAnimator(LoadAnimationListInfo(_category));
			sprite.InitPointMenuAndHelp(categorys, _isFirst: false);
		}
	}

	public void ReturnVisibleForHPointMove()
	{
		for (int i = 0; i < lstOldFemaleVisible.Count; i++)
		{
			lstFemale[i].visibleAll = lstOldFemaleVisible[i];
		}
		lstOldMaleVisible.SafeProc(0, delegate(bool visible)
		{
			male.visibleAll = visible;
		});
		if ((bool)male1)
		{
			lstOldMaleVisible.SafeProc(1, delegate(bool visible)
			{
				male1.visibleAll = visible;
			});
		}
		AnimatorStateInfo animatorStateInfo = lstFemale[0].getAnimatorStateInfo(0);
		item.SetVisible(_visible: true);
		item.SyncPlay(animatorStateInfo.fullPathHash, animatorStateInfo.normalizedTime);
		sprite.gameObject.SetActive(value: true);
		hand.SceneChangeItemEnable(_enable: true);
		raycaster.enabled = true;
		guideObject.gameObject.SetActive(sprite.axis.tglDraw.isOn);
		AllDoorOpenClose(_isOpen: false);
		flags.ctrlCamera.GetComponent<CameraEffectorConfig>().useDOF = true;
	}

	public void CancelForHPointMove()
	{
		ReturnVisibleForHPointMove();
		crossfade.FadeStart();
		SetMapObject(flags.nowAnimationInfo.useChair, flags.nowAnimationInfo.useDesk, flags.nowAnimationInfo.pathMapObjectNull.assetpath, flags.nowAnimationInfo.pathMapObjectNull.file);
	}

	public IEnumerator ResetScene()
	{
		if ((bool)hand)
		{
			hand.DetachAllItem();
		}
		HandCtrl.ReleaseList();
		for (int i = 0; i < lstFemale.Count; i++)
		{
			lstFemale[i].mouthCtrl.SafeProc(delegate(FBSCtrlMouth m)
			{
				m.OpenMin = 0f;
			});
			lstFemale[i].ChangeLookNeckTarget(0, flags.ctrlCamera.thisCmaera.transform);
			lstFemale[i].ChangeLookEyesTarget(0, flags.ctrlCamera.thisCmaera.transform);
			lstFemale[i].ChangeLookNeckPtn(0);
			lstFemale[i].ChangeLookEyesPtn(0);
			lstFemale[i].SetPosition(Vector3.zero);
			lstFemale[i].SetRotation(Vector3.zero);
		}
		map.mapRoot.SetActive(value: true);
		if (dataH != null && dataH.visibleChange != null)
		{
			dataH.visibleChange.Restore();
		}
		dynamicCtrl.InitDynamicBoneReferenceBone();
		dynamicCtrl.IsInit = false;
		yure.Release();
		yure.ResetShape();
		yureMale.Release();
		yureMale.ResetShape();
		if (particle != null || particle1 != null)
		{
			HParticleCtrl.ReleaseObject(0);
			HParticleCtrl.ReleaseObject(1);
			HParticleCtrl.ReleaseDic();
		}
		flags.lstHeroine.ForEach(delegate(Heroine heroine)
		{
			heroine.charFile.status.visibleBodyAlways = true;
			heroine.chaCtrl.hideMoz = true;
			heroine.chaCtrl.SetPosition(Vector3.zero);
			heroine.chaCtrl.SetRotation(Vector3.zero);
		});
		flags.player.chaCtrl.fileStatus.visibleBodyAlways = true;
		flags.player.chaCtrl.fileStatus.visibleSon = true;
		flags.player.charFile.status.visibleBodyAlways = true;
		flags.player.charFile.status.visibleSon = true;
		guideObject.amount.position = Vector3.zero;
		guideObject.amount.rotation = Vector3.zero;
		guideObject.amountOffset.position = Vector3.zero;
		guideObject.amountOffset.rotation = Vector3.zero;
		guideObject.amountTotal.position = Vector3.zero;
		guideObject.amountTotal.rotation = Vector3.zero;
		LoadEndADVPos();
		if (!flags.isFreeH && lstInitCategory.Contains(1306))
		{
			flags.lstHeroine.ForEach(delegate(Heroine heroine)
			{
				heroine.chaCtrl.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
			});
		}
		if ((bool)parentObjectBaseFemale)
		{
			parentObjectBaseFemale.ReleaseObject();
		}
		if ((bool)parentObjectBaseFemale1)
		{
			parentObjectBaseFemale1.ReleaseObject();
		}
		if ((bool)parentObjectBaseMale)
		{
			parentObjectBaseMale.ReleaseObject();
		}
		if ((bool)parentObjectFemale)
		{
			parentObjectFemale.ReleaseObject();
		}
		if ((bool)parentObjectFemale1)
		{
			parentObjectFemale1.ReleaseObject();
		}
		if ((bool)parentObjectMale)
		{
			parentObjectMale.ReleaseObject();
		}
		item.ReleaseItem();
		UndoMapObject();
		if (kindMap != null)
		{
			if (!kindMap.gameObject.activeSelf)
			{
				kindMap.gameObject.SetActive(value: true);
			}
			Transform[] groups = kindMap.groups;
			foreach (Transform transform in groups)
			{
				if (!transform.gameObject.activeSelf)
				{
					transform.gameObject.SetActive(value: true);
				}
			}
			groups = kindMap.targets;
			foreach (Transform transform2 in groups)
			{
				if (!transform2.gameObject.activeSelf)
				{
					transform2.gameObject.SetActive(value: true);
				}
			}
		}
		if (map.no == 36 && (categorys.Contains(2001) || categorys.Contains(2004)))
		{
			SuiteShower componentInChildren = map.mapRoot.GetComponentInChildren<SuiteShower>();
			if (componentInChildren != null)
			{
				componentInChildren.EndPeep();
			}
		}
		flags.ctrlCamera.VisibleFroceVanish(_visible: true);
		flags.ctrlCamera.CleraVanishCollider();
		flags.ctrlCamera.isOutsideTargetTex = false;
		flags.ctrlCamera.Reset(0);
		InitLocalPosition(3);
		InitLocalPosition(7);
		sprite.LightColorAllInit();
		sprite.LightDirInit();
		foreach (KeyValuePair<int, List<Collider>> item in dicCharaCollder)
		{
			BackToCharaCollder(item.Key);
		}
		foreach (string item2 in flags.hashAssetBundle)
		{
			AssetBundleManager.UnloadAssetBundle(item2, isUnloadForceRefCount: true);
		}
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
		Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
		Voice.StopAll();
		Manager.Sound.Stop(Manager.Sound.Type.GameSE3D);
		AllDoorOpenClose(_isOpen: true);
		if (flags.isFreeH)
		{
			yield return Scene.UnloadBaseSceneAsync("HProc");
		}
		if ((bool)male)
		{
			male.gameObject.SetActive(value: false);
		}
		if ((bool)male1)
		{
			Character.DeleteChara(male1);
		}
		Character.enableCorrectHandSize = true;
	}

	private AnimationListInfo LoadAnimationListInfo(int _category = -1, int _appointAction = -1, bool bFirst = false)
	{
		ActionScene instance = SingletonInitializer<ActionScene>.instance;
		bool flag = instance != null && instance.Cycle.withHeroine != null && (instance.Cycle.withHeroineSituation == 0 || instance.Cycle.withHeroineSituation == 1) && bFirst;
		switch (_appointAction)
		{
		case 0:
		case 3:
		{
			AnimationListInfo animationListInfo7 = lstUseAnimInfo[1].Find((AnimationListInfo a) => a.id == 8);
			if (animationListInfo7 != null)
			{
				return animationListInfo7;
			}
			break;
		}
		case 1:
		{
			AnimationListInfo animationListInfo9 = lstUseAnimInfo[1].Find((AnimationListInfo a) => a.id == 1);
			if (animationListInfo9 != null)
			{
				return animationListInfo9;
			}
			break;
		}
		case 2:
		{
			AnimationListInfo animationListInfo5 = lstUseAnimInfo[1].Find((AnimationListInfo a) => a.id == 5);
			if (animationListInfo5 != null)
			{
				return animationListInfo5;
			}
			break;
		}
		case 4:
		{
			AnimationListInfo animationListInfo3 = lstUseAnimInfo[2].Find((AnimationListInfo a) => a.id == 4);
			if (animationListInfo3 != null)
			{
				return animationListInfo3;
			}
			break;
		}
		case 5:
		{
			AnimationListInfo animationListInfo4 = lstUseAnimInfo[1].Find((AnimationListInfo a) => a.id == 17);
			if (animationListInfo4 != null)
			{
				return animationListInfo4;
			}
			break;
		}
		case 6:
		{
			AnimationListInfo animationListInfo8 = lstUseAnimInfo[1].Find((AnimationListInfo a) => a.id == 27);
			if (animationListInfo8 != null)
			{
				return animationListInfo8;
			}
			break;
		}
		case 8:
		{
			AnimationListInfo animationListInfo2 = lstUseAnimInfo[2].Find((AnimationListInfo a) => a.id == 28);
			if (animationListInfo2 != null)
			{
				return animationListInfo2;
			}
			break;
		}
		case 9:
		{
			AnimationListInfo animationListInfo6 = lstUseAnimInfo[1].Find((AnimationListInfo a) => a.id == 52);
			if (animationListInfo6 != null)
			{
				return animationListInfo6;
			}
			break;
		}
		case -1:
			if (flag)
			{
				AnimationListInfo animationListInfo = lstUseAnimInfo[2].Find((AnimationListInfo a) => a.id == 0);
				if (animationListInfo != null)
				{
					return animationListInfo;
				}
			}
			break;
		}
		if (_category != -1)
		{
			for (int num = 0; num < lstUseAnimInfo.Length; num++)
			{
				AnimationListInfo animationListInfo10 = lstUseAnimInfo[num].Find((AnimationListInfo a) => a.lstCategory.Any((Category c) => c.category == _category));
				if (animationListInfo10 != null)
				{
					return animationListInfo10;
				}
			}
		}
		if (categorys.Any((int c) => c >= 1000))
		{
			for (int num2 = 0; num2 < lstUseAnimInfo.Length; num2++)
			{
				AnimationListInfo animationListInfo11 = lstUseAnimInfo[num2].Find((AnimationListInfo a) => a.lstCategory.Any((Category c) => c.category >= 1000 && categorys.Contains(c.category)));
				if (animationListInfo11 != null)
				{
					return animationListInfo11;
				}
			}
		}
		for (int num3 = 0; num3 < lstUseAnimInfo.Length; num3++)
		{
			if (!lstUseAnimInfo[num3].Any())
			{
				continue;
			}
			AnimationListInfo animationListInfo12 = null;
			for (int num4 = 0; num4 < lstUseAnimInfo[num3].Count; num4++)
			{
				if (CategoryCheck(lstUseAnimInfo[num3][num4].lstCategory, categorys))
				{
					animationListInfo12 = lstUseAnimInfo[num3][num4];
					break;
				}
			}
			if (animationListInfo12 != null)
			{
				return animationListInfo12;
			}
		}
		return null;
	}

	private bool CategoryCheck(List<Category> cat, List<int> checkCat)
	{
		foreach (int ic in checkCat)
		{
			if (cat.Any((Category c) => c.category == ic))
			{
				return true;
			}
		}
		return false;
	}

	private bool SetLocalPosition(AnimationListInfo _info)
	{
		Vector3 _pos = Vector3.zero;
		Vector3 _rot = Vector3.zero;
		for (int i = 0; i < _info.lstCategory.Count; i++)
		{
			if (categorys.Contains(_info.lstCategory[i].category) && !_info.lstCategory[i].fileMove.IsNullOrEmpty())
			{
				LoadLocalMoveList(_info.lstCategory[i].fileMove, out _pos, out _rot);
				break;
			}
		}
		for (int j = 0; j < lstFemale.Count; j++)
		{
			lstFemale[j].SetPosition(_pos);
			lstFemale[j].SetRotation(_rot);
		}
		male.SetPosition(_pos);
		male.SetRotation(_rot);
		if ((bool)male1)
		{
			male1.SetPosition(_pos);
			male1.SetRotation(_rot);
		}
		Transform worldBase = lstFemale[0].objTop.transform;
		item.SetTransform(worldBase);
		flags.ctrlCamera.SetWorldBase(worldBase);
		Vector3 position = lstFemale[0].gameObject.transform.TransformVector(_pos);
		if (bChangePoint || Manager.Config.HData.HInitCamera)
		{
			if ((bool)guideObject)
			{
				guideObject.amount.position = Vector3.zero;
				guideObject.amount.rotation = Vector3.zero;
				guideObject.amountOffset.position = position;
				guideObject.amountOffset.rotation = _rot;
				guideObject.amountTotal.position = guideObject.amount.position + guideObject.amountOffset.position;
				guideObject.amountTotal.rotation = guideObject.amount.rotation + guideObject.amountOffset.rotation;
			}
		}
		else if ((bool)guideObject)
		{
			guideObject.amountOffset.position = position;
			guideObject.amountOffset.rotation = _rot;
			guideObject.amountTotal.position = guideObject.amount.position + guideObject.amountOffset.position;
			guideObject.amountTotal.rotation = guideObject.amount.rotation + guideObject.amountOffset.rotation;
		}
		loadLocalPositon = Vector3.zero;
		loadLocalRotation = Vector3.zero;
		return true;
	}

	private void InitLocalPosition(int _init)
	{
		if (!(guideObject == null))
		{
			switch (_init)
			{
			case 0:
				guideObject.amount.position.x = loadLocalPositon.x;
				break;
			case 1:
				guideObject.amount.position.y = loadLocalPositon.y;
				break;
			case 2:
				guideObject.amount.position.z = loadLocalPositon.z;
				break;
			case 3:
				guideObject.amount.position = loadLocalPositon;
				break;
			case 4:
				guideObject.amount.rotation.x = loadLocalRotation.x;
				break;
			case 5:
				guideObject.amount.rotation.y = loadLocalRotation.y;
				break;
			case 6:
				guideObject.amount.rotation.z = loadLocalRotation.z;
				break;
			case 7:
				guideObject.amount.rotation = loadLocalRotation;
				break;
			}
		}
	}

	private void EndProc()
	{
		isEnd = true;
		Heroine heroine = flags.lstHeroine[0];
		Heroine heroine2 = ((flags.lstHeroine.Count > 1) ? flags.lstHeroine[1] : null);
		ActionScene instance = SingletonInitializer<ActionScene>.instance;
		bool flag = instance != null && instance.GetOtherNPC(10f, dataH.lstFemale[0].charaBase).Length != 0;
		if ((bool)instance && instance.isPenetration && (flags.mode == HFlag.EMode.houshi3P || flags.mode == HFlag.EMode.sonyu3P))
		{
			if (isFirstPlayMasturbation)
			{
				finishADV = 103;
			}
			else
			{
				finishADV = 101;
			}
		}
		else if (flags.count.isInsertKokan && heroine.isVirgin)
		{
			finishADV = 0;
			flags.player.AddHPeopleCount();
			SingletonInitializer<Game>.instance.rankSaveData.expNumCount++;
		}
		else if (flags.GetOrgCount() == 0 && !categorys.Contains(1200) && !categorys.Contains(12) && !categorys.Contains(1306))
		{
			finishADV = ((heroine.HExperience == Heroine.HExperienceKind.淫乱) ? 7 : 6);
		}
		else if (flags.GetInsideAndOutsideCount() == 0 && !categorys.Contains(1002) && !categorys.Contains(1003) && !categorys.Contains(1305))
		{
			finishADV = 8;
		}
		else if (isInvited)
		{
			finishADV = 11;
		}
		else if (categorys.Contains(1003) || categorys.Contains(1305))
		{
			finishADV = ((heroine.lewdness >= 80 && flags.GetOrgCount() != 0) ? 10 : 9);
		}
		else if ((isEasyPlace && flag) || flags.GetOrgCount() >= 3)
		{
			GlobalMethod.ShuffleRand shuffleRand = new GlobalMethod.ShuffleRand();
			List<int> list = new List<int>();
			if (isEasyPlace && flag)
			{
				int num = ((heroine.lewdness >= 80 && flags.GetOrgCount() != 0) ? 10 : 9);
				for (int i = 0; i < 3; i++)
				{
					list.Add(num);
				}
			}
			if (flags.GetOrgCount() >= 3)
			{
				for (int j = 0; j < 2; j++)
				{
					list.Add(4);
				}
			}
			shuffleRand.Init(list);
			finishADV = shuffleRand.Get();
			if (finishADV == -999999)
			{
				finishADV = (isEasyPlace ? 9 : ((flags.GetOrgCount() >= 3) ? 4 : 10));
			}
		}
		else
		{
			GlobalMethod.ShuffleRand shuffleRand2 = new GlobalMethod.ShuffleRand();
			List<int> list2 = new List<int>();
			int[] array = new int[4] { 1, 1, 2, 3 };
			int[] array2 = new int[4] { 1, 1, 1, 2 };
			if (array[(int)heroine.HExperience] != -1)
			{
				for (int k = 0; k < array2[(int)heroine.HExperience]; k++)
				{
					list2.Add(array[(int)heroine.HExperience]);
				}
			}
			int key = Mathf.Clamp((heroine.fixCharaID < 0) ? heroine.relationNPC : heroine.relation, 0, 2);
			if (instance != null && Mathf.InverseLerp(0f, instance.stepUpDic[key], heroine.favor) >= 0.7f && heroine.isGirlfriend)
			{
				for (int l = 0; l < 3; l++)
				{
					list2.Add(5);
				}
			}
			shuffleRand2.Init(list2);
			finishADV = shuffleRand2.Get();
			if (finishADV == -999999)
			{
				finishADV = 1;
			}
		}
		countKiss = flags.count.kiss;
		myRoom = map.no == 10 || map.no == 18 || map.no == 22;
		foreach (int item in lstInitCategory)
		{
			if ((item == 12 || item >= 1002) && (item <= 1009 || item > 1200) && item <= 1400)
			{
				spHpoint = true;
				break;
			}
		}
		Orgasm = flags.GetOrgCount() != 0 || categorys.Contains(1200) || categorys.Contains(12) || categorys.Contains(1306);
		if (flags.mode != HFlag.EMode.peeping && flags.mode != HFlag.EMode.masturbation && flags.mode != HFlag.EMode.lesbian)
		{
			heroine.AddHCount();
			heroine2?.AddHCount();
		}
		if (heroine.isVirgin && flags.count.isInsertKokan)
		{
			heroine.isVirgin = false;
		}
		if (heroine.isAnalVirgin && flags.count.isInsertAnal)
		{
			heroine.isAnalVirgin = false;
		}
		for (int m = 0; m < heroine.massageExps.Length; m++)
		{
			heroine.massageExps[m] = Mathf.Min(heroine.massageExps[m] + flags.count.selectMassages[m], 100f);
		}
		if (!heroine.isKiss && flags.count.kiss != 0)
		{
			heroine.isKiss = true;
		}
		if (flags.count.isNameInsertKokan)
		{
			heroine.countNamaInsert = Mathf.Min(heroine.countNamaInsert + 1, 1000);
		}
		if (flags.count.isHoushiPlay)
		{
			heroine.countHoushi = Mathf.Min(heroine.countHoushi + 1, 1000);
		}
		CalcParameter(addParameter);
		heroine.hAreaExps[0] = Mathf.Min(heroine.hAreaExps[0] + flags.count.selectAreas[0], 100f);
		if (!flags.isUseImmediatelyFinishButton)
		{
			SingletonInitializer<Game>.instance.rankSaveData.orgasmCount += (uint)flags.GetOrgCount();
			SingletonInitializer<Game>.instance.rankSaveData.inEjaculationCount += (uint)(flags.count.sonyuInside + flags.count.sonyuCondomInside);
			SingletonInitializer<Game>.instance.rankSaveData.inEjaculationKokanNamaCount += (uint)flags.count.sonyuInside;
			SingletonInitializer<Game>.instance.rankSaveData.inEjaculationAnalCount += (uint)(flags.count.sonyuAnalInside + flags.count.sonyuAnalCondomInside);
			SingletonInitializer<Game>.instance.rankSaveData.outEjaculationCount += (uint)(flags.count.sonyuOutside + flags.count.sonyuAnalOutside);
		}
		SingletonInitializer<Game>.instance.rankSaveData.bustGauge += (uint)flags.count.selectAreasGlobal[1];
		SingletonInitializer<Game>.instance.rankSaveData.hipGauge += (uint)flags.count.selectAreasGlobal[4];
		SingletonInitializer<Game>.instance.rankSaveData.asokoGauge += (uint)flags.count.selectAreasGlobal[2];
		SingletonInitializer<Game>.instance.rankSaveData.adultToyGauge += (uint)flags.count.selectHobby;
		if (flags.mode != HFlag.EMode.peeping && flags.mode != HFlag.EMode.masturbation && flags.mode != HFlag.EMode.lesbian)
		{
			flags.player.AddHCount();
		}
		flags.player.AddOrgCount(flags.GetOrgCount());
		heroine.isDayH = true;
		if (heroine2 != null)
		{
			heroine2.isDayH = true;
		}
	}

	private void NewHeroineEndProc()
	{
		isEnd = true;
		Heroine heroine = flags.lstHeroine[0];
		if (flags.mode == HFlag.EMode.masturbation)
		{
			finishADV = ((flags.isMasturbationFound && heroine.HExperience == Heroine.HExperienceKind.淫乱) ? 105 : 104);
		}
		else if (flags.mode == HFlag.EMode.lesbian)
		{
			finishADV = 102;
		}
		else
		{
			finishADV = 100;
		}
		if (flags.mode != HFlag.EMode.peeping && flags.mode != HFlag.EMode.masturbation && flags.mode != HFlag.EMode.lesbian)
		{
			heroine.AddHCount();
		}
		if (heroine.isAnalVirgin && flags.count.isInsertAnal)
		{
			heroine.isAnalVirgin = false;
		}
		for (int i = 0; i < heroine.massageExps.Length; i++)
		{
			heroine.massageExps[i] = Mathf.Min(heroine.massageExps[i] + flags.count.selectMassages[i], 100f);
		}
		if (!heroine.isKiss && flags.count.kiss != 0)
		{
			heroine.isKiss = true;
		}
		if (flags.count.isNameInsertKokan)
		{
			heroine.countNamaInsert = Mathf.Min(heroine.countNamaInsert + 1, 1000);
		}
		if (flags.count.isHoushiPlay)
		{
			heroine.countHoushi = Mathf.Min(heroine.countHoushi + 1, 1000);
		}
		heroine.hAreaExps[0] = Mathf.Min(heroine.hAreaExps[0] + flags.count.selectAreas[0], 100f);
		if (!flags.isUseImmediatelyFinishButton)
		{
			SingletonInitializer<Game>.instance.rankSaveData.orgasmCount += (uint)flags.GetOrgCount();
			SingletonInitializer<Game>.instance.rankSaveData.inEjaculationCount += (uint)(flags.count.sonyuInside + flags.count.sonyuCondomInside);
			SingletonInitializer<Game>.instance.rankSaveData.inEjaculationKokanNamaCount += (uint)flags.count.sonyuInside;
			SingletonInitializer<Game>.instance.rankSaveData.inEjaculationAnalCount += (uint)(flags.count.sonyuAnalInside + flags.count.sonyuAnalCondomInside);
			SingletonInitializer<Game>.instance.rankSaveData.outEjaculationCount += (uint)(flags.count.sonyuOutside + flags.count.sonyuAnalOutside);
		}
		SingletonInitializer<Game>.instance.rankSaveData.bustGauge += (uint)flags.count.selectAreasGlobal[1];
		SingletonInitializer<Game>.instance.rankSaveData.hipGauge += (uint)flags.count.selectAreasGlobal[4];
		SingletonInitializer<Game>.instance.rankSaveData.asokoGauge += (uint)flags.count.selectAreasGlobal[2];
		SingletonInitializer<Game>.instance.rankSaveData.adultToyGauge += (uint)flags.count.selectHobby;
		if (flags.mode != HFlag.EMode.peeping && flags.mode != HFlag.EMode.masturbation && flags.mode != HFlag.EMode.lesbian)
		{
			flags.player.AddHCount();
		}
		flags.player.AddOrgCount(flags.GetOrgCount());
	}

	private void CalcParameter(HScene.AddParameter _add)
	{
		Heroine heroine = flags.lstHeroine[0];
		for (int i = 0; i < _add.aibus.Length; i++)
		{
			_add.aibus[i] += Mathf.Min(1000f, flags.count.selectAreas[i + 1] * flags.expCheat) * 0.1f;
			_add.aibus[i] = Mathf.Min(1000f, _add.aibus[i]);
		}
		_add.kiss += Mathf.Min(1000f, flags.count.selectAreas[0] * flags.expCheat) * 0.1f;
		_add.kiss = Mathf.Min(1000f, _add.kiss);
		int num = flags.count.houshiInside + flags.count.houshiOutside;
		for (int j = 0; j < num; j++)
		{
			switch (j)
			{
			case 0:
				_add.houshi += 15f;
				break;
			case 1:
				_add.houshi += 10f;
				break;
			case 2:
				_add.houshi += 5f;
				break;
			default:
				_add.houshi += 1f;
				break;
			}
		}
		_add.houshi = Mathf.Min(100f, _add.houshi * flags.expCheat);
		_add.sonyus[0] += (flags.count.isInsertKokan ? 5 : 0);
		num = flags.count.sonyuCondomInside + flags.count.sonyuOrg + flags.count.sonyuInside + flags.count.sonyuOutside;
		for (int k = 0; k < num; k++)
		{
			switch (k)
			{
			case 0:
				_add.sonyus[0] += 15f;
				break;
			case 1:
				_add.sonyus[0] += 10f;
				break;
			case 2:
				_add.sonyus[0] += 5f;
				break;
			default:
				_add.sonyus[0] += 1f;
				break;
			}
		}
		_add.sonyus[0] = Mathf.Min(100f, _add.sonyus[0] * flags.expCheat);
		_add.sonyus[1] += (flags.count.isInsertAnal ? 5 : 0);
		num = flags.count.sonyuAnalCondomInside + flags.count.sonyuAnalOrg + flags.count.sonyuAnalInside + flags.count.sonyuAnalOutside;
		for (int l = 0; l < num; l++)
		{
			switch (l)
			{
			case 0:
				_add.sonyus[1] += 15f;
				break;
			case 1:
				_add.sonyus[1] += 10f;
				break;
			case 2:
				_add.sonyus[1] += 5f;
				break;
			default:
				_add.sonyus[1] += 1f;
				break;
			}
		}
		_add.sonyus[1] = Mathf.Min(100f, _add.sonyus[1] * flags.expCheat);
		LoadResultExp();
		bool bitch = heroine.parameter.attribute.bitch;
		int num2 = flags.count.sonyuSame + flags.count.sonyuCondomSame + flags.count.sonyuAnalSame + flags.count.sonyuAnalCondomSame;
		int orgCount = flags.GetOrgCount();
		int insideAndOutsideCount = flags.GetInsideAndOutsideCount();
		bool flag = num2 > 0;
		bool flag2 = orgCount > 0 && orgCount != num2;
		bool flag3 = insideAndOutsideCount > 0 && insideAndOutsideCount != num2;
		float num3 = 0f;
		_add.hexpMax = 0f;
		if (!flag && !flag2 && !flag3)
		{
			num3 = (bitch ? dicHexpBase[3].bitch : dicHexpBase[3].common);
			_add.hexp.Add(new HScene.AddParameter.Exp
			{
				kind = 3,
				val = num3
			});
			_add.hexpMax += num3;
		}
		else
		{
			if (flag)
			{
				num3 = (bitch ? dicHexpBase[2].bitch : dicHexpBase[2].common);
				_add.hexp.Add(new HScene.AddParameter.Exp
				{
					kind = 2,
					val = num3
				});
				_add.hexpMax += num3;
			}
			if (flag2)
			{
				num3 = (bitch ? dicHexpBase[0].bitch : dicHexpBase[0].common);
				_add.hexp.Add(new HScene.AddParameter.Exp
				{
					kind = 0,
					val = num3
				});
				_add.hexpMax += num3;
			}
			if (flag3)
			{
				num3 = (bitch ? dicHexpBase[1].bitch : dicHexpBase[1].common);
				_add.hexp.Add(new HScene.AddParameter.Exp
				{
					kind = 1,
					val = num3
				});
				_add.hexpMax += num3;
			}
		}
		ActionScene instance = SingletonInitializer<ActionScene>.instance;
		if (heroine.hCount <= 1)
		{
			_add.hexpBonus.kind = 4;
			_add.hexpBonus.val = UnityEngine.Random.Range((int)dicHexpBonus[4].min, (int)dicHexpBonus[4].max);
		}
		else if (instance != null && instance.Cycle.withHeroine != null)
		{
			switch (instance.Cycle.withHeroineSituation)
			{
			case 0:
				_add.hexpBonus.kind = 5;
				_add.hexpBonus.val = dicHexpBonus[5].min;
				break;
			case 1:
				_add.hexpBonus.kind = 6;
				_add.hexpBonus.val = dicHexpBonus[6].min;
				break;
			}
		}
		else if (isInvited)
		{
			_add.hexpBonus.kind = 7;
			_add.hexpBonus.val = dicHexpBonus[7].min;
		}
		else
		{
			_add.hexpBonus.kind = -1;
		}
		_add.hexpMax = Mathf.Min(100f, _add.hexpMax * flags.expCheat);
	}

	private void SetConfig(bool _force = false)
	{
		if ((bool)male)
		{
			if (maleConfig.visibleSimple != Manager.Config.HData.SimpleBody || _force)
			{
				male.ChangeSimpleBodyDraw(Manager.Config.HData.SimpleBody);
				maleConfig.visibleSimple = Manager.Config.HData.SimpleBody;
			}
			if (maleConfig.color != Manager.Config.HData.SilhouetteColor || _force)
			{
				male.ChangeSimpleBodyColor(Manager.Config.HData.SilhouetteColor);
				maleConfig.color = Manager.Config.HData.SilhouetteColor;
			}
			if (maleConfig.cloth != Manager.Config.HData.IsMaleClothes || _force)
			{
				for (ChaFileDefine.ClothesKind clothesKind = ChaFileDefine.ClothesKind.top; clothesKind < ChaFileDefine.ClothesKind.shoes_inner; clothesKind++)
				{
					male.SetClothesState((int)clothesKind, (byte)((!Manager.Config.HData.IsMaleClothes) ? 3u : 0u));
					if ((bool)male1)
					{
						male1.SetClothesState((int)clothesKind, (byte)((!Manager.Config.HData.IsMaleClothes) ? 3u : 0u));
					}
				}
				maleConfig.cloth = Manager.Config.HData.IsMaleClothes;
			}
			if (maleConfig.accessoryMain != Manager.Config.HData.IsMaleAccessoriesMain || _force)
			{
				male.SetAccessoryStateCategory(0, Manager.Config.HData.IsMaleAccessoriesMain);
				if ((bool)male1)
				{
					male1.SetAccessoryStateCategory(0, Manager.Config.HData.IsMaleAccessoriesMain);
				}
				maleConfig.accessoryMain = Manager.Config.HData.IsMaleAccessoriesMain;
			}
			if (maleConfig.accessorySub != Manager.Config.HData.IsMaleAccessoriesSub || _force)
			{
				male.SetAccessoryStateCategory(1, Manager.Config.HData.IsMaleAccessoriesSub);
				if ((bool)male1)
				{
					male1.SetAccessoryStateCategory(1, Manager.Config.HData.IsMaleAccessoriesSub);
				}
				maleConfig.accessorySub = Manager.Config.HData.IsMaleAccessoriesSub;
			}
			if (maleConfig.shoes != Manager.Config.HData.IsMaleShoes || _force)
			{
				male.SetClothesState(7, (byte)((!Manager.Config.HData.IsMaleShoes) ? 3u : 0u));
				if ((bool)male1)
				{
					male1.SetClothesState(7, (byte)((!Manager.Config.HData.IsMaleShoes) ? 3u : 0u));
				}
				maleConfig.shoes = Manager.Config.HData.IsMaleShoes;
			}
		}
		bool oldMap;
		map.mapRoot.SafeProc(delegate(GameObject o)
		{
			oldMap = o.activeSelf;
			o.SetActive(Manager.Config.HData.Map);
			if (Manager.Config.HData.Map && oldMap != Manager.Config.HData.Map)
			{
				flags.ctrlCamera.CleraVanishCollider();
				flags.ctrlCamera.VisibleFroceVanish(_visible: true, Ignore: true);
				AllDoorOpenCloseWithMap(_isOpen: false);
			}
		});
		flags.ctrlCamera.thisCmaera.clearFlags = (Manager.Config.HData.Map ? CameraClearFlags.Skybox : CameraClearFlags.Color);
		flags.ctrlCamera.ConfigVanish = Manager.Config.HData.Shield;
		flags.ctrlCamera.isConfigTargetTex = Manager.Config.EtcData.Look;
		flags.ctrlCamera.thisCmaera.backgroundColor = Manager.Config.HData.BackColor;
	}

	private void SetClothStateStartMotion(int _cha)
	{
		if (lstFemale.Count > _cha)
		{
			GlobalMethod.SetAllClothState(lstFemale[_cha], _isUpper: true, (_cha == 0) ? flags.nowAnimationInfo.numFemaleUpperCloth : flags.nowAnimationInfo.numFemaleUpperCloth1);
			GlobalMethod.SetAllClothState(lstFemale[_cha], _isUpper: false, (_cha == 0) ? flags.nowAnimationInfo.numFemaleLowerCloth : flags.nowAnimationInfo.numFemaleLowerCloth1);
		}
	}

	private void LoadSpecialMapStartPosition(out int _no, out string _objName)
	{
		_objName = "";
		_no = -1;
		if (map != null && map.no == 36 && (categorys.Contains(2001) || categorys.Contains(2004)))
		{
			return;
		}
		string text = "SpecialMapStartPosition";
		foreach (string item in HFlag.lstABName)
		{
			if (!AssetBundleCheck.IsFile(item, text) || (!AssetBundleCheck.IsSimulation && !HFlag.BundleCheck(item, text)))
			{
				continue;
			}
			SPMapStartPositionData sPMapStartPositionData = CommonLib.LoadAsset<SPMapStartPositionData>(item, text, clone: false, "", check: false);
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: true);
			if (sPMapStartPositionData == null)
			{
				continue;
			}
			foreach (SPMapStartPositionData.Param item2 in sPMapStartPositionData.param)
			{
				if (categorys.Contains(item2.category))
				{
					_no = item2.mapID;
					_objName = item2.startPositionName;
				}
			}
		}
	}

	private bool SetState(Heroine _heroine, int _status)
	{
		if (_heroine == null)
		{
			return false;
		}
		int[] array = new int[4] { 0, 1, 5, 5 };
		float[] array2 = new float[4] { 0f, 1f, 100f, 100f };
		float[] array3 = new float[4] { 0f, 1f, 100f, 100f };
		bool[] array4 = new bool[4] { true, false, false, false };
		bool[] array5 = new bool[4] { true, false, false, false };
		int[] array6 = new int[4] { 0, 1, 100, 100 };
		int[] array7 = new int[4] { 0, 1, 100, 100 };
		bool[] array8 = new bool[4] { false, true, true, true };
		int[] array9 = new int[4] { 0, 1, 100, 100 };
		int[] array10 = new int[4] { 0, 1, 100, 100 };
		int[] array11 = new int[4] { 0, 0, 0, 100 };
		_heroine.hCount = array[_status];
		for (int i = 0; i < _heroine.hAreaExps.Length; i++)
		{
			_heroine.hAreaExps[i] = array2[_status];
		}
		for (int j = 0; j < _heroine.massageExps.Length; j++)
		{
			_heroine.massageExps[j] = array3[_status];
		}
		_heroine.isVirgin = array4[_status];
		_heroine.isAnalVirgin = array5[_status];
		_heroine.countKokanH = array6[_status];
		_heroine.countAnalH = array7[_status];
		_heroine.isKiss = array8[_status];
		_heroine.countNamaInsert = array9[_status];
		_heroine.countHoushi = array10[_status];
		_heroine.lewdness = array11[_status];
		_heroine.favor = 100;
		return true;
	}

	private void LoadMapDependent(int _map)
	{
		Dictionary<int, MapDependent> dictionary = new Dictionary<int, MapDependent>();
		string text = "h_map_dependent";
		foreach (string item in HFlag.lstABName)
		{
			if (!AssetBundleCheck.IsFile(item, text) || (!AssetBundleCheck.IsSimulation && !HFlag.BundleCheck(item, text)))
			{
				continue;
			}
			HMapDependentData hMapDependentData = CommonLib.LoadAsset<HMapDependentData>(item, text, clone: false, "", check: false);
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: true);
			if (hMapDependentData == null)
			{
				continue;
			}
			foreach (HMapDependentData.Param item2 in hMapDependentData.param)
			{
				if (!dictionary.ContainsKey(item2.mapID))
				{
					dictionary.Add(item2.mapID, new MapDependent());
				}
				dictionary[item2.mapID].socks = item2.socks;
				dictionary[item2.mapID].shoes = item2.shoes;
				dictionary[item2.mapID].glove = item2.glove;
			}
		}
		if (!dictionary.ContainsKey(_map))
		{
			return;
		}
		if (dictionary[_map].glove != -1)
		{
			byte state = (byte)((dictionary[_map].glove == 1) ? 3u : 0u);
			for (int i = 0; i < lstFemale.Count; i++)
			{
				lstFemale[i].SetClothesState(4, state);
			}
			male.SetClothesState(4, state);
			if ((bool)male1)
			{
				male1.SetClothesState(4, state);
			}
		}
		if (dictionary[_map].socks != -1)
		{
			byte state2 = (byte)((dictionary[_map].socks == 1) ? 3u : 0u);
			for (int j = 0; j < lstFemale.Count; j++)
			{
				lstFemale[j].SetClothesState(6, state2);
			}
			male.SetClothesState(6, state2);
			if ((bool)male1)
			{
				male1.SetClothesState(6, state2);
			}
		}
		if (dictionary[_map].shoes != -1)
		{
			byte state3 = (byte)((dictionary[_map].shoes == 1) ? 3u : 0u);
			for (int k = 0; k < lstFemale.Count; k++)
			{
				lstFemale[k].SetClothesState(7, state3);
				lstFemale[k].SetClothesState(8, state3);
			}
			male.SetClothesState(7, state3);
			male.SetClothesState(8, state3);
			if ((bool)male1)
			{
				male1.SetClothesState(7, state3);
				male1.SetClothesState(8, state3);
			}
		}
	}

	private void AllDoorOpenClose(bool _isOpen)
	{
		if (map == null || map.mapRoot == null)
		{
			return;
		}
		Door[] componentsInChildren = map.mapRoot.transform.GetComponentsInChildren<Door>(includeInactive: true);
		if (componentsInChildren.Length == 0)
		{
			return;
		}
		flags.ctrlCamera.CleraVanishCollider();
		flags.ctrlCamera.VisibleFroceVanish(_visible: true, Ignore: true);
		Door[] array = componentsInChildren;
		foreach (Door door in array)
		{
			if (_isOpen)
			{
				door.OpenForce();
			}
			else
			{
				door.CloseForce();
			}
		}
	}

	private void AllDoorOpenCloseWithMap(bool _isOpen)
	{
		if ((Scene.NowSceneNames != null && Scene.NowSceneNames.Contains("HPointMove")) || map == null || map.mapRoot == null)
		{
			return;
		}
		Door[] componentsInChildren = map.mapRoot.transform.GetComponentsInChildren<Door>(includeInactive: true);
		List<Renderer> rend = new List<Renderer>();
		Door[] array = componentsInChildren;
		foreach (Door door in array)
		{
			int num = 0;
			Renderer[] componentsInChildren2 = door.GetComponentsInChildren<Renderer>(includeInactive: true);
			for (int j = 0; j < componentsInChildren2.Length; j++)
			{
				int num2 = j;
				if (!(componentsInChildren2[num2].name == door.cachedCollider.name))
				{
					rend.Add(componentsInChildren2[num2]);
					break;
				}
			}
			Animator animator = door.GetAnimator();
			if (!(animator == null))
			{
				rend[num++].enabled = false;
				if (animator.gameObject.activeInHierarchy)
				{
					animator.Play(_isOpen ? openIdleState : closeIdleState);
				}
			}
		}
		Observable.NextFrame().Subscribe(delegate
		{
			foreach (Renderer item in rend)
			{
				item.enabled = true;
			}
		});
	}

	private void SetShortcutKey()
	{
		using ShortcutKeyScope shortcutKeyScope = new ShortcutKeyScope(base.gameObject);
		ShortcutKey shortcutkey = shortcutKeyScope.shortcutKey;
		ShortcutKey.Proc proc = new ShortcutKey.Proc
		{
			keyCode = KeyCode.Escape,
			enabled = true
		};
		proc.call.AddListener(delegate
		{
			Singleton<GameCursor>.Instance.SetCursorTexture(-1);
			Singleton<GameCursor>.Instance.SetCursorLock(setLockFlag: false);
			GameCursor.isDraw = true;
			hand.ForceFinish();
			shortcutkey._GameEnd();
		});
		shortcutkey.procList.Add(proc);
		proc = new ShortcutKey.Proc
		{
			keyCode = KeyCode.F1,
			enabled = true
		};
		proc.call.AddListener(delegate
		{
			Singleton<GameCursor>.Instance.SetCursorTexture(-1);
			Singleton<GameCursor>.Instance.SetCursorLock(setLockFlag: false);
			GameCursor.isDraw = true;
			hand.ForceFinish();
			shortcutkey._OpenConfig();
		});
		shortcutkey.procList.Add(proc);
		proc = new ShortcutKey.Proc
		{
			keyCode = KeyCode.F2,
			enabled = true
		};
		proc.call.AddListener(delegate
		{
			Singleton<GameCursor>.Instance.SetCursorTexture(-1);
			Singleton<GameCursor>.Instance.SetCursorLock(setLockFlag: false);
			GameCursor.isDraw = true;
			hand.ForceFinish();
			shortcutkey._OpenShortcutKey();
		});
		shortcutkey.procList.Add(proc);
		proc = new ShortcutKey.Proc
		{
			keyCode = KeyCode.F5,
			enabled = true
		};
		proc.call.AddListener(delegate
		{
			Singleton<GameCursor>.Instance.SetCursorTexture(-1);
			Singleton<GameCursor>.Instance.SetCursorLock(setLockFlag: false);
			GameCursor.isDraw = true;
			hand.ForceFinish();
			shortcutkey._OpenTutorial();
		});
		shortcutkey.procList.Add(proc);
		proc = new ShortcutKey.Proc
		{
			keyCode = KeyCode.F11,
			enabled = true
		};
		proc.call.AddListener(delegate
		{
			gss.Capture();
		});
		shortcutkey.procList.Add(proc);
		proc = new ShortcutKey.Proc
		{
			keyCode = KeyCode.Alpha1,
			enabled = true
		};
		proc.call.AddListener(delegate
		{
			Manager.Config.EtcData.Look = !Manager.Config.EtcData.Look;
		});
		shortcutkey.procList.Add(proc);
		proc = new ShortcutKey.Proc
		{
			keyCode = KeyCode.Alpha4,
			enabled = true
		};
		proc.call.AddListener(delegate
		{
			Manager.Config.HData.SemenType = (Manager.Config.HData.SemenType + 1) % 3;
		});
		shortcutkey.procList.Add(proc);
		proc = new ShortcutKey.Proc
		{
			keyCode = KeyCode.Alpha6,
			enabled = true
		};
		proc.call.AddListener(delegate
		{
			Manager.Config.HData.Map = !Manager.Config.HData.Map;
		});
		shortcutkey.procList.Add(proc);
		proc = new ShortcutKey.Proc
		{
			keyCode = KeyCode.Alpha7,
			enabled = true
		};
		proc.call.AddListener(delegate
		{
			Manager.Config.HData.Shield = !Manager.Config.HData.Shield;
		});
		shortcutkey.procList.Add(proc);
		proc = new ShortcutKey.Proc
		{
			keyCode = KeyCode.Alpha8,
			enabled = true
		};
		proc.call.AddListener(delegate
		{
			sprite.axis.tglDraw.isOn = !sprite.axis.tglDraw.isOn;
			sprite.MoveAxisDraw(sprite.axis.tglDraw.isOn);
		});
		shortcutkey.procList.Add(proc);
		proc = new ShortcutKey.Proc
		{
			keyCode = KeyCode.Alpha9,
			enabled = true
		};
		proc.call.AddListener(delegate
		{
			if (sprite.moveAxizInteractive.lstTgl[0].isActiveAndEnabled)
			{
				sprite.moveAxizInteractive.lstTgl.First((Toggle t) => !t.isOn).isOn = true;
			}
			else
			{
				sprite.moveAxizInteractive.lstTgl.ForEach(delegate(Toggle t)
				{
					t.isOn = !t.isOn;
				});
			}
			sprite.ChangeMoveAxis((!sprite.moveAxizInteractive.lstTgl[0].isOn) ? 1 : 0);
		});
		shortcutkey.procList.Add(proc);
		proc = new ShortcutKey.Proc
		{
			keyCode = KeyCode.Z,
			enabled = true
		};
		proc.call.AddListener(delegate
		{
			Manager.Config.HData.VisibleBody = !Manager.Config.HData.VisibleBody;
		});
		shortcutkey.procList.Add(proc);
		proc = new ShortcutKey.Proc
		{
			keyCode = KeyCode.X,
			enabled = true
		};
		proc.call.AddListener(delegate
		{
			Manager.Config.HData.VisibleSon = !Manager.Config.HData.VisibleSon;
		});
		shortcutkey.procList.Add(proc);
		proc = new ShortcutKey.Proc
		{
			keyCode = KeyCode.C,
			enabled = true
		};
		proc.call.AddListener(delegate
		{
			Manager.Config.HData.SimpleBody = !Manager.Config.HData.SimpleBody;
		});
		shortcutkey.procList.Add(proc);
		proc = new ShortcutKey.Proc
		{
			keyCode = KeyCode.V,
			enabled = true
		};
		proc.call.AddListener(delegate
		{
			Manager.Config.HData.IsMaleClothes = !Manager.Config.HData.IsMaleClothes;
		});
		shortcutkey.procList.Add(proc);
		proc = new ShortcutKey.Proc
		{
			keyCode = KeyCode.B,
			enabled = true
		};
		proc.call.AddListener(delegate
		{
			Manager.Config.HData.IsMaleAccessoriesMain = !Manager.Config.HData.IsMaleAccessoriesMain;
		});
		shortcutkey.procList.Add(proc);
		proc = new ShortcutKey.Proc
		{
			keyCode = KeyCode.N,
			enabled = true
		};
		proc.call.AddListener(delegate
		{
			Manager.Config.HData.IsMaleAccessoriesSub = !Manager.Config.HData.IsMaleAccessoriesSub;
		});
		shortcutkey.procList.Add(proc);
		proc = new ShortcutKey.Proc
		{
			keyCode = KeyCode.M,
			enabled = true
		};
		proc.call.AddListener(delegate
		{
			Manager.Config.HData.IsMaleShoes = !Manager.Config.HData.IsMaleShoes;
		});
		shortcutkey.procList.Add(proc);
		proc = new ShortcutKey.Proc
		{
			keyCode = KeyCode.A,
			enabled = true
		};
		proc.call.AddListener(delegate
		{
			flags.isAibuSelect = !flags.isAibuSelect;
		});
		shortcutkey.procList.Add(proc);
		proc = new ShortcutKey.Proc
		{
			keyCode = KeyCode.S,
			enabled = true
		};
		proc.call.AddListener(delegate
		{
			Manager.Config.HData.HInitCamera = !Manager.Config.HData.HInitCamera;
		});
		shortcutkey.procList.Add(proc);
		proc = new ShortcutKey.Proc
		{
			keyCode = KeyCode.D,
			enabled = true
		};
		proc.call.AddListener(delegate
		{
			sprite.OnFemaleGaugeLockOnGauge();
		});
		shortcutkey.procList.Add(proc);
		proc = new ShortcutKey.Proc
		{
			keyCode = KeyCode.F,
			enabled = true
		};
		proc.call.AddListener(delegate
		{
			sprite.OnMaleGaugeLockOnGauge();
		});
		shortcutkey.procList.Add(proc);
		flags.shortcut = shortcutkey;
	}

	private bool ShortCutCheck()
	{
		return (!sprite.isFade && sprite.GetFadeKindProc() != HSprite.FadeKindProc.OutEnd) & !Scene.IsFadeNow & !Scene.IsOverlap;
	}

	private void LoadAddTaii()
	{
		StringBuilder stringBuilder = new StringBuilder();
		List<string> list = new List<string>();
		foreach (string item in HFlag.lstABName)
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(item);
			if (!list.Contains(fileNameWithoutExtension))
			{
				list.Add(fileNameWithoutExtension);
			}
		}
		new Dictionary<string, Dictionary<int, List<int>>>();
		foreach (string item2 in HFlag.lstABName)
		{
			foreach (string item3 in list)
			{
				stringBuilder.Clear();
				stringBuilder.Append("AddTaii_ab").Append(item3);
				if (AssetBundleCheck.IsFile(item2, stringBuilder.ToString()) && (AssetBundleCheck.IsSimulation || HFlag.BundleCheck(item2, stringBuilder.ToString())))
				{
					AddTaiiData addTaiiData = CommonLib.LoadAsset<AddTaiiData>(item2, stringBuilder.ToString(), clone: false, "", check: false);
					AssetBundleManager.UnloadAssetBundle(item2, isUnloadForceRefCount: true);
					if (!(addTaiiData == null))
					{
						LoadAddTaii(addTaiiData.param);
					}
				}
			}
		}
	}

	private void LoadAddTaii(List<AddTaiiData.Param> param)
	{
		foreach (AddTaiiData.Param item in param)
		{
			int hExp = item.hExp;
			foreach (AddTaiiData.Info item2 in item.info)
			{
				if (!dicExpAddTaii.ContainsKey(item2.taii))
				{
					dicExpAddTaii.Add(item2.taii, new Dictionary<int, int>());
				}
				foreach (int id in item2.Ids)
				{
					if (!dicExpAddTaii[item2.taii].ContainsKey(id))
					{
						dicExpAddTaii[item2.taii].Add(id, hExp);
					}
					else
					{
						dicExpAddTaii[item2.taii][id] = hExp;
					}
				}
			}
		}
	}

	private void GetCloseCategory()
	{
		useCategorys.Clear();
		useCategorys.AddRange(categorys);
		StringBuilder stringBuilder = new StringBuilder("HPoint_");
		if (categorys.Any((int c) => c >= 1010 && c < 1100) || categorys.Any((int c) => c >= 1100 && c < 1200))
		{
			stringBuilder.Append("Add_");
		}
		else if (categorys.Any((int c) => c >= 3000 && c < 4000))
		{
			stringBuilder.Append("3P_");
		}
		List<GameObject> list = GlobalMethod.LoadAllFolder<GameObject>("h/common/", stringBuilder.ToString() + map.no);
		if (list == null || list.Count == 0)
		{
			return;
		}
		HPointData[] componentsInChildren = list[list.Count - 1].GetComponentsInChildren<HPointData>(includeInactive: true);
		HPointOmitObject component = list[list.Count - 1].GetComponent<HPointOmitObject>();
		bool flag = lstInitCategory.Any((int c) => c == 12 || c >= 1000);
		bool flag2 = lstInitCategory.Any((int c) => c >= 3000 && c < 4000);
		float num = flags.HpointSearch * flags.HpointSearch;
		HPointData[] array = componentsInChildren;
		for (int num2 = 0; num2 < array.Length; num2++)
		{
			array[num2].BackUpPosition();
		}
		foreach (HPointData hPointData in componentsInChildren)
		{
			if (component.list.Contains(hPointData.gameObject))
			{
				continue;
			}
			if (flags.isFreeH)
			{
				if (!hPointData.category.Any((int c) => MathfEx.IsRange(2000, c, 2999, isEqual: true)) && IsCategoryInAnimationList(hPointData.category) && IsExperience(hPointData.Experience))
				{
					SetCategory(hPointData);
					if (nowHpointData == null || !(hPointData.name == nowHpointData))
					{
						closeHpointData.Add(hPointData);
					}
				}
			}
			else if (flag)
			{
				if (!((!flag2) ? hPointData.category.Any((int c) => (c == 12 || c >= 1000) && lstInitCategory.Contains(c)) : hPointData.category.Any((int c) => c >= 3000 && c < 4000)) || !IsExperience(hPointData.Experience))
				{
					continue;
				}
				float sqrMagnitude = (hPointData.transform.position - HpointJudgePos).sqrMagnitude;
				if (!flags.HpointSearchLimit || !(sqrMagnitude > num))
				{
					SetCategory(hPointData, _isSpecial: true);
					if (nowHpointData == null || !(hPointData.name == nowHpointData))
					{
						closeHpointData.Add(hPointData);
					}
				}
			}
			else
			{
				if (hPointData.category.Any((int c) => c == 12 || c >= 1000) || !IsExperience(hPointData.Experience))
				{
					continue;
				}
				float sqrMagnitude = (hPointData.transform.position - HpointJudgePos).sqrMagnitude;
				if (!flags.HpointSearchLimit || !(sqrMagnitude > num))
				{
					SetCategory(hPointData);
					if (nowHpointData == null || !(hPointData.name == nowHpointData))
					{
						closeHpointData.Add(hPointData);
					}
				}
			}
		}
		if (useInitPosPoint)
		{
			closeInitPoint = false;
		}
	}

	private bool IsCategoryInAnimationList(int[] _categorys)
	{
		if (categorys.Any((int c) => c >= 1010 && c < 1100))
		{
			if (_categorys.Any((int c) => MathfEx.IsRange(1010, c, 1099, isEqual: true)))
			{
				return true;
			}
			return false;
		}
		if (categorys.Any((int c) => c >= 1100 && c < 1200))
		{
			if (_categorys.Any((int c) => MathfEx.IsRange(1100, c, 1199, isEqual: true)))
			{
				return true;
			}
			return false;
		}
		return true;
	}

	private bool IsCategoryInAnimationList(int _category)
	{
		if (categorys.Any((int c) => c >= 1010 && c < 1100))
		{
			if (MathfEx.IsRange(1010, _category, 1099, isEqual: true))
			{
				return true;
			}
			return false;
		}
		if (categorys.Any((int c) => c >= 1100 && c < 1200))
		{
			if (MathfEx.IsRange(1100, _category, 1199, isEqual: true))
			{
				return true;
			}
			return false;
		}
		return true;
	}

	private bool IsExperience(int _experience)
	{
		Heroine.HExperienceKind hExperience = flags.lstHeroine[0].HExperience;
		switch (_experience)
		{
		case 0:
			return true;
		case 1:
			if (hExperience > Heroine.HExperienceKind.不慣れ)
			{
				return true;
			}
			break;
		}
		return false;
	}

	private void SetCategory(HPointData _data, bool _isSpecial = false)
	{
		bool flag = lstInitCategory.Any((int c) => c >= 3000 && c < 4000);
		for (int num = 0; num < _data.category.Length; num++)
		{
			int num2 = _data.category[num];
			if (flags.isFreeH && !IsCategoryInAnimationList(num2))
			{
				continue;
			}
			if (_isSpecial)
			{
				if (num2 < 1000 && num2 != 12)
				{
					continue;
				}
				bool num3;
				if (flag)
				{
					if (num2 < 3000)
					{
						continue;
					}
					num3 = num2 >= 4000;
				}
				else
				{
					num3 = !lstInitCategory.Contains(num2);
				}
				if (num3)
				{
					continue;
				}
			}
			if (!useCategorys.Contains(num2))
			{
				useCategorys.Add(num2);
			}
		}
	}

	private void SetCategory(List<int> _category, bool _isSpecial = false)
	{
		bool flag = lstInitCategory.Any((int c) => c >= 3000 && c < 4000);
		for (int num = 0; num < _category.Count; num++)
		{
			int num2 = _category[num];
			if (flags.isFreeH && !IsCategoryInAnimationList(num2))
			{
				continue;
			}
			if (_isSpecial)
			{
				if (num2 < 1000 && num2 != 12)
				{
					continue;
				}
				bool num3;
				if (flag)
				{
					if (num2 < 3000)
					{
						continue;
					}
					num3 = num2 >= 4000;
				}
				else
				{
					num3 = !lstInitCategory.Contains(num2);
				}
				if (num3)
				{
					continue;
				}
			}
			if (!useCategorys.Contains(num2))
			{
				useCategorys.Add(num2);
			}
		}
	}

	private bool CategoryCheck()
	{
		foreach (Category item in flags.selectAnimationListInfo.lstCategory)
		{
			if (categorys.Contains(item.category))
			{
				return true;
			}
		}
		return false;
	}

	private void MovePointByChangeAnim()
	{
		HPointData hPointData = null;
		float num = float.PositiveInfinity;
		if (closeHpointData.Count > 0)
		{
			for (int i = 0; i < closeHpointData.Count; i++)
			{
				if (CloseCategory(closeHpointData[i], flags.selectAnimationListInfo.lstCategory))
				{
					float sqrMagnitude = (closeHpointData[i].transform.position - HpointJudgePos).sqrMagnitude;
					if (!(hPointData != null) || !(num <= sqrMagnitude))
					{
						hPointData = closeHpointData[i];
						num = sqrMagnitude;
					}
				}
			}
			if (closeInitPoint)
			{
				if (CloseCategory(lstInitCategory, flags.selectAnimationListInfo.lstCategory))
				{
					float sqrMagnitude2 = (initPos - HpointJudgePos).sqrMagnitude;
					if (num >= sqrMagnitude2)
					{
						nowInitPoint = true;
						hPointData = null;
					}
					else
					{
						nowInitPoint = false;
					}
				}
				else
				{
					nowInitPoint = false;
				}
			}
		}
		else
		{
			nowInitPoint = true;
			hPointData = null;
		}
		if (hPointData != null)
		{
			hPointData.MoveOffset();
			for (int j = 0; j < lstFemale.Count; j++)
			{
				lstFemale[j].transform.SetPositionAndRotation(hPointData.transform.position, hPointData.transform.rotation);
			}
			male.transform.SetPositionAndRotation(hPointData.transform.position, hPointData.transform.rotation);
			if ((bool)male1)
			{
				male1.transform.SetPositionAndRotation(hPointData.transform.position, hPointData.transform.rotation);
			}
			hPointData.ResetPosition();
			hPointData.GetKindObject(map.mapObjectGroup);
			GetMapObject(hPointData);
			hPointData.BackupObjectOffset();
			offSetData.Clear();
			hPointData.GetOffsetData(ref offSetData);
			categorys.Clear();
			categorys.AddRange(hPointData.category);
			HpointJudgePos = hPointData.transform.position;
		}
		else if (nowInitPoint)
		{
			for (int k = 0; k < lstFemale.Count; k++)
			{
				lstFemale[k].transform.SetPositionAndRotation(initPos, initRot);
			}
			male.transform.SetPositionAndRotation(initPos, initRot);
			if ((bool)male1)
			{
				male1.transform.SetPositionAndRotation(initPos, initRot);
			}
			UndoMapObject();
			mapObjectNull.lstChair.Clear();
			mapObjectNull.lstDesk.Clear();
			mapObjectNull.lstOther.Clear();
			categorys.Clear();
			categorys.AddRange(lstInitCategory);
			HpointJudgePos = initPos;
		}
		bChangePoint = true;
		nowHpointDataPos = lstFemale[0].transform.position;
		if ((bool)objMoveAxis)
		{
			objMoveAxis.transform.SetPositionAndRotation(lstFemale[0].transform.position, lstFemale[0].transform.rotation);
		}
		flags.mode = HFlag.EMode.none;
		sprite.ChangetaiiMovePoint(categorys);
	}

	private bool CloseCategory(HPointData _data, List<Category> select)
	{
		int[] category = _data.category;
		foreach (int num in category)
		{
			foreach (Category item in select)
			{
				if (item.category == num)
				{
					return true;
				}
			}
		}
		return false;
	}

	private bool CloseCategory(HPointData _data, List<int> select)
	{
		int[] category = _data.category;
		foreach (int num in category)
		{
			foreach (int item in select)
			{
				if (item == num)
				{
					return true;
				}
			}
		}
		return false;
	}

	private bool CloseCategory(List<int> _category, List<Category> select)
	{
		foreach (int item in _category)
		{
			foreach (Category item2 in select)
			{
				if (item2.category == item)
				{
					return true;
				}
			}
		}
		return false;
	}

	private void LoadResultExp()
	{
		dicHexpBase.Clear();
		dicHexpBonus.Clear();
		string text = "ResultExp";
		foreach (string item in HFlag.lstABName)
		{
			if (!AssetBundleCheck.IsFile(item, text) || (!AssetBundleCheck.IsSimulation && !HFlag.BundleCheck(item, text)))
			{
				continue;
			}
			ResultExpData resultExpData = CommonLib.LoadAsset<ResultExpData>(item, text, clone: false, "", check: false);
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: true);
			if (resultExpData == null)
			{
				continue;
			}
			foreach (ResultExpData.Param item2 in resultExpData.param)
			{
				if (item2.kind == 0)
				{
					if (!dicHexpBase.ContainsKey(item2.id))
					{
						dicHexpBase.Add(item2.id, new HExpBase
						{
							common = item2.common,
							bitch = item2.bitch
						});
					}
					else
					{
						dicHexpBase[item2.id] = new HExpBase
						{
							common = item2.common,
							bitch = item2.bitch
						};
					}
				}
				else if (!dicHexpBonus.ContainsKey(item2.id))
				{
					dicHexpBonus.Add(item2.id, new HExpBonus
					{
						min = item2.BonusMin,
						max = item2.BonusMax
					});
				}
				else
				{
					dicHexpBonus[item2.id] = new HExpBonus
					{
						min = item2.BonusMin,
						max = item2.BonusMax
					};
				}
			}
		}
	}

	private void LoadShopAddTaii()
	{
		dicShopAddTaii.Clear();
		foreach (string item in HFlag.lstABName)
		{
			if (!AssetBundleCheck.IsFile(item, "shopaddtaii") || (!AssetBundleCheck.IsSimulation && !HFlag.BundleCheck(item, "shopaddtaii")))
			{
				continue;
			}
			ShopAddData shopAddData = CommonLib.LoadAsset<ShopAddData>(item, "shopaddtaii", clone: false, "", check: false);
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: true);
			if (shopAddData == null)
			{
				continue;
			}
			foreach (ShopAddData.Param item2 in shopAddData.param)
			{
				if (!dicShopAddTaii.ContainsKey(item2.shopID))
				{
					dicShopAddTaii.Add(item2.shopID, new Dictionary<int, List<int>>());
				}
				foreach (ShopAddData.Info item3 in item2.info)
				{
					if (!dicShopAddTaii[item2.shopID].ContainsKey(item3.taii))
					{
						dicShopAddTaii[item2.shopID].Add(item3.taii, new List<int>(item3.Ids));
					}
					else
					{
						dicShopAddTaii[item2.shopID][item3.taii] = new List<int>(item3.Ids);
					}
				}
			}
		}
	}

	private bool CheckShopAdd(HashSet<int> buyList, int mode, int id)
	{
		foreach (KeyValuePair<int, Dictionary<int, List<int>>> item in dicShopAddTaii)
		{
			if (item.Value.ContainsKey(mode) && item.Value[mode].Contains(id))
			{
				return buyList.Contains(item.Key);
			}
		}
		return true;
	}

	private void GomuRigidAwake()
	{
		List<GameObject> list = new List<GameObject>();
		if (lstFemale[0] != null)
		{
			if (lstFemale[0].objHitBody != null)
			{
				list.Add(lstFemale[0].objHitBody);
			}
			if (lstFemale[0].objHitHead != null)
			{
				list.Add(lstFemale[0].objHitHead);
			}
		}
		if (lstFemale.Count > 1 && lstFemale[1] != null)
		{
			if (lstFemale[1].objHitBody != null)
			{
				list.Add(lstFemale[1].objHitBody);
			}
			if (lstFemale[1].objHitHead != null)
			{
				list.Add(lstFemale[1].objHitHead);
			}
		}
		if (male != null)
		{
			if (male.objHitBody != null)
			{
				list.Add(male.objHitBody);
			}
			if (male.objHitHead != null)
			{
				list.Add(male.objHitHead);
			}
		}
		List<ParentObjectCtrl> list2 = new List<ParentObjectCtrl>();
		if (parentObjectFemale != null)
		{
			list2.Add(parentObjectFemale);
		}
		if (parentObjectFemale1 != null)
		{
			list2.Add(parentObjectFemale1);
		}
		if (parentObjectMale != null)
		{
			list2.Add(parentObjectMale);
		}
		ctrlObi.GomuAwake(list, list2, item);
	}

	private void LoadEndADVPos()
	{
		string text = "endADVPos";
		string text2 = "chaPos";
		ActionScene instance = SingletonInitializer<ActionScene>.instance;
		if (!(instance != null) || instance.Cycle.withHeroine == null || (instance.Cycle.withHeroineSituation != 0 && instance.Cycle.withHeroineSituation != 1))
		{
			return;
		}
		EndADVPosData endADVPosData = null;
		foreach (string item in HFlag.lstABName)
		{
			if (AssetBundleCheck.IsFile(item, text) && (AssetBundleCheck.IsSimulation || HFlag.BundleCheck(item, text)))
			{
				endADVPosData = CommonLib.LoadAsset<EndADVPosData>(item, text, clone: false, "", check: false);
				AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: true);
			}
		}
		if (endADVPosData == null)
		{
			return;
		}
		for (int i = 0; i < endADVPosData.param.Count; i++)
		{
			if (endADVPosData.param[i].mapNo != map.no)
			{
				continue;
			}
			GameObject gameObject = CommonLib.LoadAsset<GameObject>(endADVPosData.param[i].bundle, endADVPosData.param[i].eventCG);
			AssetBundleManager.UnloadAssetBundle(endADVPosData.param[i].bundle, isUnloadForceRefCount: true);
			if (gameObject == null)
			{
				continue;
			}
			List<Transform> children = gameObject.transform.Children();
			int c;
			for (c = 0; c < children.Count; c++)
			{
				if (!(children[c].name != text2))
				{
					flags.lstHeroine.ForEach(delegate(Heroine heroine)
					{
						heroine.chaCtrl.transform.SetPositionAndRotation(children[c].position, children[c].rotation);
					});
				}
			}
		}
	}

	private void MapChangeObject()
	{
		switch (map.no)
		{
		case 9:
			if (!(map.mapVisibleState != null))
			{
				break;
			}
			if (!flags.isFreeH)
			{
				if (ActionMap.IsSpecialSecretBeach())
				{
					map.mapVisibleState.Set(1);
				}
				else
				{
					map.mapVisibleState.Set(0);
				}
			}
			else if (nFreeSecret == 4)
			{
				map.mapVisibleState.Set(1);
			}
			else
			{
				map.mapVisibleState.Set(0);
			}
			break;
		case 8:
		{
			bool gateOpen2 = Game.Player.isSecretBeachOpen;
			if (flags.isFreeH)
			{
				gateOpen2 = true;
				map.mapObjectGroup.Find("gate_close").SafeProcObject(delegate(Transform x)
				{
					x.gameObject.SetActive(!gateOpen2);
				});
				map.mapObjectGroup.Find("gate_open").SafeProcObject(delegate(Transform x)
				{
					x.gameObject.SetActive(gateOpen2);
				});
			}
			Collider[] componentsInChildren = map.mapObjectGroup.GetComponentsInChildren<Collider>();
			foreach (Collider collider in componentsInChildren)
			{
				int num2 = ((!(collider.name == "gate_close_col")) ? ((collider.name == "gate_open_col") ? 1 : 2) : 0);
				if (num2 != 2)
				{
					collider.gameObject.SetActive((num2 == 0) ? (!gateOpen2) : gateOpen2);
				}
			}
			break;
		}
		case 11:
		{
			bool gateOpen = Game.Player.isSuiteRoomOpen;
			if (flags.isFreeH)
			{
				gateOpen = true;
				map.mapObjectGroup.Find("move_partion00").SafeProcObject(delegate(Transform x)
				{
					x.gameObject.SetActive(!gateOpen);
				});
			}
			break;
		}
		case 36:
			if (categorys.Contains(2001) || categorys.Contains(2004))
			{
				SuiteShower componentInChildren = map.mapRoot.GetComponentInChildren<SuiteShower>();
				if (componentInChildren != null)
				{
					componentInChildren.StartPeep();
				}
			}
			break;
		}
	}

	private bool CheckExpAddTaii(int mode, int id, float exp)
	{
		if (!dicExpAddTaii.ContainsKey(mode))
		{
			return true;
		}
		if (!dicExpAddTaii[mode].ContainsKey(id))
		{
			return true;
		}
		return exp >= (float)dicExpAddTaii[mode][id];
	}

	private void InitOffset()
	{
		if (closeHpointData.Count <= 0)
		{
			return;
		}
		int num = -1;
		float num2 = float.PositiveInfinity;
		float num3 = 0.3f;
		for (int i = 0; i < closeHpointData.Count; i++)
		{
			if (CloseCategory(closeHpointData[i], lstInitCategory))
			{
				float sqrMagnitude = (closeHpointData[i].transform.position - HpointJudgePos).sqrMagnitude;
				if (!(sqrMagnitude > num3 * num3) && (num < 0 || !(num2 <= sqrMagnitude)))
				{
					num = i;
					num2 = sqrMagnitude;
				}
			}
		}
		if (num >= 0)
		{
			closeHpointData[num].GetKindObject(map.mapObjectGroup);
			closeHpointData[num].BackupObjectOffset();
			closeHpointData[num].GetOffsetData(ref offSetData);
		}
	}

	private bool isOnaniState(int state)
	{
		if (state != 10 && state >= 6)
		{
			return state <= 12;
		}
		return false;
	}

	private bool isOnani(Heroine female)
	{
		NPC nPC = female.charaBase as NPC;
		if (nPC != null)
		{
			return nPC.isOnanism;
		}
		return false;
	}

	public void SetBoneOffset(AnimationListInfo now, AnimationListInfo next)
	{
		int count = lstFemale.Count;
		int num = -1;
		for (int i = 0; i < count; i++)
		{
			if (!(lstFemale[i] == null) && lstFemale[i].sibBody.typeBone != 0)
			{
				num = i;
				break;
			}
		}
		if (num < 0)
		{
			if (male == null)
			{
				return;
			}
			if (male.sibBody.typeBone != 0)
			{
				num = count;
			}
			else
			{
				if (!(male1 != null) || male1.sibBody.typeBone == 0)
				{
					return;
				}
				num = count + 1;
			}
		}
		int num2 = BoneOffSetIDCheck(now.mode, now.id);
		int num3 = BoneOffSetIDCheck(next.mode, next.id);
		if (num2 == num3)
		{
			return;
		}
		if (num2 < 0)
		{
			if (num < count)
			{
				SetBoneOffset(lstFemale[num], backup: true);
			}
			else if (num == count)
			{
				SetBoneOffset(male, backup: true);
			}
			else
			{
				SetBoneOffset(male1, backup: true);
			}
		}
		for (int j = 0; j < count; j++)
		{
			if (!(lstFemale[j] == null) && lstFemale[j].sibBody.typeBone != 0)
			{
				SetBoneOffset(lstFemale[j], backup: false, num3);
			}
		}
		if (!(male == null))
		{
			if (male.sibBody.typeBone != 0)
			{
				SetBoneOffset(male, backup: false, num3);
			}
			if (!(male1 == null) && male1.sibBody.typeBone != 0)
			{
				SetBoneOffset(male1, backup: false, num3);
			}
		}
	}

	private int BoneOffSetIDCheck(HFlag.EMode mode, int id)
	{
		switch (mode)
		{
		case HFlag.EMode.aibu:
			if (id == 0)
			{
				return 5;
			}
			break;
		case HFlag.EMode.houshi:
			switch (id)
			{
			case 53:
			case 61:
			case 62:
			case 63:
			case 64:
				return 1;
			}
			break;
		case HFlag.EMode.sonyu:
			switch (id)
			{
			case 49:
			case 52:
				return 0;
			case 56:
				return 2;
			case 53:
				return 3;
			case 9:
				return 4;
			case 55:
				return 6;
			}
			break;
		case HFlag.EMode.masturbation:
			switch (id)
			{
			case 9:
				return 7;
			case 8:
				return 8;
			}
			break;
		case HFlag.EMode.houshi3P:
			if (id == 8 || id == 9)
			{
				return 9;
			}
			break;
		}
		return -1;
	}

	private void SetBoneOffset(ChaControl cha, bool backup = false, int setMode = -1)
	{
		if (!(cha.sibBody is ShapeBodyInfoFemale shapeBodyInfoFemale))
		{
			return;
		}
		ShapeInfoBase.BoneInfo boneInfo = null;
		if (backup)
		{
			BoneOffSetBackUP(cha);
			return;
		}
		SetBackUPBoneOffSet(cha);
		switch (setMode)
		{
		case 0:
			boneInfo = shapeBodyInfoFemale.correctValue[16];
			boneInfo.vctPos = Vector3.zero;
			boneInfo.vctRot = Vector3.zero;
			boneInfo.vctScl = Vector3.zero;
			boneInfo = shapeBodyInfoFemale.correctValue[17];
			boneInfo.vctPos = Vector3.zero;
			boneInfo.vctRot = Vector3.zero;
			boneInfo.vctScl = Vector3.zero;
			boneInfo = shapeBodyInfoFemale.correctValue[18];
			boneInfo.vctPos = Vector3.zero;
			boneInfo.vctRot = Vector3.zero;
			boneInfo.vctScl = Vector3.zero;
			boneInfo = shapeBodyInfoFemale.correctValue[19];
			boneInfo.vctPos = Vector3.zero;
			boneInfo.vctRot = Vector3.zero;
			boneInfo.vctScl = Vector3.zero;
			boneInfo = shapeBodyInfoFemale.correctValue[5];
			boneInfo.vctRot.x = 0f;
			return;
		case 1:
		case 2:
			boneInfo = shapeBodyInfoFemale.correctValue[5];
			boneInfo.vctRot.x = 0f;
			if (setMode == 2)
			{
				boneInfo.vctScl = Vector3.zero;
				boneInfo.vctPos.z = -0.005f;
				boneInfo = shapeBodyInfoFemale.correctValue[3];
				boneInfo.vctScl = Vector3.zero;
				boneInfo = shapeBodyInfoFemale.correctValue[4];
				boneInfo.vctScl = Vector3.zero;
				boneInfo = shapeBodyInfoFemale.correctValue[14];
				boneInfo.vctPos = Vector3.zero;
				boneInfo.vctScl = Vector3.zero;
				boneInfo = shapeBodyInfoFemale.correctValue[15];
				boneInfo.vctPos = Vector3.zero;
				boneInfo.vctScl = Vector3.zero;
			}
			return;
		case 3:
			boneInfo = shapeBodyInfoFemale.correctValue[6];
			boneInfo.vctPos = Vector3.zero;
			boneInfo.vctRot = Vector3.zero;
			boneInfo.vctScl = Vector3.zero;
			boneInfo = shapeBodyInfoFemale.correctValue[7];
			boneInfo.vctPos = Vector3.zero;
			boneInfo.vctRot = Vector3.zero;
			boneInfo.vctScl = Vector3.zero;
			boneInfo = shapeBodyInfoFemale.correctValue[8];
			boneInfo.vctPos = Vector3.zero;
			boneInfo.vctRot = Vector3.zero;
			boneInfo.vctScl = Vector3.zero;
			boneInfo = shapeBodyInfoFemale.correctValue[9];
			boneInfo.vctPos = Vector3.zero;
			boneInfo.vctRot = Vector3.zero;
			boneInfo.vctScl = Vector3.zero;
			boneInfo = shapeBodyInfoFemale.correctValue[5];
			boneInfo.vctRot.x = 0f;
			return;
		case 4:
			boneInfo = shapeBodyInfoFemale.correctValue[5];
			boneInfo.vctPos.z = -0.01f;
			boneInfo.vctRot.x = 0f;
			boneInfo.vctScl.z = 0f;
			boneInfo = shapeBodyInfoFemale.correctValue[14];
			boneInfo.vctPos.z = 0f;
			boneInfo = shapeBodyInfoFemale.correctValue[15];
			boneInfo.vctPos.z = 0f;
			return;
		case 5:
			boneInfo = shapeBodyInfoFemale.correctValue[4];
			boneInfo.vctScl.z = 0f;
			boneInfo = shapeBodyInfoFemale.correctValue[5];
			boneInfo.vctRot.x = 5f;
			boneInfo.vctScl.z = 0f;
			return;
		case 6:
			boneInfo = shapeBodyInfoFemale.correctValue[6];
			boneInfo.vctRot.x = 0f;
			boneInfo.vctRot.y = 0f;
			boneInfo = shapeBodyInfoFemale.correctValue[7];
			boneInfo.vctRot.x = 0f;
			boneInfo.vctRot.y = 0f;
			boneInfo = shapeBodyInfoFemale.correctValue[1];
			boneInfo.vctPos = Vector3.zero;
			boneInfo.vctScl = Vector3.zero;
			boneInfo = shapeBodyInfoFemale.correctValue[3];
			boneInfo.vctPos = Vector3.zero;
			boneInfo.vctRot = Vector3.zero;
			boneInfo.vctScl = Vector3.zero;
			boneInfo = shapeBodyInfoFemale.correctValue[4];
			boneInfo.vctPos = Vector3.zero;
			boneInfo.vctRot = Vector3.zero;
			boneInfo.vctScl = Vector3.zero;
			boneInfo = shapeBodyInfoFemale.correctValue[5];
			boneInfo.vctPos = Vector3.zero;
			boneInfo.vctRot = Vector3.zero;
			boneInfo.vctScl = Vector3.zero;
			boneInfo = shapeBodyInfoFemale.correctValue[14];
			boneInfo.vctPos = Vector3.zero;
			boneInfo.vctScl.z = 0f;
			boneInfo = shapeBodyInfoFemale.correctValue[15];
			boneInfo.vctPos = Vector3.zero;
			boneInfo.vctScl.z = 0f;
			boneInfo = shapeBodyInfoFemale.correctValue[16];
			boneInfo.vctPos = Vector3.zero;
			boneInfo.vctScl.y = 0f;
			boneInfo = shapeBodyInfoFemale.correctValue[17];
			boneInfo.vctPos = Vector3.zero;
			boneInfo.vctScl.y = 0f;
			return;
		case 7:
			boneInfo = shapeBodyInfoFemale.correctValue[5];
			boneInfo.vctRot.x = 0f;
			return;
		case 8:
			boneInfo = shapeBodyInfoFemale.correctValue[5];
			boneInfo.vctRot.x = 0f;
			break;
		}
		if (setMode == 9)
		{
			boneInfo = shapeBodyInfoFemale.correctValue[3];
			boneInfo.vctScl = Vector3.zero;
			boneInfo = shapeBodyInfoFemale.correctValue[4];
			boneInfo.vctScl = Vector3.zero;
			boneInfo = shapeBodyInfoFemale.correctValue[5];
			boneInfo.vctPos.z = -0.005f;
			boneInfo.vctRot.x = 0f;
			boneInfo.vctScl = Vector3.zero;
			boneInfo = shapeBodyInfoFemale.correctValue[1];
			boneInfo.vctPos = Vector3.zero;
			boneInfo.vctRot = Vector3.zero;
			boneInfo = shapeBodyInfoFemale.correctValue[2];
			boneInfo.vctPos = Vector3.zero;
			boneInfo.vctScl.y = 0f;
		}
	}

	private void BoneOffSetBackUP(ChaControl cha)
	{
		ShapeBodyInfoFemale obj = cha.sibBody as ShapeBodyInfoFemale;
		ShapeInfoBase.BoneInfo boneInfo = null;
		if (!backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm01_L))
		{
			backUPBoneOffset.Add(ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm01_L, new BoneOffSet());
		}
		if (!backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm01_R))
		{
			backUPBoneOffset.Add(ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm01_R, new BoneOffSet());
		}
		if (!backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm02_L))
		{
			backUPBoneOffset.Add(ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm02_L, new BoneOffSet());
		}
		if (!backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm02_R))
		{
			backUPBoneOffset.Add(ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm02_R, new BoneOffSet());
		}
		if (!backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_spine01))
		{
			backUPBoneOffset.Add(ShapeBodyInfoFemale.CorrectKeyName.cf_s_spine01, new BoneOffSet());
		}
		if (!backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_spine02))
		{
			backUPBoneOffset.Add(ShapeBodyInfoFemale.CorrectKeyName.cf_s_spine02, new BoneOffSet());
		}
		if (!backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_spine03))
		{
			backUPBoneOffset.Add(ShapeBodyInfoFemale.CorrectKeyName.cf_s_spine03, new BoneOffSet());
		}
		if (!backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh01_L))
		{
			backUPBoneOffset.Add(ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh01_L, new BoneOffSet());
		}
		if (!backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh01_R))
		{
			backUPBoneOffset.Add(ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh01_R, new BoneOffSet());
		}
		if (!backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh02_L))
		{
			backUPBoneOffset.Add(ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh02_L, new BoneOffSet());
		}
		if (!backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh02_R))
		{
			backUPBoneOffset.Add(ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh02_R, new BoneOffSet());
		}
		if (!backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_shoulder02_L))
		{
			backUPBoneOffset.Add(ShapeBodyInfoFemale.CorrectKeyName.cf_s_shoulder02_L, new BoneOffSet());
		}
		if (!backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_shoulder02_R))
		{
			backUPBoneOffset.Add(ShapeBodyInfoFemale.CorrectKeyName.cf_s_shoulder02_R, new BoneOffSet());
		}
		if (!backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_neck))
		{
			backUPBoneOffset.Add(ShapeBodyInfoFemale.CorrectKeyName.cf_s_neck, new BoneOffSet());
		}
		if (!backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_head))
		{
			backUPBoneOffset.Add(ShapeBodyInfoFemale.CorrectKeyName.cf_s_head, new BoneOffSet());
		}
		boneInfo = obj.correctValue[16];
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm01_L].pos = boneInfo.vctPos;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm01_L].rot = boneInfo.vctRot;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm01_L].scl = boneInfo.vctScl;
		boneInfo = obj.correctValue[17];
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm01_R].pos = boneInfo.vctPos;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm01_R].rot = boneInfo.vctRot;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm01_R].scl = boneInfo.vctScl;
		boneInfo = obj.correctValue[18];
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm02_L].pos = boneInfo.vctPos;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm02_L].rot = boneInfo.vctRot;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm02_L].scl = boneInfo.vctScl;
		boneInfo = obj.correctValue[19];
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm02_R].pos = boneInfo.vctPos;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm02_R].rot = boneInfo.vctRot;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm02_R].scl = boneInfo.vctScl;
		boneInfo = obj.correctValue[3];
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_spine01].pos = boneInfo.vctPos;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_spine01].rot = boneInfo.vctRot;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_spine01].scl = boneInfo.vctScl;
		boneInfo = obj.correctValue[4];
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_spine02].pos = boneInfo.vctPos;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_spine02].rot = boneInfo.vctRot;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_spine02].scl = boneInfo.vctScl;
		boneInfo = obj.correctValue[5];
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_spine03].scl = boneInfo.vctScl;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_spine03].pos = boneInfo.vctPos;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_spine03].rot = boneInfo.vctRot;
		boneInfo = obj.correctValue[6];
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh01_L].pos = boneInfo.vctPos;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh01_L].rot = boneInfo.vctRot;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh01_L].scl = boneInfo.vctScl;
		boneInfo = obj.correctValue[7];
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh01_R].pos = boneInfo.vctPos;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh01_R].rot = boneInfo.vctRot;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh01_R].scl = boneInfo.vctScl;
		boneInfo = obj.correctValue[8];
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh02_L].pos = boneInfo.vctPos;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh02_L].rot = boneInfo.vctRot;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh02_L].scl = boneInfo.vctScl;
		boneInfo = obj.correctValue[9];
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh02_R].pos = boneInfo.vctPos;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh02_R].rot = boneInfo.vctRot;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh02_R].scl = boneInfo.vctScl;
		boneInfo = obj.correctValue[14];
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_shoulder02_L].pos = boneInfo.vctPos;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_shoulder02_L].rot = boneInfo.vctRot;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_shoulder02_L].scl = boneInfo.vctScl;
		boneInfo = obj.correctValue[15];
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_shoulder02_R].pos = boneInfo.vctPos;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_shoulder02_R].rot = boneInfo.vctRot;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_shoulder02_R].scl = boneInfo.vctScl;
		boneInfo = obj.correctValue[1];
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_neck].pos = boneInfo.vctPos;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_neck].rot = boneInfo.vctRot;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_neck].scl = boneInfo.vctScl;
		boneInfo = obj.correctValue[2];
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_head].pos = boneInfo.vctPos;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_head].rot = boneInfo.vctRot;
		backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_head].scl = boneInfo.vctScl;
	}

	private void SetBackUPBoneOffSet(ChaControl cha)
	{
		ShapeBodyInfoFemale shapeBodyInfoFemale = cha.sibBody as ShapeBodyInfoFemale;
		if (backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm01_L))
		{
			ShapeInfoBase.BoneInfo obj = shapeBodyInfoFemale.correctValue[16];
			obj.vctPos = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm01_L].pos;
			obj.vctRot = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm01_L].rot;
			obj.vctScl = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm01_L].scl;
		}
		if (backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm01_R))
		{
			ShapeInfoBase.BoneInfo obj2 = shapeBodyInfoFemale.correctValue[17];
			obj2.vctPos = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm01_R].pos;
			obj2.vctRot = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm01_R].rot;
			obj2.vctScl = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm01_R].scl;
		}
		if (backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm02_L))
		{
			ShapeInfoBase.BoneInfo obj3 = shapeBodyInfoFemale.correctValue[18];
			obj3.vctPos = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm02_L].pos;
			obj3.vctRot = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm02_L].rot;
			obj3.vctScl = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm02_L].scl;
		}
		if (backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm02_R))
		{
			ShapeInfoBase.BoneInfo obj4 = shapeBodyInfoFemale.correctValue[19];
			obj4.vctPos = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm02_R].pos;
			obj4.vctRot = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm02_R].rot;
			obj4.vctScl = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_arm02_R].scl;
		}
		if (backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_spine03))
		{
			ShapeInfoBase.BoneInfo obj5 = shapeBodyInfoFemale.correctValue[5];
			obj5.vctScl = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_spine03].scl;
			obj5.vctPos = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_spine03].pos;
			obj5.vctRot = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_spine03].rot;
		}
		if (backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_spine01))
		{
			ShapeInfoBase.BoneInfo obj6 = shapeBodyInfoFemale.correctValue[3];
			obj6.vctPos = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_spine01].pos;
			obj6.vctRot = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_spine01].rot;
			obj6.vctScl = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_spine01].scl;
		}
		if (backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_spine02))
		{
			ShapeInfoBase.BoneInfo obj7 = shapeBodyInfoFemale.correctValue[4];
			obj7.vctPos = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_spine02].pos;
			obj7.vctRot = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_spine02].rot;
			obj7.vctScl = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_spine02].scl;
		}
		if (backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh01_L))
		{
			ShapeInfoBase.BoneInfo obj8 = shapeBodyInfoFemale.correctValue[6];
			obj8.vctPos = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh01_L].pos;
			obj8.vctRot = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh01_L].rot;
			obj8.vctScl = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh01_L].scl;
		}
		if (backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh01_R))
		{
			ShapeInfoBase.BoneInfo obj9 = shapeBodyInfoFemale.correctValue[7];
			obj9.vctPos = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh01_R].pos;
			obj9.vctRot = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh01_R].rot;
			obj9.vctScl = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh01_R].scl;
		}
		if (backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh01_L))
		{
			ShapeInfoBase.BoneInfo obj10 = shapeBodyInfoFemale.correctValue[8];
			obj10.vctPos = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh02_L].pos;
			obj10.vctRot = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh02_L].rot;
			obj10.vctScl = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh02_L].scl;
		}
		if (backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh02_R))
		{
			ShapeInfoBase.BoneInfo obj11 = shapeBodyInfoFemale.correctValue[9];
			obj11.vctPos = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh02_R].pos;
			obj11.vctRot = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh02_R].rot;
			obj11.vctScl = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_thigh02_R].scl;
		}
		if (backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_shoulder02_L))
		{
			ShapeInfoBase.BoneInfo obj12 = shapeBodyInfoFemale.correctValue[14];
			obj12.vctPos = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_shoulder02_L].pos;
			obj12.vctRot = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_shoulder02_L].rot;
			obj12.vctScl = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_shoulder02_L].scl;
		}
		if (backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_shoulder02_R))
		{
			ShapeInfoBase.BoneInfo obj13 = shapeBodyInfoFemale.correctValue[15];
			obj13.vctPos = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_shoulder02_R].pos;
			obj13.vctRot = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_shoulder02_R].rot;
			obj13.vctScl = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_shoulder02_R].scl;
		}
		if (backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_neck))
		{
			ShapeInfoBase.BoneInfo obj14 = shapeBodyInfoFemale.correctValue[1];
			obj14.vctPos = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_neck].pos;
			obj14.vctRot = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_neck].rot;
			obj14.vctScl = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_neck].scl;
		}
		if (backUPBoneOffset.ContainsKey(ShapeBodyInfoFemale.CorrectKeyName.cf_s_head))
		{
			ShapeInfoBase.BoneInfo obj15 = shapeBodyInfoFemale.correctValue[2];
			obj15.vctPos = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_head].pos;
			obj15.vctRot = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_head].rot;
			obj15.vctScl = backUPBoneOffset[ShapeBodyInfoFemale.CorrectKeyName.cf_s_head].scl;
		}
	}
}
