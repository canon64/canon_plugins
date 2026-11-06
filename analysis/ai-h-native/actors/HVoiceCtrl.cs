using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AIChara;
using AIProject;
using AIProject.Definitions;
using Manager;
using UniRx;
using UnityEngine;

public class HVoiceCtrl : MonoBehaviour
{
	[Serializable]
	public class FaceInfo
	{
		[RangeLabel("目の開き", 0f, 1f)]
		public float openEye = 10f;

		[RangeLabel("口の開き最小", 0f, 1f)]
		public float openMouthMin;

		[RangeLabel("口の開き最大", 0f, 1f)]
		public float openMouthMax = 1f;

		[Label("眉の形")]
		public int eyeBlow;

		[Label("目の形")]
		public int eye;

		[Label("口の形")]
		public int mouth;

		[RangeLabel("涙", 0f, 1f)]
		public float tear;

		[RangeLabel("頬赤", 0f, 1f)]
		public float cheek;

		[Label("ハイライト")]
		public bool highlight;

		[Label("瞬き")]
		public bool blink;

		[Label("首挙動")]
		public int behaviorNeckLine;

		[Label("目挙動")]
		public int behaviorEyeLine;

		[Label("首タゲ")]
		public int targetNeckLine;

		[Label("目タゲ")]
		public int targetEyeLine;

		[Label("視線角度")]
		public Vector3[] EyeRot = new Vector3[2];

		[Label("首角度")]
		public Vector3[] NeckRot = new Vector3[2];

		[Label("頭角度")]
		public Vector3[] HeadRot = new Vector3[2];
	}

	[Serializable]
	public class VoiceListInfo
	{
		[Label("ファイル名")]
		public string nameFile = string.Empty;

		[Label("アセットバンドルパス")]
		public string pathAsset = string.Empty;

		[Label("上書き禁止フラグ")]
		public bool notOverWrite;

		[Label("喋った(セリフ時のみ)")]
		public bool isPlay;

		[Label("呼吸グループ(呼吸時のみ)")]
		public int group = -1;

		[Label("おしっこセリフ(呼吸時のみ)")]
		public bool urine;

		[Label("アタリにあたっていない")]
		public List<FaceInfo> lstNotHitFace = new List<FaceInfo>();

		[Label("アタリにあたっている")]
		public List<FaceInfo> lstHitFace = new List<FaceInfo>();
	}

	[Serializable]
	public class BreathList
	{
		[Serializable]
		private struct InspectorBreathList
		{
			public int key;

			public VoiceListInfo value;
		}

		public Dictionary<int, VoiceListInfo> lstVoiceList = new Dictionary<int, VoiceListInfo>();

		[SerializeField]
		private List<InspectorBreathList> debugList = new List<InspectorBreathList>();

		public void DebugListSet()
		{
			foreach (KeyValuePair<int, VoiceListInfo> lstVoice in lstVoiceList)
			{
				debugList.Add(new InspectorBreathList
				{
					key = lstVoice.Key,
					value = lstVoice.Value
				});
			}
		}
	}

	[Serializable]
	public class VoiceList
	{
		public Dictionary<int, VoiceListInfo> dicdicVoiceList = new Dictionary<int, VoiceListInfo>();

		public int total;
	}

	[Serializable]
	public struct PlayVoiceinfo
	{
		public int mode;

		public int kind;

		public int voiceID;
	}

	[Serializable]
	public class ShortVoiceList
	{
		public Dictionary<int, VoiceListInfo> dicShortBreathLists = new Dictionary<int, VoiceListInfo>();
	}

	[Serializable]
	public class BreathVoicePtnInfo
	{
		public List<int> lstConditions = new List<int>();

		public List<int> lstVoice = new List<int>();

		public List<int> lstAnimeID = new List<int>();
	}

	[Serializable]
	public class BreathPtn
	{
		[Label("段階")]
		public int level;

		[Label("アニメーション名")]
		public string anim = string.Empty;

		[Label("ループ中1回")]
		public bool onlyOne;

		[Label("ループ中1回がtrueのとき使用")]
		public bool isPlay;

		[Label("強制フラグ")]
		public bool force;

		[Label("表情変更時間最小")]
		public float timeChangeFaceMin = 5f;

		[Label("表情変更時間最題")]
		public float timeChangeFaceMax = 5f;

		public List<BreathVoicePtnInfo> lstInfo = new List<BreathVoicePtnInfo>();
	}

	[Serializable]
	public class VoicePtnInfo
	{
		public List<int> lstAnimList = new List<int>();

		public List<int> lstPlayConditions = new List<int>();

		public int loadListmode = -1;

		public int loadListKind = -1;

		public List<int> lstVoice = new List<int>();
	}

	[Serializable]
	public class VoicePtn
	{
		[Label("アニメーション名")]
		public string anim = string.Empty;

		[Label("キャラの状態")]
		public int condition = -1;

		[Label("開始パターン")]
		public int startKind = -1;

		[Label("掛け合いか")]
		public int howTalk;

		[Label("外部フラグ見る")]
		public bool LookFlag = true;

		public List<VoicePtnInfo> lstInfo = new List<VoicePtnInfo>();
	}

	public struct CheckVoicePtn
	{
		[Label("アニメーション名")]
		public string anim;

		[Label("キャラの状態")]
		public int condition;

		[Label("開始パターン")]
		public int startKind;

		[Label("掛け合いか")]
		public int howTalk;

		[Label("外部フラグ見る")]
		public bool LookFlag;
	}

	[Serializable]
	public class StartVoicePtn
	{
		[Label("キャラの状態")]
		public int condition = -1;

		[Label("襲う種類")]
		public int nForce;

		[Label("どの体位の開始か")]
		public int nTaii = -1;

		[Label("タイミング名")]
		public string anim = string.Empty;

		[Label("タイミング")]
		public int timing;

		public List<VoicePtnInfo> lstInfo = new List<VoicePtnInfo>();
	}

	[Serializable]
	public class ShortBreathPtn
	{
		public ValueDictionary<int, int, int, int, List<BreathVoicePtnInfo>> dicInfo = new ValueDictionary<int, int, int, int, List<BreathVoicePtnInfo>>();
	}

	[Serializable]
	public class VoiceAnimationPlayInfo
	{
		[Label("アニメーション名(ハッシュ値)")]
		public int animationHash;

		[Label("再生した")]
		public bool[] isPlays = new bool[2];
	}

	[Serializable]
	public class VoiceAnimationPlay
	{
		public List<VoiceAnimationPlayInfo> lstPlayInfo = new List<VoiceAnimationPlayInfo>();

		[Label("モーションの再生回数")]
		public int Count;

		public void SetAllFlags(bool _play)
		{
			for (int i = 0; i < lstPlayInfo.Count; i++)
			{
				lstPlayInfo[i].isPlays[0] = _play;
				lstPlayInfo[i].isPlays[1] = _play;
			}
		}

		public void AfterFinish()
		{
			for (int i = 0; i < lstPlayInfo.Count; i++)
			{
				if (lstPlayInfo[i].isPlays[0] || lstPlayInfo[i].isPlays[1])
				{
					lstPlayInfo[i].isPlays[0] = false;
					lstPlayInfo[i].isPlays[1] = false;
				}
			}
			Count++;
		}

		public VoiceAnimationPlayInfo GetAnimation(int _animHash)
		{
			for (int i = 0; i < lstPlayInfo.Count; i++)
			{
				if (_animHash == lstPlayInfo[i].animationHash)
				{
					return lstPlayInfo[i];
				}
			}
			return null;
		}
	}

	public enum VoiceKind
	{
		breath,
		breathShort,
		voice,
		startVoice,
		none
	}

	[Serializable]
	public class Voice
	{
		public VoiceKind state = VoiceKind.none;

		[Header("呼吸")]
		public VoiceListInfo breathInfo;

		[Label("呼吸リストの配列番号")]
		public int arrayBreath;

		[Label("呼吸アニメーションステート")]
		public string animBreath;

		[Label("呼吸グループ")]
		public int breathGroup = -1;

		[Label("速い？")]
		public bool speedStateFast;

		[Label("表情変化経過時間")]
		public float timeFaceDelta;

		[Label("表情変化時間")]
		public float timeFace;

		[Label("当たってる？")]
		public bool isGaugeHit;

		[Header("セリフ")]
		public VoiceListInfo voiceInfo;

		[Label("セリフリストの配列番号")]
		public int arrayVoice;

		[Label("セリフリストの番号")]
		public int VoiceListID;

		[Label("セリフリストのシート番号")]
		public int VoiceListSheetID;

		[Label("セリフアニメーションステート")]
		public string animVoice;

		[Header("短い喘ぎ")]
		public VoiceListInfo shortInfo;

		[Label("短い喘ぎの配列番号")]
		public int arrayShort;

		[Header("共通")]
		[Label("上書き禁止")]
		public bool notOverWrite;

		public FaceInfo Face = new FaceInfo();
	}

	public BreathList[] breathLists = new BreathList[2];

	private Dictionary<int, Dictionary<int, VoiceList>>[] dicdiclstVoiceList = new Dictionary<int, Dictionary<int, VoiceList>>[2];

	private ShortVoiceList[] ShortBreathLists = new ShortVoiceList[2];

	private ValueDictionary<int, int, int, int, int, string, BreathPtn> dicBreathPtns = new ValueDictionary<int, int, int, int, int, string, BreathPtn>();

	private ValueDictionary<int, int, int, int, int, string, BreathPtn> dicBreathAddPtns = new ValueDictionary<int, int, int, int, int, string, BreathPtn>();

	private Dictionary<int, Dictionary<int, List<VoicePtn>>>[] lstLoadVoicePtn = new Dictionary<int, Dictionary<int, List<VoicePtn>>>[2];

	private List<StartVoicePtn>[] lstLoadStartVoicePtn = new List<StartVoicePtn>[2];

	private ShortBreathPtn[] shortBreathPtns = new ShortBreathPtn[2];

	private ShortBreathPtn[] shortBreathAddPtns = new ShortBreathPtn[2];

	private Dictionary<int, Dictionary<int, VoiceAnimationPlay>> dicdicVoicePlayAnimation = new Dictionary<int, Dictionary<int, VoiceAnimationPlay>>();

	public HSceneFlagCtrl ctrlFlag;

	[SerializeField]
	private BreathList[] breathUseLists = new BreathList[2];

	[SerializeField]
	private ValueDictionary<int, string, BreathPtn> dicBreathUsePtns = new ValueDictionary<int, string, BreathPtn>();

	[SerializeField]
	private List<VoicePtn>[] lstVoicePtn = new List<VoicePtn>[2];

	[SerializeField]
	private ValueDictionary<int, int, List<BreathVoicePtnInfo>> dicShortBreathUsePtns = new ValueDictionary<int, int, List<BreathVoicePtnInfo>>();

	public VoiceAnimationPlay playAnimation = new VoiceAnimationPlay();

	[SerializeField]
	private int[] personality = new int[2];

	[SerializeField]
	private float[] voicePitch = new float[2];

	[SerializeField]
	private Actor param;

	[SerializeField]
	private Actor param_sub;

	[SerializeField]
	private List<int> lstSystem;

	public Voice[] nowVoices = new Voice[2];

	public int nowMode;

	public int nowKind;

	public int nowId;

	[SerializeField]
	private int merchantType = -1;

	private GlobalMethod.FloatBlend[] blendEyes = new GlobalMethod.FloatBlend[2];

	private GlobalMethod.FloatBlend[] blendMouths = new GlobalMethod.FloatBlend[2];

	private GlobalMethod.FloatBlend[] blendMouthMaxs = new GlobalMethod.FloatBlend[2];

	private HSceneManager hSceneManager;

	public float HBeforeHouchiTime;

	public float HouchiTime;

	private bool[] isPlays = new bool[2];

	private StringBuilder sbLoadFile = new StringBuilder();

	[SerializeField]
	private int IngoLimit = 170;

	private bool masturbation;

	private bool les;

	public int MapHID = -1;

	private readonly Dictionary<int, List<int>> EtcKind = new Dictionary<int, List<int>>
	{
		{
			0,
			new List<int> { 1, 2, 3, 4, 5, 6, 8, 9, 13, 14 }
		},
		{
			1,
			new List<int> { 0, 7 }
		},
		{
			2,
			new List<int> { 10, 11, 12 }
		}
	};

	private List<string> lst = new List<string>();

	private List<string> lstBreathAbnames = new List<string>();

	public bool SetVoiceList(int _mode, int _kind, int _id, List<int> _lstSystem)
	{
		lstSystem = _lstSystem;
		nowKind = _kind;
		nowId = _id;
		switch (_mode)
		{
		case 3:
			nowMode = 4;
			break;
		case 4:
			nowMode = 5;
			break;
		case 5:
			nowMode = 3;
			break;
		default:
			nowMode = _mode;
			break;
		}
		int key = 0;
		if (nowMode == 3)
		{
			foreach (KeyValuePair<int, List<int>> item in EtcKind)
			{
				if (!item.Value.Contains(nowId))
				{
					continue;
				}
				key = item.Key;
				break;
			}
		}
		if (lstLoadVoicePtn[0].ContainsKey(nowMode))
		{
			lstLoadVoicePtn[0][nowMode].TryGetValue(key, out lstVoicePtn[0]);
		}
		if (lstLoadVoicePtn[1] != null && lstLoadVoicePtn[1].ContainsKey(nowMode))
		{
			lstLoadVoicePtn[1][nowMode].TryGetValue(key, out lstVoicePtn[1]);
		}
		playAnimation = new VoiceAnimationPlay();
		if (dicdicVoicePlayAnimation.ContainsKey(nowMode))
		{
			dicdicVoicePlayAnimation[nowMode].TryGetValue(_id, out playAnimation);
		}
		else
		{
			dicdicVoicePlayAnimation.Add(nowMode, new Dictionary<int, VoiceAnimationPlay>());
		}
		if (playAnimation == null)
		{
			playAnimation = new VoiceAnimationPlay();
			dicdicVoicePlayAnimation[nowMode].Add(nowId, playAnimation);
		}
		for (int i = 0; i < 2; i++)
		{
			nowVoices[i].animBreath = string.Empty;
			nowVoices[i].animVoice = string.Empty;
			nowVoices[i].breathGroup = -1;
		}
		return true;
	}

	public bool SetBreathVoiceList(ChaControl[] _charas, int _mode, int _kind, bool _isFemaleMain, bool _isIyagari, bool _isMainFirstPerson = true)
	{
		breathUseLists[0] = breathLists[0];
		breathUseLists[1] = breathLists[1];
		int[] array = new int[2] { -1, -1 };
		if (_isIyagari && hSceneManager.EventKind != HSceneManager.HEvent.Yobai)
		{
			array[0] = (array[1] = 2);
		}
		else if (_isFemaleMain && _mode != 4)
		{
			array[0] = (array[1] = 3);
		}
		if (_mode < 3)
		{
			_kind = 0;
		}
		for (int i = 0; i < 2; i++)
		{
			dicBreathUsePtns[i] = null;
			if (!dicBreathPtns[i].ContainsKey(_mode) || !dicBreathPtns[i][_mode].ContainsKey(_kind))
			{
				continue;
			}
			int key = ((!_isMainFirstPerson) ? (i ^ 1) : i);
			if (dicBreathPtns[i][_mode][_kind].ContainsKey(key))
			{
				if (array[i] == -1)
				{
					array[i] = (((bool)_charas[i] && hSceneManager.PersonalPhase[i] > 1) ? 1 : 0);
				}
				if (dicBreathPtns[i][_mode][_kind][key].ContainsKey(array[i]))
				{
					dicBreathUsePtns[i] = dicBreathPtns[i][_mode][_kind][key][array[i]];
				}
			}
		}
		for (int j = 0; j < 2; j++)
		{
			dicShortBreathUsePtns[j] = null;
			if (shortBreathPtns[j].dicInfo.ContainsKey(_mode) && shortBreathPtns[j].dicInfo[_mode].ContainsKey(_kind))
			{
				int key2 = ((!_isMainFirstPerson) ? (j ^ 1) : j);
				ValueDictionary<int, int, List<BreathVoicePtnInfo>> valueDictionary = shortBreathPtns[j].dicInfo[_mode][_kind];
				if (shortBreathPtns[j].dicInfo[_mode][_kind].ContainsKey(key2))
				{
					dicShortBreathUsePtns[j] = shortBreathPtns[j].dicInfo[_mode][_kind][key2];
				}
			}
		}
		for (int k = 0; k < 2; k++)
		{
			if (dicBreathUsePtns[k] == null)
			{
				dicBreathUsePtns[k] = dicBreathUsePtns.New();
			}
			if (dicShortBreathUsePtns[k] == null)
			{
				dicShortBreathUsePtns[k] = dicShortBreathUsePtns.New();
			}
		}
		return true;
	}

