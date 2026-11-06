using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ActionGame;
using ActionGame.Communication;
using Illusion.Extensions;
using SaveData;
using UnityEngine;

namespace Manager;

public class GameAssist : Singleton<GameAssist>
{
	private class FileInfo
	{
		public string assetBundle = "";

		public string file = "";
	}

	public class ConditionsInfo
	{
		public string name = "";

		public RangeValue favor;

		public RangeValue lewdness;

		public RangeValue probability;

		public int reference;

		public ConditionsInfo(List<string> _list)
		{
			name = _list.SafeGet(2);
			favor = new RangeValue(_list.SafeGet(3));
			lewdness = new RangeValue(_list.SafeGet(4));
			probability = new RangeValue(_list.SafeGet(5));
			reference = int.Parse(_list.SafeGet(6));
		}
	}

	public class CorrectionInfo
	{
		public float[] preference;

		public float[] physical;

		public float[] intellect;

		public float[] hentai;

		public int[] club;

		public int[] clubSkill;

		public CorrectionInfo()
		{
			preference = new float[3];
			physical = new float[2];
			intellect = new float[2];
			hentai = new float[3];
			club = new int[2];
			clubSkill = new int[3];
		}
	}

	public class BenefitsOfHotelInfo
	{
		public int[] talk;

		public float[] favor;

		public float confession;

		public float together;

		public float[] h;

		public float desire;

		public BenefitsOfHotelInfo()
		{
			talk = new int[3] { 1, 1, 1 };
			favor = new float[3];
			h = new float[3];
		}
	}

	public class RangeValue
	{
		public float min { get; protected set; }

		public float max { get; protected set; }

		public float minLimit { get; protected set; }

		public float maxLimit { get; protected set; }

		public bool through
		{
			get
			{
				if (min == -1f)
				{
					return max == -1f;
				}
				return false;
			}
		}

		public RangeValue(float _min = 0f, float _max = 100f)
		{
		}

		public RangeValue(string _str, float _min = 0f, float _max = 100f)
		{
			Set(_str);
			minLimit = _min;
			maxLimit = _max;
		}

		public virtual float Get(float _value)
		{
			if (min == max)
			{
				return min;
			}
			return Mathf.Lerp(min, max, Mathf.Clamp01(Mathf.InverseLerp(minLimit, maxLimit, _value)));
		}

		public virtual void Set(string _str)
		{
			string[] array = _str.Split(',');
			min = float.Parse(array[0]);
			float result = 0f;
			max = (float.TryParse(array.SafeGet(1), out result) ? result : min);
		}

		public bool Check(float _value)
		{
			if (min != max)
			{
				return MathfEx.RangeEqualOn(min, _value, max);
			}
			return _value >= min;
		}
	}

	public class MotionSpeed : RangeValue
	{
		public string[] state { get; private set; }

		public float value => Random.Range(base.min, base.max);

		public MotionSpeed(string[] _state, string _min, string _max)
		{
			state = _state;
			base.min = float.Parse(_min);
			base.max = float.Parse(_max);
		}
	}

	public class ProbabilityValue : RangeValue
	{
		public ProbabilityValue(string _str)
			: base(_str)
		{
		}
	}

	public class TopicTalkPoint
	{
		public int[] favor;

		public int[] point;

		public TopicTalkPoint(ActionGame.TopicTalkPoint _topicTalk)
		{
			favor = _topicTalk.param.Select((ActionGame.TopicTalkPoint.Param v) => v.favor).ToArray();
			point = _topicTalk.param.Select((ActionGame.TopicTalkPoint.Param v) => v.point).ToArray();
		}
	}

	public class TopicListenPoint
	{
		public int[] common;

		public int[] rare;

		public int[] point;

		public TopicListenPoint(TopicListen _topicTalk)
		{
			common = _topicTalk.param.Select((TopicListen.Param v) => v.common).ToArray();
			rare = _topicTalk.param.Select((TopicListen.Param v) => v.rare).ToArray();
			point = _topicTalk.param.Select((TopicListen.Param v) => v.point).ToArray();
		}
	}

	public class TopicInterest
	{
		public int[] favor;

		public int[] lewdness;

		public int[] point;

		public TopicInterest(TopicInterestData _data)
		{
			favor = _data.param.Select((TopicInterestData.Param v) => v.favor).ToArray();
			lewdness = _data.param.Select((TopicInterestData.Param v) => v.lewdness).ToArray();
			point = _data.param.Select((TopicInterestData.Param v) => v.point).ToArray();
		}
	}

	public class Peply
	{
		public int[] common;

		public int[] normal;
	}

	private Dictionary<int, FileInfo> dicTalkInfo;

	private Dictionary<int, FileInfo> dicOptionItemsInfo;

	private Dictionary<int, FileInfo> dicEvnetBundleInfo;

	private Dictionary<int, FileInfo> dicListenBundleInfo;

	private Dictionary<int, FileInfo> dicTopicCommonBundleInfo;

	private Dictionary<int, FileInfo> dicTopicRareBundleInfo;

