using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using ADV;
using AIChara;
using AIProject.Definitions;
using Manager;
using MessagePack;
using UnityEngine;

namespace AIProject.SaveData;

[MessagePackObject(false)]
public class AgentData : ICharacterInfo, IDiffComparer, IParams, ICommandData
{
	private CharaParams _param;

	public static int[] DesireTableKeys = Enumerable.Range(0, 16).ToArray();

	[IgnoreMember]
	Params IParams.param
	{
		[CompilerGenerated]
		get
		{
			return param;
		}
	}

	[IgnoreMember]
	public CharaParams param => this.GetCache(ref _param, () => new CharaParams(this, "H"));

	[Key(0)]
	public System.Version Version { get; set; } = new System.Version();


	[Key(1)]
	public bool OpenState { get; set; }

	[Key(2)]
	public string CharaFileName { get; set; }

	[Key(3)]
	public bool PlayEnterScene { get; set; }

	[Key(4)]
	public Dictionary<int, float> StatsTable { get; set; } = StatsTableTemprate.ToDictionary((KeyValuePair<int, float> x) => x.Key, (KeyValuePair<int, float> x) => x.Value);


	[Key(5)]
	public Dictionary<int, float> DesireTable { get; set; } = DesireTableTemprate.ToDictionary((KeyValuePair<int, float> x) => x.Key, (KeyValuePair<int, float> x) => x.Value);


	[Key(6)]
	public Dictionary<int, float> MotivationTable { get; set; }

	[Key(9)]
	public int ChunkID { get; set; }

	[Key(10)]
	public int AreaID { get; set; }

	[Key(11)]
	public List<int> ReservedWaypointIDList { get; set; } = new List<int>();


	[Key(12)]
	public List<int> WalkRouteWaypointIDList { get; set; } = new List<int>();


	[Key(15)]
	public int ActionTargetID { get; set; } = -1;


	[Key(16)]
	public Desire.ActionType ModeType { get; set; }

	[Key(17)]
	public Dictionary<int, int> TiredNumberTable { get; set; } = new Dictionary<int, int>();


	[Key(21)]
	public Vector3 Position { get; set; }

	[Key(22)]
	public Quaternion Rotation { get; set; }

	[Key(23)]
	public Sickness SickState { get; set; } = new Sickness();


	[Key(24)]
	public float Wetness { get; set; }

	[Key(25)]
	public List<StuffItem> ItemList { get; set; } = new List<StuffItem>();


	[Key(27)]
	public StuffItem CarryingItem { get; set; }

	[Key(28)]
	public StuffItem EquipedGloveItem { get; set; } = new StuffItem
	{
		CategoryID = -1,
		ID = -1
	};


	[Key(29)]
	public StuffItem EquipedShovelItem { get; set; } = new StuffItem
	{
		CategoryID = -1,
		ID = -1
	};


	[Key(30)]
	public StuffItem EquipedPickelItem { get; set; } = new StuffItem
	{
		CategoryID = -1,
		ID = -1
	};


	[Key(31)]
	public StuffItem EquipedNetItem { get; set; } = new StuffItem
	{
		CategoryID = -1,
		ID = -1
	};


	[Key(32)]
	public StuffItem EquipedFishingItem { get; set; } = new StuffItem
	{
		CategoryID = -1,
		ID = -1
	};


	[Key(33)]
	public StuffItem EquipedHeadItem { get; set; } = new StuffItem
	{
		CategoryID = -1,
		ID = -1
	};


	[Key(34)]
	public StuffItem EquipedBackItem { get; set; } = new StuffItem
	{
		CategoryID = -1,
		ID = -1
	};


	[Key(35)]
	public StuffItem EquipedNeckItem { get; set; } = new StuffItem
	{
		CategoryID = -1,
		ID = -1
	};


	[Key(36)]
	public StuffItem EquipedLampItem { get; set; } = new StuffItem
	{
		CategoryID = -1,
		ID = -1
	};


