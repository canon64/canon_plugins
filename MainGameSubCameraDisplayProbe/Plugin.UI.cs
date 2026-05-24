using System.Globalization;
using MainGameUiInputCapture;
using UnityEngine;

namespace MainGameSubCameraDisplayProbe
{
    public sealed partial class Plugin
    {
        private void DrawWindow(int id)
        {
            _lastWindowRect = _windowRect;
            GUILayout.BeginVertical();
            _windowScroll = GUILayout.BeginScrollView(_windowScroll, false, true, GUILayout.Height(_windowRect.height - 50f));

            // ───── プラグイン全体有効化
            bool prevEnabled = _settings.Enabled;
            DrawToggleRow("プラグイン全体を有効", ref _settings.Enabled);
            if (prevEnabled != _settings.Enabled)
            {
                if (_cfgEnabled != null)
                    _cfgEnabled.Value = _settings.Enabled;
                else
                    ApplyRuntimeState();
            }
            DrawToggleRow("VoiceFaceEventサブカメラトリガー", ref _settings.VoiceFaceEventSubCameraTriggerEnabled);

            // ───── ステータス
            GUILayout.Label("【ステータス】");
            GUILayout.Label("Probe: " + (_rootObject != null ? "生成済み" : "未生成"));
            GUILayout.Label("SubCamera: " + (_subCamera != null ? _subCamera.name : "none"));
            GUILayout.Label("RenderTexture: " + (_renderTexture != null ? (_renderTexture.width + "x" + _renderTexture.height) : "none"));
            GUILayout.Label("FOV: " + _settings.CameraFieldOfView.ToString("0.##", CultureInfo.InvariantCulture));
            GUILayout.Label("UI Capture: " + (UiInputCaptureApi.IsAvailable ? UiInputCaptureApi.GetStateSummary() : "unavailable"));

            // ───── 操作
            GUILayout.Space(8f);
            GUILayout.Label("【操作】");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("両方破棄", GUILayout.Height(28f)))
                DestroyProbe();
            if (GUILayout.Button("眼の前に移動", GUILayout.Height(28f)) && _rootObject != null)
                MoveProbeInFrontOfPlayer(initialCreate: false);
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            if (DrawFoldoutHeader("カメラ関連", ref _cameraUiOpen))
                DrawCameraSection();

            GUILayout.Space(8f);
            if (DrawFoldoutHeader("ディスプレイ関連", ref _displayUiOpen))
                DrawDisplaySection();

            // ───── 体位連動
            GUILayout.Space(8f);
            DrawPosePresetUi();

            // ───── その他
            GUILayout.Space(8f);
            GUILayout.Label("【その他】");
            DrawSliderWithText("Front Dist", ref _settings.SpawnDistance, 0.1f, 2f, SourceSlider);
            DrawSliderWithText("Gizmo Size", ref _settings.GizmoSizeMultiplier, 0.2f, 4f, SourceSlider);

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            UpdateWindowDragState();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
            SaveSettings();
        }

        private bool DrawFoldoutHeader(string label, ref bool open)
        {
            string text = (open ? "▼ " : "▶ ") + label;
            if (GUILayout.Button(text, GUILayout.Height(28f)))
                open = !open;
            return open;
        }

        private void DrawCameraSection()
        {
            // ───── カメラ
            GUILayout.Label("【カメラ】");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("カメラだけ破棄", GUILayout.Height(28f)))
                DestroyCameraOnly();
            if (GUILayout.Button("カメラを眼の前", GUILayout.Height(28f)))
                MoveCameraInFrontOfPlayer();
            GUILayout.EndHorizontal();
            DrawToggleRow("拍FOV連動", ref _settings.BeatFovEnabled);
            DrawSliderWithText("拍ズーム倍率", ref _settings.BeatFovZoomMultiplier, 1f, 3f, SourceSlider);
            DrawToggleRow("キャラのみ表示（背景非表示）", ref _settings.CharactersOnlyMode);
            DrawRenderResolutionUi();
            GUILayout.Label("Filter: " + _settings.RenderFilterMode);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Point", GUILayout.Height(24f)))
                _settings.RenderFilterMode = "Point";
            if (GUILayout.Button("Bilinear", GUILayout.Height(24f)))
                _settings.RenderFilterMode = "Bilinear";
            if (GUILayout.Button("Trilinear", GUILayout.Height(24f)))
                _settings.RenderFilterMode = "Trilinear";
            GUILayout.EndHorizontal();
            if (GUILayout.Button("現在カメラをサブカメラにする", GUILayout.Height(24f)))
                CopyCurrentCameraToSubCamera();

