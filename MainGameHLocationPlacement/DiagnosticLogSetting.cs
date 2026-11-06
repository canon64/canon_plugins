using BepInEx.Configuration;

namespace MainGameHLocationPlacement
{
    internal static class DiagnosticLogSetting
    {
        internal static bool Set(ConfigFile config, ConfigEntry<bool> entry, bool enabled)
        {
            if (entry == null || entry.Value == enabled) return false;
            entry.Value = enabled;
            config.Save();
            return true;
        }
    }
}
