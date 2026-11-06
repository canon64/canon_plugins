using System;
using System.Globalization;
using UnityEngine;

namespace MainGameDesktopDisplay
{
    public sealed partial class Plugin
    {
        private string _widthText, _distanceText;
        private void DrawTab()
        {
            // Registered tab: UnifiedUi/PlaybackBar owns the actual input rectangles and drag capture.
            if (!CanAct) { GUILayout.Label("対象のフリーHを開始してください。無効化後の再開は次のHからです。"); return; }
            if (Event.current.type == EventType.Layout && _uiAction != null)
            { var action = _uiAction; _uiAction = null; action(); }
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("ディスプレイ");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("目の前に呼び出す", GUILayout.Width(160))) _uiAction = () => Recall();
            if (GUILayout.Button("ギズモ表示", GUILayout.Width(110))) _uiAction = () => ShowGizmo();
            if (GUILayout.Button("削除", GUILayout.Width(65))) _uiAction = PlaybackBarDelete;
            GUILayout.EndHorizontal();
            if (FloatControl("画面幅（m）", ref _widthText, _width.Value, .1f, 20f, out var width)) { _width.Value = width; Resize(); }
            if (FloatControl("呼出距離（m）", ref _distanceText, _distance.Value, .2f, 20f, out var distance)) _distance.Value = distance;
            if (_gizmo != null && _gizmo.IsVisible)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("移動", GUILayout.Width(70))) _gizmo.SetMode(MainGameTransformGizmo.GizmoMode.Move);
                if (GUILayout.Button("回転", GUILayout.Width(70))) _gizmo.SetMode(MainGameTransformGizmo.GizmoMode.Rotate);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("映すデスクトップ");
            if (GUILayout.Button("モニター一覧を再取得", GUILayout.Width(195)))
            {
                _uiAction = () =>
                {
                    try { RefreshMonitors(); if (_root != null) StartCapture(); }
                    catch (Exception ex) { StopCapture(); _status = ex.Message; }
                };
            }
            int chosen = _monitor;
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("前へ", GUILayout.Width(65)) && _monitors.Length > 0) chosen = (_monitor + _monitors.Length - 1) % _monitors.Length;
            if (GUILayout.Button("次へ", GUILayout.Width(65)) && _monitors.Length > 0) chosen = (_monitor + 1) % _monitors.Length;
            GUILayout.EndHorizontal();
            // Keep this tree stable during MouseUp: apply choice after drawing every row.
            for (int i = 0; i < _monitors.Length; i++)
                if (GUILayout.Button((i == _monitor ? "● " : "○ ") + _monitors[i], GUILayout.Width(390))) chosen = i;
            GUILayout.EndVertical();
            var e = Event.current;
            var area = GUILayoutUtility.GetLastRect();
            if (e.type == EventType.KeyDown && area.Contains(e.mousePosition) && GUIUtility.keyboardControl == 0 && _monitors.Length > 0 && (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.DownArrow))
            { chosen = (_monitor + _monitors.Length + (e.keyCode == KeyCode.UpArrow ? -1 : 1)) % _monitors.Length; e.Use(); }
            if (chosen != _monitor) _uiAction = () => { _monitor = chosen; _monitorDevice.Value = _monitors[chosen].Device; if (_root != null) StartCapture(); };
            bool cursor = GUILayout.Toggle(_cursor.Value, "マウスカーソルも映す");
            if (cursor != _cursor.Value) _cursor.Value = cursor;
            GUILayout.BeginHorizontal();
            GUILayout.Label("更新上限", GUILayout.Width(75));
            foreach (int fps in new[] { 5, 10, 15, 30 })
                if (GUILayout.Button((_fps.Value == fps ? "● " : "") + fps + " fps", GUILayout.Width(78))) _fps.Value = fps;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("解像度上限", GUILayout.Width(85));
            int resolution = _resolution.Value;
            foreach (int pixels in new[] { 640, 1280, 1920 })
                if (GUILayout.Button((_resolution.Value == pixels ? "● " : "") + pixels + " px", GUILayout.Width(95))) resolution = pixels;
            GUILayout.EndHorizontal();
            if (resolution != _resolution.Value) { _resolution.Value = resolution; if (_root != null) StartCapture(); }
            bool gui = GUI.enabled; GUI.enabled = gui && _root != null;
            if (GUILayout.Button(_paused ? "キャプチャを再開" : "キャプチャを一時停止", GUILayout.Width(195)))
            {
                if (_paused) { if (_capture == null) StartCapture(); else { _paused = false; _driver.enabled = true; _nextFrame = 0; _status = "デスクトップ表示中"; } }
                else { _paused = true; _driver.enabled = false; _status = "一時停止中（最後の画像を表示）"; }
            }
            GUI.enabled = gui;
            GUILayout.Label("表示専用です。PC側で開いているブラウザーやアプリも、そのまま映ります。");
            GUILayout.Label(_status);
            GUILayout.EndVertical();
        }
        private static bool FloatControl(string label, ref string text, float value, float min, float max, out float next)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(105));
            next = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(180));
            bool changed = Math.Abs(next - value) > .00001f;
            if (changed) text = next.ToString("0.###", CultureInfo.InvariantCulture);
            string edit = GUILayout.TextField(text, GUILayout.Width(75));
            if (edit != text)
            {
                text = edit;
                if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && !float.IsNaN(parsed) && !float.IsInfinity(parsed) && parsed >= min && parsed <= max) { next = parsed; changed = true; }
            }
            GUILayout.EndHorizontal();
            return changed;
        }
    }
}
