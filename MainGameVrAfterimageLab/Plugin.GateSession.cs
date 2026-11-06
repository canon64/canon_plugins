using System;
using System.Threading;
using CanonGate;
namespace MainGameVrAfterimageLab
{
    public sealed partial class Plugin
    {
        private void OnDisable() { SessionGate.Stop(); }
        private void BeginGateSession() { _hSceneProc = SessionGate.HScene; ApplyConfig(); }
        private void EndGateSession() { StopAllCoroutines(); StopRuntime(); _hSceneProc = null; }
        
    }
}
