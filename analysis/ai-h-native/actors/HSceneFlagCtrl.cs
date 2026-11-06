using System;
using System.Collections.Generic;
using AIProject;
using Cinemachine;
using Manager;
using UnityEngine;

public class HSceneFlagCtrl : Singleton<HSceneFlagCtrl>
{
	public enum ClickKind
	{
		None = -1,
		FinishBefore,
		FinishInSide,
		FinishOutSide,
		FinishSame,
		FinishDrink,
		FinishVomit,
		RecoverFaintness,
		Spnking,
		PeepingRestart,
		LeaveItToYou,
		SceneEnd
	}

	[Flags]
	public enum JudgeSelect
	{
		Kiss = 1,
		Kokan = 2,
		Breast = 4,
		Anal = 8,
		Pain = 0x10,
		Constraint = 0x20
	}

	[Serializable]
	public class VoiceFlag
	{
		public bool[] playVoices = new bool[2];

		public int[] playShorts = new int[2] { -1, -1 };

		public int oldFinish = -1;

		public int playStart = -1;

		public bool dialog;

		public bool[] urines = new bool[2];

		public bool sleep;

		public Transform[] voiceTrs = new Transform[2];

		public List<string> lstUseAsset = new List<string>();

		public void MemberInit()
		{
			playVoices = new bool[2];
			playShorts = new int[2] { -1, -1 };
			oldFinish = -1;
			playStart = -1;
			dialog = false;
			urines = new bool[2];
			sleep = false;
			voiceTrs = new Transform[2];
			lstUseAsset = new List<string>();
		}
	}

	[Serializable]
	public struct HousingID
	{
		public int mapID;

		public int[] areaID;
	}

	public readonly int gotoFaintnessCount = 3;

	[Range(0f, 2f)]
	public float speed;

	[Tooltip("0:弱ループ 1:強ループ -1:その他")]
	public int loopType;

	[Range(0f, 1f)]
	public float[] motions;

	[Tooltip("現在の場所を示すID")]
	public int nPlace;

	[Tooltip("Hポイントのマーカーを識別するID")]
	public int HPointID;

	public HPoint nowHPoint;

	[Tooltip("0:なし\u30001:メタボール\u30002:パーティクル")]
	public int semenType = 1;

	public bool bFutanari;

	public bool BeforeHWait = true;

	public float HBeforeHouchiTime;

	public float StartHouchiTime;

	public bool pointMoveAnimChange;

	[RangeIntLabel("夜這いがバレる確率", 0, 100)]
	[Tooltip("0～100％で(小数不可)")]
	public int YobaiBareRate = 50;

	public bool nowOrgasm;

	public float changeMotionTimeMin;

	public float changeMotionTimeMax;

	public AnimationCurve changeMotionCurve;

	public float changeMotionMinRate;

	[Range(0f, 1f)]
	public float feel_f;

	[Range(0f, 1f)]
	public float feel_m;

	[Range(0f, 1f)]
	public float speedGuageRate = 0.01f;

	[Tooltip("ホイール一回でどれだけ回したことにするか")]
	public float wheelActionCount = 0.05f;

	[Tooltip("どの汁を制御するか")]
	public string ctrlSiru;

	public bool isInsert;

	public float changeAutoMotionTimeMin;

	public float changeAutoMotionTimeMax;

	[Range(0f, 1f)]
	public float guageDecreaseRate;

	[Range(0f, 1f)]
	public float feelSpnking;

	public float timeMasturbationChangeSpeed;

	public List<int>[,] lstSyncAnimLayers = new List<int>[2, 2];

	public Dictionary<int, List<int>> lstMapSyncAnimLayers = new Dictionary<int, List<int>>();

	public VirtualCameraController cameraCtrl;

	public CinemachineVirtualCamera HBeforeCamera;

	public int categoryMotionList = -1;

	private HScene.AnimationListInfo _selectAnimationListInfo;

	[SerializeField]
	public HScene.AnimationListInfo nowAnimationInfo = new HScene.AnimationListInfo();

	public bool stopFeelFemal;

	public bool stopFeelMale;

	public ClickKind click;

	public bool isFaintness;

	public int initiative;

	public bool isLeaveItToYou;

	public bool isAutoActionChange;

	public bool isGaugeHit;

	public bool isGaugeHit_M;

	public bool nowSpeedStateFast;

	[Range(0f, 1f)]
	public float rateTuya;

	[Range(0f, 1f)]
	public float rateNipMax = 0.3f;

	[Range(0f, 1f)]
	public float rateNip;

	public HousingID[] HousingAreaID;

	private List<int>[] endAdd = new List<int>[2]
	{
		new List<int>(),
		new List<int>()
	};

	private List<int> endAddSkil = new List<int>();

	public int numOrgasm;

	public int numOrgasmTotal;

	public int numOrgasmFemalePlayer;

