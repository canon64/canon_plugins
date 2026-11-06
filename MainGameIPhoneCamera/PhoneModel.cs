using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace MainGameIPhoneCamera
{
    public sealed partial class Plugin
    {
        private GameObject _root;
        private Camera _phoneCamera;
        private RenderTexture _image;
        private Material _screenMaterial;
        private Mesh _screenMesh;
        private Material _bezelMaterial;
        private Mesh _bezelMesh;
        private Renderer[] _phoneRenderers;
        private bool _capturing;
        private PhoneDisplay _display;

        private void SpawnPhone()
        {
            if (!CanAct) return;
            try
            {
                HidePhoneGizmo();
                ReleasePhone();
                if (_root == null) CreatePhone();
                Transform view = GetView();
                if (view == null) throw new InvalidOperationException("視点カメラがありません");
                _root.transform.SetPositionAndRotation(view.position + view.forward * .45f,
                    Quaternion.LookRotation(-view.forward, view.up));
                _status = "iPhoneを目の前へ配置。近くでグリップを押すと持てます。";
            }
            catch (Exception ex) { DestroyPhone(); ReportError("iPhone生成失敗", ex); }
        }

        private void CreatePhone()
        {
            // Installed Sideloader item definition: earthship/87, no Studio singleton or scene required.
            _root = new GameObject("IPhoneCamera");
            _root.transform.SetParent(_scene.transform, false);
            GameObject model = CommonLib.LoadAsset<GameObject>("studio/earthshipx.unity3d", "earthshipx", true, "");
            if (model == null) throw new InvalidOperationException("導入済みearthship iPhone Xを読み込めません");
            model.transform.SetParent(_root.transform, false);
            model.transform.localPosition = new Vector3(0, -.06f, 0);
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) Destroy(collider);
            var filter = model.GetComponentInChildren<MeshFilter>(true);
            if (filter == null || filter.sharedMesh == null) throw new InvalidOperationException("iPhoneの画面メッシュがありません");
            // Mask only the old lock-screen picture. Preserve the body/back materials as installed.
            _bezelMesh = PhoneScreen.Build(filter.sharedMesh, true);
            Shader bezelShader = Shader.Find("Unlit/Color");
            if (bezelShader == null) throw new InvalidOperationException("Unlit/Color shaderがありません");
            _bezelMaterial = new Material(bezelShader) { name = "IPhoneScreenBezel", color = Color.black };
            var bezel = new GameObject("ScreenBezel", typeof(MeshFilter), typeof(MeshRenderer));
            bezel.transform.SetParent(filter.transform, false);
            bezel.GetComponent<MeshFilter>().sharedMesh = _bezelMesh;
            var bezelRenderer = bezel.GetComponent<MeshRenderer>();
            bezelRenderer.sharedMaterial = _bezelMaterial;
            bezelRenderer.shadowCastingMode = ShadowCastingMode.Off;
            bezelRenderer.receiveShadows = false;
            _screenMesh = PhoneScreen.Build(filter.sharedMesh);
            var screen = new GameObject("LiveScreen", typeof(MeshFilter), typeof(MeshRenderer));
            screen.transform.SetParent(filter.transform, false);
            screen.GetComponent<MeshFilter>().sharedMesh = _screenMesh;
            Shader shader = Shader.Find("Unlit/Texture");
            if (shader == null) throw new InvalidOperationException("Unlit/Texture shaderがありません");
            _screenMaterial = new Material(shader) { name = "IPhoneCameraScreen" };
            var renderer = screen.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _screenMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _phoneRenderers = model.GetComponentsInChildren<Renderer>(true);
            var lens = new GameObject("RearCamera", typeof(Camera));
            lens.transform.SetParent(_root.transform, false);
            _phoneCamera = lens.GetComponent<Camera>();
            _phoneCamera.stereoTargetEye = StereoTargetEyeMask.None;
            _phoneCamera.nearClipPlane = .015f;
            _phoneCamera.farClipPlane = 200f;
            _phoneCamera.depth = -50;
            var main = Camera.main;
            if (main != null)
            {
                _phoneCamera.cullingMask = main.cullingMask;
                _phoneCamera.clearFlags = main.clearFlags;
                _phoneCamera.backgroundColor = main.backgroundColor;
            }
            ConfigureCamera();
            CreatePhoneSounds();
            _captureLifetime = new System.Threading.CancellationTokenSource();
            _display = new PhoneDisplay();
            _screenMaterial.mainTexture = _display.Texture;
            _cameraUi = new PhoneCameraUi(_display.Root, _screenMesh, _app, _image);
            _displayTurns = 0; _orientationPage = PhonePage.Off;
            CreatePhoneShell(_display.Root);
            Camera.onPreCull += HidePhoneForOwnCamera;
            Camera.onPostRender += RestorePhoneAfterOwnCamera;
            Logger.LogInfo("Loaded installed earthship iPhone X; independent rear camera and measured screen mesh.");
        }

        private Transform GetView()
        {
            Transform vrView = GetVrView();
            return vrView != null ? vrView : (Camera.main != null ? Camera.main.transform : null);
        }

        private void ConfigureCamera()
        {
            StopCameraZoom();
            var size = PhotoSize.FromHeight(_height.Value);
            _phoneCamera.transform.localPosition = _lensPosition.Value;
            _phoneCamera.transform.localRotation = Quaternion.Euler(_lensRotation.Value);
            ApplyCameraZoom();
            _phoneCamera.aspect = (float)size.Width / size.Height;
            if (_image != null && _image.width == size.Width && _image.height == size.Height) return;
            var next = new RenderTexture(size.Width, size.Height, 24, RenderTextureFormat.ARGB32)
                { name = "IPhonePortrait", antiAliasing = 1 };
            if (!next.Create()) { Destroy(next); throw new InvalidOperationException("縦長RenderTextureを作成できません"); }
            var old = _image;
            _image = next;
            _phoneCamera.targetTexture = next;
            _cameraUi?.CameraPreview(next);
            if (old != null) { old.Release(); Destroy(old); }
        }

        // Render events only: hide this phone for its own camera, without changing global layers.
        private void HidePhoneForOwnCamera(Camera camera)
        {
            if (camera != _phoneCamera || _phoneRenderers == null) return;
            foreach (var renderer in _phoneRenderers) if (renderer != null) renderer.forceRenderingOff = true;
        }
        private void RestorePhoneAfterOwnCamera(Camera camera)
        {
            if (camera != _phoneCamera || _phoneRenderers == null) return;
            foreach (var renderer in _phoneRenderers) if (renderer != null) renderer.forceRenderingOff = false;
        }

        private void DestroyPhone()
        {
            _zoomMotion?.Dispose(); _zoomMotion = null;
            StopMovie();
            DestroyPhoneSounds();
            ++_captureEpoch;
            _captureLifetime?.Cancel(); _captureLifetime?.Dispose(); _captureLifetime = null;
            DestroyPhoneShell(); _wallpaperSaving = false;
            _cameraUi?.Dispose(); _cameraUi = null;
            _display?.Dispose(); _display = null;
            if (_movieThumbnail != null) { Destroy(_movieThumbnail); _movieThumbnail = null; }
            _app.Reset(); _zoomText = "1.00";
            ClearPhoneGizmo();
            ReleasePhone();
            Camera.onPreCull -= HidePhoneForOwnCamera;
            Camera.onPostRender -= RestorePhoneAfterOwnCamera;
            if (_phoneCamera != null) { _phoneCamera.enabled = false; _phoneCamera.targetTexture = null; }
            if (_root != null) { _root.SetActive(false); Destroy(_root); }
            _root = null; _phoneCamera = null; _phoneRenderers = null;
            if (_image != null) { _image.Release(); Destroy(_image); _image = null; }
            if (_screenMaterial != null) { Destroy(_screenMaterial); _screenMaterial = null; }
            if (_screenMesh != null) { Destroy(_screenMesh); _screenMesh = null; }
            if (_bezelMaterial != null) { Destroy(_bezelMaterial); _bezelMaterial = null; }
            if (_bezelMesh != null) { Destroy(_bezelMesh); _bezelMesh = null; }
        }
    }
}
