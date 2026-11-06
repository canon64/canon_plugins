using System;
using System.Collections;
using System.Collections.Generic;
using Manager;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
public class EnviroSky : MonoBehaviour
{
	public enum EnviroCloudsMode
	{
		None,
		Both,
		Volume,
		Flat
	}

	public delegate void HourPassed();

	public delegate void DayPassed();

	public delegate void YearPassed();

	public delegate void WeatherChanged(EnviroWeatherPreset weatherType);

	public delegate void ZoneWeatherChanged(EnviroWeatherPreset weatherType, EnviroZone zone);

	public delegate void SeasonChanged(EnviroSeasons.Seasons season);

	public delegate void isNightE();

	public delegate void isDay();

	public delegate void ZoneChanged(EnviroZone zone);

	private static EnviroSky _instance;

	public string prefabVersion = "2.0.4";

	[Tooltip("Assign your player gameObject here. Required Field! or enable AssignInRuntime!")]
	public GameObject Player;

	[Tooltip("Assign your main camera here. Required Field! or enable AssignInRuntime!")]
	public Camera PlayerCamera;

	[Tooltip("If enabled Enviro will search for your Player and Camera by Tag!")]
	public bool AssignInRuntime;

	[Tooltip("Your Player Tag")]
	public string PlayerTag = string.Empty;

	[Tooltip("Your CameraTag")]
	public string CameraTag = "MainCamera";

	[Header("General")]
	[Tooltip("Enable this when using singlepass rendering.")]
	public bool dontDestroy;

	[Header("Camera Settings")]
	[Tooltip("Enable HDR Rendering. You want to use a third party tonemapping effect for best results!")]
	public bool HDR = true;

	[Header("Layer Setup")]
	[Tooltip("This is the layer id forfor the moon.")]
	public int moonRenderingLayer = 29;

	[Tooltip("This is the layer id for additional satellites like moons, planets.")]
	public int satelliteRenderingLayer = 30;

	[Tooltip("Activate to set recommended maincamera clear flag.")]
	public bool setCameraClearFlags = true;

	[Header("Virtual Reality")]
	[Tooltip("Enable this when using singlepass rendering.")]
	public bool singlePassVR;

	[Tooltip("Enable this to activate volume lighing")]
	[HideInInspector]
	public bool volumeLighting = true;

	[Tooltip("Enable this to activate global scattering fog. Disabled will also disable volume lighting")]
	[HideInInspector]
	public bool globalFog = true;

	[Header("Profile")]
	public EnviroProfile profile;

	[Header("Control")]
	public EnviroTime GameTime;

	public EnviroAudio Audio;

	public EnviroWeather Weather;

	public EnviroSeasons Seasons;

	public EnviroFogging Fog;

	public EnviroLightshafts LightShafts;

	[Header("Components")]
	public EnviroComponents Components;

	[HideInInspector]
	public bool started;

	[HideInInspector]
	public bool isNight = true;

	[HideInInspector]
	public EnviroLightSettings lightSettings = new EnviroLightSettings();

	[HideInInspector]
	public EnviroVolumeLightingSettings volumeLightSettings = new EnviroVolumeLightingSettings();

	[HideInInspector]
	public EnviroSkySettings skySettings = new EnviroSkySettings();

	[HideInInspector]
	public EnviroCloudSettings cloudsSettings = new EnviroCloudSettings();

	[HideInInspector]
	public EnviroWeatherSettings weatherSettings = new EnviroWeatherSettings();

	[HideInInspector]
	public EnviroFogSettings fogSettings = new EnviroFogSettings();

	[HideInInspector]
	public EnviroLightShaftsSettings lightshaftsSettings = new EnviroLightShaftsSettings();

	[HideInInspector]
	public EnviroSeasonSettings seasonsSettings = new EnviroSeasonSettings();

	[HideInInspector]
	public EnviroAudioSettings audioSettings = new EnviroAudioSettings();

	[HideInInspector]
	public EnviroSatellitesSettings satelliteSettings = new EnviroSatellitesSettings();

	[HideInInspector]
	public EnviroQualitySettings qualitySettings = new EnviroQualitySettings();

	public EnviroCloudsMode cloudsMode;

	private EnviroCloudsMode lastCloudsMode;

	private EnviroCloudSettings.CloudQuality lastCloudsQuality;

	private Material cloudShadows;

	[HideInInspector]
	public Camera moonCamera;

	[HideInInspector]
	public Camera satCamera;

	[HideInInspector]
	public EnviroVolumeLight directVolumeLight;

	[HideInInspector]
	public EnviroLightShafts lightShaftsScriptSun;

	[HideInInspector]
	public EnviroLightShafts lightShaftsScriptMoon;

	[HideInInspector]
	public EnviroSkyRendering EnviroSkyRender;

	[HideInInspector]
	public GameObject EffectsHolder;

	[HideInInspector]
	public EnviroAudioSource AudioSourceWeather;

	[HideInInspector]
	public EnviroAudioSource AudioSourceWeather2;

	[HideInInspector]
	public EnviroAudioSource AudioSourceAmbient;

	[HideInInspector]
	public EnviroAudioSource AudioSourceAmbient2;

	[HideInInspector]
	public AudioSource AudioSourceThunder;

	[HideInInspector]
	public EnviroAudioSource AudioSourceZone;

	[HideInInspector]
	public List<EnviroVegetationInstance> EnviroVegetationInstances = new List<EnviroVegetationInstance>();

	[HideInInspector]
	public Color currentWeatherSkyMod;

	[HideInInspector]
	public Color currentWeatherLightMod;

	[HideInInspector]
	public Color currentWeatherFogMod;

	[HideInInspector]
	public Color currentInteriorDirectLightMod;

	[HideInInspector]
	public Color currentInteriorAmbientLightMod;

	[HideInInspector]
	public Color currentInteriorAmbientEQLightMod;

	[HideInInspector]
	public Color currentInteriorAmbientGRLightMod;

	[HideInInspector]
	public Color currentInteriorSkyboxMod;

	[HideInInspector]
	public Color currentInteriorFogColorMod = new Color(0f, 0f, 0f, 0f);

	[HideInInspector]
	public float currentInteriorFogMod = 1f;

	[HideInInspector]
	public float currentInteriorWeatherEffectMod = 1f;

	[HideInInspector]
	public float currentInteriorZoneAudioVolume = 1f;

	[HideInInspector]
	public float currentInteriorZoneAudioFadingSpeed = 1f;

	[HideInInspector]
	public float globalVolumeLightIntensity;

	[HideInInspector]
	public EnviroWeatherCloudsConfig cloudsConfig;

	[HideInInspector]
	public float thunder;

	[HideInInspector]
	public List<GameObject> satellites = new List<GameObject>();

	[HideInInspector]
	public List<GameObject> satellitesRotation = new List<GameObject>();

	[HideInInspector]
	public DateTime dateTime;

	[HideInInspector]
	public float internalHour;

	[HideInInspector]
	public float currentHour;

	[HideInInspector]
	public float currentDay;

	[HideInInspector]
	public float currentYear;

	[HideInInspector]
	public double currentTimeInHours;

	[HideInInspector]
	public RenderTexture cloudsRenderTarget;

	[HideInInspector]
	public RenderTexture flatCloudsRenderTarget;

	[HideInInspector]
	public Material flatCloudsMat;

	[HideInInspector]
	public RenderTexture weatherMap;

	[HideInInspector]
	public RenderTexture moonRenderTarget;

	[HideInInspector]
	public RenderTexture satRenderTarget;

	[HideInInspector]
	public float customMoonPhase;

	[HideInInspector]
	public bool updateFogDensity = true;

	[HideInInspector]
	public Color customFogColor = Color.black;

	[HideInInspector]
	public float customFogIntensity;

	[HideInInspector]
	public bool profileLoaded;

	[HideInInspector]
	public bool interiorMode;

	[HideInInspector]
	public EnviroInterior lastInteriorZone;

	[HideInInspector]
	public Vector2 cloudAnim;

	[HideInInspector]
	public Vector2 cloudAnimNonScaled;

	[HideInInspector]
	public Material skyMat;

	private Transform DomeTransform;

	private Transform SunTransform;

	private Light MainLight;

	private Transform MoonTransform;

	private Renderer MoonRenderer;

	private Material MoonShader;

	private float lastHourUpdate;

	private float starsRot;

	private float lastHour;

	private double lastRelfectionUpdate;

	private double lastMoonUpdate;

	private float lastAmbientSkyUpdate;

	private bool serverMode;

	private RenderTexture cloudShadowMap;

	private Material cloudShadowMat;

	private const float pi = (float)Math.PI;

	private Vector3 K = new Vector3(686f, 678f, 666f);

	private const float n = 1.0003f;

	private const float N = 2.545E+25f;

	private const float pn = 0.035f;

	private float hourTime;

	private float LST;

	private ParticleSystem lightningEffect;

	[HideInInspector]
	public bool showSettings;

	private Material weatherMapMat;

	public static EnviroSky instance
	{
		get
		{
			if (_instance == null)
			{
				_instance = UnityEngine.Object.FindObjectOfType<EnviroSky>();
			}
			return _instance;
		}
	}

	private float OrbitRadius => DomeTransform.localScale.x;

	public event HourPassed OnHourPassed;

	public event DayPassed OnDayPassed;

	public event YearPassed OnYearPassed;

	public event WeatherChanged OnWeatherChanged;

	public event ZoneWeatherChanged OnZoneWeatherChanged;

	public event SeasonChanged OnSeasonChanged;

	public event isNightE OnNightTime;

	public event isDay OnDayTime;

	public event ZoneChanged OnZoneChanged;

	public virtual void NotifyHourPassed()
	{
		if (this.OnHourPassed != null)
		{
			this.OnHourPassed();
		}
	}

	public virtual void NotifyDayPassed()
	{
		if (this.OnDayPassed != null)
		{
			this.OnDayPassed();
		}
	}

	public virtual void NotifyYearPassed()
	{
		if (this.OnYearPassed != null)
		{
			this.OnYearPassed();
		}
	}

	public virtual void NotifyWeatherChanged(EnviroWeatherPreset type)
	{
		if (this.OnWeatherChanged != null)
		{
			this.OnWeatherChanged(type);
		}
	}

	public virtual void NotifyZoneWeatherChanged(EnviroWeatherPreset type, EnviroZone zone)
	{
		if (this.OnZoneWeatherChanged != null)
		{
			this.OnZoneWeatherChanged(type, zone);
		}
	}

	public virtual void NotifySeasonChanged(EnviroSeasons.Seasons season)
	{
		if (this.OnSeasonChanged != null)
		{
			this.OnSeasonChanged(season);
		}
	}

	public virtual void NotifyIsNight()
	{
		if (this.OnNightTime != null)
		{
			this.OnNightTime();
		}
	}

	public virtual void NotifyIsDay()
	{
		if (this.OnDayTime != null)
		{
			this.OnDayTime();
		}
	}

	public virtual void NotifyZoneChanged(EnviroZone zone)
	{
		if (this.OnZoneChanged != null)
		{
			this.OnZoneChanged(zone);
		}
	}

	private void Start()
	{
		SetTime(GameTime.Years, GameTime.Days, GameTime.Hours, GameTime.Minutes, GameTime.Seconds);
		lastHourUpdate = Mathf.RoundToInt(internalHour);
		currentTimeInHours = GetInHours(internalHour, GameTime.Days, GameTime.Years);
		Weather.weatherFullyChanged = false;
		thunder = 0f;
		lastCloudsQuality = cloudsSettings.cloudsQuality;
		if (Application.isPlaying && dontDestroy)
		{
			UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
		}
		if (weatherMapMat == null)
		{
			weatherMapMat = new Material(Shader.Find("Enviro/WeatherMap"));
		}
		if (profileLoaded)
		{
			InvokeRepeating("UpdateEnviroment", 0f, qualitySettings.UpdateInterval);
			CreateEffects();
			if (PlayerCamera != null && Player != null && !AssignInRuntime && profile != null)
			{
				Init();
			}
		}
	}

	private void OnEnable()
	{
		DomeTransform = base.transform;
		Weather.currentActiveWeatherPreset = Weather.zones[0].currentActiveZoneWeatherPreset;
		Weather.lastActiveWeatherPreset = Weather.currentActiveWeatherPreset;
		if (profile == null)
		{
			Debug.LogError("No profile assigned!");
			return;
		}
		if (!profileLoaded)
		{
			ApplyProfile(profile);
		}
		PreInit();
		if (AssignInRuntime)
		{
			started = false;
		}
		else if (PlayerCamera != null && Player != null)
		{
			Init();
		}
	}

	public void ApplyProfile(EnviroProfile p)
	{
		profile = p;
		lightSettings = JsonUtility.FromJson<EnviroLightSettings>(JsonUtility.ToJson(p.lightSettings));
		volumeLightSettings = JsonUtility.FromJson<EnviroVolumeLightingSettings>(JsonUtility.ToJson(p.volumeLightSettings));
		skySettings = JsonUtility.FromJson<EnviroSkySettings>(JsonUtility.ToJson(p.skySettings));
		cloudsSettings = JsonUtility.FromJson<EnviroCloudSettings>(JsonUtility.ToJson(p.cloudsSettings));
		weatherSettings = JsonUtility.FromJson<EnviroWeatherSettings>(JsonUtility.ToJson(p.weatherSettings));
		fogSettings = JsonUtility.FromJson<EnviroFogSettings>(JsonUtility.ToJson(p.fogSettings));
		lightshaftsSettings = JsonUtility.FromJson<EnviroLightShaftsSettings>(JsonUtility.ToJson(p.lightshaftsSettings));
		audioSettings = JsonUtility.FromJson<EnviroAudioSettings>(JsonUtility.ToJson(p.audioSettings));
		satelliteSettings = JsonUtility.FromJson<EnviroSatellitesSettings>(JsonUtility.ToJson(p.satelliteSettings));
		qualitySettings = JsonUtility.FromJson<EnviroQualitySettings>(JsonUtility.ToJson(p.qualitySettings));
		seasonsSettings = JsonUtility.FromJson<EnviroSeasonSettings>(JsonUtility.ToJson(p.seasonsSettings));
		profileLoaded = true;
	}

