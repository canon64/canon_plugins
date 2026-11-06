using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using MapEnvironmentRuntime;

namespace MainGameAiShoujoMode
{
    public sealed partial class Plugin
    {
        private ConfigEntry<bool> _startupDiagnosticsEnabled;
        private ConfigEntry<bool> _detailedStartupInventoryEnabled;
        private List<StartupTrace> _startupTraces;
        private void InitializeStartupDiagnostics()
        {
            _startupDiagnosticsEnabled = Config.Bind("Diagnostics", "StartupTraceEnabled", false,
                "AI少女・ホテルの起動診断。次のモード開始からUserData/Logs/AiModeStartupへ記録します。OFFで直ちに記録・収集・描画イベント購読を停止します。");
            _startupDiagnosticsEnabled.SettingChanged += StartupDiagnosticsChanged;
            _detailedStartupInventoryEnabled = Config.Bind("Diagnostics", "DetailedRenderInventoryEnabled", false,
                "起動診断の全描画物一覧。StartupTraceEnabledもONのときだけ採取します。大量出力で読込時間が延びるため通常はOFF。OFFでは一覧の探索・整形・ファイル出力を行いません。");
        }
        private void StartupDiagnosticsChanged(object sender, EventArgs args)
        {
            if (!_startupDiagnosticsEnabled.Value) StopStartupTraces();
        }
        private StartupTrace BeginStartupTrace(string mode)
        {
            if (_startupDiagnosticsEnabled?.Value != true) return null;
            // Unity error callbacks can run on render/worker threads; never read Unity properties here.
            var trace = StartupTrace.Create(() => _startupDiagnosticsEnabled.Value,
                Path.Combine(BepInEx.Paths.GameRootPath, "UserData", "Logs", "AiModeStartup"), mode,
                error => { if (_startupDiagnosticsEnabled.Value) Logger.LogError("STARTUP_TRACE_WRITE_FAILED " + error); });
            if (trace == null) return null;
            trace.DetailedInventoryEnabled = () => _detailedStartupInventoryEnabled.Value;
            if (_startupTraces == null) _startupTraces = new List<StartupTrace>();
            _startupTraces.RemoveAll(previous => !previous.Enabled); _startupTraces.Add(trace);
            trace.ObserveUnityErrors();
            trace.AssemblyIdentity(typeof(Plugin).Assembly);
            trace.AssemblyIdentity(typeof(SandboxHost).Assembly);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (assembly.GetName().Name == "VRGIN_OpenXR" || assembly.GetName().Name == "KKS_MainGameVR") trace.AssemblyIdentity(assembly);
            trace.Snapshot("entry.requested");
            if (_startupDiagnosticsEnabled.Value) Logger.LogInfo("STARTUP_TRACE_FILE " + trace.FilePath);
            return trace;
        }
        private void StopStartupTraces()
        {
            if (_startupTraces == null) return;
            foreach (var trace in _startupTraces) trace.Dispose();
            _startupTraces.Clear(); _startupTraces = null;
        }
        private void DisposeStartupDiagnostics()
        {
            StopStartupTraces();
            if (_startupDiagnosticsEnabled != null) _startupDiagnosticsEnabled.SettingChanged -= StartupDiagnosticsChanged;
        }
    }
}
