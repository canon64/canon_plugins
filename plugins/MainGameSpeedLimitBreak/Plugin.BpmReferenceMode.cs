using System;
using System.Globalization;
using UnityEngine;

namespace MainGameSpeedLimitBreak
{
    public partial class Plugin
    {
        private enum BpmReferenceMode
        {
            Sonyu = 0,
            Houshi = 1
        }

        private BpmReferenceMode _activeBpmReferenceMode = BpmReferenceMode.Sonyu;
        private string _lastBpmReferenceAnimationName = string.Empty;
        private float _nextBpmReferenceModeScanTime;

        private void UpdateBpmReferenceMode(bool force = false)
        {
            if (!force && Time.unscaledTime < _nextBpmReferenceModeScanTime)
            {
                return;
            }

            _nextBpmReferenceModeScanTime = Time.unscaledTime + 0.25f;
            if (!TryResolveBpmReferenceMode(out BpmReferenceMode mode, out string animationName, out string source))
            {
                return;
            }

            string animationKey = $"{source}:{animationName}";
            bool modeChanged = mode != _activeBpmReferenceMode;
            bool animationChanged = !string.Equals(
                animationKey,
                _lastBpmReferenceAnimationName,
                StringComparison.OrdinalIgnoreCase);

            _activeBpmReferenceMode = mode;
            _lastBpmReferenceAnimationName = animationKey;

            if (!modeChanged && !animationChanged)
            {
                return;
            }

            var settings = Settings;
            if (settings == null)
            {
                return;
            }

            float beforeTargetMin = settings.TargetMinSpeed;
            float beforeTargetMax = settings.TargetMaxSpeed;
            ApplyStoredCalibrationToWorkingValues(settings, mode, pushConfigEntries: true);
            EnsureAppliedBpmRangeInitialized(settings);
            ApplyAppliedRangeToTargetSpeeds(settings, settings.AppliedBpmMin, settings.AppliedBpmMax);

            LogInfo(
                $"bpm reference applied: mode={GetBpmReferenceModeLabel(mode)} src={source} anim={animationName} " +
                $"ref=[{settings.BpmReferenceAtSourceMin:0.##}/{settings.BpmReferenceAtSpeed3:0.##}] " +
                $"applied=[{settings.AppliedBpmMin:0.##}/{settings.AppliedBpmMax:0.##}] " +
                $"target=[{beforeTargetMin:0.###}/{beforeTargetMax:0.###}]=>[{settings.TargetMinSpeed:0.###}/{settings.TargetMaxSpeed:0.###}]");
            if (modeChanged)
            {
                LogInfo($"bpm reference mode switched: {GetBpmReferenceModeLabel(mode)} ({animationName}, src={source})");
            }
        }

        private BpmReferenceMode ResolveCurrentBpmReferenceMode()
        {
            if (TryResolveBpmReferenceMode(out BpmReferenceMode mode, out string animationName, out string source))
            {
                _activeBpmReferenceMode = mode;
                _lastBpmReferenceAnimationName = $"{source}:{animationName}";
                return mode;
            }

            return _activeBpmReferenceMode;
        }

        private bool TryResolveBpmReferenceMode(out BpmReferenceMode mode, out string animationName, out string source)
        {
            mode = _activeBpmReferenceMode;
            animationName = string.Empty;
            source = "none";

            if (TryResolveBpmReferenceModeFromFlags(out BpmReferenceMode fromFlagsMode, out string fromFlagsAnim, out string fromFlagsSource))
            {
                mode = fromFlagsMode;
                animationName = string.IsNullOrWhiteSpace(fromFlagsAnim) ? "<no-anim-info>" : fromFlagsAnim;
                source = fromFlagsSource;
                return true;
            }

            // Important: do not classify mode from animator clip names (e.g. M_SLoop1 / M_WLoop1).
            // Those names can diverge from actual H mode and caused wrong BPM base selection.
            return false;
        }

        private bool TryResolveBpmReferenceModeFromFlags(
            out BpmReferenceMode mode,
            out string animationName,
            out string source)
        {
            mode = _activeBpmReferenceMode;
            animationName = string.Empty;
            source = string.Empty;

            if (_hSceneProc == null || _hSceneProc.flags == null)
            {
                return false;
            }

            HFlag flags = _hSceneProc.flags;
            HSceneProc.AnimationListInfo nowInfo = flags.nowAnimationInfo;
            animationName = nowInfo?.nameAnimation ?? string.Empty;

            if (TryMapHModeToReferenceMode(flags.mode, out BpmReferenceMode modeByFlags))
            {
                mode = modeByFlags;
                source = "flags.mode";
                return true;
            }

            if (nowInfo != null && TryMapHModeToReferenceMode(nowInfo.mode, out BpmReferenceMode modeByNowInfo))
            {
                mode = modeByNowInfo;
                source = "flags.nowAnimationInfo.mode";
                return true;
            }

            return false;
        }

