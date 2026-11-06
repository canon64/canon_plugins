using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VRGIN.Core;

namespace SimpleAfterimage
{
    public sealed partial class Plugin
    {
        private RenderTexture _captureScratch;
        private Camera[] _cameraScanBuffer = Array.Empty<Camera>();
        private readonly List<Camera> _cameraCandidates = new List<Camera>();
        private readonly List<Camera> _cameraCandidateOrder = new List<Camera>();
        private readonly List<float> _cameraCandidateDepths = new List<float>();
        private static readonly IComparer<Camera> CameraDepthDescending =
            Comparer<Camera>.Create((a, b) => b.depth.CompareTo(a.depth));
        private bool _copyCaptureTextureSupported;
        private readonly List<Renderer> _kaleidoscopeHiddenRenderers = new List<Renderer>();
        private bool _kaleidoscopeThoughtMaskActive;
        private ChaControl _kaleidoscopeThoughtActor;
        private Func<ChaControl, Renderer[]> _kaleidoscopeRendererProvider;
        private int _kaleidoscopeCompositeFrame = -1;

        public static void SetKaleidoscopeThoughtMask(bool active, ChaControl actor)
            => SetKaleidoscopeThoughtMask(active, actor, null);

        public static void SetKaleidoscopeThoughtMask(bool active, ChaControl actor,
            Func<ChaControl, Renderer[]> rendererProvider)
        {
            if (active && !SessionGate.Allowed) return;

            Plugin plugin = Instance;
            if (plugin == null) return;
            plugin._kaleidoscopeThoughtMaskActive = active;
            plugin._kaleidoscopeThoughtActor = active ? actor : null;
            plugin._kaleidoscopeRendererProvider = active && actor != null ? rendererProvider : null;
        }

