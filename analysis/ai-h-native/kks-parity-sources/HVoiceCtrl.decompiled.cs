using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Illusion.CustomAttributes;
using Illusion.Game;
using Manager;
using SaveData;
using UnityEngine;

public class HVoiceCtrl : MonoBehaviour
{
	public enum VoiceKind
	{
		breath,
		breathShort,
		startvoice,
		voice
	}

	[Serializable]
	public class BreathVoiceInfo
	{
		[Label("ID")]
		public int id;

		[Label("グループ")]
		public int group;

		[Label("アセットバンドルパス")]
		public string pathAsset = "";

		[Label("ファイル名")]
		public string nameFile = "";

		[Label("何が再生れていようと上書きして再生")]
		public bool absoluteOverWrite;

		[Label("弱点判定する")]
		public bool isWeakPointJudge;

		[Label("弱点表情")]
		public int faceWeak = -1;

		public List<int> lstFaceNormal = new List<int>();

		[Label("セリフ")]
		public string word = "";

		[Label("再生した？")]
		public bool isPlay;
	}

	[Serializable]
	public class VoiceInfo
	{
		[Label("ID")]
		public int id;

		[Label("優先順位")]
		public int priority;

		[Label("アセットバンドルパス")]
		public string pathAsset = "";

		[Label("ファイル名")]
		public string nameFile = "";

		[Label("喋らせるか判断")]
		public bool[] isPlayConditions = new bool[15];

		[Label("シーン中1回しか再生しない")]
		public bool isOneShot;

		[Label("上書き禁止")]
		public bool notOverwrite;

		[Label("表情")]
		public int face = -1;

		[Label("目首")]
		public int eyeneck = -1;

		[Label("セリフ")]
		public string word = "";

		[Label("再生した？")]
		public bool isPlay;
	}

	[Serializable]
	public class ShortBreathInfo
	{
		[Label("ID")]
		public int id;

		[Label("アセットバンドルパス")]
		public string pathAsset = "";

		[Label("ファイル名")]
		public string nameFile = "";

		[Label("上書き禁止")]
		public bool notOverwrite;

		[Label("表情")]
		public int face = -1;

		[Label("セリフ")]
		public string word = "";

		[Label("再生した？")]
		public bool isPlay;
	}

	[Serializable]
	public class LinkInfo
	{
		public bool isLilnk;

		public int no;

		public int condition = -1;

		public int etc = -1;

		public VoiceKind kind;

		public float time;

		public int action;

		public LinkInfo()
		{
		}

		public LinkInfo(LinkInfo _copy)
		{
			isLilnk = _copy.isLilnk;
			no = _copy.no;
			condition = _copy.condition;
			etc = _copy.etc;
			kind = _copy.kind;
			time = 0f;
			action = 0;
		}
	}

	[Serializable]
	public class VoicePtnInfo
	{
		public List<int> lstAnimID = new List<int>();

		public List<int> lstConditions = new List<int>();

		public List<int> lstVoice = new List<int>();

		public List<int> lstSecondConditions = new List<int>();

		public List<int> lstSecondVoice = new List<int>();

		public LinkInfo link = new LinkInfo();

		public int state;
	}

	[Serializable]
	public class BreathPtn
	{
		[Label("ID")]
		public int id = -1;

		[Label("アニメーション名")]
		public string anim = "";

		[Label("表情変更時間最小")]
		public float timeChangeFaceMin = 15f;

		[Label("表情変更時間最題")]
		public float timeChangeFaceMax = 30f;

		public List<VoicePtnInfo> lstInfo = new List<VoicePtnInfo>();
	}

	[Serializable]
	public class VoicePtn
	{
		[Label("ID")]
		public int id;

		public List<VoicePtnInfo> lstInfo = new List<VoicePtnInfo>();
	}

	[Serializable]
	public class ShortBreathPtnInfo
	{
		public List<int> lstConditions = new List<int>();

		public List<int> lstVoice = new List<int>();

		public int state;
	}

	[Serializable]
	public class ShortBreathPtn
	{
		[Label("ID")]
		public int id;

		public List<ShortBreathPtnInfo> lstInfo = new List<ShortBreathPtnInfo>();
	}

	public class VoiceSelect
	{
		public int priority;

		public int state;

		public int playnum;

		public VoicePtnInfo infoPtn;

		public ShortBreathPtnInfo infoShortPtn;
	}

	[Serializable]
	public class Voice
	{
		public VoiceKind state;

		[Header("呼吸")]
		public BreathVoiceInfo breathInfo;

		[Label("呼吸アニメーションステート")]
		public string animBreath;

		[Label("表情変化経過時間")]
		public float timeFaceDelta;

		[Label("表情変化時間")]
		public float timeFace;

		[Label("表情変化時間最大")]
		public float timeFaceMax;

		[Label("表情変化時間最小")]
		public float timeFaceMin;

		[Header("セリフ")]
		public VoiceInfo voiceInfo;

		[Header("短い喘ぎ")]
		public ShortBreathInfo shortInfo;

		[Header("リンク")]
		public LinkInfo link = new LinkInfo();

		[Header("共通")]
		[Label("上書き禁止")]
		public bool notOverWrite;
	}

	private delegate bool VoicePtnConditionDelegate(List<int> _list, ChaControl _female, int _main);

	public Dictionary<int, Dictionary<int, Dictionary<int, BreathVoiceInfo>>>[] dicBreathVoiceIntos = new Dictionary<int, Dictionary<int, Dictionary<int, BreathVoiceInfo>>>[2]
	{
		new Dictionary<int, Dictionary<int, Dictionary<int, BreathVoiceInfo>>>(),
		new Dictionary<int, Dictionary<int, Dictionary<int, BreathVoiceInfo>>>()
	};

	public Dictionary<int, Dictionary<int, Dictionary<int, VoiceInfo>>>[] dicVoiceIntos = new Dictionary<int, Dictionary<int, Dictionary<int, VoiceInfo>>>[2]
	{
		new Dictionary<int, Dictionary<int, Dictionary<int, VoiceInfo>>>(),
		new Dictionary<int, Dictionary<int, Dictionary<int, VoiceInfo>>>()
	};

	public Dictionary<int, Dictionary<int, ShortBreathInfo>>[] dicShortBreathIntos = new Dictionary<int, Dictionary<int, ShortBreathInfo>>[2]
	{
		new Dictionary<int, Dictionary<int, ShortBreathInfo>>(),
		new Dictionary<int, Dictionary<int, ShortBreathInfo>>()
	};

	public Dictionary<int, Dictionary<string, BreathPtn>>[] dicBreathPtn = new Dictionary<int, Dictionary<string, BreathPtn>>[2]
	{
		new Dictionary<int, Dictionary<string, BreathPtn>>(),
		new Dictionary<int, Dictionary<string, BreathPtn>>()
	};

	public Dictionary<int, Dictionary<int, VoicePtn>>[] dicVoicePtn = new Dictionary<int, Dictionary<int, VoicePtn>>[2]
	{
		new Dictionary<int, Dictionary<int, VoicePtn>>(),
		new Dictionary<int, Dictionary<int, VoicePtn>>()
	};

	public Dictionary<int, ShortBreathPtn> dicShortBreathPtn = new Dictionary<int, ShortBreathPtn>();

	public HFlag flags;

	public FaceListCtrl[] faceLists = new FaceListCtrl[2];

	public HandCtrl hand;

	public bool isPrcoStop;

	public bool isVoicePrcoStop;

	public Transform[] voiceTrans = new Transform[2];

	[SerializeField]
	private BreathPtn[] linkUseBreathPtn = new BreathPtn[2];

	[SerializeField]
	private VoicePtn[] linkUseVoicePtn = new VoicePtn[2];

	public Voice[] nowVoices = new Voice[2]
	{
		new Voice(),
		new Voice()
	};

	private GlobalMethod.FloatBlend[] blendEyes = new GlobalMethod.FloatBlend[2];

	private GlobalMethod.FloatBlend[] blendMouths = new GlobalMethod.FloatBlend[2];

	private readonly Dictionary<string, string> fixPersonal = new Dictionary<string, string> { { "c-13", "c39" } };

	private List<string> lstUnloadBundle = new List<string>();

	private bool VomitVoice;

