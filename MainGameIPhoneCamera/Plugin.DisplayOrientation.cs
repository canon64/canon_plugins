using UnityEngine;

namespace MainGameIPhoneCamera
{
    public sealed partial class Plugin
    {
        private int _displayTurns;
        private PhonePage _orientationPage = PhonePage.Off;
        private bool TickPhoneOrientation()
        {
            // Explicitly requested one-second orientation check; never called from rendering.
            if (!CanAct || _root == null || _phoneCamera == null || _phone.Page == PhonePage.Off) return false;
            int turns = CaptureOrientation.StableFromGravity(Vector3.Dot(_phoneCamera.transform.right, Vector3.up), Vector3.Dot(_phoneCamera.transform.up, Vector3.up), _displayTurns);
            if (turns == _displayTurns && _orientationPage == _phone.Page) return true;
            _displayTurns = turns; _orientationPage = _phone.Page;
            _shellUi?.Orientation(turns);
            _cameraUi?.Orientation(turns);
            if (_phone.Page == PhonePage.Viewer)
            {
                if (_viewerPanning) _shellUi?.ViewerAxis(_viewerAxis.x, _viewerAxis.y, true);
                else if (System.Math.Abs(_viewerAxis.y) > System.Math.Abs(_viewerAxis.x)) _shellUi?.ViewerAxis(0, _viewerAxis.y, false);
            }
            Canvas.ForceUpdateCanvases();
            return true;
        }
    }
}
