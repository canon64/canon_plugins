using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Enviro/Weather Zone")]
public class EnviroZone : MonoBehaviour
{
	public enum WeatherUpdateMode
	{
		GameTimeHours,
		RealTimeMinutes
	}

	[Tooltip("Defines the zone name.")]
	public string zoneName;

	[Tooltip("Uncheck to remove OnTriggerExit call when using overlapping zone layout.")]
	public bool ExitToDefault = true;

	public List<EnviroWeatherPrefab> zoneWeather = new List<EnviroWeatherPrefab>();

	public List<EnviroWeatherPrefab> curPossibleZoneWeather;

	[Header("Zone weather settings:")]
	[Tooltip("Add all weather prefabs for this zone here.")]
	public List<EnviroWeatherPreset> zoneWeatherPresets = new List<EnviroWeatherPreset>();

	[Tooltip("Shall weather changes occure based on gametime or realtime?")]
	public WeatherUpdateMode updateMode;

	[Tooltip("Defines how often (gametime hours or realtime minutes) the system will heck to change the current weather conditions.")]
	public float WeatherUpdateIntervall = 6f;

	[Header("Zone scaling and gizmo:")]
	[Tooltip("Defines the zone scale.")]
	public Vector3 zoneScale = new Vector3(100f, 100f, 100f);

	[Tooltip("Defines the color of the zone's gizmo in editor mode.")]
	public Color zoneGizmoColor = Color.gray;

	[Header("Current active weather:")]
	[Tooltip("The current active weather conditions.")]
	public EnviroWeatherPrefab currentActiveZoneWeatherPrefab;

	public EnviroWeatherPreset currentActiveZoneWeatherPreset;

	[HideInInspector]
	public EnviroWeatherPrefab lastActiveZoneWeatherPrefab;

	[HideInInspector]
	public EnviroWeatherPreset lastActiveZoneWeatherPreset;

	private BoxCollider zoneCollider;

	private double nextUpdate;

	private float nextUpdateRealtime;

	private bool init;

	private bool isDefault;

	private void Start()
	{
		if (zoneWeatherPresets.Count > 0)
		{
			zoneCollider = base.gameObject.AddComponent<BoxCollider>();
			zoneCollider.isTrigger = true;
			if (!GetComponent<EnviroSky>())
			{
				EnviroSky.instance.RegisterZone(this);
			}
			else
			{
				isDefault = true;
			}
			UpdateZoneScale();
			nextUpdate = EnviroSky.instance.currentTimeInHours + (double)WeatherUpdateIntervall;
			nextUpdateRealtime = Time.time + WeatherUpdateIntervall * 60f;
		}
		else
		{
			Debug.LogError("Please add Weather Prefabs to Zone:" + base.gameObject.name);
		}
	}

	public void UpdateZoneScale()
	{
		if (!isDefault)
		{
			zoneCollider.size = zoneScale;
		}
		else
		{
			zoneCollider.size = Vector3.one * (1f / EnviroSky.instance.transform.localScale.y) * 0.25f;
		}
	}

