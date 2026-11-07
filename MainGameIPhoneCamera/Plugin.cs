using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using CanonGate;
using MainGameUnifiedUi;
using UnityEngine;

namespace MainGameIPhoneCamera
{
    [BepInPlugin(Guid, "MainGameIPhoneCamera", "1.3.3")]
    [BepInDependency(GateApi.PluginGuid, "2.4.0")]
    [BepInDependency("com.kks.maingame.unifiedui")]
    [BepInDependency("com.kks.maingame.uiinputcapture")]
    [BepInDependency("com.kks.maingame.transformgizmo")]
    [BepInDependency("com.bepis.bepinex.sideloader")]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.kks.maingame.iphonecamera";
        private IDisposable _registration;
        private GateApi.Permit _permit;
        private MonoBehaviour _scene;
        private bool _active;
        private ulong _lastSession;
        private ConfigEntry<bool> _enabled;
        private ConfigEntry<int> _height;
        private ConfigEntry<int> _movieHeight, _movieFps;
        private ConfigEntry<string> _ffmpegPath;
        private ConfigEntry<float> _fov, _grabRadius;
        private ConfigEntry<Vector3> _lensPosition, _lensRotation;
        private string _status = "フリーHの開始待ち";

        private bool CanAct => _active && isActiveAndEnabled && _enabled.Value && _permit.IsValid;

        private void Awake()
        {
            _mainThread = System.Threading.SynchronizationContext.Current;
            BindPhoneSettings();
            _movieHeight = Config.Bind("Video", "Height", 1080, new ConfigDescription("動画の長辺。録画開始時の縦・横を終了まで固定して保存。", new AcceptableValueList<int>(720, 1080, 1920)));
            _movieFps = Config.Bind("Video", "FPS", 30, new ConfigDescription("録画フレームレート。変更は次回録画から。", new AcceptableValueList<int>(24, 30, 60)));
            _ffmpegPath = Config.Bind("Video", "FFmpegPath", @"J:\system_tools\ffmpeg\bin\ffmpeg.exe", "音声付きMP4を保存するFFmpeg実行ファイル。");
            _enabled = Config.Bind("General", "Enabled", true, "iPhoneカメラを有効にします。再有効化は次のHから反映。");
            _height = Config.Bind("Camera", "PhotoHeight", 1920, new ConfigDescription("写真の長辺。撮影時の縦・横に合わせてPNGを保存。", new AcceptableValueRange<int>(480, 3840)));
            _fov = Config.Bind("Camera", "VerticalFov", 60f, new ConfigDescription("縦方向の撮影画角。", new AcceptableValueRange<float>(20f, 100f)));
            _grabRadius = Config.Bind("VR", "GrabRadius", .14f, new ConfigDescription("iPhoneをグリップで拾える距離（m）。", new AcceptableValueRange<float>(.03f, .4f)));
            _lensPosition = Config.Bind("Camera", "LensPosition", new Vector3(0.02f, 0.05f, -0.01f), "本体中央を基準にした背面カメラの位置。");
            _lensRotation = Config.Bind("Camera", "LensRotation", new Vector3(0f, 180f, 0f), "カメラの本体に対する角度。");
            _enabled.SettingChanged += EnabledChanged;
            _height.SettingChanged += CameraSettingsChanged;
            _fov.SettingChanged += CameraSettingsChanged;
            _lensPosition.SettingChanged += CameraSettingsChanged;
            _lensRotation.SettingChanged += CameraSettingsChanged;
            _registration = GateApi.Register(Guid, StartSession, StopSession);
            UnifiedUiApi.RegisterTab(Guid, "iPhoneカメラ", DrawTab, 620);
            Logger.LogInfo("iPhone camera ready: event-driven grip/trigger; portrait PNG -> UserData/cap.");
        }

        private void StartSession()
        {
            var context = GateApi.Current;
            if (!isActiveAndEnabled || !_enabled.Value || context == null || context.Kind != GateApi.SessionKind.H ||
                context.Scene == null || context.Id == _lastSession) return;
            _lastSession = context.Id;
            _permit = GateApi.Capture(Guid);
            if (!_permit.IsValid) return;
            _scene = context.Scene;
            _active = true;
            _status = "非表示：表示ボタンで目の前に出せます。";
            try
            {
                ConnectVrInput();
            }
            catch (Exception ex) { StopSession(); ReportError("開始失敗", ex); }
        }

        private void StopSession()
        {
            _active = false;
            _permit = default;
            try { DisconnectVrInput(); }
            finally { DestroyPhone(); _scene = null; _status = "フリーHの開始待ち"; }
        }

        private void EnabledChanged(object sender, EventArgs args) { if (!_enabled.Value) StopSession(); }
        private void CameraSettingsChanged(object sender, EventArgs args)
        {
            if (!CanAct || _phoneCamera == null) return;
            try { ConfigureCamera(); }
            catch (Exception ex) { DestroyPhone(); ReportError("カメラ設定失敗", ex); }
        }

