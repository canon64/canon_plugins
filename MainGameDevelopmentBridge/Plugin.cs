using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using CanonGate;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MainGameDevelopmentBridge
{
    [BepInPlugin(Guid, "MainGameDevelopmentBridge", "1.0.0")]
    [BepInDependency(GateApi.PluginGuid, "2.4.0")]
    [BepInDependency("com.kks.maingame.aishoujomode", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    [BepInProcess("CharaStudio")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.kks.maingame.developmentbridge";
        private static Plugin _instance;
        private ConfigEntry<bool> _enabled, _diagnostics;
        private string _startupMode = "none";
        private GateApi.ActivityRegistration _activity;
        private GateApi.ActivityPermit _permit;
        private SynchronizationContext _main;
        private PipeServer _server;
        private MailboxServer _mailbox;
        private Harmony _patches;
        private ReflectionCommands _commands;
        private bool _running, _autoAttempted;
        private string _autoState = "waiting-title", _autoError;
        private SceneFadeCanvas _autoFade;
        private Action<FadeCanvas.Fade> _autoFadeComplete;
        private int _generation;
        private readonly HashSet<TaskCompletionSource<object>> _pending = new HashSet<TaskCompletionSource<object>>();
        private string PipeName => "KKS.DevelopmentBridge." + System.Diagnostics.Process.GetCurrentProcess().Id;
        private void Awake()
        {
            _instance = this;
            _enabled = Config.Bind("Bridge", "Enabled", true, "ローカル開発ブリッジ。OFFで接続・保留要求・拡張を停止します。");
            Config.Bind("Startup", "AutoMode", "none", "互換用。自動入場には使用しません。開発用起動引数 --kks-development-mode=ai|hotel|none を使用します。");
            // Starting the game never authorizes mode entry. All navigation is explicit API work.
            _startupMode = "none";
            _autoState = "disabled";
            _diagnostics = Config.Bind("Diagnostics", "Enabled", false, "追加ログ。OFFでは追加ログを生成しません。");
            _main = SynchronizationContext.Current ?? throw new InvalidOperationException("Unity main-thread synchronization context unavailable");
            _commands = new ReflectionCommands();
            _commands.Context.OutputDirectory = Path.Combine(Paths.GameRootPath, "UserData", "DevelopmentBridge");
            _activity = GateApi.RegisterActivity(Guid, GateApi.ActivityKind.StartupService, StartRuntime, StopRuntime, includeSessions: false);
            _enabled.SettingChanged += EnabledChanged;
        }
        private void Start() { if (_enabled.Value) _activity.Begin(this); }
        private void EnabledChanged(object sender, EventArgs args)
        {
            if (!_enabled.Value) _activity.End();
            else if (isActiveAndEnabled && GateApi.Current == null) _activity.Begin(this);
        }
        private void StartRuntime()
        {
            if (!_enabled.Value) return;
            _permit = _activity.Capture(); _running = true; ++_generation;
            _server = new PipeServer(PipeName, Dispatch);
            _mailbox = new MailboxServer(Path.Combine(_commands.Context.OutputDirectory, "mailbox", System.Diagnostics.Process.GetCurrentProcess().Id.ToString()), Dispatch);
            if (_diagnostics.Value) Logger.LogInfo("Development bridge ready: " + PipeName);
        }
        private void StopRuntime()
        {
            _running = false; ++_generation; _server?.Dispose(); _server = null;
            _mailbox?.Dispose(); _mailbox = null;
            if (_autoFade != null && _autoFadeComplete != null) _autoFade.onComplete -= _autoFadeComplete;
            _autoFadeComplete = null; _autoFade = null;
            foreach (var task in _pending.ToArray()) task.TrySetCanceled();
            _pending.Clear(); StopAllCoroutines(); _commands?.Clear();
        }
        private Task<object> Dispatch(Dictionary<string, object> request)
        {
            var completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            int generation = _generation;
            _main.Post(_ =>
            {
                try
                {
                    if (!_running || generation != _generation || !_permit.IsValid) throw new InvalidOperationException("Development bridge permission expired");
                    _pending.Add(completion);
                    string op = ReflectionCommands.Text(request, "op");
                    if (op == "capture") { StartCoroutine(Capture(completion, generation)); return; }
                    object result;
                    if (op == "status") result = Status();
                    else if (op == "find")
                    {
                        var type = ReflectionCommands.ResolveType(ReflectionCommands.Text(request, "type"));
                        string filter = ReflectionCommands.Text(request, "name", "");
                        result = Resources.FindObjectsOfTypeAll(type).Where(o => o != null && o.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= default(int))
                            .Select(o => new { reference = _commands.Pack(o), name = o.name, instanceId = o.GetInstanceID(), path = ObjectPath(o) }).ToArray();
                    }
                    else result = _commands.Execute(request);
                    completion.TrySetResult(result);
                }
                catch (Exception ex) { completion.TrySetException(ex); }
                finally { if (completion.Task.IsCompleted) _pending.Remove(completion); }
            }, null);
            return completion.Task;
        }
        private object Status()
        {
            var type = AccessTools.TypeByName("MainGameAiShoujoMode.Plugin");
            var plugin = type == null ? null : AccessTools.Field(type, "_instance")?.GetValue(null);
            var mode = plugin == null ? null : AccessTools.Field(type, "_mode")?.GetValue(plugin);
            var hotel = plugin == null ? null : AccessTools.Field(type, "_hotel")?.GetValue(plugin);
            bool ready = mode != null && !(bool)AccessTools.Field(mode.GetType(), "_loading").GetValue(mode);
            bool hotelReady = hotel != null && !(bool)AccessTools.Field(hotel.GetType(), "_loading").GetValue(hotel);
            return new { pid = System.Diagnostics.Process.GetCurrentProcess().Id, scene = SceneManager.GetActiveScene().name, aiReady = ready,
                hotelReady, startupMode = _startupMode, autoState = _autoState, autoError = _autoError, pipe = PipeName,
                pipeError = _server?.Completion.IsFaulted == true ? _server.Completion.Exception.GetBaseException().ToString() : null,
                extensions = _commands.Execute(new Dictionary<string, object> { { "op", "extensions" } }) };
        }
        private static string ObjectPath(UnityEngine.Object value)
        {
            var transform = (value as Component)?.transform ?? (value as GameObject)?.transform;
            if (transform == null) return null;
            var path = transform.name;
            while (transform.parent != null) { transform = transform.parent; path = transform.name + "/" + path; }
            return path;
        }
        private IEnumerator Capture(TaskCompletionSource<object> completion, int generation)
        {
            yield return new WaitForEndOfFrame();
            Texture2D texture = null;
            try
            {
                if (!_running || generation != _generation || !_permit.IsValid) throw new InvalidOperationException("Capture cancelled");
                Directory.CreateDirectory(_commands.Context.OutputDirectory);
                string path = Path.Combine(_commands.Context.OutputDirectory, "capture-" + System.Guid.NewGuid().ToString("N") + ".png");
                texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(default(float), default(float), Screen.width, Screen.height), default(int), default(int)); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                completion.TrySetResult(new { path, width = Screen.width, height = Screen.height, scene = SceneManager.GetActiveScene().name });
            }
            catch (Exception ex) { completion.TrySetException(ex); }
            finally { if (texture != null) Destroy(texture); _pending.Remove(completion); }
        }
        private static void TitlePrepared(object __instance, object title, ref IEnumerator __result)
        { __result = ObserveTitle(__result, __instance, title); }
        private static IEnumerator ObserveTitle(IEnumerator original, object plugin, object title)
        {
            try { while (original.MoveNext()) yield return original.Current; }
            finally { (original as IDisposable)?.Dispose(); }
            var owner = _instance;
            if (owner == null || !owner._running || !owner._permit.IsValid || owner._autoAttempted || owner._startupMode == "none") yield break;
            owner._autoAttempted = true;
            owner.QueueAutoEntry(plugin, title);
        }
        private void QueueAutoEntry(object plugin, object title)
        {
            _autoFade = Manager.Scene.sceneFadeCanvas;
            if (_autoFade != null && !_autoFade.isEnd)
            {
                _autoState = "waiting-title-fade";
                _autoFadeComplete = direction =>
                {
                    _autoFade.onComplete -= _autoFadeComplete; _autoFadeComplete = null;
                    _main.Post(_ => AutoEntry(plugin, title), null);
                };
                _autoFade.onComplete += _autoFadeComplete;
            }
            else _main.Post(_ => AutoEntry(plugin, title), null);
        }
        private void AutoEntry(object plugin, object title)
        {
            if (!_running || !_permit.IsValid || !isActiveAndEnabled) return;
            try
            {
                var bindings = (IDictionary)AccessTools.Field(plugin.GetType(), "_bindings").GetValue(plugin);
                if (!bindings.Contains(title)) throw new InvalidOperationException("AI title preparation failed");
                string method = _startupMode == "ai" ? "HandleClick" : _startupMode == "hotel" ? "HandleHotelClick" : throw new ArgumentException("Unknown startup mode");
                AccessTools.Method(plugin.GetType(), method).Invoke(plugin, new[] { title });
                _autoState = "requested-" + _startupMode;
            }
            catch (Exception ex) { _autoState = "failed"; _autoError = ex.GetBaseException().ToString(); }
        }
        private void OnDisable() { _activity?.End(); StopRuntime(); }
        private void OnDestroy()
        {
            _enabled.SettingChanged -= EnabledChanged; _activity?.Dispose(); StopRuntime(); _patches?.UnpatchSelf();
            if (ReferenceEquals(_instance, this)) _instance = null;
        }
    }
}
