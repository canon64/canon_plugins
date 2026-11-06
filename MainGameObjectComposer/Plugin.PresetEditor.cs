using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SessionGate = CanonPlugins.Local.SessionGate;

namespace MainGameObjectComposer
{
    public sealed partial class Plugin
    {
        private PresetEntry _presetSource, _presetDraft;
        private bool _presetDraftDirty, _presetDropdown, _presetSplitterHeld;
        private readonly PresetEditorState _presetEditorState = new PresetEditorState();
        private int _presetEditSlot => _presetEditorState.EditSlot;
        private bool _presetSaveAsNew, _presetSaveFailed;
        private Action _presetPendingAction, _presetLeaveAction;
        private Vector2 _presetDropdownScroll;
        private string _presetSearch = "", _presetEditorMessage = "";
        private float _transformRange = 0.5f;
        private Vector3 _positionSliderCenter;
        private Vector3 _scaleSliderCenter, _rotationSliderCenter;
        private float _scaleRange = 1f, _rotationRange = 180f;
        private ManagedObjectData _presetGizmoBefore;
        private bool _presetSaveSucceeded;
        private bool _presetAutoOpenResolved;

        private void QueuePresetAction(Action action) { _presetPendingAction = action; }

        private void DrawComposerViewport(float width, float height)
        {
            if (!SessionGate.Allowed) return;
            if (!Application.isFocused) { _presetDropdown = false; _presetSplitterHeld = false; }
            if (Event.current.type == EventType.Layout && _presetPendingAction != null)
            {
                Action action = _presetPendingAction;
                _presetPendingAction = null;
                action();
            }
            width = Mathf.Max(320f, width);
            height = Mathf.Max(180f, height);
            GUILayout.BeginVertical(GUILayout.Width(width), GUILayout.Height(height));
            int page = GUILayout.Toolbar(_composerPage == 2 ? 1 : 0, ComposerPages, GUILayout.Height(28f)) == 1 ? 2 : 1;
            if (page != _composerPage)
            {
                Action leave = () => QueuePresetAction(() => { _composerPage = page; GUI.FocusControl(null); });
                if (RequestPresetLeave(leave)) leave();
            }
            Rect allocation = GUILayoutUtility.GetRect(width, Mathf.Max(100f, height - 34f), GUILayout.Width(width), GUILayout.Height(Mathf.Max(100f, height - 34f)));
            Rect body = ComposerViewportGeometry.Body(allocation, width, height);
            GUI.BeginGroup(body);
            try
            {
                if (_composerPage == 1) DrawPresetWorkspace(new Rect(0, 0, width, body.height));
                else
                {
                    GUILayout.BeginArea(new Rect(0, 0, width, body.height));
                    _mainScroll = GUILayout.BeginScrollView(_mainScroll);
                    DrawMainBody();
                    GUILayout.EndScrollView();
                    GUILayout.EndArea();
                }
            }
            finally { GUI.EndGroup(); GUILayout.EndVertical(); }
            if (Event.current.rawType == EventType.MouseUp || Event.current.rawType == EventType.ScrollWheel)
            {
                _presetSplitterHeld = false;
                PersistPresetUi();
            }
        }

