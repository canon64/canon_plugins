using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MainGameUnifiedUi;
using UnityEngine;
using SessionGate = CanonPlugins.Local.SessionGate;

namespace MainGameDollMode
{
    public partial class Plugin
    {
        private const string DollTabId = "doll-mode";
        private DollPresetLibrary _presets;
        private DollPresetValues _presetDraft;
        private string _presetsPath, _presetNewName = "", _presetStatus = "";
        private bool _presetsWritable, _draftEdited, _numberFieldsValid;
        private Vector2 _presetScroll;
        private readonly Dictionary<string, string> _presetNumberText = new Dictionary<string, string>();

        private void InitializePresetsTab()
        {
            _presetsPath = Path.Combine(_pluginDir, "presets.json");
            try
            {
                if (File.Exists(_presetsPath)) _presets = DollPresetLibrary.Load(_presetsPath);
                else { _presets = DollPresetLibrary.Create(_settings); _presets.Save(_presetsPath); }
                _presetsWritable = true;
            }
            catch (Exception ex)
            {
                _presets = DollPresetLibrary.Create(_settings);
                _presetStatus = "プリセットを読み書きできません。元ファイルを保持しています: " + ex.Message;
                LogWarn(_presetStatus);
            }
            RefreshPresetDraft();
            UnifiedUiApi.RegisterTabAfter(DollTabId, "ドールモード", DrawDollTab, "syringe", 83);
        }

        private void RefreshPresetDraft()
        {
            if (_presets == null || _settings == null) return;
            _presetDraft = DollPresetValues.Capture(_settings);
            _draftEdited = !_presetDraft.Matches(_presets.Find(_presets.SelectedId)?.Values);
            _presetNumberText.Clear();
        }

        private bool SavePresetChange(Action<DollPresetLibrary> change)
        {
            if (!_presetsWritable) { _presetStatus = "プリセットファイルを読み書きできないため保存できません。"; return false; }
            try
            {
                var next = _presets.Clone();
                change(next); next.Save(_presetsPath); _presets = next;
                return true;
            }
            catch (Exception ex) { _presetStatus = "保存できませんでした: " + ex.Message; return false; }
        }

        private void SelectPreset(string id)
        {
            var preset = _presets.Find(id);
            if (preset == null || !SavePresetChange(next => next.SelectedId = id)) return;
            ApplyPresetValues(preset.Values);
            _presetStatus = "「" + preset.Name + "」を適用しました。";
        }

        private void ApplyPresetValues(DollPresetValues values)
        {
            values.ApplyTo(_settings);
            SyncConfigFromSettings();
            SettingsStore.SaveToDefault(_pluginDir, _settings, LogWarn);
            SaveBepInExConfig();
            ResetDollModeEyeOverlayPngCache();
            if (SessionGate.Allowed)
            {
                ApplyDollModeState("preset-tab");
                ReleaseExternalDolls(false);
                if (_settings.Enabled && _settings.DollModeEnabled) ApplyHideHighlightToTargets("preset-tab", logAlways: false);
                SyncPresetExtras();
            }
            RefreshPresetDraft();
        }