        private static bool TryMapHModeToReferenceMode(HFlag.EMode hMode, out BpmReferenceMode mode)
        {
            switch (hMode)
            {
                case HFlag.EMode.houshi:
                case HFlag.EMode.houshi3P:
                case HFlag.EMode.houshi3PMMF:
                    mode = BpmReferenceMode.Houshi;
                    return true;

                case HFlag.EMode.sonyu:
                case HFlag.EMode.sonyu3P:
                case HFlag.EMode.sonyu3PMMF:
                    mode = BpmReferenceMode.Sonyu;
                    return true;

                default:
                    mode = BpmReferenceMode.Sonyu;
                    return false;
            }
        }

        private static BpmReferenceMode ClassifyBpmReferenceMode(string animationName, BpmReferenceMode fallback)
        {
            if (string.IsNullOrWhiteSpace(animationName))
            {
                return fallback;
            }

            string name = animationName.Trim().ToLowerInvariant();
            if (name.Contains("houshi") ||
                name.Contains("fera") ||
                name.Contains("service"))
            {
                return BpmReferenceMode.Houshi;
            }

            if (name.Contains("sonyu") ||
                name.Contains("insert"))
            {
                return BpmReferenceMode.Sonyu;
            }

            return fallback;
        }

        private static string GetBpmReferenceModeLabel(BpmReferenceMode mode)
        {
            return mode == BpmReferenceMode.Houshi ? "奉仕" : "挿入";
        }

        private static void NormalizeCalibrationPair(ref float minRef, ref float maxRef)
        {
            if (minRef < 0f)
            {
                minRef = 0f;
            }

            if (maxRef < 1f)
            {
                maxRef = 1f;
            }

            if (minRef > 0f && maxRef <= minRef)
            {
                maxRef = minRef + 1f;
            }
        }

        private static void GetStoredCalibrationPair(
            PluginSettings settings,
            BpmReferenceMode mode,
            out float minRef,
            out float maxRef)
        {
            minRef = 0f;
            maxRef = 1f;
            if (settings == null)
            {
                return;
            }

            if (mode == BpmReferenceMode.Houshi)
            {
                minRef = settings.BpmReferenceAtSourceMinHoushi;
                maxRef = settings.BpmReferenceAtSpeed3Houshi;
            }
            else
            {
                minRef = settings.BpmReferenceAtSourceMinSonyu;
                maxRef = settings.BpmReferenceAtSpeed3Sonyu;
            }

            NormalizeCalibrationPair(ref minRef, ref maxRef);
        }

        private static void SetStoredCalibrationPair(
            PluginSettings settings,
            BpmReferenceMode mode,
            float minRef,
            float maxRef)
        {
            if (settings == null)
            {
                return;
            }

            NormalizeCalibrationPair(ref minRef, ref maxRef);
            if (mode == BpmReferenceMode.Houshi)
            {
                settings.BpmReferenceAtSourceMinHoushi = minRef;
                settings.BpmReferenceAtSpeed3Houshi = maxRef;
            }
            else
            {
                settings.BpmReferenceAtSourceMinSonyu = minRef;
                settings.BpmReferenceAtSpeed3Sonyu = maxRef;
            }
        }

        private void ApplyStoredCalibrationToWorkingValues(
            PluginSettings settings,
            BpmReferenceMode mode,
            bool pushConfigEntries)
        {
            if (settings == null)
            {
                return;
            }

            GetStoredCalibrationPair(settings, mode, out float minRef, out float maxRef);
            settings.BpmReferenceAtSourceMin = minRef;
            settings.BpmReferenceAtSpeed3 = maxRef;
            _bpmMinInput = minRef.ToString("0.##", CultureInfo.InvariantCulture);
            _bpmMaxInput = maxRef.ToString("0.##", CultureInfo.InvariantCulture);

            if (pushConfigEntries)
            {
                PushCalibrationToConfigEntries(minRef, maxRef);
            }
        }

        private void SaveCalibrationForModeAndApplyWorking(
            PluginSettings settings,
            BpmReferenceMode mode,
            float minRef,
            float maxRef,
            bool pushConfigEntries)
        {
            if (settings == null)
            {
                return;
            }

            SetStoredCalibrationPair(settings, mode, minRef, maxRef);
            ApplyStoredCalibrationToWorkingValues(settings, mode, pushConfigEntries);
        }

        private void SyncCalibrationConfigEntriesForMode(PluginSettings settings, BpmReferenceMode mode)
        {
            if (settings == null || _cfgBpmReferenceAtSourceMin == null || _cfgBpmReferenceAtSourceMax == null)
            {
                return;
            }

            GetStoredCalibrationPair(settings, mode, out float minRef, out float maxRef);
            settings.BpmReferenceAtSourceMin = minRef;
            settings.BpmReferenceAtSpeed3 = maxRef;

            _suppressConfigSync = true;
            _cfgBpmReferenceAtSourceMin.Value = minRef;
            _cfgBpmReferenceAtSourceMax.Value = maxRef;
            _suppressConfigSync = false;
        }

        private static BpmReferenceMode ClassifyBpmReferenceModeForPreset(string animationName)
        {
            return ClassifyBpmReferenceMode(animationName, BpmReferenceMode.Sonyu);
        }
    }
}
