using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using System.Collections;

namespace MainGameGlow
{
    [BepInPlugin(Guid, PluginName, Version)]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.kks.maingame.glow";
        public const string PluginName = "MainGameGlow";
        public const string Version = "1.0.0";

        private const int CaptureGlowVolumeLayer = 31;

        private static readonly FieldInfo PostProcessLayerResourcesField =
            typeof(PostProcessLayer).GetField("m_Resources", BindingFlags.Instance | BindingFlags.NonPublic);

        public static Plugin Instance { get; private set; }

        private sealed class OverlayDrawer : MonoBehaviour
        {
            internal Plugin Owner;
            private void OnPostRender()
            {
                Owner?.DrawOverlay(GetComponent<Camera>());
            }
        }

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
        private void SetupConfig()
        {
            const string cat1 = "01. General";
            const string cat2 = "02. Capture";
            const string cat3 = "03. Glow";
            const string cat4 = "04. Overlay";
            const string cat5 = "05. Source Camera";

            _cfgEnabled = Bind(
                cat1, "Enabled", true,
                "グロープラグイン有効/無効",
                order: 100);

            _cfgVerboseLog = Bind(
                cat1, "Verbose Log", false,
                "詳細ログ出力",
                order: 90);

            _cfgUseScreenSize = Bind(
                cat2, "Use Screen Size", true,
                "キャプチャ解像度に画面サイズを使う",
                order: 100);

            _cfgCaptureWidth = Bind(
                cat2,
                "Capture Width",
                1280,
                new ConfigDescription(
                    "Use Screen Size=false のときの幅",
                    new AcceptableValueRange<int>(16, 8192),
                    BuildConfigManagerAttributes(order: 90)
                ),
                order: 90
            );

            _cfgCaptureHeight = Bind(
                cat2,
                "Capture Height",
                720,
                new ConfigDescription(
                    "Use Screen Size=false のときの高さ",
                    new AcceptableValueRange<int>(16, 8192),
                    BuildConfigManagerAttributes(order: 80)
                ),
                order: 80
            );

            _cfgCharaLayer = Bind(
                cat2, "Character Layer Name", "Chara",
                "発光キャプチャ対象レイヤー名",
                order: 70);

            _cfgGlowThreshold = Bind(
                cat3,
                "Glow Threshold",
                1f,
                new ConfigDescription(
                    "Bloom抽出閾値",
                    new AcceptableValueRange<float>(0f, 5f),
                    BuildConfigManagerAttributes(order: 100)
                ),
                order: 100
            );

            _cfgGlowStrength = Bind(
                cat3,
                "Glow Strength",
                2f,
                new ConfigDescription(
                    "Bloom強度",
                    new AcceptableValueRange<float>(0f, 10f),
                    BuildConfigManagerAttributes(order: 90)
                ),
                order: 90
            );

            _cfgGlowBlurPercent = Bind(
                cat3,
                "Glow Blur Percent",
                35f,
                new ConfigDescription(
                    "ぼかし量。0でほぼ無効、100で強い拡散",
                    new AcceptableValueRange<float>(0f, 100f),
                    BuildConfigManagerAttributes(order: 80)
                ),
                order: 80
            );

            _cfgTintR = Bind(
                cat4,
                "Tint R",
                1f,
                new ConfigDescription(
                    "発光色R",
                    new AcceptableValueRange<float>(0f, 1f),
                    BuildConfigManagerAttributes(order: 100)
                ),
                order: 100
            );

            _cfgTintG = Bind(
                cat4,
                "Tint G",
                1f,
                new ConfigDescription(
                    "発光色G",
                    new AcceptableValueRange<float>(0f, 1f),
                    BuildConfigManagerAttributes(order: 90)
                ),
                order: 90
            );

            _cfgTintB = Bind(
                cat4,
                "Tint B",
                1f,
                new ConfigDescription(
                    "発光色B",
                    new AcceptableValueRange<float>(0f, 1f),
                    BuildConfigManagerAttributes(order: 80)
                ),
                order: 80
            );

            _cfgTintA = Bind(
                cat4,
                "Tint A",
                1f,
                new ConfigDescription(
                    "色アルファ",
                    new AcceptableValueRange<float>(0f, 1f),
                    BuildConfigManagerAttributes(order: 70)
                ),
                order: 70
            );

            _cfgOverlayAlpha = Bind(
                cat4,
                "Overlay Alpha",
                1f,
                new ConfigDescription(
                    "最終合成アルファ",
                    new AcceptableValueRange<float>(0f, 1f),
                    BuildConfigManagerAttributes(order: 60)
                ),
                order: 60
            );

            _cfgPreferCameraMain = Bind(
                cat5, "Prefer Camera.main", true,
                "Camera.main を優先",
                order: 100);

            _cfgCameraNameFilter = Bind(
                cat5, "Camera Name Filter", "",
                "部分一致するカメラ名を優先",
                order: 90);

            _cfgCameraFallbackIndex = Bind(
                cat5,
                "Camera Fallback Index",
                0,
                new ConfigDescription(
                    "候補カメラのフォールバック番号",
                    new AcceptableValueRange<int>(0, 64),
                    BuildConfigManagerAttributes(order: 80)
                ),
                order: 80
            );

            Config.SettingChanged += (_, __) => ApplyConfig();
        }

