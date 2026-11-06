using System;
using System.Collections.Generic;
using UnityEx;

namespace AIProject.Definitions;

public static class Desire
{
	[Flags]
	public enum Type
	{
		None = 0,
		Toilet = 1,
		Bath = 2,
		Sleep = 4,
		Eat = 8,
		Break = 0x10,
		Gift = 0x20,
		Want = 0x40,
		Lonely = 0x80,
		H = 0x100,
		Dummy = 0x200,
		Hunt = 0x400,
		Game = 0x800,
		Cook = 0x1000,
		Animal = 0x2000,
		Location = 0x4000,
		Drink = 0x8000
	}

	public enum ActionType
	{
		Called = -1,
		Normal = 0,
		Date = 1,
		Onbu = 2,
		Idle = 3,
		SearchBath = 4,
		SearchToilet = 5,
		EndTaskEat = 6,
		SearchActor = 7,
		WithPlayer = 8,
		WithAgent = 9,
		WithMerchant = 10,
		SearchGather = 11,
		SearchGift = 12,
		EndTaskGift = 13,
		SearchBreak = 14,
		SearchCook = 15,
		SearchSleep = 16,
		EndTaskGimme = 17,
		SearchH = 18,
		EndTaskMasturbation = 19,
		Escape = 20,
		EndTaskH = 21,
		ChaseLesbianH = 22,
		EndTaskLesbianMerchantH = 23,
		ReverseRape = 25,
		EndTaskItemBox = 26,
		Steal = 27,
		SearchEat = 28,
		AfterCook = 29,
		ShallowSleep = 31,
		EndTaskSleep = 32,
		SearchGame = 35,
		EndTaskGame = 36,
		SearchPlayerToTalk = 37,
		EndTaskTalkToPlayer = 38,
		EndTaskTalk = 39,
		ReceiveTalk = 40,
		Fight = 41,
		ReceiveFight = 42,
		SearchAnimal = 33,
		EndTaskPetAnimal = 34,
		EndTaskWildAnimal = 43,
		Encounter = 44,
		Faint = 45,
		Cold2A = 46,
		Cold2B = 47,
		Cold2BMedicated = 48,
		Cold3A = 49,
		Cold3B = 50,
		Cold3BMedicated = 51,
		OverworkA = 53,
		OverworkB = 54,
		Cure = 55,
		SearchLocation = 56,
		EndTaskLocation = 57,
		EndTaskSleepAfterDate = 58,
		WalkWithAgent = 59,
		EndTaskGameWithAgent = 60,
		EndTaskToilet = 61,
		GiftForceEncounter = 62,
		GiftStandby = 63,
		SearchGimme = 64,
		WalkWithAgentFollow = 65,
		ChaseToTalk = 66,
		ChaseToPairWalk = 67,
		EndTaskGather = 68,
		SearchMasturbation = 69,
		GoTowardItemBox = 70,
		EndTaskGameThere = 71,
		EndTaskEatThere = 72,
		SearchItemForEat = 73,
		EndTaskGatherForEat = 74,
		SearchEatSpot = 75,
		SearchRevRape = 76,
		EndTaskPeeing = 77,
		SearchDrink = 78,
		SearchItemForDrink = 79,
		EndTaskGatherForDrink = 80,
		SearchDrinkSpot = 81,
		EndTaskDrink = 82,
		EndTaskDrinkThere = 83,
		EndTaskDressIn = 84,
		EndTaskBath = 85,
		EndTaskDressOut = 86,
		EndTaskCook = 87,
		EndTaskBreak = 88,
		EndTaskSleepTogether = 89,
		DiscussLesbianH = 90,
		GotoLesbianSpot = 91,
		GotoLesbianSpotFollow = 92,
		EndTaskLesbianH = 93,
		EndTaskSecondSleep = 94,
		WokenUp = 95,
		PhotoEncounter = 96,
		FoundPeeping = 97,
		GotoBath = 98,
		GotoDressOut = 99,
		WaitForCalled = 100,
		Ovation = 101,
		GoTowardFacialWash = 102,
		EndTaskFacialWash = 103,
		EndTaskSteal = 104,
		GoTowardChestInSearchLoop = 105,
		EndTaskChestInSearchLoop = 106,
		GotoClothChange = 107,
		EndTaskClothChange = 108,
		GotoRestoreCloth = 109,
		EndTaskRestoreCloth = 110,
		GotoHandWash = 111,
		EndTaskHandWash = 112,
		SearchBirthdayGift = 113,
		BirthdayGift = 114,
		WeaknessA = 115,
		WeaknessB = 116,
		TakeHPoint = 117,
		CommonSearchBreak = 118,
		CommonBreak = 119,
		CommonGameThere = 120,
		ComeSleepTogether = 121,
		ChaseYobai = 122,
		InviteSleep = 123,
		InviteSleepH = 124,
		InviteEat = 125,
		InviteBreak = 126,
		TakeSleepPoint = 127,
		TakeSleepHPoint = 128,
		TakeEatPoint = 129,
		TakeBreakPoint = 130,
		SearchWarpPoint = 131,
		Warp = 132
	}

