using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using AIProject.Definitions;
using AIProject.SaveData;
using Manager;
using Sirenix.OdinInspector;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEx;
using UnityEx.Misc;

namespace AIProject;

public class EnvironmentSimulator : SerializedMonoBehaviour
{
	[Serializable]
	public struct DateTimeThreshold
	{
		public DateTimeSerialization min;

		public DateTimeSerialization max;
	}

	[Serializable]
	public struct DateTimeSerialization
	{
		[SerializeField]
		private int _day;

		[SerializeField]
		private int _hour;

		[SerializeField]
		private int _minute;

		public DateTime Time
		{
			get
			{
				return new DateTime(1, 1, 1 + _day, _hour, _minute, 0);
			}
			set
			{
				_day = value.Day - 1;
				_hour = value.Hour;
				_minute = value.Minute;
			}
		}

		public DateTimeSerialization(DateTime dateTime)
		{
			_day = dateTime.Day;
			_hour = dateTime.Hour;
			_minute = dateTime.Minute;
		}
	}

	[SerializeField]
	private EnviroSky _enviroSky;

	[SerializeField]
	private EnviroZone _defaultEnviroZone;

	[SerializeField]
	private EnviroEvents _enviroEvents;

	private bool _isActiveSimElement = true;

	[SerializeField]
	private BoolReactiveProperty _enableTimeProgression = new BoolReactiveProperty();

	private IConnectableObservable<bool> _enableTimeChanged;

	[SerializeField]
	private GameObject _skyDomeObject;

	[SerializeField]
	[HideInEditorMode]
	[DisableInPlayMode]
	private TimeZone _timeZone = TimeZone.Day;

	private ReactiveProperty<TimeZone> _reactiveTimeZone = new ReactiveProperty<TimeZone>(TimeZone.Day);

	private Subject<Weather> _weatherChanged;

	[SerializeField]
	[HideInEditorMode]
	[DisableInPlayMode]
	private Weather _weather;

	[SerializeField]
	[DisableInEditorMode]
	[DisableInPlayMode]
	private Temperature _temperature = Temperature.Normal;

	[ShowInInspector]
	[HideInEditorMode]
	[DisableInPlayMode]
	private float _temperatureValue = 25f;

	[SerializeField]
	private float _fadeTime = 10f;

	[SerializeField]
	private EnvironmentProfile _environmentProfile;

	[SerializeField]
	private float _enviroParticleHeight;

	[SerializeField]
	private bool _enviroParticleFollowTargetHeight;

	[SerializeField]
	[DisableInPlayMode]
	private Dictionary<Weather, List<ParticleSystem>> _enviroParticlePrefabTable = new Dictionary<Weather, List<ParticleSystem>>();

	[SerializeField]
	private Dictionary<Weather, float> _weatherTemperatureCorrectedValueTable = new Dictionary<Weather, float>();

	private Subject<Unit> _onEnvironmentChange;

	private Subject<TimeZone> _onTimeZoneChange;

	private Subject<TimeZone> _onMapLightTimeZoneChange;

	private DateTime _prevDateTime = DateTime.MinValue;

	private int _deltaSeconds;

	private List<AIProject.SaveData.Environment.ScheduleData> _schedules = new List<AIProject.SaveData.Environment.ScheduleData>();

	private Subject<TimeSpan> _onDay;

	private Subject<TimeSpan> _onHour;

	private Subject<TimeSpan> _onMinute;

	private Subject<TimeSpan> _onSecond;

	private TimeSpan _tempDayTimeOfDay = TimeSpan.MinValue;

	private TimeSpan _tempNightTimeOfDay = TimeSpan.MinValue;

	private TimeZone _tempTimeZone = TimeZone.Morning;

	private ReactiveProperty<TimeZone> _reactiveTempTimeZone = new ReactiveProperty<TimeZone>(TimeZone.Morning);

	private TimeZone _mapLightTimeZone = TimeZone.Day;

	private ReactiveProperty<TimeZone> _reactiveMapLightTimeZone = new ReactiveProperty<TimeZone>(TimeZone.Day);