	public void CreateZoneWeatherTypeList()
	{
		for (int i = 0; i < zoneWeatherPresets.Count; i++)
		{
			if (zoneWeatherPresets[i] == null)
			{
				Debug.Log("Warning! Missing Weather Preset in Zone: " + zoneName);
				return;
			}
			bool flag = true;
			for (int j = 0; j < EnviroSky.instance.Weather.weatherPresets.Count; j++)
			{
				if (zoneWeatherPresets[i] == EnviroSky.instance.Weather.weatherPresets[j])
				{
					flag = false;
					zoneWeather.Add(EnviroSky.instance.Weather.WeatherPrefabs[j]);
				}
			}
			if (EnviroSky.instance.Weather.VFXHolder == null)
			{
				flag = false;
			}
			if (flag)
			{
				GameObject gameObject = new GameObject();
				EnviroWeatherPrefab enviroWeatherPrefab = gameObject.AddComponent<EnviroWeatherPrefab>();
				enviroWeatherPrefab.weatherPreset = zoneWeatherPresets[i];
				gameObject.name = enviroWeatherPrefab.weatherPreset.Name;
				enviroWeatherPrefab.effectEmmisionRates.Clear();
				gameObject.transform.parent = EnviroSky.instance.Weather.VFXHolder.transform;
				gameObject.transform.localPosition = Vector3.zero;
				gameObject.transform.localRotation = Quaternion.identity;
				zoneWeather.Add(enviroWeatherPrefab);
				EnviroSky.instance.Weather.WeatherPrefabs.Add(enviroWeatherPrefab);
				EnviroSky.instance.Weather.weatherPresets.Add(zoneWeatherPresets[i]);
			}
		}
		for (int k = 0; k < zoneWeather.Count; k++)
		{
			for (int l = 0; l < zoneWeather[k].effectSystems.Count; l++)
			{
				zoneWeather[k].effectEmmisionRates.Add(EnviroSky.GetEmissionRate(zoneWeather[k].effectSystems[l]));
				EnviroSky.SetEmissionRate(zoneWeather[k].effectSystems[l], 0f);
			}
		}
		if (isDefault && EnviroSky.instance.Weather.startWeatherPreset != null)
		{
			EnviroSky.instance.SetWeatherOverwrite(EnviroSky.instance.Weather.startWeatherPreset);
			for (int m = 0; m < zoneWeather.Count; m++)
			{
				if (zoneWeather[m].weatherPreset == EnviroSky.instance.Weather.startWeatherPreset)
				{
					currentActiveZoneWeatherPrefab = zoneWeather[m];
					lastActiveZoneWeatherPrefab = zoneWeather[m];
				}
			}
			currentActiveZoneWeatherPreset = EnviroSky.instance.Weather.startWeatherPreset;
			lastActiveZoneWeatherPreset = EnviroSky.instance.Weather.startWeatherPreset;
		}
		else
		{
			currentActiveZoneWeatherPrefab = zoneWeather[0];
			lastActiveZoneWeatherPrefab = zoneWeather[0];
			currentActiveZoneWeatherPreset = zoneWeatherPresets[0];
			lastActiveZoneWeatherPreset = zoneWeatherPresets[0];
		}
		nextUpdate = EnviroSky.instance.currentTimeInHours + (double)WeatherUpdateIntervall;
	}

	private void BuildNewWeatherList()
	{
		curPossibleZoneWeather = new List<EnviroWeatherPrefab>();
		for (int i = 0; i < zoneWeather.Count; i++)
		{
			switch (EnviroSky.instance.Seasons.currentSeasons)
			{
			case EnviroSeasons.Seasons.Spring:
				if (zoneWeather[i].weatherPreset.Spring)
				{
					curPossibleZoneWeather.Add(zoneWeather[i]);
				}
				break;
			case EnviroSeasons.Seasons.Summer:
				if (zoneWeather[i].weatherPreset.Summer)
				{
					curPossibleZoneWeather.Add(zoneWeather[i]);
				}
				break;
			case EnviroSeasons.Seasons.Autumn:
				if (zoneWeather[i].weatherPreset.Autumn)
				{
					curPossibleZoneWeather.Add(zoneWeather[i]);
				}
				break;
			case EnviroSeasons.Seasons.Winter:
				if (zoneWeather[i].weatherPreset.winter)
				{
					curPossibleZoneWeather.Add(zoneWeather[i]);
				}
				break;
			}
		}
	}

	private EnviroWeatherPrefab PossibiltyCheck()
	{
		List<EnviroWeatherPrefab> list = new List<EnviroWeatherPrefab>();
		for (int i = 0; i < curPossibleZoneWeather.Count; i++)
		{
			int num = Random.Range(0, 100);
			if (EnviroSky.instance.Seasons.currentSeasons == EnviroSeasons.Seasons.Spring)
			{
				if ((float)num <= curPossibleZoneWeather[i].weatherPreset.possibiltyInSpring)
				{
					list.Add(curPossibleZoneWeather[i]);
				}
			}
			else if (EnviroSky.instance.Seasons.currentSeasons == EnviroSeasons.Seasons.Summer)
			{
				if ((float)num <= curPossibleZoneWeather[i].weatherPreset.possibiltyInSummer)
				{
					list.Add(curPossibleZoneWeather[i]);
				}
			}
			else if (EnviroSky.instance.Seasons.currentSeasons == EnviroSeasons.Seasons.Autumn)
			{
				if ((float)num <= curPossibleZoneWeather[i].weatherPreset.possibiltyInAutumn)
				{
					list.Add(curPossibleZoneWeather[i]);
				}
			}
			else if (EnviroSky.instance.Seasons.currentSeasons == EnviroSeasons.Seasons.Winter && (float)num <= curPossibleZoneWeather[i].weatherPreset.possibiltyInWinter)
			{
				list.Add(curPossibleZoneWeather[i]);
			}
		}
		if (list.Count > 0)
		{
			EnviroSky.instance.NotifyZoneWeatherChanged(list[0].weatherPreset, this);
			return list[0];
		}
		return currentActiveZoneWeatherPrefab;
	}

