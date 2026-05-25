using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace MainGameHScenePoseInjector
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInProcess("KoikatsuSunshine")]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string GUID = "canon.maingame.hscene.poseinjector";
        public const string NAME = "MainGameHScenePoseInjector";
        public const string VERSION = "0.1.0";

        internal static Plugin Instance;
        internal static ManualLogSource Log;
        internal static string PluginDir;
        internal static StreamWriter LogFile;

        private PluginSettings _settings;
        private string _settingsPath;
        private string _logPath;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            PluginDir = Path.GetDirectoryName(Info.Location);
            _settingsPath = Path.Combine(PluginDir, "MainGameHScenePoseInjectorSettings.json");
            _logPath = Path.Combine(PluginDir, "MainGameHScenePoseInjector.log");

            try
            {
                LogFile = new StreamWriter(_logPath, append: false, encoding: new System.Text.UTF8Encoding(false))
                {
                    AutoFlush = true
                };
            }
            catch (Exception ex)
            {
                Log.LogWarning("log file open failed: " + ex.Message);
            }

            _settings = SettingsStore.LoadOrCreate(_settingsPath, LogInfo, LogWarn);
            LogInfo("plugin awake version=" + VERSION + " dir=" + PluginDir);
        }

        private void OnDestroy()
        {
            try
            {
                RestoreAllSnapshots("plugin-destroy");
                LogFile?.Flush();
                LogFile?.Close();
            }
            catch { }
        }

        private void Update()
        {
            try
            {
                if (Input.GetKeyDown(_settings.ToggleUiKey))
                {
                    _settings.UiVisible = !_settings.UiVisible;
                    SaveSettings();
                    LogInfo("ui toggle visible=" + _settings.UiVisible);
                }

                UpdateHSceneDetect();
                AutoRestoreIfControllerChanged();
            }
            catch (Exception ex)
            {
                LogWarn("update exception: " + ex.Message);
            }
        }

        private void OnGUI()
        {
            try
            {
                if (!_settings.UiVisible || !IsInHScene())
                    return;
                DrawWindow();
            }
            catch (Exception ex)
            {
                LogWarn("ongui exception: " + ex.Message);
            }
        }

        internal void SaveSettings()
        {
            SettingsStore.Save(_settingsPath, _settings, LogWarn);
        }

        internal static void LogInfo(string msg)
        {
            try
            {
                Log?.LogInfo(msg);
                LogFile?.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " [INFO] " + msg);
            }
            catch { }
        }

        internal static void LogWarn(string msg)
        {
            try
            {
                Log?.LogWarning(msg);
                LogFile?.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " [WARN] " + msg);
            }
            catch { }
        }
    }
}
