using UnityEngine;
using SessionGate = CanonPlugins.Local.SessionGate;
namespace MainGameCameraControl {
internal sealed partial class Plugin {
private BaseCameraControl_Ver2.CameraData _sessionCameraData;
private CameraControl_Ver2 _sessionCamera;
private void OnApplicationFocus(bool focused){if(!focused)ReleaseInputCapture();}
private void OnDisable(){ReleaseInputCapture();}
private void BeginGateSession(){
RefreshCameraControllerCache(); _sessionCamera=_ctrlCamera; if(_sessionCamera!=null)_sessionCameraData=_sessionCamera.GetCameraData(); RegisterUnifiedTabIfAvailable();
}
private void EndGateSession(){
_astralCameraLease=0;
MainGameUnifiedUi.UnifiedUiApi.UnregisterTab(UnifiedTabId);
_cachedFemale=null; _poseScene=null; InvalidateFemaleCache();
if(_sessionCamera!=null)_sessionCamera.SetCameraData(_sessionCameraData); _sessionCamera=null; _sessionCameraData=default; _ctrlCamera=null;
_transitionActive=false; ClearBoneLinkActiveState(); ReleaseInputCapture(); DestroyLookAtAnchor(); if(_cameraPreCullHooked){ Camera.onPreCull-=OnCameraPreCull; _cameraPreCullHooked=false; }
}
}
}
