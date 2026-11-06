using System;
using System.IO;
using UnityEngine;

namespace MainGirlHipHijack
{
    public sealed partial class Plugin
    {
        private void CapturePresetScreenshotWithoutUi(string path)
        {
            var camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("撮影カメラがありません。");
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            float previousAspect = camera.aspect;
            int previousCullingMask = camera.cullingMask;
            RenderTexture render = null;
            Texture2D pixels = null;
            try
            {
                render = RenderTexture.GetTemporary(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height), 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = render;
                camera.aspect = (float)render.width / render.height;
                // Render the scene directly, before screen UI/IMGUI composition.
                // Exclude Unity's UI layer and TransformGizmo's dedicated overlay layer.
                // Do not change Canvas.enabled, window visibility, or input ownership.
                camera.cullingMask = previousCullingMask & ~(1 << 5) & ~(1 << 31);
                camera.Render();
                RenderTexture.active = render;
                pixels = new Texture2D(render.width, render.height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (camera != null)
                {
                    camera.targetTexture = previousTarget;
                    camera.aspect = previousAspect;
                    camera.cullingMask = previousCullingMask;
                }
                if (pixels != null) Destroy(pixels);
                if (render != null) RenderTexture.ReleaseTemporary(render);
            }
        }
    }
}
