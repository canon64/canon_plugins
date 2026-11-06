using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ADV;
using AIProject.Definitions;
using Manager;
using MessagePack;
using UnityEngine;

namespace AIProject.SaveData;

[MessagePackObject(false)]
public class Environment : IDiffComparer, ICommandData
{
	[MessagePackObject(false)]
	public struct SerializableDateTime : ICommandData
	{
		private int _year;

		private int _month;

		private int _day;

		[Key(0)]
		public int Year
		{
			get
			{
				return SetYear(_year);
			}
			set
			{
				SetYear(value);
			}
		}

		[Key(1)]
		public int Month
		{
			get
			{
				return SetMonth(_month);
			}
			set
			{
				SetMonth(value);
			}
		}

		[Key(2)]
		public int Day
		{
			get
			{
				return SetDay(_day);
			}
			set
			{
				SetDay(value);
			}
		}

		[Key(3)]
		public int Hour { get; set; }

		[Key(4)]
		public int Minute { get; set; }

		[Key(5)]
		public int Second { get; set; }

		[Key(6)]
		public int Millisecond { get; set; }

		[IgnoreMember]
		public DateTime DateTime => new DateTime(Year, Month, Day, Hour, Minute, Second, Millisecond);

		public SerializableDateTime(int year, int month, int day)
			: this(year, month, day, 0, 0, 0, 0)
		{
		}

		public SerializableDateTime(int year, int month, int day, int hour, int minute, int second)
			: this(year, month, day, hour, minute, second, 0)
		{
		}

		public SerializableDateTime(int year, int month, int day, int hour, int minute, int second, int millisecond)
		{
			_year = Mathf.Max(0, year);
			_month = Mathf.Max(0, month);
			_day = Mathf.Max(0, day);
			Hour = hour;
			Minute = minute;
			Second = second;
			Millisecond = millisecond;
		}

		private int SetYear(int year)
		{
			return _year = Mathf.Max(1, year);
		}

		private int SetMonth(int month)
		{
			return _month = Mathf.Max(1, month);
		}

		private int SetDay(int day)
		{
			return _day = Mathf.Max(1, day);
		}

		public static implicit operator SerializableDateTime(DateTime dateTime)
		{
			return new SerializableDateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Millisecond);
		}

