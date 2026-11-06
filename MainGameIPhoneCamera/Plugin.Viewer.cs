using System;
using UnityEngine;

namespace MainGameIPhoneCamera
{
    public sealed partial class Plugin
    {
        private PhoneDeadline _viewerUiDeadline;
        private bool _viewerUiVisible, _viewerPanning, _viewerTriggerHeld;
        private Vector2 _viewerAxis;
        private void ShowViewerUi()
        {
            _viewerUiVisible = true; _shellUi.ViewerUi(true);
            _viewerPanning = _viewerTriggerHeld;
            RefreshViewerUiTimeout();
        }
        private void RefreshViewerUiTimeout()
        {
            _viewerUiDeadline?.Cancel();
            if (!_viewerUiVisible || _phone.Page != PhonePage.Viewer) return;
            if (_viewerTriggerHeld || Math.Abs(_viewerAxis.x) > .2f || Math.Abs(_viewerAxis.y) > .2f) return;
            if (_viewerUiDeadline == null) _viewerUiDeadline = new PhoneDeadline(_mainThread);
            int epoch = _screenEpoch;
            _viewerUiDeadline.Arm(TimeSpan.FromSeconds(3), () =>
            {
                if (!CanAct || epoch != _screenEpoch || _phone.Page != PhonePage.Viewer) return;
                _viewerUiVisible = false;
                _navigationRepeat?.Cancel(); _axisX = _axisY = 0; _phoneAxisLatched = false;
                _shellUi.ViewerUi(false);
            });
        }
        private void ConfirmViewer()
        {
            if (!_viewerUiVisible) { ShowViewerUi(); return; }
            RefreshViewerUiTimeout();
            if (!_viewerPanning) ConfirmViewerControl();
        }
        private void StopViewerInput(bool clearTrigger = true)
        {
            if (clearTrigger) _viewerTriggerHeld = false;
            _viewerAxis = Vector2.zero; _viewerPanning = false;
            _shellUi?.StopViewerMotion(); _navigationRepeat?.Cancel(); _axisX = _axisY = 0;
            _phoneAxisLatched = false;
            RefreshViewerUiTimeout();
        }
        private void ExitViewer()
        {
            StopViewerInput(); _viewerUiVisible = false;
            _viewerUiDeadline?.Cancel();
        }
        private void SetViewerPan(bool pressed)
        {
            if (_phone.Page != PhonePage.Viewer) return;
            _viewerTriggerHeld = pressed;
            _shellUi.StopViewerMotion(); _navigationRepeat?.Cancel(); _axisX = _axisY = 0;
            _viewerPanning = pressed; _phoneAxisLatched = false;
            ViewerAxis(_viewerAxis.x, _viewerAxis.y);
        }
        private void ViewerAxis(float x, float y)
        {
            _viewerAxis = new Vector2(x, y);
            RefreshViewerUiTimeout();
            if (_viewerPanning) { _shellUi.ViewerAxis(x, y, true); return; }
            if (Math.Abs(y) > Math.Abs(x) && Math.Abs(y) > .2f)
            {
                _navigationRepeat?.Cancel(); _axisX = _axisY = 0; _phoneAxisLatched = false;
                _shellUi.ViewerAxis(0, y, false); return;
            }
            _shellUi.StopViewerMotion();
            if (!_viewerUiVisible) { _axisX = _axisY = 0; _navigationRepeat?.Cancel(); _phoneAxisLatched = false; return; }
            if (Math.Abs(x) < .25f) { _phoneAxisLatched = false; _axisX = _axisY = 0; _navigationRepeat?.Cancel(); return; }
            if (_phoneAxisLatched || Math.Abs(x) < .65f) return;
            _phoneAxisLatched = true; _axisX = Math.Sign(x); _axisY = 0;
            NavigatePhone(_axisX, 0); ArmPhoneRepeat(TimeSpan.FromMilliseconds(400));
        }
    }
}
