using System;
namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        // Native map/session services are outside these host gesture/layout tests.
        private Action _mapSelectionPending, _aiIslandSettingsPending;
        private bool HasPlaybackBarHost => _videoRoomRoot != null;
        private void ReleaseHPlaceSliderCapture() { }
        // VR shortcut target storage is outside the embedded host gesture fixture.
        private void ClearVrGadgetDeleteTargets() { }
        private void CommitAiIslandUiSettings() { }
        private void DrawEmbeddedMapTab(float width, float height) => DrawEmbeddedMapTab(width);
    }
}
