using UnityEngine;
using SessionGate = CanonPlugins.Local.SessionGate;
namespace MainGameDollMode {
public partial class Plugin {
private void BeginGateSession(){
_resolvedOptionalAssemblyVersion=-1; EnsureOptionalTargetTypes(); ApplyGuardHooks(); ApplyDollModeState("session-start");
}
private void EndGateSession(){
Camera.onPreCull -= OnCameraPreCull; _cameraPreCullHookAttached=false; CancelFaceExitTransition("session-ended"); StopFaceEnterTransition("session-ended"); FinalizeDollModeExit("session-ended"); StopAllCoroutines(); _cachedActiveCharacters=null; RemoveGuardHooks();
}
}
}
