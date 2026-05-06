using System;
using BepInEx;
using BepInEx.Configuration;
using MainGameLogRelay;

namespace MainGameTransformGizmo
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInProcess("KoikatsuSunshine")]
    [BepInDependency(MainGameLogRelay.Plugin.Guid, BepInDependency.DependencyFlags.HardDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string GUID = "com.kks.maingame.transformgizmo";
        public const string PluginName = "MainGameTransformGizmo";
        public const string Version = "0.1.0";
        private const string RelayOwner = GUID;
        private const string RelayLogKey = "main/" + PluginName;
        private ConfigEntry<bool> _cfgEnableLogs;

        public static Plugin Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            _cfgEnableLogs = Config.Bind(
                "Logging",
                "EnableLogs",
                false,
                "MainGameLogRelay経由ログのON/OFF");
            _cfgEnableLogs.SettingChanged += (_, __) => ApplyRelayLoggingState();
            ApplyRelayLoggingState();

            WriteLog("INFO", $"=== {PluginName} {Version} start ===");
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

        private void ApplyRelayLoggingState()
        {
            if (!LogRelayApi.IsAvailable)
                return;

            bool enabled = _cfgEnableLogs != null && _cfgEnableLogs.Value;
            LogRelayApi.SetOwnerLogKey(RelayOwner, RelayLogKey);
            LogRelayApi.SetOwnerEnabled(RelayOwner, enabled);
        }

        private void WriteLog(string level, string message)
        {
            if (LogRelayApi.IsAvailable)
            {
                switch (level)
                {
                    case "ERROR":
                        LogRelayApi.Error(RelayOwner, message);
                        return;
                    case "WARN":
                        LogRelayApi.Warn(RelayOwner, message);
                        return;
                    case "DEBUG":
                        LogRelayApi.Debug(RelayOwner, message);
                        return;
                    default:
                        LogRelayApi.Info(RelayOwner, message);
                        return;
                }
            }

            switch (level)
            {
                case "ERROR":
                    Logger.LogError("[" + PluginName + "] " + message);
                    return;
                case "WARN":
                    Logger.LogWarning("[" + PluginName + "] " + message);
                    return;
                default:
                    Logger.LogInfo("[" + PluginName + "] " + message);
                    return;
            }
        }
    }
}
