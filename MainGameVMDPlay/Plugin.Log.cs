using System;
using MainGameLogRelay;
using BepInEx.Logging;

namespace MainGameVMDPlay
{
    public sealed partial class Plugin
    {
        private const string RelayOwner  = Guid;
        private const string RelayLogKey = "main/" + PluginName;
        private ManualLogSource _libraryLogSource;

        private ManualLogSource CreateLibraryLogSource()
        {
            // Do not register this source globally: forward only when this plugin's switch allows it.
            _libraryLogSource = new ManualLogSource(PluginName + ".Library");
            _libraryLogSource.LogEvent += (_, args) => {
                if ((_cfgLogEnabled != null && _cfgLogEnabled.Value)
                    || (args.Level & (LogLevel.Fatal | LogLevel.Error | LogLevel.Warning)) != 0)
                    Logger.Log(args.Level, args.Data);
            };
            return _libraryLogSource;
        }

        private void InitLogRelay()
        {
            if (LogRelayApi.IsAvailable)
            {
                LogRelayApi.SetOwnerLogKey(RelayOwner, RelayLogKey);
                LogRelayApi.SetOwnerEnabled(RelayOwner, _cfgLogEnabled.Value);
            }
            else
            {
                if (_cfgLogEnabled.Value) Logger.LogWarning("[MainGameVMDPlay] MainGameLogRelay not loaded — falling back to BepInEx console logging");
            }
        }

        private void LogInfo(string message)
        {
            if (_cfgLogEnabled == null || !_cfgLogEnabled.Value) return;
            DiagnosticLog("INFO " + message);
            if (LogRelayApi.IsAvailable) { LogRelayApi.Info(RelayOwner, message); return; }
            Logger.LogInfo("[" + PluginName + "] " + message);
        }

        private void LogWarn(string message)
        {
            DiagnosticLog("WARN " + message);
            if (LogRelayApi.IsAvailable) { LogRelayApi.Warn(RelayOwner, message); return; }
            Logger.LogWarning("[" + PluginName + "] " + message);
        }

        private void LogError(string message)
        {
            DiagnosticLog("ERROR " + message);
            if (LogRelayApi.IsAvailable) { LogRelayApi.Error(RelayOwner, message); return; }
            Logger.LogError("[" + PluginName + "] " + message);
        }

        private void LogDebug(string message)
        {
            if (_cfgLogEnabled == null || !_cfgLogEnabled.Value || _settings == null || !_settings.VerboseLog) return;
            if (LogRelayApi.IsAvailable) { LogRelayApi.Debug(RelayOwner, message); return; }
            Logger.LogInfo("[" + PluginName + "] [DEBUG] " + message);
        }

        private void LogDebugLazy(Func<string> factory)
        {
            if (_cfgLogEnabled == null || !_cfgLogEnabled.Value || _settings == null || !_settings.VerboseLog) return;
            if (LogRelayApi.IsAvailable) { LogRelayApi.LogLazy(RelayOwner, LogRelayLevel.Debug, factory); return; }
            Logger.LogInfo("[" + PluginName + "] [DEBUG] " + factory());
        }
    }
}
