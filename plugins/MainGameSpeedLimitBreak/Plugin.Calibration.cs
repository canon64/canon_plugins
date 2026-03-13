using System.Globalization;
using UnityEngine;

namespace MainGameSpeedLimitBreak
{
    public partial class Plugin
    {
        private void ApplyMeasuredToMin()
        {
            if (_measuredMinBpm <= 0f)
            {
                ShowUiNotice("最小値の計算結果がありません");
                return;
            }

            _bpmMinInput = _measuredMinBpm.ToString("0.##", CultureInfo.InvariantCulture);
            ApplyMinBpmCalibration(_measuredMinBpm, "apply measured min");
        }

        private void ApplyMeasuredToMax()
        {
            if (_measuredMaxBpm <= 0f)
            {
                ShowUiNotice("最大値の計算結果がありません");
                return;
            }

            _bpmMaxInput = _measuredMaxBpm.ToString("0.##", CultureInfo.InvariantCulture);
            ApplyMaxBpmCalibration(_measuredMaxBpm, "apply measured max");
        }

        private void ApplyCalibrationFromInputs()
        {
            var s = Settings;
            if (s == null)
                return;

            if (!TryParseNonNegativeFloat(_bpmMinInput, out float bpmMin))
            {
                ShowUiNotice("Min BPM入力が不正です");
                return;
            }

            if (!TryParsePositiveFloat(_bpmMaxInput, out float bpmMax))
            {
                ShowUiNotice("Max BPM入力が不正です");
                return;
            }

            if (bpmMin > 0f && bpmMax <= bpmMin)
            {
                ShowUiNotice("Max BPM は Min BPM より大きくしてください");
                return;
            }

            ApplyCalibrationPair(bpmMin, bpmMax, "manual calibration", "較正BPMを反映しました");
        }

        private void PushCalibrationToConfigEntries(float bpmMin, float bpmMax)
        {
            if (_cfgBpmReferenceAtSourceMin == null || _cfgBpmReferenceAtSourceMax == null)
                return;

            _suppressConfigSync = true;
            _cfgBpmReferenceAtSourceMin.Value = bpmMin;
            _cfgBpmReferenceAtSourceMax.Value = bpmMax;
            _suppressConfigSync = false;
        }

        private void ApplyMinBpmCalibration(float bpmMin, string reason)
        {
            var s = Settings;
            if (s == null)
                return;

            float bpmMax = s.BpmReferenceAtSpeed3;
            if (bpmMin > 0f && bpmMax <= bpmMin)
                bpmMax = bpmMin + 1f;

            ApplyCalibrationPair(bpmMin, bpmMax, reason, $"最小基準BPMを反映: {bpmMin:0.##}");
        }

        private void ApplyMaxBpmCalibration(float bpmMax, string reason)
        {
            var s = Settings;
            if (s == null)
                return;

            float bpmMin = s.BpmReferenceAtSourceMin;
            if (bpmMin > 0f && bpmMax <= bpmMin)
            {
                ShowUiNotice("最大基準BPMは最小基準BPMより大きくしてください");
                return;
            }

            ApplyCalibrationPair(bpmMin, bpmMax, reason, $"最大基準BPMを反映: {bpmMax:0.##}");
        }

        private void ApplyCalibrationPair(float bpmMin, float bpmMax, string reason, string notice)
        {
            var s = Settings;
            if (s == null)
                return;

            float clampedMin = Mathf.Max(0f, bpmMin);
            float clampedMax = Mathf.Max(1f, bpmMax);
            if (clampedMin > 0f && clampedMax <= clampedMin)
                clampedMax = clampedMin + 1f;

            float beforeMin = s.BpmReferenceAtSourceMin;
            float beforeMax = s.BpmReferenceAtSpeed3;
            s.BpmReferenceAtSourceMin = clampedMin;
            s.BpmReferenceAtSpeed3 = clampedMax;
            EnsureAppliedBpmRangeInitialized(s);
            ApplyAppliedRangeToTargetSpeeds(s, s.AppliedBpmMin, s.AppliedBpmMax);
            PushCalibrationToConfigEntries(clampedMin, clampedMax);
            SaveSettings(reason);
            SyncUiFromSettings();
            LogInfo(
                $"calibration applied ({reason}): min {beforeMin:0.##}->{clampedMin:0.##}, " +
                $"max {beforeMax:0.##}->{clampedMax:0.##}");
            ShowUiNotice(notice);
        }

