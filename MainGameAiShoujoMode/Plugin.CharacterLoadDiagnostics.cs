using System;
using System.IO;
using BepInEx.Configuration;
using MainGameAiShoujoMode.Conversation;

namespace MainGameAiShoujoMode
{
    public sealed partial class Plugin
    {
        private ConfigEntry<bool> _characterLoadTraceEnabled;
        internal static event Action CharacterLoadTraceStopped;
        private void InitializeCharacterLoadDiagnostics()
        {
            _characterLoadTraceEnabled = Config.Bind("Diagnostics", "CharacterLoadTraceEnabled", false,
                "女の子を呼び出した時だけ、読込段階ごとの体型値・胸周辺の骨・体型MODの状態を記録します。既定OFF。OFFで収集・遅延記録・専用ファイル出力を停止します。");
            _characterLoadTraceEnabled.SettingChanged += CharacterLoadTraceChanged;
        }
        private void CharacterLoadTraceChanged(object sender, EventArgs args)
        { if (!_characterLoadTraceEnabled.Value) CharacterLoadTraceStopped?.Invoke(); }
        internal static CharacterLoadTrace BeginCharacterLoadTrace()
        {
            var plugin = _instance;
            if (plugin?._characterLoadTraceEnabled?.Value != true) return null;
            return CharacterLoadTrace.Create(() => plugin.isActiveAndEnabled && plugin._characterLoadTraceEnabled.Value,
                Path.Combine(BepInEx.Paths.GameRootPath, "UserData", "Logs", "AiCharacterLoad"),
                error => { if (plugin._characterLoadTraceEnabled.Value) plugin.Logger.LogWarning("CHARACTER_LOAD_TRACE_FAILED " + error.Message); });
        }
        private void DisposeCharacterLoadDiagnostics()
        {
            CharacterLoadTraceStopped?.Invoke();
            if (_characterLoadTraceEnabled != null) _characterLoadTraceEnabled.SettingChanged -= CharacterLoadTraceChanged;
        }
    }
}
