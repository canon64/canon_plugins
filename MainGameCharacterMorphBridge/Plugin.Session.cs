using UnityEngine;
using SessionGate = CanonPlugins.Local.SessionGate;
namespace MainGameCharacterMorphBridge {
public sealed partial class Plugin {
private void BeginGateSession(){
CaptureSessionMorph(); _currentHScene=SessionGate.HScene; StartHSceneAutoInitialize(_currentHScene);
}
private void EndGateSession(){
StopAllCoroutines(); EndTattooEffect(); EndMorphGlow(); StopMorphSound(); RestoreSessionMorph(); _originalSnapshot=null; _targetSnapshot=null; StopHSceneAutoInitialize(); StopBlendTransition(); StopPostMotionReapply(); StopExternalReapply(); StopClothesChangeReapply(); _currentHScene=null; ResetAnimationObservation(); ClearDirectOverrideState();
}
}
}
