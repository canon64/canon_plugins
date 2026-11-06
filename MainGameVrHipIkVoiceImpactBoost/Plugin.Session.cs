using UnityEngine;
using SessionGate = CanonPlugins.Local.SessionGate;
namespace MainGameVrHipIkVoiceImpactBoost {
public sealed partial class Plugin {
private void BeginGateSession(){
_boostService?.Reset(); _detector?.Reset("session-start"); UpdateCueMode();
}
private void EndGateSession(){
_boostService?.Reset(); _detector?.Reset("session-ended"); MainGameVoiceFaceEventBridge.PublicApi.SetImpactCueEnabled(GUID,false); MainGameVoiceFaceEventBridge.PublicApi.CancelTransientVolumeBoost(GUID);
}
}
}
