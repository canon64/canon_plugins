using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    internal sealed class HotelRoomLightingRuntime : IDisposable
    {
        private readonly Dictionary<Light, float> _lights = new Dictionary<Light, float>();
        private readonly Dictionary<Light, Color> _lightColors = new Dictionary<Light, Color>();
        private sealed class Source
        {
            internal string label;
            internal HotelRoomLightSource state;
            internal Light light;
            internal Material material;
            internal float emissionPeak;
        }
        private readonly List<Source> _sources = new List<Source>();
        private Source _selected;
        private Vector2 _sourceScroll;
        private readonly Dictionary<string, float> _inputMax = new Dictionary<string, float>();
        private readonly Dictionary<Material, Color> _emissions = new Dictionary<Material, Color>();
        private readonly List<Tuple<object, MemberInfo, float>> _bloom = new List<Tuple<object, MemberInfo, float>>();
        private readonly HotelRoomControlSettings _cfg;
        private readonly Action<HotelRoomLighting> _save;
        private readonly Dictionary<string, string> _inputs = new Dictionary<string, string>();
        private string _status;
        internal HotelRoomLighting State { get; }
        internal HotelRoomLightingRuntime(Transform room, Camera camera, HotelRoomControlSettings cfg, HotelRoomLighting saved, Action<HotelRoomLighting> save, bool addLegacyFixtures = true)
        {
            _cfg = cfg; _save = save;
            if (addLegacyFixtures) { AddCeilingLamp(room, cfg); AddCeilingNeon(room, cfg); }
            foreach (var light in room.GetComponentsInChildren<Light>(true)) { _lights[light] = light.intensity; _lightColors[light] = light.color; }
            foreach (var material in room.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct())
                if (material.HasProperty("_EmissionColor")) _emissions[material] = material.GetColor("_EmissionColor");
            if (camera != null)
                foreach (var component in camera.GetComponents<MonoBehaviour>().Where(c => c != null && c.GetType().Name.IndexOf("Bloom", StringComparison.OrdinalIgnoreCase) >= default(int)))
                    foreach (string name in new[] { "bloomIntensity", "intensity", "Intensity" })
                    {
                        var field = component.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
                        if (field?.FieldType == typeof(float)) { _bloom.Add(Tuple.Create((object)component, (MemberInfo)field, (float)field.GetValue(component))); break; }
                        var property = component.GetType().GetProperty(name);
                        if (property?.PropertyType == typeof(float) && property.CanRead && property.CanWrite) { _bloom.Add(Tuple.Create((object)component, (MemberInfo)property, (float)property.GetValue(component, null))); break; }
                    }
            State = saved?.Copy() ?? new HotelRoomLighting { light = addLegacyFixtures ? cfg.lightDefault : cfg.colorMax,
                emission = addLegacyFixtures ? cfg.emissionDefault : cfg.colorMax, bloom = cfg.bloomDefault };
            var entries = (State.sources ?? Array.Empty<HotelRoomLightSource>()).ToList();
            foreach (var pair in _lights)
                AddSource(entries, "light/" + pair.Key.name, Label(pair.Key.name), pair.Key.color, pair.Key, null);
            foreach (var pair in _emissions.Where(pair => pair.Value.maxColorComponent > default(float)))
                AddSource(entries, "emission/" + pair.Key.name, "発光面：" + Label(pair.Key.name), pair.Value, null, pair.Key);
            State.sources = entries.ToArray(); _selected = _sources.FirstOrDefault();
            Apply();
        }
        private static string Label(string name)
        {
            foreach(var pair in new[]{
                Tuple.Create("CeilingSoft","天井の主照明"),Tuple.Create("CeilingCoveBounce","天井の間接照明"),
                Tuple.Create("CeilingDownlight","天井ダウンライト"),Tuple.Create("LiquorShelfLight","酒棚の照明"),
                Tuple.Create("WallSconceLight","壁灯"),Tuple.Create("ChandelierLight","シャンデリア"),
                Tuple.Create("Bedside_","ベッド脇の照明"),Tuple.Create("ReceptionFill","接客席の補助照明"),
                Tuple.Create("BedFill","ベッドの補助照明"),Tuple.Create("SuitePurpleAccent","紫の間接照明"),Tuple.Create("WindowMoon","窓外の月明かり")})
                if(name.StartsWith(pair.Item1,StringComparison.Ordinal))return pair.Item2+name.Substring(pair.Item1.Length);
            switch (name)
            {
                case "HotelCeilingLamp": case "Hotel ceiling lamp": return "天井灯";
                case "PinkCeilingBounce": return "天井のピンク照明";
                case "SoftEntryFill": return "入口の補助灯";
                case "HeadboardPinkWash": return "ベッド奥の照明";
                case "BedsidePinkGlow": return "ベッド横の照明・左";
                case "BedsidePinkGlow.001": return "ベッド横の照明・右";
                case "Pink neon tube": return "既存ネオン";
                case "Warm pink lamps": return "ベッド横ランプ";
                case "Hotel ceiling neon": return "天井の四角いネオン";
                default: return name;
            }
        }
        private void AddSource(List<HotelRoomLightSource> entries, string key, string label, Color color, Light light, Material material)
        {
            float peak = material == null ? _cfg.colorMax : color.maxColorComponent;
            var state = entries.SingleOrDefault(entry => entry.key == key);
            if (state == null) { state = new HotelRoomLightSource { key = key, r = color.r / peak, g = color.g / peak, b = color.b / peak, strength = _cfg.colorMax }; entries.Add(state); }
            _sources.Add(new Source { label = label, state = state, light = light, material = material, emissionPeak = peak });
        }
        private static void Write(object owner, MemberInfo member, float value)
        { if (member is FieldInfo field) field.SetValue(owner, value); else ((PropertyInfo)member).SetValue(owner, value, null); }
        private void Apply()
        {
            State.Validate();
            foreach (var pair in _lights) if (pair.Key != null) pair.Key.intensity = pair.Value * State.light;
            foreach (var pair in _emissions) if (pair.Key != null) pair.Key.SetColor("_EmissionColor", pair.Value * State.emission);
            foreach (var source in _sources)
            {
                var state = source.state;
                var color = new Color(state.r, state.g, state.b, _cfg.colorMax);
                if (source.light != null) { source.light.color = color; source.light.intensity = _lights[source.light] * State.light * state.strength; }
                if (source.material != null) source.material.SetColor("_EmissionColor", color * source.emissionPeak * State.emission * state.strength);
            }
            foreach (var value in _bloom) Write(value.Item1, value.Item2, value.Item3 * State.bloom);
        }
        internal void Draw()
        {
            GUILayout.BeginVertical(GUI.skin.box); GUILayout.Label("客室の照明・発光");
            float light = Slider("照明", State.light), emission = Slider("ネオン・ランプの発光", State.emission);
            float bloom = _bloom.Any() ? Slider("光のにじみ", State.bloom) : State.bloom;
            if (light != State.light || emission != State.emission || bloom != State.bloom)
            { State.light = light; State.emission = emission; State.bloom = bloom; Apply(); }
            if (!_bloom.Any()) GUILayout.Label("現在のカメラでは、発光量を下げて光のにじみを調整します。");
            GUILayout.Label("個別の照明・色を選択");
            _sourceScroll = GUILayout.BeginScrollView(_sourceScroll, GUILayout.Height(_cfg.lightListHeight));
            foreach (var source in _sources)
                if (GUILayout.Button((_selected == source ? "● " : "○ ") + source.label, GUILayout.ExpandWidth(false))) _selected = source;
            GUILayout.EndScrollView();
            if (_selected != null)
            {
                var state = _selected.state;
                GUILayout.Label(_selected.label);
                float r = Slider("赤 R", state.r, _cfg.colorMax, state.key), g = Slider("緑 G", state.g, _cfg.colorMax, state.key), b = Slider("青 B", state.b, _cfg.colorMax, state.key);
                float strength = Slider("個別の明るさ", state.strength, _cfg.brightnessMax, state.key);
                if (r != state.r || g != state.g || b != state.b || strength != state.strength)
                { state.r = r; state.g = g; state.b = b; state.strength = strength; Apply(); }
            }
            if (GUILayout.Button("照明・発光を保存", GUILayout.ExpandWidth(false)))
            {
                try { Save(); _status = "照明・発光を保存しました。"; }
                catch (Exception ex) { _status = ex.GetBaseException().Message; }
            }
            if (!string.IsNullOrWhiteSpace(_status)) GUILayout.Label(_status);
            GUILayout.EndVertical();
        }
        private float Slider(string label, float value, float maximum = default(float), string source = null)
        {
            if (maximum == default(float)) maximum = _cfg.brightnessMax;
            string key = (source ?? "global") + "/" + label;
            _inputMax[key] = maximum;
            GUILayout.BeginHorizontal(); GUILayout.Label(label, GUILayout.ExpandWidth(false));
            float next = GUILayout.HorizontalSlider(value, default(float), maximum);
            if (!_inputs.ContainsKey(key) || next != value) _inputs[key] = next.ToString("0.######", CultureInfo.InvariantCulture);
            GUI.SetNextControlName("MainGameAiShoujoMode.HotelPlacement.Lighting." + key);
            string text = GUILayout.TextField(_inputs[key], GUILayout.Width(_cfg.fieldWidth));
            if (text != _inputs[key])
            {
                _inputs[key] = text;
                if (TryBrightness(text, maximum, out float typed)) next = typed;
            }
            GUILayout.EndHorizontal(); return next;
        }
        private bool TryBrightness(string text, float maximum, out float value) => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && !float.IsNaN(value) && !float.IsInfinity(value) && value >= default(float) && value <= maximum;
        internal void Save()
        {
            if (_inputs.Any(pair => !TryBrightness(pair.Value, _inputMax[pair.Key], out _))) throw new InvalidOperationException("照明の入力を有効な範囲の数値にしてから保存してください。");
            _save(State.Copy());
        }
        internal bool MatchesApplied(HotelRoomLighting expected)
        {
            if (!Mathf.Approximately(State.light, expected.light) || !Mathf.Approximately(State.emission, expected.emission) || !Mathf.Approximately(State.bloom, expected.bloom)) return false;
            foreach (var saved in expected.sources ?? Array.Empty<HotelRoomLightSource>())
                if (!_sources.Any(source => source.state.key == saved.key)) throw new InvalidOperationException("保存した照明の適用先がありません：" + saved.key);
            foreach (var pair in _lights)
            {
                var source = _sources.FirstOrDefault(value => value.light == pair.Key);
                float strength = source == null ? _cfg.colorMax : source.state.strength;
                if (pair.Key == null || !Mathf.Approximately(pair.Key.intensity, pair.Value * expected.light * strength)) return false;
                Color color = source == null ? _lightColors[pair.Key] : new Color(source.state.r, source.state.g, source.state.b, _cfg.colorMax);
                if (!SameColor(pair.Key.color, color)) return false;
            }
            foreach (var pair in _emissions)
            {
                var source = _sources.FirstOrDefault(value => value.material == pair.Key);
                Color color = source == null ? pair.Value * expected.emission
                    : new Color(source.state.r, source.state.g, source.state.b, _cfg.colorMax) * source.emissionPeak * expected.emission * source.state.strength;
                if (pair.Key == null || !SameColor(pair.Key.GetColor("_EmissionColor"), color)) return false;
            }
            foreach (var value in _bloom)
            {
                object actual = value.Item2 is FieldInfo field ? field.GetValue(value.Item1) : ((PropertyInfo)value.Item2).GetValue(value.Item1, null);
                if (!Mathf.Approximately(Convert.ToSingle(actual, CultureInfo.InvariantCulture), value.Item3 * expected.bloom)) return false;
            }
            return true;
        }
        private static bool SameColor(Color left, Color right) => Mathf.Approximately(left.r, right.r) && Mathf.Approximately(left.g, right.g) && Mathf.Approximately(left.b, right.b);
        internal string AppliedSummary() => "lights=[" + string.Join(";", _lights.Keys.Where(light => light != null).Select(light => light.name + ":" + light.intensity + ":" + light.color)) + "] materials=" + _emissions.Count + " bloom=" + State.bloom;
        public void Dispose()
        {
            foreach (var pair in _lights) if (pair.Key != null) pair.Key.intensity = pair.Value;
            foreach (var pair in _lightColors) if (pair.Key != null) pair.Key.color = pair.Value;
            foreach (var pair in _emissions) if (pair.Key != null) pair.Key.SetColor("_EmissionColor", pair.Value);
            foreach (var value in _bloom) if ((value.Item1 as UnityEngine.Object) != null) Write(value.Item1, value.Item2, value.Item3);
        }
        private static void AddCeilingLamp(Transform room, HotelRoomControlSettings cfg)
        {
            if (room.Find("HotelCeilingLamp") != null) return;
            var bounds = room.GetComponentsInChildren<Renderer>(true).Single(renderer => renderer.name == "Ceiling").bounds;
            var root = new GameObject("HotelCeilingLamp"); root.transform.SetParent(room, false);
            root.transform.position = new Vector3(bounds.center.x, bounds.min.y - cfg.ceilingLampDrop, bounds.center.z);
            var fixture = GameObject.CreatePrimitive(PrimitiveType.Cylinder); fixture.name = "天井灯の器具"; fixture.transform.SetParent(root.transform, false);
            fixture.transform.localScale = new Vector3(cfg.ceilingLampDiameter, cfg.ceilingLampHalfHeight, cfg.ceilingLampDiameter);
            UnityEngine.Object.Destroy(fixture.GetComponent<Collider>());
            var material = new Material(Shader.Find("Standard")) { name = "Hotel ceiling lamp", color = cfg.ceilingLightColor };
            material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", cfg.ceilingLightColor);
            fixture.GetComponent<Renderer>().sharedMaterial = material;
            fixture.AddComponent<HotelNeonMaterialLifetime>().Owned = material;
            var lamp = new GameObject("Light"); lamp.transform.SetParent(root.transform, false); lamp.name = "HotelCeilingLamp";
            lamp.transform.localPosition = Vector3.down * cfg.ceilingLightOffset;
            var light = lamp.AddComponent<Light>(); light.type = LightType.Point; light.color = cfg.ceilingLightColor;
            light.intensity = cfg.ceilingLightIntensity; light.range = cfg.ceilingLightRange; light.shadows = LightShadows.None;
        }
        private static void AddCeilingNeon(Transform room, HotelRoomControlSettings cfg)
        {
            if (room.Find("HotelCeilingNeonRectangle") != null) return;
            var ceiling = room.GetComponentsInChildren<Renderer>(true).Single(r => r.name == "Ceiling");
            var bounds = ceiling.bounds;
            float left = bounds.min.x + cfg.neonMargin, right = bounds.max.x - cfg.neonMargin;
            float back = bounds.min.z + cfg.neonMargin, front = bounds.max.z - cfg.neonMargin, height = bounds.min.y - cfg.neonDrop;
            var points = new[] { new Vector3(left, height, back), new Vector3(right, height, back), new Vector3(right, height, front), new Vector3(left, height, front) };
            var root = new GameObject("HotelCeilingNeonRectangle"); root.transform.SetParent(room, false);
            var line = root.AddComponent<LineRenderer>(); line.useWorldSpace = false; line.loop = true;
            line.positionCount = points.Length; line.SetPositions(points.Select(room.InverseTransformPoint).ToArray()); line.widthMultiplier = cfg.neonWidth;
            var material = new Material(Shader.Find("Standard")); material.name = "Hotel ceiling neon";
            material.color = cfg.neonColor; material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", cfg.neonColor * cfg.neonEmission);
            line.sharedMaterial = material;
            root.AddComponent<HotelNeonMaterialLifetime>().Owned = material;
        }
    }
    internal sealed class HotelNeonMaterialLifetime : MonoBehaviour
    {
        internal Material Owned;
        private void OnDestroy() { if (Owned != null) UnityEngine.Object.Destroy(Owned); }
    }
}
