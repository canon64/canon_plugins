using UnityEngine;
using SessionGate = CanonPlugins.Local.SessionGate;
namespace MainGameAdvIkBridge {
public sealed partial class Plugin {
private void OnDestroy(){SessionGate.Dispose();}
private void BeginGateSession(){
TryBindAdvIk("session-start"); ApplyBridgeSettings();
}
private void EndGateSession(){
RestoreSessionProperties(); _cachedCharacters=null;
}
}
}
