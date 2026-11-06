using System;
using System.Threading;
using CanonGate;
using UnityEngine;
namespace MainGameSubCameraDisplayProbe
{
    public sealed partial class Plugin
    {
        private void OnDisable() { SessionGate.Stop(); }
        private CanonPlugins.Shared.CameraLifecycleEvents _displayCameraEvents;
        private Coroutine _refreshDisplayCoroutine;
        private void RequestDisplayCameraRefresh()
        {
            if (!SessionGate.Allowed || !isActiveAndEnabled || _refreshDisplayCoroutine != null) return;
            _refreshDisplayCoroutine = StartCoroutine(RefreshDisplayCameraOnce());
        }
        private System.Collections.IEnumerator RefreshDisplayCameraOnce()
        {
            yield return null;
            _refreshDisplayCoroutine = null;
            if (SessionGate.Allowed && !_suspendedForAstralMode && _settings.Enabled && _rootObject != null)
                ApplyDisplayLayer();
        }
        private void BeginGateSession()
        {
            RegisterUnifiedTabIfAvailable(); _cachedHSceneProc = SessionGate.HScene;
            InstallCharactersOnlyInvalidation();
            ApplyRuntimeState();
        }
        private void EndGateSession()
        {
            try
            {
                _displayCameraEvents?.Dispose(); _displayCameraEvents = null;
                Camera.onPreCull -= OnDisplayMainCameraPreCull;
                CancelPlaybackCopyTrace(); StopAllCoroutines(); _refreshDisplayCoroutine = null;
                DisableRuntimeState("gate_stop"); ReleaseActor(); RemoveCharactersOnlyInvalidation();
                _cachedHSceneProc = null; _cachedFallbackFemale = null;
            }
            finally { MainGameUnifiedUi.UnifiedUiApi.UnregisterTab(UnifiedTabId); }
        }
        
    }
}
