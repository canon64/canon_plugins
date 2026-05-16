using MainGameTransformGizmo;
using UnityEngine;
using Valve.VR;
using VRGIN.Core;

namespace MainGameSubCameraDisplayProbe
{
    public sealed partial class Plugin
    {
        private void EnsureProbe()
        {
            EnsureRoot();
            if (_rootObject == null)
                return;
            EnsureCamera();
            EnsureDisplay();
            ApplyCameraSettings();
            ApplyDisplaySettings();
        }

        private void EnsureRoot()
        {
            if (_rootObject != null)
                return;

            Camera worldCamera = ResolveWorldCamera();
            if (worldCamera == null)
            {
                LogWarn("probe create skipped: world camera not found");
                return;
            }

            _rootObject = new GameObject("__MainGameSubCameraDisplayProbeRoot");
            MoveProbeInFrontOfPlayer(initialCreate: true);
            LogInfo("probe root created");
        }

        private void EnsureCamera()
        {
            if (_rootObject == null)
                EnsureRoot();
            if (_rootObject == null || _cameraAnchorObject != null)
                return;

            _cameraAnchorObject = new GameObject("SubCameraAnchor");
            _cameraAnchorObject.transform.SetParent(_rootObject.transform, false);
            _cameraAnchorObject.transform.localPosition = new Vector3(_settings.CameraPosX, _settings.CameraPosY, _settings.CameraPosZ);
            _cameraAnchorObject.transform.localRotation = Quaternion.Euler(_settings.CameraRotX, _settings.CameraRotY, _settings.CameraRotZ);

            _cameraVisualObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cameraVisualObject.name = "CameraMarker";
            DestroyImmediate(_cameraVisualObject.GetComponent<Collider>());
            _cameraVisualObject.transform.SetParent(_cameraAnchorObject.transform, false);
            _cameraVisualObject.transform.localPosition = Vector3.zero;
            _cameraVisualObject.transform.localRotation = Quaternion.identity;

            _cameraLensObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _cameraLensObject.name = "CameraLens";
            DestroyImmediate(_cameraLensObject.GetComponent<Collider>());
            _cameraLensObject.transform.SetParent(_cameraAnchorObject.transform, false);

            ApplyCameraVisualScale();

            GameObject subCameraObject = new GameObject("SubCamera");
            subCameraObject.transform.SetParent(_cameraAnchorObject.transform, false);
            subCameraObject.transform.localPosition = Vector3.zero;
            subCameraObject.transform.localRotation = Quaternion.identity;
            _subCamera = subCameraObject.AddComponent<Camera>();
            _subCamera.clearFlags = CameraClearFlags.SolidColor;
            _subCamera.backgroundColor = Color.black;

            _renderTexture = CreateRenderTexture();
            _subCamera.targetTexture = _renderTexture;
            _subCamera.allowHDR = false;
            _subCamera.allowMSAA = false;

            _handPreviewObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _handPreviewObject.name = "HandPreviewQuad";
            DestroyImmediate(_handPreviewObject.GetComponent<Collider>());
            _handPreviewMaterial = CreateDisplayMaterial(backFace: false);
            _handPreviewMaterial.mainTexture = _renderTexture;
            _handPreviewObject.GetComponent<Renderer>().material = _handPreviewMaterial;
            SetLayerRecursive(_handPreviewObject, Mathf.Clamp(_settings.DisplayLayer, 0, 30));
            _handPreviewObject.SetActive(false);

            // ディスプレイがすでにあれば、その mainTexture を新しい RenderTexture に更新
            if (_displayMaterial != null)
                _displayMaterial.mainTexture = _renderTexture;

            AttachCameraGizmo();
            ApplyCameraSettings();
            LogInfo("camera created");
        }

