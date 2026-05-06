using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;

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
            return TryApplyBeatSyncSnapshot(
                snapshot,
                reason,
                applyEnabled: true,
                preferSavedSongBpm: false);
        }

        private bool TryApplyBeatSyncSnapshot(
            BeatSyncSnapshot snapshot,
            string reason,
            bool applyEnabled,
            bool preferSavedSongBpm)
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

                if (applyEnabled)
                    SetConfigEntryBool(instType, instance, "_cfgEnabled", snapshot.Enabled);
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

                int songMapBpm = 0;
                bool loadedFromSongMap = preferSavedSongBpm &&
                    TryApplyBeatSyncSavedSongBpm(instType, instance, out songMapBpm);
                int appliedBpm = bpm;
                if (loadedFromSongMap)
                {
                    appliedBpm = UnityEngine.Mathf.Clamp(songMapBpm, 1, 999);
                }
                else
                {
                    SetConfigEntryInt(instType, instance, "_cfgBpm", bpm);
                }

                LogInfo(
                    $"beat-sync applied reason={reason} " +
                    $"enabled={(applyEnabled ? snapshot.Enabled.ToString() : "unchanged")} bpm={appliedBpm} bpmSrc={(loadedFromSongMap ? "song-map" : "profile")} autoMotion={snapshot.AutoMotionSwitch} " +
                    $"autoThreshold={snapshot.AutoThreshold} low/high={lowThreshold:0.###}/{highThreshold:0.###}");
                return true;
            }
            catch (Exception ex)
            {
                LogWarn($"beat-sync apply failed reason={reason} error={ex.Message}");
                return false;
            }
        }

        private bool TryGetHipHijackUiVisible(out bool visible)
        {
            visible = false;
            try
            {
                Type pluginType = Type.GetType("MainGirlHipHijack.Plugin, MainGirlHipHijack");
                if (pluginType == null)
                    return false;

                PropertyInfo instanceProp = pluginType.GetProperty(
                    "Instance",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                object instance = instanceProp?.GetValue(null, null);
                if (instance == null)
                    return false;

                Type instType = instance.GetType();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                FieldInfo cfgUiField = instType.GetField("_cfgUiVisible", flags);
                if (cfgUiField != null)
                {
                    visible = GetConfigEntryBool(instType, instance, "_cfgUiVisible", false);
                    return true;
                }

                FieldInfo settingsField = instType.GetField("_settings", flags);
                object settings = settingsField?.GetValue(instance);
                if (settings == null)
                    return false;

                FieldInfo uiVisibleField = settings.GetType().GetField("UiVisible", flags);
                if (uiVisibleField == null || uiVisibleField.FieldType != typeof(bool))
                    return false;

                visible = (bool)uiVisibleField.GetValue(settings);
                return true;
            }
            catch (Exception ex)
            {
                LogWarn($"hip-hijack ui snapshot failed: {ex.Message}");
                return false;
            }
        }

        private bool TryApplyHipHijackUiVisible(bool visible, string reason)
        {
            try
            {
                Type pluginType = Type.GetType("MainGirlHipHijack.Plugin, MainGirlHipHijack");
                if (pluginType == null)
                    return false;

                PropertyInfo instanceProp = pluginType.GetProperty(
                    "Instance",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                object instance = instanceProp?.GetValue(null, null);
                if (instance == null)
                    return false;

                Type instType = instance.GetType();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                FieldInfo cfgUiField = instType.GetField("_cfgUiVisible", flags);
                if (cfgUiField != null)
                {
                    SetConfigEntryBool(instType, instance, "_cfgUiVisible", visible);
                    LogInfo($"hip-hijack ui applied reason={reason} visible={visible}");
                    return true;
                }

                FieldInfo settingsField = instType.GetField("_settings", flags);
                object settings = settingsField?.GetValue(instance);
                if (settings == null)
                    return false;

                FieldInfo uiVisibleField = settings.GetType().GetField("UiVisible", flags);
                if (uiVisibleField == null || uiVisibleField.FieldType != typeof(bool))
                    return false;

                uiVisibleField.SetValue(settings, visible);
                LogInfo($"hip-hijack ui applied via settings reason={reason} visible={visible}");
                return true;
            }
            catch (Exception ex)
            {
                LogWarn($"hip-hijack ui apply failed reason={reason} error={ex.Message}");
                return false;
            }
        }

        private bool TryGetAfterimageEnabled(out bool enabled)
        {
            enabled = false;
            try
            {
                if (!TryGetAfterimagePluginInstance(out object instance, out Type instType))
                    return false;
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                FieldInfo cfgEnabledField = instType.GetField("_cfgEnabled", flags);
                if (cfgEnabledField != null)
                {
                    enabled = GetConfigEntryBool(instType, instance, "_cfgEnabled", false);
                    return true;
                }

                FieldInfo settingsField = instType.GetField("_settings", flags);
                object settings = settingsField?.GetValue(instance);
                if (settings == null)
                    return false;

                FieldInfo enabledField = settings.GetType().GetField("Enabled", flags);
                if (enabledField == null || enabledField.FieldType != typeof(bool))
                    return false;

                enabled = (bool)enabledField.GetValue(settings);
                return true;
            }
            catch (Exception ex)
            {
                LogWarn($"afterimage snapshot failed: {ex.Message}");
                return false;
            }
        }

        private bool TryApplyAfterimageEnabled(bool enabled, string reason)
        {
            try
            {
                if (!TryGetAfterimagePluginInstance(out object instance, out Type instType))
                    return false;
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                FieldInfo cfgEnabledField = instType.GetField("_cfgEnabled", flags);
                if (cfgEnabledField != null)
                {
                    SetConfigEntryBool(instType, instance, "_cfgEnabled", enabled);
                    LogInfo($"afterimage applied reason={reason} enabled={enabled}");
                    return true;
                }

                FieldInfo settingsField = instType.GetField("_settings", flags);
                object settings = settingsField?.GetValue(instance);
                if (settings == null)
                    return false;

                FieldInfo enabledField = settings.GetType().GetField("Enabled", flags);
                if (enabledField == null || enabledField.FieldType != typeof(bool))
                    return false;

                enabledField.SetValue(settings, enabled);
                LogInfo($"afterimage applied via settings reason={reason} enabled={enabled}");
                return true;
            }
            catch (Exception ex)
            {
                LogWarn($"afterimage apply failed reason={reason} error={ex.Message}");
                return false;
            }
        }

        private bool TryGetAfterimagePluginInstance(out object instance, out Type instanceType)
        {
            instance = null;
            instanceType = null;

            try
            {
                if (Chainloader.PluginInfos != null &&
                    Chainloader.PluginInfos.TryGetValue("com.kks.maingame.simpleafterimage", out var pluginInfo))
                {
                    instance = pluginInfo?.Instance;
                    if (instance != null)
                    {
                        instanceType = instance.GetType();
                        return true;
                    }
                }
            }
            catch
            {
                // fallback below
            }

            Type pluginType = ResolveAfterimagePluginType();
            if (pluginType == null)
                return false;

            try
            {
                UnityEngine.Object[] instances = UnityEngine.Object.FindObjectsOfType(pluginType);
                if (instances != null && instances.Length > 0)
                {
                    instance = instances[0];
                    if (instance != null)
                    {
                        instanceType = instance.GetType();
                        return true;
                    }
                }
            }
            catch
            {
                // fallthrough
            }

            return false;
        }

        // ── MainGameClubLights UI表示 ─────────────────────────────────

        private bool TryGetClubLightsUiVisible(out bool visible)
        {
            visible = false;
            try
            {
                if (!TryGetClubLightsPluginInstance(out object instance, out Type instType))
                    return false;

                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                bool pluginEnabled = true;
                FieldInfo cfgEnabledField = instType.GetField("_cfgEnabled", flags);
                if (cfgEnabledField != null)
                    pluginEnabled = GetConfigEntryBool(instType, instance, "_cfgEnabled", true);

                FieldInfo cfgUiField = instType.GetField("_cfgUiVisible", flags);
                if (cfgUiField != null)
                {
                    visible = pluginEnabled && GetConfigEntryBool(instType, instance, "_cfgUiVisible", false);
                    return true;
                }

                FieldInfo settingsField = instType.GetField("_settings", flags);
                object settings = settingsField?.GetValue(instance);
                if (settings == null)
                    return false;

                FieldInfo uiVisibleField = settings.GetType().GetField("UiVisible", flags);
                if (uiVisibleField == null || uiVisibleField.FieldType != typeof(bool))
                    return false;

                visible = pluginEnabled && (bool)uiVisibleField.GetValue(settings);
                return true;
            }
            catch (Exception ex)
            {
                LogWarn($"club-lights ui snapshot failed: {ex.Message}");
                return false;
            }
        }

        private bool TryApplyClubLightsUiVisible(bool visible, string reason)
        {
            try
            {
                if (!TryGetClubLightsPluginInstance(out object instance, out Type instType))
                    return false;

                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                bool enabledBefore = GetConfigEntryBool(instType, instance, "_cfgEnabled", true);

                // UIをONにする時は、Plugin有効フラグも同時にONにする。
                if (visible)
                {
                    SetConfigEntryBool(instType, instance, "_cfgEnabled", true);
                    if (!enabledBefore)
                        LogInfo($"club-lights auto-enable reason={reason} enabledBefore={enabledBefore} enabledAfter=true");
                }
                bool enabledAfter = GetConfigEntryBool(instType, instance, "_cfgEnabled", true);

                FieldInfo cfgUiField = instType.GetField("_cfgUiVisible", flags);
                if (cfgUiField != null)
                {
                    SetConfigEntryBool(instType, instance, "_cfgUiVisible", visible);
                    LogInfo($"club-lights ui applied reason={reason} visible={visible} pluginEnabled={enabledAfter}");
                    return true;
                }

                FieldInfo settingsField = instType.GetField("_settings", flags);
                object settings = settingsField?.GetValue(instance);
                if (settings == null)
                    return false;

                FieldInfo uiVisibleField = settings.GetType().GetField("UiVisible", flags);
                if (uiVisibleField == null || uiVisibleField.FieldType != typeof(bool))
                    return false;

                uiVisibleField.SetValue(settings, visible);
                LogInfo($"club-lights ui applied via settings reason={reason} visible={visible} pluginEnabled={enabledAfter}");
                return true;
            }
            catch (Exception ex)
            {
                LogWarn($"club-lights ui apply failed reason={reason} error={ex.Message}");
                return false;
            }
        }

        private bool TryGetClubLightsPluginInstance(out object instance, out Type instanceType)
        {
            instance = null;
            instanceType = null;
            try
            {
                if (Chainloader.PluginInfos != null &&
                    Chainloader.PluginInfos.TryGetValue("com.kks.maingame.clublights", out var pluginInfo))
                {
                    instance = pluginInfo?.Instance;
                    if (instance != null)
                    {
                        instanceType = instance.GetType();
                        return true;
                    }
                }
            }
            catch
            {
                // fallback below
            }

            Type pluginType = Type.GetType("MainGameClubLights.Plugin, MainGameClubLights");
            if (pluginType == null)
                return false;

            try
            {
                PropertyInfo instanceProp = pluginType.GetProperty(
                    "Instance",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                object staticInstance = instanceProp?.GetValue(null, null);
                if (staticInstance != null)
                {
                    instance = staticInstance;
                    instanceType = staticInstance.GetType();
                    return true;
                }
            }
            catch
            {
                // ignore
            }

            try
            {
                UnityEngine.Object[] instances = UnityEngine.Object.FindObjectsOfType(pluginType);
                if (instances != null && instances.Length > 0)
                {
                    instance = instances[0];
                    if (instance != null)
                    {
                        instanceType = instance.GetType();
                        return true;
                    }
                }
            }
            catch
            {
                // ignore
            }

            return false;
        }

        // ── MainGameDollMode 人形モード ───────────────────────────────

        private bool TryGetDollModeEnabled(out bool enabled)
        {
            enabled = false;
            try
            {
                Type pluginType = Type.GetType("MainGameDollMode.Plugin, MainGameDollMode");
                if (pluginType == null)
                    return false;

                MethodInfo getter = pluginType.GetMethod(
                    "IsDollModeEnabled",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null);
                if (getter == null || getter.ReturnType != typeof(bool))
                    return false;

                object raw = getter.Invoke(null, null);
                if (raw is bool b)
                {
                    enabled = b;
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                LogWarn($"doll-mode snapshot failed: {ex.Message}");
                return false;
            }
        }

        private bool TryApplyDollModeEnabled(bool enabled, string reason)
        {
            try
            {
                Type pluginType = Type.GetType("MainGameDollMode.Plugin, MainGameDollMode");
                if (pluginType == null)
                    return false;

                MethodInfo setterWithSource = pluginType.GetMethod(
                    "SetDollModeEnabled",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(bool), typeof(string) },
                    null);
                if (setterWithSource != null && setterWithSource.ReturnType == typeof(bool))
                {
                    object raw = setterWithSource.Invoke(null, new object[] { enabled, "blankmapadd:" + reason });
                    bool ok = raw is bool b && b;
                    if (ok)
                    {
                        LogInfo($"doll-mode applied reason={reason} enabled={enabled} mode=with-source");
                        return true;
                    }
                }

                MethodInfo setter = pluginType.GetMethod(
                    "SetDollModeEnabled",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(bool) },
                    null);
                if (setter == null || setter.ReturnType != typeof(bool))
                    return false;

                object rawFallback = setter.Invoke(null, new object[] { enabled });
                bool okFallback = rawFallback is bool fb && fb;
                if (okFallback)
                {
                    LogInfo($"doll-mode applied reason={reason} enabled={enabled} mode=default");
                }
                else
                {
                    LogWarn($"doll-mode apply rejected reason={reason} enabled={enabled}");
                }

                return okFallback;
            }
            catch (Exception ex)
            {
                LogWarn($"doll-mode apply failed reason={reason} error={ex.Message}");
                return false;
            }
        }

        // ── VoiceFaceEventBridge 体位変更 ─────────────────────────────

        private bool TryGetVfebPoseChangeEnabled(out bool enabled)
        {
            enabled = false;
            try
            {
                if (!TryGetVfebPluginInstance(out object instance, out Type instType))
                    return false;
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                FieldInfo field = instType.GetField("_poseChangeEnabled", flags);
                if (field == null || field.FieldType != typeof(bool))
                    return false;

                enabled = (bool)field.GetValue(instance);
                return true;
            }
            catch (Exception ex)
            {
                LogWarn($"vfeb pose-change get failed: {ex.Message}");
                return false;
            }
        }

        private bool TryApplyVfebPoseChangeEnabled(bool enabled, string reason)
        {
            try
            {
                if (!TryGetVfebPluginInstance(out object instance, out Type instType))
                    return false;
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                // _poseChangeEnabled フィールド
                FieldInfo field = instType.GetField("_poseChangeEnabled", flags);
                if (field != null && field.FieldType == typeof(bool))
                    field.SetValue(instance, enabled);

                // _cfgPoseChangeEnabled ConfigEntry
                SetConfigEntryBool(instType, instance, "_cfgPoseChangeEnabled", enabled);

                LogInfo($"vfeb pose-change applied reason={reason} enabled={enabled}");
                return true;
            }
            catch (Exception ex)
            {
                LogWarn($"vfeb pose-change apply failed reason={reason} error={ex.Message}");
                return false;
            }
        }

        // ── VoiceFaceEventBridge 状況文送信 ─────────────────────────────

        private bool TryGetVfebScenarioTextAutoSendEnabled(out bool enabled)
        {
            enabled = false;
            try
            {
                if (!TryGetVfebPluginInstance(out object instance, out Type instType))
                    return false;

                MethodInfo getter = instType.GetMethod(
                    "TryGetScenarioTextAutoSendEnabled",
                    BindingFlags.Static | BindingFlags.Public,
                    null,
                    new[] { typeof(bool).MakeByRefType() },
                    null);
                if (getter == null || getter.ReturnType != typeof(bool))
                    return false;

                object[] args = { false };
                object raw = getter.Invoke(null, args);
                bool ok = raw is bool b && b;
                if (!ok)
                    return false;

                enabled = args[0] is bool value && value;
                return true;
            }
            catch (Exception ex)
            {
                LogWarn($"vfeb scenario auto get failed: {ex.Message}");
                return false;
            }
        }

        private bool TryApplyVfebScenarioTextAutoSendEnabled(bool enabled, string reason)
        {
            try
            {
                if (!TryGetVfebPluginInstance(out object instance, out Type instType))
                    return false;

                MethodInfo setter = instType.GetMethod(
                    "TrySetScenarioTextAutoSendEnabled",
                    BindingFlags.Static | BindingFlags.Public,
                    null,
                    new[] { typeof(bool), typeof(string), typeof(string).MakeByRefType() },
                    null);
                if (setter == null || setter.ReturnType != typeof(bool))
                    return false;

                object[] args = { enabled, reason, null };
                object raw = setter.Invoke(null, args);
                bool ok = raw is bool b && b;
                string detail = args[2] as string;
                if (ok)
                {
                    LogInfo($"vfeb scenario auto applied reason={reason} enabled={enabled}");
                }
                else
                {
                    LogWarn($"vfeb scenario auto rejected reason={reason} enabled={enabled} detail={detail}");
                }

                return ok;
            }
            catch (Exception ex)
            {
                LogWarn($"vfeb scenario auto apply failed reason={reason} error={ex.Message}");
                return false;
            }
        }

        private bool TrySendVfebScenarioTextManualNow(string reason)
        {
            try
            {
                if (!TryGetVfebPluginInstance(out object instance, out Type instType))
                    return false;

                MethodInfo sender = instType.GetMethod(
                    "TrySendScenarioTextManualNow",
                    BindingFlags.Static | BindingFlags.Public,
                    null,
                    new[] { typeof(string), typeof(string).MakeByRefType() },
                    null);
                if (sender == null || sender.ReturnType != typeof(bool))
                    return false;

                object[] args = { reason, null };
                object raw = sender.Invoke(null, args);
                bool ok = raw is bool b && b;
                string detail = args[1] as string;
                if (ok)
                {
                    LogInfo($"vfeb scenario manual sent reason={reason}");
                }
                else
                {
                    LogWarn($"vfeb scenario manual rejected reason={reason} detail={detail}");
                }

                return ok;
            }
            catch (Exception ex)
            {
                LogWarn($"vfeb scenario manual send failed reason={reason} error={ex.Message}");
                return false;
            }
        }

        // ── MainGameCameraControl UI表示 ──────────────────────────────

        private bool TryGetCameraControlUiVisible(out bool visible)
        {
            visible = false;
            try
            {
                if (!TryGetCameraControlPluginInstance(out object instance, out Type instType))
                    return false;

                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                FieldInfo cfgUiField = instType.GetField("_cfgUiVisible", flags);
                if (cfgUiField != null)
                {
                    visible = GetConfigEntryBool(instType, instance, "_cfgUiVisible", true);
                    return true;
                }

                MethodInfo getter = instType.GetMethod("TryGetUiVisible", BindingFlags.Static | BindingFlags.Public);
                if (getter != null && getter.ReturnType == typeof(bool))
                {
                    object[] args = { false };
                    object raw = getter.Invoke(null, args);
                    bool ok = raw is bool b && b;
                    if (ok)
                    {
                        visible = args[0] is bool v && v;
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                LogWarn($"camera ui snapshot failed: {ex.Message}");
                return false;
            }
        }

        private bool TryApplyCameraControlUiVisible(bool visible, string reason)
        {
            try
            {
                if (!TryGetCameraControlPluginInstance(out object instance, out Type instType))
                    return false;

                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                FieldInfo cfgUiField = instType.GetField("_cfgUiVisible", flags);
                if (cfgUiField != null)
                {
                    SetConfigEntryBool(instType, instance, "_cfgUiVisible", visible);
                }

                MethodInfo setter = instType.GetMethod("TrySetUiVisible", BindingFlags.Static | BindingFlags.Public);
                if (setter != null && setter.ReturnType == typeof(bool))
                {
                    object raw = setter.Invoke(null, new object[] { visible });
                    if (raw is bool b && b)
                    {
                        LogInfo($"camera ui applied reason={reason} visible={visible} mode=api");
                        return true;
                    }
                }

                LogInfo($"camera ui applied reason={reason} visible={visible} mode=field");
                return true;
            }
            catch (Exception ex)
            {
                LogWarn($"camera ui apply failed reason={reason} error={ex.Message}");
                return false;
            }
        }

        private bool TryGetCameraControlPluginInstance(out object instance, out Type instanceType)
        {
            instance = null;
            instanceType = null;
            try
            {
                if (Chainloader.PluginInfos != null &&
                    Chainloader.PluginInfos.TryGetValue("com.kks.maingame.cameracontrol", out var pluginInfo))
                {
                    instance = pluginInfo?.Instance;
                    if (instance != null)
                    {
                        instanceType = instance.GetType();
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        // ── MainGameAutoHVoice UI表示 ────────────────────────────────

        private bool TryGetAutoHVoiceUiVisible(out bool visible)
        {
            visible = false;
            try
            {
                if (!TryGetAutoHVoicePluginInstance(out object instance, out Type instType))
                    return false;

                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                FieldInfo cfgUiField = instType.GetField("_cfgUiVisible", flags);
                if (cfgUiField != null)
                {
                    visible = GetConfigEntryBool(instType, instance, "_cfgUiVisible", true);
                    return true;
                }

                MethodInfo getter = instType.GetMethod("TryGetUiVisible", BindingFlags.Static | BindingFlags.Public);
                if (getter != null && getter.ReturnType == typeof(bool))
                {
                    object[] args = { false };
                    object raw = getter.Invoke(null, args);
                    bool ok = raw is bool b && b;
                    if (ok)
                    {
                        visible = args[0] is bool v && v;
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                LogWarn($"auto-hvoice ui snapshot failed: {ex.Message}");
                return false;
            }
        }

        private bool TryApplyAutoHVoiceUiVisible(bool visible, string reason)
        {
            try
            {
                if (!TryGetAutoHVoicePluginInstance(out object instance, out Type instType))
                    return false;

                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                FieldInfo cfgUiField = instType.GetField("_cfgUiVisible", flags);
                if (cfgUiField != null)
                {
                    SetConfigEntryBool(instType, instance, "_cfgUiVisible", visible);
                }

                MethodInfo setter = instType.GetMethod("TrySetUiVisible", BindingFlags.Static | BindingFlags.Public);
                if (setter != null && setter.ReturnType == typeof(bool))
                {
                    object raw = setter.Invoke(null, new object[] { visible });
                    if (raw is bool b && b)
                    {
                        LogInfo($"auto-hvoice ui applied reason={reason} visible={visible} mode=api");
                        return true;
                    }
                }

                LogInfo($"auto-hvoice ui applied reason={reason} visible={visible} mode=field");
                return true;
            }
            catch (Exception ex)
            {
                LogWarn($"auto-hvoice ui apply failed reason={reason} error={ex.Message}");
                return false;
            }
        }

        private bool TryGetAutoHVoicePluginInstance(out object instance, out Type instanceType)
        {
            instance = null;
            instanceType = null;
            try
            {
                if (Chainloader.PluginInfos != null &&
                    Chainloader.PluginInfos.TryGetValue("com.kks.maingame.autohvoice", out var pluginInfo))
                {
                    instance = pluginInfo?.Instance;
                    if (instance != null)
                    {
                        instanceType = instance.GetType();
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private bool TryGetVfebPluginInstance(out object instance, out Type instanceType)
        {
            instance = null;
            instanceType = null;
            try
            {
                if (Chainloader.PluginInfos != null &&
                    Chainloader.PluginInfos.TryGetValue("com.kks.maingame.voicefaceeventbridge", out var pluginInfo))
                {
                    instance = pluginInfo?.Instance;
                    if (instance != null)
                    {
                        instanceType = instance.GetType();
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static Type ResolveAfterimagePluginType()
        {
            Type pluginType = Type.GetType("SimpleAfterimage.Plugin, SimpleAfterimage");
            if (pluginType != null)
                return pluginType;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            if (assemblies == null || assemblies.Length == 0)
                return null;

            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly asm = assemblies[i];
                if (asm == null)
                    continue;

                string asmName = asm.GetName().Name;
                if (!string.Equals(asmName, "SimpleAfterimage", StringComparison.Ordinal))
                    continue;

                Type t = asm.GetType("SimpleAfterimage.Plugin", false);
                if (t != null)
                    return t;
            }

            return assemblies
                .Select(a => a?.GetType("SimpleAfterimage.Plugin", false))
                .FirstOrDefault(t => t != null);
        }

        private bool TryApplyBeatSyncSavedSongBpm(Type instType, object instance, out int bpm)
        {
            bpm = 0;
            if (instType == null || instance == null)
                return false;

            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                FieldInfo currentSongKeyField = instType.GetField("_currentSongPathKey", flags);
                currentSongKeyField?.SetValue(instance, null);

                FieldInfo nextPollField = instType.GetField("_nextSongPathPollTime", flags);
                if (nextPollField != null)
                {
                    nextPollField.SetValue(instance, 0f);
                }

                MethodInfo refreshMethod = instType.GetMethod("RefreshSongBpmAutoLoad", flags);
                if (refreshMethod == null)
                    return false;

                refreshMethod.Invoke(instance, null);

                string key = currentSongKeyField?.GetValue(instance) as string;
                if (string.IsNullOrWhiteSpace(key))
                    return false;

                FieldInfo mapField = instType.GetField("_songBpmByPath", flags);
                if (mapField == null)
                    return false;

                var map = mapField.GetValue(instance) as IDictionary;
                if (map == null || !map.Contains(key))
                    return false;

                object raw = map[key];
                if (raw == null)
                    return false;

                bpm = UnityEngine.Mathf.Clamp(Convert.ToInt32(raw), 1, 999);
                SetConfigEntryInt(instType, instance, "_cfgBpm", bpm);
                return true;
            }
            catch (Exception ex)
            {
                LogWarn($"beat-sync song-map apply failed: {ex.Message}");
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
