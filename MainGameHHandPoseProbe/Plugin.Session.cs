using UnityEngine;
using SessionGate = CanonPlugins.Local.SessionGate;
namespace MainGameHHandPoseProbe {
public sealed partial class Plugin {
private void OnApplicationFocus(bool focused){if(!focused)ReleaseInputCapture();}
private void OnDisable(){ReleaseInputCapture();}
private void BeginGateSession(){
_cachedUiHSceneProc = SessionGate.HScene; _cachedUiFemales=ResolveList(_cachedUiHSceneProc,FiLstFemale,1); _cachedUiMales=ResolveList(_cachedUiHSceneProc,FiLstMale,0);
}
private void EndGateSession(){
RestoreSessionHands(); ReleaseInputCapture(); _cachedUiHSceneProc=null; _cachedUiFemales=null; _cachedUiMales=null;
}
}
}
