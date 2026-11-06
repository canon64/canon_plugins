using MainGameUiInputCapture;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    internal static class AiModeNativeUiCapture
    {
        internal static void Apply(string owner, Rect rect)
        {
            UiInputCaptureApi.SetInputBlockRects(owner, new[] { rect });
            // Keep the camera/key exclusion, but let this native uGUI receive its own clicks.
            // SetInputBlockRects also creates the transparent blocker intended for IMGUI.
            UiInputCaptureApi.ClearBlockRects(owner);
            UiInputCaptureApi.SetIdleCursorUnlock(owner, true);
        }
    }
}
