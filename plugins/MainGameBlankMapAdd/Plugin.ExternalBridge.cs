using System;
using System.Reflection;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private sealed class SpeedLimitBreakSnapshot
        {
            public bool ForceVanillaSpeed;
            public bool EnableVideoTimeSpeedCues;
            public float AppliedBpmMax;
        }

        private sealed class BeatSyncSnapshot
        {
            public bool Enabled;
            public int Bpm;
            public bool AutoMotionSwitch;
            public bool AutoThreshold;
            public float LowThreshold;
            public float HighThreshold;
            public float LowIntensity;
            public float MidIntensity;
            public float HighIntensity;
            public float SmoothTime;
            public float StrongMotionBeats;
            public float WeakMotionBeats;
            public float LowPassHz;
            public bool VerboseLog;
        }

        private bool TryGetSpeedLimitBreakSnapshot(out SpeedLimitBreakSnapshot snapshot)
        {
            snapshot = null;
            try
            {
                Type pluginType = Type.GetType("MainGameSpeedLimitBreak.Plugin, MainGameSpeedLimitBreak");
                if (pluginType == null)
                    return false;

                PropertyInfo settingsProp = pluginType.GetProperty(
                    "Settings",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                object settings = settingsProp?.GetValue(null, null);
                if (settings == null)
                    return false;

                Type st = settings.GetType();
                bool forceVanilla = GetFieldValue<bool>(st, settings, "ForceVanillaSpeed");
                bool timeline = GetFieldValue<bool>(st, settings, "EnableVideoTimeSpeedCues");
                float bpmMax = GetFieldValue<float>(st, settings, "AppliedBpmMax");
                if (float.IsNaN(bpmMax) || float.IsInfinity(bpmMax) || bpmMax <= 0f)
                    bpmMax = 120f;

                snapshot = new SpeedLimitBreakSnapshot
                {
                    ForceVanillaSpeed = forceVanilla,
                    EnableVideoTimeSpeedCues = timeline,
                    AppliedBpmMax = UnityEngine.Mathf.Clamp(bpmMax, 1f, 999f)
                };
                return true;
            }
            catch (Exception ex)
            {
                LogWarn($"speed-limit-break snapshot failed: {ex.Message}");
                return false;
            }
        }

        private bool TryApplySpeedLimitBreakSnapshot(SpeedLimitBreakSnapshot snapshot, string reason)
        {
            if (snapshot == null)
                return false;

            try
            {
                Type pluginType = Type.GetType("MainGameSpeedLimitBreak.Plugin, MainGameSpeedLimitBreak");
                if (pluginType == null)
                    return false;

                PropertyInfo instanceProp = pluginType.GetProperty(
                    "Instance",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                object instance = instanceProp?.GetValue(null, null);
                if (instance == null)
                    return false;

                // Apply BPM first (this may auto-disable force-vanilla in SpeedLimitBreak internally).
                MethodInfo applyTap = pluginType.GetMethod(
                    "ApplyTapBpm",
                    BindingFlags.Static | BindingFlags.Public);
                if (applyTap != null)
                {
                    float bpmMax = UnityEngine.Mathf.Clamp(snapshot.AppliedBpmMax, 1f, 999f);
                    applyTap.Invoke(null, new object[] { bpmMax });
                }

                Type instType = instance.GetType();
                SetConfigEntryBool(instType, instance, "_cfgEnableVideoTimeSpeedCues", snapshot.EnableVideoTimeSpeedCues);
                SetConfigEntryBool(instType, instance, "_cfgForceVanillaSpeed", snapshot.ForceVanillaSpeed);

                LogInfo(
                    $"speed-limit-break applied reason={reason} " +
                    $"fv={snapshot.ForceVanillaSpeed} tl={snapshot.EnableVideoTimeSpeedCues} bpmMax={snapshot.AppliedBpmMax:0.##}");
                return true;
            }
            catch (Exception ex)
            {
                LogWarn($"speed-limit-break apply failed reason={reason} error={ex.Message}");
                return false;
            }
        }

        private bool TryGetBeatSyncSnapshot(out BeatSyncSnapshot snapshot)
        {
            snapshot = null;
            try
            {
                Type pluginType = Type.GetType("MainGameBeatSyncSpeed.Plugin, MainGameBeatSyncSpeed");
                if (pluginType == null)
                    return false;

                PropertyInfo instanceProp = pluginType.GetProperty(
                    "Instance",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                object instance = instanceProp?.GetValue(null, null);
                if (instance == null)
                    return false;

                Type instType = instance.GetType();
                bool enabled = GetConfigEntryBool(instType, instance, "_cfgEnabled", true);
                int bpm = GetConfigEntryInt(instType, instance, "_cfgBpm", 128);
                bool autoMotion = GetConfigEntryBool(instType, instance, "_cfgAutoMotionSwitch", true);
                bool autoThreshold = GetConfigEntryBool(instType, instance, "_cfgAutoThreshold", true);
                float lowThreshold = GetConfigEntryFloat(instType, instance, "_cfgLowThreshold", 0.3f);
                float highThreshold = GetConfigEntryFloat(instType, instance, "_cfgHighThreshold", 0.7f);
                float lowIntensity = GetConfigEntryFloat(instType, instance, "_cfgLowIntensity", 0.25f);
                float midIntensity = GetConfigEntryFloat(instType, instance, "_cfgMidIntensity", 0.5f);
                float highIntensity = GetConfigEntryFloat(instType, instance, "_cfgHighIntensity", 1f);
                float smoothTime = GetConfigEntryFloat(instType, instance, "_cfgSmoothTime", 0.5f);
                float strongBeats = GetConfigEntryFloat(instType, instance, "_cfgStrongMotionBeats", 4f);
                float weakBeats = GetConfigEntryFloat(instType, instance, "_cfgWeakMotionBeats", 4f);
                float lowPassHz = GetConfigEntryFloat(instType, instance, "_cfgLowPassHz", 150f);
                bool verboseLog = GetConfigEntryBool(instType, instance, "_cfgVerboseLog", false);

                lowThreshold = UnityEngine.Mathf.Clamp01(lowThreshold);
                highThreshold = UnityEngine.Mathf.Clamp(highThreshold, lowThreshold + 0.01f, 1f);
                lowIntensity = UnityEngine.Mathf.Clamp01(lowIntensity);
                midIntensity = UnityEngine.Mathf.Clamp01(midIntensity);
                highIntensity = UnityEngine.Mathf.Clamp01(highIntensity);
                smoothTime = UnityEngine.Mathf.Clamp(smoothTime, 0f, 2f);
                strongBeats = UnityEngine.Mathf.Clamp(strongBeats, 0.5f, 64f);
                weakBeats = UnityEngine.Mathf.Clamp(weakBeats, 0.5f, 64f);
                lowPassHz = UnityEngine.Mathf.Clamp(lowPassHz, 50f, 500f);

                snapshot = new BeatSyncSnapshot
                {
                    Enabled = enabled,
                    Bpm = UnityEngine.Mathf.Clamp(bpm, 1, 999),
                    AutoMotionSwitch = autoMotion,
                    AutoThreshold = autoThreshold,
                    LowThreshold = lowThreshold,
                    HighThreshold = highThreshold,
                    LowIntensity = lowIntensity,
                    MidIntensity = midIntensity,
                    HighIntensity = highIntensity,
                    SmoothTime = smoothTime,
                    StrongMotionBeats = strongBeats,
                    WeakMotionBeats = weakBeats,
                    LowPassHz = lowPassHz,
                    VerboseLog = verboseLog
                };
                return true;
            }
            catch (Exception ex)
            {
                LogWarn($"beat-sync snapshot failed: {ex.Message}");
                return false;
            }
        }

        private bool TryApplyBeatSyncSnapshot(BeatSyncSnapshot snapshot, string reason)
        {
            if (snapshot == null)
                return false;

            try
            {
                Type pluginType = Type.GetType("MainGameBeatSyncSpeed.Plugin, MainGameBeatSyncSpeed");
                if (pluginType == null)
                    return false;

                PropertyInfo instanceProp = pluginType.GetProperty(
                    "Instance",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                object instance = instanceProp?.GetValue(null, null);
                if (instance == null)
                    return false;

                Type instType = instance.GetType();
                int bpm = UnityEngine.Mathf.Clamp(snapshot.Bpm, 1, 999);
                float lowThreshold = UnityEngine.Mathf.Clamp01(snapshot.LowThreshold);
                float highThreshold = UnityEngine.Mathf.Clamp(snapshot.HighThreshold, lowThreshold + 0.01f, 1f);
                float lowIntensity = UnityEngine.Mathf.Clamp01(snapshot.LowIntensity);
                float midIntensity = UnityEngine.Mathf.Clamp01(snapshot.MidIntensity);
                float highIntensity = UnityEngine.Mathf.Clamp01(snapshot.HighIntensity);
                float smoothTime = UnityEngine.Mathf.Clamp(snapshot.SmoothTime, 0f, 2f);
                float strongBeats = UnityEngine.Mathf.Clamp(snapshot.StrongMotionBeats, 0.5f, 64f);
                float weakBeats = UnityEngine.Mathf.Clamp(snapshot.WeakMotionBeats, 0.5f, 64f);
                float lowPassHz = UnityEngine.Mathf.Clamp(snapshot.LowPassHz, 50f, 500f);

                SetConfigEntryBool(instType, instance, "_cfgEnabled", snapshot.Enabled);
                SetConfigEntryInt(instType, instance, "_cfgBpm", bpm);
                SetConfigEntryBool(instType, instance, "_cfgAutoMotionSwitch", snapshot.AutoMotionSwitch);
                SetConfigEntryBool(instType, instance, "_cfgAutoThreshold", snapshot.AutoThreshold);
                SetConfigEntryFloat(instType, instance, "_cfgLowThreshold", lowThreshold);
                SetConfigEntryFloat(instType, instance, "_cfgHighThreshold", highThreshold);
                SetConfigEntryFloat(instType, instance, "_cfgLowIntensity", lowIntensity);
                SetConfigEntryFloat(instType, instance, "_cfgMidIntensity", midIntensity);
                SetConfigEntryFloat(instType, instance, "_cfgHighIntensity", highIntensity);
                SetConfigEntryFloat(instType, instance, "_cfgSmoothTime", smoothTime);
                SetConfigEntryFloat(instType, instance, "_cfgStrongMotionBeats", strongBeats);
                SetConfigEntryFloat(instType, instance, "_cfgWeakMotionBeats", weakBeats);
                SetConfigEntryFloat(instType, instance, "_cfgLowPassHz", lowPassHz);
                SetConfigEntryBool(instType, instance, "_cfgVerboseLog", snapshot.VerboseLog);

                LogInfo(
                    $"beat-sync applied reason={reason} " +
                    $"enabled={snapshot.Enabled} bpm={bpm} autoMotion={snapshot.AutoMotionSwitch} " +
                    $"autoThreshold={snapshot.AutoThreshold} low/high={lowThreshold:0.###}/{highThreshold:0.###}");
                return true;
            }
            catch (Exception ex)
            {
                LogWarn($"beat-sync apply failed reason={reason} error={ex.Message}");
                return false;
            }
        }

        private static T GetFieldValue<T>(Type ownerType, object owner, string fieldName)
        {
            FieldInfo fi = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fi == null)
                return default(T);
            object value = fi.GetValue(owner);
            if (value is T t)
                return t;
            return default(T);
        }

        private static bool GetConfigEntryBool(Type ownerType, object owner, string fieldName, bool fallback)
        {
            FieldInfo fi = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object entry = fi?.GetValue(owner);
            if (entry == null)
                return fallback;
            PropertyInfo valueProp = entry.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
            if (valueProp == null)
                return fallback;
            object raw = valueProp.GetValue(entry, null);
            return raw is bool b ? b : fallback;
        }

        private static int GetConfigEntryInt(Type ownerType, object owner, string fieldName, int fallback)
        {
            FieldInfo fi = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object entry = fi?.GetValue(owner);
            if (entry == null)
                return fallback;
            PropertyInfo valueProp = entry.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
            if (valueProp == null)
                return fallback;
            object raw = valueProp.GetValue(entry, null);
            return raw is int i ? i : fallback;
        }

        private static float GetConfigEntryFloat(Type ownerType, object owner, string fieldName, float fallback)
        {
            FieldInfo fi = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object entry = fi?.GetValue(owner);
            if (entry == null)
                return fallback;
            PropertyInfo valueProp = entry.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
            if (valueProp == null)
                return fallback;
            object raw = valueProp.GetValue(entry, null);
            return raw is float f ? f : fallback;
        }

        private static void SetConfigEntryBool(Type ownerType, object owner, string fieldName, bool value)
        {
            FieldInfo fi = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object entry = fi?.GetValue(owner);
            if (entry == null)
                return;
            PropertyInfo valueProp = entry.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
            valueProp?.SetValue(entry, value, null);
        }

        private static void SetConfigEntryInt(Type ownerType, object owner, string fieldName, int value)
        {
            FieldInfo fi = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object entry = fi?.GetValue(owner);
            if (entry == null)
                return;
            PropertyInfo valueProp = entry.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
            valueProp?.SetValue(entry, value, null);
        }

        private static void SetConfigEntryFloat(Type ownerType, object owner, string fieldName, float value)
        {
            FieldInfo fi = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object entry = fi?.GetValue(owner);
            if (entry == null)
                return;
            PropertyInfo valueProp = entry.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
            valueProp?.SetValue(entry, value, null);
        }
    }
}
