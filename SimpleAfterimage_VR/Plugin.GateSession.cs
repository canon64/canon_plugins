using System;
using System.Threading;
using CanonGate;
namespace SimpleAfterimage_VR
{
    public sealed partial class Plugin
    {
        private void OnDisable() { SessionGate.Stop(); }
        private void BeginGateSession() { _hSceneProc = SessionGate.HScene; ApplyConfig(); }
        private void EndGateSession() { StopAllCoroutines(); _drawCount = 0; if (_overlayDrawer != null) Destroy(_overlayDrawer); _overlayDrawer = null; if (_cameraRoot != null) Destroy(_cameraRoot); _cameraRoot = null; _captureCamera = null; ReleaseSlots(); _hSceneProc = null; }
        
    }
}
