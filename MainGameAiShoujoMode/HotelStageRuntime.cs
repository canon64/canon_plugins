using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CanonGate;
using DG.Tweening;
using MapEnvironmentRuntime;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    [Serializable] internal sealed class HotelStageSettings
    {
        [Serializable] internal sealed class Runtime
        {
            public float curtainSeconds, mistSeconds, menuButtonHeight, ledIntensity;
        }
        [Serializable] internal sealed class Preset { public string id, label; public float[] color; }
        [Serializable] internal sealed class Portal
        {
            public float[] position, size, plaqueSize, plaqueOffset, labelOffset;
            public float labelSize, labelYaw, metallic, smoothness;
            public int labelFontSize;
        }
        public Runtime runtime;
        public Preset[] presets;
        public Portal portal;
        internal static HotelStageSettings Read(string path)
        {
            var value = JsonCodec.FromJson<HotelStageSettings>(File.ReadAllText(path));
            if (value?.runtime == null || value.presets == null || !value.presets.Any()
                || value.runtime.curtainSeconds <= default(float) || value.runtime.mistSeconds <= default(float)
                || new[] { value.runtime.curtainSeconds, value.runtime.mistSeconds, value.runtime.menuButtonHeight, value.runtime.ledIntensity }
                    .Any(v => float.IsNaN(v) || float.IsInfinity(v) || v <= default(float))
                || value.presets.Any(p => p?.color == null || p.color.Length != Enum.GetValues(typeof(ColorChannel)).Length
                    || string.IsNullOrWhiteSpace(p.id) || string.IsNullOrWhiteSpace(p.label)
                    || p.color.Any(c => float.IsNaN(c) || float.IsInfinity(c) || c < default(float)))
                || value.presets.Select(p => p.id).Distinct().Count() != value.presets.Length)
                throw new InvalidDataException("3Fステージの演出設定が不正です。");
            if (value.portal != null && new[] { value.portal.position, value.portal.size, value.portal.plaqueSize, value.portal.plaqueOffset, value.portal.labelOffset }
                .Any(v => v == null || v.Length != Enum.GetValues(typeof(ColorChannel)).Length || v.Any(c => float.IsNaN(c) || float.IsInfinity(c))))
                throw new InvalidDataException("3F移動ボタンの座標が不正です。");
            return value;
        }
        internal enum ColorChannel { Red, Green, Blue }
        internal static Vector3 Vector(float[] v) => new Vector3(v[(int)ColorChannel.Red], v[(int)ColorChannel.Green], v[(int)ColorChannel.Blue]);
    }

    // This floor owns the gate registration, finite animations and cloned materials.
    // No Update, polling coroutine, settings watcher or automatic effect loop.
    internal sealed class HotelStageRuntime : IDisposable
    {
        internal const string ActivityGuid = Plugin.Guid + ".hotelstage";
        private readonly HotelStageSettings settings;
        private readonly Animation[] curtains;
        private readonly ParticleSystem[] mist;
        private readonly ReflectionProbe[] probes;
        private readonly Dictionary<string, Texture[]> reflections;
        private readonly Renderer[] renderers;
        private readonly Dictionary<Renderer, Material[]> original = new Dictionary<Renderer, Material[]>();
        private readonly List<Material> copies = new List<Material>();
        private readonly GateApi.ActivityRegistration activity;
        private GateApi.ActivityPermit permit;
        private Tween curtainCompletion, mistCompletion;
        private int generation, preset;
        private bool open, disposed, active;
        private string notice;
        internal bool Allowed => active && !disposed && permit.IsValid;
        internal bool CurtainOpen => open;

        internal HotelStageRuntime(Transform map, string configPath)
        {
            settings = HotelStageSettings.Read(configPath);
            curtains = map.GetComponentsInChildren<Animation>(true).Where(a => a.GetClip("CurtainTravel") != null).ToArray();
            mist = map.GetComponentsInChildren<ParticleSystem>(true).Where(p => p.name.StartsWith("3F_Mist_", StringComparison.Ordinal)).ToArray();
            renderers = map.GetComponentsInChildren<Renderer>(true).Where(r => r.sharedMaterials.Any(m => m != null && m.name == "RedLED")).ToArray();
            var activeRoot = map.Find("3F_ReflectionProbes");
            var stateRoot = map.Find("3F_ReflectionStates");
            if (activeRoot == null || stateRoot == null || !curtains.Any() || !mist.Any())
                throw new InvalidDataException("3Fの演出オブジェクトがありません。");
            probes = activeRoot.GetComponentsInChildren<ReflectionProbe>(true).OrderBy(p => p.name).ToArray();
            reflections = new Dictionary<string, Texture[]>();
            foreach (Transform child in stateRoot)
            {
                var textures = child.GetComponentsInChildren<ReflectionProbe>(true).OrderBy(p => p.name).Select(p => p.customBakedTexture).ToArray();
                if (textures.Length != probes.Length || textures.Any(t => t == null)) throw new InvalidDataException("3Fの反射データが不足しています。");
                reflections.Add(child.name, textures);
            }
            foreach (var p in settings.presets.Select((p, index) => index))
                if (!reflections.ContainsKey("closed_" + p) || !reflections.ContainsKey("open_" + p))
                    throw new InvalidDataException("3Fの反射プリセットがありません。");
            activity = GateApi.RegisterActivity(ActivityGuid, GateApi.ActivityKind.Preview, StartAllowed, StopAllowed, false);
        }

        internal void SetActive(bool shown)
        {
            if (disposed) return;
            if (shown) activity.Begin(); else activity.End();
        }
        private void StartAllowed()
        {
            permit = activity.Capture(); active = true; open = false; preset = default(int); generation++;
            foreach (var r in renderers)
            {
                original.Add(r, r.sharedMaterials);
                r.sharedMaterials = r.sharedMaterials.Select(m =>
                {
                    if (m == null || m.name != "RedLED") return m;
                    var copy = new Material(m); copies.Add(copy); return copy;
                }).ToArray();
            }
            foreach (var a in curtains) Sample(a, default(float));
            ApplyPreset(); ApplyReflection(); notice = null;
        }
        private void StopAllowed()
        {
            active = false; permit = default(GateApi.ActivityPermit); generation++;
            curtainCompletion?.Kill(); curtainCompletion = null;
            mistCompletion?.Kill(); mistCompletion = null;
            foreach (var p in mist) if (p != null) p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var a in curtains) if (a != null) { a.Stop(); a.enabled = false; }
            foreach (var pair in original) if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
            original.Clear();
            foreach (var material in copies) if (material != null) UnityEngine.Object.Destroy(material);
            copies.Clear();
        }
        private void Sample(Animation animation, float time)
        {
            animation.enabled = true; animation.Play("CurtainTravel");
            animation["CurtainTravel"].time = time; animation.Sample(); animation.Stop(); animation.enabled = false;
        }
        internal void SetCurtain(bool targetOpen)
        {
            if (!Allowed) return;
            curtainCompletion?.Kill(); curtainCompletion = null;
            float time = curtains.First()["CurtainTravel"].time;
            if (!curtains.First().enabled) time = open ? settings.runtime.curtainSeconds : default(float);
            float target = targetOpen ? settings.runtime.curtainSeconds : default(float);
            open = targetOpen;
            foreach (var animation in curtains)
            {
                animation.enabled = true; animation.Play("CurtainTravel");
                var state = animation["CurtainTravel"]; state.time = time;
                state.speed = Mathf.Sign(target - time); state.wrapMode = WrapMode.ClampForever;
                animation.Sample();
            }
            int started = generation; var captured = permit;
            curtainCompletion = DOVirtual.DelayedCall(Mathf.Abs(target - time), () =>
            {
                if (!active || disposed || generation != started || !captured.IsValid) return;
                foreach (var animation in curtains) Sample(animation, target);
                ApplyReflection(); curtainCompletion = null;
            }, true);
        }
        internal void SetPreset(int index)
        {
            if (!Allowed || index < default(int) || index >= settings.presets.Length) return;
            preset = index; ApplyPreset(); ApplyReflection();
        }
        private void ApplyPreset()
        {
            var c = settings.presets[preset].color;
            var color = new Color(c[(int)HotelStageSettings.ColorChannel.Red], c[(int)HotelStageSettings.ColorChannel.Green], c[(int)HotelStageSettings.ColorChannel.Blue]);
            foreach (var material in copies) material.SetColor("_EmissionColor", color * settings.runtime.ledIntensity);
        }
        private void ApplyReflection()
        {
            var textures = reflections[(open ? "open_" : "closed_") + preset];
            foreach (var pair in probes.Select((probe, index) => new { probe, index })) pair.probe.customBakedTexture = textures[pair.index];
        }
        internal void PlayMist()
        {
            if (!Allowed) return;
            mistCompletion?.Kill(); mistCompletion = null;
            foreach (var p in mist) { p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); p.Play(true); }
            int started = generation; var captured = permit;
            mistCompletion = DOVirtual.DelayedCall(settings.runtime.mistSeconds, () =>
            {
                if (!active || disposed || generation != started || !captured.IsValid) return;
                foreach (var p in mist) p.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                mistCompletion = null;
            }, true);
        }
        internal void Draw()
        {
            GUILayout.BeginVertical(GUI.skin.box); GUILayout.Label("3F ステージ演出");
            if (!Allowed)
            {
                notice = "演出は停止中です。canon_gate.cfg の ActivityAllowedPluginGuids に " + ActivityGuid + " を追加して3Fへ入り直してください。";
                GUILayout.Label(notice); GUILayout.EndVertical(); return;
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("後幕を開く", GUILayout.Height(settings.runtime.menuButtonHeight))) SetCurtain(true);
            if (GUILayout.Button("後幕を閉じる", GUILayout.Height(settings.runtime.menuButtonHeight))) SetCurtain(false);
            GUILayout.EndHorizontal(); GUILayout.Label(open ? "後幕：開" : "後幕：閉");
            GUILayout.BeginHorizontal();
            foreach (var pair in settings.presets.Select((value, index) => new { value, index }))
                if (GUILayout.Toggle(preset == pair.index, pair.value.label, GUI.skin.button, GUILayout.Height(settings.runtime.menuButtonHeight)) && preset != pair.index) SetPreset(pair.index);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("薄い霧を一度流す", GUILayout.Height(settings.runtime.menuButtonHeight))) PlayMist();
            GUILayout.EndVertical();
        }
        public void Dispose()
        {
            if (disposed) return;
            activity.Dispose(); StopAllowed(); disposed = true;
        }
    }
}
