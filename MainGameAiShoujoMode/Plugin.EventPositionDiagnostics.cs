using System;
using System.IO;
using BepInEx.Configuration;
using MainGameAiShoujoMode.Conversation;

namespace MainGameAiShoujoMode
{
    public sealed partial class Plugin
    {
        private ConfigEntry<bool> _eventPositionTraceEnabled;
        private CharacterLoadTrace _eventPositionTrace;
        internal static bool EventPositionTraceEnabled => _instance != null && _instance.isActiveAndEnabled
            && _instance._eventPositionTraceEnabled?.Value == true;
        private void InitializeEventPositionDiagnostics()
        {
            _eventPositionTraceEnabled = Config.Bind("Diagnostics", "EventPositionTraceEnabled", false,
                "会話・H開始操作・H開始完了時に男性・女性・視点・H地点の座標と位置関係、全H候補の採用／除外理由、配置家具の利用位置をUserData/Logs/AiEventPositionsへ記録します。OFFでは座標収集・整形・ファイル出力を行いません。");
            _eventPositionTraceEnabled.SettingChanged += EventPositionTraceChanged;
        }
        private void EventPositionTraceChanged(object sender, EventArgs args)
        { if (!_eventPositionTraceEnabled.Value) { _eventPositionTrace?.Dispose(); _eventPositionTrace = null; } }
        internal static void RecordEventPositions(Func<string> snapshot)
        {
            var plugin = _instance;
            if (plugin == null || !plugin.isActiveAndEnabled || plugin._eventPositionTraceEnabled?.Value != true) return;
            if (plugin._eventPositionTrace == null)
                plugin._eventPositionTrace = CharacterLoadTrace.Create(
                    () => plugin.isActiveAndEnabled && plugin._eventPositionTraceEnabled.Value,
                    Path.Combine(BepInEx.Paths.GameRootPath, "UserData", "Logs", "AiEventPositions"),
                    error => { if (plugin._eventPositionTraceEnabled.Value) plugin.Logger.LogWarning("EVENT_POSITION_TRACE_FAILED " + error.Message); });
            plugin._eventPositionTrace?.Mark(snapshot);
        }
        private void DisposeEventPositionDiagnostics()
        {
            _eventPositionTrace?.Dispose(); _eventPositionTrace = null;
            if (_eventPositionTraceEnabled != null) _eventPositionTraceEnabled.SettingChanged -= EventPositionTraceChanged;
        }
    }
}
