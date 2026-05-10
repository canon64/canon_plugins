using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRGIN.Core;

namespace SimpleAfterimage
{
    public sealed partial class Plugin
    {
        private void SetupCaptureCamera()
        {
            _cameraRoot = new GameObject("SimpleAfterimageCapture");
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
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            rt.Create();
            return rt;
        }

        private void ReleaseSlots()
        {
            if (_slots == null) return;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null) { _slots[i].Release(); Destroy(_slots[i]); _slots[i] = null; }
            }
        }

        private Camera ResolveCamera()
        {
            // KKS_VR (VR MOD) が有効な場合は SteamVR_Camera を使う
            if (VR.Active && VR.Camera != null && VR.Camera.SteamCam != null)
                return ((Component)VR.Camera.SteamCam).GetComponent<Camera>();

            string filter = _cfgCameraNameFilter.Value ?? "";
            bool hasFilter = filter.Length > 0;

            if (_cfgPreferCameraMain.Value && Camera.main != null && Camera.main.enabled)
            {
                if (!hasFilter || Camera.main.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    return Camera.main;
            }

            Camera[] all = Camera.allCameras;
            if (all == null || all.Length == 0) return null;

            var candidates = new List<Camera>(all.Length);
            foreach (Camera c in all)
            {
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;
                if (c == _captureCamera) continue;
                candidates.Add(c);
            }
            if (candidates.Count == 0) return null;
            candidates.Sort((a, b) => b.depth.CompareTo(a.depth));

            if (hasFilter)
            {
                foreach (Camera c in candidates)
                    if (c.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                        return c;
            }

            int idx = Mathf.Clamp(_cfgCameraFallbackIndex.Value, 0, candidates.Count - 1);
            return candidates[idx];
        }

        private void SyncOverlayDrawer(Camera srcCamera)
        {
            if (!_cfgEnabled.Value || srcCamera == null)
            {
                if (_overlayDrawer != null) { Destroy(_overlayDrawer); _overlayDrawer = null; }
                _lastSourceCamera = null;
                return;
            }

            if (_lastSourceCamera != srcCamera)
            {
                if (_overlayDrawer != null) { Destroy(_overlayDrawer); _overlayDrawer = null; }
                _overlayDrawer = srcCamera.gameObject.AddComponent<OverlayDrawer>();
                _overlayDrawer.Owner = this;
                _lastSourceCamera = srcCamera;
            }
        }

        private float GetHSceneSpeedIntensity()
        {
            if (Time.unscaledTime >= _nextHSceneScanTime)
            {
                _nextHSceneScanTime = Time.unscaledTime + 1f;
                if (_hSceneProc == null || !_hSceneProc)
                    _hSceneProc = FindObjectOfType<HSceneProc>();
            }
            if (_hSceneProc == null || _hSceneProc.flags == null) return -1f;
            float maxSpeed = _hSceneProc.flags.speedMaxBody > 0f ? _hSceneProc.flags.speedMaxBody : 1f;
            return Mathf.Clamp01(_hSceneProc.flags.speedCalc / maxSpeed);
        }

        private IEnumerator CaptureEndOfFrame(Camera src)
        {
            yield return new WaitForEndOfFrame();
            if (!_cfgEnabled.Value || _slots == null || src == null) yield break;
            _captureCamera.CopyFrom(src);
            bool pipelineReady = false;
            if (IsCaptureGlowRequested())
                pipelineReady = EnsureCaptureGlowPipeline(src);
            ApplyCaptureGlowSettings(pipelineReady);
            _captureCamera.cullingMask = _characterMask;
            _captureCamera.clearFlags = CameraClearFlags.SolidColor;
            _captureCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _captureCamera.targetTexture = _slots[_writeIndex];
            _captureCamera.Render();
            _captureCamera.targetTexture = null;
            _life[_writeIndex] = Mathf.Max(1, _cfgFadeFrames.Value);
            _writeIndex = (_writeIndex + 1) % _slots.Length;
        }
    }
}