        private void DrawPresetWorkspace(Rect bounds)
        {
            _singleSliderIndex = 0;
            if (_presetDraft == null && _presetPendingAction == null && !_presetAutoOpenResolved)
            {
                _presetAutoOpenResolved = true;
                QueuePresetAction(() => {
                    if (!SessionGate.Allowed || _presetDraft != null) return;
                    PresetEntry initial = PresetOpenSelection.Choose(_presets, BuildCurrentPositionKey(), _activePreset, _settings.EditorPresetKey, _settings.EditorPresetName);
                    if (initial != null) OpenPresetEditor(initial);
                    else _presetEditorMessage = "現在の体位に紐づくプリセットはありません。";
                });
            }
            var header = new Rect(4, 4, bounds.width - 8, 28);
            if (GUI.Button(header, (_presetSource == null && _presetDraft != null ? "新規プリセット（未保存）" : _presetDraft?.displayName ?? "プリセットを選択") + "  ▼"))
                QueuePresetAction(() => _presetDropdown = !_presetDropdown);

            bool blocked = _presetDropdown || _presetLeaveAction != null;
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !blocked;
            float leftWidth = Mathf.Clamp(bounds.width * _settings.EditorLeftFraction, 230f, Mathf.Max(230f, bounds.width - 310f));
            var left = new Rect(4, 38, leftWidth - 8, bounds.height - 42);
            var splitter = new Rect(leftWidth, 38, 7, bounds.height - 42);
            var right = new Rect(leftWidth + 12, 38, Mathf.Max(120f, bounds.width - leftWidth - 16), bounds.height - 42);
            GUI.Box(left, GUIContent.none);
            GUI.Box(right, GUIContent.none);
            if (_presetDraft != null)
            {
                DrawDraftControls(left, right);
            }
            else
            {
                GUILayout.BeginArea(left);
                GUILayout.Label(_presets.Count == 0 ? "保存済みプリセットはありません。" : string.IsNullOrEmpty(_presetEditorMessage) ? "プリセットを読み込み中です。" : _presetEditorMessage);
                DrawNewPresetControls();
                GUILayout.EndArea();
            }
            GUI.Box(splitter, "⋮");
            Event ev = Event.current;
            if (!blocked && ev.type == EventType.MouseDown && ev.button == 0 && splitter.Contains(ev.mousePosition))
            { _presetSplitterHeld = true; ev.Use(); }
            if (_presetSplitterHeld && ev.type == EventType.MouseDrag)
            {
                _settings.EditorLeftFraction = Mathf.Clamp(ev.mousePosition.x / bounds.width, 0.24f, 0.65f);
                ev.Use();
            }
            GUI.enabled = enabled;
            if (_presetDropdown) DrawPresetDropdown(bounds);
            if (_presetLeaveAction != null) DrawPresetLeavePrompt(bounds);
        }

        private void DrawNewPresetControls()
        {
            if (GUILayout.Button("新規プリセット", GUILayout.ExpandWidth(false))) QueuePresetAction(BeginNewPresetEditor);
        }

        private void DrawPresetSaveControls()
        {
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !_gizmoDragging;
            bool hasDraft = _presetDraft != null;
            bool existing = hasDraft && _presetSource != null;
            GUILayout.Label("保存先の体位: " + BuildCurrentConditionText(BuildCurrentPositionKey()));
            if (!hasDraft)
            {
                GUILayout.Label("新しいプリセット名");
                _newPresetName = GUILayout.TextField(_newPresetName ?? "");
            }
            if (GUILayout.Button(existing ? "選択中へ上書き保存" : "新規プリセットを保存", GUILayout.ExpandWidth(false), GUILayout.Height(28)))
                QueuePresetAction(() => { _presetSaveAsNew = !existing; SavePresetEditor(); });
            if (existing && GUILayout.Button("別名で保存", GUILayout.ExpandWidth(false)))
                QueuePresetAction(() => _presetSaveAsNew = !_presetSaveAsNew);
            if (existing && _presetSaveAsNew)
            {
                GUILayout.Label("新しいプリセット名");
                _newPresetName = GUILayout.TextField(_newPresetName ?? "");
                if (GUILayout.Button("新規として保存", GUILayout.ExpandWidth(false))) QueuePresetAction(() => SavePresetDraftAs());
            }
            if (hasDraft && GUILayout.Button("未保存の変更を破棄", GUILayout.ExpandWidth(false))) QueuePresetAction(ResetPresetDraft);
            GUI.enabled = enabled;
            GUILayout.Label(_presetSaveFailed ? "保存失敗" : !existing ? "未保存の新規プリセット" : _presetDraftDirty ? "未保存の変更あり" : "保存済み");
            if (!string.IsNullOrEmpty(_presetEditorMessage)) GUILayout.Label(_presetEditorMessage);
        }