	private List<EnviroParticleInfo> _playingEnviroParticles = new List<EnviroParticleInfo>();

	private Dictionary<int, EnviroParticleInfo> _enviroParticleDataTable = new Dictionary<int, EnviroParticleInfo>();

	private Dictionary<Weather, List<EnviroParticleInfo>> _enviroParticleObjectTable = new Dictionary<Weather, List<EnviroParticleInfo>>();

	private GameObject _enviroParticleRoot;

	private IDisposable _enviroParticleFollowDisposable;

	public bool EnabledSky
	{
		get
		{
			return _enviroSky.enabled;
		}
		set
		{
			_enviroSky.enabled = value;
		}
	}

	public EnviroSky EnviroSky
	{
		get
		{
			return _enviroSky;
		}
		set
		{
			_enviroSky = value;
			value.Weather.updateWeather = false;
		}
	}

	public EnviroZone DefaultEnviroZone
	{
		get
		{
			return _defaultEnviroZone;
		}
		set
		{
			_defaultEnviroZone = value;
		}
	}

	public EnviroEvents EnviroEvents
	{
		get
		{
			return _enviroEvents;
		}
		set
		{
			_enviroEvents = value;
		}
	}

	public bool IsActiveSimElement
	{
		get
		{
			return _isActiveSimElement;
		}
		set
		{
			_isActiveSimElement = value;
			_skyDomeObject.SetActiveSafe(value);
		}
	}

	public bool EnabledTimeProgression
	{
		get
		{
			return _enableTimeProgression.Value;
		}
		set
		{
			_enableTimeProgression.Value = value;
		}
	}

	public GameObject SkyDomeObject
	{
		get
		{
			return _skyDomeObject;
		}
		set
		{
			_skyDomeObject = value;
		}
	}

	public TimeZone TimeZone
	{
		[CompilerGenerated]
		get
		{
			return _timeZone;
		}
	}

	public Weather Weather
	{
		[CompilerGenerated]
		get
		{
			return _weather;
		}
	}

	public Temperature Temperature
	{
		[CompilerGenerated]
		get
		{
			return _temperature;
		}
	}

	public float TemperatureValue
	{
		get
		{
			return _temperatureValue;
		}
		set
		{
			float num = value;
			if (_environmentProfile != null)
			{
				float min = _environmentProfile.TemperatureBorder.MinDegree;
				float max = _environmentProfile.TemperatureBorder.MaxDegree;
				num = Mathf.Clamp(num, min, max);
			}
			if (_temperatureValue != num)
			{
				_temperatureValue = num;
				if (!(_environmentProfile == null))
				{
					Temperature temperatureType = _environmentProfile.TemperatureBorder.GetTemperatureType(_temperatureValue);
					RefreshTemperature(temperatureType);
				}
			}
		}
	}

	public EnvironmentProfile EnvironmentProfile
	{
		[CompilerGenerated]
		get
		{
			return _environmentProfile;
		}
	}

	public DateTime Now
	{
		get
		{
			EnviroTime gameTime = _enviroSky.GameTime;
			return new DateTime(gameTime.Years, 1, 1).AddDays(gameTime.Days - 1).AddHours(gameTime.Hours).AddMinutes(gameTime.Minutes)
				.AddSeconds(gameTime.Seconds);
		}
		set
		{
			_enviroSky.SetTime(value);
		}
	}

	public int DeltaSeconds
	{
		[CompilerGenerated]
		get
		{
			return _deltaSeconds;
		}
	}

	public ReadOnlyCollection<AIProject.SaveData.Environment.ScheduleData> Schedules
	{
		[CompilerGenerated]
		get
		{
			return _schedules.AsReadOnly();
		}
	}

	public EnviroTime OldTime { get; private set; } = new EnviroTime
	{
		Hours = 1
	};


	public EnviroTime OldDayUpdatedTime { get; private set; } = new EnviroTime
	{
		Hours = 1
	};