	[Key(37)]
	public StuffItem EquipedUmbrellaItem { get; set; } = new StuffItem
	{
		CategoryID = -1,
		ID = -1
	};


	[Key(38)]
	public Dictionary<int, int> FriendlyRelationShipTable { get; set; } = FriendlyRelationShipTableTemprate.ToDictionary((KeyValuePair<int, int> x) => x.Key, (KeyValuePair<int, int> x) => x.Value);


	[Key(39)]
	public float CallCTCount { get; set; }

	[Key(40)]
	public int CalledCount { get; set; }

	[Key(41)]
	public bool Greeted { get; set; }

	[Key(42)]
	public Tutorial.ActionType TutorialModeType { get; set; }

	[Key(43)]
	public float TalkMotivation { get; set; }

	[Key(44)]
	public int FlavorAdditionAmount { get; set; }

	[Key(45)]
	public bool CheckCatEvent { get; set; }

	[Key(46)]
	public string NowCoordinateFileName { get; set; }

	[Key(47)]
	public string BathCoordinateFileName { get; set; }

	[Key(48)]
	public bool PlayedDressIn { get; set; }

	[Key(49)]
	public Dictionary<int, Environment.SearchActionInfo> SearchActionLockTable { get; set; } = new Dictionary<int, Environment.SearchActionInfo>();


	[Key(50)]
	public bool IsOtherCoordinate { get; set; }

	[Key(51)]
	public ItemScrounge ItemScrounge { get; set; } = new ItemScrounge();


	[Key(52)]
	public bool LockTalk { get; set; }

	[Key(53)]
	public float TalkElapsedTime { get; set; }

	[Key(54)]
	public bool IsPlayerForBirthdayEvent { get; set; }

	[Key(55)]
	public Dictionary<int, HashSet<string>> advEventCheck { get; set; } = new Dictionary<int, HashSet<string>>();


	[Key(56)]
	public bool IsWet { get; set; }

	[Key(57)]
	public HashSet<int> advEventLimitation { get; set; } = new HashSet<int>();


	[Key(58)]
	public Desire.ActionType PrevMode { get; set; }

	[Key(59)]
	public int LocationCount { get; set; }

	[Key(60)]
	public int LocationTaskCount { get; set; }

	[Key(61)]
	public int CurrentActionPointID { get; set; } = -1;


	[Key(62)]
	public bool ScheduleEnabled { get; set; }

	[Key(63)]
	public float ScheduleElapsedTime { get; set; }

	[Key(64)]
	public float ScheduleDuration { get; set; }

	[Key(65)]
	public float WeaknessMotivation { get; set; }

	[Key(66)]
	public SickLockInfo ColdLockInfo { get; set; } = new SickLockInfo();


	[Key(67)]
	public SickLockInfo HeatStrokeLockInfo { get; set; } = new SickLockInfo();


	[Key(68)]
	public bool YobaiTrigger { get; set; }

	[Key(69)]
	public int MapID { get; set; }

	[Key(70)]
	public bool ParameterLock { get; set; }

	[Key(71)]
	public int? PrevMapID { get; set; }

	public static Dictionary<int, float> StatsTableTemprate
	{
		get
		{
			Dictionary<int, float> dictionary = new Dictionary<int, float>();
			dictionary[0] = 50f;
			dictionary[1] = 50f;
			dictionary[2] = 100f;
			dictionary[3] = 100f;
			dictionary[4] = 100f;
			dictionary[5] = 100f;
			dictionary[6] = 0f;
			return dictionary;
		}
	}

	public static Dictionary<int, int> FriendlyRelationShipTableTemprate
	{
		get
		{
			Dictionary<int, int> dictionary = new Dictionary<int, int>();
			dictionary[-99] = 50;
			dictionary[-90] = 50;
			dictionary[0] = 50;
			dictionary[1] = 50;
			dictionary[2] = 50;
			dictionary[3] = 50;
			return dictionary;
		}
	}