	private Dictionary<int, Dictionary<string, string[]>> dicPoseChara;

	private Dictionary<int, Dictionary<int, ConditionsInfo>> dicOccurrence;

	private Dictionary<int, int[]> dicPreference;

	private Dictionary<int, int[]> dicPreferenceCustom;

	private static readonly int sr_PrayFavor = 3;

	private static readonly int sr_PrayLewdness = 1000;

	private Dictionary<int, List<List<string>>> dicBestPlace { get; set; }

	public CorrectionInfo correctionInfo { get; private set; }

	public BenefitsOfHotelInfo benefitsOfHotel { get; private set; }

	public Dictionary<int, List<MotionSpeed>> dicMotionSpeed { get; private set; }

	public Dictionary<int, Dictionary<int, ProbabilityValue>> dicMaleCondition { get; private set; }

	public TopicTalkPoint topicTalkPoint { get; private set; }

	public TopicListenPoint topicListenPoint { get; private set; }

	public Dictionary<int, bool[]> topicListenGroup { get; private set; }

	public List<string[]> topicPersonalityGroup { get; private set; }

	public List<TopicDropData.Param> topicDrops { get; private set; }

	public int[] pointDrops { get; private set; }

	public Dictionary<int, TipsData.Param> tipsData { get; private set; }

	public TopicInterest topicInterest { get; private set; }

	public Dictionary<int, Dictionary<int, int[]>> topicReply { get; private set; }

	public Dictionary<int, Dictionary<int, Peply>> topicReplyNPC { get; private set; }

	public bool isInit { get; private set; }

	public bool GetTalkFilePath(int _personality, out string _bundle, out string _file)
	{
		if (dicTalkInfo.TryGetValue(_personality, out var value))
		{
			_bundle = value.assetBundle;
			_file = value.file;
			return true;
		}
		_bundle = "";
		_file = "";
		return false;
	}

	public bool GetOptionItemsFilePath(int _personality, out string _bundle, out string _file)
	{
		FileInfo value = null;
		if (dicOptionItemsInfo.TryGetValue(_personality, out value))
		{
			_bundle = value.assetBundle;
			_file = value.file;
			return true;
		}
		_bundle = "";
		_file = "";
		return false;
	}

	public bool GetEventBundleFilePath(int _personality, out string _bundle, out string _file)
	{
		FileInfo value = null;
		if (dicEvnetBundleInfo.TryGetValue(_personality, out value))
		{
			_bundle = value.assetBundle;
			_file = value.file;
			return true;
		}
		_bundle = "";
		_file = "";
		return false;
	}

	public bool GetListenBundleFilePath(int _personality, out string _bundle, out string _file)
	{
		FileInfo value = null;
		if (dicListenBundleInfo.TryGetValue(_personality, out value))
		{
			_bundle = value.assetBundle;
			_file = value.file;
			return true;
		}
		_bundle = "";
		_file = "";
		return false;
	}

	public bool GetTopicCommonBundleFilePath(int _personality, out string _bundle, out string _file)
	{
		FileInfo value = null;
		if (dicTopicCommonBundleInfo.TryGetValue(_personality, out value))
		{
			_bundle = value.assetBundle;
			_file = value.file;
			return true;
		}
		_bundle = "";
		_file = "";
		return false;
	}

	public bool GetTopicRareBundleFilePath(int _personality, out string _bundle, out string _file)
	{
		if (dicTopicRareBundleInfo.TryGetValue(_personality, out var value))
		{
			_bundle = value.assetBundle;
			_file = value.file;
			return true;
		}
		_bundle = "";
		_file = "";
		return false;
	}

	public string GetBestPlace(int _personality, int _stage, int _kind)
	{
		List<List<string>> value = null;
		if (!dicBestPlace.TryGetValue(_personality, out value))
		{
			return "";
		}
		if (value.IsNullOrEmpty() || !MathfEx.RangeEqualOn(0, _stage, value.Count - 1))
		{
			return "";
		}
		if (value[_stage].IsNullOrEmpty() || !MathfEx.RangeEqualOn(0, _kind, value[_stage].Count - 1))
		{
			return "";
		}
		return value[_stage][_kind];
	}

	public void ChangeExpression(int _personality, string _name, ChaControl _chaCtrl)
	{
		SingletonInitializer<Game>.instance.GetExpression(_personality, _name)?.Change(_chaCtrl);
	}

	public string[] GetPoseInfo(int _personality, string _pose)
	{
		Dictionary<string, string[]> value = null;
		if (!dicPoseChara.TryGetValue(_personality, out value))
		{
			return null;
		}
		string[] value2 = null;
		if (!value.TryGetValue(_pose, out value2))
		{
			return null;
		}
		return value2;
	}

	public ConditionsInfo GetOccurrenceCondition(int _stage, int _conditions)
	{
		Dictionary<int, ConditionsInfo> value = null;
		if (!dicOccurrence.TryGetValue(_stage, out value))
		{
			return null;
		}
		ConditionsInfo value2 = null;
		if (!value.TryGetValue(_conditions, out value2))
		{
			return null;
		}
		return value2;
	}