        private void ApplyAppliedBpmRange(float appliedMinBpm, float appliedMaxBpm, string reason, string notice)
        {
            var s = Settings;
            if (s == null)
                return;

            float safeMax = Mathf.Max(0.0001f, appliedMaxBpm);
            float safeMin = Mathf.Clamp(appliedMinBpm, 0f, safeMax - 0.0001f);
            if (safeMin >= safeMax)
            {
                safeMin = Mathf.Max(0f, safeMax * 0.25f);
            }

            float beforeAppliedMin = s.AppliedBpmMin;
            float beforeAppliedMax = s.AppliedBpmMax;
            float beforeTargetMin = s.TargetMinSpeed;
            float beforeTargetMax = s.TargetMaxSpeed;

            DisableForceVanillaForCustomApply(reason);
            s.AppliedBpmMin = safeMin;
            s.AppliedBpmMax = safeMax;
            ApplyAppliedRangeToTargetSpeeds(s, safeMin, safeMax);

            SaveSettings(reason);
            SyncUiFromSettings();
            LogInfo(
                $"applied bpm updated ({reason}): applied=[{beforeAppliedMin:0.##}/{beforeAppliedMax:0.##}]=>[{safeMin:0.##}/{safeMax:0.##}] " +
                $"target=[{beforeTargetMin:0.###}/{beforeTargetMax:0.###}]=>[{s.TargetMinSpeed:0.###}/{s.TargetMaxSpeed:0.###}]");
            ShowUiNotice(notice);
        }

        private void ApplyAppliedRangeToTargetSpeeds(PluginSettings s, float appliedMinBpm, float appliedMaxBpm)
        {
            if (s == null)
                return;

            float mappedMin = ConvertBpmToMappedSpeed(appliedMinBpm);
            float mappedMax = ConvertBpmToMappedSpeed(appliedMaxBpm);
            if (mappedMax < mappedMin)
            {
                float swap = mappedMin;
                mappedMin = mappedMax;
                mappedMax = swap;
            }

            s.TargetMinSpeed = Mathf.Max(0f, mappedMin);
            s.TargetMaxSpeed = Mathf.Max(s.TargetMinSpeed, mappedMax);
        }

        private void EnsureAppliedBpmRangeInitialized(PluginSettings s)
        {
            if (s == null)
                return;

            bool changed = false;
            if (s.AppliedBpmMax <= 0f)
            {
                s.AppliedBpmMax = Mathf.Max(1f, EstimateBpmFromMappedSpeed(s.TargetMaxSpeed));
                changed = true;
            }

            if (s.AppliedBpmMin < 0f)
            {
                s.AppliedBpmMin = 0f;
                changed = true;
            }

            if (s.AppliedBpmMin <= 0f)
            {
                s.AppliedBpmMin = Mathf.Max(0f, EstimateBpmFromMappedSpeed(s.TargetMinSpeed));
                changed = true;
            }

            if (s.AppliedBpmMax <= s.AppliedBpmMin)
            {
                s.AppliedBpmMin = Mathf.Max(0f, s.AppliedBpmMax * 0.25f);
                changed = true;
            }

            if (changed)
            {
                ApplyAppliedRangeToTargetSpeeds(s, s.AppliedBpmMin, s.AppliedBpmMax);
            }
        }

        private float ConvertBpmToMappedSpeed(float bpm)
        {
            var s = Settings;
            if (s == null)
                return bpm;

            if (HasTwoPointCalibration(s))
            {
                float bpmRange = Mathf.Max(0.0001f, s.BpmReferenceAtSpeed3 - s.BpmReferenceAtSourceMin);
                float sourceRange = Mathf.Max(0.0001f, s.SourceMaxSpeed - s.SourceMinSpeed);
                float t = (bpm - s.BpmReferenceAtSourceMin) / bpmRange;
                return s.SourceMinSpeed + sourceRange * t;
            }

            float baseBpm = Mathf.Max(1f, s.BpmReferenceAtSpeed3);
            float sourceMax = Mathf.Max(0.0001f, s.SourceMaxSpeed);
            return sourceMax * (bpm / baseBpm);
        }

        private float EstimateBpmFromMappedSpeed(float mappedSpeed)
        {
            var s = Settings;
            if (s == null)
                return mappedSpeed;

            if (HasTwoPointCalibration(s))
            {
                float sourceRange = Mathf.Max(0.0001f, s.SourceMaxSpeed - s.SourceMinSpeed);
                float bpmRange = Mathf.Max(0.0001f, s.BpmReferenceAtSpeed3 - s.BpmReferenceAtSourceMin);
                float t = (mappedSpeed - s.SourceMinSpeed) / sourceRange;
                return s.BpmReferenceAtSourceMin + bpmRange * t;
            }

            float baseBpm = Mathf.Max(1f, s.BpmReferenceAtSpeed3);
            float sourceMax = Mathf.Max(0.0001f, s.SourceMaxSpeed);
            return (mappedSpeed / sourceMax) * baseBpm;
        }

        private static bool HasTwoPointCalibration(PluginSettings s)
        {
            if (s == null)
                return false;

            return s.BpmReferenceAtSourceMin > 0f
                && s.BpmReferenceAtSpeed3 > s.BpmReferenceAtSourceMin
                && s.SourceMaxSpeed > s.SourceMinSpeed + 0.0001f;
        }
    }
}