	public void SaveProfile()
	{
		profile.lightSettings = JsonUtility.FromJson<EnviroLightSettings>(JsonUtility.ToJson(lightSettings));
		profile.volumeLightSettings = JsonUtility.FromJson<EnviroVolumeLightingSettings>(JsonUtility.ToJson(volumeLightSettings));
		profile.skySettings = JsonUtility.FromJson<EnviroSkySettings>(JsonUtility.ToJson(skySettings));
		profile.cloudsSettings = JsonUtility.FromJson<EnviroCloudSettings>(JsonUtility.ToJson(cloudsSettings));
		profile.weatherSettings = JsonUtility.FromJson<EnviroWeatherSettings>(JsonUtility.ToJson(weatherSettings));
		profile.fogSettings = JsonUtility.FromJson<EnviroFogSettings>(JsonUtility.ToJson(fogSettings));
		profile.lightshaftsSettings = JsonUtility.FromJson<EnviroLightShaftsSettings>(JsonUtility.ToJson(lightshaftsSettings));
		profile.audioSettings = JsonUtility.FromJson<EnviroAudioSettings>(JsonUtility.ToJson(audioSettings));
		profile.satelliteSettings = JsonUtility.FromJson<EnviroSatellitesSettings>(JsonUtility.ToJson(satelliteSettings));
		profile.qualitySettings = JsonUtility.FromJson<EnviroQualitySettings>(JsonUtility.ToJson(qualitySettings));
		profile.seasonsSettings = JsonUtility.FromJson<EnviroSeasonSettings>(JsonUtility.ToJson(seasonsSettings));
	}

	public void ReInit()
	{
		OnEnable();
	}

	private void PreInit()
	{
		if (GameTime.solarTime < 0.45f)
		{
			isNight = true;
		}
		else
		{
			isNight = false;
		}
		if (serverMode)
		{
			return;
		}
		CheckSatellites();
		RenderSettings.fogMode = fogSettings.Fogmode;
		SetupSkybox();
		RenderSettings.ambientMode = lightSettings.ambientMode;
		RenderSettings.fogDensity = 0f;
		RenderSettings.fogStartDistance = 0f;
		RenderSettings.fogEndDistance = 1000f;
		Components.GlobalReflectionProbe.size = base.transform.localScale;
		Components.GlobalReflectionProbe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
		if ((bool)Components.Sun)
		{
			SunTransform = Components.Sun.transform;
		}
		else
		{
			Debug.LogError("Please set sun object in inspector!");
		}
		if ((bool)Components.Moon)
		{
			MoonTransform = Components.Moon.transform;
			MoonRenderer = Components.Moon.GetComponent<Renderer>();
			if (MoonRenderer == null)
			{
				MoonRenderer = Components.Moon.AddComponent<MeshRenderer>();
			}
			MoonRenderer.shadowCastingMode = ShadowCastingMode.Off;
			MoonRenderer.receiveShadows = false;
			if (MoonRenderer.sharedMaterial != null)
			{
				UnityEngine.Object.DestroyImmediate(MoonRenderer.sharedMaterial);
			}
			if (skySettings.moonPhaseMode == EnviroSkySettings.MoonPhases.Realistic)
			{
				MoonShader = new Material(Shader.Find("Enviro/MoonShader"));
			}
			else
			{
				MoonShader = new Material(Shader.Find("Enviro/MoonShaderPhased"));
			}
			MoonShader.SetTexture("_MainTex", skySettings.moonTexture);
			MoonRenderer.sharedMaterial = MoonShader;
			customMoonPhase = skySettings.startMoonPhase;
		}
		else
		{
			Debug.LogError("Please set moon object in inspector!");
		}
		if (Components.cloudsShadowPlane != null)
		{
			UnityEngine.Object.DestroyImmediate(Components.cloudsShadowPlane);
		}
		if (weatherMap == null)
		{
			weatherMap = new RenderTexture(1024, 1024, 0, RenderTextureFormat.Default);
			weatherMap.wrapMode = TextureWrapMode.Repeat;
		}
		if (cloudShadows != null && weatherMap != null)
		{
			cloudShadows.SetTexture("_MainTex", weatherMap);
		}
		if ((bool)Components.DirectLight)
		{
			if (Components.DirectLight.name == "Direct Lght")
			{
				UnityEngine.Object.DestroyImmediate(Components.DirectLight.gameObject);
				Components.DirectLight = CreateDirectionalLight();
			}
			MainLight = Components.DirectLight.GetComponent<Light>();
			if (directVolumeLight == null)
			{
				directVolumeLight = Components.DirectLight.GetComponent<EnviroVolumeLight>();
			}
			if (directVolumeLight == null)
			{
				directVolumeLight = Components.DirectLight.gameObject.AddComponent<EnviroVolumeLight>();
			}
			if (dontDestroy && Application.isPlaying)
			{
				UnityEngine.Object.DontDestroyOnLoad(Components.DirectLight);
			}
		}
		else
		{
			GameObject gameObject = GameObject.Find("Enviro Directional Light");
			if (gameObject != null)
			{
				Components.DirectLight = gameObject.transform;
			}
			else
			{
				Components.DirectLight = CreateDirectionalLight();
			}
			MainLight = Components.DirectLight.GetComponent<Light>();
			if (directVolumeLight == null)
			{
				directVolumeLight = Components.DirectLight.GetComponent<EnviroVolumeLight>();
			}
			if (directVolumeLight == null)
			{
				directVolumeLight = Components.DirectLight.gameObject.AddComponent<EnviroVolumeLight>();
			}
			if (dontDestroy && Application.isPlaying)
			{
				UnityEngine.Object.DontDestroyOnLoad(Components.DirectLight);
			}
		}
		if (cloudShadowMap != null)
		{
			UnityEngine.Object.DestroyImmediate(cloudShadowMap);
		}
		cloudShadowMap = new RenderTexture(2048, 2048, 0, RenderTextureFormat.Default);
		cloudShadowMap.wrapMode = TextureWrapMode.Repeat;
		if (cloudShadowMat != null)
		{
			UnityEngine.Object.DestroyImmediate(cloudShadowMat);
		}
		cloudShadowMat = new Material(Shader.Find("Enviro/ShadowCookie"));
		if (cloudsSettings.shadowIntensity > 0f)
		{
			Graphics.Blit(weatherMap, cloudShadowMap, cloudShadowMat);
			MainLight.cookie = cloudShadowMap;
			MainLight.cookieSize = 10000f;
		}
		else
		{
			MainLight.cookie = null;
		}
	}

	private void SetupSkybox()
	{
		if (skySettings.skyboxMode == EnviroSkySettings.SkyboxModi.Default)
		{
			if (skyMat != null)
			{
				UnityEngine.Object.DestroyImmediate(skyMat);
			}
			if (cloudsMode == EnviroCloudsMode.None || cloudsMode == EnviroCloudsMode.Volume)
			{
				skyMat = new Material(Shader.Find("Enviro/Skybox"));
			}
			else
			{
				skyMat = new Material(Shader.Find("Enviro/SkyboxFlatClouds"));
			}
			if (skySettings.starsCubeMap != null)
			{
				skyMat.SetTexture("_Stars", skySettings.starsCubeMap);
			}
			if (skySettings.galaxyCubeMap != null)
			{
				skyMat.SetTexture("_Galaxy", skySettings.galaxyCubeMap);
			}
			RenderSettings.skybox = skyMat;
		}
		else if (skySettings.skyboxMode == EnviroSkySettings.SkyboxModi.CustomSkybox)
		{
			if (skySettings.customSkyboxMaterial != null)
			{
				RenderSettings.skybox = skySettings.customSkyboxMaterial;
			}
			skyMat = skySettings.customSkyboxMaterial;
		}
		lastCloudsMode = cloudsMode;
		if (lightSettings.ambientMode == AmbientMode.Skybox)
		{
			StartCoroutine(UpdateAmbientLightWithDelay());
		}
	}

	private IEnumerator UpdateAmbientLightWithDelay()
	{
		yield return 0;
		DynamicGI.UpdateEnvironment();
	}

	private void Init()
	{
		if (profile == null)
		{
			return;
		}
		if (serverMode)
		{
			started = true;
			return;
		}
		InitImageEffects();
		if (PlayerCamera != null)
		{
			if (setCameraClearFlags)
			{
				PlayerCamera.clearFlags = CameraClearFlags.Skybox;
			}
			if (PlayerCamera.actualRenderingPath == RenderingPath.DeferredShading)
			{
				SetCameraHDR(PlayerCamera, hdr: true);
			}
			else
			{
				SetCameraHDR(PlayerCamera, HDR);
			}
			Components.GlobalReflectionProbe.farClipPlane = PlayerCamera.farClipPlane;
			if (moonCamera == null)
			{
				CreateMoonCamera();
			}
			else
			{
				moonCamera.cullingMask = 1 << moonRenderingLayer;
				moonCamera.farClipPlane = PlayerCamera.farClipPlane * 0.5f;
				Camera[] array = UnityEngine.Object.FindObjectsOfType<Camera>();
				for (int i = 0; i < array.Length; i++)
				{
					if (array[i] != moonCamera)
					{
						array[i].cullingMask &= ~(1 << moonRenderingLayer);
					}
				}
				moonCamera.transform.SetParent(Components.Moon.transform, worldPositionStays: false);
				moonCamera.transform.localPosition = new Vector3(0f, 0f, 1.5f);
				moonCamera.transform.localEulerAngles = new Vector3(-180f, 0f, 45f);
				moonCamera.transform.localScale = Vector3.one;
				moonCamera.enabled = false;
			}
		}
		CreateMoonTexture();
		Components.Moon.layer = moonRenderingLayer;
		UnityEngine.Object.DestroyImmediate(GameObject.Find("Enviro Cameras"));
		if (satelliteSettings.additionalSatellites.Count > 0)
		{
			InitSatCamera();
		}
		started = true;
		if (MoonShader != null)
		{
			MoonShader.SetFloat("_Phase", customMoonPhase);
			MoonShader.SetColor("_Color", skySettings.moonColor);
			MoonShader.SetFloat("_Brightness", skySettings.moonBrightness * (1f - GameTime.solarTime));
		}
		if (moonCamera != null)
		{
			moonCamera.Render();
		}
	}

	public void SetCameraHDR(Camera cam, bool hdr)
	{
		cam.allowHDR = hdr;
	}

	public bool GetCameraHDR(Camera cam)
	{
		return cam.allowHDR;
	}

	private Transform CreateDirectionalLight()
	{
		GameObject gameObject = new GameObject();
		gameObject.name = "Enviro Directional Light";
		gameObject.transform.parent = base.transform;
		gameObject.transform.parent = null;
		Light light = gameObject.AddComponent<Light>();
		light.type = LightType.Directional;
		light.shadows = LightShadows.Soft;
		return gameObject.transform;
	}

	private void InitImageEffects()
	{
		EnviroSkyRender = PlayerCamera.gameObject.GetComponent<EnviroSkyRendering>();
		if (EnviroSkyRender == null)
		{
			EnviroSkyRender = PlayerCamera.gameObject.AddComponent<EnviroSkyRendering>();
		}
		EnviroLightShafts[] components = PlayerCamera.gameObject.GetComponents<EnviroLightShafts>();
		if (components.Length > 0)
		{
			lightShaftsScriptSun = components[0];
		}
		if (lightShaftsScriptSun != null)
		{
			UnityEngine.Object.DestroyImmediate(lightShaftsScriptSun.sunShaftsMaterial);
			UnityEngine.Object.DestroyImmediate(lightShaftsScriptSun.simpleClearMaterial);
			lightShaftsScriptSun.sunShaftsMaterial = new Material(Shader.Find("Enviro/Effects/LightShafts"));
			lightShaftsScriptSun.sunShaftsShader = lightShaftsScriptSun.sunShaftsMaterial.shader;
			lightShaftsScriptSun.simpleClearMaterial = new Material(Shader.Find("Enviro/Effects/ClearLightShafts"));
			lightShaftsScriptSun.simpleClearShader = lightShaftsScriptSun.simpleClearMaterial.shader;
		}
		else
		{
			lightShaftsScriptSun = PlayerCamera.gameObject.AddComponent<EnviroLightShafts>();
			lightShaftsScriptSun.sunShaftsMaterial = new Material(Shader.Find("Enviro/Effects/LightShafts"));
			lightShaftsScriptSun.sunShaftsShader = lightShaftsScriptSun.sunShaftsMaterial.shader;
			lightShaftsScriptSun.simpleClearMaterial = new Material(Shader.Find("Enviro/Effects/ClearLightShafts"));
			lightShaftsScriptSun.simpleClearShader = lightShaftsScriptSun.simpleClearMaterial.shader;
		}
		if (components.Length > 1)
		{
			lightShaftsScriptMoon = components[1];
		}
		if (lightShaftsScriptMoon != null)
		{
			UnityEngine.Object.DestroyImmediate(lightShaftsScriptMoon.sunShaftsMaterial);
			UnityEngine.Object.DestroyImmediate(lightShaftsScriptMoon.simpleClearMaterial);
			lightShaftsScriptMoon.sunShaftsMaterial = new Material(Shader.Find("Enviro/Effects/LightShafts"));
			lightShaftsScriptMoon.sunShaftsShader = lightShaftsScriptMoon.sunShaftsMaterial.shader;
			lightShaftsScriptMoon.simpleClearMaterial = new Material(Shader.Find("Enviro/Effects/ClearLightShafts"));
			lightShaftsScriptMoon.simpleClearShader = lightShaftsScriptMoon.simpleClearMaterial.shader;
		}
		else
		{
			lightShaftsScriptMoon = PlayerCamera.gameObject.AddComponent<EnviroLightShafts>();
			lightShaftsScriptMoon.sunShaftsMaterial = new Material(Shader.Find("Enviro/Effects/LightShafts"));
			lightShaftsScriptMoon.sunShaftsShader = lightShaftsScriptMoon.sunShaftsMaterial.shader;
			lightShaftsScriptMoon.simpleClearMaterial = new Material(Shader.Find("Enviro/Effects/ClearLightShafts"));
			lightShaftsScriptMoon.simpleClearShader = lightShaftsScriptMoon.simpleClearMaterial.shader;
		}
	}

