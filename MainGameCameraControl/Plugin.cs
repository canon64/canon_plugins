using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using MainGameUiInputCapture;
using MainGameTransformGizmo;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using UnityEngine;

namespace MainGameCameraControl
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInProcess("KoikatsuSunshine")]
    [BepInDependency("com.kks.maingame.uiinputcapture", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.kks.maingame.transformgizmo", BepInDependency.DependencyFlags.SoftDependency)]
    internal sealed partial class Plugin : BaseUnityPlugin
    {
        public const string GUID = "com.kks.maingame.cameracontrol";
        public const string PluginName = "MainGameCameraControl";
        public const string Version = "0.1.0";

        internal static Plugin Instance { get; private set; }
        internal static new ManualLogSource Logger { get; private set; }
        internal static string PluginDir { get; private set; }
        internal static PluginSettings Settings { get; private set; }

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private static readonly object FileLogLock = new object();

        private ConfigEntry<bool> _cfgUiVisible;
        private ConfigEntry<bool> _cfgDetailLogEnabled;
        private ConfigEntry<float> _cfgDefaultFov;
        private ConfigEntry<bool> _cfgApplyFov;
        private ConfigEntry<float> _cfgTransitionSeconds;
        private ConfigEntry<int> _cfgTransitionEasing;
        private ConfigEntry<bool> _cfgGizmoVisible;
        private ConfigEntry<float> _cfgGizmoSize;
        private const string InputOwnerKey = GUID + ".input";
        private const string InputSourceWindow = "window";
        private const float WindowWidth = 420f;
        private const float WindowHeight = 680f;
        private const float WindowDragHandleHeight = 24f;
        private static readonly string[] EasingLabels = { "Linear", "EaseIn", "EaseOut", "EaseInOut" };
        private static readonly string[] BoneTargetLabels = { "頭", "胸", "腰" };
        private static readonly string[] SaveModeLabels = { "通常", "ボーン", "ksFPV" };
        private const int SaveModeNormal = 0;
        private const int SaveModeBoneLink = 1;
        private const int SaveModeKsPlugFpv = 2;
        private const int BoneTargetHead = 0;
        private const int BoneTargetChest = 1;
        private const int BoneTargetWaist = 2;
        private static readonly string[][] BoneTargetNameCandidates =
        {
            new[] { "cf_j_head", "cf_J_Head" },
            new[] { "cf_j_spine03", "cf_J_Spine03", "cf_j_spine02", "cf_J_Spine02" },
            new[] { "cf_j_waist01", "cf_J_Waist01", "cf_j_waist02", "cf_J_Waist02" }
        };

        private readonly List<CameraPreset> _presets = new List<CameraPreset>();
        private HFlag _cachedHFlag;
        private CameraControl_Ver2 _ctrlCamera;
        private BaseCameraControl_Ver2.CameraData _currentData;
        private BaseCameraControl_Ver2.CameraData _targetData;
        private bool _hasCameraController;
        private string _statusMessage;
        private Vector2 _windowContentScroll;
        private string _presetNameBuffer = string.Empty;
        private bool _uiFoldoutPresets = true;
        private bool _uiFoldoutCurrent = true;
        private int _selectedSaveMode;
        private int _selectedBoneTarget;
        private Rect _windowRect;
        private bool _windowPointerActive;
        private bool _windowDragging;
        private bool _transitionActive;
        private float _transitionStartTime;
        private float _transitionDuration;
        private BaseCameraControl_Ver2.CameraData _transitionFrom;
        private BaseCameraControl_Ver2.CameraData _transitionTo;
        private string _pendingFovTraceReason = string.Empty;
        private string _transitionFovTraceReason = string.Empty;
        private int _fovTraceSerial;
        private bool _boneLinkActive;
        private string _activeBoneLinkPresetName = string.Empty;
        private int _activeBoneLinkTarget = -1;
        private string _activeBoneLinkBoneName = string.Empty;
        private Vector3 _activeBoneLinkLookAtOffsetLocal = Vector3.zero;
        private Vector3 _activeBoneLinkCameraOffsetLocal = Vector3.zero;
        private float _activeBoneLinkFov = 0f;
        private bool _activeUseKsPlugFpvLink;
        private GameObject _lookAtAnchorGo;
        private Transform _lookAtAnchor;
        private TransformGizmo _lookAtAnchorGizmo;
        private bool _gizmoVisible;
        private string _pluginLogPath = string.Empty;

        private void Awake()
        {
            Instance = this;
            Logger = base.Logger;
            PluginDir = Path.GetDirectoryName(Info.Location) ?? AppDomain.CurrentDomain.BaseDirectory;
            _pluginLogPath = Path.Combine(PluginDir, "MainGameCameraControl.log");
            ResetPluginLog();

            Settings = SettingsStore.LoadOrCreate(Path.Combine(PluginDir, "CameraControlSettings.json"));
            _cfgUiVisible = Config.Bind("UI", "UiVisible", Settings.UiVisible, "Camera UI visibility.");
            _cfgDetailLogEnabled = Config.Bind("Logging", "DetailLogEnabled", Settings.DetailLogEnabled, "Enable detailed camera logs.");
            _cfgDefaultFov = Config.Bind("Camera", "DefaultFov", Settings.DefaultFov, "Default FOV.");
            _cfgApplyFov = Config.Bind("Camera", "ApplyFov", Settings.ApplyFov, "Apply saved FOV.");
            _cfgTransitionSeconds = Config.Bind("Camera", "TransitionSeconds", Settings.TransitionSeconds, "Transition seconds.");
            _cfgTransitionEasing = Config.Bind("Camera", "TransitionEasing", Settings.TransitionEasing, "Transition easing.");
            _cfgGizmoVisible = Config.Bind("Gizmo", "Visible", Settings.GizmoVisible, "Show look-at gizmo.");
            _cfgGizmoSize = Config.Bind("Gizmo", "Size", Settings.GizmoSize, "Look-at gizmo size.");
            _windowRect = new Rect(Settings.WindowX, Settings.WindowY, WindowWidth, WindowHeight);
            _selectedSaveMode = Mathf.Clamp(
                Settings.SelectedSaveMode != 0 || !Settings.SaveWithBoneLink
                    ? Settings.SelectedSaveMode
                    : SaveModeBoneLink,
                0,
                SaveModeLabels.Length - 1);
            _selectedBoneTarget = Mathf.Clamp(Settings.SelectedBoneTarget, 0, BoneTargetLabels.Length - 1);
            _gizmoVisible = Settings.GizmoVisible;

            SyncFromConfig();
            LoadPresetsFromSettings();
            LogInfo(PluginName + " " + Version + " initialized");
            LogInfo("pluginDir=" + PluginDir);
            LogInfo("settingsPath=" + Path.Combine(PluginDir, "CameraControlSettings.json"));
            LogInfo("logPath=" + _pluginLogPath);
        }

        private void OnDestroy()
        {
            ReleaseInputCapture();
            DestroyLookAtAnchor();
            SaveSettings();
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            UpdateWindowDragState();
            RefreshCameraControllerCache();

            if (!_cfgUiVisible.Value)
            {
                _windowPointerActive = false;
                _windowDragging = false;
            }

            UpdateCameraTransition();
            UpdateKsPlugFpvLinkedCamera();
            UpdateBoneLinkedCamera();
            UpdateInputCapture();
        }

        private void OnGUI()
        {
            if (!_cfgUiVisible.Value)
                return;

            _windowRect.width = WindowWidth;
            _windowRect.height = WindowHeight;
            _windowRect = GUI.Window(
                958121,
                _windowRect,
                DrawUiWindow,
                "MainGameCameraControl");
            HandleWindowPointer(Event.current);
        }

        private void DrawUiWindow(int id)
        {
            bool uiChanged = false;
            const float pad = 8f;
            const float rowH = 22f;
            const float gap = 4f;
            Rect viewport = new Rect(
                pad,
                WindowDragHandleHeight + 6f,
                WindowWidth - pad * 2f,
                WindowHeight - WindowDragHandleHeight - 14f);
            float contentW = viewport.width - 18f;
            float contentH = CalculateUiContentHeight();
            Rect viewRect = new Rect(0f, 0f, contentW, contentH);
            _windowContentScroll = GUI.BeginScrollView(viewport, _windowContentScroll, viewRect, false, true);

            float y = 0f;

            bool nextApplyFov = GUI.Toggle(new Rect(0f, y, contentW, rowH), _cfgApplyFov.Value, "FOVも適用");
            if (nextApplyFov != _cfgApplyFov.Value)
            {
                _cfgApplyFov.Value = nextApplyFov;
                uiChanged = true;
            }
            y += rowH;

            bool nextDetailLogEnabled = GUI.Toggle(new Rect(0f, y, contentW, rowH), _cfgDetailLogEnabled.Value, "詳細ログ");
            if (nextDetailLogEnabled != _cfgDetailLogEnabled.Value)
            {
                _cfgDetailLogEnabled.Value = nextDetailLogEnabled;
                uiChanged = true;
            }
            y += rowH + gap;

            GUI.Label(new Rect(0f, y, contentW, rowH), "遷移時間");
            y += rowH;
            float nextTransitionSeconds = DrawSliderWithField(new Rect(0f, y, contentW, rowH), string.Empty, _cfgTransitionSeconds.Value, 0f, 3f, "F2");
            if (!Mathf.Approximately(nextTransitionSeconds, _cfgTransitionSeconds.Value))
            {
                _cfgTransitionSeconds.Value = nextTransitionSeconds;
                uiChanged = true;
            }
            y += rowH + gap;

            GUI.Label(new Rect(0f, y, contentW, rowH), "イージング");
            y += rowH;
            int nextEasing = GUI.SelectionGrid(
                new Rect(0f, y, contentW, 44f),
                Mathf.Clamp(_cfgTransitionEasing.Value, 0, EasingLabels.Length - 1),
                EasingLabels,
                2);
            if (nextEasing != _cfgTransitionEasing.Value)
            {
                _cfgTransitionEasing.Value = nextEasing;
                uiChanged = true;
            }
            y += 44f + gap;

            GUI.Label(new Rect(0f, y, contentW, rowH), "保存モード");
            y += rowH;
            int nextSaveMode = GUI.SelectionGrid(
                new Rect(0f, y, contentW, rowH),
                Mathf.Clamp(_selectedSaveMode, 0, SaveModeLabels.Length - 1),
                SaveModeLabels,
                3);
            if (nextSaveMode != _selectedSaveMode)
            {
                _selectedSaveMode = nextSaveMode;
                uiChanged = true;
            }
            y += rowH;

            if (_selectedSaveMode == SaveModeBoneLink || _selectedSaveMode == SaveModeKsPlugFpv)
            {
                GUI.Label(new Rect(0f, y, contentW, rowH), "対象ボーン");
                y += rowH;
                int nextBoneTarget = GUI.SelectionGrid(
                    new Rect(0f, y, contentW, rowH),
                    Mathf.Clamp(_selectedBoneTarget, 0, BoneTargetLabels.Length - 1),
                    BoneTargetLabels,
                    3);
                if (nextBoneTarget != _selectedBoneTarget)
                {
                    _selectedBoneTarget = nextBoneTarget;
                    uiChanged = true;
                }
                y += rowH;
            }
            y += gap;

            bool nextGizmoVisible = GUI.Toggle(new Rect(0f, y, contentW, rowH), _gizmoVisible, "ギズモ表示");
            if (nextGizmoVisible != _gizmoVisible)
            {
                _gizmoVisible = nextGizmoVisible;
                if (_cfgGizmoVisible != null)
                    _cfgGizmoVisible.Value = nextGizmoVisible;
                ApplyLookAtAnchorGizmoState();
                uiChanged = true;
            }
            y += rowH;

            GUI.Label(new Rect(0f, y, contentW, rowH), "ギズモサイズ");
            y += rowH;
            float nextGizmoSize = DrawSliderWithField(new Rect(0f, y, contentW, rowH), string.Empty, _cfgGizmoSize.Value, 0.2f, 1.5f, "F2");
            if (!Mathf.Approximately(nextGizmoSize, _cfgGizmoSize.Value))
            {
                _cfgGizmoSize.Value = nextGizmoSize;
                ApplyLookAtAnchorGizmoState();
                uiChanged = true;
            }
            y += rowH + gap;

            if (_boneLinkActive)
            {
                GUI.Label(new Rect(0f, y, contentW, rowH), "座標系: " + GetGizmoAxisSpaceLabel());
                y += rowH;
                GUI.Label(new Rect(0f, y, contentW, rowH), "注視点オフセット");
                y += rowH;
                uiChanged |= DrawBoneLinkOffsetSlider(new Rect(0f, y, contentW, rowH), "X", 0);
                y += rowH;
                uiChanged |= DrawBoneLinkOffsetSlider(new Rect(0f, y, contentW, rowH), "Y", 1);
                y += rowH;
                uiChanged |= DrawBoneLinkOffsetSlider(new Rect(0f, y, contentW, rowH), "Z", 2);
                y += rowH + gap;
            }

            _uiFoldoutCurrent = GUI.Toggle(new Rect(0f, y, contentW, rowH), _uiFoldoutCurrent, "現在値");
            y += rowH;
            if (_uiFoldoutCurrent)
            {
                GUI.Label(new Rect(0f, y, contentW, rowH), _hasCameraController ? "CameraControl_Ver2: OK" : "CameraControl_Ver2: 未取得");
                y += rowH;
                GUI.Label(new Rect(0f, y, contentW, rowH), FormatVector("Target", _currentData.Pos));
                y += rowH;
                GUI.Label(new Rect(0f, y, contentW, rowH), FormatVector("Dir", _currentData.Dir));
                y += rowH;
                GUI.Label(new Rect(0f, y, contentW, rowH), FormatVector("Rot", _currentData.Rot));
                y += rowH;
                GUI.Label(new Rect(0f, y, contentW, rowH), "Fov: " + _currentData.Fov.ToString("0.##", CultureInfo.InvariantCulture));
                y += rowH;
            }
            y += gap;

            GUI.Label(new Rect(0f, y, 60f, rowH), "保存名");
            _presetNameBuffer = GUI.TextField(new Rect(64f, y, contentW - 64f, rowH), _presetNameBuffer ?? string.Empty);
            y += rowH + gap;

            if (GUI.Button(new Rect(0f, y, 90f, rowH), "現在を保存"))
                SaveCurrentAsPreset();

            bool prevEnabled = GUI.enabled;
            GUI.enabled = _boneLinkActive;
            if (GUI.Button(new Rect(96f, y, 90f, rowH), "上書き保存"))
                OverwriteActiveBoneLinkedPreset();
            GUI.enabled = prevEnabled;
            if (GUI.Button(new Rect(192f, y, 60f, rowH), "解除"))
                ClearBoneLinkActiveState();
            if (GUI.Button(new Rect(258f, y, 70f, rowH), "Reset"))
                ResetCameraFollowState();
            y += rowH + gap;

            if (GUI.Button(new Rect(0f, y, 140f, rowH), "Reset+右Shift"))
                ResetAndPressRightShift();
            if (GUI.Button(new Rect(146f, y, 120f, rowH), "右Shift"))
                PressRightShiftKey();
            y += rowH + gap;

            _uiFoldoutPresets = GUI.Toggle(new Rect(0f, y, contentW, rowH), _uiFoldoutPresets, "保存済み");
            y += rowH;
            if (_uiFoldoutPresets)
            {
                for (int i = 0; i < _presets.Count; i++)
                {
                    CameraPreset preset = _presets[i];
                    if (preset == null)
                        continue;

                    Rect rowRect = new Rect(0f, y, contentW, 22f);
                    Color prevColor = GUI.backgroundColor;
                    if (IsActiveBoneLinkedPreset(preset))
                        GUI.backgroundColor = Color.red;

                    if (GUI.Button(new Rect(rowRect.x, rowRect.y, rowRect.width - 30f, rowRect.height), GetPresetDisplayName(preset, i)))
                        LoadPreset(preset);

                    GUI.backgroundColor = prevColor;
                    if (GUI.Button(new Rect(rowRect.xMax - 26f, rowRect.y, 26f, rowRect.height), "x"))
                    {
                        RemovePresetAt(i);
                        break;
                    }
                    y += 26f;
                }
                y += gap;
            }

            if (!string.IsNullOrWhiteSpace(_statusMessage))
            {
                GUI.Label(new Rect(0f, y, contentW, 40f), _statusMessage);
                y += 40f;
            }

            GUI.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, WindowWidth, WindowDragHandleHeight));

            if (uiChanged)
                SaveSettings();
        }

        private float CalculateUiContentHeight()
        {
            const float rowH = 22f;
            const float gap = 4f;
            float h = 0f;
            h += rowH * 2f + gap;
            h += rowH * 2f + gap;
            h += rowH + 44f + gap;
            h += rowH * 2f;
            if (_selectedSaveMode == SaveModeBoneLink || _selectedSaveMode == SaveModeKsPlugFpv)
                h += rowH * 2f;
            h += gap;
            h += rowH * 2f + gap;
            if (_boneLinkActive)
                h += rowH * 5f + gap;
            h += rowH;
            if (_uiFoldoutCurrent)
                h += rowH * 5f;
            h += gap;
            h += rowH + gap;
            h += rowH + gap;
            h += rowH + gap;
            h += rowH;
            if (_uiFoldoutPresets)
                h += Mathf.Max(rowH, _presets.Count * 26f) + gap;
            if (!string.IsNullOrWhiteSpace(_statusMessage))
                h += 40f;
            return Mathf.Max(h + 8f, WindowHeight - WindowDragHandleHeight - 14f);
        }

        private float DrawSliderWithField(Rect rect, string label, float value, float min, float max, string format = "F2")
        {
            float x = rect.x;
            float sliderW = rect.width - 68f;
            if (!string.IsNullOrEmpty(label))
            {
                GUI.Label(new Rect(rect.x, rect.y, 18f, rect.height), label);
                x += 20f;
                sliderW -= 20f;
            }

            float next = GUI.HorizontalSlider(new Rect(x, rect.y + 3f, sliderW, rect.height), value, min, max);
            string txt = GUI.TextField(new Rect(rect.xMax - 60f, rect.y, 60f, rect.height), value.ToString(format, CultureInfo.InvariantCulture));
            if (float.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                float clamped = Mathf.Clamp(parsed, min, max);
                if (!Mathf.Approximately(clamped, value))
                    return clamped;
            }
            return Mathf.Clamp(next, min, max);
        }

        private bool DrawBoneLinkOffsetSlider(Rect rect, string label, int axis)
        {
            float current = GetBoneLinkOffsetAxis(axis);
            float next = DrawSliderWithField(rect, label, current, -2f, 2f, "F2");

            if (Mathf.Approximately(next, current))
                return false;

            SetBoneLinkOffsetAxis(axis, next);
            return true;
        }

        private void SyncFromConfig()
        {
            Settings.UiVisible = _cfgUiVisible.Value;
            Settings.DetailLogEnabled = _cfgDetailLogEnabled.Value;
            Settings.DefaultFov = Mathf.Clamp(_cfgDefaultFov.Value, 5f, 120f);
            Settings.ApplyFov = _cfgApplyFov.Value;
            Settings.TransitionSeconds = Mathf.Clamp(_cfgTransitionSeconds.Value, 0f, 3f);
            Settings.TransitionEasing = Mathf.Clamp(_cfgTransitionEasing.Value, 0, EasingLabels.Length - 1);
            Settings.WindowX = _windowRect.x;
            Settings.WindowY = _windowRect.y;
            Settings.SaveWithBoneLink = _selectedSaveMode != SaveModeNormal;
            Settings.SelectedBoneTarget = Mathf.Clamp(_selectedBoneTarget, 0, BoneTargetLabels.Length - 1);
            Settings.SelectedSaveMode = Mathf.Clamp(_selectedSaveMode, 0, SaveModeLabels.Length - 1);
            Settings.GizmoVisible = _gizmoVisible;
            Settings.GizmoSize = Mathf.Clamp(_cfgGizmoSize.Value, 0.2f, 1.5f);
        }

        private void RefreshCameraControllerCache()
        {
            CameraControl_Ver2 ctrl = ResolveCtrlCamera();
            if (ctrl == null)
            {
                _hasCameraController = false;
                _ctrlCamera = null;
                return;
            }

            if (_ctrlCamera != ctrl)
            {
                _ctrlCamera = ctrl;
                _hasCameraController = true;
                _currentData = ctrl.GetCameraData();
                _targetData = _currentData;
                SetStatus("CameraControl_Ver2 を取得");
                return;
            }

            _hasCameraController = true;
        }

        private CameraControl_Ver2 ResolveCtrlCamera()
        {
            if (_cachedHFlag == null)
                _cachedHFlag = UnityEngine.Object.FindObjectOfType<HFlag>();

            if (_cachedHFlag != null && _cachedHFlag.ctrlCamera != null)
                return _cachedHFlag.ctrlCamera;

            return UnityEngine.Object.FindObjectOfType<CameraControl_Ver2>();
        }

        private void ApplyCameraData(BaseCameraControl_Ver2.CameraData data, string fovTraceReason = null)
        {
            if (_ctrlCamera == null)
                return;

            float requestedFov = Mathf.Clamp(data.Fov <= 0f ? Settings.DefaultFov : data.Fov, 5f, 120f);
            float beforeDataFov = _ctrlCamera.GetCameraData().Fov;
            float beforeCameraFov = _ctrlCamera.thisCmaera != null ? _ctrlCamera.thisCmaera.fieldOfView : -1f;
            data.Fov = requestedFov;
            _ctrlCamera.SetCameraData(data);
            _ctrlCamera.CameraFov = requestedFov;
            if (_ctrlCamera.thisCmaera != null)
                _ctrlCamera.thisCmaera.fieldOfView = requestedFov;

            if (!string.IsNullOrEmpty(fovTraceReason))
            {
                LogDebug(
                    "apply-fov"
                    + " requested=" + requestedFov.ToString("0.##", CultureInfo.InvariantCulture)
                    + " beforeData=" + beforeDataFov.ToString("0.##", CultureInfo.InvariantCulture)
                    + " beforeCamera=" + beforeCameraFov.ToString("0.##", CultureInfo.InvariantCulture)
                    + " afterData=" + _ctrlCamera.GetCameraData().Fov.ToString("0.##", CultureInfo.InvariantCulture)
                    + " afterCamera=" + (_ctrlCamera.thisCmaera != null ? _ctrlCamera.thisCmaera.fieldOfView.ToString("0.##", CultureInfo.InvariantCulture) : "none"));
                StartCoroutine(TraceFovAfterApply(++_fovTraceSerial, fovTraceReason, requestedFov));
            }
        }

        private void SaveCurrentAsPreset()
        {
            RefreshCameraControllerCache();
            if (!_hasCameraController || _ctrlCamera == null)
            {
                SetStatus("CameraControl_Ver2 が見つからない");
                return;
            }

            BaseCameraControl_Ver2.CameraData data = _ctrlCamera.GetCameraData();
            data.Fov = ResolveLiveCameraFov(data.Fov);
            string name = string.IsNullOrWhiteSpace(_presetNameBuffer)
                ? $"Preset {(_presets.Count + 1)}"
                : _presetNameBuffer.Trim();

            CameraPreset preset;
            switch (_selectedSaveMode)
            {
                case SaveModeBoneLink:
                    preset = BuildBoneLinkedPreset(name, data);
                    break;
                case SaveModeKsPlugFpv:
                    preset = BuildKsPlugFpvPreset(name, data);
                    break;
                default:
                    preset = new CameraPreset
                    {
                        Name = name,
                        TargetPosition = ToArray(data.Pos),
                        CameraDirection = ToArray(data.Dir),
                        Rotation = ToArray(data.Rot),
                        Fov = data.Fov
                    };
                    break;
            }

            if (preset == null)
            {
                SetStatus("ボーン連携保存に失敗");
                return;
            }

            _presets.Add(preset);
            CommitPresets("save-current");
            _currentData = data;
            _targetData = data;
            LogCameraData("save-preset", data, preset);
            SetStatus("保存: " + name);
        }

        private void LoadPreset(CameraPreset preset)
        {
            if (preset == null)
                return;

            RefreshCameraControllerCache();
            if (!_hasCameraController || _ctrlCamera == null)
            {
                SetStatus("CameraControl_Ver2 が見つからない");
                return;
            }

            _currentData = _ctrlCamera.GetCameraData();
            _targetData = BuildCameraDataFromPreset(preset, _currentData);
            if (!_cfgApplyFov.Value)
                _targetData.Fov = _currentData.Fov;

            if (preset.UseKsPlugFpvLink)
            {
                _boneLinkActive = true;
                _activeUseKsPlugFpvLink = true;
                _activeBoneLinkPresetName = preset.Name ?? string.Empty;
                _activeBoneLinkTarget = preset.BoneTarget;
                _activeBoneLinkBoneName = preset.BoneName ?? string.Empty;
                _activeBoneLinkLookAtOffsetLocal = GetPresetBoneLinkOffsetLocal(preset);
                _activeBoneLinkCameraOffsetLocal = GetPresetCameraOffsetLocal(preset);
                _activeBoneLinkFov = preset.Fov;
                InvokeKsPlugFpvAction();
                LogCameraData("load-current", _currentData, preset);
                LogCameraData("load-target", _targetData, preset);
                SetStatus("呼び出し: " + preset.Name);
                return;
            }

            if (preset.UseBoneLink)
            {
                _boneLinkActive = true;
                _activeUseKsPlugFpvLink = false;
                _activeBoneLinkPresetName = preset.Name ?? string.Empty;
                _activeBoneLinkTarget = preset.BoneTarget;
                _activeBoneLinkBoneName = preset.BoneName ?? string.Empty;
                _activeBoneLinkLookAtOffsetLocal = GetPresetBoneLinkOffsetLocal(preset);
                _activeBoneLinkCameraOffsetLocal = Vector3.zero;
                _activeBoneLinkFov = preset.Fov;
            }
            else
            {
                ClearBoneLinkActiveState();
            }

            LogCameraData("load-current", _currentData, preset);
            LogCameraData("load-target", _targetData, preset);
            _pendingFovTraceReason =
                "load"
                + " preset=" + (preset.Name ?? "(null)")
                + " presetFov=" + preset.Fov.ToString("0.##", CultureInfo.InvariantCulture)
                + " targetFov=" + _targetData.Fov.ToString("0.##", CultureInfo.InvariantCulture)
                + " currentFov=" + _currentData.Fov.ToString("0.##", CultureInfo.InvariantCulture)
                + " applyFov=" + _cfgApplyFov.Value
                + " boneLink=" + preset.UseBoneLink;
            StartCameraTransition(_currentData, _targetData);
            SetStatus("呼び出し: " + preset.Name);
        }

        private void RemovePresetAt(int index)
        {
            if (index < 0 || index >= _presets.Count)
                return;

            string removed = _presets[index]?.Name;
            _presets.RemoveAt(index);
            CommitPresets("remove:" + removed);
            SetStatus("削除: " + removed);
        }

        private void LoadPresetsFromSettings()
        {
            _presets.Clear();
            if (Settings.Presets != null)
                _presets.AddRange(Settings.Presets.Where(x => x != null));
            if (_presets.Count == 0)
                _presetNameBuffer = "Main";
        }

        private void CommitPresets(string reason)
        {
            Settings.Presets = _presets.ToList();
            SaveSettings();
            LogInfo("camera preset commit reason=" + reason + " count=" + _presets.Count);
        }

        private void SaveSettings()
        {
            try
            {
                Settings.UiVisible = _cfgUiVisible.Value;
                Settings.DetailLogEnabled = _cfgDetailLogEnabled.Value;
                Settings.DefaultFov = _cfgDefaultFov.Value;
                Settings.ApplyFov = _cfgApplyFov.Value;
                Settings.TransitionSeconds = _cfgTransitionSeconds.Value;
                Settings.TransitionEasing = _cfgTransitionEasing.Value;
                Settings.WindowX = _windowRect.x;
                Settings.WindowY = _windowRect.y;
                Settings.SaveWithBoneLink = _selectedSaveMode != SaveModeNormal;
                Settings.SelectedBoneTarget = _selectedBoneTarget;
                Settings.SelectedSaveMode = _selectedSaveMode;
                Settings.GizmoVisible = _gizmoVisible;
                Settings.GizmoSize = _cfgGizmoSize.Value;
                Settings.Presets = _presets.ToList();
                SettingsStore.Save(Path.Combine(PluginDir, "CameraControlSettings.json"), Settings);
            }
            catch (Exception ex)
            {
                LogWarn("camera settings save failed: " + ex.Message);
            }
        }

        private void SetStatus(string text)
        {
            _statusMessage = text;
            LogInfo("camera: " + text);
        }

        private static Vector3 ToVector3(float[] values, Vector3 fallback)
        {
            if (values == null || values.Length < 3)
                return fallback;
            return new Vector3(values[0], values[1], values[2]);
        }

        private static Vector3 ToVector3(float[] values)
        {
            return ToVector3(values, Vector3.zero);
        }

        private static string FormatVector(string label, Vector3 value)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}: {1:0.##}, {2:0.##}, {3:0.##}",
                label, value.x, value.y, value.z);
        }

        private static float[] ToArray(Vector3 value)
        {
            return new[] { value.x, value.y, value.z };
        }

        private BaseCameraControl_Ver2.CameraData BuildCameraDataFromPreset(
            CameraPreset preset,
            BaseCameraControl_Ver2.CameraData fallback)
        {
            if (preset != null && preset.UseKsPlugFpvLink && TryBuildKsPlugFpvCameraData(preset, fallback, out BaseCameraControl_Ver2.CameraData ksPlugLinked))
                return ksPlugLinked;

            if (preset != null && preset.UseBoneLink && TryBuildBoneLinkedCameraData(preset, fallback, out BaseCameraControl_Ver2.CameraData boneLinked))
                return boneLinked;

            BaseCameraControl_Ver2.CameraData data = fallback;
            Vector3 legacyTarget = ToVector3(preset.LegacyLookAt, fallback.Pos);
            data.Pos = ToVector3(preset.TargetPosition, legacyTarget);
            data.Rot = ToVector3(preset.Rotation, fallback.Rot);

            Vector3 legacyCameraPosition = ToVector3(preset.LegacyPosition, Vector3.zero);
            bool hasLegacyCameraPosition = preset.LegacyPosition != null && preset.LegacyPosition.Length >= 3;
            if (preset.CameraDirection != null && preset.CameraDirection.Length >= 3)
            {
                data.Dir = ToVector3(preset.CameraDirection, fallback.Dir);
            }
            else if (hasLegacyCameraPosition)
            {
                Quaternion rot = Quaternion.Euler(data.Rot);
                data.Dir = Quaternion.Inverse(rot) * (legacyCameraPosition - data.Pos);
            }
            else
            {
                data.Dir = fallback.Dir;
            }

            data.Fov = Mathf.Clamp(preset.Fov <= 0f ? fallback.Fov : preset.Fov, 5f, 120f);
            return data;
        }

        private CameraPreset BuildBoneLinkedPreset(string name, BaseCameraControl_Ver2.CameraData data)
        {
            ChaControl female = ResolveMainFemale();
            if (female == null)
                return null;

            Transform[] boneCache = GetFemaleBoneCache(female);
            Transform bone = ResolveBoneTarget(boneCache, _selectedBoneTarget);
            if (bone == null)
                return null;

            return new CameraPreset
            {
                Name = name,
                UseBoneLink = true,
                BoneTarget = _selectedBoneTarget,
                BoneName = bone.name,
                LookAtOffsetLocal = ToArray(GetCurrentBoneLinkOffsetLocal()),
                CameraOffsetWorld = null,
                RotationOffset = null,
                Fov = data.Fov,
                TargetPosition = ToArray(bone.position),
                CameraDirection = ToArray(data.Dir),
                Rotation = ToArray(data.Rot)
            };
        }

        private CameraPreset BuildKsPlugFpvPreset(string name, BaseCameraControl_Ver2.CameraData data)
        {
            if (_ctrlCamera == null || _ctrlCamera.thisCmaera == null)
                return null;

            ChaControl female = ResolveMainFemale();
            if (female == null)
                return null;

            Transform[] boneCache = GetFemaleBoneCache(female);
            Transform bone = ResolveBoneTarget(boneCache, _selectedBoneTarget);
            if (bone == null)
                return null;

            Transform fpvBase = ResolveKsPlugFpvBaseTransform();
            if (fpvBase == null)
                return null;

            Vector3 cameraOffsetLocal = fpvBase.InverseTransformPoint(_ctrlCamera.thisCmaera.transform.position);
            Vector3 lookAtOffsetLocal = bone.InverseTransformPoint(GetCurrentLookAtAnchorPosition(bone));

            return new CameraPreset
            {
                Name = name,
                UseBoneLink = true,
                UseKsPlugFpvLink = true,
                BoneTarget = _selectedBoneTarget,
                BoneName = bone.name,
                LookAtOffsetLocal = ToArray(lookAtOffsetLocal),
                CameraOffsetLocal = ToArray(cameraOffsetLocal),
                Fov = data.Fov,
                TargetPosition = ToArray(bone.position),
                CameraDirection = ToArray(data.Dir),
                Rotation = ToArray(data.Rot)
            };
        }

        private bool TryBuildBoneLinkedCameraData(
            CameraPreset preset,
            BaseCameraControl_Ver2.CameraData fallback,
            out BaseCameraControl_Ver2.CameraData result)
        {
            result = fallback;
            if (_ctrlCamera == null)
                return false;

            ChaControl female = ResolveMainFemaleStatic();
            if (female == null)
                return false;

            Transform[] boneCache = GetFemaleBoneCacheStatic(female);
            Transform bone = ResolveBoneTargetStatic(boneCache, preset.BoneTarget, preset.BoneName);
            if (bone == null)
                return false;

            Vector3 lookAtOffsetLocal = GetPresetBoneLinkOffsetLocal(preset);
            Vector3 lookAt = bone.TransformPoint(lookAtOffsetLocal);
            Vector3 cameraPosition = _ctrlCamera.transform.position;
            Quaternion finalRotation = BuildLookRotation(lookAt - cameraPosition, _ctrlCamera.transform.rotation);

            BaseCameraControl_Ver2.CameraData data = fallback;
            if (_ctrlCamera.transBase != null)
            {
                data.Pos = _ctrlCamera.transBase.InverseTransformPoint(lookAt);
                data.Rot = NormalizeEuler((Quaternion.Inverse(_ctrlCamera.transBase.rotation) * finalRotation).eulerAngles);
            }
            else
            {
                data.Pos = lookAt;
                data.Rot = NormalizeEuler(finalRotation.eulerAngles);
            }

            data.Dir = Quaternion.Inverse(finalRotation) * (cameraPosition - lookAt);
            data.Fov = Mathf.Clamp(preset.Fov <= 0f ? fallback.Fov : preset.Fov, 5f, 120f);
            result = data;
            return true;
        }

        private bool TryBuildKsPlugFpvCameraData(
            CameraPreset preset,
            BaseCameraControl_Ver2.CameraData fallback,
            out BaseCameraControl_Ver2.CameraData result)
        {
            result = fallback;
            if (_ctrlCamera == null)
                return false;

            Transform fpvBase = ResolveKsPlugFpvBaseTransform();
            if (fpvBase == null)
                return false;

            ChaControl female = ResolveMainFemaleStatic();
            if (female == null)
                return false;

            Transform[] boneCache = GetFemaleBoneCacheStatic(female);
            Transform bone = ResolveBoneTargetStatic(boneCache, preset.BoneTarget, preset.BoneName);
            if (bone == null)
                return false;

            Vector3 lookAt = bone.TransformPoint(GetPresetBoneLinkOffsetLocal(preset));
            Vector3 cameraPosition = fpvBase.TransformPoint(GetPresetCameraOffsetLocal(preset));
            Quaternion finalRotation = BuildLookRotation(lookAt - cameraPosition, _ctrlCamera.transform.rotation);

            BaseCameraControl_Ver2.CameraData data = fallback;
            data.Pos = fpvBase.InverseTransformPoint(lookAt);
            data.Rot = NormalizeEuler((Quaternion.Inverse(fpvBase.rotation) * finalRotation).eulerAngles);
            data.Dir = Quaternion.Inverse(finalRotation) * (cameraPosition - lookAt);
            data.Fov = Mathf.Clamp(preset.Fov <= 0f ? fallback.Fov : preset.Fov, 5f, 120f);
            result = data;
            return true;
        }

        private static Quaternion BuildLookRotation(Vector3 forward, Quaternion fallback)
        {
            if (forward.sqrMagnitude <= 0.000001f)
                return fallback;
            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private static Vector3 NormalizeEuler(Vector3 euler)
        {
            return new Vector3(NormalizeAngle(euler.x), NormalizeAngle(euler.y), NormalizeAngle(euler.z));
        }

        private static float NormalizeAngle(float angle)
        {
            float normalized = angle;
            while (normalized > 180f) normalized -= 360f;
            while (normalized < -180f) normalized += 360f;
            return normalized;
        }

        private ChaControl ResolveMainFemale()
        {
            return ResolveMainFemaleStatic();
        }

        private static ChaControl ResolveMainFemaleStatic()
        {
            HSceneProc proc = UnityEngine.Object.FindObjectOfType<HSceneProc>();
            if (proc != null)
            {
                var listField = HarmonyLib.AccessTools.Field(typeof(HSceneProc), "lstFemale");
                IList listObj = listField != null ? listField.GetValue(proc) as IList : null;
                if (listObj != null)
                {
                    for (int i = 0; i < listObj.Count; i++)
                    {
                        ChaControl cha = listObj[i] as ChaControl;
                        if (cha != null)
                            return cha;
                    }
                }
            }

            ChaControl[] all = UnityEngine.Object.FindObjectsOfType<ChaControl>();
            for (int i = 0; i < all.Length; i++)
            {
                ChaControl cha = all[i];
                if (cha != null && cha.sex == 1)
                    return cha;
            }

            return null;
        }

        private Transform[] GetFemaleBoneCache(ChaControl female)
        {
            return GetFemaleBoneCacheStatic(female);
        }

        private static Transform[] GetFemaleBoneCacheStatic(ChaControl female)
        {
            if (female == null)
                return null;

            Transform root = female.animBody != null
                ? female.animBody.transform
                : female.transform;
            return root != null ? root.GetComponentsInChildren<Transform>(true) : null;
        }

        private Transform ResolveBoneTarget(Transform[] boneCache, int boneTarget)
        {
            return ResolveBoneTargetStatic(boneCache, boneTarget, null);
        }

        private static Transform ResolveBoneTargetStatic(Transform[] boneCache, int boneTarget, string preferredName)
        {
            if (boneCache == null)
                return null;

            if (!string.IsNullOrWhiteSpace(preferredName))
            {
                Transform exact = FindBoneByName(boneCache, preferredName);
                if (exact != null)
                    return exact;
            }

            int normalizedTarget = Mathf.Clamp(boneTarget, 0, BoneTargetNameCandidates.Length - 1);
            string[] candidates = BoneTargetNameCandidates[normalizedTarget];
            for (int i = 0; i < candidates.Length; i++)
            {
                Transform found = FindBoneByName(boneCache, candidates[i]);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static Transform FindBoneByName(Transform[] boneCache, string name)
        {
            if (boneCache == null || string.IsNullOrWhiteSpace(name))
                return null;

            for (int i = 0; i < boneCache.Length; i++)
            {
                Transform current = boneCache[i];
                if (current != null && string.Equals(current.name, name, StringComparison.Ordinal))
                    return current;
            }

            return null;
        }

        private void UpdateBoneLinkedCamera()
        {
            if (!_boneLinkActive || _activeUseKsPlugFpvLink || _transitionActive || _ctrlCamera == null)
                return;

            ChaControl female = ResolveMainFemale();
            if (female == null)
                return;

            Transform[] boneCache = GetFemaleBoneCache(female);
            Transform bone = ResolveBoneTargetStatic(boneCache, _activeBoneLinkTarget, _activeBoneLinkBoneName);
            if (bone == null)
                return;

            EnsureLookAtAnchor();
            if (_lookAtAnchor == null)
                return;

            bool isDraggingAnchor = _lookAtAnchorGizmo != null && _lookAtAnchorGizmo.IsDragging;
            _lookAtAnchor.rotation = bone.rotation;
            if (isDraggingAnchor)
                _activeBoneLinkLookAtOffsetLocal = bone.InverseTransformPoint(_lookAtAnchor.position);
            else
                _lookAtAnchor.position = bone.TransformPoint(_activeBoneLinkLookAtOffsetLocal);

            Vector3 cameraPosition = _ctrlCamera.transform.position;
            Vector3 lookAt = _lookAtAnchor.position;
            Quaternion finalRotation = BuildLookRotation(lookAt - cameraPosition, _ctrlCamera.transform.rotation);

            BaseCameraControl_Ver2.CameraData data = _ctrlCamera.GetCameraData();
            if (_ctrlCamera.transBase != null)
            {
                data.Pos = _ctrlCamera.transBase.InverseTransformPoint(lookAt);
                data.Rot = NormalizeEuler((Quaternion.Inverse(_ctrlCamera.transBase.rotation) * finalRotation).eulerAngles);
            }
            else
            {
                data.Pos = lookAt;
                data.Rot = NormalizeEuler(finalRotation.eulerAngles);
            }

            data.Dir = Quaternion.Inverse(finalRotation) * (cameraPosition - lookAt);
            if (_cfgApplyFov.Value)
                data.Fov = Mathf.Clamp(_activeBoneLinkFov > 0f ? _activeBoneLinkFov : _currentData.Fov, 5f, 120f);
            else
                data.Fov = _currentData.Fov;

            ApplyCameraData(data);
            _currentData = data;
            _targetData = data;
        }

        private void UpdateKsPlugFpvLinkedCamera()
        {
            if (!_boneLinkActive || !_activeUseKsPlugFpvLink || _transitionActive || _ctrlCamera == null)
                return;

            Transform fpvBase = ResolveKsPlugFpvBaseTransform();
            if (fpvBase == null)
                return;

            ChaControl female = ResolveMainFemale();
            if (female == null)
                return;

            Transform[] boneCache = GetFemaleBoneCache(female);
            Transform bone = ResolveBoneTargetStatic(boneCache, _activeBoneLinkTarget, _activeBoneLinkBoneName);
            if (bone == null)
                return;

            EnsureLookAtAnchor();
            if (_lookAtAnchor == null)
                return;

            bool isDraggingAnchor = _lookAtAnchorGizmo != null && _lookAtAnchorGizmo.IsDragging;
            _lookAtAnchor.rotation = bone.rotation;
            if (isDraggingAnchor)
                _activeBoneLinkLookAtOffsetLocal = bone.InverseTransformPoint(_lookAtAnchor.position);
            else
                _lookAtAnchor.position = bone.TransformPoint(_activeBoneLinkLookAtOffsetLocal);

            Vector3 cameraPosition = fpvBase.TransformPoint(_activeBoneLinkCameraOffsetLocal);
            Vector3 lookAt = _lookAtAnchor.position;
            Quaternion finalRotation = BuildLookRotation(lookAt - cameraPosition, _ctrlCamera.transform.rotation);

            BaseCameraControl_Ver2.CameraData data = _ctrlCamera.GetCameraData();
            data.Pos = fpvBase.InverseTransformPoint(lookAt);
            data.Rot = NormalizeEuler((Quaternion.Inverse(fpvBase.rotation) * finalRotation).eulerAngles);
            data.Dir = Quaternion.Inverse(finalRotation) * (cameraPosition - lookAt);
            if (_cfgApplyFov.Value)
                data.Fov = Mathf.Clamp(_activeBoneLinkFov > 0f ? _activeBoneLinkFov : _currentData.Fov, 5f, 120f);
            else
                data.Fov = _currentData.Fov;

            ApplyCameraData(data);
            _currentData = data;
            _targetData = data;
        }

        private void ResetCameraFollowState()
        {
            _transitionActive = false;
            ClearBoneLinkActiveState();
            if (_ctrlCamera != null)
            {
                _currentData = _ctrlCamera.GetCameraData();
                _targetData = _currentData;
            }
            SetStatus("Reset");
        }

        private void PressRightShiftKey()
        {
            try
            {
                string result = InvokeKsPlugFpvAction();
                LogInfo("ksplug fpv action: " + result);
                SetStatus("右Shift: " + result);
            }
            catch (Exception ex)
            {
                LogWarn("ksplug fpv action failed: " + ex.Message);
                SetStatus("右Shift失敗");
            }
        }

        private void ResetAndPressRightShift()
        {
            ResetCameraFollowState();
            PressRightShiftKey();
            SetStatus("Reset+右Shift");
        }

        private Transform ResolveKsPlugFpvBaseTransform()
        {
            if (_ctrlCamera != null && _ctrlCamera.transBase != null)
                return _ctrlCamera.transBase;

            return null;
        }

        private string InvokeKsPlugFpvAction()
        {
            Assembly ksPlugAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, "ksPlug", StringComparison.OrdinalIgnoreCase));
            if (ksPlugAssembly == null)
                return "ksPlug未読込";

            Type hCtrlType = ksPlugAssembly.GetType("ksPlug.HSession.HCtrl", throwOnError: false);
            if (hCtrlType == null)
                return "HCtrl型なし";

            PropertyInfo getProperty = hCtrlType.GetProperty("Get", BindingFlags.Public | BindingFlags.Static);
            object hCtrl = getProperty != null ? getProperty.GetValue(null, null) : null;
            if (hCtrl == null)
                return "HCtrlなし";

            PropertyInfo fpvCtrlProperty = hCtrlType.GetProperty("FpvCtrl", BindingFlags.Public | BindingFlags.Instance);
            object fpvCtrl = fpvCtrlProperty != null ? fpvCtrlProperty.GetValue(hCtrl, null) : null;

            if (fpvCtrl == null)
            {
                MethodInfo addFpv = hCtrlType.GetMethod("AddFPV", BindingFlags.Public | BindingFlags.Instance);
                if (addFpv == null)
                    return "AddFPVなし";

                addFpv.Invoke(hCtrl, null);
                return "FPV開始";
            }

            MethodInfo resetFpv = hCtrlType.GetMethod("ResetFpv", BindingFlags.Public | BindingFlags.Instance);
            if (resetFpv == null)
                return "ResetFpvなし";

            ParameterInfo[] parameters = resetFpv.GetParameters();
            object[] args = parameters.Length == 1 ? new object[] { false } : null;
            resetFpv.Invoke(hCtrl, args);
            return "FPVリセット";
        }

        private bool IsActiveBoneLinkedPreset(CameraPreset preset)
        {
            if (!_boneLinkActive || preset == null || (!preset.UseBoneLink && !preset.UseKsPlugFpvLink))
                return false;

            return string.Equals(preset.Name ?? string.Empty, _activeBoneLinkPresetName ?? string.Empty, StringComparison.Ordinal)
                && preset.BoneTarget == _activeBoneLinkTarget
                && string.Equals(preset.BoneName ?? string.Empty, _activeBoneLinkBoneName ?? string.Empty, StringComparison.Ordinal)
                && preset.UseKsPlugFpvLink == _activeUseKsPlugFpvLink;
        }

        private void ClearBoneLinkActiveState()
        {
            _boneLinkActive = false;
            _activeBoneLinkPresetName = string.Empty;
            _activeBoneLinkTarget = -1;
            _activeBoneLinkBoneName = string.Empty;
            _activeBoneLinkLookAtOffsetLocal = Vector3.zero;
            _activeBoneLinkCameraOffsetLocal = Vector3.zero;
            _activeBoneLinkFov = 0f;
            _activeUseKsPlugFpvLink = false;
            DestroyLookAtAnchor();
            SetStatus("ボーン連動解除");
        }

        private void OverwriteActiveBoneLinkedPreset()
        {
            if (!_boneLinkActive)
            {
                SetStatus("ボーン連動中ではない");
                return;
            }

            CameraPreset preset = _presets.FirstOrDefault(IsActiveBoneLinkedPreset);
            if (preset == null)
            {
                SetStatus("上書き対象が見つからない");
                return;
            }

            RefreshCameraControllerCache();
            if (!_hasCameraController || _ctrlCamera == null)
            {
                SetStatus("CameraControl_Ver2 が見つからない");
                return;
            }

            BaseCameraControl_Ver2.CameraData data = _ctrlCamera.GetCameraData();
            data.Fov = ResolveLiveCameraFov(data.Fov);
            preset.UseBoneLink = true;
            preset.UseKsPlugFpvLink = _activeUseKsPlugFpvLink;
            preset.BoneTarget = _activeBoneLinkTarget;
            preset.BoneName = _activeBoneLinkBoneName ?? string.Empty;
            preset.LookAtOffsetLocal = ToArray(_activeBoneLinkLookAtOffsetLocal);
            preset.CameraOffsetLocal = _activeUseKsPlugFpvLink ? ToArray(_activeBoneLinkCameraOffsetLocal) : null;
            preset.TargetPosition = ToArray(data.Pos);
            preset.CameraDirection = ToArray(data.Dir);
            preset.Rotation = ToArray(data.Rot);
            preset.Fov = data.Fov;
            _activeBoneLinkFov = data.Fov;

            CommitPresets("overwrite:" + preset.Name);
            LogCameraData("overwrite-preset", data, preset);
            SetStatus("上書き保存: " + preset.Name);
        }

        private float ResolveLiveCameraFov(float fallback)
        {
            if (_ctrlCamera != null && _ctrlCamera.thisCmaera != null)
            {
                float live = _ctrlCamera.thisCmaera.fieldOfView;
                if (!float.IsNaN(live) && !float.IsInfinity(live) && live > 0f)
                    return Mathf.Clamp(live, 5f, 120f);
            }

            return Mathf.Clamp(fallback <= 0f ? Settings.DefaultFov : fallback, 5f, 120f);
        }

        private Vector3 GetCurrentLookAtAnchorPosition(Transform bone)
        {
            if (_lookAtAnchor != null)
                return _lookAtAnchor.position;

            return bone.TransformPoint(GetCurrentBoneLinkOffsetLocal());
        }

        private float GetBoneLinkOffsetAxis(int axis)
        {
            switch (axis)
            {
                case 0:
                    return _activeBoneLinkLookAtOffsetLocal.x;
                case 1:
                    return _activeBoneLinkLookAtOffsetLocal.y;
                case 2:
                    return _activeBoneLinkLookAtOffsetLocal.z;
                default:
                    return 0f;
            }
        }

        private void SetBoneLinkOffsetAxis(int axis, float value)
        {
            switch (axis)
            {
                case 0:
                    _activeBoneLinkLookAtOffsetLocal.x = value;
                    break;
                case 1:
                    _activeBoneLinkLookAtOffsetLocal.y = value;
                    break;
                case 2:
                    _activeBoneLinkLookAtOffsetLocal.z = value;
                    break;
            }

            if (_lookAtAnchor != null)
            {
                ChaControl female = ResolveMainFemale();
                if (female == null)
                    return;

                Transform[] boneCache = GetFemaleBoneCache(female);
                Transform bone = ResolveBoneTargetStatic(boneCache, _activeBoneLinkTarget, _activeBoneLinkBoneName);
                if (bone != null)
                {
                    _lookAtAnchor.position = bone.TransformPoint(_activeBoneLinkLookAtOffsetLocal);
                    _lookAtAnchor.rotation = bone.rotation;
                }
            }
        }

        private string GetGizmoAxisSpaceLabel()
        {
            if (_lookAtAnchorGizmo == null)
                return "未生成";

            return _lookAtAnchorGizmo.AxisSpace == GizmoAxisSpace.Local ? "Local" : "World";
        }

        private Vector3 GetPresetBoneLinkOffsetLocal(CameraPreset preset)
        {
            if (preset == null)
                return Vector3.zero;

            return ToVector3(preset.LookAtOffsetLocal, Vector3.zero);
        }

        private Vector3 GetPresetCameraOffsetLocal(CameraPreset preset)
        {
            if (preset == null)
                return Vector3.zero;

            return ToVector3(preset.CameraOffsetLocal, ToVector3(preset.CameraOffsetWorld, Vector3.zero));
        }

        private Vector3 GetCurrentBoneLinkOffsetLocal()
        {
            return _boneLinkActive ? _activeBoneLinkLookAtOffsetLocal : Vector3.zero;
        }

        private void EnsureLookAtAnchor()
        {
            if (_lookAtAnchorGo != null && _lookAtAnchor != null)
            {
                ApplyLookAtAnchorGizmoState();
                return;
            }

            _lookAtAnchorGo = new GameObject("MainGameCameraControl_LookAtAnchor");
            _lookAtAnchorGo.hideFlags = HideFlags.HideAndDontSave;
            _lookAtAnchor = _lookAtAnchorGo.transform;

            if (TransformGizmoApi.IsAvailable)
            {
                _lookAtAnchorGizmo = TransformGizmoApi.Attach(_lookAtAnchorGo);
                if (_lookAtAnchorGizmo != null)
                {
                    _lookAtAnchorGizmo.SetMode(GizmoMode.Move);
                    _lookAtAnchorGizmo.SetAxisSpace(GizmoAxisSpace.Local);
                }
            }

            ApplyLookAtAnchorGizmoState();
        }

        private void ApplyLookAtAnchorGizmoState()
        {
            if (_lookAtAnchorGizmo == null)
                return;

            _lookAtAnchorGizmo.SetFollowActive(_boneLinkActive);
            _lookAtAnchorGizmo.SetVisible(_boneLinkActive && _gizmoVisible);
            TransformGizmoApi.SetSizeMultiplier(_lookAtAnchorGizmo, Mathf.Clamp(_cfgGizmoSize.Value, 0.2f, 1.5f));
        }

        private void DestroyLookAtAnchor()
        {
            _lookAtAnchorGizmo = null;
            _lookAtAnchor = null;
            if (_lookAtAnchorGo == null)
                return;

            UnityEngine.Object.Destroy(_lookAtAnchorGo);
            _lookAtAnchorGo = null;
        }

        private void LogInfo(string message)
        {
            Logger?.LogInfo(message);
            WritePluginLog("INFO", message);
        }

        private void LogWarn(string message)
        {
            Logger?.LogWarning(message);
            WritePluginLog("WARN", message);
        }

        private void LogDebug(string message)
        {
            if (_cfgDetailLogEnabled == null || !_cfgDetailLogEnabled.Value)
                return;
            Logger?.LogInfo("[detail] " + message);
            WritePluginLog("DEBUG", message);
        }

        private void ResetPluginLog()
        {
            try
            {
                string dir = Path.GetDirectoryName(_pluginLogPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(_pluginLogPath, string.Empty, Utf8NoBom);
                WritePluginLog("INFO", "=== session start ===");
            }
            catch
            {
            }
        }

        private void WritePluginLog(string level, string message)
        {
            if (string.IsNullOrWhiteSpace(_pluginLogPath))
                return;

            try
            {
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
                    + " [" + level + "] " + message + Environment.NewLine;
                lock (FileLogLock)
                    File.AppendAllText(_pluginLogPath, line, Utf8NoBom);
            }
            catch
            {
            }
        }

        private void LogCameraData(string label, BaseCameraControl_Ver2.CameraData data, CameraPreset preset)
        {
            if (_cfgDetailLogEnabled == null || !_cfgDetailLogEnabled.Value)
                return;

            string presetName = preset != null ? (preset.Name ?? "(null)") : "(none)";
            string boneName = preset != null ? (preset.BoneName ?? "-") : "-";
            string transBaseInfo = _ctrlCamera != null && _ctrlCamera.transBase != null
                ? FormatVector("transBasePos", _ctrlCamera.transBase.position) + " / " + FormatVector("transBaseRot", _ctrlCamera.transBase.rotation.eulerAngles)
                : "transBase:none";

            LogDebug(
                label
                + " preset=" + presetName
                + " bone=" + boneName
                + " " + FormatVector("Pos", data.Pos)
                + " " + FormatVector("Dir", data.Dir)
                + " " + FormatVector("Rot", data.Rot)
                + " Fov=" + data.Fov.ToString("0.##", CultureInfo.InvariantCulture)
                + " camWorld=" + (_ctrlCamera != null ? FormatVector("cam", _ctrlCamera.transform.position) : "cam:none")
                + " " + transBaseInfo);
        }

        private IEnumerator TraceFovAfterApply(int serial, string reason, float requestedFov)
        {
            LogFovTrace(serial, "after-apply", reason, requestedFov);
            yield return null;
            LogFovTrace(serial, "after-1f", reason, requestedFov);
            for (int i = 1; i < 5; i++)
                yield return null;
            LogFovTrace(serial, "after-5f", reason, requestedFov);
            for (int i = 5; i < 30; i++)
                yield return null;
            LogFovTrace(serial, "after-30f", reason, requestedFov);
        }

        private void LogFovTrace(int serial, string stage, string reason, float requestedFov)
        {
            Camera liveCamera = _ctrlCamera != null ? _ctrlCamera.thisCmaera : null;
            Camera mainCamera = Camera.main;
            bool sameMain = liveCamera != null && mainCamera != null && ReferenceEquals(liveCamera, mainCamera);
            float dataFov = _ctrlCamera != null ? _ctrlCamera.GetCameraData().Fov : -1f;
            float controlFov = _ctrlCamera != null ? _ctrlCamera.CameraFov : -1f;

            LogInfo(
                "[fov-trace #" + serial + "]"
                + " stage=" + stage
                + " frame=" + Time.frameCount
                + " reason=" + reason
                + " requested=" + requestedFov.ToString("0.##", CultureInfo.InvariantCulture)
                + " data=" + dataFov.ToString("0.##", CultureInfo.InvariantCulture)
                + " control=" + controlFov.ToString("0.##", CultureInfo.InvariantCulture)
                + " live=" + FormatCameraFov(liveCamera)
                + " main=" + FormatCameraFov(mainCamera)
                + " sameMain=" + sameMain);
        }

        private static string FormatCameraFov(Camera camera)
        {
            if (camera == null)
                return "none";

            return (camera.name ?? "(unnamed)")
                + "#" + camera.GetInstanceID()
                + ":" + camera.fieldOfView.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string GetPresetDisplayName(CameraPreset preset, int index)
        {
            string name = string.IsNullOrWhiteSpace(preset?.Name) ? $"Preset {index + 1}" : preset.Name;
            if (preset == null)
                return name;
            if (preset.UseKsPlugFpvLink)
                return "ksFPV: " + name;
            if (preset.UseBoneLink)
                return "ボーン: " + name;
            return name;
        }

        public static bool TryGetUiVisible(out bool visible)
        {
            visible = false;
            if (Instance == null)
                return false;
            visible = Instance._cfgUiVisible != null && Instance._cfgUiVisible.Value;
            return true;
        }

        public static bool TrySetUiVisible(bool visible)
        {
            if (Instance == null || Instance._cfgUiVisible == null)
                return false;
            Instance._cfgUiVisible.Value = visible;
            Plugin.Settings.UiVisible = visible;
            Instance.SaveSettings();
            return true;
        }

        private void HandleWindowPointer(Event evt)
        {
            if (evt == null)
                return;

            bool insideWindow = _windowRect.Contains(evt.mousePosition);
            _windowPointerActive = insideWindow;

            if (!insideWindow)
                return;

            if (evt.isMouse || evt.type == EventType.ScrollWheel)
                evt.Use();
        }

        private void UpdateWindowDragState()
        {
            if (!_cfgUiVisible.Value)
            {
                _windowDragging = false;
                return;
            }

            Vector2 mouseGui = GetMousePositionInGuiSpace();
            _windowPointerActive = _windowRect.Contains(mouseGui);

            if (Input.GetMouseButtonDown(0) && GetTitleBarRect().Contains(mouseGui))
            {
                _windowDragging = true;
                return;
            }

            if (!Input.GetMouseButton(0))
                _windowDragging = false;
        }

        private Rect GetTitleBarRect()
        {
            return new Rect(_windowRect.x, _windowRect.y, _windowRect.width, WindowDragHandleHeight);
        }

        private static Vector2 GetMousePositionInGuiSpace()
        {
            Vector3 mouse = Input.mousePosition;
            return new Vector2(mouse.x, Screen.height - mouse.y);
        }

        private void UpdateInputCapture()
        {
            if (!UiInputCaptureApi.IsAvailable)
                return;

            UiInputCaptureApi.SetIdleCursorUnlock(InputOwnerKey, _cfgUiVisible.Value);
            UiInputCaptureApi.Sync(InputOwnerKey, InputSourceWindow, _windowPointerActive || _windowDragging);
        }

        private void ReleaseInputCapture()
        {
            if (!UiInputCaptureApi.IsAvailable)
                return;

            UiInputCaptureApi.SetIdleCursorUnlock(InputOwnerKey, false);
            UiInputCaptureApi.EndOwner(InputOwnerKey);
            _windowPointerActive = false;
            _windowDragging = false;
        }

        private void StartCameraTransition(
            BaseCameraControl_Ver2.CameraData from,
            BaseCameraControl_Ver2.CameraData to)
        {
            float duration = Mathf.Clamp(_cfgTransitionSeconds.Value, 0f, 3f);
            string fovTraceReason = _pendingFovTraceReason;
            _pendingFovTraceReason = string.Empty;
            _transitionFovTraceReason = string.Empty;
            if (duration <= 0.0001f)
            {
                _transitionActive = false;
                ApplyCameraData(to, fovTraceReason);
                _currentData = to;
                _targetData = to;
                return;
            }

            _transitionFrom = from;
            _transitionTo = to;
            _transitionDuration = duration;
            _transitionStartTime = Time.unscaledTime;
            _transitionFovTraceReason = fovTraceReason;
            _transitionActive = true;
            ApplyCameraData(from);
        }

        private void UpdateCameraTransition()
        {
            if (!_transitionActive || _ctrlCamera == null)
                return;

            float rawT = Mathf.Clamp01((Time.unscaledTime - _transitionStartTime) / Mathf.Max(0.0001f, _transitionDuration));
            float easedT = EvaluateEasing(rawT, _cfgTransitionEasing.Value);
            BaseCameraControl_Ver2.CameraData step = LerpCameraData(_transitionFrom, _transitionTo, easedT);
            ApplyCameraData(step);
            _currentData = step;

            if (rawT >= 1f)
            {
                _transitionActive = false;
                _currentData = _transitionTo;
                _targetData = _transitionTo;
                ApplyCameraData(_transitionTo, _transitionFovTraceReason);
                _transitionFovTraceReason = string.Empty;
            }
        }

        private static BaseCameraControl_Ver2.CameraData LerpCameraData(
            BaseCameraControl_Ver2.CameraData from,
            BaseCameraControl_Ver2.CameraData to,
            float t)
        {
            BaseCameraControl_Ver2.CameraData data = from;
            data.Pos = Vector3.Lerp(from.Pos, to.Pos, t);
            data.Dir = Vector3.Lerp(from.Dir, to.Dir, t);
            data.Rot = new Vector3(
                Mathf.LerpAngle(from.Rot.x, to.Rot.x, t),
                Mathf.LerpAngle(from.Rot.y, to.Rot.y, t),
                Mathf.LerpAngle(from.Rot.z, to.Rot.z, t));
            data.Fov = Mathf.Lerp(from.Fov, to.Fov, t);
            return data;
        }

        private static float EvaluateEasing(float t, int easing)
        {
            switch (Mathf.Clamp(easing, 0, EasingLabels.Length - 1))
            {
                case 1:
                    return t * t;
                case 2:
                    return 1f - ((1f - t) * (1f - t));
                case 3:
                    return t < 0.5f
                        ? 2f * t * t
                        : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                default:
                    return t;
            }
        }
    }
}
