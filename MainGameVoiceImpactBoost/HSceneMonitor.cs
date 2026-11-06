using System;
using UnityEngine;
using CanonPlugins.Shared;

namespace MainGameVoiceImpactBoost
{
    internal sealed class HSceneMonitor
    {
        private readonly PluginConfig _config;
        private readonly PluginFileLogger _logger;
        private readonly Action _resetOneShotState;
        private HSceneProc _currentProc;
        private float _nextCheckAt;
        private float _nextDiagnosticAt;

        public HSceneMonitor(PluginConfig config, PluginFileLogger logger, Action resetOneShotState)
        {
            _config = config;
            _logger = logger;
            _resetOneShotState = resetOneShotState;
        }

        public void Update()
        {
            // Session start/stop resets one-shot state; no H scene polling.
        }

        private void LogVerbose(string message)
        {
            if (_config.VerboseLog.Value)
            {
                _logger.LogInfo(message);
            }
        }
    }
}