        private void DrawDollTab()
        {
            if (!SessionGate.Allowed || _presetDraft == null) return;
            // UnifiedUi and its PlaybackBar host own the full content rectangle, pointer hold and idle cursor.
            _presetScroll = GUILayout.BeginScrollView(_presetScroll, GUILayout.Width(560));
            GUILayout.BeginVertical(GUILayout.Width(536));
            bool mode = GUILayout.Toggle(_settings.DollModeEnabled, "ドールモード ON");
            if (mode != _settings.DollModeEnabled) SetDollModeInternal(mode, "doll-tab");
            bool enabled = GUILayout.Toggle(_settings.Enabled, "プラグインを有効にする");
            if (enabled != _settings.Enabled)
            {
                _settings.Enabled = enabled;
                SyncConfigFromSettings(); SettingsStore.SaveToDefault(_pluginDir, _settings, LogWarn); SaveBepInExConfig();
                ApplyDollModeState("doll-tab-enable"); ReleaseExternalDolls(false);
            }
            GUILayout.Label("プリセット（選ぶと適用 / 基本は現在の設定を保存したもの）");
            foreach (var preset in _presets.Presets.ToArray())
            {
                if (GUILayout.Button((preset.Id == _presets.SelectedId ? "● " : "○ ") + preset.Name)) SelectPreset(preset.Id);
            }
            GUILayout.Label("編集中: " + (_presets.Find(_presets.SelectedId)?.Name ?? "基本") + (_draftEdited ? "（変更あり）" : ""));
            GUILayout.Label("変更はその場で反映します。次回用に残す場合は保存してください。");
            bool changedBefore = GUI.changed; GUI.changed = false; _numberFieldsValid = true;
            DrawPresetFields();
            if (GUI.changed)
            {
                _draftEdited = true;
                if (_numberFieldsValid) PreviewPresetDraft();
            }
            GUI.changed |= changedBefore;
            bool guiEnabled = GUI.enabled;
            GUI.enabled = guiEnabled && _numberFieldsValid;
            if (GUILayout.Button("現在の設定を保存")) { ApplyPresetValues(_presetDraft); _presetStatus = "現在の設定を保存しました。名前付きで残す場合はプリセットとして保存してください。"; }
            GUILayout.BeginHorizontal();
            GUILayout.Label("保存名", GUILayout.Width(55)); _presetNewName = GUILayout.TextField(_presetNewName, 64);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("新規プリセットとして保存して適用"))
            {
                var values = _presetDraft.Clone();
                if (SavePresetChange(next => next.SelectedId = next.Add(_presetNewName, values)))
                { ApplyPresetValues(values); _presetStatus = "新しいプリセットを保存しました。"; _presetNewName = ""; }
            }
            GUI.enabled = guiEnabled && _numberFieldsValid && _presets.SelectedId != DollPresetLibrary.BasicId;
            if (GUILayout.Button("選択中のプリセットを上書きして適用"))
            {
                var values = _presetDraft.Clone();
                if (SavePresetChange(next => next.Overwrite(next.SelectedId, values))) { ApplyPresetValues(values); _presetStatus = "上書き保存しました。"; }
            }
            if (GUILayout.Button("選択中のプリセットを削除して基本へ戻す"))
            {
                if (SavePresetChange(next => next.Delete(next.SelectedId))) { ApplyPresetValues(_presets.Find(DollPresetLibrary.BasicId).Values); _presetStatus = "削除して基本へ戻しました。"; }
            }
            GUI.enabled = guiEnabled;
            if (!_numberFieldsValid) GUILayout.Label("パターン番号を0以上の整数で入力してください。");
            if (!string.IsNullOrEmpty(_presetStatus)) GUILayout.Label(_presetStatus);
            if (!string.IsNullOrEmpty(_presetExtraStatus)) GUILayout.Label(_presetExtraStatus);
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
        }

        private void DrawPresetFields()
        {
            var p = _presetDraft;
            GUILayout.Label("表情");
            p.DollModeEyePattern = PresetInteger("目パターン番号", p.DollModeEyePattern);
            p.DollModeEyebrowPattern = PresetInteger("眉パターン番号", p.DollModeEyebrowPattern);
            p.DollModeMouthPattern = PresetInteger("口パターン番号", p.DollModeMouthPattern);
            p.DollModeEyesOpen = PresetSlider("目の開き", p.DollModeEyesOpen);
            p.DollModeMouthOpen = PresetSlider("口の開き", p.DollModeMouthOpen);
            p.DollModeCheekRate = PresetSlider("頬の赤み", p.DollModeCheekRate);
            p.DollModeTearsLevel = PresetLevelRadio("涙（標準）", p.DollModeTearsLevel, 3);
            p.DollModeFaceSweatLevel = PresetLevelRadio("顔の液体（標準）", p.DollModeFaceSweatLevel, 3);
            p.DollModeModEffectsEnabled = GUILayout.Toggle(p.DollModeModEffectsEnabled, "SkinEffectsの効果を指定する");
            if (p.DollModeModEffectsEnabled)
            {
                p.DollModeModTears = PresetLevelRadio("涙（MOD）", p.DollModeModTears, 3);
                p.DollModeModDrool = PresetLevelRadio("よだれ（MOD）", p.DollModeModDrool, 1);
                p.DollModeModSaliva = PresetLevelRadio("唾液（MOD）", p.DollModeModSaliva, 3);
                p.DollModeModSweat = PresetLevelRadio("汗（MOD）", p.DollModeModSweat, 3);
                p.DollModeModNose = PresetLevelRadio("鼻の液体", p.DollModeModNose, 2);
                p.DollModeModBukkake = PresetLevelRadio("bukkake（MOD）", p.DollModeModBukkake, 3);
            }
            p.DollModeSpiralOverlayEnabled = GUILayout.Toggle(p.DollModeSpiralOverlayEnabled, "螺旋瞳を目の上に重ねる（目パターンとは別）");
            if (p.DollModeSpiralOverlayEnabled)
            {
                p.DollModeSpiralOffsetX = PresetPositionSlider("螺旋 横位置", p.DollModeSpiralOffsetX);
                p.DollModeSpiralOffsetY = PresetPositionSlider("螺旋 縦位置", p.DollModeSpiralOffsetY);
                GUILayout.Label("縦は＋で上へ。横・縦とも0が元の位置です。");
                if (GUILayout.Button("螺旋の位置を中央に戻す", GUILayout.Width(220)))
                { p.DollModeSpiralOffsetX = p.DollModeSpiralOffsetY = 0; GUI.changed = true; }
            }
            if (GUILayout.Button("ハート瞳の上に螺旋を重ねる", GUILayout.Width(260)))
            { p.DollModeEyePattern = 25; p.DollModeSpiralOverlayEnabled = true; _presetNumberText.Remove("目パターン番号"); GUI.changed = true; }
            p.AllowFaceSiruDuringDollMode = GUILayout.Toggle(p.AllowFaceSiruDuringDollMode, "外部からの顔の液体・汗変更を許可");
            p.DollModeTransitionSeconds = PresetSlider("切替秒数", p.DollModeTransitionSeconds, 10f);
            GUILayout.Label("目の重ね画像（現在導入済みの画像を指定）");
            p.DollModeEyeOverlayEnabled = GUILayout.Toggle(p.DollModeEyeOverlayEnabled, "目の重ね画像を使用する");
            p.DollModeEyeOverlayPngPath = GUILayout.TextField(p.DollModeEyeOverlayPngPath ?? "");
            GUILayout.Label("体位の動き");
            p.DollModeMotionLockWEnabled = GUILayout.Toggle(p.DollModeMotionLockWEnabled, "Wループの動きを固定");
            p.DollModeMotionLockWValue = PresetSlider("W固定値", p.DollModeMotionLockWValue);
            p.DollModeMotionLockSEnabled = GUILayout.Toggle(p.DollModeMotionLockSEnabled, "Sループの動きを固定");
            p.DollModeMotionLockSValue = PresetSlider("S固定値", p.DollModeMotionLockSValue);
            GUILayout.Label("対象・他の表情制御");
            p.ApplyFemaleCharacters = GUILayout.Toggle(p.ApplyFemaleCharacters, "女性へ適用");
            p.ApplyMaleCharacters = GUILayout.Toggle(p.ApplyMaleCharacters, "男性へ適用");
            p.DisableAhegaoPlugins = GUILayout.Toggle(p.DisableAhegaoPlugins, "他のアヘ顔制御を抑止");
            GUILayout.Label("抑止対象キーワード");
            p.AhegaoPluginKeywordCsv = GUILayout.TextField(p.AhegaoPluginKeywordCsv ?? "");
        }
        private void PreviewPresetDraft()
        {
            if (!SessionGate.Allowed || _presetDraft == null) return;
            bool overlayChanged = _settings.DollModeEyeOverlayEnabled != _presetDraft.DollModeEyeOverlayEnabled
                || _settings.DollModeEyeOverlayPngPath != _presetDraft.DollModeEyeOverlayPngPath;
            _presetDraft.ApplyTo(_settings);
            if (overlayChanged) ResetDollModeEyeOverlayPngCache();
            ApplyDollModeState("preset-preview");
            ReleaseExternalDolls(false);
            if (_settings.Enabled && _settings.DollModeEnabled) ApplyHideHighlightToTargets("preset-preview", logAlways: false);
            SyncPresetExtras();
        }

