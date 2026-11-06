using UnityEngine;
namespace CanonPlugins.Local {public static class SessionGate{public static bool Allowed=true;}}
namespace MainGameCameraControl
{
    internal sealed partial class Plugin
    {
        private static Plugin Instance;
        private long _astralCameraLease;
        private CameraControl_Ver2 _ctrlCamera;
        private bool _transitionActive=true,_transitionHoldCamWorldActive=true,_voiceFaceReturnPending=true;
        private object _transitionState=new object();
        private BaseCameraControl_Ver2.CameraData _currentData,_targetData;
        private bool boneLinked=true;private int timerResets;private float baseFov;
        private void RefreshCameraControllerCache(){}
        private void ClearBoneLinkActiveState(){boneLinked=false;}
        private void SetBaseFov(float fov){baseFov=fov;}
        private void ResetAutoCameraCycleTimer(){timerResets++;}
        internal static Plugin TestCreate(CameraControl_Ver2 camera){Instance=new Plugin{_ctrlCamera=camera};return Instance;}
        internal void TestLease(long value){_astralCameraLease=value;}
        internal bool TestAdopted=>!_transitionActive&&_transitionState==null&&!_transitionHoldCamWorldActive&&!_voiceFaceReturnPending&&!boneLinked&&timerResets>0&&baseFov==_ctrlCamera.GetCameraData().Fov&&_currentData.Fov==_targetData.Fov;
    }
}
