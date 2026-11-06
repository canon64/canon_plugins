using System;
using System.IO;
using MapEnvironmentRuntime;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    public sealed partial class Plugin
    {
        private AiModeSession _mode;
        private void BeginMode(TitleScene title, AudioSource confirmation)
        {
            if (!ModeAllowed) return;
            if (TitleModeBusy) return;
            var trace = BeginStartupTrace("ai");
            try
            {
                trace?.Mark("content.validate.begin");
                string content = Path.Combine(Path.GetDirectoryName(Info.Location), "Content");
                var settings = Parameters.Load(Path.Combine(content, "parameters.json"));
                if (!File.Exists(Path.Combine(content, settings.T("content.bundle")))) throw new FileNotFoundException("AI少女モードの環境Bundleがありません。");
                if (!File.Exists(Path.Combine(settings.T("island.directory"), "manifest.json"))) throw new FileNotFoundException("抽出済みのAI少女マップがありません。");
                trace?.Mark("content.validate.end");
                var root = new GameObject("AI Shoujo Mode Session");
                _mode = root.AddComponent<AiModeSession>();
                _mode.StartupDiagnostics = trace;
                _mode.Initialize(title, content, settings, confirmation, message => Logger.LogInfo(message), exception => Logger.LogError(exception), () => _mode = null);
                _mode.RestartMoment = () => { if (ModeAllowed && title != null) BeginMode(title, null); };
                Logger.LogInfo("AI_SHOUJO_MODE_ENTER_REQUESTED");
            }
            catch (Exception ex)
            {
                trace?.Mark("entry.failed", () => ex.ToString()); trace?.Dispose();
                if (_mode != null) Destroy(_mode.gameObject);
                _mode = null; Logger.LogError("AI_SHOUJO_MODE_ENTER_FAILED " + ex);
            }
        }
        private void CloseModeImmediately() { if (_mode != null) _mode.CancelForGate(); }
    }
}
