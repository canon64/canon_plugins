using System;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;

namespace MainGameDesktopDisplay
{
    public sealed partial class Plugin
    {
        private DisplayClip _clip;
        private ConfigEntry<string> _clipEntry;
        private readonly float[] _clipDraft = new float[4];
        private readonly string[] _clipText = new string[4];
        private static readonly string[] ClipLabels = { "左（％）", "右（％）", "上（％）", "下（％）" };
        private static readonly string[] ClipControls = { "DesktopClipLeft", "DesktopClipRight", "DesktopClipTop", "DesktopClipBottom" };
        private bool _clipDirty, _clipFieldFocused;

        private void LoadMonitorClip()
        {
            if (_monitors.Length == 0) return;
            var monitor = _monitors[_monitor];
            _screenAspect = (float)monitor.Rect.Width / monitor.Rect.Height;
            string clipSection = "Clip." + monitor.Device.Replace('\\', '_');
            string defaultClip = clipSection == "Clip.__._DISPLAY1" ? "0,30.9237251,6.767307,70.79644" : "0,0,0,0";
            _clipEntry = Config.Bind(clipSection, "EdgesPercent", defaultClip, "上下左右の切り取り率：左,右,上,下（％）。表示面と取得範囲を切り詰めます。");
            var parts = _clipEntry.Value.Split(',');
            for (int i = 0; i < 4; i++)
            {
                float value = 0;
                if (i < parts.Length) float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out value);
                _clipDraft[i] = float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Clamp(value, 0, 100);
            }
            // Normalize only the in-memory model. Reading malformed settings never rewrites them.
            for (int i = 0; i < 4; i++) _clipDraft[i] = Mathf.Clamp(_clipDraft[i], 0, ClipMaximum(i));
            _clip = new DisplayClip(monitor.Rect, _clipDraft[0], _clipDraft[1], _clipDraft[2], _clipDraft[3]);
            ResetClipText();
        }
        private float ClipMaximum(int edge)
        {
            var bounds = _monitors[_monitor].Rect;
            int pixels = edge < 2 ? bounds.Width : bounds.Height;
            return Math.Max(0, 100f - _clipDraft[edge ^ 1] - 100f / Math.Max(1, pixels));
        }
        private void ResetClipText()
        {
            for (int i = 0; i < 4; i++) _clipText[i] = _clipDraft[i].ToString("0.####", CultureInfo.InvariantCulture);
            _clipDirty = _clipFieldFocused = false;
        }
        private void CommitClipDraft()
        {
            if (!_clipDirty || _clipEntry == null || _monitors.Length == 0) return;
            var next = new DisplayClip(_monitors[_monitor].Rect, _clipDraft[0], _clipDraft[1], _clipDraft[2], _clipDraft[3]);
            bool boundsChanged = next.Bounds.Left != _clip.Bounds.Left || next.Bounds.Top != _clip.Bounds.Top ||
                next.Bounds.Right != _clip.Bounds.Right || next.Bounds.Bottom != _clip.Bounds.Bottom;
            _clip = next;
            // A single setting write, never four per-edge saves or saves on each drag event.
            _clipEntry.Value = string.Join(",", _clipText[0] = _clipDraft[0].ToString("R", CultureInfo.InvariantCulture),
                _clipText[1] = _clipDraft[1].ToString("R", CultureInfo.InvariantCulture),
                _clipText[2] = _clipDraft[2].ToString("R", CultureInfo.InvariantCulture),
                _clipText[3] = _clipDraft[3].ToString("R", CultureInfo.InvariantCulture));
            ResetClipText();
            if (boundsChanged && _root != null)
            {
                bool paused = _paused;
                StartCapture();
                if (paused && _capture != null) { _paused = true; _driver.enabled = false; SyncDesktopMouse(); _status = "一時停止中（切り取り範囲を反映済み）"; }
            }
        }
        private void DrawClipControls()
        {
            if (_clipEntry == null) return;
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("表示範囲のクリッピング（離した時・入力確定時に反映）");
            for (int i = 0; i < 4; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(ClipLabels[i], GUILayout.Width(105));
                float value = GUILayout.HorizontalSlider(_clipDraft[i], 0, ClipMaximum(i), GUILayout.Width(180));
                if (Math.Abs(value - _clipDraft[i]) > .000001f)
                { _clipDraft[i] = value; _clipText[i] = value.ToString("0.####", CultureInfo.InvariantCulture); _clipDirty = true; }
                GUI.SetNextControlName(ClipControls[i]);
                string text = GUILayout.TextField(_clipText[i], GUILayout.Width(75));
                if (text != _clipText[i])
                {
                    _clipText[i] = text;
                    if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0 && value <= ClipMaximum(i))
                    { _clipDraft[i] = value; _clipDirty = true; }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            var e = Event.current;
            string control = GUI.GetNameOfFocusedControl();
            bool focused = control == ClipControls[0] || control == ClipControls[1] || control == ClipControls[2] || control == ClipControls[3];
            bool enter = focused && e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
            if (_clipDirty && ((e.rawType == EventType.MouseUp && e.button == 0 && GUIUtility.hotControl == 0) || enter || (_clipFieldFocused && !focused))) CommitClipDraft();
            _clipFieldFocused = focused;
            if (enter) e.Use();
        }
    }
}
