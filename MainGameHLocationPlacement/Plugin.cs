using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using CanonGate;

namespace MainGameHLocationPlacement
{
    [BepInPlugin(Guid, "MainGameHLocationPlacement", "1.0.5")]
    [BepInDependency(GateApi.PluginGuid, "2.4.0")]
    [BepInDependency(MainGameHLocationCoordinates.Plugin.Guid)]
    [BepInDependency(RuntimeBridge.ComposerGuid)]
    [BepInDependency(RuntimeBridge.HipGuid)]
    [BepInDependency(RuntimeBridge.MapGuid)]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.kks.maingame.hlocationplacement";
        internal static Plugin Instance;
        internal PlacementSession Session;
        private IDisposable _registration;
        private StatusHooks _statusHooks;
        private GateApi.Permit _statusPermit;
        private ConfigEntry<bool> _logs;
        internal string Status = "H場所補正：開始待ち";
        private void Awake()
        {
            Instance = this;
            _logs = Config.Bind("Logging", "EnableLogs", false, "保存・場所変更・適用時の診断ログ。OFFでは診断収集も停止します。");
            _statusHooks = new StatusHooks();
            _registration = GateApi.Register(Guid, Begin, End);
        }
        internal void Message(string text) => Status = "H場所補正：" + text;
        internal bool Diagnostics => _logs != null && _logs.Value;
        internal bool CanShowStatus => _statusPermit.IsValid;
        internal void SetDiagnostics(bool enabled)
        {
            if (DiagnosticLogSetting.Set(Config, _logs, enabled) && enabled)
                Diagnostic(() => "H場所配置の診断ログをONにしました。 " + Status);
        }
        internal void Diagnostic(Func<string> message) { if (Diagnostics) Logger.LogInfo(message()); }
        private void Begin()
        {
            if (Session != null || GateApi.Current?.Kind != GateApi.SessionKind.H) return;
            _statusPermit = GateApi.Capture(Guid);
            try { Session = new PlacementSession(Path.Combine(Path.GetDirectoryName(Info.Location), "Origins"), this); Message("通常の新規・上書き保存で保存元を記録します。"); }
            catch (Exception ex) { Message("接続停止：" + ex.Message); Diagnostic(() => ex.ToString()); }
        }
        private void End()
        {
            var session = Session; Session = null;
            _statusPermit = default(GateApi.Permit);
            try { session?.Dispose(); } catch (Exception ex) { Message("終了処理：" + ex.Message); Diagnostic(() => ex.ToString()); }
        }
        private void OnDisable() => End();
        private void OnDestroy() { _registration?.Dispose(); End(); _statusHooks?.Dispose(); if (ReferenceEquals(Instance, this)) Instance = null; }
    }
}
