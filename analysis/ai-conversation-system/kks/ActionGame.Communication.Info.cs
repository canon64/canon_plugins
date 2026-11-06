using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ADV;
using Illusion;
using Illusion.Extensions;
using Manager;
using SaveData;
using UnityEngine;

namespace ActionGame.Communication;

public class Info
{
	public enum Group
	{
		Encounter,
		Introduction,
		Talk,
		Listen,
		Apologize,
		See,
		Touch,
		Interlude,
		LeaveAlone,
		EndConversation,
		RankUp,
		Event,
		Tips
	}

	public enum Event
	{
		Tourism,
		Play,
		Experience,
		Meal,
		FollowMe,
		Date,
		Confess,
		Sex,
		ComeToTheRoom
	}

	public enum EndADV
	{
		General = -1,
		Longing,
		Interest,
		RankUp,
		FromPlayer,
		InTheMiddle
	}

	public class TalkData
	{
		public int favor;

		public int lewdness;

		public int anger;

		public string assetbundle = "";

		public string file = "";

		public string text = "";

		public string pose = "";

		public string facialExpression = "";

		public int eyeLook = 1;

		public int neckLook;

		public int topic = -1;

		public TalkData(string _assetbundle, string _file, string _text)
		{
			assetbundle = _assetbundle;
			file = _file;
			text = _text;
		}

		public TalkData(CommunicationInfo.Param param)
		{
			favor = param.favor;
			lewdness = param.lewdness;
			anger = param.anger;
			assetbundle = param.assetbundle;
			file = param.file;
			text = param.text;
			pose = param.pose;
			facialExpression = param.facialExpression;
			eyeLook = param.eyeLook;
			neckLook = param.neckLook;
			topic = param.topic;
		}

		public TalkData(CommunicationNPCData.Param param)
		{
			assetbundle = param.assetbundle;
			file = param.file;
			text = param.text;
			pose = param.pose;
			facialExpression = param.facialExpression;
			eyeLook = param.eyeLook;
			neckLook = param.neckLook;
		}

		public TalkData(TopicTalkCommon.Param param)
		{
			assetbundle = param.bundle;
			file = param.file;
			text = param.text;
			pose = param.pose;
			facialExpression = param.facialExpression;
			eyeLook = param.eyeLook;
			neckLook = param.neckLook;
		}

		public TalkData(TopicTalkRare.Param param)
		{
			assetbundle = param.bundle;
			file = param.file;
			text = param.text;
			pose = param.pose;
			facialExpression = param.facialExpression;
			eyeLook = param.eyeLook;
			neckLook = param.neckLook;
		}

		public TalkData(TopicListenData.Param param)
		{
			assetbundle = param.bundle;
			file = param.file;
			text = param.text;
			pose = param.pose;
			facialExpression = param.facialExpression;
			eyeLook = param.eyeLook;
			neckLook = param.neckLook;
		}
	}

	public class BasicInfo
	{
		public int priority;

		public int conditions = -1;

		public int row { get; private set; }

		public BasicInfo(int _row)
		{
			row = _row;
		}
	}

	public class GenericInfo : BasicInfo
	{
		public TalkData[] talk;

		public GenericInfo(int _row, int _num)
			: base(_row)
		{
			talk = new TalkData[_num];
		}

		public GenericInfo(int _row, TalkData[] _talk)
			: base(_row)
		{
			talk = _talk;
		}

		public GenericInfo(int _row, TalkData _talk)
			: base(_row)
		{
			talk = new TalkData[1];
			talk[0] = _talk;
		}
	}

	public class BranchInfo : BasicInfo
	{
		public TalkData introduction;

		public TalkData[] success;

		public TalkData failure;

		public TalkData noInterested;

		public TalkData this[int _idx]
		{
			get
			{
				List<TalkData> list = new List<TalkData>();
				list.AddRange(success);
				list.Add(failure);
				if (noInterested != null)
				{
					list.Add(noInterested);
				}
				return list[_idx];
			}
		}

		public int num => success.Length + 1 + ((noInterested != null) ? 1 : 0);

		public BranchInfo(int _row)
			: base(_row)
		{
		}
	}

	public class ChoiceInfo : TalkData
	{
		public string choice = "ああ";

		public ChoiceInfo(CommunicationInfo.Param param)
			: base(param)
		{
		}
	}

	public class SelectInfo : BasicInfo
	{
		public TalkData introduction;

		public ChoiceInfo[] choice;

		public TalkData failure;

		public int num => choice.Length;

		public SelectInfo(int _row, int _num)
			: base(_row)
		{
			choice = new ChoiceInfo[_num];
		}
	}

	public class TopicListenInfo
	{
		public TalkData introduction;

		public TalkData success;

		public TalkData[] normal;

		public TalkData[] failure;

		public int no = -1;

		public int category = -1;

		public TalkData Normal => normal.Shuffle().First();

		public TalkData Failure => failure.Shuffle().First();
	}

	private Dictionary<int, string[]> dicOptionDisplayItems;

	private Dictionary<int, string> dicEventBundle;

	private Dictionary<int, TopicListenInfo> dicTopicListenInfo;

	private PassingInfo _passingInfo;

	private BasicInfo prevInfo;

	private Queue<int> queueListen;

	private List<BasicInfo> worksEnd;

	private HashSet<int> worksHash;

	private static readonly int sr_PrayConfess = 6;

	private static readonly int sr_PrayH = 1003;

	public Dictionary<int, Dictionary<Group, Dictionary<int, List<BasicInfo>>>> dicTalkInfo { get; private set; }

	public Dictionary<int, GenericInfo> dicTopicCommon { get; private set; }

	public Dictionary<int, GenericInfo> dicTopicRare { get; private set; }

	private PassingInfo passingInfo => _passingInfo;

	private Heroine heroine => passingInfo.heroine;

	private int relation => passingInfo.heroine.relation;

	private int relationNPC => passingInfo.heroine.relationNPC;

	private bool isNPC => heroine.fixCharaID < 0;

	public int mapNo => passingInfo.mapNo;

	private string[] placeNames => passingInfo.placeNames;

	private Cycle.Type cycleType => passingInfo.cycleType;

	private bool isOtherPeople => passingInfo.isOtherPeople;

	private bool isChase => passingInfo.isChase;

	private bool isChasePossible => passingInfo.isChasePossible;

	private bool isHPossible => passingInfo.isHPossible;

	private bool isNotice => passingInfo.isNotice;

	private int state => passingInfo.state;

	private bool exposure => passingInfo.exposure;

	private bool isAttack => passingInfo.isAttack;

	private bool femaleOnly => passingInfo.femaleOnly;

	private bool isSecondH => heroine.talkEvent.Contains(4) | heroine.talkEvent.Contains(5) | (heroine.hCount != 0);

	public void Init(PassingInfo _info)
	{
		_passingInfo = _info;
		if (_info.heroine.fixCharaID < 0)
		{
			LoadCommunicationNPCData(_info.heroine.fixCharaID);
		}
		else
		{
			LoadData(_info.heroine.FixCharaIDOrPersonality);
			LoadOptionDisplayItems(_info.heroine.FixCharaIDOrPersonality);
			LoadEventBundlePath(_info.heroine.FixCharaIDOrPersonality);
			LoadTopicListenData(_info.heroine.FixCharaIDOrPersonality);
			LoadTopicTalkCommon(_info.heroine.FixCharaIDOrPersonality);
			LoadTopicTalkRare(_info.heroine.FixCharaIDOrPersonality);
		}
		prevInfo = null;
		queueListen = new Queue<int>();
		if (worksEnd == null)
		{
			worksEnd = new List<BasicInfo>();
		}
		else
		{
			worksEnd.Clear();
		}
		if (worksHash == null)
		{
			worksHash = new HashSet<int>();
		}
		else
		{
			worksHash.Clear();
		}
	}