	public EnviroTime OldHourUpdatedTime { get; private set; } = new EnviroTime
	{
		Hours = 1
	};


	public EnviroTime OldMinuteUpdatedTime { get; private set; } = new EnviroTime
	{
		Hours = 1
	};


	public EnviroTime OldSecondUpdatedTime { get; private set; } = new EnviroTime
	{
		Hours = 1
	};


	public int MapID { get; set; }

	public TimeZone TempTimeZone
	{
		[CompilerGenerated]
		get
		{
			return _tempTimeZone;
		}
	}

	public TimeZone MapLightTimeZone
	{
		[CompilerGenerated]
		get
		{
			return _mapLightTimeZone;
		}
	}

	public GameObject EnviroParticleRoot
	{
		get
		{
			if (_enviroParticleRoot == null)
			{
				_enviroParticleRoot = new GameObject("Enviro Particle Root");
				_enviroParticleRoot.transform.SetParent(base.transform, worldPositionStays: false);
			}
			return _enviroParticleRoot;
		}
		private set
		{
			_enviroParticleRoot = value;
		}
	}

	public IObservable<bool> OnEnableChangeTimeAsObservable()
	{
		if (_enableTimeChanged == null)
		{
			_enableTimeChanged = _enableTimeProgression.TakeUntilDestroy(base.gameObject).Publish();
			_enableTimeChanged.Connect();
		}
		return _enableTimeChanged;
	}

	public void SetTimeProgress(bool progress)
	{
		if (!(_enviroSky == null))
		{
			if (progress)
			{
				_enviroSky.GameTime.ProgressTime = EnviroTime.TimeProgressMode.Simulated;
			}
			else
			{
				_enviroSky.GameTime.ProgressTime = EnviroTime.TimeProgressMode.None;
			}
		}
	}

	public IObservable<Weather> OnWeatherChangedAsObservable()
	{
		return _weatherChanged ?? (_weatherChanged = new Subject<Weather>());
	}

	public IObservable<Unit> OnEnvironmentChangedAsObservable()
	{
		return _onEnvironmentChange ?? (_onEnvironmentChange = new Subject<Unit>());
	}

	public IObservable<TimeZone> OnTimeZoneChangedAsObservable()
	{
		return _onTimeZoneChange ?? (_onTimeZoneChange = new Subject<TimeZone>());
	}

	public IObservable<TimeZone> OnMapLightTimeZoneChangedAsObservable()
	{
		return _onMapLightTimeZoneChange ?? (_onMapLightTimeZoneChange = new Subject<TimeZone>());
	}

	private void Awake()
	{
		ResetTempDateTime();
	}

	private void Start()
	{
		_reactiveTimeZone.Subscribe(delegate(TimeZone timeZone)
		{
			SetTimeZone(timeZone);
		});
		_reactiveTempTimeZone.Subscribe(delegate(TimeZone timeZone)
		{
			SetTempTimeZone(timeZone);
		});
		_reactiveMapLightTimeZone.Subscribe(delegate(TimeZone timeZone)
		{
			SetMapLightTimeZone(timeZone);
		});
	}

	private void Update()
	{
		float dayLengthInMinute = _environmentProfile.DayLengthInMinute;
		_enviroSky.GameTime.DayLengthInMinutes = dayLengthInMinute;
		_enviroSky.GameTime.NightLengthInMinutes = dayLengthInMinute;
		OnTimeUpdate();
		TimeSpan timeOfDay = Now.TimeOfDay;
		if (timeOfDay >= _environmentProfile.MorningTime.TimeOfDay && timeOfDay < _environmentProfile.DayTime.TimeOfDay)
		{
			_reactiveTimeZone.Value = TimeZone.Morning;
		}
		else if (timeOfDay >= _environmentProfile.DayTime.TimeOfDay && timeOfDay < _environmentProfile.NightTime.TimeOfDay)
		{
			_reactiveTimeZone.Value = TimeZone.Day;
		}
		else
		{
			_reactiveTimeZone.Value = TimeZone.Night;
		}
		OnTemperatureValueUpdate(timeOfDay);
		OnMapLightTimeUpdate(timeOfDay);
	}

