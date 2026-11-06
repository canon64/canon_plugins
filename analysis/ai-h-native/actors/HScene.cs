using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using ADV;
using AIChara;
using AIProject;
using AIProject.Animal;
using AIProject.Definitions;
using AIProject.SaveData;
using AIProject.Scene;
using ConfigScene;
using Correct;
using Illusion;
using Manager;
using UniRx;
using UnityEngine;
using UnityEx;

public class HScene : BaseLoader
{
	[Serializable]
	public class LightInfo
	{
		[Tooltip("回すキャラライトオブジェクト")]
		public GameObject objCharaLight;

		[Tooltip("キャラライト")]
		public Light light;

		[Tooltip("初期のライトの回転")]
		public Quaternion initRot = Quaternion.identity;

		[Tooltip("初期の強さ")]
		[Space]
		public float initIntensity = 1f;

		[Tooltip("強さの最小値")]
		[Range(0f, 2f)]
		public float minIntensity;

		[Tooltip("強さの最大値")]
		[Range(0f, 2f)]
		public float maxIntensity = 2f;

		[Tooltip("初期の色")]
		public Color initColor = Color.white;
	}

	[Serializable]
	public class AnimationListInfo
	{
		public int id = -1;

		public string nameAnimation = string.Empty;

		public string assetpathBaseM = string.Empty;

		public string assetBaseM = string.Empty;

		public string assetpathMale = string.Empty;

		public string fileMale = string.Empty;

		public bool isMaleHitObject;

		public string fileMotionNeckMale;

		public string assetpathBaseF = string.Empty;

		public string assetBaseF = string.Empty;

		public string assetpathFemale;

		public string fileFemale;

		public bool isFemaleHitObject;

		public string fileMotionNeckFemale;

		public string fileMotionNeckFemalePlayer;

		public string assetpathBaseF2 = string.Empty;

		public string assetBaseF2 = string.Empty;

		public string assetpathFemale2 = string.Empty;

		public string fileFemale2 = string.Empty;

		public bool isFemaleHitObject2;

		public string fileMotionNeckFemale2;

		public UnityEx.ValueTuple<int, int> ActionCtrl = new UnityEx.ValueTuple<int, int>(-1, -1);

		public List<int> nPositons = new List<int>();

		public List<string> lstOffset = new List<string>();

		public bool isNeedItem;

		public int nDownPtn;

		public int nFaintnessLimit;

		public int nIyaAction = 1;

		public int nPhase;

		public int nHentai;

		public bool bSleep;

		public int nInitiativeFemale;

		public int nFemaleProclivity = -1;

		public int nBackInitiativeID = -1;

		public List<int> lstSystem = new List<int>();

		public int nMaleSon = -1;

		public int[] nFemaleUpperCloths = new int[2] { -1, -1 };

		public int[] nFemaleLowerCloths = new int[2] { -1, -1 };

		public int nFeelHit;

		public string nameCamera;

		public string fileSiruPaste;

		public string fileSiruPasteSecond = string.Empty;

		public string fileSe;

		public int nShortBreahtPlay = 1;

		public HashSet<int> hasVoiceCategory = new HashSet<int>();

		public int nPromiscuity = -1;

		public int nAnimListInfoID = -1;

		public int nBreathID = -1;

		public bool bMerchantMotion;
	}

	public class StartMotion
	{
		public int mode;

		public int id;

		public StartMotion(int _mode, int _id)
		{
			mode = _mode;
			id = _id;
		}
	}

	public class EndMotion
	{
		public int height;

		public string[] abNameP = new string[2];

		public string[] abNameH = new string[2];

		public string abNameM = string.Empty;

		public string[] assetNameP = new string[2];

		public string[] assetNameH = new string[2];

		public string assetNameM = string.Empty;

		public string[] stateNameP = new string[2];

		public string[] stateNameH = new string[2];

		public string stateNameM = string.Empty;

		public EndMotion(int _height, string[] _abName, string[] _assetName, string[] _stateName)
		{
			height = _height;
			for (int i = 0; i < _abName.Length; i++)
			{
				int num = i;
				if (num < 2)
				{
					abNameP[num] = _abName[num];
					assetNameP[num] = _assetName[num];
					stateNameP[num] = _stateName[num];
				}
				else if (num < 4)
				{
					abNameH[num - 2] = _abName[num];
					assetNameH[num - 2] = _assetName[num];
					stateNameH[num - 2] = _stateName[num];
				}
				else
				{
					abNameM = _abName[num];
					assetNameM = _assetName[num];
					stateNameM = _stateName[num];
				}
			}
		}
	}

	[Serializable]
	public struct DeskChairInfo
	{
		public int eventID;

		public int poseID;
	}

	private class PackData : CharaPackData
	{
		public int charaID { get; }

		public int adv_category { get; }

		public string JumpTag { get; set; } = string.Empty;


		public bool isWeaknessH { get; set; }

		public int numOrgasmFemale { get; set; }

		public int ConditionMode { get; set; }

		public bool isPainAction { get; set; }

		public bool isConstraintAction { get; set; }

		public PackData(int charaID, int adv_category)
		{
			this.charaID = charaID;
			this.adv_category = adv_category;
		}

		public override List<Program.Transfer> Create()
		{
			List<Program.Transfer> list = base.Create();
			list.Add(Program.Transfer.Create(true, Command.VAR, "bool", "isWeaknessH", $"{isWeaknessH}"));
			list.Add(Program.Transfer.Create(true, Command.VAR, "string", "JumpTag", JumpTag));
			list.Add(Program.Transfer.Create(true, Command.VAR, "int", "numOrgasmFemale", $"{numOrgasmFemale}"));
			list.Add(Program.Transfer.Create(true, Command.VAR, "int", "ConditionMode", $"{ConditionMode}"));
			list.Add(Program.Transfer.Create(true, Command.VAR, "bool", "isPainAction", $"{isPainAction}"));
			list.Add(Program.Transfer.Create(true, Command.VAR, "bool", "isConstraintAction", $"{isConstraintAction}"));
			return list;
		}

		public override void Receive(TextScenario scenario)
		{
			base.Receive(scenario);
		}
	}

	public HSceneFlagCtrl ctrlFlag;

	public GameObject objMetaBallBase;

	public GameObject objGrondInstantiate;

	public H_Lookat_dan[] ctrlLookAts;

	public YureCtrl[] ctrlYures;

	public YureCtrlMale ctrlYureMale;

	public HLayerCtrl ctrlLayer;

	public HAutoCtrl ctrlAuto;

	public CollisionCtrl[] ctrlMaleCollisionCtrls;

	public CollisionCtrl[] ctrlFemaleCollisionCtrls;

	public HVoiceCtrl ctrlVoice;

	public HMotionEyeNeckFemale[] ctrlEyeNeckFemale;

	public HMotionEyeNeckLesPlayer hMotionEyeNeckLesP;

	public HMotionEyeNeckMale[] ctrlEyeNeckMale;

	public SiruPasteCtrl[] ctrlSiruPastes;

	public ParticleSystem AtariEffect;

	public ParticleSystem FeelHitEffect3D;

	public Vector3 FeelHitEffect3DOffSet;

	public HPointCtrl hPointCtrl;

	private HParticleCtrl ctrlParitcle;

	public HitObjectCtrl[] ctrlHitObjectFemales = new HitObjectCtrl[2];

	public HitObjectCtrl[] ctrlHitObjectMales = new HitObjectCtrl[2];

	public LightInfo[] infoLight = new LightInfo[2]
	{
		new LightInfo(),
		new LightInfo()
	};

	private HSceneSprite sprite;

	[SerializeField]
	private CrossFade fade;

	private ChaControl[] chaFemales = new ChaControl[2];

	private ChaControl[] chaMales = new ChaControl[2];

	private Transform[] chaFemalesTrans = new Transform[2];

	private Transform[] chaMalesTrans = new Transform[2];

	private List<System.Tuple<int, int, MotionIK>> lstMotionIK = new List<System.Tuple<int, int, MotionIK>>();

	private int mode = -1;

	private int modeCtrl = -1;

	private List<ProcBase> lstProc = new List<ProcBase>();

	private MetaballCtrl ctrlMeta;

	private HItemCtrl ctrlItem;

	private FeelHit ctrlFeelHit;

	private bool isSyncFirstStep;

	private DynamicBoneReferenceCtrl[] ctrlDynamics = new DynamicBoneReferenceCtrl[2];

	private HSeCtrl ctrlSE;

	private GameObject objGrondCollision;

	private bool isTuyaOn;

	private List<string> abName = new List<string>();

	public bool chaFemaleLoaded;

	private List<AnimationListInfo>[] lstAnimInfo = new List<AnimationListInfo>[6]
	{
		new List<AnimationListInfo>(),
		new List<AnimationListInfo>(),
		new List<AnimationListInfo>(),
		new List<AnimationListInfo>(),
		new List<AnimationListInfo>(),
		new List<AnimationListInfo>()
	};

	private AnimationListInfo aInfo = new AnimationListInfo();

	public AnimationListInfo StartAnimInfo = new AnimationListInfo();

	private RuntimeAnimatorController[,] runtimeAnimatorControllers = new RuntimeAnimatorController[2, 3];

	public Transform StartPos;

	private bool isSetStartPos;

	private StartMotion autoMotion;

	private HSceneManager hSceneManager;

	private ActorCameraControl Camera;

	private bool nowStart;

	private bool nullPlayer = true;

	private StringBuilder sbLoadFileName = new StringBuilder();

	public bool NowStateIsEnd;

	private bool nowChangeAnim;

	private Vector3 distanceToContoroler;

	private HSceneSpriteHitem HItemDrag;

	public Transform hitemPlace;

	public Transform hParticlePlace;

	public Transform hitobjPlace;

	private Dictionary<int, float> initStandNip = new Dictionary<int, float>();

	[Tooltip("Hメッシュの判定を机にするかの基準")]
	public List<DeskChairInfo> deskChairInfos = new List<DeskChairInfo>();

	private RuntimeAnimatorController[] racM;

	private RuntimeAnimatorController[] racF;

	private RuntimeAnimatorController[] HoushiRacM;

	private RuntimeAnimatorController[] HoushiRacF;

	private Dictionary<string, RuntimeAnimatorController> racEtcM = new Dictionary<string, RuntimeAnimatorController>();

	private Dictionary<string, RuntimeAnimatorController> racEtcF = new Dictionary<string, RuntimeAnimatorController>();

	private HashSet<string> hashUseAssetBundleAnimator = new HashSet<string>();

	private bool prevBeforeWait = true;

	private bool _bareYobai;

	private bool[] prevCharaEntry;

	private bool useLotion;

	private PackData packData;

	private RuntimeAnimatorController racBeforeWait;

	private StartWaitAnim startWait;

	private Dictionary<int, int> preBeforWaitState;

	public HParticleCtrl CtrlParticle
	{
		get
		{
			if (ctrlParitcle == null)
			{
				ctrlParitcle = ((!Singleton<Manager.Resources>.IsInstance()) ? null : Singleton<Manager.Resources>.Instance.HSceneTable.hParticle);
			}
			return ctrlParitcle;
		}
	}

	public bool NowChangeAnim
	{
		[CompilerGenerated]
		get
		{
			return nowChangeAnim;
		}
	}

	private OpenData openData { get; } = new OpenData();


	public IEnumerator InitCoroutine()
	{
		useLotion = false;
		prevCharaEntry = new bool[Config.GraphicData.CharasEntry.Length];
		for (int k = 0; k < prevCharaEntry.Length; k++)
		{
			prevCharaEntry[k] = Config.GraphicData.CharasEntry[k];
		}
		nowStart = true;
		hSceneManager = Singleton<HSceneManager>.Instance;
		ctrlFlag.bFutanari = hSceneManager.bFutanari;
		ctrlFlag.semenType = Config.HData.Siru;
		racM = new RuntimeAnimatorController[2];
		HoushiRacM = new RuntimeAnimatorController[2];
		racF = new RuntimeAnimatorController[2];
		HoushiRacF = new RuntimeAnimatorController[2];
		racEtcM.Clear();
		racEtcF.Clear();
		fade = hSceneManager.Player.CameraControl.CrossFade;
		ctrlFlag.cameraCtrl.Init();
		ctrlFlag.cameraCtrl.loadVanish();
		HousingVanishSet();
		int nipIndex = ChaFileDefine.cf_bodyshapename.Length - 1;
		chaFemales = new ChaControl[2];
		chaMales = new ChaControl[2];
		chaFemalesTrans = new Transform[2];
		chaMalesTrans = new Transform[2];
		initStandNip.Clear();
		GameObject objCommon = GameObject.Find("CommonSpace");
		if (hSceneManager.females[0] == null)
		{
			ChaFileControl chaFileControl = new ChaFileControl();
			if (!hSceneManager.pngFemales[0].IsNullOrEmpty())
			{
				if (!chaFileControl.LoadCharaFile(hSceneManager.pngFemales[0], byte.MaxValue, noLoadPng: true))
				{
					chaFileControl = null;
				}
			}
			else
			{
				chaFileControl = null;
			}
			chaFemales[0] = Singleton<Character>.Instance.CreateChara(1, objCommon, 0, chaFileControl);
			chaFemales[0].Load();
			chaFemalesTrans[0] = chaFemales[0].transform;
			StartPos.position = chaFemalesTrans[0].position;
			StartPos.rotation = chaFemalesTrans[0].rotation;
		}
		else
		{
			chaFemales[0] = hSceneManager.females[0].ChaControl;
			chaFemales[0].visibleAll = true;
			chaFemalesTrans[0] = chaFemales[0].transform;
			Transform transform = hSceneManager.females[0].Controller.transform;
			StartPos.position = transform.position;
			StartPos.rotation = transform.rotation;
			initStandNip.Add(0, chaFemales[0].GetShapeBodyValue(nipIndex));
		}
		if (!(hSceneManager.females[1] == null))
		{
			chaFemales[1] = hSceneManager.females[1].ChaControl;
			chaFemalesTrans[1] = chaFemales[1].transform;
			initStandNip.Add(1, chaFemales[1].GetShapeBodyValue(nipIndex));
		}
		objCommon = GameObject.Find("HScene");
		if (!(hSceneManager.male == null))
		{
			chaMales[0] = hSceneManager.male.ChaControl;
			chaMalesTrans[0] = chaMales[0].transform;
		}
		for (int l = 0; l < chaFemales.Length; l++)
		{
			if (!(chaFemales[l] == null))
			{
				chaFemales[l].ChangeBustInert(h: true);
			}
		}
		if (hSceneManager.Player != null && hSceneManager.Player.ChaControl.sex == 1 && hSceneManager.bFutanari)
		{
			hSceneManager.Player.ChaControl.ChangeBustInert(h: true);
		}
		sprite = Singleton<HSceneSprite>.Instance;
		hPointCtrl.SetHSceneSprite(sprite, this);
		hPointCtrl.InitHPoint();
		hPointCtrl.playerSex = hSceneManager.Player.ChaControl.sex;
		if (hSceneManager.Player.ChaControl.sex == 1 && hSceneManager.bFutanari)
		{
			hPointCtrl.playerSex = 0;
		}
		if ((hSceneManager.Player.ChaControl.sex == 0 || (hSceneManager.Player.ChaControl.sex == 1 && hSceneManager.bFutanari)) && chaFemales[1] != null)
		{
			hPointCtrl.ExistSecondfemale = true;
		}
		if (!hSceneManager.IsHousingHEnter)
		{
			if (hSceneManager.HMeshCheck(0, deskChairInfos))
			{
				if (hSceneManager.hitHmesh.Item3 < 0)
				{
					if (hSceneManager.EventKind == HSceneManager.HEvent.Yobai)
					{
						hSceneManager.height = 0;
					}
					else
					{
						hSceneManager.height = 1;
					}
				}
				else
				{
					hSceneManager.height = hSceneManager.hitHmesh.Item3;
				}
			}
			else
			{
				switch (hSceneManager.EventKind)
				{
				case HSceneManager.HEvent.Back:
				case HSceneManager.HEvent.GyakuYobai:
				case HSceneManager.HEvent.Neonani:
					hSceneManager.height = 0;
					break;
				case HSceneManager.HEvent.Bath:
					hSceneManager.height = 11;
					ctrlFlag.AddParam(27, 1);
					break;
				case HSceneManager.HEvent.Toilet1:
					hSceneManager.height = 13;
					break;
				case HSceneManager.HEvent.Toilet2:
				case HSceneManager.HEvent.ShagmiBare:
					hSceneManager.height = 14;
					break;
				case HSceneManager.HEvent.Kitchen:
					hSceneManager.height = 9;
					break;
				case HSceneManager.HEvent.Tachi:
					hSceneManager.height = 12;
					ctrlFlag.AddParam(27, 1);
					break;
				case HSceneManager.HEvent.MapBath:
					hSceneManager.height = 1;
					ctrlFlag.AddParam(27, 1);
					break;
				case HSceneManager.HEvent.Stairs:
				case HSceneManager.HEvent.StairsBare:
					hSceneManager.height = 10;
					break;
				case HSceneManager.HEvent.KabeanaBack:
				case HSceneManager.HEvent.KabeanaFront:
					hSceneManager.height = 15;
					break;
				case HSceneManager.HEvent.TsukueBare:
					hSceneManager.height = 4;
					break;
				default:
					yield return PutHmesh();
					break;
				}
				hSceneManager.hitHmesh = new UnityEx.ValueTuple<RaycastHit, GameObject, int>(hSceneManager.hitHmesh.Item1, hSceneManager.hitHmesh.Item2, hSceneManager.height);
			}
		}
		Singleton<Manager.Resources>.Instance.HSceneTable.HMeshObjDic.TryGetValue(Singleton<Manager.Map>.Instance.MapID, out var hmesh);
		if (hmesh != null)
		{
			hmesh.SetActive(value: false);
		}
		hPointCtrl.Init(Singleton<Manager.Map>.Instance.MapID, hSceneManager.Player.AreaID);
		if (hSceneManager.EventKind != HSceneManager.HEvent.Yobai)
		{
			if (hSceneManager.height != -1 && hSceneManager.height != 1 && !hSceneManager.IsHousingHEnter)
			{
				if (hPointCtrl.CheckStartPoint(ref StartPos, hSceneManager.height))
				{
					sprite.usePoint = true;
					hPointCtrl.InitUsePoint = true;
				}
			}
			else if (hSceneManager.IsHousingHEnter)
			{
				hPointCtrl.HousingHStart(hSceneManager.enterPoint);
				sprite.usePoint = true;
				hPointCtrl.InitUsePoint = true;
				StartPos.position = hSceneManager.enterPoint.pivot.position;
				StartPos.rotation = hSceneManager.enterPoint.pivot.rotation;
			}
		}
		else if (hPointCtrl.CheckStartPointYobai(ref StartPos))
		{
			sprite.usePoint = true;
			hPointCtrl.InitUsePoint = true;
		}
		else
		{
			hSceneManager.height = 0;
		}
		if (hSceneManager.height != 11)
		{
			hPointCtrl.SetStartPos(StartPos, hSceneManager.height);
		}
		else if (!sprite.usePoint)
		{
			hSceneManager.height = 1;
			hPointCtrl.SetStartPos(StartPos, hSceneManager.height);
		}
		if (hSceneManager.EventKind == HSceneManager.HEvent.Normal && !hSceneManager.IsHousingHEnter)
		{
			hSceneManager.females[0].Position = StartPos.position;
			chaFemalesTrans[0].position = StartPos.position;
			chaFemalesTrans[0].rotation = StartPos.rotation;
			hSceneManager.females[0].ChaControl.animBody.transform.localPosition = Vector3.zero;
			hSceneManager.females[0].ChaControl.animBody.transform.localRotation = Quaternion.identity;
			if (!hSceneManager.bMerchant)
			{
				yield return BeforeWait();
			}
			hSceneManager.Player.SetActiveOnEquipedItem(active: false);
			hSceneManager.Player.ChaControl.setAllLayerWeight(0f);
		}
		ctrlFlag.nPlace = hSceneManager.height;
		if (hSceneManager.EventKind == HSceneManager.HEvent.Yobai)
		{
			ctrlFlag.isFaintness = true;
		}
		CreateListAnimationFileName();
		if (chaMales[0] != null)
		{
			ctrlMeta = new MetaballCtrl(objMetaBallBase, chaMales[0].objBodyBone, chaFemales[0].objBodyBone, chaFemales[0]);
		}
		else
		{
			ctrlMeta = new MetaballCtrl(objMetaBallBase, null, chaFemales[0].objBodyBone, chaFemales[0]);
		}
		ctrlItem = new HItemCtrl();
		ctrlItem.HItemInit(hitemPlace);
		for (int i = 0; i < ctrlHitObjectFemales.Length; i++)
		{
			if (!(chaFemales[i] == null) && !(chaFemales[i].objBodyBone == null))
			{
				ctrlHitObjectFemales[i].id = i;
				yield return ctrlHitObjectFemales[i].HitObjInit(1, chaFemales[i].objBodyBone, chaFemales[i]);
			}
		}
		for (int j = 0; j < ctrlHitObjectMales.Length; j++)
		{
			if (!(chaMales[j] == null) && !(chaMales[j].objBodyBone == null))
			{
				ctrlHitObjectMales[j].id = j;
				yield return ctrlHitObjectMales[j].HitObjInit(0, chaMales[j].objBodyBone, chaMales[j]);
			}
		}
		for (int m = 0; m < chaMales.Length; m++)
		{
			if (!(chaMales[m] == null) && !(chaMales[m].objBodyBone == null))
			{
				ctrlLookAts[m].DankonInit(chaMales[m], chaFemales);
			}
		}
		ctrlFeelHit = new FeelHit();
		if (!hSceneManager.bMerchant)
		{
			ctrlFeelHit.FeelHitInit(hSceneManager.Personality[0]);
		}
		else
		{
			ctrlFeelHit.FeelHitInit(90);
		}
		ctrlYures[0].chaFemale = chaFemales[0];
		ctrlYures[0].femaleID = 0;
		if (chaFemales[1] != null && chaFemales[1].objBodyBone != null)
		{
			ctrlYures[1].chaFemale = chaFemales[1];
			ctrlYures[1].femaleID = 1;
		}
		if (chaMales[0] != null && chaMales[0].objBodyBone != null)
		{
			ctrlYureMale.chaMale = chaMales[0];
			ctrlYureMale.MaleID = 0;
		}
		ctrlLayer.Init(chaFemales, chaMales);
		if (!hSceneManager.bMerchant)
		{
			ctrlAuto.Load(hSceneManager.strAssetLeaveItToYouFolder, hSceneManager.Personality[0]);
		}
		else
		{
			ctrlAuto.Load(hSceneManager.strAssetLeaveItToYouFolder, 5);
		}
		ctrlDynamics[0] = new DynamicBoneReferenceCtrl();
		ctrlDynamics[1] = new DynamicBoneReferenceCtrl();
		ctrlDynamics[0].Init(chaFemales[0]);
		ctrlSE = new HSeCtrl();
		if (hSceneManager.Player.ChaControl.sex == 0 || hSceneManager.Player.ChaControl.fileParam.futanari)
		{
			if (hSceneManager.Agent[1] != null)
			{
				if (!hSceneManager.bMerchant)
				{
					yield return ctrlVoice.Init(hSceneManager.Agent[0].ChaControl.fileParam.personality, hSceneManager.Agent[0].ChaControl.fileParam.voicePitch, hSceneManager.Agent[0], hSceneManager.Agent[1].ChaControl.fileParam.personality, hSceneManager.Agent[1].ChaControl.fileParam.voicePitch, hSceneManager.Agent[1]);
				}
				else
				{
					yield return ctrlVoice.Init(-90, hSceneManager.merchantActor.ChaControl.fileParam.voicePitch, hSceneManager.merchantActor, hSceneManager.Agent[1].ChaControl.fileParam.personality, hSceneManager.Agent[1].ChaControl.fileParam.voicePitch, hSceneManager.Agent[1], 0);
				}
			}
			else if (!hSceneManager.bMerchant)
			{
				yield return ctrlVoice.Init(hSceneManager.Agent[0].ChaControl.fileParam.personality, hSceneManager.Agent[0].ChaControl.fileParam.voicePitch, hSceneManager.Agent[0]);
			}
			else
			{
				yield return ctrlVoice.Init(-90, hSceneManager.merchantActor.ChaControl.fileParam.voicePitch, hSceneManager.merchantActor, 0, 0f, null, 0);
			}
		}
		else if (!hSceneManager.bMerchant)
		{
			yield return ctrlVoice.Init(hSceneManager.Agent[0].ChaControl.fileParam.personality, hSceneManager.Agent[0].ChaControl.fileParam.voicePitch, hSceneManager.Agent[0]);
		}
		else
		{
			yield return ctrlVoice.Init(-90, hSceneManager.merchantActor.ChaControl.fileParam.voicePitch, hSceneManager.merchantActor, 0, 0f, null, 0);
		}
		if (chaFemales[0] != null && chaFemales[0].cmpBoneBody.targetEtc.trfHeadParent != null)
		{
			ctrlFlag.voice.voiceTrs[0] = chaFemales[0].cmpBoneBody.targetEtc.trfHeadParent;
		}
		else
		{
			ctrlFlag.voice.voiceTrs[0] = null;
		}
		if (chaFemales[1] != null && chaFemales[1].cmpBoneBody.targetEtc.trfHeadParent != null)
		{
			ctrlFlag.voice.voiceTrs[1] = chaFemales[1].cmpBoneBody.targetEtc.trfHeadParent;
		}
		else
		{
			ctrlFlag.voice.voiceTrs[1] = null;
		}
		ctrlEyeNeckFemale[0].Init(chaFemales[0], 0);
		ctrlEyeNeckFemale[0].SetPartner((!(chaMales[0] != null)) ? null : chaMales[0].objBodyBone, (!(chaMales[1] != null)) ? null : chaMales[1].objBodyBone, (!(chaFemales[1] != null)) ? null : chaFemales[1].objBodyBone);
		if (chaMales[0] != null && chaMales[0].objBodyBone != null)
		{
			ctrlEyeNeckMale[0].Init(chaMales[0], 0);
			ctrlEyeNeckMale[0].SetPartner(chaFemales[0].objBodyBone, (!(chaFemales[1] != null)) ? null : chaFemales[1].objBodyBone, (!(chaMales[1] != null)) ? null : chaMales[1].objBodyBone);
		}
		yield return null;
		bool tmp = false;
		if (chaFemales[1] != null && chaFemales[1].objBodyBone != null)
		{
			if (hSceneManager.Player.ChaControl.sex != 1 || ctrlFlag.bFutanari)
			{
				ctrlEyeNeckFemale[1].Init(chaFemales[1], 1);
				ctrlEyeNeckFemale[1].SetPartner((!(chaMales[0] != null)) ? null : chaMales[0].objBodyBone, (!(chaMales[1] != null)) ? null : chaMales[1].objBodyBone, chaFemales[0].objBodyBone);
			}
			else
			{
				hMotionEyeNeckLesP.Init(chaFemales[1], 1);
				hMotionEyeNeckLesP.SetPartner(chaFemales[0].objBodyBone);
			}
			tmp = true;
		}
		if (chaMales[1] != null && chaMales[1].objBodyBone != null)
		{
			ctrlEyeNeckMale[1].Init(chaMales[1], 1);
			ctrlEyeNeckMale[1].SetPartner(chaFemales[0].objBodyBone, (!(chaFemales[1] != null)) ? null : chaFemales[1].objBodyBone, (!(chaMales[0] != null)) ? null : chaMales[0].objBodyBone);
			tmp = true;
		}
		if (tmp)
		{
			yield return null;
		}
		ctrlSiruPastes[0].Init(chaFemales[0]);
		if (chaFemales[1] != null && chaFemales[1].objBodyBone != null)
		{
			ctrlSiruPastes[1].Init(chaFemales[1]);
		}
		ctrlMeta.SetParticle(CtrlParticle);
		if ((bool)objGrondInstantiate)
		{
			objGrondCollision = UnityEngine.Object.Instantiate(objGrondInstantiate, chaFemales[0].objTop.transform);
			objGrondCollision.name = objGrondInstantiate.name;
			objGrondCollision.transform.localPosition = Vector3.zero;
			objGrondCollision.transform.localRotation = Quaternion.identity;
		}
		runtimeAnimatorControllers = Singleton<Manager.Resources>.Instance.HSceneTable.HBaseRuntimeAnimatorControllers;
		if (hSceneManager.EventKind == HSceneManager.HEvent.FromFemale)
		{
			ctrlFlag.initiative = 1;
		}
		if (hSceneManager.EventKind == HSceneManager.HEvent.GyakuYobai)
		{
			ctrlFlag.initiative = 2;
		}
		if (ctrlFlag.nowAnimationInfo.nAnimListInfoID == 3 && ctrlFlag.nowAnimationInfo.id == 8)
		{
			chaFemales[0].SetAccessoryStateAll(show: false);
		}
		if (hSceneManager.EventKind == HSceneManager.HEvent.Bath)
		{
			ctrlFlag.rateTuya = 1f;
		}
		SetStartVoice();
		AnimatorStateInfo ai = chaFemales[0].getAnimatorStateInfo(0);
		ctrlEyeNeckFemale[0].Proc(ai, null, 0);
		ctrlFlag.voice.sleep = hSceneManager.EventKind == HSceneManager.HEvent.Yobai;
		ctrlVoice.BreathProc(ai, chaFemales[0], 0, hSceneManager.EventKind == HSceneManager.HEvent.Yobai);
		Singleton<Manager.Sound>.Instance.Listener = ctrlFlag.cameraCtrl.transform;
		ctrlFlag.selectAnimationListInfo = null;
		yield return sprite.Init();
		sprite.StartAnimInfo = StartAnimInfo;
		sprite.setAnimationList(lstAnimInfo);
		sprite.Setting(chaFemales);
		bool initiative = ctrlFlag.initiative != 0;
		sprite.MainCategoryOfLeaveItToYou(initiative);
		sprite.SetToggleLeaveItToYou(initiative);
		sprite.SetFinishSelect(mode, modeCtrl);
		yield return null;
		DeliveryMember member = new DeliveryMember
		{
			ctrlFlag = ctrlFlag,
			chaMales = chaMales,
			chaFemales = chaFemales,
			fade = fade,
			ctrlMeta = ctrlMeta,
			sprite = sprite,
			item = ctrlItem,
			feelHit = ctrlFeelHit,
			auto = ctrlAuto,
			voice = ctrlVoice,
			particle = CtrlParticle,
			se = ctrlSE,
			lstMotionIK = lstMotionIK,
			AtariEffect = AtariEffect,
			FeelHitEffect3D = FeelHitEffect3D
		};
		lstProc.Add(new Aibu(member));
		lstProc.Add(new Houshi(member));
		(lstProc[1] as Houshi).SetAnimationList(lstAnimInfo[1]);
		lstProc.Add(new Sonyu(member));
		yield return null;
		lstProc.Add(new Spnking(member));
		lstProc.Add(new Masturbation(member));
		lstProc.Add(new Peeping(member));
		yield return null;
		lstProc.Add(new Les(member));
		lstProc.Add(new MultiPlay_F2M1(member));
		(lstProc[7] as MultiPlay_F2M1).SetAnimationList(lstAnimInfo[5]);
		if (mode != -1 && modeCtrl != -1 && ProcBase.endInit)
		{
			lstProc[mode].setAnimationParamater();
		}
		yield return null;
		base.enabled = true;
		nowStart = false;
		sprite.enabled = true;
		nullPlayer = hSceneManager.Player == null;
		if (!nullPlayer && Camera == null)
		{
			Camera = hSceneManager.Player.CameraControl;
		}
		if (!nullPlayer && !hSceneManager.bMerchant)
		{
			Camera.HCamera = ctrlFlag.cameraCtrl;
		}
		List<Light> light = new List<Light>();
		if (!nullPlayer)
		{
			Camera.DisableUpdateCustomLight();
			light.Add(Camera.NormalKeyLight);
			light.Add(Camera.CustomKeyLight);
		}
		else
		{
			light.Add(ctrlFlag.cameraCtrl.thisCamera.GetComponentInChildren<Light>());
		}
		infoLight = new LightInfo[2];
		for (int n = 0; n < light.Count; n++)
		{
			int num = n;
			infoLight[num] = new LightInfo();
			infoLight[num].objCharaLight = light[num].gameObject;
			infoLight[num].light = light[num];
			infoLight[num].initRot = light[num].transform.localRotation;
			infoLight[num].initIntensity = Mathf.InverseLerp(infoLight[num].minIntensity, infoLight[num].maxIntensity, light[num].intensity);
			infoLight[num].initColor = light[num].color;
		}
		sprite.SetLightInfo(infoLight);
		HItemDrag = sprite.objHItem.GetComponent<HSceneSpriteHitem>();
		NowStateIsEnd = false;
		PlayerData playerData = Singleton<Game>.Instance?.WorldData?.PlayerData;
		int charaID;
		int adv_category;
		IParams param;
		if (!hSceneManager.bMerchant)
		{
			AgentData agentData = hSceneManager.Agent[0].AgentData;
			charaID = agentData.param.charaID;
			adv_category = 10;
			param = agentData;
		}
		else
		{
			MerchantData merchantData = hSceneManager.merchantActor.MerchantData;
			charaID = merchantData.param.charaID;
			adv_category = 10;
			param = merchantData;
		}
		packData = new PackData(charaID, adv_category);
		packData.SetCommandData(Singleton<Game>.Instance?.WorldData?.Environment);
		packData.SetParam(param, playerData);
		if (hSceneManager.EventKind == HSceneManager.HEvent.Back)
		{
			Singleton<HSceneFlagCtrl>.Instance.AddParam(31, 1);
		}
		else if (hSceneManager.EventKind == HSceneManager.HEvent.Kitchen)
		{
			Singleton<HSceneFlagCtrl>.Instance.AddParam(32, 1);
		}
		else if (hSceneManager.EventKind == HSceneManager.HEvent.Yobai)
		{
			Singleton<HSceneFlagCtrl>.Instance.AddParam(33, 1);
		}
		if (!(ctrlFlag.nowHPoint != null))
		{
			yield break;
		}
		foreach (KeyValuePair<int, UnityEx.ValueTuple<int, int>> item in ctrlFlag.nowHPoint._nPlace)
		{
			if (item.Value.Item1 == 13 || item.Value.Item1 == 14)
			{
				ctrlFlag.AddParam(29, 1);
				ctrlFlag.isToilet = true;
			}
		}
	}