        private void EnsureDisplay()
        {
            if (_rootObject == null)
                EnsureRoot();
            if (_rootObject == null || _displayAnchorObject != null)
                return;

            _displayAnchorObject = new GameObject("DisplayAnchor");
            _displayAnchorObject.transform.SetParent(_rootObject.transform, false);
            _displayAnchorObject.transform.localPosition = new Vector3(_settings.DisplayPosX, _settings.DisplayPosY, _settings.DisplayPosZ);
            _displayAnchorObject.transform.localRotation = Quaternion.Euler(_settings.DisplayRotX, _settings.DisplayRotY, _settings.DisplayRotZ);

            _displayObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _displayObject.name = "DisplayQuad";
            _displayObject.transform.SetParent(_displayAnchorObject.transform, false);
            _displayObject.transform.localPosition = Vector3.zero;
            _displayObject.transform.localRotation = Quaternion.identity;
            _displayMaterial = CreateDisplayMaterial(backFace: false);
            _displayMaterial.mainTexture = _renderTexture;
            _displayObject.GetComponent<Renderer>().material = _displayMaterial;

            EnsureDisplayOverlayCamera();
            ApplyDisplayLayer();
            AttachDisplayGizmo();
            ApplyDisplaySettings();
            LogInfo("display created");
        }

        private void ApplyCameraVisualScale()
        {
            if (_settings == null) return;
            float scale = Mathf.Clamp(_settings.CameraVisualScale, 0.1f, 3f);

            if (_cameraVisualObject != null)
                _cameraVisualObject.transform.localScale = new Vector3(0.1f, 0.06f, 0.16f) * scale;

            if (_cameraLensObject != null)
            {
                _cameraLensObject.transform.localPosition = new Vector3(0f, 0f, -0.11f * scale);
                _cameraLensObject.transform.localScale = Vector3.one * (0.04f * scale);
            }
        }

        private RenderTexture CreateRenderTexture()
        {
            int width = Mathf.Clamp(_settings.RenderWidth, 64, 4096);
            int height = Mathf.Clamp(_settings.RenderHeight, 64, 4096);
            _settings.RenderWidth = width;
            _settings.RenderHeight = height;

            RenderTexture renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            renderTexture.name = "SubCameraDisplayProbeRT";
            renderTexture.useMipMap = false;
            renderTexture.autoGenerateMips = false;
            renderTexture.antiAliasing = 1;
            renderTexture.filterMode = ParseFilterMode(_settings.RenderFilterMode);
            renderTexture.wrapMode = TextureWrapMode.Clamp;
            renderTexture.Create();
            return renderTexture;
        }

        private void RecreateRenderTexture(string reason)
        {
            if (_subCamera == null)
                return;

            StopVideoRecording("render-texture-recreate");
            RenderTexture old = _renderTexture;
            _subCamera.targetTexture = null;
            _renderTexture = CreateRenderTexture();
            _subCamera.targetTexture = _renderTexture;

            if (_displayMaterial != null)
                _displayMaterial.mainTexture = _renderTexture;
            if (_handPreviewMaterial != null)
                _handPreviewMaterial.mainTexture = _renderTexture;

            if (old != null)
            {
                old.Release();
                Destroy(old);
            }

            ApplyDisplaySettings();
            LogInfo("render texture recreated reason=" + reason + " size=" + _settings.RenderWidth + "x" + _settings.RenderHeight);
        }

        private void AttachCameraGizmo()
        {
            if (_cameraGizmo != null || _cameraAnchorObject == null)
                return;

            _cameraGizmo = TransformGizmoApi.Attach(_cameraAnchorObject);
            if (_cameraGizmo != null)
            {
                TransformGizmoApi.EnableCameraInputCaptureOnDrag(_cameraGizmo);
                _cameraGizmo.SetVisible(_settings.CameraGizmoVisible);
                _cameraGizmo.SetSizeMultiplier(_settings.GizmoSizeMultiplier);
                _cameraGizmo.DragStateChanged += OnGizmoDragStateChanged;
            }
        }

        private void AttachDisplayGizmo()
        {
            if (_displayGizmo != null || _displayAnchorObject == null)
                return;

            _displayGizmo = TransformGizmoApi.Attach(_displayAnchorObject);
            if (_displayGizmo != null)
            {
                TransformGizmoApi.EnableCameraInputCaptureOnDrag(_displayGizmo);
                _displayGizmo.SetVisible(_settings.DisplayGizmoVisible);
                _displayGizmo.SetSizeMultiplier(_settings.GizmoSizeMultiplier);
                _displayGizmo.DragStateChanged += OnGizmoDragStateChanged;
            }
        }

        private void OnGizmoDragStateChanged(bool dragging)
        {
            _gizmoDragging = dragging;
            if (dragging)
            {
                return;
            }

            CaptureActiveBoneCameraOffset();
            PersistTransformsToSettings();
        }

