using System.Collections;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace MainGameGlow
{
    [BepInPlugin(Guid, PluginName, Version)]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.kks.maingame.glow";
        public const string PluginName = "MainGameGlow";
        public const string Version = "1.0.0";

        public static Plugin Instance { get; private set; }

        // ---------------- Config ----------------
        private bool _captureQueued;
        private ConfigEntry<bool> _cfgEnabled;
        private ConfigEntry<bool> _cfgVerboseLog;

        private ConfigEntry<bool> _cfgUseScreenSize;
        private ConfigEntry<int> _cfgCaptureWidth;
        private ConfigEntry<int> _cfgCaptureHeight;
        private ConfigEntry<string> _cfgCharaLayer;

        private ConfigEntry<float> _cfgGlowThreshold;
        private ConfigEntry<float> _cfgGlowStrength;
        private ConfigEntry<float> _cfgGlowBlurPercent;

        private ConfigEntry<float> _cfgTintR;
        private ConfigEntry<float> _cfgTintG;
        private ConfigEntry<float> _cfgTintB;
        private ConfigEntry<float> _cfgTintA;
        private ConfigEntry<float> _cfgOverlayAlpha;

        private ConfigEntry<bool> _cfgPreferCameraMain;
        private ConfigEntry<string> _cfgCameraNameFilter;
        private ConfigEntry<int> _cfgCameraFallbackIndex;

        // ---------------- Runtime ----------------

        private Camera _captureCamera;
        private Camera _lastSourceCamera;
        private OverlayDrawer _overlayDrawer;
        private GameObject _cameraRoot;

        private RenderTexture _captureRt;
        private int _characterMask;
        private int _rtWidth;
        private int _rtHeight;

        private PostProcessLayer _capturePostProcessLayer;
        private PostProcessVolume _capturePostProcessVolume;
        private PostProcessProfile _capturePostProcessProfile;
        private Bloom _captureBloom;
        private GameObject _captureGlowVolumeRoot;

        private bool _captureGlowInitFailed;
        private bool _captureGlowTemplateWarned;

        private string _pluginLogPath;
        private string _lastGlowDecision;
        private float _nextGlowHeartbeatTime;

        private void Awake()
        {
            Instance = this;

            string pluginDir = Path.GetDirectoryName(Info.Location) ?? Paths.PluginPath;
            _pluginLogPath = Path.Combine(pluginDir, "MainGameGlow.log");

            LogPlugin("INFO", "=== Awake start ===");

            SetupConfig();
            LoadOrCreateSettingsJson();
            ApplyConfig();

            Logger.LogInfo($"{PluginName} {Version} loaded.");
            LogPlugin("INFO", $"{PluginName} {Version} loaded");
        }

        private void Update()
        {
            if (!_cfgEnabled.Value)
            {
                SyncOverlayDrawer(null);
                return;
            }

            if (_cfgUseScreenSize.Value)
            {
                int curW = Screen.width;
                int curH = Screen.height;
                if (curW != _rtWidth || curH != _rtHeight)
                    ApplyConfig();
            }

            Camera src = ResolveCamera();
            SyncOverlayDrawer(src);
        }

        private void LateUpdate()
        {
            if (!_cfgEnabled.Value)
                return;

            Camera src = ResolveCamera();
            if (src == null || _captureRt == null)
                return;

            if (!_captureQueued)
                StartCoroutine(CaptureGlowEndOfFrame(src));
        }

        private IEnumerator CaptureGlowEndOfFrame(Camera src)
        {
            _captureQueued = true;
            yield return new WaitForEndOfFrame();
            _captureQueued = false;

            if (!_cfgEnabled.Value || _captureRt == null || src == null)
                yield break;

            CaptureGlow(src);
        }

        internal static bool TryGetGlowRenderTextureInternalApi(out RenderTexture rt, out string reason)
        {
            rt = null;
            reason = string.Empty;
            if (Instance == null)
            {
                reason = "instance_null";
                return false;
            }
            if (Instance._captureRt == null)
            {
                reason = "render_texture_not_ready";
                return false;
            }
            rt = Instance._captureRt;
            reason = "ok";
            return true;
        }

        internal static bool TryGetEnabledInternalApi(out bool enabled)
        {
            enabled = false;
            if (Instance == null || Instance._cfgEnabled == null)
                return false;
            enabled = Instance._cfgEnabled.Value;
            return true;
        }

        internal static bool TrySetEnabledInternalApi(bool enabled)
        {
            if (Instance == null || Instance._cfgEnabled == null)
                return false;
            Instance._cfgEnabled.Value = enabled;
            return true;
        }

        internal static bool TryGetGlowParametersInternalApi(
            out float threshold, out float strength, out float blurPercent,
            out float tintR, out float tintG, out float tintB, out float tintA, out float overlayAlpha,
            out string reason)
        {
            threshold = 0f; strength = 0f; blurPercent = 0f;
            tintR = 1f; tintG = 1f; tintB = 1f; tintA = 1f; overlayAlpha = 1f;
            reason = string.Empty;
            if (Instance == null)
            {
                reason = "instance_null";
                return false;
            }
            threshold = Instance._cfgGlowThreshold != null ? Instance._cfgGlowThreshold.Value : 0f;
            strength = Instance._cfgGlowStrength != null ? Instance._cfgGlowStrength.Value : 0f;
            blurPercent = Instance._cfgGlowBlurPercent != null ? Instance._cfgGlowBlurPercent.Value : 0f;
            tintR = Instance._cfgTintR != null ? Instance._cfgTintR.Value : 1f;
            tintG = Instance._cfgTintG != null ? Instance._cfgTintG.Value : 1f;
            tintB = Instance._cfgTintB != null ? Instance._cfgTintB.Value : 1f;
            tintA = Instance._cfgTintA != null ? Instance._cfgTintA.Value : 1f;
            overlayAlpha = Instance._cfgOverlayAlpha != null ? Instance._cfgOverlayAlpha.Value : 1f;
            reason = "ok";
            return true;
        }

        private void OnDestroy()
        {
            LogPlugin("INFO", "OnDestroy start");

            if (_overlayDrawer != null)
            {
                Destroy(_overlayDrawer);
                _overlayDrawer = null;
            }

            CleanupCaptureGlowPipeline();

            if (_cameraRoot != null)
            {
                Destroy(_cameraRoot);
                _cameraRoot = null;
            }

            ReleaseCaptureRt();

            if (Instance == this)
                Instance = null;

            LogPlugin("INFO", "OnDestroy end");
        }
    }
}