            DrawToggleRow("カメラギズモ表示", ref _settings.CameraGizmoVisible);
            if (DrawSliderWithText("FOV", ref _settings.CameraFieldOfView, 10f, 170f, SourceSlider))
                SyncActiveFovFromUi();
            DrawSliderWithText("Near", ref _settings.CameraNearClip, 0.01f, 1f, SourceSlider);
            DrawSliderWithText("Far", ref _settings.CameraFarClip, 10f, 1000f, SourceSlider);

            bool cameraPosChanged = DrawVector3SlidersZoom("Camera Pos", ref _settings.CameraPosX, ref _settings.CameraPosY, ref _settings.CameraPosZ, -20f, 20f);
            bool cameraRotChanged = DrawVector3SlidersZoom("Camera Rot", ref _settings.CameraRotX, ref _settings.CameraRotY, ref _settings.CameraRotZ, 0f, 360f);
            if (cameraPosChanged || cameraRotChanged)
                SyncCameraControlSliders(cameraPosChanged, cameraRotChanged);

            // ボーン追従中以外も常に場所を確保する（追従ON/OFFで下の要素が跳ねないように）
            DrawActiveBoneFollowUi();

            // ───── 名前付きプリセット / ボーン追従
            GUILayout.Space(8f);
            DrawPresetUi();

            // ───── 遷移移動
            GUILayout.Space(8f);
            GUILayout.Label("【遷移移動】");
            DrawSliderWithText("遷移時間", ref _settings.TransitionSeconds, 0f, 3f, SourceSlider);
            DrawTransitionEasingUi();

            // ───── VR / 掴み操作
            GUILayout.Space(8f);
            GUILayout.Label("【VR / 掴み操作】");
            DrawSliderWithText("VR Grip Dist", ref _settings.VrGripStartDistance, 0.05f, 1.5f, SourceSlider);
            DrawToggleRow("カメラ掴み中にカメラ付属プレビュー表示", ref _settings.ShowHandPreviewWhileGrabbingCamera);
            DrawToggleRow("カメラ掴み中スティック上下でFOV", ref _settings.VrFovStickEnabled);
            DrawSliderWithText("VR FOV Speed", ref _settings.VrFovStickSpeed, 1f, 120f, SourceSlider);
            DrawSliderWithText("VR FOV Dead", ref _settings.VrFovStickDeadzone, 0f, 0.95f, SourceSlider);
            DrawToggleRow("カメラ掴み中トリガーで写真保存", ref _settings.PhotoCaptureOnTrigger);

            // ───── 写真
            GUILayout.Space(8f);
            GUILayout.Label("【写真】");
            if (GUILayout.Button("写真保存テスト", GUILayout.Height(24f)))
                CaptureSubCameraPhoto("ui-test");

            // ───── 動画録画
            GUILayout.Space(8f);
            DrawVideoRecordingUi();

