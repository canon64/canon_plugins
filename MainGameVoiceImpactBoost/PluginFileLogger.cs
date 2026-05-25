using System;
using System.IO;
using BepInEx.Logging;

namespace MainGameVoiceImpactBoost
{
    internal sealed class PluginFileLogger
    {
        private readonly ManualLogSource _logger;
        private readonly string _logFilePath;

        public PluginFileLogger(ManualLogSource logger, string pluginLocation, string fileName)
        {
            _logger = logger;
            string pluginDir = Path.GetDirectoryName(pluginLocation) ?? Directory.GetCurrentDirectory();
            _logFilePath = Path.Combine(pluginDir, fileName);
        }

        public void LogInfo(string message)
        {
            _logger.LogInfo(message);
            Append("[INFO] " + message);
        }

        private void Append(string line)
        {
            try
            {
                File.AppendAllText(
                    _logFilePath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + line + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
