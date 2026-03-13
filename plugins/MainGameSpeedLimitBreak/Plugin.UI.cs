using BepInEx.Configuration;
using System;
using System.Globalization;
using UnityEngine;

namespace MainGameSpeedLimitBreak
{
    public partial class Plugin
    {
        private void OnGUI()
        {
            if (_showBpmUi)
            {
                _windowRect = GUI.Window(_windowId, _windowRect, DrawBpmWindow, "SpeedLimitBreak BPM");
            }

            if (!string.IsNullOrEmpty(_uiNotice) && Time.unscaledTime <= _uiNoticeUntil)
            {
                var style = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 18
                };

                var rect = new Rect((Screen.width - 360f) * 0.5f, 24f, 360f, 40f);
                GUI.Box(rect, _uiNotice, style);
            }
        }

        private void DrawBpmWindow(int id)
        {
            var s = Settings;

            GUILayout.BeginVertical();
            if (s == null)
            {
                GUILayout.Label("settings not loaded");
                GUILayout.EndVertical();
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
                return;
            }

            float scrollHeight = Mathf.Max(160f, _windowRect.height - 52f);
            _windowScroll = GUILayout.BeginScrollView(_windowScroll, false, true, GUILayout.Height(scrollHeight));

            GUILayout.Label($"現在 TargetSpeed(最小/最大): {s.TargetMinSpeed:0.###} / {s.TargetMaxSpeed:0.###}");
            GUILayout.Label($"現在反映BPM(最小/最大): {s.AppliedBpmMin:0.##} / {s.AppliedBpmMax:0.##}");
            GUILayout.Label($"基準BPM(速度1): {s.BpmReferenceAtSourceMin:0.##}");
            GUILayout.Label($"基準BPM(速度3): {s.BpmReferenceAtSpeed3:0.##}");
            GUILayout.Label($"Speed mode: {(s.ForceVanillaSpeed ? "VANILLA (locked)" : "CUSTOM (applied)")}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset to vanilla speed", GUILayout.Width(170f)))
            {
                RestoreVanillaSpeedBaseline();
            }