	public static Dictionary<ActionType, ActionType> SickPairTable = new Dictionary<ActionType, ActionType>
	{
		[ActionType.Cold2A] = ActionType.Cold2B,
		[ActionType.Cold3A] = ActionType.Cold3B,
		[ActionType.OverworkA] = ActionType.OverworkB,
		[ActionType.WeaknessA] = ActionType.WeaknessB
	};

	private static ActionType[] _sickFilterTable = new ActionType[9]
	{
		ActionType.Faint,
		ActionType.Cold2A,
		ActionType.Cold2B,
		ActionType.Cold2BMedicated,
		ActionType.Cold3A,
		ActionType.Cold3B,
		ActionType.Cold3BMedicated,
		ActionType.OverworkA,
		ActionType.OverworkB
	};

	public static readonly Type[] WithPlayerDesireTable = new Type[3]
	{
		Type.Gift,
		Type.Game,
		Type.H
	};

	private static Dictionary<Type, int> _desireKeyTable;

	public static Dictionary<Type, string> NameTable { get; } = new Dictionary<Type, string>
	{
		[Type.None] = "なし",
		[Type.Toilet] = "トイレ",
		[Type.Bath] = "風呂",
		[Type.Sleep] = "睡眠",
		[Type.Eat] = "飲食",
		[Type.Break] = "休憩",
		[Type.Gift] = "ギフト",
		[Type.Want] = "おねだり",
		[Type.Lonely] = "寂しい",
		[Type.H] = "H",
		[Type.Dummy] = "ダミー",
		[Type.Hunt] = "採取",
		[Type.Game] = "遊び",
		[Type.Cook] = "料理",
		[Type.Animal] = "動物",
		[Type.Location] = "ロケーション",
		[Type.Drink] = "飲む"
	};


	public static Dictionary<ActionType, Type> ModeTable { get; }

	public static UnityEx.ValueTuple<EventType, Type>[] ValuePairs { get; }

	public static bool ContainsSickFilterTable(ActionType source)
	{
		ActionType[] sickFilterTable = _sickFilterTable;
		foreach (ActionType actionType in sickFilterTable)
		{
			if (source == actionType)
			{
				return true;
			}
		}
		return false;
	}

	public static int GetDesireKey(EventType type)
	{
		UnityEx.ValueTuple<EventType, Type>[] valuePairs = ValuePairs;
		for (int i = 0; i < valuePairs.Length; i++)
		{
			UnityEx.ValueTuple<EventType, Type> valueTuple = valuePairs[i];
			if (type == valueTuple.Item1)
			{
				return GetDesireKey(valueTuple.Item2);
			}
		}
		return -1;
	}

	public static int GetDesireKey(Type key)
	{
		if (!_desireKeyTable.TryGetValue(key, out var value))
		{
			return -1;
		}
		return value;
	}

	public static Type DesireTypeFromValue(int value)
	{
		foreach (KeyValuePair<Type, int> item in _desireKeyTable)
		{
			if (item.Value == value)
			{
				return item.Key;
			}
		}
		return Type.None;
	}

	public static UnityEx.ValueTuple<EventType, Type>[] FindAt(Type type)
	{
		List<UnityEx.ValueTuple<EventType, Type>> list = ListPool<UnityEx.ValueTuple<EventType, Type>>.Get();
		UnityEx.ValueTuple<EventType, Type>[] valuePairs = ValuePairs;
		for (int i = 0; i < valuePairs.Length; i++)
		{
			UnityEx.ValueTuple<EventType, Type> item = valuePairs[i];
			if (type == item.Item2)
			{
				list.Add(item);
			}
		}
		UnityEx.ValueTuple<EventType, Type>[] result = list.ToArray();
		ListPool<UnityEx.ValueTuple<EventType, Type>>.Release(list);
		return result;
	}