	public bool LoadData(int _personality)
	{
		string _bundle = "";
		string _file = "";
		if (!Singleton<GameAssist>.Instance.GetTalkFilePath(_personality, out _bundle, out _file))
		{
			return false;
		}
		AssetBundleLoadAssetOperation assetBundleLoadAssetOperation = AssetBundleManager.LoadAsset(_bundle, _file, typeof(CommunicationInfo));
		if (assetBundleLoadAssetOperation.IsEmpty())
		{
			AssetBundleManager.UnloadAssetBundle(_bundle, isUnloadForceRefCount: true);
			return false;
		}
		CommunicationInfo asset = assetBundleLoadAssetOperation.GetAsset<CommunicationInfo>();
		dicTalkInfo = new Dictionary<int, Dictionary<Group, Dictionary<int, List<BasicInfo>>>>();
		int row = 0;
		row = LoadGenericOnly(asset.param, row, 0, Group.Introduction, 14);
		List<BasicInfo> listCommand = GetListCommand(0, Group.Talk, 0);
		int failureRow = row + 6;
		for (int i = 0; i < 2; i++)
		{
			listCommand.Add(CreateBranch(asset.param, ref row, 2, failureRow));
		}
		row++;
		for (int j = 0; j < 3; j++)
		{
			listCommand.Add(CreateGeneric(asset.param, ref row, 2));
		}
		List<BasicInfo> listCommand2 = GetListCommand(0, Group.Talk, 1);
		int failureRow2 = row + 9;
		for (int k = 0; k < 3; k++)
		{
			listCommand2.Add(CreateBranch(asset.param, ref row, 2, failureRow2));
		}
		row++;
		for (int l = 0; l < 3; l++)
		{
			listCommand2.Add(CreateGeneric(asset.param, ref row, 2));
		}
		for (int m = 0; m < 6; m++)
		{
			listCommand2.Add(CreateBranch(asset.param, ref row, 1, row + 2));
			row++;
		}
		List<BasicInfo> listCommand3 = GetListCommand(0, Group.Talk, 2);
		int failureRow3 = row + 9;
		for (int n = 0; n < 3; n++)
		{
			listCommand3.Add(CreateBranch(asset.param, ref row, 2, failureRow3));
		}
		row++;
		for (int num = 0; num < 3; num++)
		{
			listCommand3.Add(CreateGeneric(asset.param, ref row, 2));
		}
		LoadTouch(0, Group.See, 2, asset.param, ref row);
		LoadTouch(0, Group.Touch, 4, asset.param, ref row);
		row = LoadGenericOnly(asset.param, row, 0, Group.Interlude, 2);
		row = LoadGenericOnly(asset.param, row, 0, Group.LeaveAlone, 2);
		row = LoadGenericOnly(asset.param, row, 0, Group.EndConversation, 9);
		row = LoadGenericOnly(asset.param, row, 0, Group.EndConversation, 1);
		LoadEvent(0, 0, 1, 0, asset.param, ref row);
		LoadEvent(0, 1, 1, 0, asset.param, ref row);
		LoadEvent(0, 2, 1, 0, asset.param, ref row);
		LoadEvent(0, 3, 1, 0, asset.param, ref row);
		LoadEvent(0, 4, 1, 0, asset.param, ref row);
		row = LoadGenericOnly(asset.param, row, 1, Group.Introduction, 17);
		List<BasicInfo> listCommand4 = GetListCommand(1, Group.Talk, 0);
		int noInterestedRow = row + 12;
		for (int num2 = 0; num2 < 4; num2++)
		{
			listCommand4.Add(CreateTalk(asset.param, ref row, noInterestedRow));
		}
		row++;
		listCommand4.Add(CreateGeneric(asset.param, ref row, 2));
		List<BasicInfo> listCommand5 = GetListCommand(1, Group.Talk, 1);
		int noInterestedRow2 = row + 12;
		for (int num3 = 0; num3 < 4; num3++)
		{
			listCommand5.Add(CreateTalk(asset.param, ref row, noInterestedRow2));
		}
		row++;
		listCommand5.Add(CreateGeneric(asset.param, ref row, 2));
		List<BasicInfo> listCommand6 = GetListCommand(1, Group.Talk, 2);
		int noInterestedRow3 = row + 12;
		for (int num4 = 0; num4 < 4; num4++)
		{
			listCommand6.Add(CreateTalk(asset.param, ref row, noInterestedRow3));
		}
		row++;
		listCommand6.Add(CreateGeneric(asset.param, ref row, 2));
		LoadTouch(1, Group.See, 2, asset.param, ref row);
		LoadTouch(1, Group.Touch, 4, asset.param, ref row);
		row = LoadGenericOnly(asset.param, row, 1, Group.Interlude, 3);
		row = LoadGenericOnly(asset.param, row, 1, Group.LeaveAlone, 2);
		row = LoadGenericOnly(asset.param, row, 1, Group.EndConversation, 9);
		LoadEvent(1, 6, 2, 1, asset.param, ref row);
		LoadEvent(1, 7, 4, 1, asset.param, ref row);
		LoadEvent(1, 0, 1, 1, asset.param, ref row);
		LoadEvent(1, 1, 1, 1, asset.param, ref row);
		LoadEvent(1, 2, 1, 1, asset.param, ref row);
		LoadEvent(1, 3, 1, 1, asset.param, ref row);
		LoadEvent(1, 4, 1, 0, asset.param, ref row);
		row = LoadGenericOnly(asset.param, row, 2, Group.Introduction, 17);
		List<BasicInfo> listCommand7 = GetListCommand(2, Group.Talk, 0);
		int noInterestedRow4 = row + 12;
		for (int num5 = 0; num5 < 4; num5++)
		{
			listCommand7.Add(CreateTalk(asset.param, ref row, noInterestedRow4));
		}
		row++;
		for (int num6 = 0; num6 < 2; num6++)
		{
			listCommand7.Add(CreateGeneric(asset.param, ref row, 2));
		}
		List<BasicInfo> listCommand8 = GetListCommand(2, Group.Talk, 1);
		int noInterestedRow5 = row + 12;
		for (int num7 = 0; num7 < 4; num7++)
		{
			listCommand8.Add(CreateTalk(asset.param, ref row, noInterestedRow5));
		}
		row++;
		for (int num8 = 0; num8 < 2; num8++)
		{
			listCommand8.Add(CreateGeneric(asset.param, ref row, 2));
		}
		List<BasicInfo> listCommand9 = GetListCommand(2, Group.Talk, 2);
		int noInterestedRow6 = row + 12;
		for (int num9 = 0; num9 < 4; num9++)
		{
			listCommand9.Add(CreateTalk(asset.param, ref row, noInterestedRow6));
		}
		row++;
		for (int num10 = 0; num10 < 2; num10++)
		{
			listCommand9.Add(CreateGeneric(asset.param, ref row, 2));
		}
		LoadTouch(2, Group.See, 2, asset.param, ref row);
		LoadTouch(2, Group.Touch, 4, asset.param, ref row);
		row = LoadGenericOnly(asset.param, row, 2, Group.Interlude, 3);
		row = LoadGenericOnly(asset.param, row, 2, Group.LeaveAlone, 2);
		row = LoadGenericOnly(asset.param, row, 2, Group.EndConversation, 9);
		int num11 = -1;
		int num12 = -1;
		List<BasicInfo> listCommand10 = GetListCommand(2, Group.Event, 7);
		listCommand10.Add(CreateGeneric(asset.param, row++));
		num11 = row;
		listCommand10.Add(CreateTalk(asset.param, ref row, -1, _introduction: false));
		num12 = row;
		listCommand10.Add(CreateSelect(asset.param, ref row, 2));
		for (int num13 = 0; num13 < 2; num13++)
		{
			listCommand10.Add(CreateBranch(asset.param, ref row, 1, row + 2));
			row++;
		}
		List<BasicInfo> listCommand11 = GetListCommand(2, Group.Event, 0);
		listCommand11.Add(CreateGeneric(asset.param, row++));
		listCommand11.Add(CreateSelect(asset.param, ref row, 2));
		List<BasicInfo> listCommand12 = GetListCommand(2, Group.Event, 1);
		listCommand12.Add(CreateGeneric(asset.param, row++));
		listCommand12.Add(CreateSelect(asset.param, ref row, 2));
		List<BasicInfo> listCommand13 = GetListCommand(2, Group.Event, 2);
		listCommand13.Add(CreateGeneric(asset.param, row++));
		listCommand13.Add(CreateSelect(asset.param, ref row, 2));
		List<BasicInfo> listCommand14 = GetListCommand(2, Group.Event, 3);
		listCommand14.Add(CreateGeneric(asset.param, row++));
		listCommand14.Add(CreateSelect(asset.param, ref row, 2));
		GetListCommand(2, Group.Event, 8).Add(CreateBranch(asset.param, ref row, 1, row + 2));
		row++;
		GetListCommand(2, Group.Event, 4).Add(CreateBranch(asset.param, ref row, 1, row + 2));
		row++;
		row += 10;
		int num14 = row;
		LoadGenericOnly(asset.param, num14 + 12, 0, Group.Introduction, 1);
		LoadGenericOnly(asset.param, num14 + 10, 1, Group.Introduction, 1);
		LoadGenericOnly(asset.param, num14 + 13, 1, Group.Introduction, 2);
		LoadGenericOnly(asset.param, num14 + 11, 2, Group.Introduction, 1);
		LoadGenericOnly(asset.param, num14 + 15, 2, Group.Introduction, 2);
		List<BasicInfo> listCommand15 = GetListCommand(2, Group.Talk, 0);
		listCommand15.Add(CreateGeneric(asset.param, ref row, 1));
		listCommand15.Add(CreateTalk(asset.param, ref row));
		List<BasicInfo> listCommand16 = GetListCommand(2, Group.Interlude, 0);
		listCommand16.Add(CreateGeneric(asset.param, ref row, 1));
		listCommand16.Add(CreateGeneric(asset.param, ref row, 3));
		List<BasicInfo> listCommand17 = GetListCommand(2, Group.Event, 7);
		BranchInfo branchInfo = CreateTalk(asset.param, ref num11, -1, _introduction: false);
		branchInfo.success[0] = LoadData(asset.param[num14 + 8], ref branchInfo.priority, ref branchInfo.conditions);
		listCommand17.Add(branchInfo);
		SelectInfo selectInfo = CreateSelect(asset.param, ref num12, 2);
		selectInfo.introduction = LoadData(asset.param[num14 + 9], ref selectInfo.priority, ref selectInfo.conditions);
		listCommand17.Add(selectInfo);
		AssetBundleManager.UnloadAssetBundle(_bundle, isUnloadForceRefCount: true);
		return true;
	}

	public bool LoadOptionDisplayItems(int _personality)
	{
		string _bundle = "";
		string _file = "";
		if (!Singleton<GameAssist>.Instance.GetOptionItemsFilePath(_personality, out _bundle, out _file))
		{
			return false;
		}
		AssetBundleLoadAssetOperation assetBundleLoadAssetOperation = AssetBundleManager.LoadAsset(_bundle, _file, typeof(ExcelData));
		if (assetBundleLoadAssetOperation.IsEmpty())
		{
			AssetBundleManager.UnloadAssetBundle(_bundle, isUnloadForceRefCount: true);
			return false;
		}
		ExcelData asset = assetBundleLoadAssetOperation.GetAsset<ExcelData>();
		if (dicOptionDisplayItems == null)
		{
			dicOptionDisplayItems = new Dictionary<int, string[]>();
		}
		else
		{
			dicOptionDisplayItems.Clear();
		}
		foreach (List<string> item in from p in asset.list.Skip(1)
			select p.list)
		{
			dicOptionDisplayItems[int.Parse(item[0])] = item.Skip(1).ToArray();
		}
		AssetBundleManager.UnloadAssetBundle(_bundle, isUnloadForceRefCount: true);
		return true;
	}

	public bool LoadEventBundlePath(int _personality)
	{
		string _bundle = "";
		string _file = "";
		if (!Singleton<GameAssist>.Instance.GetEventBundleFilePath(_personality, out _bundle, out _file))
		{
			return false;
		}
		AssetBundleLoadAssetOperation assetBundleLoadAssetOperation = AssetBundleManager.LoadAsset(_bundle, _file, typeof(ExcelData));
		if (assetBundleLoadAssetOperation.IsEmpty())
		{
			AssetBundleManager.UnloadAssetBundle(_bundle, isUnloadForceRefCount: true);
			return false;
		}
		ExcelData asset = assetBundleLoadAssetOperation.GetAsset<ExcelData>();
		if (dicEventBundle == null)
		{
			dicEventBundle = new Dictionary<int, string>();
		}
		else
		{
			dicEventBundle.Clear();
		}
		foreach (List<string> item in from p in asset.list.Skip(1)
			select p.list)
		{
			dicEventBundle[int.Parse(item[0])] = item[1];
		}
		AssetBundleManager.UnloadAssetBundle(_bundle, isUnloadForceRefCount: true);
		return true;
	}

	public bool LoadTopicListenData(int _personality)
	{
		string _bundle = "";
		string _file = "";
		if (!Singleton<GameAssist>.Instance.GetListenBundleFilePath(_personality, out _bundle, out _file))
		{
			return false;
		}
		TopicListenData tl = CommonLib.LoadAsset<TopicListenData>(_bundle, _file);
		if (tl == null)
		{
			return false;
		}
		if (dicTopicListenInfo == null)
		{
			dicTopicListenInfo = new Dictionary<int, TopicListenInfo>();
		}
		else
		{
			dicTopicListenInfo.Clear();
		}
		return Singleton<GameAssist>.Instance.GetTopicListenGroup(_personality) switch
		{
			0 => LoadA(), 
			1 => LoadB(), 
			2 => LoadC(), 
			3 => LoadD(), 
			_ => false, 
		};
		void Load(int _s, int _n, int _f)
		{
			List<TopicListenData.Param> param = tl.param;
			TopicListenInfo topicListenInfo = new TopicListenInfo();
			TopicListenData.Param param2 = param.SafeGet(_s);
			topicListenInfo.no = param2.no;
			topicListenInfo.category = param2.category;
			topicListenInfo.introduction = new TalkData(param2);
			param2 = param.SafeGet(_s + 1);
			topicListenInfo.success = new TalkData(param2);
			topicListenInfo.normal = new TalkData[2];
			for (int i = 0; i < 2; i++)
			{
				param2 = param.SafeGet(_n + i);
				topicListenInfo.normal[i] = new TalkData(param2);
			}
			topicListenInfo.failure = new TalkData[2];
			for (int j = 0; j < 2; j++)
			{
				param2 = param.SafeGet(_f + j);
				topicListenInfo.failure[j] = new TalkData(param2);
			}
			dicTopicListenInfo[topicListenInfo.no] = topicListenInfo;
		}
		bool LoadA()
		{
			Load(0, 4, 6);
			Load(2, 4, 6);
			Load(8, 16, 18);
			Load(10, 16, 18);
			Load(12, 16, 18);
			Load(14, 16, 18);
			Load(20, 24, 26);
			Load(22, 24, 26);
			Load(28, 32, 34);
			Load(30, 32, 34);
			return true;
		}
		bool LoadB()
		{
			Load(0, 6, 8);
			Load(2, 6, 8);
			Load(4, 6, 8);
			Load(10, 14, 16);
			Load(12, 14, 16);
			Load(18, 22, 24);
			Load(20, 22, 24);
			Load(26, 32, 34);
			Load(28, 32, 34);
			Load(30, 32, 34);
			return true;
		}
		bool LoadC()
		{
			Load(0, 4, 6);
			Load(2, 4, 6);
			Load(8, 16, 18);
			Load(10, 16, 18);
			Load(12, 16, 18);
			Load(14, 16, 18);
			Load(20, 24, 26);
			Load(22, 24, 26);
			Load(28, 32, 34);
			Load(30, 32, 34);
			return true;
		}
		bool LoadD()
		{
			Load(0, 6, 8);
			Load(2, 6, 8);
			Load(4, 6, 8);
			Load(10, 14, 16);
			Load(12, 14, 16);
			Load(18, 22, 24);
			Load(20, 22, 24);
			Load(26, 32, 34);
			Load(28, 32, 34);
			Load(30, 32, 34);
			return true;
		}
	}