	public static Dictionary<int, float> DesireTableTemprate
	{
		[CompilerGenerated]
		get
		{
			return DesireTableKeys.ToDictionary((int x) => x, (int _) => 0f);
		}
	}

	public static ReadOnlyDictionary<int, int> FlavorSkillTemplate { get; } = new ReadOnlyDictionary<int, int>(Enumerable.Range(0, 8).ToDictionary((int x) => x, (int x) => 0));


	public AgentData()
	{
		Init();
	}

	public StuffItem EquipedSearchItem(int id)
	{
		switch (id)
		{
		case 0:
		case 1:
		case 2:
			return EquipedGloveItem;
		case 3:
			return EquipedShovelItem;
		case 4:
			return EquipedPickelItem;
		case 5:
			return EquipedNetItem;
		case 6:
			return EquipedFishingItem;
		default:
			return null;
		}
	}

	public void Init()
	{
		MotivationTable = DesireTableKeys.ToDictionary((int x) => x, (int _) => StatsTable[5]);
		TalkMotivation = StatsTable[5];
	}

	public void Copy(AgentData source)
	{
		Version = source.Version;
		OpenState = source.OpenState;
		CharaFileName = source.CharaFileName;
		PlayEnterScene = source.PlayEnterScene;
		if (!PlayEnterScene)
		{
			CharaFileName = null;
		}
		StatsTable = source.StatsTable.ToDictionary((KeyValuePair<int, float> x) => x.Key, (KeyValuePair<int, float> x) => x.Value);
		DesireTable = source.DesireTable.ToDictionary((KeyValuePair<int, float> x) => x.Key, (KeyValuePair<int, float> x) => x.Value);
		MotivationTable = source.MotivationTable.ToDictionary((KeyValuePair<int, float> x) => x.Key, (KeyValuePair<int, float> x) => x.Value);
		ChunkID = source.ChunkID;
		AreaID = source.AreaID;
		ReservedWaypointIDList = source.ReservedWaypointIDList.ToList();
		WalkRouteWaypointIDList = source.WalkRouteWaypointIDList.ToList();
		ActionTargetID = source.ActionTargetID;
		ModeType = source.ModeType;
		TiredNumberTable = source.TiredNumberTable.ToDictionary((KeyValuePair<int, int> x) => x.Key, (KeyValuePair<int, int> x) => x.Value);
		Position = source.Position;
		Rotation = source.Rotation;
		SickState = new Sickness(source.SickState);
		ItemList.Clear();
		foreach (StuffItem item in source.ItemList)
		{
			ItemList.Add(new StuffItem(item));
		}
		if (source.CarryingItem != null)
		{
			CarryingItem = new StuffItem(source.CarryingItem);
		}
		else
		{
			CarryingItem = null;
		}
		EquipedGloveItem = new StuffItem(source.EquipedGloveItem);
		EquipedShovelItem = new StuffItem(source.EquipedShovelItem);
		EquipedPickelItem = new StuffItem(source.EquipedPickelItem);
		EquipedNetItem = new StuffItem(source.EquipedNetItem);
		EquipedFishingItem = new StuffItem(source.EquipedFishingItem);
		EquipedHeadItem = new StuffItem(source.EquipedHeadItem);
		EquipedBackItem = new StuffItem(source.EquipedBackItem);
		EquipedNeckItem = new StuffItem(source.EquipedNeckItem);
		EquipedLampItem = new StuffItem(source.EquipedLampItem);
		EquipedUmbrellaItem = new StuffItem(source.EquipedUmbrellaItem);
		FriendlyRelationShipTable = source.FriendlyRelationShipTable.ToDictionary((KeyValuePair<int, int> x) => x.Key, (KeyValuePair<int, int> x) => x.Value);
		CallCTCount = source.CallCTCount;
		CalledCount = source.CalledCount;
		Greeted = source.Greeted;
		TutorialModeType = source.TutorialModeType;
		TalkMotivation = source.TalkMotivation;
		FlavorAdditionAmount = source.FlavorAdditionAmount;
		CheckCatEvent = source.CheckCatEvent;
		NowCoordinateFileName = source.NowCoordinateFileName;
		BathCoordinateFileName = source.BathCoordinateFileName;
		PlayedDressIn = source.PlayedDressIn;
		SearchActionLockTable = source.SearchActionLockTable.ToDictionary((KeyValuePair<int, Environment.SearchActionInfo> x) => x.Key, (KeyValuePair<int, Environment.SearchActionInfo> x) => x.Value);
		IsOtherCoordinate = source.IsOtherCoordinate;
		ItemScrounge = new ItemScrounge(source.ItemScrounge);
		LockTalk = source.LockTalk;
		TalkElapsedTime = source.TalkElapsedTime;
		IsPlayerForBirthdayEvent = source.IsPlayerForBirthdayEvent;
		if (source.advEventCheck == null)
		{
			source.advEventCheck = new Dictionary<int, HashSet<string>>();
		}
		advEventCheck = source.advEventCheck.ToDictionary((KeyValuePair<int, HashSet<string>> v) => v.Key, (KeyValuePair<int, HashSet<string>> v) => new HashSet<string>(v.Value));
		IsWet = source.IsWet;
		if (source.advEventLimitation == null)
		{
			source.advEventLimitation = new HashSet<int>();
		}
		advEventLimitation = new HashSet<int>(source.advEventLimitation);
		PrevMode = source.PrevMode;
		CurrentActionPointID = source.CurrentActionPointID;
		ScheduleEnabled = source.ScheduleEnabled;
		ScheduleElapsedTime = source.ScheduleElapsedTime;
		ScheduleDuration = source.ScheduleDuration;
		WeaknessMotivation = source.WeaknessMotivation;
		if (source.ColdLockInfo != null)
		{
			ColdLockInfo = new SickLockInfo
			{
				Lock = source.ColdLockInfo.Lock,
				ElapsedTime = source.ColdLockInfo.ElapsedTime
			};
		}
		if (source.HeatStrokeLockInfo != null)
		{
			HeatStrokeLockInfo = new SickLockInfo
			{
				Lock = source.HeatStrokeLockInfo.Lock,
				ElapsedTime = source.HeatStrokeLockInfo.ElapsedTime
			};
		}
		MapID = source.MapID;
		ParameterLock = source.ParameterLock;
		PrevMapID = source.PrevMapID;
	}