	public ProbabilityValue GetCommunicationConndition(int _stage, int _conditions)
	{
		Dictionary<int, ProbabilityValue> value = null;
		if (!dicMaleCondition.TryGetValue(_stage, out value))
		{
			return null;
		}
		ProbabilityValue value2 = null;
		if (!value.TryGetValue(_conditions, out value2))
		{
			return null;
		}
		return value2;
	}

	public bool ChangeParam(Heroine _heroine, ChangeValueAbstractInfo _value, bool _isNPC)
	{
		int favor = _value.favor;
		int lewdness = _value.lewdness;
		int num = Mathf.Clamp((!_isNPC) ? _heroine.relation : _heroine.relationNPC, 0, 2);
		float num2 = Mathf.Lerp(0f, correctionInfo.preference[0], Preference(_heroine));
		Player player = Game.Player;
		float num3 = 0f;
		num3 += Mathf.Lerp(0f, correctionInfo.intellect[0], Mathf.InverseLerp(0f, 100f, player.intellect));
		num3 += Mathf.Lerp(0f, correctionInfo.physical[0], Mathf.InverseLerp(0f, 100f, player.physical));
		num3 += Mathf.Lerp(0f, correctionInfo.hentai[0], Mathf.InverseLerp(0f, 100f, player.hentai));
		float num4 = Mathf.Lerp(0f, correctionInfo.hentai[1], Mathf.InverseLerp(0f, 100f, player.hentai));
		float num5 = (Game.IsSameAccommodation(_heroine) ? benefitsOfHotel.favor[num] : 0f);
		PrayInfo.Param value;
		int num6 = ((favor > 0 && Game.saveData.prayedResult == sr_PrayFavor && SingletonInitializer<ActionScene>.instance.prayInfoTable.TryGetValue(sr_PrayFavor, out value)) ? value.Effect : 0);
		PrayInfo.Param value2;
		int num7 = ((lewdness > 0 && Game.saveData.prayedResult == sr_PrayLewdness && SingletonInitializer<ActionScene>.instance.prayInfoTable.TryGetValue(sr_PrayLewdness, out value2)) ? value2.Effect : 0);
		favor += (int)((float)Mathf.Abs(favor) * (num2 + num3 + num5) * (_heroine.chaCtrl.GetAttribute(1) ? 1.5f : 1f)) + num6;
		lewdness += (int)((float)Mathf.Abs(lewdness) * (num2 + num4)) + num7;
		_heroine.favor = Mathf.Clamp(_heroine.favor + favor, 0, SingletonInitializer<ActionScene>.instance.stepUpDic[num]);
		_heroine.lewdness = Mathf.Clamp(_heroine.lewdness + lewdness, 0, 100);
		if (favor <= 0)
		{
			return lewdness > 0;
		}
		return true;
	}

	public float Preference(Heroine _heroine)
	{
		int[] array = PreferenceValue(_heroine);
		Player player = Game.Player;
		int num = 0;
		num += Mathf.Abs(player.parameter.aggressive + 5 - (array[0] + 5));
		num += Mathf.Abs(player.parameter.diligence + 5 - (array[1] + 5));
		num += Mathf.Abs(player.parameter.kindness + 5 - (array[2] + 5));
		return Mathf.InverseLerp(30f, 0f, num);
	}

	public int[] PreferenceValue(Heroine _heroine)
	{
		if (!dicPreference.ContainsKey(_heroine.personality))
		{
			return new int[3];
		}
		int[] array = dicPreference[_heroine.personality].ToArray();
		foreach (KeyValuePair<int, int[]> item in dicPreferenceCustom.Where((KeyValuePair<int, int[]> v) => _heroine.chaCtrl.GetAttribute(v.Key)))
		{
			array[0] = Mathf.Clamp(array[0] + item.Value[0], -5, 5);
			array[1] = Mathf.Clamp(array[1] + item.Value[1], -5, 5);
			array[2] = Mathf.Clamp(array[2] + item.Value[2], -5, 5);
		}
		return array;
	}

	public void DecreaseTalkTime(Heroine _heroine, int _value)
	{
		_heroine.talkTime = Mathf.Clamp(_heroine.talkTime - _value, 0, _heroine.talkTimeMax);
	}

	public void AddTalkTime()
	{
		int add = correctionInfo.clubSkill[2];
		Game.HeroineList.ForEach(delegate(Heroine p)
		{
			p.talkTime += add;
			p.talkTimeMax += add;
		});
	}

	public Dictionary<string, float> GetMotionSpeed(int _personality)
	{
		Dictionary<string, float> dictionary = new Dictionary<string, float>();
		List<MotionSpeed> value = null;
		if (!dicMotionSpeed.TryGetValue(_personality, out value))
		{
			return dictionary;
		}
		foreach (MotionSpeed item in value)
		{
			float value2 = item.value;
			string[] state = item.state;
			foreach (string key in state)
			{
				dictionary[key] = value2;
			}
		}
		return dictionary;
	}

