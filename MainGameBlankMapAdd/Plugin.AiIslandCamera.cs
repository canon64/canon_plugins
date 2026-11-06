using System;
using HarmonyLib;
using UnityEngine;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private sealed class AiIslandCameraScope : IDisposable
        {
            private readonly CameraControl_Ver2 _controller;
            private readonly Camera _camera;
            private readonly bool _limitPosition, _limitDistance, _limitRotation;
            private readonly float _moveSpeed, _farClip;
            private readonly float _shadowDistance;
            private readonly bool _occlusion;
            private readonly CameraClearFlags _clearFlags;
            private readonly DepthTextureMode _depthMode;
            private readonly BaseCameraControl_Ver2.CameraData _cameraData;
            private bool _restored;
            internal AiIslandCameraScope(CameraControl_Ver2 controller)
            {
                _controller = controller; _camera = controller.thisCmaera;
                _limitPosition = controller.isLimitPos; _limitDistance = controller.isLimitDir;
                _limitRotation = controller.isLlimitRot; _moveSpeed = controller.moveSpeed;
                _cameraData = controller.GetCameraData();
                _shadowDistance = QualitySettings.shadowDistance;
                if (_camera != null)
                {
                    _farClip = _camera.farClipPlane; _clearFlags = _camera.clearFlags;
                    _depthMode = _camera.depthTextureMode;
                    _occlusion = _camera.useOcclusionCulling;
                }
            }
            internal bool Owns(BaseCameraControl_Ver2 controller) => _controller != null && _controller == controller;
            internal void Apply(float multiplier, float farClip, float shadowDistance)
            {
                if (_controller == null || _restored) return;
                _controller.isLimitPos = false; _controller.isLimitDir = false; _controller.isLlimitRot = false;
                _controller.moveSpeed = _moveSpeed * Mathf.Clamp(multiplier, 1f, 100f);
                QualitySettings.shadowDistance = Mathf.Clamp(shadowDistance, 10f, 300f);
                if (_camera != null)
                {
                    _camera.farClipPlane = Mathf.Clamp(farClip, 50f, 10000f);
                    // Source baked occlusion is at the original position and scale, so it is invalid here.
                    _camera.useOcclusionCulling = false;
                    _camera.clearFlags = CameraClearFlags.Skybox;
                    _camera.depthTextureMode = _depthMode | DepthTextureMode.Depth;
                }
            }
            public void Dispose()
            {
                if (_restored) return;
                _restored = true;
                QualitySettings.shadowDistance = _shadowDistance;
                if (_controller != null)
                {
                    _controller.isLimitPos = _limitPosition; _controller.isLimitDir = _limitDistance;
                    _controller.isLlimitRot = _limitRotation; _controller.moveSpeed = _moveSpeed;
                    _controller.SetCameraData(_cameraData);
                }
                if (_camera != null)
                {
                    _camera.farClipPlane = _farClip; _camera.clearFlags = _clearFlags;
                    _camera.depthTextureMode = _depthMode;
                    _camera.useOcclusionCulling = _occlusion;
                }
            }
        }

        // Settings are applied on island attachment/configuration changes, never every camera frame.

        private struct AiIslandKeyState { internal bool Active; internal Vector3 Position; }
        [HarmonyPrefix, HarmonyPatch(typeof(BaseCameraControl_Ver2), "InputKeyProc")]
        private static void AiIslandKeyPrefix(BaseCameraControl_Ver2 __instance, out AiIslandKeyState __state)
        {
            Plugin owner = _aiIslandOwner;
            bool active = owner != null && owner._aiIslandActive && owner._aiIslandCamera != null && owner._aiIslandCamera.Owns(__instance) &&
                (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.UpArrow) ||
                 Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.PageUp) || Input.GetKey(KeyCode.PageDown)) &&
                !Input.GetKey(KeyCode.R) && !Input.GetKey(KeyCode.Keypad5);
            __state = new AiIslandKeyState { Active = active, Position = active ? __instance.GetCameraData().Pos : Vector3.zero };
        }
        [HarmonyPostfix, HarmonyPatch(typeof(BaseCameraControl_Ver2), "InputKeyProc")]
        private static void AiIslandKeyPostfix(BaseCameraControl_Ver2 __instance, AiIslandKeyState __state)
        {
            Plugin owner = _aiIslandOwner;
            if (!__state.Active || owner == null || !owner._aiIslandActive) return;
            // The original method still owns key handling and UI exclusion; only scale its resulting translation.
            BaseCameraControl_Ver2.CameraData data = __instance.GetCameraData();
            data.Pos = __state.Position + (data.Pos - __state.Position) * Mathf.Clamp(owner._aiMoveMultiplier.Value, 1f, 100f);
            __instance.SetCameraData(data);
        }
    }
}
