using System;
using BepInEx;
using BepInEx.Configuration;
using CanonGate;
using CanonPlugins.Shared;
using MainGameTransformGizmo;
using MainGameUnifiedUi;
using UnityEngine;
using UnityEngine.Rendering;

namespace MainGameDesktopDisplay
{
    [BepInPlugin(Guid, "MainGameDesktopDisplay", "1.4.1")]
    [BepInDependency(GateApi.PluginGuid, "2.4.0")]
    [BepInDependency("com.kks.maingame.unifiedui")]
    [BepInDependency("com.kks.maingame.uiinputcapture")]
    [BepInDependency("com.kks.maingame.transformgizmo")]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.kks.maingame.desktopdisplay";
        private IDisposable _registration;
        private GateApi.Permit _permit;
        private MonoBehaviour _scene;
        private ulong _session;
        private bool _active, _paused;
        private ConfigEntry<bool> _enabled, _cursor;
        private ConfigEntry<int> _fps, _resolution;
        private ConfigEntry<float> _width, _distance;
        private ConfigEntry<string> _monitorDevice;
        private DesktopCapture.Monitor[] _monitors = Array.Empty<DesktopCapture.Monitor>();
        private int _monitor;
        private DesktopCapture _capture;
        private GameObject _root, _panel;
        private Transform _panelTransform, _bodyTransform;
        private string[] _monitorLabels = Array.Empty<string>(), _monitorSelectedLabels = Array.Empty<string>();
        private Renderer _renderer;
        private Material _screenMaterial, _bodyMaterial;
        private Texture2D _texture;
        private DesktopRenderDriver _driver;
        private TransformGizmo _gizmo;
        private readonly GadgetBarCycle _cycle = new GadgetBarCycle();
        private float _nextFrame;
        private float _screenAspect = 16f / 9f;
        private string _statusText = "対象のフリーH開始待ち", _barHint;
        private string _status { get => _statusText; set { if (_statusText != value) { _statusText = value; _barHint = null; } } }
        private Action _uiAction;
        private bool CanAct => _active && isActiveAndEnabled && _enabled.Value && _permit.IsValid;