	public bool CheckMoveMotion(string _state, int _personality)
	{
		return CheckMotion(_state, _personality, "Locomotion");
	}

	public bool CheckEscapeMotion(string _state, int _personality)
	{
		return CheckMotion(_state, _personality, "Escape");
	}

	public bool CheckMoveAngerMotion(string _state, int _personality)
	{
		return CheckMotion(_state, _personality, "Locomotion_Anger");
	}

	public bool CheckChangeMoveSpeed(string _state, int _personality)
	{
		return CheckMoveMotion(_state, _personality) | CheckEscapeMotion(_state, _personality) | CheckMoveAngerMotion(_state, _personality);
	}

	public int GetTopic(Heroine _heroine)
	{
		int group = GetTopicListenGroup(_heroine.personality);
		int num = (from v in topicListenGroup
			where v.Value[@group] && !_heroine.topicListen.Contains(v.Key)
			select v.Key).Shuffle().First();
		_heroine.topicListen.Enqueue(num);
		if (_heroine.topicListen.Count > 5)
		{
			_heroine.topicListen.Dequeue();
		}
		return num;
	}

	public int GetTopicTalkCommon(Heroine _heroine, Topic.Param _param)
	{
		return topicReply[_heroine.personality][_param.No].Shuffle().First();
	}

	public int GetTopicReplyNPC(Heroine _heroine, Topic.Param _param)
	{
		Peply peply = topicReplyNPC[_heroine.fixCharaID][_param.No];
		if (_heroine.relationNPC != 0)
		{
			return peply.normal.Shuffle().First();
		}
		return peply.common.Shuffle().First();
	}

	public int GetTopicListenGroup(int _personality)
	{
		string personality = Voice.infoTable[_personality].Personality;
		return Mathf.Clamp(topicPersonalityGroup.FindIndex((string[] _p) => _p.Contains(personality)) % 4, 0, 3);
	}

	public TopicDropData.Param DropTopic(int _no)
	{
		return topicDrops.SafeGet(_no);
	}

	public TopicDropData.Param DropTopic(string _state)
	{
		return topicDrops.Where((TopicDropData.Param v) => v.states.Contains(_state)).Shuffle().FirstOrDefault();
	}

	private IEnumerator LoadInfo()
	{
		isInit = false;
		List<string> list = (from s in CommonLib.GetAssetBundleNameListFromPath("communication/", subdirCheck: true)
			where Regex.Match(Path.GetFileNameWithoutExtension(s), "info_(\\d*)").Success
			select s).ToList();
		SortedDictionary<int, string> sortedDictionary = new SortedDictionary<int, string>();
		foreach (string item in list)
		{
			int result = -1;
			if (int.TryParse(Path.GetFileNameWithoutExtension(item).Replace("info_", ""), out result))
			{
				sortedDictionary.Add(result, item);
			}
		}
		foreach (string item2 in sortedDictionary.Values.ToList())
		{
			Match match = Regex.Match(Path.GetFileNameWithoutExtension(item2), "_(\\d*)");
			LoadPathInfo(item2);
			LoadPose(item2);
			LoadOccurrence(item2);
			LoadPreference(item2, match);
			LoadPreferenceCustom(item2, match);
			LoadCorrection(item2, match);
			LoadMotionSpeed(item2, match);
			LoadCommunicationCondition(item2, match);
			LoadStaffBenefitsInfo(item2, match);
			LoadTopicTalkPoint(item2, match);
			LoadTopicListen(item2, match);
			LoadTopicListenGroup(item2, match);
			LoadTopicPersonalityGroup(item2, match);
			LoadTopicDropData(item2, match);
			LoadTopicInterest(item2, match);
			LoadTopicReply(item2);
			LoadTopicReplyNPC(item2);
			LoadPointDropData(item2, match);
			LoadTipsData(item2, match);
		}
		isInit = true;
		yield break;
	}

	private void LoadPathInfo(string _path)
	{
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(_path);
		if (!AssetBundleCheck.IsFile(_path, fileNameWithoutExtension))
		{
			return;
		}
		ExcelData excelData = CommonLib.LoadAsset<ExcelData>(_path, fileNameWithoutExtension);
		if (excelData == null)
		{
			return;
		}
		foreach (List<string> item in from p in excelData.list.Skip(1)
			select p.list)
		{
			if (int.TryParse(item[0], out var result))
			{
				dicTalkInfo[result] = new FileInfo
				{
					assetBundle = item[1],
					file = item[2]
				};
				dicOptionItemsInfo[result] = new FileInfo
				{
					assetBundle = item.SafeGet(3),
					file = item.SafeGet(4)
				};
				dicEvnetBundleInfo[result] = new FileInfo
				{
					assetBundle = item.SafeGet(5),
					file = item.SafeGet(6)
				};
				dicListenBundleInfo[result] = new FileInfo
				{
					assetBundle = item.SafeGet(7),
					file = item.SafeGet(8)
				};
				dicTopicCommonBundleInfo[result] = new FileInfo
				{
					assetBundle = item.SafeGet(9),
					file = item.SafeGet(10)
				};
				dicTopicRareBundleInfo[result] = new FileInfo
				{
					assetBundle = item.SafeGet(11),
					file = item.SafeGet(12)
				};
			}
		}
	}

