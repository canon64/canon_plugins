using System;
using System.Collections.Generic;
using UnityEngine;

namespace MainGameGlow
{
    public sealed partial class Plugin
    {
        private void SetupCaptureCamera()
        {
            _cameraRoot = new GameObject("MainGameGlowCapture");
            _cameraRoot.hideFlags = HideFlags.DontSave;

            _captureCamera = _cameraRoot.AddComponent<Camera>();
            _captureCamera.enabled = false;
            _captureCamera.allowHDR = true;

            _captureGlowInitFailed = false;
            _captureGlowTemplateWarned = false;

            LogPlugin("INFO", "capture camera created");
        }

        private RenderTexture CreateRT(int w, int h)
        {
            RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            rt.Create();
            return rt;
        }

        private void ReleaseCaptureRt()
        {
            if (_captureRt != null)
            {
                _captureRt.Release();
                Destroy(_captureRt);
                _captureRt = null;
            }
        }

        private Camera ResolveCamera()
        {
            string filter = _cfgCameraNameFilter.Value ?? "";
            bool hasFilter = filter.Length > 0;

            if (_cfgPreferCameraMain.Value && Camera.main != null && Camera.main.enabled)
            {
                if (!hasFilter || Camera.main.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    return Camera.main;
            }

            Camera[] all = Camera.allCameras;
            if (all == null || all.Length == 0)
                return null;

            List<Camera> candidates = new List<Camera>(all.Length);
            foreach (Camera c in all)
            {
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy)
                    continue;
                if (c == _captureCamera)
                    continue;
                candidates.Add(c);
            }

            if (candidates.Count == 0)
                return null;

            candidates.Sort((a, b) => b.depth.CompareTo(a.depth));

            if (hasFilter)
            {
                foreach (Camera c in candidates)
                {
                    if (c.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                        return c;
                }
            }

            int idx = Mathf.Clamp(_cfgCameraFallbackIndex.Value, 0, candidates.Count - 1);
            return candidates[idx];
        }

        private bool IsGlowRequested()
        {
            return _cfgGlowStrength != null
                && _cfgGlowBlurPercent != null
                && _cfgGlowStrength.Value > 0.0001f
                && _cfgGlowBlurPercent.Value > 0.0001f;
        }

        private void CaptureGlow(Camera src)
        {
            if (_captureCamera == null || _captureRt == null || src == null)
                return;

            _captureCamera.CopyFrom(src);
            _captureCamera.cullingMask = _characterMask;
            _captureCamera.clearFlags = CameraClearFlags.SolidColor;
            _captureCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _captureCamera.allowHDR = true;
            _captureCamera.targetTexture = _captureRt;

            bool pipelineReady = false;
            if (IsGlowRequested())
                pipelineReady = EnsureCaptureGlowPipeline(src);

            ApplyCaptureGlowSettings(pipelineReady);

            _captureCamera.Render();
            _captureCamera.targetTexture = null;
        }
    }
}
