using System;
using System.Threading;
using CanonGate;
namespace MainGameClubLights
{
    public sealed partial class Plugin
    {
        private void OnDisable() { SessionGate.Stop(); }
        private void BeginGateSession() { RegisterUnifiedTabIfAvailable(); _hSceneProc = SessionGate.HScene; _insideHScene = true; if (_cfgEnabled.Value) OnHSceneStart(); }
        private void EndGateSession() { try { ReleaseInputCapture(); _insideHScene = false; OnHSceneEnd(); } finally { MainGameUnifiedUi.UnifiedUiApi.UnregisterTab(UnifiedTabId); } }
        
    }
}
