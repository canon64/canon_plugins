using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AIChara;
using AIProject;
using AIProject.Animal;
using AIProject.Definitions;
using AIProject.SaveData;
using Correct;
using Housing;
using UniRx;
using UnityEngine;
using UnityEx;

namespace Manager;

public class HSceneManager : Singleton<HSceneManager>
{
	public enum HEvent
	{
		Hpoint = -1,
		Normal,
		Yobai,
		Bath,
		Toilet1,
		Toilet2,
		ShagmiBare,
		Back,
		Kitchen,
		Tachi,
		Stairs,
		StairsBare,
		GyakuYobai,
		FromFemale,
		MapBath,
		KabeanaBack,
		KabeanaFront,
		Neonani,
		TsukueBare
	}

	public PlayerActor Player;

	public AgentActor[] Agent = new AgentActor[2];

	public MerchantActor merchantActor;

	public Actor[] females = new Actor[2];

	public Actor male;

	public string[] pngFemales = new string[2];

	public string pngMale;

	public HEvent EventKind;

	public HashSet<string> hashUseAssetBundle = new HashSet<string>();

	public bool bMerchant;

	public int MerchantLimit = -1;

	public float SerchHmeshRange = 50f;

	public GameObject HSceneSet;

	public GameObject HSceneUISet;

	public int numFemaleClothCustom;

	public Dictionary<int, int> HSkil = new Dictionary<int, int>
	{
		{ 0, 0 },
		{ 1, 0 },
		{ 2, 0 },
		{ 3, 0 },
		{ 4, 0 }
	};

	public int[] PersonalPhase = new int[2] { -1, -1 };

	public int[] Personality = new int[2];

	public int hMainKind;

	public int height = -1;

	public bool isForce;

	public float Temperature;

	public float[] Mood = new float[2];

	public float Toilet;

	public float? ReserveToilet;

	public float Bath;

	private int Reliability;

	private int Instinct;

	private int Dirty;

	private int RiskManagement;

	private int Darkness;

	private int Sociability;

	private int Pheromone;

	public bool handsIK;

	[DisabledGroup("ふたなりボディか")]
	public bool bFutanari;

	[DisabledGroup("ヒロインからHに誘ってきたパターン")]
	public int nInvitePtn = -1;

	private bool bSleepStart;

	public UnityEx.ValueTuple<RaycastHit, GameObject, int> hitHmesh = default(UnityEx.ValueTuple<RaycastHit, GameObject, int>);

	public RaycastHit[] hits;

	[DisabledGroup("cameramesh")]
	public GameObject CameraMesh;

	public static readonly Dictionary<string, int> HmeshTag = new Dictionary<string, int>
	{
		{ "bath", 11 },
		{ "stand", 1 },
		{ "standfloor", -1 },
		{ "wall", 2 },
		{ "bed", 0 },
		{ "chair", 3 },
		{ "table", 4 },
		{ "stairs", 10 },
		{ "sofa", 6 },
		{ "sofabed", 7 },
		{ "wasiki", 14 },
		{ "counter", 9 },
		{ "shower", 12 }
	};

	public static readonly Dictionary<string, int> HmeshNomalKindTag = new Dictionary<string, int>
	{
		{ "bath", 11 },
		{ "wall", 2 },
		{ "bed", 0 },
		{ "chair", 3 },
		{ "table", 4 },
		{ "stairs", 10 },
		{ "sofa", 6 },
		{ "sofabed", 7 },
		{ "wasiki", 14 },
		{ "counter", 9 },
		{ "shower", 12 }
	};

	public static readonly Dictionary<string, int> HmeshLesTag = new Dictionary<string, int>
	{
		{ "standfloor", -1 },
		{ "wall", 2 },
		{ "bed", 0 },
		{ "chair", 3 },
		{ "table", 4 },
		{ "stand", 1 },
		{ "sofa", 6 }
	};

	[Label("キャラの位置にパーティクルを出すか")]
	public bool isParticle;

	private Dictionary<int, bool> tmpHadItems = new Dictionary<int, bool>();

	public bool[] isHAddTaii = new bool[2];

	[HideInInspector]
	public readonly string strAssetCameraList = "list/h/camera/";

	[HideInInspector]
	public readonly string strAssetAnimationInfoListFolder = "list/h/animationinfo/";

	[HideInInspector]
	public readonly string strAssetStartAnimationListFolder = "list/h/startanimation/";

	[HideInInspector]
	public readonly string strAssetStartWaitAnimListFolder = "list/h/startwaitanim/";

	[HideInInspector]
	public readonly string strAssetEndAnimationInfoFolder = "list/h/endanimation/";

	[HideInInspector]
	public readonly string strAssetMoveOffsetListFolder = "list/h/move/";

	[HideInInspector]
	public readonly string strAssetNeckCtrlListFolder = "list/h/neckcontrol/";

	[HideInInspector]
	public readonly string strAssetYureListFolder = "list/h/yure/";

