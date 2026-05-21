using System;
using UnityEngine;

namespace MainGameVoiceImpactBoost
{
    internal sealed class HSceneMonitor
    {
        private readonly PluginConfig _config;
        private readonly PluginFileLogger _logger;
        private readonly Action _resetOneShotState;
        private HSceneProc _currentProc;
        private float _nextCheckAt;

        public HSceneMonitor(PluginConfig config, PluginFileLogger logger, Action resetOneShotState)
        {
            _config = config;
            _logger = logger;
            _resetOneShotState = resetOneShotState;
        }

        public void Update()
        {
            if (Time.unscaledTime - _nextCheckAt < 1f)
            {
                return;
            }

            _nextCheckAt = Time.unscaledTime;
            HSceneProc proc = UnityEngine.Object.FindObjectOfType<HSceneProc>();
            if (proc == null && _currentProc != null)
            {
                _resetOneShotState();
                LogVerbose("hscene ended, oneshot reset");
            }
            else if (proc != null && _currentProc == null)
            {
                _resetOneShotState();
                LogVerbose("hscene started, oneshot reset");
            }

            _currentProc = proc;
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