        private int PresetLevelRadio(string label, int value, int maximum)
        {
            // At most six choices per row, so tears 0..10 also remain inside the tab width.
            for (int start = 0; start <= maximum; start += 6)
            {
                GUILayout.BeginHorizontal(); GUILayout.Label(start == 0 ? label : "", GUILayout.Width(135));
                for (int level = start; level <= maximum && level < start + 6; level++)
                {
                    string caption = (value == level ? "●" : "○") + (level == 0 ? "なし" : level.ToString(CultureInfo.InvariantCulture));
                    bool selected = GUILayout.Toggle(value == level, caption, GUI.skin.button, GUILayout.Width(52));
                    if (selected && value != level) { value = level; GUI.changed = true; }
                }
                GUILayout.FlexibleSpace(); GUILayout.EndHorizontal();
            }
            return value;
        }

        private float PresetPositionSlider(string label, float value)
        {
            GUILayout.BeginHorizontal(); GUILayout.Label(label, GUILayout.Width(105));
            value = GUILayout.HorizontalSlider(value, -.5f, .5f);
            GUILayout.Label(value.ToString("0.000", CultureInfo.InvariantCulture), GUILayout.Width(52));
            GUILayout.EndHorizontal(); return (float)Math.Round(value, 3, MidpointRounding.AwayFromZero);
        }

        private float PresetSlider(string label, float value, float maximum = 1f)
        {
            GUILayout.BeginHorizontal(); GUILayout.Label(label, GUILayout.Width(105));
            value = GUILayout.HorizontalSlider(value, 0, maximum);
            GUILayout.Label(value.ToString("0.00", CultureInfo.InvariantCulture), GUILayout.Width(42)); GUILayout.EndHorizontal();
            return (float)Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }
        private int PresetInteger(string label, int value, int maximum = int.MaxValue)
        {
            if (!_presetNumberText.TryGetValue(label, out string text)) text = value.ToString(CultureInfo.InvariantCulture);
            GUILayout.BeginHorizontal(); GUILayout.Label(label, GUILayout.Width(135));
            text = GUILayout.TextField(text, 10, GUILayout.Width(70)); _presetNumberText[label] = text;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed >= 0 && parsed <= maximum) return parsed;
            _numberFieldsValid = false; return value;
        }
    }
}