            // ───── 手元プレビュー位置（カメラ掴み中の小型ビューファインダのカメラ起点オフセット）
            GUILayout.Space(8f);
            GUILayout.Label("【手元プレビュー位置】");
            DrawSliderWithText("カメラ見た目サイズ", ref _settings.CameraVisualScale, 0.1f, 3f, SourceSlider);
            DrawVector3SlidersZoom("Preview Pos", ref _settings.HandPreviewOffsetX, ref _settings.HandPreviewOffsetY, ref _settings.HandPreviewOffsetZ, -1f, 1f);
        }

        private void DrawDisplaySection()
        {
            // ───── ディスプレイ
            GUILayout.Label("【ディスプレイ】");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("ディスプレイだけ破棄", GUILayout.Height(28f)))
                DestroyDisplayOnly();
            if (GUILayout.Button("ディスプレイを眼の前", GUILayout.Height(28f)))
                MoveDisplayInFrontOfPlayer();
            GUILayout.EndHorizontal();
            DrawToggleRow("Overlay Camera", ref _settings.UseDisplayOverlayCamera);
            DrawSliderWithText("Display Layer", ref _displayLayerFloat, 0f, 30f, SourceSlider);
            _settings.DisplayLayer = Mathf.Clamp(Mathf.RoundToInt(_displayLayerFloat), 0, 30);
            _settings.DisplayWidth = SettingsStore.CalculateDisplayWidth(_settings);
            GUILayout.Label("Width: Auto " + _settings.DisplayWidth.ToString("0.##", CultureInfo.InvariantCulture)
                + " (Render " + _settings.RenderWidth + "x" + _settings.RenderHeight + ")");
            DrawSliderWithText("Height", ref _settings.DisplayHeight, 0.1f, 5f, SourceSlider);
            DrawToggleRow("ディスプレイギズモ表示", ref _settings.DisplayGizmoVisible);
            DrawVector3SlidersZoom("Display Pos", ref _settings.DisplayPosX, ref _settings.DisplayPosY, ref _settings.DisplayPosZ, -20f, 20f);
            DrawVector3SlidersZoom("Display Rot", ref _settings.DisplayRotX, ref _settings.DisplayRotY, ref _settings.DisplayRotZ, 0f, 360f);

            // ───── ディスプレイプリセット
            GUILayout.Space(8f);
            DrawDisplayPresetUi();
        }

        private void DrawTransitionEasingUi()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("動き方", GUILayout.Width(90f));
            int current = Mathf.Clamp(_settings.TransitionEasing, 0, TransitionEasingLabels.Length - 1);
            for (int i = 0; i < TransitionEasingLabels.Length; i++)
            {
                Color prevColor = GUI.backgroundColor;
                if (i == current)
                    GUI.backgroundColor = Color.cyan;
                if (GUILayout.Button(TransitionEasingLabels[i], GUILayout.Height(22f)))
                    _settings.TransitionEasing = i;
                GUI.backgroundColor = prevColor;
            }
            GUILayout.EndHorizontal();
        }

        private void DrawAutoCycleUi()
        {
            GUILayout.Label("自動切替");
            DrawToggleRow("自動切替を有効", ref _settings.AutoCycleEnabled);
            DrawSliderWithText("切替間隔(秒)", ref _settings.AutoCycleIntervalSeconds, 1f, 60f, SourceSlider);
            DrawSliderWithText("横槍復帰(秒)", ref _settings.AutoCyclePauseAfterExternalSeconds, 0f, 120f, SourceSlider);
            DrawToggleRow("ランダム順", ref _settings.AutoCycleRandomOrder);
        }

        private void DrawPresetUi()
        {
            GUILayout.Label("【名前付きプリセット / ボーン追従】");
            GUILayout.BeginHorizontal();
            GUILayout.Label("保存名", GUILayout.Width(54f));
            _presetNameBuffer = GUILayout.TextField(_presetNameBuffer ?? string.Empty, GUILayout.Width(220f));
            if (GUILayout.Button("保存", GUILayout.Height(24f)))
                SaveCurrentPreset();
            bool prevEnabled = GUI.enabled;
            GUI.enabled = prevEnabled && !string.IsNullOrWhiteSpace(_presetNameBuffer);
            if (GUILayout.Button("上書き", GUILayout.Height(24f)))
                OverwriteActivePreset();
            GUI.enabled = prevEnabled;
            if (GUILayout.Button("追従解除", GUILayout.Height(24f)))
            {
                ClearBoneFollow();
                SetStatus("ボーン追従解除");
            }
            GUILayout.EndHorizontal();

            _settings.SelectedSaveMode = GUILayout.SelectionGrid(
                Mathf.Clamp(_settings.SelectedSaveMode, 0, SaveModeLabels.Length - 1),
                SaveModeLabels,
                SaveModeLabels.Length);

            if (_settings.SelectedSaveMode == SaveModeBoneLink)
            {
                GUILayout.Label("対象ボーン");
                int prevBoneTarget = _settings.SelectedBoneTarget;
                _settings.SelectedBoneTarget = GUILayout.SelectionGrid(
                    Mathf.Clamp(_settings.SelectedBoneTarget, 0, BoneTargetLabels.Length - 1),
                    BoneTargetLabels,
                    BoneTargetLabels.Length);
                // 追従中に対象ボーンを変えたら、追従先も切り替えてカメラを移動
                if (_settings.SelectedBoneTarget != prevBoneTarget && _boneFollowActive)
                    RetargetActiveBone(_settings.SelectedBoneTarget);
                DrawToggleRow("カメラ位置も保存", ref _settings.SaveBoneCameraPosition);
            }

            DrawToggleRow("体位ごとに上書き保存", ref _settings.SaveCameraPoseOverrides);

            string state = _boneFollowActive
                ? "ボーン追従 " + _activeBonePresetName
                : (string.IsNullOrWhiteSpace(_activeCameraPresetName) ? "通常" : "通常 " + _activeCameraPresetName);
            GUILayout.Label("状態: " + state);

            SubCameraPreset[] presets = _settings.Presets ?? new SubCameraPreset[0];
            for (int i = 0; i < presets.Length; i++)
            {
                SubCameraPreset preset = presets[i];
                if (preset == null)
                    continue;

                bool hasOverrides = preset.UsePoseOverrides && preset.PoseOverrides != null && preset.PoseOverrides.Length > 0;
                string presetKey = preset.Name ?? string.Empty;
                bool expanded = hasOverrides && _expandedPosePresets.Contains(presetKey);

                GUILayout.BeginHorizontal();
                bool nextInclude = GUILayout.Toggle(preset.AutoCycleInclude, GUIContent.none, GUILayout.Width(20f));
                if (nextInclude != preset.AutoCycleInclude)
                {
                    preset.AutoCycleInclude = nextInclude;
                    SaveSettings();
                }
                if (hasOverrides)
                {
                    if (GUILayout.Button(expanded ? "▼" : "▶", GUILayout.Width(24f), GUILayout.Height(24f)))
                    {
                        if (expanded)
                            _expandedPosePresets.Remove(presetKey);
                        else
                            _expandedPosePresets.Add(presetKey);
                        expanded = !expanded;
                    }
                }
                Color prevColor = GUI.backgroundColor;
                if (IsActiveBonePreset(preset))
                    GUI.backgroundColor = Color.red;
                if (GUILayout.Button(GetPresetDisplayName(preset, i), GUILayout.Height(24f)))
                    LoadPreset(preset);
                GUI.backgroundColor = prevColor;
                if (GUILayout.Button("x", GUILayout.Width(28f), GUILayout.Height(24f)))
                {
                    RemovePresetAt(i);
                    GUILayout.EndHorizontal();
                    break;
                }
                GUILayout.EndHorizontal();

                // 展開時：このプリセットに保存された体位一覧（★=今いる体位、[骨]、個別削除）
                if (expanded)
                {
                    TryGetCurrentPoseInfo(out string curPoseKey, out _);
                    CameraPoseOverride[] overrides = preset.PoseOverrides;
                    for (int j = 0; j < overrides.Length; j++)
                    {
                        CameraPoseOverride o = overrides[j];
                        if (o == null)
                            continue;

                        GUILayout.BeginHorizontal();
                        GUILayout.Space(28f);
                        bool isCurrent = !string.IsNullOrEmpty(curPoseKey)
                            && string.Equals(o.Key, curPoseKey, System.StringComparison.Ordinal);
                        string boneTag = string.IsNullOrEmpty(o.BoneName)
                            ? string.Empty
                            : " [" + BoneTargetLabels[Mathf.Clamp(o.BoneTarget, 0, BoneTargetLabels.Length - 1)] + "]";
                        string poseName = string.IsNullOrWhiteSpace(o.DisplayName) ? o.Key : o.DisplayName;
                        GUILayout.Label((isCurrent ? "★ " : "・") + poseName + boneTag);
                        if (GUILayout.Button("削除", GUILayout.Width(50f), GUILayout.Height(20f)))
                        {
                            RemovePoseOverrideAt(preset, j);
                            GUILayout.EndHorizontal();
                            break;
                        }
                        GUILayout.EndHorizontal();
                    }
                }
            }

            GUILayout.Space(4f);
            DrawAutoCycleUi();

            if (!string.IsNullOrWhiteSpace(_statusMessage))
                GUILayout.Label(_statusMessage);
        }

        private void DrawActiveBoneFollowUi()
        {
            bool prevEnabled = GUI.enabled;
            GUI.enabled = prevEnabled && _boneFollowActive;

            GUILayout.Label(_boneFollowActive ? "注視点オフセット" : "注視点オフセット（ボーン追従中のみ）");
            float x = _activeLookAtOffsetLocal.x;
            float y = _activeLookAtOffsetLocal.y;
            float z = _activeLookAtOffsetLocal.z;

            DrawSliderWithTextZoom("Look X", ref x, -2f, 2f, SourceSlider);
            DrawSliderWithTextZoom("Look Y", ref y, -2f, 2f, SourceSlider);
            DrawSliderWithTextZoom("Look Z", ref z, -2f, 2f, SourceSlider);

            GUI.enabled = prevEnabled;

            if (!_boneFollowActive)
                return;

            Vector3 next = new Vector3(Round2(x), Round2(y), Round2(z));
            if ((_activeLookAtOffsetLocal - next).sqrMagnitude <= 0.000001f)
                return;

            _activeLookAtOffsetLocal = next;
            UpdateBoneFollow();
            SaveActiveBonePresetOffsets();
            SetStatus("注視点オフセット更新");
        }

        private void DrawPosePresetUi()
        {
            GUILayout.Label("【体位連動】");
            DrawToggleRow("体位ごとに自動適用", ref _settings.PosePresetAutoApply);

            bool hasPose = TryGetCurrentPoseInfo(out string key, out string displayName);
            string label = hasPose ? (displayName + " (" + key + ")") : "未取得 (Hシーン外)";
            GUILayout.Label("現在の体位: " + label);

            int cameraTagged = CountPoseAwareCameraPresets();
            int displayTagged = CountPoseAwareDisplayPresets();
            GUILayout.Label("体位属性: カメラ " + cameraTagged + "件 / ディスプレイ " + displayTagged + "件");
        }

        private void DrawDisplayPresetUi()
        {
            GUILayout.Label("【ディスプレイプリセット】");
            GUILayout.BeginHorizontal();
            GUILayout.Label("保存名", GUILayout.Width(54f));
            _displayPresetNameBuffer = GUILayout.TextField(_displayPresetNameBuffer ?? string.Empty, GUILayout.Width(220f));
            if (GUILayout.Button("保存", GUILayout.Height(24f)))
                SaveCurrentDisplayPreset();
            bool prevEnabled = GUI.enabled;
            GUI.enabled = prevEnabled && !string.IsNullOrWhiteSpace(_displayPresetNameBuffer);
            if (GUILayout.Button("上書き", GUILayout.Height(24f)))
                OverwriteActiveDisplayPreset();
            GUI.enabled = prevEnabled;
            GUILayout.EndHorizontal();
            DrawToggleRow("体位ごとに上書き保存", ref _settings.SaveDisplayPoseOverrides);

            DisplayPreset[] presets = _settings.DisplayPresets ?? new DisplayPreset[0];
            for (int i = 0; i < presets.Length; i++)
            {
                DisplayPreset preset = presets[i];
                if (preset == null)
                    continue;

                GUILayout.BeginHorizontal();
                Color prevColor = GUI.backgroundColor;
                if (IsActiveDisplayPreset(preset))
                    GUI.backgroundColor = Color.red;
                if (GUILayout.Button(GetDisplayPresetDisplayName(preset, i), GUILayout.Height(24f)))
                    LoadDisplayPreset(preset);
                GUI.backgroundColor = prevColor;
                if (GUILayout.Button("x", GUILayout.Width(28f), GUILayout.Height(24f)))
                {
                    RemoveDisplayPresetAt(i);
                    GUILayout.EndHorizontal();
                    break;
                }
                GUILayout.EndHorizontal();
            }
        }

        private void DrawToggleRow(string label, ref bool value)
        {
            bool next = GUILayout.Toggle(value, label);
            if (next != value)
            {
                value = next;
            }
        }

        private bool DrawSliderWithText(string label, ref float value, float min, float max, string sourceKey)
        {
            string key = "slider." + sourceKey + "." + label;
            bool changed = false;
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(90f));
            float before = value;
            float sliderValue = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(260f));
            bool sliderChanged = Mathf.Abs(sliderValue - before) > 0.0001f;
            if (sliderChanged)
            {
                value = Round2(sliderValue);
                SetNumericBuffer(key, value);
                _sliderDragging = Input.GetMouseButton(0);
                changed = true;
            }

            string raw = DrawNamedNumericField(key, value, 70f);
            GUILayout.EndHorizontal();

            if (!sliderChanged && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                parsed = Mathf.Clamp(parsed, min, max);
                if (Mathf.Abs(parsed - value) > 0.0001f)
                {
                    value = Round2(parsed);
                    changed = true;
                }
            }

            return changed;
        }

        private bool DrawSliderWithTextZoom(string label, ref float value, float min, float max, string sourceKey)
        {
            string key = "slider." + sourceKey + "." + label;
            string zoomKey = "zoom." + key;
            if (!_sliderZoom.TryGetValue(zoomKey, out float zoom))
                zoom = 1f;

            float sliderMin = min;
            float sliderMax = max;
            if (zoom < 1f)
            {
                float window = (max - min) * zoom;
                sliderMin = value - window * 0.5f;
                sliderMax = value + window * 0.5f;
                if (sliderMin < min)
                {
                    sliderMin = min;
                    sliderMax = Mathf.Min(max, sliderMin + window);
                }
                else if (sliderMax > max)
                {
                    sliderMax = max;
                    sliderMin = Mathf.Max(min, sliderMax - window);
                }
            }

            bool changed = false;
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(90f));
            float before = value;
            float sliderValue = GUILayout.HorizontalSlider(value, sliderMin, sliderMax, GUILayout.Width(200f));
            bool sliderChanged = Mathf.Abs(sliderValue - before) > 0.0001f;
            if (sliderChanged)
            {
                value = Round2(Mathf.Clamp(sliderValue, min, max));
                SetNumericBuffer(key, value);
                _sliderDragging = Input.GetMouseButton(0);
                changed = true;
            }

            string raw = DrawNamedNumericField(key, value, 60f);

            if (DrawZoomButton("全", zoom, 1f))
                _sliderZoom[zoomKey] = 1f;
            if (DrawZoomButton("細", zoom, 0.1f))
                _sliderZoom[zoomKey] = 0.1f;
            if (DrawZoomButton("微", zoom, 0.01f))
                _sliderZoom[zoomKey] = 0.01f;
            GUILayout.EndHorizontal();

            if (!sliderChanged && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                parsed = Mathf.Clamp(parsed, min, max);
                if (Mathf.Abs(parsed - value) > 0.0001f)
                {
                    value = Round2(parsed);
                    changed = true;
                }
            }

            return changed;
        }

        private static bool DrawZoomButton(string label, float currentZoom, float targetZoom)
        {
            Color prev = GUI.backgroundColor;
            if (Mathf.Approximately(currentZoom, targetZoom))
                GUI.backgroundColor = Color.cyan;
            bool clicked = GUILayout.Button(label, GUILayout.Width(28f), GUILayout.Height(20f));
            GUI.backgroundColor = prev;
            return clicked;
        }

        private void DrawVector3Fields(string label, ref float x, ref float y, ref float z)
        {
            GUILayout.Label(label);
            GUILayout.BeginHorizontal();
            x = DrawFloatField(label + ".X", "X", x);
            y = DrawFloatField(label + ".Y", "Y", y);
            z = DrawFloatField(label + ".Z", "Z", z);
            GUILayout.EndHorizontal();
        }

        private bool DrawVector3Sliders(string label, ref float x, ref float y, ref float z, float min, float max)
        {
            GUILayout.Label(label);
            bool changed = false;
            changed |= DrawSliderWithText(label + " X", ref x, min, max, SourceSlider);
            changed |= DrawSliderWithText(label + " Y", ref y, min, max, SourceSlider);
            changed |= DrawSliderWithText(label + " Z", ref z, min, max, SourceSlider);
            return changed;
        }

        private bool DrawVector3SlidersZoom(string label, ref float x, ref float y, ref float z, float min, float max)
        {
            GUILayout.Label(label);
            bool changed = false;
            changed |= DrawSliderWithTextZoom(label + " X", ref x, min, max, SourceSlider);
            changed |= DrawSliderWithTextZoom(label + " Y", ref y, min, max, SourceSlider);
            changed |= DrawSliderWithTextZoom(label + " Z", ref z, min, max, SourceSlider);
            return changed;
        }

        private float DrawFloatField(string key, string label, float value)
        {
            GUILayout.Label(label, GUILayout.Width(14f));
            string raw = DrawNamedNumericField("float." + key, value, 60f);
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                return Round2(parsed);
            return value;
        }

        private string DrawNamedNumericField(string key, float value, float width)
        {
            string controlName = "MainGameSubCameraDisplayProbe." + key;
            bool focused = GUI.GetNameOfFocusedControl() == controlName;
            if (!_numericFieldBuffers.TryGetValue(key, out string text) || !focused)
            {
                text = value.ToString("0.##", CultureInfo.InvariantCulture);
                _numericFieldBuffers[key] = text;
            }

            GUI.SetNextControlName(controlName);
            string next = GUILayout.TextField(text, GUILayout.Width(width));
            _numericFieldBuffers[key] = next;
            return next;
        }

        private void SetNumericBuffer(string key, float value)
        {
            _numericFieldBuffers[key] = value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private void UpdateWindowDragState()
        {
            Rect dragArea = new Rect(0f, 0f, 10000f, 24f);
            Event current = Event.current;
            if (current == null)
                return;

            if (current.type == EventType.MouseDown && dragArea.Contains(current.mousePosition))
            {
                _windowDragging = true;
            }
            else if (current.type == EventType.MouseUp)
            {
                if (_windowDragging)
                    _windowDragging = false;
                _sliderDragging = false;
            }
        }

        private void UpdateInputCapture()
        {
            UpdateIdleCursorUnlock();
            bool visible = _settings.UiVisible;
            bool mouseInWindow = visible && IsMouseInGuiRect(_lastWindowRect);
            bool mouseDown = Input.GetMouseButton(0);
            bool sliderActive = visible && mouseDown && GUIUtility.hotControl != 0 && mouseInWindow;
            bool headerActive = visible && mouseDown && IsMouseInGuiRect(new Rect(_lastWindowRect.x, _lastWindowRect.y, _lastWindowRect.width, 24f));
            bool windowActive = visible && (mouseInWindow || sliderActive || headerActive || _windowDragging);

            UiInputCaptureApi.Sync(InputCaptureOwnerKey, SourceWindow, windowActive);
            UiInputCaptureApi.Sync(InputCaptureOwnerKey, SourceSlider, sliderActive || _sliderDragging);
            UiInputCaptureApi.Sync(InputCaptureOwnerKey, SourceGizmo, _gizmoDragging);

            _sliderDragging = sliderActive;
        }

        private void UpdateIdleCursorUnlock()
        {
            UiInputCaptureApi.SetIdleCursorUnlock(InputCaptureOwnerKey, _settings.UiVisible);
        }

        private void ReleaseInputCapture()
        {
            UiInputCaptureApi.SetIdleCursorUnlock(InputCaptureOwnerKey, false);
            UiInputCaptureApi.EndOwner(InputCaptureOwnerKey);
        }

        private void ResetInputCaptureState()
        {
            _windowDragging = false;
            _sliderDragging = false;
            _gizmoDragging = false;
        }

        private static bool IsMouseInGuiRect(Rect guiRect)
        {
            if (guiRect.width <= 0f || guiRect.height <= 0f)
                return false;

            Vector3 mouse = Input.mousePosition;
            Vector2 guiMouse = new Vector2(mouse.x, Screen.height - mouse.y);
            return guiRect.Contains(guiMouse);
        }
    }
}
