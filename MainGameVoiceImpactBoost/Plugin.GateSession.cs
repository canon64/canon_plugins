using System;
using System.Threading;
using CanonGate;
namespace MainGameVoiceImpactBoost
{
    public sealed partial class Plugin
    {
        private void OnDisable() { SessionGate.Stop(); }
        private void BeginGateSession() { _boostService.ResetOneShotState(); TrySubscribe(); UpdateCueMode(); }
        private void EndGateSession() { TryUnsubscribe(); MainGameVoiceFaceEventBridge.PublicApi.CancelTransientVolumeBoost(GUID); MainGameVoiceFaceEventBridge.PublicApi.SetImpactCueEnabled(GUID, false); _lastCueModeEnabled = false; _boostService.ResetOneShotState(); }
        
    }
}