	private void LoadPlaceInfo(string _path, Match _match)
	{
		if (!_match.Success)
		{
			return;
		}
		string text = $"Place_{_match.Groups[1]:00}";
		if (!AssetBundleCheck.IsFile(_path, text) || text.Check(ignoreCase: true, AssetBundleCheck.GetAllAssetName(_path, _WithExtension: false)) == -1)
		{
			return;
		}
		ExcelData excelData = CommonLib.LoadAsset<ExcelData>(_path, text);
		if (excelData == null)
		{
			return;
		}
		foreach (List<string> item in from p in excelData.list.Skip(1)
			select p.list)
		{
			dicBestPlace[int.Parse(item[0])] = new List<List<string>>
			{
				item.Skip(1).Take(3).ToList(),
				item.Skip(4).Take(3).ToList(),
				item.Skip(7).Take(3).ToList()
			};
		}
	}

	private void LoadPose(string _path)
	{
		foreach (Match item in from s in AssetBundleCheck.GetAllAssetName(_path, _WithExtension: false)
			select Regex.Match(s, "pose_(-?\\d*)") into r
			where r.Success
			select r)
		{
			int key = int.Parse(item.Groups[1].Value);
			if (!dicPoseChara.ContainsKey(key))
			{
				dicPoseChara.Add(key, new Dictionary<string, string[]>());
			}
			foreach (List<string> item2 in from p in AssetBundleManager.LoadAsset(_path, item.Value, typeof(ExcelData)).GetAsset<ExcelData>().list.Skip(1)
				select p.list)
			{
				dicPoseChara[key][item2[0]] = item2.Skip(1).ToArray();
			}
		}
		AssetBundleManager.UnloadAssetBundle(_path, isUnloadForceRefCount: false);
	}

	private void LoadOccurrence(string _path)
	{
		string match = "OccurrenceCondition_(\\d*)";
		if (!AssetBundleCheck.IsSimulation)
		{
			match = match.ToLower();
		}
		foreach (Match item in from s in AssetBundleCheck.GetAllAssetName(_path, _WithExtension: false)
			select Regex.Match(s, match) into r
			where r.Success
			select r)
		{
			foreach (List<string> item2 in from p in AssetBundleManager.LoadAsset(_path, item.Value, typeof(ExcelData)).GetAsset<ExcelData>().list.Skip(1)
				select p.list)
			{
				int key = int.Parse(item2[0]);
				if (!dicOccurrence.ContainsKey(key))
				{
					dicOccurrence.Add(key, new Dictionary<int, ConditionsInfo>());
				}
				int key2 = int.Parse(item2[1]);
				dicOccurrence[key][key2] = new ConditionsInfo(item2);
			}
		}
		AssetBundleManager.UnloadAssetBundle(_path, isUnloadForceRefCount: false);
	}

	private void LoadPreference(string _path, Match _match)
	{
		if (!_match.Success)
		{
			return;
		}
		string text = $"preference_{_match.Groups[1]:00}";
		if (!AssetBundleCheck.IsFile(_path, text) || text.Check(ignoreCase: true, AssetBundleCheck.GetAllAssetName(_path, _WithExtension: false)) == -1)
		{
			return;
		}
		ExcelData excelData = CommonLib.LoadAsset<ExcelData>(_path, text);
		if (excelData == null)
		{
			return;
		}
		foreach (List<string> item in from p in excelData.list.Skip(1)
			select p.list)
		{
			int result = -1;
			if (int.TryParse(item.SafeGet(1), out result))
			{
				dicPreference[result] = (from s in item.Skip(2).Take(3)
					select int.Parse(s)).ToArray();
			}
		}
	}

	private void LoadPreferenceCustom(string _path, Match _match)
	{
		if (!_match.Success)
		{
			return;
		}
		string text = $"custom_{_match.Groups[1]:00}";
		if (!AssetBundleCheck.IsFile(_path, text) || text.Check(ignoreCase: true, AssetBundleCheck.GetAllAssetName(_path, _WithExtension: false)) == -1)
		{
			return;
		}
		ExcelData excelData = CommonLib.LoadAsset<ExcelData>(_path, text);
		if (excelData == null)
		{
			return;
		}
		foreach (List<string> item in from p in excelData.list.Skip(1)
			select p.list)
		{
			int result = -1;
			if (int.TryParse(item.SafeGet(1), out result))
			{
				dicPreferenceCustom[result] = (from s in item.Skip(2).Take(3)
					select int.Parse(s)).ToArray();
			}
		}
	}

