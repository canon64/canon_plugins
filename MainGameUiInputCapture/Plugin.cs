using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using MainGameLogRelay;
using UnityEngine;

namespace MainGameUiInputCapture
{
    [BepInPlugin(Guid, PluginName, Version)]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    [BepInProcess("CharaStudio")]
    [BepInDependency(MainGameLogRelay.Plugin.Guid, BepInDependency.DependencyFlags.HardDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.kks.maingame.uiinputcapture";
        public const string PluginName = "MainGameUiInputCapture";
        public const string Version = "1.0.0";
        private const string RelayOwner = Guid;
        private const string RelayLogKey = "main/" + PluginName;

        internal static Plugin Instance { get; private set; }

        private string _pluginDir;
        private ConfigEntry<bool> _cfgEnableLogs;
        private UiInputCaptureSettings _settings;
        private UiInputCaptureRuntime _runtime;
        private readonly object _debugOwnersSync = new object();
        private readonly HashSet<string> _debugEnabledOwners = new HashSet<string>(System.StringComparer.Ordinal);

        private void Awake()
        {
            Instance = this;
            _pluginDir = Path.GetDirectoryName(Info.Location) ?? string.Empty;
            _cfgEnableLogs = Config.Bind(
                "Logging",
                "EnableLogs",
                false,
                "MainGameLogRelay経由ログのON/OFF");
            _cfgEnableLogs.SettingChanged += (_, __) => ApplyRelayLoggingState();
            ApplyRelayLoggingState();

            _settings = SettingsStore.LoadOrCreate(_pluginDir, LogInfo, LogWarn, LogError);
            _runtime = new UiInputCaptureRuntime(this);

            LogInfo("loaded");
            LogInfo("settings=" + Path.Combine(_pluginDir, SettingsStore.FileName));
        }

        private void LateUpdate()
        {
            _runtime?.FrameUpdate();
        }

        private void OnDestroy()
        {
            _runtime?.ReleaseAll("plugin destroy");
            SaveSettings();
            LogInfo("destroyed");
            _runtime = null;
            Instance = null;
        }

        private void SaveSettings()
        {
            SettingsStore.Save(_pluginDir, _settings, LogWarn);
        }

        private void ApplyRelayLoggingState()
        {
            if (!LogRelayApi.IsAvailable)
                return;

            bool enabled = _cfgEnableLogs != null && _cfgEnableLogs.Value;
            LogRelayApi.SetOwnerLogKey(RelayOwner, RelayLogKey);
            LogRelayApi.SetOwnerEnabled(RelayOwner, enabled);
        }

        internal UiInputCaptureRuntime Runtime => _runtime;
        internal bool DetailLogEnabled => _settings != null && _settings.DetailLogEnabled;
        internal bool LogStateOnTransition => _settings == null || _settings.LogStateOnTransition;

        internal void SetOwnerDebug(string ownerKey, bool enabled)
        {
            if (!TryNormalizeKey(ownerKey, out string owner))
                return;

            lock (_debugOwnersSync)
            {
                if (enabled)
                    _debugEnabledOwners.Add(owner);
                else
                    _debugEnabledOwners.Remove(owner);
            }
        }

        internal bool IsOwnerDebugEnabled(string ownerKey)
        {
            if (!TryNormalizeKey(ownerKey, out string owner))
                return false;

            lock (_debugOwnersSync)
            {
                return _debugEnabledOwners.Contains(owner);
            }
        }

        internal void LogDebugForOwner(string ownerKey, string message)
        {
            if (!IsOwnerDebugEnabled(ownerKey))
                return;

            LogDebugRaw(message);
        }

        internal void LogDebugRaw(string message)
        {
            if (LogRelayApi.IsAvailable)
            {
                LogRelayApi.Debug(RelayOwner, message);
                return;
            }

            Logger.LogInfo("[" + PluginName + "] " + message);
        }

        internal void LogInfo(string message)
        {
            if (LogRelayApi.IsAvailable)
            {
                LogRelayApi.Info(RelayOwner, message);
                return;
            }

            Logger.LogInfo("[" + PluginName + "] " + message);
        }

        internal void LogWarn(string message)
        {
            if (LogRelayApi.IsAvailable)
            {
                LogRelayApi.Warn(RelayOwner, message);
                return;
            }

            Logger.LogWarning("[" + PluginName + "] " + message);
        }

        internal void LogError(string message)
        {
            if (LogRelayApi.IsAvailable)
            {
                LogRelayApi.Error(RelayOwner, message);
                return;
            }

            Logger.LogError("[" + PluginName + "] " + message);
        }

        internal void LogDebug(string message)
        {
            if (!DetailLogEnabled)
                return;
            LogDebugRaw(message);
        }

        private static bool TryNormalizeKey(string raw, out string normalized)
        {
            normalized = raw == null ? string.Empty : raw.Trim();
            return normalized.Length > 0;
        }
    }
}
