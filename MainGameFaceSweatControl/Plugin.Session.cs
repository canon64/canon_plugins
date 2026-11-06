using UnityEngine;
using SessionGate = CanonPlugins.Local.SessionGate;
namespace MainGameFaceSweatControl {
public sealed partial class Plugin {
private void OnApplicationFocus(bool focused){if(!focused)ReleaseInputCapture();}
private void OnDisable(){ReleaseInputCapture();}
private void BeginGateSession(){
RefreshTargets("session-start");
}
private void EndGateSession(){
RestoreSessionFace(); OnBlankRoomLeft(); _targets.Clear();
}
}
}