        private void HideKaleidoscopeActorForCapture()
        {
            ChaControl actor = _kaleidoscopeThoughtActor;
            if (!_kaleidoscopeThoughtMaskActive || actor == null) return;
            // Resolve at capture time: a reload may finish after the owner's Update.
            // The two-argument API retains its legacy behaviour for older callers.
            Renderer[] renderers = _kaleidoscopeRendererProvider != null
                ? _kaleidoscopeRendererProvider(actor)
                : actor.GetComponentsInChildren<Renderer>(true);
            if (renderers == null) return;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled) continue;
                _kaleidoscopeHiddenRenderers.Add(renderer);
                renderer.enabled = false;
            }
        }

        private void RestoreKaleidoscopeActorAfterCapture()
        {
            foreach (Renderer renderer in _kaleidoscopeHiddenRenderers)
                if (renderer != null) renderer.enabled = true;
            _kaleidoscopeHiddenRenderers.Clear();
        }

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

        private RenderTexture CreateRT(int w, int h, int depthBits = 0)
        {
            var rt = new RenderTexture(w, h, depthBits, RenderTextureFormat.ARGB32)
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

        private void ReleaseCaptureCameraRuntime()
        {
            if (_captureCamera != null)
                _captureCamera.targetTexture = null;

            if (_captureScratch != null)
            {
                _captureScratch.Release();
                Destroy(_captureScratch);
                _captureScratch = null;
            }
            _copyCaptureTextureSupported = false;

            CleanupCaptureGlowPipeline();

            if (_cameraRoot != null)
            {
                Destroy(_cameraRoot);
                _cameraRoot = null;
            }

            _captureCamera = null;
        }

        private bool IsHSceneActive()
        {
            return SessionGate.Allowed;
        }

        private void StopAfterimageRuntime()
        {
            StopCaptureLoop();
            // Do not retain cameras from a scene after the effect stops.
            _cameraCandidates.Clear();
            _cameraCandidateOrder.Clear();
            _cameraCandidateDepths.Clear();
            _cameraScanBuffer = Array.Empty<Camera>();
            _externalGlowPaused = false;
            bool hadRuntime = _afterimageRuntimeReady
                || _overlayDrawer != null
                || _lastSourceCamera != null
                || _cameraRoot != null
                || _slots != null
                || _capturePostProcessLayer != null
                || _capturePostProcessVolume != null
                || _capturePostProcessProfile != null
                || _captureGlowVolumeRoot != null;

            _drawCount = 0;
            _frameCounter = 0;
            _writeIndex = 0;
            ClearVrStereoSlots();

            if (!hadRuntime)
                return;

            if (_overlayDrawer != null)
            {
                Destroy(_overlayDrawer);
                _overlayDrawer = null;
            }
            _lastSourceCamera = null;

            ReleaseCaptureCameraRuntime();

            ReleaseSlots();
            ReleaseVrStereoSlots();
            _slots = null;
            _life = null;
            _slotFadeFrames = null;
            _slotAlphaScale = null;
            _slotExternal = null;
            _drawSlots = null;
            _drawAlpha = null;
            _rtWidth = 0;
            _rtHeight = 0;
            _afterimageRuntimeReady = false;
        }

        private Camera ResolveCamera()
        {
            if (VR.Active && VR.Camera != null && VR.Camera.SteamCam != null)
                return ((Component)VR.Camera.SteamCam).GetComponent<Camera>();

            string filter = _cfgCameraNameFilter.Value ?? "";
            bool hasFilter = filter.Length > 0;

            Camera main = _cfgPreferCameraMain.Value ? Camera.main : null;
            if (main != null && main.enabled)
            {
                if (!hasFilter || main.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    return main;
            }

            int required = Camera.allCamerasCount;
            if (_cameraScanBuffer.Length < required)
                Array.Resize(ref _cameraScanBuffer, required);
            int count = Camera.GetAllCameras(_cameraScanBuffer);
            // GetAllCameras only fills the active prefix. Release any old tail.
            Array.Clear(_cameraScanBuffer, count, _cameraScanBuffer.Length - count);
            List<Camera> candidates = _cameraCandidates;
            int eligibleCount = 0;
            bool changed = false;
            for (int i = 0; i < count; i++)
            {
                Camera c = _cameraScanBuffer[i];
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;
                if (c == _captureCamera) continue;
                float depth = c.depth;
                if (eligibleCount == _cameraCandidateOrder.Count)
                {
                    _cameraCandidateOrder.Add(c);
                    _cameraCandidateDepths.Add(depth);
                    changed = true;
                }
                else
                {
                    if (_cameraCandidateOrder[eligibleCount] != c ||
                        !_cameraCandidateDepths[eligibleCount].Equals(depth)) changed = true;
                    _cameraCandidateOrder[eligibleCount] = c;
                    _cameraCandidateDepths[eligibleCount] = depth;
                }
                eligibleCount++;
            }
            if (eligibleCount < _cameraCandidateOrder.Count)
            {
                int removed = _cameraCandidateOrder.Count - eligibleCount;
                _cameraCandidateOrder.RemoveRange(eligibleCount, removed);
                _cameraCandidateDepths.RemoveRange(eligibleCount, removed);
                changed = true;
            }
            if (changed)
            {
                candidates.Clear();
                candidates.AddRange(_cameraCandidateOrder);
                candidates.Sort(CameraDepthDescending);
            }
            if (candidates.Count == 0) return null;

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
            if (!IsEffectivelyEnabled() || srcCamera == null)
            {
                if (_overlayDrawer != null) { Destroy(_overlayDrawer); _overlayDrawer = null; }
                _lastSourceCamera = null;
                return;
            }

            bool useVr = ShouldUseVrStereoRenderImage(srcCamera);
            if (_lastSourceCamera != srcCamera || _overlayDrawer == null ||
                (_overlayDrawer is VrOverlayDrawer) != useVr)
            {
                if (_overlayDrawer != null)
                {
                    _overlayDrawer.enabled = false;
                    Destroy(_overlayDrawer);
                    _overlayDrawer = null;
                }
                _overlayDrawer = useVr
                    ? (OverlayDrawer)srcCamera.gameObject.AddComponent<VrOverlayDrawer>()
                    : srcCamera.gameObject.AddComponent<MonoOverlayDrawer>();
                _overlayDrawer.Owner = this;
                _lastSourceCamera = srcCamera;
            }
        }

        private float GetHSceneSpeedIntensity()
        {
            if (!IsHSceneActive())
                return -1f;

            if (_hSceneProc == null || _hSceneProc.flags == null) return -1f;
            float maxSpeed = _hSceneProc.flags.speedMaxBody > 0f ? _hSceneProc.flags.speedMaxBody : 1f;
            return Mathf.Clamp01(_hSceneProc.flags.speedCalc / maxSpeed);
        }

        private void ScheduleCapture(Camera src)
        {
            _pendingCaptureSource = src;
            if (_captureLoop == null)
                _captureLoop = StartCoroutine(CaptureLoop());
        }

        private void StopCaptureLoop()
        {
            _pendingCaptureSource = null;
            if (_captureLoop == null) return;
            StopCoroutine(_captureLoop);
            _captureLoop = null;
        }

        private IEnumerator CaptureLoop()
        {
            while (true)
            {
                yield return _captureEndOfFrameWait;
                Camera src = _pendingCaptureSource;
                _pendingCaptureSource = null;
                if (src != null)
                    CaptureFrame(src);
            }
        }

        private void CaptureFrame(Camera src)
        {
            if (PauseForExternalGlow()) return;
            if (!IsEffectivelyEnabled() || !IsHSceneActive() || _slots == null || _captureCamera == null || src == null) return;

            InitWarpSlotMetadata();
            bool warpSlot = ShouldCaptureWarpAfterimage();
            if (IsBaseAfterimageCapturePausedByWarp() && !warpSlot) return;

            _captureCamera.CopyFrom(src);
            bool pipelineReady = false;
            if (IsCaptureGlowRequested())
                pipelineReady = EnsureCaptureGlowPipeline(src);
            ApplyCaptureGlowSettings(pipelineReady);
            if (warpSlot && IsWarpFullSceneActive())
            {
                // Whole scene minus UI: keep the source skybox/background so the ghosted frame
                // includes the full background, then drop only the excluded (UI) layer.
                int uiLayer = LayerMask.NameToLayer(_cfgFullSceneExcludeLayer.Value ?? "UI");
                int exclude = uiLayer >= 0 ? (1 << uiLayer) : 0;
                _captureCamera.cullingMask = src.cullingMask & ~exclude;
                _captureCamera.clearFlags = src.clearFlags;
                _captureCamera.backgroundColor = src.backgroundColor;
            }
            else
            {
                _captureCamera.cullingMask = _characterMask;
                _captureCamera.clearFlags = CameraClearFlags.SolidColor;
                _captureCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }
            if (_captureScratch == null || _captureScratch.width != _rtWidth || _captureScratch.height != _rtHeight)
            {
                if (_captureScratch != null)
                {
                    _captureScratch.Release();
                    Destroy(_captureScratch);
                }
                _captureScratch = CreateRT(_rtWidth, _rtHeight, 24);
                _copyCaptureTextureSupported =
                    (SystemInfo.copyTextureSupport & CopyTextureSupport.Basic) != 0;
            }

            _captureCamera.targetTexture = _captureScratch;
            try
            {
                HideKaleidoscopeActorForCapture();
                _captureCamera.Render();
            }
            finally
            {
                RestoreKaleidoscopeActorAfterCapture();
                if (_captureCamera != null) _captureCamera.targetTexture = null;
            }
            if (_slots[_writeIndex] == null)
                _slots[_writeIndex] = CreateRT(_rtWidth, _rtHeight);
            if (_copyCaptureTextureSupported)
                Graphics.CopyTexture(_captureScratch, _slots[_writeIndex]);
            else
                Graphics.Blit(_captureScratch, _slots[_writeIndex]);

            if (warpSlot) MarkCurrentSlotAsWarp();
            else MarkCurrentSlotAsNormal();

            _writeIndex = (_writeIndex + 1) % _slots.Length;
        }
    }
}