	public void InitializeEvents()
	{
	}

	private void OnHourUpdate()
	{
	}

	public void OnMinuteUpdate(TimeSpan deltaTime)
	{
	}

	private void OnSecondUpdate()
	{
		DateTime now = Now;
		_deltaSeconds = (int)(now - _prevDateTime).TotalSeconds;
		_prevDateTime = now;
	}

	private void OnTimeUpdate()
	{
		EnviroTime oldTime = OldTime;
		EnviroTime gameTime = _enviroSky.GameTime;
		DateTime newTime = new DateTime(1, 1, gameTime.Days, gameTime.Hours, gameTime.Minutes, gameTime.Seconds);
		bool flag = false;
		if (gameTime.Days > oldTime.Days)
		{
			TimeSpan value = DeltaTimeSpan(newTime, OldDayUpdatedTime);
			TimeSpan value2 = DeltaTimeSpan(newTime, OldHourUpdatedTime);
			TimeSpan value3 = DeltaTimeSpan(newTime, OldMinuteUpdatedTime);
			TimeSpan value4 = DeltaTimeSpan(newTime, OldSecondUpdatedTime);
			gameTime.Days = 1;
			if (_onDay != null)
			{
				_onDay.OnNext(value);
			}
			if (_onHour != null)
			{
				_onHour.OnNext(value2);
			}
			if (_onMinute != null)
			{
				_onMinute.OnNext(value3);
			}
			if (_onSecond != null)
			{
				_onSecond.OnNext(value4);
			}
			flag = true;
			SetTimeToEnviroTime(OldDayUpdatedTime, gameTime);
			SetTimeToEnviroTime(OldHourUpdatedTime, gameTime);
			SetTimeToEnviroTime(OldMinuteUpdatedTime, gameTime);
			SetTimeToEnviroTime(OldSecondUpdatedTime, gameTime);
		}
		else if (gameTime.Hours > oldTime.Hours)
		{
			TimeSpan value5 = DeltaTimeSpan(newTime, OldHourUpdatedTime);
			TimeSpan value6 = DeltaTimeSpan(newTime, OldMinuteUpdatedTime);
			TimeSpan value7 = DeltaTimeSpan(newTime, OldSecondUpdatedTime);
			if (_onHour != null)
			{
				_onHour.OnNext(value5);
			}
			if (_onMinute != null)
			{
				_onMinute.OnNext(value6);
			}
			if (_onSecond != null)
			{
				_onSecond.OnNext(value7);
			}
			flag = true;
			SetTimeToEnviroTime(OldHourUpdatedTime, gameTime);
			SetTimeToEnviroTime(OldMinuteUpdatedTime, gameTime);
			SetTimeToEnviroTime(OldSecondUpdatedTime, gameTime);
		}
		else if (gameTime.Minutes > oldTime.Minutes)
		{
			TimeSpan value8 = DeltaTimeSpan(newTime, OldMinuteUpdatedTime);
			TimeSpan value9 = DeltaTimeSpan(newTime, OldSecondUpdatedTime);
			if (_onMinute != null)
			{
				_onMinute.OnNext(value8);
			}
			if (_onSecond != null)
			{
				_onSecond.OnNext(value9);
			}
			flag = true;
			SetTimeToEnviroTime(OldMinuteUpdatedTime, gameTime);
			SetTimeToEnviroTime(OldSecondUpdatedTime, gameTime);
		}
		else if (gameTime.Seconds > oldTime.Seconds)
		{
			TimeSpan value10 = DeltaTimeSpan(newTime, OldSecondUpdatedTime);
			if (_onSecond != null)
			{
				_onSecond.OnNext(value10);
			}
			flag = true;
			SetTimeToEnviroTime(OldSecondUpdatedTime, gameTime);
		}
		if (flag)
		{
			OldTime.Days = gameTime.Days;
			OldTime.Hours = gameTime.Hours;
			OldTime.Minutes = gameTime.Minutes;
			OldTime.Seconds = gameTime.Seconds;
		}
	}