	public void InitSatCamera()
	{
		Camera[] array = UnityEngine.Object.FindObjectsOfType<Camera>();
		for (int i = 0; i < array.Length; i++)
		{
			array[i].cullingMask &= ~(1 << satelliteRenderingLayer);
		}
		UnityEngine.Object.DestroyImmediate(GameObject.Find("Enviro Sat Camera"));
		GameObject gameObject = new GameObject();
		gameObject.name = "Enviro Sat Camera";
		gameObject.transform.position = PlayerCamera.transform.position;
		gameObject.transform.rotation = PlayerCamera.transform.rotation;
		gameObject.hideFlags = HideFlags.DontSave;
		satCamera = gameObject.AddComponent<Camera>();
		satCamera.farClipPlane = PlayerCamera.farClipPlane;
		satCamera.nearClipPlane = PlayerCamera.nearClipPlane;
		satCamera.aspect = PlayerCamera.aspect;
		SetCameraHDR(satCamera, HDR);
		satCamera.useOcclusionCulling = false;
		satCamera.renderingPath = RenderingPath.Forward;
		satCamera.fieldOfView = PlayerCamera.fieldOfView;
		satCamera.clearFlags = CameraClearFlags.Color;
		satCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
		satCamera.cullingMask = 1 << satelliteRenderingLayer;
		satCamera.depth = PlayerCamera.depth + 1f;
		satCamera.enabled = true;
		PlayerCamera.cullingMask &= ~(1 << satelliteRenderingLayer);
		RenderTextureFormat format = ((!GetCameraHDR(satCamera)) ? RenderTextureFormat.Default : RenderTextureFormat.DefaultHDR);
		satRenderTarget = new RenderTexture(Screen.currentResolution.width, Screen.currentResolution.height, 16, format);
		satCamera.targetTexture = satRenderTarget;
		satCamera.enabled = false;
	}

	private void CreateMoonCamera()
	{
		Camera[] array = UnityEngine.Object.FindObjectsOfType<Camera>();
		for (int i = 0; i < array.Length; i++)
		{
			array[i].cullingMask &= ~(1 << moonRenderingLayer);
		}
		UnityEngine.Object.DestroyImmediate(GameObject.Find("Enviro Moon Render Cam"));
		GameObject gameObject = new GameObject();
		gameObject.name = "Enviro Moon Render Cam";
		gameObject.transform.SetParent(Components.Moon.transform, worldPositionStays: false);
		gameObject.transform.localPosition = new Vector3(0f, 0f, 1.5f);
		gameObject.transform.localEulerAngles = new Vector3(-180f, 0f, 45f);
		gameObject.transform.localScale = Vector3.one;
		moonCamera = gameObject.AddComponent<Camera>();
		moonCamera.farClipPlane = PlayerCamera.farClipPlane * 0.5f;
		moonCamera.nearClipPlane = 0.01f;
		moonCamera.aspect = PlayerCamera.aspect;
		SetCameraHDR(moonCamera, HDR);
		moonCamera.renderingPath = RenderingPath.Forward;
		moonCamera.fieldOfView = PlayerCamera.fieldOfView;
		moonCamera.clearFlags = CameraClearFlags.Color;
		moonCamera.backgroundColor = Color.black;
		moonCamera.cullingMask = 1 << moonRenderingLayer;
		PlayerCamera.cullingMask &= ~(1 << moonRenderingLayer);
		moonCamera.enabled = false;
	}

	private void RenderMoon()
	{
		if (currentTimeInHours > lastMoonUpdate + 0.1 || (currentTimeInHours < lastMoonUpdate - 0.1 && skySettings.renderMoon))
		{
			moonCamera.Render();
			lastMoonUpdate = currentTimeInHours;
		}
	}

	private void CreateMoonTexture()
	{
		if (moonRenderTarget != null && moonCamera != null)
		{
			moonCamera.targetTexture = null;
			UnityEngine.Object.DestroyImmediate(moonRenderTarget);
		}
		RenderTextureFormat format = ((!GetCameraHDR(moonCamera)) ? RenderTextureFormat.Default : RenderTextureFormat.DefaultHDR);
		moonRenderTarget = new RenderTexture(512, 512, 0, format);
		moonCamera.targetTexture = moonRenderTarget;
	}

	public void CreateEffects()
	{
		EffectsHolder = GameObject.Find("Enviro Effects");
		if (EffectsHolder != null)
		{
			int childCount = EffectsHolder.transform.childCount;
			int num = childCount - 1;
			while (0 <= num)
			{
				UnityEngine.Object.DestroyImmediate(EffectsHolder.transform.GetChild(num).gameObject);
				num--;
			}
		}
		else
		{
			EffectsHolder = new GameObject("Enviro Effects");
			EffectsHolder.transform.parent = base.transform;
			EffectsHolder.transform.parent = null;
		}
		CreateWeatherEffectHolder();
		if (Application.isPlaying && dontDestroy)
		{
			UnityEngine.Object.DontDestroyOnLoad(EffectsHolder);
		}
		SetEffectsHolderPlace(EffectsHolder);
	}

	public int RegisterMe(EnviroVegetationInstance me)
	{
		EnviroVegetationInstances.Add(me);
		return EnviroVegetationInstances.Count - 1;
	}

	public void ChangeSeason(EnviroSeasons.Seasons season)
	{
		Seasons.currentSeasons = season;
		NotifySeasonChanged(season);
	}

	private void UpdateSeason()
	{
		if (currentDay >= 0f && currentDay < seasonsSettings.SpringInDays)
		{
			Seasons.currentSeasons = EnviroSeasons.Seasons.Spring;
			if (Seasons.lastSeason != Seasons.currentSeasons)
			{
				NotifySeasonChanged(EnviroSeasons.Seasons.Spring);
			}
			Seasons.lastSeason = Seasons.currentSeasons;
		}
		else if (currentDay >= seasonsSettings.SpringInDays && currentDay < seasonsSettings.SpringInDays + seasonsSettings.SummerInDays)
		{
			Seasons.currentSeasons = EnviroSeasons.Seasons.Summer;
			if (Seasons.lastSeason != Seasons.currentSeasons)
			{
				NotifySeasonChanged(EnviroSeasons.Seasons.Summer);
			}
			Seasons.lastSeason = Seasons.currentSeasons;
		}
		else if (currentDay >= seasonsSettings.SpringInDays + seasonsSettings.SummerInDays && currentDay < seasonsSettings.SpringInDays + seasonsSettings.SummerInDays + seasonsSettings.AutumnInDays)
		{
			Seasons.currentSeasons = EnviroSeasons.Seasons.Autumn;
			if (Seasons.lastSeason != Seasons.currentSeasons)
			{
				NotifySeasonChanged(EnviroSeasons.Seasons.Autumn);
			}
			Seasons.lastSeason = Seasons.currentSeasons;
		}
		else if (currentDay >= seasonsSettings.SpringInDays + seasonsSettings.SummerInDays + seasonsSettings.AutumnInDays && currentDay <= seasonsSettings.SpringInDays + seasonsSettings.SummerInDays + seasonsSettings.AutumnInDays + seasonsSettings.WinterInDays)
		{
			Seasons.currentSeasons = EnviroSeasons.Seasons.Winter;
			if (Seasons.lastSeason != Seasons.currentSeasons)
			{
				NotifySeasonChanged(EnviroSeasons.Seasons.Winter);
			}
			Seasons.lastSeason = Seasons.currentSeasons;
		}
	}

	private void UpdateEnviroment()
	{
		if (Seasons.calcSeasons)
		{
			UpdateSeason();
		}
		if (EnviroVegetationInstances.Count <= 0)
		{
			return;
		}
		for (int i = 0; i < EnviroVegetationInstances.Count; i++)
		{
			if (EnviroVegetationInstances[i] != null)
			{
				EnviroVegetationInstances[i].UpdateInstance();
			}
		}
	}

	private void CreateSatellite(int id)
	{
		if (satelliteSettings.additionalSatellites[id].prefab == null)
		{
			Debug.Log("Satellite without prefab! Pleae assign a prefab to all satellites.");
			return;
		}
		GameObject gameObject = new GameObject();
		gameObject.name = satelliteSettings.additionalSatellites[id].name;
		gameObject.transform.parent = Components.satellites;
		satellitesRotation.Add(gameObject);
		GameObject gameObject2 = UnityEngine.Object.Instantiate(satelliteSettings.additionalSatellites[id].prefab, gameObject.transform);
		gameObject2.layer = satelliteRenderingLayer;
		satellites.Add(gameObject2);
	}

	public void CheckSatellites()
	{
		satellites = new List<GameObject>();
		int childCount = Components.satellites.childCount;
		for (int num = childCount - 1; num >= 0; num--)
		{
			UnityEngine.Object.DestroyImmediate(Components.satellites.GetChild(num).gameObject);
		}
		satellites.Clear();
		satellitesRotation.Clear();
		for (int i = 0; i < satelliteSettings.additionalSatellites.Count; i++)
		{
			CreateSatellite(i);
		}
	}

	private void CalculateSatPositions(float siderealTime)
	{
		for (int i = 0; i < satelliteSettings.additionalSatellites.Count; i++)
		{
			Quaternion localRotation = Quaternion.Euler(90f - GameTime.Latitude, GameTime.Longitude, 0f);
			localRotation *= Quaternion.Euler(satelliteSettings.additionalSatellites[i].yRot, siderealTime, satelliteSettings.additionalSatellites[i].xRot);
			if (satellites.Count >= i)
			{
				satellites[i].transform.localPosition = new Vector3(0f, satelliteSettings.additionalSatellites[i].orbit, 0f);
			}
			if (satellitesRotation.Count >= i)
			{
				satellitesRotation[i].transform.localRotation = localRotation;
			}
		}
	}

	private void UpdateCameraComponents()
	{
		if (EnviroSkyRender != null)
		{
			EnviroSkyRender.volumeLighting = volumeLighting;
			EnviroSkyRender.dirVolumeLighting = volumeLightSettings.dirVolumeLighting;
			EnviroSkyRender.simpleFog = fogSettings.useSimpleFog;
			EnviroSkyRender.distanceFog = fogSettings.distanceFog;
			EnviroSkyRender.heightFog = fogSettings.heightFog;
			EnviroSkyRender.height = fogSettings.height;
			EnviroSkyRender.heightDensity = fogSettings.heightDensity;
			EnviroSkyRender.useRadialDistance = fogSettings.useRadialDistance;
			EnviroSkyRender.startDistance = fogSettings.startDistance;
		}
		if (lightShaftsScriptSun != null)
		{
			lightShaftsScriptSun.resolution = lightshaftsSettings.resolution;
			lightShaftsScriptSun.screenBlendMode = lightshaftsSettings.screenBlendMode;
			lightShaftsScriptSun.useDepthTexture = lightshaftsSettings.useDepthTexture;
			lightShaftsScriptSun.sunThreshold = lightshaftsSettings.thresholdColorSun.Evaluate(GameTime.solarTime);
			lightShaftsScriptSun.sunShaftBlurRadius = lightshaftsSettings.blurRadius;
			lightShaftsScriptSun.sunShaftIntensity = lightshaftsSettings.intensity;
			lightShaftsScriptSun.maxRadius = lightshaftsSettings.maxRadius;
			lightShaftsScriptSun.sunColor = lightshaftsSettings.lightShaftsColorSun.Evaluate(GameTime.solarTime);
			lightShaftsScriptSun.sunTransform = Components.Sun.transform;
			if (LightShafts.sunLightShafts)
			{
				lightShaftsScriptSun.enabled = true;
			}
			else
			{
				lightShaftsScriptSun.enabled = false;
			}
		}
		if (lightShaftsScriptMoon != null)
		{
			lightShaftsScriptMoon.resolution = lightshaftsSettings.resolution;
			lightShaftsScriptMoon.screenBlendMode = lightshaftsSettings.screenBlendMode;
			lightShaftsScriptMoon.useDepthTexture = lightshaftsSettings.useDepthTexture;
			lightShaftsScriptMoon.sunThreshold = lightshaftsSettings.thresholdColorMoon.Evaluate(GameTime.lunarTime);
			lightShaftsScriptMoon.sunShaftBlurRadius = lightshaftsSettings.blurRadius;
			lightShaftsScriptMoon.sunShaftIntensity = Mathf.Clamp(lightshaftsSettings.intensity - GameTime.solarTime, 0f, 100f);
			lightShaftsScriptMoon.maxRadius = lightshaftsSettings.maxRadius;
			lightShaftsScriptMoon.sunColor = lightshaftsSettings.lightShaftsColorMoon.Evaluate(GameTime.lunarTime);
			lightShaftsScriptMoon.sunTransform = Components.Moon.transform;
			if (LightShafts.moonLightShafts)
			{
				lightShaftsScriptMoon.enabled = true;
			}
			else
			{
				lightShaftsScriptMoon.enabled = false;
			}
		}
	}