	public bool LoadTopicTalkCommon(int _personality)
	{
		string _bundle = "";
		string _file = "";
		if (!Singleton<GameAssist>.Instance.GetTopicCommonBundleFilePath(_personality, out _bundle, out _file))
		{
			return false;
		}
		TopicTalkCommon ttc = CommonLib.LoadAsset<TopicTalkCommon>(_bundle, _file);
		if (ttc == null)
		{
			return false;
		}
		if (dicTopicCommon == null)
		{
			dicTopicCommon = new Dictionary<int, GenericInfo>();
		}
		int num = 0;
		int num2 = 0;
		for (int i = 0; i < 6; i++)
		{
			dicTopicCommon[num++] = Load(num2);
			num2 += 2;
		}
		for (int j = 0; j < 6; j++)
		{
			dicTopicCommon[num++] = Load(num2);
			num2 += 2;
		}
		for (int k = 0; k < 6; k++)
		{
			dicTopicCommon[num++] = Load(num2);
			num2 += 2;
		}
		for (int l = 0; l < 6; l++)
		{
			dicTopicCommon[num++] = Load(num2);
			num2 += 2;
		}
		for (int m = 0; m < 3; m++)
		{
			dicTopicCommon[num++] = Load(num2);
			num2 += 2;
		}
		for (int n = 0; n < 2; n++)
		{
			TopicTalkCommon.Param param = ttc.param[54 + n];
			dicTopicCommon[num++] = new GenericInfo(54 + n, new TalkData(param.bundle, param.file, param.text));
		}
		return true;
		GenericInfo Load(int _s)
		{
			GenericInfo genericInfo = new GenericInfo(_s, 2);
			TopicTalkCommon.Param param2 = ttc.param[_s];
			TopicTalkCommon.Param param3 = ttc.param[_s + 1];
			genericInfo.talk[0] = new TalkData(param2);
			genericInfo.talk[1] = new TalkData(param3);
			return genericInfo;
		}
	}

	public bool LoadTopicTalkRare(int _personality)
	{
		string _bundle = "";
		string _file = "";
		if (!Singleton<GameAssist>.Instance.GetTopicRareBundleFilePath(_personality, out _bundle, out _file))
		{
			return false;
		}
		TopicTalkRare topicTalkRare = CommonLib.LoadAsset<TopicTalkRare>(_bundle, _file);
		if (topicTalkRare == null)
		{
			return false;
		}
		if (dicTopicRare == null)
		{
			dicTopicRare = new Dictionary<int, GenericInfo>();
		}
		else
		{
			dicTopicRare.Clear();
		}
		int num = -1;
		List<TalkData> list = new List<TalkData>();
		foreach (TopicTalkRare.Param item in topicTalkRare.param)
		{
			if (num != item.no)
			{
				if (list.Any())
				{
					dicTopicRare[num] = new GenericInfo(-1, list.ToArray());
				}
				num = item.no;
				_ = item.category;
				list.Clear();
			}
			list.Add(new TalkData(item));
		}
		dicTopicRare[num] = new GenericInfo(-1, list.ToArray());
		return true;
	}

	public bool LoadCommunicationNPCData(int _personality)
	{
		string _bundle = "";
		string _file = "";
		if (!Singleton<GameAssist>.Instance.GetTalkFilePath(_personality, out _bundle, out _file))
		{
			return false;
		}
		CommunicationNPCData communicationNPCData = CommonLib.LoadAsset<CommunicationNPCData>(_bundle, _file);
		if (communicationNPCData == null)
		{
			return false;
		}
		dicTalkInfo = new Dictionary<int, Dictionary<Group, Dictionary<int, List<BasicInfo>>>>();
		int row = 0;
		List<CommunicationNPCData.Param> lst = communicationNPCData.param;
		LoadGeneric(0, Group.Introduction, 3);
		LoadGenericLen(0, Group.Listen, 3, 2);
		LoadGeneric(0, Group.EndConversation, 1);
		LoadGeneric(1, Group.Introduction, 6);
		LoadGenericLen(1, Group.Listen, 3, 2);
		LoadGeneric(1, Group.EndConversation, 1);
		LoadGeneric(2, Group.Introduction, 6);
		LoadGenericLen(2, Group.Listen, 4, 2);
		LoadGeneric(2, Group.EndConversation, 1);
		LoadGeneric(3, Group.Introduction, 6);
		LoadGenericLen(3, Group.Listen, 4, 2);
		LoadGeneric(3, Group.EndConversation, 1);
		LoadGenericADV(0, Group.RankUp, 0, 11);
		LoadGenericADV(1, Group.RankUp, 0, 10);
		LoadGenericADV(2, Group.RankUp, 0, 15);
		LoadGenericLen(0, Group.Talk, 7, 2);
		LoadGenericLen(1, Group.Talk, 8, 2);
		LoadGenericADV(0, Group.Tips, 0, 4);
		LoadGenericADV(0, Group.Tips, 0, 3);
		LoadGenericADV(0, Group.Tips, 0, 5);
		LoadGenericADV(0, Group.Tips, 0, 4);
		LoadGenericADV(0, Group.Tips, 0, 4);
		LoadGenericADV(0, Group.Tips, 0, 3);
		LoadGenericADV(0, Group.Tips, 0, 4);
		LoadGenericADV(0, Group.Tips, 0, 4);
		LoadGenericADV(1, Group.Tips, 0, 4);
		LoadGenericADV(1, Group.Tips, 0, 3);
		LoadGenericADV(1, Group.Tips, 0, 5);
		LoadGenericADV(1, Group.Tips, 0, 4);
		LoadGenericADV(1, Group.Tips, 0, 4);
		LoadGenericADV(1, Group.Tips, 0, 3);
		LoadGenericADV(1, Group.Tips, 0, 4);
		LoadGenericADV(1, Group.Tips, 0, 4);
		LoadGenericADV(3, Group.Event, 0, 8);
		LoadGenericADV(3, Group.Event, 1, 2);
		LoadGenericADV(3, Group.Event, 8, 2);
		LoadGenericADV(0, Group.See, 0, 1);
		LoadGenericADV(0, Group.See, 1, 1);
		LoadGenericADV(0, Group.Touch, 0, 1);
		LoadGenericADV(0, Group.Touch, 1, 1);
		LoadGenericADV(0, Group.Touch, 2, 1);
		LoadGenericADV(0, Group.Touch, 3, 1);
		LoadGeneric(0, Group.LeaveAlone, 1);
		LoadGenericADV(1, Group.See, 0, 1);
		LoadGenericADV(1, Group.See, 1, 1);
		LoadGenericADV(1, Group.Touch, 0, 1);
		LoadGenericADV(1, Group.Touch, 1, 1);
		LoadGenericADV(1, Group.Touch, 2, 1);
		LoadGenericADV(1, Group.Touch, 3, 1);
		LoadGeneric(1, Group.LeaveAlone, 1);
		LoadGenericADV(3, Group.See, 0, 1);
		LoadGenericADV(3, Group.See, 1, 1);
		LoadGenericADV(3, Group.Touch, 0, 1);
		LoadGenericADV(3, Group.Touch, 1, 1);
		LoadGenericADV(3, Group.Touch, 2, 1);
		LoadGenericADV(3, Group.Touch, 3, 1);
		LoadGeneric(3, Group.LeaveAlone, 1);
		return true;
		void LoadGeneric(int _stage, Group _group, int _num)
		{
			List<BasicInfo> listCommand3 = GetListCommand(_stage, _group, 0);
			for (int l = 0; l < _num; l++)
			{
				CommunicationNPCData.Param param3 = lst[row];
				listCommand3.Add(new GenericInfo(row, new TalkData(param3))
				{
					priority = param3.priority,
					conditions = param3.conditions
				});
				row++;
			}
		}
		void LoadGenericADV(int _stage, Group _group, int _command, int _len)
		{
			List<BasicInfo> listCommand = GetListCommand(_stage, _group, _command);
			GenericInfo genericInfo = new GenericInfo(row, _len);
			for (int i = 0; i < _len; i++)
			{
				CommunicationNPCData.Param param = lst[row++];
				if (i == 0)
				{
					genericInfo.priority = param.priority;
					genericInfo.conditions = param.conditions;
				}
				genericInfo.talk[i] = new TalkData(param);
			}
			listCommand.Add(genericInfo);
		}
		void LoadGenericLen(int _stage, Group _group, int _num, int _len)
		{
			List<BasicInfo> listCommand2 = GetListCommand(_stage, _group, 0);
			for (int j = 0; j < _num; j++)
			{
				GenericInfo genericInfo2 = new GenericInfo(row, _len);
				for (int k = 0; k < _len; k++)
				{
					CommunicationNPCData.Param param2 = lst[row++];
					if (k == 0)
					{
						genericInfo2.priority = param2.priority;
						genericInfo2.conditions = param2.conditions;
					}
					genericInfo2.talk[k] = new TalkData(param2);
				}
				listCommand2.Add(genericInfo2);
			}
		}
	}

	public List<Program.Transfer> GetEncounterADV(bool _isGeneric)
	{
		BasicInfo basicInfo = GetListCommand(0, Group.Encounter, 0)[_isGeneric ? 1 : 0];
		return CreateGenericADV(basicInfo as GenericInfo);
	}

	public List<Program.Transfer> GetIntroductionADV(ref bool _isEnd, ref int _eventNo, out ChangeValueAbstractInfo _outInfo, bool _check = false)
	{
		int num = Mathf.Clamp(relation, 0, 2);
		worksEnd.Clear();
		foreach (BasicInfo item in from v in GetListCommand(num, Group.Introduction, 0)
			where !heroine.talkIntro.Contains(v.conditions)
			orderby v.priority descending
			select v)
		{
			switch (item.conditions)
			{
			case 1:
			{
				if (isOtherPeople)
				{
					break;
				}
				int num2 = mapNo;
				if (num2 == 28 || (uint)(num2 - 30) <= 1u)
				{
					if (num < 2 || heroine.talkTime <= 0)
					{
						_isEnd = true;
					}
					return Create(ref _eventNo, out _outInfo, item, _isEnd);
				}
				break;
			}
			case 2:
			{
				int num2 = mapNo;
				if (num2 == 28 || (uint)(num2 - 30) <= 1u)
				{
					if (num < 2 || heroine.talkTime <= 0)
					{
						_isEnd = true;
					}
					return Create(ref _eventNo, out _outInfo, item, _isEnd);
				}
				break;
			}
			case 3:
			{
				if (isOtherPeople)
				{
					break;
				}
				int num2 = mapNo;
				if (num2 == 17 || num2 == 33)
				{
					if (num < 2 || heroine.talkTime <= 0)
					{
						_isEnd = true;
					}
					return Create(ref _eventNo, out _outInfo, item, _isEnd);
				}
				break;
			}
			case 4:
			{
				if (num >= 1 && !isOtherPeople)
				{
					break;
				}
				int num2 = mapNo;
				if (num2 == 17 || num2 == 33)
				{
					if (num < 2 || heroine.talkTime <= 0)
					{
						_isEnd = true;
					}
					return Create(ref _eventNo, out _outInfo, item, _isEnd);
				}
				break;
			}
			case 5:
				if (heroine.talkTime <= 0)
				{
					_isEnd = true;
					return Create(ref _eventNo, out _outInfo, item, _isEnd);
				}
				break;
			case 30:
			{
				if (isOtherPeople)
				{
					break;
				}
				int num2 = mapNo;
				if ((uint)(num2 - 13) <= 1u)
				{
					if (num < 2 || heroine.talkTime <= 0)
					{
						_isEnd = true;
					}
					return Create(ref _eventNo, out _outInfo, item, _isEnd);
				}
				break;
			}
			case 31:
			{
				if (num >= 1 && !isOtherPeople)
				{
					break;
				}
				int num2 = mapNo;
				if ((uint)(num2 - 13) <= 1u)
				{
					if (num < 2 || heroine.talkTime <= 0)
					{
						_isEnd = true;
					}
					return Create(ref _eventNo, out _outInfo, item, _isEnd);
				}
				break;
			}
			case 11:
				if (femaleOnly)
				{
					_isEnd = true;
					return Create(ref _eventNo, out _outInfo, item, _isEnd);
				}
				break;
			case 6:
				if (!isOtherPeople && heroine.chaCtrl.GetNowClothesType() % 2 == 1)
				{
					heroine.talkIntro.Add(8);
					return Create(ref _eventNo, out _outInfo, item, _isEnd);
				}
				break;
			case 7:
				if (isOtherPeople && heroine.chaCtrl.GetNowClothesType() % 2 == 1)
				{
					return Create(ref _eventNo, out _outInfo, item, _isEnd);
				}
				break;
			case 8:
				if (!isOtherPeople && heroine.NowCoordinate == 1)
				{
					return Create(ref _eventNo, out _outInfo, item, _isEnd);
				}
				break;
			case 9:
				if (isOtherPeople && heroine.NowCoordinate == 1)
				{
					return Create(ref _eventNo, out _outInfo, item, _isEnd);
				}
				break;
			case 10:
				if (isChase)
				{
					return Create(ref _eventNo, out _outInfo, item, _isEnd);
				}
				break;
			case 12:
				if (Mathf.InverseLerp(0f, SingletonInitializer<ActionScene>.instance.stepUpDic[num], heroine.favor) >= 0.5f)
				{
					return Create(ref _eventNo, out _outInfo, item, _isEnd);
				}
				break;
			case 13:
				if (heroine.lewdness >= 50)
				{
					return Create(ref _eventNo, out _outInfo, item, _isEnd);
				}
				break;
			case 14:
				if (!isNotice)
				{
					return Create(ref _eventNo, out _outInfo, item, _isEnd);
				}
				break;
			case 32:
				if (isAttack)
				{
					return Create(ref _eventNo, out _outInfo, item, _isEnd);
				}
				break;
			default:
				worksEnd.Add(item);
				break;
			case 101:
				break;
			}
		}
		return Create(ref _eventNo, out _outInfo, worksEnd.Shuffle().First(), _isEnd);
		List<Program.Transfer> Create(ref int _event, out ChangeValueAbstractInfo _info, BasicInfo _bi, bool _end)
		{
			_event = _bi.conditions;
			if (!_check && MathfEx.RangeEqualOn(12, _event, 13))
			{
				heroine.talkIntro.Add(_event);
			}
			_info = new ChangeValueInfo();
			return CreateGenericADV(_bi as GenericInfo, _info as ChangeValueInfo, !_end);
		}
	}