	public void SetTimeToEnviroTime(EnviroTime time, EnviroTime newTime)
	{
		time.Days = newTime.Days;
		time.Hours = newTime.Hours;
		time.Minutes = newTime.Minutes;
		time.Seconds = newTime.Seconds;
	}

	public TimeSpan DeltaTimeSpan(DateTime newTime, EnviroTime oldEnviroTime)
	{
		DateTime dateTime = new DateTime(1, 1, oldEnviroTime.Days, oldEnviroTime.Hours, oldEnviroTime.Minutes, oldEnviroTime.Seconds);
		return newTime - dateTime;
	}

	public IObservable<TimeSpan> OnDayAsObservable()
	{
		return _onDay ?? (_onDay = new Subject<TimeSpan>());
	}

	public IObservable<TimeSpan> OnHourAsObservable()
	{
		return _onHour ?? (_onHour = new Subject<TimeSpan>());
	}

	public IObservable<TimeSpan> OnMinuteAsObservable()
	{
		return _onMinute ?? (_onMinute = new Subject<TimeSpan>());
	}

	public IObservable<TimeSpan> OnSecondAsObservable()
	{
		return _onSecond ?? (_onSecond = new Subject<TimeSpan>());
	}

	public void SetTimeZone(TimeZone zone)
	{
		_timeZone = zone;
		if (_onTimeZoneChange != null)
		{
			_onTimeZoneChange.OnNext(zone);
		}
		if (_onEnvironmentChange != null)
		{
			_onEnvironmentChange.OnNext(Unit.Default);
		}
	}

	public void RefreshWeather(Weather weather, bool forced = false)
	{
		if (forced || _weather != weather)
		{
			Debug.Log($"set weather: {weather.ToString()}");
			_weather = weather;
			if (_weatherChanged != null)
			{
				_weatherChanged.OnNext(weather);
			}
			_enviroSky.ChangeWeather((int)weather);
			RefreshEnviroParticles(weather);
		}
	}

	private void ResetTempDateTime()
	{
		if (_environmentProfile != null)
		{
			_tempDayTimeOfDay = _environmentProfile.WeatherTemperatureRange.DayTime.Time.TimeOfDay;
			_tempNightTimeOfDay = _environmentProfile.WeatherTemperatureRange.NightTime.Time.TimeOfDay;
		}
	}

	private void SetTempTimeZone(TimeZone timeZone)
	{
		_tempTimeZone = timeZone;
		switch (_tempTimeZone)
		{
		case TimeZone.Day:
		{
			WeatherProbability probWeatherProfile = Singleton<Manager.Resources>.Instance.CommonDefine.ProbWeatherProfile;
			List<Weather> list = Enum.GetValues(typeof(Weather)).Cast<Weather>().Skip(0)
				.ToList();
			Weather weather = probWeatherProfile.Lottery(list);
			while (_weather == weather)
			{
				list.Remove(weather);
				weather = probWeatherProfile.Lottery(list);
			}
			RefreshWeather(weather);
			break;
		}
		}
		RefreshTemperatureValue();
	}

	public void SetTemperatureValue(float tempValue)
	{
		Observable.TimerFrame(2).TakeUntilDestroy(this).Subscribe(delegate
		{
			if (!(_environmentProfile == null))
			{
				Threshold range = _environmentProfile.WeatherTemperatureRange.GetRange(_tempTimeZone, _weather);
				if (range.min <= tempValue && tempValue <= range.max)
				{
					TemperatureValue = tempValue;
				}
				else
				{
					TemperatureValue = range.RandomValue;
				}
			}
		});
	}