        private void SavePresetEditor()
        {
            if (_presetDraft != null)
            {
                if (_presetSaveAsNew) SavePresetDraftAs();
                else SavePresetDraft(true);
                return;
            }
            string name = (_newPresetName ?? "").Trim();
            if (name.Length == 0)
            { _presetSaveFailed = true; _presetEditorMessage = "新しいプリセット名を入力してください。"; return; }
            int count = _presets.Count;
            SaveCurrentToPreset(customName: name);
            if (_presetSaveSucceeded && _presets.Count > count)
            {
                OpenPresetEditor(_presets[_presets.Count - 1]);
                _presetEditorMessage = "保存済み：" + name;
            }
            else
            {
                if (_presets.Count > count) _presets.RemoveAt(_presets.Count - 1);
                _presetSaveFailed = true;
                _presetEditorMessage = "保存に失敗しました。名前と配置を保持しています。";
            }
        }

        private void MarkPresetDraftChanged()
        {
            _presetDraftDirty = true;
            _presetSaveFailed = false;
            _presetEditorMessage = "";
        }

        private void ApplyPresetEditorFixedSlot()
        {
            string selection = _selectedId;
            var objects = _presetDraft.useLoopSplit && _presetEditorState.FixedSlot == 2 && _presetDraft.loop2Json.Count > 0
                ? _presetDraft.loop2Json : _presetDraft.loop1Json;
            ApplyLayoutData(new ObjectLayoutFile { objectsJson = objects, selectedId = selection }, true);
            RefreshPresetPose();
            RefreshSelectedEditorFields();
            RaiseManagedObjectListChanged();
        }

        private void RefreshPresetPose()
        {
            if (_presetDraft == null || _movementEditPreset != null || _gizmoDragging) return;
            TickActivePreset();
            TickMotions(0f);
            TickRotationOrbits();
            TickLinearDrivers();
            TickAngleDrivers();
            AlignVibeRotationObjects();
        }

