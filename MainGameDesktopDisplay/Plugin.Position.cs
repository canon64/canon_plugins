using System;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;

namespace MainGameDesktopDisplay
{
    public sealed partial class Plugin
    {
        private ConfigEntry<float> _spawnX, _spawnY;
        private readonly float[] _spawn = new float[3], _spawnCentre = new float[3];
        private readonly string[] _spawnText = new string[3];
        private static readonly string[] SpawnLabels = { "左右（m）", "上下（m）", "呼出距離（m）" };
        private static readonly string[] SpawnControls = { "DesktopSpawnX", "DesktopSpawnY", "DesktopSpawnZ" };
        private Vector3 _spawnOrigin;
        private Quaternion _spawnRotation;
        private float _spawnSpan = 1;
        private bool _spawnDirty, _spawnFocused;

        private void InitializeSpawnPosition()
        {
            _spawnX = Config.Bind("Display", "SpawnRightMeters", 0f, "呼び出す位置の左右。視点基準、右が正（m）。");
            _spawnY = Config.Bind("Display", "SpawnUpMeters", 0f, "呼び出す位置の上下。視点基準、上が正（m）。");
            _spawn[0] = Finite(_spawnX.Value) ? _spawnX.Value : 0;
            _spawn[1] = Finite(_spawnY.Value) ? _spawnY.Value : 0;
            _spawn[2] = Finite(_distance.Value) ? _distance.Value : .5f;
            for (int i = 0; i < 3; i++) { _spawnCentre[i] = _spawn[i]; SetSpawnText(i); }
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private void SetSpawnText(int i) => _spawnText[i] = _spawn[i].ToString("0.###", CultureInfo.InvariantCulture);
        private void ApplySpawnPosition()
        {
            if (!CanAct || _root == null) return;
            SetPointerActive(false); ReleaseDisplayGrab(); HideGizmo();
            _root.transform.SetPositionAndRotation(_spawnOrigin + _spawnRotation * new Vector3(_spawn[0], _spawn[1], _spawn[2]), _spawnRotation);
        }
        private void CommitSpawnPosition()
        {
            if (!_spawnDirty) return;
            if (_spawnX.Value != _spawn[0]) _spawnX.Value = _spawn[0];
            if (_spawnY.Value != _spawn[1]) _spawnY.Value = _spawn[1];
            if (_distance.Value != _spawn[2]) _distance.Value = _spawn[2];
            for (int i = 0; i < 3; i++) { _spawnCentre[i] = _spawn[i]; SetSpawnText(i); }
            _spawnDirty = false;
        }
        private void DrawSpawnPosition()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("出現位置（呼び出した時の視点基準）");
            GUILayout.Label("右・上が＋。調整中は表示位置へ反映し、離した時・入力確定時に保存。");
            GUILayout.BeginHorizontal();
            GUILayout.Label("調整幅 ±", GUILayout.Width(85));
            if (GUILayout.Button(_spawnSpan == .25f ? "● 0.25m" : "0.25m", GUILayout.Width(85))) _spawnSpan = .25f;
            if (GUILayout.Button(_spawnSpan == 1 ? "● 1m" : "1m", GUILayout.Width(65))) _spawnSpan = 1;
            if (GUILayout.Button(_spawnSpan == 5 ? "● 5m" : "5m", GUILayout.Width(65))) _spawnSpan = 5;
            GUILayout.EndHorizontal();
            for (int i = 0; i < 3; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(SpawnLabels[i], GUILayout.Width(105));
                float min = i == 2 ? Math.Max(.2f, _spawnCentre[i] - _spawnSpan) : _spawnCentre[i] - _spawnSpan;
                float max = i == 2 ? Math.Min(20, _spawnCentre[i] + _spawnSpan) : _spawnCentre[i] + _spawnSpan;
                float value = GUILayout.HorizontalSlider(_spawn[i], min, max, GUILayout.Width(180));
                if (value != _spawn[i]) { _spawn[i] = value; SetSpawnText(i); _spawnDirty = true; ApplySpawnPosition(); }
                GUI.SetNextControlName(SpawnControls[i]);
                string text = GUILayout.TextField(_spawnText[i], GUILayout.Width(75));
                if (text != _spawnText[i])
                {
                    _spawnText[i] = text;
                    if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && Finite(value) && (i != 2 || (value >= .2f && value <= 20)))
                    { _spawn[i] = value; _spawnCentre[i] = value; _spawnDirty = true; ApplySpawnPosition(); }
                }
                GUILayout.EndHorizontal();
            }
            var e = Event.current;
            string control = GUI.GetNameOfFocusedControl();
            bool focused = control == SpawnControls[0] || control == SpawnControls[1] || control == SpawnControls[2];
            bool enter = focused && e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
            if ((e.rawType == EventType.MouseUp && e.button == 0 && GUIUtility.hotControl == 0) || enter || (_spawnFocused && !focused)) CommitSpawnPosition();
            _spawnFocused = focused;
            if (enter) e.Use();
            GUILayout.EndVertical();
        }
    }
}
