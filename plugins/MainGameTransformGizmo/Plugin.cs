using System;
using System.IO;
using System.Text;
using BepInEx;

namespace MainGameTransformGizmo
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInProcess("KoikatsuSunshine")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string GUID = "com.kks.maingame.transformgizmo";
        public const string PluginName = "MainGameTransformGizmo";
        public const string Version = "0.1.0";

        public static Plugin Instance { get; private set; }

        private readonly object _logLock = new object();
        private string _logPath;

        private void Awake()
        {
            Instance = this;

            string pluginDir = Path.GetDirectoryName(Info.Location) ?? Paths.PluginPath;
            string logDir = Path.Combine(pluginDir, "_logs");
            Directory.CreateDirectory(logDir);
            _logPath = Path.Combine(logDir, "info.txt");

            File.WriteAllText(
                _logPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [INFO] === {PluginName} {Version} start ==={Environment.NewLine}",
                Encoding.UTF8);
            Logger.LogInfo($"=== {PluginName} {Version} start ===");
        }

        private void OnDestroy()
        {
            WriteLog("INFO", "plugin destroy");
            if (ReferenceEquals(Instance, this))
                Instance = null;
        }

        internal void LogInfo(string message)
        {
            WriteLog("INFO", message);
        }

        private void WriteLog(string level, string message)
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
            Logger.LogInfo(message);
            if (string.IsNullOrEmpty(_logPath)) return;

            lock (_logLock)
            {
                File.AppendAllText(_logPath, line + Environment.NewLine, Encoding.UTF8);
            }
        }
    }
}