	[Tooltip("通常絶頂回数もカウントされます 同時絶頂したら、こいつも+1、絶頂回数も+1、中出しか外出しも+1")]
	public int numSameOrgasm;

	public int numInside;

	public int numOutSide;

	public int numDrink;

	public int numVomit;

	public int numAibu;

	public int numHoushi;

	public int numSonyu;

	public int numLes;

	public int numMulti;

	public int numUrine;

	public int numFaintness;

	public int numKokan;

	public int numAnal;

	public int numLeadFemale;

	[EnumFlags]
	public List<JudgeSelect> isJudgeSelect = new List<JudgeSelect>();

	public bool isHoushiFinish;

	public bool isNotCtrl = true;

	public bool isPainActionParam;

	public bool isPainAction;

	public bool isConstraintAction;

	public bool isItemRelease;

	public bool isFemaleNaked;

	public bool isToilet;

	public VoiceFlag voice = new VoiceFlag();

	public Dictionary<int, VoiceFlag> MapHvoices = new Dictionary<int, VoiceFlag>();

	public Dictionary<int, HVoiceCtrl> ctrlMapHVoice = new Dictionary<int, HVoiceCtrl>();

	private GameObject MapHVoiceParent;

	public HScene.AnimationListInfo selectAnimationListInfo
	{
		get
		{
			return _selectAnimationListInfo;
		}
		set
		{
			_selectAnimationListInfo = value;
		}
	}

	public Dictionary<int, int> ChangeParams { get; private set; } = new Dictionary<int, int>();


	public void AddOrgasm()
	{
		numOrgasmTotal = Mathf.Clamp(numOrgasmTotal + 1, 0, 999999);
		if (numOrgasmTotal == gotoFaintnessCount)
		{
			if (Singleton<HSceneManager>.Instance.isForce)
			{
				AddParam(4, 0);
			}
			else
			{
				AddParam(6, 0);
			}
		}
	}

	public void AddParam(int ptn, int mode)
	{
		ParameterPacket value = new ParameterPacket();
		switch (mode)
		{
		case 0:
			if (!Singleton<Manager.Resources>.Instance.HSceneTable.HBaseParamTable.TryGetValue(ptn, out value))
			{
				return;
			}
			break;
		case 1:
			if (!Singleton<Manager.Resources>.Instance.HSceneTable.HactionParamTable.TryGetValue(ptn, out value))
			{
				return;
			}
			break;
		}
		if (endAdd[mode].Contains(ptn))
		{
			return;
		}
		endAdd[mode].Add(ptn);
		foreach (KeyValuePair<int, TriThreshold> parameter in value.Parameters)
		{
			int num = Mathf.RoundToInt(UnityEngine.Random.Range(parameter.Value.SThreshold, parameter.Value.LThreshold));
			if (!ChangeParams.ContainsKey(parameter.Key))
			{
				ChangeParams.Add(parameter.Key, num);
			}
			else
			{
				ChangeParams[parameter.Key] += num;
			}
		}
	}

	public void AddSkileParam(int id)
	{
		Dictionary<int, float> value = new Dictionary<int, float>();
		if (!Singleton<Manager.Resources>.Instance.HSceneTable.HSkileParamTable.TryGetValue(id, out value) || endAddSkil.Contains(id))
		{
			return;
		}
		endAddSkil.Add(id);
		foreach (KeyValuePair<int, float> item in value)
		{
			if (item.Key != Manager.Resources.HSceneTables.HTagTable["ゲージ"])
			{
				int num = Mathf.RoundToInt(item.Value);
				if (!ChangeParams.ContainsKey(item.Key))
				{
					ChangeParams.Add(item.Key, num);
				}
				else
				{
					ChangeParams[item.Key] += num;
				}
			}
		}
	}

	public float SkilChangeSpeed(int id)
	{
		Dictionary<int, float> value = new Dictionary<int, float>();
		if (!Singleton<Manager.Resources>.Instance.HSceneTable.HSkileParamTable.TryGetValue(id, out value))
		{
			return 1f;
		}
		foreach (KeyValuePair<int, float> item in value)
		{
			if (item.Key != Manager.Resources.HSceneTables.HTagTable["ゲージ"])
			{
				continue;
			}
			return item.Value;
		}
		return 1f;
	}

	private void Start()
	{
		Init();
	}

	public void Init()
	{
		isNotCtrl = true;
		isFaintness = false;
		lstSyncAnimLayers[0, 0] = new List<int>();
		lstSyncAnimLayers[0, 1] = new List<int>();
		lstSyncAnimLayers[1, 0] = new List<int>();
		lstSyncAnimLayers[1, 1] = new List<int>();
		voice.playShorts = new int[2] { -1, -1 };
	}

	private void OnDisable()
	{
		EndProc();
	}

