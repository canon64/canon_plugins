using UnityEngine;
using SessionGate = CanonPlugins.Local.SessionGate;
namespace MainGameCharacterAfterimage {
public sealed partial class Plugin {
private void BeginGateSession(){
_sourceCamera = ResolveSourceCamera(); if (_sourceCamera != null) RebindPipeline(_sourceCamera);
}
private void EndGateSession(){
ShutdownPipeline("session-ended"); _sourceCamera=null;
}
}
}
