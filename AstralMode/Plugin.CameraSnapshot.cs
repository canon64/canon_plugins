using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using SessionGate = CanonPlugins.Local.SessionGate;

namespace AstralMode
{
    public sealed partial class Plugin
    {
        private CameraControl_Ver2 _savedViewController;
        private BaseCameraControl_Ver2.CameraData _savedViewData;
        private Vector3 _savedViewBasePosition, _savedViewWorldPosition;
        private Quaternion _savedViewBaseRotation, _savedViewWorldRotation;
        private bool _savedViewHasBase;
        private object _savedViewReset, _savedViewTarget;
        private long _savedViewCameraLease;
        private float _savedViewRenderFov;
        private Transform _savedViewBaseTransform;
        private bool _savedViewLimitDir;
        private static readonly FieldInfo ViewResetField = AccessTools.Field(typeof(BaseCameraControl_Ver2), "CamReset");
        private static readonly FieldInfo ViewTargetField = AccessTools.Field(typeof(BaseCameraControl_Ver2), "targetObj");

        private static object CallCameraLease(string methodName, params object[] arguments)
        {
            var type = AccessTools.TypeByName("MainGameCameraControl.Plugin");
            return type == null ? null : AccessTools.Method(type, methodName)?.Invoke(null, arguments);
        }

        private bool CaptureRealCameraView()
        {
            if (!SessionGate.Allowed) return false;
            if (_savedViewController != null) return true;
            var ctrl = _hFlag != null ? _hFlag.ctrlCamera : null;
            if (ctrl == null) { Logger.LogWarning("Astral entry cancelled: no camera to save"); return false; }
            _savedViewData = ctrl.GetCameraData();
            _savedViewWorldPosition = ctrl.transform.position;
            _savedViewWorldRotation = ctrl.transform.rotation;
            _savedViewRenderFov = ctrl.thisCmaera != null ? ctrl.thisCmaera.fieldOfView : _savedViewData.Fov;
            _savedViewBasePosition = Vector3.zero;
            _savedViewBaseRotation = Quaternion.identity;
            _savedViewHasBase = ctrl.GetWorldBase(ref _savedViewBasePosition, ref _savedViewBaseRotation);
            _savedViewBaseTransform = ctrl.transBase;
            _savedViewLimitDir = ctrl.isLimitDir;
            _savedViewTarget = ViewTargetField?.GetValue(ctrl);
            object reset = ViewResetField?.GetValue(ctrl);
            _savedViewReset = reset == null ? null : AccessTools.Method(typeof(object), "MemberwiseClone").Invoke(reset, null);
            _savedViewController = ctrl;
            try { CaptureKsPlugPov(); }
            catch (Exception ex) { Logger.LogWarning("KsPlug camera snapshot failed: " + ex); }
            try { _savedViewCameraLease = (long)(CallCameraLease("SuspendForAstralCamera") ?? 0L); }
            catch (Exception ex) { Logger.LogWarning("Camera driver suspension failed: " + ex.Message); }
            TraceCoordinates("camera:saved-real-view");
            return true;
        }

        private void RestoreRealCameraView(bool finish)
        {
            try
            {
            var ctrl = _savedViewController;
            if (ctrl != null && _hFlag != null && ctrl == _hFlag.ctrlCamera)
            {
                if (_savedViewBaseTransform != null) ctrl.transBase = _savedViewBaseTransform;
                ctrl.isLimitDir = _savedViewLimitDir;
                if (_savedViewHasBase) ctrl.SetWorldBase(_savedViewBasePosition, _savedViewBaseRotation);
                if (_savedViewReset != null) ViewResetField?.SetValue(ctrl, _savedViewReset);
                ViewTargetField?.SetValue(ctrl, _savedViewTarget);
                ctrl.SetCameraData(_savedViewData);
                // Native data may have been stale when another camera driver wrote
                // the rendered transform. The captured world view is authoritative.
                if (Vector3.Distance(ctrl.transform.position, _savedViewWorldPosition) > 0.0001f ||
                    Quaternion.Angle(ctrl.transform.rotation, _savedViewWorldRotation) > 0.001f)
                {
                    var exact = _savedViewData;
                    Quaternion basis = _savedViewHasBase ? _savedViewBaseRotation : Quaternion.identity;
                    Vector3 origin = _savedViewHasBase ? _savedViewBasePosition : Vector3.zero;
                    exact.Rot = (Quaternion.Inverse(basis) * _savedViewWorldRotation).eulerAngles;
                    exact.Pos = Quaternion.Inverse(basis) * (_savedViewWorldPosition - _savedViewWorldRotation * exact.Dir - origin);
                    ctrl.SetCameraData(exact);
                }
                if (ctrl.thisCmaera != null) ctrl.thisCmaera.fieldOfView = _savedViewRenderFov;
                TraceCoordinates("camera:restored-real-view");
                float positionError = Vector3.Distance(ctrl.transform.position, _savedViewWorldPosition);
                float rotationError = Quaternion.Angle(ctrl.transform.rotation, _savedViewWorldRotation);
                try { AppendCoordinateTrace("CAMERA_RESTORE_ERROR position=" + positionError.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)
                    + " rotation=" + rotationError.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)); }
                catch (Exception ex) { Logger.LogWarning("Camera restore trace failed: " + ex.Message); }
            }
            }
            catch (Exception ex) { Logger.LogWarning("Real camera restore failed: " + ex); }
            finally
            {
            RestoreVrView(finish);
            if (finish) FinishRealCameraViewRestore();
            }
        }

        private void FinishRealCameraViewRestore()
        {
            try { CallCameraLease("ResumeFromAstralCamera", _savedViewCameraLease); }
            catch (Exception ex) { Logger.LogWarning("Camera driver resume failed: " + ex.Message); }
            _savedViewCameraLease = 0;
            _savedViewController = null;
            _savedViewReset = null;
            _savedViewTarget = null;
        }

    }
}
