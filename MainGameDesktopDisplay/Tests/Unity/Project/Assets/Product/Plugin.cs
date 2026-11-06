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
    [BepInPlugin(Guid, "MainGameDesktopDisplay", "1.0.0")]
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
        private Renderer _renderer;
        private Material _screenMaterial, _bodyMaterial;
        private Texture2D _texture;
        private DesktopRenderDriver _driver;
        private TransformGizmo _gizmo;
        private readonly GadgetBarCycle _cycle = new GadgetBarCycle();
        private float _nextFrame;
        private string _status = "対象のフリーH開始待ち";
        private Action _uiAction;
        private bool CanAct => _active && isActiveAndEnabled && _enabled.Value && _permit.IsValid;

        private void Awake()
        {
            _enabled = Config.Bind("General", "Enabled", true, "ディスプレイを有効にします。再有効化は次のHから。");
            _monitorDevice = Config.Bind("Desktop", "Monitor", "", "表示対象のモニター名。空欄はメインモニター。");
            _cursor = Config.Bind("Desktop", "Cursor", true, "デスクトップのマウスカーソルを含めます。");
            _fps = Config.Bind("Desktop", "FPS", 15, new ConfigDescription("キャプチャ更新の上限。", new AcceptableValueList<int>(5, 10, 15, 30)));
            _resolution = Config.Bind("Desktop", "MaximumDimension", 1280, new ConfigDescription("キャプチャ画像の長辺上限。", new AcceptableValueList<int>(640, 1280, 1920)));
            _width = Config.Bind("Display", "WidthMeters", 1.2f, new ConfigDescription("画面の幅（m）。", new AcceptableValueRange<float>(.1f, 20f)));
            _distance = Config.Bind("Display", "DistanceMeters", 1.5f, new ConfigDescription("呼び出し距離（m）。", new AcceptableValueRange<float>(.2f, 20f)));
            _widthText = _width.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            _distanceText = _distance.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
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
        }
        private void StopSession()
        {
            _active = false; _permit = default;
            _uiAction = null;
            PlaybackBarDelete(); _scene = null;
            _status = "対象のフリーH開始待ち";
        }
        private void EnabledChanged(object sender, EventArgs args) { if (!_enabled.Value) StopSession(); }
        public void PlaybackBarPrimaryAction()
        {
            if (!CanAct) return;
            _cycle.Press(_root != null, Recall, ShowGizmo);
        }
        public string GetPlaybackBarActionHint() => "左クリック：ディスプレイを視点前へ呼び出し → ギズモ表示を交互に実行\n右クリック：削除・キャプチャ停止\n映すモニター・一時停止は総合UIの「ディスプレイ」タブ\n" + _status;
        private bool Recall()
        {
            if (!CanAct) return false;
            try
            {
                Transform view = View();
                if (view == null) throw new InvalidOperationException("視点カメラがありません。");
                HideGizmo();
                bool ready = true;
                if (_root == null)
                {
                    RefreshMonitors();
                    CreateDisplay();
                    ready = StartCapture();
                }
                _root.transform.SetPositionAndRotation(view.position + view.forward * _distance.Value, view.rotation);
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
            Destroy(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = _bodyMaterial;
            _panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _panel.name = "DesktopScreen"; _panel.transform.SetParent(_root.transform, false);
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
        }
        private void Resize()
        {
            if (_root == null || _capture == null) return;
            float height = _width.Value * _capture.Height / _capture.Width;
            _panel.transform.localScale = new Vector3(_width.Value, height, 1);
            _root.transform.GetChild(0).localScale = new Vector3(_width.Value + .04f, height + .04f, .04f);
        }
        private void RefreshMonitors()
        {
            _monitors = DesktopCapture.Enumerate();
            if (_monitors.Length == 0) throw new InvalidOperationException("表示できるモニターがありません。");
            _monitor = 0;
            for (int i = 0; i < _monitors.Length; i++) if (_monitors[i].Device == _monitorDevice.Value) _monitor = i;
        }
        private bool StartCapture()
        {
            if (!CanAct || _root == null) return false;
            StopCapture();
            try
            {
                _capture = new DesktopCapture(_monitors[_monitor].Rect, _resolution.Value);
                _texture = new Texture2D(_capture.Width, _capture.Height, TextureFormat.BGRA32, false)
                    { name = "DesktopLivePixels", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                _screenMaterial.mainTexture = _texture;
                Resize();
                _paused = false; _nextFrame = 0;
                UploadDesktop(); // First frame fails visibly on this explicit action, never a black placeholder.
                _driver.enabled = true;
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
            if (_driver != null) _driver.enabled = false;
            if (_screenMaterial != null) _screenMaterial.mainTexture = Texture2D.blackTexture;
            _capture?.Dispose(); _capture = null;
            if (_texture != null) Destroy(_texture);
            _texture = null;
        }
        private bool ShowGizmo()
        {
            if (!CanAct || _root == null) return false;
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
            HideGizmo(); _gizmo = null; _cycle.Reset();
            StopCapture();
            if (_driver != null) _driver.Paint = null;
            if (_root != null) { _root.SetActive(false); Destroy(_root); }
            if (_screenMaterial != null) Destroy(_screenMaterial);
            if (_bodyMaterial != null) Destroy(_bodyMaterial);
            _root = _panel = null; _renderer = null; _driver = null; _screenMaterial = _bodyMaterial = null;
            _paused = false; _status = "非表示・キャプチャ停止";
        }
        private void OnApplicationFocus(bool focused) { if (!focused) HideGizmo(); }
        private void OnApplicationPause(bool paused)
        {
            if (paused && _driver != null) { _paused = true; _driver.enabled = false; HideGizmo(); _status = "一時停止中：再開ボタンで再開します。"; }
        }
        private void OnDisable() => StopSession();
        private void OnDestroy()
        {
            StopSession(); _registration?.Dispose(); _registration = null;
            UnifiedUiApi.UnregisterTab(Guid);
            if (_enabled != null) _enabled.SettingChanged -= EnabledChanged;
        }
    }
    // Unity render event attached only to the display mesh; invisible/deleted means no capture work.
    public sealed class DesktopRenderDriver : MonoBehaviour
    {
        internal Action Paint;
        private void OnWillRenderObject() { if (enabled) Paint?.Invoke(); }
    }
}