	private Vector3 CalculatePosition()
	{
		Vector3 result = default(Vector3);
		result.x = Player.transform.position.x;
		result.z = Player.transform.position.z;
		result.y = Player.transform.position.y;
		return result;
	}

	private void RenderFlatCloudsMap()
	{
		if (flatCloudsMat == null)
		{
			flatCloudsMat = new Material(Shader.Find("Enviro/FlatCloudMap"));
		}
		flatCloudsRenderTarget = RenderTexture.GetTemporary(512 * (int)(cloudsSettings.flatCloudsResolution + 1), 512 * (int)(cloudsSettings.flatCloudsResolution + 1), 0, RenderTextureFormat.DefaultHDR);
		flatCloudsRenderTarget.wrapMode = TextureWrapMode.Repeat;
		flatCloudsMat.SetVector("_CloudAnimation", cloudAnimNonScaled);
		flatCloudsMat.SetTexture("_NoiseTex", cloudsSettings.flatCloudsNoiseTexture);
		flatCloudsMat.SetFloat("_CloudScale", cloudsSettings.flatCloudsScale);
		flatCloudsMat.SetFloat("_Coverage", cloudsConfig.flatCoverage);
		flatCloudsMat.SetInt("noiseOctaves", cloudsSettings.flatCloudsNoiseOctaves);
		flatCloudsMat.SetFloat("_Softness", cloudsConfig.flatSoftness);
		flatCloudsMat.SetFloat("_Brightness", cloudsConfig.flatBrightness);
		flatCloudsMat.SetFloat("_MorphingSpeed", cloudsSettings.flatCloudsMorphingSpeed);
		Graphics.Blit(null, flatCloudsRenderTarget, flatCloudsMat);
		RenderTexture.ReleaseTemporary(flatCloudsRenderTarget);
	}

	private void RenderWeatherMap()
	{
		if (cloudsSettings.customWeatherMap == null)
		{
			weatherMapMat.SetVector("_WindDir", cloudAnimNonScaled);
			weatherMapMat.SetFloat("_AnimSpeedScale", cloudsSettings.weatherAnimSpeedScale);
			weatherMapMat.SetInt("_Tiling", cloudsSettings.weatherMapTiling);
			weatherMapMat.SetVector("_Location", cloudsSettings.locationOffset);
			double value = instance.cloudsConfig.coverage * cloudsSettings.globalCloudCoverage;
			weatherMapMat.SetFloat("_Coverage", (float)Math.Round(value, 4));
			Graphics.Blit(null, weatherMap, weatherMapMat);
		}
	}

	private void RenderCloudMaps()
	{
		switch (cloudsMode)
		{
		case EnviroCloudsMode.None:
			break;
		case EnviroCloudsMode.Flat:
			RenderFlatCloudsMap();
			RenderWeatherMap();
			break;
		case EnviroCloudsMode.Volume:
			RenderWeatherMap();
			break;
		case EnviroCloudsMode.Both:
			RenderFlatCloudsMap();
			RenderWeatherMap();
			break;
		}
	}

	private void Update()
	{
		if (profile == null)
		{
			Debug.Log("No profile applied! Please create and assign a profile.");
			return;
		}
		if (!started && !serverMode)
		{
			UpdateTime();
			UpdateSunAndMoonPosition();
			CalculateDirectLight();
			UpdateAmbientLight();
			UpdateReflections();
			RenderMoon();
			if (!AssignInRuntime || !(PlayerTag != string.Empty) || !(CameraTag != string.Empty) || !Application.isPlaying)
			{
				started = false;
				return;
			}
			GameObject gameObject = GameObject.FindGameObjectWithTag(PlayerTag);
			if (gameObject != null)
			{
				Player = gameObject;
			}
			for (int i = 0; i < Camera.allCameras.Length; i++)
			{
				if (Camera.allCameras[i].tag == CameraTag)
				{
					PlayerCamera = Camera.allCameras[i];
				}
			}
			if (!(Player != null) || !(PlayerCamera != null))
			{
				started = false;
				return;
			}
			Init();
			started = true;
		}
		UpdateTime();
		ValidateParameters();
		if (!serverMode)
		{
			if (lastCloudsMode != cloudsMode)
			{
				SetupSkybox();
			}
			RenderCloudMaps();
			UpdateCameraComponents();
			UpdateAmbientLight();
			UpdateReflections();
			UpdateWeather();
			UpdateCloudShadows();
			UpdateSkyRenderingComponent();
			RenderMoon();
			UpdateSunAndMoonPosition();
			CalculateDirectLight();
			CalculateSatPositions(LST);
			if (!isNight && GameTime.solarTime < 0.45f)
			{
				isNight = true;
				NotifyIsNight();
			}
			else if (isNight && GameTime.solarTime >= 0.45f)
			{
				isNight = false;
				NotifyIsDay();
			}
			if (lastCloudsQuality != cloudsSettings.cloudsQuality && (cloudsMode == EnviroCloudsMode.Volume || cloudsMode == EnviroCloudsMode.Both))
			{
				ChangeCloudsQuality(cloudsSettings.cloudsQuality);
			}
		}
		else
		{
			UpdateWeather();
			if (!isNight && GameTime.solarTime < 0.45f)
			{
				isNight = true;
				NotifyIsNight();
			}
			else if (isNight && GameTime.solarTime >= 0.45f)
			{
				isNight = false;
				NotifyIsDay();
			}
		}
	}

	private void LateUpdate()
	{
		if (!serverMode && PlayerCamera != null && Player != null)
		{
			base.transform.position = Player.transform.position;
			base.transform.localScale = new Vector3(PlayerCamera.farClipPlane, PlayerCamera.farClipPlane, PlayerCamera.farClipPlane);
			SetEffectsHolderPlace(EffectsHolder);
		}
	}

	private void SetEffectsHolderPlace(GameObject obj)
	{
		if (!(obj == null))
		{
			if (Player == null)
			{
				obj.transform.SetPositionAndRotation(base.transform.position, base.transform.rotation);
				return;
			}
			if (!Singleton<Manager.Resources>.IsInstance() || Player == PlayerCamera)
			{
				obj.transform.SetPositionAndRotation(Player.transform.position, Player.transform.rotation);
				return;
			}
			Vector3 enviroEffectOffset = Singleton<Manager.Resources>.Instance.LocomotionProfile.EnviroEffectOffset;
			Vector3 position = Player.transform.position;
			Vector3 position2 = PlayerCamera.transform.position;
			position.y = (position2.y = 0f);
			float y = Vector3.SignedAngle(Vector3.forward, position - position2, Vector3.up);
			Quaternion quaternion = Quaternion.Euler(0f, y, 0f);
			obj.transform.SetPositionAndRotation(Player.transform.position + quaternion * enviroEffectOffset, quaternion);
		}
	}

	private void UpdateCloudShadows()
	{
		if (cloudsSettings.shadowIntensity == 0f || cloudsMode == EnviroCloudsMode.None || cloudsMode == EnviroCloudsMode.Flat)
		{
			if (MainLight.cookie != null)
			{
				MainLight.cookie = null;
			}
		}
		else if (cloudsSettings.shadowIntensity > 0f)
		{
			cloudShadowMap.DiscardContents(discardColor: true, discardDepth: true);
			cloudShadowMat.SetFloat("_shadowIntensity", cloudsSettings.shadowIntensity);
			if (cloudsMode == EnviroCloudsMode.Volume || cloudsMode == EnviroCloudsMode.Both)
			{
				cloudShadowMat.SetTexture("_MainTex", weatherMap);
				Graphics.Blit(weatherMap, cloudShadowMap, cloudShadowMat);
			}
			if (Application.isPlaying)
			{
				MainLight.cookie = cloudShadowMap;
			}
			else
			{
				MainLight.cookie = null;
			}
			MainLight.cookieSize = cloudsSettings.shadowCookieSize;
		}
	}

	public Vector3 BetaRay()
	{
		Vector3 vector = skySettings.waveLength * 1E-09f;
		Vector3 result = default(Vector3);
		result.x = 8f * Mathf.Pow((float)Math.PI, 3f) * Mathf.Pow(Mathf.Pow(1.0003f, 2f) - 1f, 2f) * 6.105f / (7.635E+25f * Mathf.Pow(vector.x, 4f) * 5.755f) * 2000f;
		result.y = 8f * Mathf.Pow((float)Math.PI, 3f) * Mathf.Pow(Mathf.Pow(1.0003f, 2f) - 1f, 2f) * 6.105f / (7.635E+25f * Mathf.Pow(vector.y, 4f) * 5.755f) * 2000f;
		result.z = 8f * Mathf.Pow((float)Math.PI, 3f) * Mathf.Pow(Mathf.Pow(1.0003f, 2f) - 1f, 2f) * 6.105f / (7.635E+25f * Mathf.Pow(vector.z, 4f) * 5.755f) * 2000f;
		return result;
	}

	public Vector3 BetaMie()
	{
		float num = 0.2f * skySettings.turbidity * 10f;
		Vector3 result = default(Vector3);
		result.x = 434f * num * (float)Math.PI * Mathf.Pow((float)Math.PI * 2f / skySettings.waveLength.x, 2f) * K.x;
		result.y = 434f * num * (float)Math.PI * Mathf.Pow((float)Math.PI * 2f / skySettings.waveLength.y, 2f) * K.y;
		result.z = 434f * num * (float)Math.PI * Mathf.Pow((float)Math.PI * 2f / skySettings.waveLength.z, 2f) * K.z;
		result.x = Mathf.Pow(result.x, -1f);
		result.y = Mathf.Pow(result.y, -1f);
		result.z = Mathf.Pow(result.z, -1f);
		return result;
	}

	public Vector3 GetMieG()
	{
		return new Vector3(1f - skySettings.g * skySettings.g, 1f + skySettings.g * skySettings.g, 2f * skySettings.g);
	}

	public Vector3 GetMieGScene()
	{
		return new Vector3(1f - fogSettings.g * fogSettings.g, 1f + fogSettings.g * fogSettings.g, 2f * fogSettings.g);
	}

