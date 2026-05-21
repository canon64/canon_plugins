using System;
using System.Reflection;
using UnityEngine;

namespace MainGameSubCameraDisplayProbe
{
    public sealed partial class Plugin
    {
        private enum BeatZone { Unknown, Low, Mid, High }

        private Type _beatPluginType;
        private PropertyInfo _beatInstanceProperty;
        private FieldInfo _beatZoneRawTargetField;
        private FieldInfo _beatCfgBpmField;
        private FieldInfo _beatCfgLowIntensityField;
        private FieldInfo _beatCfgMidIntensityField;
        private FieldInfo _beatCfgHighIntensityField;
        private bool _beatLookupDone;
        private const float BeatZoneTransitionSec = 0.5f;
        private float _beatSmoothedZoneSpeed01 = -1f;
        private float _beatPhase01;
        private float _beatLastBpmHz;
        private float _lastBeatCurrentMultiplier = 1f;

        private void EnsureBeatLookup()
        {
            if (_beatLookupDone)
                return;

            _beatLookupDone = true;
            _beatPluginType = Type.GetType("MainGameBeatSyncSpeed.Plugin, MainGameBeatSyncSpeed");
            if (_beatPluginType == null)
                return;

            _beatInstanceProperty = _beatPluginType.GetProperty("Instance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            _beatZoneRawTargetField = _beatPluginType.GetField("_currentZoneRawTarget", BindingFlags.Instance | BindingFlags.NonPublic);
            _beatCfgBpmField = _beatPluginType.GetField("_cfgBpm", BindingFlags.Instance | BindingFlags.NonPublic);
            _beatCfgLowIntensityField = _beatPluginType.GetField("_cfgLowIntensity", BindingFlags.Instance | BindingFlags.NonPublic);
            _beatCfgMidIntensityField = _beatPluginType.GetField("_cfgMidIntensity", BindingFlags.Instance | BindingFlags.NonPublic);
            _beatCfgHighIntensityField = _beatPluginType.GetField("_cfgHighIntensity", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private object GetBeatInstance()
        {
            EnsureBeatLookup();
            if (_beatInstanceProperty == null)
                return null;
            try { return _beatInstanceProperty.GetValue(null, null); }
            catch { return null; }
        }

        private static bool TryReadConfigEntryFloat(object owner, FieldInfo field, out float value)
        {
            value = 0f;
            if (owner == null || field == null)
                return false;

            try
            {
                object entry = field.GetValue(owner);
                if (entry == null)
                    return false;

                PropertyInfo valueProp = entry.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
                if (valueProp == null)
                    return false;

                object raw = valueProp.GetValue(entry, null);
                if (raw is float f) { value = f; return true; }
                if (raw is double d) { value = (float)d; return true; }
                if (raw is int i) { value = i; return true; }
            }
            catch { }
            return false;
        }

        private static bool TryReadConfigEntryInt(object owner, FieldInfo field, out int value)
        {
            value = 0;
            if (owner == null || field == null)
                return false;

            try
            {
                object entry = field.GetValue(owner);
                if (entry == null)
                    return false;

                PropertyInfo valueProp = entry.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
                if (valueProp == null)
                    return false;

                object raw = valueProp.GetValue(entry, null);
                if (raw is int i) { value = i; return true; }
                if (raw is float f) { value = Mathf.RoundToInt(f); return true; }
                if (raw is double d) { value = Mathf.RoundToInt((float)d); return true; }
            }
            catch { }
            return false;
        }

        private bool TryGetBeatZone(out BeatZone zone, out float lowI, out float midI, out float highI)
        {
            zone = BeatZone.Unknown;
            lowI = 0f; midI = 0.5f; highI = 1f;

            object inst = GetBeatInstance();
            if (inst == null || _beatZoneRawTargetField == null)
                return false;

            float rawTarget;
            try
            {
                object raw = _beatZoneRawTargetField.GetValue(inst);
                if (!(raw is float f) || f < 0f) return false;
                rawTarget = f;
            }
            catch { return false; }

            if (!TryReadConfigEntryFloat(inst, _beatCfgLowIntensityField, out float low)) return false;
            if (!TryReadConfigEntryFloat(inst, _beatCfgMidIntensityField, out float mid)) return false;
            if (!TryReadConfigEntryFloat(inst, _beatCfgHighIntensityField, out float high)) return false;

            lowI = low; midI = mid; highI = high;

            float dLow = Mathf.Abs(rawTarget - low);
            float dMid = Mathf.Abs(rawTarget - mid);
            float dHigh = Mathf.Abs(rawTarget - high);
            if (dLow <= dMid && dLow <= dHigh) zone = BeatZone.Low;
            else if (dHigh <= dLow && dHigh <= dMid) zone = BeatZone.High;
            else zone = BeatZone.Mid;
            return true;
        }

        private bool TryGetBeatLoopTarget(out float bpmHz, out float zoneSpeed01)
        {
            bpmHz = 0f;
            zoneSpeed01 = 0f;
            object inst = GetBeatInstance();
            if (inst == null) return false;

            if (!TryReadConfigEntryInt(inst, _beatCfgBpmField, out int bpm)) return false;
            if (!TryGetBeatZone(out BeatZone zone, out float low, out float mid, out float high)) return false;

            float rawZoneSpeed01 = zone == BeatZone.Low ? low
                : zone == BeatZone.Mid ? mid
                : zone == BeatZone.High ? high : -1f;
            if (rawZoneSpeed01 < 0f) return false;

            bpmHz = Mathf.Max(1, bpm) / 60f;
            zoneSpeed01 = Mathf.Clamp01(rawZoneSpeed01);
            return true;
        }

        private float ResolveBeatCurrentMultiplier(float zoomMultiplier)
        {
            float targetSpeed01 = 0f;
            float bpmHz = 0f;
            if (TryGetBeatLoopTarget(out float resolvedBpmHz, out float resolvedSpeed01))
            {
                bpmHz = resolvedBpmHz;
                _beatLastBpmHz = bpmHz;
                targetSpeed01 = resolvedSpeed01;
            }
            else
            {
                bpmHz = _beatLastBpmHz;
            }

            if (_beatSmoothedZoneSpeed01 < 0f)
                _beatSmoothedZoneSpeed01 = targetSpeed01;
            else
                _beatSmoothedZoneSpeed01 = Mathf.MoveTowards(
                    _beatSmoothedZoneSpeed01,
                    targetSpeed01,
                    Time.unscaledDeltaTime / BeatZoneTransitionSec);

            float speed01 = Mathf.Clamp01(_beatSmoothedZoneSpeed01);
            if (speed01 <= 0.0001f || bpmHz <= 0f)
            {
                _lastBeatCurrentMultiplier = 1f;
                return 1f;
            }

            _beatPhase01 = Mathf.Repeat(_beatPhase01 + bpmHz * speed01 * Time.unscaledDeltaTime, 1f);
            float pulse01 = Mathf.Sin(_beatPhase01 * Mathf.PI);
            float clampedZoom = Mathf.Clamp(zoomMultiplier, 1f, 3f);
            float multiplier = 1f + (clampedZoom - 1f) * pulse01 * speed01;
            _lastBeatCurrentMultiplier = Mathf.Max(1f, multiplier);
            return _lastBeatCurrentMultiplier;
        }

        private float ResolveBeatZoomedFov(float baseFov)
        {
            float clampedBase = Mathf.Clamp(baseFov <= 0f ? 60f : baseFov, 10f, 170f);
            if (_settings == null || !_settings.BeatFovEnabled)
                return clampedBase;

            float multiplier = ResolveBeatCurrentMultiplier(_settings.BeatFovZoomMultiplier);
            float zoomedFov = clampedBase / multiplier;
            return Mathf.Clamp(zoomedFov, 10f, 170f);
        }

        private void UpdateBeatFovRuntime()
        {
            if (_subCamera == null || _settings == null || !_settings.BeatFovEnabled)
                return;

            float baseFov = _transitionActive
                ? _subCamera.fieldOfView
                : Mathf.Clamp(_settings.CameraFieldOfView, 10f, 170f);
            float zoomedFov = ResolveBeatZoomedFov(baseFov);
            _subCamera.fieldOfView = zoomedFov;
        }
    }
}
