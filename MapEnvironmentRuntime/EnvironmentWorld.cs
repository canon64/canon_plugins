using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MapEnvironmentRuntime
{
    public sealed class EnvironmentWorld : MonoBehaviour
    {
        public Parameters Settings { get; private set; }
        public float Hour { get; private set; }
        public int Year { get; private set; }
        public int Month { get; private set; }
        public int Day { get; private set; }
        public float Declination { get; private set; }
        public string Weather { get; private set; }
        public string NextWeather { get; private set; }
        public float CloudCoverage { get; private set; }
        public float CloudDensity { get; private set; }
        public Vector3 CloudAdvection { get; private set; }
        public float CloudFormation { get; private set; }
        public Vector3 WindVelocity => _wind.Velocity;
        public string WindState => _wind.State;
        public Vector3 SunDirection { get; private set; }
        public Vector3 MoonDirection { get; private set; }
        public float MoonIllumination { get; private set; }
        public Color NightAmbient { get; private set; }
        public float GroundHeight => _map.position.y;
        public int SkyHistoryFrames => _skyFilter == null ? default(int) : _skyFilter.AccumulatedFrames;
        public SkyTemporalFilter CloudRenderer => _skyFilter;
        public void BindCamera(Camera camera)
        {
            if (camera == null || (_skyFilter != null && _skyFilter.gameObject == camera.gameObject)) return;
            if (_skyFilter != null) DestroyImmediate(_skyFilter);
            _skyFilter = camera.gameObject.AddComponent<SkyTemporalFilter>();
            _skyFilter.Initialize(this, _filterSource);
        }
        public Light Sun => _sun;
        public CloudField Clouds { get; } = new CloudField();
        private readonly CloudWeather _weather = new CloudWeather();
        private SkyTemporalFilter _skyFilter;
        private Material _filterSource;
        private float _filterAt;
        private readonly CloudWindModel _wind = new CloudWindModel();
        private readonly TemperatureModel _temperature = new TemperatureModel();
        public float Temperature => _temperature.Current;
        public float TemperatureMinimum => _temperature.Minimum;
        public float TemperatureMaximum => _temperature.Maximum;
        public float CumulusMix { get; private set; }
        public float SummerCloudFactor { get; private set; }
        public float CloudDevelopment { get; private set; }
        public bool Playing { get; set; }
        public void SetHour(float hour) { Hour = Mathf.Repeat(hour, Settings.N("clock.hoursPerDay")); TickEnvironment(); }
        public void SetDate(int year, int month, int day, float hour) { Year = year; Month = month; Day = day; SetHour(hour); }
        public string Error { get; private set; }
        private Transform _map;
        private Material _sky, _oldSky;
        private Light _sun, _moon, _oldSun;
        private readonly Dictionary<Light, bool> _oldLights = new Dictionary<Light, bool>();
        private AmbientMode _oldAmbientMode;
        private Color _oldAmbientSky, _oldAmbientEquator, _oldAmbientGround, _oldFogColor;
        private bool _oldFog;
        private FogMode _oldFogMode;
        private float _oldFogDensity, _oldReflectionIntensity, _oldAmbientIntensity, _oldShadowDistance;
        private DefaultReflectionMode _oldReflectionMode;
        private Cubemap _oldReflection;
        private ReflectionProbe _probe;
        private float _reflectionAt, _elapsed;
        private bool _saved;

        public void Initialize(Transform map, Parameters settings)
        {
            _map = map;
            _oldSky = RenderSettings.skybox; _oldSun = RenderSettings.sun;
            _oldAmbientMode = RenderSettings.ambientMode;
            _oldAmbientSky = RenderSettings.ambientSkyColor; _oldAmbientEquator = RenderSettings.ambientEquatorColor; _oldAmbientGround = RenderSettings.ambientGroundColor;
            _oldFog = RenderSettings.fog; _oldFogMode = RenderSettings.fogMode; _oldFogDensity = RenderSettings.fogDensity; _oldFogColor = RenderSettings.fogColor;
            _oldReflection = RenderSettings.customReflection; _oldReflectionMode = RenderSettings.defaultReflectionMode;
            _oldReflectionIntensity = RenderSettings.reflectionIntensity; _oldAmbientIntensity = RenderSettings.ambientIntensity;
            _oldShadowDistance = QualitySettings.shadowDistance;
            _saved = true;
            foreach (var light in FindObjectsOfType<Light>())
                if (light.type == LightType.Directional) { _oldLights[light] = light.enabled; light.enabled = false; }
            var carrier = map.Find(settings.T("content.carrier"));
            if (carrier == null) throw new InvalidOperationException("Sky material carrier missing");
            _sky = new Material(carrier.GetComponent<Renderer>().sharedMaterial);
            if (!_sky.shader.isSupported) throw new InvalidOperationException("Sky shader unsupported");
            var filterCarrier = map.Find(settings.T("content.temporalCarrier"));
            if (filterCarrier == null) throw new InvalidOperationException("Sky temporal material carrier missing");
            _filterSource = filterCarrier.GetComponent<Renderer>().sharedMaterial;
            _sun = CreateLight("Sandbox Sun"); _moon = CreateLight("Sandbox Moon");
            var reflection = new GameObject("Sandbox Environment Reflection");
            reflection.transform.SetParent(transform, false);
            _probe = reflection.AddComponent<ReflectionProbe>();
            _probe.mode = ReflectionProbeMode.Realtime;
            _probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            _probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            _probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            _probe.cullingMask = default(int);
            Apply(settings);
        }
        private Light CreateLight(string name)
        {
            var node = new GameObject(name); node.transform.SetParent(transform, false);
            var light = node.AddComponent<Light>(); light.type = LightType.Directional; light.shadows = LightShadows.Soft;
            return light;
        }
        public void Apply(Parameters settings)
        {
            settings.Validate();
            if(_filterSource!=null && (_filterSource.GetTexture("_NoiseTex").width!=settings.I("cloud.noise.baseResolution") || _filterSource.GetTexture("_DetailTex").width!=settings.I("cloud.noise.detailResolution")))
                throw new InvalidOperationException("cloud.noise.baseResolution / detailResolution require rebuilding the shared bundle");
            Settings = settings;
            var island = FindObjectOfType<SandboxIslandLoader>(); if (island != null) island.ApplySettings(settings);
            _wind.Configure(settings);
            _temperature.Configure(settings);
            Year = settings.I("calendar.year"); Month = settings.I("calendar.month"); Day = settings.I("calendar.day");
            Hour = settings.N("clock.hours"); Playing = settings.B("clock.playing");
            foreach (var item in settings.numbers)
                if (!string.IsNullOrEmpty(item.shader)) _sky.SetFloat(item.shader, item.value);
            foreach (var geometry in settings.geometry)
            {
                var child = _map.Find(geometry.name);
                if (child == null) throw new InvalidOperationException("Map object missing: " + geometry.name);
                string prefix = "map." + geometry.name;
                child.localPosition = settings.V(prefix + ".position"); child.localScale = settings.V(prefix + ".scale");
                var color = settings.V(prefix + ".color");
                child.GetComponent<Renderer>().material.color = new Color(color.x, color.y, color.z, settings.N("math.one"));
            }
            _probe.resolution = settings.I("light.reflectionResolution");
            QualitySettings.shadowDistance = settings.N("light.shadowDistance");
            _sun.shadowCustomResolution = settings.I("light.shadowResolution"); _moon.shadowCustomResolution = settings.I("light.shadowResolution");
            _reflectionAt = settings.N("math.zero");
            TickWeather(settings.N("math.zero"));
            TickEnvironment();
            Error = null;
        }
        private void Update()
        {
            if (Settings == null) return;
            _elapsed += Time.unscaledDeltaTime;
            if (Playing)
            {
                Hour += Time.unscaledDeltaTime * Settings.N("clock.hoursPerDay") /
                    (Settings.N("clock.dayMinutes") * Settings.N("clock.secondsPerMinute"));
                while (Hour >= Settings.N("clock.hoursPerDay"))
                {
                    Hour -= Settings.N("clock.hoursPerDay"); Day++;
                    if (Day > CalendarClock.DaysInMonth(Year, Month, Settings)) { Day = Settings.I("math.one"); Month++; }
                    if (Month > Settings.I("calendar.months")) { Month = Settings.I("math.one"); Year++; }
                    if (Year > Settings.I("calendar.maxYear")) Year = Settings.I("math.one");
                }
            }
            if (Playing) TickWeather(Time.unscaledDeltaTime);
            TickEnvironment();
            if (_elapsed >= _filterAt)
            {
                _filterAt = _elapsed + Settings.N("cloud.temporal.scanSeconds");
                var camera = Camera.main;
                BindCamera(camera);
            }
        }
        private void TickWeather(float delta)
        {
            _weather.Evaluate(CalendarClock.AbsoluteSeconds(Year,Month,Day,Hour,Settings),Settings);
            Weather=_weather.Current;NextWeather=_weather.Next;CloudCoverage=_weather.Coverage;CloudDensity=_weather.Density;
        }
        private void TickEnvironment()
        {
            float zero = Settings.N("math.zero"), one = Settings.N("math.one");
            TickWeather(zero);
            _temperature.Update(Settings, Year, Month, Day, Hour);
            CumulusMix = SeasonalClimate.MonthValue(Settings, "cloud.cumulusMix.", Year, Month, Day, Hour);
            SummerCloudFactor = SeasonalClimate.MonthValue(Settings, "cloud.summer.", Year, Month, Day, Hour);
            // 時刻角は一周、緯度・赤緯はJSONの角度換算係数を使う。
            float radians = Settings.N("math.pi") / Settings.N("math.degreesHalfTurn");
            int dayOfYear = CalendarClock.DayOfYear(Year, Month, Day, Settings);
            Declination = Settings.N("clock.declination") + Settings.N("calendar.axialTilt") * Mathf.Sin(Settings.N("math.tau") *
                (dayOfYear - Settings.N("calendar.equinoxDay")) / CalendarClock.DaysInYear(Year, Settings));
            float latitude = Settings.N("clock.latitude") * radians, declination = Declination * radians;
            float angle = (Hour / Settings.N("clock.hoursPerDay") - Settings.N("math.half")) * Settings.N("math.tau");
            var direction = new Vector3(-Mathf.Cos(declination) * Mathf.Sin(angle),
                Mathf.Sin(latitude) * Mathf.Sin(declination) + Mathf.Cos(latitude) * Mathf.Cos(declination) * Mathf.Cos(angle),
                Mathf.Cos(latitude) * Mathf.Sin(declination) - Mathf.Sin(latitude) * Mathf.Cos(declination) * Mathf.Cos(angle));
            direction = Quaternion.AngleAxis(Settings.N("clock.rotationOffset"), Vector3.up) * direction;
            CloudDevelopment = Mathf.Lerp(Settings.N("cloud.cumulus.nightGrowth"), one,
                Mathf.SmoothStep(zero, one, Mathf.Clamp01(direction.y / Settings.N("cloud.cumulus.sunGrowthHeight"))));
            _sky.SetFloat("_CumulusMix", CumulusMix); _sky.SetFloat("_SummerFactor", SummerCloudFactor); _sky.SetFloat("_CloudDevelopment", CloudDevelopment);
            float daylight = Mathf.SmoothStep(zero, one, Mathf.InverseLerp(-Settings.N("sky.twilightDepth"), Settings.N("sky.twilightHigh"), direction.y));
            _sun.transform.rotation = Quaternion.LookRotation(-direction);
            double secondsPerDay = Settings.N("clock.hoursPerDay") * Settings.N("calendar.minutesPerHour") * Settings.N("clock.secondsPerMinute");
            double moonEpoch = CalendarClock.AbsoluteSeconds(Settings.I("moon.epochYear"), Settings.I("moon.epochMonth"), Settings.I("moon.epochDay"), Settings.N("moon.epochHour"), Settings);
            double lunarDays = (CalendarClock.AbsoluteSeconds(Year, Month, Day, Hour, Settings) - moonEpoch) / secondsPerDay;
            double cycle = lunarDays / Settings.N("sky.moonPeriod") + Settings.N("sky.moonPhase");
            float lunarCycle = (float)(cycle - Math.Floor(cycle));
            var lunarPole = Quaternion.AngleAxis(Settings.N("clock.rotationOffset"), Vector3.up) * new Vector3(zero, Mathf.Sin(latitude), Mathf.Cos(latitude));
            var lunarSide = Vector3.Cross(lunarPole, direction).normalized;
            float lunarAngle = lunarCycle * Settings.N("math.tau");
            var moonDirection = (direction * Mathf.Cos(lunarAngle) + lunarSide * Mathf.Sin(lunarAngle)).normalized;
            SunDirection = direction; MoonDirection = moonDirection;
            MoonIllumination = Mathf.Clamp01((one - Vector3.Dot(direction, moonDirection)) * Settings.N("math.half"));
            _moon.transform.rotation = Quaternion.LookRotation(-moonDirection);
            var sunColor = new Color(one, Mathf.Lerp(Settings.N("light.sunsetGreen"), one, daylight), Mathf.Lerp(Settings.N("light.sunsetBlue"), one, daylight), one);
            _sun.color = sunColor; _sun.intensity = Mathf.Max(zero, direction.y) * Settings.N("light.sunIntensity") *
                (one - Mathf.Clamp01(CloudCoverage) * Settings.N("weather.sunOcclusion"));
            _moon.color = new Color(Settings.N("light.moonRed"), Settings.N("light.moonGreen"), one, one);
            float moonAltitude = Mathf.SmoothStep(zero, one, Mathf.InverseLerp(Settings.N("light.moonAltitudeMin"), Settings.N("light.moonAltitudeFull"), moonDirection.y));
            _moon.intensity = (one - daylight) * Settings.N("light.moonIntensity") * Mathf.Pow(MoonIllumination, Settings.N("light.moonPhasePower")) * moonAltitude;
            _sky.SetVector("_SunDirection", direction); _sky.SetFloat("_Elapsed", _elapsed); _sky.SetFloat("_GroundHeight", _map.position.y);
            double cloudSeconds = CalendarClock.AbsoluteSeconds(Year, Month, Day, Hour, Settings) * Settings.N("cloud.timeScale");
            CloudAdvection = _wind.Evaluate(cloudSeconds, Settings);
            Clouds.Update(this,_wind.Travel);
            _sky.SetVector("_CloudAdvection", CloudAdvection);
            CloudFormation = (float)(cloudSeconds * Settings.N("cloud.formationRate") % Settings.N("cloud.evolutionWrap"));
            _sky.SetFloat("_CloudFormation", CloudFormation);
            _sky.SetFloat("_CloudSamplePhase", Settings.N("cloud.temporal.enabled") > zero ? Time.frameCount % Settings.I("cloud.sampling.phaseCycle") : zero);
            _sky.SetVector("_MoonDirection", moonDirection);
            _sky.SetFloat("_MoonPhase", (one - Mathf.Cos(lunarCycle * Settings.N("math.tau"))) * Settings.N("math.half"));
            _sky.SetFloat("_Coverage", Mathf.Clamp01(CloudCoverage)); _sky.SetFloat("_Density", CloudDensity);
            RenderSettings.skybox = _sky; RenderSettings.sun = _sun;
            _probe.size = Vector3.one * Settings.N("cloud.maxDistance");
            RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientIntensity = one;
            var day = new Color(Settings.N("light.ambientRed"), Settings.N("light.ambientGreen"), one, one);
            float nightPhase = Mathf.Lerp(Settings.N("light.newMoonAmbientRatio"), one, Mathf.Pow(MoonIllumination, Settings.N("light.nightPhasePower")));
            _sky.SetFloat("_NightSkyGain", Mathf.Lerp(Settings.N("sky.newMoonGain"), Settings.N("sky.fullMoonGain"), Mathf.Pow(MoonIllumination, Settings.N("light.nightPhasePower"))));
            NightAmbient = new Color(Settings.N("light.nightRed"), Settings.N("light.nightGreen"), Settings.N("light.nightBlue"), one) * (Settings.N("light.ambientNight") * nightPhase);
            _sky.SetColor("_NightAmbient", NightAmbient);
            var ambient = Color.Lerp(NightAmbient, day * Settings.N("light.ambientDay"), daylight);
            RenderSettings.ambientSkyColor = ambient;
            RenderSettings.ambientEquatorColor = Color.Lerp(ambient, Color.Lerp(NightAmbient, sunColor * ambient.maxColorComponent, daylight), Settings.N("light.equatorBlend"));
            RenderSettings.ambientGroundColor = ambient * Settings.N("light.groundBounce");
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = Settings.N("light.fogDensity"); RenderSettings.fogColor = RenderSettings.ambientEquatorColor;
            if (_elapsed >= _reflectionAt)
            {
                _probe.transform.position = _map.position + Vector3.up * Settings.N("render.reflectionHeight");
                _probe.RenderProbe();
                _reflectionAt = _elapsed + Settings.N("light.reflectionInterval");
            }
            if (_probe.texture != null)
            {
                Shader.SetGlobalTexture("_SandboxEnvironmentReflection", _probe.texture);
            }
        }
        private void OnDestroy()
        {
            if (_skyFilter != null) Destroy(_skyFilter);
            if (_saved)
            {
                RenderSettings.skybox = _oldSky; RenderSettings.sun = _oldSun;
                RenderSettings.ambientMode = _oldAmbientMode; RenderSettings.ambientIntensity = _oldAmbientIntensity;
                RenderSettings.ambientSkyColor = _oldAmbientSky; RenderSettings.ambientEquatorColor = _oldAmbientEquator; RenderSettings.ambientGroundColor = _oldAmbientGround;
                RenderSettings.fog = _oldFog; RenderSettings.fogMode = _oldFogMode; RenderSettings.fogDensity = _oldFogDensity; RenderSettings.fogColor = _oldFogColor;
                RenderSettings.defaultReflectionMode = _oldReflectionMode; RenderSettings.customReflection = _oldReflection; RenderSettings.reflectionIntensity = _oldReflectionIntensity;
                QualitySettings.shadowDistance = _oldShadowDistance;
                foreach (var pair in _oldLights) if (pair.Key != null) pair.Key.enabled = pair.Value;
            }
            if (_sky != null) Destroy(_sky);
            if(_sun!=null) Destroy(_sun.gameObject);
            if(_moon!=null) Destroy(_moon.gameObject);
            if(_probe!=null) Destroy(_probe.gameObject);
        }
    }
}