	[HideInInspector]
	public readonly string strAssetLayerCtrlListFolder = "list/h/layer/";

	[HideInInspector]
	public readonly string strAssetDankonListFolder = "list/h/lookatdan/";

	[HideInInspector]
	public readonly string strAssetDynamicBoneListFolder = "list/h/dynamicbone/";

	[HideInInspector]
	public readonly string strAssetHAutoListFolder = "list/h/hauto/hauto/01.unity3d";

	[HideInInspector]
	public readonly string strAssetLeaveItToYouFolder = "list/h/hauto/leaveittoyou/";

	[HideInInspector]
	public readonly string strAssetHParticleListFolder = "list/h/hparticle/";

	[HideInInspector]
	public readonly string strAssetSiruPasteListFolder = "list/h/sirupaste/";

	[HideInInspector]
	public readonly string strAssetMetaBallListFolder = "list/h/sirumetaball/";

	[HideInInspector]
	public readonly string strAssetHpointPrefabListFolder = "list/h/hpoint/prefab/";

	[HideInInspector]
	public readonly string strAssetHpointListFolder = "list/h/hpoint/pointinfo/";

	[HideInInspector]
	public readonly string strAssetFeelHitListFolder = "list/h/feelhit/";

	[HideInInspector]
	public readonly string strAssetHItemInfoListFolder = "list/h/hitem/";

	[HideInInspector]
	public readonly string strAssetHItemObjInfoListFolder = "list/h/hitemobj/";

	[HideInInspector]
	public readonly string strAssetHitObjListFolder = "list/h/hit/hitobject/";

	[HideInInspector]
	public readonly string strAssetCollisionListFolder = "list/h/hit/collision/";

	[HideInInspector]
	public readonly string strAssetSEListFolder = "list/h/sound/se/";

	[HideInInspector]
	public readonly string strAssetBGMListFolder = "list/h/sound/bgm/";

	[HideInInspector]
	public readonly string strAssetVoiceListFolder = "list/h/sound/voice/";

	[HideInInspector]
	public readonly string strAssetBreathListFolder = "list/h/sound/breath/";

	[HideInInspector]
	public readonly string strAssetSE = "sound/data/se/h";

	[HideInInspector]
	public readonly string strAssetParam = "list/h/param/";

	[HideInInspector]
	public readonly string strAssetIKListFolder = "list/h/ikinfo/";

	[HideInInspector]
	public readonly string HmeshListFolder = "list/map/chunk/";

	public int maleFinish;

	public int femalePlayerFinish;

	public int femaleFinish;

	public bool isCtrl;

	public byte endStatus;

	public bool onDeskChair;

	public Dictionary<AgentActor, Desire.ActionType> ReturnActionTypes = new Dictionary<AgentActor, Desire.ActionType>();

	private Vector3 start = Vector3.zero;

	public IDisposable choiceDisposable;

	private bool _isH;

	public bool IsHousingHEnter;

	public HPoint enterPoint;

	public bool[] FemaleLumpActive = new bool[2];

	public static bool SleepStart
	{
		get
		{
			if (Singleton<HSceneManager>.IsInstance())
			{
				return Singleton<HSceneManager>.Instance.bSleepStart;
			}
			return false;
		}
		set
		{
			if (Singleton<HSceneManager>.IsInstance())
			{
				Singleton<HSceneManager>.Instance.bSleepStart = value;
			}
		}
	}

	public static bool isHScene
	{
		[CompilerGenerated]
		get
		{
			return Singleton<HSceneManager>.IsInstance() && Singleton<HSceneManager>.Instance._isH;
		}
	}

	public void SetEventInfo(HEvent _event)
	{
		EventKind = _event;
	}