	private void SetupShader(float setup)
	{
		if (skyMat != null)
		{
			skyMat.SetVector("_SunDir", -SunTransform.transform.forward);
			skyMat.SetVector("_MoonDir", Components.Moon.transform.forward);
			skyMat.SetColor("_MoonColor", skySettings.moonColor);
			skyMat.SetFloat("_MoonSize", skySettings.moonSize);
			skyMat.SetFloat("_MoonBrightness", skySettings.moonBrightness);
			if (skySettings.renderMoon)
			{
				skyMat.SetTexture("_MoonTex", moonRenderTarget);
			}
			else
			{
				skyMat.SetTexture("_MoonTex", null);
			}
			skyMat.SetColor("_scatteringColor", skySettings.scatteringColor.Evaluate(GameTime.solarTime));
			skyMat.SetColor("_sunDiskColor", skySettings.sunDiskColor.Evaluate(GameTime.solarTime));
			skyMat.SetColor("_weatherSkyMod", Color.Lerp(currentWeatherSkyMod, currentInteriorSkyboxMod, currentInteriorSkyboxMod.a));
			skyMat.SetColor("_weatherFogMod", Color.Lerp(currentWeatherFogMod, currentInteriorFogColorMod, currentInteriorFogColorMod.a));
			skyMat.SetVector("_Bm", BetaMie() * (skySettings.mie * Fog.scatteringStrenght));
			skyMat.SetVector("_Br", BetaRay() * skySettings.rayleigh);
			skyMat.SetVector("_mieG", GetMieG());
			skyMat.SetFloat("_SunIntensity", skySettings.sunIntensity);
			skyMat.SetFloat("_SunDiskSize", skySettings.sunDiskScale);
			skyMat.SetFloat("_SunDiskIntensity", skySettings.sunDiskIntensity);
			skyMat.SetFloat("_SunDiskSize", skySettings.sunDiskScale);
			skyMat.SetFloat("_Exposure", skySettings.skyExposure);
			skyMat.SetFloat("_SkyLuminance", skySettings.skyLuminence.Evaluate(GameTime.solarTime));
			skyMat.SetFloat("_scatteringPower", skySettings.scatteringCurve.Evaluate(GameTime.solarTime));
			skyMat.SetFloat("_SkyColorPower", skySettings.skyColorPower.Evaluate(GameTime.solarTime));
			skyMat.SetFloat("_StarsIntensity", skySettings.starsIntensity.Evaluate(GameTime.solarTime));
			skyMat.SetFloat("_GalaxyIntensity", skySettings.galaxyIntensity.Evaluate(GameTime.solarTime));
			skyMat.SetColor("_moonGlowColor", skySettings.moonGlowColor);
			if (skySettings.blackGroundMode)
			{
				skyMat.SetInt("_blackGround", 1);
			}
			else
			{
				skyMat.SetInt("_blackGround", 0);
			}
			float value = ((!HDR) ? 0f : 1f);
			skyMat.SetFloat("_hdr", value);
			skyMat.SetFloat("_moonGlowStrenght", skySettings.moonGlow.Evaluate(GameTime.solarTime));
			skyMat.SetVector("_CloudAnimation", cloudAnim);
			if (cloudsSettings.cirrusCloudsTexture != null)
			{
				skyMat.SetTexture("_CloudMap", cloudsSettings.cirrusCloudsTexture);
			}
			skyMat.SetColor("_CloudColor", cloudsSettings.cirrusCloudsColor.Evaluate(GameTime.solarTime));
			skyMat.SetFloat("_CloudAltitude", cloudsSettings.cirrusCloudsAltitude);
			skyMat.SetFloat("_CloudAlpha", cloudsConfig.cirrusAlpha);
			skyMat.SetFloat("_CloudCoverage", cloudsConfig.cirrusCoverage);
			skyMat.SetFloat("_CloudColorPower", cloudsConfig.cirrusColorPow);
			if (flatCloudsRenderTarget != null)
			{
				skyMat.SetTexture("_Cloud1Map", flatCloudsRenderTarget);
				skyMat.SetColor("_Cloud1Color", cloudsSettings.flatCloudsColor.Evaluate(GameTime.solarTime));
				skyMat.SetFloat("_Cloud1Altitude", cloudsSettings.flatCloudsAltitude);
				skyMat.SetFloat("_Cloud1Alpha", cloudsConfig.flatAlpha);
				skyMat.SetFloat("_Cloud1ColorPower", cloudsConfig.flatColorPow);
			}
		}
		Shader.SetGlobalVector("_SunDir", -Components.Sun.transform.forward);
		Shader.SetGlobalVector("_MoonDir", -Components.Moon.transform.forward);
		Shader.SetGlobalColor("_scatteringColor", skySettings.scatteringColor.Evaluate(GameTime.solarTime));
		Shader.SetGlobalColor("_sunDiskColor", skySettings.sunDiskColor.Evaluate(GameTime.solarTime));
		Shader.SetGlobalColor("_weatherSkyMod", Color.Lerp(currentWeatherSkyMod, currentInteriorSkyboxMod, currentInteriorSkyboxMod.a));
		Shader.SetGlobalColor("_weatherFogMod", Color.Lerp(currentWeatherFogMod, currentInteriorFogColorMod, currentInteriorFogColorMod.a));
		Shader.SetGlobalFloat("_gameTime", Mathf.Clamp(1f - GameTime.solarTime, 0.5f, 1f));
		Shader.SetGlobalFloat("_SkyFogHeight", Fog.skyFogHeight);
		Shader.SetGlobalFloat("_scatteringStrenght", Fog.scatteringStrenght);
		Shader.SetGlobalFloat("_skyFogIntensity", fogSettings.skyFogIntensity);
		Shader.SetGlobalFloat("_SunBlocking", Fog.sunBlocking);
		Shader.SetGlobalVector("_EnviroParams", new Vector4(Mathf.Clamp(1f - GameTime.solarTime, 0.5f, 1f), (!fogSettings.distanceFog) ? 0f : 1f, (!fogSettings.heightFog) ? 0f : 1f, (!HDR) ? 0f : 1f));
		Shader.SetGlobalVector("_Bm", BetaMie() * (skySettings.mie * (Fog.scatteringStrenght * GameTime.solarTime)));
		Shader.SetGlobalVector("_BmScene", BetaMie() * (fogSettings.mie * (Fog.scatteringStrenght * GameTime.solarTime)));
		Shader.SetGlobalVector("_Br", BetaRay() * skySettings.rayleigh);
		Shader.SetGlobalVector("_mieG", GetMieG());
		Shader.SetGlobalVector("_mieGScene", GetMieGScene());
		Shader.SetGlobalFloat("_SunIntensity", skySettings.sunIntensity);
		Shader.SetGlobalFloat("_SunDiskSize", skySettings.sunDiskScale);
		Shader.SetGlobalFloat("_SunDiskIntensity", skySettings.sunDiskIntensity);
		Shader.SetGlobalFloat("_SunDiskSize", skySettings.sunDiskScale);
		Shader.SetGlobalFloat("_Exposure", skySettings.skyExposure);
		Shader.SetGlobalFloat("_SkyLuminance", skySettings.skyLuminence.Evaluate(GameTime.solarTime));
		Shader.SetGlobalFloat("_scatteringPower", skySettings.scatteringCurve.Evaluate(GameTime.solarTime));
		Shader.SetGlobalFloat("_SkyColorPower", skySettings.skyColorPower.Evaluate(GameTime.solarTime));
		Shader.SetGlobalFloat("_heightFogIntensity", fogSettings.heightFogIntensity);
		Shader.SetGlobalFloat("_distanceFogIntensity", fogSettings.distanceFogIntensity);
		if (Application.isPlaying)
		{
			Shader.SetGlobalFloat("_maximumFogDensity", 1f - fogSettings.maximumFogDensity);
		}
		else
		{
			Shader.SetGlobalFloat("_maximumFogDensity", 1f);
		}
		Shader.SetGlobalFloat("_lightning", thunder);
		float num = 0f;
		if (Weather.currentActiveWeatherPreset != null)
		{
			num = Weather.currentActiveWeatherPreset.WindStrenght;
		}
		if (cloudsSettings.useWindZoneDirection)
		{
			cloudsSettings.cloudsWindDirectionX = 0f - Components.windZone.transform.forward.x;
			cloudsSettings.cloudsWindDirectionY = 0f - Components.windZone.transform.forward.z;
		}
		cloudAnim += new Vector2(cloudsSettings.cloudsTimeScale * (num * cloudsSettings.cloudsWindDirectionX) * cloudsSettings.cloudsWindStrengthModificator * Time.deltaTime, cloudsSettings.cloudsTimeScale * (num * cloudsSettings.cloudsWindDirectionY) * cloudsSettings.cloudsWindStrengthModificator * Time.deltaTime);
		cloudAnimNonScaled += new Vector2(cloudsSettings.cloudsTimeScale * (num * cloudsSettings.cloudsWindDirectionX) * cloudsSettings.cloudsWindStrengthModificator * Time.deltaTime * 0.1f, cloudsSettings.cloudsTimeScale * (num * cloudsSettings.cloudsWindDirectionY) * cloudsSettings.cloudsWindStrengthModificator * Time.deltaTime * 0.1f);
		if (cloudAnim.x > 1f)
		{
			cloudAnim.x = -1f;
		}
		else if (cloudAnim.x < -1f)
		{
			cloudAnim.x = 1f;
		}
		if (cloudAnim.y > 1f)
		{
			cloudAnim.y = -1f;
		}
		else if (cloudAnim.y < -1f)
		{
			cloudAnim.y = 1f;
		}
		if (MoonShader != null)
		{
			MoonShader.SetFloat("_Phase", customMoonPhase);
			MoonShader.SetColor("_Color", skySettings.moonColor);
			MoonShader.SetFloat("_Brightness", skySettings.moonBrightness * (1f - GameTime.solarTime));
		}
	}

	private void UpdateSkyRenderingComponent()
	{
		if (!(EnviroSkyRender == null))
		{
			EnviroSkyRender.Resolution = volumeLightSettings.Resolution;
			if (EnviroSkyRender._volumeRenderingMaterial != null)
			{
				EnviroSkyRender._volumeRenderingMaterial.SetTexture("_Clouds", cloudsRenderTarget);
				float value = ((!HDR) ? 0f : 1f);
				EnviroSkyRender._volumeRenderingMaterial.SetFloat("_hdr", value);
			}
		}
	}

	private DateTime CreateSystemDate()
	{
		return default(DateTime).AddYears(GameTime.Years - 1).AddDays(GameTime.Days - 1);
	}

	private void UpdateSunAndMoonPosition()
	{
		DateTime dateTime = CreateSystemDate();
		float num = 367 * dateTime.Year - 7 * (dateTime.Year + (dateTime.Month / 12 + 9) / 12) / 4 + 275 * dateTime.Month / 9 + dateTime.Day - 730530;
		num += GetUniversalTimeOfDay() / 24f;
		float ecl = 23.4393f - 3.563E-07f * num;
		if (skySettings.sunAndMoonPosition == EnviroSkySettings.SunAndMoonCalc.Realistic)
		{
			CalculateSunPosition(num, ecl, simpleMoon: false);
			CalculateMoonPosition(num, ecl);
		}
		else
		{
			CalculateSunPosition(num, ecl, simpleMoon: true);
		}
		CalculateStarsPosition(LST);
	}

	private float Remap(float value, float from1, float to1, float from2, float to2)
	{
		return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
	}

	private void CalculateSunPosition(float d, float ecl, bool simpleMoon)
	{
		float num = 282.9404f + 4.70935E-05f * d;
		float num2 = 0.016709f - 1.151E-09f * d;
		float num3 = 356.047f + 0.98560023f * d;
		float num4 = num3 + num2 * 57.29578f * Mathf.Sin((float)Math.PI / 180f * num3) * (1f + num2 * Mathf.Cos((float)Math.PI / 180f * num3));
		float num5 = Mathf.Cos((float)Math.PI / 180f * num4) - num2;
		float num6 = Mathf.Sin((float)Math.PI / 180f * num4) * Mathf.Sqrt(1f - num2 * num2);
		float num7 = 57.29578f * Mathf.Atan2(num6, num5);
		float num8 = Mathf.Sqrt(num5 * num5 + num6 * num6);
		float num9 = num7 + num;
		float num10 = num8 * Mathf.Cos((float)Math.PI / 180f * num9);
		float num11 = num8 * Mathf.Sin((float)Math.PI / 180f * num9);
		float num12 = num10;
		float num13 = num11 * Mathf.Cos((float)Math.PI / 180f * ecl);
		float y = num11 * Mathf.Sin((float)Math.PI / 180f * ecl);
		float f = Mathf.Atan2(y, Mathf.Sqrt(num12 * num12 + num13 * num13));
		float num14 = Mathf.Sin(f);
		float num15 = Mathf.Cos(f);
		float num16 = num9 + 180f;
		float num17 = num16 + GetUniversalTimeOfDay() * 15f;
		LST = num17 + GameTime.Longitude;
		float num18 = LST - 57.29578f * Mathf.Atan2(num13, num12);
		float f2 = (float)Math.PI / 180f * num18;
		float num19 = Mathf.Sin(f2);
		float num20 = Mathf.Cos(f2);
		float num21 = num20 * num15;
		float num22 = num19 * num15;
		float num23 = num14;
		float num24 = Mathf.Sin((float)Math.PI / 180f * GameTime.Latitude);
		float num25 = Mathf.Cos((float)Math.PI / 180f * GameTime.Latitude);
		float num26 = num21 * num24 - num23 * num25;
		float num27 = num22;
		float y2 = num21 * num25 + num23 * num24;
		float num28 = Mathf.Atan2(num27, num26) + (float)Math.PI;
		float num29 = Mathf.Atan2(y2, Mathf.Sqrt(num26 * num26 + num27 * num27));
		float num30 = (float)Math.PI / 2f - num29;
		float phi = num28;
		GameTime.solarTime = Mathf.Clamp01(Remap(num30, -1.5f, 0f, 1.5f, 1f));
		SunTransform.localPosition = OrbitalToLocal(num30, phi);
		SunTransform.LookAt(DomeTransform.position);
		if (simpleMoon)
		{
			MoonTransform.localPosition = OrbitalToLocal(num30 - (float)Math.PI, phi);
			MoonTransform.LookAt(DomeTransform.position);
		}
		SetupShader(num30);
	}

	private void CalculateMoonPosition(float d, float ecl)
	{
		float num = 125.1228f - 0.05295381f * d;
		float num2 = 5.1454f;
		float num3 = 318.0634f + 0.16435732f * d;
		float num4 = 60.2666f;
		float num5 = 0.0549f;
		float num6 = 115.3654f + 13.064993f * d;
		float num7 = (float)Math.PI / 180f * num6;
		float f = num7 + num5 * Mathf.Sin(num7) * (1f + num5 * Mathf.Cos(num7));
		float num8 = num4 * (Mathf.Cos(f) - num5);
		float num9 = num4 * (Mathf.Sqrt(1f - num5 * num5) * Mathf.Sin(f));
		float num10 = 57.29578f * Mathf.Atan2(num9, num8);
		float num11 = Mathf.Sqrt(num8 * num8 + num9 * num9);
		float f2 = (float)Math.PI / 180f * num;
		float num12 = Mathf.Sin(f2);
		float num13 = Mathf.Cos(f2);
		float f3 = (float)Math.PI / 180f * (num10 + num3);
		float num14 = Mathf.Sin(f3);
		float num15 = Mathf.Cos(f3);
		float f4 = (float)Math.PI / 180f * num2;
		float num16 = Mathf.Cos(f4);
		float num17 = num11 * (num13 * num15 - num12 * num14 * num16);
		float num18 = num11 * (num12 * num15 + num13 * num14 * num16);
		float num19 = num11 * (num14 * Mathf.Sin(f4));
		float num20 = Mathf.Cos((float)Math.PI / 180f * ecl);
		float num21 = Mathf.Sin((float)Math.PI / 180f * ecl);
		float num22 = num17;
		float num23 = num18 * num20 - num19 * num21;
		float y = num18 * num21 + num19 * num20;
		float num24 = Mathf.Atan2(num23, num22);
		float f5 = Mathf.Atan2(y, Mathf.Sqrt(num22 * num22 + num23 * num23));
		float f6 = (float)Math.PI / 180f * LST - num24;
		float num25 = Mathf.Cos(f6) * Mathf.Cos(f5);
		float num26 = Mathf.Sin(f6) * Mathf.Cos(f5);
		float num27 = Mathf.Sin(f5);
		float f7 = (float)Math.PI / 180f * GameTime.Latitude;
		float num28 = Mathf.Sin(f7);
		float num29 = Mathf.Cos(f7);
		float num30 = num25 * num28 - num27 * num29;
		float num31 = num26;
		float y2 = num25 * num29 + num27 * num28;
		float num32 = Mathf.Atan2(num31, num30) + (float)Math.PI;
		float num33 = Mathf.Atan2(y2, Mathf.Sqrt(num30 * num30 + num31 * num31));
		float num34 = (float)Math.PI / 2f - num33;
		float phi = num32;
		MoonTransform.localPosition = OrbitalToLocal(num34, phi);
		GameTime.lunarTime = Mathf.Clamp01(Remap(num34, -1.5f, 0f, 1.5f, 1f));
		MoonTransform.LookAt(DomeTransform.position);
	}