	private void WeatherUpdate()
	{
		nextUpdate = EnviroSky.instance.currentTimeInHours + (double)WeatherUpdateIntervall;
		nextUpdateRealtime = Time.time + WeatherUpdateIntervall * 60f;
		BuildNewWeatherList();
		lastActiveZoneWeatherPrefab = currentActiveZoneWeatherPrefab;
		lastActiveZoneWeatherPreset = currentActiveZoneWeatherPreset;
		currentActiveZoneWeatherPrefab = PossibiltyCheck();
		currentActiveZoneWeatherPreset = currentActiveZoneWeatherPrefab.weatherPreset;
		EnviroSky.instance.NotifyZoneWeatherChanged(currentActiveZoneWeatherPreset, this);
	}

	private IEnumerator CreateWeatherListLate()
	{
		yield return 0;
		CreateZoneWeatherTypeList();
		init = true;
	}

	private void LateUpdate()
	{
		if (EnviroSky.instance == null)
		{
			Debug.Log("No EnviroSky instance found!");
			return;
		}
		if (EnviroSky.instance.started && !init)
		{
			if (isDefault)
			{
				CreateZoneWeatherTypeList();
				init = true;
			}
			else
			{
				StartCoroutine(CreateWeatherListLate());
			}
		}
		if (updateMode == WeatherUpdateMode.GameTimeHours)
		{
			if (EnviroSky.instance.currentTimeInHours > nextUpdate && EnviroSky.instance.Weather.updateWeather && EnviroSky.instance.started)
			{
				WeatherUpdate();
			}
		}
		else if (Time.time > nextUpdateRealtime && EnviroSky.instance.Weather.updateWeather && EnviroSky.instance.started)
		{
			WeatherUpdate();
		}
		if (!(EnviroSky.instance.Player == null) && isDefault && init)
		{
			zoneCollider.center = new Vector3(0f, (EnviroSky.instance.Player.transform.position.y - EnviroSky.instance.transform.position.y) / EnviroSky.instance.transform.lossyScale.y, 0f);
		}
	}

	private void OnTriggerEnter(Collider col)
	{
		if (EnviroSky.instance == null)
		{
			return;
		}
		if (EnviroSky.instance.profile.weatherSettings.useTag)
		{
			if (col.gameObject.tag == EnviroSky.instance.gameObject.tag)
			{
				EnviroSky.instance.Weather.currentActiveZone = this;
				EnviroSky.instance.NotifyZoneChanged(this);
			}
		}
		else if ((bool)col.gameObject.GetComponent<EnviroSky>())
		{
			EnviroSky.instance.Weather.currentActiveZone = this;
			EnviroSky.instance.NotifyZoneChanged(this);
		}
	}

	private void OnTriggerExit(Collider col)
	{
		if (!ExitToDefault || EnviroSky.instance == null)
		{
			return;
		}
		if (EnviroSky.instance.profile.weatherSettings.useTag)
		{
			if (col.gameObject.tag == EnviroSky.instance.gameObject.tag)
			{
				EnviroSky.instance.Weather.currentActiveZone = EnviroSky.instance.Weather.zones[0];
				EnviroSky.instance.NotifyZoneChanged(EnviroSky.instance.Weather.zones[0]);
			}
		}
		else if ((bool)col.gameObject.GetComponent<EnviroSky>())
		{
			EnviroSky.instance.Weather.currentActiveZone = EnviroSky.instance.Weather.zones[0];
			EnviroSky.instance.NotifyZoneChanged(EnviroSky.instance.Weather.zones[0]);
		}
	}

	private void OnDrawGizmos()
	{
		Gizmos.color = zoneGizmoColor;
		Gizmos.DrawCube(base.transform.position, new Vector3(zoneScale.x, zoneScale.y, zoneScale.z));
	}
}
