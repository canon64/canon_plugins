using UnityEngine;
using SessionGate = CanonPlugins.Local.SessionGate;
namespace MainGameAutoHVoice {
internal sealed partial class Plugin {
private void OnApplicationFocus(bool focused){if(!focused)ReleaseInputCapture();}
private void OnDisable(){ReleaseInputCapture();}
private void BeginGateSession(){
CurrentProc=SessionGate.HScene; RegisterUnifiedTabIfAvailable();
}
private void EndGateSession(){
CurrentProc=null; _lastCapturedRaw=-1; StopAllCoroutines(); ReleaseInputCapture(); MainGameUnifiedUi.UnifiedUiApi.UnregisterTab(UnifiedTabId);
}
}
}