	private void CalculateStarsPosition(float siderealTime)
	{
		if (siderealTime > 24f)
		{
			siderealTime -= 24f;
		}
		else if (siderealTime < 0f)
		{
			siderealTime += 24f;
		}
		Quaternion localRotation = Quaternion.Euler(90f - GameTime.Latitude, (float)Math.PI / 180f * GameTime.Longitude, 0f);
		localRotation *= Quaternion.Euler(0f, siderealTime, 0f);
		Components.starsRotation.localRotation = localRotation;
		if (RenderSettings.skybox != null)
		{
			RenderSettings.skybox.SetMatrix("_StarsMatrix", Components.starsRotation.worldToLocalMatrix);
		}
	}

	private Vector3 UpdateSatellitePosition(float orbit, float orbit2, float speed)
	{
		float f = (float)Math.PI / 180f * GameTime.Latitude;
		float num = Mathf.Sin(f);
		float num2 = Mathf.Cos(f);
		float num3 = (float)Math.PI / 180f * GameTime.Longitude;
		float f2 = orbit2 * Mathf.Sin((float)Math.PI / 184f * ((float)GameTime.Days - 81f));
		float num4 = Mathf.Sin(f2);
		float num5 = Mathf.Cos(f2);
		float num6 = (int)(GameTime.Longitude / 15f);
		float num7 = (float)Math.PI / 12f * num6;
		float num8 = GetUniversalTimeOfDay() + orbit * Mathf.Sin(0.03333255f * ((float)GameTime.Days - 80f)) - speed * Mathf.Sin((float)Math.PI / 355f * ((float)GameTime.Days - 8f)) + 12f / (float)Math.PI * (num7 - num3);
		float f3 = (float)Math.PI / 12f * num8;
		float num9 = Mathf.Sin(f3);
		float num10 = Mathf.Cos(f3);
		float f4 = num * num4 - num2 * num5 * num10;
		float num11 = Mathf.Asin(f4);
		float y = (0f - num5) * num9;
		float x = num2 * num4 - num * num5 * num10;
		float num12 = Mathf.Atan2(y, x);
		float theta = (float)Math.PI / 2f - num11;
		float phi = num12;
		return OrbitalToLocal(theta, phi);
	}

	private Vector3 OrbitalToLocal(float theta, float phi)
	{
		float num = Mathf.Sin(theta);
		float y = Mathf.Cos(theta);
		float num2 = Mathf.Sin(phi);
		float num3 = Mathf.Cos(phi);
		Vector3 result = default(Vector3);
		result.z = num * num3;
		result.y = y;
		result.x = num * num2;
		return result;
	}

	private void UpdateReflections()
	{
		Components.GlobalReflectionProbe.intensity = lightSettings.globalReflectionsIntensity;
		Components.GlobalReflectionProbe.size = base.transform.localScale * lightSettings.globalReflectionsScale;
		if ((currentTimeInHours > lastRelfectionUpdate + (double)lightSettings.globalReflectionsUpdate || currentTimeInHours < lastRelfectionUpdate - (double)lightSettings.globalReflectionsUpdate) && lightSettings.globalReflections)
		{
			Components.GlobalReflectionProbe.enabled = true;
			lastRelfectionUpdate = currentTimeInHours;
			Components.GlobalReflectionProbe.RenderProbe();
		}
		else if (!lightSettings.globalReflections)
		{
			Components.GlobalReflectionProbe.enabled = false;
		}
	}

	private void UpdateTime()
	{
		if (Application.isPlaying)
		{
			float num = 0f;
			num = (isNight ? (0.4f / GameTime.NightLengthInMinutes) : (0.4f / GameTime.DayLengthInMinutes));
			hourTime = num * Time.deltaTime;
			switch (GameTime.ProgressTime)
			{
			case EnviroTime.TimeProgressMode.None:
				SetTime(GameTime.Years, GameTime.Days, GameTime.Hours, GameTime.Minutes, GameTime.Seconds);
				break;
			case EnviroTime.TimeProgressMode.Simulated:
				internalHour += hourTime;
				SetGameTime();
				customMoonPhase += Time.deltaTime / (30f * (GameTime.DayLengthInMinutes * 60f)) * 2f;
				break;
			case EnviroTime.TimeProgressMode.OneDay:
				internalHour += hourTime;
				SetGameTime();
				customMoonPhase += Time.deltaTime / (30f * (GameTime.DayLengthInMinutes * 60f)) * 2f;
				break;
			case EnviroTime.TimeProgressMode.SystemTime:
				SetTime(DateTime.Now);
				customMoonPhase += Time.deltaTime / 2592000f * 2f;
				break;
			}
		}
		else
		{
			SetTime(GameTime.Years, GameTime.Days, GameTime.Hours, GameTime.Minutes, GameTime.Seconds);
		}
		if (customMoonPhase < -1f)
		{
			customMoonPhase += 2f;
		}
		else if (customMoonPhase > 1f)
		{
			customMoonPhase -= 2f;
		}
		if (internalHour > lastHourUpdate + 1f)
		{
			lastHourUpdate = internalHour;
			NotifyHourPassed();
		}
		if ((float)GameTime.Days >= seasonsSettings.SpringInDays + seasonsSettings.SummerInDays + seasonsSettings.AutumnInDays + seasonsSettings.WinterInDays)
		{
			GameTime.Years++;
			GameTime.Days = 0;
			NotifyYearPassed();
		}
		currentHour = internalHour;
		currentDay = GameTime.Days;
		currentYear = GameTime.Years;
		currentTimeInHours = GetInHours(internalHour, currentDay, currentYear);
	}

	private void SetInternalTime(int year, int dayOfYear, int hour, int minute, int seconds)
	{
		GameTime.Years = year;
		GameTime.Days = dayOfYear;
		GameTime.Minutes = minute;
		GameTime.Hours = hour;
		internalHour = (float)hour + (float)minute * 0.0166667f + (float)seconds * 0.000277778f;
	}

	private void SetGameTime()
	{
		if (internalHour >= 24f)
		{
			internalHour -= 24f;
			NotifyHourPassed();
			lastHourUpdate = internalHour;
			if (GameTime.ProgressTime != EnviroTime.TimeProgressMode.OneDay)
			{
				GameTime.Days++;
				NotifyDayPassed();
			}
		}
		else if (internalHour < 0f)
		{
			internalHour = 24f + internalHour;
			lastHourUpdate = internalHour;
			if (GameTime.ProgressTime != EnviroTime.TimeProgressMode.OneDay)
			{
				GameTime.Days--;
				NotifyDayPassed();
			}
		}
		float num = internalHour;
		GameTime.Hours = (int)num;
		num -= (float)GameTime.Hours;
		GameTime.Minutes = (int)(num * 60f);
		num -= (float)GameTime.Minutes * 0.0166667f;
		GameTime.Seconds = (int)(num * 3600f);
	}

	private void UpdateAmbientLight()
	{
		switch (lightSettings.ambientMode)
		{
		case AmbientMode.Flat:
		{
			Color a = Color.Lerp(lightSettings.ambientSkyColor.Evaluate(GameTime.solarTime), currentWeatherLightMod, currentWeatherLightMod.a) * lightSettings.ambientIntensity.Evaluate(GameTime.solarTime);
			RenderSettings.ambientSkyColor = Color.Lerp(a, currentInteriorAmbientLightMod, currentInteriorAmbientLightMod.a);
			break;
		}
		case AmbientMode.Trilight:
		{
			Color a2 = Color.Lerp(lightSettings.ambientSkyColor.Evaluate(GameTime.solarTime), currentWeatherLightMod, currentWeatherLightMod.a) * lightSettings.ambientIntensity.Evaluate(GameTime.solarTime);
			RenderSettings.ambientSkyColor = Color.Lerp(a2, currentInteriorAmbientLightMod, currentInteriorAmbientLightMod.a);
			Color a3 = Color.Lerp(lightSettings.ambientEquatorColor.Evaluate(GameTime.solarTime), currentWeatherLightMod, currentWeatherLightMod.a) * lightSettings.ambientIntensity.Evaluate(GameTime.solarTime);
			RenderSettings.ambientEquatorColor = Color.Lerp(a3, currentInteriorAmbientEQLightMod, currentInteriorAmbientEQLightMod.a);
			Color a4 = Color.Lerp(lightSettings.ambientGroundColor.Evaluate(GameTime.solarTime), currentWeatherLightMod, currentWeatherLightMod.a) * lightSettings.ambientIntensity.Evaluate(GameTime.solarTime);
			RenderSettings.ambientGroundColor = Color.Lerp(a4, currentInteriorAmbientGRLightMod, currentInteriorAmbientGRLightMod.a);
			break;
		}
		case AmbientMode.Skybox:
			RenderSettings.ambientIntensity = lightSettings.ambientIntensity.Evaluate(GameTime.solarTime);
			if (lastAmbientSkyUpdate < internalHour || lastAmbientSkyUpdate > internalHour + 0.101f)
			{
				DynamicGI.UpdateEnvironment();
				lastAmbientSkyUpdate = internalHour + 0.1f;
			}
			break;
		}
	}

	private void CalculateDirectLight()
	{
		Color a = Color.Lerp(lightSettings.LightColor.Evaluate(GameTime.solarTime), currentWeatherLightMod, currentWeatherLightMod.a);
		MainLight.color = Color.Lerp(a, currentInteriorDirectLightMod, currentInteriorDirectLightMod.a);
		Shader.SetGlobalColor("_EnviroLighting", lightSettings.LightColor.Evaluate(GameTime.solarTime));
		Shader.SetGlobalVector("_SunDirection", -Components.Sun.transform.forward);
		Shader.SetGlobalVector("_SunPosition", Components.Sun.transform.localPosition + -Components.Sun.transform.forward * 10000f);
		Shader.SetGlobalVector("_MoonPosition", Components.Moon.transform.localPosition);
		float intensity;
		if (!isNight)
		{
			intensity = lightSettings.directLightSunIntensity.Evaluate(GameTime.solarTime);
			Components.Sun.transform.LookAt(new Vector3(DomeTransform.position.x, DomeTransform.position.y - lightSettings.directLightAngleOffset, DomeTransform.position.z));
			Components.DirectLight.rotation = Components.Sun.transform.rotation;
		}
		else
		{
			intensity = lightSettings.directLightMoonIntensity.Evaluate(GameTime.lunarTime);
			Components.Moon.transform.LookAt(new Vector3(DomeTransform.position.x, DomeTransform.position.y - lightSettings.directLightAngleOffset, DomeTransform.position.z));
			Components.DirectLight.rotation = Components.Moon.transform.rotation;
		}
		MainLight.intensity = intensity;
		MainLight.shadowStrength = lightSettings.shadowIntensity.Evaluate(GameTime.solarTime);
	}

	private Quaternion LightLookAt(Quaternion inputRotation, Quaternion newRotation)
	{
		return Quaternion.Lerp(inputRotation, newRotation, 500f * Time.deltaTime);
	}

	private void ValidateParameters()
	{
		internalHour = Mathf.Repeat(internalHour, 24f);
		GameTime.Longitude = Mathf.Clamp(GameTime.Longitude, -180f, 180f);
		GameTime.Latitude = Mathf.Clamp(GameTime.Latitude, -90f, 90f);
	}

	public void RegisterZone(EnviroZone zoneToAdd)
	{
		Weather.zones.Add(zoneToAdd);
	}

	public void EnterZone(EnviroZone zone)
	{
		Weather.currentActiveZone = zone;
	}

	public void ExitZone()
	{
	}

	public void CreateWeatherEffectHolder()
	{
		if (Weather.VFXHolder == null)
		{
			GameObject gameObject = new GameObject();
			gameObject.name = "VFX";
			gameObject.transform.parent = EffectsHolder.transform;
			gameObject.transform.localPosition = Vector3.zero;
			Weather.VFXHolder = gameObject;
		}
	}

	private void UpdateAudioSource(EnviroWeatherPreset i)
	{
		if (i != null && i.weatherSFX != null)
		{
			if (i.weatherSFX == Weather.currentAudioSource.audiosrc.clip)
			{
				if (Weather.currentAudioSource.audiosrc.volume < 0.1f)
				{
					Weather.currentAudioSource.FadeIn(i.weatherSFX);
				}
			}
			else if (Weather.currentAudioSource == AudioSourceWeather)
			{
				AudioSourceWeather.FadeOut();
				AudioSourceWeather2.FadeIn(i.weatherSFX);
				Weather.currentAudioSource = AudioSourceWeather2;
			}
			else if (Weather.currentAudioSource == AudioSourceWeather2)
			{
				AudioSourceWeather2.FadeOut();
				AudioSourceWeather.FadeIn(i.weatherSFX);
				Weather.currentAudioSource = AudioSourceWeather;
			}
		}
		else
		{
			AudioSourceWeather.FadeOut();
			AudioSourceWeather2.FadeOut();
		}
	}