	private void LoadCorrection(string _path, Match _match)
	{
		if (!_match.Success)
		{
			return;
		}
		string text = $"correction_{_match.Groups[1]:00}";
		if (AssetBundleCheck.IsFile(_path, text) && text.Check(ignoreCase: true, AssetBundleCheck.GetAllAssetName(_path, _WithExtension: false)) != -1)
		{
			ExcelData excelData = CommonLib.LoadAsset<ExcelData>(_path, text);
			if (!(excelData == null))
			{
				int num = 1;
				correctionInfo.preference[0] = float.Parse(excelData.Get(num++, 2).Split('%')[0]) * 0.01f;
				correctionInfo.preference[1] = float.Parse(excelData.Get(num++, 2).Split('%')[0]) * 0.01f;
				correctionInfo.preference[2] = float.Parse(excelData.Get(num++, 2).Split('%')[0]) * 0.01f;
				correctionInfo.physical[0] = float.Parse(excelData.Get(num++, 2).Split('%')[0]) * 0.01f;
				correctionInfo.physical[1] = float.Parse(excelData.Get(num++, 2).Split('%')[0]) * 0.01f;
				correctionInfo.intellect[0] = float.Parse(excelData.Get(num++, 2).Split('%')[0]) * 0.01f;
				correctionInfo.intellect[1] = float.Parse(excelData.Get(num++, 2).Split('%')[0]) * 0.01f;
				correctionInfo.hentai[0] = float.Parse(excelData.Get(num++, 2).Split('%')[0]) * 0.01f;
				correctionInfo.hentai[1] = float.Parse(excelData.Get(num++, 2).Split('%')[0]) * 0.01f;
				correctionInfo.hentai[2] = float.Parse(excelData.Get(num++, 2).Split('%')[0]);
				correctionInfo.club[0] = int.Parse(excelData.Get(num++, 2));
				correctionInfo.club[1] = int.Parse(excelData.Get(num++, 2));
				correctionInfo.clubSkill[0] = int.Parse(excelData.Get(num++, 2));
				correctionInfo.clubSkill[1] = int.Parse(excelData.Get(num++, 2));
				correctionInfo.clubSkill[2] = int.Parse(excelData.Get(num++, 2));
			}
		}
	}

	private void LoadMotionSpeed(string _path, Match _match)
	{
		if (!_match.Success)
		{
			return;
		}
		string text = $"speed_{_match.Groups[1]:00}";
		if (!AssetBundleCheck.IsFile(_path, text) || text.Check(ignoreCase: true, AssetBundleCheck.GetAllAssetName(_path, _WithExtension: false)) == -1)
		{
			return;
		}
		ExcelData excelData = CommonLib.LoadAsset<ExcelData>(_path, text);
		if (excelData == null)
		{
			return;
		}
		List<string> list = excelData.list[0].list.Where((string s) => !s.IsNullOrEmpty()).ToList();
		foreach (List<string> item in from p in excelData.list.Skip(2)
			select p.list)
		{
			int result = -1;
			if (int.TryParse(item.SafeGet(1), out result))
			{
				List<MotionSpeed> list2 = new List<MotionSpeed>();
				for (int i = 0; i < list.Count; i++)
				{
					int num = 2 + i * 2;
					list2.Add(new MotionSpeed(list[i].Split('/'), item.SafeGet(num), item.SafeGet(num + 1)));
				}
				dicMotionSpeed[result] = list2;
			}
		}
	}

	private void LoadCommunicationCondition(string _path, Match _match)
	{
		if (!_match.Success)
		{
			return;
		}
		string text = $"CommunicationCondition_{_match.Groups[1]:00}";
		if (!AssetBundleCheck.IsFile(_path, text) || text.Check(ignoreCase: true, AssetBundleCheck.GetAllAssetName(_path, _WithExtension: false)) == -1)
		{
			return;
		}
		ExcelData excelData = CommonLib.LoadAsset<ExcelData>(_path, text);
		if (excelData == null)
		{
			return;
		}
		foreach (List<string> item in from p in excelData.list.Skip(1)
			select p.list)
		{
			int result = -1;
			if (int.TryParse(item.SafeGet(0), out result))
			{
				if (!dicMaleCondition.ContainsKey(result))
				{
					dicMaleCondition.Add(result, new Dictionary<int, ProbabilityValue>());
				}
				int result2 = -1;
				if (int.TryParse(item.SafeGet(1), out result2))
				{
					dicMaleCondition[result][result2] = new ProbabilityValue(item.SafeGet(3));
				}
			}
		}
	}

