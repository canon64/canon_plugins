using System;
using System.Threading;
using CanonGate;
namespace MainGameVrScreenEffectProbe
{
    public sealed partial class Plugin
    {
        private void OnDisable() { SessionGate.Stop(); }
        private void BeginGateSession() { ScanAndAttach("gate_start"); }
        private void EndGateSession() { DetachAllLayers("gate_stop"); }
        
    }
}
