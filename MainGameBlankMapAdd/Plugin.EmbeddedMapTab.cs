using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;
using MainGameUiInputCapture;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private string _mapTabInputKey;
        private Vector2 _mapAdjustScroll, _mapListScroll;
        private float _mapAdjustmentWidth = 620f;
        private Action _mapSelectionPending;
        private bool _mapTabSkyPage;
        private bool _mapTabPendingScrollReset;
        private readonly string[] _mapTabInputs = new string[7];
        private readonly float[] _mapTabSliderCenters = new float[3];
        private readonly float[] _mapTabRotationSliderCenters = new float[3];
        private int _mapTabSliderRangeIndex = 1;
        private int _mapTabRotationRangeIndex = 3;
        private static readonly float[] MapTabSliderRanges = { 1f, 10f, 100f, 1000f, 10000f, 100000f, 1000000f };
        private static readonly float[] MapTabRotationRanges = { 1f, 5f, 15f, 45f, 180f };
        private readonly Dictionary<int, Texture2D> _mapTabThumbnails = new Dictionary<int, Texture2D>();
        private string _mapPlacementEditKey, _mapPlacementStatus;
        private Transform _mapPlacementEditRoot;
        private float[] _mapPlacementBefore;
        private bool _mapPlacementBeforeActorCenter;

        private void DrawMapPlacementControls()
        {
            if (!TryMapWarpContext(out string key, out Transform root, out bool hotel)) return;
            PrepareMapWarpNavigation(key, root);
            var entries = MapWarpPlacementEntries(hotel);
            if (_mapPlacementEditKey != key || _mapPlacementEditRoot != root)
            {
                _mapPlacementEditKey = key;
                _mapPlacementEditRoot = root;
                _mapPlacementBefore = Array.ConvertAll(entries, e => e.Value);
                _mapPlacementBeforeActorCenter = !hotel && EnsureRoomOverlayPositionConfig(_roomOverlayPositionKey).RotateAroundActor.Value;
                _mapPlacementStatus = null;
                _mapTabInputKey = null;
            }
            EnsureMapTabInputs(key, entries);
            GUILayout.BeginVertical("box");
            GUILayout.Label("マップ自体の座標・角度");
            GUILayout.Label("位置の調整幅 ±" + MapTabSliderRanges[_mapTabSliderRangeIndex].ToString("G", CultureInfo.InvariantCulture));
            _mapTabSliderRangeIndex = GUILayout.Toolbar(_mapTabSliderRangeIndex,
                new[] { "1", "10", "100", "千", "1万", "10万", "100万" }, GUILayout.Width(Mathf.Min(470f, _mapAdjustmentWidth - 40f)));
            bool autoSave = Config.SaveOnConfigSet;
            Config.SaveOnConfigSet = false;
            try
            {
                for (int i = 0; i < 3; i++) DrawMapTabAxis("位置 " + "XYZ"[i], i, entries[i], hotel);
                GUILayout.Label("角度の調整幅（度）");
                _mapTabRotationRangeIndex = GUILayout.Toolbar(_mapTabRotationRangeIndex,
                    new[] { "±1°", "±5°", "±15°", "±45°", "±180°" }, GUILayout.Width(Mathf.Min(400f, _mapAdjustmentWidth - 40f)));
                for (int i = 0; i < 3; i++) DrawMapTabRotationAxis("角度 " + "XYZ"[i], i, entries[i + 3], hotel);
                if (hotel)
                {
                    DrawMapTabNumber("倍率", 6, entries[6], .05f, 20f);
                    float shown = Mathf.Clamp(entries[6].Value, .05f, 20f);
                    float scale = DrawCapturedMapSlider(shown, .05f, 20f);
                    if (scale != shown)
                    {
                        entries[6].Value = scale;
                        _mapTabInputs[6] = scale.ToString("R", CultureInfo.InvariantCulture);
                        ApplyVamHotelTransform();
                    }
                }
                else
                {
                    var settings = EnsureRoomOverlayPositionConfig(_roomOverlayPositionKey);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("回転中心", GUILayout.Width(75f));
                    if (_mapAdjustmentWidth < 450f) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                    int selected = settings.RotateAroundActor.Value ? 1 : 0;
                    int next = GUILayout.Toolbar(selected, new[] { "マップ原点", "読込時のキャラ位置" }, GUILayout.Width(320f));
                    if (next != selected) SetMapTabRotationCenter(settings, next == 1);
                    GUILayout.EndHorizontal();
                }
                GUILayout.BeginHorizontal();
                bool validNumbers = true;
                for (int i = 0; i < entries.Length; i++)
                {
                    float number;
                    if (!(float.TryParse(_mapTabInputs[i], NumberStyles.Float, CultureInfo.InvariantCulture, out number) ||
                        float.TryParse(_mapTabInputs[i], NumberStyles.Float, CultureInfo.CurrentCulture, out number)) ||
                        float.IsNaN(number) || float.IsInfinity(number)) validNumbers = false;
                }
                bool enabled = GUI.enabled;
                GUI.enabled = enabled && validNumbers;
                if (GUILayout.Button("配置を保存", GUILayout.Width(120f), GUILayout.Height(28f)))
                {
                    try
                    {
                        Config.Save();
                        _mapPlacementBefore = Array.ConvertAll(entries, e => e.Value);
                        _mapPlacementBeforeActorCenter = !hotel && EnsureRoomOverlayPositionConfig(_roomOverlayPositionKey).RotateAroundActor.Value;
                        _mapPlacementStatus = "このマップの座標・角度を保存しました。";
                    }
                    catch (Exception ex) { _mapPlacementStatus = "配置を保存できません：" + ex.Message; }
                }
                GUI.enabled = enabled;
                if (GUILayout.Button("未保存の変更を戻す", GUILayout.Width(170f), GUILayout.Height(28f)))
                {
                    for (int i = 0; i < entries.Length; i++) entries[i].Value = _mapPlacementBefore[i];
                    if (!hotel) EnsureRoomOverlayPositionConfig(_roomOverlayPositionKey).RotateAroundActor.Value = _mapPlacementBeforeActorCenter;
                    ApplyMapTabTransform(hotel); _mapTabInputKey = null;
                    _mapPlacementStatus = "保存時の配置へ戻しました。";
                }
                GUILayout.EndHorizontal();
                if (!validNumbers) GUILayout.Label("数値を正しく入力してから保存してください。");
            }
            finally { Config.SaveOnConfigSet = autoSave; }
            SyncHPlaceSliderCapture();
            if (!string.IsNullOrEmpty(_mapPlacementStatus)) GUILayout.Label(_mapPlacementStatus);
            GUILayout.EndVertical();
        }

        private float DrawCapturedMapSlider(float value, float min, float max, float width = 220f)
        {
            int previous = GUIUtility.hotControl;
            float next = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(width));
            if (GUIUtility.hotControl != 0 && GUIUtility.hotControl != previous)
            {
                _hPlaceSliderHotControl = GUIUtility.hotControl;
                UiInputCaptureApi.Sync("MainGameBlankMapAdd.PlaybackBar", "h-location-slider", true);
            }
            return next;
        }

        private void DrawEmbeddedMapTab(float availableWidth, float availableHeight)
        {
            ApplyPendingHPlaceUi();
            if (Event.current.type == EventType.Layout && _mapSelectionPending != null)
            {
                var selection = _mapSelectionPending;
                _mapSelectionPending = null;
                if (_hPlaceDraft == null && CanUseRoomOverlay()) selection();
            }
            GUILayout.BeginHorizontal();
            Color oldTabColor = GUI.backgroundColor;
            bool tabsEnabled = GUI.enabled;
            GUI.enabled = tabsEnabled && _hPlaceDraft == null;
            if (!_mapTabSkyPage) GUI.backgroundColor = new Color(.6f, .85f, 1f);
            if (GUILayout.Button("マップ配置", GUILayout.Width(130f)))
            {
                _mapSelectionPending = () => { _mapTabSkyPage = false; _mapTabPendingScrollReset = true; };
            }
            GUI.backgroundColor = _mapTabSkyPage ? new Color(.6f, .85f, 1f) : oldTabColor;
            if (GUILayout.Button("空・環境光", GUILayout.Width(130f)))
            {
                _mapSelectionPending = () => { _mapTabSkyPage = true; _mapTabPendingScrollReset = true; };
            }
            GUI.backgroundColor = oldTabColor;
            GUI.enabled = tabsEnabled;
            GUILayout.EndHorizontal();
            if (!CanUseRoomOverlay())
            {
                GUILayout.Label("オールポーズマップのHシーンで使用できます。");
                return;
            }

            if (Event.current.type == EventType.Layout) RefreshRoomOverlayChoices();
            var layout = new MapTabLayout(availableWidth, availableHeight);
            float adjustmentWidth = layout.AdjustmentWidth;
            _mapAdjustmentWidth = adjustmentWidth;
            float listWidth = layout.ListWidth;
            float paneHeight = layout.PaneHeight;
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(adjustmentWidth));
            _mapAdjustScroll = GUILayout.BeginScrollView(_mapAdjustScroll, false, true,
                GUILayout.Width(adjustmentWidth), GUILayout.Height(paneHeight));
            GUILayout.BeginVertical("box");
            GUILayout.Label("マップ調整");
            GUILayout.Label("現在: " + (string.IsNullOrEmpty(_roomOverlayDisplayName) ? "元の部屋" : _roomOverlayDisplayName),
                GUILayout.MinWidth(220f), GUILayout.ExpandWidth(false));
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !_roomOverlayLoading && _hPlaceDraft == null;
            if (GUILayout.Button("元の部屋に戻す", GUILayout.Width(130f))) _mapSelectionPending = ClearRoomOverlay;
            GUI.enabled = previousEnabled;
            GUILayout.EndVertical();
            bool previousWrap = GUI.skin.label.wordWrap;
            GUI.skin.label.wordWrap = true;
            try
            {
                if (_roomOverlayLoading)
                {
                    GUILayout.Label(_aiLoadInProgress ? _aiIslandStatus : "マップを読み込み中です。");
                    if (_aiLoadInProgress && GUILayout.Button("島の読み込みを中止", GUILayout.Width(190f))) _aiCancelRequested = true;
                }
                else if (_mapTabSkyPage) DrawMapSkyControls();
                else
                {
                    DrawAiIslandSettings();
                    if (_hPlaceDraft == null)
                    {
                        DrawMapPlacementControls();
                        DrawMapWarpPoints();
                    }
                    DrawHLocationPresets(adjustmentWidth - 24f);
                }
            }
            finally { GUI.skin.label.wordWrap = previousWrap; }
            if (Event.current.rawType == EventType.MouseUp ||
                (Event.current.rawType == EventType.KeyUp && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)))
                CommitAiIslandUiSettings();
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.Space(8f);
            GUILayout.BeginVertical(GUILayout.Width(listWidth));
            _mapListScroll = GUILayout.BeginScrollView(_mapListScroll, false, true,
                GUILayout.Width(listWidth), GUILayout.Height(paneHeight));
            GUILayout.Label("マップ一覧");
            GUI.enabled = previousEnabled && !_roomOverlayLoading && _hPlaceDraft == null;
            DrawAiIslandChoice();
            DrawMapChoiceList(listWidth - 20f);
            GUI.enabled = previousEnabled;
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
        private void DrawMapChoiceList(float availableWidth)
        {
            GUILayout.BeginVertical("box");
            BaseMap map = LastReservedMap;
            var thumbnails = map != null ? map.mapThumbnailInfoDic : null;
            GUIStyle nameStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            const float tileWidth = 160f;
            int columns = MapTabLayout.Columns(availableWidth);
            for (int i = 0; i < _roomOverlayChoices.Count; i++)
            {
                if (i % columns == 0) GUILayout.BeginHorizontal();
                MapInfo.Param choice = _roomOverlayChoices[i];
                string name = string.IsNullOrEmpty(choice.DisplayName) ? choice.MapName : choice.DisplayName;
                Texture2D image = null;
                int? id = choice.FindThumbnailID();
                if (id.HasValue && thumbnails != null && thumbnails.TryGetValue(id.Value, out MapThumbnailInfo.Param thumbnail))
                {
                    if (!_mapTabThumbnails.TryGetValue(id.Value, out image) || image == null)
                    {
                        image = thumbnail.GetThumbnail();
                        if (image != null) _mapTabThumbnails[id.Value] = image;
                    }
                }
                Rect tile = GUILayoutUtility.GetRect(tileWidth, 124f, GUILayout.Width(tileWidth), GUILayout.Height(124f));
                Color oldColor = GUI.backgroundColor;
                if (_roomOverlaySceneName == choice.AssetName) GUI.backgroundColor = new Color(.6f, .85f, 1f);
                if (GUI.Button(tile, GUIContent.none)) _mapSelectionPending = () => SelectRoomOverlay(choice);
                GUI.backgroundColor = oldColor;
                if (image != null)
                    GUI.DrawTexture(new Rect(tile.x + 5f, tile.y + 4f, tile.width - 10f, 91f),
                        image, ScaleMode.ScaleToFit);
                GUI.Label(new Rect(tile.x + 4f, tile.y + 97f, tile.width - 8f, 24f), name, nameStyle);
                if (i % columns == columns - 1 || i == _roomOverlayChoices.Count - 1) GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        private void EnsureMapTabInputs(string key, ConfigEntry<float>[] entries)
        {
            if (_mapTabInputKey == key) return;
            _mapTabInputKey = key;
            for (int i = 0; i < entries.Length; i++)
                _mapTabInputs[i] = entries[i].Value.ToString("R", CultureInfo.InvariantCulture);
            for (int i = 0; i < 3; i++) _mapTabSliderCenters[i] = entries[i].Value;
            for (int i = 0; i < 3; i++) _mapTabRotationSliderCenters[i] = entries[i + 3].Value;
        }

        private void SyncMapTabPositionInputs(ConfigEntry<float>[] entries)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                _mapTabInputs[axis] = entries[axis].Value.ToString("R", CultureInfo.InvariantCulture);
                _mapTabSliderCenters[axis] = entries[axis].Value;
            }
        }

        private void DrawMapTabAxis(string label, int index, ConfigEntry<float> entry, bool hotel)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(70f));
            string previous = _mapTabInputs[index] ?? string.Empty;
            string input = GUILayout.TextField(previous, GUILayout.Width(125f));
            _mapTabInputs[index] = input;
            float parsed;
            if (input != previous && (float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ||
                 float.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed)) &&
                !float.IsNaN(parsed) && !float.IsInfinity(parsed) && entry.Value != parsed)
            {
                entry.Value = parsed;
                _mapTabSliderCenters[index] = parsed;
                ApplyMapTabTransform(hotel);
            }
            float range = MapTabSliderRanges[_mapTabSliderRangeIndex];
            if (_mapAdjustmentWidth < 610f) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
            float shown = Mathf.Clamp(entry.Value, _mapTabSliderCenters[index] - range, _mapTabSliderCenters[index] + range);
            float next = DrawCapturedMapSlider(shown, _mapTabSliderCenters[index] - range, _mapTabSliderCenters[index] + range);
            if (next != shown)
            {
                entry.Value = next;
                _mapTabInputs[index] = next.ToString("R", CultureInfo.InvariantCulture);
                ApplyMapTabTransform(hotel);
            }
            if (GUILayout.Button("現在値の周辺を調整", GUILayout.Width(160f)))
                _mapTabSliderCenters[index] = entry.Value;
            GUILayout.EndHorizontal();
        }

        private void DrawMapTabRotationAxis(string label, int axis, ConfigEntry<float> entry, bool hotel)
        {
            int index = axis + 3;
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(70f));
            string previous = _mapTabInputs[index] ?? string.Empty;
            string input = GUILayout.TextField(previous, GUILayout.Width(125f));
            _mapTabInputs[index] = input;
            float parsed;
            if (input != previous && (float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ||
                float.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed)) &&
                !float.IsNaN(parsed) && !float.IsInfinity(parsed) && entry.Value != parsed)
            {
                entry.Value = parsed;
                _mapTabRotationSliderCenters[axis] = parsed;
                ApplyMapTabRotation(hotel);
            }
            float range = MapTabRotationRanges[_mapTabRotationRangeIndex];
            if (_mapAdjustmentWidth < 610f) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
            float shown = Mathf.Clamp(entry.Value, _mapTabRotationSliderCenters[axis] - range, _mapTabRotationSliderCenters[axis] + range);
            float next = DrawCapturedMapSlider(shown, _mapTabRotationSliderCenters[axis] - range, _mapTabRotationSliderCenters[axis] + range);
            if (next != shown)
            {
                entry.Value = next;
                _mapTabInputs[index] = next.ToString("R", CultureInfo.InvariantCulture);
                ApplyMapTabRotation(hotel);
            }
            if (GUILayout.Button("現在値の周辺を調整", GUILayout.Width(160f)))
                _mapTabRotationSliderCenters[axis] = entry.Value;
            GUILayout.EndHorizontal();
        }

        private void DrawMapTabNumber(string label, int index, ConfigEntry<float> entry, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(70f));
            string previous = _mapTabInputs[index] ?? string.Empty;
            string input = GUILayout.TextField(previous, GUILayout.Width(125f));
            _mapTabInputs[index] = input;
            float parsed;
            if (input != previous && (float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ||
                 float.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed)) &&
                !float.IsNaN(parsed) && !float.IsInfinity(parsed))
            {
                float value = Mathf.Clamp(parsed, min, max);
                if (entry.Value != value)
                {
                    entry.Value = value;
                    ApplyVamHotelTransform();
                }
            }
            GUILayout.EndHorizontal();
        }

        private void ApplyMapTabTransform(bool hotel)
        {
            if (hotel) ApplyVamHotelTransform();
            else
            {
                ApplyRoomOverlayPosition();
                if (_roomOverlaySceneName == StreetlightRoadSceneKey) ApplyStreetlightCameraPreview();
            }
        }

        private void SetMapTabRotationCenter(RoomOverlayPosition value, bool useActor)
        {
            if (_roomOverlayPositionRoot == null) return;
            Vector3 oldPosition = _roomOverlayPositionRoot.transform.position;
            Quaternion rotation = _roomOverlayBaseRotation * Quaternion.Euler(
                value.RotationX.Value, value.RotationY.Value, value.RotationZ.Value);
            Quaternion delta = rotation * Quaternion.Inverse(_roomOverlayBaseRotation);
            Vector3 unrotatedPosition = useActor
                ? _roomOverlayActorAnchor + Quaternion.Inverse(delta) * (oldPosition - _roomOverlayActorAnchor)
                : oldPosition;
            Vector3 offset = unrotatedPosition - _roomOverlayBasePosition;
            value.X.Value = offset.x;
            value.Y.Value = offset.y;
            value.Z.Value = offset.z;
            for (int i = 0; i < 3; i++)
            {
                float coordinate = i == 0 ? offset.x : i == 1 ? offset.y : offset.z;
                _mapTabInputs[i] = coordinate.ToString("R", CultureInfo.InvariantCulture);
                _mapTabSliderCenters[i] = coordinate;
            }
            value.RotateAroundActor.Value = useActor;
            ApplyMapTabTransform(false);
        }
    }
}