	private void LoadStaffBenefitsInfo(string _path, Match _match)
	{
		if (!_match.Success)
		{
			return;
		}
		string text = $"benefits_{_match.Groups[1]:00}";
		if (AssetBundleCheck.IsFile(_path, text) && text.Check(ignoreCase: true, AssetBundleCheck.GetAllAssetName(_path, _WithExtension: false)) != -1)
		{
			ExcelData excelData = CommonLib.LoadAsset<ExcelData>(_path, text);
			if (!(excelData == null))
			{
				int num = 1;
				benefitsOfHotel.favor[0] = float.Parse(excelData.Get(num++, 2).Split('%')[0]) * 0.01f;
				benefitsOfHotel.favor[1] = float.Parse(excelData.Get(num++, 2).Split('%')[0]) * 0.01f;
				benefitsOfHotel.confession = float.Parse(excelData.Get(num++, 2).Split('%')[0]);
				benefitsOfHotel.together = float.Parse(excelData.Get(num++, 2).Split('%')[0]);
				benefitsOfHotel.h[1] = float.Parse(excelData.Get(num++, 2).Split('%')[0]);
				benefitsOfHotel.favor[2] = float.Parse(excelData.Get(num++, 2).Split('%')[0]) * 0.01f;
				benefitsOfHotel.desire = 1f + float.Parse(excelData.Get(num++, 2).Split('%')[0]) * 0.01f;
				benefitsOfHotel.h[2] = float.Parse(excelData.Get(num++, 2).Split('%')[0]);
			}
		}
	}

	private void LoadTopicTalkPoint(string _path, Match _match)
	{
		if (!_match.Success)
		{
			return;
		}
		string text = $"TopicTalkPoint_{_match.Groups[1]:00}";
		if (AssetBundleCheck.IsFile(_path, text) && text.Check(ignoreCase: true, AssetBundleCheck.GetAllAssetName(_path, _WithExtension: false)) != -1)
		{
			ActionGame.TopicTalkPoint topicTalkPoint = CommonLib.LoadAsset<ActionGame.TopicTalkPoint>(_path, text);
			if (!(topicTalkPoint == null))
			{
				this.topicTalkPoint = new TopicTalkPoint(topicTalkPoint);
			}
		}
	}

	private void LoadTopicListen(string _path, Match _match)
	{
		if (!_match.Success)
		{
			return;
		}
		string text = $"TopicListen_{_match.Groups[1]:00}";
		if (AssetBundleCheck.IsFile(_path, text) && text.Check(ignoreCase: true, AssetBundleCheck.GetAllAssetName(_path, _WithExtension: false)) != -1)
		{
			TopicListen topicListen = CommonLib.LoadAsset<TopicListen>(_path, text);
			if (!(topicListen == null))
			{
				topicListenPoint = new TopicListenPoint(topicListen);
			}
		}
	}

	private void LoadTopicListenGroup(string _path, Match _match)
	{
		if (!_match.Success)
		{
			return;
		}
		string text = $"TopicListenGroup_{_match.Groups[1]:00}";
		if (!AssetBundleCheck.IsFile(_path, text) || text.Check(ignoreCase: true, AssetBundleCheck.GetAllAssetName(_path, _WithExtension: false)) == -1)
		{
			return;
		}
		TopicListenGroup topicListenGroup = CommonLib.LoadAsset<TopicListenGroup>(_path, text);
		if (topicListenGroup == null)
		{
			return;
		}
		if (this.topicListenGroup == null)
		{
			this.topicListenGroup = new Dictionary<int, bool[]>();
		}
		foreach (TopicListenGroup.Param item in topicListenGroup.param)
		{
			this.topicListenGroup[item.no] = item.used.ToArray();
		}
	}

	private void LoadTopicPersonalityGroup(string _path, Match _match)
	{
		if (!_match.Success)
		{
			return;
		}
		string text = $"TopicPersonalityGroup_{_match.Groups[1]:00}";
		if (!AssetBundleCheck.IsFile(_path, text) || text.Check(ignoreCase: true, AssetBundleCheck.GetAllAssetName(_path, _WithExtension: false)) == -1)
		{
			return;
		}
		TopicPersonalityGroup topicPersonalityGroup = CommonLib.LoadAsset<TopicPersonalityGroup>(_path, text);
		if (!(topicPersonalityGroup == null))
		{
			for (int i = 0; i < topicPersonalityGroup.param.Count; i++)
			{
				this.topicPersonalityGroup.Add(topicPersonalityGroup.param[i].personality.ToArray());
			}
		}
	}

	private void LoadTopicDropData(string _path, Match _match)
	{
		if (!_match.Success)
		{
			return;
		}
		string text = $"TopicDrop_{_match.Groups[1]:00}";
		if (!AssetBundleCheck.IsFile(_path, text) || text.Check(ignoreCase: true, AssetBundleCheck.GetAllAssetName(_path, _WithExtension: false)) == -1)
		{
			return;
		}
		TopicDropData topicDropData = CommonLib.LoadAsset<TopicDropData>(_path, text);
		if (topicDropData == null)
		{
			return;
		}
		foreach (TopicDropData.Param item in topicDropData.param)
		{
			topicDrops.Add(item);
		}
	}

	private void LoadPointDropData(string _path, Match _match)
	{
		if (!_match.Success)
		{
			return;
		}
		string text = $"PointDrop_{_match.Groups[1]:00}";
		if (AssetBundleCheck.IsFile(_path, text) && text.Check(ignoreCase: true, AssetBundleCheck.GetAllAssetName(_path, _WithExtension: false)) != -1)
		{
			PointDropData pointDropData = CommonLib.LoadAsset<PointDropData>(_path, text);
			if (!(pointDropData == null))
			{
				pointDrops = pointDropData.param[0].values.ToArray();
			}
		}
	}