        private void DrawTab()
        {
            if (!CanAct) { GUILayout.Label("対象のフリーHを開始してください。無効化後の再開は次のHからです。"); return; }
            GUILayout.Label("グリップ保持／トリガー撮影・録画／左右で写真・ビデオ／上下でズーム");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_root == null ? "iPhoneを表示" : "iPhoneを非表示"))
            {
                if (_root == null) SpawnPhone();
                else { DestroyPhone(); _status = "非表示：表示ボタンで目の前に出せます。"; }
            }
            GUILayout.EndHorizontal();
            bool guiEnabled = GUI.enabled;
            GUI.enabled = guiEnabled && _root != null;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("メニュー", GUILayout.Width(100))) BackPhone();
            if (GUILayout.Button("ロック", GUILayout.Width(90))) { if (_movie != null) StopMovie(); ShowPhonePage(PhonePage.Locked); }
            if (GUILayout.Button("A：決定", GUILayout.Width(100))) ConfirmPhone();
            if (GUILayout.Button("写真アプリ", GUILayout.Width(120))) { if (_movie != null) StopMovie(); ShowPhonePage(PhonePage.Gallery); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("←", GUILayout.Width(45))) NavigatePhone(-1, 0);
            if (GUILayout.Button("↑", GUILayout.Width(45))) NavigatePhone(0, 1);
            if (GUILayout.Button("↓", GUILayout.Width(45))) NavigatePhone(0, -1);
            if (GUILayout.Button("→", GUILayout.Width(45))) NavigatePhone(1, 0);
            GUILayout.EndHorizontal();
            GUI.enabled = guiEnabled && _root != null && !_app.Busy && !IsSavingMovie;
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_app.Mode == CaptureMode.Photo, "写真", "Button", GUILayout.Width(90))) SelectCameraMode(CaptureMode.Photo);
            if (GUILayout.Toggle(_app.Mode == CaptureMode.Video, "ビデオ", "Button", GUILayout.Width(90))) SelectCameraMode(CaptureMode.Video);
            GUILayout.EndHorizontal();
            GUI.enabled = guiEnabled && _root != null && _app.Phase != CapturePhase.Saving && !IsSavingMovie;
            if (GUILayout.Button(_movie != null ? "■ 録画を停止" : _app.Mode == CaptureMode.Photo ? "写真を撮る（縦横自動）" : "● 録画を開始", GUILayout.Width(220))) ShutterAction();
            GUI.enabled = guiEnabled && _root != null;
            GUILayout.BeginHorizontal();
            GUILayout.Label("ズーム", GUILayout.Width(65));
            float zoom = GUILayout.HorizontalSlider(_app.Zoom, PhoneZoomPlan.Minimum, PhoneZoomPlan.Maximum, GUILayout.Width(180));
            if (Math.Abs(zoom - _app.Zoom) > .001f) SetCameraZoom(zoom);
            string zoomText = GUILayout.TextField(_zoomText, GUILayout.Width(65));
            if (zoomText != _zoomText)
            {
                _zoomText = zoomText;
                if (float.TryParse(zoomText, out float value) && value >= PhoneZoomPlan.Minimum && value <= PhoneZoomPlan.Maximum) SetCameraZoom(value);
            }
            GUILayout.Label("倍（0.1～20）", GUILayout.Width(95));
            GUILayout.EndHorizontal();
            GUI.enabled = guiEnabled;
            DrawWallpaperControls();
            GUILayout.Label("保存先：" + PhotoDirectory);
            GUILayout.Label(_status);
            if (_root != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("左へ回す")) RotatePhone(-15f);
                if (GUILayout.Button("右へ回す")) RotatePhone(15f);
                GUILayout.EndHorizontal();
            }
        }

        private void RotatePhone(float angle)
        { if (CanAct && _root != null && !IsHeld) _root.transform.Rotate(Vector3.up, angle, Space.World); }

        private void ReportError(string message, Exception ex)
        { _status = message + ": " + ex.Message; Logger.LogError(message + ": " + ex); }

        private string PhotoDirectory => Path.Combine(Paths.GameRootPath, "UserData", "cap");
        private void OnApplicationFocus(bool focused) { if (!focused) { HidePhoneGizmo(); if (!VRGIN.Core.VR.Active) ReleasePhone(); } }
        private void OnApplicationPause(bool paused) { if (paused) { StopMovie(); HidePhoneGizmo(); ReleasePhone(); } }
        private void OnDisable() => StopSession();
        private void OnDestroy()
        {
            UnifiedUiApi.UnregisterTab(Guid);
            UnbindPhoneSettings();
            _enabled.SettingChanged -= EnabledChanged;
            _height.SettingChanged -= CameraSettingsChanged;
            _fov.SettingChanged -= CameraSettingsChanged;
            _lensPosition.SettingChanged -= CameraSettingsChanged;
            _lensRotation.SettingChanged -= CameraSettingsChanged;
            try { _registration?.Dispose(); }
            finally { _registration = null; StopSession(); }
        }
    }
}
