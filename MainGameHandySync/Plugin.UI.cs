using UnityEngine;
using MainGameUiInputCapture;

namespace MainGameHandySync
{
    internal sealed partial class Plugin
    {
        private Rect _windowRect = new Rect(20, 120, 420, 410);
        private bool _windowVisible = true;
        private bool _handyInputRegistered;
        private int _handyHotControl;

        private void OnGUI()
        {
            if (!SessionGate.Allowed || _cfgShowUI == null || !_cfgShowUI.Value || !InHScene ||
                !_windowVisible || !_hasHandyFocus) { ClearHandyInput(); return; }

            if (_windowVisible)
            {
                int previousHotControl = GUIUtility.hotControl;
                _windowRect = GUILayout.Window(872341, _windowRect, DrawHandySyncWindow, "HandySync 設定");
                int hotControl = GUIUtility.hotControl;
                if (hotControl == 0) _handyHotControl = 0;
                else if (hotControl != previousHotControl && _windowRect.Contains(Event.current.mousePosition)) _handyHotControl = hotControl;
                else if (hotControl != _handyHotControl) _handyHotControl = 0;
                UiInputCaptureApi.SetInputBlockRects(GUID, new[] { _windowRect });
                UiInputCaptureApi.SetIdleCursorUnlock(GUID, true);
                UiInputCaptureApi.Sync(GUID, "window", _handyHotControl != 0);
                _handyInputRegistered = true;
            }
        }

        private void DrawHandySyncWindow(int id)
        {
            GUILayout.BeginVertical();

            // Status
            GUILayout.Label($"Pose: {_currentPoseKey}");
            GUILayout.Label($"Connection: {(string.IsNullOrWhiteSpace(GetConnectionKey()) ? "No Key" : "Key Set")}");

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            _cfgLinked.Value = GUILayout.Toggle(_cfgLinked.Value, "連動", GUILayout.Width(70));
            if (GUILayout.Button(RhythmDivisor == 1 ? "1" : "1/" + RhythmDivisor, GUILayout.Width(48))) CycleRhythmDivisor();
            GUILayout.EndHorizontal();
            _cfgBpmStroke.Value = GUILayout.Toggle(_cfgBpmStroke.Value, "BPMでストローク", GUILayout.ExpandWidth(false));
            if (_cfgBpmStroke.Value)
            {
                GUILayout.Label(UsingRhythmBpm ? "RhythmSyncの往復BPMに連動" : "RhythmSyncのBPM取得不可：従来動作");
                if (_lastRhythmTarget.HasValue)
                {
                    var target = _lastRhythmTarget.Value;
                    GUILayout.Label($"目標 {target.Bpm:0.##} BPM ／ 1往復 {target.Period:0.###} 秒");
                    if (target.Width <= 0) GUILayout.Label("幅ゼロ：停止");
                    else if (target.Limited) GUILayout.Label("速度上限・下限のため目標の往復時間に届きません");
                }
            }
            if (_handyClient.HampFailed) GUILayout.Label("Handy送信失敗：停止要求済み。設定変更または連動の入れ直しで再送");
            GUILayout.EndVertical();

            GUILayout.Space(8);

            // Phase Offset Slider (main request)
            float currentOffset = _cfgPhaseOffset != null ? _cfgPhaseOffset.Value : 0f;
            GUILayout.Label($"Phase Offset: {currentOffset:F3}");
            float newOffset = GUILayout.HorizontalSlider(currentOffset, 0f, 1f, GUILayout.Width(380));
            if (Mathf.Abs(newOffset - currentOffset) > 0.001f && _cfgPhaseOffset != null)
            {
                _cfgPhaseOffset.Value = newOffset;
            }

            GUILayout.Space(6);

            // Sync Button (main request)
            if (GUILayout.Button("SYNC NOW (Align to Current Animation)", GUILayout.Height(28)))
            {
                PerformSyncNow();
            }

            GUILayout.Space(10);

            // Log toggles
            GUILayout.Label("Logging");
            if (_cfgEnableNormalLog != null)
                _cfgEnableNormalLog.Value = GUILayout.Toggle(_cfgEnableNormalLog.Value, "Normal Log");
            if (_cfgEnableDetailedLog != null)
                _cfgEnableDetailedLog.Value = GUILayout.Toggle(_cfgEnableDetailedLog.Value, "Detailed Log");
            if (_cfgEnableErrorLog != null)
                _cfgEnableErrorLog.Value = GUILayout.Toggle(_cfgEnableErrorLog.Value, "Error Log");

            GUILayout.Space(8);

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private bool _hasHandyFocus = true;
        private void OnApplicationFocus(bool focus) { _hasHandyFocus = focus; if (!focus) ClearHandyInput(); }
        private void ClearHandyInput()
        {
            if (!_handyInputRegistered) return;
            _handyInputRegistered = false;
            UiInputCaptureApi.Sync(GUID, "window", false);
            UiInputCaptureApi.ClearInputBlockRects(GUID);
            UiInputCaptureApi.SetIdleCursorUnlock(GUID, false);
            if (_handyHotControl != 0 && GUIUtility.hotControl == _handyHotControl) GUIUtility.hotControl = 0;
            _handyHotControl = 0;
        }

        private void PerformSyncNow()
        {
            if (_animationReader == null || _hFlag == null || _hSceneProc == null) return;

            float currentAnim = _animationReader.GetAnimationPhase(_hSceneProc, _hFlag);
            // Set offset so that (currentAnim + offset) ≈ 0 (or desired position)
            float desired = 0.0f; // or 0.5f for middle, etc.
            float newOffset = (desired - currentAnim + 1f) % 1f;

            if (_cfgPhaseOffset != null)
                _cfgPhaseOffset.Value = newOffset;

            if (ShouldLogNormal())
                SimpleFileLogger.Info($"Sync Now executed. New offset = {newOffset:F3}");
        }
    }
}
