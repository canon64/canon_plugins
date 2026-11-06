using System;
using System.Globalization;
using UnityEngine;

namespace MainGameDesktopDisplay
{
    public sealed partial class Plugin
    {
        private string _widthText;
        // Only one settings group is expanded. Change its tree on Layout, never mid-MouseUp.
        private int _openSettingsSection, _pendingSettingsSection = -1;
        private static readonly int[] FpsChoices = { 5, 10, 15, 30 }, ResolutionChoices = { 640, 1280, 1920 };
        private static readonly string[] FpsLabels = { "5 fps", "10 fps", "15 fps", "30 fps" }, FpsSelected = { "● 5 fps", "● 10 fps", "● 15 fps", "● 30 fps" };
        private static readonly string[] ResolutionLabels = { "640 px", "1280 px", "1920 px" }, ResolutionSelected = { "● 640 px", "● 1280 px", "● 1920 px" };
        private void DrawTab()
        {
            // Registered tab: UnifiedUi/PlaybackBar owns the actual input rectangles and drag capture.
            if (!CanAct) { GUILayout.Label("対象のフリーHを開始してください。無効化後の再開は次のHからです。"); return; }
            if (Event.current.type == EventType.Layout && _pendingSettingsSection >= 0)
            {
                CommitSpawnPosition(); CommitClipDraft();
                _openSettingsSection = _pendingSettingsSection; _pendingSettingsSection = -1;
                _spawnFocused = _clipFieldFocused = false;
            }
            if (Event.current.type == EventType.Layout && _uiAction != null)
            { var action = _uiAction; _uiAction = null; action(); }
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("ディスプレイ");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("目の前に呼び出す", GUILayout.Width(160))) _uiAction = () => Recall();
            if (GUILayout.Button("ギズモ表示", GUILayout.Width(110))) _uiAction = () => ShowGizmo();
            if (GUILayout.Button("削除", GUILayout.Width(65))) _uiAction = PlaybackBarDelete;
            GUILayout.EndHorizontal();
            if (_gizmo != null && _gizmo.IsVisible)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("移動", GUILayout.Width(70))) _gizmo.SetMode(MainGameTransformGizmo.GizmoMode.Move);
                if (GUILayout.Button("回転", GUILayout.Width(70))) _gizmo.SetMode(MainGameTransformGizmo.GizmoMode.Rotate);
                GUILayout.EndHorizontal();
            }
            DrawCapturePause();
            GUILayout.Label(_status);
            GUILayout.EndVertical();

            if (SettingsSection(1, "▶ 出現位置", "▼ 出現位置")) DrawSpawnPosition();
            if (SettingsSection(2, "▶ 画面サイズ", "▼ 画面サイズ"))
            {
                GUILayout.BeginVertical(GUI.skin.box);
                if (FloatControl("画面幅（m）", ref _widthText, _width.Value, .1f, 20f, out var width)) { _width.Value = width; Resize(); }
                GUILayout.EndVertical();
            }
            if (SettingsSection(3, "▶ 映すモニター", "▼ 映すモニター")) DrawMonitorSettings();
            if (SettingsSection(4, "▶ 上下左右のクリッピング", "▼ 上下左右のクリッピング")) DrawClipControls();
            if (SettingsSection(7, "▶ 画面プリセット", "▼ 画面プリセット")) DrawDisplayPresets();
            if (SettingsSection(5, "▶ 画質・更新頻度", "▼ 画質・更新頻度")) DrawCaptureSettings();
            if (SettingsSection(6, "▶ VR・マウス操作", "▼ VR・マウス操作"))
            {
                GUILayout.BeginVertical(GUI.skin.box);
                bool mouseEnabled = GUILayout.Toggle(_mouseEnabled.Value, "PCマウス操作を有効にする");
                if (mouseEnabled != _mouseEnabled.Value) _mouseEnabled.Value = mouseEnabled;
                GUILayout.Label("右手の照準が画面に当たり50cm以内の時だけ、線とカーソル追従が有効。");
                GUILayout.Label("A＝左クリック／B＝右クリック／スティック上下左右＝縦横スクロール。");
                GUILayout.Label("保持手のスティック上下＝サイズ／左右＝モニター切替。中央に戻して次へ。");
                GUILayout.EndVertical();
            }
        }
        private bool SettingsSection(int section, string closed, string opened)
        {
            bool expanded = _openSettingsSection == section;
            if (GUILayout.Button(expanded ? opened : closed, GUILayout.Width(390))) _pendingSettingsSection = expanded ? 0 : section;
            return expanded;
        }
        private void DrawMonitorSettings()
        {
            GUILayout.BeginVertical(GUI.skin.box);
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
                if (GUILayout.Button(i == _monitor ? _monitorSelectedLabels[i] : _monitorLabels[i], GUILayout.Width(390))) chosen = i;
            GUILayout.EndVertical();
            var e = Event.current;
            var area = GUILayoutUtility.GetLastRect();
            if (e.type == EventType.KeyDown && area.Contains(e.mousePosition) && GUIUtility.keyboardControl == 0 && _monitors.Length > 0 && (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.DownArrow))
            { chosen = (_monitor + _monitors.Length + (e.keyCode == KeyCode.UpArrow ? -1 : 1)) % _monitors.Length; e.Use(); }
            if (chosen != _monitor) _uiAction = () => SelectMonitor(chosen);
            GUILayout.EndVertical();
        }
        private void DrawCaptureSettings()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            bool cursor = GUILayout.Toggle(_cursor.Value, "マウスカーソルも映す");
            if (cursor != _cursor.Value) _cursor.Value = cursor;
            GUILayout.BeginHorizontal();
            GUILayout.Label("更新上限", GUILayout.Width(75));
            for (int i = 0; i < FpsChoices.Length; i++)
                if (GUILayout.Button(_fps.Value == FpsChoices[i] ? FpsSelected[i] : FpsLabels[i], GUILayout.Width(78))) _fps.Value = FpsChoices[i];
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("解像度上限", GUILayout.Width(85));
            int resolution = _resolution.Value;
            for (int i = 0; i < ResolutionChoices.Length; i++)
                if (GUILayout.Button(_resolution.Value == ResolutionChoices[i] ? ResolutionSelected[i] : ResolutionLabels[i], GUILayout.Width(95))) resolution = ResolutionChoices[i];
            GUILayout.EndHorizontal();
            if (resolution != _resolution.Value) { _resolution.Value = resolution; if (_root != null) StartCapture(); }
            GUILayout.EndVertical();
        }
        private void DrawCapturePause()
        {
            bool gui = GUI.enabled; GUI.enabled = gui && _root != null;
            if (GUILayout.Button(_paused ? "キャプチャを再開" : "キャプチャを一時停止", GUILayout.Width(195)))
            {
                if (_paused) { if (_capture == null) StartCapture(); else { _paused = false; _driver.enabled = true; _nextFrame = 0; _status = "デスクトップ表示中"; } }
                else { _paused = true; _driver.enabled = false; ReleaseDesktopMouse(); SyncDesktopMouse(); _status = "一時停止中（最後の画像を表示）"; }
                SyncDesktopMouse();
            }
            GUI.enabled = gui;
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
