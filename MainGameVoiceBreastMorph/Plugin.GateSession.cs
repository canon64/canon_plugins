using MainGameVoiceFaceEventBridge;
using System;
using System.Threading;
using CanonGate;
namespace MainGameVoiceBreastMorph
{
    public sealed partial class Plugin
    {
        private void OnDisable() { SessionGate.Stop(); }
        private void BeginGateSession() { _originalStates.Clear(); ResponseTextTriggerApi.Received += OnResponseText; }
        private void EndGateSession() { ResponseTextTriggerApi.Received -= OnResponseText; CancelTattooFade(); StopAllCoroutines(); _transition = _skinTransition = _otherBreastTransition = null; _originalStates.Clear(); }
        
    }
}
