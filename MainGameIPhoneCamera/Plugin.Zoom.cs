using System;
using UnityEngine;

namespace MainGameIPhoneCamera
{
    public sealed partial class Plugin
    {
        private PhoneZoomMotion _zoomMotion;
        private void CameraZoomAxis(float x, float y)
        {
            if (!CanAct || _phoneCamera == null) return;
            if (Math.Abs(y) > Math.Abs(x) && Math.Abs(y) > .2f)
            {
                if (_zoomMotion == null)
                {
                    _zoomMotion = _phoneCamera.gameObject.AddComponent<PhoneZoomMotion>();
                    _zoomMotion.Initialize(_phoneCamera, value =>
                    {
                        if (!CanAct || _phone.Page != PhonePage.Camera) return;
                        _app.SetZoom(value); _zoomText = _app.Zoom.ToString("0.00");
                        _cameraUi?.Zoom(_app.Zoom);
                    });
                }
                _zoomMotion.Drive(y, _fov.Value);
                return;
            }
            StopCameraZoom();
            if (_app.Axis(x, 0)) _cameraUi?.Apply(_app);
        }
        private void StopCameraZoom() { _zoomMotion?.StopMotion(); }
    }
}
