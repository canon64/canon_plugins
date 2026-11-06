using UnityEngine;
using SessionGate = CanonPlugins.Local.SessionGate;
namespace MainGameCameraLookControl {
public sealed partial class Plugin {
private void BeginGateSession(){
_policy=_settings.Snapshot(); _cameras.Prepare(); TryRegisterTab();
}
private void EndGateSession(){
ClearRollLayers(); ReleaseWindowCapture(); UnregisterTab(); _runtimes.Clear(); _evaluations.Clear();
}
}
}
