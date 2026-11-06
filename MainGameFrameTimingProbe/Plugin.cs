using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace MainGameFrameTimingProbe
{
    [BepInDependency("canon.gate", "2.2.0")]
    [BepInPlugin(Id, "MainGameFrameTimingProbe", "1.0.0")]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    [DefaultExecutionOrder(-32000)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "com.kks.maingame.frametimingprobe";
        private static Plugin _instance;
        private ConfigEntry<int> _remaining;
        private ConfigEntry<bool> _hSceneOnly;
        private ConfigEntry<float> _warmupSeconds, _baselineSeconds, _durationSeconds;
        private ConfigEntry<float> _timeoutSeconds;
        private CaptureFocusGate _focusGate;
        private MonoBehaviour _scene;
        private HFlag _sceneFlags;
        private float _nextSceneCheck, _phaseStart;
        private int _phase, _mainThread;
        private long _previous;
        private Harmony _harmony;
        private TimingWindow _window;
        private WindowReport _baseline;
        private NativeTimingSampler _nativeTiming;
        private NativeMarkerReport[] _nativeBaseline;
        private readonly Dictionary<MethodBase, int> _indices = new Dictionary<MethodBase, int>();
        private readonly List<MethodBase> _patchedMethods = new List<MethodBase>();
        private readonly List<string> _patchFailures = new List<string>();
        private FrameTiming[] _engineTiming = new FrameTiming[1];
        private double _lastEngineStamp;
        private bool _engineAvailable = true;
        private string _context;
        private Task _writer;

        private void Awake()
        {
            _instance = this;
            _mainThread = Thread.CurrentThread.ManagedThreadId;
            _remaining = Config.Bind("Capture", "Remaining", 0,
                new ConfigDescription("0: disabled. Set 1 to record the next active H scene once. No automatic game control.", new AcceptableValueRange<int>(0, 10)));
            _hSceneOnly = Config.Bind("Capture", "HSceneOnly", true, "Wait for an active H scene. Set false to measure other scenes after warmup.");
            _warmupSeconds = Config.Bind("Capture", "WarmupSeconds", 10f,
                new ConfigDescription("Wait after the active H scene is detected.", new AcceptableValueRange<float>(0, 120)));
            _baselineSeconds = Config.Bind("Capture", "BaselineSeconds", 5f,
                new ConfigDescription("Frame timing without method instrumentation.", new AcceptableValueRange<float>(3, 30)));
            _durationSeconds = Config.Bind("Capture", "DurationSeconds", 15f,
                new ConfigDescription("Bounded method timing window; hooks are then removed.", new AcceptableValueRange<float>(3, 60)));
            _timeoutSeconds = Config.Bind("Capture", "TimeoutSeconds", 120f,
                new ConfigDescription("Wall-clock limit after capture starts, including focus pauses. Sampling stops on timeout.", new AcceptableValueRange<float>(30, 300)));
            CanonPlugins.Shared.PluginSessionGate.Bind(this, Id,
                StartTimingSession, () => { Finish("session ended", false); _scene = null; _sceneFlags = null; });
        }

        private void StartTimingSession()
        {
            _scene = CanonPlugins.Shared.PluginSessionGate.Scene;
            _sceneFlags = CanonPlugins.Shared.PluginSessionGate.ReadScene<HFlag>("flags");
            _phase = 0;
            if (_remaining.Value <= 0 || (_writer != null && !_writer.IsCompleted)) return;
            if (_hSceneOnly.Value && (_scene == null || _sceneFlags == null)) return;
            _phase = 1; _phaseStart = Time.realtimeSinceStartup;
            _focusGate = new CaptureFocusGate(_phaseStart, _timeoutSeconds.Value);
            _baseline = null; _nativeBaseline = null; _context = null; _patchFailures.Clear();
        }

        private void Update()
        {
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed) return;
            if (_writer != null && _writer.IsCompleted)
            {
                if (_writer.IsFaulted) Logger.LogError("Timing report write failed: " + _writer.Exception);
                _writer = null;
            }
            if (_remaining.Value <= 0)
            {
                if (_phase != 0) Finish("disabled");
                return;
            }
            float now = Time.realtimeSinceStartup;
            if (_phase == 0) return;
            float pauseDuration;
            CaptureFocusStep focus = _focusGate.Step(now, Application.isFocused, out pauseDuration);
            if (focus == CaptureFocusStep.TimedOut) { Finish("timeout"); return; }
            if (focus == CaptureFocusStep.Paused) { _window?.Stop(); _nativeTiming?.Pause(); return; }
            if (focus == CaptureFocusStep.Resumed)
            {
                _phaseStart += pauseDuration;
                _nativeTiming?.Resume();
                _previous = Stopwatch.GetTimestamp();
                _window?.Start();
                return; // Exclude the unfocused interval and the first returning frame.
            }
            if (_phase == 1)
            {
                if (now - _phaseStart < _warmupSeconds.Value) return;
                _context = "resolution=" + Screen.width + "x" + Screen.height +
                    "; vsync=" + QualitySettings.vSyncCount + "; refreshHz=" + Screen.currentResolution.refreshRate + "; targetFPS=" + Application.targetFrameRate +
                    "; device=" + SystemInfo.graphicsDeviceName + "; cpu=" + SystemInfo.processorType + SnapshotState();
                _baseline = null;
                _patchFailures.Clear();
                _window = new TimingWindow(new string[0], Stopwatch.Frequency);
                _nativeTiming = new NativeTimingSampler();
                _window.Start();
                _phase = 2;
                _phaseStart = Time.realtimeSinceStartup;
                _previous = Stopwatch.GetTimestamp();
                return;
            }
            long stamp = Stopwatch.GetTimestamp();
            _window.Frame(stamp - _previous);
            _nativeTiming?.Sample();
            _previous = stamp;
            ReadEngineTiming();
            if (_phase == 2 && now - _phaseStart >= _baselineSeconds.Value)
            {
                _window.Stop();
                _baseline = _window.Snapshot();
                _nativeTiming?.Dispose();
                _nativeBaseline = _nativeTiming?.Snapshot();
                try { Install(); }
                catch (Exception ex) { _patchFailures.Add(ex.ToString()); Finish("hook setup failed"); return; }
                _phase = 3;
                _nativeTiming = new NativeTimingSampler();
                _phaseStart = Time.realtimeSinceStartup;
                _previous = Stopwatch.GetTimestamp(); // Exclude installation from the frame samples.
                _window.Start();
            }
            else if (_phase == 3 && now - _phaseStart >= _durationSeconds.Value) Finish("completed");
        }

        private void ReadEngineTiming()
        {
            if (!_engineAvailable) return;
            try
            {
                if (FrameTimingManager.GetLatestTimings(1, _engineTiming) > 0)
                {
                    double stamp = _engineTiming[0].cpuTimeFrameComplete;
                    if (stamp > 0 && stamp != _lastEngineStamp)
                    {
                        _lastEngineStamp = stamp;
                        _window.EngineTiming(_engineTiming[0].cpuFrameTime, _engineTiming[0].gpuFrameTime);
                    }
                }
                FrameTimingManager.CaptureFrameTimings();
            }
            catch { _engineAvailable = false; }
        }

        private void Install()
        {
            _indices.Clear();
            _patchedMethods.Clear();
            var names = new List<string>();
            // Include the game callbacks around their existing Harmony hooks. These are
            // inclusive timings of the H scene, not costs attributed to canon_plugins.
            foreach (string name in new[] { "Update", "LateUpdate" })
            {
                MethodInfo method = typeof(HSceneProc).GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);
                if (method == null || method.GetMethodBody() == null)
                { _patchFailures.Add("Game callback unavailable: HSceneProc." + name); continue; }
                _indices.Add(method, names.Count);
                names.Add("GAME_INCLUSIVE:Assembly-CSharp:HSceneProc." + name);
            }
            string canonical = Path.GetFullPath(Path.Combine(Paths.PluginPath, "canon_plugins")) + Path.DirectorySeparatorChar;
            var assemblies = Chainloader.PluginInfos.Values.Where(info => info.Instance != null && info.Instance != this &&
                !string.IsNullOrEmpty(info.Location) && Path.GetFullPath(info.Location).StartsWith(canonical, StringComparison.OrdinalIgnoreCase))
                .Select(info => info.Instance.GetType().Assembly).Distinct().ToArray();
            foreach (Assembly assembly in assemblies)
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
                foreach (Type type in types)
                    foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    {
                        if (_indices.Count >= 512) { _patchFailures.Add("512 method limit reached; remaining methods omitted"); goto ready; }
                        if (!MethodSelection.Selected(method)) continue;
                        _indices.Add(method, names.Count);
                        names.Add(assembly.GetName().Name + ":" + type.FullName + "." + method);
                    }
            }
        ready:
            _window = new TimingWindow(names.ToArray(), Stopwatch.Frequency);
            _harmony = new Harmony(Id);
            var prefix = new HarmonyMethod(typeof(Plugin), nameof(BeginMethod));
            var finalizer = new HarmonyMethod(typeof(Plugin), nameof(EndMethod));
            prefix.priority = Priority.First;
            finalizer.priority = Priority.Last;
            foreach (var item in _indices)
            {
                try { _harmony.Patch(item.Key, prefix: prefix, finalizer: finalizer); _patchedMethods.Add(item.Key); }
                catch (Exception ex) { _patchFailures.Add(names[item.Value] + ": " + ex.GetType().Name + ": " + ex.Message); }
            }
        }

        private static void BeginMethod(MethodBase __originalMethod, out TimingWindow.Ticket __state)
        {
            __state = default;
            Plugin p = _instance;
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed || p == null || Thread.CurrentThread.ManagedThreadId != p._mainThread || p._window == null || !p._window.Recording) return;
            if (p._indices.TryGetValue(__originalMethod, out int index))
                __state = p._window.Begin(index, Stopwatch.GetTimestamp());
        }

        // A void finalizer observes exceptions without replacing or suppressing them.
        private static void EndMethod(TimingWindow.Ticket __state)
        {
            if (__state.Owner != null) __state.Owner.End(__state, Stopwatch.GetTimestamp());
        }

        private void Finish(string reason, bool consumeRequest = true)
        {
            int phase = _phase;
            _phase = 0;
            if (phase == 0) return;
            if (phase == 1 && reason != "timeout")
            {
                _scene = null;
                _nextSceneCheck = Time.realtimeSinceStartup + 1;
                return; // A warmup interruption does not consume the requested capture.
            }
            _window?.Stop();
            _nativeTiming?.Dispose();
            try
            {
                if (_harmony != null)
                    HookCleanup.Remove(_harmony, _patchedMethods, Id,
                        (method, ex) => _patchFailures.Add("Unpatch failed: " + method.Name + ": " + ex));
            }
            catch (Exception ex) { _patchFailures.Add("Unpatch failed: " + ex); }
            finally { _harmony = null; _indices.Clear(); _patchedMethods.Clear(); }
            if (phase >= 2 || reason == "timeout")
            {
                var report = new CaptureReport {
                    Utc = DateTime.UtcNow.ToString("o"), Reason = reason, Context = _context, EndState = SnapshotState(),
                    Baseline = phase == 2 ? (_baseline ?? _window?.Snapshot()) : _baseline,
                    Instrumented = phase == 3 ? _window?.Snapshot() : null,
                    PatchFailures = _patchFailures.ToArray(),
                    NativeBaseline = phase == 2 ? _nativeTiming?.Snapshot() : _nativeBaseline,
                    NativeInstrumented = phase == 3 ? _nativeTiming?.Snapshot() : null,
                    FocusPauses = _focusGate?.Pauses ?? 0, FocusPausedSeconds = _focusGate?.PausedSeconds ?? 0,
                    Notes = "Sequential windows, not a controlled A/B speedup. Focus gaps and first returning frame are excluded. Hooks remain inactive during focus pauses and are removed at completion/timeout. Method hooks add overhead. Native recorder overhead is included in both windows; thread filters are unchanged and overlapping native markers must not be summed. Native markers without samples are null. GAME_INCLUSIVE covers game HSceneProc Update/LateUpdate with existing hooks; it is not a canon-only cost. Exclusive time subtracts only measured nested methods, not all external/game work. Other game/external callbacks are not comprehensively covered. Camera timings include native/driver waits. Missing engine timings are null."
                };
                string directory = Path.Combine(Path.GetDirectoryName(Info.Location), "captures");
                string file = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json");
                _writer = Task.Run(() => {
                    Directory.CreateDirectory(directory);
                    using (var stream = File.Create(file)) new DataContractJsonSerializer(typeof(CaptureReport)).WriteObject(stream, report);
                });
                Logger.LogInfo("Timing capture stopped: " + reason + "; report: " + file);
            }
            _window = null;
            _nativeTiming = null;
            _scene = null;
            if (consumeRequest) { _remaining.Value = Math.Max(0, _remaining.Value - 1); Config.Save(); }
            _nextSceneCheck = Time.realtimeSinceStartup + 30;
        }

        private void OnDisable() { Finish("plugin disabled", false); }
        private void OnDestroy() {
            CanonPlugins.Shared.PluginSessionGate.Dispose(); CanonPlugins.Shared.PluginSessionGate.Dispose(); Finish("plugin destroyed", false); if (_instance == this) _instance = null; }

        // Only before/after sampling: reflection, boxing and formatting never run in timing hooks.
        private static string SnapshotState()
        {
            var result = new StringBuilder();
            foreach (var info in Chainloader.PluginInfos.Values)
            {
                object target = info.Instance;
                if (target == null) continue;
                string type = target.GetType().FullName;
                if (type != "MainGameDisplacementMap.Plugin" && type != "SimpleAfterimage.Plugin" &&
                    type != "MainGameKaleidoscope.Plugin" && type != "MainGameMilkSpray.Plugin") continue;
                result.Append("; ").Append(type).Append('{');
                foreach (string path in new[] { "_useManagedMapSampling", "_managedMapSamplingChecked", "_dispMapGeneration", "_dispMapVersion",
                    "_mapSamplingMode", "_mapSamplingMaxError", "_sourceFrameCopyFailed", "_sourceFrameCapturedAt",
                    "_rtW", "_rtH", "_cfgEnabled", "_cfgStrength", "_cfgPulse", "_meshW", "_meshH", "_drawCount", "_rtWidth", "_rtHeight", "_cfgCaptureInterval",
                    "_warpAfterimageIntensity", "_kaleidoscopeThoughtMaskActive", "_strength", "_manualActive", "_thoughtActive", "_renderer._count" })
                {
                    try
                    {
                        object value = target;
                        bool exists = true;
                        foreach (string part in path.Split('.'))
                        {
                            var field = value?.GetType().GetField(part, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                            if (field == null) { exists = false; break; }
                            value = field.GetValue(value);
                        }
                        if (!exists) continue;
                        if (value is ConfigEntryBase entry) value = entry.BoxedValue;
                        result.Append(path).Append('=').Append(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)).Append(',');
                    }
                    catch { result.Append(path).Append("=unavailable,"); }
                }
                result.Append('}');
            }
            return result.ToString();
        }
    }

    [DataContract]
    internal sealed class CaptureReport
    {
        [DataMember] public string Utc, Reason, Context, EndState, Notes;
        [DataMember] public WindowReport Baseline, Instrumented;
        [DataMember] public string[] PatchFailures;
        [DataMember] public NativeMarkerReport[] NativeBaseline, NativeInstrumented;
        [DataMember] public int FocusPauses;
        [DataMember] public float FocusPausedSeconds;
    }
}