        private void Awake()
        {
            _enabled = Config.Bind("General", "Enabled", true, "ディスプレイを有効にします。再有効化は次のHから。");
            _mouseEnabled = Config.Bind("Desktop", "MouseControl", true, "右手のA＝左クリック、B＝右クリック、スティック上下左右＝縦横スクロール。表示中だけ専用入力を有効にします。");
            _mouseEnabled.SettingChanged += MouseSettingChanged;
            _monitorDevice = Config.Bind("Desktop", "Monitor", "", "表示対象のモニター名。空欄はメインモニター。");
            _cursor = Config.Bind("Desktop", "Cursor", true, "デスクトップのマウスカーソルを含めます。");
            _fps = Config.Bind("Desktop", "FPS", 15, new ConfigDescription("キャプチャ更新の上限。", new AcceptableValueList<int>(5, 10, 15, 30)));
            _resolution = Config.Bind("Desktop", "MaximumDimension", 1280, new ConfigDescription("キャプチャ画像の長辺上限。", new AcceptableValueList<int>(640, 1280, 1920)));
            _width = Config.Bind("Display", "WidthMeters", 1.2f, new ConfigDescription("画面の幅（m）。", new AcceptableValueRange<float>(.1f, 20f)));
            _distance = Config.Bind("Display", "DistanceMeters", .5f, new ConfigDescription("呼び出し距離（m）。", new AcceptableValueRange<float>(.2f, 20f)));
            _widthText = _width.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            InitializeSpawnPosition();
            InitializeDisplayPresets();
            _enabled.SettingChanged += EnabledChanged;
            _registration = GateApi.Register(Guid, StartSession, StopSession);
            UnifiedUiApi.RegisterTab(Guid, "ディスプレイ", DrawTab, 625);
        }
        private void StartSession()
        {
            var context = GateApi.Current;
            if (!isActiveAndEnabled || !_enabled.Value || context == null || context.Kind != GateApi.SessionKind.H || context.Scene == null || context.Id == _session) return;
            _session = context.Id;
            _permit = GateApi.Capture(Guid);
            if (!_permit.IsValid) return;
            _scene = context.Scene;
            _active = true;
            _status = "非表示：PlaybackBarの「ディスプレイ」で呼び出してください。";
            try { ConnectGrabInput(); }
            catch (Exception ex) { StopSession(); _status = "VR入力の接続失敗：" + ex.Message; }
        }
        private void StopSession()
        {
            _active = false; _permit = default;
            _uiAction = null;
            DisconnectGrabInput();
            PlaybackBarDelete(); _scene = null;
            _status = "対象のフリーH開始待ち";
        }
        private void EnabledChanged(object sender, EventArgs args) { if (!_enabled.Value) StopSession(); }
        public void PlaybackBarPrimaryAction()
        {
            if (!CanAct) return;
            _cycle.Press(_root != null, Recall, ShowGizmo, HideGizmo, _gizmo != null && _gizmo.IsVisible);
        }
        public string GetPlaybackBarActionHint() => _barHint ?? (_barHint = "左クリック：初回だけ呼び出し→以後ギズモ表示／非表示／右クリック：削除\nVR：右手の照準が画面に当たり50cm以内の時だけ線・カーソル追従・A/B左右クリック・上下左右スクロール\nグリップ保持中の上下＝サイズ／左右＝モニター切替。切り取り・画面プリセットは設定タブ\n" + _statusText);
        private bool Recall()
        {
            if (!CanAct) return false;
            try
            {
                Transform view = View();
                if (view == null) throw new InvalidOperationException("視点カメラがありません。");
                HideGizmo();
                SetPointerActive(false);
                ReleaseDisplayGrab();
                bool ready = true;
                if (_root == null)
                {
                    RefreshMonitors();
                    CreateDisplay();
                    ready = StartCapture();
                }
                _spawnOrigin = view.position; _spawnRotation = view.rotation;
                ApplySpawnPosition();
                if (ready) _status = _paused ? "一時停止中" : "デスクトップ表示中：" + _monitors[_monitor];
                return ready;
            }
            catch (Exception ex) { PlaybackBarDelete(); _status = "表示失敗：" + ex.Message; return false; }
        }
        private static Transform View()
        {
            if (VRGIN.Core.VR.Active && VRGIN.Core.VR.Camera != null) return VRGIN.Core.VR.Camera.Head;
            var camera = Camera.main;
            return camera != null ? camera.transform : null;
        }
        private void CreateDisplay()
        {
            var textureShader = Shader.Find("Unlit/Texture");
            var bodyShader = Shader.Find("Unlit/Color");
            if (textureShader == null || bodyShader == null) throw new InvalidOperationException("表示用シェーダーがありません。");
            _root = new GameObject("DesktopDisplay");
            _root.transform.SetParent(_scene.transform, false);
            _bodyMaterial = new Material(bodyShader) { color = new Color(.025f, .03f, .04f), name = "DesktopDisplayBezel" };
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Bezel"; body.transform.SetParent(_root.transform, false);
            _bodyTransform = body.transform;
            Destroy(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = _bodyMaterial;
            _panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _panel.name = "DesktopScreen"; _panel.transform.SetParent(_root.transform, false);
            _panelTransform = _panel.transform;
            _panel.transform.localPosition = new Vector3(0, 0, -.021f);
            Destroy(_panel.GetComponent<Collider>());
            _screenMaterial = new Material(textureShader) { name = "DesktopDisplayPixels" };
            _renderer = _panel.GetComponent<Renderer>();
            _renderer.sharedMaterial = _screenMaterial;
            _renderer.shadowCastingMode = ShadowCastingMode.Off; _renderer.receiveShadows = false;
            body.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            body.GetComponent<Renderer>().receiveShadows = false;
            _driver = _panel.AddComponent<DesktopRenderDriver>();
            _driver.enabled = false;
            _driver.Paint = PaintDesktop;
            CreateMouseRay();
        }
        private void Resize()
        {
            if (_root == null || _panel == null) return;
            float height = _width.Value / _screenAspect;
            _panelTransform.localScale = new Vector3(_width.Value * _clip.Width, height * _clip.Height, 1);
            _panelTransform.localPosition = new Vector3(_width.Value * _clip.CenterX, height * _clip.CenterY, -.021f);
            _bodyTransform.localScale = new Vector3(_width.Value * _clip.Width + .04f, height * _clip.Height + .04f, .04f);
            _bodyTransform.localPosition = new Vector3(_panelTransform.localPosition.x, _panelTransform.localPosition.y, 0);
        }
        private void RefreshMonitors(bool applyAssignedPreset = true, bool requireSelectedMonitor = false)
        {
            var monitors = DesktopCapture.Enumerate();
            if (requireSelectedMonitor && !string.IsNullOrEmpty(_monitorDevice.Value) &&
                !Array.Exists(monitors, item => MonitorKey(item.Device) == MonitorKey(_monitorDevice.Value)))
                throw new InvalidOperationException("保存したモニターが見つかりません：" + _monitorDevice.Value);
            _monitors = monitors;
            if (_monitors.Length == 0) throw new InvalidOperationException("表示できるモニターがありません。");
            _monitor = 0;
            for (int i = 0; i < _monitors.Length; i++) if (MonitorKey(_monitors[i].Device) == MonitorKey(_monitorDevice.Value)) _monitor = i;
            _monitorLabels = new string[_monitors.Length]; _monitorSelectedLabels = new string[_monitors.Length];
            for (int i = 0; i < _monitors.Length; i++) { string label = _monitors[i].ToString(); _monitorLabels[i] = "○ " + label; _monitorSelectedLabels[i] = "● " + label; }
            CacheMonitorPresetBindings();
            LoadMonitorClip();
            if (applyAssignedPreset) ApplyAssignedMonitorPreset();
        }
        private static string MonitorKey(string device)
        {
            if (string.IsNullOrEmpty(device)) return "";
            return device.Substring(device.LastIndexOf('\\') + 1);
        }
        private bool ChooseMonitor(int index)
        {
            if (index < 0 || index >= _monitors.Length || index == _monitor) return false;
            CommitClipDraft();
            _monitor = index;
            // Store DISPLAYn, without the Windows path prefix that string config escaping can alter.
            _monitorDevice.Value = MonitorKey(_monitors[index].Device);
            LoadMonitorClip();
            ApplyAssignedMonitorPreset();
            return true;
        }
        private void SelectMonitor(int index)
        {
            if (!CanAct || !ChooseMonitor(index)) return;
            RestartSelectedMonitorCapture();
        }
        private void RestartSelectedMonitorCapture()
        {
            if (_root != null)
            {
                bool paused = _paused;
                StartCapture();
                if (paused && _capture != null) { _paused = true; _driver.enabled = false; SyncDesktopMouse(); _status = "一時停止中（モニター切替済み）"; }
            }
        }
        private bool StartCapture()
        {
            if (!CanAct || _root == null) return false;
            StopCapture();
            try
            {
                _capture = new DesktopCapture(_clip.Bounds, _resolution.Value);
                _texture = new Texture2D(_capture.Width, _capture.Height, TextureFormat.BGRA32, false)
                    { name = "DesktopLivePixels", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                _screenMaterial.mainTexture = _texture;
                Resize();
                _paused = false; _nextFrame = 0;
                UploadDesktop(); // First frame fails visibly on this explicit action, never a black placeholder.
                _driver.enabled = true;
                _mouseBounds = _clip.Bounds; _pointerFaulted = false;
                if (_mouseHand != null) _desktopMouse.RefreshBounds();
                _mouseReady = true; SyncDesktopMouse();
                _status = "デスクトップ表示中：" + _monitors[_monitor];
                return true;
            }
            catch (Exception ex) { StopCapture(); _paused = true; _status = "キャプチャ停止：" + ex.Message + "。モニターを再取得して再開してください。"; return false; }
        }
        private void UploadDesktop()
        {
            _capture.Capture(_cursor.Value);
            _texture.LoadRawTextureData(_capture.Pixels, _capture.ByteCount);
            _texture.Apply(false, false);
        }
        private void PaintDesktop()
        {
            // Actual video pixel production only, called when this mesh is rendered.
            // No Update, timer, room queries, camera searches, or setting reapplication.
            if (!CanAct || _paused || _capture == null || Time.realtimeSinceStartup < _nextFrame) return;
            _nextFrame = Time.realtimeSinceStartup + 1f / _fps.Value;
            try { UploadDesktop(); }
            catch (Exception ex) { StopCapture(); _paused = true; _status = "キャプチャ停止：" + ex.Message; }
        }
        private void StopCapture()
        {
            _mouseReady = false; ReleaseDesktopMouse(); SyncDesktopMouse();
            if (_driver != null) _driver.enabled = false;
            if (_screenMaterial != null) _screenMaterial.mainTexture = Texture2D.blackTexture;
            _capture?.Dispose(); _capture = null;
            if (_texture != null) Destroy(_texture);
            _texture = null;
        }
        private bool ShowGizmo()
        {
            if (!CanAct || _root == null) return false;
            SetPointerActive(false);
            ReleaseDisplayGrab();
            try
            {
                if (!TransformGizmoApi.IsAvailable) throw new InvalidOperationException("共通ギズモが利用できません。");
                if (_gizmo == null) _gizmo = TransformGizmoApi.Attach(_root);
                if (_gizmo == null) throw new InvalidOperationException("共通ギズモの許可を確認してください。");
                _gizmo.enabled = true;
                if (!TransformGizmoApi.EnableCameraInputCaptureOnDrag(_gizmo)) throw new InvalidOperationException("ギズモの入力保護を開始できません。");
                _gizmo.SetMode(GizmoMode.Move); _gizmo.SetVisible(true);
                return _gizmo.IsVisible;
            }
            catch (Exception ex) { HideGizmo(); _status = ex.Message; return false; }
        }
        private void HideGizmo()
        {
            if (_gizmo == null) return;
            _gizmo.SetVisible(false); TransformGizmoApi.DisableCameraInputCaptureOnDrag(_gizmo); _gizmo.enabled = false;
        }
        public void PlaybackBarDelete()
        {
            ReleaseDesktopMouse(); DestroyMouseRay();
            ReleaseDisplayGrab();
            CompletePendingGrabLocks();
            HideGizmo(); _gizmo = null; _cycle.Reset();
            StopCapture();
            if (_driver != null) _driver.Paint = null;
            if (_root != null) { _root.SetActive(false); Destroy(_root); }
            if (_screenMaterial != null) Destroy(_screenMaterial);
            if (_bodyMaterial != null) Destroy(_bodyMaterial);
            _root = _panel = null; _renderer = null; _driver = null; _screenMaterial = _bodyMaterial = null;
            _panelTransform = _bodyTransform = null;
            _paused = false; _status = "非表示・キャプチャ停止";
        }
        private void OnApplicationFocus(bool focused) { if (!focused) HideGizmo(); }
        private void OnApplicationPause(bool paused)
        {
            if (paused) { _paused = true; ReleaseDesktopMouse(); SyncDesktopMouse(); }
            if (paused) ReleaseDisplayGrab();
            if (paused && _driver != null) { _paused = true; _driver.enabled = false; HideGizmo(); _status = "一時停止中：再開ボタンで再開します。"; }
        }
        private void OnDisable() => StopSession();
        private void OnDestroy()
        {
            StopSession(); _registration?.Dispose(); _registration = null;
            UnifiedUiApi.UnregisterTab(Guid);
            if (_enabled != null) _enabled.SettingChanged -= EnabledChanged;
            if (_mouseEnabled != null) _mouseEnabled.SettingChanged -= MouseSettingChanged;
        }
    }
    // Unity render event attached only to the display mesh; invisible/deleted means no capture work.
    public sealed class DesktopRenderDriver : MonoBehaviour
    {
        internal Action Paint;
        private int _lastFrame = -1;
        private void OnWillRenderObject()
        { if (enabled && _lastFrame != Time.frameCount) { _lastFrame = Time.frameCount; Paint?.Invoke(); } }
    }
}