        private void SyncGizmoState()
        {
            if (_cameraGizmo != null)
            {
                _cameraGizmo.SetVisible(_settings.CameraGizmoVisible);
                _cameraGizmo.SetSizeMultiplier(_settings.GizmoSizeMultiplier);
            }

            if (_displayGizmo != null)
            {
                _displayGizmo.SetVisible(_settings.DisplayGizmoVisible);
                _displayGizmo.SetSizeMultiplier(_settings.GizmoSizeMultiplier);
            }

            if ((_cameraGizmo != null && _cameraGizmo.IsDragging) || (_displayGizmo != null && _displayGizmo.IsDragging))
            {
                _gizmoDragging = true;
            }
            else if (_gizmoDragging)
            {
                _gizmoDragging = false;
                PersistTransformsToSettings();
            }
        }

        private void ApplyCameraSettings()
        {
            if (_subCamera == null || _cameraAnchorObject == null)
                return;

            _subCamera.fieldOfView = _settings.CameraFieldOfView;
            _subCamera.nearClipPlane = _settings.CameraNearClip;
            _subCamera.farClipPlane = _settings.CameraFarClip;
            if (_renderTexture != null)
                _renderTexture.filterMode = ParseFilterMode(_settings.RenderFilterMode);
            ApplyCameraVisualScale();
            if (_boneFollowActive)
                return;

            _cameraAnchorObject.transform.localPosition = new Vector3(_settings.CameraPosX, _settings.CameraPosY, _settings.CameraPosZ);
            _cameraAnchorObject.transform.localRotation = Quaternion.Euler(_settings.CameraRotX, _settings.CameraRotY, _settings.CameraRotZ);
        }

        private void SyncCameraControlSliders(bool positionChanged, bool rotationChanged)
        {
            if (_cameraAnchorObject == null)
                return;

            if (!_boneFollowActive)
            {
                ApplyCameraSettings();
                return;
            }

            if (positionChanged)
            {
                Transform bone = ResolveActiveBone();
                if (bone != null && _rootObject != null)
                {
                    _activeSaveCameraPosition = true;
                    Vector3 localPosition = new Vector3(_settings.CameraPosX, _settings.CameraPosY, _settings.CameraPosZ);
                    Vector3 desiredWorldPosition = _rootObject.transform.TransformPoint(localPosition);
                    _activeCameraOffsetLocal = desiredWorldPosition - bone.position;
                    UpdateBoneFollow();
                    PersistTransformsToSettings();
                    SaveActiveBonePresetOffsets();
                }
            }

            if (rotationChanged)
                SetStatus("ボーン追従中の回転は注視点で決定");
        }

        private void CopyCurrentCameraToSubCamera()
        {
            EnsureProbe();
            if (_cameraAnchorObject == null || _subCamera == null)
            {
                SetStatus("サブカメラ未生成");
                return;
            }

            Camera source = ResolveCurrentCameraCopySource();
            if (source == null)
            {
                SetStatus("コピー元カメラなし");
                return;
            }

            ClearBoneFollow();
            _cameraAnchorObject.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            _settings.CameraFieldOfView = Round2(Mathf.Clamp(source.fieldOfView, 10f, 170f));
            _settings.CameraNearClip = Round2(Mathf.Max(0.01f, source.nearClipPlane));
            _settings.CameraFarClip = Round2(Mathf.Max(_settings.CameraNearClip + 1f, source.farClipPlane));
            _subCamera.fieldOfView = _settings.CameraFieldOfView;
            _subCamera.nearClipPlane = _settings.CameraNearClip;
            _subCamera.farClipPlane = _settings.CameraFarClip;
            PersistTransformsToSettings();
            SetStatus("現在カメラをコピー: " + source.name);
        }

        private Camera ResolveCurrentCameraCopySource()
        {
            Camera main = Camera.main;
            if (IsCurrentCameraCopySource(main))
                return main;

            Camera[] cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (IsCurrentCameraCopySource(camera))
                    return camera;
            }

            return null;
        }

        private bool IsCurrentCameraCopySource(Camera camera)
        {
            return camera != null
                && camera.isActiveAndEnabled
                && camera.targetTexture == null
                && camera != _subCamera
                && camera != _displayOverlayCamera;
        }

