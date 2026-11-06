using UnityEngine;
using System;
using System.Threading;
using CanonGate;
namespace MainGameKaleidoscope
{
    public sealed partial class Plugin
    {
        private void BeginGateSession() { MainGameUnifiedUi.UnifiedUiApi.RegisterTab(TabId, "万華鏡", DrawTab, 74); _clock = 0f; _duckDiscoveryDone = false; InstallRendererInvalidation(); ScanCharacters(); }
        private void EndGateSession() { try { _thoughtActive = false; _manualActive = false; _strength = 0f; SimpleAfterimage.Plugin.SetKaleidoscopeThoughtMask(false, null); DisposeScreenEffect(); RestoreAudioDucking(); RemoveRendererInvalidation(); } finally { MainGameUnifiedUi.UnifiedUiApi.UnregisterTab(TabId); } }
        
    }
}