	private void UpdateClouds(EnviroWeatherPreset i, bool withTransition)
	{
		if (!(i == null))
		{
			float num = 500f * Time.deltaTime;
			if (withTransition)
			{
				num = weatherSettings.cloudTransitionSpeed * Time.deltaTime;
			}
			cloudsConfig.topColor = Color.Lerp(cloudsConfig.topColor, i.cloudsConfig.topColor, num);
			cloudsConfig.bottomColor = Color.Lerp(cloudsConfig.bottomColor, i.cloudsConfig.bottomColor, num);
			cloudsConfig.coverage = Mathf.Lerp(cloudsConfig.coverage, i.cloudsConfig.coverage, num);
			cloudsConfig.coverageHeight = Mathf.Lerp(cloudsConfig.coverageHeight, i.cloudsConfig.coverageHeight, num);
			cloudsConfig.raymarchingScale = Mathf.Lerp(cloudsConfig.raymarchingScale, i.cloudsConfig.raymarchingScale, num);
			cloudsConfig.skyBlending = Mathf.Lerp(cloudsConfig.skyBlending, i.cloudsConfig.skyBlending, num);
			cloudsConfig.density = Mathf.Lerp(cloudsConfig.density, i.cloudsConfig.density, num);
			cloudsConfig.alphaCoef = Mathf.Lerp(cloudsConfig.alphaCoef, i.cloudsConfig.alphaCoef, num);
			cloudsConfig.scatteringCoef = Mathf.Lerp(cloudsConfig.scatteringCoef, i.cloudsConfig.scatteringCoef, num);
			cloudsConfig.cloudType = Mathf.Lerp(cloudsConfig.cloudType, i.cloudsConfig.cloudType, num);
			cloudsConfig.cirrusAlpha = Mathf.Lerp(cloudsConfig.cirrusAlpha, i.cloudsConfig.cirrusAlpha, num);
			cloudsConfig.cirrusCoverage = Mathf.Lerp(cloudsConfig.cirrusCoverage, i.cloudsConfig.cirrusCoverage, num);
			cloudsConfig.cirrusColorPow = Mathf.Lerp(cloudsConfig.cirrusColorPow, i.cloudsConfig.cirrusColorPow, num);
			cloudsConfig.flatAlpha = Mathf.Lerp(cloudsConfig.flatAlpha, i.cloudsConfig.flatAlpha, num);
			cloudsConfig.flatCoverage = Mathf.Lerp(cloudsConfig.flatCoverage, i.cloudsConfig.flatCoverage, num);
			cloudsConfig.flatColorPow = Mathf.Lerp(cloudsConfig.flatColorPow, i.cloudsConfig.flatColorPow, num);
			cloudsConfig.flatSoftness = Mathf.Lerp(cloudsConfig.flatSoftness, i.cloudsConfig.flatSoftness, num);
			cloudsConfig.flatBrightness = Mathf.Lerp(cloudsConfig.flatBrightness, i.cloudsConfig.flatBrightness, num);
			globalVolumeLightIntensity = Mathf.Lerp(globalVolumeLightIntensity, i.volumeLightIntensity, num);
			currentWeatherSkyMod = Color.Lerp(currentWeatherSkyMod, i.weatherSkyMod.Evaluate(GameTime.solarTime), num);
			currentWeatherFogMod = Color.Lerp(currentWeatherFogMod, i.weatherFogMod.Evaluate(GameTime.solarTime), num * 10f);
			currentWeatherLightMod = Color.Lerp(currentWeatherLightMod, i.weatherLightMod.Evaluate(GameTime.solarTime), num);
		}
	}