        private void DrawPresetDropdown(Rect bounds)
        {
            Rect popup = new Rect(4, 34, bounds.width - 8, Mathf.Min(320, bounds.height - 38));
            GUI.Box(popup, GUIContent.none);
            GUILayout.BeginArea(new Rect(popup.x + 6, popup.y + 6, popup.width - 12, popup.height - 12));
            _presetSearch = GUILayout.TextField(_presetSearch);
            _presetDropdownScroll = GUILayout.BeginScrollView(_presetDropdownScroll);
            for (int i = 0; i < _presets.Count; i++)
            {
                PresetEntry entry = _presets[i];
                string label = entry.displayName + " / " + BuildPresetConditionText(entry);
                if (!string.IsNullOrEmpty(_presetSearch) && label.IndexOf(_presetSearch, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (GUILayout.Button((entry == _presetSource ? "● " : "") + label, GUILayout.Height(28)))
                {
                    if (entry != _presetSource)
                    {
                        Action select = () => QueuePresetAction(() => OpenPresetEditor(entry));
                        if (RequestPresetLeave(select)) select();
                    }
                    else QueuePresetAction(() => _presetDropdown = false);
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            if (Event.current.type == EventType.MouseDown && !popup.Contains(Event.current.mousePosition))
            { QueuePresetAction(() => _presetDropdown = false); Event.current.Use(); }
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            { QueuePresetAction(() => _presetDropdown = false); Event.current.Use(); }
        }

        private void OpenPresetEditor(PresetEntry source)
        {
            if (source == null) return;
            _presetSource = source;
            _presetDraft = JsonUtility.FromJson<PresetEntry>(JsonUtility.ToJson(source));
            _presetEditorState.Restore(source.editorView);
            if (!_presetDraft.useLoopSplit) _presetEditorState.Select(1);
            _presetDraftDirty = false;
            _presetDropdown = false;
            _presetEditorMessage = "";
            _presetSaveFailed = false;
            _presetSaveAsNew = false;
            DeactivatePreset();
            ActivatePreset(_presetDraft);
            ApplyPresetEditorFixedSlot();
            _selectedId = _objects.Any(o => o.id == _settings.EditorObjectId) ? _settings.EditorObjectId : _objects.FirstOrDefault()?.id;
            RefreshSelectedEditorFields();
            _settings.EditorPresetKey = source.key;
            _settings.EditorPresetName = source.displayName;
            PersistPresetUi();
        }

        private void SetPresetEditSlot(int slot)
        {
            if (_presetDraft == null || _movementEditPreset != null) return;
            if (!_presetDraft.useLoopSplit) slot = 1;
            if (slot == 2 && _presetDraft.loop2Json.Count == 0)
            {
                _presetDraft.loop2Json = new List<string>(_presetDraft.loop1Json);
                MarkPresetDraftChanged();
                RebuildActivePresetCache();
                _presetEditorMessage = "L2をL1の配置から作成しました（未保存）。";
            }
            _presetEditorState.Select(slot);
            ApplyPresetEditorFixedSlot();
        }

        private bool SavePresetDraft(bool keepEditing)
        {
            if (_presetDraft == null) return true;
            if (_movementEditPreset != null) EndMovementEdit(true);
            return CommitPresetDraft(keepEditing, null);
        }

        private bool SavePresetDraftAs(bool keepEditing = true)
        {
            if (_presetDraft == null) return true;
            string name = (_presetSource == null ? _presetDraft.displayName : _newPresetName ?? "").Trim();
            string currentKey = BuildCurrentPositionKey();
            if (name.Length == 0 || _presets.Any(p => p.key == currentKey && p.displayName == name))
            { _presetSaveFailed = true; _presetEditorMessage = "重複しない新しいプリセット名を入力してください。"; return false; }
            if (_movementEditPreset != null) EndMovementEdit(true);
            return CommitPresetDraft(keepEditing, name);
        }

        private bool CommitPresetDraft(bool keepEditing, string newName)
        {
            PresetEntry draft = _presetDraft;
            bool append = newName != null;
            PresetEntry candidate;
            try
            {
                candidate = JsonUtility.FromJson<PresetEntry>(JsonUtility.ToJson(draft));
                candidate.editorView = _presetEditorState.Capture();
                if (append) candidate.displayName = newName;
                PresetConditionBinding.Bind(candidate, BuildCurrentPositionKey(), UpdatePresetConditionMetadata);
                // Encode each authored endpoint independently; never sample the displayed blend.
                PresetEndpointCoordinates.Capture(candidate, data => EncodeObjects(DecodeObjects(data)));
            }
            catch (Exception)
            {
                _presetSaveFailed = true;
                _presetEditorMessage = "保存失敗：配置データを準備できませんでした。編集内容は保持しています。";
                return false;
            }
            _settings.EditorObjectId = _selectedId;
            bool success = false;
            _presetDraft = null; // Let the common atomic writer persist this one transaction.
            try
            {
                success = PresetWriteTransaction.Commit(_presets, _presetSource, candidate, append, () => {
                    SavePresets(); return _presetSaveSucceeded;
                }, (saved, target) => JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(saved), target));
            }
            catch (Exception) { success = false; }
            finally { _presetDraft = draft; }
            if (!success)
            {
                _presetSaveFailed = true;
                _presetEditorMessage = "保存に失敗しました。編集内容を保持しています。";
                return false;
            }
            _motionPresetSavePending = false;
            _presetDraftDirty = false;
            _lastAutoSwitchKey = null;
            if (!append) candidate = _presetSource;
            DeactivatePreset();
            _presetDraft = _presetSource = null;
            _presetEditorState.Open();
            if (keepEditing)
            {
                OpenPresetEditor(candidate);
            }
            else ActivatePreset(candidate);
            _presetEditorMessage = "保存済み：" + candidate.displayName;
            return true;
        }

        private void ResetPresetDraft()
        {
            if (_presetSource == null) { BeginNewPresetEditor(); return; }
            PresetEntry source = _presetSource;
            int slot = _presetEditSlot;
            int fixedSlot = _presetEditorState.FixedSlot;
            _settings.EditorObjectId = _selectedId;
            AbortPresetEditor();
            OpenPresetEditor(source);
            SetPresetEditSlot(fixedSlot);
            SetPresetEditSlot(slot);
        }

        private void AbortPresetEditor()
        {
            _presetAutoOpenResolved = false;
            if (_presetDraft == null) return;
            if (_movementEditPreset != null) EndMovementEdit(false);
            PresetEntry source = _presetSource;
            DeactivatePreset();
            _presetDraft = null;
            _presetSource = null;
            _presetDraftDirty = false;
            _presetEditorState.Open();
            _motionPresetSavePending = false;
            _presetDropdown = false;
            _presetSplitterHeld = false;
            _presetGizmoBefore = null;
            _presetLeaveAction = _presetPendingAction = null;
            if (source != null && SessionGate.Allowed) ActivatePreset(source);
            else if (source != null) ApplyLayoutData(new ObjectLayoutFile { objectsJson = source.loop1Json }, false);
        }

        private bool RequestPresetLeave(Action leave)
        {
            if (_presetDraft == null) { _presetAutoOpenResolved = false; return true; }
            if (_presetDraftDirty || _movementEditPreset != null)
            { QueuePresetAction(() => { _presetLeaveAction = leave; _presetDropdown = false; }); return false; }
            QueuePresetAction(() => {
                AbortPresetEditor();
                _lastAutoSwitchKey = null;
                leave?.Invoke();
            });
            return false;
        }

        private bool PrepareComposerVisibility(bool visible)
        {
            if (visible && !_settings.UiVisible) _presetAutoOpenResolved = false;
            if (visible || !_settings.UiVisible) return true;
            return RequestPresetLeave(() => {
                _settings.UiVisible = false;
                if (_cfgUiVisible != null) _cfgUiVisible.Value = false;
                SaveSettings();
            });
        }

        private void DrawPresetLeavePrompt(Rect bounds)
        {
            Rect rect = new Rect(8, 38, bounds.width - 16, 108);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 8, rect.width - 16, rect.height - 16));
            GUILayout.Label("未保存の変更があります。保存して切り替えますか？");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("保存して切替")) QueuePresetAction(() => ResolvePresetLeave(true));
            if (GUILayout.Button("破棄して切替")) QueuePresetAction(() => ResolvePresetLeave(false));
            if (GUILayout.Button("切替中止")) QueuePresetAction(() => _presetLeaveAction = null);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void ResolvePresetLeave(bool save)
        {
            Action leave = _presetLeaveAction;
            if (save) { if (!(_presetSaveAsNew ? SavePresetDraftAs(false) : SavePresetDraft(false))) return; }
            else AbortPresetEditor();
            _presetLeaveAction = null;
            _lastAutoSwitchKey = null;
            leave?.Invoke();
        }

        private void CaptureDraftObjectChanges(Dictionary<string, ManagedObjectData> before, bool structure = true)
        {
            if (_presetDraft == null || before == null) return;
            var current = BuildIdMap(_objects);
            bool changed = false;
            foreach (int slot in new[] { 1, 2 })
            {
                bool slotChanged = false;
                List<ManagedObjectData> data = DecodeObjects(slot == 1 ? _presetDraft.loop1Json : _presetDraft.loop2Json);
                bool editValues = _presetEditSlot == 0 || _presetEditSlot == slot;
                foreach (ManagedObjectData target in data.ToArray())
                {
                    if (!current.TryGetValue(target.id, out ManagedObjectData after))
                    { if (structure) { data.Remove(target); slotChanged = true; } continue; }
                    if (editValues && before.TryGetValue(target.id, out ManagedObjectData old))
                    {
                        ManagedObjectData edited = after;
                        if (old.parentKind != after.parentKind || old.parentRefId != after.parentRefId)
                        {
                            // Reparent helpers already reproject both saved poses in their own coordinate systems.
                            edited = CloneObject(after); edited.localPosition = old.localPosition; edited.localEulerAngles = old.localEulerAngles;
                        }
                        slotChanged |= PresetEditFields.Apply(old, edited, target, _presetEditSlot == 0);
                    }
                    if (structure && before.ContainsKey(target.id))
                    {
                        if (target.parentKind != after.parentKind || target.parentRefId != after.parentRefId) slotChanged = true;
                        target.parentKind = after.parentKind; target.parentRefId = after.parentRefId; target.parentId = after.parentId;
                    }
                }
                if (structure) foreach (ManagedObjectData added in _objects)
                    if (!before.ContainsKey(added.id)) { data.Add(CloneObject(added)); slotChanged = true; }
                if (slotChanged)
                {
                    if (slot == 1) _presetDraft.loop1Json = EncodeObjects(data);
                    else _presetDraft.loop2Json = EncodeObjects(data);
                    changed = true;
                }
            }
            if (changed) { MarkPresetDraftChanged(); RebuildActivePresetCache(); RefreshPresetPose(); }
        }

        private void PersistPresetUi()
        {
            if (_settings.EditorObjectId == _selectedId && _settings.EditorLeftScroll == _lastPresetLeftScroll
                && _settings.EditorRightScroll == _lastPresetRightScroll && _settings.EditorLeftFraction == _lastPresetFraction
                && _settings.EditorPresetKey == _lastPresetKey && _settings.EditorPresetName == _lastPresetName) return;
            _settings.EditorObjectId = _selectedId;
            _lastPresetLeftScroll = _settings.EditorLeftScroll; _lastPresetRightScroll = _settings.EditorRightScroll;
            _lastPresetFraction = _settings.EditorLeftFraction;
            _lastPresetKey = _settings.EditorPresetKey; _lastPresetName = _settings.EditorPresetName;
            SaveSettings();
        }
        private Vector2 _lastPresetLeftScroll, _lastPresetRightScroll;
        private float _lastPresetFraction;
        private string _lastPresetKey, _lastPresetName;

        private void DrawTransformRangeControls()
        {
            GUILayout.Label("位置の調整幅（現在値の周辺）");
            GUILayout.BeginHorizontal();
            foreach (float range in new[] { 0.01f, 0.1f, 0.5f, 10f, 1000f })
                if (GUILayout.Button("±" + range + "m", GUILayout.ExpandWidth(false)))
                { _transformRange = range; _positionSliderCenter = GetSelectedData()?.localPosition ?? Vector3.zero; }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("現在値の周辺を調整", GUILayout.ExpandWidth(false)))
                _positionSliderCenter = GetSelectedData()?.localPosition ?? Vector3.zero;
        }

        

        

        private void DrawRotationRangeControls()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("角度の調整幅", GUILayout.Width(100));
            foreach (float range in new[] { 5f, 45f, 180f })
                if (GUILayout.Button("±" + range + "°", GUILayout.ExpandWidth(false)))
                { _rotationRange = range; _rotationSliderCenter = GetSelectedData()?.localEulerAngles ?? Vector3.zero; }
            GUILayout.EndHorizontal();
        }
        private void DrawScaleRangeControls()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("サイズの調整幅", GUILayout.Width(100));
            foreach (float range in new[] { 0.01f, 0.1f, 1f, 10f })
                if (GUILayout.Button("±" + range, GUILayout.ExpandWidth(false)))
                { _scaleRange = range; _scaleSliderCenter = GetSelectedData()?.localScale ?? Vector3.one; }
            GUILayout.EndHorizontal();
        }
    }
}