	static Desire()
	{
		Dictionary<ActionType, Type> dictionary = new Dictionary<ActionType, Type>();
		dictionary[ActionType.Normal] = Type.None;
		dictionary[ActionType.Date] = Type.None;
		dictionary[ActionType.SearchBath] = Type.Bath;
		dictionary[ActionType.SearchToilet] = Type.Toilet;
		dictionary[ActionType.EndTaskEat] = Type.Eat;
		dictionary[ActionType.SearchActor] = Type.Lonely;
		dictionary[ActionType.WithPlayer] = Type.None;
		dictionary[ActionType.SearchGather] = Type.Hunt;
		dictionary[ActionType.SearchGift] = Type.Gift;
		dictionary[ActionType.EndTaskGift] = Type.Gift;
		dictionary[ActionType.SearchBreak] = Type.Break;
		dictionary[ActionType.SearchCook] = Type.Cook;
		dictionary[ActionType.SearchSleep] = Type.Sleep;
		dictionary[ActionType.EndTaskGimme] = Type.Want;
		dictionary[ActionType.EndTaskMasturbation] = Type.H;
		dictionary[ActionType.SearchH] = Type.H;
		dictionary[ActionType.EndTaskH] = Type.H;
		dictionary[ActionType.SearchRevRape] = Type.H;
		dictionary[ActionType.ReverseRape] = Type.H;
		dictionary[ActionType.SearchEat] = Type.Eat;
		dictionary[ActionType.SearchGame] = Type.Game;
		dictionary[ActionType.EndTaskTalkToPlayer] = Type.Lonely;
		dictionary[ActionType.EndTaskTalk] = Type.Lonely;
		dictionary[ActionType.ReceiveTalk] = Type.Lonely;
		dictionary[ActionType.Fight] = Type.Lonely;
		dictionary[ActionType.ReceiveFight] = Type.Lonely;
		dictionary[ActionType.SearchAnimal] = Type.Animal;
		dictionary[ActionType.EndTaskPetAnimal] = Type.Animal;
		dictionary[ActionType.EndTaskWildAnimal] = Type.Animal;
		dictionary[ActionType.SearchLocation] = Type.Location;
		dictionary[ActionType.EndTaskLocation] = Type.Location;
		dictionary[ActionType.WalkWithAgent] = Type.Game;
		dictionary[ActionType.ChaseToTalk] = Type.Lonely;
		dictionary[ActionType.ChaseToPairWalk] = Type.Lonely;
		dictionary[ActionType.SearchMasturbation] = Type.H;
		dictionary[ActionType.SearchItemForEat] = Type.Hunt;
		dictionary[ActionType.SearchEatSpot] = Type.Eat;
		dictionary[ActionType.SearchRevRape] = Type.H;
		dictionary[ActionType.SearchItemForDrink] = Type.Hunt;
		dictionary[ActionType.SearchDrinkSpot] = Type.Drink;
		dictionary[ActionType.WalkWithAgent] = Type.Game;
		dictionary[ActionType.EndTaskToilet] = Type.Toilet;
		dictionary[ActionType.EndTaskBath] = Type.Bath;
		dictionary[ActionType.GiftForceEncounter] = Type.Gift;
		dictionary[ActionType.SearchGimme] = Type.Want;
		dictionary[ActionType.EndTaskGame] = Type.Game;
		dictionary[ActionType.EndTaskGameThere] = Type.Game;
		dictionary[ActionType.EndTaskBreak] = Type.Break;
		dictionary[ActionType.EndTaskCook] = Type.Cook;
		dictionary[ActionType.EndTaskSleep] = Type.Sleep;
		dictionary[ActionType.EndTaskSecondSleep] = Type.Sleep;
		dictionary[ActionType.EndTaskGather] = Type.Hunt;
		dictionary[ActionType.EndTaskGatherForEat] = Type.Hunt;
		dictionary[ActionType.EndTaskGatherForDrink] = Type.Hunt;
		dictionary[ActionType.InviteSleep] = Type.Lonely;
		dictionary[ActionType.InviteSleepH] = Type.Lonely;
		dictionary[ActionType.InviteEat] = Type.Lonely;
		dictionary[ActionType.InviteBreak] = Type.Lonely;
		dictionary[ActionType.SearchWarpPoint] = Type.Game;
		ModeTable = dictionary;
		_desireKeyTable = new Dictionary<Type, int>
		{
			[Type.Toilet] = 0,
			[Type.Bath] = 1,
			[Type.Sleep] = 2,
			[Type.Eat] = 3,
			[Type.Break] = 4,
			[Type.Gift] = 5,
			[Type.Want] = 6,
			[Type.Lonely] = 7,
			[Type.H] = 8,
			[Type.Dummy] = 9,
			[Type.Hunt] = 10,
			[Type.Game] = 11,
			[Type.Cook] = 12,
			[Type.Animal] = 13,
			[Type.Location] = 14,
			[Type.Drink] = 15
		};
		ValuePairs = new UnityEx.ValueTuple<EventType, Type>[14]
		{
			new UnityEx.ValueTuple<EventType, Type>(EventType.Sleep, Type.Sleep),
			new UnityEx.ValueTuple<EventType, Type>(EventType.Break, Type.Break),
			new UnityEx.ValueTuple<EventType, Type>(EventType.Eat, Type.Eat),
			new UnityEx.ValueTuple<EventType, Type>(EventType.Toilet, Type.Toilet),
			new UnityEx.ValueTuple<EventType, Type>(EventType.Play, Type.Game),
			new UnityEx.ValueTuple<EventType, Type>(EventType.Search, Type.Hunt),
			new UnityEx.ValueTuple<EventType, Type>(EventType.Cook, Type.Cook),
			new UnityEx.ValueTuple<EventType, Type>(EventType.DressIn, Type.Bath),
			new UnityEx.ValueTuple<EventType, Type>(EventType.Bath, Type.Bath),
			new UnityEx.ValueTuple<EventType, Type>(EventType.DressOut, Type.Bath),
			new UnityEx.ValueTuple<EventType, Type>(EventType.Masturbation, Type.H),
			new UnityEx.ValueTuple<EventType, Type>(EventType.ShallowSleep, Type.Sleep),
			new UnityEx.ValueTuple<EventType, Type>(EventType.Location, Type.Location),
			new UnityEx.ValueTuple<EventType, Type>(EventType.Drink, Type.Drink)
		};
	}
}
