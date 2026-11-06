using System;
using System.Threading;
using CanonGate;
namespace MainGameVrVoiceGate
{
    public sealed partial class Plugin
    {
        private void BeginGateSession() { _lastGateActive = false; }
        private void EndGateSession() { SendStopIfActive("gate_stop"); _lastGateActive = false; _nextHeartbeatTime = 0f; }
        
    }
}