	public bool AfterFinish()
	{
		if (playAnimation == null)
		{
			return false;
		}
		playAnimation.AfterFinish();
		return true;
	}

	public bool Proc(AnimatorStateInfo _ai, params ChaControl[] _females)
	{
		isPlays[0] = false;
		isPlays[1] = false;
		for (int num = 1; num > -1; num--)
		{
			if ((_females.Length != 1 || num != 1) && !(_females[num] == null))
			{
				isPlays[num] = StartVoiceProc(_females, num);
			}
		}
		for (int num2 = 1; num2 > -1; num2--)
		{
			if ((_females.Length != 1 || num2 != 1) && !(_females[num2] == null) && !isPlays[num2])
			{
				isPlays[num2] = VoiceProc(_ai, _females[num2], num2);
			}
		}
		for (int i = 0; i < 2; i++)
		{
			if ((_females.Length != 1 || i != 1) && !(_females[i] == null) && !isPlays[i])
			{
				isPlays[i] = ShortBreathProc(_females[i], i);
			}
		}
		for (int j = 0; j < 2; j++)
		{
			if ((_females.Length != 1 || j != 1) && !(_females[j] == null) && !isPlays[j])
			{
				BreathProc(_ai, _females[j], j);
			}
		}
		for (int k = 0; k < 2; k++)
		{
			if (_females.Length != 1 && !(_females[k] == null))
			{
				OpenCtrl(_females[k], k);
			}
		}
		nowVoices[0].speedStateFast = ctrlFlag.nowSpeedStateFast;
		if (_females.Length > 1 && _females[1] != null && _females[1].visibleAll && _females[1].objTop != null)
		{
			nowVoices[1].speedStateFast = ctrlFlag.nowSpeedStateFast;
		}
		for (int l = 0; l < 2; l++)
		{
			if ((l != 1 || (_females.Length > 1 && !(_females[1] == null) && _females[1].visibleAll && !(_females[1].objTop == null))) && !isVoiceCheck(l) && (nowVoices[l].state == VoiceKind.startVoice || nowVoices[l].state == VoiceKind.voice))
			{
				nowVoices[l].state = VoiceKind.none;
			}
		}
		if ((isVoiceCheck(0) && (nowVoices[0].state == VoiceKind.voice || nowVoices[0].state == VoiceKind.startVoice)) || (isVoiceCheck(1) && (nowVoices[1].state == VoiceKind.voice || nowVoices[1].state == VoiceKind.startVoice)))
		{
			HouchiTime = 0f;
		}
		if (ctrlFlag.StartHouchiTime < HouchiTime)
		{
			if (MapHID < 0)
			{
				ctrlFlag.voice.playVoices[0] = true;
				ctrlFlag.voice.playVoices[1] = true;
			}
			else if (ctrlFlag.MapHvoices.ContainsKey(MapHID))
			{
				ctrlFlag.MapHvoices[MapHID].playVoices[0] = true;
				ctrlFlag.MapHvoices[MapHID].playVoices[1] = true;
			}
		}
		for (int num3 = 1; num3 > -1; num3--)
		{
			if (((_females.Length == 1 && num3 == 1) || _females[num3] == null || !_females[num3].visibleAll) && (nowVoices[num3].state == VoiceKind.voice || nowVoices[num3].state == VoiceKind.startVoice))
			{
				nowVoices[num3].state = VoiceKind.none;
			}
		}
		return true;
	}

	public bool OpenCtrl(ChaControl _female, int _main)
	{
		if (!_female.visibleAll || _female.objBody == null)
		{
			return false;
		}
		float _ans = 0f;
		if (blendEyes[0] == null)
		{
			blendEyes[0] = new GlobalMethod.FloatBlend();
			blendEyes[1] = new GlobalMethod.FloatBlend();
		}
		if (blendMouths[0] == null)
		{
			blendMouths[0] = new GlobalMethod.FloatBlend();
			blendMouths[1] = new GlobalMethod.FloatBlend();
		}
		if (blendMouthMaxs[0] == null)
		{
			blendMouthMaxs[0] = new GlobalMethod.FloatBlend();
			blendMouthMaxs[1] = new GlobalMethod.FloatBlend();
		}
		if (blendEyes[_main].Proc(ref _ans))
		{
			_female.ChangeEyesOpenMax(_ans);
		}
		FBSCtrlMouth mouthCtrl = _female.mouthCtrl;
		if (mouthCtrl != null)
		{
			float _ans2 = 0f;
			if (blendMouths[_main].Proc(ref _ans2))
			{
				mouthCtrl.OpenMin = _ans2;
			}
			_ans2 = 1f;
			if (blendMouthMaxs[_main].Proc(ref _ans2))
			{
				mouthCtrl.OpenMax = _ans2;
			}
		}
		return true;
	}

	public bool FaceReset(ChaControl _female)
	{
		_female.ChangeEyesOpenMax(1f);
		FBSCtrlMouth mouthCtrl = _female.mouthCtrl;
		if (mouthCtrl != null)
		{
			mouthCtrl.OpenMin = 0f;
		}
		_female.DisableShapeMouth(disable: false);
		return true;
	}

	public bool BreathProc(AnimatorStateInfo _ai, ChaControl _female, int _main, bool _forceSleepIdle = false)
	{
		if (!_female.visibleAll || _female.objBody == null)
		{
			return false;
		}
		if (breathUseLists[_main].lstVoiceList.Count == 0)
		{
			return false;
		}
		foreach (KeyValuePair<string, BreathPtn> item in dicBreathUsePtns[_main])
		{
			if (!_ai.IsName(item.Key) && !_forceSleepIdle)
			{
				continue;
			}
			BreathPtn value = item.Value;
			if (_forceSleepIdle && value.anim != "D_Idle")
			{
				continue;
			}
			if (value.onlyOne && (value.anim == nowVoices[_main].animBreath || value.anim == nowVoices[_main].animVoice))
			{
				break;
			}
			if (nowVoices[_main].state != 0)
			{
				if (!value.force && isVoiceCheck(_main))
				{
					break;
				}
			}
			else
			{
				if (ctrlFlag.isGaugeHit != nowVoices[_main].isGaugeHit)
				{
					if (nowVoices[_main].breathInfo != null)
					{
						SetBreathFace(value, _main);
						SetFace(nowVoices[_main].Face, _female, _main);
					}
				}
				else
				{
					nowVoices[_main].timeFaceDelta += Time.deltaTime;
					if (nowVoices[_main].timeFaceDelta >= nowVoices[_main].timeFace && nowVoices[_main].breathInfo != null)
					{
						SetBreathFace(value, _main);
						SetFace(nowVoices[_main].Face, _female, _main);
					}
				}
				nowVoices[_main].isGaugeHit = ctrlFlag.isGaugeHit;
			}
			List<int> list = new List<int>();
			for (int i = 0; i < value.lstInfo.Count; i++)
			{
				BreathVoicePtnInfo breathVoicePtnInfo = value.lstInfo[i];
				if (IsPlayBreathVoicePtn(_female, breathVoicePtnInfo, _main))
				{
					list.AddRange(breathVoicePtnInfo.lstVoice.OrderBy((int inf) => Guid.NewGuid()).ToList());
				}
			}
			list = list.OrderBy((int inf) => Guid.NewGuid()).ToList();
			if (list.Count == 0)
			{
				break;
			}
			int num = list[0];
			VoiceListInfo voiceListInfo = breathUseLists[_main].lstVoiceList[num];
			if (nowVoices[_main].state != 0 || (nowVoices[_main].breathInfo != voiceListInfo && nowVoices[_main].breathGroup != voiceListInfo.group) || !isVoiceCheck(_main))
			{
				HSceneFlagCtrl.VoiceFlag voiceFlag = VoiceFlag();
				Manager.Voice instance = Singleton<Manager.Voice>.Instance;
				int no = personality[_main];
				string pathAsset = voiceListInfo.pathAsset;
				string nameFile = voiceListInfo.nameFile;
				float pitch = voicePitch[_main];
				Transform voiceTrans = voiceFlag.voiceTrs[_main];
				Transform transform = instance.OnecePlayChara(no, pathAsset, nameFile, pitch, 0f, 0f, isAsync: true, voiceTrans);
				AudioSource component = transform.GetComponent<AudioSource>();
				if (masturbation || les)
				{
					Singleton<Manager.Sound>.Instance.AudioSettingData3DOnly(component, 2);
				}
				else
				{
					component.rolloffMode = AudioRolloffMode.Linear;
				}
				_female.SetVoiceTransform(transform);
				if (!voiceFlag.lstUseAsset.Contains(voiceListInfo.pathAsset))
				{
					voiceFlag.lstUseAsset.Add(voiceListInfo.pathAsset);
				}
				nowVoices[_main].breathInfo = voiceListInfo;
				nowVoices[_main].state = VoiceKind.breath;
				nowVoices[_main].animBreath = value.anim;
				nowVoices[_main].notOverWrite = nowVoices[_main].breathInfo.notOverWrite;
				nowVoices[_main].arrayBreath = num;
				nowVoices[_main].breathGroup = voiceListInfo.group;
				SetBreathFace(value, _main);
				if (voiceFlag.urines[_main] && voiceListInfo.urine)
				{
					voiceFlag.urines[_main] = false;
				}
				SetFace(nowVoices[_main].Face, _female, _main);
			}
			break;
		}
		return true;
	}

	private bool IsPlayBreathVoicePtn(ChaControl _female, BreathVoicePtnInfo _lst, int _main)
	{
		if (!IsBreathPtnConditions(_female, _lst.lstConditions, _main))
		{
			return false;
		}
		if (!IsBreathAnimationList(_lst.lstAnimeID, nowId))
		{
			return false;
		}
		return true;
	}

	private bool IsBreathAnimationList(List<int> _lstAnimList, int _idNow)
	{
		if (_lstAnimList.Count == 0)
		{
			return true;
		}
		if (_lstAnimList.Contains(-1))
		{
			return true;
		}
		return _lstAnimList.Contains(_idNow);
	}

	private bool IsBreathPtnConditions(ChaControl _female, List<int> _lstConditions, int _main)
	{
		for (int i = 0; i < _lstConditions.Count; i++)
		{
			switch (_lstConditions[i])
			{
			case 0:
				if (hSceneManager.PersonalPhase[_main] > 1)
				{
					return false;
				}
				break;
			case 1:
				if (hSceneManager.PersonalPhase[_main] < 2)
				{
					return false;
				}
				break;
			case 2:
				if (hSceneManager.GetFlaverSkillLevel(2) >= IngoLimit)
				{
					return false;
				}
				break;
			case 3:
				if (hSceneManager.GetFlaverSkillLevel(2) < IngoLimit)
				{
					return false;
				}
				break;
			case 4:
				if (VoiceFlag().sleep)
				{
					return false;
				}
				break;
			case 5:
				if (!VoiceFlag().sleep)
				{
					return false;
				}
				break;
			case 6:
				if (ctrlFlag.nowSpeedStateFast)
				{
					return false;
				}
				break;
			case 7:
				if (!ctrlFlag.nowSpeedStateFast)
				{
					return false;
				}
				break;
			case 8:
				if (VoiceFlag().urines[_main])
				{
					return false;
				}
				break;
			case 9:
				if (!VoiceFlag().urines[_main])
				{
					return false;
				}
				break;
			}
		}
		return true;
	}

	private bool SetBreathFace(BreathPtn _ptn, int _main)
	{
		if (ctrlFlag.isGaugeHit)
		{
			if (nowVoices[_main].breathInfo.lstHitFace.Count > 0)
			{
				nowVoices[_main].Face = nowVoices[_main].breathInfo.lstHitFace[UnityEngine.Random.Range(0, nowVoices[_main].breathInfo.lstHitFace.Count)];
			}
		}
		else if (nowVoices[_main].breathInfo.lstNotHitFace.Count > 0)
		{
			nowVoices[_main].Face = nowVoices[_main].breathInfo.lstNotHitFace[UnityEngine.Random.Range(0, nowVoices[_main].breathInfo.lstNotHitFace.Count)];
		}
		nowVoices[_main].timeFaceDelta = 0f;
		nowVoices[_main].timeFace = UnityEngine.Random.Range(_ptn.timeChangeFaceMin, _ptn.timeChangeFaceMax);
		return true;
	}

	public bool VoiceProc(AnimatorStateInfo _ai, ChaControl _female, int _main)
	{
		HSceneFlagCtrl.VoiceFlag voiceFlag = VoiceFlag();
		if (Singleton<HSceneManager>.Instance.EventKind == HSceneManager.HEvent.Yobai || !_female.visibleAll || _female.objBody == null)
		{
			voiceFlag.playVoices[_main] = false;
			return false;
		}
		int num = VoiceProcDetail(_ai, _female, _isFirst: true, _main);
		if (num == 3)
		{
			num = VoiceProcDetail(_ai, _female, _isFirst: false, _main);
		}
		if (num != 2)
		{
			voiceFlag.playVoices[_main] = false;
			if (num == 1)
			{
				voiceFlag.playShorts[_main] = -1;
			}
		}
		return num == 1;
	}