        private ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description, int order = 0)
        {
            return Config.Bind(
                section,
                key,
                defaultValue,
                new ConfigDescription(description, null, BuildConfigManagerAttributes(order)));
        }

        private ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, ConfigDescription description, int order = 0)
        {
            List<object> tags = new List<object>();
            if (description.Tags != null)
                tags.AddRange(description.Tags);

            object attrs = BuildConfigManagerAttributes(order);
            if (attrs != null)
                tags.Add(attrs);

            return Config.Bind(
                section,
                key,
                defaultValue,
                new ConfigDescription(description.Description, description.AcceptableValues, tags.ToArray()));
        }

        private object BuildConfigManagerAttributes(int order = 0)
        {
            try
            {
                Type attrType = Type.GetType("ConfigurationManager.ConfigurationManagerAttributes, ConfigurationManager");
                if (attrType == null)
                    return null;

                object obj = Activator.CreateInstance(attrType);

                PropertyInfo orderProp = attrType.GetProperty("Order");
                if (orderProp != null && orderProp.CanWrite)
                    orderProp.SetValue(obj, order, null);

                return obj;
            }
            catch
            {
                return null;
            }
        }

        private void ApplyConfig()
        {
            _characterMask = LayerMask.GetMask(_cfgCharaLayer.Value ?? "Chara");

            int newW = _cfgUseScreenSize.Value
                ? Screen.width
                : Mathf.Max(16, _cfgCaptureWidth.Value);

            int newH = _cfgUseScreenSize.Value
                ? Screen.height
                : Mathf.Max(16, _cfgCaptureHeight.Value);

            newW = Mathf.Max(16, newW);
            newH = Mathf.Max(16, newH);

            bool needRebuild =
                _captureRt == null ||
                _rtWidth != newW ||
                _rtHeight != newH;

            if (needRebuild)
            {
                ReleaseCaptureRt();
                _captureRt = CreateRT(newW, newH);
                _rtWidth = newW;
                _rtHeight = newH;

                if (_cfgVerboseLog.Value)
                    Logger.LogInfo($"capture RT rebuilt: {newW}x{newH}");
            }

            if (_cameraRoot == null)
                SetupCaptureCamera();

            bool pipelineReady = _captureBloom != null && _capturePostProcessLayer != null && _capturePostProcessVolume != null;
            ApplyCaptureGlowSettings(pipelineReady);
        }

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

        private void SyncOverlayDrawer(Camera srcCamera)
        {
            if (!_cfgEnabled.Value || srcCamera == null)
            {
                if (_overlayDrawer != null)
                {
                    Destroy(_overlayDrawer);
                    _overlayDrawer = null;
                }

                _lastSourceCamera = null;
                return;
            }

            if (_lastSourceCamera != srcCamera)
            {
                if (_overlayDrawer != null)
                {
                    Destroy(_overlayDrawer);
                    _overlayDrawer = null;
                }

                _overlayDrawer = srcCamera.gameObject.AddComponent<OverlayDrawer>();
                _overlayDrawer.Owner = this;
                _lastSourceCamera = srcCamera;
            }
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

        private bool EnsureCaptureGlowPipeline(Camera src)
        {
            if (_captureGlowInitFailed)
            {
                LogGlowDecision("pipeline blocked: previous init failed");
                return false;
            }

            if (_captureCamera == null)
            {
                LogGlowDecision("pipeline pending: capture camera is null");
                return false;
            }

            if (_capturePostProcessLayer != null && _capturePostProcessVolume != null && _captureBloom != null)
            {
                LogGlowDecision("pipeline ready: existing post-process components reused");
                return true;
            }

            try
            {
                PostProcessLayer template = FindPostProcessLayerTemplate(src);
                PostProcessResources resources = ResolvePostProcessResources(template);

                if (resources == null)
                {
                    LogGlowDecision("pipeline pending: PostProcessResources not found");
                    return false;
                }

                if (_capturePostProcessLayer == null)
                    _capturePostProcessLayer = _captureCamera.gameObject.AddComponent<PostProcessLayer>();

                if (template != null)
                {
                    string templateJson = JsonUtility.ToJson(template);
                    JsonUtility.FromJsonOverwrite(templateJson, _capturePostProcessLayer);
                }
                else
                {
                    _capturePostProcessLayer.Init(resources);
                }

                if (PostProcessLayerResourcesField != null)
                    PostProcessLayerResourcesField.SetValue(_capturePostProcessLayer, resources);

                _capturePostProcessLayer.volumeTrigger = _captureCamera.transform;
                _capturePostProcessLayer.volumeLayer = 1 << CaptureGlowVolumeLayer;
                _capturePostProcessLayer.antialiasingMode = PostProcessLayer.Antialiasing.None;
                _capturePostProcessLayer.enabled = false;

                if (_captureGlowVolumeRoot == null)
                {
                    _captureGlowVolumeRoot = new GameObject("MainGameGlowVolume");
                    _captureGlowVolumeRoot.hideFlags = HideFlags.DontSave;
                    _captureGlowVolumeRoot.layer = CaptureGlowVolumeLayer;
                    _captureGlowVolumeRoot.transform.SetParent(_cameraRoot.transform, false);
                }

                if (_capturePostProcessVolume == null)
                    _capturePostProcessVolume = _captureGlowVolumeRoot.AddComponent<PostProcessVolume>();

                if (_capturePostProcessProfile != null)
                    Destroy(_capturePostProcessProfile);

                _capturePostProcessProfile = ScriptableObject.CreateInstance<PostProcessProfile>();
                _capturePostProcessProfile.hideFlags = HideFlags.DontSave;

                _captureBloom = _capturePostProcessProfile.AddSettings<Bloom>();
                _captureBloom.enabled.Override(true);
                _captureBloom.fastMode.Override(true);
                _captureBloom.softKnee.Override(0.5f);
                _captureBloom.clamp.Override(65472f);
                _captureBloom.color.Override(Color.white);
                _captureBloom.dirtIntensity.Override(0f);
                _captureBloom.dirtTexture.Override(null);
                _captureBloom.anamorphicRatio.Override(0f);

                _capturePostProcessVolume.isGlobal = true;
                _capturePostProcessVolume.priority = 10000f;
                _capturePostProcessVolume.weight = 1f;
                _capturePostProcessVolume.sharedProfile = _capturePostProcessProfile;
                _capturePostProcessVolume.enabled = false;

                string templateName = template != null ? template.gameObject.name : "(manual)";
                LogGlowDecision("pipeline created: template=" + templateName + ", volumeLayer=" + CaptureGlowVolumeLayer, force: true);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogWarning("capture glow init failed: " + ex.Message);
                _captureGlowInitFailed = true;
                LogGlowDecision("pipeline error: init failed (" + ex.Message + ")", force: true);
                CleanupCaptureGlowPipeline();
                return false;
            }
        }

        private PostProcessLayer FindPostProcessLayerTemplate(Camera src)
        {
            if (src != null)
            {
                PostProcessLayer onSrc = src.GetComponent<PostProcessLayer>();
                if (onSrc != null)
                    return onSrc;
            }

            PostProcessLayer[] activeLayers = FindObjectsOfType<PostProcessLayer>();
            for (int i = 0; i < activeLayers.Length; i++)
            {
                PostProcessLayer layer = activeLayers[i];
                if (layer == null || layer == _capturePostProcessLayer)
                    continue;
                return layer;
            }

            PostProcessLayer[] allLayers = Resources.FindObjectsOfTypeAll<PostProcessLayer>();
            for (int i = 0; i < allLayers.Length; i++)
            {
                PostProcessLayer layer = allLayers[i];
                if (layer == null || layer == _capturePostProcessLayer)
                    continue;
                if (layer.hideFlags == HideFlags.HideAndDontSave)
                    continue;
                return layer;
            }

            if (!_captureGlowTemplateWarned)
            {
                Logger.LogWarning("capture glow pending: PostProcessLayer が見つからないためテンプレート無しで初期化します");
                _captureGlowTemplateWarned = true;
            }

            return null;
        }

        private PostProcessResources ResolvePostProcessResources(PostProcessLayer template)
        {
            if (template != null && PostProcessLayerResourcesField != null)
            {
                PostProcessResources templateResources = PostProcessLayerResourcesField.GetValue(template) as PostProcessResources;
                if (templateResources != null)
                {
                    _captureGlowTemplateWarned = false;
                    return templateResources;
                }
            }

            PostProcessResources[] resources = Resources.FindObjectsOfTypeAll<PostProcessResources>();
            if (resources != null && resources.Length > 0)
            {
                _captureGlowTemplateWarned = false;
                return resources[0];
            }

            return null;
        }

        private void ApplyCaptureGlowSettings(bool pipelineReady)
        {
            bool enableGlow = IsGlowRequested();

            if (!enableGlow)
            {
                if (_capturePostProcessLayer != null) _capturePostProcessLayer.enabled = false;
                if (_capturePostProcessVolume != null) _capturePostProcessVolume.enabled = false;

                string reason = "disabled by config:";
                if (_cfgGlowStrength == null || _cfgGlowBlurPercent == null)
                    reason += " config not ready";
                else if (_cfgGlowStrength.Value <= 0.0001f)
                    reason += " strength<=0";
                else if (_cfgGlowBlurPercent.Value <= 0.0001f)
                    reason += " blur<=0";
                else
                    reason += " unknown";

                LogGlowDecision(reason);
                return;
            }

            if (!pipelineReady || _captureBloom == null || _capturePostProcessLayer == null || _capturePostProcessVolume == null)
                return;

            float threshold = Mathf.Clamp(_cfgGlowThreshold.Value, 0f, 5f);
            float strength = Mathf.Clamp(_cfgGlowStrength.Value, 0f, 10f);
            float blur01 = Mathf.Clamp01(_cfgGlowBlurPercent.Value / 100f);
            float diffusion = Mathf.Lerp(1f, 10f, blur01);

            _captureBloom.threshold.Override(threshold);
            _captureBloom.intensity.Override(strength);
            _captureBloom.diffusion.Override(diffusion);

            _capturePostProcessVolume.enabled = true;
            _capturePostProcessLayer.enabled = true;

            LogGlowDecision(
                $"active: threshold={threshold:0.##}, strength={strength:0.##}, blur%={_cfgGlowBlurPercent.Value:0.##}, diffusion={diffusion:0.##}");
        }

        internal void DrawOverlay(Camera cam)
        {
            if (!_cfgEnabled.Value || _captureRt == null)
                return;

            float r = Mathf.Clamp01(_cfgTintR.Value);
            float g = Mathf.Clamp01(_cfgTintG.Value);
            float b = Mathf.Clamp01(_cfgTintB.Value);
            float a = Mathf.Clamp01(_cfgTintA.Value) * Mathf.Clamp01(_cfgOverlayAlpha.Value);

            if (a <= 0.0001f)
                return;

            float w = cam != null ? cam.pixelWidth : Screen.width;
            float h = cam != null ? cam.pixelHeight : Screen.height;
            Rect rect = new Rect(0, 0, w, h);

            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, w, h, 0f);
            try
            {
                Graphics.DrawTexture(
                    rect,
                    _captureRt,
                    new Rect(0f, 0f, 1f, 1f),
                    0, 0, 0, 0,
                    new Color(r, g, b, a));
            }
            finally
            {
                GL.PopMatrix();
            }
        }

        private void CleanupCaptureGlowPipeline()
        {
            if (_capturePostProcessVolume != null)
            {
                _capturePostProcessVolume.sharedProfile = null;
                Destroy(_capturePostProcessVolume);
                _capturePostProcessVolume = null;
            }

            if (_captureGlowVolumeRoot != null)
            {
                Destroy(_captureGlowVolumeRoot);
                _captureGlowVolumeRoot = null;
            }

            if (_capturePostProcessLayer != null)
            {
                Destroy(_capturePostProcessLayer);
                _capturePostProcessLayer = null;
            }

            if (_capturePostProcessProfile != null)
            {
                Destroy(_capturePostProcessProfile);
                _capturePostProcessProfile = null;
            }

            _captureBloom = null;
            LogGlowDecision("pipeline cleaned up", force: true);
        }

        private void LogPlugin(string level, string message)
        {
            if (string.IsNullOrEmpty(_pluginLogPath))
                return;

            try
            {
                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
                File.AppendAllText(_pluginLogPath, line + Environment.NewLine, new UTF8Encoding(false));
            }
            catch
            {
            }
        }

        private void LogGlowDecision(string reason, bool force = false)
        {
            bool verbose = _cfgVerboseLog != null && _cfgVerboseLog.Value;

            if (force || !string.Equals(_lastGlowDecision, reason, StringComparison.Ordinal))
            {
                _lastGlowDecision = reason;
                _nextGlowHeartbeatTime = Time.unscaledTime + 5f;
                LogPlugin("GLOW", reason);
                return;
            }

            if (verbose && Time.unscaledTime >= _nextGlowHeartbeatTime)
            {
                _nextGlowHeartbeatTime = Time.unscaledTime + 5f;
                LogPlugin("GLOW", "heartbeat: " + reason);
            }
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