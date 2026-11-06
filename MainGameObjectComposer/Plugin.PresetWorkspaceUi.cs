using System;
using System.Collections.Generic;
using UnityEngine;

namespace MainGameObjectComposer
{
    public sealed partial class Plugin
    {
        private GUIStyle _composerSectionStyle;
        private GUIStyle _composerBoldLabel;
        private GUIStyle ComposerBoldLabel => _composerBoldLabel ?? (_composerBoldLabel = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = true });

        private void DrawComposerSection(string key, string title, Action draw)
        {
            if (_settings.EditorExpandedSections == null) _settings.EditorExpandedSections = new List<string>();
            bool open = _settings.EditorExpandedSections.Contains(key);
            if (_composerSectionStyle == null)
                _composerSectionStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, wordWrap = true };
            if (GUILayout.Button((open ? "▼ " : "▶ ") + title, _composerSectionStyle, GUILayout.Height(28f)))
                QueuePresetAction(() => {
                    if (open) _settings.EditorExpandedSections.Remove(key);
                    else _settings.EditorExpandedSections.Add(key);
                    SaveSettings();
                });
            if (!open) return;
            GUILayout.BeginVertical("box");
            draw();
            GUILayout.EndVertical();
        }

        private void RunPresetObjectAction(Action action)
        {
            if (_presetDraft == null) return;
            var before = BuildIdMap(CloneObjectList(_objects));
            action();
            CaptureDraftObjectChanges(before);
            RefreshSelectedEditorFields();
        }

        private void BeginNewPresetEditor()
        {
            DeactivatePreset();
            _presetSource = null;
            _presetDraft = new PresetEntry { key = BuildCurrentPositionKey(), displayName = "", autoLoadEligible = false };
            UpdatePresetConditionMetadata(_presetDraft, _presetDraft.key);
            _presetEditorState.Open();
            _presetDraftDirty = _presetSaveFailed = false;
            _presetSaveAsNew = true;
            _newPresetName = "";
            _presetEditorMessage = "新規プリセット：名前を付け、使用オブジェクトから追加してください。";
            ApplyLayoutData(new ObjectLayoutFile(), true);
            _activePreset = _presetDraft;
            RebuildActivePresetCache();
            if (_settings.EditorExpandedSections == null) _settings.EditorExpandedSections = new List<string>();
            if (!_settings.EditorExpandedSections.Contains("preset-save")) _settings.EditorExpandedSections.Add("preset-save");
        }

        private void DrawDraftControls(Rect left, Rect right)
        {
            bool input = Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp
                || Event.current.type == EventType.MouseDrag || Event.current.type == EventType.KeyDown;
            var before = input ? BuildIdMap(CloneObjectList(_objects)) : null;
            bool wasMovementEdit = _movementEditPreset != null;
            string presetBefore = input ? JsonUtility.ToJson(_presetDraft) : null;
            GUILayout.BeginArea(new Rect(left.x + 6, left.y + 6, left.width - 12, left.height - 12));
            _settings.EditorLeftScroll = GUILayout.BeginScrollView(_settings.EditorLeftScroll);
            if (GUILayout.Button("新規プリセット", GUILayout.ExpandWidth(false), GUILayout.Height(28)))
            {
                Action create = () => QueuePresetAction(BeginNewPresetEditor);
                if (RequestPresetLeave(create)) create();
            }
            DrawComposerSection("preset-save", "選択中のプリセット・保存", () => {
                GUILayout.Label("プリセット名");
                _presetDraft.displayName = GUILayout.TextField(_presetDraft.displayName ?? "");
                DrawPresetSaveControls();
            });
            DrawComposerSection("preset-conditions", "配置・自動切替・適用レーン", () => {
                GUILayout.Label(BuildPresetConditionText(_presetDraft));
                bool split = GUILayout.Toggle(_presetDraft.useLoopSplit, "L1/L2の配置を分ける");
                if (split != _presetDraft.useLoopSplit) QueuePresetAction(() => {
                    _presetDraft.useLoopSplit = split; MarkPresetDraftChanged();
                    if (!split) SetPresetEditSlot(1);
                    RebuildActivePresetCache();
                });
                _presetDraft.autoLoadEligible = GUILayout.Toggle(_presetDraft.autoLoadEligible, "体位変更時の自動切替候補にする");
                bool automatic = GUILayout.Toggle(_settings.AutoSwitchPresetOnPositionChange, "体位／モーション変更で自動切替");
                if (automatic != _settings.AutoSwitchPresetOnPositionChange)
                { _settings.AutoSwitchPresetOnPositionChange = automatic; _lastAutoSwitchKey = null; SaveSettings(); }
                GUILayout.Label("この画面の編集中は自動切替を保留します。");
                GUILayout.Label("適用するレーン");
                GUILayout.BeginHorizontal();
                _presetDraft.applyToW = GUILayout.Toggle(_presetDraft.applyToW, "W");
                _presetDraft.applyToS = GUILayout.Toggle(_presetDraft.applyToS, "S");
                _presetDraft.applyToOther = GUILayout.Toggle(_presetDraft.applyToOther, "その他");
                GUILayout.EndHorizontal();
            });
            DrawComposerSection("preset-objects", "使用オブジェクト", () => {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("円柱を追加", GUILayout.ExpandWidth(false))) QueuePresetAction(() => RunPresetObjectAction(CreateCylinder));
                if (GUILayout.Button("回転を追加", GUILayout.ExpandWidth(false))) QueuePresetAction(() => RunPresetObjectAction(CreateRotationObject));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("ピストンを追加", GUILayout.ExpandWidth(false))) QueuePresetAction(() => RunPresetObjectAction(CreatePistonObject));
                if (GUILayout.Button("アングルを追加", GUILayout.ExpandWidth(false))) QueuePresetAction(() => RunPresetObjectAction(CreateAngleObject));
                GUILayout.EndHorizontal();
                if (_objects.Count == 0) GUILayout.Label("オブジェクトはありません。上のボタンから追加してください。");
                DrawObjectTree();
            });
            DrawMotionIntensityPresetEditor(_presetDraft);
            DrawComposerSection("preset-movement", "Grokタグによる腰の連動", () => DrawMovementEditor(_presetDraft));
            DrawComposerSection("preset-penetration", "Better Penetration", () => DrawBetterPenetrationPresetEditor(_presetDraft));
            if (_presetSource != null && GUILayout.Button("選択中のプリセットを削除", GUILayout.ExpandWidth(false)))
            {
                PresetEntry source = _presetSource;
                Action remove = () => QueuePresetAction(() => { DeactivatePreset(); _presets.Remove(source); SavePresets(); });
                if (RequestPresetLeave(remove)) remove();
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(right.x + 6, right.y + 6, right.width - 12, right.height - 12));
            _settings.EditorRightScroll = GUILayout.BeginScrollView(_settings.EditorRightScroll);
            ManagedObjectData selected = GetSelectedData();
            GUILayout.BeginHorizontal();
            GUILayout.Label(selected?.name ?? "左の一覧から選択", GUILayout.MinWidth(50));
            GUILayout.Label("編集先", GUILayout.Width(45));
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && _presetDraft.useLoopSplit && _movementEditPreset == null;
            int index = _presetEditSlot == 1 ? 0 : _presetEditSlot == 2 ? 1 : 2;
            int next = GUILayout.Toolbar(index, new[] { "L1", "L2", "両方" }, GUILayout.Width(156), GUILayout.Height(26));
            if (next != index) QueuePresetAction(() => SetPresetEditSlot(next == 0 ? 1 : next == 1 ? 2 : 0));
            GUI.enabled = enabled && selected != null;
            if (GUILayout.Button("削除", GUILayout.Width(48))) QueuePresetAction(() => RunPresetObjectAction(DeleteSelectedObject));
            GUI.enabled = enabled;
            GUILayout.EndHorizontal();
            DrawSelectedSection();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            if (input && _presetDraft != null)
            {
                if (!wasMovementEdit && _movementEditPreset == null) CaptureDraftObjectChanges(before);
                if (presetBefore != JsonUtility.ToJson(_presetDraft))
                { MarkPresetDraftChanged(); if (!wasMovementEdit) RefreshPresetPose(); }
            }
        }
    }
}
