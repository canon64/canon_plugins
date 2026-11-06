using System;
using System.Threading;
using CanonGate;
namespace MainGameSubtitleCore
{
    internal sealed partial class Plugin
    {
        private void OnDisable() { SessionGate.Stop(); }
        private void BeginGateSession()
        {
            _overlayResolvedFont = null; _overlayResolvedFontSource = string.Empty;
            ReloadInputPanelState(); UpdateInputPanelRuntime();
        }
        private void EndGateSession()
        {
            SetInputPanelVisible(false);
            _inputPanelVisible = false;
            TextInputState.Clear();
            if (_inputCanvas != null) UnityEngine.Object.Destroy(_inputCanvas.gameObject);
            _inputCanvas = null;
            if (_eventSystem != null && _eventSystem.transform.parent == transform &&
                _eventSystem.gameObject.name == "MainGameSubtitleEventSystem")
                UnityEngine.Object.Destroy(_eventSystem.gameObject);
            _eventSystem = null;
            lock (_externalRequestsLock) _externalRequests.Clear();
            _overlayText = _overlayUpperText = _overlayShadowText = _overlayUpperShadowText = string.Empty;
            _overlayUntil = _overlayUpperUntil = 0f;
            _overlayResolvedFont = null; _overlayResolvedFontSource = string.Empty;
        }
        
    }
}