	public HashSet<int> ConfirmEvent(int _stage)
	{
		_stage = Mathf.Clamp(_stage, 0, 2);
		Func<int, bool, bool> func = null;
		worksHash.Clear();
		func = _stage switch
		{
			2 => ConfirmEvent2, 
			1 => ConfirmEvent1, 
			_ => ConfirmEvent0, 
		};
		foreach (KeyValuePair<int, List<BasicInfo>> item in GetDictionaryGroup(_stage, Group.Event))
		{
			foreach (BasicInfo item2 in item.Value.OrderByDescending((BasicInfo o) => o.priority))
			{
				if (func(item2.conditions, arg2: false))
				{
					worksHash.Add(item.Key);
				}
				if (worksHash.Contains(item.Key))
				{
					break;
				}
			}
		}
		return worksHash;
	}

	public HashSet<int> ConfirmR18(int _stage)
	{
		_stage = Mathf.Clamp(_stage, 0, 2);
		worksHash.Clear();
		switch (_stage)
		{
		case 1:
			worksHash.Add(0);
			break;
		case 2:
			worksHash.Add(0);
			worksHash.Add(1);
			break;
		}
		return worksHash;
	}

	public TopicListenInfo GetTopicListenInfo(int _no)
	{
		if (!dicTopicListenInfo.TryGetValue(_no, out var value))
		{
			return null;
		}
		return value;
	}

	public List<Program.Transfer> GetIntroductionADV(ref bool _isEnd)
	{
		worksEnd.Clear();
		foreach (BasicInfo item in from v in GetListCommand(relationNPC, Group.Introduction, 0)
			orderby v.priority descending
			select v)
		{
			switch (item.conditions)
			{
			case 0:
				if (heroine.talkTime <= 0 && (cycleType == Cycle.Type.Morning || cycleType == Cycle.Type.Daytime))
				{
					_isEnd = true;
					return Create(item, _isEnd);
				}
				break;
			case 1:
				if (heroine.talkTime <= 0 && (cycleType == Cycle.Type.Evening || cycleType == Cycle.Type.Night))
				{
					_isEnd = true;
					return Create(item, _isEnd);
				}
				break;
			case 2:
				if (cycleType == Cycle.Type.Morning || cycleType == Cycle.Type.Daytime)
				{
					worksEnd.Add(item);
				}
				break;
			case 3:
				if (cycleType == Cycle.Type.Evening || cycleType == Cycle.Type.Night)
				{
					worksEnd.Add(item);
				}
				break;
			default:
				worksEnd.Add(item);
				break;
			}
		}
		return Create(worksEnd.Shuffle().First(), _isEnd);
		List<Program.Transfer> Create(BasicInfo _bi, bool _end)
		{
			return CreateGenericADV(_bi as GenericInfo, _close: true, !_end);
		}
	}

	public List<Program.Transfer> GetTipsADV(ref int index)
	{
		int stage = Mathf.Clamp(relationNPC, 0, 1);
		List<BasicInfo> listCommand = GetListCommand(stage, Group.Tips, 0);
		HashSet<int> tips = Game.saveData.tips;
		var anon = (from d in listCommand.Select((BasicInfo v, int i) => new { v, i })
			where !tips.Contains(d.i)
			select d).Shuffle().First();
		index = anon.i;
		return CreateGenericADV(anon.v as GenericInfo);
	}

	public bool CheckTips()
	{
		int stage = Mathf.Clamp(relationNPC, 0, 1);
		List<BasicInfo> listCommand = GetListCommand(stage, Group.Tips, 0);
		HashSet<int> tips = Game.saveData.tips;
		return listCommand.Count > tips.Count;
	}

	public List<Program.Transfer> GetListenADV()
	{
		GenericInfo info = GetListCommand(relationNPC, Group.Listen, 0).Shuffle().First() as GenericInfo;
		return CreateGenericADV(info);
	}

	public List<Program.Transfer> GetTalkADV(int _idx)
	{
		int stage = Mathf.Clamp(relationNPC, 0, 1);
		List<BasicInfo> listCommand = GetListCommand(stage, Group.Talk, 0);
		return CreateGenericADV(listCommand[_idx] as GenericInfo);
	}

	public List<Program.Transfer> GetRankUpADV()
	{
		switch (relationNPC)
		{
		case 0:
			if (heroine.favor < SingletonInitializer<ActionScene>.instance.stepUpDic[0])
			{
				return null;
			}
			break;
		case 1:
			if (heroine.favor < SingletonInitializer<ActionScene>.instance.stepUpDic[1])
			{
				return null;
			}
			break;
		default:
			return null;
		}
		return CreateGenericADV(GetListCommand(relationNPC, Group.RankUp, 0)[0] as GenericInfo);
	}

	public List<Program.Transfer> GetReminiscenceADV()
	{
		return CreateGenericADV(GetListCommand(relationNPC, Group.RankUp, 0)[0] as GenericInfo);
	}

	public List<Program.Transfer> GetEndConversationADV(bool _endSelect = false)
	{
		if (!(_endSelect | (heroine.talkTime <= 0)))
		{
			return null;
		}
		return CreateGenericADV(GetListCommand(relationNPC, Group.EndConversation, 0)[0] as GenericInfo);
	}

	public List<Program.Transfer> GetADV(out ChangeValueAbstractInfo _outInfo, Group _group, int _command = -1)
	{
		int stage = Mathf.Clamp(relation, 0, 2);
		List<BasicInfo> list = null;
		if (_command != -1)
		{
			list = GetListCommand(stage, _group, _command);
		}
		else
		{
			Dictionary<int, List<BasicInfo>> dic = GetDictionaryGroup(stage, _group);
			if (_group == Group.Listen)
			{
				int num = (from v in dic.Keys
					where dic[v].Any((BasicInfo b) => ConditionDeterminationListen(b.conditions))
					where !queueListen.Contains(v)
					select v into i
					orderby Guid.NewGuid()
					select i).First();
				queueListen.Enqueue(num);
				if (queueListen.Count > 2)
				{
					queueListen.Dequeue();
				}
				list = dic[num];
			}
			else
			{
				list = dic[dic.Keys.OrderBy((int i) => Guid.NewGuid()).First()];
			}
		}
		Func<int, bool> func = null;
		func = _group switch
		{
			Group.Talk => ConditionDeterminationSpeak, 
			Group.Listen => ConditionDeterminationListen, 
			_ => delegate(int _c)
			{
				_ = _c - -1;
				_ = 1;
				return true;
			}, 
		};
		List<BasicInfo> list2 = new List<BasicInfo>();
		foreach (BasicInfo item in list.OrderByDescending((BasicInfo v) => v.priority))
		{
			if (!list2.IsNullOrEmpty() && list2[0].priority > item.priority)
			{
				break;
			}
			if (func(item.conditions))
			{
				list2.Add(item);
			}
		}
		if (list2.Count > 1 && prevInfo != null)
		{
			list2.Remove(prevInfo);
		}
		BasicInfo basicInfo = (prevInfo = list2.Shuffle().First());
		if (basicInfo != null)
		{
			if (basicInfo is GenericInfo genericInfo)
			{
				GenericInfo info = genericInfo;
				_outInfo = new ChangeValueInfo();
				return CreateGenericADV(info, _outInfo as ChangeValueInfo);
			}
			if (basicInfo is BranchInfo branchInfo)
			{
				BranchInfo info2 = branchInfo;
				_outInfo = new ChangeValueInfo();
				return CreateBranchADV(info2, _outInfo as ChangeValueInfo);
			}
			if (basicInfo is SelectInfo selectInfo)
			{
				SelectInfo info3 = selectInfo;
				_outInfo = new ChangeValueSelectInfo();
				return CreateSelectADV(info3, _outInfo as ChangeValueSelectInfo);
			}
		}
		_outInfo = null;
		return null;
	}