        private void ApplyDisplaySettings()
        {
            if (_displayAnchorObject == null || _displayObject == null)
                return;

            _displayAnchorObject.transform.localPosition = new Vector3(_settings.DisplayPosX, _settings.DisplayPosY, _settings.DisplayPosZ);
            _displayAnchorObject.transform.localRotation = Quaternion.Euler(_settings.DisplayRotX, _settings.DisplayRotY, _settings.DisplayRotZ);
            _settings.DisplayHeight = Mathf.Max(0.1f, Round2(_settings.DisplayHeight));
            _settings.DisplayWidth = SettingsStore.CalculateDisplayWidth(_settings);
            _displayObject.transform.localScale = new Vector3(_settings.DisplayWidth, _settings.DisplayHeight, 1f);

            ApplyDisplayLayer();
            SyncDisplayOverlayCamera();
        }

        private void PersistTransformsToSettings()
        {
            if (_cameraAnchorObject != null)
            {
                Vector3 pos = _cameraAnchorObject.transform.localPosition;
                Vector3 rot = _cameraAnchorObject.transform.localRotation.eulerAngles;
                _settings.CameraPosX = Round2(pos.x);
                _settings.CameraPosY = Round2(pos.y);
                _settings.CameraPosZ = Round2(pos.z);
                _settings.CameraRotX = Round2(rot.x);
                _settings.CameraRotY = Round2(rot.y);
                _settings.CameraRotZ = Round2(rot.z);
            }

            if (_displayAnchorObject != null)
            {
                Vector3 pos = _displayAnchorObject.transform.localPosition;
                Vector3 rot = _displayAnchorObject.transform.localRotation.eulerAngles;
                _settings.DisplayPosX = Round2(pos.x);
                _settings.DisplayPosY = Round2(pos.y);
                _settings.DisplayPosZ = Round2(pos.z);
                _settings.DisplayRotX = Round2(rot.x);
                _settings.DisplayRotY = Round2(rot.y);
                _settings.DisplayRotZ = Round2(rot.z);
            }

            SaveSettings();
        }

        private void DestroyCameraOnly()
        {
            StopVideoRecording("camera-only-destroy");
            StopGrip();

            if (_cameraGizmo != null)
            {
                _cameraGizmo.DragStateChanged -= OnGizmoDragStateChanged;
                TransformGizmoApi.DisableCameraInputCaptureOnDrag(_cameraGizmo);
                _cameraGizmo = null;
            }

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
                _renderTexture = null;
            }

            if (_handPreviewObject != null)
            {
                Destroy(_handPreviewObject);
                _handPreviewObject = null;
            }
            if (_handPreviewMaterial != null)
            {
                Destroy(_handPreviewMaterial);
                _handPreviewMaterial = null;
            }

            if (_cameraAnchorObject != null)
            {
                Destroy(_cameraAnchorObject);
                _cameraAnchorObject = null;
            }

