using System;
using System.Reflection;
using UnityEngine;

namespace AstralMode
{
    public sealed partial class Plugin
    {
        private VrTransitionRenderer _vrTransition;

        private bool TryBeginVrTransition()
        {
            if (_vrTransition != null) return true;
            if (!IsVrActive()) return true;
            try
            {
                Type vrType = FindType("VRGIN.Core.VR", "VRGIN_OpenXR");
                object rig = vrType?.GetProperty("Camera", BindingFlags.Public | BindingFlags.Static)?.GetValue(null, null);
                var steam = rig?.GetType().GetProperty("SteamCam")?.GetValue(rig, null) as Component;
                Camera camera = steam != null ? steam.GetComponent<Camera>() : null;
                if (camera == null) throw new InvalidOperationException("VR.Camera.SteamCam の Camera を取得できません。");
                Type helper = FindType("VRGIN.Helpers.UnityHelper", "VRGIN_OpenXR");
                var shader = helper?.GetMethod("GetShader", new[] { typeof(string) })
                    ?.Invoke(null, new object[] { "Custom/SteamVR_Fade" }) as Shader;
                if (shader == null || !shader.isSupported)
                    throw new InvalidOperationException("SteamVRの白フェード用シェーダーを使用できません。");
                _vrTransition = camera.gameObject.AddComponent<VrTransitionRenderer>();
                _vrTransition.Initialize(camera, shader, ReportVrTransitionFailure);
                // Keep the normal-mode Bloom pipeline and its settings.
                // Never add/enable a post-process layer or change its volume mask.
                return true;
            }
            catch (Exception ex)
            {
                EndVrTransition();
                ReportVrTransitionFailure("VR移行演出の開始", ex);
                return false;
            }
        }

        private void ReportVrTransitionFailure(string operation, Exception ex)
        {
            MainGameNotifications.NotificationApi.Publish(Guid, "アストラルVR演出の失敗",
                operation + ": " + (ex.InnerException ?? ex).Message,
                MainGameNotifications.NotificationSeverity.Failure);
        }

        private void EndVrTransition()
        {
            if (_vrTransition == null) return;
            var renderer = _vrTransition;
            _vrTransition = null;
            try { renderer.Release(); }
            finally
            {
                Destroy(renderer);
                _fovCaptured = false;
                _fovTargetCamera = null;
                _fovTargetCameraId = 0;
                DetachFovPreCullHook();
            }
        }

        private void OnDisable()
        {
            EndVrTransition();
            RestoreVrView(true);
        }
    }
}
