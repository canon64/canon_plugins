using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MainGameUnifiedUi;
using MapEnvironmentRuntime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MainGameAiShoujoMode.Lighting
{
    // Owned exclusively by AiModeSession. No update loop, polling, or automatic cause confirmation.
    internal sealed class AiLightingSession : IDisposable
    {
        private const string Tab = Plugin.Guid + ".lighting";
        private readonly EnvironmentWorld _world;
        private readonly Parameters _settings;
        private readonly Transform _root;
        private readonly SynchronizationContext _main;
        private readonly LightingComparison _comparison = new LightingComparison();
        private readonly Dictionary<Behaviour, LightingEffect> _changedEffects = new Dictionary<Behaviour, LightingEffect>();
        private readonly List<Behaviour> _differentEffects = new List<Behaviour>();
        private readonly HashSet<Camera> _profileCameras = new HashSet<Camera>();
        private Camera _walking, _camera;
        private Light _native, _walkingLight;
        private HSceneProc _nativeScene;
        private Action _restoreNativeDefaults;
        private LightingLight _nativeOriginal;
        private LightingEffect[] _walkEffects = Array.Empty<LightingEffect>();
        private LightingSnapshot _walkSnapshot;
        private LightingProfile _profile;
        private NativeHLightingRenderer _nativeRenderer;
        internal Light DedicatedControlLight => _nativeRenderer?.OwnedLight ?? _walkingLight;
        private Action _cancelProbe;
        private bool _disposed, _inH, _transition, _clockOwned, _oldPlaying, _nativeOverridden;
        private string _status = "未確認です。歩行中に比較を開始し、Hへ移ってください。", _run;
        internal bool ClockPlayingForSave => _clockOwned ? _oldPlaying : _world.Playing;
        internal AiLightingSession(EnvironmentWorld world, Transform root, Camera walking)
        {
            _world = world; _root = root; _walking = walking; _camera = walking; _settings = world.Settings;
            _main = SynchronizationContext.Current ?? throw new InvalidOperationException("照明設定はメインスレッドから開始してください。");
            Plugin.AiLightingChanged += SettingChanged;
            UnifiedUiApi.RegisterTab(Tab, "AI照明", Draw);
            Protect(Refresh);
        }
        private void SettingChanged()
        {
            _main.Post(_ => { if (!_disposed) Protect(Refresh); }, null);
        }
        private void Protect(Action operation)
        {
            try { operation(); }
            catch (Exception error)
            {
                _status = error.GetBaseException().Message;
                try { RestoreOverrides(); } catch { /* Preserve the original user-facing failure. */ }
                finally { _nativeRenderer?.Suspend(); DestroyWalkingLight(); ReleaseClock(); }
            }
        }
        private void Refresh()
        {
            if (_disposed) return;
            CancelProbe(); RestoreOverrides(); DestroyWalkingLight();
            if (Plugin.AiNativeLighting)
            {
                ReleaseClock(); _profile = null; _differentEffects.Clear();
                ApplyNativeRendering();
                return;
            }
            _nativeRenderer?.Dispose(); _nativeRenderer = null;
            if (!Plugin.AiLightingTrace) { ReleaseClock(); _differentEffects.Clear(); }
            if (Plugin.AiLightingTrace)
            {
                if (_run == null) _run = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N");
                if (_inH) PrepareHComparison(); else if (!_transition) CaptureWalk("walking.settings");
                if (!_transition) ArmRender(_inH ? "h.settings" : "walking.settings");
                return;
            }
            if (!Plugin.AiLightingEnabled) { _profile = null; return; }
            _profile = LightingJson.Load(Plugin.AiLightingProfilePath);
            if (_profile == null) { _status = "確認済みの照明設定がありません。まず比較を行ってください。"; return; }
            if (!_transition) { ApplyProfile(); ArmRender(_inH ? "h.profile" : "walking.profile"); }
        }
        internal void WalkingReady(Camera camera)
        {
            if (_disposed) return;
            Protect(() =>
            {
                CancelProbe(); RestoreOverrides(); DestroyWalkingLight();
                _native = null; _nativeScene = null; _nativeOriginal = null; _inH = false; _transition = false; _walking = camera; _camera = camera;
                if (Plugin.AiNativeLighting) { ApplyNativeRendering(); return; }
                if (Plugin.AiLightingTrace) { CaptureWalk("walking.return"); ArmRender("walking.return"); }
                else if (Plugin.AiLightingEnabled) { ApplyProfile(); ArmRender("walking.profile"); }
            });
        }
        internal void BeforeH()
        {
            if (_disposed) return;
            Protect(() =>
            {
                CancelProbe(); RestoreOverrides();
                _nativeRenderer?.Suspend();
                if (Plugin.AiLightingTrace) CaptureWalk("walking.before-h");
                // A walking light must never overlap the native H light, including failed startup.
                DestroyWalkingLight(); _transition = true;
            });
        }
        internal void BeforeDedicatedH()
        {
            if (_disposed) return;
            // Dedicated H keeps the island camera and its owned rendering; no native H light is created.
            Protect(() => { CancelProbe(); if (Plugin.AiLightingTrace) CaptureWalk("walking.before-dedicated-h"); });
        }
        internal void DedicatedReady(Camera camera)
        {
            if (_disposed) return;
            Protect(() =>
            {
                if (camera != _walking) WalkingReady(camera);
                _camera = camera; _nativeScene = null; _native = null; _inH = false; _transition = false;
                if (Plugin.AiLightingTrace) { Record("dedicated-h.ready"); ArmRender("dedicated-h.ready"); }
                _status = "AI専用Hは島のカメラ・環境光・所有中の補助ライトと画面効果を継続しています。見た目の一致は未確認です。";
            });
        }
        internal void HReady(Camera camera, HSceneProc scene)
        {
            if (_disposed) return;
            Protect(() =>
            {
                _transition = false; _inH = true; _camera = camera; _nativeScene = scene; _native = scene == null ? null : scene.lightCamera;
                if (Plugin.AiNativeLighting) { ApplyNativeRendering(); return; }
                if (Plugin.AiLightingTrace) { PrepareHComparison(); Record("h.ready"); ArmRender("h.ready"); }
                else if (Plugin.AiLightingEnabled) { ApplyProfile(); ArmRender("h.profile"); }
            });
        }
        private IEnumerable<ChaControl> Characters() => Resources.FindObjectsOfTypeAll<ChaControl>()
            .Where(character => character != null && character.gameObject.scene.IsValid() && character.gameObject.scene.isLoaded && character.gameObject.activeInHierarchy);
        private void CaptureWalk(string stage)
        {
            if (!Plugin.AiLightingTrace || _walking == null) return;
            _walkEffects = LightingUnity.OutputCameras(_walking).SelectMany(camera => camera.GetComponents<Behaviour>())
                .Where(component => component != null && LightingUnity.IsImageEffect(component.GetType())).Select(LightingUnity.CaptureEffect).ToArray();
            _walkSnapshot = Record(stage);
        }
        private void PrepareHComparison()
        {
            if (!Plugin.AiLightingTrace || !_inH) return;
            _comparison.Reset(); _differentEffects.Clear();
            _nativeOriginal = LightingUnity.CaptureLight(_native, _camera);
            foreach (var camera in LightingUnity.OutputCameras(_camera))
                foreach (var component in camera.GetComponents<Behaviour>().Where(component => component != null && LightingUnity.IsImageEffect(component.GetType())))
                {
                    var current = LightingUnity.CaptureEffect(component);
                    if (!_walkEffects.Any(old => old.type == current.type && old.enabled == current.enabled && old.order == current.order && old.Signature == current.Signature)) _differentEffects.Add(component);
                }
        }
        private LightingSnapshot Record(string stage)
        {
            if (_disposed || !Plugin.AiLightingTrace || _camera == null) return null;
            if (_run == null) _run = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N");
            var snapshot = LightingUnity.Snapshot(stage, _camera, _world, Characters());
            var directory = Path.Combine(Plugin.AiLightingTracePath, _run); Directory.CreateDirectory(directory);
            var file = stage + "-" + Guid.NewGuid().ToString("N") + ".json";
            File.WriteAllText(Path.Combine(directory, file), LightingJson.Write(snapshot));
            if (_walkSnapshot != null && (_inH || stage.StartsWith("walking.return", StringComparison.Ordinal))) File.WriteAllText(Path.Combine(directory, file + ".diff.json"), LightingJson.Write(LightingDifference.Compare(_walkSnapshot, snapshot)));
            _status = "記録先：" + directory;
            return snapshot;
        }
        private void ArmRender(string stage)
        {
            CancelProbe();
            if (_disposed || (!Plugin.AiLightingTrace && (!Plugin.AiLightingEnabled || _profile?.effects?.Any() != true))) return;
            Camera.CameraCallback callback = null;
            callback = rendered =>
            {
                if (_disposed || (!Plugin.AiLightingTrace && !Plugin.AiLightingEnabled)) { CancelProbe(); return; }
                if (rendered != _camera && !rendered.stereoEnabled && rendered != Camera.main) return;
                // The first real output removes the hook immediately. Disabled logical cameras are never awaited.
                CancelProbe();
                Protect(() =>
                {
                    if (Plugin.AiLightingTrace) Record(stage + ".first-render");
                    else ApplyNewOutputCamera(rendered);
                });
            };
            Camera.onPreCull += callback;
            _cancelProbe = () => Camera.onPreCull -= callback;
        }
        private void ApplyNativeRendering()
        {
            if (_transition) return;
            if (_nativeRenderer == null) _nativeRenderer = new NativeHLightingRenderer(_root);
            if (_inH) _nativeRenderer.HReady(_camera, _nativeScene);
            else _nativeRenderer.Walking(_walking);
            {
                // A one-shot callback covers an output camera created during the transition.
                Camera.CameraCallback callback = null;
                callback = output =>
                {
                    if (_disposed || !Plugin.AiNativeLighting || _transition) { CancelProbe(); return; }
                    if (output != _camera && !output.stereoEnabled && output != Camera.main) return;
                    CancelProbe(); Protect(() =>
                    {
                        if (_inH) _nativeRenderer.HReady(_camera, _nativeScene, output);
                        else _nativeRenderer.ApplyCamera(output);
                    });
                };
                Camera.onPreCull += callback;
                _cancelProbe = () => Camera.onPreCull -= callback;
            }
            if (Plugin.AiLightingTrace) Record(_inH ? "h.native" : "walking.native");
            _status = _inH ? "本編Hの描画を使用中。歩行用の追加照明は停止しています。" : "歩行用のキャラ専用補助ライトを適用中。島の色補正・昼夜・天候は維持します。";
        }
        private void CancelProbe() { var cancel = _cancelProbe; _cancelProbe = null; cancel?.Invoke(); }
        private void SelectComparison(string mode)
        {
            if (!_inH || !Plugin.AiLightingTrace || _nativeOriginal == null) throw new InvalidOperationException("診断を有効にし、H開始後に比較してください。");
            RestoreOverrides(); _comparison.Select(mode);
            if (mode == "without-light" && _native != null) { _nativeOverridden = true; _native.enabled = false; }
            if (mode == "without-effects" || mode.StartsWith("without-effect:", StringComparison.Ordinal))
                foreach (var component in _differentEffects.Where(component => component != null && (mode == "without-effects" || mode == "without-effect:" + component.GetType().FullName)))
                { _changedEffects[component] = LightingUnity.CaptureEffect(component); component.enabled = false; }
            Record("h.compare." + mode); ArmRender("h.compare." + mode);
        }
        private Behaviour MatchEffect(Camera camera, LightingEffect effect)
        {
            var matches = camera == null ? Array.Empty<Behaviour>() : camera.GetComponents<Behaviour>().Where(component => component != null && component.GetType().FullName == effect.type).ToArray();
            if (matches.Length != Math.Sign(int.MaxValue)) throw new InvalidOperationException("対応する画面効果を一意に特定できません：" + effect.type);
            LightingUnity.ValidateEffect(effect, matches.Single()); return matches.Single();
        }
        private void ApplyProfile()
        {
            if (_profile == null || _camera == null) return;
            var targets = LightingUnity.OutputCameras(_camera);
            // Validate everything before mutating the first camera or light.
            var effects = (from camera in targets from effect in _profile.effects ?? Array.Empty<LightingEffect>()
                           select new { component = MatchEffect(camera, effect), profile = effect }).ToArray();
            foreach (var pair in effects)
            { _changedEffects[pair.component] = LightingUnity.CaptureEffect(pair.component); LightingUnity.ApplyEffect(pair.profile, pair.component); }
            foreach (var camera in targets) _profileCameras.Add(camera);
            if (_profile.light != null)
            {
                if (_inH)
                {
                    if (_native == null) throw new InvalidOperationException("このHの補助ライトがありません。");
                    _nativeOriginal = LightingUnity.CaptureLight(_native, _camera); _nativeOverridden = true;
                    LightingUnity.ApplyLight(_profile.light, _native, _camera);
                    _native.enabled = _profile.light.enabled && _profile.light.active;
                    ApplyNativeDefaults();
                }
                else
                {
                    var node = new GameObject("AI confirmed H lighting");
                    node.transform.SetParent(_profile.light.anchor == "camera" ? _camera.transform : _root, false);
                    _walkingLight = node.AddComponent<Light>();
                    LightingUnity.ApplyLight(_profile.light, _walkingLight, _camera);
                    _walkingLight.enabled = _profile.light.enabled && _profile.light.active;
                }
            }
            _status = "確認済みH照明を適用中。昼夜・天候は維持します。";
        }
        private void ApplyNewOutputCamera(Camera camera)
        {
            if (_profile == null || camera == null || _profileCameras.Contains(camera)) return;
            var targets = (_profile.effects ?? Array.Empty<LightingEffect>()).Select(effect => new { effect, component = MatchEffect(camera, effect) }).ToArray();
            foreach (var pair in targets)
            { _changedEffects[pair.component] = LightingUnity.CaptureEffect(pair.component); LightingUnity.ApplyEffect(pair.effect, pair.component); }
            _profileCameras.Add(camera);
        }
        private void RestoreOverrides()
        {
            foreach (var pair in _changedEffects.ToArray())
                if (pair.Key != null) try { LightingUnity.ApplyEffect(pair.Value, pair.Key); } catch { pair.Key.enabled = pair.Value.enabled; }
            _changedEffects.Clear();
            _profileCameras.Clear();
            if (_nativeOverridden)
            {
                _nativeOverridden = false;
                if (_native != null && _nativeOriginal != null) LightingUnity.ApplyLight(_nativeOriginal, _native, _camera);
            }
            var defaults = _restoreNativeDefaults; _restoreNativeDefaults = null; defaults?.Invoke();
        }
        private void ApplyNativeDefaults()
        {
            var data = _nativeScene == null ? null : HarmonyLib.AccessTools.Field(typeof(HSceneProc), "lightData")?.GetValue(_nativeScene) as HSceneProc.LightData;
            if (data == null || data.light != _native) return;
            var color = data.initColor; var intensity = data.initIntensity; var rotation = data.initRot;
            _restoreNativeDefaults = () => { data.initColor = color; data.initIntensity = intensity; data.initRot = rotation; };
            data.initColor = _native.color; data.initIntensity = _native.intensity;
            data.initRot = new Vector2(_native.transform.localEulerAngles.x, _native.transform.localEulerAngles.y);
        }
        private void DestroyWalkingLight()
        { if (_walkingLight != null) { _walkingLight.enabled = false; Object.DestroyImmediate(_walkingLight.gameObject); _walkingLight = null; } }
        private void SaveConfirmed()
        {
            if (!_inH || !Plugin.AiLightingTrace || _walkSnapshot == null || (!_comparison.LightConfirmed && !_comparison.EffectsConfirmed))
                throw new InvalidOperationException("歩行→Hの比較を行い、差が消えた項目を確認してください。");
            if (_world.Hour != _walkSnapshot.hour || _world.Year != _walkSnapshot.year || _world.Month != _walkSnapshot.month || _world.Day != _walkSnapshot.day || _world.Weather != _walkSnapshot.weather)
                throw new InvalidOperationException("比較中に日時または天候が変わりました。同じ条件で比較し直してください。");
            RestoreOverrides();
            if (_comparison.EffectsConfirmed && !_comparison.ConfirmedEffects.Any()) throw new InvalidOperationException("画面効果を下の一覧で一種類ずつ切り替え、差が消える種類を確認してください。");
            var selected = _differentEffects.Where(component => component != null && _comparison.ConfirmedEffects.Contains(component.GetType().FullName)).Select(LightingUnity.CaptureEffect).ToArray();
            // Multiple actual cameras must agree. Do not silently pick one eye or overwrite a different profile.
            foreach (var group in selected.GroupBy(effect => effect.type))
                if (group.Select(effect => effect.Signature + effect.enabled).Distinct().Count() != Math.Sign(int.MaxValue)) throw new InvalidOperationException("VRと管理カメラで画面効果が異なります。個別の原因確認が必要です。");
            selected = selected.GroupBy(effect => effect.type).Select(group => group.First()).ToArray();
            foreach (var effect in selected) foreach (var camera in LightingUnity.OutputCameras(_walking)) MatchEffect(camera, effect);
            var profile = new LightingProfile { confirmed = true, evidence = _run, capturedUtc = DateTime.UtcNow.ToString("o"),
                light = _comparison.LightConfirmed ? LightingUnity.CaptureLight(_native, _camera) : null, effects = selected };
            LightingJson.Save(Plugin.AiLightingProfilePath, profile); _profile = profile;
            Plugin.AiLightingTrace = false; Plugin.AiLightingEnabled = true;
            _status = "確認した照明を保存しました。次回の歩行開始からも適用します。";
        }
        private void BeginComparison()
        {
            if (_inH || _transition) throw new InvalidOperationException("歩行中に比較を開始してください。");
            if (!_clockOwned) { _oldPlaying = _world.Playing; _clockOwned = true; _world.Playing = false; }
            Plugin.AiLightingTrace = true; _comparison.Reset(); Protect(Refresh);
            _status = "比較中は時刻を固定します。同じ位置・視線でHへ移ってください。終了で時刻進行を戻します。";
        }
        private void ReleaseClock() { if (_clockOwned) { if (_world != null) _world.Playing = _oldPlaying; _clockOwned = false; } }
        private void Draw()
        {
            if (_disposed || !Plugin.ModeAllowed) return;
            GUILayout.Label("AI少女：Hシーンと歩行の描画設定");
            var native = GUILayout.Toggle(Plugin.AiNativeLighting, "Hと同じ照明・色補正を歩行にも使う");
            if (native != Plugin.AiNativeLighting) Plugin.AiNativeLighting = native;
            if (Plugin.AiNativeLighting)
            {
                GUILayout.Label(_status);
                var diagnostics = GUILayout.Toggle(Plugin.AiLightingTrace, "描画状態の診断記録");
                if (diagnostics != Plugin.AiLightingTrace) Plugin.AiLightingTrace = diagnostics;
                return;
            }
            GUILayout.Label("AI少女：昼夜を維持したH照明の比較・共通化");
            var enabled = GUILayout.Toggle(Plugin.AiLightingEnabled, "確認済みのH照明を歩行にも適用");
            if (enabled != Plugin.AiLightingEnabled) Plugin.AiLightingEnabled = enabled;
            var trace = GUILayout.Toggle(Plugin.AiLightingTrace, "照明診断を有効にする（OFFで記録・探索・描画購読を停止）");
            if (trace != Plugin.AiLightingTrace) Plugin.AiLightingTrace = trace;
            GUILayout.Label(_status);
            if (Plugin.AiLightingTrace) GUILayout.Label("診断中は保存済み照明の適用を一時解除し、元の歩行とHを比較します。");
            GUILayout.Label("比較は同じ時刻・位置・視線で。空・雲の描画は切り替えません。");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("歩行で比較を開始", GUILayout.ExpandWidth(false))) Protect(BeginComparison);
            if (GUILayout.Button("比較を終了して元へ戻す", GUILayout.ExpandWidth(false))) Protect(() => { RestoreOverrides(); ReleaseClock(); Plugin.AiLightingTrace = false; });
            GUILayout.EndHorizontal();
            var previous = GUI.enabled; GUI.enabled = previous && _inH && Plugin.AiLightingTrace;
            GUILayout.BeginHorizontal();
            foreach (var item in new[] { Tuple.Create("baseline", "Hの元の照明"), Tuple.Create("without-light", "補助ライトだけOFF"), Tuple.Create("without-effects", "差分の画面効果だけOFF") })
                if (GUILayout.Toggle(_comparison.Mode == item.Item1, item.Item2, GUI.skin.button, GUILayout.ExpandWidth(false)) && _comparison.Mode != item.Item1) Protect(() => SelectComparison(item.Item1));
            GUILayout.EndHorizontal();
            foreach (var type in _differentEffects.Where(component => component != null).Select(component => component.GetType().FullName).Distinct())
                if (GUILayout.Toggle(_comparison.Mode == "without-effect:" + type, "この効果だけOFF：" + type, GUI.skin.button, GUILayout.ExpandWidth(false)) && _comparison.Mode != "without-effect:" + type)
                    Protect(() => SelectComparison("without-effect:" + type));
            if (GUILayout.Button("この切替で歩行との差が消えたと確認", GUILayout.ExpandWidth(false))) Protect(() => { _comparison.Confirm(); _status = "確認しました。元のH設定を保存できます。"; });
            if (GUILayout.Button("確認したH設定を保存して適用", GUILayout.ExpandWidth(false))) Protect(SaveConfirmed);
            GUI.enabled = previous;
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            Plugin.AiLightingChanged -= SettingChanged; UnifiedUiApi.UnregisterTab(Tab);
            CancelProbe();
            try { RestoreOverrides(); }
            finally { _nativeRenderer?.Dispose(); _nativeRenderer = null; DestroyWalkingLight(); ReleaseClock(); }
        }
    }
}
