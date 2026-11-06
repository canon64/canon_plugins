using System;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace MainGameVrDildo
{
    public sealed partial class Plugin
    {
        private static readonly string[] GaugeEffectNames = { "SweatLevel", "TearLevel", "DroolLevel" };
        private static readonly string[] GaugeEffectCountNames = { "WetTexturesFaceCount", "TearTexturesCount", "DroolTexturesCount" };
        private readonly int[] _effectOriginal = new int[3];
        private readonly int[] _effectApplied = new int[3];
        private readonly bool[] _effectOwned = new bool[3];
        private ChaControl _effectFemale;
        private object _effectController;
        private Type _effectControllerType, _effectTextureType;
        private readonly PropertyInfo[] _effectProperties = new PropertyInfo[3];
        private readonly PropertyInfo[] _effectCountProperties = new PropertyInfo[3];
        private int _effectAssemblyVersion, _effectResolvedAssemblyVersion = -1;
        private float _nextEffectControllerProbe;
        private bool _oralFluidStarted;
        private float _oralContactSeconds;

        private void UpdateGaugeEffects()
        {
            bool ready = _settings != null && _female != null && _scene != null &&
                _scene.isActiveAndEnabled && _sceneFlags != null;
            if (!ready) { ReleaseGaugeEffects(); return; }
            if (_effectFemale != _female)
            {
                ReleaseGaugeEffects();
                _effectFemale = _female;
            }
            bool oral = _settings.Enabled && _settings.OralFluidEnabled && _model != null &&
                _selectedTarget == TargetKind.Oral && _oralContact;
            if (oral)
            {
                _oralFluidStarted = true;
                _oralContactSeconds += Mathf.Max(0f, Time.deltaTime);
            }
            float gauge = _settings.MotionGaugeEnabled
                ? Mathf.Clamp01(_motionGaugeCm / _settings.MotionGaugeFullCm) : 0f;
            int sweat = 0;
            if (_model != null && _settings.Enabled && _settings.SweatEnabled &&
                _sceneFlags.mode == HFlag.EMode.aibu)
            {
                if (gauge >= _settings.SweatStage1GaugeFraction) sweat = _settings.SweatStage1Level;
                if (gauge >= _settings.SweatStage2GaugeFraction) sweat = _settings.SweatStage2Level;
                if (gauge >= _settings.SweatStage3GaugeFraction) sweat = _settings.SweatStage3Level;
            }
            int tears = OralFluidLevel(_settings.OralTearLevel, _settings.OralTearStepSeconds);
            int drool = OralFluidLevel(_settings.OralDroolLevel, _settings.OralDroolStepSeconds);
            if (sweat == 0 && tears == 0 && drool == 0 &&
                !_effectOwned[0] && !_effectOwned[1] && !_effectOwned[2]) return;
            if (!ResolveGaugeEffectController()) return;
            ApplyGaugeEffect(0, sweat);
            ApplyGaugeEffect(1, tears);
            ApplyGaugeEffect(2, drool);
        }

        private void GaugeEffectAssemblyLoaded(object sender, AssemblyLoadEventArgs args)
            => Interlocked.Increment(ref _effectAssemblyVersion);

        private bool ResolveGaugeEffectController()
        {
            int version = Volatile.Read(ref _effectAssemblyVersion);
            if (_effectControllerType == null && _effectResolvedAssemblyVersion != version)
            {
                _effectResolvedAssemblyVersion = version;
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    _effectControllerType = assembly.GetType("KK_SkinEffects.SkinEffectsController", false);
                    if (_effectControllerType == null) continue;
                    _effectTextureType = assembly.GetType("KK_SkinEffects.TextureLoader", false);
                    for (int i = 0; i < GaugeEffectNames.Length; i++)
                    {
                        _effectProperties[i] = _effectControllerType.GetProperty(GaugeEffectNames[i]);
                        _effectCountProperties[i] = _effectTextureType?.GetProperty(GaugeEffectCountNames[i],
                            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    }
                    break;
                }
            }
            if (_effectControllerType == null) return false;
            if (_effectController is UnityEngine.Object component && component == null) _effectController = null;
            if (_effectController == null && Time.unscaledTime >= _nextEffectControllerProbe)
            {
                // Optional character controllers can be added after the character loads.
                _nextEffectControllerProbe = Time.unscaledTime + 0.5f;
                _effectController = _female.GetComponent(_effectControllerType);
            }
            return _effectController != null;
        }

        private int OralFluidLevel(int maximum, float secondsPerLevel)
        {
            if (!_oralFluidStarted || !_settings.OralFluidEnabled || maximum <= 0) return 0;
            return Mathf.Min(maximum, 1 + Mathf.FloorToInt(_oralContactSeconds /
                Mathf.Max(0.5f, secondsPerLevel)));
        }

        private void ApplyGaugeEffect(int channel, int desired)
        {
            PropertyInfo property = _effectProperties[channel];
            if (property == null || !property.CanWrite) return;
            try
            {
                int current = Convert.ToInt32(property.GetValue(_effectController, null));
                if (desired <= 0)
                {
                    RestoreGaugeEffect(channel, property, current);
                    return;
                }
                if (!_effectOwned[channel])
                {
                    _effectOriginal[channel] = current;
                    _effectOwned[channel] = true;
                }
                PropertyInfo count = _effectCountProperties[channel];
                int available = count != null ? Convert.ToInt32(count.GetValue(null, null)) : 0;
                int value = Mathf.Max(_effectOriginal[channel],
                    Mathf.Clamp(desired, 0, available > 0 ? available : 3));
                if (current != value) property.SetValue(_effectController,
                    Convert.ChangeType(value, property.PropertyType), null);
                _effectApplied[channel] = value;
            }
            catch (Exception ex) { LogDiagnostic("Gauge effect " + GaugeEffectNames[channel] + ": " + ex.Message); }
        }

        private void RestoreGaugeEffect(int channel, PropertyInfo property, int current)
        {
            if (!_effectOwned[channel]) return;
            if (current == _effectApplied[channel] && current != _effectOriginal[channel])
                property.SetValue(_effectController,
                    Convert.ChangeType(_effectOriginal[channel], property.PropertyType), null);
            _effectOwned[channel] = false;
        }

        private void ReleaseGaugeEffects()
        {
            if (_effectController != null && _effectControllerType != null)
                for (int i = 0; i < GaugeEffectNames.Length; i++)
                {
                    PropertyInfo property = _effectProperties[i];
                    if (!_effectOwned[i] || property == null) continue;
                    try { RestoreGaugeEffect(i, property, Convert.ToInt32(property.GetValue(_effectController, null))); }
                    catch (Exception ex) { LogDiagnostic("Gauge effect restore: " + ex.Message); }
                }
            for (int i = 0; i < _effectOwned.Length; i++) _effectOwned[i] = false;
            _effectFemale = null;
            _effectController = null;
            _nextEffectControllerProbe = 0f;
            _oralFluidStarted = false;
            _oralContactSeconds = 0f;
        }

        private void ReleaseGaugeSweat()
        {
            if (_effectController == null || _effectControllerType == null || !_effectOwned[0]) return;
            PropertyInfo property = _effectProperties[0];
            if (property == null) return;
            try { RestoreGaugeEffect(0, property, Convert.ToInt32(property.GetValue(_effectController, null))); }
            catch (Exception ex) { LogDiagnostic("Gauge sweat restore: " + ex.Message); }
        }
    }
}