            if (GUILayout.Button(s.ForceVanillaSpeed ? "Resume custom speed" : "Lock vanilla speed", GUILayout.Width(170f)))
            {
                ToggleForceVanillaSpeed();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("較正BPM (速度1/速度3)");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Min", GUILayout.Width(38f));
            _bpmMinInput = GUILayout.TextField(_bpmMinInput, GUILayout.Width(90f));
            GUILayout.Label("Max", GUILayout.Width(38f));
            _bpmMaxInput = GUILayout.TextField(_bpmMaxInput, GUILayout.Width(90f));
            if (GUILayout.Button("基準反映", GUILayout.Width(80f)))
            {
                ApplyCalibrationFromInputs();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("アニメBPM計算");
            GUILayout.BeginHorizontal();
            bool measuring = _bpmMeasure.Running;
            GUI.enabled = !measuring;
            if (GUILayout.Button("最小値を計算", GUILayout.Width(110f)))
            {
                StartBpmMeasure(BpmMeasureTarget.Min);
            }
            if (GUILayout.Button("最大値を計算", GUILayout.Width(110f)))
            {
                StartBpmMeasure(BpmMeasureTarget.Max);
            }
            GUI.enabled = true;
            if (measuring)
            {
                GUILayout.Label($"計算中: {GetMeasureTargetLabel(_bpmMeasure.Target)}", GUILayout.Width(120f));
                if (GUILayout.Button("中止", GUILayout.Width(60f)))
                {
                    StopBpmMeasure("manual stop", keepResult: true);
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Label($"計算結果 Min: {FormatMeasuredBpm(_measuredMinBpm)}   Max: {FormatMeasuredBpm(_measuredMaxBpm)}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("結果→最小値へ反映", GUILayout.Width(150f)))
            {
                ApplyMeasuredToMin();
            }
            if (GUILayout.Button("結果→最大値へ反映", GUILayout.Width(150f)))
            {
                ApplyMeasuredToMax();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("反映BPM (速度レンジ適用)");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Max", GUILayout.Width(38f));
            _appliedBpmMaxInput = GUILayout.TextField(_appliedBpmMaxInput, GUILayout.Width(90f));
            if (GUILayout.Button("最大へ適用", GUILayout.Width(90f)))
            {
                ApplyMaxBpmFromInput();
            }
            GUILayout.Label("※最小=1/4自動", GUILayout.Width(100f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Min", GUILayout.Width(38f));
            _appliedBpmMinInput = GUILayout.TextField(_appliedBpmMinInput, GUILayout.Width(90f));
            if (GUILayout.Button("最小へ適用", GUILayout.Width(90f)))
            {
                ApplyMinBpmFromInput();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("保存名", GUILayout.Width(50f));
            _presetNameInput = GUILayout.TextField(_presetNameInput, GUILayout.Width(220f));
            if (GUILayout.Button("保存", GUILayout.Width(70f)))
            {
                SavePresetFromInput();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("現在値から保存名を再生成", GUILayout.Width(200f)))
            {
                _presetNameInput = BuildNextDefaultPresetName();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("保存済みプリセット");

            int applyIndex = -1;
            int applyBaseIndex = -1;
            int deleteIndex = -1;
            var sortedIndices = BuildSortedPresetIndices();

            string lastFolder = null;
            for (int oi = 0; oi < sortedIndices.Count; oi++)
            {
                int i = sortedIndices[oi];
                var p = s.BpmPresets[i];

                string folder = GetPresetFolder(p);
                if (!string.Equals(lastFolder, folder, StringComparison.OrdinalIgnoreCase))
                {
                    GUILayout.Label($"[{folder}]");
                    lastFolder = folder;
                }

                GUILayout.BeginHorizontal("box");
                if (GUILayout.Button("呼び出し", GUILayout.Width(70f)))
                {
                    applyIndex = i;
                }
                if (GUILayout.Button("Base", GUILayout.Width(50f)))
                {
                    applyBaseIndex = i;
                }
                if (GUILayout.Button("削除", GUILayout.Width(50f)))
                {
                    deleteIndex = i;
                }
                string displayName = string.IsNullOrWhiteSpace(p.Name)
                    ? BuildPresetDisplayName(p.AnimationName, p.AppliedBpmMax, p.AppliedBpmMin)
                    : p.Name;

                GUILayout.BeginVertical();
                GUILayout.Label(displayName);
                GUILayout.Label($"applied=[{p.AppliedBpmMax:0.##}/{p.AppliedBpmMin:0.##}]  base=[{p.BaseBpmMin:0.##}/{p.BaseBpmMax:0.##}]");
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }

            if (applyIndex >= 0) ApplyPreset(applyIndex);
            if (applyBaseIndex >= 0) ApplyPresetBaseOnly(applyBaseIndex);
            if (deleteIndex >= 0) DeletePreset(deleteIndex);

            GUILayout.Space(4f);
            string uiToggle = GetConfigHotkey(_cfgUiToggleHotkey, "LeftAlt+S");
            GUILayout.Label($"操作: {uiToggle} で表示切替 / Enter で最大へ適用");

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private void SyncUiFromSettings()
        {
            var s = Settings;
            if (s == null)
                return;

            EnsureAppliedBpmRangeInitialized(s);
            _appliedBpmMinInput = s.AppliedBpmMin.ToString("0.##", CultureInfo.InvariantCulture);
            _appliedBpmMaxInput = s.AppliedBpmMax.ToString("0.##", CultureInfo.InvariantCulture);
            _bpmMinInput = s.BpmReferenceAtSourceMin.ToString("0.##", CultureInfo.InvariantCulture);
            _bpmMaxInput = s.BpmReferenceAtSpeed3.ToString("0.##", CultureInfo.InvariantCulture);
            _presetNameInput = BuildNextDefaultPresetName();
            _lastAppliedPresetOrderIndex = -1;
        }

        private void SetupConfigManagerBindings()
        {
            var s = Settings ?? new PluginSettings();

            _cfgEnableBpmSpeedRemap = Config.Bind(
                "Behavior",
                "00 Enable BPM Remap",
                s.EnableBpmSpeedRemap.GetValueOrDefault(true),
                "Enable remapping animator speed/speedBody from configured BPM-applied range.");

            _cfgForceVanillaSpeed = Config.Bind(
                "Behavior",
                "01 Force Vanilla Speed",
                s.ForceVanillaSpeed,
                "Disable all speed/gauge intervention and keep vanilla in-game speed behavior.");

            _cfgEnablePerFrameTrace = Config.Bind(
                "Debug",
                "00 Enable Per-Frame Trace",
                s.EnablePerFrameTrace,
                "Enable per-frame patch/timeline diagnostic logs. Keep OFF in normal use.");

            _cfgBpmReferenceAtSourceMin = Config.Bind(
                "Calibration",
                "10 BPM Ref At Source Min",
                s.BpmReferenceAtSourceMin,
                "Measured BPM at SourceMinSpeed. Set 0 to disable two-point calibration.");

            _cfgBpmReferenceAtSourceMax = Config.Bind(
                "Calibration",
                "11 BPM Ref At Source Max",
                s.BpmReferenceAtSpeed3,
                "Measured BPM at SourceMaxSpeed (gauge max).");

            _cfgBpmReferenceAtSourceMin.SettingChanged += OnConfigCalibrationChanged;
            _cfgBpmReferenceAtSourceMax.SettingChanged += OnConfigCalibrationChanged;
            _cfgEnableBpmSpeedRemap.SettingChanged += OnConfigCalibrationChanged;
            _cfgForceVanillaSpeed.SettingChanged += OnConfigCalibrationChanged;
            _cfgEnablePerFrameTrace.SettingChanged += OnConfigCalibrationChanged;

            _cfgEnableVideoTimeSpeedCues = Config.Bind(
                "Video Timeline",
                "00 Enable Speed Timeline",
                s.EnableVideoTimeSpeedCues,
                "Enable applying saved speed presets from external video timeline cue file.");

            _cfgVideoTimeCueFilePath = Config.Bind(
                "Video Timeline",
                "01 Cue File Path",
                "SpeedTimeline.json",
                "Timeline cue JSON path. Relative path is resolved from plugin folder.");

            _cfgEnableVideoTimeSpeedCues.SettingChanged += OnConfigCalibrationChanged;
            _cfgVideoTimeCueFilePath.SettingChanged += OnConfigCalibrationChanged;

            _cfgUiToggleHotkey = Config.Bind(
                "Hotkeys",
                "00 Toggle UI",
                "LeftAlt+S",
                "Toggle BPM UI on/off.");

            _cfgPresetPrevHotkey = Config.Bind(
                "Hotkeys",
                "01 Previous Preset",
                "",
                "Apply previous preset in sorted list order.");

            _cfgPresetNextHotkey = Config.Bind(
                "Hotkeys",
                "02 Next Preset",
                "",
                "Apply next preset in sorted list order.");

            _cfgPresetSlotHotkeys = new ConfigEntry<string>[10];
            for (int i = 0; i < _cfgPresetSlotHotkeys.Length; i++)
            {
                int slot = i + 1;
                _cfgPresetSlotHotkeys[i] = Config.Bind(
                    "Hotkeys",
                    $"{10 + slot:00} Preset Slot {slot}",
                    "",
                    $"Apply preset slot {slot} from current sorted preset list.");
            }

            ApplyConfigManagerOverrides(saveIfChanged: false, reason: "config init");
        }

        private void OnConfigCalibrationChanged(object sender, EventArgs e)
        {
            if (_suppressConfigSync)
                return;

            ApplyConfigManagerOverrides(saveIfChanged: true, reason: "config manager changed");
            SyncUiFromSettings();
        }

        private void ApplyConfigManagerOverrides(bool saveIfChanged, string reason)
        {
            var s = Settings;
            if (s == null ||
                _cfgEnableBpmSpeedRemap == null ||
                _cfgForceVanillaSpeed == null ||
                _cfgEnablePerFrameTrace == null ||
                _cfgBpmReferenceAtSourceMin == null ||
                _cfgBpmReferenceAtSourceMax == null ||
                _cfgEnableVideoTimeSpeedCues == null ||
                _cfgVideoTimeCueFilePath == null)
                return;

            float minRef = Mathf.Max(0f, _cfgBpmReferenceAtSourceMin.Value);
            float maxRef = Mathf.Max(1f, _cfgBpmReferenceAtSourceMax.Value);
            bool calibrationChanged = false;

            bool changed = false;
            if (!Mathf.Approximately(s.BpmReferenceAtSourceMin, minRef))
            {
                s.BpmReferenceAtSourceMin = minRef;
                changed = true;
                calibrationChanged = true;
            }

            if (!Mathf.Approximately(s.BpmReferenceAtSpeed3, maxRef))
            {
                s.BpmReferenceAtSpeed3 = maxRef;
                changed = true;
                calibrationChanged = true;
            }

            bool bpmRemapEnabled = _cfgEnableBpmSpeedRemap.Value;
            if (s.EnableBpmSpeedRemap.GetValueOrDefault(true) != bpmRemapEnabled)
            {
                s.EnableBpmSpeedRemap = bpmRemapEnabled;
                changed = true;
                LogInfo($"bpm remap enable changed: {bpmRemapEnabled}");
            }

            bool forceVanillaSpeed = _cfgForceVanillaSpeed.Value;
            if (s.ForceVanillaSpeed != forceVanillaSpeed)
            {
                s.ForceVanillaSpeed = forceVanillaSpeed;
                changed = true;
                if (forceVanillaSpeed)
                {
                    ResetVideoCueRuntime(clearTriggerOnce: true);
                }
                LogInfo($"force vanilla speed changed: {forceVanillaSpeed}");
            }

            bool perFrameTraceEnabled = _cfgEnablePerFrameTrace.Value;
            if (s.EnablePerFrameTrace != perFrameTraceEnabled)
            {
                s.EnablePerFrameTrace = perFrameTraceEnabled;
                changed = true;
                LogInfo($"per-frame trace changed: {perFrameTraceEnabled}");
            }

            if (s.BpmReferenceAtSourceMin > 0f && s.BpmReferenceAtSpeed3 <= s.BpmReferenceAtSourceMin)
            {
                s.BpmReferenceAtSpeed3 = s.BpmReferenceAtSourceMin + 1f;
                changed = true;
                calibrationChanged = true;

                _suppressConfigSync = true;
                _cfgBpmReferenceAtSourceMax.Value = s.BpmReferenceAtSpeed3;
                _suppressConfigSync = false;
            }

            bool timelineEnabled = _cfgEnableVideoTimeSpeedCues.Value;
            if (s.EnableVideoTimeSpeedCues != timelineEnabled)
            {
                s.EnableVideoTimeSpeedCues = timelineEnabled;
                changed = true;
                if (!timelineEnabled)
                {
                    ResetVideoCueRuntime(clearTriggerOnce: true);
                }
                LogInfo($"video timeline enable changed: {timelineEnabled}");
            }

            string rawCuePath = (_cfgVideoTimeCueFilePath.Value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(rawCuePath))
            {
                rawCuePath = "SpeedTimeline.json";
                _suppressConfigSync = true;
                _cfgVideoTimeCueFilePath.Value = rawCuePath;
                _suppressConfigSync = false;
            }

            string resolvedCuePath = ResolveVideoCueFilePath(rawCuePath);
            bool firstInit = string.IsNullOrWhiteSpace(_videoCueFileResolvedPath);
            bool cuePathChanged = !string.Equals(_videoCueFileResolvedPath, resolvedCuePath, StringComparison.OrdinalIgnoreCase);
            if (cuePathChanged)
            {
                _videoCueFileResolvedPath = resolvedCuePath;
                ReloadVideoCueTimelineFromConfiguredPath(migrateLegacyIfMissing: firstInit);
            }

            if (calibrationChanged)
            {
                EnsureAppliedBpmRangeInitialized(s);
                ApplyAppliedRangeToTargetSpeeds(s, s.AppliedBpmMin, s.AppliedBpmMax);
                LogInfo($"calibration updated: bpmRefMin={s.BpmReferenceAtSourceMin:0.##}, bpmRefMax={s.BpmReferenceAtSpeed3:0.##}");
            }

            if (saveIfChanged)
            {
                if (changed)
                {
                    SaveSettings(reason);
                }

                if (cuePathChanged)
                {
                    LogInfo("video timeline cue file path changed: " + _videoCueFileResolvedPath);
                }
            }
        }

        private void ApplyMaxBpmFromInput()
        {
            if (!TryParsePositiveFloat(_appliedBpmMaxInput, out float bpmMax))
            {
                ShowUiNotice("最大BPM入力が不正です");
                return;
            }

            float bpmMin = Mathf.Max(0f, bpmMax * 0.25f);
            _appliedBpmMinInput = bpmMin.ToString("0.##", CultureInfo.InvariantCulture);
            ApplyAppliedBpmRange(bpmMin, bpmMax, "manual input max", $"反映BPMを適用: {bpmMax:0.##}-{bpmMin:0.##}");
        }

        private void ApplyMinBpmFromInput()
        {
            if (!TryParseNonNegativeFloat(_appliedBpmMinInput, out float bpmMin))
            {
                ShowUiNotice("最小BPM入力が不正です");
                return;
            }

            var s = Settings;
            if (s == null)
                return;

            EnsureAppliedBpmRangeInitialized(s);
            if (bpmMin >= s.AppliedBpmMax)
            {
                ShowUiNotice("最小BPMは最大BPMより小さくしてください");
                return;
            }

            ApplyAppliedBpmRange(bpmMin, s.AppliedBpmMax, "manual input min", $"反映最小BPMを適用: {bpmMin:0.##}");
        }

        private void ShowUiNotice(string text)
        {
            _uiNotice = text;
            _uiNoticeUntil = Time.unscaledTime + 2.0f;
        }
    }
}