	public void EndProc()
	{
		bFutanari = false;
		BeforeHWait = true;
		nowAnimationInfo = new HScene.AnimationListInfo();
		selectAnimationListInfo = null;
		feel_f = 0f;
		feel_m = 0f;
		initiative = 0;
		isLeaveItToYou = false;
		isAutoActionChange = false;
		isGaugeHit = false;
		isGaugeHit_M = false;
		nowSpeedStateFast = false;
		speed = 0f;
		for (int i = 0; i < motions.Length; i++)
		{
			motions[i] = 0f;
		}
		nPlace = 0;
		HPointID = 0;
		if (nowHPoint != null)
		{
			ChangeHItem componentInChildren = nowHPoint.GetComponentInChildren<ChangeHItem>();
			if (componentInChildren != null)
			{
				componentInChildren.ChangeActive(val: true);
			}
		}
		nowHPoint = null;
		stopFeelFemal = false;
		stopFeelMale = false;
		isFaintness = false;
		rateTuya = 0f;
		rateNip = 0f;
		numOrgasm = 0;
		numOrgasmTotal = 0;
		numOrgasmFemalePlayer = 0;
		numSameOrgasm = 0;
		numInside = 0;
		numOutSide = 0;
		numDrink = 0;
		numVomit = 0;
		numAibu = 0;
		numHoushi = 0;
		numSonyu = 0;
		numLes = 0;
		numUrine = 0;
		numFaintness = 0;
		numKokan = 0;
		numAnal = 0;
		numLeadFemale = 0;
		isJudgeSelect = new List<JudgeSelect>();
		isHoushiFinish = false;
		isNotCtrl = true;
		isPainActionParam = false;
		isPainAction = false;
		isConstraintAction = false;
		nowOrgasm = false;
		voice.MemberInit();
		ChangeParams.Clear();
		endAdd[0].Clear();
		endAdd[1].Clear();
		endAddSkil.Clear();
		for (int j = 0; j < 2; j++)
		{
			for (int k = 0; k < 2; k++)
			{
				if (lstSyncAnimLayers[j, k] != null)
				{
					lstSyncAnimLayers[j, k].Clear();
				}
			}
		}
	}

	public void MapHVoiceInit()
	{
		int agentMax = Singleton<Manager.Resources>.Instance.DefinePack.MapDefines.AgentMax;
		if (MapHvoices == null)
		{
			MapHvoices = new Dictionary<int, VoiceFlag>();
		}
		if (MapHVoiceParent == null)
		{
			MapHVoiceParent = new GameObject("MapHVoiceCtrls");
			MapHVoiceParent.transform.SetParent(base.transform);
		}
		for (int i = 0; i < agentMax; i++)
		{
			if (!ctrlMapHVoice.ContainsKey(i) || !(ctrlMapHVoice[i] != null))
			{
				ctrlMapHVoice.Add(i, new GameObject($"MapHVoiceCtrl_{i}").AddComponent<HVoiceCtrl>());
				ctrlMapHVoice[i].transform.SetParent(MapHVoiceParent.transform);
				ctrlMapHVoice[i].MapHID = i;
				ctrlMapHVoice[i].ctrlFlag = this;
			}
		}
	}

	public int AddMapHvoices()
	{
		int num = 0;
		if (MapHvoices.ContainsKey(num))
		{
			int i;
			for (i = 0; MapHvoices.ContainsKey(i); i++)
			{
			}
			num = i;
		}
		MapHvoices.Add(num, new VoiceFlag());
		MapHvoices[num].MemberInit();
		if (MapHVoiceParent == null)
		{
			MapHVoiceParent = new GameObject("MapHVoiceCtrls");
			MapHVoiceParent.transform.SetParent(base.transform);
		}
		if (!ctrlMapHVoice.ContainsKey(num))
		{
			ctrlMapHVoice.Add(num, new GameObject($"MapHVoiceCtrl_{num}").AddComponent<HVoiceCtrl>());
			ctrlMapHVoice[num].transform.SetParent(MapHVoiceParent.transform);
			ctrlMapHVoice[num].MapHID = num;
			ctrlMapHVoice[num].ctrlFlag = this;
		}
		return num;
	}

	public void RemoveMapHvoices(int mapHID)
	{
		if (MapHvoices.ContainsKey(mapHID))
		{
			for (int i = 0; i < MapHvoices[mapHID].lstUseAsset.Count; i++)
			{
				AssetBundleManager.UnloadAssetBundle(MapHvoices[mapHID].lstUseAsset[i], isUnloadForceRefCount: true);
			}
			MapHvoices.Remove(mapHID);
		}
	}

	public void AddMapSyncAnimLayer(int addID)
	{
		lstMapSyncAnimLayers.Add(addID, new List<int>());
	}

	public void RemoveMapSyncAnimLayer(int mapHID)
	{
		if (lstMapSyncAnimLayers.ContainsKey(mapHID))
		{
			lstMapSyncAnimLayers[mapHID].Clear();
			lstMapSyncAnimLayers.Remove(mapHID);
		}
	}
}
