using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using MainGameTransformGizmo;
using MainGameUiInputCapture;
using UnityEngine;

namespace MainGameSubCameraDisplayProbe
{
    [BepInPlugin(Guid, PluginName, Version)]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    [BepInDependency(MainGameTransformGizmo.Plugin.GUID, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(MainGameUiInputCapture.Plugin.Guid, BepInDependency.DependencyFlags.HardDependency)]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.kks.maingame.subcameradisplayprobe";
        public const string PluginName = "MainGameSubCameraDisplayProbe";
        public const string Version = "0.1.0";

        internal static Plugin Instance { get; private set; }

        private const int WindowId = 0x53434450;
        private const string InputCaptureOwnerKey = Guid + ".input";
        private const string SourceWindow = "window-drag";
        private const string SourceSlider = "slider-drag";
        private const string SourceGizmo = "gizmo-drag";
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private static readonly string[] SaveModeLabels = { "通常", "ボーン" };
        private static readonly string[] BoneTargetLabels = { "頭", "胸", "腰" };
        private static readonly RenderResolutionPreset[] BuiltInRenderResolutionPresets =
        {
            new RenderResolutionPreset { Name = "16:9 640x360", Width = 640, Height = 360 },
            new RenderResolutionPreset { Name = "16:9 1280x720", Width = 1280, Height = 720 },
            new RenderResolutionPreset { Name = "16:9 1920x1080", Width = 1920, Height = 1080 },
            new RenderResolutionPreset { Name = "9:16 360x640", Width = 360, Height = 640 },
            new RenderResolutionPreset { Name = "9:16 720x1280", Width = 720, Height = 1280 },
            new RenderResolutionPreset { Name = "9:16 1080x1920", Width = 1080, Height = 1920 }
        };
        private static readonly string[][] BoneTargetNameCandidates =
        {
            new[] { "cf_j_head", "cf_J_Head" },
            new[] { "cf_j_spine03", "cf_J_Spine03", "cf_j_spine02", "cf_J_Spine02" },
            new[] { "cf_j_waist01", "cf_J_Waist01", "cf_j_waist02", "cf_J_Waist02" }
        };

        private ProbeSettings _settings;
        private string _pluginDir;
        private string _settingsPath;
        private string _logPath;

        private ConfigEntry<bool> _cfgEnabled;
        private ConfigEntry<bool> _cfgShowWindow;
        private ConfigEntry<KeyboardShortcut> _cfgToggleUiKey;

        private Rect _windowRect;
        private Rect _lastWindowRect;
        private Vector2 _windowScroll;
        private readonly Dictionary<string, string> _numericFieldBuffers = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _sliderZoom = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly HashSet<string> _emittedInfoMessages = new HashSet<string>(StringComparer.Ordinal);
        private string _presetNameBuffer = string.Empty;
        private string _displayPresetNameBuffer = string.Empty;
        private string _statusMessage = string.Empty;
        private bool _cameraUiOpen = true;
        private bool _displayUiOpen = true;
        private bool _windowDragging;
        private bool _sliderDragging;
        private bool _gizmoDragging;
        private float _displayLayerFloat;
        private GameObject _rootObject;
        private GameObject _cameraAnchorObject;
        private GameObject _displayAnchorObject;
        private GameObject _cameraVisualObject;
        private GameObject _cameraLensObject;
        private GameObject _displayObject;
        private GameObject _handPreviewObject;
        private GameObject _displayOverlayCameraObject;
        private Camera _subCamera;
        private Camera _displayOverlayCamera;
        private RenderTexture _renderTexture;
        private Material _displayMaterial;
        private Material _handPreviewMaterial;
        private TransformGizmo _cameraGizmo;
        private TransformGizmo _displayGizmo;
        private bool _boneFollowActive;
        private string _activeCameraPresetName = string.Empty;
        private string _activeBonePresetName = string.Empty;
        private int _activeBoneTarget = -1;
        private string _activeBoneName = string.Empty;
        private bool _renderResolutionDropdownOpen;
        private int _customRenderWidth;
        private int _customRenderHeight;
        private string _customRenderPresetNameBuffer = string.Empty;
        private bool _activeSaveCameraPosition;
        private Vector3 _activeLookAtOffsetLocal = Vector3.zero;
        private Vector3 _activeCameraOffsetLocal = Vector3.zero;
        private float _activePresetFov = 60f;
        private string _activeDisplayPresetName = string.Empty;

        private void Awake()
        {
            Instance = this;
            _pluginDir = Path.GetDirectoryName(Info.Location) ?? string.Empty;
            _settingsPath = Path.Combine(_pluginDir, "MainGameSubCameraDisplayProbeSettings.json");
            _logPath = Path.Combine(_pluginDir, PluginName + ".log");

            _settings = SettingsStore.LoadOrCreate(_settingsPath, LogInfo, LogWarn);
            _windowRect = new Rect(_settings.WindowX, _settings.WindowY, _settings.WindowWidth, _settings.WindowHeight);
            _displayLayerFloat = _settings.DisplayLayer;
            _presetNameBuffer = _settings.PresetName ?? "SubCamera";
            _displayPresetNameBuffer = _settings.DisplayPresetName ?? "Display";
            _customRenderWidth = _settings.RenderWidth;
            _customRenderHeight = _settings.RenderHeight;
            _customRenderPresetNameBuffer = "Custom";

            _cfgEnabled = Config.Bind("General", "Enabled", _settings.Enabled, "プラグインを有効にする");
            _cfgShowWindow = Config.Bind("UI", "ShowWindow", _settings.UiVisible, "UIを表示する");
            _cfgToggleUiKey = Config.Bind("UI", "ToggleUiKey", new KeyboardShortcut(_settings.ToggleUiKey), "UI表示切替キー");

            _cfgEnabled.SettingChanged += (_, __) =>
            {
                _settings.Enabled = _cfgEnabled.Value;
                SaveSettings();
                ApplyRuntimeState();
            };
            _cfgShowWindow.SettingChanged += (_, __) =>
            {
                _settings.UiVisible = _cfgShowWindow.Value;
                SaveSettings();
                UpdateIdleCursorUnlock();
            };
            _cfgToggleUiKey.SettingChanged += (_, __) =>
            {
                _settings.ToggleUiKey = _cfgToggleUiKey.Value.MainKey;
                SaveSettings();
            };

            LogInfo("loaded");
            EnsurePhotoOutputDirectory();
            ApplyRuntimeState();
        }

        private void OnDestroy()
        {
            StopVideoRecording("plugin-destroy");
            ReleaseInputCapture();
            DisableCharactersOnlyHook();
            DestroyProbe();
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            HandleToggleKey();

            if (!_settings.Enabled)
            {
                DisableRuntimeState("plugin-disabled");
                return;
            }

            if (_rootObject == null && _settings.AutoCreateOnStart)
                EnsureProbe();

            bool cameraDragging = (_cameraGizmo != null && _cameraGizmo.IsDragging) || IsHoldingCameraAnchor();
            if (cameraDragging && _transitionActive)
                CancelTransition("gizmo-drag");

            UpdateCameraTransition();

            if (!cameraDragging && !_transitionActive)
                UpdateBoneFollow();

            if (_subCamera != null && !cameraDragging && !_transitionActive)
                ApplyCameraSettings();

            UpdateBeatFovRuntime();
            UpdateAutoCycle();
            UpdatePosePresetTracking();

            if (_displayObject != null && !(_displayGizmo != null && _displayGizmo.IsDragging))
                ApplyDisplaySettings();

            UpdateVrGrab();
            UpdateVideoRecording();
            SyncGizmoState();
            SyncDisplayOverlayCamera();
            SyncCharactersOnlyHook();
            UpdateInputCapture();
        }

        private void OnGUI()
        {
            if (!_settings.UiVisible)
                return;

            _windowRect = GUI.Window(WindowId, _windowRect, DrawWindow, PluginName);
        }

        public static bool TryGetUiVisible(out bool visible)
        {
            visible = false;
            Plugin inst = Instance;
            if (inst == null)
                return false;

            visible = inst._cfgShowWindow != null
                ? inst._cfgShowWindow.Value
                : inst._settings != null && inst._settings.UiVisible;
            return true;
        }

        public static bool TrySetUiVisible(bool visible)
        {
            Plugin inst = Instance;
            if (inst == null)
                return false;

            if (inst._cfgShowWindow != null)
            {
                inst._cfgShowWindow.Value = visible;
            }
            else if (inst._settings != null)
            {
                inst._settings.UiVisible = visible;
                inst.SaveSettings();
                inst.UpdateIdleCursorUnlock();
            }

            inst.LogInfo("ui visible set via api visible=" + visible);
            return true;
        }

        private void HandleToggleKey()
        {
            if (_cfgToggleUiKey != null && _cfgToggleUiKey.Value.IsDown())
            {
                bool visible = !_cfgShowWindow.Value;
                _cfgShowWindow.Value = visible;
            }
        }

        private void ApplyRuntimeState()
        {
            if (_settings.Enabled)
            {
                if (_settings.AutoCreateOnStart)
                    EnsureProbe();
            }
            else
            {
                DisableRuntimeState("plugin-disabled");
            }
        }

        private void DisableRuntimeState(string reason)
        {
            StopVideoRecording(reason);
            StopGrip();
            DisableCharactersOnlyHook();
            ResetInputCaptureState();
            ReleaseInputCapture();
            if (_rootObject != null)
                DestroyProbe();
        }

        private void LogInfo(string message)
        {
            if (!ShouldEmitInfoLog(message))
                return;

            Logger.LogInfo("[" + PluginName + "] " + message);
            WriteLog("INFO", message);
        }

        private bool ShouldEmitInfoLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            return _emittedInfoMessages.Add(message);
        }

        private void LogWarn(string message)
        {
            Logger.LogWarning("[" + PluginName + "] " + message);
            WriteLog("WARN", message);
        }

        private void WriteLog(string level, string message)
        {
            try
            {
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + level + "] " + message;
                File.AppendAllText(_logPath, line + Environment.NewLine, Utf8NoBom);
            }
            catch
            {
            }
        }

        private void SaveSettings()
        {
            _settings.WindowX = Round2(_windowRect.x);
            _settings.WindowY = Round2(_windowRect.y);
            _settings.WindowWidth = Round2(_windowRect.width);
            _settings.WindowHeight = Round2(_windowRect.height);
            _settings.PresetName = _presetNameBuffer ?? string.Empty;
            _settings.DisplayPresetName = _displayPresetNameBuffer ?? string.Empty;
            SettingsStore.Save(_settingsPath, _settings, LogWarn);
        }

        private static float Round2(float value)
        {
            return (float)Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }
    }
}
