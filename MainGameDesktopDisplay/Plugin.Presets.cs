using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BepInEx.Configuration;
using UnityEngine;

namespace MainGameDesktopDisplay
{
    public sealed partial class Plugin
    {
        private ConfigEntry<string> _presetsEntry, _lastPreset;
        private List<DisplayPreset> _displayPresets;
        private string[] _presetLabels = Array.Empty<string>(), _presetSelectedLabels = Array.Empty<string>();
        private string _presetName;
        private int _presetToLoad;
        private bool _presetsReadable;
        private ConfigEntry<string>[] _monitorPresetEntries = Array.Empty<ConfigEntry<string>>();
        private string[] _monitorKeys = Array.Empty<string>(), _presetIds = Array.Empty<string>();
        private int[][] _monitorPresetChoices = Array.Empty<int[]>();
        private int _assignmentMonitor, _assignmentPreset;
        private void InitializeDisplayPresets()
        {
            _presetsEntry = Config.Bind("Presets", "Screens", "", "名前・モニター・上下左右の切り取り・画面幅の保存データ。");
            _lastPreset = Config.Bind("Presets", "LastName", "", "最後に保存・呼び出した画面プリセット名。");
            _presetName = _lastPreset.Value;
            try { _displayPresets = DisplayPreset.Decode(_presetsEntry.Value); _presetsReadable = true; }
            catch (Exception ex) { _displayPresets = new List<DisplayPreset>(); _presetsReadable = false; _status = "プリセット読込失敗（保存データは保持）：" + ex.Message; }
            CachePresetLabels();
            if (_monitors.Length > 0) CacheMonitorPresetBindings();
        }
        private void CachePresetLabels()
        {
            _presetLabels = new string[_displayPresets.Count]; _presetSelectedLabels = new string[_displayPresets.Count];
            _presetIds = new string[_displayPresets.Count];
            for (int i = 0; i < _displayPresets.Count; i++)
            {
                string label = _displayPresets[i].Name + " [" + _displayPresets[i].Monitor + "]";
                _presetLabels[i] = label; _presetSelectedLabels[i] = "● " + label;
                _presetIds[i] = Convert.ToBase64String(Encoding.UTF8.GetBytes(_displayPresets[i].Name));
            }
        }
        private void CacheMonitorPresetBindings()
        {
            _monitorKeys = new string[_monitors.Length];
            _monitorPresetEntries = new ConfigEntry<string>[_monitors.Length];
            _monitorPresetChoices = new int[_monitors.Length][];
            for (int m = 0; m < _monitors.Length; m++)
            {
                string key = _monitorKeys[m] = MonitorKey(_monitors[m].Device);
                _monitorPresetEntries[m] = Config.Bind("Preset." + key, "SelectedName", "", "このモニターで自動適用するプリセット名（Base64）。空欄は最後の手動切り取りを使用。");
                var choices = new List<int>();
                for (int p = 0; p < _displayPresets.Count; p++) if (_displayPresets[p].Monitor == key) choices.Add(p);
                _monitorPresetChoices[m] = choices.ToArray();
            }
        }
        private void ApplyAssignedMonitorPreset()
        {
            if (!_presetsReadable || _monitor >= _monitorPresetEntries.Length) return;
            string id = _monitorPresetEntries[_monitor].Value;
            if (string.IsNullOrEmpty(id)) return;
            foreach (int index in _monitorPresetChoices[_monitor])
            {
                if (_presetIds[index] != id) continue;
                var preset = _displayPresets[index];
                Array.Copy(preset.Edges, _clipDraft, 4);
                for (int i = 0; i < 4; i++) _clipDraft[i] = Mathf.Clamp(_clipDraft[i], 0, ClipMaximum(i));
                _clip = new DisplayClip(_monitors[_monitor].Rect, _clipDraft[0], _clipDraft[1], _clipDraft[2], _clipDraft[3]);
                ResetClipText();
                if (_width.Value != preset.Width) _width.Value = preset.Width;
                _widthText = preset.Width.ToString("0.###", CultureInfo.InvariantCulture);
                _lastPreset.Value = _presetName = preset.Name;
                return;
            }
            // Keep unavailable/invalid assignments and use this monitor's last manual crop.
        }
        private void AssignMonitorPreset(int monitor, int preset)
        {
            if (!CanAct || monitor < 0 || monitor >= _monitorPresetEntries.Length) return;
            if (preset < -1 || preset >= _displayPresets.Count || (preset >= 0 && _displayPresets[preset].Monitor != _monitorKeys[monitor])) return;
            string id = preset < 0 ? "" : _presetIds[preset];
            if (_monitorPresetEntries[monitor].Value == id) return;
            if (monitor == _monitor) CommitClipDraft();
            _monitorPresetEntries[monitor].Value = id;
            if (monitor == _monitor)
            {
                LoadMonitorClip(); ApplyAssignedMonitorPreset();
                RestartSelectedMonitorCapture();
            }
        }
        private void SaveDisplayPreset()
        {
            if (!CanAct || !_presetsReadable || _monitors.Length == 0 || string.IsNullOrWhiteSpace(_presetName)) return;
            CommitClipDraft();
            string name = _presetName.Trim();
            var preset = new DisplayPreset { Name = name, Monitor = MonitorKey(_monitors[_monitor].Device), Width = _width.Value };
            Array.Copy(_clipDraft, preset.Edges, 4);
            int index = _displayPresets.FindIndex(p => p.Name == name);
            if (index < 0) _displayPresets.Add(preset); else _displayPresets[index] = preset;
            _presetsEntry.Value = DisplayPreset.Encode(_displayPresets);
            _lastPreset.Value = _presetName = name;
            CachePresetLabels();
            CacheMonitorPresetBindings();
            _monitorPresetEntries[_monitor].Value = Convert.ToBase64String(Encoding.UTF8.GetBytes(name));
            _status = "画面プリセット保存：" + name;
        }
        private void LoadDisplayPreset(int index)
        {
            if (!CanAct || index < 0 || index >= _displayPresets.Count) return;
            var preset = _displayPresets[index];
            int monitor = Array.FindIndex(_monitors, item => MonitorKey(item.Device) == preset.Monitor);
            if (monitor < 0) { _status = "プリセットのモニターが未接続です：" + preset.Monitor; return; }
            _monitorPresetEntries[monitor].Value = _presetIds[index];
            CommitClipDraft();
            if (!ChooseMonitor(monitor)) { LoadMonitorClip(); ApplyAssignedMonitorPreset(); }
            _monitorDevice.Value = preset.Monitor;
            RestartSelectedMonitorCapture();
            _lastPreset.Value = _presetName = preset.Name;
            _status = "画面プリセット読込：" + preset.Name;
        }
        private void DrawDisplayPresets()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("モニター・切り取り範囲・画面幅をまとめて保存。同名で保存すると上書き。");
            _presetName = GUILayout.TextField(_presetName, GUILayout.Width(300));
            bool enabled = GUI.enabled; GUI.enabled = enabled && _presetsReadable && !string.IsNullOrWhiteSpace(_presetName) && _monitors.Length > 0;
            if (GUILayout.Button("現在の画面をこの名前で保存", GUILayout.Width(250))) _uiAction = SaveDisplayPreset;
            GUI.enabled = enabled;
            if (!_presetsReadable) GUILayout.Label("保存データを読み取れません。元データの上書きを止めています。");
            GUILayout.Label("モニターごとの自動適用（左右切替・次回呼び出しでも有効）");
            for (int m = 0; m < _monitorPresetEntries.Length; m++)
            {
                GUILayout.Label(_monitorLabels[m]);
                string selected = _monitorPresetEntries[m].Value;
                if (GUILayout.Button(string.IsNullOrEmpty(selected) ? "● 指定なし（最後の手動切り取り）" : "指定なし（最後の手動切り取り）", GUILayout.Width(390)))
                { _assignmentMonitor = m; _assignmentPreset = -1; _uiAction = AssignSelectedMonitorPreset; }
                foreach (int p in _monitorPresetChoices[m])
                    if (GUILayout.Button(selected == _presetIds[p] ? _presetSelectedLabels[p] : _presetLabels[p], GUILayout.Width(390)))
                    { _assignmentMonitor = m; _assignmentPreset = p; _uiAction = AssignSelectedMonitorPreset; }
            }
            GUILayout.Label("プリセットを今すぐ呼び出す");
            for (int i = 0; i < _displayPresets.Count; i++)
            {
                if (GUILayout.Button(_lastPreset.Value == _displayPresets[i].Name ? _presetSelectedLabels[i] : _presetLabels[i], GUILayout.Width(390)))
                { _presetToLoad = i; _uiAction = LoadSelectedDisplayPreset; }
            }
            GUILayout.EndVertical();
        }
        private void LoadSelectedDisplayPreset() => LoadDisplayPreset(_presetToLoad);
        private void AssignSelectedMonitorPreset() => AssignMonitorPreset(_assignmentMonitor, _assignmentPreset);
    }
}