	public List<Program.Transfer> GetEventADV(int _command, ref int _derive, ref bool _first, ref bool _success)
	{
		_first = false;
		_success = false;
		int num = Mathf.Clamp(relation, 0, 2);
		switch (num)
		{
		case 0:
			return GetStage1Branch0(num, _command, ref _derive);
		case 1:
			switch (_command)
			{
			case 6:
				if (!isNPC)
				{
					bool flag6 = mapNo == 9;
					BranchInfo branchInfo3 = GetListCommand(1, Group.Event, _command)[flag6 ? 1 : 0] as BranchInfo;
					int num5 = (int)Singleton<GameAssist>.Instance.GetCommunicationConndition(1, branchInfo3.conditions).Get(heroine.favor);
					if (Game.IsSameAccommodation(heroine))
					{
						num5 = (int)Mathf.Clamp((float)num5 + Singleton<GameAssist>.Instance.benefitsOfHotel.confession, 0f, 100f);
					}
					bool flag7 = RandomBranch(num5, 100 - num5) == 0;
					if (Game.saveData.prayedResult == sr_PrayConfess)
					{
						Game.saveData.prayedResult = -1;
						flag7 = true;
					}
					_success = flag7;
					List<Program.Transfer> result4 = (flag6 ? null : CreateBranchADV(branchInfo3, flag7));
					if (flag7)
					{
						heroine.talkEvent.Add(1);
						heroine.talkEvent.Add(2);
						heroine.isGirlfriend = true;
						if (!flag6)
						{
							heroine.favor = 0;
						}
						Game.Player.girlfriendedCnt++;
						if (!heroine.isFirstGirlfriend)
						{
							heroine.isFirstGirlfriend = true;
							SingletonInitializer<Game>.instance.rankSaveData.girlfriendCount++;
							return result4;
						}
					}
					else
					{
						_derive = 0;
					}
					return result4;
				}
				heroine.isGirlfriend = true;
				heroine.relationNPC = 3;
				return CreateGenericADV(GetListCommand(2, Group.RankUp, 0)[0] as GenericInfo);
			case 20:
			{
				BranchInfo branchInfo2 = null;
				bool flag4 = isSecondH;
				foreach (BasicInfo item in from o in GetListCommand(1, Group.Event, 7)
					orderby o.priority descending
					select o)
				{
					switch (item.conditions)
					{
					case 2:
						if (!flag4)
						{
							branchInfo2 = item as BranchInfo;
						}
						break;
					case 3:
						if (flag4)
						{
							branchInfo2 = item as BranchInfo;
						}
						break;
					case 4:
						if (flag4 && isOtherPeople)
						{
							branchInfo2 = item as BranchInfo;
						}
						break;
					case 5:
						if (flag4 && exposure)
						{
							branchInfo2 = item as BranchInfo;
						}
						break;
					}
					if (branchInfo2 != null)
					{
						break;
					}
				}
				int num4 = (int)Singleton<GameAssist>.Instance.GetCommunicationConndition(1, branchInfo2.conditions).Get(heroine.lewdness);
				if (Game.IsSameAccommodation(heroine))
				{
					num4 = (int)Mathf.Clamp((float)num4 + Singleton<GameAssist>.Instance.benefitsOfHotel.h[1], 0f, 100f);
				}
				if (Game.saveData.prayedResult == sr_PrayH && SingletonInitializer<ActionScene>.instance.prayInfoTable.TryGetValue(sr_PrayH, out var value2))
				{
					num4 = Mathf.Clamp(num4 + value2.Effect, 0, 100);
				}
				bool flag5 = RandomBranch(num4, 100 - num4) == 0;
				List<Program.Transfer> result3 = CreateBranchADV(branchInfo2, flag5);
				if (flag5)
				{
					heroine.talkEvent.Add(4);
					_derive = 2;
				}
				return result3;
			}
			default:
				return GetStage1Branch0(num, _command, ref _derive);
			}
		case 2:
			switch (_command)
			{
			case 6:
				if (!isNPC)
				{
					return null;
				}
				heroine.isGirlfriend = true;
				heroine.relationNPC = 3;
				return CreateGenericADV(GetListCommand(2, Group.RankUp, 0)[0] as GenericInfo);
			case 20:
				if (!isNPC)
				{
					bool flag2 = isSecondH;
					foreach (BasicInfo item2 in from o in GetListCommand(2, Group.Event, 7)
						orderby o.priority descending
						select o)
					{
						GameAssist.ProbabilityValue communicationConndition = Singleton<GameAssist>.Instance.GetCommunicationConndition(2, item2.conditions);
						if (communicationConndition == null)
						{
							continue;
						}
						int num3 = (int)communicationConndition.Get(heroine.lewdness);
						if (Game.IsSameAccommodation(heroine))
						{
							num3 = (int)Mathf.Clamp((float)num3 + Singleton<GameAssist>.Instance.benefitsOfHotel.h[2], 0f, 100f);
						}
						if (Game.saveData.prayedResult == sr_PrayH && SingletonInitializer<ActionScene>.instance.prayInfoTable.TryGetValue(sr_PrayH, out var value))
						{
							num3 = Mathf.Clamp(num3 + value.Effect, 0, 100);
						}
						bool flag3 = RandomBranch(num3, 100 - num3) == 0;
						switch (item2.conditions)
						{
						case 0:
							if (!flag2)
							{
								heroine.talkEvent.Add(5);
								_derive = 2;
								return CreateGenericADV(item2 as GenericInfo);
							}
							break;
						case 3:
							if (flag2)
							{
								if (flag3)
								{
									_derive = 2;
								}
								return CreateBranchADV(item2 as BranchInfo, flag3);
							}
							break;
						case 4:
							if (flag2 && isOtherPeople)
							{
								if (flag3)
								{
									_derive = 2;
								}
								return CreateBranchADV(item2 as BranchInfo, flag3);
							}
							break;
						case 5:
							if (flag2 && exposure)
							{
								if (flag3)
								{
									_derive = 2;
								}
								return CreateBranchADV(item2 as BranchInfo, flag3);
							}
							break;
						}
					}
					return null;
				}
				_derive = 2;
				return CreateGenericADV(GetListCommand(3, Group.Event, (!heroine.isVirgin) ? 1 : 0)[0] as GenericInfo);
			case 21:
				if (!isNPC)
				{
					BranchInfo branchInfo = GetListCommand(2, Group.Event, 8)[0] as BranchInfo;
					int num2 = (int)Singleton<GameAssist>.Instance.GetCommunicationConndition(2, branchInfo.conditions).Get(heroine.lewdness);
					bool flag = RandomBranch(num2, 100 - num2) == 0;
					List<Program.Transfer> result2 = CreateBranchADV(branchInfo, flag);
					if (flag)
					{
						_derive = 3;
					}
					return result2;
				}
				_derive = 3;
				return CreateGenericADV(GetListCommand(3, Group.Event, 8)[0] as GenericInfo);
			case 4:
			{
				BranchInfo info = GetListCommand(2, Group.Event, _command)[0] as BranchInfo;
				List<Program.Transfer> result = CreateBranchADV(info, _success: true);
				_derive = 1;
				return result;
			}
			default:
				return GetStage2Generic(_command, ref _derive);
			}
		default:
			return null;
		}
	}

	public List<Program.Transfer> GetEventFromHeroineADV(out ChangeValueAbstractInfo _outInfo, ref int _derive)
	{
		int num = Mathf.Clamp(relation, 0, 2);
		List<Tuple<float, BasicInfo>> list = new List<Tuple<float, BasicInfo>>();
		Func<int, float> func = null;
		func = num switch
		{
			2 => ConfirmEvent2, 
			1 => ConfirmEvent1, 
			_ => ConfirmEvent0, 
		};
		foreach (KeyValuePair<int, List<BasicInfo>> item in GetDictionaryGroup(num, Group.Event))
		{
			foreach (BasicInfo item2 in item.Value.OrderByDescending((BasicInfo o) => o.priority))
			{
				float num2 = func(item2.conditions);
				if (num2 > 0f)
				{
					list.Add(new Tuple<float, BasicInfo>(num2, item2));
				}
			}
		}
		if (list.Count == 0)
		{
			_outInfo = null;
			return null;
		}
		Tuple<float, BasicInfo> tuple = list.Shuffle().ElementAt(0);
		if (!Utils.ProbabilityCalclator.DetectFromPercent(tuple.Item1))
		{
			_outInfo = null;
			return null;
		}
		return GetHeroineADV(tuple.Item2, num, out _outInfo, ref _derive);
		float Check(int _stage, int _condition)
		{
			GameAssist.ConditionsInfo occurrenceCondition = Singleton<GameAssist>.Instance.GetOccurrenceCondition(_stage, _condition);
			if (occurrenceCondition == null)
			{
				return 0f;
			}
			if (!occurrenceCondition.favor.through && !occurrenceCondition.favor.Check(heroine.favor))
			{
				return 0f;
			}
			if (!occurrenceCondition.lewdness.through && !occurrenceCondition.lewdness.Check(heroine.lewdness))
			{
				return 0f;
			}
			if (occurrenceCondition.probability.through)
			{
				return 100f;
			}
			return occurrenceCondition.probability.Get((occurrenceCondition.reference == 0) ? heroine.favor : heroine.lewdness);
		}
		static float ConfirmEvent0(int _conditions)
		{
			return 0f;
		}
		float ConfirmEvent1(int _conditions)
		{
			bool flag = heroine.talkTime <= 0;
			switch (_conditions)
			{
			case 100:
				if (!(heroine.confessed || heroine.talkCommand.Contains(6) || flag))
				{
					return Check(1, _conditions);
				}
				break;
			case 101:
				if (isHPossible && !heroine.talkCommand.Contains(7) && flag)
				{
					return Check(1, _conditions);
				}
				break;
			case 102:
				if (!(heroine.talkCommand.Contains(0) || flag))
				{
					return Check(1, _conditions);
				}
				break;
			case 103:
				if (!(heroine.talkCommand.Contains(1) || flag))
				{
					return Check(1, _conditions);
				}
				break;
			case 104:
				if (!(heroine.talkCommand.Contains(2) || flag))
				{
					return Check(1, _conditions);
				}
				break;
			case 105:
				if (!(heroine.talkCommand.Contains(3) || flag) && !heroine.isLunch)
				{
					return Check(1, _conditions);
				}
				break;
			case 106:
				if (!(heroine.talkCommand.Contains(5) || flag))
				{
					return Check(1, _conditions);
				}
				break;
			}
			return 0f;
		}
		float ConfirmEvent2(int _conditions)
		{
			bool flag2 = heroine.talkTime <= 0;
			switch (_conditions)
			{
			case 201:
				if (isHPossible && !heroine.talkCommand.Contains(7) && flag2)
				{
					return Check(2, _conditions);
				}
				break;
			case 207:
				if (isHPossible && !heroine.talkCommand.Contains(7) && flag2 && heroine.talkEvent.Contains(55))
				{
					return Check(2, _conditions);
				}
				break;
			case 202:
				if (!(heroine.talkCommand.Contains(0) || flag2))
				{
					return Check(2, _conditions);
				}
				break;
			case 203:
				if (!(heroine.talkCommand.Contains(1) || flag2))
				{
					return Check(2, _conditions);
				}
				break;
			case 204:
				if (!(heroine.talkCommand.Contains(2) || flag2))
				{
					return Check(2, _conditions);
				}
				break;
			case 205:
				if (!(heroine.talkCommand.Contains(3) || flag2) && !heroine.isLunch)
				{
					return Check(2, _conditions);
				}
				break;
			case 206:
				if (!(heroine.talkCommand.Contains(5) || flag2))
				{
					return Check(2, _conditions);
				}
				break;
			}
			return 0f;
		}
	}

	public List<Program.Transfer> GetEventFromHeroineADV(int _command, out ChangeValueAbstractInfo _outInfo, ref int _derive)
	{
		int num = Mathf.Clamp(relation, 1, 2);
		int conditions = ((num == 1) ? 101 : 201);
		BasicInfo target = null;
		if (_command == 3)
		{
			target = GetListCommand(num, Group.Event, 7).Find((BasicInfo v) => v.conditions == conditions);
		}
		return GetHeroineADV(target, num, out _outInfo, ref _derive);
	}

	public List<Program.Transfer> GetInterludeADV(ChangeValueAbstractInfo _cvInfo)
	{
		int stage = Mathf.Clamp(relation, 0, 2);
		BasicInfo basicInfo = null;
		foreach (BasicInfo item in from v in GetListCommand(stage, Group.Interlude, 0)
			orderby v.priority descending
			select v)
		{
			switch (item.conditions)
			{
			case 1:
				if (_cvInfo != null && _cvInfo.lewdness > 0 && Utils.ProbabilityCalclator.DetectFromPercent(30f))
				{
					basicInfo = item;
				}
				break;
			case 0:
				if (_cvInfo != null && _cvInfo.favor > 0 && Utils.ProbabilityCalclator.DetectFromPercent(30f))
				{
					basicInfo = item;
				}
				break;
			default:
				if (Utils.ProbabilityCalclator.DetectFromPercent(30f))
				{
					basicInfo = item;
				}
				break;
			}
			if (basicInfo != null)
			{
				break;
			}
		}
		if (basicInfo != null)
		{
			return CreateGenericADV(basicInfo as GenericInfo);
		}
		return null;
	}

	public List<Program.Transfer> GetLeaveAloneADV()
	{
		int stage = Mathf.Clamp(relation, 0, 2);
		BasicInfo basicInfo = null;
		foreach (BasicInfo item in from v in GetListCommand(stage, Group.LeaveAlone, 0)
			orderby v.priority descending
			select v)
		{
			_ = item.conditions;
			basicInfo = item;
			if (basicInfo != null)
			{
				break;
			}
		}
		return CreateGenericADV(basicInfo as GenericInfo);
	}

	public List<Program.Transfer> GetEndConversationADV(bool _oldAnger, bool _endSelect, out EndADV _endNo)
	{
		int num = Mathf.Clamp(relation, 0, 2);
		_endNo = EndADV.General;
		worksEnd.Clear();
		foreach (BasicInfo item in from v in GetListCommand(num, Group.EndConversation, 0)
			orderby v.priority descending
			select v)
		{
			switch (item.conditions)
			{
			case 0:
				if (heroine.talkTime <= 0 && Mathf.InverseLerp(0f, SingletonInitializer<ActionScene>.instance.stepUpDic[num], heroine.favor) >= 0.5f)
				{
					_endNo = EndADV.Longing;
					return CreateGenericADV(item as GenericInfo);
				}
				break;
			case 1:
				if (heroine.talkTime <= 0 && heroine.lewdness >= 50)
				{
					_endNo = EndADV.Interest;
					return CreateGenericADV(item as GenericInfo);
				}
				break;
			case 2:
				if (_endSelect && Mathf.InverseLerp(0f, heroine.talkTimeMax, heroine.talkTime) < 0.5f)
				{
					_endNo = EndADV.FromPlayer;
					return CreateGenericADV(item as GenericInfo);
				}
				break;
			case 3:
				if (_endSelect && Mathf.InverseLerp(0f, heroine.talkTimeMax, heroine.talkTime) >= 0.5f)
				{
					_endNo = EndADV.InTheMiddle;
					return CreateGenericADV(item as GenericInfo);
				}
				break;
			case 10:
				if (heroine.favor >= SingletonInitializer<ActionScene>.instance.stepUpDic[num])
				{
					_endNo = EndADV.RankUp;
					return CreateGenericADV(item as GenericInfo);
				}
				break;
			default:
				if (heroine.talkTime <= 0)
				{
					worksEnd.Add(item);
				}
				break;
			case 101:
			case 102:
				break;
			}
		}
		if (!worksEnd.IsNullOrEmpty())
		{
			BasicInfo basicInfo = worksEnd.Shuffle().First();
			return CreateGenericADV(basicInfo as GenericInfo);
		}
		return null;
	}

