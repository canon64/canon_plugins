using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace MainGameDisplayEffectViewer
{
    internal static class SettingsStore
    {
        internal const string FileName = "MainGameDisplayEffectViewerSettings.json";
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        internal static MainGameDisplayEffectViewerSettings LoadOrCreate(
            string pluginDir,
            Action<string> logInfo,
            Action<string> logWarn)
        {
            string path = Path.Combine(pluginDir, FileName);
            try
            {
                if (!File.Exists(path))
                {
                    var defaults = Normalize(new MainGameDisplayEffectViewerSettings());
                    Save(pluginDir, defaults, logWarn, createBackup: false);
                    logInfo?.Invoke("settings created: " + path);
                    return defaults;
                }

                string json = File.ReadAllText(path, Encoding.UTF8);
                MainGameDisplayEffectViewerSettings loaded = JsonUtility.FromJson<MainGameDisplayEffectViewerSettings>(json);
                if (loaded == null)
                {
                    logWarn?.Invoke("settings parse null -> defaults");
                    loaded = new MainGameDisplayEffectViewerSettings();
                }

                return Normalize(loaded);
            }
            catch (Exception ex)
            {
                logWarn?.Invoke("settings load failed: " + ex.Message);
                return Normalize(new MainGameDisplayEffectViewerSettings());
            }
        }

        internal static void Save(
            string pluginDir,
            MainGameDisplayEffectViewerSettings settings,
            Action<string> logWarn,
            bool createBackup = true)
        {
            string path = Path.Combine(pluginDir, FileName);
            try
            {
                Directory.CreateDirectory(pluginDir);
                if (createBackup && File.Exists(path))
                {
                    string backupPath = path + ".bak_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    File.Copy(path, backupPath, true);
                }

                MainGameDisplayEffectViewerSettings normalized =
                    Normalize(settings ?? new MainGameDisplayEffectViewerSettings());

                string json = JsonUtility.ToJson(normalized, true);
                File.WriteAllText(path, json + Environment.NewLine, Utf8NoBom);
            }
            catch (Exception ex)
            {
                logWarn?.Invoke("settings save failed: " + ex.Message);
            }
        }

        private static MainGameDisplayEffectViewerSettings Normalize(MainGameDisplayEffectViewerSettings src)
        {
            if (src == null)
                src = new MainGameDisplayEffectViewerSettings();

            if (src.ResolveIntervalSeconds < 0.05f)
                src.ResolveIntervalSeconds = 0.05f;
            if (src.AutoRescanSeconds < 0f)
                src.AutoRescanSeconds = 0f;
            if (src.MaxRows < 50)
                src.MaxRows = 50;
            if (src.MaxRows > 2000)
                src.MaxRows = 2000;

            if (src.InitialSearch == null)
                src.InitialSearch = string.Empty;
            if (string.IsNullOrWhiteSpace(src.TestTopicName))
                src.TestTopicName = "DEV TOOL TOPIC";
            if (string.IsNullOrWhiteSpace(src.TestNormalMessage))
                src.TestNormalMessage = "DEV TOOL NORMAL MESSAGE";

            return src;
        }
    }
}
