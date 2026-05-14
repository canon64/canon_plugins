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
        private bool _beatLastLoopActive;
        private float _beatStopReturnStartTime;
        private float _beatStopReturnStartMultiplier = 1f;
        private bool _beatStopReturnActive;
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

        private bool TryGetBeatLoopHz(out float hz)
        {
            hz = 0f;
            object inst = GetBeatInstance();
            if (inst == null) return false;

            if (!TryReadConfigEntryInt(inst, _beatCfgBpmField, out int bpm)) return false;
            if (!TryGetBeatZone(out BeatZone zone, out float low, out float mid, out float high)) return false;

            float zoneSpeed01 = zone == BeatZone.Low ? low
                : zone == BeatZone.Mid ? mid
                : zone == BeatZone.High ? high : -1f;
            if (zoneSpeed01 < 0f) return false;

            hz = (Mathf.Max(1, bpm) / 60f) * Mathf.Max(0f, Mathf.Clamp01(zoneSpeed01));
            return hz > 0f;
        }

        private float ResolveBeatCurrentMultiplier(float zoomMultiplier)
        {
            if (TryGetBeatLoopHz(out float hz) && hz > 0f)
            {
                float clampedZoom = Mathf.Clamp(zoomMultiplier, 1f, 3f);
                float phase01 = Mathf.Repeat(Time.unscaledTime * hz, 1f);
                float pulse01 = Mathf.Sin(phase01 * Mathf.PI);
                float multiplier = Mathf.Lerp(1f, clampedZoom, pulse01);
                _beatStopReturnActive = false;
                _beatLastLoopActive = true;
                _lastBeatCurrentMultiplier = Mathf.Max(1f, multiplier);
                return _lastBeatCurrentMultiplier;
            }

            if (_beatLastLoopActive && !_beatStopReturnActive)
            {
                _beatStopReturnActive = true;
                _beatStopReturnStartTime = Time.unscaledTime;
                _beatStopReturnStartMultiplier = Mathf.Max(1f, _lastBeatCurrentMultiplier);
            }
            _beatLastLoopActive = false;

            if (_beatStopReturnActive)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - _beatStopReturnStartTime) / 0.5f);
                float multiplier = Mathf.Lerp(_beatStopReturnStartMultiplier, 1f, t);
                _lastBeatCurrentMultiplier = Mathf.Max(1f, multiplier);
                if (t >= 1f) _beatStopReturnActive = false;
                return _lastBeatCurrentMultiplier;
            }

            _lastBeatCurrentMultiplier = 1f;
            return 1f;
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
            if (_subCamera == null || _settings == null || !_settings.BeatFovEnabled || _transitionActive)
                return;

            float baseFov = Mathf.Clamp(_settings.CameraFieldOfView, 10f, 170f);
            float zoomedFov = ResolveBeatZoomedFov(baseFov);
            _subCamera.fieldOfView = zoomedFov;
        }
    }
}