	public void ComplementDiff()
	{
		if (Version < new System.Version("0.0.1"))
		{
			ModeType = Desire.ActionType.Normal;
			CurrentActionPointID = -1;
		}
		Version = AIProject.Definitions.Version.AgentDataVersion;
	}

	public void UpdateDiff()
	{
		foreach (int key in StatsTableTemprate.Keys)
		{
			if (!StatsTable.ContainsKey(key))
			{
				StatsTable[key] = StatsTableTemprate[key];
			}
		}
		foreach (int key2 in DesireTableTemprate.Keys)
		{
			if (DesireTable.TryGetValue(key2, out var _))
			{
				DesireTable[key2] = DesireTableTemprate[key2];
			}
		}
		ItemList.OrganizeItemList();
	}

	public IEnumerable<CommandData> CreateCommandData(string head)
	{
		List<CommandData> list = new List<CommandData>();
		string text = head + "DesireTable";
		foreach (KeyValuePair<int, float> item3 in DesireTable)
		{
			int key = item3.Key;
			list.Add(new CommandData(CommandData.Command.FLOAT, text + $"[{key}]", () => item3.Value, delegate(object o)
			{
				DesireTable[key] = Mathf.Clamp((float)o, 0f, 100f);
			}));
		}
		string head2 = head + "SickState";
		SickState.AddList(list, head2);
		if (param.actor != null && param.actor.ChaControl != null && param.actor.ChaControl.fileGameInfo != null)
		{
			string text2 = head + "StatsTable";
			foreach (KeyValuePair<int, float> item4 in StatsTable)
			{
				int key2 = item4.Key;
				list.Add(new CommandData(CommandData.Command.FLOAT, text2 + $"[{key2}]", () => (item4.Key == 7) ? ((object)param.actor.ChaControl.fileGameInfo.morality) : ((object)item4.Value), delegate(object o)
				{
					AgentActor agentActor = param.actor as AgentActor;
					if (agentActor != null)
					{
						float num3 = (float)o;
						if (key2 == 3)
						{
							StatusProfile statusProfile3 = Singleton<Manager.Resources>.Instance.StatusProfile;
							int flavor = agentActor.ChaControl.fileGameInfo.flavorState[6];
							float num4 = AgentActor.FlavorVariation(statusProfile3.DarknessPhysicalBuffMinMax, statusProfile3.DarknessPhysicalBuff, flavor);
							num3 += num4;
						}
						agentActor.SetStatus(key2, num3);
					}
				}));
			}
			ChaFileGameInfo fileGameInfo = param.actor.ChaControl.fileGameInfo;
			ChaFileGameInfo.MinMaxInfo tempBound = fileGameInfo.tempBound;
			string text3 = head + "tempBound";
			list.Add(new CommandData(CommandData.Command.FLOAT, text3 + ".low", () => tempBound.lower));
			list.Add(new CommandData(CommandData.Command.FLOAT, text3 + ".high", () => tempBound.upper));
			ChaFileGameInfo.MinMaxInfo MoodBound = fileGameInfo.moodBound;
			string text4 = head + "MoodBound";
			list.Add(new CommandData(CommandData.Command.FLOAT, text4 + ".low", () => MoodBound.lower));
			list.Add(new CommandData(CommandData.Command.FLOAT, text4 + ".high", () => MoodBound.upper));
			Dictionary<int, int> flavorState = fileGameInfo.flavorState;
			string text5 = head + "FlavorSkillTable";
			foreach (KeyValuePair<int, int> item5 in flavorState)
			{
				list.Add(new CommandData(CommandData.Command.Int, text5 + $"[{item5.Key}]", () => item5.Value, delegate(object o)
				{
					SetFlavorSkill(item5.Key, (int)o);
				}));
			}
			string key3 = string.Format("{0}{1}", head, "TalkMotivation");
			list.Add(new CommandData(CommandData.Command.FLOAT, key3, () => TalkMotivation, delegate(object o)
			{
				float max = StatsTable[5];
				TalkMotivation = Mathf.Clamp((float)o, 0f, max);
			}));
			string key4 = $"{head}InstructProb";
			list.Add(new CommandData(CommandData.Command.FLOAT, key4, delegate
			{
				int num2 = fileGameInfo.flavorState[1];
				StatusProfile statusProfile2 = Singleton<Manager.Resources>.Instance.StatusProfile;
				float defaultInstructionRate = statusProfile2.DefaultInstructionRate;
				Threshold flavorReliabilityInstructionMinMax = statusProfile2.FlavorReliabilityInstructionMinMax;
				float t2 = Mathf.InverseLerp(flavorReliabilityInstructionMinMax.min, flavorReliabilityInstructionMinMax.max, num2);
				defaultInstructionRate += statusProfile2.FlavorReliabilityInstruction.Lerp(t2);
				if (fileGameInfo.normalSkill.ContainsValue(27))
				{
					defaultInstructionRate += statusProfile2.InstructionRateDebuff;
				}
				return defaultInstructionRate;
			}));
			string key5 = $"{head}FollowProb";
			list.Add(new CommandData(CommandData.Command.FLOAT, key5, delegate
			{
				int num = fileGameInfo.flavorState[1];
				StatusProfile statusProfile = Singleton<Manager.Resources>.Instance.StatusProfile;
				float defaultFollowRate = statusProfile.DefaultFollowRate;
				float t = Mathf.InverseLerp(statusProfile.FollowReliabilityMinMax.min, statusProfile.FollowReliabilityMinMax.max, num);
				defaultFollowRate += statusProfile.FollowRateReliabilityBuff.Lerp(t);
				if (fileGameInfo.normalSkill.ContainsValue(8))
				{
					defaultFollowRate += statusProfile.FollowRateBuff;
				}
				return defaultFollowRate;
			}));
			int TotalFlavor = fileGameInfo.totalFlavor;
			string key6 = head + "TotalFlavor";
			list.Add(new CommandData(CommandData.Command.Int, key6, () => TotalFlavor));
			Dictionary<int, float> desireDefVal = fileGameInfo.desireDefVal;
			string text6 = head + "DesireDef";
			foreach (KeyValuePair<int, float> item6 in desireDefVal)
			{
				int desireKey = Desire.GetDesireKey((Desire.Type)item6.Key);
				list.Add(new CommandData(CommandData.Command.FLOAT, text6 + $"[{desireKey}]", () => item6.Value));
			}
			int Phase = fileGameInfo.phase;
			string key7 = head + "Phase";
			list.Add(new CommandData(CommandData.Command.Int, key7, () => Phase));
			Dictionary<int, int> NormalSkill = fileGameInfo.normalSkill;
			string text7 = head + "NormalSkill";
			foreach (KeyValuePair<int, int> item2 in NormalSkill)
			{
				list.Add(new CommandData(CommandData.Command.Int, text7 + $"[{item2.Key}]", () => item2.Value, delegate(object o)
				{
					NormalSkill[item2.Key] = (int)o;
				}));
			}
			Dictionary<int, int> HSkill = fileGameInfo.hSkill;
			string text8 = head + "HSkill";
			foreach (KeyValuePair<int, int> item in HSkill)
			{
				list.Add(new CommandData(CommandData.Command.Int, text8 + $"[{item.Key}]", () => item.Value, delegate(object o)
				{
					HSkill[item.Key] = (int)o;
				}));
			}
			int FavoritePlace = fileGameInfo.favoritePlace;
			string key8 = head + "FavoritePlace";
			list.Add(new CommandData(CommandData.Command.Int, key8, () => FavoritePlace));
		}
		return list;
	}