	public int VoiceProcDetail(AnimatorStateInfo _ai, ChaControl _female, bool _isFirst, int _main)
	{
		if (!_female.visibleAll || _female.objBody == null)
		{
			return 0;
		}
		int result = 0;
		VoiceAnimationPlayInfo voiceAnimationPlayInfo = playAnimation.GetAnimation(_ai.shortNameHash);
		if (voiceAnimationPlayInfo == null)
		{
			voiceAnimationPlayInfo = new VoiceAnimationPlayInfo();
			voiceAnimationPlayInfo.animationHash = _ai.shortNameHash;
			playAnimation.lstPlayInfo.Add(voiceAnimationPlayInfo);
		}
		HSceneFlagCtrl.VoiceFlag voiceFlag = VoiceFlag();
		bool flag = ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 4 && ctrlFlag.nowAnimationInfo.nInitiativeFemale != 0;
		if (lstVoicePtn == null || lstVoicePtn[_main] == null)
		{
			if (flag && nowVoices[0].state != VoiceKind.voice && nowVoices[0].state != VoiceKind.startVoice)
			{
				voiceFlag.dialog = false;
			}
			return 0;
		}
		if (nowVoices[_main].notOverWrite && isVoiceCheck(_main))
		{
			return 2;
		}
		if (dicdiclstVoiceList[_main] == null || dicdiclstVoiceList[_main].Count == 0)
		{
			return 0;
		}
		int id = ctrlFlag.nowAnimationInfo.id;
		bool flag2 = ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 3;
		flag2 = flag2 && (id == 3 || id == 13 || id == 14);
		bool flag3 = false;
		for (int i = 0; i < lstVoicePtn[_main].Count; i++)
		{
			VoicePtn voicePtn = lstVoicePtn[_main][i];
			if ((!hSceneManager.bMerchant && !VoicePtnCondition(voicePtn.condition, _main)) || !VoicePtnStartEvent(voicePtn.startKind, _female, _main))
			{
				continue;
			}
			List<PlayVoiceinfo> list;
			if (voicePtn.howTalk == 0)
			{
				if (!_ai.IsName(voicePtn.anim))
				{
					continue;
				}
				List<int> _lstCategory = new List<int>();
				flag3 = voicePtn.anim == "WLoop" || voicePtn.anim == "MLoop" || voicePtn.anim == "SLoop" || voicePtn.anim == "OLoop";
				if (!flag2)
				{
					if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 3)
					{
						bool flag4 = false;
						if (voicePtn.lstInfo.Count > 0)
						{
							for (int j = 0; j < voicePtn.lstInfo.Count; j++)
							{
								flag4 |= VoiceAnimationList(voicePtn.lstInfo[j].lstAnimList, nowId);
								if (flag4)
								{
									break;
								}
							}
						}
						if (!flag4)
						{
							continue;
						}
					}
					if (voicePtn.LookFlag)
					{
						if (!voiceFlag.playVoices[_main])
						{
							break;
						}
					}
					else if (voiceAnimationPlayInfo.isPlays[_main])
					{
						break;
					}
				}
				else if (flag3)
				{
					if (!voicePtn.LookFlag)
					{
						continue;
					}
					if (!voiceFlag.playVoices[_main])
					{
						break;
					}
				}
				else if (voicePtn.LookFlag)
				{
					if (!voiceFlag.playVoices[_main])
					{
						break;
					}
				}
				else if (voiceAnimationPlayInfo.isPlays[_main])
				{
					break;
				}
				list = (from inf in GetPlayListNum(voicePtn.lstInfo, dicdiclstVoiceList[_main], ref _lstCategory, _female, playAnimation, _main)
					orderby Guid.NewGuid()
					select inf).ToList();
				if (list.Count == 0)
				{
					InitListPlayFlag(voicePtn.lstInfo, dicdiclstVoiceList[_main], _lstCategory);
					list = (from inf in GetPlayListNum(voicePtn.lstInfo, dicdiclstVoiceList[_main], ref _lstCategory, _female, playAnimation, _main)
						orderby Guid.NewGuid()
						select inf).ToList();
					if (list.Count == 0)
					{
						result = 3;
						continue;
					}
				}
				int voiceID = list[0].voiceID;
				int mode = list[0].mode;
				int kind = list[0].kind;
				if (dicdiclstVoiceList[_main].Count != 0 && dicdiclstVoiceList[_main].ContainsKey(mode) && dicdiclstVoiceList[_main][mode].ContainsKey(kind) && dicdiclstVoiceList[_main][mode][kind].dicdicVoiceList.Count != 0 && dicdiclstVoiceList[_main][mode][kind].dicdicVoiceList.ContainsKey(voiceID))
				{
					VoiceListInfo voiceListInfo = dicdiclstVoiceList[_main][mode][kind].dicdicVoiceList[voiceID];
					Manager.Voice instance = Singleton<Manager.Voice>.Instance;
					int no = personality[_main];
					string pathAsset = voiceListInfo.pathAsset;
					string nameFile = voiceListInfo.nameFile;
					float pitch = voicePitch[_main];
					Transform voiceTrans = voiceFlag.voiceTrs[_main];
					Transform transform = instance.OnecePlayChara(no, pathAsset, nameFile, pitch, 0f, 0f, isAsync: true, voiceTrans);
					AudioSource component = transform.GetComponent<AudioSource>();
					if (masturbation || les)
					{
						Singleton<Manager.Sound>.Instance.AudioSettingData3DOnly(component, 2);
					}
					else
					{
						component.rolloffMode = AudioRolloffMode.Linear;
					}
					_female.SetVoiceTransform(transform);
					if (!voiceFlag.lstUseAsset.Contains(voiceListInfo.pathAsset))
					{
						voiceFlag.lstUseAsset.Add(voiceListInfo.pathAsset);
					}
					nowVoices[_main].voiceInfo = voiceListInfo;
					nowVoices[_main].state = VoiceKind.voice;
					nowVoices[_main].animVoice = voicePtn.anim;
					nowVoices[_main].notOverWrite = nowVoices[_main].voiceInfo.notOverWrite;
					nowVoices[_main].arrayVoice = voiceID;
					nowVoices[_main].VoiceListID = mode;
					nowVoices[_main].VoiceListSheetID = kind;
					nowVoices[_main].voiceInfo.isPlay = true;
					if (nowVoices[_main].voiceInfo.lstHitFace.Count > 0)
					{
						nowVoices[_main].Face = nowVoices[_main].voiceInfo.lstHitFace[UnityEngine.Random.Range(0, nowVoices[_main].voiceInfo.lstHitFace.Count)];
					}
					voiceAnimationPlayInfo.isPlays[_main] = true;
					result = 1;
					SetFace(nowVoices[_main].Face, _female, _main);
					voiceFlag.dialog = false;
				}
				break;
			}
			if (flag)
			{
				if ((voicePtn.howTalk == 1 && (voiceFlag.dialog || _main == 1)) || (voicePtn.howTalk == 2 && (!voiceFlag.dialog || _main == 0)))
				{
					continue;
				}
			}
			else if ((voicePtn.howTalk == 1 && (voiceFlag.dialog || _main == 0)) || (voicePtn.howTalk == 2 && (!voiceFlag.dialog || _main == 1)))
			{
				continue;
			}
			if (!_ai.IsName(voicePtn.anim))
			{
				continue;
			}
			List<int> _lstCategory2 = new List<int>();
			if (voicePtn.LookFlag)
			{
				if ((!flag && _main == 1) || (flag && _main == 0))
				{
					if (!voiceFlag.playVoices[_main])
					{
						break;
					}
					if (voiceAnimationPlayInfo.isPlays[_main])
					{
						continue;
					}
				}
			}
			else if (voiceAnimationPlayInfo.isPlays[_main])
			{
				break;
			}
			if (nowVoices[_main ^ 1].state == VoiceKind.voice || nowVoices[_main ^ 1].state == VoiceKind.startVoice)
			{
				break;
			}
			list = (from inf in GetPlayListNum(voicePtn.lstInfo, dicdiclstVoiceList[_main], ref _lstCategory2, _female, playAnimation, _main)
				orderby Guid.NewGuid()
				select inf).ToList();
			if (list.Count == 0 && list.Count == 0)
			{
				result = 3;
				continue;
			}
			int voiceID2 = list[0].voiceID;
			int mode2 = list[0].mode;
			int kind2 = list[0].kind;
			if (dicdiclstVoiceList[_main].Count != 0 && dicdiclstVoiceList[_main].ContainsKey(mode2) && dicdiclstVoiceList[_main][mode2].ContainsKey(kind2) && dicdiclstVoiceList[_main][mode2][kind2].dicdicVoiceList.Count != 0 && dicdiclstVoiceList[_main][mode2][kind2].dicdicVoiceList.ContainsKey(voiceID2))
			{
				VoiceListInfo voiceListInfo2 = dicdiclstVoiceList[_main][mode2][kind2].dicdicVoiceList[voiceID2];
				Manager.Voice instance2 = Singleton<Manager.Voice>.Instance;
				int no = personality[_main];
				string nameFile = voiceListInfo2.pathAsset;
				string pathAsset = voiceListInfo2.nameFile;
				float pitch = voicePitch[_main];
				Transform voiceTrans = voiceFlag.voiceTrs[_main];
				Transform transform2 = instance2.OnecePlayChara(no, nameFile, pathAsset, pitch, 0f, 0f, isAsync: true, voiceTrans);
				AudioSource component2 = transform2.GetComponent<AudioSource>();
				if (masturbation || les)
				{
					Singleton<Manager.Sound>.Instance.AudioSettingData3DOnly(component2, 2);
				}
				else
				{
					component2.rolloffMode = AudioRolloffMode.Linear;
				}
				_female.SetVoiceTransform(transform2);
				if (!voiceFlag.lstUseAsset.Contains(voiceListInfo2.pathAsset))
				{
					voiceFlag.lstUseAsset.Add(voiceListInfo2.pathAsset);
				}
				nowVoices[_main].voiceInfo = voiceListInfo2;
				nowVoices[_main].state = VoiceKind.voice;
				nowVoices[_main].animVoice = voicePtn.anim;
				nowVoices[_main].notOverWrite = nowVoices[_main].voiceInfo.notOverWrite;
				nowVoices[_main].arrayVoice = voiceID2;
				nowVoices[_main].VoiceListID = mode2;
				nowVoices[_main].VoiceListSheetID = kind2;
				nowVoices[_main].voiceInfo.isPlay = true;
				if (nowVoices[_main].voiceInfo.lstHitFace.Count > 0)
				{
					nowVoices[_main].Face = nowVoices[_main].voiceInfo.lstHitFace[UnityEngine.Random.Range(0, nowVoices[_main].voiceInfo.lstHitFace.Count)];
				}
				voiceAnimationPlayInfo.isPlays[_main] = true;
				result = 1;
				SetFace(nowVoices[_main].Face, _female, _main);
				voiceFlag.dialog = voicePtn.howTalk == 1;
			}
			break;
		}
		return result;
	}

	private List<PlayVoiceinfo> GetPlayListNum(List<VoicePtnInfo> _lst, Dictionary<int, Dictionary<int, VoiceList>> _lstVoice, ref List<int> _lstCategory, ChaControl female, VoiceAnimationPlay nowVoiceInfo, int _main)
	{
		List<PlayVoiceinfo> list = new List<PlayVoiceinfo>();
		for (int i = 0; i < _lst.Count; i++)
		{
			if (VoiceAnimationList(_lst[i].lstAnimList, nowId) && (ctrlFlag.initiative == 0 || _lst[i].lstPlayConditions.Contains(13)) && VoicePtnConditions(_lst[i].lstPlayConditions, female, nowVoiceInfo, _main))
			{
				_lstCategory.Add(i);
				list.AddRange(GetPlayNum(_lst[i], _lstVoice));
			}
		}
		return list;
	}

	private bool VoicePtnStartEvent(int EventPtn, ChaControl _female, int _main)
	{
		switch (EventPtn)
		{
		case 0:
			if (_female != null && hSceneManager.Mood[_main] < _female.fileGameInfo.moodBound.lower)
			{
				return false;
			}
			if (hSceneManager.EventKind != 0 && hSceneManager.EventKind != HSceneManager.HEvent.GyakuYobai && hSceneManager.EventKind != HSceneManager.HEvent.FromFemale)
			{
				return false;
			}
			break;
		case 1:
			if ((!(_female != null) || !(hSceneManager.Mood[_main] < _female.fileGameInfo.moodBound.lower)) && (hSceneManager.EventKind == HSceneManager.HEvent.Normal || hSceneManager.EventKind == HSceneManager.HEvent.GyakuYobai || hSceneManager.EventKind == HSceneManager.HEvent.FromFemale))
			{
				return false;
			}
			break;
		}
		return true;
	}

	private bool VoicePtnCondition(int _condition, int _main)
	{
		if (ctrlFlag.isFaintness)
		{
			if (_condition != 4)
			{
				return false;
			}
			return true;
		}
		switch (_condition)
		{
		case 0:
			if (hSceneManager.PersonalPhase[_main] >= 2)
			{
				return false;
			}
			break;
		case 1:
			if (hSceneManager.PersonalPhase[_main] < 2)
			{
				return false;
			}
			break;
		case 4:
			return false;
		}
		return true;
	}

	private bool VoicePtnConditionAccurate(int _condition, int _main)
	{
		switch (_condition)
		{
		case 0:
			if (hSceneManager.PersonalPhase[_main] >= 2 || hSceneManager.isForce || ctrlFlag.isFaintness)
			{
				return false;
			}
			break;
		case 1:
			if (hSceneManager.PersonalPhase[_main] < 2 || hSceneManager.isForce || ctrlFlag.isFaintness)
			{
				return false;
			}
			break;
		case 2:
			if (!hSceneManager.isForce || ctrlFlag.isFaintness)
			{
				return false;
			}
			break;
		case 3:
			if (ctrlFlag.isFaintness)
			{
				return false;
			}
			break;
		case 4:
			if (!ctrlFlag.isFaintness)
			{
				return false;
			}
			break;
		}
		return true;
	}

	private bool VoiceAnimationList(List<int> _lstAnimList, int _idNow)
	{
		if (_lstAnimList.Count == 0)
		{
			return false;
		}
		if (_lstAnimList.Contains(-1))
		{
			return true;
		}
		return _lstAnimList.Contains(_idNow);
	}

	private bool VoicePtnConditions(List<int> _lstConditions, ChaControl female, VoiceAnimationPlay nowVoiceInfo, int _main)
	{
		if (_lstConditions.Contains(13) && ctrlFlag.initiative == 0)
		{
			return false;
		}
		HSceneFlagCtrl.VoiceFlag voiceFlag = VoiceFlag();
		for (int i = 0; i < _lstConditions.Count; i++)
		{
			switch (_lstConditions[i])
			{
			case 0:
				if (ctrlFlag.StartHouchiTime > HouchiTime)
				{
					return false;
				}
				break;
			case 1:
				if (female.GetBustSizeKind() != 0)
				{
					return false;
				}
				break;
			case 2:
				if (female.GetBustSizeKind() != 2)
				{
					return false;
				}
				break;
			case 3:
				if (female.GetBustSizeKind() != 1)
				{
					return false;
				}
				break;
			case 4:
				if (nowVoiceInfo == null || nowVoiceInfo.Count == 0)
				{
					return false;
				}
				break;
			case 5:
				if (nowVoiceInfo == null || nowVoiceInfo.Count < 1)
				{
					return false;
				}
				break;
			case 6:
				if (hSceneManager.GetFlaverSkillLevel(2) < IngoLimit)
				{
					return false;
				}
				break;
			case 7:
				if (hSceneManager.GetFlaverSkillLevel(2) >= IngoLimit)
				{
					return false;
				}
				break;
			case 8:
				if (_main != 0)
				{
					return false;
				}
				break;
			case 9:
				if (_main != 1)
				{
					return false;
				}
				break;
			case 10:
				if (voiceFlag.oldFinish != 0)
				{
					return false;
				}
				break;
			case 11:
				if (voiceFlag.oldFinish != 2)
				{
					return false;
				}
				break;
			case 12:
				if (voiceFlag.oldFinish != 3)
				{
					return false;
				}
				break;
			case 14:
				if (voiceFlag.oldFinish != 1)
				{
					return false;
				}
				break;
			}
		}
		if (_lstConditions.Contains(0) && nowMode < 4)
		{
			HouchiTime = 0f;
		}
		return true;
	}

	private List<PlayVoiceinfo> GetPlayNum(VoicePtnInfo _lstPlay, Dictionary<int, Dictionary<int, VoiceList>> _lstVoice)
	{
		List<PlayVoiceinfo> list = new List<PlayVoiceinfo>();
		if (_lstVoice.Count == 0)
		{
			return list;
		}
		int loadListmode = _lstPlay.loadListmode;
		int loadListKind = _lstPlay.loadListKind;
		if (!_lstVoice.ContainsKey(loadListmode) || !_lstVoice[loadListmode].ContainsKey(loadListKind))
		{
			return list;
		}
		Dictionary<int, VoiceListInfo> dicdicVoiceList = _lstVoice[loadListmode][loadListKind].dicdicVoiceList;
		PlayVoiceinfo item = default(PlayVoiceinfo);
		for (int i = 0; i < _lstPlay.lstVoice.Count; i++)
		{
			if (!dicdicVoiceList.ContainsKey(_lstPlay.lstVoice[i]))
			{
				GlobalMethod.DebugLog("再生しようとしている番号がリストにない", 1);
			}
			else if (!dicdicVoiceList[_lstPlay.lstVoice[i]].isPlay)
			{
				item.mode = loadListmode;
				item.kind = loadListKind;
				item.voiceID = _lstPlay.lstVoice[i];
				list.Add(item);
			}
		}
		return list;
	}

	private bool InitListPlayFlag(List<VoicePtnInfo> _lst, Dictionary<int, Dictionary<int, VoiceList>> _lstVoice, List<int> _lstCategory)
	{
		for (int i = 0; i < _lstCategory.Count; i++)
		{
			if (_lst.Count <= _lstCategory[i])
			{
				continue;
			}
			int loadListmode = _lst[_lstCategory[i]].loadListmode;
			int loadListKind = _lst[_lstCategory[i]].loadListKind;
			for (int j = 0; j < _lst[_lstCategory[i]].lstVoice.Count; j++)
			{
				int key = _lst[_lstCategory[i]].lstVoice[j];
				if (_lstVoice.ContainsKey(loadListmode) && _lstVoice[loadListmode].ContainsKey(loadListKind))
				{
					if (!_lstVoice[loadListmode][loadListKind].dicdicVoiceList.ContainsKey(key))
					{
						GlobalMethod.DebugLog("再生しようとしている番号がリストにない", 1);
					}
					else
					{
						_lstVoice[loadListmode][loadListKind].dicdicVoiceList[key].isPlay = false;
					}
				}
			}
		}
		return true;
	}

	private bool StartVoiceProc(ChaControl[] _females, int _nFemale)
	{
		bool result = false;
		List<StartVoicePtn> list = null;
		HSceneFlagCtrl.VoiceFlag voiceFlag = VoiceFlag();
		if (_females[_nFemale] == null || _females[_nFemale].objBody == null)
		{
			if (voiceFlag.playStart != -1 && _nFemale == 0)
			{
				voiceFlag.playStart = -1;
			}
			return false;
		}
		list = lstLoadStartVoicePtn[_nFemale];
		if (list == null)
		{
			return false;
		}
		for (int i = 0; i < 2; i++)
		{
			if (nowVoices[i].notOverWrite && isVoiceCheck(_nFemale))
			{
				return result;
			}
		}
		if (_nFemale == 0 && voiceFlag.playStart > 4 && (nowVoices[1].state == VoiceKind.startVoice || nowVoices[1].state == VoiceKind.voice))
		{
			return result;
		}
		if (dicdiclstVoiceList[_nFemale] == null)
		{
			return false;
		}
		for (int j = 0; j < list.Count; j++)
		{
			StartVoicePtn startVoicePtn = list[j];
			if (startVoicePtn.nTaii != nowMode || (!hSceneManager.bMerchant && !VoicePtnCondition(startVoicePtn.condition, _nFemale)) || startVoicePtn.timing != voiceFlag.playStart)
			{
				continue;
			}
			if (startVoicePtn.nForce != -1)
			{
				if (startVoicePtn.nForce == 0)
				{
					if (hSceneManager.isForce || (hSceneManager.EventKind != 0 && hSceneManager.EventKind != HSceneManager.HEvent.GyakuYobai && hSceneManager.EventKind != HSceneManager.HEvent.FromFemale))
					{
						continue;
					}
				}
				else if (MapHID >= 0 || !hSceneManager.isForce || (startVoicePtn.nForce == 2 && Singleton<HSceneFlagCtrl>.Instance.nPlace != 15) || (startVoicePtn.nForce == 3 && Singleton<HSceneFlagCtrl>.Instance.nPlace != 12) || (startVoicePtn.nForce == 5 && hSceneManager.Mood[_nFemale] > _females[_nFemale].fileGameInfo.moodBound.lower) || (startVoicePtn.nForce == 4 && hSceneManager.Agent[0].BehaviorResources.Mode != Desire.ActionType.EndTaskMasturbation))
				{
					continue;
				}
			}
			List<int> _lstCategory = new List<int>();
			List<PlayVoiceinfo> list2 = (from inf in GetPlayListNum(startVoicePtn.lstInfo, dicdiclstVoiceList[_nFemale], ref _lstCategory, _nFemale, _females[_nFemale])
				orderby Guid.NewGuid()
				select inf).ToList();
			if (list2.Count == 0)
			{
				InitGetPlayListNum(startVoicePtn.lstInfo, dicdiclstVoiceList[_nFemale], _lstCategory);
				list2 = (from inf in GetPlayListNum(startVoicePtn.lstInfo, dicdiclstVoiceList[_nFemale], ref _lstCategory, _nFemale, _females[_nFemale])
					orderby Guid.NewGuid()
					select inf).ToList();
				if (list2.Count == 0)
				{
					if (j == list.Count - 1)
					{
						Debug.Log("再生するものがない[" + _nFemale + "]");
						if (_nFemale == 0 && voiceFlag.playStart < 5)
						{
							voiceFlag.playStart = -1;
						}
						break;
					}
					continue;
				}
			}
			if (startVoicePtn.timing == 4 && ctrlFlag.nowAnimationInfo.nInitiativeFemale != 0)
			{
				voiceFlag.playStart = -1;
				break;
			}
			int mode = list2[0].mode;
			int kind = list2[0].kind;
			int voiceID = list2[0].voiceID;
			if (!dicdiclstVoiceList[_nFemale][mode][kind].dicdicVoiceList.ContainsKey(voiceID))
			{
				GlobalMethod.DebugLog("配列外してる[" + _nFemale + "]", 1);
			}
			else
			{
				if (Singleton<HSceneManager>.Instance.EventKind == HSceneManager.HEvent.Yobai)
				{
					break;
				}
				VoiceListInfo voiceListInfo = dicdiclstVoiceList[_nFemale][mode][kind].dicdicVoiceList[voiceID];
				Manager.Voice instance = Singleton<Manager.Voice>.Instance;
				int no = personality[_nFemale];
				string pathAsset = voiceListInfo.pathAsset;
				string nameFile = voiceListInfo.nameFile;
				float pitch = voicePitch[_nFemale];
				Transform voiceTrans = voiceFlag.voiceTrs[_nFemale];
				Transform transform = instance.OnecePlayChara(no, pathAsset, nameFile, pitch, 0f, 0f, isAsync: true, voiceTrans);
				AudioSource component = transform.GetComponent<AudioSource>();
				if (masturbation || les)
				{
					Singleton<Manager.Sound>.Instance.AudioSettingData3DOnly(component, 2);
				}
				else
				{
					component.rolloffMode = AudioRolloffMode.Linear;
				}
				_females[_nFemale].SetVoiceTransform(transform);
				if (!voiceFlag.lstUseAsset.Contains(voiceListInfo.pathAsset))
				{
					voiceFlag.lstUseAsset.Add(voiceListInfo.pathAsset);
				}
				nowVoices[_nFemale].voiceInfo = voiceListInfo;
				if (startVoicePtn.timing != 0 && startVoicePtn.timing < 4)
				{
					VoiceKind[] array = new VoiceKind[3]
					{
						VoiceKind.startVoice,
						VoiceKind.startVoice,
						VoiceKind.voice
					};
					nowVoices[_nFemale].state = array[startVoicePtn.timing - 1];
					if (_nFemale == 1)
					{
						voiceFlag.playStart += 4;
						if (nowVoices[0].state != 0 && nowVoices[0].state != VoiceKind.breathShort)
						{
							Singleton<Manager.Voice>.Instance.Stop(voiceFlag.voiceTrs[_nFemale ^ 1]);
						}
					}
					else
					{
						voiceFlag.playStart = -1;
					}
				}
				else if (startVoicePtn.timing == 0)
				{
					nowVoices[_nFemale].state = VoiceKind.startVoice;
					voiceFlag.playStart = -1;
				}
				else
				{
					if (startVoicePtn.timing == 5 || startVoicePtn.timing == 6)
					{
						nowVoices[_nFemale].state = VoiceKind.startVoice;
					}
					else
					{
						nowVoices[_nFemale].state = VoiceKind.voice;
					}
					if (_nFemale == 1 && nowVoices[0].state != 0 && nowVoices[0].state != VoiceKind.breathShort)
					{
						Singleton<Manager.Voice>.Instance.Stop(voiceFlag.voiceTrs[_nFemale ^ 1]);
					}
					voiceFlag.playStart = -1;
				}
				nowVoices[_nFemale].animVoice = startVoicePtn.anim;
				nowVoices[_nFemale].notOverWrite = nowVoices[_nFemale].voiceInfo.notOverWrite;
				nowVoices[_nFemale].arrayVoice = voiceID;
				nowVoices[_nFemale].VoiceListID = mode;
				nowVoices[_nFemale].VoiceListSheetID = kind;
				if (nowVoices[_nFemale].voiceInfo.lstHitFace.Count > 0)
				{
					nowVoices[_nFemale].Face = nowVoices[_nFemale].voiceInfo.lstHitFace[UnityEngine.Random.Range(0, nowVoices[_nFemale].voiceInfo.lstHitFace.Count)];
				}
				nowVoices[_nFemale].voiceInfo.isPlay = true;
				result = true;
				SetFace(nowVoices[_nFemale].Face, _females[_nFemale], _nFemale);
			}
			break;
		}
		return result;
	}

	private List<PlayVoiceinfo> GetPlayListNum(List<VoicePtnInfo> _lst, Dictionary<int, Dictionary<int, VoiceList>> _lstVoice, ref List<int> _lstCategory, int _main, ChaControl female)
	{
		List<PlayVoiceinfo> list = new List<PlayVoiceinfo>();
		for (int i = 0; i < _lst.Count; i++)
		{
			if ((_lst[i].lstAnimList[0] == -1 || _lst[i].lstAnimList.Contains(ctrlFlag.nowAnimationInfo.id)) && StartVoicePtnConditions(_lst[i].lstPlayConditions, female, _main))
			{
				list.AddRange(GetPlayNum(_lst[i], _lstVoice));
				_lstCategory.Add(i);
			}
		}
		return list;
	}

	private bool StartVoicePtnConditions(List<int> conditions, ChaControl female, int _main)
	{
		if (conditions.Contains(7) && ctrlFlag.initiative == 0)
		{
			return false;
		}
		for (int i = 0; i < conditions.Count; i++)
		{
			switch (conditions[i])
			{
			case 0:
				if (female.GetBustSizeKind() != 0)
				{
					return false;
				}
				break;
			case 1:
				if (female.GetBustSizeKind() != 2)
				{
					return false;
				}
				break;
			case 2:
				if (female.GetBustSizeKind() != 1)
				{
					return false;
				}
				break;
			case 3:
				if (hSceneManager.GetFlaverSkillLevel(2) < IngoLimit)
				{
					return false;
				}
				break;
			case 4:
				if (hSceneManager.GetFlaverSkillLevel(2) >= IngoLimit)
				{
					return false;
				}
				break;
			case 5:
				if (_main != 0)
				{
					return false;
				}
				break;
			case 6:
				if (_main != 1)
				{
					return false;
				}
				break;
			}
		}
		return true;
	}

	private void InitGetPlayListNum(List<VoicePtnInfo> _lst, Dictionary<int, Dictionary<int, VoiceList>> _lstVoice, List<int> _lstCategory)
	{
		for (int i = 0; i < _lstCategory.Count; i++)
		{
			if (_lst.Count <= _lstCategory[i])
			{
				GlobalMethod.DebugLog("開始音声のカテゴリ取得に失敗(フラグ再設定時)", 1);
				continue;
			}
			int loadListmode = _lst[_lstCategory[i]].loadListmode;
			int loadListKind = _lst[_lstCategory[i]].loadListKind;
			for (int j = 0; j < _lst[_lstCategory[i]].lstVoice.Count; j++)
			{
				if (!_lstVoice.ContainsKey(loadListmode) || !_lstVoice[loadListmode].ContainsKey(loadListKind))
				{
					GlobalMethod.DebugLog("開始音声の再生番号取得に失敗(フラグ再設定時)", 1);
					continue;
				}
				int key = _lst[_lstCategory[i]].lstVoice[j];
				if (!_lstVoice[loadListmode][loadListKind].dicdicVoiceList.ContainsKey(key))
				{
					GlobalMethod.DebugLog("開始音声の再生番号取得に失敗(フラグ再設定時)", 1);
				}
				else
				{
					_lstVoice[loadListmode][loadListKind].dicdicVoiceList[key].isPlay = false;
				}
			}
		}
	}

	private List<PlayVoiceinfo> GetHBeforePlayListNum(List<VoicePtnInfo> _lst, Dictionary<int, Dictionary<int, VoiceList>> _lstVoice, ref List<int> _lstCategory, int _main, ChaControl female)
	{
		List<PlayVoiceinfo> list = new List<PlayVoiceinfo>();
		for (int i = 0; i < _lst.Count; i++)
		{
			if (StartVoicePtnConditions(_lst[i].lstPlayConditions, female, _main))
			{
				list.AddRange(GetPlayNum(_lst[i], _lstVoice));
				_lstCategory.Add(i);
			}
		}
		return list;
	}

	public void HBeforeProc(ChaControl[] _females)
	{
		isPlays[0] = false;
		isPlays[1] = false;
		for (int num = 1; num > -1; num--)
		{
			int num2 = num;
			if (_females.Length != 1 && !(_females[num2] == null))
			{
				isPlays[num2] = HBeforeProc(_females, num2);
			}
		}
	}

	private bool HBeforeProc(ChaControl[] _females, int _nFemale)
	{
		List<StartVoicePtn> list = null;
		HSceneFlagCtrl.VoiceFlag voiceFlag = VoiceFlag();
		if (_females[_nFemale] == null || _females[_nFemale].objBody == null)
		{
			if (voiceFlag.playStart != -1 && _nFemale == 0)
			{
				voiceFlag.playStart = -1;
			}
			return false;
		}
		list = lstLoadStartVoicePtn[_nFemale];
		if (list == null)
		{
			return false;
		}
		for (int i = 0; i < 2; i++)
		{
			if (nowVoices[i].notOverWrite && isVoiceCheck(_nFemale))
			{
				return false;
			}
		}
		if (_nFemale == 0 && voiceFlag.playStart > 4 && (nowVoices[1].state == VoiceKind.startVoice || nowVoices[1].state == VoiceKind.voice))
		{
			return false;
		}
		if (dicdiclstVoiceList[_nFemale] == null)
		{
			return false;
		}
		for (int j = 0; j < list.Count; j++)
		{
			StartVoicePtn startVoicePtn = list[j];
			if (startVoicePtn.timing != 0)
			{
				continue;
			}
			if (startVoicePtn.nForce != -1)
			{
				if (startVoicePtn.nForce == 0)
				{
					if (hSceneManager.EventKind != 0 && hSceneManager.EventKind != HSceneManager.HEvent.GyakuYobai && hSceneManager.EventKind != HSceneManager.HEvent.FromFemale)
					{
						continue;
					}
				}
				else if (!hSceneManager.isForce || (startVoicePtn.nForce == 2 && Singleton<HSceneFlagCtrl>.Instance.nPlace != 15) || (startVoicePtn.nForce == 3 && Singleton<HSceneFlagCtrl>.Instance.nPlace != 12) || (startVoicePtn.nForce == 4 && hSceneManager.Agent[0].BehaviorResources.Mode != Desire.ActionType.EndTaskMasturbation))
				{
					continue;
				}
			}
			if ((!hSceneManager.bMerchant && !VoicePtnCondition(startVoicePtn.condition, _nFemale)) || startVoicePtn.timing != voiceFlag.playStart)
			{
				continue;
			}
			List<int> _lstCategory = new List<int>();
			List<PlayVoiceinfo> list2 = (from inf in GetHBeforePlayListNum(startVoicePtn.lstInfo, dicdiclstVoiceList[_nFemale], ref _lstCategory, _nFemale, _females[_nFemale])
				orderby Guid.NewGuid()
				select inf).ToList();
			if (list2.Count == 0)
			{
				InitGetPlayListNum(startVoicePtn.lstInfo, dicdiclstVoiceList[_nFemale], _lstCategory);
				list2 = (from inf in GetHBeforePlayListNum(startVoicePtn.lstInfo, dicdiclstVoiceList[_nFemale], ref _lstCategory, _nFemale, _females[_nFemale])
					orderby Guid.NewGuid()
					select inf).ToList();
				if (list2.Count == 0)
				{
					if (j == list.Count - 1)
					{
						Debug.Log("再生するものがない[" + _nFemale + "]");
						if (_nFemale == 0 && voiceFlag.playStart < 5)
						{
							voiceFlag.playStart = -1;
						}
						break;
					}
					continue;
				}
			}
			int mode = list2[0].mode;
			int kind = list2[0].kind;
			int voiceID = list2[0].voiceID;
			if (!dicdiclstVoiceList[_nFemale][mode][kind].dicdicVoiceList.ContainsKey(voiceID))
			{
				GlobalMethod.DebugLog("配列外してる[" + _nFemale + "]", 1);
			}
			else
			{
				if (Singleton<HSceneManager>.Instance.EventKind == HSceneManager.HEvent.Yobai)
				{
					break;
				}
				VoiceListInfo voiceListInfo = dicdiclstVoiceList[_nFemale][mode][kind].dicdicVoiceList[voiceID];
				Manager.Voice instance = Singleton<Manager.Voice>.Instance;
				int no = personality[_nFemale];
				string pathAsset = voiceListInfo.pathAsset;
				string nameFile = voiceListInfo.nameFile;
				float pitch = voicePitch[_nFemale];
				Transform voiceTrans = voiceFlag.voiceTrs[_nFemale];
				Transform transform = instance.OnecePlayChara(no, pathAsset, nameFile, pitch, 0f, 0f, isAsync: true, voiceTrans);
				AudioSource component = transform.GetComponent<AudioSource>();
				if (masturbation || les)
				{
					Singleton<Manager.Sound>.Instance.AudioSettingData3DOnly(component, 2);
				}
				else
				{
					component.rolloffMode = AudioRolloffMode.Linear;
				}
				_females[_nFemale].SetVoiceTransform(transform);
				if (!voiceFlag.lstUseAsset.Contains(voiceListInfo.pathAsset))
				{
					voiceFlag.lstUseAsset.Add(voiceListInfo.pathAsset);
				}
				nowVoices[_nFemale].voiceInfo = voiceListInfo;
				nowVoices[_nFemale].state = VoiceKind.startVoice;
				if (_nFemale == 1)
				{
					voiceFlag.playStart += 5;
					if (nowVoices[0].state != 0 && nowVoices[0].state != VoiceKind.breathShort)
					{
						Singleton<Manager.Voice>.Instance.Stop(voiceFlag.voiceTrs[_nFemale ^ 1]);
					}
				}
				else
				{
					voiceFlag.playStart = -1;
				}
				nowVoices[_nFemale].animVoice = startVoicePtn.anim;
				nowVoices[_nFemale].notOverWrite = nowVoices[_nFemale].voiceInfo.notOverWrite;
				nowVoices[_nFemale].arrayVoice = voiceID;
				nowVoices[_nFemale].VoiceListID = mode;
				nowVoices[_nFemale].VoiceListSheetID = kind;
				if (nowVoices[_nFemale].voiceInfo.lstHitFace.Count > 0)
				{
					nowVoices[_nFemale].Face = nowVoices[_nFemale].voiceInfo.lstHitFace[UnityEngine.Random.Range(0, nowVoices[_nFemale].voiceInfo.lstHitFace.Count)];
				}
				nowVoices[_nFemale].voiceInfo.isPlay = true;
				SetFace(nowVoices[_nFemale].Face, _females[_nFemale], _nFemale);
				HBeforeHouchiTime = 0f;
			}
			break;
		}
		return true;
	}

	public bool ShortBreathProc(ChaControl _female, int _main)
	{
		HSceneFlagCtrl.VoiceFlag voiceFlag = VoiceFlag();
		if (!_female.visibleAll || _female.objBody == null)
		{
			voiceFlag.playShorts[_main] = -1;
		}
		if (nowVoices[_main].notOverWrite && isVoiceCheck(_main))
		{
			voiceFlag.playShorts[_main] = -1;
		}
		if (Singleton<HSceneManager>.Instance.EventKind == HSceneManager.HEvent.Yobai)
		{
			voiceFlag.playShorts[_main] = -1;
		}
		if (dicShortBreathUsePtns[_main] == null)
		{
			voiceFlag.playShorts[_main] = -1;
		}
		if (voiceFlag.playShorts[_main] == -1)
		{
			return false;
		}
		if (dicShortBreathUsePtns[_main] != null && !dicShortBreathUsePtns[_main].ContainsKey(voiceFlag.playShorts[_main]))
		{
			return false;
		}
		List<BreathVoicePtnInfo> list = dicShortBreathUsePtns[_main][voiceFlag.playShorts[_main]];
		if (list.Count == 0)
		{
			voiceFlag.playShorts[_main] = -1;
			return false;
		}
		List<int> list2 = new List<int>();
		for (int i = 0; i < list.Count; i++)
		{
			if (IsPlayShortBreathVoicePtn(_female, list[i], _main))
			{
				list2.AddRange(list[i].lstVoice);
			}
		}
		if (list2.Count == 0)
		{
			return false;
		}
		list2 = list2.OrderBy((int inf) => Guid.NewGuid()).ToList();
		int num = list2[0];
		if (!ShortBreathLists[_main].dicShortBreathLists.ContainsKey(num))
		{
			voiceFlag.playShorts[_main] = -1;
			return false;
		}
		Dictionary<int, VoiceListInfo> dicShortBreathLists = ShortBreathLists[_main].dicShortBreathLists;
		Manager.Voice instance = Singleton<Manager.Voice>.Instance;
		int no = personality[_main];
		string pathAsset = dicShortBreathLists[num].pathAsset;
		string nameFile = dicShortBreathLists[num].nameFile;
		float pitch = voicePitch[_main];
		Transform voiceTrans = voiceFlag.voiceTrs[_main];
		Transform transform = instance.OnecePlayChara(no, pathAsset, nameFile, pitch, 0f, 0f, isAsync: true, voiceTrans);
		AudioSource component = transform.GetComponent<AudioSource>();
		if (masturbation || les)
		{
			Singleton<Manager.Sound>.Instance.AudioSettingData3DOnly(component, 2);
		}
		else
		{
			component.rolloffMode = AudioRolloffMode.Linear;
		}
		_female.SetVoiceTransform(transform);
		if (!voiceFlag.lstUseAsset.Contains(dicShortBreathLists[num].pathAsset))
		{
			voiceFlag.lstUseAsset.Add(dicShortBreathLists[num].pathAsset);
		}
		nowVoices[_main].shortInfo = dicShortBreathLists[num];
		nowVoices[_main].state = VoiceKind.breathShort;
		nowVoices[_main].notOverWrite = nowVoices[_main].shortInfo.notOverWrite;
		nowVoices[_main].arrayShort = num;
		nowVoices[_main].Face = nowVoices[_main].shortInfo.lstHitFace[UnityEngine.Random.Range(0, nowVoices[_main].shortInfo.lstHitFace.Count)];
		voiceFlag.playShorts[_main] = -1;
		SetFace(nowVoices[_main].Face, _female, _main);
		return true;
	}

	private bool IsPlayShortBreathVoicePtn(ChaControl _female, BreathVoicePtnInfo _lst, int _main)
	{
		if (!CheckShortVoiceCondition(_female, _lst.lstConditions, _main))
		{
			return false;
		}
		if (!IsShortBreathAnimationList(_lst.lstAnimeID, nowId))
		{
			return false;
		}
		return true;
	}

	private bool IsShortBreathAnimationList(List<int> _lstAnimList, int _idNow)
	{
		if (_lstAnimList.Count == 0)
		{
			return true;
		}
		if (_lstAnimList.Contains(-1))
		{
			return true;
		}
		return _lstAnimList.Contains(_idNow);
	}

	private bool CheckShortVoiceCondition(ChaControl _female, List<int> _lstConditions, int _main)
	{
		HSceneFlagCtrl.VoiceFlag voiceFlag = VoiceFlag();
		for (int i = 0; i < _lstConditions.Count; i++)
		{
			switch (_lstConditions[i])
			{
			case 0:
				if (hSceneManager.isForce || ctrlFlag.nowAnimationInfo.nInitiativeFemale != 0 || ctrlFlag.isFaintness || hSceneManager.PersonalPhase[_main] > 1)
				{
					return false;
				}
				break;
			case 1:
				if (hSceneManager.isForce || ctrlFlag.nowAnimationInfo.nInitiativeFemale != 0 || ctrlFlag.isFaintness || hSceneManager.PersonalPhase[_main] < 2)
				{
					return false;
				}
				break;
			case 2:
				if (!hSceneManager.isForce || ctrlFlag.isFaintness)
				{
					return false;
				}
				break;
			case 3:
				if (ctrlFlag.isFaintness || ctrlFlag.nowAnimationInfo.nInitiativeFemale == 0)
				{
					return false;
				}
				break;
			case 4:
				if (!ctrlFlag.isFaintness || voiceFlag.sleep)
				{
					return false;
				}
				break;
			case 5:
				if (!ctrlFlag.isFaintness || !voiceFlag.sleep)
				{
					return false;
				}
				break;
			case 6:
				if (hSceneManager.GetFlaverSkillLevel(2) >= IngoLimit)
				{
					return false;
				}
				break;
			case 7:
				if (hSceneManager.GetFlaverSkillLevel(2) < IngoLimit)
				{
					return false;
				}
				break;
			}
		}
		return true;
	}

	public void PlaySoundETC(string _animName, int _mode, ChaControl _female, int _main, bool _iya = false)
	{
		isPlays[_main] = true;
		int num = -1;
		if (Singleton<HSceneManager>.Instance.EventKind == HSceneManager.HEvent.Yobai && ctrlFlag.isFaintness)
		{
			isPlays[_main] = false;
			return;
		}
		VoiceAnimationPlayInfo animation = playAnimation.GetAnimation(Animator.StringToHash(_animName));
		if (_mode > 2)
		{
			isPlays[_main] = false;
			return;
		}
		HSceneFlagCtrl.VoiceFlag voiceFlag = VoiceFlag();
		int no;
		string nameFile;
		string pathAsset;
		float pitch;
		Transform voiceTrans;
		switch (_mode)
		{
		case 0:
		{
			num = (ctrlFlag.isFaintness ? 2 : ((hSceneManager.Agent[_main].ChaControl.fileGameInfo.phase >= 2) ? 1 : 0));
			if (!dicdiclstVoiceList[_main].ContainsKey(personality[_main]) || !dicdiclstVoiceList[_main][3].ContainsKey(5) || !dicdiclstVoiceList[_main][3][5].dicdicVoiceList.ContainsKey(num))
			{
				isPlays[_main] = false;
				return;
			}
			VoiceListInfo voiceListInfo = dicdiclstVoiceList[_main][3][5].dicdicVoiceList[num];
			Manager.Voice instance = Singleton<Manager.Voice>.Instance;
			no = personality[_main];
			pathAsset = voiceListInfo.pathAsset;
			nameFile = voiceListInfo.nameFile;
			pitch = voicePitch[_main];
			voiceTrans = voiceFlag.voiceTrs[_main];
			instance.OnecePlayChara(no, pathAsset, nameFile, pitch, 0f, 0f, isAsync: true, voiceTrans);
			if (!voiceFlag.lstUseAsset.Contains(voiceListInfo.pathAsset))
			{
				voiceFlag.lstUseAsset.Add(voiceListInfo.pathAsset);
			}
			nowVoices[_main].voiceInfo = voiceListInfo;
			nowVoices[_main].state = VoiceKind.voice;
			nowVoices[_main].animVoice = _animName;
			nowVoices[_main].notOverWrite = nowVoices[_main].voiceInfo.notOverWrite;
			nowVoices[_main].arrayVoice = num;
			nowVoices[_main].Face = nowVoices[_main].voiceInfo.lstHitFace[UnityEngine.Random.Range(0, nowVoices[_main].voiceInfo.lstHitFace.Count)];
			nowVoices[_main].voiceInfo.isPlay = true;
			animation.isPlays[_main] = true;
			SetFace(nowVoices[_main].Face, _female, _main);
			voiceFlag.dialog = false;
			return;
		}
		case 1:
			num = ((!_iya) ? 15 : 13);
			break;
		default:
			num = ((!_iya) ? 16 : 14);
			break;
		}
		Dictionary<int, VoiceListInfo> dicShortBreathLists = ShortBreathLists[_main].dicShortBreathLists;
		if (!dicShortBreathLists.ContainsKey(num))
		{
			isPlays[_main] = false;
			return;
		}
		Manager.Voice instance2 = Singleton<Manager.Voice>.Instance;
		no = personality[_main];
		nameFile = dicShortBreathLists[num].pathAsset;
		pathAsset = dicShortBreathLists[num].nameFile;
		pitch = voicePitch[_main];
		voiceTrans = voiceFlag.voiceTrs[_main];
		instance2.OnecePlayChara(no, nameFile, pathAsset, pitch, 0f, 0f, isAsync: true, voiceTrans);
		if (!voiceFlag.lstUseAsset.Contains(dicShortBreathLists[num].pathAsset))
		{
			voiceFlag.lstUseAsset.Add(dicShortBreathLists[num].pathAsset);
		}
		nowVoices[_main].shortInfo = dicShortBreathLists[num];
		nowVoices[_main].state = VoiceKind.breathShort;
		nowVoices[_main].notOverWrite = nowVoices[_main].shortInfo.notOverWrite;
		nowVoices[_main].arrayShort = num;
		nowVoices[_main].Face = nowVoices[_main].shortInfo.lstHitFace[UnityEngine.Random.Range(0, nowVoices[_main].shortInfo.lstHitFace.Count)];
		voiceFlag.playShorts[_main] = -1;
		SetFace(nowVoices[_main].Face, _female, _main);
	}

	private bool isVoiceCheck(int id)
	{
		if (MapHID < 0)
		{
			if (ctrlFlag.voice.voiceTrs[id] != null)
			{
				return Singleton<Manager.Voice>.Instance.IsVoiceCheck(ctrlFlag.voice.voiceTrs[id]);
			}
			return false;
		}
		if (ctrlFlag.MapHvoices.ContainsKey(MapHID) && ctrlFlag.MapHvoices[MapHID].voiceTrs[id] != null)
		{
			return Singleton<Manager.Voice>.Instance.IsVoiceCheck(ctrlFlag.MapHvoices[MapHID].voiceTrs[id]);
		}
		return false;
	}

	private HSceneFlagCtrl.VoiceFlag VoiceFlag()
	{
		if (MapHID < 0)
		{
			return ctrlFlag.voice;
		}
		if (ctrlFlag.MapHvoices.ContainsKey(MapHID))
		{
			return ctrlFlag.MapHvoices[MapHID];
		}
		return null;
	}

	private bool SetFace(FaceInfo _face, ChaControl _female, int _main)
	{
		FBSCtrlEyes eyesCtrl = _female.eyesCtrl;
		if (eyesCtrl != null)
		{
			blendEyes[_main].Start(eyesCtrl.OpenMax, _face.openEye, 0.3f);
		}
		FBSCtrlMouth mouthCtrl = _female.mouthCtrl;
		if (mouthCtrl != null)
		{
			blendMouths[_main].Start(mouthCtrl.OpenMin, _face.openMouthMin, 0.3f);
			blendMouthMaxs[_main].Start(mouthCtrl.OpenMax, _face.openMouthMax, 0.3f);
		}
		_female.ChangeEyebrowPtn(_face.eyeBlow);
		_female.ChangeEyesPtn(_face.eye);
		_female.ChangeMouthPtn(_face.mouth);
		if (_face.mouth == 10 || _face.mouth == 13)
		{
			_female.DisableShapeMouth(disable: true);
		}
		else
		{
			_female.DisableShapeMouth(disable: false);
		}
		_female.ChangeTearsRate(_face.tear);
		_female.ChangeHohoAkaRate(_face.cheek);
		_female.HideEyeHighlight(!_face.highlight);
		_female.ChangeEyesBlinkFlag(_face.blink);
		return true;
	}

	public IEnumerator Init(int _personality, float _pitch, Actor _param, int _personality_sub = 0, float _pitch_sub = 0f, Actor _param_sub = null, int merchant = -1, bool musterbation = false, bool les = false)
	{
		personality[0] = _personality;
		voicePitch[0] = _pitch;
		personality[1] = _personality_sub;
		voicePitch[1] = _pitch_sub;
		param = _param;
		param_sub = _param_sub;
		nowVoices[0] = new Voice();
		nowVoices[1] = new Voice();
		dicdiclstVoiceList[0] = new Dictionary<int, Dictionary<int, VoiceList>>();
		dicdiclstVoiceList[1] = new Dictionary<int, Dictionary<int, VoiceList>>();
		ShortBreathLists[0] = new ShortVoiceList();
		ShortBreathLists[1] = new ShortVoiceList();
		shortBreathPtns[0] = new ShortBreathPtn();
		shortBreathPtns[1] = new ShortBreathPtn();
		shortBreathAddPtns[0] = new ShortBreathPtn();
		shortBreathAddPtns[1] = new ShortBreathPtn();
		breathUseLists[0] = new BreathList();
		breathUseLists[1] = new BreathList();
		lstLoadVoicePtn[0] = new Dictionary<int, Dictionary<int, List<VoicePtn>>>();
		lstLoadVoicePtn[1] = new Dictionary<int, Dictionary<int, List<VoicePtn>>>();
		lstLoadStartVoicePtn[0] = new List<StartVoicePtn>();
		lstLoadStartVoicePtn[1] = new List<StartVoicePtn>();
		blendEyes[0] = new GlobalMethod.FloatBlend();
		blendEyes[1] = new GlobalMethod.FloatBlend();
		blendMouths[0] = new GlobalMethod.FloatBlend();
		blendMouths[1] = new GlobalMethod.FloatBlend();
		blendMouthMaxs[0] = new GlobalMethod.FloatBlend();
		blendMouthMaxs[1] = new GlobalMethod.FloatBlend();
		dicdicVoicePlayAnimation = new Dictionary<int, Dictionary<int, VoiceAnimationPlay>>();
		for (int i = 0; i < 2; i++)
		{
			dicBreathPtns[i] = dicBreathPtns.New();
			dicBreathUsePtns[i] = dicBreathUsePtns.New();
			dicShortBreathUsePtns[i] = dicShortBreathUsePtns.New();
			dicBreathAddPtns[i] = dicBreathAddPtns.New();
		}
		hSceneManager = Singleton<HSceneManager>.Instance;
		for (int j = 0; j < breathLists.Length; j++)
		{
			breathLists[j] = new BreathList();
		}
		lstBreathAbnames.Clear();
		lstBreathAbnames = CommonLib.GetAssetBundleNameListFromPath(hSceneManager.strAssetBreathListFolder);
		masturbation = musterbation;
		this.les = les;
		yield return null;
		yield return LoadBreathList(hSceneManager.strAssetBreathListFolder, musterbation, les);
		for (int k = 0; k < breathLists.Length; k++)
		{
			breathLists[k].DebugListSet();
		}
		yield return LoadVoiceList(hSceneManager.strAssetVoiceListFolder, musterbation, les);
		yield return LoadShortBreathList(hSceneManager.strAssetVoiceListFolder, musterbation);
		for (int nLoopCnt = 0; nLoopCnt < 2; nLoopCnt++)
		{
			if (nLoopCnt == 0 || !(param_sub == null))
			{
				yield return LoadStartVoicePtnList(merchant: personality[nLoopCnt] == -90, charNum: nLoopCnt, _pathAssetFolder: hSceneManager.strAssetVoiceListFolder);
			}
		}
		yield return null;
	}

	private IEnumerator LoadBreathList(string _pathAssetFolder, bool musterbation = false, bool les = false)
	{
		for (int i = 0; i < 2; i++)
		{
			if (i == 0 || !(param_sub == null))
			{
				if (!musterbation && !les)
				{
					LoadBreath(personality[i], i, _pathAssetFolder);
				}
				else
				{
					LoadMapHBreath(personality[i], i, _pathAssetFolder);
				}
			}
		}
		yield return null;
		for (int j = 0; j < 2; j++)
		{
			if (j == 0 || !(param_sub == null))
			{
				if (!musterbation && !les)
				{
					LoadBreathPtn(j, 0, 0);
					LoadBreathPtn(j, 1, 0);
					LoadBreathPtn(j, 2, 0);
					LoadBreathPtn(j, 3, 0);
					LoadBreathPtn(j, 3, 3);
					LoadBreathPtn(j, 3, 5);
					LoadBreathPtn(j, 4, 0);
					LoadBreathPtn(j, 5, 3);
					LoadBreathAddPtn(j, 0, 0);
					LoadBreathAddPtn(j, 1, 0);
					LoadBreathAddPtn(j, 2, 0);
					LoadBreathAddPtn(j, 3, 0);
					LoadBreathAddPtn(j, 3, 3);
					LoadBreathAddPtn(j, 3, 5);
					LoadBreathAddPtn(j, 4, 0);
					LoadBreathAddPtn(j, 5, 3);
				}
				else if (!les)
				{
					LoadBreathPtn(j, 3, 0);
					LoadBreathPtn(j, 3, 3);
					LoadBreathPtn(j, 3, 5);
					LoadBreathAddPtn(j, 3, 0);
					LoadBreathAddPtn(j, 3, 3);
					LoadBreathAddPtn(j, 3, 5);
				}
				else
				{
					LoadBreathPtn(j, 4, 0);
					LoadBreathAddPtn(j, 4, 0);
				}
			}
		}
		yield return null;
	}

	private bool LoadBreath(int _personality, int _main, string _pathAssetFolder)
	{
		sbLoadFile.Clear();
		sbLoadFile.AppendFormat("HBreath_{0:00}", (_personality == -90) ? 5 : _personality);
		StartCoroutine(LoadBreathBase(GlobalMethod.LoadAllListText(lstBreathAbnames, sbLoadFile.ToString()), breathLists[_main]));
		return true;
	}

	private bool LoadMapHBreath(int _personality, int _main, string _pathAssetFolder)
	{
		sbLoadFile.Clear();
		sbLoadFile.AppendFormat("HBreath_{0:00}", (_personality == -90) ? 5 : _personality);
		Observable.FromCoroutine(() => LoadBreathBase(GlobalMethod.LoadAllListText(lstBreathAbnames, sbLoadFile.ToString()), breathLists[_main])).Subscribe();
		return true;
	}

	private IEnumerator LoadBreathBase(string _str, BreathList _breath)
	{
		if (_str.IsNullOrEmpty())
		{
			yield break;
		}
		GlobalMethod.GetListString(_str, out var aastr);
		int nY = aastr.Length;
		Dictionary<int, VoiceListInfo> dicBreath = _breath.lstVoiceList;
		for (int i = 0; i < nY; i++)
		{
			int key = int.Parse(aastr[i][0]);
			if (!dicBreath.ContainsKey(key))
			{
				dicBreath.Add(key, new VoiceListInfo());
			}
			VoiceListInfo voiceListInfo = dicBreath[key];
			voiceListInfo.pathAsset = aastr[i][1];
			voiceListInfo.nameFile = aastr[i][2];
			int.TryParse(aastr[i][3], out voiceListInfo.group);
			voiceListInfo.notOverWrite = aastr[i][4] == "1";
			voiceListInfo.urine = aastr[i][5] == "1";
			voiceListInfo.lstNotHitFace.Clear();
			for (int j = 0; j < 3; j++)
			{
				LoadBreathFace(aastr, i, j * 10 + 6, voiceListInfo.lstNotHitFace);
			}
			voiceListInfo.lstHitFace.Clear();
			LoadBreathFace(aastr, i, 36, voiceListInfo.lstHitFace);
		}
		yield return null;
	}

	private void LoadBreathFace(string[][] _aastr, int _line, int _idx, List<FaceInfo> _lst)
	{
		FaceInfo faceInfo = new FaceInfo();
		if (float.TryParse(_aastr[_line][_idx++], out var result) && !(result < 0f))
		{
			faceInfo.openEye = result;
			faceInfo.openMouthMin = float.Parse(_aastr[_line][_idx++]);
			faceInfo.openMouthMax = float.Parse(_aastr[_line][_idx++]);
			faceInfo.eyeBlow = int.Parse(_aastr[_line][_idx++]);
			faceInfo.eye = int.Parse(_aastr[_line][_idx++]);
			faceInfo.mouth = int.Parse(_aastr[_line][_idx++]);
			faceInfo.tear = float.Parse(_aastr[_line][_idx++]);
			faceInfo.cheek = float.Parse(_aastr[_line][_idx++]);
			faceInfo.highlight = _aastr[_line][_idx++] == "1";
			faceInfo.blink = _aastr[_line][_idx++] == "1";
			_lst.Add(faceInfo);
		}
	}

	private bool LoadBreathPtn(int personal, int mode, int kind)
	{
		sbLoadFile.Clear();
		sbLoadFile.AppendFormat("HBreathPattern_{0:00}_{1:00}_{2:00}", personal, mode, kind);
		string text = GlobalMethod.LoadAllListText(lstBreathAbnames, sbLoadFile.ToString());
		if (text.IsNullOrEmpty())
		{
			return false;
		}
		GlobalMethod.GetListString(text, out var data);
		int num = data.Length;
		int num2 = ((num != 0) ? data[0].Length : 0);
		if (!dicBreathPtns[personal].ContainsKey(mode))
		{
			dicBreathPtns[personal][mode] = dicBreathPtns[personal].New();
		}
		if (!dicBreathPtns[personal][mode].ContainsKey(kind))
		{
			dicBreathPtns[personal][mode][kind] = dicBreathPtns[personal][mode].New();
		}
		ValueDictionary<int, int, string, BreathPtn> valueDictionary = dicBreathPtns[personal][mode][kind];
		for (int i = 0; i < num; i++)
		{
			int key = int.Parse(data[i][0]);
			if (!valueDictionary.ContainsKey(key))
			{
				valueDictionary[key] = valueDictionary.New();
			}
			int num3 = int.Parse(data[i][1]);
			if (!valueDictionary[key].ContainsKey(num3))
			{
				valueDictionary[key][num3] = valueDictionary[key].New();
			}
			ValueDictionary<string, BreathPtn> valueDictionary2 = valueDictionary[key][num3];
			string text2 = data[i][2];
			if (!valueDictionary2.ContainsKey(text2))
			{
				valueDictionary2.Add(text2, new BreathPtn());
			}
			BreathPtn breathPtn = valueDictionary2[text2];
			breathPtn.level = num3;
			breathPtn.anim = text2;
			breathPtn.onlyOne = data[i][3] == "1";
			breathPtn.isPlay = false;
			breathPtn.force = data[i][4] == "1";
			breathPtn.timeChangeFaceMin = float.Parse(data[i][5]);
			breathPtn.timeChangeFaceMax = float.Parse(data[i][6]);
			breathPtn.lstInfo.Clear();
			for (int j = 7; j < num2; j += 3)
			{
				string text3 = data[i][j];
				if (text3.IsNullOrEmpty())
				{
					break;
				}
				BreathVoicePtnInfo breathVoicePtnInfo = new BreathVoicePtnInfo();
				string[] array = text3.Split(',');
				int result = 0;
				for (int k = 0; k < array.Length; k++)
				{
					if (int.TryParse(array[k], out result))
					{
						breathVoicePtnInfo.lstConditions.Add(result);
					}
				}
				array = data[i][j + 1].Split(',');
				for (int l = 0; l < array.Length; l++)
				{
					if (int.TryParse(array[l], out result))
					{
						breathVoicePtnInfo.lstAnimeID.Add(result);
					}
				}
				array = data[i][j + 2].Split(',');
				for (int m = 0; m < array.Length; m++)
				{
					if (int.TryParse(array[m], out result))
					{
						breathVoicePtnInfo.lstVoice.Add(result);
					}
				}
				breathPtn.lstInfo.Add(breathVoicePtnInfo);
			}
		}
		return true;
	}

	private bool LoadBreathAddPtn(int personal, int mode, int kind)
	{
		for (int i = 0; i < 5; i++)
		{
			sbLoadFile.Clear();
			sbLoadFile.AppendFormat("HBreathPattern_{0:00}_{1:00}_{2:00}_{3:00}", personal, mode, kind, i);
			string text = GlobalMethod.LoadAllListText(lstBreathAbnames, sbLoadFile.ToString());
			if (text.IsNullOrEmpty())
			{
				continue;
			}
			GlobalMethod.GetListString(text, out var data);
			int num = data.Length;
			int num2 = ((num != 0) ? data[0].Length : 0);
			if (!dicBreathAddPtns[personal].ContainsKey(mode))
			{
				dicBreathAddPtns[personal][mode] = dicBreathAddPtns[personal].New();
			}
			if (!dicBreathAddPtns[personal][mode].ContainsKey(kind))
			{
				dicBreathAddPtns[personal][mode][kind] = dicBreathAddPtns[personal][mode].New();
			}
			ValueDictionary<int, int, string, BreathPtn> valueDictionary = dicBreathAddPtns[personal][mode][kind];
			for (int j = 0; j < num; j++)
			{
				int key = int.Parse(data[j][0]);
				if (!valueDictionary.ContainsKey(key))
				{
					valueDictionary[key] = valueDictionary.New();
				}
				int num3 = int.Parse(data[j][1]);
				if (!valueDictionary[key].ContainsKey(num3))
				{
					valueDictionary[key][num3] = valueDictionary[key].New();
				}
				ValueDictionary<string, BreathPtn> valueDictionary2 = valueDictionary[key][num3];
				string text2 = data[j][2];
				if (!valueDictionary2.ContainsKey(text2))
				{
					valueDictionary2.Add(text2, new BreathPtn());
				}
				BreathPtn breathPtn = valueDictionary2[text2];
				breathPtn.level = num3;
				breathPtn.anim = text2;
				breathPtn.onlyOne = data[j][3] == "1";
				breathPtn.isPlay = false;
				breathPtn.force = data[j][4] == "1";
				breathPtn.timeChangeFaceMin = float.Parse(data[j][5]);
				breathPtn.timeChangeFaceMax = float.Parse(data[j][6]);
				for (int k = 7; k < num2; k += 3)
				{
					string text3 = data[j][k];
					if (text3 == string.Empty)
					{
						break;
					}
					BreathVoicePtnInfo bvi = new BreathVoicePtnInfo();
					string[] array = text3.Split(',');
					int result = 0;
					for (int l = 0; l < array.Length; l++)
					{
						if (int.TryParse(array[l], out result))
						{
							bvi.lstConditions.Add(result);
						}
					}
					array = data[j][k + 1].Split(',');
					for (int m = 0; m < array.Length; m++)
					{
						if (int.TryParse(array[m], out result))
						{
							bvi.lstAnimeID.Add(result);
						}
					}
					array = data[j][k + 2].Split(',');
					for (int n = 0; n < array.Length; n++)
					{
						if (int.TryParse(array[n], out result))
						{
							bvi.lstVoice.Add(result);
						}
					}
					BreathVoicePtnInfo breathVoicePtnInfo = breathPtn.lstInfo.Find((BreathVoicePtnInfo f) => f.lstConditions.SequenceEqual(bvi.lstConditions) && f.lstAnimeID.SequenceEqual(bvi.lstAnimeID));
					if (breathVoicePtnInfo == null)
					{
						breathPtn.lstInfo.Add(bvi);
					}
					else
					{
						breathVoicePtnInfo.lstVoice = new List<int>(bvi.lstVoice);
					}
				}
			}
		}
		LoadBreathPtnComposition(personal, mode, kind);
		return true;
	}

	private bool LoadBreathPtnComposition(int personal, int mode, int kind)
	{
		if (!dicBreathPtns.ContainsKey(personal))
		{
			return false;
		}
		if (!dicBreathPtns[personal].ContainsKey(mode))
		{
			return false;
		}
		if (!dicBreathPtns[personal][mode].ContainsKey(kind))
		{
			return false;
		}
		ValueDictionary<int, int, string, BreathPtn> valueDictionary = dicBreathPtns[personal][mode][kind];
		if (!dicBreathAddPtns.ContainsKey(personal))
		{
			return false;
		}
		if (!dicBreathAddPtns[personal].ContainsKey(mode))
		{
			return false;
		}
		if (!dicBreathAddPtns[personal][mode].ContainsKey(kind))
		{
			return false;
		}
		ValueDictionary<int, int, string, BreathPtn> valueDictionary2 = dicBreathAddPtns[personal][mode][kind];
		foreach (KeyValuePair<int, ValueDictionary<int, string, BreathPtn>> item in valueDictionary)
		{
			if (!valueDictionary2.ContainsKey(item.Key))
			{
				continue;
			}
			foreach (KeyValuePair<int, ValueDictionary<string, BreathPtn>> item2 in item.Value)
			{
				if (!valueDictionary2[item.Key].ContainsKey(item2.Key))
				{
					continue;
				}
				foreach (KeyValuePair<string, BreathPtn> item3 in item2.Value)
				{
					if (valueDictionary2[item.Key][item2.Key].ContainsKey(item3.Key))
					{
						item3.Value.lstInfo.AddRange(valueDictionary2[item.Key][item2.Key][item3.Key].lstInfo);
					}
				}
			}
		}
		return true;
	}

	private IEnumerator LoadVoiceList(string _pathAssetFolder, bool musterbation = false, bool les = false)
	{
		for (int nLoopCharCnt = 0; nLoopCharCnt < 2; nLoopCharCnt++)
		{
			if (nLoopCharCnt != 0 && param_sub == null)
			{
				continue;
			}
			if (!musterbation && !les)
			{
				for (int i = 0; i < 5; i++)
				{
					int kind = i;
					LoadVoice(personality[nLoopCharCnt], 0, kind, _pathAssetFolder, nLoopCharCnt);
				}
				for (int j = 0; j < 5; j++)
				{
					int kind2 = j;
					LoadVoice(personality[nLoopCharCnt], 1, kind2, _pathAssetFolder, nLoopCharCnt);
				}
				for (int k = 0; k < 6; k++)
				{
					int kind3 = k;
					LoadVoice(personality[nLoopCharCnt], 2, kind3, _pathAssetFolder, nLoopCharCnt);
				}
				yield return null;
				for (int l = 0; l < 5; l++)
				{
					int kind4 = l;
					LoadVoice(personality[nLoopCharCnt], 3, kind4, _pathAssetFolder, nLoopCharCnt);
				}
				LoadVoice(personality[nLoopCharCnt], 4, 0, _pathAssetFolder, nLoopCharCnt);
				LoadVoice(personality[nLoopCharCnt], 5, 0, _pathAssetFolder, nLoopCharCnt);
				LoadVoice(personality[nLoopCharCnt], 6, 0, _pathAssetFolder, nLoopCharCnt);
				LoadVoice(personality[nLoopCharCnt], 7, 0, _pathAssetFolder, nLoopCharCnt);
			}
			else if (!les)
			{
				for (int m = 0; m < 5; m++)
				{
					int kind5 = m;
					LoadVoice(personality[nLoopCharCnt], 3, kind5, _pathAssetFolder, nLoopCharCnt);
				}
				LoadVoice(personality[nLoopCharCnt], 6, 0, _pathAssetFolder, nLoopCharCnt);
			}
			else
			{
				LoadVoice(personality[nLoopCharCnt], 4, 0, _pathAssetFolder, nLoopCharCnt);
				LoadVoice(personality[nLoopCharCnt], 6, 0, _pathAssetFolder, nLoopCharCnt);
			}
		}
		yield return null;
		for (int n = 0; n < 2; n++)
		{
			if (n == 0 || !(param_sub == null))
			{
				bool merchant = personality[n] == -90;
				if (!musterbation && !les)
				{
					LoadVoicePtn(0, 0, n, _pathAssetFolder, merchant);
					LoadVoicePtn(1, 0, n, _pathAssetFolder, merchant);
					LoadVoicePtn(2, 0, n, _pathAssetFolder, merchant);
					LoadVoicePtn(3, 0, n, _pathAssetFolder, merchant);
					LoadVoicePtn(3, 1, n, _pathAssetFolder, merchant);
					LoadVoicePtn(3, 2, n, _pathAssetFolder, merchant);
					LoadVoicePtn(4, 0, n, _pathAssetFolder, merchant);
					LoadVoicePtn(5, 0, n, _pathAssetFolder, merchant);
				}
				else if (!les)
				{
					LoadVoicePtn(3, 0, n, _pathAssetFolder, merchant);
					LoadVoicePtn(3, 1, n, _pathAssetFolder, merchant);
					LoadVoicePtn(3, 2, n, _pathAssetFolder, merchant);
				}
				else
				{
					LoadVoicePtn(4, 0, n, _pathAssetFolder, merchant);
				}
			}
		}
		yield return null;
	}

	private bool LoadVoice(int _personality, int _mode, int _kind, string _pathAssetFolder, int _famale)
	{
		sbLoadFile.Clear();
		sbLoadFile.AppendFormat("HVoiceFace_{0:00}_{1:00}_{2:00}", _personality, _mode, _kind);
		lst.Clear();
		GlobalMethod.LoadAllListTextFromList(_pathAssetFolder, sbLoadFile.ToString(), ref lst);
		foreach (string item in lst)
		{
			if (item == string.Empty)
			{
				continue;
			}
			GlobalMethod.GetListString(item, out var data);
			int num = data.Length;
			int num2 = 0;
			int result = 0;
			if (!int.TryParse(data[num2][0], out result))
			{
				continue;
			}
			num2++;
			if (result != 0 && num2 + result == num)
			{
				VoiceList voiceList = null;
				if (!dicdiclstVoiceList[_famale].ContainsKey(_mode))
				{
					dicdiclstVoiceList[_famale].Add(_mode, new Dictionary<int, VoiceList>());
					dicdiclstVoiceList[_famale][_mode].Add(_kind, new VoiceList());
				}
				else if (!dicdiclstVoiceList[_famale][_mode].ContainsKey(_kind))
				{
					dicdiclstVoiceList[_famale][_mode].Add(_kind, new VoiceList());
				}
				voiceList = dicdiclstVoiceList[_famale][_mode][_kind];
				LoadVoiceFace(data, result, ref num2, voiceList.dicdicVoiceList);
				voiceList.total += result;
			}
		}
		return true;
	}

	private void LoadVoiceFace(string[][] aastr, int endY, ref int line, Dictionary<int, VoiceListInfo> dic)
	{
		int[] array = new int[2] { -1, -1 };
		while (line - 1 < endY)
		{
			int num = aastr[line].Length;
			if (!int.TryParse(aastr[line][0], out array[0]))
			{
				array[0] = array[1] + 1;
			}
			VoiceListInfo voiceListInfo = new VoiceListInfo();
			voiceListInfo.pathAsset = aastr[line][1];
			voiceListInfo.nameFile = aastr[line][2];
			if (num < 4)
			{
				line++;
				array[1]++;
				continue;
			}
			voiceListInfo.notOverWrite = aastr[line][3] == "1";
			if ((num - 4) % 32 != 0)
			{
				line++;
				array[1]++;
				continue;
			}
			for (int i = 4; i < num; i += 32)
			{
				int num2 = 0;
				FaceInfo faceInfo = new FaceInfo();
				if (aastr[line][i + num2].IsNullOrEmpty())
				{
					break;
				}
				float num3 = float.Parse(aastr[line][i + num2++]);
				if (num3 < 0f)
				{
					break;
				}
				faceInfo.openEye = num3;
				faceInfo.openMouthMin = float.Parse(aastr[line][i + num2++]);
				faceInfo.openMouthMax = float.Parse(aastr[line][i + num2++]);
				faceInfo.eyeBlow = int.Parse(aastr[line][i + num2++]);
				faceInfo.eye = int.Parse(aastr[line][i + num2++]);
				faceInfo.mouth = int.Parse(aastr[line][i + num2++]);
				faceInfo.tear = float.Parse(aastr[line][i + num2++]);
				faceInfo.cheek = float.Parse(aastr[line][i + num2++]);
				faceInfo.highlight = aastr[line][i + num2++] == "1";
				faceInfo.blink = aastr[line][i + num2++] == "1";
				faceInfo.behaviorNeckLine = int.Parse(aastr[line][i + num2++]);
				faceInfo.behaviorEyeLine = int.Parse(aastr[line][i + num2++]);
				faceInfo.targetNeckLine = int.Parse(aastr[line][i + num2++]);
				Vector3 zero = Vector3.zero;
				if (faceInfo.targetNeckLine == 7)
				{
					for (int j = 0; j < 2; j++)
					{
						zero = Vector3.zero;
						if (!float.TryParse(aastr[line][i + num2++], out zero.x))
						{
							zero.x = 0f;
						}
						if (!float.TryParse(aastr[line][i + num2++], out zero.y))
						{
							zero.y = 0f;
						}
						if (!float.TryParse(aastr[line][i + num2++], out zero.z))
						{
							zero.z = 0f;
						}
						faceInfo.NeckRot[j] = zero;
					}
					for (int k = 0; k < 2; k++)
					{
						zero = Vector3.zero;
						if (!float.TryParse(aastr[line][i + num2++], out zero.x))
						{
							zero.x = 0f;
						}
						if (!float.TryParse(aastr[line][i + num2++], out zero.y))
						{
							zero.y = 0f;
						}
						if (!float.TryParse(aastr[line][i + num2++], out zero.z))
						{
							zero.z = 0f;
						}
						faceInfo.HeadRot[k] = zero;
					}
				}
				else
				{
					num2 += 12;
				}
				faceInfo.targetEyeLine = int.Parse(aastr[line][i + num2++]);
				if (faceInfo.targetEyeLine == 7)
				{
					for (int l = 0; l < 2; l++)
					{
						zero = Vector3.zero;
						if (!float.TryParse(aastr[line][i + num2++], out zero.x))
						{
							zero.x = 0f;
						}
						if (!float.TryParse(aastr[line][i + num2++], out zero.y))
						{
							zero.y = 0f;
						}
						if (!float.TryParse(aastr[line][i + num2++], out zero.z))
						{
							zero.z = 0f;
						}
						faceInfo.EyeRot[l] = zero;
					}
				}
				else
				{
					num2 += 6;
				}
				voiceListInfo.lstHitFace.Add(faceInfo);
			}
			if (!dic.ContainsKey(array[0]))
			{
				dic.Add(array[0], voiceListInfo);
			}
			else
			{
				dic[array[0]] = voiceListInfo;
			}
			line++;
			array[1]++;
		}
	}

	private bool LoadVoicePtn(int taii, int kind, int charNum, string _pathAssetFolder, bool merchant)
	{
		sbLoadFile.Clear();
		if (!merchant)
		{
			sbLoadFile.AppendFormat("HVoicePattern_{0:00}_{1:00}", taii, kind);
		}
		else
		{
			sbLoadFile.AppendFormat("HVoicePattern_-90_{0:00}_{1:00}", taii, kind);
		}
		if (!lstLoadVoicePtn[charNum].ContainsKey(taii))
		{
			lstLoadVoicePtn[charNum].Add(taii, new Dictionary<int, List<VoicePtn>>());
		}
		if (!lstLoadVoicePtn[charNum][taii].ContainsKey(kind))
		{
			lstLoadVoicePtn[charNum][taii].Add(kind, new List<VoicePtn>());
		}
		lst.Clear();
		GlobalMethod.LoadAllListTextFromList(_pathAssetFolder, sbLoadFile.ToString(), ref lst);
		CheckVoicePtn ptn = default(CheckVoicePtn);
		for (int i = 0; i < lst.Count; i++)
		{
			GlobalMethod.GetListString(lst[i], out var data);
			int num = 0;
			int num2 = data.Length;
			while (num < num2)
			{
				int num3 = data[num].Length;
				int result = -1;
				if (!int.TryParse(data[num][0], out result))
				{
					num++;
					continue;
				}
				ptn.condition = result;
				if (!int.TryParse(data[num][1], out result))
				{
					result = -1;
				}
				ptn.startKind = result;
				if (!int.TryParse(data[num][2], out result))
				{
					result = 0;
				}
				ptn.howTalk = result;
				ptn.LookFlag = data[num][3] == "0";
				ptn.anim = data[num][4];
				VoicePtn voicePtn = CheckPtn(lstLoadVoicePtn[charNum][taii][kind], ptn);
				if (voicePtn == null)
				{
					lstLoadVoicePtn[charNum][taii][kind].Add(new VoicePtn());
					voicePtn = lstLoadVoicePtn[charNum][taii][kind][lstLoadVoicePtn[charNum][taii][kind].Count - 1];
				}
				else
				{
					voicePtn.lstInfo.Clear();
				}
				voicePtn.condition = ptn.condition;
				voicePtn.startKind = ptn.startKind;
				voicePtn.howTalk = ptn.howTalk;
				voicePtn.LookFlag = ptn.LookFlag;
				voicePtn.anim = ptn.anim;
				int num4 = 5;
				while (num4 < num3)
				{
					VoicePtnInfo voicePtnInfo = new VoicePtnInfo();
					if (data[num][num4].IsNullOrEmpty())
					{
						break;
					}
					string[] array = data[num][num4++].Split(',');
					for (int j = 0; j < array.Length; j++)
					{
						voicePtnInfo.lstAnimList.Add(int.Parse(array[j]));
					}
					if (data[num][num4].IsNullOrEmpty())
					{
						break;
					}
					array = data[num][num4++].Split(',');
					for (int k = 0; k < array.Length; k++)
					{
						voicePtnInfo.lstPlayConditions.Add(int.Parse(array[k]));
					}
					if (data[num][num4].IsNullOrEmpty())
					{
						break;
					}
					voicePtnInfo.loadListmode = int.Parse(data[num][num4++]);
					if (data[num][num4].IsNullOrEmpty())
					{
						break;
					}
					voicePtnInfo.loadListKind = int.Parse(data[num][num4++]);
					if (data[num][num4].IsNullOrEmpty())
					{
						break;
					}
					array = data[num][num4++].Split(',');
					for (int l = 0; l < array.Length; l++)
					{
						voicePtnInfo.lstVoice.Add(int.Parse(array[l]));
					}
					voicePtn.lstInfo.Add(voicePtnInfo);
				}
				num++;
			}
		}
		return true;
	}

	private VoicePtn CheckPtn(List<VoicePtn> ptns, CheckVoicePtn ptn)
	{
		VoicePtn result = null;
		VoicePtn voicePtn = null;
		for (int i = 0; i < ptns.Count; i++)
		{
			voicePtn = ptns[i];
			if (voicePtn != null && voicePtn.condition == ptn.condition && voicePtn.startKind == ptn.startKind && voicePtn.howTalk == ptn.howTalk && voicePtn.LookFlag == ptn.LookFlag && !(voicePtn.anim != ptn.anim))
			{
				result = voicePtn;
			}
		}
		return result;
	}

	private IEnumerator LoadShortBreathList(string _pathAssetFolder, bool musterbation = false)
	{
		for (int i = 0; i < 2; i++)
		{
			if (i == 0 || !(param_sub == null))
			{
				LoadShortBreath(personality[i], i);
				if (!musterbation)
				{
					LoadShortBreathPtn(i, 0, 0);
					LoadShortBreathPtn(i, 1, 0);
					LoadShortBreathPtn(i, 2, 0);
					LoadShortBreathPtn(i, 3, 0);
					LoadShortBreathPtn(i, 3, 3);
					LoadShortBreathPtn(i, 3, 5);
					LoadShortBreathPtn(i, 4, 0);
					LoadShortBreathPtn(i, 5, 3);
					LoadShortBreathAddPtn(i, 0, 0);
					LoadShortBreathAddPtn(i, 1, 0);
					LoadShortBreathAddPtn(i, 2, 0);
					LoadShortBreathAddPtn(i, 3, 0);
					LoadShortBreathAddPtn(i, 3, 3);
					LoadShortBreathAddPtn(i, 3, 5);
					LoadShortBreathAddPtn(i, 4, 0);
					LoadShortBreathAddPtn(i, 5, 3);
				}
				else
				{
					LoadShortBreathPtn(i, 3, 0);
					LoadShortBreathPtn(i, 3, 3);
					LoadShortBreathPtn(i, 3, 5);
					LoadShortBreathAddPtn(i, 3, 0);
					LoadShortBreathAddPtn(i, 3, 3);
					LoadShortBreathAddPtn(i, 3, 5);
				}
			}
		}
		yield return null;
	}

	private bool LoadShortBreath(int _personality, int _main)
	{
		sbLoadFile.Clear();
		sbLoadFile.AppendFormat("HShort_breath_{0:00}", _personality);
		string text = GlobalMethod.LoadAllListText(lstBreathAbnames, sbLoadFile.ToString());
		if (text.IsNullOrEmpty())
		{
			return false;
		}
		GlobalMethod.GetListString(text, out var data);
		int num = data.Length;
		int num2 = ((num != 0) ? data[0].Length : 0);
		for (int i = 0; i < num; i++)
		{
			int result = 0;
			int num3 = 0;
			string s = data[i][num3++];
			if (!int.TryParse(s, out result))
			{
				continue;
			}
			if (!ShortBreathLists[_main].dicShortBreathLists.ContainsKey(result))
			{
				ShortBreathLists[_main].dicShortBreathLists.Add(result, new VoiceListInfo());
			}
			VoiceListInfo voiceListInfo = ShortBreathLists[_main].dicShortBreathLists[result];
			voiceListInfo.pathAsset = data[i][num3++];
			voiceListInfo.nameFile = data[i][num3++];
			voiceListInfo.notOverWrite = data[i][num3++] == "1";
			voiceListInfo.lstHitFace.Clear();
			for (int j = num3; j < num2; j += 10)
			{
				int num4 = 0;
				FaceInfo faceInfo = new FaceInfo();
				if (!float.TryParse(data[i][j + num4++], out var result2) || result2 < 0f)
				{
					break;
				}
				faceInfo.openEye = result2;
				faceInfo.openMouthMin = float.Parse(data[i][j + num4++]);
				faceInfo.openMouthMax = float.Parse(data[i][j + num4++]);
				faceInfo.eyeBlow = int.Parse(data[i][j + num4++]);
				faceInfo.eye = int.Parse(data[i][j + num4++]);
				faceInfo.mouth = int.Parse(data[i][j + num4++]);
				faceInfo.tear = float.Parse(data[i][j + num4++]);
				faceInfo.cheek = float.Parse(data[i][j + num4++]);
				faceInfo.highlight = data[i][j + num4++] == "1";
				faceInfo.blink = data[i][j + num4++] == "1";
				voiceListInfo.lstHitFace.Add(faceInfo);
			}
		}
		return true;
	}

	private bool LoadShortBreathPtn(int _main, int _mode, int _kind)
	{
		sbLoadFile.Clear();
		sbLoadFile.AppendFormat("HShortBreathPattern_{0:00}_{1:00}_{2:00}", _main, _mode, _kind);
		string text = GlobalMethod.LoadAllListText(lstBreathAbnames, sbLoadFile.ToString());
		if (text.IsNullOrEmpty())
		{
			return false;
		}
		GlobalMethod.GetListString(text, out var data);
		int num = data.Length;
		int num2 = ((num != 0) ? data[0].Length : 0);
		ValueDictionary<int, int, int, int, List<BreathVoicePtnInfo>> dicInfo = shortBreathPtns[_main].dicInfo;
		for (int i = 0; i < num; i++)
		{
			if (!dicInfo.ContainsKey(_mode))
			{
				dicInfo[_mode] = dicInfo.New();
			}
			if (!dicInfo[_mode].ContainsKey(_kind))
			{
				dicInfo[_mode][_kind] = dicInfo[_mode].New();
			}
			int num3 = 0;
			int key = int.Parse(data[i][num3++]);
			if (!dicInfo[_mode][_kind].ContainsKey(key))
			{
				dicInfo[_mode][_kind][key] = dicInfo[_mode][_kind].New();
			}
			int key2 = int.Parse(data[i][num3++]);
			if (!dicInfo[_mode][_kind][key].ContainsKey(key2))
			{
				dicInfo[_mode][_kind][key][key2] = new List<BreathVoicePtnInfo>();
			}
			dicInfo[_mode][_kind][key][key2].Clear();
			for (int j = num3; j < num2; j += 3)
			{
				string text2 = data[i][j];
				if (text2 == string.Empty)
				{
					break;
				}
				BreathVoicePtnInfo breathVoicePtnInfo = new BreathVoicePtnInfo();
				string[] array = text2.Split(',');
				int result = 0;
				for (int k = 0; k < array.Length; k++)
				{
					if (int.TryParse(array[k], out result))
					{
						breathVoicePtnInfo.lstConditions.Add(result);
					}
				}
				array = data[i][j + 1].Split(',');
				for (int l = 0; l < array.Length; l++)
				{
					if (int.TryParse(array[l], out result))
					{
						breathVoicePtnInfo.lstAnimeID.Add(result);
					}
				}
				array = data[i][j + 2].Split(',');
				for (int m = 0; m < array.Length; m++)
				{
					if (int.TryParse(array[m], out result))
					{
						breathVoicePtnInfo.lstVoice.Add(result);
					}
				}
				dicInfo[_mode][_kind][key][key2].Add(breathVoicePtnInfo);
			}
		}
		return true;
	}

	private bool LoadShortBreathAddPtn(int _main, int _mode, int _kind)
	{
		for (int i = 0; i < 5; i++)
		{
			sbLoadFile.Clear();
			sbLoadFile.AppendFormat("HShortBreathPattern_{0:00}_{1:00}_{2:00}_{3:00}", _main, _mode, _kind, i);
			string text = GlobalMethod.LoadAllListText(lstBreathAbnames, sbLoadFile.ToString());
			if (text.IsNullOrEmpty())
			{
				continue;
			}
			GlobalMethod.GetListString(text, out var data);
			int num = data.Length;
			int num2 = ((num != 0) ? data[0].Length : 0);
			ValueDictionary<int, int, int, int, List<BreathVoicePtnInfo>> dicInfo = shortBreathAddPtns[_main].dicInfo;
			for (int j = 0; j < num; j++)
			{
				if (!dicInfo.ContainsKey(_mode))
				{
					dicInfo[_mode] = dicInfo.New();
				}
				if (!dicInfo[_mode].ContainsKey(_kind))
				{
					dicInfo[_mode][_kind] = dicInfo[_mode].New();
				}
				int num3 = 0;
				int key = int.Parse(data[j][num3++]);
				if (!dicInfo[_mode][_kind].ContainsKey(key))
				{
					dicInfo[_mode][_kind][key] = dicInfo[_mode][_kind].New();
				}
				int key2 = int.Parse(data[j][num3++]);
				if (!dicInfo[_mode][_kind][key].ContainsKey(key2))
				{
					dicInfo[_mode][_kind][key][key2] = new List<BreathVoicePtnInfo>();
				}
				for (int k = num3; k < num2; k += 3)
				{
					string text2 = data[j][k];
					if (text2 == string.Empty)
					{
						break;
					}
					BreathVoicePtnInfo bvi = new BreathVoicePtnInfo();
					string[] array = text2.Split(',');
					int result = 0;
					for (int l = 0; l < array.Length; l++)
					{
						if (int.TryParse(array[l], out result))
						{
							bvi.lstConditions.Add(result);
						}
					}
					array = data[j][k + 1].Split(',');
					for (int m = 0; m < array.Length; m++)
					{
						if (int.TryParse(array[m], out result))
						{
							bvi.lstAnimeID.Add(result);
						}
					}
					array = data[j][k + 2].Split(',');
					for (int n = 0; n < array.Length; n++)
					{
						if (int.TryParse(array[n], out result))
						{
							bvi.lstVoice.Add(result);
						}
					}
					BreathVoicePtnInfo breathVoicePtnInfo = dicInfo[_mode][_kind][key][key2].Find((BreathVoicePtnInfo f) => f.lstConditions.SequenceEqual(bvi.lstConditions) && f.lstAnimeID.SequenceEqual(bvi.lstAnimeID));
					if (breathVoicePtnInfo == null)
					{
						dicInfo[_mode][_kind][key][key2].Add(bvi);
					}
					else
					{
						breathVoicePtnInfo.lstVoice = new List<int>(bvi.lstVoice);
					}
				}
			}
		}
		LoadShortBreathPtnComposition(_main, _mode, _kind);
		return true;
	}

	private bool LoadShortBreathPtnComposition(int _main, int _mode, int _kind)
	{
		if (!shortBreathPtns[_main].dicInfo.ContainsKey(_mode))
		{
			return false;
		}
		if (!shortBreathPtns[_main].dicInfo[_mode].ContainsKey(_kind))
		{
			return false;
		}
		ValueDictionary<int, int, List<BreathVoicePtnInfo>> valueDictionary = shortBreathPtns[_main].dicInfo[_mode][_kind];
		if (!shortBreathAddPtns[_main].dicInfo.ContainsKey(_mode))
		{
			return false;
		}
		if (!shortBreathAddPtns[_main].dicInfo[_mode].ContainsKey(_kind))
		{
			return false;
		}
		ValueDictionary<int, int, List<BreathVoicePtnInfo>> valueDictionary2 = shortBreathAddPtns[_main].dicInfo[_mode][_kind];
		foreach (KeyValuePair<int, ValueDictionary<int, List<BreathVoicePtnInfo>>> item in valueDictionary)
		{
			if (!valueDictionary2.ContainsKey(item.Key))
			{
				continue;
			}
			foreach (KeyValuePair<int, List<BreathVoicePtnInfo>> item2 in item.Value)
			{
				if (valueDictionary2[item.Key].ContainsKey(item2.Key))
				{
					item2.Value.AddRange(valueDictionary2[item.Key][item2.Key]);
				}
			}
		}
		return true;
	}

	private IEnumerator LoadStartVoicePtnList(int charNum, string _pathAssetFolder, bool merchant)
	{
		sbLoadFile.Clear();
		if (!merchant)
		{
			sbLoadFile.Append("HStartVoicePattern");
		}
		else
		{
			sbLoadFile.AppendFormat("HStartVoicePattern_-90");
		}
		if (lstLoadStartVoicePtn[charNum] == null)
		{
			lstLoadStartVoicePtn[charNum] = new List<StartVoicePtn>();
		}
		lst.Clear();
		GlobalMethod.LoadAllListTextFromList(_pathAssetFolder, sbLoadFile.ToString(), ref lst);
		string[] aStr = new string[8] { "H前待機", "開始(H前待機から)", "開始(その他)", "再開始", "行為変更", "H前待機返し", "開始返し", "再開始返し" };
		for (int nLoopCnt = 0; nLoopCnt < lst.Count; nLoopCnt++)
		{
			GlobalMethod.GetListString(lst[nLoopCnt], out var aastr);
			int line = 0;
			int limitY = aastr.Length;
			while (line < limitY)
			{
				StartVoicePtn startVoicePtn = new StartVoicePtn();
				int num = aastr[line].Length;
				int result = -1;
				if (!int.TryParse(aastr[line][0], out result))
				{
					line++;
					continue;
				}
				startVoicePtn.condition = result;
				if (!int.TryParse(aastr[line][1], out result))
				{
					result = 0;
				}
				startVoicePtn.nForce = result;
				if (!int.TryParse(aastr[line][2], out result))
				{
					result = 0;
				}
				startVoicePtn.nTaii = result;
				if (!int.TryParse(aastr[line][3], out result))
				{
					result = -1;
				}
				startVoicePtn.timing = result;
				startVoicePtn.anim = aStr[result];
				int num2 = 4;
				while (num2 < num)
				{
					VoicePtnInfo voicePtnInfo = new VoicePtnInfo();
					if (aastr[line][num2].IsNullOrEmpty())
					{
						break;
					}
					string[] buf3 = aastr[line][num2++].Split(',');
					for (int i = 0; i < buf3.Length; i++)
					{
						voicePtnInfo.lstAnimList.Add(int.Parse(buf3[i]));
					}
					buf3 = aastr[line][num2++].Split(',');
					for (int j = 0; j < buf3.Length; j++)
					{
						voicePtnInfo.lstPlayConditions.Add(int.Parse(buf3[j]));
					}
					voicePtnInfo.loadListmode = int.Parse(aastr[line][num2++]);
					voicePtnInfo.loadListKind = int.Parse(aastr[line][num2++]);
					buf3 = aastr[line][num2++].Split(',');
					for (int k = 0; k < buf3.Length; k++)
					{
						voicePtnInfo.lstVoice.Add(int.Parse(buf3[k]));
					}
					startVoicePtn.lstInfo.Add(voicePtnInfo);
				}
				int num3 = AddStartVoicePtn(lstLoadStartVoicePtn[charNum], startVoicePtn);
				if (num3 >= 0)
				{
					lstLoadStartVoicePtn[charNum][num3] = startVoicePtn;
				}
				else
				{
					lstLoadStartVoicePtn[charNum].Add(startVoicePtn);
				}
				line++;
			}
			yield return null;
		}
	}

	private int AddStartVoicePtn(List<StartVoicePtn> voicePtns, StartVoicePtn check)
	{
		for (int i = 0; i < voicePtns.Count; i++)
		{
			if (voicePtns[i].condition == check.condition && voicePtns[i].nForce == check.nForce && voicePtns[i].nTaii == check.nTaii && voicePtns[i].timing == check.timing)
			{
				return i;
			}
		}
		return -1;
	}
}
