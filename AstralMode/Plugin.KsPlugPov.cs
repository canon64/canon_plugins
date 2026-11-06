using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;
using SessionGate = CanonPlugins.Local.SessionGate;

namespace AstralMode
{
    public sealed partial class Plugin
    {
        private Component _ksPovOwner;
        private Type _ksPovOwnerType;
        private object _ksSavedSource, _ksSavedCustom, _ksSavedOriData;
        private bool _ksHadPov, _ksPovChanged;
        private float _ksSavedFov;

        private Component KsPov => _ksPovOwner == null ? null :
            AccessTools.Property(_ksPovOwnerType, "FpvCtrl").GetValue(_ksPovOwner, null) as Component;

        private void CaptureKsPlugPov()
        {
            _ksPovOwner = null;
            _ksPovChanged = false;
            _ksSavedOriData = null;
            _ksPovOwnerType = AccessTools.TypeByName("ksPlug.HSession.HCtrl");
            if (_ksPovOwnerType == null) return;
            var owner = AccessTools.Property(_ksPovOwnerType, "Get").GetValue(null, null) as Component;
            if (owner == null || !ReferenceEquals(AccessTools.Property(_ksPovOwnerType, "HFlag").GetValue(owner, null), _hFlag)) return;
            _ksPovOwner = owner;
            _ksSavedSource = AccessTools.Field(_ksPovOwnerType, "FpvSource").GetValue(owner);
            _ksSavedCustom = AccessTools.Field(_ksPovOwnerType, "FpvCustomCha").GetValue(owner);
            var pov = KsPov;
            _ksHadPov = pov != null;
            if (pov != null)
            {
                _ksSavedOriData = AccessTools.Field(pov.GetType(), "OriData").GetValue(pov);
                _ksSavedFov = (float)AccessTools.Field(pov.GetType(), "fovValue").GetValue(pov);
            }
        }

        private IEnumerator BeginKsPlugMalePov()
        {
            if (IsVrActive())
            {
                if (!PlaceVrMaleView()) yield return ExitAstralCoroutine();
                yield break;
            }
            if (!CanPlaceAstralCharacters || _ksPovOwner == null)
            {
                Logger.LogWarning("KsPlug male POV unavailable; no substitute camera applied");
                yield break;
            }
            IEnumerator nativeReset = null;
            try
            {
                var source = AccessTools.Field(_ksPovOwnerType, "FpvSource");
                source.SetValue(_ksPovOwner, Enum.Parse(source.FieldType, "Boy1"));
                AccessTools.Field(_ksPovOwnerType, "FpvCustomCha").SetValue(_ksPovOwner, null);
                _ksPovChanged = true;
                // These are exactly the two branches of KsPlug's RightShift handler.
                if (KsPov == null) AccessTools.Method(_ksPovOwnerType, "AddFPV").Invoke(_ksPovOwner, null);
                else nativeReset = (IEnumerator)AccessTools.Method(_ksPovOwnerType, "IE_ResetFpv").Invoke(_ksPovOwner, new object[] { false });
            }
            catch (Exception ex) { Logger.LogWarning("KsPlug male POV start failed: " + ex); }
            if (nativeReset != null)
            {
                var permit = SessionGate.Capture();
                try
                {
                    // Forward the native operation, checking its session at each yield.
                    // IE_ResetFpv(false) has one timed yield; this is not a monitor.
                    while (permit.IsValid && CanPlaceAstralCharacters && _ksPovOwner != null && nativeReset.MoveNext())
                        yield return nativeReset.Current;
                }
                finally { (nativeReset as IDisposable)?.Dispose(); }
            }
            // Native FPV.Start -> IE_Init sets the nose origin, head visibility,
            // female face target and its configured FOV at end of frame.
            yield return null;
            yield return new WaitForEndOfFrame();
            if (!CanPlaceAstralCharacters) yield break;
            TraceCoordinates("camera:ksplug-rightshift-boy-view");
        }

        private void StopKsPlugAstralPov()
        {
            if (!_ksPovChanged || _ksPovOwner == null) return;
            try
            {
                if (KsPov != null) AccessTools.Method(_ksPovOwnerType, "DestroyFPV").Invoke(_ksPovOwner, null);
                AccessTools.Field(_ksPovOwnerType, "FpvSource").SetValue(_ksPovOwner, _ksSavedSource);
                AccessTools.Field(_ksPovOwnerType, "FpvCustomCha").SetValue(_ksPovOwner, _ksSavedCustom);
            }
            catch (Exception ex) { Logger.LogWarning("KsPlug male POV stop failed: " + ex); }
        }

        private IEnumerator RestoreKsPlugPov()
        {
            if (!_ksPovChanged || _ksPovOwner == null || !SessionGate.Allowed) yield break;
            try
            {
                if (_ksHadPov && KsPov == null) AccessTools.Method(_ksPovOwnerType, "AddFPV").Invoke(_ksPovOwner, null);
            }
            catch (Exception ex) { Logger.LogWarning("KsPlug original POV start failed: " + ex); }
            if (_ksHadPov)
            {
                yield return null;
                yield return new WaitForEndOfFrame();
                if (!SessionGate.Allowed || _ksPovOwner == null) yield break;
                var pov = KsPov;
                if (pov != null)
                {
                    if (_ksSavedOriData != null) AccessTools.Field(pov.GetType(), "OriData").SetValue(pov, _ksSavedOriData);
                    AccessTools.Field(pov.GetType(), "fovValue").SetValue(pov, _ksSavedFov);
                }
            }
            _ksPovChanged = false;
        }
    }
}
