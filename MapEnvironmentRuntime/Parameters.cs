using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MapEnvironmentRuntime
{
    public sealed class NumberSetting
    {
        public string key, unit, description, shader;
        public float value, initial, min, max;
        public bool randomize;
        public float randomMin, randomMax;
    }
    public sealed class TextSetting { public string key, value; }
    public sealed class GeometrySetting { public string name, primitive; public bool collider; }
    public sealed class Parameters
    {
        public string revision, auditReceipt;
        public NumberSetting[] numbers;
        public TextSetting[] text;
        public GeometrySetting[] geometry;
        public string[] auditRoots, auditExtensions, auditIgnore;
        public string[] cloudTypes;
        [NonSerialized] private Dictionary<string, NumberSetting> _numericIndex;
        [NonSerialized] private Dictionary<string, TextSetting> _textIndex;
        public float N(string key) { if (_numericIndex == null) _numericIndex = numbers.ToDictionary(item => item.key); if (!_numericIndex.TryGetValue(key, out var item)) throw new InvalidDataException("Missing numeric parameter: " + key); return item.value; }
        public int I(string key) => checked((int)N(key));
        public string T(string key) { if (_textIndex == null) _textIndex = text.ToDictionary(item => item.key); return _textIndex[key].value; }
        public bool B(string key) => bool.Parse(T(key));
        public Vector3 V(string prefix) => new Vector3(N(prefix + ".x"), N(prefix + ".y"), N(prefix + ".z"));
        public Parameters Copy() => JsonCodec.FromJson<Parameters>(JsonCodec.ToJson(this));
        public string Json() => JsonCodec.ToJson(this, true);
        public static Parameters Load(string path)
        {
            var value = JsonCodec.FromJson<Parameters>(File.ReadAllText(path));
            if (value == null) throw new InvalidDataException("parameters.json is empty");
            value.Validate();
            return value;
        }
        public void ValidateDocument()
        {
            if (numbers == null || text == null || geometry == null || string.IsNullOrEmpty(revision))
                throw new InvalidDataException("Incomplete parameter document: numbers=" + (numbers != null) + ", text=" + (text != null) + ", geometry=" + (geometry != null) + ", revision=" + !string.IsNullOrEmpty(revision));
        }
        public void Validate()
        {
            ValidateDocument();
            var keys = new HashSet<string>();
            foreach (var item in numbers)
            {
                if (string.IsNullOrEmpty(item.key) || !keys.Add(item.key) || string.IsNullOrEmpty(item.description)
                    || float.IsNaN(item.value) || float.IsInfinity(item.value) || item.min > item.max
                    || item.value < item.min || item.value > item.max || item.initial < item.min || item.initial > item.max)
                    throw new InvalidDataException("Invalid parameter: " + item.key);
                if (item.randomize && (float.IsNaN(item.randomMin) || float.IsNaN(item.randomMax)
                    || item.randomMin < item.min || item.randomMax > item.max || item.randomMin > item.randomMax))
                    throw new InvalidDataException("Invalid random range: " + item.key);
            }
            keys.Clear();
            foreach (var item in text)
                if (string.IsNullOrEmpty(item.key) || item.value == null || !keys.Add(item.key)) throw new InvalidDataException("Invalid text parameter: " + item.key);
            if (N("math.zero") != default(float) || N("math.one") != Mathf.Sign(N("math.one"))) throw new InvalidDataException("Invalid arithmetic identities");
            if (N("clock.hours") >= N("clock.hoursPerDay")) throw new InvalidDataException("clock.hours must be less than clock.hoursPerDay");
            foreach (string key in new[] { "island.visibility.rayBudget", "island.visibility.scanBudget", "island.visibility.hitCapacity", "island.visibility.confirmations" })
                if (N(key) != Mathf.Floor(N(key))) throw new InvalidDataException("Integer required: " + key);
            foreach (var key in new[] { "calendar.year", "calendar.month", "calendar.day" })
                if (N(key) != Mathf.Floor(N(key))) throw new InvalidDataException("Integer required: " + key);
            if (I("calendar.day") > CalendarClock.DaysInMonth(I("calendar.year"), I("calendar.month"), this)) throw new InvalidDataException("存在しない月日です。");
            if (N("weather.holdMax") < N("weather.holdMin")) throw new InvalidDataException("weather.holdMax must be >= holdMin");
            if (T("movement.mode") != "free" && T("movement.mode") != "walk") throw new InvalidDataException("movement.mode must be free or walk");
            if (N("movement.bodyHeight") < N("movement.bodyRadius") * N("math.two")) throw new InvalidDataException("movement.bodyHeight must fit bodyRadius");
            if (N("movement.crouchHeight") < N("movement.bodyRadius") * N("math.two") || N("movement.crouchHeight") >= N("movement.bodyHeight")) throw new InvalidDataException("movement.crouchHeight must fit bodyRadius and be lower than bodyHeight");
            if (N("movement.standHitCapacity") != Mathf.Floor(N("movement.standHitCapacity"))) throw new InvalidDataException("movement.standHitCapacity requires an integer");
            if (N("wind.holdMaxHours") < N("wind.holdMinHours") || N("wind.transitionHours") > N("wind.holdMinHours")) throw new InvalidDataException("Invalid wind hold/transition durations");
            if (N("wind.normalWeight") + N("wind.calmWeight") + N("wind.strongWeight") <= N("math.zero")) throw new InvalidDataException("Wind weights must have a positive total");
            foreach (string state in new[] { "normal", "calm", "strong" })
                if (N("wind." + state + "Min") > N("wind." + state + "Max")) throw new InvalidDataException("Invalid wind range: " + state);
            if (N("wind.segments") != Mathf.Floor(N("wind.segments")) || N("wind.seed") != Mathf.Floor(N("wind.seed"))) throw new InvalidDataException("Wind seed and segments require integers");
            foreach (string key in new[] { "moon.epochYear", "moon.epochMonth", "moon.epochDay" })
                if (N(key) != Mathf.Floor(N(key))) throw new InvalidDataException("Integer required: " + key);
            if (I("moon.epochDay") > CalendarClock.DaysInMonth(I("moon.epochYear"), I("moon.epochMonth"), this)) throw new InvalidDataException("Invalid moon epoch date");
            for (int month = I("math.one"); month <= I("calendar.months"); month++)
            {
                string suffix = month.ToString(CultureInfo.InvariantCulture);
                if (!(N("temperature.low." + suffix) < N("temperature.mean." + suffix) && N("temperature.mean." + suffix) < N("temperature.high." + suffix)))
                    throw new InvalidDataException("temperature.low/mean/high must be ordered for month " + suffix);
                N("cloud.cumulusMix." + suffix); N("cloud.summer." + suffix);
            }
            foreach (var pair in new[] { new[] { "temperature.changeMinDays", "temperature.changeMaxDays" }, new[] { "temperature.powerMin", "temperature.powerMax" },
                new[] { "cloud.cumulus.widthMin", "cloud.cumulus.widthMax" }, new[] { "cloud.cumulus.heightMin", "cloud.cumulus.heightMax" },
                new[] { "cloud.cumulus.towerMin", "cloud.cumulus.towerMax" }, new[] { "cloud.cumulus.lobeRadiusMin", "cloud.cumulus.lobeRadiusMax" },
                new[] { "cloud.cumulus.lobeHeightMin", "cloud.cumulus.lobeHeightMax" }, new[] { "cloud.cumulus.lifeStart", "cloud.cumulus.lifeFull" } })
                if (N(pair.First()) >= N(pair.Last())) throw new InvalidDataException("Invalid ordered range: " + string.Join(" / ", pair));
            float cloudRadius = Mathf.Max(N("cloud.cumulus.mainRadius"), N("cloud.cumulus.lobeSpread") + N("cloud.cumulus.lobeRadiusMax")) * N("cloud.cumulus.widthMax");
            if (cloudRadius + N("cloud.cumulus.cellSize") * N("cloud.cumulus.jitter") * N("math.half") >= N("cloud.cumulus.cellSize") * N("math.half"))
                throw new InvalidDataException("cloud.cumulus.cellSize too small for width, lobes and jitter");
            if (N("cloud.cumulus.lobeHeightMax") + N("cloud.cumulus.lobeVerticalRadius") > N("math.one")) throw new InvalidDataException("Cumulus lobe height exceeds cloud layer");
            if (N("light.moonAltitudeFull") <= N("light.moonAltitudeMin")) throw new InvalidDataException("light.moonAltitudeFull must exceed moonAltitudeMin");
            foreach (string key in new[] { "cloud.sampling.lowerWeight", "cloud.sampling.filterStrength", "cloud.cumulus.candidateFeather" })
                if (N(key) <= N("math.zero")) throw new InvalidDataException("Positive value required: " + key);
            N("cloud.sampling.spread");
            foreach (string key in new[] { "cloud.temporal.enabled", "cloud.temporal.historyWeight", "cloud.temporal.depthThreshold", "cloud.temporal.clampExpansion", "cloud.temporal.resetDistance", "cloud.temporal.resetAngle", "cloud.temporal.resetSeconds", "cloud.temporal.scanSeconds", "cloud.sampling.phaseCycle" }) N(key);
            if (N("cloud.sampling.phaseCycle") != Mathf.Floor(N("cloud.sampling.phaseCycle"))) throw new InvalidDataException("Integer required: cloud.sampling.phaseCycle");
            T("content.temporalCarrier");
            if (cloudTypes == null || cloudTypes.Length != I("cloud.typeCount")) throw new InvalidDataException("Invalid cloudTypes");
            if (!new[] { "automatic", "season", "type" }.Contains(T("cloud.mode"))) throw new InvalidDataException("cloud.mode: automatic / season / type");
            if (!new[] { "spring", "summer", "autumn", "winter" }.Contains(T("cloud.fixedSeason"))) throw new InvalidDataException("Invalid cloud.fixedSeason");
            if (!cloudTypes.Contains(T("cloud.fixedType"))) throw new InvalidDataException("Invalid cloud.fixedType");
            if (!new[] { "low", "standard", "high" }.Contains(T("cloud.quality"))) throw new InvalidDataException("Invalid cloud.quality");
            if (!new[] { "normal", "static", "passthrough", "single" }.Contains(T("cloud.debug"))) throw new InvalidDataException("Invalid cloud.debug");
            foreach (string type in cloudTypes)
            {
                string prefix = "cloud.type." + type + ".";
                foreach (string name in new[] { "base", "width", "height" })
                    if (N(prefix + name + "Min") > N(prefix + name + "Max")) throw new InvalidDataException("Invalid range: " + prefix + name);
                if (N(prefix + "widthMax") * (N("math.one") + N("cloud.shape.jitter")) >= N(prefix + "cell")) throw new InvalidDataException("Cloud width overlaps its cell boundary: " + type);
                if (N(prefix + "lifetime") <= N("math.zero")) throw new InvalidDataException("Cloud lifetime must be positive: " + type);
            }
            foreach (string key in new[] { "cloud.steps", "cloud.shadow.steps", "cloud.shadow.resolution", "cloud.noise.baseResolution", "cloud.noise.detailResolution", "cloud.test.sequenceFrames", "cloud.test.maximumSequenceFrames" })
                if (N(key) != Mathf.Floor(N(key))) throw new InvalidDataException("Integer required: " + key);
            foreach (string key in new[] { "cloud.noise.baseResolution", "cloud.noise.detailResolution", "cloud.shadow.resolution" })
                if (!Mathf.IsPowerOfTwo(I(key))) throw new InvalidDataException("Power of two required: " + key);
            foreach (string key in new[] { "temperature.seed", "temperature.solveIterations", "temperature.integrationSteps", "temperature.dailySamples", "temperature.variationSegments", "cloud.cumulus.lobes" })
                if (N(key) != Mathf.Floor(N(key))) throw new InvalidDataException("Integer required: " + key);
            foreach (var key in new[] { "cloud.steps", "cloud.lightSteps", "noise.octaves", "render.width", "render.height", "render.aa", "render.vsync", "capture.superSize", "light.reflectionResolution", "light.shadowResolution", "api.port" })
                if (N(key) != Mathf.Floor(N(key))) throw new InvalidDataException("Integer required: " + key);
            foreach (var key in new[] { "light.reflectionResolution", "light.shadowResolution" })
                if (!Mathf.IsPowerOfTwo(I(key))) throw new InvalidDataException("Power of two required: " + key);
            if (I("render.aa") != default(int) && !Mathf.IsPowerOfTwo(I("render.aa"))) throw new InvalidDataException("Invalid render.aa");
            foreach (var item in geometry)
            {
                if (!Enum.TryParse(item.primitive, out PrimitiveType primitive)) throw new InvalidDataException("Unknown primitive: " + item.primitive);
                V("map." + item.name + ".position"); V("map." + item.name + ".scale"); V("map." + item.name + ".color");
            }
        }
        public static void AtomicWrite(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string pending = path + ".pending";
            File.WriteAllText(pending, content, new System.Text.UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(pending, path, null);
            else File.Move(pending, path);
        }
        public static float ParseNumber(string value) => float.Parse(value, CultureInfo.InvariantCulture);
    }
}
