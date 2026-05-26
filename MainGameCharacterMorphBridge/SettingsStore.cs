using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace MainGameCharacterMorphBridge
{
    internal static class SettingsStore
    {
        internal const string FileName = "MainGameCharacterMorphBridgeSettings.json";
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        internal static PluginSettings LoadOrCreate(
            string pluginDir,
            Action<string> info,
            Action<string> warn,
            Action<string> error)
        {
            string path = Path.Combine(pluginDir, FileName);
            try
            {
                if (!File.Exists(path))
                {
                    var created = new PluginSettings().Normalize();
                    Save(pluginDir, created, warn);
                    info?.Invoke("settings created: " + path);
                    return created;
                }

                string json = File.ReadAllText(path, Encoding.UTF8);
                var parsed = Deserialize(json);
                if (parsed == null)
                {
                    warn?.Invoke("settings parse failed; fallback defaults");
                    parsed = new PluginSettings();
                }
                else
                {
                    ApplyMissingDefaults(parsed, json, info);
                }

                parsed.Normalize();
                Save(pluginDir, parsed, warn);
                return parsed;
            }
            catch (Exception ex)
            {
                error?.Invoke("settings load failed: " + ex.Message);
                return new PluginSettings().Normalize();
            }
        }

        internal static void Save(string pluginDir, PluginSettings settings, Action<string> warn)
        {
            string path = Path.Combine(pluginDir, FileName);
            string tempPath = path + ".tmp";
            try
            {
                var normalized = (settings ?? new PluginSettings()).Normalize();
                var serializer = new DataContractJsonSerializer(typeof(PluginSettings));
                using (var ms = new MemoryStream())
                {
                    serializer.WriteObject(ms, normalized);
                    string json = Encoding.UTF8.GetString(ms.ToArray());
                    File.WriteAllText(tempPath, json + Environment.NewLine, Utf8NoBom);
                }

                if (File.Exists(path))
                    File.Replace(tempPath, path, null);
                else
                    File.Move(tempPath, path);
            }
            catch (Exception ex)
            {
                warn?.Invoke("settings save failed: " + ex.Message);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch
                {
                }
            }
        }

        private static PluginSettings Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var serializer = new DataContractJsonSerializer(typeof(PluginSettings));
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            using (var ms = new MemoryStream(bytes))
            {
                return serializer.ReadObject(ms) as PluginSettings;
            }
        }

        private static void ApplyMissingDefaults(PluginSettings settings, string json, Action<string> info)
        {
            if (settings == null)
                return;

            if (!ContainsJsonProperty(json, "AutoLoadOnHSceneStart"))
            {
                settings.AutoLoadOnHSceneStart = true;
                info?.Invoke("settings default applied: AutoLoadOnHSceneStart=true");
            }

            if (!ContainsJsonProperty(json, "AutoResetBlendOnHSceneStart"))
            {
                settings.AutoResetBlendOnHSceneStart = true;
                info?.Invoke("settings default applied: AutoResetBlendOnHSceneStart=true");
            }

            if (!ContainsJsonProperty(json, "RegisteredCards"))
            {
                settings.RegisteredCards = new MorphCardRegistration[0];
                info?.Invoke("settings default applied: RegisteredCards=[]");
            }
        }

        private static bool ContainsJsonProperty(string json, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(propertyName))
                return false;

            string pattern = "\"" + propertyName + "\"";
            return json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
