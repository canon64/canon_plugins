using System;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEx;

namespace AIProject;

public class EnvironmentProfile : ScriptableObject
{
	[Serializable]
	public struct TemperatureBorderInfo
	{
		[SerializeField]
		private int _minDegree;

		[SerializeField]
		private int _lowBorder;

		[SerializeField]
		private int _highBorder;

		[SerializeField]
		private int _maxDegree;

		public int MinDegree
		{
			[CompilerGenerated]
			get
			{
				return _minDegree;
			}
		}

		public int LowBorder
		{
			[CompilerGenerated]
			get
			{
				return _lowBorder;
			}
		}

		public int HighBorder
		{
			[CompilerGenerated]
			get
			{
				return _highBorder;
			}
		}

		public int MaxDegree
		{
			[CompilerGenerated]
			get
			{
				return _maxDegree;
			}
		}

		public int TotalDegree
		{
			[CompilerGenerated]
			get
			{
				return _maxDegree - _minDegree;
			}
		}

		public Temperature GetTemperatureType(float degree)
		{
			degree = Mathf.Clamp(degree, _minDegree, _maxDegree);
			if (degree < (float)_lowBorder)
			{
				return Temperature.Cold;
			}
			if ((float)_lowBorder <= degree && degree < (float)_highBorder)
			{
				return Temperature.Normal;
			}
			if ((float)_highBorder <= degree)
			{
				return Temperature.Hot;
			}
			return Temperature.Normal;
		}

		public float GetRandValue()
		{
			return UnityEngine.Random.Range(_minDegree, _maxDegree + 1);
		}

		public UnityEx.ValueTuple<int, int> GetValueRange(Temperature temp)
		{
			return temp switch
			{
				Temperature.Cold => new UnityEx.ValueTuple<int, int>(_minDegree, _lowBorder - _minDegree - 1), 
				Temperature.Normal => new UnityEx.ValueTuple<int, int>(_lowBorder, _highBorder - _lowBorder - 1), 
				Temperature.Hot => new UnityEx.ValueTuple<int, int>(_highBorder, _maxDegree - _highBorder), 
				_ => new UnityEx.ValueTuple<int, int>(0, 0), 
			};
		}

		public int GetRandValue(Temperature temp)
		{
			return temp switch
			{
				Temperature.Cold => UnityEngine.Random.Range(_minDegree, _lowBorder), 
				Temperature.Normal => UnityEngine.Random.Range(_lowBorder, _highBorder), 
				Temperature.Hot => UnityEngine.Random.Range(_highBorder, _maxDegree + 1), 
				_ => 0, 
			};
		}
	}

	[Serializable]
	public struct WeatherTemperatureRangeDayInfo
	{
		[SerializeField]
		private EnvironmentSimulator.DateTimeSerialization _dayTime;

		[SerializeField]
		private EnvironmentSimulator.DateTimeSerialization _nightTime;

		[SerializeField]
		private WeatherTemperatureRangeInfo _dayTimeRange;

		[SerializeField]
		private WeatherTemperatureRangeInfo _nightTimeRange;

		public EnvironmentSimulator.DateTimeSerialization DayTime
		{
			[CompilerGenerated]
			get
			{
				return _dayTime;
			}
		}

		public EnvironmentSimulator.DateTimeSerialization NightTime
		{
			[CompilerGenerated]
			get
			{
				return _nightTime;
			}
		}

		public Threshold GetRange(TimeZone timeZone, Weather weather)
		{
			return timeZone switch
			{
				TimeZone.Day => _dayTimeRange.GetRange(weather), 
				TimeZone.Night => _nightTimeRange.GetRange(weather), 
				_ => new Threshold(0f, 0f), 
			};
		}
	}

	[Serializable]
	public struct WeatherTemperatureRangeInfo
	{
		[SerializeField]
		private Threshold _clearRange;

		[SerializeField]
		private Threshold _cloud1Range;

		[SerializeField]
		private Threshold _cloud2Range;

		[SerializeField]
		private Threshold _cloud3Range;

		[SerializeField]
		private Threshold _cloud4Range;

		[SerializeField]
		private Threshold _fogRange;

		[SerializeField]
		private Threshold _rainRange;

		[SerializeField]
		private Threshold _stormRange;

		public Threshold GetRange(Weather weather)
		{
			return weather switch
			{
				Weather.Clear => _clearRange, 
				Weather.Cloud1 => _cloud1Range, 
				Weather.Cloud2 => _cloud2Range, 
				Weather.Cloud3 => _cloud3Range, 
				Weather.Cloud4 => _cloud4Range, 
				Weather.Fog => _fogRange, 
				Weather.Rain => _rainRange, 
				Weather.Storm => _stormRange, 
				_ => new Threshold(0f, 0f), 
			};
		}
	}

	[SerializeField]
	[PropertyRange(1.0, 120.0)]
	private float _dayLengthInMinute = 50f;

	[SerializeField]
	[DisableInPlayMode]
	private EnvironmentSimulator.DateTimeSerialization _morningTime = new EnvironmentSimulator.DateTimeSerialization(new DateTime(1, 1, 1, 6, 0, 0));