	public TouchReactionInfo GetTouchVoice(bool _touch, int _parts, int _stage, out ChangeValueAbstractInfo _cvInfo)
	{
		TouchReactionInfo touchReactionInfo = new TouchReactionInfo();
		int key = 0;
		if (!isNPC)
		{
			key = Mathf.Clamp(relation, 0, 2);
		}
		else
		{
			switch (relationNPC)
			{
			case 0:
				key = 0;
				break;
			case 1:
			case 2:
				key = 1;
				break;
			case 3:
				key = 3;
				break;
			}
		}
		TalkData talkData = (dicTalkInfo[key][_touch ? Group.Touch : Group.See][_parts][_stage] as GenericInfo).talk[0];
		touchReactionInfo.assetbundle = talkData.assetbundle;
		touchReactionInfo.file = talkData.file;
		touchReactionInfo.facialExpression = talkData.facialExpression;
		touchReactionInfo.eyesLook = talkData.eyeLook;
		touchReactionInfo.neckLook = talkData.neckLook;
		touchReactionInfo.pose = talkData.pose;
		_cvInfo = new ChangeValueInfo(talkData.favor, talkData.lewdness);
		return touchReactionInfo;
	}

	public LeaveAloneReactionInfo GetLeaveAloneVoice()
	{
		BasicInfo basicInfo = null;
		int stage = 0;
		if (!isNPC)
		{
			stage = Mathf.Clamp(relation, 0, 2);
		}
		else
		{
			switch (relationNPC)
			{
			case 0:
				stage = 0;
				break;
			case 1:
			case 2:
				stage = 1;
				break;
			case 3:
				stage = 3;
				break;
			}
		}
		foreach (BasicInfo item in from v in GetListCommand(stage, Group.LeaveAlone, 0)
			orderby v.priority descending
			select v)
		{
			basicInfo = ((item.conditions == 0) ? null : item);
			if (basicInfo != null)
			{
				break;
			}
		}
		return new LeaveAloneReactionInfo(basicInfo as GenericInfo);
	}

	public InterludeReactionInfo GetInterludeVoice(ChangeValueAbstractInfo _cvInfo)
	{
		BasicInfo basicInfo = null;
		foreach (BasicInfo item in from v in GetListCommand(Mathf.Clamp(relation, 0, 2), Group.Interlude, 0)
			orderby v.priority descending
			select v)
		{
			switch (item.conditions)
			{
			case 1:
				if (_cvInfo != null && _cvInfo.lewdness > 0 && Utils.ProbabilityCalclator.DetectFromPercent(30f))
				{
					basicInfo = item;
				}
				break;
			case 0:
				if (_cvInfo != null && _cvInfo.favor > 0 && Utils.ProbabilityCalclator.DetectFromPercent(30f))
				{
					basicInfo = item;
				}
				break;
			case 10:
				if (!heroine.isNickNameEvent && heroine.isNickNameNormal && Utils.ProbabilityCalclator.DetectFromPercent(30f))
				{
					basicInfo = item;
				}
				break;
			case 11:
				if (heroine.isNickNameEvent && heroine.isNickNameNormal && Utils.ProbabilityCalclator.DetectFromPercent(30f))
				{
					basicInfo = item;
				}
				break;
			default:
				if (Utils.ProbabilityCalclator.DetectFromPercent(30f))
				{
					basicInfo = item;
				}
				break;
			}
			if (basicInfo != null)
			{
				break;
			}
		}
		return new InterludeReactionInfo(basicInfo as GenericInfo);
	}

	public List<Program.Transfer> CreateGenericADV(GenericInfo _info, bool _close = true, bool _motion = true)
	{
		List<Program.Transfer> list = CreateNewList();
		TalkData[] talk = _info.talk;
		foreach (TalkData talk2 in talk)
		{
			AddTransfer(list, talk2, _motion);
		}
		if (_close)
		{
			list.Add(Program.Transfer.Close());
		}
		return list;
	}

	public List<Program.Transfer> CreateADV(TalkData _data, bool _close = true, bool _motion = true)
	{
		List<Program.Transfer> list = CreateNewList();
		AddTransfer(list, _data, _motion);
		if (_close)
		{
			list.Add(Program.Transfer.Close());
		}
		return list;
	}

	public List<Program.Transfer> CreateSelectADV(SelectInfo _info, int _select)
	{
		List<Program.Transfer> list = CreateNewList();
		AddTransferHeroinText(list, _info.introduction);
		string[] value = null;
		bool flag = dicOptionDisplayItems.TryGetValue(_info.conditions, out value);
		switch (_info.choice.Length)
		{
		case 2:
			list.Add(Program.Transfer.Create(false, Command.SelectionAdd, "tag0", flag ? value[0] : _info.choice[0].choice));
			list.Add(Program.Transfer.Create(false, Command.SelectionAdd, "tag1", flag ? value[1] : _info.choice[1].choice));
			list.Add(Program.Transfer.Create(false, Command.Selection, "true"));
			break;
		case 3:
			list.Add(Program.Transfer.Create(false, Command.SelectionAdd, "tag0", flag ? value[0] : _info.choice[0].choice));
			list.Add(Program.Transfer.Create(false, Command.SelectionAdd, "tag1", flag ? value[1] : _info.choice[1].choice));
			list.Add(Program.Transfer.Create(false, Command.SelectionAdd, "tag2", flag ? value[2] : _info.choice[2].choice));
			list.Add(Program.Transfer.Create(false, Command.Selection, "true"));
			break;
		}
		for (int i = 0; i < _info.choice.Length; i++)
		{
			TalkData talk = ((_info.failure == null) ? _info.choice[i] : ((i == _select) ? _info.choice[i] : _info.failure));
			list.Add(Program.Transfer.Create(false, Command.Tag, "tag" + i));
			list.Add(Program.Transfer.Create(false, Command.Calc, TalkScene.KEY_NAME, "=", i.ToString()));
			AddTransferHeroinText(list, talk);
			list.Add(Program.Transfer.Close());
		}
		return list;
	}

	public List<Program.Transfer> CreateBranchADV(BranchInfo _info, int _branch)
	{
		List<Program.Transfer> list = CreateNewList();
		AddTransfer(list, _info.introduction);
		AddTransfer(list, _info[_branch]);
		list.Add(Program.Transfer.Close());
		return list;
	}

	private TalkData LoadData(CommunicationInfo.Param _src)
	{
		return new TalkData(_src);
	}

	private TalkData LoadData(CommunicationInfo.Param _src, ref int _priority, ref int _conditions)
	{
		_priority = _src.priority;
		_conditions = _src.conditions;
		return new TalkData(_src);
	}

	private Dictionary<int, List<BasicInfo>> GetDictionaryGroup(int _stage, Group _group)
	{
		if (!dicTalkInfo.ContainsKey(_stage))
		{
			dicTalkInfo.Add(_stage, new Dictionary<Group, Dictionary<int, List<BasicInfo>>>());
		}
		Dictionary<Group, Dictionary<int, List<BasicInfo>>> dictionary = dicTalkInfo[_stage];
		if (!dictionary.ContainsKey(_group))
		{
			dictionary.Add(_group, new Dictionary<int, List<BasicInfo>>());
		}
		return dictionary[_group];
	}

	private List<BasicInfo> GetListCommand(int _stage, Group _group, int _command)
	{
		Dictionary<int, List<BasicInfo>> dictionaryGroup = GetDictionaryGroup(_stage, _group);
		if (!dictionaryGroup.ContainsKey(_command))
		{
			dictionaryGroup.Add(_command, new List<BasicInfo>());
		}
		return dictionaryGroup[_command];
	}

	private BranchInfo CreateBranch(List<CommunicationInfo.Param> _list, ref int _startRow, int _successNum, int _failureRow)
	{
		BranchInfo branchInfo = new BranchInfo(_startRow)
		{
			success = new TalkData[_successNum]
		};
		branchInfo.introduction = LoadData(_list[_startRow++], ref branchInfo.priority, ref branchInfo.conditions);
		for (int i = 0; i < _successNum; i++)
		{
			branchInfo.success[i] = LoadData(_list[_startRow++]);
		}
		branchInfo.failure = LoadData(_list[_failureRow]);
		return branchInfo;
	}

	private BranchInfo CreateTalk(List<CommunicationInfo.Param> _list, ref int _startRow, int _noInterestedRow = -1, bool _introduction = true)
	{
		BranchInfo branchInfo = new BranchInfo(_startRow)
		{
			success = new TalkData[1]
		};
		if (_introduction)
		{
			branchInfo.introduction = LoadData(_list[_startRow++], ref branchInfo.priority, ref branchInfo.conditions);
			branchInfo.success[0] = LoadData(_list[_startRow++]);
		}
		else
		{
			branchInfo.introduction = null;
			branchInfo.success[0] = LoadData(_list[_startRow++], ref branchInfo.priority, ref branchInfo.conditions);
		}
		branchInfo.failure = LoadData(_list[_startRow++]);
		if (_noInterestedRow != -1)
		{
			branchInfo.noInterested = LoadData(_list[_noInterestedRow]);
		}
		return branchInfo;
	}

	private int LoadGenericOnly(List<CommunicationInfo.Param> _list, int _row, int _stage, Group _group, int _num)
	{
		List<BasicInfo> listCommand = GetListCommand(_stage, _group, 0);
		int _priority = 0;
		int _conditions = -1;
		for (int i = 0; i < _num; i++)
		{
			listCommand.Add(new GenericInfo(_row, LoadData(_list[_row++], ref _priority, ref _conditions))
			{
				priority = _priority,
				conditions = _conditions
			});
		}
		return _row;
	}

	private GenericInfo CreateGeneric(List<CommunicationInfo.Param> _list, ref int _startRow, int _num)
	{
		GenericInfo genericInfo = new GenericInfo(_startRow, _num);
		for (int i = 0; i < _num; i++)
		{
			genericInfo.talk[i] = ((i == 0) ? LoadData(_list[_startRow++], ref genericInfo.priority, ref genericInfo.conditions) : LoadData(_list[_startRow++]));
		}
		return genericInfo;
	}

	private GenericInfo CreateGeneric(List<CommunicationInfo.Param> _list, int _row)
	{
		GenericInfo genericInfo = new GenericInfo(_row, 1);
		genericInfo.talk[0] = LoadData(_list[_row], ref genericInfo.priority, ref genericInfo.conditions);
		return genericInfo;
	}

	private SelectInfo CreateSelect(List<CommunicationInfo.Param> _list, ref int _startRow, int _num, int _failureRow = -1)
	{
		SelectInfo selectInfo = new SelectInfo(_startRow, _num);
		selectInfo.introduction = LoadData(_list[_startRow++], ref selectInfo.priority, ref selectInfo.conditions);
		for (int i = 0; i < _num; i++)
		{
			selectInfo.choice[i] = LoadChoiceData(_list[_startRow++]);
		}
		if (_failureRow != -1)
		{
			selectInfo.failure = LoadData(_list[_failureRow]);
		}
		return selectInfo;
	}

	private ChoiceInfo LoadChoiceData(CommunicationInfo.Param _src)
	{
		return new ChoiceInfo(_src);
	}

	private void LoadTalkAndListenAnger(int _stage, List<CommunicationInfo.Param> _list, ref int _row)
	{
		foreach (KeyValuePair<int, List<BasicInfo>> item in GetDictionaryGroup(_stage, Group.Talk))
		{
			item.Value.Add(CreateGeneric(_list, _row));
		}
		foreach (KeyValuePair<int, List<BasicInfo>> item2 in GetDictionaryGroup(_stage, Group.Listen))
		{
			item2.Value.Add(CreateGeneric(_list, _row));
		}
		_row++;
	}

