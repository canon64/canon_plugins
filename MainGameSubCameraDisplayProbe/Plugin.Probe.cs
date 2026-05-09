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

            _cameraAnchorObject = new GameObject("SubCameraAnchor");
            _cameraAnchorObject.transform.SetParent(_rootObject.transform, false);
            _cameraAnchorObject.transform.localPosition = new Vector3(_settings.CameraPosX, _settings.CameraPosY, _settings.CameraPosZ);
            _cameraAnchorObject.transform.localRotation = Quaternion.Euler(_settings.CameraRotX, _settings.CameraRotY, _settings.CameraRotZ);

            _displayAnchorObject = new GameObject("DisplayAnchor");
            _displayAnchorObject.transform.SetParent(_rootObject.transform, false);
            _displayAnchorObject.transform.localPosition = new Vector3(_settings.DisplayPosX, _settings.DisplayPosY, _settings.DisplayPosZ);
            _displayAnchorObject.transform.localRotation = Quaternion.Euler(_settings.DisplayRotX, _settings.DisplayRotY, _settings.DisplayRotZ);

            _cameraVisualObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cameraVisualObject.name = "CameraMarker";
            DestroyImmediate(_cameraVisualObject.GetComponent<Collider>());
            _cameraVisualObject.transform.SetParent(_cameraAnchorObject.transform, false);
            _cameraVisualObject.transform.localPosition = Vector3.zero;
            _cameraVisualObject.transform.localRotation = Quaternion.identity;
            _cameraVisualObject.transform.localScale = new Vector3(0.1f, 0.06f, 0.16f);

            GameObject lensObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lensObject.name = "CameraLens";
            DestroyImmediate(lensObject.GetComponent<Collider>());
            lensObject.transform.SetParent(_cameraAnchorObject.transform, false);
            lensObject.transform.localPosition = new Vector3(0f, 0f, -0.11f);
            lensObject.transform.localScale = Vector3.one * 0.04f;

            GameObject subCameraObject = new GameObject("SubCamera");
            subCameraObject.transform.SetParent(_cameraAnchorObject.transform, false);
            subCameraObject.transform.localPosition = Vector3.zero;
            subCameraObject.transform.localRotation = Quaternion.identity;
            _subCamera = subCameraObject.AddComponent<Camera>();
            _subCamera.clearFlags = CameraClearFlags.SolidColor;
            _subCamera.backgroundColor = Color.black;

            _renderTexture = new RenderTexture(_settings.RenderWidth, _settings.RenderHeight, 24, RenderTextureFormat.ARGB32);
            _renderTexture.name = "SubCameraDisplayProbeRT";
            _renderTexture.useMipMap = false;
            _renderTexture.autoGenerateMips = false;
            _renderTexture.antiAliasing = 1;
            _renderTexture.filterMode = ParseFilterMode(_settings.RenderFilterMode);
            _renderTexture.wrapMode = TextureWrapMode.Clamp;
            _renderTexture.Create();
            _subCamera.targetTexture = _renderTexture;
            _subCamera.allowHDR = false;
            _subCamera.allowMSAA = false;

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
            AttachGizmos();
            ApplyCameraSettings();
            ApplyDisplaySettings();
            LogInfo("probe created");
        }

        private void AttachGizmos()
        {
            _cameraGizmo = TransformGizmoApi.Attach(_cameraAnchorObject);
            _displayGizmo = TransformGizmoApi.Attach(_displayAnchorObject);
            if (_cameraGizmo != null)
            {
                TransformGizmoApi.EnableCameraInputCaptureOnDrag(_cameraGizmo);
                _cameraGizmo.SetVisible(_settings.CameraGizmoVisible);
                _cameraGizmo.SetSizeMultiplier(_settings.GizmoSizeMultiplier);
                _cameraGizmo.DragStateChanged += OnGizmoDragStateChanged;
            }

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

        private void DestroyProbe()
        {
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
            _displayObject = null;
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

            if (TryResolveFrontPose(_settings.SpawnDistance, out Vector3 targetPos, out Quaternion targetRot, out _))
            {
                _rootObject.transform.SetPositionAndRotation(targetPos, targetRot);
                if (!initialCreate)
                    ResetLocalLayoutForFront();
                LogInfo(initialCreate ? "probe spawn at front" : "probe moved to front");
                return;
            }

            LogWarn(initialCreate ? "probe spawn skipped: front anchor not found" : "probe move skipped: front anchor not found");
        }

        private void MoveCameraInFrontOfPlayer()
        {
            EnsureProbe();
            if (_cameraAnchorObject == null)
            {
                SetStatus("サブカメラ未生成");
                return;
            }

            if (!TryResolveFrontPose(_settings.SpawnDistance, out Vector3 targetPos, out _, out Quaternion facingPlayerRot))
            {
                SetStatus("視点基準なし");
                return;
            }

            ClearBoneFollow();
            _cameraAnchorObject.transform.SetPositionAndRotation(targetPos, facingPlayerRot);
            PersistTransformsToSettings();
            ApplyCameraSettings();
            SetStatus("カメラを眼の前へ移動");
        }

        private void MoveDisplayInFrontOfPlayer()
        {
            EnsureProbe();
            if (_displayAnchorObject == null)
            {
                SetStatus("ディスプレイ未生成");
                return;
            }

            if (!TryResolveFrontPose(_settings.SpawnDistance, out Vector3 targetPos, out _, out Quaternion facingPlayerRot))
            {
                SetStatus("視点基準なし");
                return;
            }

            _displayAnchorObject.transform.SetPositionAndRotation(targetPos, facingPlayerRot);
            PersistTransformsToSettings();
            ApplyDisplaySettings();
            SetStatus("ディスプレイを眼の前へ移動");
        }

        private static bool TryResolveFrontPose(
            float distance,
            out Vector3 position,
            out Quaternion forwardRotation,
            out Quaternion facingPlayerRotation)
        {
            position = Vector3.zero;
            forwardRotation = Quaternion.identity;
            facingPlayerRotation = Quaternion.identity;

            Transform anchor = ResolveFrontAnchor();
            if (anchor == null)
                return false;

            Vector3 forward = Vector3.ProjectOnPlane(anchor.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f)
                forward = anchor.forward;
            if (forward.sqrMagnitude < 1e-4f)
                forward = Vector3.forward;
            forward.Normalize();

            position = anchor.position + forward * Mathf.Max(0.1f, distance);
            forwardRotation = Quaternion.LookRotation(forward, Vector3.up);
            facingPlayerRotation = Quaternion.LookRotation(-forward, Vector3.up);
            return true;
        }

        private void ResetLocalLayoutForFront()
        {
            ClearBoneFollow();

            _settings.CameraPosX = 0f;
            _settings.CameraPosY = 0f;
            _settings.CameraPosZ = 0.6f;
            _settings.CameraRotX = 0f;
            _settings.CameraRotY = 180f;
            _settings.CameraRotZ = 0f;

            _settings.DisplayPosX = 0f;
            _settings.DisplayPosY = 0f;
            _settings.DisplayPosZ = 0f;
            _settings.DisplayRotX = 0f;
            _settings.DisplayRotY = 180f;
            _settings.DisplayRotZ = 0f;

            ApplyCameraSettings();
            ApplyDisplaySettings();
            SaveSettings();
        }

        private static Transform ResolveFrontAnchor()
        {
            try
            {
                if (VR.Active && VR.Camera != null && VR.Camera.SteamCam != null && VR.Camera.Head != null)
                    return VR.Camera.Head;
            }
            catch
            {
            }

            Camera main = Camera.main;
            if (main != null && main.isActiveAndEnabled)
                return main.transform;

            return ResolveSpawnAnchor();
        }

        private static Transform ResolveSpawnAnchor()
        {
            if (VR.Active && VR.Mode != null)
            {
                if (VR.Mode.Right != null)
                    return ((Component)VR.Mode.Right).transform;
                if (VR.Mode.Left != null)
                    return ((Component)VR.Mode.Left).transform;
            }

            return Camera.main != null ? Camera.main.transform : null;
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
