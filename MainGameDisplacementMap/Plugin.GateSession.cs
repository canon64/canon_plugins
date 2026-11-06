using System;
using System.Threading;
using CanonGate;
namespace MainGameDisplacementMap
{
    public sealed partial class Plugin
    {
        private void OnDisable() { SessionGate.Stop(); }
        private void BeginGateSession()
        {
            _hscene = SessionGate.HScene;
            RefreshCaptureBinding();
        }
        private void EndGateSession()
        {
            _cameraEvents?.Dispose(); _cameraEvents = null;
            StopAllCoroutines(); _refreshCaptureCoroutine = null;
            _externalActive = false; StopRuntime(); _hscene = null;
        }
        
    }
}
