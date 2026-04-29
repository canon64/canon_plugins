using MainGameUiInputCapture;
using UnityEngine;

namespace MainGameClubLights
{
    public sealed partial class Plugin
    {
        private const string InputOwnerKey  = Guid + ".input";
        private const string SourceWindow   = "window";
        private const string SourceSlider   = "slider";
        private const string SourceScroll   = "scroll";
        private const string SourceGizmo    = "gizmo-drag";

        private bool _windowDragging;
        private bool _sliderDragging;
        private bool _scrollDragging;
        private bool _gizmoDragging;

        // ── 毎フレーム呼び出し ───────────────────────────────────────────────

        private void UpdateInputCapture()
        {
            if (!UiInputCaptureApi.IsAvailable) return;

            // マウスボタン離したらリセット
            if (!Input.GetMouseButton(0))
            {
                _windowDragging = false;
                _sliderDragging = false;
                _scrollDragging = false;
                _gizmoDragging  = false;
            }

            // UIが非表示になったら全解放
            if (!_settings.UiVisible)
            {
                _windowDragging = false;
                _sliderDragging = false;
                _scrollDragging = false;
            }

            // UI表示中はカーソルを常に解放
            UiInputCaptureApi.SetIdleCursorUnlock(InputOwnerKey, _settings.UiVisible);

            UiInputCaptureApi.Sync(InputOwnerKey, SourceWindow, _windowDragging);
            UiInputCaptureApi.Sync(InputOwnerKey, SourceSlider, _sliderDragging);
            UiInputCaptureApi.Sync(InputOwnerKey, SourceScroll, _scrollDragging);
            UiInputCaptureApi.Sync(InputOwnerKey, SourceGizmo,  _gizmoDragging);
        }

        private void ReleaseInputCapture()
        {
            if (!UiInputCaptureApi.IsAvailable) return;
            UiInputCaptureApi.SetIdleCursorUnlock(InputOwnerKey, false);
            UiInputCaptureApi.EndOwner(InputOwnerKey);
            _windowDragging = false;
            _sliderDragging = false;
            _scrollDragging = false;
            _gizmoDragging  = false;
        }

        // UI側から呼ぶ
        internal void SetWindowDragging(bool v) => _windowDragging = v;
        internal void SetSliderDragging(bool v) => _sliderDragging = v;
        internal void SetScrollDragging(bool v) => _scrollDragging = v;

        // ギズモから呼ぶ
        internal void OnGizmoDragStateChanged(bool dragging) => _gizmoDragging = dragging;
    }
}
