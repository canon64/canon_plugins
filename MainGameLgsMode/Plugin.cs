using System;
using System.IO;
using BepInEx;

namespace MainGameLgsMode
{
    [BepInPlugin(Guid, "MainGameLgsMode", "1.0.0")]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    [BepInDependency("marco.kkapi")]
    [BepInDependency(CanonGate.GateApi.PluginGuid)]
    [BepInDependency("com.kks.maingame.aishoujomode", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.kks.maingame.lgsmode";
        private SettingsStore _settings;
        private ModeController _controller;
        private GameEvents _events;
        private IDisposable _registration;
        private ManualKeyInput _keys;

        private void OnEnable()
        {
            string root = Path.GetDirectoryName(Info.Location);
            _settings = new SettingsStore(root);
            var diagnostics = new DiagnosticOutput(root, message => Logger.LogInfo(message), error => Logger.LogError(error));
            _controller = new ModeController(_settings.Current, new CommandMailbox(root),
                diagnostics.Info, diagnostics.Error);
            _settings.Changed += _controller.Configure;
            _settings.Error += _controller.Report;
            _keys = new ManualKeyInput(() => _settings.Current, _controller);
            // Hardware synchronization is a startup service explicitly requested across game modes.
            // It observes events only; it never changes KKS scene objects or requests H permissions.
            _registration = CanonGate.GateApi.RegisterActivity(Guid,
                CanonGate.GateApi.ActivityKind.StartupService, () => { }, () => { }, false);
            _events = new GameEvents(_controller);
            try { _events.Connect(); }
            catch (Exception error) { _controller.Report(error); OnDisable(); }
        }

        private void OnDisable()
        {
            _keys = null;
            _events?.Dispose(); _events = null;
            _registration?.Dispose(); _registration = null;
            _settings?.Dispose(); _settings = null;
            _controller?.Dispose(); _controller = null;
        }

        // Key events only; no Update/Input polling or visible IMGUI window.
        private void OnGUI() => _keys?.Handle(UnityEngine.Event.current);
        private void OnApplicationFocus(bool focused) { if (!focused) _keys?.Reset(); }
    }
}