		public IEnumerable<CommandData> CreateCommandData(string head)
		{
			SerializableDateTime s = this;
			return new CommandData[1]
			{
				new CommandData(CommandData.Command.String, head + string.Format("[{0}]", "DateTime"), () => s.DateTime.ToString())
			};
		}
	}

	[MessagePackObject(false)]
	public struct SerializableTimeSpan : ICommandData
	{
		private int _days;

		private int _hours;

		private int _minutes;

		private int _seconds;

		private int _milliseconds;

		[Key(0)]
		public int Days
		{
			get
			{
				return SetDays(_days);
			}
			set
			{
				SetDays(value);
			}
		}

		[Key(1)]
		public int Hours
		{
			get
			{
				return SetHours(_hours);
			}
			set
			{
				SetHours(value);
			}
		}

		[Key(2)]
		public int Minutes
		{
			get
			{
				return SetMinutes(_minutes);
			}
			set
			{
				SetMinutes(value);
			}
		}

		[Key(3)]
		public int Seconds
		{
			get
			{
				return SetSeconds(_seconds);
			}
			set
			{
				SetSeconds(value);
			}
		}

		[Key(4)]
		public int MilliSeconds
		{
			get
			{
				return SetMilliseconds(_milliseconds);
			}
			set
			{
				SetMilliseconds(value);
			}
		}

		[IgnoreMember]
		public TimeSpan TimeSpan => new TimeSpan(Days, Hours, Minutes, Seconds, MilliSeconds);

		public SerializableTimeSpan(int days, int hours, int minutes, int seconds, int milliseconds)
		{
			_days = Mathf.Max(0, days);
			_hours = Mathf.Max(0, hours);
			_minutes = Mathf.Max(0, minutes);
			_seconds = Mathf.Max(0, seconds);
			_milliseconds = Mathf.Max(0, milliseconds);
		}

		public SerializableTimeSpan(int days, int hours, int minutes, int seconds)
		{
			_days = Mathf.Max(0, days);
			_hours = Mathf.Max(0, hours);
			_minutes = Mathf.Max(0, minutes);
			_seconds = Mathf.Max(0, seconds);
			_milliseconds = Mathf.Max(0, 0);
		}

		private int SetDays(int days)
		{
			return _days = Mathf.Max(0, days);
		}

		private int SetHours(int hours)
		{
			return _hours = Mathf.Max(0, hours);
		}

		private int SetMinutes(int minutes)
		{
			return _minutes = Mathf.Max(0, minutes);
		}

		private int SetSeconds(int seconds)
		{
			return _seconds = Mathf.Max(0, _seconds);
		}

		private int SetMilliseconds(int milliseconds)
		{
			return _milliseconds = Mathf.Max(0, _milliseconds);
		}

		public static implicit operator SerializableTimeSpan(TimeSpan timeSpan)
		{
			return new SerializableTimeSpan(timeSpan.Days, timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds, timeSpan.Milliseconds);
		}

		public IEnumerable<CommandData> CreateCommandData(string head)
		{
			SerializableTimeSpan s = this;
			return new CommandData[1]
			{
				new CommandData(CommandData.Command.String, head + string.Format("[{0}]", "TimeSpan"), () => s.TimeSpan.ToString())
			};
		}
	}

	[MessagePackObject(false)]
	public class ScheduleData
	{
		[Key(0)]
		public int DaysToGo { get; set; }

		[Key(1)]
		public SerializableDateTime StartTime { get; set; }

		[Key(2)]
		public SerializableTimeSpan Duration { get; set; }

		[Key(3)]
		public SchedulingEvent Event { get; set; }

		public ScheduleData()
		{
		}

		public ScheduleData(ScheduleData source)
		{
			DaysToGo = source.DaysToGo;
			Event = source.Event;
		}
	}

	[MessagePackObject(false)]
	public struct SchedulingEvent
	{
		[Key(0)]
		public int agentID;

		[Key(1)]
		public int pointID;

		public SchedulingEvent(int aID, int pID)
		{
			agentID = aID;
			pointID = pID;
		}
	}

	[MessagePackObject(false)]
	public class SearchActionInfo
	{
		[Key(0)]
		public int Count { get; set; }

		[Key(1)]
		public float ElapsedTime { get; set; }
	}

	[MessagePackObject(false)]
	public class PlantInfo
	{
		[Key(0)]
		public int nameHash { get; }

		[Key(1)]
		public int timeLimit { get; }

		[Key(2)]
		public float timer { get; private set; }

		[Key(3)]
		public StuffItem[] result { get; }

		[IgnoreMember]
		public float progress
		{
			[CompilerGenerated]
			get
			{
				return Mathf.InverseLerp(0f, timeLimit, timer);
			}
		}

		[IgnoreMember]
		public bool isEnd
		{
			[CompilerGenerated]
			get
			{
				return timer >= (float)timeLimit;
			}
		}

		public PlantInfo(int nameHash, int timeLimit, StuffItem[] result)
		{
			this.nameHash = nameHash;
			this.timeLimit = timeLimit;
			this.result = result;
		}

		[SerializationConstructor]
		public PlantInfo(int nameHash, int timeLimit, float timer, StuffItem[] result)
		{
			this.nameHash = nameHash;
			this.timeLimit = timeLimit;
			this.timer = timer;
			this.result = result.Select((StuffItem x) => new StuffItem(x)).ToArray();
		}

		public void AddTimer(float add)
		{
			timer = Mathf.Min(timer + add, timeLimit);
		}

		public void Finish()
		{
			timer = timeLimit;
		}

		public override string ToString()
		{
			int num = timeLimit - (int)timer;
			int num2 = num / 3600;
			num %= 3600;
			int num3 = num / 60;
			num %= 60;
			int num4 = num;
			string text = string.Empty;
			if (num2 > 0)
			{
				text += $"{num2}";
			}
			return text + $"{num3:00}:{num4:00}";
		}
	}

	[MessagePackObject(false)]
	public class ChickenInfo
	{
		[Key(0)]
		public string name = string.Empty;

		[Key(1)]
		public AnimalData AnimalData;
	}

	[MessagePackObject(false)]
	public class PetHomeInfo
	{
		[Key(0)]
		public int HousingID { get; set; }

		[Key(1)]
		public AnimalData AnimalData { get; set; }

		[Key(2)]
		public bool ChaseActor { get; set; }

		[Key(3)]
		public bool NicknameDisplay { get; set; }

		public PetHomeInfo()
		{
		}

		public PetHomeInfo(PetHomeInfo source)
		{
			Copy(source);
		}

		public void Copy(PetHomeInfo source)
		{
			if (source != null)
			{
				HousingID = source.HousingID;
				if (source.AnimalData != null)
				{
					AnimalData = new AnimalData();
					AnimalData.Copy(source.AnimalData);
				}
				ChaseActor = source.ChaseActor;
				NicknameDisplay = source.NicknameDisplay;
			}
		}
	}

	[MessagePackObject(false)]
	public class DateTemperatureInfo
	{
		[Key(0)]
		public Temperature Temperature { get; set; } = Temperature.Normal;


		[Key(1)]
		public float MorningTemp { get; set; }

		[Key(2)]
		public float DayTemp { get; set; }

		[Key(3)]
		public float NightTemp { get; set; }

		public DateTemperatureInfo(Temperature temp, float morning, float day, float night)
		{
			Temperature = temp;
			MorningTemp = morning;
			DayTemp = day;
			NightTemp = night;
		}

		public DateTemperatureInfo()
		{
		}

		public DateTemperatureInfo(DateTemperatureInfo info)
		{
			Copy(info);
		}

		public void Copy(DateTemperatureInfo info)
		{
			if (info != null)
			{
				Temperature = info.Temperature;
				MorningTemp = info.MorningTemp;
				DayTemp = info.DayTemp;
				NightTemp = info.NightTemp;
			}
		}

		public float GetTempValue(TimeZone timeZone)
		{
			return timeZone switch
			{
				TimeZone.Morning => MorningTemp, 
				TimeZone.Day => DayTemp, 
				TimeZone.Night => NightTemp, 
				_ => (MorningTemp + DayTemp + NightTemp) / 3f, 
			};
		}
	}

	[Key(0)]
	public System.Version Version { get; set; } = new System.Version();


	[Key(1)]
	public int TutorialProgress { get; set; }

	[Key(2)]
	public SerializableTimeSpan TotalPlayTime { get; set; } = default(SerializableTimeSpan);


	[Key(3)]
	public SerializableDateTime Time { get; set; } = default(SerializableDateTime);


	[Key(5)]
	public Weather Weather { get; set; } = Weather.Cloud1;


	[Key(7)]
	public Temperature Temperature { get; set; } = Temperature.Normal;


	[Key(8)]
	public List<StuffItem> ItemListInStorage { get; set; } = new List<StuffItem>();


	[Key(9)]
	public List<StuffItem> ItemListInPantry { get; set; } = new List<StuffItem>();


	[Key(10)]
	public Dictionary<int, bool> AreaOpenState { get; set; } = new Dictionary<int, bool>();


	[Key(11)]
	public float TemperatureValue { get; set; }

	[Key(12)]
	public Dictionary<int, bool> TimeObjOpenState { get; set; } = new Dictionary<int, bool>();


	[Key(13)]
	public Dictionary<int, Dictionary<int, bool>> BasePointOpenState { get; set; } = new Dictionary<int, Dictionary<int, bool>>();


	[Key(14)]
	public Dictionary<int, bool> LightObjectSwitchStateTable { get; set; } = new Dictionary<int, bool>();


	[Key(15)]
	public Dictionary<int, Dictionary<int, int>> EventPointStateTable { get; set; } = new Dictionary<int, Dictionary<int, int>>();


	[Key(16)]
	public Dictionary<int, List<PlantInfo>> FarmlandTable { get; set; } = new Dictionary<int, List<PlantInfo>>();


	[Key(17)]
	public Dictionary<int, SearchActionInfo> SearchActionLockTable { get; set; } = new Dictionary<int, SearchActionInfo>();


	[Key(18)]
	public Dictionary<int, List<ChickenInfo>> ChickenTable { get; set; } = new Dictionary<int, List<ChickenInfo>>();


	[Key(19)]
	public List<StuffItem> ItemListInEggBox { get; set; } = new List<StuffItem>();


	[Key(20)]
	public int TotalAgentFlavorAdditionAmount { get; set; }

	[Key(21)]
	public List<int> RegIDList { get; set; } = new List<int>();


	[Key(22)]
	public Dictionary<int, PetHomeInfo> PetHomeStateTable { get; set; } = new Dictionary<int, PetHomeInfo>();


	[Key(23)]
	public List<string> ClosetCoordinateList { get; set; } = new List<string>();


	[Key(24)]
	public List<string> DressCoordinateList { get; set; } = new List<string>();


	[Key(25)]
	public Dictionary<int, string> JukeBoxAudioNameTable { get; set; } = new Dictionary<int, string>();


	[Key(28)]
	public Dictionary<int, bool> OnceActionPointStateTable { get; set; } = new Dictionary<int, bool>();


	[Key(29)]
	public Dictionary<int, float> DropSearchActionPointCoolTimeTable { get; set; } = new Dictionary<int, float>();


	[Key(31)]
	public Dictionary<int, Dictionary<int, string>> AnotherJukeBoxAudioNameTable { get; set; } = new Dictionary<int, Dictionary<int, string>>();


	[Key(32)]
	public Dictionary<int, Dictionary<int, Dictionary<int, AnimalData>>> HousingChickenDataTable { get; set; } = new Dictionary<int, Dictionary<int, Dictionary<int, AnimalData>>>();


	[Key(33)]
	public Dictionary<int, RecyclingData> RecyclingDataTable { get; set; } = new Dictionary<int, RecyclingData>();


	public Environment()
	{
		Init();
	}

	public void Init()
	{
		BasePointOpenState = new Dictionary<int, Dictionary<int, bool>> { [0] = new Dictionary<int, bool>
		{
			[-1] = false,
			[0] = false,
			[1] = false,
			[2] = false
		} };
	}

	public void Copy(Environment source)
	{
		Version = source.Version;
		TutorialProgress = source.TutorialProgress;
		TotalPlayTime = source.TotalPlayTime;
		Time = source.Time;
		Weather = source.Weather;
		Temperature = source.Temperature;
		ItemListInStorage.Clear();
		foreach (StuffItem item in source.ItemListInStorage)
		{
			ItemListInStorage.Add(new StuffItem(item.CategoryID, item.ID, item.Count));
		}
		ItemListInPantry.Clear();
		foreach (StuffItem item2 in source.ItemListInPantry)
		{
			ItemListInPantry.Add(new StuffItem(item2.CategoryID, item2.ID, item2.Count));
		}
		AreaOpenState = source.AreaOpenState.ToDictionary((KeyValuePair<int, bool> x) => x.Key, (KeyValuePair<int, bool> x) => x.Value);
		TemperatureValue = source.TemperatureValue;
		TimeObjOpenState = source.TimeObjOpenState.ToDictionary((KeyValuePair<int, bool> x) => x.Key, (KeyValuePair<int, bool> x) => x.Value);
		BasePointOpenState = source.BasePointOpenState.ToDictionary((KeyValuePair<int, Dictionary<int, bool>> x) => x.Key, (KeyValuePair<int, Dictionary<int, bool>> x) => x.Value?.ToDictionary((KeyValuePair<int, bool> y) => y.Key, (KeyValuePair<int, bool> y) => y.Value));
		LightObjectSwitchStateTable = source.LightObjectSwitchStateTable.ToDictionary((KeyValuePair<int, bool> x) => x.Key, (KeyValuePair<int, bool> x) => x.Value);
		EventPointStateTable = source.EventPointStateTable.ToDictionary((KeyValuePair<int, Dictionary<int, int>> x) => x.Key, (KeyValuePair<int, Dictionary<int, int>> x) => x.Value?.ToDictionary((KeyValuePair<int, int> y) => y.Key, (KeyValuePair<int, int> y) => y.Value));
		FarmlandTable = source.FarmlandTable.ToDictionary((KeyValuePair<int, List<PlantInfo>> x) => x.Key, (KeyValuePair<int, List<PlantInfo>> x) => x.Value?.Select((PlantInfo y) => (y == null) ? null : new PlantInfo(y.nameHash, y.timeLimit, y.timer, y.result))?.ToList());
		SearchActionLockTable = source.SearchActionLockTable.ToDictionary((KeyValuePair<int, SearchActionInfo> x) => x.Key, (KeyValuePair<int, SearchActionInfo> x) => x.Value);
		ChickenTable = source.ChickenTable.ToDictionary((KeyValuePair<int, List<ChickenInfo>> x) => x.Key, (KeyValuePair<int, List<ChickenInfo>> x) => x.Value?.Select((ChickenInfo y) => (y == null) ? null : new ChickenInfo
		{
			name = y.name,
			AnimalData = ((y.AnimalData == null) ? null : new AnimalData(y.AnimalData))
		})?.ToList());
		ItemListInEggBox.Clear();
		foreach (StuffItem item3 in source.ItemListInEggBox)
		{
			ItemListInEggBox.Add(new StuffItem(item3.CategoryID, item3.ID, item3.Count));
		}
		TotalAgentFlavorAdditionAmount = source.TotalAgentFlavorAdditionAmount;
		RegIDList = source.RegIDList.ToList();
		PetHomeStateTable = source.PetHomeStateTable.ToDictionary((KeyValuePair<int, PetHomeInfo> x) => x.Key, (KeyValuePair<int, PetHomeInfo> x) => new PetHomeInfo(x.Value));
		ClosetCoordinateList = source.ClosetCoordinateList.ToList();
		DressCoordinateList = source.DressCoordinateList.ToList();
		JukeBoxAudioNameTable = source.JukeBoxAudioNameTable.Where((KeyValuePair<int, string> pair) => !pair.Value.IsNullOrEmpty()).ToDictionary((KeyValuePair<int, string> x) => x.Key, (KeyValuePair<int, string> x) => x.Value);
		if (!source.OnceActionPointStateTable.IsNullOrEmpty())
		{
			OnceActionPointStateTable = source.OnceActionPointStateTable.ToDictionary((KeyValuePair<int, bool> x) => x.Key, (KeyValuePair<int, bool> x) => x.Value);
		}
		else
		{
			OnceActionPointStateTable.Clear();
		}
		if (!source.DropSearchActionPointCoolTimeTable.IsNullOrEmpty())
		{
			DropSearchActionPointCoolTimeTable = source.DropSearchActionPointCoolTimeTable.ToDictionary((KeyValuePair<int, float> x) => x.Key, (KeyValuePair<int, float> x) => x.Value);
		}
		else
		{
			DropSearchActionPointCoolTimeTable.Clear();
		}
		if (!source.AnotherJukeBoxAudioNameTable.IsNullOrEmpty())
		{
			AnotherJukeBoxAudioNameTable = source.AnotherJukeBoxAudioNameTable.Where((KeyValuePair<int, Dictionary<int, string>> x) => !x.Value.IsNullOrEmpty()).ToDictionary((KeyValuePair<int, Dictionary<int, string>> x) => x.Key, (KeyValuePair<int, Dictionary<int, string>> x) => x.Value.Where((KeyValuePair<int, string> y) => !y.Value.IsNullOrEmpty()).ToDictionary((KeyValuePair<int, string> y) => y.Key, (KeyValuePair<int, string> y) => y.Value));
		}
		else
		{
			AnotherJukeBoxAudioNameTable.Clear();
		}
		if (!source.HousingChickenDataTable.IsNullOrEmpty())
		{
			HousingChickenDataTable = source.HousingChickenDataTable.Where((KeyValuePair<int, Dictionary<int, Dictionary<int, AnimalData>>> a) => !a.Value.IsNullOrEmpty()).ToDictionary((KeyValuePair<int, Dictionary<int, Dictionary<int, AnimalData>>> a) => a.Key, (KeyValuePair<int, Dictionary<int, Dictionary<int, AnimalData>>> a) => a.Value.Where((KeyValuePair<int, Dictionary<int, AnimalData>> b) => !b.Value.IsNullOrEmpty()).ToDictionary((KeyValuePair<int, Dictionary<int, AnimalData>> b) => b.Key, (KeyValuePair<int, Dictionary<int, AnimalData>> b) => b.Value.Where((KeyValuePair<int, AnimalData> c) => c.Value != null).ToDictionary((KeyValuePair<int, AnimalData> c) => c.Key, (KeyValuePair<int, AnimalData> c) => new AnimalData(c.Value))));
		}
		else
		{
			HousingChickenDataTable.Clear();
		}
		if (!source.RecyclingDataTable.IsNullOrEmpty())
		{
			RecyclingDataTable = source.RecyclingDataTable.Where((KeyValuePair<int, RecyclingData> x) => x.Value != null).ToDictionary((KeyValuePair<int, RecyclingData> x) => x.Key, (KeyValuePair<int, RecyclingData> x) => new RecyclingData(x.Value));
		}
		else
		{
			RecyclingDataTable.Clear();
		}
	}

	public void ComplementWithVersion()
	{
		Version = AIProject.Definitions.Version.EnvironmentDataVersion;
	}

	public void SetSimulation(EnvironmentSimulator sim)
	{
		Time = sim.Now;
		Weather = sim.Weather;
		Temperature = sim.Temperature;
		TemperatureValue = sim.TemperatureValue;
	}

	public void UpdateDiff()
	{
		ItemListInStorage.OrganizeItemList();
	}

	public IEnumerable<CommandData> CreateCommandData(string head)
	{
		List<CommandData> list = new List<CommandData>();
		string text = head + "AreaOpenState";
		foreach (KeyValuePair<int, bool> item in AreaOpenState)
		{
			int key = item.Key;
			bool value = item.Value;
			list.Add(new CommandData(CommandData.Command.BOOL, text + $"[{key}]", () => value, delegate(object o)
			{
				bool flag = (bool)o;
				if (Singleton<Manager.Map>.IsInstance())
				{
					Singleton<Manager.Map>.Instance.SetOpenAreaState(key, flag);
				}
				else
				{
					AreaOpenState[key] = flag;
				}
			}));
		}
		return list;
	}
}
