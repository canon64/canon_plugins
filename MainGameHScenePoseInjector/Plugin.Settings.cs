using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using UnityEngine;

namespace MainGameHScenePoseInjector
{
    [DataContract]
    internal sealed class PluginSettings
    {
        [DataMember(Name = "Enabled")]
        public bool Enabled = true;
        [DataMember(Name = "UiVisible")]
        public bool UiVisible = false;
        [DataMember(Name = "ToggleUiKey")]
        public KeyCode ToggleUiKey = KeyCode.F4;

        [DataMember(Name = "WindowX")]
        public float WindowX = 40f;
        [DataMember(Name = "WindowY")]
        public float WindowY = 80f;
        [DataMember(Name = "WindowWidth")]
        public float WindowWidth = 1100f;
        [DataMember(Name = "WindowHeight")]
        public float WindowHeight = 560f;

        [DataMember(Name = "LastAppliedPoseId")]
        public int LastAppliedPoseId = -1;
        [DataMember(Name = "LastEyebrowPtn")]
        public int LastEyebrowPtn = -1;
        [DataMember(Name = "LastEyesPtn")]
        public int LastEyesPtn = -1;
        [DataMember(Name = "LastMouthPtn")]
        public int LastMouthPtn = -1;
        [DataMember(Name = "AutoRestoreOnControllerChange")]
        public bool AutoRestoreOnControllerChange = true;
        [DataMember(Name = "ApplyToAllFemales")]
        public bool ApplyToAllFemales = false;
        [DataMember(Name = "HSceneLostConfirmSeconds")]
        public float HSceneLostConfirmSeconds = 0.75f;
        [DataMember(Name = "ControllerChangeConfirmSeconds")]
        public float ControllerChangeConfirmSeconds = 0.5f;
    }

    internal static class SettingsStore
    {
        internal static PluginSettings LoadOrCreate(string path, Action<string> logInfo, Action<string> logWarn)
        {
            try
            {
                if (!File.Exists(path))
                {
                    PluginSettings created = Normalize(new PluginSettings());
                    Save(path, created, logWarn);
                    logInfo?.Invoke("settings created: " + path);
                    return created;
                }

                string json = File.ReadAllText(path, Encoding.UTF8);
                PluginSettings loaded = Deserialize(json) ?? new PluginSettings();
                loaded = Normalize(loaded);
                Save(path, loaded, logWarn);
                return loaded;
            }
            catch (Exception ex)
            {
                logWarn?.Invoke("settings load failed: " + ex.Message);
                return Normalize(new PluginSettings());
            }
        }

        internal static void Save(string path, PluginSettings settings, Action<string> logWarn)
        {
            try
            {
                PluginSettings normalized = Normalize(settings ?? new PluginSettings());
                string json = Serialize(normalized);
                File.WriteAllText(path, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                logWarn?.Invoke("settings save failed: " + ex.Message);
            }
        }

        private static PluginSettings Normalize(PluginSettings settings)
        {
            if (settings == null)
                settings = new PluginSettings();

            // 旧v0.1デフォルト(320)/v0.2初期デフォルト(720) のまま残ってる設定は新レイアウト用に自動拡張
            if (settings.WindowWidth <= 760f)
                settings.WindowWidth = 1100f;
            settings.WindowWidth = Mathf.Clamp(settings.WindowWidth, 600f, 1920f);
            settings.WindowHeight = Mathf.Clamp(settings.WindowHeight, 200f, 1080f);
            settings.WindowX = Mathf.Max(0f, settings.WindowX);
            settings.WindowY = Mathf.Max(0f, settings.WindowY);
            if (settings.HSceneLostConfirmSeconds <= 0f)
                settings.HSceneLostConfirmSeconds = 0.75f;
            if (settings.ControllerChangeConfirmSeconds <= 0f)
                settings.ControllerChangeConfirmSeconds = 0.5f;
            settings.HSceneLostConfirmSeconds = Mathf.Clamp(settings.HSceneLostConfirmSeconds, 0.1f, 5f);
            settings.ControllerChangeConfirmSeconds = Mathf.Clamp(settings.ControllerChangeConfirmSeconds, 0.1f, 5f);

            return settings;
        }

        private static string Serialize(PluginSettings settings)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(PluginSettings));
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.WriteObject(stream, settings);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static PluginSettings Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(PluginSettings));
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return serializer.ReadObject(stream) as PluginSettings;
            }
        }
    }
}