	private void LoadTouch(int _stage, Group _group, int _num, List<CommunicationInfo.Param> _list, ref int _row)
	{
		int row = _row + 3 * _num;
		for (int i = 0; i < _num; i++)
		{
			List<BasicInfo> listCommand = GetListCommand(_stage, _group, i);
			for (int j = 0; j < 3; j++)
			{
				listCommand.Add(CreateGeneric(_list, _row++));
			}
			listCommand.Add(CreateGeneric(_list, row));
		}
		_row++;
	}

	private void LoadEvent(int _stage, int _command, int _b, int _s, List<CommunicationInfo.Param> _list, ref int _row)
	{
		List<BasicInfo> listCommand = GetListCommand(_stage, Group.Event, _command);
		for (int i = 0; i < _b; i++)
		{
			listCommand.Add(CreateBranch(_list, ref _row, 1, _row + 2));
			_row++;
		}
		for (int j = 0; j < _s; j++)
		{
			listCommand.Add(CreateSelect(_list, ref _row, 2));
		}
	}

	private List<Program.Transfer> CreateSelectADV(SelectInfo _info, ChangeValueSelectInfo _cvsInfo)
	{
		List<Program.Transfer> list = CreateNewList();
		AddTransferHeroinText(list, _info.introduction);
		string[] value = null;
		bool flag = dicOptionDisplayItems.TryGetValue(_info.conditions, out value);
		switch (_info.choice.Length)
		{
		case 2:
			list.Add(Program.Transfer.Create(false, Command.SelectionAdd, "tag0", flag ? value[0] : _info.choice[0].choice));
			list.Add(Program.Transfer.Create(false, Command.SelectionAdd, "tag1", flag ? value[1] : _info.choice[1].choice));
			list.Add(Program.Transfer.Create(false, Command.Selection, "true"));
			break;
		case 3:
			list.Add(Program.Transfer.Create(false, Command.SelectionAdd, "tag0", flag ? value[0] : _info.choice[0].choice));
			list.Add(Program.Transfer.Create(false, Command.SelectionAdd, "tag1", flag ? value[1] : _info.choice[1].choice));
			list.Add(Program.Transfer.Create(false, Command.SelectionAdd, "tag2", flag ? value[2] : _info.choice[2].choice));
			list.Add(Program.Transfer.Create(false, Command.Selection, "true"));
			break;
		}
		_cvsInfo.Init(_info.choice.Length);
		int num2 = (_cvsInfo.success = CheckSelectConditions(_info.conditions));
		for (int i = 0; i < _info.choice.Length; i++)
		{
			TalkData talkData = ((_info.failure == null) ? _info.choice[i] : ((i == num2) ? _info.choice[i] : _info.failure));
			list.Add(Program.Transfer.Create(false, Command.Tag, "tag" + i));
			list.Add(Program.Transfer.Create(false, Command.Calc, TalkScene.KEY_NAME, "=", i.ToString()));
			if (talkData.text.IsNullOrEmpty())
			{
				AddTransfer(list, talkData);
			}
			else
			{
				AddTransferHeroinText(list, talkData);
			}
			list.Add(Program.Transfer.Close());
			_cvsInfo[i, 0] = talkData.favor;
			_cvsInfo[i, 1] = talkData.lewdness;
		}
		return list;
	}

	private int CheckSelectConditions(int _conditions)
	{
		int result = 0;
		switch (_conditions)
		{
		case 12:
			result = (new int[3] { 2, 1, 0 })[heroine.chaCtrl.GetBustCategory()];
			break;
		case 13:
			result = (new int[3] { 2, 1, 0 })[heroine.chaCtrl.GetHeightCategory()];
			break;
		case 14:
			result = (new int[3] { 2, 1, 0 })[heroine.chaCtrl.GetWaistCategory()];
			break;
		}
		return result;
	}

	private List<Program.Transfer> CreateGenericADV(GenericInfo _info, ChangeValueInfo _cvInfo, bool _motion = true)
	{
		TalkData talkData = _info.talk[0];
		_cvInfo[0] = talkData.favor;
		_cvInfo[1] = talkData.lewdness;
		_cvInfo.topic = talkData.topic;
		return CreateGenericADV(_info, _close: true, _motion);
	}

	private List<Program.Transfer> CreateBranchADV(BranchInfo _info, ChangeValueInfo _cvInfo)
	{
		List<Program.Transfer> list = CreateNewList();
		AddTransfer(list, _info.introduction);
		TalkData talkData = null;
		switch (_info.conditions)
		{
		case 1:
			talkData = ((ChaInfo.GetAttributeType(heroine.charFile) != 0) ? _info[RandomBranch(30, 30, 40)] : _info[RandomBranch(45, 20, 35)]);
			break;
		case 2:
			talkData = ((ChaInfo.GetAttributeType(heroine.charFile) != 1) ? _info[RandomBranch(30, 30, 40)] : _info[RandomBranch(45, 20, 35)]);
			break;
		case 3:
			talkData = ((ChaInfo.GetAttributeType(heroine.charFile) != 0) ? _info[RandomBranch(20, 40, 40)] : _info[RandomBranch(30, 40, 30)]);
			break;
		case 4:
			talkData = ((ChaInfo.GetAttributeType(heroine.charFile) != 1) ? _info[RandomBranch(20, 40, 40)] : _info[RandomBranch(30, 40, 30)]);
			break;
		case 7:
			talkData = _info[(Singleton<GameAssist>.Instance.PreferenceValue(heroine)[0] <= 3) ? 1 : 0];
			break;
		case 8:
			talkData = _info[(Singleton<GameAssist>.Instance.PreferenceValue(heroine)[0] >= -3) ? 1 : 0];
			break;
		case 9:
			talkData = _info[(Singleton<GameAssist>.Instance.PreferenceValue(heroine)[1] <= 3) ? 1 : 0];
			break;
		case 10:
			talkData = _info[(Singleton<GameAssist>.Instance.PreferenceValue(heroine)[1] >= -3) ? 1 : 0];
			break;
		case 11:
			talkData = _info[(Singleton<GameAssist>.Instance.PreferenceValue(heroine)[2] <= 3) ? 1 : 0];
			break;
		case 12:
			talkData = _info[(Singleton<GameAssist>.Instance.PreferenceValue(heroine)[2] >= -3) ? 1 : 0];
			break;
		case 41:
			talkData = _info[0];
			break;
		default:
			if (MathfEx.RangeEqualOn(1, _info.conditions, heroine.talkTemper.Length))
			{
				talkData = _info[heroine.talkTemper[_info.conditions - 1]];
				break;
			}
			switch (_info.success.Length)
			{
			case 1:
				talkData = ((_info.noInterested != null) ? _info[RandomBranch(30, 30, 40)] : _info[RandomBranch(30, 70)]);
				break;
			case 2:
				talkData = _info[RandomBranch(20, 40, 40)];
				break;
			}
			break;
		}
		AddTransfer(list, talkData);
		list.Add(Program.Transfer.Close());
		_cvInfo[0] = talkData.favor;
		_cvInfo[1] = talkData.lewdness;
		_cvInfo.topic = talkData.topic;
		return list;
	}

	private List<Program.Transfer> CreateBranchADV(BranchInfo _info, bool _success, bool _close = true)
	{
		List<Program.Transfer> list = CreateNewList();
		if (_info.introduction != null)
		{
			AddTransfer(list, _info.introduction);
		}
		AddTransfer(list, _success ? _info.success[0] : _info.failure);
		if (_close)
		{
			list.Add(Program.Transfer.Close());
		}
		return list;
	}

	private void AddTransfer(List<Program.Transfer> _list, TalkData _talk, bool _motion = true)
	{
		if (_talk.file.IsNullOrEmpty())
		{
			_list.Add(Program.Transfer.Text("[P名]", _talk.text));
		}
		else if (_talk.text.IsNullOrEmpty())
		{
			int result = 0;
			if (int.TryParse(_talk.file, out result))
			{
				heroine.talkEvent.Add(result);
			}
			_list.Add(Program.Transfer.Open(_talk.assetbundle, _talk.file, bool.TrueString));
		}
		else
		{
			AddTransferHeroinText(_list, _talk, _motion);
		}
	}

	private void AddTransferHeroinText(List<Program.Transfer> _list, TalkData _talk, bool _motion = true)
	{
		_list.Add(Program.Transfer.Create(true, Command.CharaLookEyes, "0", _talk.eyeLook.ToString(), "0"));
		_list.Add(Program.Transfer.Create(true, Command.CharaLookNeck, "0", _talk.neckLook.ToString(), "0"));
		if (_motion && !_talk.pose.IsNullOrEmpty())
		{
			string[] poseInfo = Singleton<GameAssist>.Instance.GetPoseInfo(heroine.FixCharaIDOrPersonality, _talk.pose);
			if (!((IReadOnlyCollection<string>)(object)poseInfo).IsNullOrEmpty())
			{
				_list.Add(Program.Transfer.Motion("0", poseInfo[2], poseInfo[0], poseInfo[1], poseInfo.SafeGet(3), poseInfo.SafeGet(4)));
			}
		}
		if (!_talk.facialExpression.IsNullOrEmpty())
		{
			_list.Add(Program.Transfer.Expression("0", _talk.facialExpression));
		}
		string[] array = _talk.file.Split(',');
		foreach (string text in array)
		{
			if (Regex.Match(text, "callName(\\d*)").Success)
			{
				_list.Add(Program.Transfer.Voice("0", "callBundle", text));
			}
			else
			{
				_list.Add(Program.Transfer.Voice("0", _talk.assetbundle, text));
			}
		}
		_list.Add(Program.Transfer.Text("[H名]", _talk.text));
	}

	private int RandomBranch(params int[] _values)
	{
		int num = Array.FindIndex(_values, (int v) => v == 100);
		if (num != -1)
		{
			return num;
		}
		return Utils.ProbabilityCalclator.DetermineFromDict(_values.Select((int value, int index) => new { value, index }).ToDictionary(v => v.index, v => v.value));
	}

	private List<Program.Transfer> CreateNewList()
	{
		List<Program.Transfer> list = new List<Program.Transfer>();
		if (passingInfo.player != null)
		{
			Program.SetParam(passingInfo.player, heroine, list);
		}
		else
		{
			Program.SetParam(heroine, list);
		}
		return list;
	}

	private bool ConditionDeterminationSpeak(int _conditions)
	{
		switch (_conditions)
		{
		case 0:
			return false;
		case -1:
			return true;
		case 40:
			if (heroine.isNickNameNormal)
			{
				return false;
			}
			return !heroine.isNickNameEvent;
		case 41:
			if (heroine.isNickNameNormal)
			{
				return false;
			}
			return heroine.talkEvent.Contains(54);
		default:
			return true;
		}
	}

	private bool ConditionDeterminationListen(int _conditions)
	{
		_ = _conditions - -1;
		_ = 1;
		return true;
	}

	private bool ConfirmEvent0(int _conditions, bool _heroine)
	{
		if (_heroine)
		{
			return false;
		}
		switch (_conditions)
		{
		case 0:
			if (!heroine.talkCommand.Contains(0))
			{
				return true;
			}
			break;
		case 1:
			if (!heroine.talkCommand.Contains(1))
			{
				return true;
			}
			break;
		case 2:
			if (!heroine.talkCommand.Contains(2))
			{
				return true;
			}
			break;
		case 3:
			if (!heroine.talkCommand.Contains(3))
			{
				return true;
			}
			break;
		case 4:
			if (isChasePossible && heroine.talkTime > 1)
			{
				return true;
			}
			break;
		}
		return false;
	}

