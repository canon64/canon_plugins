using System.Diagnostics;
using UnityEngine;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private KsPlugTimeControl _specialKsPlugTime;
        private bool _specialTimeAvailable;
        private bool _specialTimeEnabled;
        private string _specialTimeReadError;
        private string _specialTimeWriteError;

        private void DrawKsPlugTimeOperation(float availableWidth)
        {
            if (_specialKsPlugTime == null)
            {
                using (var process = Process.GetCurrentProcess())
                    _specialKsPlugTime = new KsPlugTimeControl(process.ProcessName.EndsWith("_VR", System.StringComparison.OrdinalIgnoreCase));
            }
            // 表示中のLayoutで現在値を読むだけ。閉じている間の監視や値の強制は行わない。
            if (Event.current.type == EventType.Layout)
                _specialTimeAvailable = _specialKsPlugTime.TryRead(out _specialTimeEnabled, out _specialTimeReadError);

            GUILayout.Space(8f);
            GUILayout.Label("ksPlug：H中の時間経過（" + _specialKsPlugTime.Edition + "）");
            GUILayout.Label(_specialTimeAvailable ? "現在：" + (_specialTimeEnabled ? "ON" : "OFF") : "現在：接続できません");
            bool original = GUI.enabled;
            GUILayout.BeginHorizontal();
            try
            {
                GUI.enabled = original && _specialTimeAvailable && !_specialTimeEnabled;
                if (GUILayout.Button(new GUIContent("ONにする", "H中の時間帯の進行を有効にし、ksPlugの設定へ保存します。"), GUILayout.Width(130f)))
                    SetKsPlugTimeFromSpecialOperations(true);
                GUI.enabled = original && _specialTimeAvailable && _specialTimeEnabled;
                if (GUILayout.Button(new GUIContent("OFFにする", "H中の時間帯の進行を停止し、ksPlugの設定へ保存します。"), GUILayout.Width(130f)))
                    SetKsPlugTimeFromSpecialOperations(false);
            }
            finally { GUI.enabled = original; GUILayout.EndHorizontal(); }
            GUILayout.Label("朝・昼・夕方・夜などの時間帯を進める設定です。変更は即時反映し、次回起動にも引き継ぎます。", _specialOperationsHelpStyle, GUILayout.Width(availableWidth));
            GUILayout.Label("AstralModeの時間停止設定がONの場合、アストラル中はこの設定がONでも時間を停止します。", _specialOperationsHelpStyle, GUILayout.Width(availableWidth));
            // 成功・失敗でLayoutと入力イベント間のコントロール数を変えない。
            GUILayout.Label(_specialTimeAvailable ? "" : _specialTimeReadError ?? "ksPlugが読み込まれているか確認してください。", _specialOperationsHelpStyle, GUILayout.Width(availableWidth));
            GUILayout.Label(_specialTimeWriteError ?? "", _specialOperationsHelpStyle, GUILayout.Width(availableWidth));
        }

        private void SetKsPlugTimeFromSpecialOperations(bool enabled)
        {
            if (_specialKsPlugTime.TrySet(enabled, out string error))
            {
                _specialTimeEnabled = enabled;
                _specialTimeWriteError = null;
            }
            else
            {
                _specialTimeWriteError = "時間経過の変更・保存に失敗しました：" + error;
                LogWarn(_specialTimeWriteError);
            }
        }
    }
}
