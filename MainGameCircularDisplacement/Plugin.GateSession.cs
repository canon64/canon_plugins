using System;
using System.Threading;
using CanonGate;
namespace MainGameCircularDisplacement
{
    public sealed partial class Plugin
    {
        private void OnDisable() { SessionGate.Stop(); }
        private void BeginGateSession() { _hscene = SessionGate.HScene; if (_enabled.Value && _mode.Value != Mode.Off) { var camera = ResolveCamera(); if (camera != null) EnsureRuntime(camera); } }
        private void EndGateSession() { StopAllCoroutines(); _externalActive = false; StopRuntime(); _hscene = null; }
        
    }
}