	public void HsceneEnter(Actor actor, int isMerchantLimitType = -1, AgentActor agent2 = null, HEvent _numEvent = HEvent.Normal)
	{
		_isH = true;
		enterPoint = null;
		IsHousingHEnter = false;
		MapUIContainer.SetActiveCommandList(active: false);
		choiceDisposable = MapUIContainer.CommandList.OnCompletedStopAsObservable().Take(1).Subscribe(delegate
		{
			MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.None);
		});
		Singleton<MapUIContainer>.Instance.MinimapUI.MiniMap.SetActive(value: false);
		Singleton<MapUIContainer>.Instance.MinimapUI.MiniMapIcon.SetActive(value: false);
		Singleton<Map>.Instance.SetActiveMapEffect(active: false);
		if (HSceneSet == null)
		{
			HSceneSet = Singleton<Resources>.Instance.HSceneTable.HSceneSet;
		}
		if (HSceneUISet == null)
		{
			HSceneUISet = Singleton<Resources>.Instance.HSceneTable.HSceneUISet;
		}
		Player = Singleton<Map>.Instance.Player;
		Player.enabled = false;
		handsIK = Player.HandsHolder.enabled;
		if (handsIK)
		{
			Player.HandsHolder.enabled = false;
		}
		Player.SetActiveOnEquipedItem(active: false);
		Player.ChaControl.setAllLayerWeight(0f);
		bMerchant = actor.GetComponent<MerchantActor>() != null;
		MerchantLimit = isMerchantLimitType;
		FemaleLumpActive[0] = false;
		FemaleLumpActive[1] = false;
		if (!bMerchant)
		{
			Agent[0] = actor.GetComponent<AgentActor>();
			Agent[0].BehaviorResources.ChangeMode(Desire.ActionType.Idle);
			Agent[0].DisableBehavior();
			Agent[0].AnimationAgent.DisableItems();
			Agent[0].Controller.enabled = false;
			Agent[0].AnimationAgent.enabled = false;
			PersonalPhase[0] = Agent[0].ChaControl.fileGameInfo.phase;
			Personality[0] = Agent[0].ChaControl.chaFile.parameter.personality;
			if (Agent[0].ChaControl.objExtraAccessory[3] != null)
			{
				FemaleLumpActive[0] = Agent[0].ChaControl.objExtraAccessory[3].activeSelf;
				Agent[0].ChaControl.ShowExtraAccessory(ChaControlDefine.ExtraAccessoryParts.Waist, show: false);
			}
			Agent[0].SetActiveOnEquipedItem(active: false);
			Agent[0].ChaControl.setAllLayerWeight(0f);
			HSkil = Agent[0].ChaControl.fileGameInfo.hSkill;
		}
		else
		{
			merchantActor = actor.GetComponent<MerchantActor>();
			merchantActor.DisableBehavior();
			merchantActor.Controller.enabled = false;
			merchantActor.AnimationMerchant.enabled = false;
			PersonalPhase[0] = 3;
		}
		if (agent2 != null)
		{
			Agent[1] = agent2;
			Agent[1].BehaviorResources.ChangeMode(Desire.ActionType.Idle);
			Agent[1].DisableBehavior();
			Agent[1].AnimationAgent.DisableItems();
			Agent[1].Controller.enabled = false;
			Agent[1].AnimationAgent.enabled = false;
			PersonalPhase[1] = Agent[1].ChaControl.fileGameInfo.phase;
			Personality[1] = Agent[1].ChaControl.chaFile.parameter.personality;
			if (Agent[1].ChaControl.objExtraAccessory[3] != null)
			{
				FemaleLumpActive[1] = Agent[1].ChaControl.objExtraAccessory[3].activeSelf;
				Agent[1].ChaControl.ShowExtraAccessory(ChaControlDefine.ExtraAccessoryParts.Waist, show: false);
			}
			Agent[1].SetActiveOnEquipedItem(active: false);
			Agent[1].ChaControl.setAllLayerWeight(0f);
		}
		isCtrl = false;
		endStatus = 0;
		AnimalBase.CreateDisplay = false;
		AnimalManager animalManager = Singleton<AnimalManager>.Instance;
		for (int num = 0; num < animalManager.Animals.Count; num++)
		{
			int index = num;
			animalManager.Animals[index].BodyEnabled = false;
			animalManager.Animals[index].enabled = false;
		}
		animalManager.ClearAnimalPointBehavior();
		if (!bMerchant)
		{
			Temperature = Agent[0].AgentData.StatsTable[0];
			Mood[0] = Agent[0].AgentData.StatsTable[1];
			Mood[1] = ((!Agent[1]) ? 0f : Agent[1].AgentData.StatsTable[1]);
			if (ReserveToilet.HasValue)
			{
				Toilet = ReserveToilet.Value;
				ReserveToilet = null;
			}
			else
			{
				int desireKey = Desire.GetDesireKey(Desire.Type.Toilet);
				Toilet = Agent[0].AgentData.DesireTable[desireKey];
			}
			int desireKey2 = Desire.GetDesireKey(Desire.Type.Bath);
			Bath = Agent[0].AgentData.DesireTable[desireKey2];
			Reliability = Agent[0].GetFlavorSkill(FlavorSkill.Type.Reliability);
			Instinct = Agent[0].GetFlavorSkill(FlavorSkill.Type.Instinct);
			Dirty = Agent[0].GetFlavorSkill(FlavorSkill.Type.Dirty);
			RiskManagement = Agent[0].GetFlavorSkill(FlavorSkill.Type.Wariness);
			Darkness = Agent[0].GetFlavorSkill(FlavorSkill.Type.Darkness);
			Sociability = Agent[0].GetFlavorSkill(FlavorSkill.Type.Sociability);
			Pheromone = Agent[0].GetFlavorSkill(FlavorSkill.Type.Pheromone);
			isHAddTaii = new bool[2]
			{
				Agent[0].ChaControl.fileGameInfo.isHAddTaii0,
				Agent[0].ChaControl.fileGameInfo.isHAddTaii1
			};
		}
		Player.Controller.ChangeState("Sex");
		ReturnActionTypes.Clear();
		EventKind = _numEvent;
		Singleton<Voice>.Instance.StopAll();
		if (!bMerchant)
		{
			StartCoroutine(HsceneInit(Agent));
		}
		else
		{
			StartCoroutine(HsceneInit(merchantActor, (!(agent2 != null)) ? null : Agent[1]));
		}
	}

	public void HousingHEnter(Actor actor, HPoint hpoint)
	{
		_isH = true;
		IsHousingHEnter = true;
		MapUIContainer.SetActiveCommandList(active: false);
		choiceDisposable = MapUIContainer.CommandList.OnCompletedStopAsObservable().Take(1).Subscribe(delegate
		{
			MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.None);
		});
		Singleton<MapUIContainer>.Instance.MinimapUI.MiniMap.SetActive(value: false);
		Singleton<MapUIContainer>.Instance.MinimapUI.MiniMapIcon.SetActive(value: false);
		Singleton<Map>.Instance.SetActiveMapEffect(active: false);
		if (HSceneSet == null)
		{
			HSceneSet = Singleton<Resources>.Instance.HSceneTable.HSceneSet;
		}
		if (HSceneUISet == null)
		{
			HSceneUISet = Singleton<Resources>.Instance.HSceneTable.HSceneUISet;
		}
		Player = Singleton<Map>.Instance.Player;
		Player.enabled = false;
		handsIK = Player.HandsHolder.enabled;
		if (handsIK)
		{
			Player.HandsHolder.enabled = false;
		}
		Player.SetActiveOnEquipedItem(active: false);
		Player.ChaControl.setAllLayerWeight(0f);
		Agent[0] = actor.GetComponent<AgentActor>();
		Agent[0].BehaviorResources.ChangeMode(Desire.ActionType.Idle);
		Agent[0].DisableBehavior();
		Agent[0].AnimationAgent.DisableItems();
		Agent[0].Controller.enabled = false;
		Agent[0].AnimationAgent.enabled = false;
		PersonalPhase[0] = Agent[0].ChaControl.fileGameInfo.phase;
		Personality[0] = Agent[0].ChaControl.chaFile.parameter.personality;
		Agent[0].SetActiveOnEquipedItem(active: false);
		Agent[0].ChaControl.setAllLayerWeight(0f);
		HSkil = Agent[0].ChaControl.fileGameInfo.hSkill;
		isCtrl = false;
		endStatus = 0;
		AnimalBase.CreateDisplay = false;
		AnimalManager animalManager = Singleton<AnimalManager>.Instance;
		for (int num = 0; num < animalManager.Animals.Count; num++)
		{
			int index = num;
			animalManager.Animals[index].BodyEnabled = false;
			animalManager.Animals[index].enabled = false;
		}
		animalManager.ClearAnimalPointBehavior();
		if (!bMerchant)
		{
			Temperature = Agent[0].AgentData.StatsTable[0];
			Mood[0] = Agent[0].AgentData.StatsTable[1];
			Mood[1] = ((!Agent[1]) ? 0f : Agent[1].AgentData.StatsTable[1]);
			if (Mood[0] < Agent[0].ChaControl.fileGameInfo.moodBound.lower)
			{
				isForce = true;
			}
			int desireKey = Desire.GetDesireKey(Desire.Type.Toilet);
			Toilet = Agent[0].AgentData.DesireTable[desireKey];
			int desireKey2 = Desire.GetDesireKey(Desire.Type.Bath);
			Bath = Agent[0].AgentData.DesireTable[desireKey2];
			Reliability = Agent[0].GetFlavorSkill(FlavorSkill.Type.Reliability);
			Instinct = Agent[0].GetFlavorSkill(FlavorSkill.Type.Instinct);
			Dirty = Agent[0].GetFlavorSkill(FlavorSkill.Type.Dirty);
			RiskManagement = Agent[0].GetFlavorSkill(FlavorSkill.Type.Wariness);
			Darkness = Agent[0].GetFlavorSkill(FlavorSkill.Type.Darkness);
			Sociability = Agent[0].GetFlavorSkill(FlavorSkill.Type.Sociability);
			Pheromone = Agent[0].GetFlavorSkill(FlavorSkill.Type.Pheromone);
			isHAddTaii = new bool[2]
			{
				Agent[0].ChaControl.fileGameInfo.isHAddTaii0,
				Agent[0].ChaControl.fileGameInfo.isHAddTaii1
			};
		}
		Player.Controller.ChangeState("Sex");
		ReturnActionTypes.Clear();
		EventKind = HEvent.Normal;
		if (EventKind == HEvent.Back)
		{
			Singleton<HSceneFlagCtrl>.Instance.AddParam(31, 1);
		}
		Singleton<Voice>.Instance.StopAll();
		height = hpoint._nPlace[0].Item1;
		enterPoint = hpoint;
		StartCoroutine(HsceneInit(Agent));
	}

	private IEnumerator HsceneInit(AgentActor[] agent)
	{
		if (CameraMesh == null)
		{
			string name = Singleton<Resources>.Instance.Map.CameraColliderList[Singleton<Map>.Instance.MapID].asset + "(Clone)";
			CameraMesh = GameObject.Find(name);
			yield return null;
		}
		if ((object)CameraMesh != null)
		{
			CameraMesh.SetActive(value: false);
		}
		HSceneSet.SetActive(value: true);
		HSceneUISet.SetActive(value: true);
		foreach (AgentActor value in Singleton<Map>.Instance.AgentTable.Values)
		{
			if (!(value == null) && !(value == agent[0]) && (!(agent[1] != null) || !(value == agent[1])))
			{
				value.DisableEntity();
				ReturnActionTypes.Add(value, value.BehaviorResources.Mode);
			}
		}
		MerchantActor merchant = Singleton<Map>.Instance.Merchant;
		if (merchant.CurrentMode != Merchant.ActionType.Absent)
		{
			merchant.ChaControl.visibleAll = false;
		}
		merchant.DisableBehavior();
		merchant.Controller.enabled = false;
		merchant.AnimationMerchant.enabled = false;
		females[0] = agent[0];
		bFutanari = Singleton<Map>.Instance.Player.ChaControl.fileParam.futanari;
		if (Singleton<Map>.Instance.Player.ChaControl.sex == 0 || (Singleton<Map>.Instance.Player.ChaControl.sex == 1 && bFutanari))
		{
			male = Player;
			FrameCorrect.AddFrameCorrect(male.ChaControl.animBody.gameObject);
			IKCorrect.AddIKCorrect(male.ChaControl.animBody.gameObject);
			if (agent[1] != null)
			{
				females[1] = agent[1];
				FrameCorrect.AddFrameCorrect(females[1].ChaControl.animBody.gameObject);
				IKCorrect.AddIKCorrect(females[1].ChaControl.animBody.gameObject);
			}
		}
		else
		{
			females[1] = Player;
			FrameCorrect.AddFrameCorrect(females[1].ChaControl.animBody.gameObject);
			IKCorrect.AddIKCorrect(females[1].ChaControl.animBody.gameObject);
		}
		yield return null;
		HScene hScene = Singleton<HSceneFlagCtrl>.Instance.GetComponent<HScene>();
		yield return hScene.InitCoroutine();
		Singleton<HPointCtrl>.Instance.CameraSet();
	}

	private IEnumerator HsceneInit(MerchantActor Merchant, AgentActor agent = null)
	{
		if (CameraMesh == null)
		{
			string name = Singleton<Resources>.Instance.Map.CameraColliderList[Singleton<Map>.Instance.MapID].asset + "(Clone)";
			CameraMesh = GameObject.Find(name);
			yield return null;
		}
		if ((object)CameraMesh != null)
		{
			CameraMesh.SetActive(value: false);
		}
		HSceneSet.SetActive(value: true);
		HSceneUISet.SetActive(value: true);
		foreach (AgentActor agentactor in Singleton<Map>.Instance.AgentTable.Values)
		{
			if (!(agentactor == null) && (!(agent != null) || !(agentactor == agent)))
			{
				agentactor.DisableEntity();
				ReturnActionTypes.Add(agentactor, agentactor.BehaviorResources.Mode);
				yield return null;
			}
		}
		females[0] = merchantActor;
		bFutanari = Singleton<Map>.Instance.Player.ChaControl.fileParam.futanari;
		if (Singleton<Map>.Instance.Player.ChaControl.sex == 0 || (Singleton<Map>.Instance.Player.ChaControl.sex == 1 && bFutanari))
		{
			male = Player;
			FrameCorrect.AddFrameCorrect(male.ChaControl.animBody.gameObject);
			yield return null;
			IKCorrect.AddIKCorrect(male.ChaControl.animBody.gameObject);
			if (agent != null)
			{
				females[1] = agent;
				FrameCorrect.AddFrameCorrect(females[1].ChaControl.animBody.gameObject);
				IKCorrect.AddIKCorrect(females[1].ChaControl.animBody.gameObject);
			}
		}
		else
		{
			females[1] = Player;
			FrameCorrect.AddFrameCorrect(females[1].ChaControl.animBody.gameObject);
			IKCorrect.AddIKCorrect(females[1].ChaControl.animBody.gameObject);
		}
		yield return null;
		GC.Collect();
		UnityEngine.Resources.UnloadUnusedAssets();
		yield return null;
		HScene hScene = Singleton<HSceneFlagCtrl>.Instance.GetComponent<HScene>();
		yield return hScene.InitCoroutine();
		Singleton<HPointCtrl>.Instance.CameraSet();
	}

	public bool HMeshCheck(int mode, List<HScene.DeskChairInfo> deskChairInfos)
	{
		hits = new RaycastHit[10];
		start = Vector3.zero;
		int num = 0;
		if (bMerchant)
		{
			start = ((mode != 1) ? merchantActor.Controller.transform.position : merchantActor.ChaControl.transform.position);
		}
		else
		{
			start = ((mode != 1) ? Agent[0].Controller.transform.position : Agent[0].ChaControl.transform.position);
		}
		start.y = ((!bMerchant) ? Agent[0].ChaControl.objHeadBone.transform.position.y : merchantActor.ChaControl.objHeadBone.transform.position.y);
		int layerMask = 131072;
		num = Physics.RaycastNonAlloc(start, Vector3.down, hits, SerchHmeshRange, layerMask);
		if (num == 0)
		{
			return false;
		}
		int mapID = Singleton<Map>.Instance.MapID;
		int areaID = Player.AreaID;
		HSceneFlagCtrl.HousingID[] housingAreaID = Singleton<HSceneFlagCtrl>.Instance.HousingAreaID;
		BasePoint[] basePoints = Singleton<Map>.Instance.PointAgent.BasePoints;
		int num2 = -1;
		int num3 = -1;
		for (int i = 0; i < housingAreaID.Length; i++)
		{
			if (mapID != housingAreaID[i].mapID)
			{
				continue;
			}
			for (int j = 0; j < housingAreaID[i].areaID.Length; j++)
			{
				if (housingAreaID[i].areaID[j] == areaID)
				{
					num2 = areaID;
					break;
				}
			}
		}
		for (int k = 0; k < basePoints.Length; k++)
		{
			if (basePoints[k].AreaIDInHousing == num2)
			{
				num3 = basePoints[k].ID;
				break;
			}
		}
		HPoint[] array = null;
		if (num3 != -1)
		{
			array = Singleton<Housing>.Instance.GetHPoint(num3);
		}
		bool flag = false;
		for (int l = 0; l < num; l++)
		{
			flag = HmeshNomalKindTag.ContainsKey(hits[l].collider.gameObject.tag);
			if (flag)
			{
				break;
			}
		}
		RaycastHit item = hits[0];
		if (num > 1)
		{
			float num4 = (item.point - start).sqrMagnitude;
			for (int m = 1; m < num; m++)
			{
				if (!bMerchant)
				{
					if (EventKind == HEvent.FromFemale)
					{
						if ((Singleton<Map>.Instance.Player.ChaControl.sex == 0 || (Singleton<Map>.Instance.Player.ChaControl.sex == 1 && bFutanari)) && hits[m].collider.gameObject.tag != "standfloor" && hits[m].collider.gameObject.tag != "bed")
						{
							continue;
						}
					}
					else if (EventKind == HEvent.Yobai)
					{
						if (hits[m].collider.gameObject.tag != "bed" && hits[m].collider.gameObject.tag != "sofabed")
						{
							continue;
						}
					}
					else if (EventKind == HEvent.Normal && ((flag && !HmeshNomalKindTag.ContainsKey(hits[m].collider.gameObject.tag)) || (EventKind != HEvent.GyakuYobai && SleepStart && hits[m].collider.gameObject.tag != "standfloor" && hits[m].collider.gameObject.tag != "bed")))
					{
						continue;
					}
					if (Singleton<Map>.Instance.Player.ChaControl.sex == 1 && !bFutanari && !HmeshLesTag.ContainsKey(hits[m].collider.gameObject.tag))
					{
						continue;
					}
				}
				else if ("sofa" == hits[m].collider.gameObject.tag || !HmeshTag.ContainsKey(hits[m].collider.gameObject.tag) || HmeshTag[hits[m].collider.gameObject.tag] > 4)
				{
					continue;
				}
				float sqrMagnitude = (hits[m].point - start).sqrMagnitude;
				if (!(num4 < sqrMagnitude) && !(hits[m].collider.gameObject.tag == "Untagged"))
				{
					item = hits[m];
					num4 = sqrMagnitude;
				}
			}
		}
		else if (item.collider.gameObject.tag == "Untagged")
		{
			return false;
		}
		if (array != null && array.Length > 0)
		{
			float num5 = -1f;
			for (int n = 0; n < array.Length; n++)
			{
				List<Collider> housingHcol = GetHousingHcol(array[n]);
				for (int num6 = 0; num6 < num; num6++)
				{
					if (!housingHcol.Contains(hits[num6].collider))
					{
						continue;
					}
					if (!bMerchant)
					{
						if (EventKind == HEvent.FromFemale)
						{
							if ((Singleton<Map>.Instance.Player.ChaControl.sex == 0 || (Singleton<Map>.Instance.Player.ChaControl.sex == 1 && bFutanari)) && hits[num6].collider.gameObject.tag != "standfloor" && hits[num6].collider.gameObject.tag != "bed")
							{
								continue;
							}
						}
						else if (EventKind == HEvent.Yobai)
						{
							if (hits[num6].collider.gameObject.tag != "standfloor" && hits[num6].collider.gameObject.tag != "bed" && hits[num6].collider.gameObject.tag != "sofabed")
							{
								continue;
							}
						}
						else if (EventKind == HEvent.Normal && ((flag && !HmeshNomalKindTag.ContainsKey(hits[num6].collider.gameObject.tag)) || (EventKind != HEvent.GyakuYobai && SleepStart && hits[num6].collider.gameObject.tag != "standfloor" && hits[num6].collider.gameObject.tag != "bed")))
						{
							continue;
						}
						if (Singleton<Map>.Instance.Player.ChaControl.sex == 1 && !bFutanari && !HmeshLesTag.ContainsKey(hits[num6].collider.gameObject.tag))
						{
							continue;
						}
					}
					else if ("sofa" == hits[num6].collider.gameObject.tag || !HmeshTag.ContainsKey(hits[num6].collider.gameObject.tag) || HmeshTag[hits[num6].collider.gameObject.tag] > 4)
					{
						continue;
					}
					float sqrMagnitude2 = (hits[num6].point - start).sqrMagnitude;
					if ((!(num5 >= 0f) || !(num5 < sqrMagnitude2)) && !(hits[num6].collider.gameObject.tag == "Untagged"))
					{
						item = hits[num6];
						num5 = sqrMagnitude2;
					}
				}
			}
		}
		if (!bMerchant)
		{
			if (EventKind == HEvent.FromFemale)
			{
				if (item.collider.gameObject.tag != "standfloor" && item.collider.gameObject.tag != "bed")
				{
					return false;
				}
			}
			else if (EventKind == HEvent.Yobai && item.collider.gameObject.tag != "standfloor" && item.collider.gameObject.tag != "bed" && item.collider.gameObject.tag != "sofabed")
			{
				return false;
			}
			if (Singleton<Map>.Instance.Player.ChaControl.sex == 1 && !bFutanari && !HmeshLesTag.ContainsKey(item.collider.gameObject.tag))
			{
				return false;
			}
		}
		else
		{
			if ("sofa" == item.collider.gameObject.tag)
			{
				return false;
			}
			if (!HmeshTag.ContainsKey(item.collider.gameObject.tag))
			{
				return false;
			}
			if (HmeshTag[item.collider.gameObject.tag] > 4)
			{
				return false;
			}
		}
		if (item.collider.gameObject.tag != "table" || bMerchant)
		{
			hitHmesh.Item1 = item;
			hitHmesh.Item2 = item.collider.gameObject;
		}
		else
		{
			bool flag2 = false;
			RaycastHit item2 = default(RaycastHit);
			GameObject gameObject = null;
			for (int num7 = 0; num7 < num; num7++)
			{
				if (!(hits[num7].collider.gameObject.tag != "chair"))
				{
					flag2 = true;
					item2 = hits[num7];
					gameObject = hits[num7].collider.gameObject;
					break;
				}
			}
			if (flag2)
			{
				onDeskChair = false;
				for (int num8 = 0; num8 < deskChairInfos.Count; num8++)
				{
					int index = num8;
					if (deskChairInfos[index].eventID == Agent[0].ActionID && deskChairInfos[index].poseID == Agent[0].PoseID)
					{
						onDeskChair = true;
						break;
					}
				}
				if (!onDeskChair)
				{
					hitHmesh.Item1 = item2;
					hitHmesh.Item2 = gameObject;
					hitHmesh.Item3 = ((!HmeshTag.ContainsKey(gameObject.tag)) ? (-2) : HmeshTag[gameObject.tag]);
					return true;
				}
				hitHmesh.Item1 = item;
				hitHmesh.Item2 = item.collider.gameObject;
			}
		}
		switch (EventKind)
		{
		case HEvent.Back:
		case HEvent.GyakuYobai:
		case HEvent.FromFemale:
			hitHmesh.Item3 = 0;
			break;
		case HEvent.Bath:
			hitHmesh.Item3 = 11;
			break;
		case HEvent.Toilet1:
			hitHmesh.Item3 = 13;
			break;
		case HEvent.Toilet2:
		case HEvent.ShagmiBare:
			hitHmesh.Item3 = 14;
			break;
		case HEvent.Kitchen:
			hitHmesh.Item3 = 9;
			break;
		case HEvent.Tachi:
		case HEvent.MapBath:
			hitHmesh.Item3 = 1;
			break;
		case HEvent.Stairs:
		case HEvent.StairsBare:
			hitHmesh.Item3 = 10;
			break;
		case HEvent.KabeanaBack:
		case HEvent.KabeanaFront:
			hitHmesh.Item3 = 15;
			break;
		case HEvent.Neonani:
			hitHmesh.Item3 = 0;
			break;
		case HEvent.TsukueBare:
			hitHmesh.Item3 = 4;
			break;
		case HEvent.Yobai:
			if (HmeshTag.ContainsKey(item.collider.gameObject.tag))
			{
				if (HmeshTag[item.collider.gameObject.tag] == -1)
				{
					hitHmesh.Item3 = 0;
				}
				else
				{
					hitHmesh.Item3 = HmeshTag[item.collider.gameObject.tag];
				}
			}
			else
			{
				hitHmesh.Item3 = 0;
			}
			break;
		default:
			if (females[1] == null || females[1].GetComponent<PlayerActor>() != null)
			{
				hitHmesh.Item3 = ((!HmeshTag.ContainsKey(item.collider.gameObject.tag)) ? (-2) : HmeshTag[item.collider.gameObject.tag]);
			}
			else
			{
				hitHmesh.Item3 = 0;
			}
			break;
		}
		if (SleepStart)
		{
			hitHmesh.Item3 = 0;
		}
		return true;
	}

	public void SetFlaverSkillParamator(int kind, int val)
	{
		switch (kind)
		{
		case 0:
			Reliability += val;
			Agent[0].SetFlavorSkill(FlavorSkill.Type.Reliability, Reliability);
			break;
		case 1:
			Instinct += val;
			Agent[0].SetFlavorSkill(FlavorSkill.Type.Instinct, Instinct);
			break;
		case 2:
			Dirty += val;
			Agent[0].SetFlavorSkill(FlavorSkill.Type.Dirty, Dirty);
			break;
		case 3:
			RiskManagement += val;
			Agent[0].SetFlavorSkill(FlavorSkill.Type.Wariness, RiskManagement);
			break;
		case 4:
			Darkness += val;
			Agent[0].SetFlavorSkill(FlavorSkill.Type.Darkness, Darkness);
			break;
		case 5:
			Sociability += val;
			Agent[0].SetFlavorSkill(FlavorSkill.Type.Sociability, Sociability);
			break;
		case 6:
			Pheromone += val;
			Agent[0].SetFlavorSkill(FlavorSkill.Type.Pheromone, Pheromone);
			break;
		}
	}

	public void SetParamator(int kind, int val)
	{
		if (kind < 10)
		{
			Agent[0].AddStatus(kind, val);
		}
		else if (kind < 100)
		{
			Agent[0].AddFlavorSkill(kind - 10, val);
		}
		else
		{
			Agent[0].SetDesire(kind - 100, val);
		}
	}

	public int GetFlaverSkillLevel(int kind)
	{
		return kind switch
		{
			0 => Reliability, 
			1 => Instinct, 
			2 => Dirty, 
			3 => RiskManagement, 
			4 => Darkness, 
			5 => Sociability, 
			_ => -1, 
		};
	}

	public bool CheckHadItem(int _mode, int _id)
	{
		List<Dictionary<int, List<HItemCtrl.ListItem>>>[] lstHItemObjInfo = Singleton<Resources>.Instance.HSceneTable.lstHItemObjInfo;
		List<StuffItem> itemList = Singleton<Map>.Instance.Player.PlayerData.ItemList;
		List<StuffItem> itemListInStorage = Singleton<Game>.Instance.Environment.ItemListInStorage;
		tmpHadItems.Clear();
		for (int i = 0; i < lstHItemObjInfo[_mode].Count; i++)
		{
			if (!lstHItemObjInfo[_mode][i].ContainsKey(_id))
			{
				continue;
			}
			for (int j = 0; j < lstHItemObjInfo[_mode][i][_id].Count; j++)
			{
				int num = j;
				if (lstHItemObjInfo[_mode][i][_id][num].itemkind != 1)
				{
					tmpHadItems.Add(num, value: true);
					continue;
				}
				int itemID = lstHItemObjInfo[_mode][i][_id][num].itemID;
				foreach (StuffItem item in itemList)
				{
					if (item.CategoryID != 14 || item.ID != itemID)
					{
						continue;
					}
					tmpHadItems.Add(num, value: true);
					break;
				}
				if (tmpHadItems.ContainsKey(num) && tmpHadItems[num])
				{
					continue;
				}
				foreach (StuffItem item2 in itemListInStorage)
				{
					if (item2.CategoryID == 14 && item2.ID == itemID)
					{
						if (tmpHadItems.ContainsKey(num))
						{
							tmpHadItems[num] = true;
						}
						else
						{
							tmpHadItems.Add(num, value: true);
						}
					}
				}
				if (!tmpHadItems.ContainsKey(num))
				{
					tmpHadItems.Add(num, value: false);
				}
			}
		}
		foreach (bool value in tmpHadItems.Values)
		{
			if (!value)
			{
				return false;
			}
		}
		return true;
	}

	public void EndHScene()
	{
		_isH = false;
	}

	private List<Collider> GetHousingHcol(HPoint hPoint)
	{
		ItemComponent componentInParent = hPoint.GetComponentInParent<ItemComponent>();
		List<Collider> list = new List<Collider>();
		if (componentInParent != null)
		{
			Collider[] componentsInChildren = componentInParent.GetComponentsInChildren<Collider>();
			Collider[] array = componentsInChildren;
			foreach (Collider collider in array)
			{
				if (collider.gameObject.layer == LayerMask.NameToLayer("HArea"))
				{
					list.Add(collider);
				}
			}
		}
		return list;
	}
}
