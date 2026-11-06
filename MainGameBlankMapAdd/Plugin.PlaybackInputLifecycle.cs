using UnityEngine;
namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private void HideUiBlocker()
        {
            ClearVrGadgetDeleteTargets();
            ReleaseHPlaceSliderCapture();
            _hPlaceUiAction = null;
            _mapSelectionPending = null;
            _aiIslandSettingsPending = null;
            CommitAiIslandUiSettings();
            // H-location edits survive hiding the UI so the player can frame the camera.
            // Explicit cancel, map changes and session teardown own draft rollback.
            ClearPlaybackHelpWindow();
            if ((_playbackSeekControl != 0 && GUIUtility.hotControl == _playbackSeekControl)
                || (_playbackSliderHotControl != 0 && GUIUtility.hotControl == _playbackSliderHotControl))
                GUIUtility.hotControl = 0;
            _playbackSeekControl = _playbackSliderHotControl = 0;
            _playbackSeekDragging = false;
            if (_playbackVolumeDragging && !_vmdBarMode) CommitPlaybackBarVolume();
            if (_playbackGainDragging) CommitPlaybackBarVideoGain();
            _playbackVolumeDragging = _playbackGainDragging = false;
            ClearEmbeddedBarInteraction();
            _uiResources?.HideBlocker();
        }

    }
}