            _subCamera = null;
            _cameraVisualObject = null;
            _cameraLensObject = null;
            ClearBoneFollow();
            CancelTransition("camera-only-destroy");
            LogInfo("camera destroyed");
        }

        private void DestroyDisplayOnly()
        {
            if (_displayGizmo != null)
            {
                _displayGizmo.DragStateChanged -= OnGizmoDragStateChanged;
                TransformGizmoApi.DisableCameraInputCaptureOnDrag(_displayGizmo);
                _displayGizmo = null;
            }

            if (_displayMaterial != null)
            {
                Destroy(_displayMaterial);
                _displayMaterial = null;
            }

            if (_displayOverlayCameraObject != null)
            {
                Destroy(_displayOverlayCameraObject);
                _displayOverlayCameraObject = null;
                _displayOverlayCamera = null;
            }

            if (_displayAnchorObject != null)
            {
                Destroy(_displayAnchorObject);
                _displayAnchorObject = null;
            }

            _displayObject = null;
            LogInfo("display destroyed");
        }

        private void DestroyProbe()
        {
            StopVideoRecording("probe-destroy");
            StopGrip();

            if (_cameraGizmo != null)
                _cameraGizmo.DragStateChanged -= OnGizmoDragStateChanged;
            if (_displayGizmo != null)
                _displayGizmo.DragStateChanged -= OnGizmoDragStateChanged;
            if (_cameraGizmo != null)
                TransformGizmoApi.DisableCameraInputCaptureOnDrag(_cameraGizmo);
            if (_displayGizmo != null)
                TransformGizmoApi.DisableCameraInputCaptureOnDrag(_displayGizmo);

            _cameraGizmo = null;
            _displayGizmo = null;

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
                _renderTexture = null;
            }

            if (_displayMaterial != null)
            {
                Destroy(_displayMaterial);
                _displayMaterial = null;
            }

            if (_handPreviewMaterial != null)
            {
                Destroy(_handPreviewMaterial);
                _handPreviewMaterial = null;
            }

            if (_displayOverlayCameraObject != null)
            {
                Destroy(_displayOverlayCameraObject);
                _displayOverlayCameraObject = null;
                _displayOverlayCamera = null;
            }

            if (_rootObject != null)
                Destroy(_rootObject);

            _rootObject = null;
            _cameraAnchorObject = null;
            _displayAnchorObject = null;
            _cameraVisualObject = null;
            _cameraLensObject = null;
            _displayObject = null;
            _handPreviewObject = null;
            _subCamera = null;
            ClearBoneFollow();
            LogInfo("probe destroyed");
        }

        private static Camera ResolveWorldCamera()
        {
            Camera main = Camera.main;
            if (main != null && main.isActiveAndEnabled)
                return main;

            Camera[] cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null || !camera.isActiveAndEnabled || camera.targetTexture != null)
                    continue;
                return camera;
            }

            return null;
        }

        private void MoveProbeInFrontOfPlayer(bool initialCreate)
        {
            if (_rootObject == null)
                return;

            bool rootAtView = !initialCreate;
            if (TryResolveFrontRootPose(_settings.SpawnDistance, rootAtView, out Vector3 targetPos, out Quaternion targetRot, out string source))
            {
                _rootObject.transform.SetPositionAndRotation(targetPos, targetRot);
                if (!initialCreate)
                    ResetLocalLayoutForFront();
                LogInfo((initialCreate ? "probe spawn at front" : "probe moved to front")
                    + " source=" + source
                    + " root=" + targetPos.ToString("F3"));
                return;
            }

            LogWarn(initialCreate ? "probe spawn skipped: front anchor not found" : "probe move skipped: front anchor not found");
        }

        private void MoveCameraInFrontOfPlayer()
        {
            EnsureCamera();
            if (_cameraAnchorObject == null)
            {
                SetStatus("サブカメラ未生成");
                return;
            }

            if (!TryResolveFrontPlacement(_settings.SpawnDistance, Vector3.zero, out Vector3 targetPos, out _, out Quaternion forwardRot, out string source))
            {
                SetStatus("視点基準なし");
                return;
            }

            ClearBoneFollow();
            _cameraAnchorObject.transform.SetPositionAndRotation(targetPos, forwardRot);
            PersistTransformsToSettings();
            ApplyCameraSettings();
            LogInfo("camera moved to front source=" + source + " pos=" + targetPos.ToString("F3"));
            SetStatus("カメラを眼の前へ移動");
        }

        private void MoveDisplayInFrontOfPlayer()
        {
            EnsureDisplay();
            if (_displayAnchorObject == null)
            {
                SetStatus("ディスプレイ未生成");
                return;
            }

            if (!TryResolveFrontPlacement(_settings.SpawnDistance, Vector3.zero, out Vector3 targetPos, out _, out Quaternion displayRot, out string source))
            {
                SetStatus("視点基準なし");
                return;
            }

            _displayAnchorObject.transform.SetPositionAndRotation(targetPos, displayRot);
            PersistTransformsToSettings();
            ApplyDisplaySettings();
            LogInfo("display moved to front source=" + source + " pos=" + targetPos.ToString("F3"));
            SetStatus("ディスプレイを眼の前へ移動");
        }

        private static bool TryResolveFrontRootPose(
            float distance,
            bool rootAtView,
            out Vector3 position,
            out Quaternion rotation,
            out string source)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            source = "none";

            if (!TryResolveFrontReference(out Vector3 referencePosition, out Quaternion referenceRotation, out source))
                return false;

            float safeDistance = Mathf.Max(0.1f, distance);
            position = rootAtView
                ? referencePosition
                : referencePosition + referenceRotation * (Vector3.forward * safeDistance);
            rotation = referenceRotation;
            return true;
        }

        private static bool TryResolveFrontPlacement(
            float distance,
            Vector3 localOffset,
            out Vector3 position,
            out Quaternion facingPlayerRotation,
            out Quaternion displayRotation,
            out string source)
        {
            position = Vector3.zero;
            facingPlayerRotation = Quaternion.identity;
            displayRotation = Quaternion.identity;
            source = "none";

            if (!TryResolveFrontReference(out Vector3 referencePosition, out Quaternion referenceRotation, out source))
                return false;

            float safeDistance = Mathf.Max(0.1f, distance);
            Vector3 offset = localOffset + Vector3.forward * safeDistance;
            position = referencePosition + referenceRotation * offset;
            facingPlayerRotation = referenceRotation * Quaternion.Euler(0f, 180f, 0f);
            displayRotation = referenceRotation;
            return true;
        }

        private void ResetLocalLayoutForFront()
        {
            ClearBoneFollow();

            float distance = Mathf.Max(0.1f, _settings.SpawnDistance);

            _settings.CameraPosX = 0.45f;
            _settings.CameraPosY = -0.12f;
            _settings.CameraPosZ = distance;
            _settings.CameraRotX = 0f;
            _settings.CameraRotY = 0f;
            _settings.CameraRotZ = 0f;

            _settings.DisplayPosX = -0.45f;
            _settings.DisplayPosY = -0.12f;
            _settings.DisplayPosZ = distance;
            _settings.DisplayRotX = 0f;
            _settings.DisplayRotY = 0f;
            _settings.DisplayRotZ = 0f;

            ApplyCameraSettings();
            ApplyDisplaySettings();
            SaveSettings();
        }

        private static bool TryResolveFrontReference(out Vector3 position, out Quaternion rotation, out string source)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            source = "none";

            try
            {
                if (VR.Active)
                {
                    Transform head = ResolveVrHead();
                    if (head != null)
                    {
                        position = head.position;
                        rotation = head.rotation;
                        source = "VR.Head:" + head.name;
                        return true;
                    }

                    Camera vrMain = Camera.main;
                    if (vrMain != null && vrMain.isActiveAndEnabled && vrMain.GetComponent<SteamVR_Camera>() != null)
                    {
                        position = vrMain.transform.position;
                        rotation = vrMain.transform.rotation;
                        source = "VR.Camera.main:" + vrMain.name;
                        return true;
                    }

                    source = "VR.HeadMissing";
                    return false;
                }
            }
            catch
            {
                source = "VR.ResolveException";
                return false;
            }

            Camera main = Camera.main;
            if (main != null && main.isActiveAndEnabled)
            {
                position = main.transform.position;
                rotation = ResolveDesktopFrontRotation(main.transform);
                source = "Camera.main:" + main.name;
                return true;
            }

            Transform fallback = ResolveSpawnAnchor();
            if (fallback == null)
                return false;

            position = fallback.position;
            rotation = ResolveDesktopFrontRotation(fallback);
            source = "Camera.fallback:" + fallback.name;
            return true;
        }

        private static Transform ResolveVrHead()
        {
            try
            {
                VRCamera camera = VR.Camera;
                if (camera == null || camera.SteamCam == null || camera.Head == null)
                    return null;
                return camera.Head;
            }
            catch
            {
                return null;
            }
        }

        private static Quaternion ResolveDesktopFrontRotation(Transform anchor)
        {
            Vector3 forward = anchor != null ? Vector3.ProjectOnPlane(anchor.forward, Vector3.up) : Vector3.forward;
            if (forward.sqrMagnitude < 1e-4f && anchor != null)
                forward = anchor.forward;
            if (forward.sqrMagnitude < 1e-4f)
                forward = Vector3.forward;
            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private static Transform ResolveSpawnAnchor()
        {
            Camera main = Camera.main;
            return main != null && main.isActiveAndEnabled ? main.transform : null;
        }

        private void EnsureDisplayOverlayCamera()
        {
            if (!_settings.UseDisplayOverlayCamera)
                return;

            if (_displayOverlayCamera != null)
                return;

            _displayOverlayCameraObject = new GameObject("SubCameraDisplayOverlayCamera");
            DontDestroyOnLoad(_displayOverlayCameraObject);
            _displayOverlayCamera = _displayOverlayCameraObject.AddComponent<Camera>();
            _displayOverlayCamera.clearFlags = CameraClearFlags.Nothing;
            _displayOverlayCamera.cullingMask = 1 << Mathf.Clamp(_settings.DisplayLayer, 0, 30);
            _displayOverlayCamera.nearClipPlane = 0.01f;
            _displayOverlayCamera.farClipPlane = 1000f;
            _displayOverlayCamera.enabled = true;
            SyncDisplayOverlayCamera();
        }

        private void SyncDisplayOverlayCamera()
        {
            int displayMask = 1 << Mathf.Clamp(_settings.DisplayLayer, 0, 30);
            Camera main = ResolveWorldCamera();
            bool useOverlay = _settings.UseDisplayOverlayCamera;
            if (_displayOverlayCamera == null)
            {
                if (useOverlay)
                    EnsureDisplayOverlayCamera();
            }

            if (_displayOverlayCamera != null && !useOverlay)
                _displayOverlayCamera.enabled = false;

            if (_displayOverlayCamera != null && useOverlay && main != null)
            {
                _displayOverlayCamera.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
                _displayOverlayCamera.fieldOfView = main.fieldOfView;
                _displayOverlayCamera.orthographic = main.orthographic;
                _displayOverlayCamera.orthographicSize = main.orthographicSize;
                _displayOverlayCamera.nearClipPlane = main.nearClipPlane;
                _displayOverlayCamera.farClipPlane = main.farClipPlane;
                _displayOverlayCamera.depth = main.depth + 100f;
                _displayOverlayCamera.cullingMask = displayMask;
                _displayOverlayCamera.clearFlags = CameraClearFlags.Nothing;
                _displayOverlayCamera.allowHDR = false;
                _displayOverlayCamera.allowMSAA = false;
                _displayOverlayCamera.enabled = true;
            }

            Camera[] cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null || camera == _displayOverlayCamera)
                    continue;
                if (camera == _subCamera)
                {
                    camera.cullingMask &= ~displayMask;
                    continue;
                }

                if (camera.targetTexture == null)
                {
                    if (useOverlay)
                        camera.cullingMask &= ~displayMask;
                    else
                    {
                        if (camera == main)
                            camera.cullingMask |= displayMask;
                        else
                            camera.cullingMask &= ~displayMask;
                    }
                }
            }
        }

        private void ApplyDisplayLayer()
        {
            int layer = Mathf.Clamp(_settings.DisplayLayer, 0, 30);
            SetLayerRecursive(_displayObject, layer);
            SetLayerRecursive(_handPreviewObject, layer);
            SyncDisplayOverlayCamera();
        }

        private static void SetLayerRecursive(GameObject target, int layer)
        {
            if (target == null)
                return;

            target.layer = layer;
            Transform t = target.transform;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursive(t.GetChild(i).gameObject, layer);
        }

        private static FilterMode ParseFilterMode(string raw)
        {
            string value = raw == null ? string.Empty : raw.Trim().ToLowerInvariant();
            switch (value)
            {
                case "bilinear":
                    return FilterMode.Bilinear;
                case "trilinear":
                    return FilterMode.Trilinear;
                default:
                    return FilterMode.Point;
            }
        }

        private static Material CreateDisplayMaterial(bool backFace)
        {
            Material material = new Material(Shader.Find("Unlit/Texture") ?? Shader.Find("Standard"));
            material.name = backFace ? "SubCameraDisplayBackMaterial" : "SubCameraDisplayMaterial";
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            material.SetOverrideTag("RenderType", "Opaque");
            material.SetOverrideTag("Queue", "Geometry");
            if (material.HasProperty("_Cull"))
                material.SetInt("_Cull", 0);
            if (material.HasProperty("_ZWrite"))
                material.SetInt("_ZWrite", 1);
            if (material.HasProperty("_ZTest"))
                material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
            if (material.HasProperty("_Mode"))
                material.SetFloat("_Mode", 0f);
            if (material.HasProperty("_SrcBlend"))
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            if (material.HasProperty("_DstBlend"))
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            return material;
        }
    }
}