	private void UpdateFog(EnviroWeatherPreset i, bool withTransition)
	{
		if (i != null)
		{
			float t = 500f * Time.deltaTime;
			if (withTransition)
			{
				t = weatherSettings.fogTransitionSpeed * Time.deltaTime;
			}
			if (fogSettings.Fogmode == FogMode.Linear)
			{
				RenderSettings.fogEndDistance = Mathf.Lerp(RenderSettings.fogEndDistance, i.fogDistance, t);
				RenderSettings.fogStartDistance = Mathf.Lerp(RenderSettings.fogStartDistance, i.fogStartDistance, t);
			}
			else if (updateFogDensity)
			{
				RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, i.fogDensity, t) * currentInteriorFogMod;
			}
			Color a = Color.Lerp(lightSettings.ambientSkyColor.Evaluate(GameTime.solarTime), customFogColor, customFogIntensity);
			RenderSettings.fogColor = Color.Lerp(a, currentWeatherFogMod, currentWeatherFogMod.a);
			fogSettings.heightDensity = Mathf.Lerp(fogSettings.heightDensity, i.heightFogDensity, t);
			Fog.skyFogHeight = Mathf.Lerp(Fog.skyFogHeight, i.SkyFogHeight, t);
			Fog.skyFogStrength = Mathf.Lerp(Fog.skyFogStrength, i.SkyFogIntensity, t);
			fogSettings.skyFogIntensity = Mathf.Lerp(fogSettings.skyFogIntensity, i.SkyFogIntensity, t);
			Fog.scatteringStrenght = Mathf.Lerp(Fog.scatteringStrenght, i.FogScatteringIntensity, t);
			Fog.sunBlocking = Mathf.Lerp(Fog.sunBlocking, i.fogSunBlocking, t);
		}
	}

	private void UpdateEffectSystems(EnviroWeatherPrefab id, bool withTransition)
	{
		if (!(id != null))
		{
			return;
		}
		float t = 500f * Time.deltaTime;
		if (withTransition)
		{
			t = weatherSettings.effectTransitionSpeed * Time.deltaTime;
		}
		for (int i = 0; i < id.effectSystems.Count; i++)
		{
			if (id.effectSystems[i].isStopped)
			{
				id.effectSystems[i].Play();
			}
			float emissionRate = Mathf.Lerp(GetEmissionRate(id.effectSystems[i]), id.effectEmmisionRates[i] * qualitySettings.GlobalParticleEmissionRates, t) * currentInteriorWeatherEffectMod;
			SetEmissionRate(id.effectSystems[i], emissionRate);
		}
		for (int j = 0; j < Weather.WeatherPrefabs.Count; j++)
		{
			if (!(Weather.WeatherPrefabs[j].gameObject != id.gameObject))
			{
				continue;
			}
			for (int k = 0; k < Weather.WeatherPrefabs[j].effectSystems.Count; k++)
			{
				float num = Mathf.Lerp(GetEmissionRate(Weather.WeatherPrefabs[j].effectSystems[k]), 0f, t);
				if (num < 1f)
				{
					num = 0f;
				}
				SetEmissionRate(Weather.WeatherPrefabs[j].effectSystems[k], num);
				if (num == 0f && !Weather.WeatherPrefabs[j].effectSystems[k].isStopped)
				{
					Weather.WeatherPrefabs[j].effectSystems[k].Stop();
				}
			}
		}
		UpdateWeatherVariables(id.weatherPreset);
	}

	private void UpdateWeatherVariables(EnviroWeatherPreset p)
	{
		Components.windZone.windMain = p.WindStrenght;
		if (Weather.wetness < p.wetnessLevel)
		{
			Weather.wetness = Mathf.Lerp(Weather.curWetness, p.wetnessLevel, weatherSettings.wetnessAccumulationSpeed * Time.deltaTime);
		}
		else
		{
			Weather.wetness = Mathf.Lerp(Weather.curWetness, p.wetnessLevel, weatherSettings.wetnessDryingSpeed * Time.deltaTime);
		}
		Weather.wetness = Mathf.Clamp(Weather.wetness, 0f, 1f);
		Weather.curWetness = Weather.wetness;
		if (Weather.snowStrength < p.snowLevel)
		{
			Weather.snowStrength = Mathf.Lerp(Weather.curSnowStrength, p.snowLevel, weatherSettings.snowAccumulationSpeed * Time.deltaTime);
		}
		else
		{
			Weather.snowStrength = Mathf.Lerp(Weather.curSnowStrength, p.snowLevel, weatherSettings.snowMeltingSpeed * Time.deltaTime);
		}
		Weather.snowStrength = Mathf.Clamp(Weather.snowStrength, 0f, 1f);
		Weather.curSnowStrength = Weather.snowStrength;
		Shader.SetGlobalFloat("_EnviroGrassSnow", Weather.curSnowStrength);
	}

	public static float GetEmissionRate(ParticleSystem system)
	{
		return system.emission.rateOverTime.constantMax;
	}

	public static void SetEmissionRate(ParticleSystem sys, float emissionRate)
	{
		ParticleSystem.EmissionModule emission = sys.emission;
		ParticleSystem.MinMaxCurve rateOverTime = emission.rateOverTime;
		rateOverTime.constantMax = emissionRate;
		emission.rateOverTime = rateOverTime;
	}

	private IEnumerator PlayThunderRandom()
	{
		yield return new WaitForSeconds(UnityEngine.Random.Range(Weather.currentActiveWeatherPreset.lightningInterval, Weather.currentActiveWeatherPreset.lightningInterval * 2f));
		if (Weather.currentActiveWeatherPrefab != null && Weather.currentActiveWeatherPrefab.weatherPreset.isLightningStorm)
		{
			if (Weather.weatherFullyChanged)
			{
				PlayLightning();
			}
			StartCoroutine(PlayThunderRandom());
		}
		else
		{
			StartCoroutine(PlayThunderRandom());
			Components.LightningGenerator.StopLightning();
		}
	}

	public IEnumerator PlayLightningEffect(Vector3 position)
	{
		lightningEffect.transform.position = position;
		lightningEffect.transform.eulerAngles = new Vector3(UnityEngine.Random.Range(-80f, -100f), 0f, 0f);
		lightningEffect.Play();
		yield return new WaitForSeconds(0.5f);
		lightningEffect.Stop();
	}

	public void PlayLightning()
	{
		if (lightningEffect != null)
		{
			StartCoroutine(PlayLightningEffect(new Vector3(base.transform.position.x + UnityEngine.Random.Range(0f - weatherSettings.lightningRange, weatherSettings.lightningRange), weatherSettings.lightningHeight, base.transform.position.z + UnityEngine.Random.Range(0f - weatherSettings.lightningRange, weatherSettings.lightningRange))));
		}
		if (audioSettings.ThunderSFX != null && 0 < audioSettings.ThunderSFX.Count)
		{
			int index = UnityEngine.Random.Range(0, audioSettings.ThunderSFX.Count);
			AudioClip audioClip = audioSettings.ThunderSFX[index];
			if (audioClip != null)
			{
				AudioSourceThunder.clip = audioClip;
				AudioSourceThunder.loop = false;
				AudioSourceThunder.Play();
			}
		}
		Components.LightningGenerator.Lightning();
	}

	private void UpdateWeather()
	{
		if (Weather.currentActiveWeatherPreset != Weather.currentActiveZone.currentActiveZoneWeatherPreset)
		{
			Weather.lastActiveWeatherPreset = Weather.currentActiveWeatherPreset;
			Weather.lastActiveWeatherPrefab = Weather.currentActiveWeatherPrefab;
			Weather.currentActiveWeatherPreset = Weather.currentActiveZone.currentActiveZoneWeatherPreset;
			Weather.currentActiveWeatherPrefab = Weather.currentActiveZone.currentActiveZoneWeatherPrefab;
			if (Weather.currentActiveWeatherPreset != null)
			{
				NotifyWeatherChanged(Weather.currentActiveWeatherPreset);
				Weather.weatherFullyChanged = false;
				if (!serverMode)
				{
					if (Weather.currentActiveWeatherPrefab != null && Weather.currentActiveWeatherPrefab.weatherPreset.isLightningStorm)
					{
						StartCoroutine(PlayThunderRandom());
					}
					else
					{
						StopCoroutine(PlayThunderRandom());
						Components.LightningGenerator.StopLightning();
					}
				}
			}
		}
		if (Weather.currentActiveWeatherPrefab != null && !serverMode)
		{
			UpdateClouds(Weather.currentActiveWeatherPreset, withTransition: true);
			UpdateFog(Weather.currentActiveWeatherPreset, withTransition: true);
			UpdateEffectSystems(Weather.currentActiveWeatherPrefab, withTransition: true);
			if (!Weather.weatherFullyChanged)
			{
				CalcWeatherTransitionState();
			}
		}
		else if (Weather.currentActiveWeatherPrefab != null)
		{
			UpdateWeatherVariables(Weather.currentActiveWeatherPrefab.weatherPreset);
		}
	}

	public void ForceWeatherUpdate()
	{
		Weather.lastActiveWeatherPreset = Weather.currentActiveWeatherPreset;
		Weather.lastActiveWeatherPrefab = Weather.currentActiveWeatherPrefab;
		Weather.currentActiveWeatherPreset = Weather.currentActiveZone.currentActiveZoneWeatherPreset;
		Weather.currentActiveWeatherPrefab = Weather.currentActiveZone.currentActiveZoneWeatherPrefab;
		if (!(Weather.currentActiveWeatherPreset != null))
		{
			return;
		}
		NotifyWeatherChanged(Weather.currentActiveWeatherPreset);
		Weather.weatherFullyChanged = false;
		if (!serverMode)
		{
			if (Weather.currentActiveWeatherPrefab != null && Weather.currentActiveWeatherPrefab.weatherPreset.isLightningStorm)
			{
				StartCoroutine(PlayThunderRandom());
				return;
			}
			StopCoroutine(PlayThunderRandom());
			Components.LightningGenerator.StopLightning();
		}
	}

	private void CalcWeatherTransitionState()
	{
		bool flag = false;
		flag = ((cloudsConfig.coverage >= Weather.currentActiveWeatherPreset.cloudsConfig.coverage - 0.01f) ? true : false);
		Weather.weatherFullyChanged = flag;
	}

	public void SetWeatherOverwrite(int weatherId)
	{
		if (weatherId >= 0 && weatherId <= Weather.WeatherPrefabs.Count)
		{
			if (Weather.WeatherPrefabs[weatherId] != Weather.currentActiveWeatherPrefab)
			{
				Weather.currentActiveZone.currentActiveZoneWeatherPrefab = Weather.WeatherPrefabs[weatherId];
				Weather.currentActiveZone.currentActiveZoneWeatherPreset = Weather.WeatherPrefabs[weatherId].weatherPreset;
				instance.NotifyZoneWeatherChanged(Weather.WeatherPrefabs[weatherId].weatherPreset, Weather.currentActiveZone);
			}
			UpdateClouds(Weather.currentActiveZone.currentActiveZoneWeatherPreset, withTransition: false);
			UpdateFog(Weather.currentActiveZone.currentActiveZoneWeatherPreset, withTransition: false);
			UpdateEffectSystems(Weather.currentActiveZone.currentActiveZoneWeatherPrefab, withTransition: false);
		}
	}

	public void SetWeatherOverwrite(EnviroWeatherPreset preset)
	{
		if (preset == null)
		{
			return;
		}
		if (preset != Weather.currentActiveWeatherPreset)
		{
			for (int i = 0; i < Weather.WeatherPrefabs.Count; i++)
			{
				if (preset == Weather.WeatherPrefabs[i].weatherPreset)
				{
					Weather.currentActiveZone.currentActiveZoneWeatherPrefab = Weather.WeatherPrefabs[i];
					Weather.currentActiveZone.currentActiveZoneWeatherPreset = preset;
					instance.NotifyZoneWeatherChanged(preset, Weather.currentActiveZone);
				}
			}
		}
		UpdateClouds(Weather.currentActiveZone.currentActiveZoneWeatherPreset, withTransition: false);
		UpdateFog(Weather.currentActiveZone.currentActiveZoneWeatherPreset, withTransition: false);
		UpdateEffectSystems(Weather.currentActiveZone.currentActiveZoneWeatherPrefab, withTransition: false);
	}

	public void ChangeWeather(int weatherId)
	{
		if (weatherId >= 0 && weatherId < Weather.WeatherPrefabs.Count && Weather.WeatherPrefabs[weatherId] != Weather.currentActiveWeatherPrefab)
		{
			Weather.currentActiveZone.currentActiveZoneWeatherPrefab = Weather.WeatherPrefabs[weatherId];
			Weather.currentActiveZone.currentActiveZoneWeatherPreset = Weather.WeatherPrefabs[weatherId].weatherPreset;
			instance.NotifyZoneWeatherChanged(Weather.WeatherPrefabs[weatherId].weatherPreset, Weather.currentActiveZone);
		}
	}

	public void ChangeWeather(string weatherName)
	{
		for (int i = 0; i < Weather.WeatherPrefabs.Count; i++)
		{
			if (Weather.WeatherPrefabs[i].weatherPreset.Name == weatherName && Weather.WeatherPrefabs[i] != Weather.currentActiveWeatherPrefab)
			{
				ChangeWeather(i);
				instance.NotifyZoneWeatherChanged(Weather.WeatherPrefabs[i].weatherPreset, Weather.currentActiveZone);
			}
		}
	}

	public void ChangeCloudsQuality(EnviroCloudSettings.CloudQuality q)
	{
		switch (q)
		{
		case EnviroCloudSettings.CloudQuality.Custom:
			return;
		case EnviroCloudSettings.CloudQuality.Lowest:
			cloudsSettings.bottomCloudHeight = 2000f;
			cloudsSettings.topCloudHeight = 4000f;
			cloudsSettings.cloudsWorldScale = 120000f;
			cloudsSettings.raymarchSteps = 75;
			cloudsSettings.stepsInDepthModificator = 0.7f;
			cloudsSettings.cloudsRenderResolution = 2;
			cloudsSettings.reprojectionPixelSize = EnviroCloudSettings.ReprojectionPixelSize.Medium;
			cloudsSettings.baseNoiseUV = 26f;
			cloudsSettings.detailNoiseUV = 1f;
			cloudsSettings.detailQuality = EnviroCloudSettings.CloudDetailQuality.Low;
			break;
		case EnviroCloudSettings.CloudQuality.Low:
			cloudsSettings.bottomCloudHeight = 2000f;
			cloudsSettings.topCloudHeight = 4000f;
			cloudsSettings.cloudsWorldScale = 120000f;
			cloudsSettings.raymarchSteps = 90;
			cloudsSettings.stepsInDepthModificator = 0.7f;
			cloudsSettings.cloudsRenderResolution = 2;
			cloudsSettings.reprojectionPixelSize = EnviroCloudSettings.ReprojectionPixelSize.Low;
			cloudsSettings.baseNoiseUV = 30f;
			cloudsSettings.detailNoiseUV = 1f;
			cloudsSettings.detailQuality = EnviroCloudSettings.CloudDetailQuality.Low;
			break;
		case EnviroCloudSettings.CloudQuality.Medium:
			cloudsSettings.bottomCloudHeight = 2000f;
			cloudsSettings.topCloudHeight = 4500f;
			cloudsSettings.cloudsWorldScale = 120000f;
			cloudsSettings.raymarchSteps = 100;
			cloudsSettings.stepsInDepthModificator = 0.7f;
			cloudsSettings.cloudsRenderResolution = 1;
			cloudsSettings.reprojectionPixelSize = EnviroCloudSettings.ReprojectionPixelSize.Medium;
			cloudsSettings.baseNoiseUV = 35f;
			cloudsSettings.detailNoiseUV = 50f;
			cloudsSettings.detailQuality = EnviroCloudSettings.CloudDetailQuality.Low;
			break;
		case EnviroCloudSettings.CloudQuality.High:
			cloudsSettings.bottomCloudHeight = 2000f;
			cloudsSettings.topCloudHeight = 5000f;
			cloudsSettings.cloudsWorldScale = 120000f;
			cloudsSettings.raymarchSteps = 128;
			cloudsSettings.stepsInDepthModificator = 0.6f;
			cloudsSettings.cloudsRenderResolution = 1;
			cloudsSettings.reprojectionPixelSize = EnviroCloudSettings.ReprojectionPixelSize.Medium;
			cloudsSettings.baseNoiseUV = 40f;
			cloudsSettings.detailNoiseUV = 50f;
			cloudsSettings.detailQuality = EnviroCloudSettings.CloudDetailQuality.Low;
			break;
		case EnviroCloudSettings.CloudQuality.Ultra:
			cloudsSettings.bottomCloudHeight = 2000f;
			cloudsSettings.topCloudHeight = 5500f;
			cloudsSettings.cloudsWorldScale = 120000f;
			cloudsSettings.raymarchSteps = 150;
			cloudsSettings.stepsInDepthModificator = 0.5f;
			cloudsSettings.cloudsRenderResolution = 1;
			cloudsSettings.reprojectionPixelSize = EnviroCloudSettings.ReprojectionPixelSize.Low;
			cloudsSettings.baseNoiseUV = 40f;
			cloudsSettings.detailNoiseUV = 70f;
			cloudsSettings.detailQuality = EnviroCloudSettings.CloudDetailQuality.Low;
			break;
		case EnviroCloudSettings.CloudQuality.VR_Low:
			cloudsSettings.bottomCloudHeight = 3000f;
			cloudsSettings.topCloudHeight = 4200f;
			cloudsSettings.cloudsWorldScale = 30000f;
			cloudsSettings.raymarchSteps = 60;
			cloudsSettings.cloudsRenderResolution = 2;
			cloudsSettings.reprojectionPixelSize = EnviroCloudSettings.ReprojectionPixelSize.Low;
			cloudsSettings.baseNoiseUV = 20f;
			cloudsSettings.detailNoiseUV = 1f;
			cloudsSettings.detailQuality = EnviroCloudSettings.CloudDetailQuality.Low;
			break;
		case EnviroCloudSettings.CloudQuality.VR_Medium:
			cloudsSettings.bottomCloudHeight = 3000f;
			cloudsSettings.topCloudHeight = 4500f;
			cloudsSettings.cloudsWorldScale = 30000f;
			cloudsSettings.raymarchSteps = 75;
			cloudsSettings.cloudsRenderResolution = 1;
			cloudsSettings.reprojectionPixelSize = EnviroCloudSettings.ReprojectionPixelSize.Medium;
			cloudsSettings.baseNoiseUV = 22f;
			cloudsSettings.detailNoiseUV = 1f;
			cloudsSettings.detailQuality = EnviroCloudSettings.CloudDetailQuality.Low;
			break;
		case EnviroCloudSettings.CloudQuality.VR_High:
			cloudsSettings.bottomCloudHeight = 3000f;
			cloudsSettings.topCloudHeight = 4500f;
			cloudsSettings.cloudsWorldScale = 30000f;
			cloudsSettings.raymarchSteps = 80;
			cloudsSettings.cloudsRenderResolution = 1;
			cloudsSettings.reprojectionPixelSize = EnviroCloudSettings.ReprojectionPixelSize.Medium;
			cloudsSettings.baseNoiseUV = 23f;
			cloudsSettings.detailNoiseUV = 1f;
			cloudsSettings.detailQuality = EnviroCloudSettings.CloudDetailQuality.Low;
			break;
		}
		lastCloudsQuality = q;
		cloudsSettings.cloudsQuality = q;
	}

	public int GetActiveWeatherID()
	{
		for (int i = 0; i < Weather.WeatherPrefabs.Count; i++)
		{
			if (Weather.WeatherPrefabs[i].weatherPreset == Weather.currentActiveWeatherPreset)
			{
				return i;
			}
		}
		return -1;
	}

	public void Save()
	{
		PlayerPrefs.SetFloat("Time_Hours", internalHour);
		PlayerPrefs.SetInt("Time_Days", GameTime.Days);
		PlayerPrefs.SetInt("Time_Years", GameTime.Years);
		for (int i = 0; i < Weather.WeatherPrefabs.Count; i++)
		{
			if (Weather.WeatherPrefabs[i] == Weather.currentActiveWeatherPrefab)
			{
				PlayerPrefs.SetInt("currentWeather", i);
			}
		}
	}

	public void Load()
	{
		if (PlayerPrefs.HasKey("Time_Hours"))
		{
			internalHour = PlayerPrefs.GetFloat("Time_Hours");
		}
		if (PlayerPrefs.HasKey("Time_Days"))
		{
			GameTime.Days = PlayerPrefs.GetInt("Time_Days");
		}
		if (PlayerPrefs.HasKey("Time_Years"))
		{
			GameTime.Years = PlayerPrefs.GetInt("Time_Years");
		}
		if (PlayerPrefs.HasKey("currentWeather"))
		{
			SetWeatherOverwrite(PlayerPrefs.GetInt("currentWeather"));
		}
	}

	public void SetTime(DateTime date)
	{
		GameTime.Years = date.Year;
		GameTime.Days = date.DayOfYear;
		GameTime.Minutes = date.Minute;
		GameTime.Seconds = date.Second;
		GameTime.Hours = date.Hour;
		internalHour = (float)date.Hour + (float)date.Minute * 0.0166667f + (float)date.Second * 0.000277778f;
	}

	public void SetTime(int year, int dayOfYear, int hour, int minute, int seconds)
	{
		GameTime.Years = year;
		GameTime.Days = dayOfYear;
		GameTime.Minutes = minute;
		GameTime.Hours = hour;
		internalHour = (float)hour + (float)minute * 0.0166667f + (float)seconds * 0.000277778f;
	}

	public void SetInternalTimeOfDay(float inHours)
	{
		internalHour = inHours;
		GameTime.Hours = (int)inHours;
		inHours -= (float)GameTime.Hours;
		GameTime.Minutes = (int)(inHours * 60f);
		inHours -= (float)GameTime.Minutes * 0.0166667f;
		GameTime.Seconds = (int)(inHours * 3600f);
	}

	public string GetTimeStringWithSeconds()
	{
		return $"{GameTime.Hours:00}:{GameTime.Minutes:00}:{GameTime.Seconds:00}";
	}

	public string GetTimeString()
	{
		return $"{GameTime.Hours:00}:{GameTime.Minutes:00}";
	}

	public float GetUniversalTimeOfDay()
	{
		return internalHour - (float)GameTime.utcOffset;
	}

	public double GetInHours(float hours, float days, float years)
	{
		return hours + days * 24f + years * (seasonsSettings.SpringInDays + seasonsSettings.SummerInDays + seasonsSettings.AutumnInDays + seasonsSettings.WinterInDays) * 24f;
	}

	public void AssignAndStart(GameObject player, Camera Camera)
	{
		Player = player;
		PlayerCamera = Camera;
		Init();
		started = true;
		if (Singleton<Map>.IsInstance() && Singleton<Map>.Instance.Simulator != null && player != null)
		{
			Singleton<Map>.Instance.Simulator.SetEnviroParticleTarget(player.transform);
		}
	}

	public void StartAsServer()
	{
		Player = base.gameObject;
		serverMode = true;
		Init();
	}

	public void ChangeFocus(GameObject player, Camera Camera)
	{
		Player = player;
		RemoveEnviroCameraComponents(PlayerCamera);
		PlayerCamera = Camera;
		InitImageEffects();
		if (Singleton<Map>.IsInstance() && Singleton<Map>.Instance.Simulator != null && player != null)
		{
			Singleton<Map>.Instance.Simulator?.SetEnviroParticleTarget(player.transform);
		}
	}

	private void RemoveEnviroCameraComponents(Camera cam)
	{
		EnviroFog component = cam.GetComponent<EnviroFog>();
		if (component != null)
		{
			UnityEngine.Object.Destroy(component);
		}
		EnviroLightShafts[] components = cam.GetComponents<EnviroLightShafts>();
		for (int i = 0; i < components.Length; i++)
		{
			UnityEngine.Object.Destroy(components[i]);
		}
		EnviroSkyRendering component2 = cam.GetComponent<EnviroSkyRendering>();
		if (component2 != null)
		{
			UnityEngine.Object.Destroy(component2);
		}
	}

	public void Play(EnviroTime.TimeProgressMode progressMode = EnviroTime.TimeProgressMode.Simulated)
	{
		SetupSkybox();
		if (!Components.DirectLight.gameObject.activeSelf)
		{
			Components.DirectLight.gameObject.SetActive(value: true);
		}
		GameTime.ProgressTime = progressMode;
		EffectsHolder.SetActive(value: true);
		EnviroSkyRender.enabled = true;
		started = true;
	}

	public void Stop(bool disableLight = false, bool stopTime = true)
	{
		if (disableLight)
		{
			Components.DirectLight.gameObject.SetActive(value: false);
		}
		if (stopTime)
		{
			GameTime.ProgressTime = EnviroTime.TimeProgressMode.None;
		}
		EffectsHolder.SetActive(value: false);
		EnviroSkyRender.enabled = false;
		lightShaftsScriptSun.enabled = false;
		lightShaftsScriptMoon.enabled = false;
		started = false;
	}
}