	public void AddFlavorSkill(int id, int value)
	{
		AddFlavorAdditionAmount(value);
	}

	public void AddFlavorAdditionAmount(int value)
	{
		int a = FlavorAdditionAmount + value;
		FlavorAdditionAmount = Mathf.Max(a, 0);
		if (Singleton<Game>.IsInstance())
		{
			Environment environment = Singleton<Game>.Instance.Environment;
			if (environment != null)
			{
				a = environment.TotalAgentFlavorAdditionAmount + value;
				environment.TotalAgentFlavorAdditionAmount = Mathf.Clamp(a, 0, 99999);
			}
		}
	}

	public void SetFlavorSkill(int id, int value)
	{
		ChaFileGameInfo fileGameInfo = param.actor.ChaControl.fileGameInfo;
		if (fileGameInfo.flavorState.ContainsKey(id))
		{
			int num = value - fileGameInfo.flavorState[id];
			fileGameInfo.flavorState[id] = Mathf.Clamp(value, 0, 99999);
			int a = fileGameInfo.totalFlavor + num;
			fileGameInfo.totalFlavor = Mathf.Max(a, 0);
			AddFlavorAdditionAmount(num);
		}
	}

	public HashSet<string> GetAdvEventCheck(int category)
	{
		if (advEventCheck == null)
		{
			advEventCheck = new Dictionary<int, HashSet<string>>();
		}
		if (!advEventCheck.TryGetValue(category, out var value))
		{
			value = (advEventCheck[category] = new HashSet<string>());
		}
		return value;
	}
}
