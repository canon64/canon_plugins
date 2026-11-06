using SessionGate = CanonPlugins.Local.SessionGate;
using UnityEngine;

namespace MainGameCameraControl
{
    internal sealed partial class Plugin
    {
        private long _astralCameraLease;
        private long _astralCameraLeaseSerial;

        public static long SuspendForAstralCamera()
        {
            var p = Instance;
            if (!SessionGate.Allowed || p == null) return 0;
            if (p._astralCameraLease != 0) return p._astralCameraLease;
            p._astralCameraLease = ++p._astralCameraLeaseSerial;
            p.ReleaseInputCapture();
            return p._astralCameraLease;
        }

        public static void ResumeFromAstralCamera(long token)
        {
            var p = Instance;
            if (p == null || token == 0 || p._astralCameraLease != token) return;
            // The owner restores native CameraData first. Discard intermediate astral
            // targets so a pending transition cannot overwrite that restored view.
            p._astralCameraLease = 0;
            p._transitionActive = false;
            p._transitionState = null;
            p._transitionHoldCamWorldActive = false;
            p._voiceFaceReturnPending = false;
            if (p._ctrlCamera != null)
            {
                p._currentData = p._ctrlCamera.GetCameraData();
                p._targetData = p._currentData;
                p.SetBaseFov(p._currentData.Fov);
            }
            p.ResetAutoCameraCycleTimer();
        }
    }
}
