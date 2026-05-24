using System.Collections.Generic;
using UnityEngine;

namespace MainGameHScenePoseInjector
{
    public sealed partial class Plugin
    {
        private Rect _windowRect;
        private Vector2 _scrollPose;
        private Vector2 _scrollEb;
        private Vector2 _scrollEye;
        private Vector2 _scrollMouth;
        private const int WindowId = 0x504F5345; // 'POSE'
        private string _statusMessage = "";
        private float _statusUntil = 0f;

        private void DrawWindow()
        {
            EnsureRectFromSettings();
            _windowRect = GUI.Window(WindowId, _windowRect, DrawWindowContents, "ポーズ・表情 (HScene)");
            PersistRectToSettings();
        }

        private void EnsureRectFromSettings()
        {
            if (_windowRect.width <= 0f || _windowRect.height <= 0f)
            {
                _windowRect = new Rect(
                    _settings.WindowX,
                    _settings.WindowY,
                    _settings.WindowWidth,
                    _settings.WindowHeight);
            }
        }

        private void PersistRectToSettings()
        {
            bool changed = false;
            if (Mathf.Abs(_settings.WindowX - _windowRect.x) > 0.5f) { _settings.WindowX = _windowRect.x; changed = true; }
            if (Mathf.Abs(_settings.WindowY - _windowRect.y) > 0.5f) { _settings.WindowY = _windowRect.y; changed = true; }
            if (Mathf.Abs(_settings.WindowWidth - _windowRect.width) > 0.5f) { _settings.WindowWidth = _windowRect.width; changed = true; }
            if (Mathf.Abs(_settings.WindowHeight - _windowRect.height) > 0.5f) { _settings.WindowHeight = _windowRect.height; changed = true; }
            if (changed) SaveSettings();
        }

        private void DrawWindowContents(int id)
        {
            GUILayout.BeginVertical();

            // Status
            GUILayout.Label("HScene: " + (IsInHScene() ? "検出中" : "未検出"));
            int curId = CurrentAppliedPoseId();
            GUILayout.Label("ポーズ適用中: " + (IsAnyPoseApplied() ? ("ID=" + curId) : "なし")
                + "  /  最後の表情: 眉=" + _settings.LastEyebrowPtn
                + " 目=" + _settings.LastEyesPtn
                + " 口=" + _settings.LastMouthPtn);

            // Toolbar
            GUILayout.BeginHorizontal();
            bool prevEnabled = GUI.enabled;
            GUI.enabled = prevEnabled && IsAnyPoseApplied();
            if (GUILayout.Button("ポーズ解除", GUILayout.Height(26f), GUILayout.Width(100f)))
            {
                RestoreAllSnapshots("ui-cancel");
                SetStatus("ポーズ解除しました");
            }
            GUI.enabled = prevEnabled;
            if (GUILayout.Button("一覧再読込", GUILayout.Height(26f), GUILayout.Width(100f)))
            {
                _poseList = null;
                _poseListLoadAttempted = false;
                _eyebrowList = _eyesList = _mouthList = null;
                _patternListLoadAttempted = false;
                SetStatus("一覧をクリア");
            }
            DrawToggleRow("複数女キャラ全員に適用", ref _settings.ApplyToAllFemales);
            DrawToggleRow("Animator差替で自動解除", ref _settings.AutoRestoreOnControllerChange);
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);

            // 4-column layout
            float listHeight = Mathf.Max(120f, _windowRect.height - 130f);
            float colWidth = Mathf.Max(120f, (_windowRect.width - 60f) / 4f);

            GUILayout.BeginHorizontal();

            DrawPoseColumn(curId, colWidth, listHeight);
            DrawPatternColumn("眉", GetEyebrowList(), ref _scrollEb,
                _settings.LastEyebrowPtn, id => ApplyExpressionPick(id, null, null), colWidth, listHeight);
            DrawPatternColumn("目", GetEyesList(), ref _scrollEye,
                _settings.LastEyesPtn, id => ApplyExpressionPick(null, id, null), colWidth, listHeight);
            DrawPatternColumn("口", GetMouthList(), ref _scrollMouth,
                _settings.LastMouthPtn, id => ApplyExpressionPick(null, null, id), colWidth, listHeight);

            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_statusMessage) && Time.unscaledTime < _statusUntil)
                GUILayout.Label(_statusMessage);

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }

        private void DrawPoseColumn(int curPoseId, float width, float height)
        {
            GUILayout.BeginVertical(GUILayout.Width(width));
            GUILayout.Label("【ポーズ】");

            List<PoseEntry> poses = GetPoseList();
            if (poses.Count == 0)
            {
                GUILayout.Label("(空)");
            }
            else
            {
                _scrollPose = GUILayout.BeginScrollView(_scrollPose, false, true, GUILayout.Height(height));
                for (int i = 0; i < poses.Count; i++)
                {
                    PoseEntry p = poses[i];
                    Color prev = GUI.backgroundColor;
                    if (p.Id == curPoseId && IsAnyPoseApplied())
                        GUI.backgroundColor = Color.cyan;
                    string label = p.Name + " (" + p.Id + ")";
                    if (GUILayout.Button(label, GUILayout.Height(22f)))
                    {
                        bool ok = ApplyPose(p);
                        SetStatus(ok ? ("ポーズ適用: " + p.Name) : ("失敗: " + p.Name));
                    }
                    GUI.backgroundColor = prev;
                }
                GUILayout.EndScrollView();
            }

            GUILayout.EndVertical();
        }

        private void DrawPatternColumn(string title, List<PatternEntry> list, ref Vector2 scroll,
            int currentSelectedId, System.Action<int> onPick, float width, float height)
        {
            GUILayout.BeginVertical(GUILayout.Width(width));
            GUILayout.Label("【" + title + "】");

            if (list == null || list.Count == 0)
            {
                GUILayout.Label("(空)");
            }
            else
            {
                scroll = GUILayout.BeginScrollView(scroll, false, true, GUILayout.Height(height));
                for (int i = 0; i < list.Count; i++)
                {
                    PatternEntry p = list[i];
                    Color prev = GUI.backgroundColor;
                    if (p.Id == currentSelectedId)
                        GUI.backgroundColor = Color.yellow;
                    string label = p.Name + " (" + p.Id + ")";
                    if (GUILayout.Button(label, GUILayout.Height(22f)))
                    {
                        onPick(p.Id);
                    }
                    GUI.backgroundColor = prev;
                }
                GUILayout.EndScrollView();
            }

            GUILayout.EndVertical();
        }

        private void ApplyExpressionPick(int? eb, int? eye, int? mouth)
        {
            bool ok = ApplyExpression(eb, eye, mouth);
            if (ok)
            {
                if (eb.HasValue) _settings.LastEyebrowPtn = eb.Value;
                if (eye.HasValue) _settings.LastEyesPtn = eye.Value;
                if (mouth.HasValue) _settings.LastMouthPtn = mouth.Value;
                SaveSettings();
                SetStatus("表情適用: 眉=" + eb + " 目=" + eye + " 口=" + mouth);
            }
            else
            {
                SetStatus("表情適用失敗");
            }
        }

        private void DrawToggleRow(string label, ref bool value)
        {
            bool next = GUILayout.Toggle(value, label);
            if (next != value)
            {
                value = next;
                SaveSettings();
            }
        }

        private void SetStatus(string msg)
        {
            _statusMessage = msg;
            _statusUntil = Time.unscaledTime + 3f;
            LogInfo("status: " + msg);
        }
    }
}
