using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace CoordCatalog
{
    [BepInDependency("canon.gate", "2.2.0")]
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInProcess("KoikatsuSunshine")]
    public class CoordCatalogPlugin : BaseUnityPlugin
    {
        public const string GUID = "koikatsu.coordcatalog";
        public const string PluginName = "CoordCatalog";
        public const string Version = "1.0.0";

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<KeyboardShortcut> CaptureKey;
        internal static ConfigEntry<bool> ShowCoordName;
        internal static ConfigEntry<bool> ShowIndex;
        internal static ConfigEntry<int> LabelFontSize;
        internal static ConfigEntry<int> OutputWidth;
        internal static ConfigEntry<int> OutputHeight;

        internal static string PluginDir;
        internal static StreamWriter FileLog;
        private ConfigEntry<bool> _logEnabled;
        internal static bool LoggingEnabled { get; private set; }

        private CatalogCapture _capture;

        private void Awake()
        {
            PluginDir = Path.GetDirectoryName(Info.Location);
            _logEnabled = Config.Bind("Logging", "LogEnabled", false,
                "通常ログと専用ファイルの出力。OFFでは起動時のログファイルも作成しない。変更は即時反映。");
            _logEnabled.SettingChanged += (_, __) => ApplyLogging();
            ApplyLogging();

            Enabled = Config.Bind("General", "Enabled", false,
                "trueにするとCaptureKeyでカタログ撮影を開始する");
            CaptureKey = Config.Bind("Input", "CaptureKey",
                new KeyboardShortcut(KeyCode.F10),
                "カタログ撮影を開始するキー");
            ShowCoordName = Config.Bind("Display", "ShowCoordinateName", true,
                "各コマの下にコーデ名を表示する");
            ShowIndex = Config.Bind("Display", "ShowIndex", true,
                "各コマの下にインデックス番号を表示する");
            LabelFontSize = Config.Bind("Display", "LabelFontSize", 32,
                "ラベルのフォントサイズ（pt）。大きくするとラベル欄も広がる");
            OutputWidth = Config.Bind("Display", "OutputWidth", 1920,
                "出力画像の幅（px）");
            OutputHeight = Config.Bind("Display", "OutputHeight", 1080,
                "出力画像の高さ（px）");

            _capture = gameObject.AddComponent<CatalogCapture>();
            _capture.Init(this);

            if (LoggingEnabled) Logger.LogInfo($"{PluginName} v{Version} loaded.");
            FileLog?.WriteLine($"[{DateTime.Now:HH:mm:ss}] Awake complete. PluginDir={PluginDir}");
            CanonPlugins.Shared.PluginSessionGate.Bind(this, GUID, () => { _capture.CancelCapture(); }, () => { _capture.CancelCapture(); });
        }

        private void ApplyLogging()
        {
            FileLog?.Dispose();
            FileLog = null;
            LoggingEnabled = _logEnabled.Value;
            if (!LoggingEnabled) return;
            var logDir = Path.Combine(PluginDir, "_logs");
            Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, $"coordcatalog_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log");
            FileLog = new StreamWriter(logPath, false, System.Text.Encoding.UTF8) { AutoFlush = true };
        }

        private void OnDestroy()
        {
            CanonPlugins.Shared.PluginSessionGate.Dispose();
            FileLog?.Dispose();
            FileLog = null;
            LoggingEnabled = false;
        }

        private void Update()
        {
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed) return;
            if (Enabled.Value && CaptureKey.Value.IsDown() && !_capture.IsRunning)
                _capture.StartCapture();
        }
    }
}
