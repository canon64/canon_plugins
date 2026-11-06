using MainGameUiInputCapture;
using UnityEngine;

namespace MainGameVrDildo
{
    public sealed partial class Plugin
    {
        private const string SettingsInputOwner = Guid + ".settings";
        private const string SettingsDragSource = "imgui-drag";
        private readonly Rect[] _settingsInputRects = new Rect[1];
        private int _settingsHotControl;
        private const string ExpressionMenuInputOwner = Guid + ".expression-menu";
        private readonly Rect[] _expressionMenuInputRects = new Rect[1];
        private int _expressionMenuHotControl;

        private void SyncExpressionMenuInput()
        {
            _expressionMenuInputRects[0] = _expressionMenuRect;
            UiInputCaptureApi.SetInputBlockRects(ExpressionMenuInputOwner, _expressionMenuInputRects);
            UiInputCaptureApi.SetIdleCursorUnlock(ExpressionMenuInputOwner, true);
            UiInputCaptureApi.Sync(ExpressionMenuInputOwner, SettingsDragSource,
                _expressionMenuHotControl != 0 && GUIUtility.hotControl == _expressionMenuHotControl && Input.GetMouseButton(0));
        }

        private void ClearExpressionMenuInput()
        {
            UiInputCaptureApi.Sync(ExpressionMenuInputOwner, SettingsDragSource, false);
            UiInputCaptureApi.ClearInputBlockRects(ExpressionMenuInputOwner);
            UiInputCaptureApi.SetIdleCursorUnlock(ExpressionMenuInputOwner, false);
            if (_expressionMenuHotControl != 0 && GUIUtility.hotControl == _expressionMenuHotControl) GUIUtility.hotControl = 0;
            _expressionMenuHotControl = 0;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            if (!VRGIN.Core.VR.Active) ReleaseGrab();
            DetachDesktopGizmo();
            CloseExpressionMenu(); ClearStandaloneInputCapture();
        }

        private void SyncStandaloneInputCapture()
        {
            if (!Application.isFocused || !_showSettings)
            {
                ClearStandaloneInputCapture();
                return;
            }
            _settingsInputRects[0] = _window;
            UiInputCaptureApi.SetInputBlockRects(SettingsInputOwner, _settingsInputRects);
            UiInputCaptureApi.SetIdleCursorUnlock(SettingsInputOwner, true);
            UiInputCaptureApi.Sync(SettingsInputOwner, SettingsDragSource,
                _settingsHotControl != 0 && GUIUtility.hotControl == _settingsHotControl && Input.GetMouseButton(0));
        }

        private void ClearStandaloneInputCapture()
        {
            UiInputCaptureApi.Sync(SettingsInputOwner, SettingsDragSource, false);
            UiInputCaptureApi.ClearInputBlockRects(SettingsInputOwner);
            UiInputCaptureApi.SetIdleCursorUnlock(SettingsInputOwner, false);
            if (_settingsHotControl != 0 && GUIUtility.hotControl == _settingsHotControl)
                GUIUtility.hotControl = 0;
            _settingsHotControl = 0;
        }

        private void OnDisable()
        {
            DisconnectGrabInput();
            _syringeVoiceLeases.Clear();
            StopPiston();
            ReleaseGrab();
            EndGaugePoseSource();
            CloseExpressionMenu();
            EndCloneReactionSession();
            ReleaseGaugeFace();
            ClearStandaloneInputCapture();
            DetachDesktopGizmo();
        }
    }
}
