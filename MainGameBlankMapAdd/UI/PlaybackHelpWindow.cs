using UnityEngine;

namespace MainGameBlankMapAdd.UI
{
    // Registered by the SAME OnGUI host as EmbeddedPlaybackWindowId on every
    // GUI event. Normal GUI.Box/depth overlays cannot overtake floating windows.
    internal sealed class PlaybackHelpWindow
    {
        internal const int WindowId = 0x50424850;
        private readonly GUI.WindowFunction _drawWindow;
        private Rect _rect;
        private GUIContent _content;
        private GUIStyle _style;

        internal PlaybackHelpWindow() { _drawWindow = DrawWindow; }

        internal void Set(Rect rect, GUIContent content, GUIStyle style)
        {
            _rect = rect;
            _content = content;
            _style = style;
        }

        internal void Clear() { _content = null; _style = null; }

        internal void Draw()
        {
            if (_content == null || _style == null) return;
            bool enabled = GUI.enabled;
            try
            {
                // Register through Layout, mouse/key events AND Repaint. A disabled
                // window participates in window ordering without accepting input.
                GUI.enabled = false;
                GUI.Window(WindowId, _rect, _drawWindow, GUIContent.none, GUIStyle.none);
                GUI.BringWindowToFront(WindowId);
            }
            finally { GUI.enabled = enabled; }
        }

        private void DrawWindow(int id)
        {
            // Native window callbacks may arrive after the host has cleared this help.
            var content = _content; var style = _style;
            if (content == null || style == null) return;
            bool enabled = GUI.enabled;
            Color color = GUI.color;
            try
            {
                // This is a passive box INSIDE the frontmost native GUI window.
                // No buttons, focus changes, DragWindow, Event.Use or hotControl.
                GUI.enabled = true;
                GUI.color = new Color(1f, 1f, 1f, 0.96f);
                GUI.Box(new Rect(0f, 0f, _rect.width, _rect.height), content, style);
            }
            finally { GUI.color = color; GUI.enabled = enabled; }
        }
    }
}