	private bool ConfirmEvent1(int _conditions, bool _heroine)
	{
		bool flag = heroine.talkTime <= 0;
		if (!_heroine)
		{
			switch (_conditions)
			{
			case 0:
				if (!heroine.talkCommand.Contains(6) && !(Mathf.InverseLerp(0f, SingletonInitializer<ActionScene>.instance.stepUpDic[state], heroine.favor) < 0.7f))
				{
					return true;
				}
				break;
			case 1:
				if (!heroine.talkCommand.Contains(6) && !(Mathf.InverseLerp(0f, SingletonInitializer<ActionScene>.instance.stepUpDic[state], heroine.favor) < 1f))
				{
					return true;
				}
				break;
			case 2:
				if (!heroine.talkCommand.Contains(7) && isHPossible && !isSecondH)
				{
					return true;
				}
				break;
			case 3:
				if (!heroine.talkCommand.Contains(7) && isHPossible && isSecondH)
				{
					return true;
				}
				break;
			case 4:
				if (!heroine.talkCommand.Contains(7) && isHPossible && isSecondH && isOtherPeople)
				{
					return true;
				}
				break;
			case 5:
				if (!heroine.talkCommand.Contains(7) && isHPossible && isSecondH && exposure)
				{
					return true;
				}
				break;
			case 6:
				if (!heroine.talkCommand.Contains(0))
				{
					return true;
				}
				break;
			case 7:
				if (!heroine.talkCommand.Contains(1))
				{
					return true;
				}
				break;
			case 8:
				if (!heroine.talkCommand.Contains(2))
				{
					return true;
				}
				break;
			case 9:
				if (!heroine.talkCommand.Contains(3))
				{
					return true;
				}
				break;
			case 10:
				if (isChasePossible && heroine.talkTime > 1)
				{
					return true;
				}
				break;
			case 11:
				if (!heroine.talkCommand.Contains(5))
				{
					return true;
				}
				break;
			}
		}
		else
		{
			switch (_conditions)
			{
			case 100:
				if (!(heroine.confessed || heroine.talkCommand.Contains(6) || flag))
				{
					return CheckConditions(1, _conditions);
				}
				break;
			case 101:
				if (isHPossible && !heroine.talkCommand.Contains(7) && flag)
				{
					return CheckConditions(1, _conditions);
				}
				break;
			case 102:
				if (!(heroine.talkCommand.Contains(0) || flag))
				{
					return CheckConditions(1, _conditions);
				}
				break;
			case 103:
				if (!(heroine.talkCommand.Contains(1) || flag))
				{
					return CheckConditions(1, _conditions);
				}
				break;
			case 104:
				if (!(heroine.talkCommand.Contains(2) || flag))
				{
					return CheckConditions(1, _conditions);
				}
				break;
			case 105:
				if (!(heroine.talkCommand.Contains(3) || flag) && !heroine.isLunch)
				{
					return CheckConditions(1, _conditions);
				}
				break;
			case 106:
				if (!(heroine.talkCommand.Contains(5) || flag))
				{
					return CheckConditions(1, _conditions);
				}
				break;
			}
		}
		return false;
	}

	private bool ConfirmEvent2(int _conditions, bool _heroine)
	{
		bool flag = heroine.talkTime <= 0;
		if (!_heroine)
		{
			switch (_conditions)
			{
			case 0:
				if (!heroine.talkCommand.Contains(7) && isHPossible && !isSecondH)
				{
					return true;
				}
				break;
			case 3:
				if (!heroine.talkCommand.Contains(7) && isHPossible && isSecondH)
				{
					return true;
				}
				break;
			case 13:
				if (!heroine.talkCommand.Contains(7) && isHPossible && isSecondH && heroine.talkEvent.Contains(54))
				{
					return true;
				}
				break;
			case 4:
				if (!heroine.talkCommand.Contains(7) && isHPossible && isSecondH && isOtherPeople)
				{
					return true;
				}
				break;
			case 5:
				if (!heroine.talkCommand.Contains(7) && isHPossible && isSecondH && exposure)
				{
					return true;
				}
				break;
			case 6:
				if (!heroine.talkCommand.Contains(0))
				{
					return true;
				}
				break;
			case 7:
				if (!heroine.talkCommand.Contains(1))
				{
					return true;
				}
				break;
			case 8:
				if (!heroine.talkCommand.Contains(2))
				{
					return true;
				}
				break;
			case 9:
				if (!heroine.talkCommand.Contains(3))
				{
					return true;
				}
				break;
			case 10:
				if (isChasePossible && heroine.talkTime > 1)
				{
					return true;
				}
				break;
			case 12:
				if (!heroine.talkCommand.Contains(8))
				{
					return true;
				}
				break;
			case 11:
				if (!heroine.talkCommand.Contains(5))
				{
					return true;
				}
				break;
			}
		}
		else
		{
			switch (_conditions)
			{
			case 201:
				if (isHPossible && !heroine.talkCommand.Contains(7) && flag)
				{
					return CheckConditions(2, _conditions);
				}
				break;
			case 207:
				if (isHPossible && !heroine.talkCommand.Contains(7) && flag && heroine.talkEvent.Contains(55))
				{
					return CheckConditions(2, _conditions);
				}
				break;
			case 202:
				if (!(heroine.talkCommand.Contains(0) || flag))
				{
					return CheckConditions(2, _conditions);
				}
				break;
			case 203:
				if (!(heroine.talkCommand.Contains(1) || flag))
				{
					return CheckConditions(2, _conditions);
				}
				break;
			case 204:
				if (!(heroine.talkCommand.Contains(2) || flag))
				{
					return CheckConditions(2, _conditions);
				}
				break;
			case 205:
				if (!(heroine.talkCommand.Contains(3) || flag) && !heroine.isLunch)
				{
					return CheckConditions(2, _conditions);
				}
				break;
			case 206:
				if (!(heroine.talkCommand.Contains(5) || flag))
				{
					return CheckConditions(2, _conditions);
				}
				break;
			}
		}
		return false;
	}

	private bool CheckEventFlag(params int[] _array)
	{
		bool flag = false;
		foreach (int item in _array)
		{
			flag |= heroine.talkEvent.Contains(item);
		}
		return flag;
	}

	private bool CheckConditions(int _stage, int _condition, float _rev = 0f)
	{
		GameAssist.ConditionsInfo occurrenceCondition = Singleton<GameAssist>.Instance.GetOccurrenceCondition(_stage, _condition);
		if (occurrenceCondition == null)
		{
			return true;
		}
		if (!occurrenceCondition.favor.through && !occurrenceCondition.favor.Check(heroine.favor))
		{
			return false;
		}
		if (!occurrenceCondition.lewdness.through && !occurrenceCondition.lewdness.Check(heroine.lewdness))
		{
			return false;
		}
		if (occurrenceCondition.probability.through)
		{
			return true;
		}
		float num = occurrenceCondition.probability.Get((occurrenceCondition.reference == 0) ? heroine.favor : heroine.lewdness);
		if (_rev != 0f)
		{
			num = Mathf.Clamp(num + _rev, 0f, 100f);
		}
		return Utils.ProbabilityCalclator.DetectFromPercent(num);
	}

	private List<Program.Transfer> GetFirstEventADV(int _stage, int _command, ref int _derive, ref bool _first)
	{
		BranchInfo branchInfo = GetListCommand(0, Group.Event, _command)[0] as BranchInfo;
		List<Program.Transfer> list = CreateNewList();
		AddTransfer(list, branchInfo.introduction);
		int item = Array.IndexOf(new int[4] { 6, 8, 9, 11 }, _command) + 3;
		int num = (int)Singleton<GameAssist>.Instance.GetCommunicationConndition(_stage, branchInfo.conditions).Get(heroine.favor);
		if (_stage == 1 && Game.IsSameAccommodation(heroine))
		{
			num = (int)Mathf.Clamp((float)num + Singleton<GameAssist>.Instance.benefitsOfHotel.together, 0f, 100f);
		}
		bool flag = RandomBranch(num, 100 - num) == 0;
		heroine.talkEvent.Contains(item);
		AddTransfer(list, flag ? branchInfo.success[heroine.talkEvent.Contains(item) ? 1 : 0] : branchInfo.failure);
		list.Add(Program.Transfer.Close());
		return list;
	}

	private List<Program.Transfer> GetStage1Branch0(int _stage, int _command, ref int _derive)
	{
		BranchInfo branchInfo = GetListCommand(_stage, Group.Event, _command)[0] as BranchInfo;
		int num = (int)Singleton<GameAssist>.Instance.GetCommunicationConndition(_stage, branchInfo.conditions).Get(heroine.favor);
		if (_stage == 1 && Game.IsSameAccommodation(heroine))
		{
			num = (int)Mathf.Clamp((float)num + Singleton<GameAssist>.Instance.benefitsOfHotel.together, 0f, 100f);
		}
		bool flag = RandomBranch(num, 100 - num) == 0;
		List<Program.Transfer> result = CreateBranchADV(branchInfo, flag);
		if (flag)
		{
			switch (_command)
			{
			case 0:
				_derive = 4;
				break;
			case 1:
				_derive = 5;
				break;
			case 2:
				_derive = 6;
				break;
			case 3:
				_derive = 7;
				break;
			case 4:
				_derive = 1;
				break;
			default:
				_derive = 0;
				break;
			}
		}
		return result;
	}

	private List<Program.Transfer> GetStage1Branch1(int _command, ref int _derive)
	{
		BranchInfo branchInfo = GetListCommand(1, Group.Event, _command)[0] as BranchInfo;
		int num = (int)Singleton<GameAssist>.Instance.GetCommunicationConndition(1, branchInfo.conditions).Get(heroine.favor);
		if (Game.IsSameAccommodation(heroine))
		{
			num = (int)Mathf.Clamp((float)num + Singleton<GameAssist>.Instance.benefitsOfHotel.together, 0f, 100f);
		}
		bool flag = RandomBranch(num, 100 - num) == 0;
		List<Program.Transfer> result = CreateBranchADV(branchInfo, flag);
		if (flag)
		{
			int num2 = Array.IndexOf(new int[3] { 6, 10, 11 }, _command);
			ResultEnum[] array = new ResultEnum[3]
			{
				ResultEnum.GoHome,
				ResultEnum.None,
				ResultEnum.Chase
			};
			_derive = (int)array[num2];
		}
		return result;
	}

	private List<Program.Transfer> GetStage2Generic(int _command, ref int _derive)
	{
		List<Program.Transfer> result = CreateGenericADV(GetListCommand(2, Group.Event, _command)[0] as GenericInfo);
		switch (_command)
		{
		case 0:
			_derive = 4;
			break;
		case 1:
			_derive = 5;
			break;
		case 2:
			_derive = 6;
			break;
		case 3:
			_derive = 7;
			break;
		case 4:
			_derive = 1;
			break;
		default:
			_derive = 0;
			break;
		}
		return result;
	}

	private List<Program.Transfer> GetStaffBranch(int _min, int _max, ref int _derive)
	{
		BranchInfo info = GetListCommand(1, Group.Event, 10)[0] as BranchInfo;
		int num = (int)Mathf.Lerp(_min, _max, Mathf.InverseLerp(0f, 100f, heroine.favor));
		bool success = RandomBranch(num, 100 - num) == 0;
		return CreateBranchADV(info, success);
	}

	private List<Program.Transfer> GetHeroineADV(BasicInfo _target, int _stage, out ChangeValueAbstractInfo _outInfo, ref int _derive)
	{
		switch (_target.conditions)
		{
		case 100:
			_derive = 6;
			break;
		case 101:
		case 201:
			_derive = 7;
			break;
		case 102:
		case 202:
			_derive = 0;
			break;
		case 103:
		case 203:
			_derive = 1;
			break;
		case 104:
		case 204:
			_derive = 2;
			break;
		case 105:
		case 205:
			_derive = 3;
			break;
		case 107:
		case 207:
			_derive = 7;
			break;
		}
		if (_target != null)
		{
			if (_target is GenericInfo genericInfo)
			{
				GenericInfo info = genericInfo;
				_outInfo = new ChangeValueInfo();
				return CreateGenericADV(info, _outInfo as ChangeValueInfo);
			}
			if (_target is BranchInfo branchInfo)
			{
				BranchInfo info2 = branchInfo;
				_outInfo = new ChangeValueInfo();
				return CreateBranchADV(info2, _outInfo as ChangeValueInfo);
			}
			if (_target is SelectInfo selectInfo)
			{
				SelectInfo info3 = selectInfo;
				_outInfo = new ChangeValueSelectInfo();
				return CreateSelectADV(info3, _outInfo as ChangeValueSelectInfo);
			}
		}
		_outInfo = null;
		return null;
	}

	private static string BestPlaceNameConvert(string placeName)
	{
		switch (placeName)
		{
		case "1F":
		case "2F":
		case "3F":
			return "廊下";
		case "教室1-1":
		case "教室2-1":
		case "教室2-2":
		case "教室3-1":
			return "教室";
		default:
			return placeName;
		}
	}

	private static bool IsBestPlaceNameConvert(string bestPlaceName)
	{
		if (!(bestPlaceName == "廊下"))
		{
			if (bestPlaceName == "教室")
			{
				return true;
			}
			return false;
		}
		return true;
	}
}
