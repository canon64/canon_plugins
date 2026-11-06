using System;
using System.Collections.Generic;
using MainGameUiInputCapture;
using UnityEngine;

namespace CanonGate
{
    /// <summary>Draws an entry-time snapshot only. Disabled whenever hidden; no Update or inspections.</summary>
    public sealed class WarningWindow : MonoBehaviour
    {
        private const string Owner = GateApi.PluginGuid + ".warning";
        private const string Drag = "warning-pointer";
        private readonly Rect[] _rects = new Rect[1];
        private Rect _rect;
        private Rect _bodyRect;
        private Rect _closeRect;
        private Vector2 _scroll;
        private string _title;
        private string[] _lines;
        private GUIStyle _label;
        private bool _visible;
        private bool _dismissed;
        private object _sceneKey;
        private bool _focused = true;
        private bool _inputRegistered;
        private bool _dragging;
        private bool _closeArmed;
        private int _ownedHot;
        private int _width, _height;
        internal bool Visible => _visible;
        internal string[] Lines => _lines;
        internal Rect CloseButtonRect => _closeRect;
        internal Action<string> Trace;

        private void Awake() { enabled = false; }

        internal void BeginScene(object sceneKey)
        {
            if (ReferenceEquals(_sceneKey, sceneKey)) return;
            Hide();
            _sceneKey = sceneKey;
            _dismissed = false;
        }

        private void Dismiss()
        {
            _dismissed = true;
            Hide();
            Trace?.Invoke("dismissed; drawing and input capture disabled");
        }

        internal void Show(IReadOnlyList<UnregisteredPlugin> missing)
        {
            if (_dismissed) return;
            Hide();
            if (missing.Count == 0) return;
            _title = "canon_gate 未接続：" + missing.Count + "件";
            _lines = new string[missing.Count];
            for (int i = 0; i < missing.Count; ++i) _lines[i] = missing[i].Name + "\n" + missing[i].Guid;
            _scroll = Vector2.zero;
            _visible = true;
            _focused = Application.isFocused;
            enabled = true;
            RefreshRect();
            Trace?.Invoke("shown; unregistered=" + missing.Count);
        }

        internal void Hide()
        {
            _visible = false;
            ReleaseInput();
            _lines = null;
            enabled = false;
        }

        private void RefreshRect()
        {
            _width = Screen.width; _height = Screen.height;
            _rect = new Rect(16, 16, Mathf.Max(1, Mathf.Min(720, _width - 32)), Mathf.Max(1, Mathf.Min(480, _height - 32)));
            _bodyRect = new Rect(_rect.x + 12, _rect.y + 12, Mathf.Max(1, _rect.width - 24), Mathf.Max(1, _rect.height - 66));
            _closeRect = new Rect(_rect.x + 12, _rect.y + _rect.height - 42, Mathf.Max(1, _rect.width - 24), 30);
            _rects[0] = _rect;
            if (!_visible || !_focused) return;
            UiInputCaptureApi.SetInputBlockRects(Owner, _rects);
            UiInputCaptureApi.SetIdleCursorUnlock(Owner, true);
            _inputRegistered = true;
        }

        private void OnGUI()
        {
            if (!_visible || !_focused) return;
            var keyboardEvent = Event.current;
            if (keyboardEvent != null && keyboardEvent.rawType == EventType.KeyDown && keyboardEvent.keyCode == KeyCode.Escape)
            {
                Trace?.Invoke("Escape key; dismiss warning");
                Dismiss();
                keyboardEvent.Use();
                return;
            }
            if (_width != Screen.width || _height != Screen.height) RefreshRect();
            int beforeHot = GUIUtility.hotControl;
            int depth = GUI.depth;
            bool previousEnabled = GUI.enabled;
            var previousMatrix = GUI.matrix;
            var previousColor = GUI.color;
            bool close = false;
            bool up = false;
            bool failed = false;
            Event ev = Event.current;
            try
            {
                GUI.depth = -10000;
                GUI.enabled = true;
                GUI.matrix = Matrix4x4.identity;
                GUI.color = Color.white;
                if (_label == null) _label = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false };
                bool down = ev != null && ev.rawType == EventType.MouseDown && ev.button == 0;
                up = ev != null && ev.rawType == EventType.MouseUp && ev.button == 0;
                if (down)
                {
                    _closeArmed = _closeRect.Contains(ev.mousePosition);
                    if (_closeArmed) Trace?.Invoke("close mouse-down");
                    if (_rect.Contains(ev.mousePosition))
                    {
                        _dragging = true;
                        UiInputCaptureApi.Sync(Owner, Drag, true);
                    }
                }
                close = up && _closeArmed && _closeRect.Contains(ev.mousePosition);
                if (up && _closeArmed) Trace?.Invoke(close ? "close mouse-up inside button" : "close mouse-up outside button; cancelled");
                // The close hit area is independent of GUILayout IDs, scroll offsets and native hotControl.
                GUI.Box(_rect, string.Empty, GUI.skin.window);
                GUILayout.BeginArea(_bodyRect);
                try
                {
                    GUILayout.Label(_title, _label);
                    GUILayout.Label("以下のプラグインはcanon_gate未接続です。この警告では動作を停止していません", _label);
                    _scroll = GUILayout.BeginScrollView(_scroll);
                    try { foreach (string line in _lines) GUILayout.Label(line, _label); }
                    finally { GUILayout.EndScrollView(); }
                }
                finally { GUILayout.EndArea(); }
                GUI.Box(_closeRect, "閉じる（Esc）", GUI.skin.button);
            }
            catch
            {
                failed = true;
                throw;
            }
            finally
            {
                if (_dragging && GUIUtility.hotControl != 0 && (beforeHot == 0 || GUIUtility.hotControl == _ownedHot))
                    _ownedHot = GUIUtility.hotControl;
                GUI.depth = depth;
                GUI.enabled = previousEnabled;
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
                if (failed) Hide();
            }
            if (close)
            {
                Dismiss();
                ev.Use();
            }
            else if (up)
            {
                _closeArmed = false;
                ReleaseDrag();
            }
        }

        private void ReleaseDrag()
        {
            if (_dragging) UiInputCaptureApi.Sync(Owner, Drag, false);
            _dragging = false;
            if (_ownedHot != 0 && GUIUtility.hotControl == _ownedHot) GUIUtility.hotControl = 0;
            _ownedHot = 0;
        }

        private void ReleaseInput()
        {
            _closeArmed = false;
            ReleaseDrag();
            if (!_inputRegistered) return;
            UiInputCaptureApi.ClearInputBlockRects(Owner);
            UiInputCaptureApi.SetIdleCursorUnlock(Owner, false);
            _inputRegistered = false;
        }

        private void OnApplicationFocus(bool focused)
        {
            _focused = focused;
            if (!focused) ReleaseInput();
            else if (_visible) RefreshRect();
        }
        private void OnDisable() => Hide();
        private void OnDestroy() => Hide();
    }
}