	public void RefreshTemperatureValue()
	{
		if (!Singleton<Manager.Resources>.IsInstance())
		{
			return;
		}
		Dictionary<int, Dictionary<int, Dictionary<int, List<UnityEx.ValueTuple<int, int>>>>> tempRangeTable = Singleton<Manager.Resources>.Instance.Map.TempRangeTable;
		if (tempRangeTable.IsNullOrEmpty())
		{
			return;
		}
		if (!tempRangeTable.TryGetValue(MapID, out var value) || value.IsNullOrEmpty())
		{
			if (MapID != 0)
			{
				tempRangeTable.TryGetValue(0, out value);
			}
			if (value.IsNullOrEmpty())
			{
				return;
			}
		}
		if (!AIProject.Definitions.Environment.TimeZoneIDTable.TryGetValue(_tempTimeZone, out var value2) || !value.TryGetValue(value2, out var value3) || value3.IsNullOrEmpty() || !value3.TryGetValue((int)_weather, out var value4) || value4.IsNullOrEmpty())
		{
			return;
		}
		List<UnityEx.ValueTuple<int, int>> list = ListPool<UnityEx.ValueTuple<int, int>>.Get();
		list.AddRange(value4);
		int num = 0;
		foreach (UnityEx.ValueTuple<int, int> item in list)
		{
			num += item.Item2;
		}
		int num2 = UnityEngine.Random.Range(0, num);
		while (0 < list.Count)
		{
			UnityEx.ValueTuple<int, int> valueTuple = list.PopFront();
			if (num2 < valueTuple.Item2)
			{
				TemperatureValue = valueTuple.Item1;
				ListPool<UnityEx.ValueTuple<int, int>>.Release(list);
				return;
			}
			num2 -= valueTuple.Item2;
		}
		ListPool<UnityEx.ValueTuple<int, int>>.Release(list);
	}

	private void OnTemperatureValueUpdate(TimeSpan now)
	{
		if (_tempNightTimeOfDay < _tempDayTimeOfDay)
		{
			if (now < _tempNightTimeOfDay || _tempDayTimeOfDay <= now)
			{
				_reactiveTempTimeZone.Value = TimeZone.Day;
			}
			else
			{
				_reactiveTempTimeZone.Value = TimeZone.Night;
			}
		}
		else if (_tempDayTimeOfDay <= now && now < _tempNightTimeOfDay)
		{
			_reactiveTempTimeZone.Value = TimeZone.Day;
		}
		else
		{
			_reactiveTempTimeZone.Value = TimeZone.Night;
		}
	}

	public void SetMapLightTimeZone(TimeZone timeZone)
	{
		_mapLightTimeZone = timeZone;
		if (_onMapLightTimeZoneChange != null)
		{
			_onMapLightTimeZoneChange.OnNext(timeZone);
		}
	}

	private void OnMapLightTimeUpdate(TimeSpan now)
	{
		if (_environmentProfile.LightMorningTime.TimeOfDay <= now && now < _environmentProfile.LightDayTime.TimeOfDay)
		{
			_reactiveMapLightTimeZone.Value = TimeZone.Morning;
		}
		else if (_environmentProfile.LightDayTime.TimeOfDay <= now && now < _environmentProfile.LightNightTime.TimeOfDay)
		{
			_reactiveMapLightTimeZone.Value = TimeZone.Day;
		}
		else
		{
			_reactiveMapLightTimeZone.Value = TimeZone.Night;
		}
	}

	public void EnviroParticleActivate(bool active)
	{
		GameObject enviroParticleRoot = EnviroParticleRoot;
		if (!(enviroParticleRoot == null) && enviroParticleRoot.activeSelf != active)
		{
			enviroParticleRoot.SetActive(active);
		}
	}

