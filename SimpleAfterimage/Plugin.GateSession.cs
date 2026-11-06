using System;
using System.Threading;
using CanonGate;
namespace SimpleAfterimage
{
    public sealed partial class Plugin
    {
        private void OnDisable() { SessionGate.Stop(); }
        private void BeginGateSession() { RegisterAfterimageTab(); _hSceneProc = SessionGate.HScene; ApplyConfig(); }
        private void EndGateSession() { try { StopAllCoroutines(); ClearExternalAfterimage(); ClearWarpAfterimage(); StopAfterimageRuntime(); SetKaleidoscopeThoughtMask(false, null); _hSceneProc = null; } finally { MainGameUnifiedUi.UnifiedUiApi.UnregisterTab("simple-afterimage"); } }
        
    }
}
