using System;
using UnityEngine;

[Serializable]
public class EnviroProfile : ScriptableObject
{
	public enum settingsMode
	{
		Lighting,
		Sky,
		Weather,
		Clouds,
		Fog,
		VolumeLighting,
		Lightshafts,
		Season,
		Satellites,
		Audio,
		Quality
	}

	public string version;

	public EnviroLightSettings lightSettings = new EnviroLightSettings();

	public EnviroVolumeLightingSettings volumeLightSettings = new EnviroVolumeLightingSettings();

	public EnviroSkySettings skySettings = new EnviroSkySettings();

	public EnviroCloudSettings cloudsSettings = new EnviroCloudSettings();

	public EnviroWeatherSettings weatherSettings = new EnviroWeatherSettings();

	public EnviroFogSettings fogSettings = new EnviroFogSettings();

	public EnviroLightShaftsSettings lightshaftsSettings = new EnviroLightShaftsSettings();

	public EnviroSeasonSettings seasonsSettings = new EnviroSeasonSettings();

	public EnviroAudioSettings audioSettings = new EnviroAudioSettings();

	public EnviroSatellitesSettings satelliteSettings = new EnviroSatellitesSettings();

	public EnviroQualitySettings qualitySettings = new EnviroQualitySettings();

	[HideInInspector]
	public settingsMode viewMode;

	[HideInInspector]
	public bool showPlayerSetup = true;

	[HideInInspector]
	public bool showRenderingSetup;

	[HideInInspector]
	public bool showComponentsSetup;

	[HideInInspector]
	public bool showTimeUI;

	[HideInInspector]
	public bool showWeatherUI;

	[HideInInspector]
	public bool showAudioUI;

	[HideInInspector]
	public bool showEffectsUI;

	[HideInInspector]
	public bool modified;
}
