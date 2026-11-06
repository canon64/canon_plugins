using SessionGate = CanonPlugins.Local.SessionGate;

namespace MainGameCameraControl
{
    internal sealed partial class Plugin
    {
        // One-shot adoption after a location tool writes native CameraData. No settings are changed.
        public static bool TryAdoptHLocationCamera()
        {
            var p=Instance;
            if(!SessionGate.Allowed||p==null||p._astralCameraLease!=0)return false;
            p.RefreshCameraControllerCache();
            if(p._ctrlCamera==null)return true;
            p._transitionActive=false;
            p._transitionState=null;
            p._transitionHoldCamWorldActive=false;
            p._voiceFaceReturnPending=false;
            p.ClearBoneLinkActiveState();
            p._currentData=p._ctrlCamera.GetCameraData();
            p._targetData=p._currentData;
            p.SetBaseFov(p._currentData.Fov);
            p.ResetAutoCameraCycleTimer();
            return true;
        }
    }
}