	public void InitializeEnviroParticle()
	{
		if (_enviroParticlePrefabTable.IsNullOrEmpty())
		{
			return;
		}
		GameObject enviroParticleRoot = EnviroParticleRoot;
		foreach (KeyValuePair<Weather, List<ParticleSystem>> item in _enviroParticlePrefabTable)
		{
			if (item.Value.IsNullOrEmpty())
			{
				continue;
			}
			foreach (ParticleSystem item2 in item.Value)
			{
				if (!(item2 == null))
				{
					int instanceID = item2.GetInstanceID();
					if (!_enviroParticleDataTable.TryGetValue(instanceID, out var value))
					{
						ParticleSystem particleSystem = UnityEngine.Object.Instantiate(item2, enviroParticleRoot.transform);
						value = (_enviroParticleDataTable[instanceID] = new EnviroParticleInfo(instanceID, particleSystem));
						particleSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
						value.Emission = 0f;
					}
					if (!_enviroParticleObjectTable.TryGetValue(item.Key, out var value2))
					{
						value2 = (_enviroParticleObjectTable[item.Key] = new List<EnviroParticleInfo>());
					}
					value2.Add(value);
				}
			}
		}
	}

	public void SetEnviroParticleTarget(Transform target)
	{
		if (target == null)
		{
			return;
		}
		GameObject envParRoot = EnviroParticleRoot;
		if (envParRoot == null)
		{
			return;
		}
		if (_enviroParticleFollowDisposable != null)
		{
			_enviroParticleFollowDisposable.Dispose();
		}
		_enviroParticleFollowDisposable = envParRoot.transform.LateUpdateAsObservable().TakeUntilDestroy(target).Subscribe(delegate
		{
			Vector3 position = target.position;
			if (_enviroParticleFollowTargetHeight)
			{
				position.y += _enviroParticleHeight;
			}
			else
			{
				position.y = _enviroParticleHeight;
			}
			envParRoot.transform.position = position;
		});
	}

	public void StopEnviroParticleTargetFollow()
	{
		if (_enviroParticleFollowDisposable != null)
		{
			_enviroParticleFollowDisposable.Dispose();
			_enviroParticleFollowDisposable = null;
		}
	}

	private void RefreshEnviroParticles(Weather weather)
	{
		if (!_enviroParticleObjectTable.TryGetValue(weather, out var value) || value.IsNullOrEmpty())
		{
			if (_playingEnviroParticles.IsNullOrEmpty())
			{
				return;
			}
			foreach (EnviroParticleInfo playingEnviroParticle in _playingEnviroParticles)
			{
				playingEnviroParticle.PlayFadeOut(_fadeTime);
			}
			_playingEnviroParticles.Clear();
			return;
		}
		List<EnviroParticleInfo> list = ListPool<EnviroParticleInfo>.Get();
		foreach (EnviroParticleInfo playingEnviroParticle2 in _playingEnviroParticles)
		{
			if (!value.Contains(playingEnviroParticle2) && !list.Contains(playingEnviroParticle2))
			{
				list.Add(playingEnviroParticle2);
			}
		}
		foreach (EnviroParticleInfo item in list)
		{
			item.PlayFadeOut(_fadeTime);
		}
		ListPool<EnviroParticleInfo>.Release(list);
		foreach (EnviroParticleInfo item2 in value)
		{
			item2.PlayFadeIn(_fadeTime);
		}
		_playingEnviroParticles.Clear();
		_playingEnviroParticles.AddRange(value);
	}

	public void RefreshTemperature(Temperature temperature)
	{
		if (_temperature != temperature)
		{
			_temperature = temperature;
		}
	}

	public bool Elapsed(DateTime dateTime)
	{
		return Now > dateTime;
	}

	public void ElapseDays(int days)
	{
		foreach (AIProject.SaveData.Environment.ScheduleData schedule in Schedules)
		{
			schedule.DaysToGo -= days;
		}
		List<AIProject.SaveData.Environment.ScheduleData> list = _schedules.FindAll(delegate(AIProject.SaveData.Environment.ScheduleData x)
		{
			TimeSpan timeSpan = Now - x.StartTime.DateTime;
			return x.DaysToGo == 0 && x.StartTime.DateTime < Now && x.Duration.TimeSpan < timeSpan;
		});
		foreach (AIProject.SaveData.Environment.ScheduleData item in list)
		{
			AgentActor agentActor = Singleton<Manager.Map>.Instance.AgentTable[item.Event.agentID];
			_schedules.Remove(item);
		}
	}
}
