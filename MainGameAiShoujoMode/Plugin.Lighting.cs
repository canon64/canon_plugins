using System;
using System.IO;
using BepInEx.Configuration;

namespace MainGameAiShoujoMode
{
    public sealed partial class Plugin
    {
        private ConfigEntry<bool> _lightingEnabled, _lightingTrace, _nativeLighting;
        internal static bool AiNativeLighting { get => _instance?._nativeLighting?.Value == true; set { if (_instance?._nativeLighting != null) _instance._nativeLighting.Value = value; } }
        internal static bool AiLightingEnabled { get => _instance?._lightingEnabled?.Value == true; set { if (_instance?._lightingEnabled != null) _instance._lightingEnabled.Value = value; } }
        internal static bool AiLightingTrace { get => _instance?._lightingTrace?.Value == true; set { if (_instance?._lightingTrace != null) _instance._lightingTrace.Value = value; } }
        internal static string AiLightingProfilePath => Path.Combine(BepInEx.Paths.GameRootPath, "UserData", "AIShoujoMode", "h-lighting-profile.json");
        internal static string AiLightingTracePath => Path.Combine(BepInEx.Paths.GameRootPath, "UserData", "Logs", "AiLighting");
        internal static event Action AiLightingChanged;
        private void InitializeLighting()
        {
            _nativeLighting = Config.Bind("AiLighting", "UseNativeHRendering", true, "歩行中のキャラ専用補助ライトを自動適用します。島の色補正・昼夜・天候は維持し、本編Hの画面効果で上書きしません。");
            _lightingEnabled = Config.Bind("AiLighting", "Enabled", false, "比較で確認・保存したH用照明をAI少女の歩行中にも適用します。昼夜・天候は維持。OFFで元の照明へ戻します。");
            _lightingTrace = Config.Bind("AiLighting", "DiagnosticsEnabled", false, "歩行・H開始・最初の描画・復帰の照明差分を一度ずつ記録。OFFでは探索・整形・描画購読・ファイル出力を停止します。");
            _nativeLighting.SettingChanged += LightingChanged; _lightingEnabled.SettingChanged += LightingChanged; _lightingTrace.SettingChanged += LightingChanged;
        }
        private void LightingChanged(object sender, EventArgs args) => AiLightingChanged?.Invoke();
        private void DisposeLighting()
        { if (_nativeLighting != null) _nativeLighting.SettingChanged -= LightingChanged; if (_lightingEnabled != null) _lightingEnabled.SettingChanged -= LightingChanged; if (_lightingTrace != null) _lightingTrace.SettingChanged -= LightingChanged; }
    }
}