	private void Update()
	{
		if (ctrlFlag.BeforeHWait)
		{
			if (!hSceneManager.isForce)
			{
				ctrlVoice.HBeforeHouchiTime += Time.unscaledDeltaTime;
				if (ctrlVoice.HBeforeHouchiTime >= ctrlFlag.HBeforeHouchiTime)
				{
					ctrlVoice.HBeforeProc(chaFemales);
				}
			}
			ShortcutKey();
			return;
		}
		if (aInfo != ctrlFlag.nowAnimationInfo)
		{
			aInfo = ctrlFlag.nowAnimationInfo;
		}
		if (ctrlFlag.cameraCtrl.isConfigTargetTex != Config.ActData.Look)
		{
			ctrlFlag.cameraCtrl.isConfigTargetTex = Config.ActData.Look;
		}
		if (ctrlFlag.rateNip < ctrlFlag.feel_f)
		{
			ctrlFlag.rateNip = ctrlFlag.feel_f;
		}
		float rate = Mathf.Lerp(0f, ctrlFlag.rateNipMax, ctrlFlag.rateNip);
		bool gloss = Config.HData.Gloss;
		if (ctrlFlag.rateTuya < ctrlFlag.feel_f && gloss)
		{
			ctrlFlag.rateTuya = ctrlFlag.feel_f;
		}
		PlayerWet();
		if (!useLotion)
		{
			useLotion = HItemDrag.Effect(7);
		}
		if (ctrlFlag.rateTuya < 1f && useLotion && gloss)
		{
			ctrlFlag.rateTuya = 1f;
			HItemDrag.SetUse(7, use: false);
		}
		float skinGlossRate = ((!isTuyaOn) ? ctrlFlag.rateTuya : 1f);
		if (!gloss)
		{
			skinGlossRate = 0f;
		}
		for (int i = 0; i < chaFemales.Length && !(chaFemales[i] == null) && !(chaFemales[i].objTop == null); i++)
		{
			if (!(hSceneManager.Player != null) || !(hSceneManager.Player.ChaControl == chaFemales[i]))
			{
				chaFemales[i].ChangeNipRate(rate);
				chaFemales[i].skinGlossRate = skinGlossRate;
			}
		}
		if (HItemDrag.Effect(3))
		{
			hSceneManager.Toilet = 70f;
			HItemDrag.SetUse(3, use: false);
		}
		if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.SceneEnd)
		{
			EndProc();
		}
		if (NowStateIsEnd)
		{
			return;
		}
		AnimatorStateInfo animatorStateInfo = chaFemales[0].getAnimatorStateInfo(0);
		ctrlVoice.Proc(animatorStateInfo, chaFemales);
		ctrlSiruPastes[0].Proc(animatorStateInfo);
		if (aInfo.nPromiscuity >= 1)
		{
			ctrlSiruPastes[1].Proc(animatorStateInfo);
		}
		if (mode != -1 && modeCtrl != -1 && ProcBase.endInit)
		{
			lstProc[mode].Proc(modeCtrl, aInfo);
		}
		bool isAutoAutoLeaveItToYou = false;
		if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.LeaveItToYou)
		{
			ctrlFlag.initiative = ((ctrlFlag.initiative == 0) ? 1 : 0);
			ctrlFlag.isAutoActionChange = false;
			sprite.MainCategoryOfLeaveItToYou(ctrlFlag.initiative != 0);
			ctrlAuto.Reset();
			ctrlAuto.AutoAutoLeaveItToYouInit();
			if (ctrlFlag.initiative != 0)
			{
				GetAutoAnimation(_isFirst: false);
				if (ctrlFlag.selectAnimationListInfo == null)
				{
					GetAutoAnimation(_isFirst: true);
				}
				ctrlFlag.numLeadFemale++;
			}
			else
			{
				ReturnToNormalFromTheAuto();
				AtariEffect.Stop();
				FeelHitEffect3D.Stop();
			}
		}
		if (ctrlFlag.isAutoActionChange && ctrlFlag.selectAnimationListInfo == null)
		{
			sprite.SetMotionListDraw(_active: false);
			GetAutoAnimation(_isFirst: false);
			if (ctrlFlag.selectAnimationListInfo == null)
			{
				GetAutoAnimation(_isFirst: true);
				if (ctrlFlag.selectAnimationListInfo == null)
				{
					ctrlFlag.isAutoActionChange = false;
				}
			}
			ctrlAuto.SetSpeed(ctrlFlag.speed);
		}
		if (ctrlFlag.selectAnimationListInfo != null && !nowChangeAnim)
		{
			if (IsIdle(chaFemales[0].animBody) && !ctrlFlag.isFaintness && !ctrlFlag.pointMoveAnimChange && ctrlFlag.nowAnimationInfo != ctrlFlag.selectAnimationListInfo)
			{
				ctrlFlag.voice.playStart = 4;
			}
			nowChangeAnim = true;
			Observable.NextFrame().Subscribe(delegate
			{
				Observable.FromCoroutine(() => ChangeAnimation(ctrlFlag.selectAnimationListInfo, _isForceResetCamera: false, isAutoAutoLeaveItToYou, !ctrlFlag.pointMoveAnimChange)).Finally(delegate
				{
					if (!nowChangeAnim)
					{
						ctrlFlag.selectAnimationListInfo = null;
						ctrlFlag.isAutoActionChange = false;
					}
					if (ctrlFlag.pointMoveAnimChange)
					{
						ctrlFlag.pointMoveAnimChange = false;
					}
					GlobalMethod.setCameraMoveFlag(ctrlFlag.cameraCtrl, _bPlay: true);
					sprite.ChangeStart = false;
				}).Subscribe();
			});
			if (hSceneManager.Player.CameraControl.Mode != CameraMode.H)
			{
				hSceneManager.Player.CameraControl.Mode = CameraMode.H;
				ctrlFlag.HBeforeCamera.gameObject.SetActive(value: false);
			}
		}
		for (int j = 0; j < ctrlHitObjectFemales.Length && (aInfo.nPromiscuity >= 1 || j <= 0); j++)
		{
			if (!(chaFemales[j] == null) && !(chaFemales[j].objBodyBone == null))
			{
				ctrlHitObjectFemales[j].Proc(chaFemales[j].animBody);
				bool flag = false;
				flag = hSceneManager.Player.ChaControl.sex == 1 && !ctrlFlag.bFutanari;
				if (j == 0 || !flag)
				{
					ctrlEyeNeckFemale[j].Proc(animatorStateInfo, ctrlVoice.nowVoices[j].Face, j);
				}
				else
				{
					hMotionEyeNeckLesP.Proc(animatorStateInfo, j);
				}
			}
		}
		for (int k = 0; k < ctrlHitObjectMales.Length; k++)
		{
			if (!(chaMales[k] == null) && !(chaMales[k].objTop == null))
			{
				if (aInfo.nPromiscuity != 0 && k > 0)
				{
					break;
				}
				if (!(chaMales[k].objBodyBone == null))
				{
					ctrlHitObjectMales[k].Proc(chaMales[k].animBody);
					ctrlEyeNeckMale[k].Proc(animatorStateInfo);
				}
			}
		}
		ctrlDynamics[0].Proc();
		if (aInfo.nPromiscuity >= 1)
		{
			ctrlDynamics[1].Proc();
		}
		ctrlSE.Proc(animatorStateInfo, chaFemales);
		sprite.GuidProc(animatorStateInfo);
		ShortcutKey();
		if (!GlobalMethod.IsCameraMoveFlag(ctrlFlag.cameraCtrl) && !UnityEngine.Input.GetMouseButton(0) && !UnityEngine.Input.GetMouseButton(1))
		{
			GlobalMethod.setCameraMoveFlag(ctrlFlag.cameraCtrl, _bPlay: true);
		}
		ctrlFlag.click = HSceneFlagCtrl.ClickKind.None;
	}

	public void SetStartAnimationInfo(HSceneManager.HEvent hEvent, int mode = -1)
	{
		StartAnimInfo = null;
		List<UnityEx.ValueTuple<HSceneManager.HEvent, int, StartMotion>> list = new List<UnityEx.ValueTuple<HSceneManager.HEvent, int, StartMotion>>();
		list = Singleton<Manager.Resources>.Instance.HSceneTable.lstStartAnimInfo;
		int num = -1;
		if (!hSceneManager.IsHousingHEnter)
		{
			switch (hEvent)
			{
			case HSceneManager.HEvent.Normal:
			{
				for (int num56 = 0; num56 < list.Count; num56++)
				{
					if (list[num56].Item1 != hEvent || list[num56].Item2 != hSceneManager.height)
					{
						continue;
					}
					if (mode != -1)
					{
						if (list[num56].Item3.mode != mode)
						{
							continue;
						}
						num = list[num56].Item3.id;
						for (int num57 = 0; num57 < lstAnimInfo[mode].Count; num57++)
						{
							if (num != lstAnimInfo[mode][num57].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[mode][num57].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[mode][num57].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[mode][num57];
							break;
						}
					}
					else
					{
						num = list[num56].Item3.id;
						int num58 = list[num56].Item3.mode;
						for (int num59 = 0; num59 < lstAnimInfo[num58].Count; num59++)
						{
							if (num != lstAnimInfo[num58][num59].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[num58][num59].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[num58][num59].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[num58][num59];
							break;
						}
					}
					if (StartAnimInfo == null)
					{
						continue;
					}
					break;
				}
				break;
			}
			case HSceneManager.HEvent.Yobai:
			{
				for (int num24 = 0; num24 < list.Count; num24++)
				{
					if (list[num24].Item1 != hEvent || list[num24].Item2 != hSceneManager.height)
					{
						continue;
					}
					if (mode != -1)
					{
						if (list[num24].Item3.mode != mode)
						{
							continue;
						}
						num = list[num24].Item3.id;
						for (int num25 = 0; num25 < lstAnimInfo[mode].Count; num25++)
						{
							if (num == lstAnimInfo[mode][num25].id && lstAnimInfo[mode][num25].bSleep)
							{
								StartAnimInfo = lstAnimInfo[mode][num25];
								break;
							}
						}
					}
					else
					{
						num = list[num24].Item3.id;
						int num26 = list[num24].Item3.mode;
						for (int num27 = 0; num27 < lstAnimInfo[num26].Count; num27++)
						{
							if (num == lstAnimInfo[num26][num27].id && lstAnimInfo[num26][num27].bSleep)
							{
								StartAnimInfo = lstAnimInfo[num26][num27];
								break;
							}
						}
					}
					if (StartAnimInfo == null)
					{
						continue;
					}
					break;
				}
				break;
			}
			case HSceneManager.HEvent.Bath:
			{
				for (int num40 = 0; num40 < list.Count; num40++)
				{
					if (list[num40].Item1 != hEvent || list[num40].Item2 != 11)
					{
						continue;
					}
					if (mode != -1)
					{
						if (list[num40].Item3.mode != mode)
						{
							continue;
						}
						num = list[num40].Item3.id;
						for (int num41 = 0; num41 < lstAnimInfo[mode].Count; num41++)
						{
							if (num != lstAnimInfo[mode][num41].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[mode][num41].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[mode][num41].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[mode][num41];
							break;
						}
					}
					else
					{
						num = list[num40].Item3.id;
						int num42 = list[num40].Item3.mode;
						for (int num43 = 0; num43 < lstAnimInfo[num42].Count; num43++)
						{
							if (num != lstAnimInfo[num42][num43].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[num42][num43].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[num42][num43].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[num42][num43];
							break;
						}
					}
					if (StartAnimInfo == null)
					{
						continue;
					}
					break;
				}
				break;
			}
			case HSceneManager.HEvent.Toilet1:
			{
				for (int num8 = 0; num8 < list.Count; num8++)
				{
					if (list[num8].Item1 != hEvent || list[num8].Item2 != 13)
					{
						continue;
					}
					if (mode != -1)
					{
						if (list[num8].Item3.mode != mode)
						{
							continue;
						}
						num = list[num8].Item3.id;
						for (int num9 = 0; num9 < lstAnimInfo[mode].Count; num9++)
						{
							if (num != lstAnimInfo[mode][num9].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[mode][num9].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[mode][num9].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[mode][num9];
							break;
						}
					}
					else
					{
						num = list[num8].Item3.id;
						int num10 = list[num8].Item3.mode;
						for (int num11 = 0; num11 < lstAnimInfo[num10].Count; num11++)
						{
							if (num != lstAnimInfo[num10][num11].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[num10][num11].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[num10][num11].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[num10][num11];
							break;
						}
					}
					if (StartAnimInfo == null)
					{
						continue;
					}
					break;
				}
				break;
			}
			case HSceneManager.HEvent.Toilet2:
			{
				for (int num48 = 0; num48 < list.Count; num48++)
				{
					if (list[num48].Item1 != hEvent || list[num48].Item2 != 14)
					{
						continue;
					}
					if (mode != -1)
					{
						if (list[num48].Item3.mode != mode)
						{
							continue;
						}
						num = list[num48].Item3.id;
						for (int num49 = 0; num49 < lstAnimInfo[mode].Count; num49++)
						{
							if (num != lstAnimInfo[mode][num49].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[mode][num49].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[mode][num49].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[mode][num49];
							break;
						}
					}
					else
					{
						num = list[num48].Item3.id;
						int num50 = list[num48].Item3.mode;
						for (int num51 = 0; num51 < lstAnimInfo[num50].Count; num51++)
						{
							if (num != lstAnimInfo[num50][num51].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[num50][num51].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[num50][num51].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[num50][num51];
							break;
						}
					}
					if (StartAnimInfo == null)
					{
						continue;
					}
					break;
				}
				break;
			}
			case HSceneManager.HEvent.ShagmiBare:
			{
				for (int num32 = 0; num32 < list.Count; num32++)
				{
					if (list[num32].Item1 != hEvent || list[num32].Item2 != hSceneManager.height)
					{
						continue;
					}
					if (mode != -1)
					{
						if (list[num32].Item3.mode != mode)
						{
							continue;
						}
						num = list[num32].Item3.id;
						for (int num33 = 0; num33 < lstAnimInfo[mode].Count; num33++)
						{
							if (num != lstAnimInfo[mode][num33].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[mode][num33].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[mode][num33].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[mode][num33];
							break;
						}
					}
					else
					{
						num = list[num32].Item3.id;
						int num34 = list[num32].Item3.mode;
						for (int num35 = 0; num35 < lstAnimInfo[num34].Count; num35++)
						{
							if (num != lstAnimInfo[num34][num35].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[num34][num35].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[num34][num35].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[num34][num35];
							break;
						}
					}
					if (StartAnimInfo == null)
					{
						continue;
					}
					break;
				}
				break;
			}
			case HSceneManager.HEvent.Back:
			{
				for (int num16 = 0; num16 < list.Count; num16++)
				{
					if (list[num16].Item1 != hEvent || list[num16].Item2 != 0)
					{
						continue;
					}
					if (mode != -1)
					{
						if (list[num16].Item3.mode != mode)
						{
							continue;
						}
						num = list[num16].Item3.id;
						for (int num17 = 0; num17 < lstAnimInfo[mode].Count; num17++)
						{
							if (num != lstAnimInfo[mode][num17].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[mode][num17].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[mode][num17].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[mode][num17];
							break;
						}
					}
					else
					{
						num = list[num16].Item3.id;
						int num18 = list[num16].Item3.mode;
						for (int num19 = 0; num19 < lstAnimInfo[num18].Count; num19++)
						{
							if (num != lstAnimInfo[num18][num19].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[num18][num19].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[num18][num19].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[num18][num19];
							break;
						}
					}
					if (StartAnimInfo == null)
					{
						continue;
					}
					break;
				}
				break;
			}
			case HSceneManager.HEvent.Kitchen:
			{
				for (int l = 0; l < list.Count; l++)
				{
					if (list[l].Item1 != hEvent || list[l].Item2 != 9)
					{
						continue;
					}
					if (mode != -1)
					{
						if (list[l].Item3.mode != mode)
						{
							continue;
						}
						num = list[l].Item3.id;
						for (int m = 0; m < lstAnimInfo[mode].Count; m++)
						{
							if (num != lstAnimInfo[mode][m].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[mode][m].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[mode][m].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[mode][m];
							break;
						}
					}
					else
					{
						num = list[l].Item3.id;
						int num3 = list[l].Item3.mode;
						for (int n = 0; n < lstAnimInfo[num3].Count; n++)
						{
							if (num != lstAnimInfo[num3][n].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[num3][n].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[num3][n].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[num3][n];
							break;
						}
					}
					if (StartAnimInfo == null)
					{
						continue;
					}
					break;
				}
				break;
			}
			case HSceneManager.HEvent.Tachi:
			{
				for (int num52 = 0; num52 < list.Count; num52++)
				{
					if (list[num52].Item1 != hEvent || list[num52].Item2 != 12)
					{
						continue;
					}
					if (mode != -1)
					{
						if (list[num52].Item3.mode != mode)
						{
							continue;
						}
						num = list[num52].Item3.id;
						for (int num53 = 0; num53 < lstAnimInfo[mode].Count; num53++)
						{
							if (num != lstAnimInfo[mode][num53].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[mode][num53].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[mode][num53].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[mode][num53];
							break;
						}
					}
					else
					{
						num = list[num52].Item3.id;
						int num54 = list[num52].Item3.mode;
						for (int num55 = 0; num55 < lstAnimInfo[num54].Count; num55++)
						{
							if (num != lstAnimInfo[num54][num55].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[num54][num55].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[num54][num55].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[num54][num55];
							break;
						}
					}
					if (StartAnimInfo == null)
					{
						continue;
					}
					break;
				}
				break;
			}
			case HSceneManager.HEvent.MapBath:
			{
				for (int num44 = 0; num44 < list.Count; num44++)
				{
					if (list[num44].Item1 != hEvent || list[num44].Item2 != 1)
					{
						continue;
					}
					if (mode != -1)
					{
						if (list[num44].Item3.mode != mode)
						{
							continue;
						}
						num = list[num44].Item3.id;
						for (int num45 = 0; num45 < lstAnimInfo[mode].Count; num45++)
						{
							if (num != lstAnimInfo[mode][num45].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[mode][num45].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[mode][num45].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[mode][num45];
							break;
						}
					}
					else
					{
						num = list[num44].Item3.id;
						int num46 = list[num44].Item3.mode;
						for (int num47 = 0; num47 < lstAnimInfo[num46].Count; num47++)
						{
							if (num != lstAnimInfo[num46][num47].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[num46][num47].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[num46][num47].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[num46][num47];
							break;
						}
					}
					if (StartAnimInfo == null)
					{
						continue;
					}
					break;
				}
				break;
			}
			case HSceneManager.HEvent.Stairs:
			case HSceneManager.HEvent.StairsBare:
			{
				for (int num36 = 0; num36 < list.Count; num36++)
				{
					if (list[num36].Item1 != hEvent || list[num36].Item2 != 10)
					{
						continue;
					}
					if (mode != -1)
					{
						if (list[num36].Item3.mode != mode)
						{
							continue;
						}
						num = list[num36].Item3.id;
						for (int num37 = 0; num37 < lstAnimInfo[mode].Count; num37++)
						{
							if (num != lstAnimInfo[mode][num37].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[mode][num37].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[mode][num37].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[mode][num37];
							break;
						}
					}
					else
					{
						num = list[num36].Item3.id;
						int num38 = list[num36].Item3.mode;
						for (int num39 = 0; num39 < lstAnimInfo[num38].Count; num39++)
						{
							if (num != lstAnimInfo[num38][num39].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[num38][num39].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[num38][num39].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[num38][num39];
							break;
						}
					}
					if (StartAnimInfo == null)
					{
						continue;
					}
					break;
				}
				break;
			}
			case HSceneManager.HEvent.GyakuYobai:
			{
				for (int num28 = 0; num28 < list.Count; num28++)
				{
					if (list[num28].Item1 != hEvent || list[num28].Item2 != hSceneManager.height)
					{
						continue;
					}
					if (mode != -1)
					{
						if (list[num28].Item3.mode != mode)
						{
							continue;
						}
						num = list[num28].Item3.id;
						for (int num29 = 0; num29 < lstAnimInfo[mode].Count; num29++)
						{
							if (num != lstAnimInfo[mode][num29].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[mode][num29].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[mode][num29].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[mode][num29];
							break;
						}
					}
					else
					{
						num = list[num28].Item3.id;
						int num30 = list[num28].Item3.mode;
						for (int num31 = 0; num31 < lstAnimInfo[num30].Count; num31++)
						{
							if (num != lstAnimInfo[num30][num31].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[num30][num31].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[num30][num31].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[num30][num31];
							break;
						}
					}
					if (StartAnimInfo == null)
					{
						continue;
					}
					break;
				}
				break;
			}
			case HSceneManager.HEvent.FromFemale:
			{
				for (int num20 = 0; num20 < list.Count; num20++)
				{
					if (list[num20].Item1 != hEvent || list[num20].Item2 != hSceneManager.height)
					{
						continue;
					}
					if (mode != -1)
					{
						if (list[num20].Item3.mode != mode)
						{
							continue;
						}
						num = list[num20].Item3.id;
						for (int num21 = 0; num21 < lstAnimInfo[mode].Count; num21++)
						{
							if (num != lstAnimInfo[mode][num21].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[mode][num21].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[mode][num21].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[mode][num21];
							break;
						}
					}
					else
					{
						num = list[num20].Item3.id;
						int num22 = list[num20].Item3.mode;
						for (int num23 = 0; num23 < lstAnimInfo[num22].Count; num23++)
						{
							if (num != lstAnimInfo[num22][num23].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[num22][num23].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[num22][num23].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[num22][num23];
							break;
						}
					}
					if (StartAnimInfo == null)
					{
						continue;
					}
					break;
				}
				break;
			}
			case HSceneManager.HEvent.KabeanaBack:
			case HSceneManager.HEvent.KabeanaFront:
			{
				for (int num12 = 0; num12 < list.Count; num12++)
				{
					if (list[num12].Item1 != hEvent || list[num12].Item2 != 15)
					{
						continue;
					}
					if (mode != -1)
					{
						if (list[num12].Item3.mode != mode)
						{
							continue;
						}
						num = list[num12].Item3.id;
						for (int num13 = 0; num13 < lstAnimInfo[mode].Count; num13++)
						{
							if (num != lstAnimInfo[mode][num13].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[mode][num13].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[mode][num13].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[mode][num13];
							break;
						}
					}
					else
					{
						num = list[num12].Item3.id;
						int num14 = list[num12].Item3.mode;
						for (int num15 = 0; num15 < lstAnimInfo[num14].Count; num15++)
						{
							if (num != lstAnimInfo[num14][num15].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[num14][num15].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[num14][num15].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[num14][num15];
							break;
						}
					}
					if (StartAnimInfo == null)
					{
						continue;
					}
					break;
				}
				break;
			}
			case HSceneManager.HEvent.Neonani:
			{
				for (int num4 = 0; num4 < list.Count; num4++)
				{
					if (list[num4].Item1 != hEvent || list[num4].Item2 != 0)
					{
						continue;
					}
					if (mode != -1)
					{
						if (list[num4].Item3.mode != mode)
						{
							continue;
						}
						num = list[num4].Item3.id;
						for (int num5 = 0; num5 < lstAnimInfo[mode].Count; num5++)
						{
							if (num != lstAnimInfo[mode][num5].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[mode][num5].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[mode][num5].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[mode][num5];
							break;
						}
					}
					else
					{
						num = list[num4].Item3.id;
						int num6 = list[num4].Item3.mode;
						for (int num7 = 0; num7 < lstAnimInfo[num6].Count; num7++)
						{
							if (num != lstAnimInfo[num6][num7].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[num6][num7].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[num6][num7].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[num6][num7];
							break;
						}
					}
					if (StartAnimInfo == null)
					{
						continue;
					}
					break;
				}
				break;
			}
			case HSceneManager.HEvent.TsukueBare:
			{
				for (int i = 0; i < list.Count; i++)
				{
					if (list[i].Item1 != hEvent || list[i].Item2 != 4)
					{
						continue;
					}
					if (mode != -1)
					{
						if (list[i].Item3.mode != mode)
						{
							continue;
						}
						num = list[i].Item3.id;
						for (int j = 0; j < lstAnimInfo[mode].Count; j++)
						{
							if (num != lstAnimInfo[mode][j].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[mode][j].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[mode][j].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[mode][j];
							break;
						}
					}
					else
					{
						num = list[i].Item3.id;
						int num2 = list[i].Item3.mode;
						for (int k = 0; k < lstAnimInfo[num2].Count; k++)
						{
							if (num != lstAnimInfo[num2][k].id)
							{
								continue;
							}
							if (hSceneManager.isForce)
							{
								if (lstAnimInfo[num2][k].nIyaAction == 0)
								{
									continue;
								}
							}
							else if (lstAnimInfo[num2][k].nIyaAction == 2)
							{
								continue;
							}
							StartAnimInfo = lstAnimInfo[num2][k];
							break;
						}
					}
					if (StartAnimInfo == null)
					{
						continue;
					}
					break;
				}
				break;
			}
			}
		}
		else
		{
			for (int num60 = 0; num60 < list.Count; num60++)
			{
				if (list[num60].Item2 != hSceneManager.height)
				{
					continue;
				}
				num = list[num60].Item3.id;
				int num61 = list[num60].Item3.mode;
				for (int num62 = 0; num62 < lstAnimInfo[num61].Count; num62++)
				{
					if (num == lstAnimInfo[num61][num62].id)
					{
						StartAnimInfo = lstAnimInfo[num61][num62];
						break;
					}
				}
				if (StartAnimInfo != null)
				{
					break;
				}
			}
		}
		if (StartAnimInfo == null)
		{
			int num63 = ((mode != -1) ? mode : 0);
			for (int num64 = 0; num64 < lstAnimInfo[num63].Count; num64++)
			{
				int index = num64;
				if (lstAnimInfo[num63][index].nPositons.Contains(hSceneManager.height))
				{
					StartAnimInfo = lstAnimInfo[num63][index];
					break;
				}
			}
			if (StartAnimInfo == null)
			{
				StartAnimInfo = lstAnimInfo[num63][0];
			}
		}
		ChangeStartAnimation(hEvent);
	}

	private void ChangeStartAnimation(HSceneManager.HEvent hEvent)
	{
		if (Singleton<Voice>.Instance.IsVoiceCheck(ctrlFlag.voice.voiceTrs[0]))
		{
			Singleton<Voice>.Instance.Stop(ctrlFlag.voice.voiceTrs[0]);
		}
		bool flag = hEvent == HSceneManager.HEvent.Normal;
		fade.FadeStart(1f);
		Observable.FromCoroutine(() => StartAnim(StartAnimInfo, 0, hEvent)).Finally(delegate
		{
			ctrlFlag.voice.playStart = 1;
			GlobalMethod.setCameraMoveFlag(ctrlFlag.cameraCtrl, _bPlay: true);
		}).Subscribe();
		if (hSceneManager.Player.CameraControl.HCamera == null)
		{
			hSceneManager.Player.CameraControl.HCamera = ctrlFlag.cameraCtrl;
		}
		if (flag)
		{
			hSceneManager.Player.CameraControl.Mode = CameraMode.H;
			ctrlFlag.HBeforeCamera.gameObject.SetActive(value: false);
		}
		Singleton<HPointCtrl>.Instance.HEnterCategory = StartAnimInfo.nAnimListInfoID;
	}

	public void SetStartAnimationInfoM(HSceneManager.HEvent hEvent)
	{
		List<UnityEx.ValueTuple<HSceneManager.HEvent, int, StartMotion>> lstStartAnimInfoM = Singleton<Manager.Resources>.Instance.HSceneTable.lstStartAnimInfoM;
		StartAnimInfo = null;
		if (hSceneManager.MerchantLimit == 0)
		{
			return;
		}
		List<AnimationListInfo> list = new List<AnimationListInfo>();
		for (int i = 0; i < lstStartAnimInfoM.Count; i++)
		{
			int num = lstStartAnimInfoM[i].Item3.mode;
			int id = lstStartAnimInfoM[i].Item3.id;
			if (hSceneManager.MerchantLimit < 1 || lstStartAnimInfoM[i].Item1 != 0 || lstStartAnimInfoM[i].Item2 != hSceneManager.height)
			{
				continue;
			}
			if (hSceneManager.Player.ChaControl.sex == 1 && !hSceneManager.bFutanari)
			{
				if (num != 4)
				{
					continue;
				}
			}
			else if ((hSceneManager.MerchantLimit == 1 && num != 1) || (hSceneManager.Agent[1] == null && num == 5) || num == 4)
			{
				continue;
			}
			for (int j = 0; j < lstAnimInfo[num].Count; j++)
			{
				if (id == lstAnimInfo[num][j].id)
				{
					list.Add(lstAnimInfo[num][j]);
				}
			}
		}
		ShuffleRand shuffleRand = new ShuffleRand();
		int num2 = 0;
		shuffleRand.Init(list.Count);
		num2 = shuffleRand.Get();
		StartAnimInfo = list[num2];
		Observable.FromCoroutine(() => StartAnim(StartAnimInfo, 1)).Finally(delegate
		{
			GlobalMethod.setCameraMoveFlag(ctrlFlag.cameraCtrl, _bPlay: true);
		}).Subscribe();
		Singleton<HPointCtrl>.Instance.HEnterCategory = StartAnimInfo.nAnimListInfoID;
	}

	private IEnumerator StartAnim(AnimationListInfo StartAnimInfo, int mode, HSceneManager.HEvent hEvent = HSceneManager.HEvent.Normal)
	{
		while (!sprite.enabled)
		{
			yield return null;
		}
		switch (mode)
		{
		case 0:
		{
			bool camChange = hEvent == HSceneManager.HEvent.Normal;
			yield return ChangeAnimation(StartAnimInfo, camChange, _isForceLoopAction: false, _UseFade: false);
			break;
		}
		case 1:
			yield return ChangeAnimation(StartAnimInfo, _isForceResetCamera: false);
			break;
		}
	}

	private void EndProc()
	{
		ctrlFlag.click = HSceneFlagCtrl.ClickKind.None;
		NowStateIsEnd = true;
		sprite.ReSetLight();
		Camera.EnableUpdateCustomLight();
		Singleton<Manager.Sound>.Instance.Listener = Singleton<Manager.Map>.Instance.Player.CameraControl.transform;
		Singleton<Manager.Resources>.Instance.HSceneTable.HMeshObjDic[Singleton<Manager.Map>.Instance.MapID].SetActive(value: true);
		hSceneManager.CameraMesh?.SetActive(value: true);
		EndAnimChange();
		hSceneManager.endStatus = (byte)(ctrlFlag.isFaintness ? 1u : 0u);
		chaFemales[0].SetClothesStateAll(0);
		chaFemales[0].SetAccessoryStateAll(show: true);
		for (int i = 0; i < 5; i++)
		{
			ChaFileDefine.SiruParts parts = (ChaFileDefine.SiruParts)i;
			chaFemales[0].SetSiruFlag(parts, 0);
		}
		if (ctrlEyeNeckFemale[0] != null && chaFemales[0] != null && chaFemales[0].objBodyBone != null)
		{
			ctrlEyeNeckFemale[0].NowEndADV = true;
		}
		if (ctrlEyeNeckFemale[1] != null && chaFemales[1] != null && chaFemales[1].objBodyBone != null)
		{
			if (hSceneManager.Player.ChaControl.sex == 0)
			{
				ctrlEyeNeckFemale[1].NowEndADV = true;
			}
			else if (hSceneManager.Player.ChaControl.sex == 1)
			{
				if (ctrlFlag.bFutanari)
				{
					ctrlEyeNeckFemale[1].NowEndADV = true;
				}
				else
				{
					hMotionEyeNeckLesP.NowEndADV = true;
				}
			}
		}
		if (ctrlEyeNeckMale[0] != null && chaMales[0] != null && chaMales[0].objBodyBone != null)
		{
			ctrlEyeNeckMale[0].NowEndADV = true;
		}
		if (ctrlEyeNeckMale[1] != null && chaMales[1] != null && chaMales[1].objBodyBone != null)
		{
			ctrlEyeNeckMale[1].NowEndADV = true;
		}
		foreach (AgentActor value in Singleton<Manager.Map>.Instance.AgentTable.Values)
		{
			if (!(value == null) && hSceneManager.ReturnActionTypes.ContainsKey(value))
			{
				value.EnableEntity();
			}
		}
		MerchantActor merchant = Singleton<Manager.Map>.Instance.Merchant;
		if (hSceneManager.bMerchant)
		{
			if (!merchant.ChaControl.neckLookCtrl.enabled)
			{
				merchant.ChaControl.neckLookCtrl.enabled = true;
			}
			if (!merchant.ChaControl.eyeLookCtrl.enabled)
			{
				merchant.ChaControl.eyeLookCtrl.enabled = true;
			}
			merchant.ChaControl.ChangeLookNeckPtn(3);
			merchant.ChaControl.ChangeLookNeckTarget(0);
			merchant.ChaControl.ChangeLookEyesPtn(0);
			merchant.ChaControl.ChangeLookEyesTarget(0);
			Singleton<Game>.Instance.GetExpression(merchant.ID, "標準")?.Change(merchant.ChaControl);
			merchant.ChaControl.ChangeMouthOpenMin(merchant.ChaControl.fileStatus.mouthOpenMin);
		}
		int shortNameHash = merchant.ChaControl.animBody.GetCurrentAnimatorStateInfo(0).shortNameHash;
		merchant.AnimationMerchant.SetAnimatorController(merchant.ChaControl.animBody.runtimeAnimatorController);
		merchant.AnimationMerchant.MapIK.Calc(shortNameHash);
		merchant.EnableEntity();
		if (hSceneManager.male != null)
		{
			if (!hSceneManager.male.ChaControl.neckLookCtrl.enabled)
			{
				hSceneManager.male.ChaControl.neckLookCtrl.enabled = true;
			}
			if (!hSceneManager.male.ChaControl.eyeLookCtrl.enabled)
			{
				hSceneManager.male.ChaControl.eyeLookCtrl.enabled = true;
			}
			hSceneManager.male.ChaControl.ChangeLookNeckPtn(3);
			hSceneManager.male.ChaControl.ChangeLookNeckTarget(0);
			hSceneManager.male.ChaControl.ChangeLookEyesPtn(0);
			hSceneManager.male.ChaControl.ChangeLookEyesTarget(0);
			Singleton<Game>.Instance.GetExpression(hSceneManager.male.ID, "標準")?.Change(hSceneManager.male.ChaControl);
			hSceneManager.male.ChaControl.ChangeMouthOpenMin(hSceneManager.male.ChaControl.fileStatus.mouthOpenMin);
		}
		if (hSceneManager.females != null)
		{
			for (int j = 0; j < hSceneManager.females.Length; j++)
			{
				if (!(hSceneManager.females[j] == null))
				{
					if (!hSceneManager.females[j].ChaControl.neckLookCtrl.enabled)
					{
						hSceneManager.females[j].ChaControl.neckLookCtrl.enabled = true;
					}
					if (!hSceneManager.females[j].ChaControl.eyeLookCtrl.enabled)
					{
						hSceneManager.females[j].ChaControl.eyeLookCtrl.enabled = true;
					}
					hSceneManager.females[j].ChaControl.ChangeLookNeckPtn(3);
					hSceneManager.females[j].ChaControl.ChangeLookNeckTarget(0);
					hSceneManager.females[j].ChaControl.ChangeLookEyesPtn(0);
					hSceneManager.females[j].ChaControl.ChangeLookEyesTarget(0);
					int personality = 0;
					if ((bool)hSceneManager.females[j].GetComponent<PlayerActor>())
					{
						personality = -100;
					}
					else if (hSceneManager.females[j].GetComponent<MerchantActor>() == null)
					{
						personality = hSceneManager.females[j].ChaControl.fileParam.personality;
					}
					Singleton<Game>.Instance.GetExpression(personality, "標準")?.Change(hSceneManager.females[j].ChaControl);
					hSceneManager.females[j].ChaControl.ChangeMouthOpenMin(hSceneManager.females[j].ChaControl.fileStatus.mouthOpenMin);
				}
			}
		}
		for (int k = 0; k < chaMales.Length; k++)
		{
			if (!(chaMales[k] == null) && !(chaMales[k].objBody == null))
			{
				chaMales[k].visibleSon = false;
			}
		}
		ctrlFlag.cameraCtrl.Reset(0);
		Camera.enabled = true;
		Camera.EnabledInput = true;
		Camera.Mode = CameraMode.Normal;
		AnimalBase.CreateDisplay = true;
		AnimalManager instance = Singleton<AnimalManager>.Instance;
		int index = 0;
		for (int l = 0; l < instance.Animals.Count; l++)
		{
			instance.Animals[index].BodyEnabled = true;
			instance.Animals[index++].enabled = true;
		}
		instance.SettingAnimalPointBehavior();
		ctrlFlag.selectAnimationListInfo = null;
		if (ctrlItem != null)
		{
			ctrlItem.ReleaseItem();
		}
		ctrlFlag.cameraCtrl.visibleForceVanish(_visible: true);
		ctrlMeta.Clear();
		hPointCtrl.MarkerObjDel();
		hPointCtrl.ExistSecondfemale = false;
		if (ctrlFlag.nowHPoint.endPlayerPos != null)
		{
			hSceneManager.Player.ActivateNavMeshAgent();
			hSceneManager.Player.NavMeshAgent.Warp(ctrlFlag.nowHPoint.endPlayerPos.position);
			hSceneManager.Player.ChaControl.transform.position = Vector3.zero;
			hSceneManager.Player.ChaControl.animBody.transform.position = hSceneManager.Player.NavMeshAgent.transform.position;
			hSceneManager.Player.Rotation = ctrlFlag.nowHPoint.endPlayerPos.rotation;
		}
		else
		{
			hSceneManager.Player.ActivateNavMeshAgent();
			hSceneManager.Player.NavMeshAgent.Warp(ctrlFlag.nowHPoint.transform.position);
			hSceneManager.Player.ChaControl.transform.position = Vector3.zero;
			hSceneManager.Player.ChaControl.animBody.transform.position = hSceneManager.Player.NavMeshAgent.transform.position;
			hSceneManager.Player.Rotation = ctrlFlag.nowHPoint.transform.rotation;
		}
		bool flag = false;
		if (ctrlFlag.nowHPoint.endFemalePos != null)
		{
			if (!hSceneManager.bMerchant)
			{
				for (int m = 0; m < 2; m++)
				{
					if (hSceneManager.females[m] == null || hSceneManager.females[m].GetComponent<PlayerActor>() != null)
					{
						continue;
					}
					if (m > 0 && ctrlFlag.nowHPoint.endFemalePos.Length <= 1)
					{
						hSceneManager.females[m].ActivateNavMeshAgent();
						hSceneManager.females[m].NavMeshAgent.Warp(ctrlFlag.nowHPoint.transform.position);
						hSceneManager.females[m].ChaControl.transform.position = Vector3.zero;
						hSceneManager.females[m].ChaControl.animBody.transform.position = hSceneManager.females[m].NavMeshAgent.transform.position;
						hSceneManager.females[m].Rotation = ctrlFlag.nowHPoint.transform.rotation;
						continue;
					}
					if (ctrlFlag.nowHPoint.endFemalePos[m] != null)
					{
						hSceneManager.females[m].ActivateNavMeshAgent();
						hSceneManager.females[m].NavMeshAgent.Warp(ctrlFlag.nowHPoint.endFemalePos[m].position);
						hSceneManager.females[m].ChaControl.transform.position = Vector3.zero;
						hSceneManager.females[m].ChaControl.animBody.transform.position = hSceneManager.females[m].NavMeshAgent.transform.position;
						hSceneManager.females[m].Rotation = ctrlFlag.nowHPoint.endFemalePos[m].rotation;
					}
					else
					{
						hSceneManager.females[m].ActivateNavMeshAgent();
						hSceneManager.females[m].NavMeshAgent.Warp(ctrlFlag.nowHPoint.transform.position);
						hSceneManager.females[m].ChaControl.transform.position = Vector3.zero;
						hSceneManager.females[m].ChaControl.animBody.transform.position = hSceneManager.females[m].NavMeshAgent.transform.position;
						hSceneManager.females[m].Rotation = ctrlFlag.nowHPoint.transform.rotation;
					}
					hSceneManager.females[m].Locomotor.transform.LookAt(hSceneManager.Player.Locomotor.transform);
				}
			}
			else
			{
				if (ctrlFlag.nowHPoint.endFemalePos[0] != null)
				{
					hSceneManager.merchantActor.ActivateNavMeshAgent();
					hSceneManager.merchantActor.NavMeshAgent.Warp(ctrlFlag.nowHPoint.endFemalePos[0].position);
					hSceneManager.merchantActor.ChaControl.transform.position = Vector3.zero;
					hSceneManager.merchantActor.ChaControl.animBody.transform.position = hSceneManager.merchantActor.NavMeshAgent.transform.position;
					hSceneManager.merchantActor.Rotation = ctrlFlag.nowHPoint.endFemalePos[0].rotation;
				}
				else
				{
					hSceneManager.merchantActor.ActivateNavMeshAgent();
					hSceneManager.merchantActor.NavMeshAgent.Warp(ctrlFlag.nowHPoint.transform.position);
					hSceneManager.merchantActor.ChaControl.transform.position = Vector3.zero;
					hSceneManager.merchantActor.ChaControl.animBody.transform.position = hSceneManager.merchantActor.NavMeshAgent.transform.position;
					hSceneManager.merchantActor.Rotation = ctrlFlag.nowHPoint.transform.rotation;
				}
				if (hSceneManager.Agent[1] != null)
				{
					if (ctrlFlag.nowHPoint.endFemalePos[1] != null && ctrlFlag.nowHPoint.endFemalePos.Length > 1)
					{
						hSceneManager.Agent[1].ActivateNavMeshAgent();
						hSceneManager.Agent[1].NavMeshAgent.Warp(ctrlFlag.nowHPoint.endFemalePos[1].position);
						hSceneManager.Agent[1].ChaControl.transform.position = Vector3.zero;
						hSceneManager.Agent[1].ChaControl.animBody.transform.position = hSceneManager.Agent[1].NavMeshAgent.transform.position;
						hSceneManager.Agent[1].Rotation = ctrlFlag.nowHPoint.endFemalePos[1].rotation;
					}
					else
					{
						hSceneManager.Agent[1].ActivateNavMeshAgent();
						hSceneManager.Agent[1].NavMeshAgent.Warp(ctrlFlag.nowHPoint.transform.position);
						hSceneManager.Agent[1].ChaControl.transform.position = Vector3.zero;
						hSceneManager.Agent[1].ChaControl.animBody.transform.position = hSceneManager.Agent[1].NavMeshAgent.transform.position;
						hSceneManager.Agent[1].Rotation = ctrlFlag.nowHPoint.transform.rotation;
					}
				}
				hSceneManager.merchantActor.Locomotor.transform.LookAt(hSceneManager.Player.Locomotor.transform);
				if (hSceneManager.Agent[1] != null)
				{
					hSceneManager.Agent[1].Locomotor.transform.LookAt(hSceneManager.Player.Locomotor.transform);
				}
			}
		}
		hSceneManager.Player.ChaControl.visibleAll = false;
		if (!hSceneManager.bMerchant)
		{
			if (ctrlFlag.numOrgasmTotal < ctrlFlag.gotoFaintnessCount)
			{
				if (!hSceneManager.isForce)
				{
					ctrlFlag.AddParam(3, 0);
				}
				else
				{
					ctrlFlag.AddParam(5, 0);
					if (hSceneManager.HSkil.ContainsValue(9))
					{
						ctrlFlag.AddSkileParam(9);
					}
				}
			}
			if (ctrlFlag.numOutSide > 0)
			{
				ctrlFlag.AddParam(11, 1);
				if (hSceneManager.HSkil.ContainsValue(8))
				{
					ctrlFlag.AddSkileParam(8);
				}
			}
			if (ctrlFlag.numInside > 0)
			{
				ctrlFlag.AddParam(12, 1);
				if (hSceneManager.HSkil.ContainsValue(5))
				{
					ctrlFlag.AddSkileParam(5);
				}
			}
			if (ctrlFlag.numSameOrgasm > 0)
			{
				ctrlFlag.AddParam(13, 1);
				if (hSceneManager.HSkil.ContainsValue(2))
				{
					ctrlFlag.AddSkileParam(2);
				}
			}
			if (ctrlFlag.numAibu > 0)
			{
				ctrlFlag.AddParam(0, 1);
			}
			if (ctrlFlag.numHoushi > 0)
			{
				ctrlFlag.AddParam(1, 1);
				if (hSceneManager.HSkil.ContainsValue(4))
				{
					ctrlFlag.AddSkileParam(4);
				}
			}
			if (ctrlFlag.numSonyu > 0)
			{
				ctrlFlag.AddParam(2, 1);
			}
			if (ctrlFlag.numLes > 0)
			{
				ctrlFlag.AddParam(7, 1);
				if (hSceneManager.HSkil.ContainsValue(12))
				{
					ctrlFlag.AddSkileParam(12);
				}
			}
			if (ctrlFlag.numUrine > 0 && hSceneManager.HSkil.ContainsValue(14))
			{
				ctrlFlag.AddSkileParam(14);
			}
			if (ctrlFlag.numLeadFemale > 0)
			{
				ctrlFlag.AddParam(4, 1);
				if (hSceneManager.HSkil.ContainsValue(10))
				{
					ctrlFlag.AddSkileParam(10);
				}
			}
			if (ctrlFlag.isPainActionParam)
			{
				ctrlFlag.AddParam(3, 1);
			}
			if (hSceneManager.isForce)
			{
				ctrlFlag.AddParam(5, 1);
			}
			if (ctrlFlag.numFaintness > 0)
			{
				ctrlFlag.AddParam(14, 1);
				if (hSceneManager.HSkil.ContainsValue(20))
				{
					ctrlFlag.AddSkileParam(20);
				}
			}
			if (ctrlFlag.numOrgasmTotal <= 0 && hSceneManager.HSkil.ContainsValue(21))
			{
				ctrlFlag.AddSkileParam(21);
			}
			if (ctrlFlag.isNotCtrl)
			{
				ctrlFlag.AddParam(16, 1);
			}
			if (ctrlFlag.isFemaleNaked)
			{
				ctrlFlag.AddParam(34, 1);
			}
			foreach (KeyValuePair<int, int> changeParam in ctrlFlag.ChangeParams)
			{
				hSceneManager.SetParamator(changeParam.Key, changeParam.Value);
			}
		}
		hSceneManager.maleFinish = ctrlFlag.numInside + ctrlFlag.numOutSide + ctrlFlag.numDrink + ctrlFlag.numVomit;
		hSceneManager.femalePlayerFinish = ctrlFlag.numOrgasmFemalePlayer;
		hSceneManager.femaleFinish = ctrlFlag.numOrgasmTotal;
		hSceneManager.endStatus = (byte)(ctrlFlag.isFaintness ? 1u : 0u);
		hSceneManager.isCtrl = !ctrlFlag.isNotCtrl;
		EndProcADV();
		hSceneManager.merchantActor = null;
	}

	private void LateUpdate()
	{
		LotionProc();
		if (ctrlFlag.BeforeHWait)
		{
			if (preBeforWaitState != null && preBeforWaitState.Count > 0)
			{
				if (preBeforWaitState.ContainsKey(0))
				{
					int shortNameHash = hSceneManager.Player.ChaControl.animBody.GetCurrentAnimatorStateInfo(0).shortNameHash;
					if (preBeforWaitState[0] != shortNameHash)
					{
						hSceneManager.Player.Animation.MapIK.Calc(shortNameHash);
						preBeforWaitState[0] = shortNameHash;
					}
				}
				for (int i = 0; i < hSceneManager.Agent.Length; i++)
				{
					if (!(hSceneManager.Agent[i] == null) && !(hSceneManager.Agent[i].ChaControl == null) && !(hSceneManager.Agent[i].ChaControl.animBody == null) && preBeforWaitState.ContainsKey(1 + i))
					{
						int shortNameHash = hSceneManager.Agent[i].ChaControl.animBody.GetCurrentAnimatorStateInfo(0).shortNameHash;
						if (preBeforWaitState[1 + i] != shortNameHash)
						{
							hSceneManager.Agent[i].Animation.MapIK.Calc(shortNameHash);
							preBeforWaitState[1 + i] = shortNameHash;
						}
					}
				}
			}
			prevBeforeWait = ctrlFlag.BeforeHWait;
			return;
		}
		if (prevBeforeWait && !ctrlFlag.BeforeHWait)
		{
			sprite.endFade = 0;
			prevBeforeWait = ctrlFlag.BeforeHWait;
		}
		if (!ProcBase.endInit)
		{
			return;
		}
		bool flag = Singleton<Game>.Instance.Dialog != null;
		bool activeSelf = hSceneManager.HSceneUISet.activeSelf;
		if (NowStateIsEnd)
		{
			if (!flag && activeSelf)
			{
				hSceneManager.HSceneUISet.SetActive(value: false);
			}
			return;
		}
		if (chaFemales[0].GetNowClothesType() == 3)
		{
			ctrlFlag.isFemaleNaked = true;
		}
		HSystem hData = Config.HData;
		List<int> list = new List<int>();
		list.Add(0);
		list.Add(2);
		list.Add(1);
		list.Add(3);
		list.Add(5);
		list.Add(6);
		foreach (int item in list)
		{
			if (hSceneManager.Player.ChaControl.IsClothesStateKind(item))
			{
				byte state = 0;
				if (!hData.Cloth)
				{
					state = 2;
				}
				hSceneManager.Player.ChaControl.SetClothesState(item, state);
			}
		}
		hSceneManager.Player.ChaControl.SetAccessoryStateAll(hData.Accessory);
		hSceneManager.Player.ChaControl.SetClothesState(7, (byte)((!hData.Shoes) ? 2u : 0u));
		ctrlFlag.semenType = Config.HData.Siru;
		if (Camera != null)
		{
			Camera.AmbientLight = Config.GraphicData.AmbientLight;
		}
		ctrlFlag.cameraCtrl.ConfigVanish = Config.GraphicData.Shield;
		SyncAnimation();
	}

	private void LotionProc()
	{
		if (!useLotion)
		{
			return;
		}
		float num = 100f;
		num = 100f;
		for (int i = 0; i < hSceneManager.Agent.Length; i++)
		{
			if (!(hSceneManager.Agent[i] == null))
			{
				AgentData agentData = hSceneManager.Agent[i].AgentData;
				if (agentData != null)
				{
					agentData.Wetness = num;
					float wetRate = Mathf.InverseLerp(0f, 100f, agentData.Wetness);
					hSceneManager.Agent[i].ChaControl.wetRate = wetRate;
				}
			}
		}
		if (hSceneManager.bMerchant && hSceneManager.merchantActor != null)
		{
			MerchantData merchantData = hSceneManager.merchantActor.MerchantData;
			if (merchantData != null)
			{
				merchantData.Wetness = num;
				float wetRate2 = Mathf.InverseLerp(0f, 100f, merchantData.Wetness);
				hSceneManager.merchantActor.ChaControl.wetRate = wetRate2;
			}
		}
	}

	public void ConfigEnd()
	{
		ctrlFlag.click = HSceneFlagCtrl.ClickKind.None;
		NowStateIsEnd = true;
		sprite.ReSetLight();
		Camera.EnableUpdateCustomLight();
		Singleton<Manager.Sound>.Instance.Listener = Singleton<Manager.Map>.Instance.Player.CameraControl.transform;
		Singleton<Manager.Resources>.Instance.HSceneTable.HMeshObjDic[Singleton<Manager.Map>.Instance.MapID].SetActive(value: true);
		hSceneManager.CameraMesh?.SetActive(value: true);
		EndAnimChange();
		hSceneManager.endStatus = (byte)(ctrlFlag.isFaintness ? 1u : 0u);
		chaFemales[0].SetClothesStateAll(0);
		if (ctrlEyeNeckFemale[0] != null && chaFemales[0] != null && chaFemales[0].objBodyBone != null)
		{
			ctrlEyeNeckFemale[0].NowEndADV = true;
		}
		if (ctrlEyeNeckFemale[1] != null && chaFemales[1] != null && chaFemales[1].objBodyBone != null)
		{
			if (hSceneManager.Player.ChaControl.sex == 0)
			{
				ctrlEyeNeckFemale[1].NowEndADV = true;
			}
			else if (hSceneManager.Player.ChaControl.sex == 1)
			{
				if (ctrlFlag.bFutanari)
				{
					ctrlEyeNeckFemale[1].NowEndADV = true;
				}
				else
				{
					hMotionEyeNeckLesP.NowEndADV = true;
				}
			}
		}
		if (ctrlEyeNeckMale[0] != null && chaMales[0] != null && chaMales[0].objBodyBone != null)
		{
			ctrlEyeNeckMale[0].NowEndADV = true;
		}
		if (ctrlEyeNeckMale[1] != null && chaMales[1] != null && chaMales[1].objBodyBone != null)
		{
			ctrlEyeNeckMale[1].NowEndADV = true;
		}
		foreach (AgentActor value in Singleton<Manager.Map>.Instance.AgentTable.Values)
		{
			if (!(value == null) && hSceneManager.ReturnActionTypes.ContainsKey(value))
			{
				value.EnableEntity();
			}
		}
		MerchantActor merchant = Singleton<Manager.Map>.Instance.Merchant;
		if (hSceneManager.bMerchant)
		{
			if (!merchant.ChaControl.neckLookCtrl.enabled)
			{
				merchant.ChaControl.neckLookCtrl.enabled = true;
			}
			if (!merchant.ChaControl.eyeLookCtrl.enabled)
			{
				merchant.ChaControl.eyeLookCtrl.enabled = true;
			}
			merchant.ChaControl.ChangeLookNeckPtn(3);
			merchant.ChaControl.ChangeLookNeckTarget(0);
			merchant.ChaControl.ChangeLookEyesPtn(0);
			merchant.ChaControl.ChangeLookEyesTarget(0);
			Singleton<Game>.Instance.GetExpression(merchant.ID, "標準")?.Change(merchant.ChaControl);
			merchant.ChaControl.ChangeMouthOpenMin(merchant.ChaControl.fileStatus.mouthOpenMin);
		}
		int shortNameHash = merchant.ChaControl.animBody.GetCurrentAnimatorStateInfo(0).shortNameHash;
		merchant.AnimationMerchant.SetAnimatorController(merchant.ChaControl.animBody.runtimeAnimatorController);
		merchant.AnimationMerchant.MapIK.Calc(shortNameHash);
		merchant.EnableEntity();
		if (hSceneManager.male != null)
		{
			if (!hSceneManager.male.ChaControl.neckLookCtrl.enabled)
			{
				hSceneManager.male.ChaControl.neckLookCtrl.enabled = true;
			}
			if (!hSceneManager.male.ChaControl.eyeLookCtrl.enabled)
			{
				hSceneManager.male.ChaControl.eyeLookCtrl.enabled = true;
			}
			hSceneManager.male.ChaControl.ChangeLookNeckPtn(3);
			hSceneManager.male.ChaControl.ChangeLookNeckTarget(0);
			hSceneManager.male.ChaControl.ChangeLookEyesPtn(0);
			hSceneManager.male.ChaControl.ChangeLookEyesTarget(0);
			Singleton<Game>.Instance.GetExpression(hSceneManager.male.ID, "標準")?.Change(hSceneManager.male.ChaControl);
			hSceneManager.male.ChaControl.ChangeMouthOpenMin(hSceneManager.male.ChaControl.fileStatus.mouthOpenMin);
		}
		if (hSceneManager.females != null)
		{
			for (int i = 0; i < hSceneManager.females.Length; i++)
			{
				if (!(hSceneManager.females[i] == null))
				{
					if (!hSceneManager.females[i].ChaControl.neckLookCtrl.enabled)
					{
						hSceneManager.females[i].ChaControl.neckLookCtrl.enabled = true;
					}
					if (!hSceneManager.females[i].ChaControl.eyeLookCtrl.enabled)
					{
						hSceneManager.females[i].ChaControl.eyeLookCtrl.enabled = true;
					}
					hSceneManager.females[i].ChaControl.ChangeLookNeckPtn(3);
					hSceneManager.females[i].ChaControl.ChangeLookNeckTarget(0);
					hSceneManager.females[i].ChaControl.ChangeLookEyesPtn(0);
					hSceneManager.females[i].ChaControl.ChangeLookEyesTarget(0);
					int personality = 0;
					if ((bool)hSceneManager.females[i].GetComponent<PlayerActor>())
					{
						personality = -100;
					}
					else if (hSceneManager.females[i].GetComponent<MerchantActor>() == null)
					{
						personality = hSceneManager.females[i].ChaControl.fileParam.personality;
					}
					Singleton<Game>.Instance.GetExpression(personality, "標準")?.Change(hSceneManager.females[i].ChaControl);
					hSceneManager.females[i].ChaControl.ChangeMouthOpenMin(hSceneManager.females[i].ChaControl.fileStatus.mouthOpenMin);
				}
			}
		}
		for (int j = 0; j < chaMales.Length; j++)
		{
			if (!(chaMales[j] == null) && !(chaMales[j].objBody == null))
			{
				chaMales[j].visibleSon = false;
			}
		}
		ctrlFlag.cameraCtrl.Reset(0);
		Camera.enabled = true;
		Camera.EnabledInput = true;
		Camera.Mode = CameraMode.Normal;
		AnimalBase.CreateDisplay = true;
		AnimalManager instance = Singleton<AnimalManager>.Instance;
		int index = 0;
		for (int k = 0; k < instance.Animals.Count; k++)
		{
			instance.Animals[index].BodyEnabled = true;
			instance.Animals[index++].enabled = true;
		}
		instance.SettingAnimalPointBehavior();
		ctrlFlag.selectAnimationListInfo = null;
		if (ctrlItem != null)
		{
			ctrlItem.ReleaseItem();
		}
		ctrlFlag.cameraCtrl.visibleForceVanish(_visible: true);
		ctrlMeta.Clear();
		hPointCtrl.MarkerObjDel();
		hPointCtrl.ExistSecondfemale = false;
		CloseADV();
		hSceneManager.HSceneUISet.SetActive(value: false);
		hSceneManager.merchantActor = null;
	}

	private void OnDisable()
	{
		if (nowStart)
		{
			return;
		}
		if ((bool)ctrlFlag && (bool)ctrlFlag.cameraCtrl)
		{
			ctrlFlag.cameraCtrl.visibleForceVanish(_visible: true);
			ctrlFlag.cameraCtrl.ResetVanish();
		}
		if (Singleton<Manager.Housing>.IsInstance())
		{
			Singleton<Manager.Housing>.Instance.EndShield();
		}
		hSceneManager.isForce = false;
		for (int i = 0; i < 2; i++)
		{
			if (ctrlDynamics[i] != null)
			{
				ctrlDynamics[i].InitDynamicBoneReferenceBone();
			}
		}
		if (ctrlVoice != null)
		{
			ctrlVoice.FaceReset(chaFemales[0]);
			if (chaFemales[1] != null)
			{
				ctrlVoice.FaceReset(chaFemales[1]);
			}
		}
		for (int j = 0; j < ctrlYures.Length; j++)
		{
			if (ctrlYures[j] != null)
			{
				ctrlYures[j].ResetShape();
			}
		}
		if (ctrlYureMale != null)
		{
			ctrlYureMale.ResetShape();
		}
		if (ctrlSiruPastes != null)
		{
			SiruPasteCtrl[] array = ctrlSiruPastes;
			foreach (SiruPasteCtrl siruPasteCtrl in array)
			{
				siruPasteCtrl.Release();
			}
		}
		ChaControl[] array2 = chaFemales;
		foreach (ChaControl chaControl in array2)
		{
			if (!(chaControl == null) && !(chaControl.objBody == null))
			{
				chaControl.ChangeBustInert(h: false);
				chaControl.playDynamicBoneBust(0, play: true);
				chaControl.playDynamicBoneBust(1, play: true);
				chaControl.fileStatus.skinTuyaRate = 0f;
				chaControl.ChangeEyesOpenMax(1f);
				FBSCtrlMouth mouthCtrl = chaControl.mouthCtrl;
				if (mouthCtrl != null)
				{
					mouthCtrl.OpenMin = 0f;
				}
				chaControl.SetAccessoryStateAll(show: true);
				chaControl.SetClothesStateAll(0);
				for (int m = 0; m < 5; m++)
				{
					ChaFileDefine.SiruParts parts = (ChaFileDefine.SiruParts)m;
					chaControl.SetSiruFlag(parts, 0);
				}
				chaControl.DisableShapeMouth(disable: false);
				for (int n = 0; n < 7; n++)
				{
					int id = n;
					chaControl.DisableShapeBodyID(2, id, disable: false);
				}
				chaControl.DisableShapeBodyID(2, 7, disable: false);
				chaControl.ReleaseHitObject();
			}
		}
		for (int num = 0; num < chaFemales.Length; num++)
		{
			if (!(chaFemales[num] == null) && !(chaFemales[num].objBody == null))
			{
				float value = 0f;
				if (initStandNip.TryGetValue(num, out value))
				{
					chaFemales[num].ChangeNipRate(value);
				}
			}
		}
		for (int num2 = 0; num2 < chaMales.Length; num2++)
		{
			if (!(chaMales[num2] == null) && !(chaMales[num2].objBody == null))
			{
				chaMales[num2].ReleaseHitObject();
			}
		}
		initStandNip.Clear();
		if (CtrlParticle != null)
		{
			CtrlParticle.RePlaceObject();
		}
		for (int num3 = 0; num3 < ctrlHitObjectFemales.Length; num3++)
		{
			if (ctrlHitObjectFemales[num3] != null)
			{
				ctrlHitObjectFemales[num3].ReleaseObject();
			}
		}
		for (int num4 = 0; num4 < ctrlHitObjectMales.Length; num4++)
		{
			if (ctrlHitObjectMales[num4] != null)
			{
				ctrlHitObjectMales[num4].ReleaseObject();
			}
		}
		if ((bool)objGrondCollision)
		{
			UnityEngine.Object.Destroy(objGrondCollision);
			objGrondCollision = null;
		}
		if (Singleton<Manager.Sound>.IsInstance())
		{
			Singleton<Manager.Sound>.Instance.Stop(Manager.Sound.Type.GameSE2D);
			Singleton<Manager.Sound>.Instance.Stop(Manager.Sound.Type.GameSE3D);
		}
		if (Singleton<Voice>.IsInstance())
		{
			Singleton<Voice>.Instance.StopAll();
		}
		ctrlVoice.HBeforeHouchiTime = 0f;
		hSceneManager.bMerchant = false;
		if (Singleton<GameCursor>.IsInstance())
		{
			Singleton<GameCursor>.Instance.SetCursorLock(setLockFlag: false);
		}
		AssetBundleManager.UnloadAssetBundle(hSceneManager.strAssetSE, isUnloadForceRefCount: true);
		for (int num5 = 0; num5 < ctrlFlag.voice.lstUseAsset.Count; num5++)
		{
			AssetBundleManager.UnloadAssetBundle(ctrlFlag.voice.lstUseAsset[num5], isUnloadForceRefCount: true);
		}
		if (Singleton<HSceneManager>.IsInstance())
		{
			foreach (string item in hSceneManager.hashUseAssetBundle)
			{
				AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: true);
			}
			hSceneManager.hashUseAssetBundle.Clear();
		}
		ctrlLayer.Release();
		isSetStartPos = false;
		mode = -1;
		modeCtrl = -1;
		nowStart = false;
		nullPlayer = true;
		NowStateIsEnd = false;
		nowChangeAnim = false;
		lstMotionIK.Clear();
		Singleton<HPointCtrl>.Instance.endHScene();
		lstProc.Clear();
		packData = null;
		hSceneManager.choiceDisposable?.Dispose();
		hSceneManager.choiceDisposable = null;
		hSceneManager.nInvitePtn = -1;
		HSceneManager.SleepStart = false;
		hSceneManager.Agent[0] = null;
		hSceneManager.Agent[1] = null;
		useLotion = false;
		if (hSceneManager.Player != null && hSceneManager.Player.ChaControl.sex == 1 && hSceneManager.bFutanari)
		{
			hSceneManager.Player.ChaControl.ChangeBustInert(h: false);
		}
		hSceneManager.numFemaleClothCustom = 0;
		hSceneManager.MerchantLimit = -1;
		hSceneManager.EndHScene();
		ctrlFlag.isJudgeSelect.Clear();
	}

	public void HParticleSetNull()
	{
		ctrlParitcle = null;
	}

	private void CreateListAnimationFileName()
	{
		lstAnimInfo = Singleton<Manager.Resources>.Instance.HSceneTable.lstAnimInfo;
	}

	private void SyncAnimation()
	{
		if (chaFemales[0].animBody == null)
		{
			return;
		}
		AnimatorStateInfo animatorStateInfo = chaFemales[0].getAnimatorStateInfo(0);
		List<int> list = ctrlFlag.lstSyncAnimLayers[0, 0];
		for (int i = 1; i < ctrlFlag.lstSyncAnimLayers.GetLength(0); i++)
		{
			for (int j = 1; j < ctrlFlag.lstSyncAnimLayers.GetLength(1); j++)
			{
				for (int k = 0; k < ctrlFlag.lstSyncAnimLayers[i, j].Count; k++)
				{
					if (!list.Contains(ctrlFlag.lstSyncAnimLayers[i, j][k]))
					{
						list.Add(ctrlFlag.lstSyncAnimLayers[i, j][k]);
					}
				}
			}
		}
		ctrlItem.ParentScaleReject();
		if (chaMales[0] == null || chaMales[0].animBody == null || chaMales[0].objBodyBone == null)
		{
			if (!chaFemales[0].isBlend(0) && ctrlItem.GetItem() != null)
			{
				ctrlItem.syncPlay(animatorStateInfo);
				ctrlItem.Update();
			}
			if ((bool)chaFemales[1])
			{
				chaFemales[1].syncPlay(animatorStateInfo, 0);
			}
			if (list.Count == 0)
			{
				isSyncFirstStep = false;
				return;
			}
			if (!isSyncFirstStep)
			{
				isSyncFirstStep = true;
				return;
			}
			for (int l = 0; l < chaFemales.Length; l++)
			{
				if (chaFemales[l] == null || ((bool)chaFemales[l] && chaFemales[l].animBody == null))
				{
					continue;
				}
				for (int m = 0; m < ctrlFlag.lstSyncAnimLayers[1, l].Count; m++)
				{
					int num = ctrlFlag.lstSyncAnimLayers[1, l][m];
					if (chaFemales[l].animBody.layerCount > num)
					{
						chaFemales[l].syncPlay(animatorStateInfo, num);
					}
				}
			}
			return;
		}
		if (chaFemales[1] != null && chaFemales[1].objTop != null)
		{
			chaFemales[1].syncPlay(animatorStateInfo, 0);
		}
		for (int n = 0; n < chaMales.Length; n++)
		{
			if (chaMales[n] == null || chaMales[n].objTop == null)
			{
				continue;
			}
			for (int num2 = 0; num2 < ctrlFlag.lstSyncAnimLayers[0, n].Count; num2++)
			{
				int num3 = ctrlFlag.lstSyncAnimLayers[0, n][num2];
				if (chaMales[n].animBody.layerCount > num3)
				{
					chaMales[num2].syncPlay(animatorStateInfo, 0);
				}
			}
		}
		if (ctrlItem.GetItem() != null)
		{
			ctrlItem.syncPlay(animatorStateInfo);
			ctrlItem.Update();
		}
		if (ctrlFlag.lstSyncAnimLayers[1, 0].Count == 0)
		{
			isSyncFirstStep = false;
			return;
		}
		if (!isSyncFirstStep)
		{
			isSyncFirstStep = true;
			return;
		}
		bool flag = false;
		bool flag2 = false;
		for (int num4 = 0; num4 < chaFemales.Length; num4++)
		{
			if (chaFemales[num4] == null || ((bool)chaFemales[num4] && chaFemales[num4].animBody == null))
			{
				continue;
			}
			for (int num5 = 0; num5 < ctrlFlag.lstSyncAnimLayers[1, num4].Count; num5++)
			{
				int num6 = ctrlFlag.lstSyncAnimLayers[1, num4][num5];
				if (chaFemales[num4].animBody.layerCount > num6)
				{
					chaFemales[num4].syncPlay(animatorStateInfo, num6);
					flag = true;
				}
			}
		}
		for (int num7 = 0; num7 < chaMales.Length; num7++)
		{
			if (chaMales[num7] == null || ((bool)chaMales[num7] && chaMales[num7].animBody == null))
			{
				continue;
			}
			for (int num8 = 0; num8 < ctrlFlag.lstSyncAnimLayers[0, num7].Count; num8++)
			{
				int num9 = ctrlFlag.lstSyncAnimLayers[0, num7][num8];
				if (chaMales[num7].animBody.layerCount > num9)
				{
					chaMales[num7].syncPlay(animatorStateInfo, num9);
					flag2 = true;
				}
			}
		}
		if (flag)
		{
			for (int num10 = 0; num10 < chaFemales.Length; num10++)
			{
				if (!(chaFemales[num10] == null) && (!chaFemales[num10] || !(chaFemales[num10].animBody == null)))
				{
					chaFemales[num10].animBody.Update(0f);
				}
			}
		}
		if (flag2)
		{
			for (int num11 = 0; num11 < chaMales.Length; num11++)
			{
				if (!(chaMales[num11] == null) && (!chaMales[num11] || !(chaMales[num11].animBody == null)))
				{
					chaMales[num11].animBody.Update(0f);
				}
			}
		}
		for (int num12 = 0; num12 < ctrlEyeNeckFemale.Length; num12++)
		{
			if (!(ctrlEyeNeckFemale[num12] == null))
			{
				bool flag3 = false;
				flag3 = hSceneManager.Player.ChaControl.sex == 1 && !ctrlFlag.bFutanari;
				if (num12 == 0 || !flag3)
				{
					ctrlEyeNeckFemale[num12].EyeNeckCalc();
				}
				else
				{
					hMotionEyeNeckLesP.EyeNeckCalc();
				}
			}
		}
		for (int num13 = 0; num13 < ctrlEyeNeckMale.Length; num13++)
		{
			if (!(ctrlEyeNeckMale[num13] == null))
			{
				ctrlEyeNeckMale[num13].EyeNeckCalc();
			}
		}
	}

	public IEnumerator ChangeAnimation(AnimationListInfo _info, bool _isForceResetCamera, bool _isForceLoopAction = false, bool _UseFade = true)
	{
		GlobalMethod.setCameraMoveFlag(ctrlFlag.cameraCtrl, _bPlay: false);
		ctrlFlag.isInsert = false;
		ctrlFlag.isGaugeHit = false;
		ctrlFlag.isGaugeHit_M = false;
		AtariEffect.Stop();
		FeelHitEffect3D.Stop();
		nowChangeAnim = true;
		ProcBase.endInit = false;
		if (_info == null)
		{
			nowChangeAnim = false;
			ProcBase.endInit = true;
			yield break;
		}
		if (ctrlFlag.nowAnimationInfo == _info)
		{
			nowChangeAnim = false;
			ProcBase.endInit = true;
			yield break;
		}
		if (ctrlFlag.isFaintness && _info.nDownPtn == 0)
		{
			Debug.LogError("脱力中なのに脱力ができないモーションが選択された");
			nowChangeAnim = false;
			ProcBase.endInit = true;
			yield break;
		}
		bool[] isSpeek = new bool[2]
		{
			(ctrlVoice.nowVoices[0].state == HVoiceCtrl.VoiceKind.voice || ctrlVoice.nowVoices[0].state == HVoiceCtrl.VoiceKind.startVoice) && Singleton<Voice>.Instance.IsVoiceCheck(ctrlFlag.voice.voiceTrs[0]),
			false
		};
		if (chaFemales[1] != null && chaFemales[1].visibleAll && chaFemales[1].objBody != null)
		{
			isSpeek[1] = (ctrlVoice.nowVoices[1].state == HVoiceCtrl.VoiceKind.voice || ctrlVoice.nowVoices[1].state == HVoiceCtrl.VoiceKind.startVoice) && Singleton<Voice>.Instance.IsVoiceCheck(ctrlFlag.voice.voiceTrs[1]);
		}
		while (isSpeek[0] | isSpeek[1])
		{
			yield return null;
			isSpeek[0] = (ctrlVoice.nowVoices[0].state == HVoiceCtrl.VoiceKind.voice || ctrlVoice.nowVoices[0].state == HVoiceCtrl.VoiceKind.startVoice) && Singleton<Voice>.Instance.IsVoiceCheck(ctrlFlag.voice.voiceTrs[0]);
			if (chaFemales[1] != null && chaFemales[1].visibleAll && chaFemales[1].objBody != null)
			{
				isSpeek[1] = (ctrlVoice.nowVoices[1].state == HVoiceCtrl.VoiceKind.voice || ctrlVoice.nowVoices[1].state == HVoiceCtrl.VoiceKind.startVoice) && Singleton<Voice>.Instance.IsVoiceCheck(ctrlFlag.voice.voiceTrs[1]);
			}
		}
		sbLoadFileName.Clear();
		bool isIdle = IsIdle(chaFemales[0].animBody) || _info.nAnimListInfoID != ctrlFlag.nowAnimationInfo.nAnimListInfoID || _info.nBreathID != ctrlFlag.nowAnimationInfo.nBreathID;
		if (isIdle && _isForceLoopAction)
		{
			isIdle = false;
		}
		bool afterIdle = IsAfterIdle(chaFemales[0].animBody, 0);
		int oldMode = ctrlFlag.nowAnimationInfo.ActionCtrl.Item1;
		if (_info.ActionCtrl.Item1 < 3)
		{
			mode = _info.ActionCtrl.Item1;
		}
		else if (_info.ActionCtrl.Item1 == 3)
		{
			switch (_info.ActionCtrl.Item2)
			{
			case 0:
				mode = 2;
				break;
			case 1:
				mode = 2;
				break;
			case 2:
				mode = 3;
				break;
			case 3:
				mode = 0;
				break;
			case 4:
				mode = 0;
				break;
			case 5:
				mode = 4;
				break;
			case 6:
				mode = 5;
				break;
			}
		}
		else
		{
			mode = _info.ActionCtrl.Item1 + 2;
		}
		modeCtrl = _info.ActionCtrl.Item2;
		bool NumCharaChange = false;
		if (CtrlParticle != null)
		{
			CtrlParticle.RePlaceObject();
		}
		for (int s = 0; s < chaMales.Length; s++)
		{
			if (_info.fileMale != string.Empty && (s != 1 || _info.nPromiscuity == 0))
			{
				if (chaMales[s] == null)
				{
					continue;
				}
				if (chaMales[s].objTop == null)
				{
					chaMales[s].Load();
					chaMales[s].SetShapeBodyValue(0, 0.75f);
					NumCharaChange = true;
				}
				else
				{
					chaMales[s].visibleAll = true;
					NumCharaChange = true;
				}
				FrameCorrect.AddFrameCorrect(chaMales[s].animBody.gameObject);
				IKCorrect.AddIKCorrect(chaMales[s].animBody.gameObject);
				if (!ctrlHitObjectMales[s].isInit)
				{
					ctrlHitObjectMales[s].id = s;
					yield return ctrlHitObjectMales[s].HitObjInit(0, chaMales[s].objBodyBone, chaMales[s]);
				}
				ctrlHitObjectMales[s].setActiveObject(val: true);
				bool existChar = false;
				foreach (System.Tuple<int, int, MotionIK> item in lstMotionIK)
				{
					if (item.Item1 == 0 && item.Item2 == s)
					{
						existChar = true;
						break;
					}
				}
				if (!existChar)
				{
					lstMotionIK.Add(new System.Tuple<int, int, MotionIK>(0, s, new MotionIK(chaMales[s], isHscene: true)));
				}
			}
			else if (!(chaMales[s] == null) && chaMales[s].visibleAll && ctrlHitObjectMales[s] != null)
			{
				if (ctrlHitObjectMales[s].isInit)
				{
					ctrlHitObjectMales[s].setActiveObject(val: false);
				}
				chaMales[s].visibleAll = false;
				NumCharaChange = true;
				chaMales[s].ReleaseHitObject();
				ctrlMaleCollisionCtrls[s].Release();
				ctrlFlag.lstSyncAnimLayers[0, s].Clear();
				ctrlFlag.lstSyncAnimLayers[0, s].Clear();
			}
		}
		for (int i = 0; i < chaFemales.Length; i++)
		{
			if (chaFemales[i] == null)
			{
				continue;
			}
			if (i != 1 || _info.nPromiscuity >= 1)
			{
				if (chaFemales[i].objTop == null)
				{
					chaFemales[i].Load();
					NumCharaChange = true;
				}
				else
				{
					chaFemales[i].visibleAll = true;
					NumCharaChange = true;
				}
				FrameCorrect.AddFrameCorrect(chaFemales[i].animBody.gameObject);
				IKCorrect.AddIKCorrect(chaFemales[i].animBody.gameObject);
				bool flag = false;
				foreach (System.Tuple<int, int, MotionIK> item2 in lstMotionIK)
				{
					if (item2.Item1 == 1 && item2.Item2 == i)
					{
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					lstMotionIK.Add(new System.Tuple<int, int, MotionIK>(1, i, new MotionIK(chaFemales[i], isHscene: true)));
				}
			}
			else if (!(chaFemales[i] == null) && chaFemales[i].visibleAll)
			{
				chaFemales[i].visibleAll = false;
				NumCharaChange = true;
				chaFemales[i].ReleaseHitObject();
				ctrlFemaleCollisionCtrls[i].Release();
				ctrlVoice.nowVoices[i].state = HVoiceCtrl.VoiceKind.breath;
				ctrlFlag.voice.playShorts[i] = -1;
				ctrlFlag.voice.playVoices[i] = false;
				ctrlFlag.lstSyncAnimLayers[1, i].Clear();
				ctrlFlag.lstSyncAnimLayers[1, i].Clear();
			}
		}
		for (int j = 0; j < lstMotionIK.Count; j++)
		{
			lstMotionIK[j].Item3.MapIK = false;
			lstMotionIK[j].Item3.SetPartners(lstMotionIK);
			lstMotionIK[j].Item3.Reset();
		}
		abName = GlobalMethod.GetAssetBundleNameListFromPath(hSceneManager.strAssetIKListFolder);
		yield return null;
		AnimationListInfo tmpAddInfo = null;
		racM[0] = null;
		racM[1] = null;
		HoushiRacM[0] = null;
		HoushiRacM[1] = null;
		hashUseAssetBundleAnimator.Clear();
		for (int s2 = 0; s2 < chaMales.Length; s2++)
		{
			if (chaMales[s2] == null || chaMales[s2].animBody == null || _info.fileMale == string.Empty)
			{
				continue;
			}
			if (_info.id == -1)
			{
				chaMales[s2].LoadAnimation(_info.assetpathMale, _info.fileMale, string.Empty);
			}
			else
			{
				if (_info.ActionCtrl.Item1 < 3)
				{
					if (mode == 1 && _info.nDownPtn == 1)
					{
						foreach (AnimationListInfo tmpInfo in lstAnimInfo[1])
						{
							bool placeCheck = false;
							foreach (int nPositon in tmpInfo.nPositons)
							{
								if (!_info.nPositons.Contains(nPositon))
								{
									continue;
								}
								placeCheck = true;
								break;
							}
							if (_info.ActionCtrl.Item2 != tmpInfo.ActionCtrl.Item2 || !placeCheck || tmpInfo.nFaintnessLimit != 1)
							{
								continue;
							}
							sbLoadFileName.Clear();
							sbLoadFileName.Append(tmpInfo.fileMale);
							HoushiRacM[s2] = CommonLib.LoadAsset<RuntimeAnimatorController>(tmpInfo.assetpathMale, sbLoadFileName.ToString(), clone: false, string.Empty);
							hashUseAssetBundleAnimator.Add(tmpInfo.assetpathMale);
							yield return null;
							tmpAddInfo = tmpInfo;
							break;
						}
					}
					sbLoadFileName.Clear();
					sbLoadFileName.Append(_info.fileMale);
					racM[s2] = CommonLib.LoadAsset<RuntimeAnimatorController>(_info.assetpathMale, sbLoadFileName.ToString(), clone: false, string.Empty);
					yield return null;
				}
				else
				{
					if (!racEtcM.ContainsKey(_info.assetBaseM))
					{
						racEtcM.Add(_info.assetBaseM, CommonLib.LoadAsset<RuntimeAnimatorController>(_info.assetpathBaseM, _info.assetBaseM, clone: false, string.Empty));
						hashUseAssetBundleAnimator.Add(_info.assetpathBaseM);
					}
					sbLoadFileName.Clear();
					sbLoadFileName.AppendFormat("{0}{1}", _info.fileMale, string.Empty);
					racM[s2] = CommonLib.LoadAsset<RuntimeAnimatorController>(_info.assetpathMale, sbLoadFileName.ToString(), clone: false, string.Empty);
				}
				ctrlLayer.LoadExcel(sbLoadFileName.ToString(), 0, s2);
			}
			hashUseAssetBundleAnimator.Add(_info.assetpathMale);
			yield return null;
		}
		racF[0] = null;
		racF[1] = null;
		HoushiRacF[0] = null;
		HoushiRacF[1] = null;
		for (int s3 = 0; s3 < chaFemales.Length; s3++)
		{
			if (chaFemales[s3] == null || chaFemales[s3].animBody == null)
			{
				continue;
			}
			if (_info.id == -1)
			{
				chaFemales[s3].LoadAnimation(_info.assetpathFemale, _info.fileFemale, string.Empty);
			}
			else
			{
				if (_info.ActionCtrl.Item1 < 3)
				{
					if (mode == 1 && _info.nDownPtn == 1)
					{
						if (tmpAddInfo == null)
						{
							foreach (AnimationListInfo tmpInfo2 in lstAnimInfo[1])
							{
								bool placeCheck2 = false;
								foreach (int nPositon2 in tmpInfo2.nPositons)
								{
									if (!_info.nPositons.Contains(nPositon2))
									{
										continue;
									}
									placeCheck2 = true;
									break;
								}
								if (_info.ActionCtrl.Item2 != tmpInfo2.ActionCtrl.Item2 || placeCheck2 || tmpInfo2.nFaintnessLimit != 1)
								{
									continue;
								}
								sbLoadFileName.Clear();
								sbLoadFileName.Append(tmpInfo2.fileFemale);
								HoushiRacF[s3] = CommonLib.LoadAsset<RuntimeAnimatorController>(tmpInfo2.assetpathFemale, sbLoadFileName.ToString(), clone: false, string.Empty);
								hashUseAssetBundleAnimator.Add(tmpInfo2.assetpathFemale);
								yield return null;
								tmpAddInfo = tmpInfo2;
								break;
							}
						}
						else
						{
							sbLoadFileName.Clear();
							sbLoadFileName.Append(tmpAddInfo.fileFemale);
							HoushiRacF[s3] = CommonLib.LoadAsset<RuntimeAnimatorController>(tmpAddInfo.assetpathFemale, sbLoadFileName.ToString(), clone: false, string.Empty);
							hashUseAssetBundleAnimator.Add(tmpAddInfo.assetpathFemale);
							yield return null;
						}
					}
					sbLoadFileName.Clear();
					sbLoadFileName.Append(_info.fileFemale);
					racF[s3] = CommonLib.LoadAsset<RuntimeAnimatorController>(_info.assetpathFemale, sbLoadFileName.ToString(), clone: false, string.Empty);
					yield return null;
				}
				else if (_info.nPromiscuity < 1)
				{
					if (!racEtcF.ContainsKey(_info.assetBaseF))
					{
						racEtcF.Add(_info.assetBaseF, CommonLib.LoadAsset<RuntimeAnimatorController>(_info.assetpathBaseF, _info.assetBaseF, clone: false, string.Empty));
						hashUseAssetBundleAnimator.Add(_info.assetpathBaseF);
					}
					sbLoadFileName.Clear();
					sbLoadFileName.Append(_info.fileFemale);
					racF[s3] = CommonLib.LoadAsset<RuntimeAnimatorController>(_info.assetpathFemale, sbLoadFileName.ToString(), clone: false, string.Empty);
				}
				else if (_info.nPromiscuity == 1)
				{
					if (s3 == 0)
					{
						if (!racEtcF.ContainsKey(_info.assetBaseF))
						{
							racEtcF.Add(_info.assetBaseF, CommonLib.LoadAsset<RuntimeAnimatorController>(_info.assetpathBaseF, _info.assetBaseF, clone: false, string.Empty));
							hashUseAssetBundleAnimator.Add(_info.assetpathBaseF);
						}
						sbLoadFileName.Clear();
						sbLoadFileName.Append(_info.fileFemale);
						racF[s3] = CommonLib.LoadAsset<RuntimeAnimatorController>(_info.assetpathFemale, sbLoadFileName.ToString(), clone: false, string.Empty);
					}
					else
					{
						if (!racEtcF.ContainsKey(_info.assetBaseF2))
						{
							racEtcF.Add(_info.assetBaseF2, CommonLib.LoadAsset<RuntimeAnimatorController>(_info.assetpathBaseF2, _info.assetBaseF2, clone: false, string.Empty));
							hashUseAssetBundleAnimator.Add(_info.assetpathBaseF2);
						}
						sbLoadFileName.Clear();
						sbLoadFileName.Append(_info.fileFemale2);
						racF[s3] = CommonLib.LoadAsset<RuntimeAnimatorController>(_info.assetpathFemale2, sbLoadFileName.ToString(), clone: false, string.Empty);
					}
				}
				else if (_info.nPromiscuity == 2)
				{
					if (s3 != 0)
					{
						if (!racEtcF.ContainsKey(_info.assetBaseF))
						{
							racEtcF.Add(_info.assetBaseF, CommonLib.LoadAsset<RuntimeAnimatorController>(_info.assetpathBaseF, _info.assetBaseF, clone: false, string.Empty));
							hashUseAssetBundleAnimator.Add(_info.assetpathBaseF);
						}
						sbLoadFileName.Clear();
						sbLoadFileName.Append(_info.fileFemale);
						racF[s3] = CommonLib.LoadAsset<RuntimeAnimatorController>(_info.assetpathFemale, sbLoadFileName.ToString(), clone: false, string.Empty);
					}
					else
					{
						if (!racEtcF.ContainsKey(_info.assetBaseF2))
						{
							racEtcF.Add(_info.assetBaseF2, CommonLib.LoadAsset<RuntimeAnimatorController>(_info.assetpathBaseF2, _info.assetBaseF2, clone: false, string.Empty));
							hashUseAssetBundleAnimator.Add(_info.assetpathBaseF2);
						}
						sbLoadFileName.Clear();
						sbLoadFileName.Append(_info.fileFemale2);
						racF[s3] = CommonLib.LoadAsset<RuntimeAnimatorController>(_info.assetpathFemale2, sbLoadFileName.ToString(), clone: false, string.Empty);
					}
				}
				ctrlLayer.LoadExcel(sbLoadFileName.ToString(), 1, s3);
			}
			hashUseAssetBundleAnimator.Add(_info.assetpathFemale);
			if (_info.nPromiscuity > 0)
			{
				hashUseAssetBundleAnimator.Add(_info.assetpathFemale2);
			}
			yield return null;
		}
		bool racMChange = false;
		for (int k = 0; k < chaMales.Length; k++)
		{
			if (chaMales[k] == null || chaMales[k].animBody == null || racM[k] == null)
			{
				continue;
			}
			racMChange = true;
			if (_info.ActionCtrl.Item1 < 3)
			{
				if (mode == 0 && oldMode != _info.ActionCtrl.Item1)
				{
					chaMales[k].animBody.runtimeAnimatorController = runtimeAnimatorControllers[0, 0];
				}
				else if (mode == 1 && oldMode != _info.ActionCtrl.Item1)
				{
					chaMales[k].animBody.runtimeAnimatorController = runtimeAnimatorControllers[0, 1];
				}
				else if (mode == 2 && oldMode != _info.ActionCtrl.Item1)
				{
					chaMales[k].animBody.runtimeAnimatorController = runtimeAnimatorControllers[0, 2];
				}
				if (HoushiRacM[k] == null)
				{
					chaMales[k].animBody.runtimeAnimatorController = Illusion.Utils.Animator.SetupAnimatorOverrideController(chaMales[k].animBody.runtimeAnimatorController, racM[k]);
				}
				else
				{
					chaMales[k].animBody.runtimeAnimatorController = MixRuntimeControler(chaMales[k].animBody.runtimeAnimatorController, racM[k], HoushiRacM[k]);
				}
			}
			else
			{
				chaMales[k].animBody.runtimeAnimatorController = Illusion.Utils.Animator.SetupAnimatorOverrideController(racEtcM[_info.assetBaseM], racM[k]);
			}
		}
		bool racFChange = false;
		for (int l = 0; l < chaFemales.Length; l++)
		{
			if (racF[l] == null)
			{
				continue;
			}
			racFChange = true;
			if (_info.ActionCtrl.Item1 < 3)
			{
				if (mode == 0 && oldMode != _info.ActionCtrl.Item1)
				{
					chaFemales[l].animBody.runtimeAnimatorController = runtimeAnimatorControllers[1, 0];
				}
				else if (mode == 1 && oldMode != _info.ActionCtrl.Item1)
				{
					chaFemales[l].animBody.runtimeAnimatorController = runtimeAnimatorControllers[1, 1];
				}
				else if (mode == 2 && oldMode != _info.ActionCtrl.Item1)
				{
					chaFemales[l].animBody.runtimeAnimatorController = runtimeAnimatorControllers[1, 2];
				}
				if (HoushiRacF[l] == null)
				{
					chaFemales[l].animBody.runtimeAnimatorController = Illusion.Utils.Animator.SetupAnimatorOverrideController(chaFemales[l].animBody.runtimeAnimatorController, racF[l]);
				}
				else
				{
					chaFemales[l].animBody.runtimeAnimatorController = MixRuntimeControler(chaFemales[l].animBody.runtimeAnimatorController, racF[l], HoushiRacF[l]);
				}
			}
			else if (_info.nPromiscuity < 1)
			{
				chaFemales[l].animBody.runtimeAnimatorController = Illusion.Utils.Animator.SetupAnimatorOverrideController(racEtcF[_info.assetBaseF], racF[l]);
			}
			else if (_info.nPromiscuity == 1)
			{
				switch (l)
				{
				case 0:
					chaFemales[l].animBody.runtimeAnimatorController = Illusion.Utils.Animator.SetupAnimatorOverrideController(racEtcF[_info.assetBaseF], racF[l]);
					break;
				case 1:
					chaFemales[l].animBody.runtimeAnimatorController = Illusion.Utils.Animator.SetupAnimatorOverrideController(racEtcF[_info.assetBaseF2], racF[l]);
					break;
				}
			}
			else if (_info.nPromiscuity == 2)
			{
				switch (l)
				{
				case 0:
					chaFemales[l].animBody.runtimeAnimatorController = Illusion.Utils.Animator.SetupAnimatorOverrideController(racEtcF[_info.assetBaseF2], racF[l]);
					break;
				case 1:
					chaFemales[l].animBody.runtimeAnimatorController = Illusion.Utils.Animator.SetupAnimatorOverrideController(racEtcF[_info.assetBaseF], racF[l]);
					break;
				}
			}
		}
		if (!racMChange && !racFChange && _info.id != -1)
		{
			ProcBase.endInit = true;
			nowChangeAnim = false;
			yield break;
		}
		if (_UseFade)
		{
			fade.FadeStart(1.5f);
		}
		for (int m = 0; m < chaMales.Length && !(chaMales[m] == null) && !(chaMales[m].objTop == null); m++)
		{
			chaMales[m].visibleSon = _info.nMaleSon == 1;
		}
		if (!isSetStartPos)
		{
			Vector3 pos = Vector3.zero;
			Vector3 rot = Vector3.zero;
			int num = -1;
			for (int n = 0; n < _info.lstOffset.Count; n++)
			{
				if (ctrlFlag.nPlace == _info.nPositons[n])
				{
					num = n;
					break;
				}
			}
			if (num >= 0 && _info.lstOffset[num] != null && _info.lstOffset[num] != string.Empty)
			{
				LoadMoveOffset(_info.lstOffset[num], out pos, out rot);
			}
			SetPosition(StartPos.position, StartPos.rotation, pos, rot, _FadeStart: false);
			GlobalMethod.setCameraBase(ctrlFlag.cameraCtrl, chaFemalesTrans[0]);
			isSetStartPos = true;
		}
		else
		{
			Vector3 pos2 = Vector3.zero;
			Vector3 rot2 = Vector3.zero;
			int num2 = -1;
			for (int num3 = 0; num3 < _info.lstOffset.Count; num3++)
			{
				if (ctrlFlag.nPlace == _info.nPositons[num3])
				{
					num2 = num3;
					break;
				}
			}
			if (num2 >= 0 && _info.lstOffset[num2] != null && _info.lstOffset[num2] != string.Empty)
			{
				LoadMoveOffset(_info.lstOffset[num2], out pos2, out rot2);
			}
			SetPosition(hPointCtrl.lstMarker[ctrlFlag.HPointID].Item2.pivot.position, hPointCtrl.lstMarker[ctrlFlag.HPointID].Item2.pivot.rotation, pos2, rot2, _FadeStart: false);
			GlobalMethod.setCameraBase(ctrlFlag.cameraCtrl, chaFemalesTrans[0]);
			setCameraLoad(_info, _isForceResetCamera);
		}
		if (afterIdle)
		{
			ctrlVoice.AfterFinish();
		}
		for (int num4 = 0; num4 < ctrlYures.Length; num4++)
		{
			if (!(ctrlYures[num4] == null))
			{
				if (_info.nPromiscuity < 1 && num4 == 1)
				{
					ctrlYures[num4].Release();
				}
				else
				{
					ctrlYures[num4].Load(_info.id, _info.ActionCtrl.Item1);
				}
			}
		}
		if (chaMales[0] == null || !chaMales[0].visibleAll || chaMales[0].objBody == null)
		{
			ctrlYureMale.Release();
		}
		else
		{
			ctrlYureMale.Load(_info.id, _info.ActionCtrl.Item1);
		}
		for (int num5 = 0; num5 < ctrlLookAts.Length; num5++)
		{
			if (chaMales[num5] == null || chaMales[num5].objBodyBone == null)
			{
				ctrlLookAts[num5].Release();
				continue;
			}
			sbLoadFileName.Clear();
			sbLoadFileName.Append(_info.fileMale);
			ctrlLookAts[num5].LoadList(sbLoadFileName.ToString(), _info.id);
		}
		for (int num6 = 0; num6 < chaMales.Length; num6++)
		{
			if (chaMales[num6] == null || chaMales[num6].animBody == null || !chaMales[num6].visibleAll)
			{
				continue;
			}
			if (_info.isMaleHitObject)
			{
				if (!ctrlFlag.nowAnimationInfo.isMaleHitObject)
				{
					chaMales[num6].LoadHitObject();
				}
			}
			else
			{
				chaMales[num6].ReleaseHitObject();
			}
			if ((bool)chaMales[num6].objHitBody)
			{
				sbLoadFileName.Clear();
				sbLoadFileName.Append(_info.fileMale);
				ctrlMaleCollisionCtrls[num6].Init(chaFemales[0], chaMales[num6].objHitHead, chaMales[num6].objHitBody);
				ctrlMaleCollisionCtrls[num6].LoadExcel(sbLoadFileName.ToString());
			}
			else
			{
				ctrlMaleCollisionCtrls[num6].Release();
			}
			if (mode != -1)
			{
				for (int num7 = 0; num7 < lstMotionIK.Count; num7++)
				{
					if (lstMotionIK[num7].Item1 != 0 || lstMotionIK[num7].Item2 != num6)
					{
						continue;
					}
					sbLoadFileName.Clear();
					sbLoadFileName.Append(_info.fileMale);
					for (int num8 = 0; num8 < abName.Count; num8++)
					{
						if (!GlobalMethod.AssetFileExist(abName[num8], sbLoadFileName.ToString(), string.Empty))
						{
							lstMotionIK[num7].Item3.data.states = null;
							continue;
						}
						lstMotionIK[num7].Item3.LoadData(abName[num8], sbLoadFileName.ToString());
						break;
					}
					if (mode != 1 || _info.nDownPtn != 1 || tmpAddInfo == null)
					{
						continue;
					}
					sbLoadFileName.Clear();
					sbLoadFileName.Append(tmpAddInfo.fileMale);
					for (int num9 = 0; num9 < abName.Count; num9++)
					{
						if (GlobalMethod.AssetFileExist(abName[num9], sbLoadFileName.ToString(), string.Empty))
						{
							lstMotionIK[num7].Item3.LoadData(abName[num9], sbLoadFileName.ToString(), add: true);
							break;
						}
					}
				}
			}
			if (chaMales[num6] != null && mode >= 0)
			{
				CtrlParticle.ParticleLoad(chaMales[num6].objBodyBone, num6 * 2);
			}
		}
		for (int num10 = 0; num10 < chaFemales.Length; num10++)
		{
			if (chaFemales[num10] == null || chaFemales[num10].animBody == null || !chaFemales[num10].visibleAll)
			{
				continue;
			}
			if (_info.id != -1 && mode != -1)
			{
				for (int num11 = 0; num11 < lstMotionIK.Count; num11++)
				{
					if (lstMotionIK[num11].Item1 != 1 || lstMotionIK[num11].Item2 != num10)
					{
						continue;
					}
					sbLoadFileName.Clear();
					if (_info.nPromiscuity < 1)
					{
						sbLoadFileName.Append(_info.fileFemale);
					}
					else if (_info.nPromiscuity == 1)
					{
						switch (num10)
						{
						case 0:
							sbLoadFileName.Append(_info.fileFemale);
							break;
						case 1:
							sbLoadFileName.Append(_info.fileFemale2);
							break;
						}
					}
					else if (_info.nPromiscuity == 2)
					{
						switch (num10)
						{
						case 0:
							sbLoadFileName.Append(_info.fileFemale2);
							break;
						case 1:
							sbLoadFileName.Append(_info.fileFemale);
							break;
						}
					}
					for (int num12 = 0; num12 < abName.Count; num12++)
					{
						if (!GlobalMethod.AssetFileExist(abName[num12], sbLoadFileName.ToString(), string.Empty))
						{
							lstMotionIK[num11].Item3.data.states = null;
							continue;
						}
						lstMotionIK[num11].Item3.LoadData(abName[num12], sbLoadFileName.ToString());
						break;
					}
					if (mode != 1 || _info.nDownPtn != 1 || tmpAddInfo == null)
					{
						continue;
					}
					sbLoadFileName.Clear();
					sbLoadFileName.Append(tmpAddInfo.fileFemale);
					for (int num13 = 0; num13 < abName.Count; num13++)
					{
						if (GlobalMethod.AssetFileExist(abName[num13], sbLoadFileName.ToString(), string.Empty))
						{
							lstMotionIK[num11].Item3.LoadData(abName[num13], sbLoadFileName.ToString(), add: true);
							break;
						}
					}
				}
			}
			if (_info.isFemaleHitObject)
			{
				if (!ctrlFlag.nowAnimationInfo.isFemaleHitObject)
				{
					chaFemales[num10].LoadHitObject();
				}
			}
			else
			{
				chaFemales[num10].ReleaseHitObject();
			}
			if ((bool)chaFemales[num10].objHitBody)
			{
				ctrlFemaleCollisionCtrls[num10].Init(chaFemales[num10], chaFemales[num10].objHitHead, chaFemales[num10].objHitBody);
				sbLoadFileName.Clear();
				if (_info.nPromiscuity < 1)
				{
					sbLoadFileName.Append(_info.fileFemale);
				}
				else if (_info.nPromiscuity == 1)
				{
					switch (num10)
					{
					case 0:
						sbLoadFileName.Append(_info.fileFemale);
						break;
					case 1:
						sbLoadFileName.Append(_info.fileFemale2);
						break;
					}
				}
				else if (_info.nPromiscuity == 2)
				{
					switch (num10)
					{
					case 0:
						sbLoadFileName.Append(_info.fileFemale2);
						break;
					case 1:
						sbLoadFileName.Append(_info.fileFemale);
						break;
					}
				}
				ctrlFemaleCollisionCtrls[num10].LoadExcel(sbLoadFileName.ToString());
			}
			else
			{
				ctrlFemaleCollisionCtrls[num10].Release();
			}
			if (chaFemales[num10] != null && mode >= 0)
			{
				if (num10 == 1)
				{
					CtrlParticle.ParticleLoad(chaFemales[num10].objBodyBone, 3);
					ctrlDynamics[num10].Init(chaFemales[num10]);
				}
				else
				{
					CtrlParticle.ParticleLoad(chaFemales[num10].objBodyBone, 1);
				}
			}
		}
		SetClothStateStartMotion(0, _info);
		if (_info.nPromiscuity >= 1)
		{
			SetClothStateStartMotion(1, _info);
		}
		if (_info.isNeedItem)
		{
			ctrlItem.ReleaseItem();
			yield return null;
			ctrlFlag.AddParam(9, 1);
			ctrlItem.LoadItem(_info.ActionCtrl.Item1, _info.id, (!(chaMales[0] != null)) ? null : chaMales[0].objBodyBone, chaFemales[0].objBodyBone, (!(chaMales[1] != null)) ? null : chaMales[1].objBodyBone, (!(chaFemales[1] != null)) ? null : chaFemales[1].objBodyBone);
		}
		else
		{
			ctrlItem.ReleaseItem();
			if (ctrlFlag.nowHPoint != null)
			{
				ChangeHItem componentInChildren = ctrlFlag.nowHPoint.GetComponentInChildren<ChangeHItem>();
				if (componentInChildren != null)
				{
					componentInChildren.ChangeActive(val: true);
				}
				ctrlFlag.nowHPoint.SetEffectActive(set: false);
			}
		}
		List<HItemCtrl.Item> itemList = ctrlItem.GetItems();
		int itemListCount = itemList.Count;
		GameObject[] items = new GameObject[itemListCount];
		for (int num14 = 0; num14 < itemListCount; num14++)
		{
			items[num14] = itemList[num14].objItem;
		}
		for (int num15 = 0; num15 < lstMotionIK.Count; num15++)
		{
			if (lstMotionIK[num15].Item3.items != null)
			{
				lstMotionIK[num15].Item3.items.Clear();
			}
			lstMotionIK[num15].Item3.SetItems(lstMotionIK[num15].Item3, items);
			lstMotionIK[num15].Item3.Reset();
		}
		if (mode != -1)
		{
			lstProc[mode].Init(modeCtrl);
			lstProc[mode].SetStartMotion(isIdle, modeCtrl, _info);
			ctrlVoice.HouchiTime = 0f;
		}
		yield return MapUIContainer.DrawOnceTutorialUI(5, Camera);
		ctrlMeta.Clear();
		if (mode != 0 && _info.nPromiscuity != 2)
		{
			ctrlMeta.Load(hSceneManager.strAssetMetaBallListFolder, _info.fileFemale, (!(chaMales[0] != null)) ? null : ((!chaMales[0].objBodyBone) ? ctrlItem.GetItem() : chaMales[0].objBodyBone), (!(chaMales[1] != null)) ? null : ((!chaMales[1].objBodyBone) ? ctrlItem.GetItem() : chaMales[1].objBodyBone), (!(chaFemales[1] != null)) ? null : chaFemales[1].objBodyBone, chaFemales[1]);
		}
		yield return null;
		if (sprite != null)
		{
			if (mode == 2)
			{
				sprite.SetFinishSelect(mode, modeCtrl, _info.ActionCtrl.Item1, _info.ActionCtrl.Item2);
			}
			else
			{
				sprite.SetFinishSelect(mode, modeCtrl);
			}
		}
		if (_info.id == -1)
		{
			ctrlFlag.nowAnimationInfo = _info;
			ProcBase.endInit = true;
			nowChangeAnim = false;
			yield break;
		}
		CtrlParticle.ParticleLoad(ctrlItem.GetItem(), 4);
		for (int num16 = 0; num16 < ctrlHitObjectFemales.Length; num16++)
		{
			if (ctrlHitObjectFemales[num16] == null)
			{
				continue;
			}
			if (_info.nPromiscuity < 1 && num16 == 1)
			{
				ctrlHitObjectFemales[num16].HitObjLoadExcel(string.Empty);
				continue;
			}
			sbLoadFileName.Clear();
			if (_info.nPromiscuity < 1)
			{
				sbLoadFileName.Append(_info.fileFemale);
			}
			else if (_info.nPromiscuity == 1)
			{
				switch (num16)
				{
				case 0:
					sbLoadFileName.Append(_info.fileFemale);
					break;
				case 1:
					sbLoadFileName.Append(_info.fileFemale2);
					break;
				}
			}
			else if (_info.nPromiscuity == 2)
			{
				switch (num16)
				{
				case 0:
					sbLoadFileName.Append(_info.fileFemale2);
					break;
				case 1:
					sbLoadFileName.Append(_info.fileFemale);
					break;
				}
			}
			ctrlHitObjectFemales[num16].HitObjLoadExcel(sbLoadFileName.ToString());
		}
		for (int num17 = 0; num17 < ctrlHitObjectMales.Length; num17++)
		{
			if (ctrlHitObjectMales[num17] != null)
			{
				if (_info.nPromiscuity != 0 && num17 == 1)
				{
					ctrlHitObjectMales[num17].HitObjLoadExcel(string.Empty);
					continue;
				}
				sbLoadFileName.Clear();
				sbLoadFileName.Append(_info.fileMale);
				ctrlHitObjectMales[num17].HitObjLoadExcel(sbLoadFileName.ToString());
			}
		}
		for (int num18 = 0; num18 < ctrlDynamics.Length; num18++)
		{
			if (ctrlDynamics[num18] == null)
			{
				continue;
			}
			if (_info.nPromiscuity < 1 && num18 == 1)
			{
				ctrlDynamics[num18].Load(hSceneManager.strAssetDynamicBoneListFolder, string.Empty);
				continue;
			}
			sbLoadFileName.Clear();
			if (_info.nPromiscuity < 1)
			{
				sbLoadFileName.Append(_info.fileFemale);
			}
			else if (_info.nPromiscuity == 1)
			{
				switch (num18)
				{
				case 0:
					sbLoadFileName.Append(_info.fileFemale);
					break;
				case 1:
					sbLoadFileName.Append(_info.fileFemale2);
					break;
				}
			}
			else if (_info.nPromiscuity == 2)
			{
				switch (num18)
				{
				case 0:
					sbLoadFileName.Append(_info.fileFemale2);
					break;
				case 1:
					sbLoadFileName.Append(_info.fileFemale);
					break;
				}
			}
			ctrlDynamics[num18].Load(hSceneManager.strAssetDynamicBoneListFolder, sbLoadFileName.ToString());
		}
		GameObject[] objBoneFemale = new GameObject[2]
		{
			chaFemales[0].objBodyBone,
			(!(chaFemales[1] != null)) ? null : chaFemales[1].objBodyBone
		};
		ctrlSE.InitOldMember();
		ctrlSE.Load(hSceneManager.strAssetSEListFolder, _info.fileSe, (!(chaMales[0] != null)) ? null : chaMales[0].objBodyBone, objBoneFemale);
		ctrlVoice.SetVoiceList(_info.nAnimListInfoID, _info.nBreathID, _info.id, _info.lstSystem);
		bool _isMainFirstPerson = true;
		if (_info.ActionCtrl.Item1 == 4)
		{
			_isMainFirstPerson = _info.nInitiativeFemale == 0;
		}
		ctrlVoice.SetBreathVoiceList(chaFemales, _info.ActionCtrl.Item1, _info.ActionCtrl.Item2, _info.nInitiativeFemale != 0, hSceneManager.isForce, _isMainFirstPerson);
		bool femalePlayer5 = false;
		for (int num19 = 0; num19 < ctrlEyeNeckFemale.Length; num19++)
		{
			if (ctrlEyeNeckFemale[num19] == null && hMotionEyeNeckLesP == null)
			{
				continue;
			}
			if (_info.nPromiscuity < 1 && num19 == 1)
			{
				if (NumCharaChange)
				{
					femalePlayer5 = hSceneManager.Player.ChaControl.sex == 1 && !ctrlFlag.bFutanari;
					if (num19 == 0 || !femalePlayer5)
					{
						ctrlEyeNeckFemale[num19].SetPartner(null, null, null);
						ctrlEyeNeckFemale[num19].Release();
					}
					else
					{
						hMotionEyeNeckLesP.SetPartner(null);
						hMotionEyeNeckLesP.Release();
					}
				}
				continue;
			}
			if (NumCharaChange)
			{
				femalePlayer5 = false;
				femalePlayer5 = hSceneManager.Player.ChaControl.sex == 1 && !ctrlFlag.bFutanari;
				if (num19 == 0 || !femalePlayer5)
				{
					ctrlEyeNeckFemale[num19].SetPartner((!(chaMales[0] != null)) ? null : chaMales[0].objBodyBone, (!chaMales[1]) ? null : chaMales[1].objBodyBone, (!chaFemales[num19 ^ 1]) ? null : chaFemales[num19 ^ 1].objBodyBone);
				}
				else
				{
					hMotionEyeNeckLesP.SetPartner((!chaFemales[0]) ? null : chaFemales[0].objBodyBone);
				}
			}
			if (chaFemales[num19] == null || chaFemales[num19].objBody == null || !chaFemales[num19].visibleAll)
			{
				continue;
			}
			femalePlayer5 = false;
			sbLoadFileName.Clear();
			if (_info.nPromiscuity < 1)
			{
				sbLoadFileName.Append(_info.fileMotionNeckFemale);
			}
			else if (_info.nPromiscuity == 1)
			{
				switch (num19)
				{
				case 0:
					sbLoadFileName.Append(_info.fileMotionNeckFemale);
					break;
				case 1:
					sbLoadFileName.Append(_info.fileMotionNeckFemale2);
					break;
				}
			}
			else if (_info.nPromiscuity == 2)
			{
				switch (num19)
				{
				case 0:
					sbLoadFileName.Append(_info.fileMotionNeckFemale2);
					break;
				case 1:
					femalePlayer5 = hSceneManager.Player.ChaControl.sex == 1 && !ctrlFlag.bFutanari;
					if (!femalePlayer5)
					{
						sbLoadFileName.Append(_info.fileMotionNeckFemale);
					}
					else
					{
						sbLoadFileName.Append(_info.fileMotionNeckFemalePlayer);
					}
					break;
				}
			}
			if (num19 == 0 || !femalePlayer5)
			{
				ctrlEyeNeckFemale[num19].Load(hSceneManager.strAssetNeckCtrlListFolder, sbLoadFileName.ToString());
			}
			else
			{
				hMotionEyeNeckLesP.Load(hSceneManager.strAssetNeckCtrlListFolder, sbLoadFileName.ToString());
			}
		}
		for (int num20 = 0; num20 < ctrlEyeNeckMale.Length; num20++)
		{
			if (ctrlEyeNeckMale[num20] == null)
			{
				continue;
			}
			if (_info.nPromiscuity != 0 && num20 == 1)
			{
				if (NumCharaChange)
				{
					ctrlEyeNeckMale[num20].SetPartner(null, null, null);
					ctrlEyeNeckMale[num20].Release();
				}
				continue;
			}
			if (NumCharaChange)
			{
				ctrlEyeNeckMale[num20].SetPartner(chaFemales[0].objBodyBone, (!chaFemales[1]) ? null : chaFemales[1].objBodyBone, (!chaMales[num20 ^ 1]) ? null : chaMales[num20 ^ 1].objBodyBone);
			}
			sbLoadFileName.Clear();
			sbLoadFileName.Append(_info.fileMotionNeckMale);
			ctrlEyeNeckMale[num20].Load(hSceneManager.strAssetNeckCtrlListFolder, sbLoadFileName.ToString());
		}
		yield return null;
		for (int num21 = 0; num21 < ctrlSiruPastes.Length; num21++)
		{
			if (ctrlSiruPastes[num21] == null)
			{
				continue;
			}
			if (_info.nPromiscuity < 1 && num21 == 1)
			{
				ctrlSiruPastes[num21].Release();
				continue;
			}
			sbLoadFileName.Clear();
			if (_info.ActionCtrl.Item1 != 5)
			{
				sbLoadFileName.Append(_info.fileSiruPaste);
			}
			else
			{
				switch (num21)
				{
				case 0:
					sbLoadFileName.Append(_info.fileSiruPaste);
					break;
				case 1:
					sbLoadFileName.Append(_info.fileSiruPasteSecond);
					break;
				}
			}
			ctrlSiruPastes[num21].Load(hSceneManager.strAssetSiruPasteListFolder, sbLoadFileName.ToString(), _info.id);
		}
		ctrlAuto.AutoAutoLeaveItToYouInit();
		ctrlFlag.nowAnimationInfo = _info;
		for (int num22 = 0; num22 < chaFemales.Length && !(chaFemales[num22] == null) && chaFemales[num22].visibleAll && !(chaFemales[num22].objTop == null); num22++)
		{
			chaFemales[num22].reSetupDynamicBoneBust = true;
			chaFemales[num22].resetDynamicBoneAll = true;
		}
		for (int num23 = 0; num23 < chaMales.Length && !(chaMales[num23] == null) && chaMales[num23].visibleAll && !(chaMales[num23].objTop == null); num23++)
		{
			chaMales[num23].reSetupDynamicBoneBust = true;
			chaMales[num23].resetDynamicBoneAll = true;
		}
		for (int num24 = 0; num24 < chaFemales.Length && (_info.nPromiscuity >= 1 || num24 != 1); num24++)
		{
			chaFemales[num24].eyesCtrl?.SetOpenRateForce(1f);
		}
		ctrlFeelHit.InitTime();
		if (ctrlVoice.nowVoices[0].state == HVoiceCtrl.VoiceKind.voice || ctrlVoice.nowVoices[0].state == HVoiceCtrl.VoiceKind.startVoice)
		{
			for (int num25 = 0; num25 < chaFemales.Length && (_info.nPromiscuity >= 1 || num25 != 1); num25++)
			{
				Singleton<Voice>.Instance.Stop(ctrlFlag.voice.voiceTrs[num25]);
			}
		}
		if (chaFemales[1] != null && (!chaFemales[1].visibleAll || chaFemales[1].objBody == null))
		{
			Singleton<Voice>.Instance.Stop(ctrlFlag.voice.voiceTrs[1]);
		}
		for (int num26 = 0; num26 < _info.lstSystem.Count; num26++)
		{
			ctrlFlag.isJudgeSelect.Add((HSceneFlagCtrl.JudgeSelect)(1 << _info.lstSystem[num26]));
		}
		ctrlFlag.voice.dialog = false;
		if (mode == 0)
		{
			ctrlFlag.numAibu++;
			if (ctrlFlag.isJudgeSelect.Contains(HSceneFlagCtrl.JudgeSelect.Anal))
			{
				ctrlFlag.numAnal++;
			}
		}
		else if (mode == 1)
		{
			ctrlFlag.numHoushi++;
		}
		else if (mode == 2)
		{
			ctrlFlag.numSonyu++;
			if (ctrlFlag.isJudgeSelect.Contains(HSceneFlagCtrl.JudgeSelect.Kokan))
			{
				ctrlFlag.numKokan++;
			}
			if (ctrlFlag.isJudgeSelect.Contains(HSceneFlagCtrl.JudgeSelect.Anal))
			{
				ctrlFlag.numAnal++;
			}
		}
		else if (mode == 6)
		{
			ctrlFlag.numLes++;
		}
		if (ctrlFlag.isJudgeSelect.Contains(HSceneFlagCtrl.JudgeSelect.Kiss))
		{
			ctrlFlag.AddParam(6, 1);
		}
		if (_info.ActionCtrl.Item1 == 3 && _info.id == 13)
		{
			ctrlFlag.AddParam(17, 1);
		}
		else if (_info.ActionCtrl.Item1 == 2 && _info.id == 38)
		{
			ctrlFlag.AddParam(19, 1);
		}
		else if (_info.ActionCtrl.Item1 == 0 && _info.id == 16)
		{
			ctrlFlag.AddParam(23, 1);
		}
		else if (_info.ActionCtrl.Item1 == 2 && _info.id == 44)
		{
			ctrlFlag.AddParam(25, 1);
		}
		else if (_info.ActionCtrl.Item1 == 3 && _info.id == 14)
		{
			ctrlFlag.AddParam(35, 1);
		}
		else if (_info.ActionCtrl.Item1 == 2 && _info.id == 45)
		{
			ctrlFlag.AddParam(37, 1);
		}
		else if (_info.ActionCtrl.Item1 == 1 && _info.id == 30)
		{
			ctrlFlag.AddParam(39, 1);
		}
		else if (_info.ActionCtrl.Item1 == 2 && _info.id == 46)
		{
			ctrlFlag.AddParam(41, 1);
		}
		else if (_info.ActionCtrl.Item1 == 0 && _info.id == 100)
		{
			ctrlFlag.AddParam(43, 1);
		}
		else if (_info.ActionCtrl.Item1 == 2 && _info.id == 113)
		{
			ctrlFlag.AddParam(45, 1);
		}
		else if (_info.ActionCtrl.Item1 == 1 && _info.id == 104)
		{
			ctrlFlag.AddParam(47, 1);
		}
		else if (_info.ActionCtrl.Item1 == 2 && _info.id == 110)
		{
			ctrlFlag.AddParam(49, 1);
		}
		if (_info.ActionCtrl.Item1 == 3 && _info.ActionCtrl.Item2 == 3)
		{
			ctrlFlag.AddParam(21, 1);
		}
		foreach (string item3 in hashUseAssetBundleAnimator)
		{
			AssetBundleManager.UnloadAssetBundle(item3, isUnloadForceRefCount: false);
		}
		GC.Collect();
		UnityEngine.Resources.UnloadUnusedAssets();
		yield return null;
		ctrlAuto.AutoAutoLeaveItToYouInit();
		nowChangeAnim = false;
	}

	private bool IsIdle(Animator _anim)
	{
		if (_anim.runtimeAnimatorController == null)
		{
			return true;
		}
		AnimatorStateInfo currentAnimatorStateInfo = _anim.GetCurrentAnimatorStateInfo(0);
		if (currentAnimatorStateInfo.IsName("Idle") || currentAnimatorStateInfo.IsName("WIdle") || currentAnimatorStateInfo.IsName("SIdle") || currentAnimatorStateInfo.IsName("Insert") || currentAnimatorStateInfo.IsName("D_Idle") || currentAnimatorStateInfo.IsName("D_Insert"))
		{
			return true;
		}
		return false;
	}

	private bool IsAfterIdle(Animator _anim, int _mode)
	{
		if (_anim.runtimeAnimatorController == null)
		{
			return true;
		}
		AnimatorStateInfo currentAnimatorStateInfo = _anim.GetCurrentAnimatorStateInfo(0);
		switch (_mode)
		{
		case 0:
			if (currentAnimatorStateInfo.IsName("Orgasm_A") || currentAnimatorStateInfo.IsName("Orgasm_OUT_A") || currentAnimatorStateInfo.IsName("Drink_A") || currentAnimatorStateInfo.IsName("Vomit_A") || currentAnimatorStateInfo.IsName("Orgasm_IN_A") || currentAnimatorStateInfo.IsName("OrgasmM_OUT_A") || currentAnimatorStateInfo.IsName("D_Orgasm_A") || currentAnimatorStateInfo.IsName("D_Orgasm_OUT_A") || currentAnimatorStateInfo.IsName("D_Orgasm_IN_A") || currentAnimatorStateInfo.IsName("D_OrgasmM_OUT_A"))
			{
				return true;
			}
			break;
		case 1:
			if (currentAnimatorStateInfo.IsName("Orgasm_A") || currentAnimatorStateInfo.IsName("Orgasm_OUT_A") || currentAnimatorStateInfo.IsName("Drink_A") || currentAnimatorStateInfo.IsName("Vomit_A") || currentAnimatorStateInfo.IsName("Orgasm_IN_A") || currentAnimatorStateInfo.IsName("OrgasmM_OUT_A"))
			{
				return true;
			}
			break;
		case 2:
			if (currentAnimatorStateInfo.IsName("D_Orgasm_A") || currentAnimatorStateInfo.IsName("D_Orgasm_OUT_A") || currentAnimatorStateInfo.IsName("D_Orgasm_IN_A") || currentAnimatorStateInfo.IsName("D_OrgasmM_OUT_A"))
			{
				return true;
			}
			break;
		}
		return false;
	}

	public bool setCameraLoad(AnimationListInfo _info, bool _isForceResetCamera)
	{
		if (_isForceResetCamera || (isSetStartPos && Config.HData.InitCamera))
		{
			GlobalMethod.loadCamera(ctrlFlag.cameraCtrl, hSceneManager.strAssetCameraList, _info.nameCamera);
		}
		else
		{
			GlobalMethod.loadResetCamera(ctrlFlag.cameraCtrl, hSceneManager.strAssetCameraList, _info.nameCamera);
		}
		return true;
	}

	private bool SetPosition(Transform _trans, Vector3 offsetpos, Vector3 offsetrot, bool _FadeStart = true)
	{
		if (_trans == null)
		{
			return false;
		}
		for (int i = 0; i < chaMales.Length; i++)
		{
			if (!(chaMales[i] == null) && !(chaMales[i].objBody == null))
			{
				chaMalesTrans[i].position = _trans.position;
				chaMalesTrans[i].rotation = _trans.rotation;
				chaMalesTrans[i].localPosition += _trans.rotation * offsetpos;
				chaMalesTrans[i].localRotation = Quaternion.Euler(offsetrot) * chaMalesTrans[i].localRotation;
				chaMales[i].animBody.transform.localPosition = Vector3.zero;
				chaMales[i].animBody.transform.localRotation = Quaternion.identity;
				chaMales[i].SetPosition(Vector3.zero);
				chaMales[i].SetRotation(Vector3.zero);
				chaMales[i].resetDynamicBoneAll = true;
			}
		}
		if (hSceneManager.male != null && chaMales[0] != null && chaMales[0].objBody != null)
		{
			hSceneManager.male.Position = chaMalesTrans[0].position;
		}
		for (int j = 0; j < chaFemales.Length; j++)
		{
			if (!(chaFemales[j] == null) && !(chaFemales[j].objBody == null))
			{
				chaFemalesTrans[j].position = _trans.position;
				chaFemalesTrans[j].rotation = _trans.rotation;
				chaFemalesTrans[j].localPosition += _trans.rotation * offsetpos;
				chaFemalesTrans[j].localRotation = Quaternion.Euler(offsetrot) * chaFemalesTrans[j].localRotation;
				chaFemales[j].animBody.transform.localPosition = Vector3.zero;
				chaFemales[j].animBody.transform.localRotation = Quaternion.identity;
				chaFemales[j].SetPosition(Vector3.zero);
				chaFemales[j].SetRotation(Vector3.zero);
				chaFemales[j].ResetDynamicBoneAll();
				chaFemales[j].resetDynamicBoneAll = true;
				if (hSceneManager.females != null && hSceneManager.females[j] != null)
				{
					hSceneManager.females[j].Position = chaFemalesTrans[j].position;
				}
			}
		}
		Vector3 position = chaFemales[0].objHeadBone.transform.position;
		position += FeelHitEffect3DOffSet;
		FeelHitEffect3D.transform.position = position;
		if (_FadeStart)
		{
			fade.FadeStart(2f);
		}
		ctrlItem.setTransform(_trans);
		return true;
	}

	private bool SetPosition(Vector3 pos, Quaternion rot, Vector3 offsetpos, Vector3 offsetrot, bool _FadeStart = true)
	{
		for (int i = 0; i < chaMales.Length; i++)
		{
			if (!(chaMales[i] == null) && !(chaMales[i].objBody == null))
			{
				chaMalesTrans[i].position = pos;
				chaMalesTrans[i].rotation = rot;
				chaMalesTrans[i].localPosition += rot * offsetpos;
				chaMalesTrans[i].localRotation = Quaternion.Euler(offsetrot) * chaMalesTrans[i].localRotation;
				chaMales[i].animBody.transform.localPosition = Vector3.zero;
				chaMales[i].animBody.transform.localRotation = Quaternion.identity;
				chaMales[i].SetPosition(Vector3.zero);
				chaMales[i].SetRotation(Vector3.zero);
				chaMales[i].resetDynamicBoneAll = true;
			}
		}
		for (int j = 0; j < chaFemales.Length; j++)
		{
			if (!(chaFemales[j] == null) && !(chaFemales[j].objBody == null))
			{
				chaFemalesTrans[j].position = pos;
				chaFemalesTrans[j].rotation = rot;
				chaFemalesTrans[j].localPosition += rot * offsetpos;
				chaFemalesTrans[j].localRotation = Quaternion.Euler(offsetrot) * chaFemalesTrans[j].localRotation;
				chaFemales[j].animBody.transform.localPosition = Vector3.zero;
				chaFemales[j].animBody.transform.localRotation = Quaternion.identity;
				chaFemales[j].SetPosition(Vector3.zero);
				chaFemales[j].SetRotation(Vector3.zero);
				chaFemales[j].ResetDynamicBoneAll();
				chaFemales[j].resetDynamicBoneAll = true;
			}
		}
		Vector3 position = chaFemales[0].objHeadBone.transform.position;
		position += FeelHitEffect3DOffSet;
		FeelHitEffect3D.transform.position = position;
		if (_FadeStart)
		{
			fade.FadeStart();
		}
		ctrlItem.setTransform(chaFemalesTrans[0].position, chaFemalesTrans[0].rotation.eulerAngles);
		objMetaBallBase.transform.rotation = chaFemalesTrans[0].rotation;
		return true;
	}

	public bool SetMovePositionPoint(Transform trans, Vector3 offsetpos, Vector3 offsetrot)
	{
		SetPosition(trans, offsetpos, offsetrot, _FadeStart: false);
		GlobalMethod.setCameraBase(ctrlFlag.cameraCtrl, chaFemalesTrans[0]);
		GlobalMethod.loadCamera(ctrlFlag.cameraCtrl, hSceneManager.strAssetCameraList, ctrlFlag.nowAnimationInfo.nameCamera);
		return true;
	}

	private bool ShortcutKey()
	{
		bool isFade = sprite.isFade;
		isFade |= sprite.GetFadeKindProc() == HSceneSprite.FadeKindProc.OutEnd;
		isFade |= Singleton<Game>.Instance.Config != null;
		isFade |= Singleton<Game>.Instance.Dialog != null;
		isFade |= Singleton<Game>.Instance.ExitScene != null;
		if (isFade | (Singleton<Game>.Instance.MapShortcutUI != null))
		{
			return true;
		}
		GlobalMethod.CameraKeyCtrl(ctrlFlag.cameraCtrl, chaFemales);
		if (UnityEngine.Input.GetKeyDown(KeyCode.F3))
		{
			ConfigWindow.UnLoadAction = delegate
			{
			};
			ConfigWindow.TitleChangeAction = delegate
			{
				ConfigEnd();
				ConfigWindow.UnLoadAction = null;
				Singleton<Game>.Instance.Dialog.TimeScale = 1f;
			};
			Singleton<Game>.Instance.LoadConfig();
			return true;
		}
		if (!UnityEngine.Input.GetKey(KeyCode.LeftShift) && !UnityEngine.Input.GetKey(KeyCode.RightShift))
		{
			if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1))
			{
				Config.HData.EyeDir0 = !Config.HData.EyeDir0;
			}
			if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2))
			{
				Config.HData.NeckDir0 = !Config.HData.NeckDir0;
			}
		}
		else
		{
			if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1))
			{
				Config.HData.EyeDir1 = !Config.HData.EyeDir1;
			}
			if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2))
			{
				Config.HData.NeckDir1 = !Config.HData.NeckDir1;
			}
		}
		if (!UnityEngine.Input.GetKey(KeyCode.LeftShift) && !UnityEngine.Input.GetKey(KeyCode.RightShift))
		{
			if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3))
			{
				Config.HData.Visible = !Config.HData.Visible;
			}
			if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha4))
			{
				Config.HData.Son = !Config.HData.Son;
			}
			if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha5))
			{
				Config.HData.Cloth = !Config.HData.Cloth;
			}
			if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha6))
			{
				Config.HData.Accessory = !Config.HData.Accessory;
			}
			if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha7))
			{
				Config.HData.Shoes = !Config.HData.Shoes;
			}
			if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha8))
			{
				Config.GraphicData.SimpleBody = !Config.GraphicData.SimpleBody;
			}
			if (UnityEngine.Input.GetKeyDown(KeyCode.T))
			{
				sprite.ChangeStateAllEquip();
			}
		}
		if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha9))
		{
			Config.HData.Siru = (Config.HData.Siru + 1) % 3;
		}
		if (!UnityEngine.Input.GetKey(KeyCode.LeftShift) && !UnityEngine.Input.GetKey(KeyCode.RightShift))
		{
			if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha0))
			{
				bool[] flags = new bool[5]
				{
					chaFemales[0].GetSiruFlag(ChaFileDefine.SiruParts.SiruKao) != 0,
					chaFemales[0].GetSiruFlag(ChaFileDefine.SiruParts.SiruFrontTop) != 0,
					chaFemales[0].GetSiruFlag(ChaFileDefine.SiruParts.SiruFrontBot) != 0,
					chaFemales[0].GetSiruFlag(ChaFileDefine.SiruParts.SiruBackTop) != 0,
					chaFemales[0].GetSiruFlag(ChaFileDefine.SiruParts.SiruBackBot) != 0
				};
				byte lv = (byte)((!GlobalMethod.CheckFlagsArray(flags, 1)) ? 2u : 0u);
				if ((bool)chaFemales[0] && chaFemales[0].visibleAll && chaFemales[0].objBody != null)
				{
					for (ChaFileDefine.SiruParts siruParts = ChaFileDefine.SiruParts.SiruKao; siruParts < (ChaFileDefine.SiruParts)5; siruParts++)
					{
						chaFemales[0].SetSiruFlag(siruParts, lv);
					}
				}
			}
		}
		else if ((bool)chaFemales[1] && chaFemales[1].visibleAll && chaFemales[1].objBody != null && UnityEngine.Input.GetKeyDown(KeyCode.Alpha0))
		{
			bool[] flags2 = new bool[5]
			{
				chaFemales[1].GetSiruFlag(ChaFileDefine.SiruParts.SiruKao) != 0,
				chaFemales[1].GetSiruFlag(ChaFileDefine.SiruParts.SiruFrontTop) != 0,
				chaFemales[1].GetSiruFlag(ChaFileDefine.SiruParts.SiruFrontBot) != 0,
				chaFemales[1].GetSiruFlag(ChaFileDefine.SiruParts.SiruBackTop) != 0,
				chaFemales[1].GetSiruFlag(ChaFileDefine.SiruParts.SiruBackBot) != 0
			};
			byte lv2 = (byte)((!GlobalMethod.CheckFlagsArray(flags2, 1)) ? 2u : 0u);
			for (ChaFileDefine.SiruParts siruParts2 = ChaFileDefine.SiruParts.SiruKao; siruParts2 < (ChaFileDefine.SiruParts)5; siruParts2++)
			{
				chaFemales[1].SetSiruFlag(siruParts2, lv2);
			}
		}
		if (UnityEngine.Input.GetKeyDown(KeyCode.A))
		{
			Config.HData.FeelingGauge = !Config.HData.FeelingGauge;
		}
		if (UnityEngine.Input.GetKeyDown(KeyCode.S))
		{
			Config.HData.MenuIcon = !Config.HData.MenuIcon;
		}
		if (UnityEngine.Input.GetKeyDown(KeyCode.D))
		{
			Config.HData.FinishButton = !Config.HData.FinishButton;
		}
		if (UnityEngine.Input.GetKeyDown(KeyCode.F))
		{
			Config.HData.InitCamera = !Config.HData.InitCamera;
		}
		if (UnityEngine.Input.GetKeyDown(KeyCode.Z))
		{
			Config.ActData.Look = !Config.ActData.Look;
		}
		if (UnityEngine.Input.GetKeyDown(KeyCode.X))
		{
			isTuyaOn = !isTuyaOn;
		}
		if (UnityEngine.Input.GetKeyDown(KeyCode.C))
		{
			Config.HData.ActionGuide = !Config.HData.ActionGuide;
		}
		return true;
	}

	private bool GetAutoAnimation(bool _isFirst)
	{
		autoMotion = ctrlAuto.GetAnimation(lstAnimInfo, ctrlFlag.initiative, _isFirst);
		if (autoMotion == null || lstAnimInfo.Length <= autoMotion.mode || autoMotion.mode == -1)
		{
			return false;
		}
		foreach (AnimationListInfo item in lstAnimInfo[autoMotion.mode])
		{
			if (item.id != autoMotion.id)
			{
				continue;
			}
			ctrlFlag.selectAnimationListInfo = item;
			break;
		}
		return true;
	}

	private int GetAnimationListModeFromSelectInfo(AnimationListInfo _info)
	{
		if (_info == null)
		{
			return -1;
		}
		int result = -1;
		for (int i = 0; i < lstAnimInfo.Length; i++)
		{
			for (int j = 0; j < lstAnimInfo[i].Count; j++)
			{
				if (lstAnimInfo[i][j] == _info)
				{
					result = i;
					break;
				}
			}
		}
		return result;
	}

	private bool SetStartVoice()
	{
		if (hSceneManager.EventKind != 0)
		{
			return false;
		}
		ctrlFlag.voice.playStart = 0;
		return true;
	}

	private void SetClothStateStartMotion(int _cha, AnimationListInfo info)
	{
		if (chaFemales.Length > _cha)
		{
			byte state = (byte)info.nFemaleUpperCloths[_cha];
			byte state2 = (byte)info.nFemaleLowerCloths[_cha];
			GlobalMethod.SetAllClothState(chaFemales[_cha], _isUpper: true, state);
			GlobalMethod.SetAllClothState(chaFemales[_cha], _isUpper: false, state2);
		}
	}

	private bool ReturnToNormalFromTheAuto()
	{
		int animationListModeFromSelectInfo = GetAnimationListModeFromSelectInfo(ctrlFlag.nowAnimationInfo);
		if (animationListModeFromSelectInfo != -1)
		{
			foreach (AnimationListInfo item in lstAnimInfo[animationListModeFromSelectInfo])
			{
				if (item.id != ctrlFlag.nowAnimationInfo.nBackInitiativeID)
				{
					continue;
				}
				ctrlFlag.selectAnimationListInfo = item;
				break;
			}
		}
		return true;
	}

	private void EndProcADV()
	{
		if (!hSceneManager.bMerchant)
		{
			switch (hSceneManager.EventKind)
			{
			case HSceneManager.HEvent.Yobai:
			{
				Manager.ADV.ChangeADVCamera(hSceneManager.Agent[0].GetComponent<AgentActor>());
				hSceneManager.females[0].ChaControl.transform.localPosition = Vector3.zero;
				hSceneManager.females[0].ChaControl.transform.localRotation = Quaternion.identity;
				hSceneManager.females[0].ChaControl.animBody.transform.position = hSceneManager.females[0].Position;
				hSceneManager.females[0].ChaControl.animBody.transform.rotation = hSceneManager.females[0].Rotation;
				hSceneManager.HSceneUISet.SetActive(value: false);
				ShuffleRand shuffleRand = new ShuffleRand(100);
				if (shuffleRand.Get() < ctrlFlag.YobaiBareRate)
				{
					_bareYobai = true;
					openData.FindLoad("11", packData.charaID, packData.adv_category);
					packData.onComplete = delegate
					{
						CloseADV();
					};
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
					return;
				}
				_bareYobai = false;
				int personality = 0;
				if ((bool)hSceneManager.females[0].GetComponent<PlayerActor>())
				{
					personality = -100;
				}
				else if (hSceneManager.females[0].GetComponent<MerchantActor>() == null)
				{
					personality = hSceneManager.females[0].ChaControl.fileParam.personality;
				}
				Singleton<Game>.Instance.GetExpression(personality, "標準（目閉じ）")?.Change(hSceneManager.females[0].ChaControl);
				Observable.NextFrame().Subscribe(delegate
				{
					CloseADV();
				});
				return;
			}
			case HSceneManager.HEvent.Bath:
			case HSceneManager.HEvent.Toilet1:
			case HSceneManager.HEvent.Toilet2:
			case HSceneManager.HEvent.ShagmiBare:
			case HSceneManager.HEvent.Back:
			case HSceneManager.HEvent.Kitchen:
			case HSceneManager.HEvent.Tachi:
			case HSceneManager.HEvent.Stairs:
			case HSceneManager.HEvent.StairsBare:
			case HSceneManager.HEvent.MapBath:
			case HSceneManager.HEvent.KabeanaBack:
			case HSceneManager.HEvent.KabeanaFront:
			case HSceneManager.HEvent.Neonani:
			case HSceneManager.HEvent.TsukueBare:
				if (!ctrlFlag.isFaintness)
				{
					openData.FindLoad("9", packData.charaID, packData.adv_category);
				}
				else
				{
					openData.FindLoad("1", packData.charaID, packData.adv_category);
				}
				packData.onComplete = delegate
				{
					CloseADV();
				};
				Manager.ADV.ChangeADVCamera(hSceneManager.Agent[0].GetComponent<AgentActor>());
				hSceneManager.females[0].ChaControl.transform.localPosition = Vector3.zero;
				hSceneManager.females[0].ChaControl.transform.localRotation = Quaternion.identity;
				hSceneManager.females[0].ChaControl.animBody.transform.position = hSceneManager.females[0].Position;
				hSceneManager.females[0].ChaControl.animBody.transform.rotation = hSceneManager.females[0].Rotation;
				Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				return;
			case HSceneManager.HEvent.GyakuYobai:
				if (!HSceneManager.SleepStart)
				{
					if (!ctrlFlag.isFaintness)
					{
						openData.FindLoad("7", packData.charaID, packData.adv_category);
					}
					else
					{
						openData.FindLoad("1", packData.charaID, packData.adv_category);
					}
				}
				else if (!ctrlFlag.isFaintness)
				{
					openData.FindLoad("13", packData.charaID, packData.adv_category);
				}
				else
				{
					openData.FindLoad("14", packData.charaID, packData.adv_category);
				}
				packData.onComplete = delegate
				{
					CloseADV();
				};
				Manager.ADV.ChangeADVCamera(hSceneManager.Agent[0].GetComponent<AgentActor>());
				hSceneManager.females[0].ChaControl.transform.localPosition = Vector3.zero;
				hSceneManager.females[0].ChaControl.transform.localRotation = Quaternion.identity;
				hSceneManager.females[0].ChaControl.animBody.transform.position = hSceneManager.females[0].Position;
				hSceneManager.females[0].ChaControl.animBody.transform.rotation = hSceneManager.females[0].Rotation;
				Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				return;
			}
			if (hSceneManager.EventKind != HSceneManager.HEvent.FromFemale || hSceneManager.nInvitePtn != -1)
			{
				if (hSceneManager.females[1] == null || hSceneManager.females[1].GetComponent<PlayerActor>() != null)
				{
					bool flag = false;
					flag = hSceneManager.femalePlayerFinish == 0 && hSceneManager.maleFinish == 0 && ctrlFlag.numOrgasmTotal == 0;
					bool flag2 = ctrlFlag.isJudgeSelect.Contains(HSceneFlagCtrl.JudgeSelect.Constraint);
					bool flag3 = ctrlFlag.isJudgeSelect.Contains(HSceneFlagCtrl.JudgeSelect.Pain);
					if ((flag2 || flag3) && (hSceneManager.isForce || hSceneManager.GetFlaverSkillLevel(2) < 170))
					{
						if (flag3)
						{
							if (!ctrlFlag.isFaintness)
							{
								openData.FindLoad("5", packData.charaID, packData.adv_category);
								packData.isPainAction = ctrlFlag.isPainAction;
							}
							else
							{
								openData.FindLoad("1", packData.charaID, packData.adv_category);
							}
							packData.ConditionMode = 2;
							packData.onComplete = delegate
							{
								CloseADV();
							};
							Manager.ADV.ChangeADVCamera(hSceneManager.Agent[0]);
							hSceneManager.females[0].ChaControl.transform.localPosition = Vector3.zero;
							hSceneManager.females[0].ChaControl.transform.localRotation = Quaternion.identity;
							hSceneManager.females[0].ChaControl.animBody.transform.position = hSceneManager.females[0].Position;
							hSceneManager.females[0].ChaControl.animBody.transform.rotation = hSceneManager.females[0].Rotation;
							Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
						}
						else if (flag2)
						{
							if (!ctrlFlag.isFaintness)
							{
								openData.FindLoad("12", packData.charaID, packData.adv_category);
								packData.isConstraintAction = ctrlFlag.isConstraintAction;
							}
							else
							{
								openData.FindLoad("1", packData.charaID, packData.adv_category);
							}
							packData.ConditionMode = 2;
							packData.onComplete = delegate
							{
								CloseADV();
							};
							Manager.ADV.ChangeADVCamera(hSceneManager.Agent[0]);
							hSceneManager.females[0].ChaControl.transform.localPosition = Vector3.zero;
							hSceneManager.females[0].ChaControl.transform.localRotation = Quaternion.identity;
							hSceneManager.females[0].ChaControl.animBody.transform.position = hSceneManager.females[0].Position;
							hSceneManager.females[0].ChaControl.animBody.transform.rotation = hSceneManager.females[0].Rotation;
							Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
						}
					}
					else if (!ctrlFlag.isNotCtrl && !flag)
					{
						if (hSceneManager.femalePlayerFinish > 0 || hSceneManager.maleFinish > 0)
						{
							if (ctrlFlag.numOrgasmTotal == 0)
							{
								if (!ctrlFlag.isFaintness)
								{
									openData.FindLoad("3", packData.charaID, packData.adv_category);
								}
								else
								{
									openData.FindLoad("1", packData.charaID, packData.adv_category);
								}
								if (hSceneManager.isForce)
								{
									packData.ConditionMode = 2;
								}
								else
								{
									packData.ConditionMode = ((hSceneManager.Agent[0].ChaControl.fileGameInfo.phase >= 2) ? 1 : 0);
								}
								packData.onComplete = delegate
								{
									CloseADV();
								};
								Manager.ADV.ChangeADVCamera(hSceneManager.Agent[0]);
								hSceneManager.females[0].ChaControl.transform.localPosition = Vector3.zero;
								hSceneManager.females[0].ChaControl.transform.localRotation = Quaternion.identity;
								hSceneManager.females[0].ChaControl.animBody.transform.position = hSceneManager.females[0].Position;
								hSceneManager.females[0].ChaControl.animBody.transform.rotation = hSceneManager.females[0].Rotation;
								Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
							}
							else if (!ctrlFlag.isFaintness)
							{
								openData.FindLoad("0", packData.charaID, packData.adv_category);
								packData.numOrgasmFemale = ctrlFlag.numOrgasmTotal;
								if (hSceneManager.isForce)
								{
									packData.ConditionMode = 2;
								}
								else
								{
									packData.ConditionMode = ((hSceneManager.Agent[0].ChaControl.fileGameInfo.phase >= 2) ? 1 : 0);
								}
								packData.onComplete = delegate
								{
									CloseADV();
								};
								Manager.ADV.ChangeADVCamera(hSceneManager.Agent[0]);
								hSceneManager.females[0].ChaControl.transform.localPosition = Vector3.zero;
								hSceneManager.females[0].ChaControl.transform.localRotation = Quaternion.identity;
								hSceneManager.females[0].ChaControl.animBody.transform.position = hSceneManager.females[0].Position;
								hSceneManager.females[0].ChaControl.animBody.transform.rotation = hSceneManager.females[0].Rotation;
								Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
							}
							else
							{
								openData.FindLoad("1", packData.charaID, packData.adv_category);
								if (hSceneManager.isForce)
								{
									packData.ConditionMode = 2;
								}
								else
								{
									packData.ConditionMode = ((hSceneManager.Agent[0].ChaControl.fileGameInfo.phase >= 2) ? 1 : 0);
								}
								packData.onComplete = delegate
								{
									CloseADV();
								};
								Manager.ADV.ChangeADVCamera(hSceneManager.Agent[0]);
								hSceneManager.females[0].ChaControl.transform.localPosition = Vector3.zero;
								hSceneManager.females[0].ChaControl.transform.localRotation = Quaternion.identity;
								hSceneManager.females[0].ChaControl.animBody.transform.position = hSceneManager.females[0].Position;
								hSceneManager.females[0].ChaControl.animBody.transform.rotation = hSceneManager.females[0].Rotation;
								Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
							}
						}
						else
						{
							if (!ctrlFlag.isFaintness)
							{
								openData.FindLoad("2", packData.charaID, packData.adv_category);
							}
							else
							{
								openData.FindLoad("1", packData.charaID, packData.adv_category);
							}
							if (hSceneManager.isForce)
							{
								packData.ConditionMode = 2;
							}
							else
							{
								packData.ConditionMode = ((hSceneManager.Agent[0].ChaControl.fileGameInfo.phase >= 2) ? 1 : 0);
							}
							packData.onComplete = delegate
							{
								CloseADV();
							};
							Manager.ADV.ChangeADVCamera(hSceneManager.Agent[0]);
							hSceneManager.females[0].ChaControl.transform.localPosition = Vector3.zero;
							hSceneManager.females[0].ChaControl.transform.localRotation = Quaternion.identity;
							hSceneManager.females[0].ChaControl.animBody.transform.position = hSceneManager.females[0].Position;
							hSceneManager.females[0].ChaControl.animBody.transform.rotation = hSceneManager.females[0].Rotation;
							Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
						}
					}
					else
					{
						openData.FindLoad("4", packData.charaID, packData.adv_category);
						if (hSceneManager.isForce)
						{
							packData.ConditionMode = 2;
						}
						else
						{
							packData.ConditionMode = ((hSceneManager.Agent[0].ChaControl.fileGameInfo.phase >= 2) ? 1 : 0);
						}
						packData.onComplete = delegate
						{
							CloseADV();
						};
						Manager.ADV.ChangeADVCamera(hSceneManager.Agent[0].GetComponent<AgentActor>());
						hSceneManager.females[0].ChaControl.transform.localPosition = Vector3.zero;
						hSceneManager.females[0].ChaControl.transform.localRotation = Quaternion.identity;
						hSceneManager.females[0].ChaControl.animBody.transform.position = hSceneManager.females[0].Position;
						hSceneManager.females[0].ChaControl.animBody.transform.rotation = hSceneManager.females[0].Rotation;
						Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
					}
				}
				else if (hSceneManager.females[1] != null && hSceneManager.females[1].GetComponent<PlayerActor>() == null)
				{
					if (!ctrlFlag.isFaintness)
					{
						openData.FindLoad("10", packData.charaID, packData.adv_category);
					}
					else
					{
						openData.FindLoad("1", packData.charaID, packData.adv_category);
					}
					packData.onComplete = delegate
					{
						CloseADV();
					};
					Manager.ADV.ChangeADVCamera(hSceneManager.Agent[0].GetComponent<AgentActor>());
					hSceneManager.females[0].ChaControl.transform.localPosition = Vector3.zero;
					hSceneManager.females[0].ChaControl.transform.localRotation = Quaternion.identity;
					hSceneManager.females[0].ChaControl.animBody.transform.position = hSceneManager.females[0].Position;
					hSceneManager.females[0].ChaControl.animBody.transform.rotation = hSceneManager.females[0].Rotation;
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				}
			}
			else if (hSceneManager.EventKind == HSceneManager.HEvent.FromFemale || hSceneManager.nInvitePtn == 0)
			{
				if (!ctrlFlag.isFaintness)
				{
					openData.FindLoad("6", packData.charaID, packData.adv_category);
				}
				else
				{
					openData.FindLoad("1", packData.charaID, packData.adv_category);
				}
				if (hSceneManager.isForce)
				{
					packData.ConditionMode = 2;
				}
				else
				{
					packData.ConditionMode = ((hSceneManager.Agent[0].ChaControl.fileGameInfo.phase >= 2) ? 1 : 0);
				}
				packData.onComplete = delegate
				{
					CloseADV();
				};
				Manager.ADV.ChangeADVCamera(hSceneManager.Agent[0].GetComponent<AgentActor>());
				hSceneManager.females[0].ChaControl.transform.localPosition = Vector3.zero;
				hSceneManager.females[0].ChaControl.transform.localRotation = Quaternion.identity;
				hSceneManager.females[0].ChaControl.animBody.transform.position = hSceneManager.females[0].Position;
				hSceneManager.females[0].ChaControl.animBody.transform.rotation = hSceneManager.females[0].Rotation;
				Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
			}
			else if (hSceneManager.nInvitePtn == 1)
			{
				if (!ctrlFlag.isFaintness)
				{
					openData.FindLoad("8", packData.charaID, packData.adv_category);
				}
				else
				{
					openData.FindLoad("1", packData.charaID, packData.adv_category);
				}
				if (hSceneManager.isForce)
				{
					packData.ConditionMode = 2;
				}
				else
				{
					packData.ConditionMode = ((hSceneManager.Agent[0].ChaControl.fileGameInfo.phase >= 2) ? 1 : 0);
				}
				packData.onComplete = delegate
				{
					CloseADV();
				};
				Manager.ADV.ChangeADVCamera(hSceneManager.Agent[0].GetComponent<AgentActor>());
				hSceneManager.females[0].ChaControl.transform.localPosition = Vector3.zero;
				hSceneManager.females[0].ChaControl.transform.localRotation = Quaternion.identity;
				hSceneManager.females[0].ChaControl.animBody.transform.position = hSceneManager.females[0].Position;
				hSceneManager.females[0].ChaControl.animBody.transform.rotation = hSceneManager.females[0].Rotation;
				Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
			}
		}
		else
		{
			openData.FindLoad("0", packData.charaID, packData.adv_category);
			packData.onComplete = delegate
			{
				CloseADV();
			};
			Manager.ADV.ChangeADVCamera(hSceneManager.merchantActor);
			packData.JumpTag = "OUT";
			packData.isWeaknessH = ctrlFlag.isFaintness;
			Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
		}
	}

	private void CloseADV()
	{
		if (packData != null)
		{
			packData.Release();
		}
		int num = Config.GraphicData.MaxCharaNum - 1;
		if (!MapScene.EqualsSequence(prevCharaEntry, Config.GraphicData.CharasEntry))
		{
			Singleton<Manager.Map>.Instance.ApplyConfig(delegate
			{
				CloseADVProc();
			}, null);
		}
		else
		{
			CloseADVProc();
		}
	}

	private void CloseADVProc()
	{
		for (int i = 0; i < hSceneManager.females.Length; i++)
		{
			hSceneManager.females[i] = null;
		}
		hSceneManager.male = null;
		MerchantActor merchant = Singleton<Manager.Map>.Instance.Merchant;
		if (hSceneManager.Player.CameraControl.Mode != 0)
		{
			hSceneManager.Player.CameraControl.Mode = CameraMode.Normal;
		}
		int shortNameHash = hSceneManager.Player.ChaControl.animBody.GetCurrentAnimatorStateInfo(0).shortNameHash;
		hSceneManager.Player.ChaControl.SetAccessoryStateAll(show: true);
		hSceneManager.Player.ChaControl.SetClothesStateAll(0);
		hSceneManager.Player.Animation.enabled = true;
		hSceneManager.Player.Animation.SetAnimatorController(hSceneManager.Player.ChaControl.animBody.runtimeAnimatorController);
		hSceneManager.Player.Animation.MapIK.Calc(shortNameHash);
		hSceneManager.Player.Controller.enabled = true;
		hSceneManager.Player.CameraControl.enabled = true;
		if (!hSceneManager.Player.NavMeshAgent.isOnNavMesh)
		{
			Vector3 position = hSceneManager.Player.Position;
			hSceneManager.Player.NavMeshWarp(position, hSceneManager.Player.Rotation, 3, 200f);
		}
		hSceneManager.Player.ChaControl.transform.position = Vector3.zero;
		hSceneManager.Player.ChaControl.transform.rotation = Quaternion.identity;
		if (hSceneManager.Player.CurrentPoint != null)
		{
			hSceneManager.Player.CurrentPoint.RemoveBookingUser(hSceneManager.Player);
		}
		hSceneManager.Player.ReleaseCurrentPoint();
		if (!hSceneManager.bMerchant)
		{
			for (int j = 0; j < hSceneManager.Agent.Length; j++)
			{
				if (!(hSceneManager.Agent[j] == null))
				{
					hSceneManager.Agent[j].ChaControl.transform.position = Vector3.zero;
					hSceneManager.Agent[j].ChaControl.transform.rotation = Quaternion.identity;
					hSceneManager.Agent[j].ChaControl.ChangeNowCoordinate(reload: true);
				}
			}
		}
		else
		{
			merchant.ChaControl.transform.position = Vector3.zero;
			merchant.ChaControl.transform.rotation = Quaternion.identity;
		}
		if (!hSceneManager.Player.ChaControl.visibleAll)
		{
			hSceneManager.Player.ChaControl.visibleAll = true;
		}
		hSceneManager.Player.Controller.ChangeState("Normal");
		if (hSceneManager.Player.ChaControl.sex == 1 && !hSceneManager.bFutanari)
		{
			hSceneManager.Player.ChaControl.ChangeNowCoordinate(reload: true);
		}
		if (hSceneManager.Player.Partner != null)
		{
			AgentActor component = hSceneManager.Player.Partner.GetComponent<AgentActor>();
			component.DeactivatePairing(0);
			component.ActivateHoldingHands(0, enabled: false);
		}
		int num = 0;
		foreach (AgentActor value in Singleton<Manager.Map>.Instance.AgentTable.Values)
		{
			if (value == null || hSceneManager.ReturnActionTypes.ContainsKey(value))
			{
				continue;
			}
			shortNameHash = value.ChaControl.animBody.GetCurrentAnimatorStateInfo(0).shortNameHash;
			value.AnimationAgent.enabled = true;
			value.AnimationAgent.StopAllAnimCoroutine();
			value.AnimationAgent.EndIgnoreExpression();
			value.AnimationAgent.ResetDefaultAnimatorController();
			value.AnimationAgent.SetAnimatorController(value.ChaControl.animBody.runtimeAnimatorController);
			value.AnimationAgent.MapIK.Calc(shortNameHash);
			int desireKey = Desire.GetDesireKey(Desire.Type.H);
			value.SetDesire(desireKey, 0f);
			EmptyImmoral(value);
			value.Controller.enabled = true;
			value.AnimationAgent.EnableItems();
			value.EnableBehavior();
			value.LocomotorAgent.enabled = true;
			value.ActivateNavMeshAgent();
			if (!value.NavMeshAgent.isOnNavMesh)
			{
				Vector3 position2 = value.Position;
				value.NavMeshWarp(position2, value.Rotation, num++, 200f);
			}
			value.ChaControl.visibleAll = true;
			value.ResetActionFlag();
			if (hSceneManager.EventKind == HSceneManager.HEvent.Yobai)
			{
				if (_bareYobai)
				{
					value.ChangeBehavior(Desire.ActionType.Normal);
				}
				else
				{
					value.RecoverAction();
				}
			}
			else if (hSceneManager.endStatus == 1 && !ctrlFlag.nowHPoint._nPlace.Exists((KeyValuePair<int, UnityEx.ValueTuple<int, int>> x) => x.Value.Item1 == 11 || x.Value.Item1 == 13 || x.Value.Item1 == 14))
			{
				value.StartWeakness();
			}
			else
			{
				value.ChangeBehavior(Desire.ActionType.Normal);
			}
		}
		if (hSceneManager.bMerchant)
		{
			shortNameHash = merchant.ChaControl.animBody.GetCurrentAnimatorStateInfo(0).shortNameHash;
			merchant.AnimationMerchant.enabled = true;
			merchant.AnimationMerchant.EndIgnoreExpression();
			merchant.AnimationMerchant.SetAnimatorController(merchant.ChaControl.animBody.runtimeAnimatorController);
			merchant.AnimationMerchant.MapIK.Calc(shortNameHash);
			merchant.Controller.enabled = true;
			merchant.LocomotorMerchant.enabled = true;
			merchant.EnableBehavior();
			merchant.ChangeBehavior(merchant.LastNormalMode);
			merchant.ChaControl.ChangeNowCoordinate(reload: true);
		}
		hSceneManager.Player.enabled = true;
		hSceneManager.Player.HandsHolder.enabled = hSceneManager.handsIK;
		hSceneManager.Player.SetActiveOnEquipedItem(active: true);
		AgentActor[] agent = hSceneManager.Agent;
		foreach (AgentActor agentActor in agent)
		{
			if (!(agentActor == null))
			{
				agentActor.SetActiveOnEquipedItem(active: true);
			}
		}
		if (hSceneManager.HSceneSet.activeSelf)
		{
			hSceneManager.HSceneSet.SetActive(value: false);
		}
		Singleton<Manager.ADV>.Instance.Captions.EndADV();
		Singleton<MapUIContainer>.Instance.MinimapUI.MiniMap.SetActive(value: true);
		Singleton<MapUIContainer>.Instance.MinimapUI.MiniMapIcon.SetActive(value: true);
		Singleton<Manager.Map>.Instance.SetActiveMapEffect(active: true);
		MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.InvokeAcception);
		Observable.NextFrame().Subscribe(delegate
		{
			MapUIContainer.SetVisibleHUD(active: true);
		});
	}

	private RuntimeAnimatorController MixRuntimeControler(RuntimeAnimatorController src, RuntimeAnimatorController over1, RuntimeAnimatorController over2)
	{
		if (src == null || over1 == null || over2 == null)
		{
			return null;
		}
		AnimatorOverrideController animatorOverrideController = new AnimatorOverrideController(src);
		List<AnimationClip> list = new List<AnimationClip>();
		list.AddRange(new AnimatorOverrideController(over1).animationClips);
		list.AddRange(new AnimatorOverrideController(over2).animationClips);
		foreach (AnimationClip item in list)
		{
			animatorOverrideController[item.name] = item;
		}
		animatorOverrideController.name = over1.name;
		return animatorOverrideController;
	}

	private void EndAnimChange()
	{
		List<EndMotion> lstEndAnimInfo = Singleton<Manager.Resources>.Instance.HSceneTable.lstEndAnimInfo;
		AnimationListInfo nowAnimationInfo = ctrlFlag.nowAnimationInfo;
		int num = 0;
		num = ((!(hSceneManager.females[0].GetComponent<MerchantActor>() == null)) ? 5 : chaFemales[0].fileParam.personality);
		int shortNameHash;
		if (!hSceneManager.bMerchant)
		{
			chaFemales[0].LoadAnimation(lstEndAnimInfo[num].abNameH[0], lstEndAnimInfo[num].assetNameH[0], string.Empty);
			chaFemales[0].setPlay(lstEndAnimInfo[num].stateNameH[0], 0);
			shortNameHash = chaFemales[0].animBody.GetCurrentAnimatorStateInfo(0).shortNameHash;
			chaFemales[0].animBody.GetComponent<ActorAnimationAgent>().MapIK.Calc(shortNameHash);
		}
		else
		{
			chaFemales[0].LoadAnimation(lstEndAnimInfo[num].abNameM, lstEndAnimInfo[num].assetNameM, string.Empty);
			chaFemales[0].setPlay(lstEndAnimInfo[num].stateNameM, 0);
			shortNameHash = chaFemales[0].animBody.GetCurrentAnimatorStateInfo(0).shortNameHash;
			chaFemales[0].animBody.GetComponent<ActorAnimationMerchant>().MapIK.Calc(shortNameHash);
		}
		if (chaFemales[1] != null && chaFemales[1].objTop != null && chaFemales[1] != hSceneManager.Player.ChaControl)
		{
			chaFemales[1].LoadAnimation(lstEndAnimInfo[num].abNameH[1], lstEndAnimInfo[num].assetNameH[1], string.Empty);
			chaFemales[1].setPlay(lstEndAnimInfo[num].stateNameH[1], 0);
			shortNameHash = chaFemales[1].animBody.GetCurrentAnimatorStateInfo(0).shortNameHash;
			chaFemales[1].animBody.GetComponent<ActorAnimationAgent>().MapIK.Calc(shortNameHash);
		}
		hSceneManager.Player.ChaControl.LoadAnimation(lstEndAnimInfo[num].abNameP[hSceneManager.Player.ChaControl.sex], lstEndAnimInfo[num].assetNameP[hSceneManager.Player.ChaControl.sex], string.Empty);
		hSceneManager.Player.ChaControl.setPlay(lstEndAnimInfo[num].stateNameP[hSceneManager.Player.ChaControl.sex], 0);
		shortNameHash = hSceneManager.Player.ChaControl.animBody.GetCurrentAnimatorStateInfo(0).shortNameHash;
		hSceneManager.Player.ChaControl.animBody.GetComponent<ActorAnimationPlayer>().MapIK.Calc(shortNameHash);
		fade.FadeStart(1f);
	}

	private IEnumerator BeforeWait()
	{
		if (preBeforWaitState == null)
		{
			preBeforWaitState = new Dictionary<int, int>();
		}
		else
		{
			preBeforWaitState.Clear();
		}
		int ID = -1;
		if (hSceneManager.EventKind == HSceneManager.HEvent.Normal)
		{
			switch (hSceneManager.height)
			{
			case 0:
				ID = 2;
				break;
			case 2:
				ID = 3;
				break;
			case 3:
			case 6:
			case 7:
				ID = 1;
				break;
			case 4:
				ID = ((!hSceneManager.onDeskChair) ? 4 : 5);
				break;
			default:
				ID = 0;
				break;
			}
			if (!hSceneManager.bMerchant && hSceneManager.Agent[0].ActionID == 0 && hSceneManager.Agent[0].PoseID == 3)
			{
				ID = 2;
			}
		}
		if (hSceneManager.females[1] != null && hSceneManager.females[1].GetComponent<PlayerActor>() == null)
		{
			ID = 0;
		}
		foreach (StartWaitAnim startWaitAnim in Singleton<Manager.Resources>.Instance.HSceneTable.startWaitAnims)
		{
			if (startWaitAnim.ID == ID)
			{
				startWait = startWaitAnim;
			}
		}
		Camera = hSceneManager.Player.CameraControl;
		Vector3 pos = chaFemalesTrans[0].position;
		GlobalMethod.setCameraBase(Singleton<HSceneFlagCtrl>.Instance.cameraCtrl, pos, chaFemalesTrans[0].rotation.eulerAngles);
		Camera.HCamera = ctrlFlag.cameraCtrl;
		Transform parent = ctrlFlag.HBeforeCamera.transform.parent;
		parent.position = pos;
		parent.rotation = chaFemalesTrans[0].rotation;
		ctrlFlag.HBeforeCamera.gameObject.SetActive(value: true);
		if (startWait == null)
		{
			GlobalMethod.loadCamera(ctrlFlag.HBeforeCamera, hSceneManager.strAssetCameraList, "DefaultCameraData");
		}
		else
		{
			GlobalMethod.loadCamera(ctrlFlag.HBeforeCamera, hSceneManager.strAssetCameraList, startWait.CameraFile);
		}
		yield return null;
		if (startWait == null)
		{
			yield break;
		}
		hSceneManager.Player.ChaControl.transform.position = chaFemalesTrans[0].position;
		hSceneManager.Player.ChaControl.transform.rotation = chaFemalesTrans[0].rotation;
		hSceneManager.Player.ChaControl.animBody.transform.localPosition = Vector3.zero;
		hSceneManager.Player.ChaControl.animBody.transform.localRotation = Quaternion.identity;
		if (startWait.VisibleMode != 1)
		{
			hSceneManager.Player.ChaControl.visibleAll = true;
			hSceneManager.Player.SetActiveOnEquipedItem(active: false);
			hSceneManager.Player.ChaControl.setAllLayerWeight(0f);
			int num = ((Singleton<Manager.Map>.Instance.Player.ChaControl.sex != 0 && !hSceneManager.bFutanari) ? 1 : 0);
			racBeforeWait = CommonLib.LoadAsset<RuntimeAnimatorController>(startWait.Player[num].abName, startWait.Player[num].assetName, clone: false, string.Empty);
			if ((bool)racBeforeWait)
			{
				hSceneManager.Player.Animation.SetAnimatorController(racBeforeWait);
				hSceneManager.Player.ChaControl.setPlay(startWait.Player[num].State, 0);
				AssetBundleManager.UnloadAssetBundle(startWait.Player[num].abName, isUnloadForceRefCount: true);
				int animStateHash2 = hSceneManager.Player.ChaControl.animBody.GetCurrentAnimatorStateInfo(0).shortNameHash;
				preBeforWaitState.Add(0, animStateHash2);
				hSceneManager.Player.Animation.MapIK.Calc(animStateHash2);
			}
		}
		for (int i = 0; i < hSceneManager.Agent.Length; i++)
		{
			if (!(hSceneManager.Agent[i] == null) && !(hSceneManager.Agent[i].ChaControl == null) && !(hSceneManager.Agent[i].ChaControl.animBody == null))
			{
				racBeforeWait = CommonLib.LoadAsset<RuntimeAnimatorController>(startWait.Agent[i].abName, startWait.Agent[i].assetName, clone: false, string.Empty);
				if (!(racBeforeWait == null))
				{
					hSceneManager.Agent[i].AnimationAgent.SetAnimatorController(racBeforeWait);
					hSceneManager.Agent[i].ChaControl.setPlay(startWait.Agent[i].State, 0);
					float shapeBodyValue = hSceneManager.Agent[i].ChaControl.GetShapeBodyValue(0);
					hSceneManager.Agent[i].AnimationAgent.SetFloat("height", shapeBodyValue);
					AssetBundleManager.UnloadAssetBundle(startWait.Agent[i].abName, isUnloadForceRefCount: true);
					int animStateHash2 = hSceneManager.Agent[i].ChaControl.animBody.GetCurrentAnimatorStateInfo(0).shortNameHash;
					preBeforWaitState.Add(1 + i, animStateHash2);
					hSceneManager.Agent[i].AnimationAgent.MapIK.Calc(animStateHash2);
				}
			}
		}
		yield return null;
	}

	public void LoadMoveOffset(string file, out Vector3 pos, out Vector3 rot)
	{
		string text = GlobalMethod.LoadAllListText(hSceneManager.strAssetMoveOffsetListFolder, file);
		GlobalMethod.GetListString(text, out var data);
		pos = Vector3.zero;
		rot = Vector3.zero;
		int length = data.GetLength(0);
		for (int i = 0; i < length; i++)
		{
			int num = 0;
			pos.x = float.Parse(data[i][num++]);
			pos.y = float.Parse(data[i][num++]);
			pos.z = float.Parse(data[i][num++]);
			rot.x = float.Parse(data[i][num++]);
			rot.y = float.Parse(data[i][num++]);
			rot.z = float.Parse(data[i][num++]);
		}
	}

	private void EmptyImmoral(AgentActor agent)
	{
		agent.SetDefaultImmoral();
	}

	public ChaControl[] GetFemales()
	{
		return chaFemales;
	}

	private void HousingVanishSet()
	{
		if (!(Singleton<Manager.Map>.Instance.PointAgent != null))
		{
			return;
		}
		BasePoint[] basePoints = Singleton<Manager.Map>.Instance.PointAgent.BasePoints;
		if (basePoints == null)
		{
			return;
		}
		int mapID = Singleton<Manager.Map>.Instance.MapID;
		int num = -1;
		Dictionary<int, Dictionary<int, List<int>>> vanishHousingAreaGroup = Singleton<Manager.Resources>.Instance.Map.VanishHousingAreaGroup;
		if (vanishHousingAreaGroup == null || !vanishHousingAreaGroup.TryGetValue(mapID, out var value))
		{
			return;
		}
		foreach (KeyValuePair<int, List<int>> item in value)
		{
			if (!item.Value.Contains(hSceneManager.Player.AreaID))
			{
				continue;
			}
			num = item.Key;
			break;
		}
		if (num < 0)
		{
			return;
		}
		for (int i = 0; i < basePoints.Length; i++)
		{
			if (!(basePoints[i].OwnerArea == null) && value[num].Contains(basePoints[i].OwnerArea.AreaID) && basePoints[i].ID >= 0)
			{
				Singleton<Manager.Housing>.Instance.StartShield(basePoints[i].ID);
				break;
			}
		}
	}

	private IEnumerator PutHmesh()
	{
		if (hSceneManager.EventKind == HSceneManager.HEvent.Yobai)
		{
			StartPos.position = hSceneManager.Agent[0].Position;
			yield break;
		}
		GameObject hMeshDataObj = null;
		Singleton<Manager.Resources>.Instance.HSceneTable.HMeshObjDic.TryGetValue(Singleton<Manager.Map>.Instance.MapID, out hMeshDataObj);
		HMeshData hMeshData = ((!(hMeshDataObj != null)) ? null : hMeshDataObj.GetComponent<HMeshData>());
		int areaid = hSceneManager.Player.AreaID;
		Vector3 allNearestVertex = Vector3.zero;
		float allMiniDistanceSqr = float.PositiveInfinity;
		int GpContain = -1;
		Dictionary<int, List<int>> areagpIds = null;
		int mapID = Singleton<Manager.Map>.Instance.MapID;
		Dictionary<int, Dictionary<int, List<int>>> vanishAreaGp = Singleton<Manager.Resources>.Instance.Map.VanishHousingAreaGroup;
		if (vanishAreaGp != null && vanishAreaGp.TryGetValue(mapID, out areagpIds))
		{
			foreach (KeyValuePair<int, List<int>> item in areagpIds)
			{
				if (!item.Value.Contains(areaid))
				{
					continue;
				}
				GpContain = item.Key;
				break;
			}
		}
		string tag = string.Empty;
		if (GpContain < 0)
		{
			PutHmesh(ref allNearestVertex, ref allMiniDistanceSqr, hMeshData, areaid, ref tag);
		}
		else
		{
			for (int i = 0; i < areagpIds[GpContain].Count; i++)
			{
				PutHmesh(ref allNearestVertex, ref allMiniDistanceSqr, hMeshData, areagpIds[GpContain][i], ref tag);
			}
		}
		Vector3 pos2 = Vector3.zero;
		if (hSceneManager.bMerchant)
		{
			hSceneManager.merchantActor.ActivateNavMeshAgent();
			hSceneManager.merchantActor.NavMeshAgent.Warp(allNearestVertex);
			yield return null;
			pos2 = hSceneManager.merchantActor.Position;
			hSceneManager.merchantActor.DeactivateNavMeshAgent();
		}
		else
		{
			hSceneManager.Agent[0].ActivateNavMeshAgent();
			hSceneManager.Agent[0].NavMeshAgent.Warp(allNearestVertex);
			yield return null;
			pos2 = hSceneManager.Agent[0].Position;
			hSceneManager.Agent[0].DeactivateNavMeshAgent();
		}
		StartPos.position = pos2;
		bool forceFloor3 = hSceneManager.EventKind == HSceneManager.HEvent.FromFemale;
		forceFloor3 |= hSceneManager.EventKind == HSceneManager.HEvent.GyakuYobai;
		forceFloor3 |= hSceneManager.EventKind == HSceneManager.HEvent.Yobai;
		if ((hSceneManager.females[1] == null || hSceneManager.females[1].GetComponent<PlayerActor>() != null) && !forceFloor3)
		{
			if (HSceneManager.HmeshTag.ContainsKey(tag))
			{
				hSceneManager.height = ((HSceneManager.HmeshTag[tag] < 0) ? 1 : HSceneManager.HmeshTag[tag]);
			}
			else
			{
				hSceneManager.height = 1;
				Debug.LogWarning($"吸われる予定のHメッシュのタグがおかしい(タグ：{tag} 場所強制 1(立ち))");
			}
		}
		else if (HSceneManager.HmeshTag.ContainsKey(tag))
		{
			hSceneManager.height = ((HSceneManager.HmeshTag[tag] >= 0) ? HSceneManager.HmeshTag[tag] : 0);
		}
		else
		{
			hSceneManager.height = 0;
			Debug.LogWarning($"吸われる予定のHメッシュのタグがおかしい(タグ：{tag} 場所強制 0(床))");
		}
		if (HSceneManager.SleepStart)
		{
			hSceneManager.height = 0;
		}
	}

	private void PutHmesh(ref Vector3 allNearestVertex, ref float allMiniDistanceSqr, HMeshData hMeshData, int areaid, ref string tag)
	{
		if (hMeshData == null || !hMeshData.dicColliders.TryGetValue(areaid, out var value))
		{
			value = null;
		}
		Vector3 position = hSceneManager.females[0].Position;
		StringBuilder stringBuilder = StringBuilderPool.Get();
		if (value != null)
		{
			Collider[] array = value;
			foreach (Collider collider in array)
			{
				stringBuilder.Clear();
				stringBuilder.Append(collider.gameObject.tag);
				if (stringBuilder.ToString() == "Untagged")
				{
					continue;
				}
				if (!hSceneManager.bMerchant)
				{
					if (hSceneManager.EventKind == HSceneManager.HEvent.FromFemale)
					{
						if ((Singleton<Manager.Map>.Instance.Player.ChaControl.sex == 0 || (Singleton<Manager.Map>.Instance.Player.ChaControl.sex == 1 && Singleton<HSceneManager>.Instance.bFutanari)) && stringBuilder.ToString() != "standfloor" && stringBuilder.ToString() != "bed")
						{
							continue;
						}
					}
					else if (hSceneManager.EventKind == HSceneManager.HEvent.Yobai && stringBuilder.ToString() != "standfloor" && stringBuilder.ToString() != "bed" && stringBuilder.ToString() != "sofabed")
					{
						continue;
					}
					if ((Singleton<Manager.Map>.Instance.Player.ChaControl.sex == 1 && !Singleton<HSceneManager>.Instance.bFutanari && !HSceneManager.HmeshLesTag.ContainsKey(stringBuilder.ToString())) || (hSceneManager.EventKind != HSceneManager.HEvent.GyakuYobai && HSceneManager.SleepStart && stringBuilder.ToString() != "standfloor" && stringBuilder.ToString() != "bed"))
					{
						continue;
					}
				}
				else if ("sofa" == stringBuilder.ToString() || !HSceneManager.HmeshTag.ContainsKey(stringBuilder.ToString()) || HSceneManager.HmeshTag[stringBuilder.ToString()] > 4)
				{
					continue;
				}
				Mesh sharedMesh = collider.GetComponent<MeshCollider>().sharedMesh;
				float num = float.PositiveInfinity;
				Vector3 vector = Vector3.zero;
				Vector3[] vertices = sharedMesh.vertices;
				for (int j = 0; j < vertices.Length; j++)
				{
					Vector3 vector2 = vertices[j];
					if (!(vector2.y > ((!hSceneManager.bMerchant) ? hSceneManager.Agent[0].ChaControl.objHeadBone.transform.position.y : hSceneManager.merchantActor.ChaControl.objHeadBone.transform.position.y)))
					{
						Vector3 vector3 = new Vector3(vector2.x, vector2.y, vector2.z);
						if (Singleton<Manager.Map>.Instance.MapID == 0 && (areaid == 11 || areaid == 12 || areaid == 13))
						{
							Transform transform = collider.transform;
							vector3.x *= transform.localScale.x;
							vector3.y *= transform.localScale.y;
							vector3.z *= transform.localScale.z;
							vector3 = transform.rotation * vector3;
							vector3 += transform.position;
						}
						float sqrMagnitude = (position - vector3).sqrMagnitude;
						if (sqrMagnitude < num)
						{
							num = sqrMagnitude;
							vector = vector3;
						}
					}
				}
				if (num < allMiniDistanceSqr)
				{
					allNearestVertex = vector;
					tag = collider.gameObject.tag;
					allMiniDistanceSqr = num;
				}
			}
		}
		StringBuilderPool.Release(stringBuilder);
	}

	public ProcBase GetProcBase()
	{
		if (mode == -1 || lstProc.Count < mode)
		{
			return null;
		}
		return lstProc[mode];
	}

	private void PlayerWet()
	{
		if (hSceneManager.Player == null)
		{
			return;
		}
		PlayerData playerData = hSceneManager.Player.PlayerData;
		StatusProfile statusProfile = Singleton<Manager.Resources>.Instance.StatusProfile;
		EnvironmentSimulator simulator = Singleton<Manager.Map>.Instance.Simulator;
		if (simulator.EnabledTimeProgression)
		{
			Weather weather = simulator.Weather;
			if (hSceneManager.Player.AreaType == MapArea.AreaType.Indoor)
			{
				playerData.Wetness += statusProfile.DrySpeed * Time.deltaTime;
			}
			else
			{
				switch (weather)
				{
				case Weather.Rain:
					playerData.Wetness += statusProfile.WetRateInRain * Time.deltaTime;
					break;
				case Weather.Storm:
					playerData.Wetness += statusProfile.WetRateInStorm * Time.deltaTime;
					break;
				default:
					playerData.Wetness += statusProfile.DrySpeed * Time.deltaTime;
					break;
				}
				playerData.Wetness = Mathf.Clamp(playerData.Wetness, 0f, 100f);
			}
		}
		if (hSceneManager.Player.ChaControl != null)
		{
			float wetRate = Mathf.InverseLerp(0f, 100f, playerData.Wetness);
			hSceneManager.Player.ChaControl.wetRate = wetRate;
		}
	}

	public ChaControl[] GetMales()
	{
		return chaMales;
	}
}
