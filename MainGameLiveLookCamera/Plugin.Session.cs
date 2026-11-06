using UnityEngine;
using SessionGate = CanonPlugins.Local.SessionGate;
namespace MainGameLiveLookCamera {
internal sealed partial class Plugin {
private BaseCameraControl_Ver2.CameraData _sessionCameraData;
private CameraControl_Ver2 _sessionCamera;
private void OnApplicationFocus(bool focused){if(!focused)ReleaseInputCapture();}
private void OnDisable(){ReleaseInputCapture();}
private void BeginGateSession(){
RefreshCameraControllerCache(); _sessionCamera=_ctrlCamera; if(_sessionCamera!=null)_sessionCameraData=_sessionCamera.GetCameraData(); Camera.onPreCull -= OnCameraPreCull; Camera.onPreCull += OnCameraPreCull;
}
private void EndGateSession(){
_cachedFemale=null;
if(_sessionCamera!=null)_sessionCamera.SetCameraData(_sessionCameraData); _sessionCamera=null; _sessionCameraData=default; _ctrlCamera=null;
Camera.onPreCull -= OnCameraPreCull; ClearBoneLook("session-ended"); ReleaseInputCapture();
}
}
}
