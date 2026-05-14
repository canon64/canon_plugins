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

            GUILayout.Label("状態");
            GUILayout.Label("Probe: " + (_rootObject != null ? "生成済み" : "未生成"));
            GUILayout.Label("SubCamera: " + (_subCamera != null ? _subCamera.name : "none"));
            GUILayout.Label("RenderTexture: " + (_renderTexture != null ? (_renderTexture.width + "x" + _renderTexture.height) : "none"));
            GUILayout.Label("FOV: " + _settings.CameraFieldOfView.ToString("0.##", CultureInfo.InvariantCulture));
            GUILayout.Label("UI Capture: " + (UiInputCaptureApi.IsAvailable ? UiInputCaptureApi.GetStateSummary() : "unavailable"));

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("両方破棄", GUILayout.Height(28f)))
                DestroyProbe();
            if (GUILayout.Button("カメラだけ破棄", GUILayout.Height(28f)))
                DestroyCameraOnly();
            if (GUILayout.Button("ディスプレイだけ破棄", GUILayout.Height(28f)))
                DestroyDisplayOnly();
            GUILayout.EndHorizontal();

            if (GUILayout.Button("眼の前に移動", GUILayout.Height(28f)) && _rootObject != null)
            {
                MoveProbeInFrontOfPlayer(initialCreate: false);
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("カメラを眼の前", GUILayout.Height(28f)))
                MoveCameraInFrontOfPlayer();
            if (GUILayout.Button("ディスプレイを眼の前", GUILayout.Height(28f)))
                MoveDisplayInFrontOfPlayer();
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            DrawToggleRow("起動時に自動生成", ref _settings.AutoCreateOnStart);
            DrawToggleRow("カメラギズモ表示", ref _settings.CameraGizmoVisible);
            DrawToggleRow("ディスプレイギズモ表示", ref _settings.DisplayGizmoVisible);
            DrawToggleRow("VR掴み有効", ref _settings.EnableVrGrip);
            DrawToggleRow("VRでカメラを掴む", ref _settings.GripMovesCamera);
            DrawToggleRow("VRでディスプレイを掴む", ref _settings.GripMovesDisplay);
            DrawToggleRow("カメラ掴み中に手元プレビュー表示", ref _settings.ShowHandPreviewWhileGrabbingCamera);
            DrawToggleRow("カメラ掴み中トリガーで写真保存", ref _settings.PhotoCaptureOnTrigger);
            if (GUILayout.Button("写真保存テスト", GUILayout.Height(24f)))
                CaptureSubCameraPhoto("ui-test");
            DrawVideoRecordingUi();

            GUILayout.Space(8f);
            GUILayout.Label("サブカメラ");
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
            DrawToggleRow("Overlay Camera", ref _settings.UseDisplayOverlayCamera);
            DrawSliderWithText("Display Layer", ref _displayLayerFloat, 0f, 30f, SourceSlider);
            _settings.DisplayLayer = Mathf.Clamp(Mathf.RoundToInt(_displayLayerFloat), 0, 30);
            if (DrawSliderWithText("FOV", ref _settings.CameraFieldOfView, 10f, 170f, SourceSlider))
                SyncActiveFovFromUi();
            DrawSliderWithText("Near", ref _settings.CameraNearClip, 0.01f, 1f, SourceSlider);
            DrawSliderWithText("Far", ref _settings.CameraFarClip, 10f, 1000f, SourceSlider);
            DrawToggleRow("拍FOV連動", ref _settings.BeatFovEnabled);
            DrawSliderWithText("拍ズーム倍率", ref _settings.BeatFovZoomMultiplier, 1f, 3f, SourceSlider);
            bool cameraPosChanged = DrawVector3Sliders("Camera Pos", ref _settings.CameraPosX, ref _settings.CameraPosY, ref _settings.CameraPosZ, -20f, 20f);
            bool cameraRotChanged = DrawVector3Sliders("Camera Rot", ref _settings.CameraRotX, ref _settings.CameraRotY, ref _settings.CameraRotZ, 0f, 360f);
            if (cameraPosChanged || cameraRotChanged)
                SyncCameraControlSliders(cameraPosChanged, cameraRotChanged);

            GUILayout.Space(8f);
            GUILayout.Label("遷移移動");
            DrawSliderWithText("遷移時間", ref _settings.TransitionSeconds, 0f, 3f, SourceSlider);
            DrawTransitionEasingUi();

            GUILayout.Space(8f);
            DrawPresetUi();

            GUILayout.Space(8f);
            GUILayout.Label("ディスプレイ");
            _settings.DisplayWidth = SettingsStore.CalculateDisplayWidth(_settings);
            GUILayout.Label("Width: Auto " + _settings.DisplayWidth.ToString("0.##", CultureInfo.InvariantCulture)
                + " (Render " + _settings.RenderWidth + "x" + _settings.RenderHeight + ")");
            DrawSliderWithText("Height", ref _settings.DisplayHeight, 0.1f, 5f, SourceSlider);
            DrawVector3Sliders("Display Pos", ref _settings.DisplayPosX, ref _settings.DisplayPosY, ref _settings.DisplayPosZ, -20f, 20f);
            DrawVector3Sliders("Display Rot", ref _settings.DisplayRotX, ref _settings.DisplayRotY, ref _settings.DisplayRotZ, 0f, 360f);
            DrawDisplayPresetUi();

            GUILayout.Space(8f);
            GUILayout.Label("共通");
            DrawSliderWithText("Front Dist", ref _settings.SpawnDistance, 0.1f, 2f, SourceSlider);
            DrawSliderWithText("Gizmo Size", ref _settings.GizmoSizeMultiplier, 0.2f, 4f, SourceSlider);
            DrawSliderWithText("カメラ見た目サイズ", ref _settings.CameraVisualScale, 0.1f, 3f, SourceSlider);
            DrawSliderWithText("VR Grip Dist", ref _settings.VrGripStartDistance, 0.05f, 1.5f, SourceSlider);
            DrawToggleRow("カメラ掴み中スティック上下でFOV", ref _settings.VrFovStickEnabled);
            DrawSliderWithText("VR FOV Speed", ref _settings.VrFovStickSpeed, 1f, 120f, SourceSlider);
            DrawSliderWithText("VR FOV Dead", ref _settings.VrFovStickDeadzone, 0f, 0.95f, SourceSlider);

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            UpdateWindowDragState();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
            SaveSettings();
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
            DrawAutoCycleUi();
            GUILayout.Space(4f);
            GUILayout.Label("プリセット / ボーン追従");
            GUILayout.BeginHorizontal();
            GUILayout.Label("保存名", GUILayout.Width(54f));
            _presetNameBuffer = GUILayout.TextField(_presetNameBuffer ?? string.Empty, GUILayout.Width(220f));
            if (GUILayout.Button("保存", GUILayout.Height(24f)))
                SaveCurrentPreset();
            bool prevEnabled = GUI.enabled;
            GUI.enabled = prevEnabled && _boneFollowActive;
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
                _settings.SelectedBoneTarget = GUILayout.SelectionGrid(
                    Mathf.Clamp(_settings.SelectedBoneTarget, 0, BoneTargetLabels.Length - 1),
                    BoneTargetLabels,
                    BoneTargetLabels.Length);
                DrawToggleRow("カメラ位置も保存", ref _settings.SaveBoneCameraPosition);
            }

            GUILayout.Label("状態: " + (_boneFollowActive ? ("ボーン追従 " + _activeBonePresetName) : "通常"));
            if (_boneFollowActive)
                DrawActiveBoneFollowUi();

            SubCameraPreset[] presets = _settings.Presets ?? new SubCameraPreset[0];
            for (int i = 0; i < presets.Length; i++)
            {
                SubCameraPreset preset = presets[i];
                if (preset == null)
                    continue;

                GUILayout.BeginHorizontal();
                bool nextInclude = GUILayout.Toggle(preset.AutoCycleInclude, GUIContent.none, GUILayout.Width(20f));
                if (nextInclude != preset.AutoCycleInclude)
                {
                    preset.AutoCycleInclude = nextInclude;
                    SaveSettings();
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
            }

            if (!string.IsNullOrWhiteSpace(_statusMessage))
                GUILayout.Label(_statusMessage);
        }

        private void DrawActiveBoneFollowUi()
        {
            GUILayout.Label("注視点オフセット");
            float x = _activeLookAtOffsetLocal.x;
            float y = _activeLookAtOffsetLocal.y;
            float z = _activeLookAtOffsetLocal.z;

            DrawSliderWithText("Look X", ref x, -2f, 2f, SourceSlider);
            DrawSliderWithText("Look Y", ref y, -2f, 2f, SourceSlider);
            DrawSliderWithText("Look Z", ref z, -2f, 2f, SourceSlider);

            Vector3 next = new Vector3(Round2(x), Round2(y), Round2(z));
            if ((_activeLookAtOffsetLocal - next).sqrMagnitude <= 0.000001f)
                return;

            _activeLookAtOffsetLocal = next;
            UpdateBoneFollow();
            SaveActiveBonePresetOffsets();
            SetStatus("注視点オフセット更新");
        }

        private void DrawDisplayPresetUi()
        {
            GUILayout.Label("ディスプレイプリセット");
            GUILayout.BeginHorizontal();
            GUILayout.Label("保存名", GUILayout.Width(54f));
            _displayPresetNameBuffer = GUILayout.TextField(_displayPresetNameBuffer ?? string.Empty, GUILayout.Width(220f));
            if (GUILayout.Button("保存", GUILayout.Height(24f)))
                SaveCurrentDisplayPreset();
            bool prevEnabled = GUI.enabled;
            GUI.enabled = prevEnabled && !string.IsNullOrWhiteSpace(_activeDisplayPresetName);
            if (GUILayout.Button("上書き", GUILayout.Height(24f)))
                OverwriteActiveDisplayPreset();
            GUI.enabled = prevEnabled;
            GUILayout.EndHorizontal();

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