	[SerializeField]
	[DisableInPlayMode]
	private EnvironmentSimulator.DateTimeSerialization _dayTime = new EnvironmentSimulator.DateTimeSerialization(new DateTime(1, 1, 1, 10, 0, 0));

	[SerializeField]
	[DisableInPlayMode]
	private EnvironmentSimulator.DateTimeSerialization _nightTime = new EnvironmentSimulator.DateTimeSerialization(new DateTime(1, 1, 1, 18, 0, 0));

	[SerializeField]
	private Threshold _weatherDurationMinThreshold = new Threshold(1f, 3f);

	[SerializeField]
	private float _weatherDurationMax = 4f;

	[SerializeField]
	[PropertyRange(0.0, 1.0)]
	private float _rainIntensity = 1f;

	[SerializeField]
	[PropertyRange(0.0, 1.0)]
	private float _rainMistThreshold = 1f;

	[SerializeField]
	private float _windSoundVolumeModifier = 0.5f;

	[SerializeField]
	private Threshold _windSpeedRange = new Threshold(50f, 500f);

	[SerializeField]
	[MinValue(1.0)]
	private float _windSoundMultiplier = 500f;

	[SerializeField]
	private Threshold _windChangeIntervalThreshold = new Threshold(5f, 30f);

	[SerializeField]
	private TemperatureBorderInfo _temperatureBorder = default(TemperatureBorderInfo);

	[SerializeField]
	private int _searchCount;

	[SerializeField]
	private float _searchCoolTimeDuration = 1f;

	[SerializeField]
	private float _runtimeMapItemLifeTime = 1f;

	[SerializeField]
	private EnvironmentSimulator.DateTimeSerialization _lightMorningTime = new EnvironmentSimulator.DateTimeSerialization(new DateTime(1, 1, 1, 7, 0, 0));

	[SerializeField]
	private EnvironmentSimulator.DateTimeSerialization _lightDayTime = new EnvironmentSimulator.DateTimeSerialization(new DateTime(1, 1, 1, 15, 0, 0));

	[SerializeField]
	private EnvironmentSimulator.DateTimeSerialization _lightNightTime = new EnvironmentSimulator.DateTimeSerialization(new DateTime(1, 1, 1, 18, 0, 0));

	[SerializeField]
	private WeatherTemperatureRangeDayInfo _weatherTemperatureRange = default(WeatherTemperatureRangeDayInfo);

	public float DayLengthInMinute
	{
		get
		{
			return _dayLengthInMinute;
		}
		set
		{
			_dayLengthInMinute = value;
		}
	}

	public DateTime MorningTime
	{
		[CompilerGenerated]
		get
		{
			return _morningTime.Time;
		}
	}

	public DateTime DayTime
	{
		[CompilerGenerated]
		get
		{
			return _dayTime.Time;
		}
	}

	public DateTime NightTime
	{
		[CompilerGenerated]
		get
		{
			return _nightTime.Time;
		}
	}

	public Threshold WeatherDurationMinThreshold
	{
		[CompilerGenerated]
		get
		{
			return _weatherDurationMinThreshold;
		}
	}

	public float WeatherDurationMax
	{
		[CompilerGenerated]
		get
		{
			return _weatherDurationMax;
		}
	}

	public float RainIntensity
	{
		[CompilerGenerated]
		get
		{
			return _rainIntensity;
		}
	}

	public float RainMistThreshold
	{
		[CompilerGenerated]
		get
		{
			return _rainMistThreshold;
		}
	}

	public float WindSoundVolumeModifier
	{
		[CompilerGenerated]
		get
		{
			return _windSoundVolumeModifier;
		}
	}

	public Threshold WindSpeedRange
	{
		[CompilerGenerated]
		get
		{
			return _windSpeedRange;
		}
	}

	public float WindSoundMultiplier
	{
		[CompilerGenerated]
		get
		{
			return _windSoundMultiplier;
		}
	}

	public Threshold WindChangeIntervalThreshold
	{
		[CompilerGenerated]
		get
		{
			return _windChangeIntervalThreshold;
		}
	}

	public TemperatureBorderInfo TemperatureBorder
	{
		[CompilerGenerated]
		get
		{
			return _temperatureBorder;
		}
	}

	public int SearchCount
	{
		[CompilerGenerated]
		get
		{
			return _searchCount;
		}
	}

	public float SearchCoolTimeDuration
	{
		[CompilerGenerated]
		get
		{
			return _searchCoolTimeDuration;
		}
	}

	public float RuntimeMapItemLifeTime
	{
		[CompilerGenerated]
		get
		{
			return _runtimeMapItemLifeTime;
		}
	}

	public DateTime LightMorningTime
	{
		[CompilerGenerated]
		get
		{
			return _lightMorningTime.Time;
		}
	}

	public DateTime LightDayTime
	{
		[CompilerGenerated]
		get
		{
			return _lightDayTime.Time;
		}
	}

	public DateTime LightNightTime
	{
		[CompilerGenerated]
		get
		{
			return _lightNightTime.Time;
		}
	}

	public WeatherTemperatureRangeDayInfo WeatherTemperatureRange
	{
		[CompilerGenerated]
		get
		{
			return _weatherTemperatureRange;
		}
	}
}