	public IEnumerator Init(string _chaFolder, string _chaFolder1)
	{
		isPrcoStop = false;
		isVoicePrcoStop = false;
		lstUnloadBundle.Clear();
		for (int i = 0; i < 2; i++)
		{
			dicBreathVoiceIntos[i].Clear();
			dicBreathPtn[i].Clear();
			dicVoiceIntos[i].Clear();
			dicVoicePtn[i].Clear();
			for (int j = 0; j < 8; j++)
			{
				dicBreathVoiceIntos[i].Add(j, new Dictionary<int, Dictionary<int, BreathVoiceInfo>>());
				dicBreathPtn[i].Add(j, new Dictionary<string, BreathPtn>());
			}
			for (int k = 0; k < 9; k++)
			{
				dicVoiceIntos[i].Add(k, new Dictionary<int, Dictionary<int, VoiceInfo>>());
				dicVoicePtn[i].Add(k, new Dictionary<int, VoicePtn>());
			}
		}
		blendEyes[0] = new GlobalMethod.FloatBlend();
		blendEyes[1] = new GlobalMethod.FloatBlend();
		blendMouths[0] = new GlobalMethod.FloatBlend();
		blendMouths[1] = new GlobalMethod.FloatBlend();
		string[] personal = new string[2] { _chaFolder, _chaFolder1 };
		for (int l = 0; l < personal.Length; l++)
		{
			int num = l;
			if (fixPersonal.ContainsKey(personal[num]))
			{
				personal[num] = fixPersonal[personal[num]];
			}
		}
		bool second = false;
		int ptnNum = 1;
		if (personal[1] != "")
		{
			second = true;
			ptnNum = 2;
		}
		LoadBreathList(personal[0], personal[1], second, ptnNum);
		LoadShortBreathList(personal[0], personal[1]);
		yield return null;
		yield return LoadVoiceList(personal[0], personal[1], second, ptnNum);
		foreach (string item in lstUnloadBundle)
		{
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: true);
		}
	}

	private bool LoadBreathList(string _chaFolder, string _chaFolder1, bool second, int ptnNum)
	{
		string[] array = new string[2] { _chaFolder, _chaFolder1 };
		bool flag = false;
		for (int i = 0; i < 2; i++)
		{
			if (flag && !second)
			{
				continue;
			}
			LoadBreathBase(array[i], dicBreathVoiceIntos[i]);
			for (int j = 0; j < 8; j++)
			{
				if (!flag)
				{
					LoadBreathPtn(j, ptnNum);
				}
			}
			if (!flag)
			{
				flag = true;
			}
		}
		return true;
	}

	private bool LoadBreathBase(string nameFile, Dictionary<int, Dictionary<int, Dictionary<int, BreathVoiceInfo>>> _breath)
	{
		string text = "personality_breath_" + nameFile;
		foreach (string item in HFlag.lstABName)
		{
			if (!AssetBundleCheck.IsFile(item, text) || (!AssetBundleCheck.IsSimulation && !HFlag.BundleCheck(item, text)))
			{
				continue;
			}
			BreathAllData breathAllData = CommonLib.LoadAsset<BreathAllData>(item, text, clone: false, "", check: false);
			if (!lstUnloadBundle.Contains(item))
			{
				lstUnloadBundle.Add(item);
			}
			if (breathAllData == null)
			{
				continue;
			}
			BreathVoiceInfo breathVoiceInfo = null;
			int num = -1;
			for (int i = 0; i < 8; i++)
			{
				if (!_breath[i].ContainsKey(0))
				{
					_breath[i].Add(0, new Dictionary<int, BreathVoiceInfo>());
				}
				if (!_breath[i].ContainsKey(1))
				{
					_breath[i].Add(1, new Dictionary<int, BreathVoiceInfo>());
				}
				if (!_breath[i].ContainsKey(2))
				{
					_breath[i].Add(2, new Dictionary<int, BreathVoiceInfo>());
				}
				if (!_breath[i].ContainsKey(3))
				{
					_breath[i].Add(3, new Dictionary<int, BreathVoiceInfo>());
				}
				foreach (BreathAllData.Param item2 in breathAllData.param)
				{
					if (item2.mode != (MathfEx.IsRange(6, i, 7, isEqual: true) ? (i - 5) : i))
					{
						continue;
					}
					foreach (BreathAllData.Breathdata datum in item2.data)
					{
						int id = datum.id;
						int num2 = datum.group;
						foreach (KeyValuePair<int, Dictionary<int, BreathVoiceInfo>> item3 in _breath[i])
						{
							if (!item3.Value.ContainsKey(id))
							{
								item3.Value.Add(id, new BreathVoiceInfo());
							}
							breathVoiceInfo = item3.Value[id];
							num = item3.Key;
							breathVoiceInfo.id = id;
							breathVoiceInfo.group = num2;
							if (datum.info[num] == null || datum.info[num].pathAsset.IsNullOrEmpty())
							{
								item3.Value.Remove(id);
								continue;
							}
							breathVoiceInfo.pathAsset = datum.info[num].pathAsset;
							breathVoiceInfo.nameFile = datum.info[num].nameFile;
							breathVoiceInfo.word = datum.info[num].word;
							breathVoiceInfo.absoluteOverWrite = datum.info[num].absoluteOverWrite;
							breathVoiceInfo.isWeakPointJudge = datum.info[num].isWeakPointJudge;
							breathVoiceInfo.faceWeak = datum.info[num].faceWeak;
							breathVoiceInfo.lstFaceNormal.Clear();
							breathVoiceInfo.lstFaceNormal.AddRange(datum.info[num].lstFaceNormal);
						}
					}
				}
			}
		}
		return true;
	}

	private bool LoadBreathPtn(int _mode, int _main)
	{
		string text = "breath_" + _mode.ToString("00") + "_00";
		foreach (string item in HFlag.lstABName)
		{
			if (!AssetBundleCheck.IsFile(item, text) || (!AssetBundleCheck.IsSimulation && !HFlag.BundleCheck(item, text)))
			{
				continue;
			}
			BreathPatternData breathPatternData = CommonLib.LoadAsset<BreathPatternData>(item, text, clone: false, "", check: false);
			if (!lstUnloadBundle.Contains(item))
			{
				lstUnloadBundle.Add(item);
			}
			if (breathPatternData == null)
			{
				continue;
			}
			foreach (BreathPatternData.Param item2 in breathPatternData.param)
			{
				if (!dicBreathPtn[0][_mode].ContainsKey(item2.anim))
				{
					for (int i = 0; i < _main; i++)
					{
						dicBreathPtn[i][_mode].Add(item2.anim, new BreathPtn());
					}
				}
				for (int j = 0; j < _main; j++)
				{
					BreathPtn breathPtn = dicBreathPtn[j][_mode][item2.anim];
					breathPtn.id = item2.id;
					breathPtn.anim = item2.anim;
					breathPtn.timeChangeFaceMin = item2.timeChangeFaceMin;
					breathPtn.timeChangeFaceMax = item2.timeChangeFaceMax;
					breathPtn.lstInfo.Clear();
					for (int k = 0; k < item2.lstInfo.Count; k++)
					{
						breathPtn.lstInfo.Add(new VoicePtnInfo());
						breathPtn.lstInfo[k].lstAnimID.AddRange(item2.lstInfo[k].lstAnimID);
						breathPtn.lstInfo[k].state = item2.lstInfo[k].state;
						breathPtn.lstInfo[k].lstConditions.AddRange(item2.lstInfo[k].lstConditions);
						breathPtn.lstInfo[k].lstVoice.AddRange(item2.lstInfo[k].lstVoice);
						breathPtn.lstInfo[k].link.isLilnk = item2.lstInfo[k].link.isLilnk;
						breathPtn.lstInfo[k].link.no = item2.lstInfo[k].link.no;
						breathPtn.lstInfo[k].link.condition = item2.lstInfo[k].link.condition;
						breathPtn.lstInfo[k].link.etc = item2.lstInfo[k].link.etc;
						breathPtn.lstInfo[k].link.kind = item2.lstInfo[k].link.kind;
					}
				}
			}
		}
		return true;
	}

	private IEnumerator LoadVoiceList(string _chaFolder, string _chaFolder1, bool second, int ptnNum)
	{
		string[] chaNo = new string[2] { _chaFolder, _chaFolder1 };
		bool loadPtn = false;
		int i = 0;
		while (i < 2)
		{
			if (!loadPtn || second)
			{
				LoadVoice(chaNo[i], i);
				for (int j = 0; j < 9; j++)
				{
					if (!loadPtn)
					{
						LoadVoicePtn(j, ptnNum);
					}
				}
				if (!loadPtn)
				{
					loadPtn = true;
				}
				yield return null;
			}
			int num = i + 1;
			i = num;
		}
	}

	private bool LoadVoice(string _chaFolder, int _main)
	{
		string text = "personality_voice_" + _chaFolder;
		foreach (string item in HFlag.lstABName)
		{
			if (!AssetBundleCheck.IsFile(item, text) || (!AssetBundleCheck.IsSimulation && !HFlag.BundleCheck(item, text)))
			{
				continue;
			}
			VoiceAllData voiceAllData = CommonLib.LoadAsset<VoiceAllData>(item, text, clone: false, "", check: false);
			if (!lstUnloadBundle.Contains(item))
			{
				lstUnloadBundle.Add(item);
			}
			if (voiceAllData == null)
			{
				continue;
			}
			VoiceInfo voiceInfo = null;
			int num = -1;
			for (int i = 0; i < 9; i++)
			{
				if (!dicVoiceIntos[_main][i].ContainsKey(0))
				{
					dicVoiceIntos[_main][i].Add(0, new Dictionary<int, VoiceInfo>());
				}
				if (!dicVoiceIntos[_main][i].ContainsKey(1))
				{
					dicVoiceIntos[_main][i].Add(1, new Dictionary<int, VoiceInfo>());
				}
				if (!dicVoiceIntos[_main][i].ContainsKey(2))
				{
					dicVoiceIntos[_main][i].Add(2, new Dictionary<int, VoiceInfo>());
				}
				if (!dicVoiceIntos[_main][i].ContainsKey(3))
				{
					dicVoiceIntos[_main][i].Add(3, new Dictionary<int, VoiceInfo>());
				}
				foreach (VoiceAllData.Param item2 in voiceAllData.param)
				{
					if (item2.mode != (MathfEx.IsRange(7, i, 8, isEqual: true) ? (i - 5) : i))
					{
						continue;
					}
					foreach (VoiceAllData.Voicedata datum in item2.data)
					{
						int id = datum.id;
						foreach (KeyValuePair<int, Dictionary<int, VoiceInfo>> item3 in dicVoiceIntos[_main][i])
						{
							if (!item3.Value.ContainsKey(id))
							{
								item3.Value.Add(id, new VoiceInfo());
							}
							voiceInfo = item3.Value[id];
							num = item3.Key;
							voiceInfo.id = id;
							voiceInfo.priority = datum.info[num].priority;
							voiceInfo.pathAsset = datum.info[num].pathAsset;
							if (voiceInfo.pathAsset.IsNullOrEmpty())
							{
								item3.Value.Remove(id);
								continue;
							}
							voiceInfo.nameFile = datum.info[num].nameFile;
							voiceInfo.word = datum.info[num].word;
							for (int j = 0; j < voiceInfo.isPlayConditions.Length; j++)
							{
								voiceInfo.isPlayConditions[j] = datum.info[num].playConditions[j];
							}
							voiceInfo.isOneShot = datum.info[num].isOneShot;
							voiceInfo.notOverwrite = datum.info[num].notOverwrite;
							voiceInfo.face = datum.info[num].face;
							voiceInfo.eyeneck = datum.info[num].eyeneck;
						}
					}
				}
			}
		}
		return true;
	}

	private bool LoadVoicePtn(int _mode, int _main)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendFormat("voice_{0:00}_00", _mode);
		foreach (string item in HFlag.lstABName)
		{
			if (!AssetBundleCheck.IsFile(item, stringBuilder.ToString()) || (!AssetBundleCheck.IsSimulation && !HFlag.BundleCheck(item, stringBuilder.ToString())))
			{
				continue;
			}
			VoicePatternData voicePatternData = CommonLib.LoadAsset<VoicePatternData>(item, stringBuilder.ToString(), clone: false, "", check: false);
			if (!lstUnloadBundle.Contains(item))
			{
				lstUnloadBundle.Add(item);
			}
			if (voicePatternData == null)
			{
				continue;
			}
			foreach (VoicePatternData.Param item2 in voicePatternData.param)
			{
				int id = item2.id;
				if (!dicVoicePtn[0][_mode].ContainsKey(id))
				{
					for (int i = 0; i < _main; i++)
					{
						dicVoicePtn[i][_mode].Add(id, new VoicePtn());
					}
				}
				for (int j = 0; j < _main; j++)
				{
					VoicePtn voicePtn = dicVoicePtn[j][_mode][id];
					voicePtn.id = id;
					voicePtn.lstInfo.Clear();
					for (int k = 0; k < item2.lstInfo.Count; k++)
					{
						voicePtn.lstInfo.Add(new VoicePtnInfo());
						voicePtn.lstInfo[k].lstAnimID.AddRange(item2.lstInfo[k].lstAnimID);
						voicePtn.lstInfo[k].state = item2.lstInfo[k].state;
						voicePtn.lstInfo[k].lstConditions.AddRange(item2.lstInfo[k].lstConditions);
						voicePtn.lstInfo[k].lstVoice.AddRange(item2.lstInfo[k].lstVoice);
						voicePtn.lstInfo[k].lstSecondConditions.AddRange(item2.lstInfo[k].lstSecondConditions);
						voicePtn.lstInfo[k].lstSecondVoice.AddRange(item2.lstInfo[k].lstSecondVoice);
						voicePtn.lstInfo[k].link.isLilnk = item2.lstInfo[k].link.isLilnk;
						voicePtn.lstInfo[k].link.no = item2.lstInfo[k].link.no;
						voicePtn.lstInfo[k].link.condition = item2.lstInfo[k].link.condition;
						voicePtn.lstInfo[k].link.etc = item2.lstInfo[k].link.etc;
						voicePtn.lstInfo[k].link.kind = item2.lstInfo[k].link.kind;
					}
				}
			}
		}
		return true;
	}

	private bool LoadShortBreathList(string _chaFolder, string _chaFolder1)
	{
		string[] array = new string[2] { _chaFolder, _chaFolder1 };
		for (int i = 0; i < 2; i++)
		{
			dicShortBreathIntos[i].Clear();
			if (!(array[i] == ""))
			{
				LoadShortBreath(array[i], i);
			}
		}
		LoadShortBreathPtn();
		return true;
	}

	private bool LoadShortBreath(string _chaFolder, int _main)
	{
		dicShortBreathIntos[_main].Add(0, new Dictionary<int, ShortBreathInfo>());
		dicShortBreathIntos[_main].Add(1, new Dictionary<int, ShortBreathInfo>());
		dicShortBreathIntos[_main].Add(2, new Dictionary<int, ShortBreathInfo>());
		dicShortBreathIntos[_main].Add(3, new Dictionary<int, ShortBreathInfo>());
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("personality_shortbreath_").Append(_chaFolder).Append("_00");
		foreach (string item in HFlag.lstABName)
		{
			if (!AssetBundleCheck.IsFile(item, stringBuilder.ToString()) || (!AssetBundleCheck.IsSimulation && !HFlag.BundleCheck(item, stringBuilder.ToString())))
			{
				continue;
			}
			ShortBreathData shortBreathData = CommonLib.LoadAsset<ShortBreathData>(item, stringBuilder.ToString(), clone: false, "", check: false);
			if (!lstUnloadBundle.Contains(item))
			{
				lstUnloadBundle.Add(item);
			}
			if (shortBreathData == null)
			{
				continue;
			}
			foreach (ShortBreathData.Param item2 in shortBreathData.param)
			{
				int id = item2.id;
				foreach (KeyValuePair<int, Dictionary<int, ShortBreathInfo>> item3 in dicShortBreathIntos[_main])
				{
					int key = item3.Key;
					if (!item3.Value.ContainsKey(id))
					{
						item3.Value.Add(id, new ShortBreathInfo());
					}
					ShortBreathInfo shortBreathInfo = item3.Value[id];
					shortBreathInfo.id = id;
					shortBreathInfo.pathAsset = item2.infos[key].pathAsset;
					shortBreathInfo.nameFile = item2.infos[key].nameFile;
					shortBreathInfo.word = item2.infos[key].word;
					shortBreathInfo.notOverwrite = item2.infos[key].notOverwrite;
					shortBreathInfo.face = item2.infos[key].face;
				}
			}
		}
		return true;
	}

	private bool LoadShortBreathPtn()
	{
		string text = "shortbreath_00";
		dicShortBreathPtn.Clear();
		foreach (string item in HFlag.lstABName)
		{
			if (!AssetBundleCheck.IsFile(item, text) || (!AssetBundleCheck.IsSimulation && !HFlag.BundleCheck(item, text)))
			{
				continue;
			}
			ShortBreathPatternData shortBreathPatternData = CommonLib.LoadAsset<ShortBreathPatternData>(item, text, clone: false, "", check: false);
			if (!lstUnloadBundle.Contains(item))
			{
				lstUnloadBundle.Add(item);
			}
			if (shortBreathPatternData == null)
			{
				continue;
			}
			foreach (ShortBreathPatternData.Param item2 in shortBreathPatternData.param)
			{
				int id = item2.id;
				if (!dicShortBreathPtn.ContainsKey(id))
				{
					dicShortBreathPtn.Add(id, new ShortBreathPtn());
				}
				ShortBreathPtn shortBreathPtn = dicShortBreathPtn[id];
				shortBreathPtn.id = id;
				shortBreathPtn.lstInfo.Clear();
				for (int i = 0; i < item2.lstInfo.Count; i++)
				{
					shortBreathPtn.lstInfo.Add(new ShortBreathPtnInfo());
					shortBreathPtn.lstInfo[i].state = item2.lstInfo[i].state;
					shortBreathPtn.lstInfo[i].lstConditions.AddRange(item2.lstInfo[i].lstConditions);
					shortBreathPtn.lstInfo[i].lstVoice.AddRange(item2.lstInfo[i].lstVoice);
				}
			}
		}
		return true;
	}

	public bool PlayVoice(ChaControl _female, Utils.Voice.Setting _setting, int _face, int _eyeneck, int _voiceKind, int _action, int _main)
	{
		_female.SetLipSync(Utils.Voice.OncePlayChara(_setting));
		if (_main >= 2)
		{
			return false;
		}
		faceLists[_main].SetFace(_face, _female, _voiceKind, _action);
		flags.voice.eyenecks[_main] = _eyeneck;
		return true;
	}

	public bool Proc(AnimatorStateInfo _ai, List<ChaControl> _females)
	{
		if (isPrcoStop)
		{
			return true;
		}
		bool[] array = new bool[2];
		if (!isVoicePrcoStop)
		{
			for (int i = 0; i < 2; i++)
			{
				if (!array[i] && _females.Count > i)
				{
					array[i] = VoiceProc(_ai, _females[i], i);
				}
			}
		}
		for (int j = 0; j < 2; j++)
		{
			if (!array[j] && _females.Count > j)
			{
				array[j] = ShortBreathProc(_ai, _females[j], j);
			}
		}
		for (int k = 0; k < 2; k++)
		{
			if (!array[k] && _females.Count > k)
			{
				BreathProc(_ai, _females[k], k);
			}
		}
		for (int l = 0; l < 2; l++)
		{
			if (_females.Count > l)
			{
				OpenCtrl(_females[l], l);
			}
		}
		return true;
	}

	public bool OpenCtrl(ChaControl _female, int _main)
	{
		float _ans = 0f;
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
		return true;
	}

	private int GetStateSelect(int _condition, int _main)
	{
		if (flags.lstHeroine[_main].HExperience == Heroine.HExperienceKind.淫乱)
		{
			return 3;
		}
		switch (_condition)
		{
		case 0:
			return (int)flags.lstHeroine[_main].HExperience;
		case 1:
			if (!flags.lstHeroine[_main].isKiss)
			{
				return 0;
			}
			if (flags.lstHeroine[_main].HExperience != Heroine.HExperienceKind.初めて)
			{
				return (int)flags.lstHeroine[_main].HExperience;
			}
			return 1;
		case 2:
			if (flags.lstHeroine[_main].hAreaExps[1] < 0.05f)
			{
				return 0;
			}
			return (int)flags.lstHeroine[_main].HExperience;
		case 3:
			if (flags.lstHeroine[_main].hAreaExps[2] < 0.05f)
			{
				return 0;
			}
			return (int)flags.lstHeroine[_main].HExperience;
		case 4:
			if (flags.lstHeroine[_main].hAreaExps[3] < 0.05f)
			{
				return 0;
			}
			return (int)flags.lstHeroine[_main].HExperience;
		case 5:
			if (flags.lstHeroine[_main].hAreaExps[4] < 0.05f)
			{
				return 0;
			}
			return (int)flags.lstHeroine[_main].HExperience;
		case 6:
			if (flags.lstHeroine[_main].hAreaExps[5] < 0.05f)
			{
				return 0;
			}
			return (int)flags.lstHeroine[_main].HExperience;
		case 7:
			if (flags.lstHeroine[_main].massageExps[0] < 0.05f)
			{
				return 0;
			}
			return (int)flags.lstHeroine[_main].HExperience;
		case 8:
			if (flags.lstHeroine[_main].countKokanH < 0.05f)
			{
				return 0;
			}
			return (int)flags.lstHeroine[_main].HExperience;
		case 9:
			if (flags.lstHeroine[_main].countAnalH < 0.05f)
			{
				return 0;
			}
			return (int)flags.lstHeroine[_main].HExperience;
		case 10:
			if (flags.lstHeroine[_main].massageExps[1] < 0.05f)
			{
				return 0;
			}
			return (int)flags.lstHeroine[_main].HExperience;
		default:
			return 0;
		}
	}

	private bool BreathProc(AnimatorStateInfo _ai, ChaControl _female, int _main)
	{
		if (_female == null || (_female != null && _female.objBody == null))
		{
			return false;
		}
		if (dicBreathVoiceIntos[_main][(int)flags.mode].Count == 0)
		{
			return false;
		}
		Dictionary<int, Dictionary<int, BreathVoiceInfo>> dictionary = dicBreathVoiceIntos[_main][(int)flags.mode];
		if (dictionary.Count == 0)
		{
			return false;
		}
		if (BreathLinkProc(dictionary, _female, _main))
		{
			return true;
		}
		if (nowVoices[_main].state == VoiceKind.breath)
		{
			nowVoices[_main].timeFaceDelta += Time.deltaTime;
			if (nowVoices[_main].timeFaceDelta >= nowVoices[_main].timeFace)
			{
				SetBreathFace(_female, (int)flags.mode, _main);
			}
		}
		foreach (KeyValuePair<string, BreathPtn> item in dicBreathPtn[_main][(int)flags.mode])
		{
			if (!_ai.IsName(item.Key))
			{
				continue;
			}
			bool flag = item.Key == "Vomit";
			if (!flag && VomitVoice)
			{
				VomitVoice = false;
			}
			List<VoicePtnInfo> list = new List<VoicePtnInfo>();
			for (int i = 0; i < item.Value.lstInfo.Count; i++)
			{
				if (IsPlayBreathVoicePtn(item.Value.lstInfo[i], _main))
				{
					list.Add(item.Value.lstInfo[i]);
				}
			}
			List<VoiceSelect> pLayNumBreathList = GetPLayNumBreathList(list, dictionary, _main);
			if (pLayNumBreathList.Count == 0)
			{
				if (VomitVoice)
				{
					break;
				}
				for (int j = 0; j < list.Count; j++)
				{
					int stateSelect = GetStateSelect(list[j].state, _main);
					for (int k = 0; k < list[j].lstVoice.Count; k++)
					{
						if (dictionary[stateSelect].ContainsKey(list[j].lstVoice[k]))
						{
							dictionary[stateSelect][list[j].lstVoice[k]].isPlay = false;
						}
					}
				}
				pLayNumBreathList = GetPLayNumBreathList(list, dictionary, _main);
				if (pLayNumBreathList.Count == 0)
				{
					break;
				}
			}
			pLayNumBreathList = pLayNumBreathList.OrderBy((VoiceSelect l) => Guid.NewGuid()).ToList();
			if (!dictionary[pLayNumBreathList[0].state][pLayNumBreathList[0].playnum].absoluteOverWrite)
			{
				if ((nowVoices[_main].state != VoiceKind.breath || (nowVoices[_main].state == VoiceKind.breath && nowVoices[_main].breathInfo != null && dictionary[pLayNumBreathList[0].state][pLayNumBreathList[0].playnum].group == nowVoices[_main].breathInfo.group)) && Manager.Voice.IsPlay(flags.transVoiceMouth[_main]))
				{
					break;
				}
			}
			else if (nowVoices[_main].state == VoiceKind.breath && nowVoices[_main].breathInfo != null && dictionary[pLayNumBreathList[0].state][pLayNumBreathList[0].playnum].group == nowVoices[_main].breathInfo.group && Manager.Voice.IsPlay(flags.transVoiceMouth[_main]))
			{
				break;
			}
			nowVoices[_main].breathInfo = dictionary[pLayNumBreathList[0].state][pLayNumBreathList[0].playnum];
			Utils.Voice.Setting s = new Utils.Voice.Setting
			{
				no = flags.lstHeroine[_main].voiceNo,
				assetBundleName = nowVoices[_main].breathInfo.pathAsset,
				assetName = nowVoices[_main].breathInfo.nameFile,
				pitch = flags.lstHeroine[_main].voicePitch,
				voiceTrans = flags.transVoiceMouth[_main]
			};
			_female.SetLipSync(Utils.Voice.OncePlayChara(s));
			flags.hashAssetBundle.Add(nowVoices[_main].breathInfo.pathAsset);
			nowVoices[_main].state = VoiceKind.breath;
			nowVoices[_main].notOverWrite = false;
			nowVoices[_main].breathInfo.isPlay = true;
			nowVoices[_main].timeFaceMax = item.Value.timeChangeFaceMax;
			nowVoices[_main].timeFaceMin = item.Value.timeChangeFaceMin;
			if (pLayNumBreathList[0].infoPtn.link.isLilnk)
			{
				linkUseBreathPtn[_main] = item.Value;
				nowVoices[_main].link = pLayNumBreathList[0].infoPtn.link;
			}
			else
			{
				linkUseBreathPtn[_main] = null;
				nowVoices[_main].link = new LinkInfo();
			}
			linkUseVoicePtn[_main] = null;
			if (flag)
			{
				VomitVoice = true;
			}
			SetBreathFace(_female, (int)flags.mode, _main);
			flags.voice.eyenecks[_main] = -1;
			break;
		}
		return true;
	}

	private bool BreathLinkProc(Dictionary<int, Dictionary<int, BreathVoiceInfo>> _dicUseVoiceInfo, ChaControl _female, int _main)
	{
		if (!nowVoices[_main].link.isLilnk || nowVoices[_main].link.kind != VoiceKind.breath)
		{
			return false;
		}
		if (nowVoices[_main].link.condition == -1)
		{
			if (Manager.Voice.IsPlay(flags.transVoiceMouth[_main]))
			{
				return false;
			}
			if (nowVoices[_main].link.etc == 1)
			{
				flags.voice.gentles[_main] = true;
			}
		}
		else if (nowVoices[_main].link.condition == 0)
		{
			nowVoices[_main].link.time += Time.deltaTime;
			if (nowVoices[_main].link.time < (float)nowVoices[_main].link.etc)
			{
				return false;
			}
		}
		else if (nowVoices[_main].link.condition == 1)
		{
			if (flags.gaugeFemale >= 50f)
			{
				return false;
			}
		}
		else if (nowVoices[_main].link.condition == 2)
		{
			if (Manager.Voice.IsPlay(flags.transVoiceMouth[_main]))
			{
				return false;
			}
			if (!flags.voice.isAfterVoicePlay)
			{
				return false;
			}
			flags.voice.gentles[_main] = true;
		}
		int num = -1;
		int num2 = 0;
		if (linkUseBreathPtn[_main] != null)
		{
			for (int i = 0; i < linkUseBreathPtn[_main].lstInfo.Count; i++)
			{
				if (linkUseBreathPtn[_main].lstInfo[i].lstConditions.Contains(nowVoices[_main].link.no) && IsBreathAnimationList(linkUseBreathPtn[_main].lstInfo[i].lstAnimID, flags.nowAnimationInfo.id))
				{
					num2 = GetStateSelect(linkUseBreathPtn[_main].lstInfo[i].state, _main);
					num = GetBreathVoiceLinkPlayNum(linkUseBreathPtn[_main].lstInfo[i].lstVoice, _dicUseVoiceInfo, num2, _main);
					break;
				}
			}
		}
		if (num == -1)
		{
			GlobalMethod.DebugLog("link先取得エラー", 1);
			nowVoices[_main].link = new LinkInfo();
			linkUseBreathPtn[_main] = null;
			return false;
		}
		nowVoices[_main].breathInfo = _dicUseVoiceInfo[num2][num];
		Utils.Voice.Setting s = new Utils.Voice.Setting
		{
			no = flags.lstHeroine[_main].voiceNo,
			assetBundleName = nowVoices[_main].breathInfo.pathAsset,
			assetName = nowVoices[_main].breathInfo.nameFile,
			pitch = flags.lstHeroine[_main].voicePitch,
			voiceTrans = flags.transVoiceMouth[_main]
		};
		_female.SetLipSync(Utils.Voice.OncePlayChara(s));
		flags.hashAssetBundle.Add(nowVoices[_main].breathInfo.pathAsset);
		nowVoices[_main].state = VoiceKind.breath;
		nowVoices[_main].notOverWrite = false;
		nowVoices[_main].timeFaceMax = linkUseBreathPtn[_main].timeChangeFaceMax;
		nowVoices[_main].timeFaceMin = linkUseBreathPtn[_main].timeChangeFaceMin;
		SetBreathFace(_female, (int)flags.mode, _main);
		flags.voice.eyenecks[_main] = -1;
		linkUseBreathPtn[_main] = null;
		nowVoices[_main].link.time = 0f;
		nowVoices[_main].link = new LinkInfo();
		return true;
	}

	private int GetBreathVoiceLinkPlayNum(List<int> _info, Dictionary<int, Dictionary<int, BreathVoiceInfo>> _breathVoice, int _state, int _main)
	{
		int num = -1;
		GlobalMethod.ShuffleRandIndex shuffleRandIndex = new GlobalMethod.ShuffleRandIndex();
		shuffleRandIndex.Init(_info);
		do
		{
			int _get = -1;
			if (1 == shuffleRandIndex.Get(out _get))
			{
				return -1;
			}
			num = _info[_get];
		}
		while (!_breathVoice[_state].ContainsKey(num));
		return num;
	}

	private List<VoiceSelect> GetPLayNumBreathList(List<VoicePtnInfo> _lstPlayInfo, Dictionary<int, Dictionary<int, BreathVoiceInfo>> _dicUseVoiceInfo, int _main)
	{
		List<VoiceSelect> list = new List<VoiceSelect>();
		for (int i = 0; i < _lstPlayInfo.Count; i++)
		{
			int stateSelect = GetStateSelect(_lstPlayInfo[i].state, _main);
			for (int j = 0; j < _lstPlayInfo[i].lstVoice.Count; j++)
			{
				if (_dicUseVoiceInfo[stateSelect].ContainsKey(_lstPlayInfo[i].lstVoice[j]) && !_dicUseVoiceInfo[stateSelect][_lstPlayInfo[i].lstVoice[j]].isPlay)
				{
					VoiceSelect voiceSelect = new VoiceSelect();
					voiceSelect.state = stateSelect;
					voiceSelect.playnum = _lstPlayInfo[i].lstVoice[j];
					voiceSelect.infoPtn = _lstPlayInfo[i];
					list.Add(voiceSelect);
				}
			}
		}
		return list;
	}

	private bool IsPlayBreathVoicePtn(VoicePtnInfo _lst, int _main)
	{
		if (!IsBreathAnimationList(_lst.lstAnimID, flags.nowAnimationInfo.id))
		{
			return false;
		}
		if (!IsBreathPtnConditions(_lst.lstConditions, _main))
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

	private bool IsBreathPtnConditions(List<int> _lstConditions, int _main)
	{
		for (int i = 0; i < _lstConditions.Count; i++)
		{
			if (_lstConditions[i] >= 90)
			{
				return false;
			}
			switch (_lstConditions[i])
			{
			case 0:
				if (flags.gaugeFemale < 50f)
				{
					return false;
				}
				break;
			case 1:
				if (flags.gaugeFemale >= 50f)
				{
					return false;
				}
				break;
			case 2:
				if (!hand.IsAction())
				{
					return false;
				}
				break;
			case 3:
				if (hand.IsAction())
				{
					return false;
				}
				break;
			case 4:
				if (flags.voice.speedMotion)
				{
					return false;
				}
				break;
			case 5:
				if (!flags.voice.speedMotion)
				{
					return false;
				}
				break;
			case 6:
				if (flags.voice.speedItem)
				{
					return false;
				}
				break;
			case 7:
				if (!flags.voice.speedItem)
				{
					return false;
				}
				break;
			case 8:
				if (!hand.IsKissAction())
				{
					return false;
				}
				break;
			case 9:
				if (hand.IsKissAction())
				{
					return false;
				}
				break;
			case 10:
				if (flags.voice.gentles[_main])
				{
					return false;
				}
				break;
			case 11:
				if (!flags.voice.gentles[_main])
				{
					return false;
				}
				break;
			case 12:
				if (flags.mode == HFlag.EMode.aibu)
				{
					if (flags.speed < 0.01f)
					{
						return false;
					}
				}
				else if (flags.speedCalc < 0.01f)
				{
					return false;
				}
				break;
			case 13:
				if (flags.mode == HFlag.EMode.aibu)
				{
					if (flags.speed >= 0.01f)
					{
						return false;
					}
				}
				else if (flags.speedCalc >= 0.01f)
				{
					return false;
				}
				break;
			case 14:
				if (!hand.IsItemTouch())
				{
					return false;
				}
				break;
			case 15:
				if (hand.IsItemTouch())
				{
					return false;
				}
				break;
			case 16:
				if (_main != 0)
				{
					return false;
				}
				break;
			case 17:
				if (_main != 1)
				{
					return false;
				}
				break;
			case 18:
				if (flags.nowAnimationInfo.numMainVoiceID != -1)
				{
					if (_main == 0 && flags.nowAnimationInfo.numMainVoiceID != 0)
					{
						return false;
					}
					if (_main == 1 && flags.nowAnimationInfo.numSubVoiceID != 0)
					{
						return false;
					}
				}
				break;
			case 19:
				if (flags.nowAnimationInfo.numMainVoiceID != -1)
				{
					if (_main == 0 && flags.nowAnimationInfo.numMainVoiceID != 1)
					{
						return false;
					}
					if (_main == 1 && flags.nowAnimationInfo.numSubVoiceID != 1)
					{
						return false;
					}
				}
				break;
			}
		}
		return true;
	}

	private bool SetBreathFace(ChaControl _female, int _action, int _main)
	{
		nowVoices[_main].timeFaceDelta = 0f;
		nowVoices[_main].timeFace = UnityEngine.Random.Range(nowVoices[_main].timeFaceMax, nowVoices[_main].timeFaceMin);
		if (nowVoices[_main].breathInfo.isWeakPointJudge && hand.IsAction() && IsWeakPoint(_main))
		{
			faceLists[_main].SetFace(nowVoices[_main].breathInfo.faceWeak, _female, 0, _action);
			return true;
		}
		if (nowVoices[_main].breathInfo.lstFaceNormal.Count != 0)
		{
			faceLists[_main].SetFace(nowVoices[_main].breathInfo.lstFaceNormal[UnityEngine.Random.Range(0, nowVoices[_main].breathInfo.lstFaceNormal.Count)], _female, 0, _action);
		}
		return true;
	}

	private bool IsWeakPoint(int _main)
	{
		int useAreaItemActive = hand.GetUseAreaItemActive();
		HandCtrl.AibuColliderKind useItemStickArea = hand.GetUseItemStickArea(useAreaItemActive);
		if (useItemStickArea == HandCtrl.AibuColliderKind.none)
		{
			return false;
		}
		if (flags.lstHeroine[_main].weakPoint == -1)
		{
			return false;
		}
		if (flags.lstHeroine[_main].weakPoint == 0 && useItemStickArea != HandCtrl.AibuColliderKind.mouth)
		{
			return false;
		}
		if (flags.lstHeroine[_main].weakPoint == 1 && ((useItemStickArea != HandCtrl.AibuColliderKind.muneL && useItemStickArea != HandCtrl.AibuColliderKind.muneR) || hand.GetUseItemStickObjectID(useAreaItemActive) != 0))
		{
			return false;
		}
		if (flags.lstHeroine[_main].weakPoint == 2 && useItemStickArea != HandCtrl.AibuColliderKind.kokan)
		{
			return false;
		}
		if (flags.lstHeroine[_main].weakPoint == 3 && useItemStickArea != HandCtrl.AibuColliderKind.anal)
		{
			return false;
		}
		if (flags.lstHeroine[_main].weakPoint == 4 && useItemStickArea != HandCtrl.AibuColliderKind.siriL && useItemStickArea != HandCtrl.AibuColliderKind.siriR)
		{
			return false;
		}
		if (flags.lstHeroine[_main].weakPoint == 5 && ((useItemStickArea != HandCtrl.AibuColliderKind.muneL && useItemStickArea != HandCtrl.AibuColliderKind.muneR) || hand.GetUseItemStickObjectID(useAreaItemActive) == 0))
		{
			return false;
		}
		return true;
	}

	private bool VoiceProc(AnimatorStateInfo _ai, ChaControl _female, int _main)
	{
		if (_female == null || (_female != null && _female.objBody == null))
		{
			return false;
		}
		if (VoiceLinkProc(_female, _main))
		{
			return true;
		}
		if (flags.voice.playVoices[_main] == -1)
		{
			return false;
		}
		if (nowVoices[_main].notOverWrite && Manager.Voice.IsPlay(flags.transVoiceMouth[_main]))
		{
			return false;
		}
		int num = flags.voice.playVoices[_main] / 100;
		int key = flags.voice.playVoices[_main] % 100;
		if (!dicVoicePtn[_main].ContainsKey(num))
		{
			GlobalMethod.DebugLog("セリフパターン行為取得失敗[取得した行為番号 : " + num + "][元番号 : " + flags.voice.playVoices[_main] + "]", 1);
			flags.voice.playVoices[_main] = -1;
			return false;
		}
		if (!dicVoicePtn[_main][num].ContainsKey(key))
		{
			GlobalMethod.DebugLog("セリフパターンID取得失敗[取得した行為番号 : " + num + "][[取得したID番号 : " + key + "][元番号 : " + flags.voice.playVoices[_main] + "]", 1);
			flags.voice.playVoices[_main] = -1;
			return false;
		}
		VoicePtn voicePtn = dicVoicePtn[_main][num][key];
		Dictionary<int, Dictionary<int, VoiceInfo>> dictionary = dicVoiceIntos[_main][num];
		if (dictionary.Count == 0)
		{
			flags.voice.playVoices[_main] = -1;
			return false;
		}
		List<VoicePtnInfo> list = new List<VoicePtnInfo>();
		for (int i = 0; i < voicePtn.lstInfo.Count; i++)
		{
			if (IsPlayVoicePtn(voicePtn.lstInfo[i], num, _female, _main))
			{
				list.Add(voicePtn.lstInfo[i]);
			}
		}
		List<VoiceSelect> pLayNumVoiceList = GetPLayNumVoiceList(list, dictionary, num, _female, _main);
		if (pLayNumVoiceList.Count == 0)
		{
			for (int j = 0; j < list.Count; j++)
			{
				int stateSelect = GetStateSelect(list[j].state, _main);
				for (int k = 0; k < list[j].lstVoice.Count; k++)
				{
					int key2 = list[j].lstVoice[k];
					if (dictionary[stateSelect].ContainsKey(key2) && !dictionary[stateSelect][key2].isOneShot)
					{
						dictionary[stateSelect][key2].isPlay = false;
					}
				}
				for (int l = 0; l < list[j].lstSecondVoice.Count; l++)
				{
					int key3 = list[j].lstSecondVoice[l];
					if (dictionary[stateSelect].ContainsKey(key3) && !dictionary[stateSelect][key3].isOneShot)
					{
						dictionary[stateSelect][key3].isPlay = false;
					}
				}
			}
			pLayNumVoiceList = GetPLayNumVoiceList(list, dictionary, num, _female, _main);
			if (pLayNumVoiceList.Count == 0)
			{
				flags.voice.playVoices[_main] = -1;
				return false;
			}
		}
		for (int m = 0; m < pLayNumVoiceList.Count; m++)
		{
			VoiceSelect voiceSelect = pLayNumVoiceList[m];
			voiceSelect.priority = dictionary[voiceSelect.state][voiceSelect.playnum].priority;
		}
		pLayNumVoiceList = (from p in pLayNumVoiceList
			orderby Guid.NewGuid()
			orderby p.priority
			select p).ToList();
		nowVoices[_main].voiceInfo = dictionary[pLayNumVoiceList[0].state][pLayNumVoiceList[0].playnum];
		Utils.Voice.Setting s = new Utils.Voice.Setting
		{
			no = flags.lstHeroine[_main].voiceNo,
			assetBundleName = nowVoices[_main].voiceInfo.pathAsset,
			assetName = nowVoices[_main].voiceInfo.nameFile,
			pitch = flags.lstHeroine[_main].voicePitch,
			voiceTrans = flags.transVoiceMouth[_main]
		};
		_female.SetLipSync(Utils.Voice.OncePlayChara(s));
		flags.hashAssetBundle.Add(nowVoices[_main].voiceInfo.pathAsset);
		nowVoices[_main].state = VoiceKind.voice;
		nowVoices[_main].notOverWrite = nowVoices[_main].voiceInfo.notOverwrite;
		nowVoices[_main].voiceInfo.isPlay = true;
		flags.voice.playVoices[_main] = -1;
		flags.voice.playShorts[_main] = -1;
		faceLists[_main].SetFace(nowVoices[_main].voiceInfo.face, _female, 1, num);
		flags.voice.eyenecks[_main] = nowVoices[_main].voiceInfo.eyeneck;
		flags.voice.SetAibuIdleTime();
		flags.voice.SetHoushiIdleTime();
		flags.voice.SetSonyuIdleTime();
		if (pLayNumVoiceList[0].infoPtn.link.isLilnk)
		{
			linkUseVoicePtn[_main] = voicePtn;
			nowVoices[_main].link = pLayNumVoiceList[0].infoPtn.link;
			nowVoices[_main].link.action = num;
		}
		else
		{
			linkUseVoicePtn[_main] = null;
			nowVoices[_main].link = new LinkInfo();
		}
		linkUseBreathPtn[_main] = null;
		return true;
	}

	private bool VoiceLinkProc(ChaControl _female, int _main)
	{
		if (!nowVoices[_main].link.isLilnk || nowVoices[_main].link.kind != VoiceKind.voice)
		{
			return false;
		}
		if (nowVoices[_main].link.condition == -1)
		{
			if (Manager.Voice.IsPlay(flags.transVoiceMouth[_main]))
			{
				return false;
			}
			if (nowVoices[_main].link.etc == 1)
			{
				flags.voice.gentles[_main] = true;
			}
		}
		else if (nowVoices[_main].link.condition == 0)
		{
			nowVoices[_main].link.time += Time.deltaTime;
			if (nowVoices[_main].link.time < (float)nowVoices[_main].link.etc)
			{
				return false;
			}
		}
		else if (nowVoices[_main].link.condition == 1 && flags.gaugeFemale <= 50f)
		{
			return false;
		}
		int action = nowVoices[_main].link.action;
		Dictionary<int, Dictionary<int, VoiceInfo>> dictionary = dicVoiceIntos[_main][action];
		int num = -1;
		int num2 = 0;
		if (linkUseVoicePtn[_main] != null)
		{
			for (int i = 0; i < linkUseVoicePtn[_main].lstInfo.Count; i++)
			{
				if (!linkUseVoicePtn[_main].lstInfo[i].lstConditions.Contains(nowVoices[_main].link.no) || !IsBreathAnimationList(linkUseVoicePtn[_main].lstInfo[i].lstAnimID, flags.nowAnimationInfo.id))
				{
					continue;
				}
				num2 = GetStateSelect(linkUseVoicePtn[_main].lstInfo[i].state, _main);
				num = GetVoiceLinkPlayNumEX(linkUseVoicePtn[_main].lstInfo[i], dictionary, _female, action, num2, _main);
				if (num != -1)
				{
					break;
				}
				for (int j = 0; j < linkUseVoicePtn[_main].lstInfo[i].lstVoice.Count; j++)
				{
					int key = linkUseVoicePtn[_main].lstInfo[i].lstVoice[j];
					if (dictionary[num2].ContainsKey(key) && !dictionary[num2][key].isOneShot)
					{
						dictionary[num2][key].isPlay = false;
					}
				}
				num = GetVoiceLinkPlayNumEX(linkUseVoicePtn[_main].lstInfo[i], dictionary, _female, action, num2, _main);
				break;
			}
		}
		if (num == -1)
		{
			GlobalMethod.DebugLog("link先取得エラー", 1);
			nowVoices[_main].link = new LinkInfo();
			linkUseVoicePtn[_main] = null;
			return false;
		}
		nowVoices[_main].voiceInfo = dictionary[num2][num];
		Utils.Voice.Setting s = new Utils.Voice.Setting
		{
			no = flags.lstHeroine[_main].voiceNo,
			assetBundleName = nowVoices[_main].voiceInfo.pathAsset,
			assetName = nowVoices[_main].voiceInfo.nameFile,
			pitch = flags.lstHeroine[_main].voicePitch,
			voiceTrans = flags.transVoiceMouth[_main]
		};
		_female.SetLipSync(Utils.Voice.OncePlayChara(s));
		flags.hashAssetBundle.Add(nowVoices[_main].voiceInfo.pathAsset);
		nowVoices[_main].state = VoiceKind.voice;
		nowVoices[_main].notOverWrite = nowVoices[_main].voiceInfo.notOverwrite;
		nowVoices[_main].voiceInfo.isPlay = true;
		flags.voice.playShorts[_main] = -1;
		faceLists[_main].SetFace(nowVoices[_main].voiceInfo.face, _female, 1, nowVoices[_main].link.action);
		flags.voice.eyenecks[_main] = nowVoices[_main].voiceInfo.eyeneck;
		flags.voice.SetAibuIdleTime();
		flags.voice.SetHoushiIdleTime();
		flags.voice.SetSonyuIdleTime();
		nowVoices[_main].link.time = 0f;
		nowVoices[_main].link = new LinkInfo();
		linkUseVoicePtn[_main] = null;
		return true;
	}

	private int GetVoiceLinkPlayNum(List<int> _info, Dictionary<int, Dictionary<int, VoiceInfo>> _voice, int _state, int _main)
	{
		List<VoiceSelect> list = new List<VoiceSelect>();
		for (int i = 0; i < _info.Count; i++)
		{
			if (_voice[_state].ContainsKey(_info[i]) && !_voice[_state][_info[i]].isPlay)
			{
				list.Add(new VoiceSelect
				{
					playnum = _info[i],
					priority = _voice[_state][_info[i]].priority
				});
			}
		}
		if (list.Count == 0)
		{
			return -1;
		}
		list = (from p in list
			orderby Guid.NewGuid()
			orderby p.priority
			select p).ToList();
		return list[0].playnum;
	}

	private int GetVoiceLinkPlayNumEX(VoicePtnInfo _vPtnInfo, Dictionary<int, Dictionary<int, VoiceInfo>> _voice, ChaControl _female, int _action, int _state, int _main)
	{
		Func<bool[], int, bool>[] array = new Func<bool[], int, bool>[9] { IsVoiceConditionsStart, IsVoiceConditionsAibu, IsVoiceConditionsHoushi, IsVoiceConditionsSonyu, IsVoiceConditionsMasturbation, IsVoiceConditionsPeeping, IsVoiceConditionsLesbian, IsVoiceConditions3PHoushi, IsVoiceConditions3PSonyu };
		List<VoiceSelect> list = new List<VoiceSelect>();
		List<int> lstVoice = _vPtnInfo.lstVoice;
		Dictionary<int, VoiceInfo> dictionary = _voice[_state];
		for (int i = 0; i < lstVoice.Count; i++)
		{
			int num = lstVoice[i];
			if (dictionary.ContainsKey(num) && !dictionary[num].isPlay && array[_action](dictionary[num].isPlayConditions, _main))
			{
				list.Add(new VoiceSelect
				{
					playnum = num,
					priority = dictionary[num].priority
				});
			}
		}
		if (IsPlayVoiceSecondPtn(_vPtnInfo, _action, _female, _main))
		{
			for (int j = 0; j < _vPtnInfo.lstSecondVoice.Count; j++)
			{
				int num2 = _vPtnInfo.lstSecondVoice[j];
				if (dictionary.ContainsKey(num2) && !dictionary[num2].isPlay && array[_action](dictionary[num2].isPlayConditions, _main))
				{
					list.Add(new VoiceSelect
					{
						playnum = num2,
						priority = dictionary[num2].priority
					});
				}
			}
		}
		if (list.Count == 0)
		{
			return -1;
		}
		list = (from p in list
			orderby Guid.NewGuid()
			orderby p.priority
			select p).ToList();
		return list[0].playnum;
	}

	private List<VoiceSelect> GetPLayNumVoiceList(List<VoicePtnInfo> _lstPlayInfo, Dictionary<int, Dictionary<int, VoiceInfo>> _dicUseVoiceInfo, int _action, ChaControl _female, int _main)
	{
		Func<bool[], int, bool>[] array = new Func<bool[], int, bool>[9] { IsVoiceConditionsStart, IsVoiceConditionsAibu, IsVoiceConditionsHoushi, IsVoiceConditionsSonyu, IsVoiceConditionsMasturbation, IsVoiceConditionsPeeping, IsVoiceConditionsLesbian, IsVoiceConditions3PHoushi, IsVoiceConditions3PSonyu };
		List<VoiceSelect> list = new List<VoiceSelect>();
		for (int i = 0; i < _lstPlayInfo.Count; i++)
		{
			int stateSelect = GetStateSelect(_lstPlayInfo[i].state, _main);
			for (int j = 0; j < _lstPlayInfo[i].lstVoice.Count; j++)
			{
				if (_dicUseVoiceInfo[stateSelect].ContainsKey(_lstPlayInfo[i].lstVoice[j]) && !_dicUseVoiceInfo[stateSelect][_lstPlayInfo[i].lstVoice[j]].isPlay && array[_action](_dicUseVoiceInfo[stateSelect][_lstPlayInfo[i].lstVoice[j]].isPlayConditions, _main))
				{
					VoiceSelect voiceSelect = new VoiceSelect();
					voiceSelect.state = stateSelect;
					voiceSelect.playnum = _lstPlayInfo[i].lstVoice[j];
					voiceSelect.infoPtn = _lstPlayInfo[i];
					list.Add(voiceSelect);
				}
			}
			if (!IsPlayVoiceSecondPtn(_lstPlayInfo[i], _action, _female, _main))
			{
				continue;
			}
			for (int k = 0; k < _lstPlayInfo[i].lstSecondVoice.Count; k++)
			{
				if (_dicUseVoiceInfo[stateSelect].ContainsKey(_lstPlayInfo[i].lstSecondVoice[k]) && !_dicUseVoiceInfo[stateSelect][_lstPlayInfo[i].lstSecondVoice[k]].isPlay && array[_action](_dicUseVoiceInfo[stateSelect][_lstPlayInfo[i].lstSecondVoice[k]].isPlayConditions, _main))
				{
					VoiceSelect voiceSelect2 = new VoiceSelect();
					voiceSelect2.state = stateSelect;
					voiceSelect2.playnum = _lstPlayInfo[i].lstSecondVoice[k];
					voiceSelect2.infoPtn = _lstPlayInfo[i];
					list.Add(voiceSelect2);
				}
			}
		}
		return list;
	}

	private bool IsVoiceConditionsStart(bool[] _conditions, int _main)
	{
		for (int i = 0; i < _conditions.Length; i++)
		{
			if (!_conditions[i])
			{
				continue;
			}
			switch (i)
			{
			case 0:
				if (flags.count.isHoushiPlay)
				{
					return false;
				}
				break;
			case 1:
				if (flags.selectAnimationListInfo == null || (flags.selectAnimationListInfo != null && (flags.selectAnimationListInfo.mode != HFlag.EMode.houshi || flags.selectAnimationListInfo.sysTaii != 1)))
				{
					return false;
				}
				break;
			case 2:
				if (flags.selectAnimationListInfo == null || (flags.selectAnimationListInfo != null && (flags.selectAnimationListInfo.mode != HFlag.EMode.houshi || flags.selectAnimationListInfo.sysTaii != 2)))
				{
					return false;
				}
				break;
			case 3:
				if (flags.count.sonyuKokanPlay + flags.count.sonyuAnalPlay != 0)
				{
					return false;
				}
				break;
			case 4:
				if (flags.nowAnimationInfo.isFemaleInitiative || (flags.selectAnimationListInfo != null && flags.selectAnimationListInfo.isFemaleInitiative))
				{
					return false;
				}
				break;
			case 5:
				if (flags.mode != HFlag.EMode.sonyu)
				{
					return false;
				}
				if (flags.finish == HFlag.FinishKind.none || flags.finish == HFlag.FinishKind.orgS || flags.finish == HFlag.FinishKind.orgW)
				{
					return false;
				}
				break;
			}
		}
		return true;
	}

	private bool IsVoiceConditionsAibu(bool[] _conditions, int _main)
	{
		for (int i = 0; i < _conditions.Length; i++)
		{
			if (_conditions[i])
			{
				switch (i)
				{
				}
			}
		}
		return true;
	}

	private bool IsVoiceConditionsHoushi(bool[] _conditions, int _main)
	{
		for (int i = 0; i < _conditions.Length; i++)
		{
			if (!_conditions[i])
			{
				continue;
			}
			switch (i)
			{
			case 0:
				if (flags.count.isHoushiPlay)
				{
					return false;
				}
				break;
			}
		}
		return true;
	}

	private bool IsVoiceConditionsSonyu(bool[] _conditions, int _main)
	{
		for (int i = 0; i < _conditions.Length; i++)
		{
			if (!_conditions[i])
			{
				continue;
			}
			switch (i)
			{
			case 0:
				if (flags.count.sonyuKokanPlay + flags.count.sonyuAnalPlay != 0)
				{
					return false;
				}
				break;
			case 1:
				if (!flags.lstHeroine[0].isVirgin || flags.count.isInsertKokanVoiceCondition)
				{
					return false;
				}
				break;
			case 2:
				if (!flags.lstHeroine[0].isAnalVirgin || flags.count.isInsertAnalVoiceCondition)
				{
					return false;
				}
				break;
			case 3:
				if (!flags.isInsertOK[_main])
				{
					return false;
				}
				break;
			case 4:
				if (flags.count.sonyuOrg != 0)
				{
					return false;
				}
				break;
			case 5:
				if (flags.count.sonyuAnalOrg != 0)
				{
					return false;
				}
				break;
			case 6:
				if (flags.count.sonyuOrg != 0 || flags.count.sonyuAnalOrg != 1)
				{
					return false;
				}
				break;
			case 7:
				if (flags.count.sonyuAnalOutside != 0 || flags.count.sonyuAnalInside != 1)
				{
					return false;
				}
				break;
			case 8:
				if (flags.count.sonyuOrg != 1)
				{
					return false;
				}
				break;
			case 9:
				if (flags.count.sonyuAnalOrg != 1)
				{
					return false;
				}
				break;
			case 10:
				if (flags.isAnalPlay || HFlag.GetMenstruation(flags.lstHeroine[0].MenstruationDay) == HFlag.MenstruationType.安全日)
				{
					return false;
				}
				break;
			case 11:
				if (flags.isCondom)
				{
					return false;
				}
				break;
			case 12:
				if (!flags.isAnalPlay)
				{
					return false;
				}
				break;
			case 13:
				if (flags.lstHeroine[0].weakPoint != 3)
				{
					return false;
				}
				break;
			case 14:
				if (!flags.lstHeroine[0].isGirlfriend)
				{
					return false;
				}
				break;
			}
		}
		return true;
	}

	private bool IsVoiceConditionsMasturbation(bool[] _conditions, int _main)
	{
		for (int i = 0; i < _conditions.Length; i++)
		{
			if (_conditions[i])
			{
				switch (i)
				{
				}
			}
		}
		return true;
	}

	private bool IsVoiceConditionsPeeping(bool[] _conditions, int _main)
	{
		for (int i = 0; i < _conditions.Length; i++)
		{
			if (_conditions[i])
			{
				switch (i)
				{
				}
			}
		}
		return true;
	}

	private bool IsVoiceConditionsLesbian(bool[] _conditions, int _main)
	{
		for (int i = 0; i < _conditions.Length; i++)
		{
			if (_conditions[i])
			{
				switch (i)
				{
				}
			}
		}
		return true;
	}

	private bool IsVoiceConditions3PHoushi(bool[] _conditions, int _main)
	{
		for (int i = 0; i < _conditions.Length; i++)
		{
			if (!_conditions[i])
			{
				continue;
			}
			switch (i)
			{
			case 0:
				if (flags.count.isHoushiPlay)
				{
					return false;
				}
				break;
			}
		}
		return true;
	}

	private bool IsVoiceConditions3PSonyu(bool[] _conditions, int _main)
	{
		for (int i = 0; i < _conditions.Length; i++)
		{
			if (!_conditions[i])
			{
				continue;
			}
			switch (i)
			{
			case 0:
				if (flags.count.sonyuKokanPlay + flags.count.sonyuAnalPlay != 0)
				{
					return false;
				}
				break;
			case 1:
				if (!flags.lstHeroine[0].isVirgin || flags.count.isInsertKokanVoiceCondition)
				{
					return false;
				}
				break;
			case 2:
				if (!flags.lstHeroine[0].isAnalVirgin || flags.count.isInsertAnalVoiceCondition)
				{
					return false;
				}
				break;
			case 3:
				if (!flags.isInsertOK[_main])
				{
					return false;
				}
				break;
			case 4:
				if (flags.count.sonyuOrg != 0)
				{
					return false;
				}
				break;
			case 5:
				if (flags.count.sonyuAnalOrg != 0)
				{
					return false;
				}
				break;
			case 6:
				if (flags.count.sonyuOrg != 0 || flags.count.sonyuAnalOrg != 1)
				{
					return false;
				}
				break;
			case 7:
				if (flags.count.sonyuAnalOutside != 0 || flags.count.sonyuAnalInside != 1)
				{
					return false;
				}
				break;
			case 8:
				if (flags.count.sonyuOrg != 1)
				{
					return false;
				}
				break;
			case 9:
				if (flags.count.sonyuAnalOrg != 1)
				{
					return false;
				}
				break;
			case 10:
				if (flags.isAnalPlay || HFlag.GetMenstruation(flags.lstHeroine[0].MenstruationDay) == HFlag.MenstruationType.安全日)
				{
					return false;
				}
				break;
			case 11:
				if (flags.isCondom)
				{
					return false;
				}
				break;
			case 12:
				if (!flags.isAnalPlay)
				{
					return false;
				}
				break;
			case 13:
				if (flags.lstHeroine[0].weakPoint != 3)
				{
					return false;
				}
				break;
			case 14:
				if (!flags.lstHeroine[0].isGirlfriend)
				{
					return false;
				}
				break;
			}
		}
		return true;
	}

	private bool IsPlayVoicePtn(VoicePtnInfo _lst, int _action, ChaControl _female, int _main)
	{
		if (_action != 0 || (!_lst.lstConditions.Contains(20) && !_lst.lstConditions.Contains(21)))
		{
			if (!IsAnimationList(_lst.lstAnimID, flags.nowAnimationInfo.id))
			{
				return false;
			}
		}
		else if (_lst.lstConditions.Contains(20))
		{
			if (flags.selectAnimationListInfo == null)
			{
				return false;
			}
			if (!IsAnimationList(_lst.lstAnimID, flags.selectAnimationListInfo.id))
			{
				return false;
			}
		}
		else if (_lst.lstConditions.Contains(21))
		{
			if (flags.selectAnimationListInfo == null)
			{
				return false;
			}
			if (IsAnimationList(_lst.lstAnimID, flags.selectAnimationListInfo.id))
			{
				return false;
			}
		}
		if (!(new VoicePtnConditionDelegate[9] { IsPtnConditionsStart, IsPtnConditionsAibu, IsPtnConditionsHoushi, IsPtnConditionsSonyu, IsPtnConditionsMasturbation, IsPtnConditionsPeeping, IsPtnConditionsLesbian, IsPtnConditions3PHoushi, IsPtnConditions3PSonyu })[_action](_lst.lstConditions, _female, _main))
		{
			return false;
		}
		return true;
	}

	private bool IsAnimationList(List<int> _lstAnimList, int _idNow)
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

	private bool IsPtnConditionsStart(List<int> _lstConditions, ChaControl _female, int _main)
	{
		for (int i = 0; i < _lstConditions.Count; i++)
		{
			if (_lstConditions[i] >= 90)
			{
				return false;
			}
			switch (_lstConditions[i])
			{
			case 0:
				if (flags.isInsertOK[_main])
				{
					return false;
				}
				break;
			case 1:
				if (HFlag.GetMenstruation(flags.lstHeroine[_main].MenstruationDay) != HFlag.MenstruationType.危険日)
				{
					return false;
				}
				break;
			case 2:
				if (HFlag.GetMenstruation(flags.lstHeroine[_main].MenstruationDay) != HFlag.MenstruationType.安全日)
				{
					return false;
				}
				break;
			case 3:
				if (!flags.lstHeroine[_main].isGirlfriend || flags.lstHeroine[_main].HExperience != Heroine.HExperienceKind.淫乱)
				{
					return false;
				}
				break;
			case 4:
				if (flags.mode != HFlag.EMode.aibu || flags.selectAnimationListInfo == null || (flags.selectAnimationListInfo != null && flags.selectAnimationListInfo.sysTaii == 1))
				{
					return false;
				}
				break;
			case 5:
				if (flags.mode != HFlag.EMode.aibu || flags.selectAnimationListInfo == null || (flags.selectAnimationListInfo != null && flags.selectAnimationListInfo.sysTaii == 0))
				{
					return false;
				}
				break;
			case 6:
				if (flags.mode != HFlag.EMode.houshi || flags.selectAnimationListInfo == null || (flags.selectAnimationListInfo != null && flags.selectAnimationListInfo.kindHoushi != 0))
				{
					return false;
				}
				break;
			case 7:
				if (flags.mode != HFlag.EMode.houshi || flags.selectAnimationListInfo == null || (flags.selectAnimationListInfo != null && flags.selectAnimationListInfo.kindHoushi != 1))
				{
					return false;
				}
				break;
			case 8:
				if (flags.mode != HFlag.EMode.houshi || flags.selectAnimationListInfo == null || (flags.selectAnimationListInfo != null && flags.selectAnimationListInfo.kindHoushi != 2))
				{
					return false;
				}
				break;
			case 9:
				if (flags.mode != HFlag.EMode.sonyu || flags.selectAnimationListInfo == null || (flags.selectAnimationListInfo != null && !flags.selectAnimationListInfo.isFemaleInitiative))
				{
					return false;
				}
				break;
			case 10:
				if (flags.mode != HFlag.EMode.sonyu || flags.nowAnimationInfo.sysTaii != 0)
				{
					return false;
				}
				break;
			case 11:
				if (flags.mode != HFlag.EMode.sonyu || flags.nowAnimationInfo.sysTaii != 1)
				{
					return false;
				}
				break;
			case 12:
				if (flags.mode != HFlag.EMode.sonyu || flags.nowAnimationInfo.sysTaii != 2)
				{
					return false;
				}
				break;
			case 13:
				if (!flags.isInsertOK[_main])
				{
					return false;
				}
				break;
			case 14:
				if (flags.mode != HFlag.EMode.aibu || flags.nowAnimationInfo.sysTaii == 1)
				{
					return false;
				}
				break;
			case 15:
				if (flags.mode != HFlag.EMode.aibu || flags.nowAnimationInfo.sysTaii == 0)
				{
					return false;
				}
				break;
			case 16:
				if ((flags.mode != HFlag.EMode.sonyu && flags.mode != HFlag.EMode.sonyu3P) || !flags.nowAnimationInfo.isFemaleInitiative)
				{
					return false;
				}
				break;
			case 17:
				if (flags.nowAnimationInfo.numMainVoiceID != -1)
				{
					if (_main == 0 && flags.nowAnimationInfo.numMainVoiceID != 0)
					{
						return false;
					}
					if (_main == 1 && flags.nowAnimationInfo.numSubVoiceID != 0)
					{
						return false;
					}
				}
				break;
			case 18:
				if (flags.nowAnimationInfo.numMainVoiceID != -1)
				{
					if (_main == 0 && flags.nowAnimationInfo.numMainVoiceID != 1)
					{
						return false;
					}
					if (_main == 1 && flags.nowAnimationInfo.numSubVoiceID != 1)
					{
						return false;
					}
				}
				break;
			case 19:
				if ((flags.mode != HFlag.EMode.sonyu && flags.mode != HFlag.EMode.sonyu3P) || flags.nowAnimationInfo.isFemaleInitiative)
				{
					return false;
				}
				break;
			}
		}
		return true;
	}

	private bool IsPtnConditionsAibu(List<int> _lstConditions, ChaControl _female, int _main)
	{
		for (int i = 0; i < _lstConditions.Count; i++)
		{
			if (_lstConditions[i] >= 90)
			{
				return false;
			}
			switch (_lstConditions[i])
			{
			case 0:
				if (flags.lstHeroine[_main].weakPoint == 0)
				{
					return false;
				}
				break;
			case 1:
				if (flags.lstHeroine[_main].weakPoint != 0)
				{
					return false;
				}
				break;
			case 2:
				if (flags.lstHeroine[_main].weakPoint == 1)
				{
					return false;
				}
				break;
			case 3:
				if (flags.lstHeroine[_main].weakPoint != 1)
				{
					return false;
				}
				break;
			case 4:
				if (flags.lstHeroine[_main].weakPoint == 2)
				{
					return false;
				}
				break;
			case 5:
				if (flags.lstHeroine[_main].weakPoint != 2)
				{
					return false;
				}
				break;
			case 6:
				if (flags.lstHeroine[_main].weakPoint == 3)
				{
					return false;
				}
				break;
			case 7:
				if (flags.lstHeroine[_main].weakPoint != 3)
				{
					return false;
				}
				break;
			case 8:
				if (flags.lstHeroine[_main].weakPoint == 4)
				{
					return false;
				}
				break;
			case 9:
				if (flags.lstHeroine[_main].weakPoint != 4)
				{
					return false;
				}
				break;
			case 10:
				if (flags.lstHeroine[_main].weakPoint == 5)
				{
					return false;
				}
				break;
			case 11:
				if (flags.lstHeroine[_main].weakPoint != 5)
				{
					return false;
				}
				break;
			}
		}
		return true;
	}

	private bool IsPtnConditionsHoushi(List<int> _lstConditions, ChaControl _female, int _main)
	{
		if (flags.mode != HFlag.EMode.houshi)
		{
			return false;
		}
		for (int i = 0; i < _lstConditions.Count; i++)
		{
			if (_lstConditions[i] >= 90)
			{
				return false;
			}
			switch (_lstConditions[i])
			{
			case 0:
				if (flags.nowAnimationInfo.kindHoushi != 0 || flags.nowAnimationInfo.sysTaii != 0)
				{
					return false;
				}
				break;
			case 1:
				if (flags.nowAnimationInfo.kindHoushi != 1 || flags.nowAnimationInfo.sysTaii != 1)
				{
					return false;
				}
				break;
			case 2:
				if (flags.nowAnimationInfo.kindHoushi != 1 || flags.nowAnimationInfo.sysTaii != 2)
				{
					return false;
				}
				break;
			case 3:
				if ((!flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionW != 0) || (flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionS != 0))
				{
					return false;
				}
				break;
			case 4:
				if ((!flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionW != 1) || (flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionS != 1))
				{
					return false;
				}
				break;
			case 5:
				if ((!flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionW != 2) || (flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionS != 2))
				{
					return false;
				}
				break;
			case 6:
				if ((!flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionW != 3) || (flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionS != 3))
				{
					return false;
				}
				break;
			case 7:
				if ((!flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionW != 4) || (flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionS != 4))
				{
					return false;
				}
				break;
			case 8:
				if ((!flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionW != 5) || (flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionS != 5))
				{
					return false;
				}
				break;
			case 9:
				if (flags.nowAnimationInfo.kindHoushi != 2 || flags.nowAnimationInfo.sysTaii != 0 || _female.GetBustCategory() != 0)
				{
					return false;
				}
				break;
			case 10:
				if (flags.nowAnimationInfo.kindHoushi != 2 || flags.nowAnimationInfo.sysTaii != 0 || _female.GetBustCategory() != 1)
				{
					return false;
				}
				break;
			case 11:
				if (flags.nowAnimationInfo.kindHoushi != 2 || flags.nowAnimationInfo.sysTaii != 0 || _female.GetBustCategory() != 2)
				{
					return false;
				}
				break;
			case 12:
				if (flags.nowAnimationInfo.kindHoushi != 2 || flags.nowAnimationInfo.sysTaii != 1 || _female.GetBustCategory() != 0)
				{
					return false;
				}
				break;
			case 13:
				if (flags.nowAnimationInfo.kindHoushi != 2 || flags.nowAnimationInfo.sysTaii != 1 || _female.GetBustCategory() != 1)
				{
					return false;
				}
				break;
			case 14:
				if (flags.nowAnimationInfo.kindHoushi != 2 || flags.nowAnimationInfo.sysTaii != 1 || _female.GetBustCategory() != 2)
				{
					return false;
				}
				break;
			case 15:
				if (flags.nowAnimationInfo.kindHoushi != 2 || flags.nowAnimationInfo.sysTaii != 2 || _female.GetBustCategory() != 0)
				{
					return false;
				}
				break;
			case 16:
				if (flags.nowAnimationInfo.kindHoushi != 2 || flags.nowAnimationInfo.sysTaii != 2 || _female.GetBustCategory() != 1)
				{
					return false;
				}
				break;
			case 17:
				if (flags.nowAnimationInfo.kindHoushi != 2 || flags.nowAnimationInfo.sysTaii != 2 || _female.GetBustCategory() != 2)
				{
					return false;
				}
				break;
			case 18:
				if (!flags.nowAnimationInfo.isSplash)
				{
					return false;
				}
				break;
			case 19:
				if (flags.nowAnimationInfo.sysTaii != 0)
				{
					return false;
				}
				break;
			case 20:
				if (flags.nowAnimationInfo.sysTaii != 1)
				{
					return false;
				}
				break;
			case 21:
				if (flags.nowAnimationInfo.sysTaii != 2)
				{
					return false;
				}
				break;
			case 22:
				if (flags.nowAnimationInfo.isSplash)
				{
					return false;
				}
				break;
			case 23:
				if ((!flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionW != 6) || (flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionS != 6))
				{
					return false;
				}
				break;
			case 24:
				if ((!flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionW != 7) || (flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionS != 7))
				{
					return false;
				}
				break;
			}
		}
		return true;
	}

	private bool IsPtnConditionsSonyu(List<int> _lstConditions, ChaControl _female, int _main)
	{
		if (flags.mode != HFlag.EMode.sonyu)
		{
			return false;
		}
		for (int i = 0; i < _lstConditions.Count; i++)
		{
			if (_lstConditions[i] >= 90)
			{
				return false;
			}
			switch (_lstConditions[i])
			{
			case 0:
				if (flags.isAnalPlay)
				{
					return false;
				}
				break;
			case 1:
				if (!flags.isAnalPlay)
				{
					return false;
				}
				break;
			case 2:
				if (!hand.IsAction())
				{
					return false;
				}
				break;
			case 3:
			{
				bool flag = false;
				for (int j = 0; j < 3; j++)
				{
					if (hand.GetUseItemStickArea(j) != HandCtrl.AibuColliderKind.anal)
					{
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					return false;
				}
				if (flags.lstHeroine[_main].hAreaExps[3] >= 100f)
				{
					return false;
				}
				break;
			}
			case 4:
			{
				int useAreaItemActive4 = hand.GetUseAreaItemActive();
				HandCtrl.AibuColliderKind useItemStickArea2 = hand.GetUseItemStickArea(useAreaItemActive4);
				if (useItemStickArea2 == HandCtrl.AibuColliderKind.none)
				{
					return false;
				}
				if (flags.lstHeroine[_main].weakPoint == -1)
				{
					return false;
				}
				if (flags.lstHeroine[_main].weakPoint == 0 && useItemStickArea2 != HandCtrl.AibuColliderKind.mouth)
				{
					return false;
				}
				if (flags.lstHeroine[_main].weakPoint == 1 && ((useItemStickArea2 != HandCtrl.AibuColliderKind.muneL && useItemStickArea2 != HandCtrl.AibuColliderKind.muneR) || hand.GetUseItemStickObjectID(useAreaItemActive4) != 0))
				{
					return false;
				}
				if (flags.lstHeroine[_main].weakPoint == 2 && useItemStickArea2 != HandCtrl.AibuColliderKind.kokan)
				{
					return false;
				}
				if (flags.lstHeroine[_main].weakPoint == 3 && useItemStickArea2 != HandCtrl.AibuColliderKind.anal)
				{
					return false;
				}
				if (flags.lstHeroine[_main].weakPoint == 4 && useItemStickArea2 != HandCtrl.AibuColliderKind.siriL && useItemStickArea2 != HandCtrl.AibuColliderKind.siriR)
				{
					return false;
				}
				if (flags.lstHeroine[_main].weakPoint == 5 && ((useItemStickArea2 != HandCtrl.AibuColliderKind.muneL && useItemStickArea2 != HandCtrl.AibuColliderKind.muneR) || hand.GetUseItemStickObjectID(useAreaItemActive4) == 0))
				{
					return false;
				}
				break;
			}
			case 5:
				if (!hand.IsKissAction())
				{
					return false;
				}
				break;
			case 6:
				if (hand.IsKissAction())
				{
					return false;
				}
				break;
			case 7:
				if (!flags.isCondom)
				{
					return false;
				}
				break;
			case 8:
				if (flags.isCondom)
				{
					return false;
				}
				break;
			case 9:
				if (HFlag.GetMenstruation(flags.lstHeroine[_main].MenstruationDay) != HFlag.MenstruationType.危険日)
				{
					return false;
				}
				break;
			case 10:
				if (HFlag.GetMenstruation(flags.lstHeroine[_main].MenstruationDay) != HFlag.MenstruationType.安全日)
				{
					return false;
				}
				break;
			case 11:
				if (hand.IsAction())
				{
					return false;
				}
				break;
			case 12:
			{
				bool flag2 = false;
				for (int k = 0; k < 3; k++)
				{
					if (hand.GetUseItemStickArea(k) != HandCtrl.AibuColliderKind.anal)
					{
						flag2 = true;
						break;
					}
				}
				if (!flag2)
				{
					return false;
				}
				if (flags.lstHeroine[_main].hAreaExps[3] < 100f)
				{
					return false;
				}
				break;
			}
			case 13:
			{
				int useAreaItemActive2 = hand.GetUseAreaItemActive();
				HandCtrl.AibuColliderKind useItemStickArea = hand.GetUseItemStickArea(useAreaItemActive2);
				if (useItemStickArea == HandCtrl.AibuColliderKind.none)
				{
					return false;
				}
				if (flags.lstHeroine[_main].weakPoint != -1)
				{
					if (flags.lstHeroine[_main].weakPoint == 0 && useItemStickArea == HandCtrl.AibuColliderKind.mouth)
					{
						return false;
					}
					if (flags.lstHeroine[_main].weakPoint == 1 && (useItemStickArea == HandCtrl.AibuColliderKind.muneL || useItemStickArea == HandCtrl.AibuColliderKind.muneR) && hand.GetUseItemStickObjectID(useAreaItemActive2) == 0)
					{
						return false;
					}
					if (flags.lstHeroine[_main].weakPoint == 2 && useItemStickArea == HandCtrl.AibuColliderKind.kokan)
					{
						return false;
					}
					if (flags.lstHeroine[_main].weakPoint == 3 && useItemStickArea == HandCtrl.AibuColliderKind.anal)
					{
						return false;
					}
					if (flags.lstHeroine[_main].weakPoint == 4 && (useItemStickArea == HandCtrl.AibuColliderKind.siriL || useItemStickArea == HandCtrl.AibuColliderKind.siriR))
					{
						return false;
					}
					if (flags.lstHeroine[_main].weakPoint == 5 && (useItemStickArea == HandCtrl.AibuColliderKind.muneL || useItemStickArea == HandCtrl.AibuColliderKind.muneR) && hand.GetUseItemStickObjectID(useAreaItemActive2) != 0)
					{
						return false;
					}
				}
				break;
			}
			case 14:
				if (!flags.lstHeroine[_main].isVirgin || flags.count.isInsertKokan)
				{
					return false;
				}
				break;
			case 15:
				if (flags.lstHeroine[_main].isVirgin && !flags.count.isInsertKokan)
				{
					return false;
				}
				break;
			case 16:
				if (!flags.lstHeroine[_main].isAnalVirgin || flags.count.isInsertAnal)
				{
					return false;
				}
				break;
			case 17:
				if (flags.lstHeroine[_main].isAnalVirgin && !flags.count.isInsertAnal)
				{
					return false;
				}
				break;
			case 18:
			{
				if (!hand.IsAction())
				{
					return false;
				}
				int useAreaItemActive3 = hand.GetUseAreaItemActive();
				if (hand.GetUseItemStickArea(useAreaItemActive3) != HandCtrl.AibuColliderKind.anal)
				{
					return false;
				}
				break;
			}
			case 19:
			{
				if (!hand.IsAction())
				{
					return false;
				}
				int useAreaItemActive = hand.GetUseAreaItemActive();
				if (hand.GetUseItemStickArea(useAreaItemActive) == HandCtrl.AibuColliderKind.anal)
				{
					return false;
				}
				break;
			}
			}
		}
		return true;
	}

	private bool IsPtnConditionsMasturbation(List<int> _lstConditions, ChaControl _female, int _main)
	{
		if (flags.mode != HFlag.EMode.masturbation)
		{
			return false;
		}
		for (int i = 0; i < _lstConditions.Count; i++)
		{
			if (_lstConditions[i] >= 90)
			{
				return false;
			}
			switch (_lstConditions[i])
			{
			case 0:
				if (!flags.isMasturbationFound)
				{
					return false;
				}
				break;
			}
		}
		return true;
	}

	private bool IsPtnConditionsPeeping(List<int> _lstConditions, ChaControl _female, int _main)
	{
		if (flags.mode != HFlag.EMode.peeping)
		{
			return false;
		}
		for (int i = 0; i < _lstConditions.Count; i++)
		{
			if (_lstConditions[i] >= 90)
			{
				return false;
			}
			switch (_lstConditions[i])
			{
			}
		}
		return true;
	}

	private bool IsPtnConditionsLesbian(List<int> _lstConditions, ChaControl _female, int _main)
	{
		if (flags.mode != HFlag.EMode.lesbian)
		{
			return false;
		}
		for (int i = 0; i < _lstConditions.Count; i++)
		{
			if (_lstConditions[i] >= 90)
			{
				return false;
			}
			switch (_lstConditions[i])
			{
			case 0:
				if (_main != 0)
				{
					return false;
				}
				break;
			case 1:
				if (_main != 1)
				{
					return false;
				}
				break;
			}
		}
		return true;
	}

	private bool IsPtnConditions3PHoushi(List<int> _lstConditions, ChaControl _female, int _main)
	{
		if (flags.mode != HFlag.EMode.houshi3P)
		{
			return false;
		}
		for (int i = 0; i < _lstConditions.Count; i++)
		{
			if (_lstConditions[i] >= 90)
			{
				return false;
			}
			switch (_lstConditions[i])
			{
			case 0:
				if (flags.nowAnimationInfo.kindHoushi != 0 || flags.nowAnimationInfo.sysTaii != 0)
				{
					return false;
				}
				break;
			case 1:
				if (flags.nowAnimationInfo.kindHoushi != 1 || flags.nowAnimationInfo.sysTaii != 1)
				{
					return false;
				}
				break;
			case 2:
				if (flags.nowAnimationInfo.kindHoushi != 1 || flags.nowAnimationInfo.sysTaii != 2)
				{
					return false;
				}
				break;
			case 3:
				if ((!flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionW != 0) || (flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionS != 0))
				{
					return false;
				}
				break;
			case 4:
				if ((!flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionW != 1) || (flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionS != 1))
				{
					return false;
				}
				break;
			case 5:
				if ((!flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionW != 2) || (flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionS != 2))
				{
					return false;
				}
				break;
			case 6:
				if ((!flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionW != 3) || (flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionS != 3))
				{
					return false;
				}
				break;
			case 7:
				if ((!flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionW != 4) || (flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionS != 4))
				{
					return false;
				}
				break;
			case 8:
				if ((!flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionW != 5) || (flags.voice.loopMotionAorB && flags.nowAnimationInfo.houshiLoopActionS != 5))
				{
					return false;
				}
				break;
			case 9:
				if (flags.nowAnimationInfo.kindHoushi != 2 || flags.nowAnimationInfo.sysTaii != 0 || _female.GetBustCategory() != 0)
				{
					return false;
				}
				break;
			case 10:
				if (flags.nowAnimationInfo.kindHoushi != 2 || flags.nowAnimationInfo.sysTaii != 0 || _female.GetBustCategory() != 1)
				{
					return false;
				}
				break;
			case 11:
				if (flags.nowAnimationInfo.kindHoushi != 2 || flags.nowAnimationInfo.sysTaii != 0 || _female.GetBustCategory() != 2)
				{
					return false;
				}
				break;
			case 12:
				if (flags.nowAnimationInfo.kindHoushi != 2 || flags.nowAnimationInfo.sysTaii != 1 || _female.GetBustCategory() != 0)
				{
					return false;
				}
				break;
			case 13:
				if (flags.nowAnimationInfo.kindHoushi != 2 || flags.nowAnimationInfo.sysTaii != 1 || _female.GetBustCategory() != 1)
				{
					return false;
				}
				break;
			case 14:
				if (flags.nowAnimationInfo.kindHoushi != 2 || flags.nowAnimationInfo.sysTaii != 1 || _female.GetBustCategory() != 2)
				{
					return false;
				}
				break;
			case 15:
				if (flags.nowAnimationInfo.kindHoushi != 2 || flags.nowAnimationInfo.sysTaii != 2 || _female.GetBustCategory() != 0)
				{
					return false;
				}
				break;
			case 16:
				if (flags.nowAnimationInfo.kindHoushi != 2 || flags.nowAnimationInfo.sysTaii != 2 || _female.GetBustCategory() != 1)
				{
					return false;
				}
				break;
			case 17:
				if (flags.nowAnimationInfo.kindHoushi != 2 || flags.nowAnimationInfo.sysTaii != 2 || _female.GetBustCategory() != 2)
				{
					return false;
				}
				break;
			case 18:
				if (!flags.nowAnimationInfo.isSplash)
				{
					return false;
				}
				break;
			case 19:
				if (flags.nowAnimationInfo.sysTaii != 0)
				{
					return false;
				}
				break;
			case 20:
				if (flags.nowAnimationInfo.sysTaii != 1)
				{
					return false;
				}
				break;
			case 21:
				if (flags.nowAnimationInfo.sysTaii != 2)
				{
					return false;
				}
				break;
			case 22:
				if (flags.nowAnimationInfo.isSplash)
				{
					return false;
				}
				break;
			case 23:
				if (flags.nowAnimationInfo.numMainVoiceID != -1)
				{
					if (_main == 0 && flags.nowAnimationInfo.numMainVoiceID != 0)
					{
						return false;
					}
					if (_main == 1 && flags.nowAnimationInfo.numSubVoiceID != 0)
					{
						return false;
					}
				}
				break;
			case 24:
				if (flags.nowAnimationInfo.numMainVoiceID != -1)
				{
					if (_main == 0 && flags.nowAnimationInfo.numMainVoiceID != 1)
					{
						return false;
					}
					if (_main == 1 && flags.nowAnimationInfo.numSubVoiceID != 1)
					{
						return false;
					}
				}
				break;
			}
		}
		return true;
	}

	private bool IsPtnConditions3PSonyu(List<int> _lstConditions, ChaControl _female, int _main)
	{
		if (flags.mode != HFlag.EMode.sonyu3P)
		{
			return false;
		}
		for (int i = 0; i < _lstConditions.Count; i++)
		{
			if (_lstConditions[i] >= 90)
			{
				return false;
			}
			switch (_lstConditions[i])
			{
			case 0:
				if (flags.isAnalPlay)
				{
					return false;
				}
				break;
			case 1:
				if (!flags.isAnalPlay)
				{
					return false;
				}
				break;
			case 2:
				if (!hand.IsAction())
				{
					return false;
				}
				break;
			case 3:
			{
				bool flag = false;
				for (int j = 0; j < 3; j++)
				{
					if (hand.GetUseItemStickArea(j) != HandCtrl.AibuColliderKind.anal)
					{
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					return false;
				}
				if (flags.lstHeroine[_main].hAreaExps[3] >= 100f)
				{
					return false;
				}
				break;
			}
			case 4:
			{
				int useAreaItemActive = hand.GetUseAreaItemActive();
				HandCtrl.AibuColliderKind useItemStickArea = hand.GetUseItemStickArea(useAreaItemActive);
				if (useItemStickArea == HandCtrl.AibuColliderKind.none)
				{
					return false;
				}
				if (flags.lstHeroine[_main].weakPoint == -1)
				{
					return false;
				}
				if (flags.lstHeroine[_main].weakPoint == 0 && useItemStickArea != HandCtrl.AibuColliderKind.mouth)
				{
					return false;
				}
				if (flags.lstHeroine[_main].weakPoint == 1 && ((useItemStickArea != HandCtrl.AibuColliderKind.muneL && useItemStickArea != HandCtrl.AibuColliderKind.muneR) || hand.GetUseItemStickObjectID(useAreaItemActive) != 0))
				{
					return false;
				}
				if (flags.lstHeroine[_main].weakPoint == 2 && useItemStickArea != HandCtrl.AibuColliderKind.kokan)
				{
					return false;
				}
				if (flags.lstHeroine[_main].weakPoint == 3 && useItemStickArea != HandCtrl.AibuColliderKind.anal)
				{
					return false;
				}
				if (flags.lstHeroine[_main].weakPoint == 4 && useItemStickArea != HandCtrl.AibuColliderKind.siriL && useItemStickArea != HandCtrl.AibuColliderKind.siriR)
				{
					return false;
				}
				if (flags.lstHeroine[_main].weakPoint == 5 && ((useItemStickArea != HandCtrl.AibuColliderKind.muneL && useItemStickArea != HandCtrl.AibuColliderKind.muneR) || hand.GetUseItemStickObjectID(useAreaItemActive) == 0))
				{
					return false;
				}
				break;
			}
			case 5:
				if (!hand.IsKissAction())
				{
					return false;
				}
				break;
			case 6:
				if (hand.IsKissAction())
				{
					return false;
				}
				break;
			case 7:
				if (!flags.isCondom)
				{
					return false;
				}
				break;
			case 8:
				if (flags.isCondom)
				{
					return false;
				}
				break;
			case 9:
				if (HFlag.GetMenstruation(flags.lstHeroine[_main].MenstruationDay) != HFlag.MenstruationType.危険日)
				{
					return false;
				}
				break;
			case 10:
				if (HFlag.GetMenstruation(flags.lstHeroine[_main].MenstruationDay) != HFlag.MenstruationType.安全日)
				{
					return false;
				}
				break;
			case 11:
				if (hand.IsAction())
				{
					return false;
				}
				break;
			case 12:
			{
				bool flag2 = false;
				for (int k = 0; k < 3; k++)
				{
					if (hand.GetUseItemStickArea(k) != HandCtrl.AibuColliderKind.anal)
					{
						flag2 = true;
						break;
					}
				}
				if (!flag2)
				{
					return false;
				}
				if (flags.lstHeroine[_main].hAreaExps[3] < 100f)
				{
					return false;
				}
				break;
			}
			case 13:
			{
				int useAreaItemActive3 = hand.GetUseAreaItemActive();
				HandCtrl.AibuColliderKind useItemStickArea2 = hand.GetUseItemStickArea(useAreaItemActive3);
				if (useItemStickArea2 == HandCtrl.AibuColliderKind.none)
				{
					return false;
				}
				if (flags.lstHeroine[_main].weakPoint != -1)
				{
					if (flags.lstHeroine[_main].weakPoint == 0 && useItemStickArea2 == HandCtrl.AibuColliderKind.mouth)
					{
						return false;
					}
					if (flags.lstHeroine[_main].weakPoint == 1 && (useItemStickArea2 == HandCtrl.AibuColliderKind.muneL || useItemStickArea2 == HandCtrl.AibuColliderKind.muneR) && hand.GetUseItemStickObjectID(useAreaItemActive3) == 0)
					{
						return false;
					}
					if (flags.lstHeroine[_main].weakPoint == 2 && useItemStickArea2 == HandCtrl.AibuColliderKind.kokan)
					{
						return false;
					}
					if (flags.lstHeroine[_main].weakPoint == 3 && useItemStickArea2 == HandCtrl.AibuColliderKind.anal)
					{
						return false;
					}
					if (flags.lstHeroine[_main].weakPoint == 4 && (useItemStickArea2 == HandCtrl.AibuColliderKind.siriL || useItemStickArea2 == HandCtrl.AibuColliderKind.siriR))
					{
						return false;
					}
					if (flags.lstHeroine[_main].weakPoint == 5 && (useItemStickArea2 == HandCtrl.AibuColliderKind.muneL || useItemStickArea2 == HandCtrl.AibuColliderKind.muneR) && hand.GetUseItemStickObjectID(useAreaItemActive3) != 0)
					{
						return false;
					}
				}
				break;
			}
			case 14:
				if (!flags.lstHeroine[_main].isVirgin || flags.count.isInsertKokan)
				{
					return false;
				}
				break;
			case 15:
				if (flags.lstHeroine[_main].isVirgin && !flags.count.isInsertKokan)
				{
					return false;
				}
				break;
			case 16:
				if (!flags.lstHeroine[_main].isAnalVirgin || flags.count.isInsertAnal)
				{
					return false;
				}
				break;
			case 17:
				if (flags.lstHeroine[_main].isAnalVirgin && !flags.count.isInsertAnal)
				{
					return false;
				}
				break;
			case 18:
			{
				if (!hand.IsAction())
				{
					return false;
				}
				int useAreaItemActive4 = hand.GetUseAreaItemActive();
				if (hand.GetUseItemStickArea(useAreaItemActive4) != HandCtrl.AibuColliderKind.anal)
				{
					return false;
				}
				break;
			}
			case 19:
			{
				if (!hand.IsAction())
				{
					return false;
				}
				int useAreaItemActive2 = hand.GetUseAreaItemActive();
				if (hand.GetUseItemStickArea(useAreaItemActive2) == HandCtrl.AibuColliderKind.anal)
				{
					return false;
				}
				break;
			}
			case 20:
				if (flags.nowAnimationInfo.numMainVoiceID != -1)
				{
					if (_main == 0 && flags.nowAnimationInfo.numMainVoiceID != 0)
					{
						return false;
					}
					if (_main == 1 && flags.nowAnimationInfo.numSubVoiceID != 0)
					{
						return false;
					}
				}
				break;
			case 21:
				if (flags.nowAnimationInfo.numMainVoiceID != -1)
				{
					if (_main == 0 && flags.nowAnimationInfo.numMainVoiceID != 1)
					{
						return false;
					}
					if (_main == 1 && flags.nowAnimationInfo.numSubVoiceID != 1)
					{
						return false;
					}
				}
				break;
			}
		}
		return true;
	}

	private bool IsPlayVoiceSecondPtn(VoicePtnInfo _lst, int _action, ChaControl _female, int _main)
	{
		if (!(new VoicePtnConditionDelegate[9] { IsPtnSecondConditionsStart, IsPtnSecondConditionsAibu, IsPtnSecondConditionsHoushi, IsPtnSecondConditionsSonyu, IsPtnSecondConditionsMasturbation, IsPtnSecondConditionsPeeping, IsPtnSecondConditionsLesbian, IsPtnSecondConditions3PHoushi, IsPtnSecondConditions3PSonyu })[_action](_lst.lstSecondConditions, _female, _main))
		{
			return false;
		}
		return true;
	}

	private bool IsPtnSecondConditionsStart(List<int> _lstConditions, ChaControl _female, int _main)
	{
		for (int i = 0; i < _lstConditions.Count; i++)
		{
			switch (_lstConditions[i])
			{
			case -1:
				return false;
			}
		}
		return true;
	}

	private bool IsPtnSecondConditionsAibu(List<int> _lstConditions, ChaControl _female, int _main)
	{
		for (int i = 0; i < _lstConditions.Count; i++)
		{
			switch (_lstConditions[i])
			{
			case -1:
				return false;
			case 0:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				break;
			case 1:
				if (flags.GetOrgCount() < 2)
				{
					return false;
				}
				break;
			case 2:
				if (flags.count.kiss < 2)
				{
					return false;
				}
				break;
			case 3:
				if (flags.count.notKiss < 2)
				{
					return false;
				}
				break;
			case 4:
				if (flags.count.notPowerful < 2)
				{
					return false;
				}
				break;
			case 5:
				if (flags.count.dontTouchAnal < 2)
				{
					return false;
				}
				break;
			case 6:
				if (flags.count.dontTouchMassage < 2)
				{
					return false;
				}
				break;
			case 7:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[0, 1].click && !flags.count.mouseAction[0, 2].click)
				{
					return false;
				}
				break;
			case 8:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[0, 1].drag && !flags.count.mouseAction[0, 2].drag)
				{
					return false;
				}
				break;
			case 9:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[0, 3].click)
				{
					return false;
				}
				break;
			case 10:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[0, 3].drag)
				{
					return false;
				}
				break;
			case 11:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[0, 4].click)
				{
					return false;
				}
				break;
			case 12:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[0, 4].drag)
				{
					return false;
				}
				break;
			case 13:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[0, 5].click && !flags.count.mouseAction[0, 6].click)
				{
					return false;
				}
				break;
			case 14:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[0, 5].drag && !flags.count.mouseAction[0, 6].drag)
				{
					return false;
				}
				break;
			case 15:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[1, 3].click)
				{
					return false;
				}
				break;
			case 16:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[1, 3].drag)
				{
					return false;
				}
				break;
			case 17:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[1, 4].click)
				{
					return false;
				}
				break;
			case 18:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[1, 4].drag)
				{
					return false;
				}
				break;
			case 19:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[1, 1].click && !flags.count.mouseAction[1, 2].click)
				{
					return false;
				}
				break;
			case 20:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[1, 1].drag && !flags.count.mouseAction[1, 2].drag)
				{
					return false;
				}
				break;
			case 21:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[2, 3].click)
				{
					return false;
				}
				break;
			case 22:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[2, 3].drag)
				{
					return false;
				}
				break;
			case 23:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[2, 4].click)
				{
					return false;
				}
				break;
			case 24:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[2, 4].drag)
				{
					return false;
				}
				break;
			case 25:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[2, 5].click && !flags.count.mouseAction[2, 6].click)
				{
					return false;
				}
				break;
			case 26:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[2, 5].drag && !flags.count.mouseAction[2, 6].drag)
				{
					return false;
				}
				break;
			case 27:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[2, 1].click && !flags.count.mouseAction[2, 2].click)
				{
					return false;
				}
				break;
			case 28:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[2, 1].drag && !flags.count.mouseAction[2, 2].drag)
				{
					return false;
				}
				break;
			case 29:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[3, 3].click)
				{
					return false;
				}
				break;
			case 30:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[3, 3].drag)
				{
					return false;
				}
				break;
			case 31:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[3, 5].click && !flags.count.mouseAction[3, 6].click)
				{
					return false;
				}
				break;
			case 32:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[3, 5].drag && !flags.count.mouseAction[3, 6].drag)
				{
					return false;
				}
				break;
			case 33:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[3, 1].click && !flags.count.mouseAction[3, 2].click)
				{
					return false;
				}
				break;
			case 34:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[3, 1].drag && !flags.count.mouseAction[3, 2].drag)
				{
					return false;
				}
				break;
			case 35:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[4, 3].click)
				{
					return false;
				}
				break;
			case 36:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[4, 3].drag)
				{
					return false;
				}
				break;
			case 37:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[5, 3].click)
				{
					return false;
				}
				break;
			case 38:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[5, 3].drag)
				{
					return false;
				}
				break;
			case 39:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[6, 3].click)
				{
					return false;
				}
				break;
			case 40:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[6, 3].drag)
				{
					return false;
				}
				break;
			case 41:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[6, 1].click && !flags.count.mouseAction[6, 2].click)
				{
					return false;
				}
				break;
			case 42:
				if (flags.GetOrgCount() < 1)
				{
					return false;
				}
				if (!flags.count.mouseAction[6, 1].drag && !flags.count.mouseAction[6, 2].drag)
				{
					return false;
				}
				break;
			}
		}
		return true;
	}

	private bool IsPtnSecondConditionsHoushi(List<int> _lstConditions, ChaControl _female, int _main)
	{
		if (flags.mode != HFlag.EMode.houshi)
		{
			return false;
		}
		for (int i = 0; i < _lstConditions.Count; i++)
		{
			switch (_lstConditions[i])
			{
			case -1:
				return false;
			case 0:
				if (flags.count.houshiInside + flags.count.houshiOutside < 1)
				{
					return false;
				}
				break;
			case 1:
				if (flags.count.houshiInside + flags.count.houshiOutside < 2)
				{
					return false;
				}
				break;
			case 2:
				if (flags.count.handFinish < 2)
				{
					return false;
				}
				break;
			case 3:
				if (flags.count.nameFinish < 2)
				{
					return false;
				}
				break;
			case 4:
				if (flags.count.kuwaeFinish < 2)
				{
					return false;
				}
				break;
			case 5:
				if (flags.count.paizuriFinish < 2)
				{
					return false;
				}
				break;
			case 6:
				if (flags.count.paizurinameFinish < 2)
				{
					return false;
				}
				break;
			case 7:
				if (flags.count.paizurikuwaeFinish < 2)
				{
					return false;
				}
				break;
			case 8:
				if (flags.count.houshiOutside < 1)
				{
					return false;
				}
				break;
			case 9:
				if (flags.count.houshiOutside < 2)
				{
					return false;
				}
				break;
			case 10:
				if (flags.count.houshiDrink < 2)
				{
					return false;
				}
				break;
			case 11:
				if (flags.count.houshiVomit < 2)
				{
					return false;
				}
				break;
			case 12:
				if (flags.count.splash < 1)
				{
					return false;
				}
				break;
			case 13:
				if (flags.count.splash < 2)
				{
					return false;
				}
				break;
			}
		}
		return true;
	}

	private bool IsPtnSecondConditionsSonyu(List<int> _lstConditions, ChaControl _female, int _main)
	{
		if (flags.mode != HFlag.EMode.sonyu)
		{
			return false;
		}
		for (int i = 0; i < _lstConditions.Count; i++)
		{
			switch (_lstConditions[i])
			{
			case -1:
				return false;
			case 0:
				if (flags.count.sonyuCondomInside < 1)
				{
					return false;
				}
				break;
			case 1:
				if (flags.count.sonyuCondomInside < 2)
				{
					return false;
				}
				break;
			case 2:
				if (flags.count.sonyuCondomInside + flags.count.sonyuInside + flags.count.sonyuOutside < 1)
				{
					return false;
				}
				break;
			case 3:
				if (flags.count.sonyuTare < 2)
				{
					return false;
				}
				break;
			case 4:
				if (flags.count.sonyuOrg < 1)
				{
					return false;
				}
				break;
			case 5:
				if (flags.count.sonyuOrg < 2)
				{
					return false;
				}
				break;
			case 6:
				if (flags.count.sonyuOutside < 1)
				{
					return false;
				}
				break;
			case 7:
				if (flags.count.sonyuOutside < 2)
				{
					return false;
				}
				break;
			case 8:
				if (flags.count.sonyuCondomInside + flags.count.sonyuInside + flags.count.sonyuOutside < 1 || flags.count.sonyuOrg < 1)
				{
					return false;
				}
				break;
			case 9:
				if (flags.count.sonyuSame < 1)
				{
					return false;
				}
				break;
			case 10:
				if (flags.count.sonyuSame < 2)
				{
					return false;
				}
				break;
			case 11:
				if (flags.count.sonyuCondomSame < 2)
				{
					return false;
				}
				break;
			case 12:
				if (flags.count.sonyuInside < 2)
				{
					return false;
				}
				break;
			case 13:
				if (flags.count.notCondomPlay < 2)
				{
					return false;
				}
				break;
			case 14:
				if (flags.count.sonyuAnalCondomInside < 1)
				{
					return false;
				}
				break;
			case 15:
				if (flags.count.sonyuAnalCondomInside < 2)
				{
					return false;
				}
				break;
			case 16:
				if (flags.count.sonyuAnalCondomInside + flags.count.sonyuAnalInside + flags.count.sonyuAnalOutside < 1)
				{
					return false;
				}
				break;
			case 17:
				if (flags.count.sonyuAnalTare < 2)
				{
					return false;
				}
				break;
			case 18:
				if (flags.count.sonyuAnalOrg < 1)
				{
					return false;
				}
				break;
			case 19:
				if (flags.count.sonyuAnalOrg < 2)
				{
					return false;
				}
				break;
			case 20:
				if (flags.count.sonyuAnalOutside < 1)
				{
					return false;
				}
				break;
			case 21:
				if (flags.count.sonyuAnalOutside < 2)
				{
					return false;
				}
				break;
			case 22:
				if (flags.count.sonyuAnalCondomInside + flags.count.sonyuAnalInside + flags.count.sonyuAnalOutside < 1 || flags.count.sonyuAnalOrg < 1)
				{
					return false;
				}
				break;
			case 23:
				if (flags.count.sonyuAnalSame < 1)
				{
					return false;
				}
				break;
			case 24:
				if (flags.count.sonyuAnalSame < 2)
				{
					return false;
				}
				break;
			case 25:
				if (flags.count.sonyuAnalCondomSame < 2)
				{
					return false;
				}
				break;
			case 26:
				if (flags.count.sonyuAnalInside < 2)
				{
					return false;
				}
				break;
			case 27:
				if (flags.count.notAnalPlay < 2)
				{
					return false;
				}
				break;
			}
		}
		return true;
	}

	private bool IsPtnSecondConditionsMasturbation(List<int> _lstConditions, ChaControl _female, int _main)
	{
		if (flags.mode != HFlag.EMode.masturbation)
		{
			return false;
		}
		for (int i = 0; i < _lstConditions.Count; i++)
		{
			switch (_lstConditions[i])
			{
			case -1:
				return false;
			}
		}
		return true;
	}

	private bool IsPtnSecondConditionsPeeping(List<int> _lstConditions, ChaControl _female, int _main)
	{
		if (flags.mode != HFlag.EMode.peeping)
		{
			return false;
		}
		for (int i = 0; i < _lstConditions.Count; i++)
		{
			switch (_lstConditions[i])
			{
			case -1:
				return false;
			}
		}
		return true;
	}

	private bool IsPtnSecondConditionsLesbian(List<int> _lstConditions, ChaControl _female, int _main)
	{
		if (flags.mode != HFlag.EMode.lesbian)
		{
			return false;
		}
		for (int i = 0; i < _lstConditions.Count; i++)
		{
			switch (_lstConditions[i])
			{
			case -1:
				return false;
			}
		}
		return true;
	}

	private bool IsPtnSecondConditions3PHoushi(List<int> _lstConditions, ChaControl _female, int _main)
	{
		if (flags.mode != HFlag.EMode.houshi)
		{
			return false;
		}
		for (int i = 0; i < _lstConditions.Count; i++)
		{
			switch (_lstConditions[i])
			{
			case -1:
				return false;
			case 0:
				if (flags.count.houshiInside + flags.count.houshiOutside < 1)
				{
					return false;
				}
				break;
			case 1:
				if (flags.count.houshiInside + flags.count.houshiOutside < 2)
				{
					return false;
				}
				break;
			case 2:
				if (flags.count.handFinish < 2)
				{
					return false;
				}
				break;
			case 3:
				if (flags.count.nameFinish < 2)
				{
					return false;
				}
				break;
			case 4:
				if (flags.count.kuwaeFinish < 2)
				{
					return false;
				}
				break;
			case 5:
				if (flags.count.paizuriFinish < 2)
				{
					return false;
				}
				break;
			case 6:
				if (flags.count.paizurinameFinish < 2)
				{
					return false;
				}
				break;
			case 7:
				if (flags.count.paizurikuwaeFinish < 2)
				{
					return false;
				}
				break;
			case 8:
				if (flags.count.houshiOutside < 1)
				{
					return false;
				}
				break;
			case 9:
				if (flags.count.houshiOutside < 2)
				{
					return false;
				}
				break;
			case 10:
				if (flags.count.houshiDrink < 2)
				{
					return false;
				}
				break;
			case 11:
				if (flags.count.houshiVomit < 2)
				{
					return false;
				}
				break;
			case 12:
				if (flags.count.splash < 1)
				{
					return false;
				}
				break;
			case 13:
				if (flags.count.splash < 2)
				{
					return false;
				}
				break;
			}
		}
		return true;
	}

	private bool IsPtnSecondConditions3PSonyu(List<int> _lstConditions, ChaControl _female, int _main)
	{
		if (flags.mode != HFlag.EMode.sonyu)
		{
			return false;
		}
		for (int i = 0; i < _lstConditions.Count; i++)
		{
			switch (_lstConditions[i])
			{
			case -1:
				return false;
			case 0:
				if (flags.count.sonyuCondomInside < 1)
				{
					return false;
				}
				break;
			case 1:
				if (flags.count.sonyuCondomInside < 2)
				{
					return false;
				}
				break;
			case 2:
				if (flags.count.sonyuCondomInside + flags.count.sonyuInside + flags.count.sonyuOutside < 1)
				{
					return false;
				}
				break;
			case 3:
				if (flags.count.sonyuTare < 2)
				{
					return false;
				}
				break;
			case 4:
				if (flags.count.sonyuOrg < 1)
				{
					return false;
				}
				break;
			case 5:
				if (flags.count.sonyuOrg < 2)
				{
					return false;
				}
				break;
			case 6:
				if (flags.count.sonyuOutside < 1)
				{
					return false;
				}
				break;
			case 7:
				if (flags.count.sonyuOutside < 2)
				{
					return false;
				}
				break;
			case 8:
				if (flags.count.sonyuCondomInside + flags.count.sonyuInside + flags.count.sonyuOutside < 1 || flags.count.sonyuOrg < 1)
				{
					return false;
				}
				break;
			case 9:
				if (flags.count.sonyuSame < 1)
				{
					return false;
				}
				break;
			case 10:
				if (flags.count.sonyuSame < 2)
				{
					return false;
				}
				break;
			case 11:
				if (flags.count.sonyuCondomSame < 2)
				{
					return false;
				}
				break;
			case 12:
				if (flags.count.sonyuInside < 2)
				{
					return false;
				}
				break;
			case 13:
				if (flags.count.notCondomPlay < 2)
				{
					return false;
				}
				break;
			case 14:
				if (flags.count.sonyuAnalCondomInside < 1)
				{
					return false;
				}
				break;
			case 15:
				if (flags.count.sonyuAnalCondomInside < 2)
				{
					return false;
				}
				break;
			case 16:
				if (flags.count.sonyuAnalCondomInside + flags.count.sonyuAnalInside + flags.count.sonyuAnalOutside < 1)
				{
					return false;
				}
				break;
			case 17:
				if (flags.count.sonyuAnalTare < 2)
				{
					return false;
				}
				break;
			case 18:
				if (flags.count.sonyuAnalOrg < 1)
				{
					return false;
				}
				break;
			case 19:
				if (flags.count.sonyuAnalOrg < 2)
				{
					return false;
				}
				break;
			case 20:
				if (flags.count.sonyuAnalOutside < 1)
				{
					return false;
				}
				break;
			case 21:
				if (flags.count.sonyuAnalOutside < 2)
				{
					return false;
				}
				break;
			case 22:
				if (flags.count.sonyuAnalCondomInside + flags.count.sonyuAnalInside + flags.count.sonyuAnalOutside < 1 || flags.count.sonyuAnalOrg < 1)
				{
					return false;
				}
				break;
			case 23:
				if (flags.count.sonyuAnalSame < 1)
				{
					return false;
				}
				break;
			case 24:
				if (flags.count.sonyuAnalSame < 2)
				{
					return false;
				}
				break;
			case 25:
				if (flags.count.sonyuAnalCondomSame < 2)
				{
					return false;
				}
				break;
			case 26:
				if (flags.count.sonyuAnalInside < 2)
				{
					return false;
				}
				break;
			case 27:
				if (flags.count.notAnalPlay < 2)
				{
					return false;
				}
				break;
			}
		}
		return true;
	}

	private bool ShortBreathProc(AnimatorStateInfo _ai, ChaControl _female, int _main)
	{
		if (_female == null || (_female != null && _female.objBody == null))
		{
			return false;
		}
		if (flags.voice.playShorts[_main] == -1)
		{
			return false;
		}
		Dictionary<int, Dictionary<int, ShortBreathInfo>> dictionary = dicShortBreathIntos[_main];
		if (dictionary.Count == 0)
		{
			return false;
		}
		if (nowVoices[_main].notOverWrite && Manager.Voice.IsPlay(flags.transVoiceMouth[_main]))
		{
			flags.voice.playShorts[_main] = -1;
			return false;
		}
		if (!dicShortBreathPtn.ContainsKey(flags.voice.playShorts[_main]))
		{
			GlobalMethod.DebugLog("短い喘ぎのパターンIDエラー エラーID[" + flags.voice.playShorts[_main] + "]", 1);
			flags.voice.playShorts[_main] = -1;
			return false;
		}
		ShortBreathPtn shortBreathPtn = dicShortBreathPtn[flags.voice.playShorts[_main]];
		List<ShortBreathPtnInfo> list = new List<ShortBreathPtnInfo>();
		for (int i = 0; i < shortBreathPtn.lstInfo.Count; i++)
		{
			if (IsPlayShortBreathPtn(shortBreathPtn.lstInfo[i], _ai, _main))
			{
				list.Add(shortBreathPtn.lstInfo[i]);
			}
		}
		List<VoiceSelect> pLayNumShortVoiceList = GetPLayNumShortVoiceList(list, dictionary, _main);
		if (pLayNumShortVoiceList.Count == 0)
		{
			for (int j = 0; j < list.Count; j++)
			{
				int stateSelect = GetStateSelect(list[j].state, _main);
				for (int k = 0; k < list[j].lstVoice.Count; k++)
				{
					int key = list[j].lstVoice[k];
					if (dictionary[stateSelect].ContainsKey(key))
					{
						dictionary[stateSelect][key].isPlay = false;
					}
				}
			}
			pLayNumShortVoiceList = GetPLayNumShortVoiceList(list, dictionary, _main);
			if (pLayNumShortVoiceList.Count == 0)
			{
				flags.voice.playShorts[_main] = -1;
				return false;
			}
		}
		pLayNumShortVoiceList = pLayNumShortVoiceList.OrderBy((VoiceSelect l) => Guid.NewGuid()).ToList();
		nowVoices[_main].shortInfo = dictionary[pLayNumShortVoiceList[0].state][pLayNumShortVoiceList[0].playnum];
		Utils.Voice.Setting s = new Utils.Voice.Setting
		{
			no = flags.lstHeroine[_main].voiceNo,
			assetBundleName = nowVoices[_main].shortInfo.pathAsset,
			assetName = nowVoices[_main].shortInfo.nameFile,
			pitch = flags.lstHeroine[_main].voicePitch,
			voiceTrans = flags.transVoiceMouth[_main]
		};
		_female.SetLipSync(Utils.Voice.OncePlayChara(s));
		flags.hashAssetBundle.Add(nowVoices[_main].shortInfo.pathAsset);
		nowVoices[_main].state = VoiceKind.breathShort;
		nowVoices[_main].notOverWrite = nowVoices[_main].shortInfo.notOverwrite;
		nowVoices[_main].shortInfo.isPlay = true;
		flags.voice.playShorts[_main] = -1;
		faceLists[_main].SetFace(nowVoices[_main].shortInfo.face, _female, 2, 0);
		nowVoices[_main].link = new LinkInfo();
		linkUseBreathPtn[_main] = null;
		linkUseVoicePtn[_main] = null;
		return true;
	}

	private List<VoiceSelect> GetPLayNumShortVoiceList(List<ShortBreathPtnInfo> _lstPlayInfo, Dictionary<int, Dictionary<int, ShortBreathInfo>> _dicUseVoiceInfo, int _main)
	{
		List<VoiceSelect> list = new List<VoiceSelect>();
		for (int i = 0; i < _lstPlayInfo.Count; i++)
		{
			int stateSelect = GetStateSelect(_lstPlayInfo[i].state, _main);
			for (int j = 0; j < _lstPlayInfo[i].lstVoice.Count; j++)
			{
				if (_dicUseVoiceInfo[stateSelect].ContainsKey(_lstPlayInfo[i].lstVoice[j]) && !_dicUseVoiceInfo[stateSelect][_lstPlayInfo[i].lstVoice[j]].isPlay)
				{
					VoiceSelect voiceSelect = new VoiceSelect();
					voiceSelect.state = stateSelect;
					voiceSelect.playnum = _lstPlayInfo[i].lstVoice[j];
					voiceSelect.infoShortPtn = _lstPlayInfo[i];
					list.Add(voiceSelect);
				}
			}
		}
		return list;
	}

	private bool IsPlayShortBreathPtn(ShortBreathPtnInfo _lst, AnimatorStateInfo _ai, int _main)
	{
		if (!IsShortPtnConditions(_lst.lstConditions, _ai, _main))
		{
			return false;
		}
		return true;
	}

	private bool IsShortPtnConditions(List<int> _lstConditions, AnimatorStateInfo _ai, int _main)
	{
		for (int i = 0; i < _lstConditions.Count; i++)
		{
			switch (_lstConditions[i])
			{
			case 0:
				if (flags.mode == HFlag.EMode.houshi3PMMF || flags.mode == HFlag.EMode.sonyu3PMMF)
				{
					if (flags.mode == HFlag.EMode.houshi3PMMF)
					{
						if (flags.nowAnimationInfo.id != 0 && (_ai.IsName("WLoop") || _ai.IsName("SLoop") || _ai.IsName("OLoop") || _ai.IsName("IN_Start") || _ai.IsName("IN_Loop")))
						{
							return false;
						}
					}
					else if (flags.mode == HFlag.EMode.sonyu3PMMF && flags.nowAnimationInfo.id == 2 && (_ai.IsName("InsertIdle") || _ai.IsName("WLoop") || _ai.IsName("SLoop") || _ai.IsName("OLoop")))
					{
						return false;
					}
				}
				else if (flags.mode == HFlag.EMode.houshi3P)
				{
					switch (_main)
					{
					case 0:
						if (!IsShortVoice3PConditions(flags.nowAnimationInfo.numMainVoiceID, 0, _ai))
						{
							return false;
						}
						break;
					case 1:
						if (!IsShortVoice3PConditions(flags.nowAnimationInfo.numSubVoiceID, 0, _ai))
						{
							return false;
						}
						break;
					}
				}
				else if (flags.mode == HFlag.EMode.houshi && (flags.nowAnimationInfo.sysTaii == 1 || flags.nowAnimationInfo.sysTaii == 2) && (_ai.IsName("WLoop") || _ai.IsName("SLoop") || _ai.IsName("OLoop") || _ai.IsName("IN_Start") || _ai.IsName("IN_Loop")))
				{
					return false;
				}
				break;
			case 1:
				if (!_ai.IsName("WLoop") && !_ai.IsName("SLoop") && !_ai.IsName("OLoop") && !_ai.IsName("IN_Start") && !_ai.IsName("IN_Loop"))
				{
					return false;
				}
				if (flags.mode == HFlag.EMode.houshi3PMMF || flags.mode == HFlag.EMode.sonyu3PMMF)
				{
					if (flags.mode == HFlag.EMode.houshi3PMMF)
					{
						if (flags.nowAnimationInfo.id != 2 || !_ai.IsName("OLoop"))
						{
							return false;
						}
					}
					else if (flags.mode == HFlag.EMode.sonyu3PMMF)
					{
						return false;
					}
				}
				else if (flags.mode == HFlag.EMode.houshi3P)
				{
					switch (_main)
					{
					case 0:
						if (!IsShortVoice3PConditions(flags.nowAnimationInfo.numMainVoiceID, 1, _ai))
						{
							return false;
						}
						break;
					case 1:
						if (!IsShortVoice3PConditions(flags.nowAnimationInfo.numSubVoiceID, 1, _ai))
						{
							return false;
						}
						break;
					}
				}
				else if (flags.mode != HFlag.EMode.houshi || flags.nowAnimationInfo.sysTaii != 1)
				{
					return false;
				}
				break;
			case 2:
				if (flags.mode == HFlag.EMode.houshi3PMMF || flags.mode == HFlag.EMode.sonyu3PMMF)
				{
					if (flags.mode == HFlag.EMode.houshi3PMMF)
					{
						if (!_ai.IsName("WLoop") && !_ai.IsName("SLoop") && !_ai.IsName("OLoop") && !_ai.IsName("IN_Start") && !_ai.IsName("IN_Loop"))
						{
							return false;
						}
						if (flags.nowAnimationInfo.id == 0)
						{
							return false;
						}
						if (flags.nowAnimationInfo.id == 2 && !_ai.IsName("WLoop") && !_ai.IsName("SLoop"))
						{
							return false;
						}
					}
					else if (flags.mode == HFlag.EMode.sonyu3PMMF && (flags.nowAnimationInfo.id != 2 || (!_ai.IsName("InsertIdle") && !_ai.IsName("WLoop") && !_ai.IsName("SLoop") && !_ai.IsName("OLoop"))))
					{
						return false;
					}
					break;
				}
				if (!_ai.IsName("WLoop") && !_ai.IsName("SLoop") && !_ai.IsName("OLoop") && !_ai.IsName("IN_Start") && !_ai.IsName("IN_Loop"))
				{
					return false;
				}
				if (flags.mode == HFlag.EMode.houshi3P)
				{
					switch (_main)
					{
					case 0:
						if (!IsShortVoice3PConditions(flags.nowAnimationInfo.numMainVoiceID, 2, _ai))
						{
							return false;
						}
						break;
					case 1:
						if (!IsShortVoice3PConditions(flags.nowAnimationInfo.numSubVoiceID, 2, _ai))
						{
							return false;
						}
						break;
					}
				}
				else if (flags.mode != HFlag.EMode.houshi || flags.nowAnimationInfo.sysTaii != 2)
				{
					return false;
				}
				break;
			case 3:
				if (flags.voice.isShortsPlayTouchWeak[_main])
				{
					return false;
				}
				break;
			case 4:
				if (!flags.voice.isShortsPlayTouchWeak[_main])
				{
					return false;
				}
				break;
			case 5:
				if (flags.nowAnimationInfo.numVoiceKindID == 1)
				{
					return false;
				}
				break;
			case 6:
				if (flags.nowAnimationInfo.numVoiceKindID == 0)
				{
					return false;
				}
				break;
			case 7:
				if ((flags.mode == HFlag.EMode.houshi && ((flags.nowAnimationInfo.houshiLoopActionW == 7 && _ai.IsName("WLoop")) || (flags.nowAnimationInfo.houshiLoopActionS == 7 && _ai.IsName("SLoop")))) || (flags.nowAnimationInfo.numVoiceKindID == 2 && (_ai.IsName("OLoop") || _ai.IsName("IN_Start") || _ai.IsName("IN_Loop"))))
				{
					return false;
				}
				break;
			case 8:
				if ((flags.mode != HFlag.EMode.houshi || ((flags.nowAnimationInfo.houshiLoopActionW != 7 || !_ai.IsName("WLoop")) && (flags.nowAnimationInfo.houshiLoopActionS != 7 || !_ai.IsName("SLoop")))) && (flags.nowAnimationInfo.numVoiceKindID != 2 || (!_ai.IsName("OLoop") && !_ai.IsName("IN_Start") && !_ai.IsName("IN_Loop"))))
				{
					return false;
				}
				break;
			case 9:
				if (nowVoices[_main].state != VoiceKind.voice && nowVoices[_main].state != VoiceKind.startvoice)
				{
					return false;
				}
				break;
			case 10:
				if (nowVoices[_main].state == VoiceKind.voice || nowVoices[_main].state == VoiceKind.startvoice)
				{
					return false;
				}
				break;
			}
		}
		return true;
	}

	private bool IsShortVoice3PConditions(int _voiceID, int _voiceKind, AnimatorStateInfo _ai)
	{
		switch (_voiceID)
		{
		case 0:
			if (_ai.IsName("WLoop") && flags.nowAnimationInfo.mainHoushi3PShortVoicePtns[0] != _voiceKind)
			{
				return false;
			}
			if (_ai.IsName("SLoop") && flags.nowAnimationInfo.mainHoushi3PShortVoicePtns[1] != _voiceKind)
			{
				return false;
			}
			if (_ai.IsName("OLoop") && flags.nowAnimationInfo.mainHoushi3PShortVoicePtns[2] != _voiceKind)
			{
				return false;
			}
			if ((_ai.IsName("IN_Start") || _ai.IsName("IN_Loop")) && flags.nowAnimationInfo.mainHoushi3PShortVoicePtns[3] != _voiceKind)
			{
				return false;
			}
			break;
		case 1:
			if (_ai.IsName("WLoop") && flags.nowAnimationInfo.subHoushi3PShortVoicePtns[0] != _voiceKind)
			{
				return false;
			}
			if (_ai.IsName("SLoop") && flags.nowAnimationInfo.subHoushi3PShortVoicePtns[1] != _voiceKind)
			{
				return false;
			}
			if (_ai.IsName("OLoop") && flags.nowAnimationInfo.subHoushi3PShortVoicePtns[2] != _voiceKind)
			{
				return false;
			}
			if ((_ai.IsName("IN_Start") || _ai.IsName("IN_Loop")) && flags.nowAnimationInfo.subHoushi3PShortVoicePtns[3] != _voiceKind)
			{
				return false;
			}
			break;
		}
		return true;
	}

	public void InitD(string _chaFolder, string _chaFolder1)
	{
		isPrcoStop = false;
		isVoicePrcoStop = false;
		for (int i = 0; i < 2; i++)
		{
			dicBreathVoiceIntos[i].Clear();
			dicBreathPtn[i].Clear();
			dicVoiceIntos[i].Clear();
			dicVoicePtn[i].Clear();
			for (int j = 0; j < 8; j++)
			{
				dicBreathVoiceIntos[i].Add(j, new Dictionary<int, Dictionary<int, BreathVoiceInfo>>());
				dicBreathPtn[i].Add(j, new Dictionary<string, BreathPtn>());
			}
			for (int k = 0; k < 9; k++)
			{
				dicVoiceIntos[i].Add(k, new Dictionary<int, Dictionary<int, VoiceInfo>>());
				dicVoicePtn[i].Add(k, new Dictionary<int, VoicePtn>());
			}
		}
		blendEyes[0] = new GlobalMethod.FloatBlend();
		blendEyes[1] = new GlobalMethod.FloatBlend();
		blendMouths[0] = new GlobalMethod.FloatBlend();
		blendMouths[1] = new GlobalMethod.FloatBlend();
		string[] array = new string[2] { _chaFolder, _chaFolder1 };
		for (int l = 0; l < array.Length; l++)
		{
			int num = l;
			if (fixPersonal.ContainsKey(array[num]))
			{
				array[num] = fixPersonal[array[num]];
			}
		}
		bool second = false;
		int ptnNum = 1;
		if (array[1] != "")
		{
			second = true;
			ptnNum = 2;
		}
		LoadBreathList(array[0], array[1], second, ptnNum);
		LoadShortBreathList(array[0], array[1]);
		LoadVoiceListD(array[0], array[1], second, ptnNum);
	}

	private bool LoadVoiceListD(string _chaFolder, string _chaFolder1, bool second, int ptnNum)
	{
		string[] array = new string[2] { _chaFolder, _chaFolder1 };
		bool flag = false;
		for (int i = 0; i < 2; i++)
		{
			if (flag && !second)
			{
				continue;
			}
			LoadVoice(array[i], i);
			for (int j = 0; j < 9; j++)
			{
				if (!flag)
				{
					LoadVoicePtn(j, ptnNum);
				}
			}
			if (!flag)
			{
				flag = true;
			}
		}
		return true;
	}

	public bool PlayPtnCheckD(int _action, VoicePtnInfo _vpi)
	{
		HSceneProc.AnimationListInfo nowAnimationInfo = flags.nowAnimationInfo;
		int num = _action * 100;
		int mode = (int)nowAnimationInfo.mode;
		mode++;
		mode *= 100;
		if (num != 0 && num != mode)
		{
			return false;
		}
		if (!_vpi.lstAnimID.Contains(nowAnimationInfo.id))
		{
			return false;
		}
		return true;
	}
}