	private void LoadTipsData(string _path, Match _match)
	{
		if (!_match.Success)
		{
			return;
		}
		string text = $"tips_{_match.Groups[1]:00}";
		if (!AssetBundleCheck.IsFile(_path, text) || text.Check(ignoreCase: true, AssetBundleCheck.GetAllAssetName(_path, _WithExtension: false)) == -1)
		{
			return;
		}
		TipsData tipsData = CommonLib.LoadAsset<TipsData>(_path, text);
		if (tipsData == null)
		{
			return;
		}
		foreach (TipsData.Param item in tipsData.param)
		{
			this.tipsData[item.no] = item;
		}
	}

	private void LoadTopicInterest(string _path, Match _match)
	{
		if (!_match.Success)
		{
			return;
		}
		string text = $"TopicInterest_{_match.Groups[1]:00}";
		if (AssetBundleCheck.IsFile(_path, text) && text.Check(ignoreCase: true, AssetBundleCheck.GetAllAssetName(_path, _WithExtension: false)) != -1)
		{
			TopicInterestData topicInterestData = CommonLib.LoadAsset<TopicInterestData>(_path, text);
			if (!(topicInterestData == null))
			{
				topicInterest = new TopicInterest(topicInterestData);
			}
		}
	}

	private void LoadTopicReply(string _path)
	{
		string match = "TopicReply_(\\d*)";
		if (!AssetBundleCheck.IsSimulation)
		{
			match = match.ToLower();
		}
		foreach (Match item in from s in AssetBundleCheck.GetAllAssetName(_path, _WithExtension: false)
			select Regex.Match(s, match) into r
			where r.Success
			select r)
		{
			TopicReply topicReply = CommonLib.LoadAsset<TopicReply>(_path, item.Value);
			if (!(topicReply == null) && int.TryParse(item.Groups[1].ToString(), out var result))
			{
				this.topicReply[result] = topicReply.param.ToDictionary((TopicReply.Param v) => v.No, (TopicReply.Param v) => v.Reply.ToArray());
			}
		}
		AssetBundleManager.UnloadAssetBundle(_path, isUnloadForceRefCount: false);
	}

	private void LoadTopicReplyNPC(string _path)
	{
		string match = "TopicReplyNPC_(-?\\d*)";
		if (!AssetBundleCheck.IsSimulation)
		{
			match = match.ToLower();
		}
		foreach (Match item in from s in AssetBundleCheck.GetAllAssetName(_path, _WithExtension: false)
			select Regex.Match(s, match) into r
			where r.Success
			select r)
		{
			TopicReply topicReply = CommonLib.LoadAsset<TopicReply>(_path, item.Value);
			if (!(topicReply == null) && int.TryParse(item.Groups[1].ToString(), out var result))
			{
				topicReplyNPC[result] = topicReply.param.ToDictionary((TopicReply.Param v) => v.No, (TopicReply.Param v) => new Peply
				{
					common = v.Reply.ToArray(),
					normal = v.ReplyN.ToArray()
				});
			}
		}
		AssetBundleManager.UnloadAssetBundle(_path, isUnloadForceRefCount: false);
	}

	private bool CheckMotion(string _state, int _personality, string _key)
	{
		List<MotionSpeed> value = null;
		if (!dicMotionSpeed.TryGetValue(_personality, out value))
		{
			return false;
		}
		return value.Find((MotionSpeed v) => v.state.Contains(_key)).state.Contains(_state);
	}

	protected override void Awake()
	{
		base.Awake();
		dicTalkInfo = new Dictionary<int, FileInfo>();
		dicOptionItemsInfo = new Dictionary<int, FileInfo>();
		dicEvnetBundleInfo = new Dictionary<int, FileInfo>();
		dicListenBundleInfo = new Dictionary<int, FileInfo>();
		dicTopicCommonBundleInfo = new Dictionary<int, FileInfo>();
		dicTopicRareBundleInfo = new Dictionary<int, FileInfo>();
		dicBestPlace = new Dictionary<int, List<List<string>>>();
		dicPoseChara = new Dictionary<int, Dictionary<string, string[]>>();
		dicOccurrence = new Dictionary<int, Dictionary<int, ConditionsInfo>>();
		dicPreference = new Dictionary<int, int[]>();
		dicPreferenceCustom = new Dictionary<int, int[]>();
		correctionInfo = new CorrectionInfo();
		dicMotionSpeed = new Dictionary<int, List<MotionSpeed>>();
		dicMaleCondition = new Dictionary<int, Dictionary<int, ProbabilityValue>>();
		benefitsOfHotel = new BenefitsOfHotelInfo();
		topicPersonalityGroup = new List<string[]>();
		topicDrops = new List<TopicDropData.Param>();
		tipsData = new Dictionary<int, TipsData.Param>();
		topicReply = new Dictionary<int, Dictionary<int, int[]>>();
		topicReplyNPC = new Dictionary<int, Dictionary<int, Peply>>();
		StartCoroutine(LoadInfo());
	}
}
