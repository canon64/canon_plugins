using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private const string OverlayQuadName = "__SubCameraOverlayQuad";
        private const string VideoSurfacePrefix = "VideoSurface_";

        private static readonly string[] OverlayLogicalFaceNames =
            { "Front", "Back", "Left", "Right", "Floor", "Ceiling" };
        private static readonly string[] OverlayTileModeOptions = { "single", "tile" };

        private readonly List<GameObject> _overlayQuads = new List<GameObject>();
        private Material _overlayMaterial;
        private RenderTexture _cachedSubCameraRenderTexture;
        private int _cachedSubCameraTextureWidth;
        private int _cachedSubCameraTextureHeight;

        private enum OverlayBeatZone { Unknown, Low, Mid, High }
        private Type _beatPluginType;
        private PropertyInfo _beatInstanceProperty;
        private FieldInfo _beatZoneRawTargetField;
        private FieldInfo _beatCfgBpmField;
        private FieldInfo _beatCfgLowIntensityField;
        private FieldInfo _beatCfgMidIntensityField;
        private FieldInfo _beatCfgHighIntensityField;
        private bool _beatLookupDone;
        private bool _overlayLoggedMissingApi;
        private bool _overlayLoggedMissingTexture;
        private string _lastBuiltOverlaySurface = string.Empty;
        private string _lastBuiltOverlayMode = string.Empty;
        private int _lastBuiltOverlayChildCount = -1;
        private GameObject _lastBuiltOverlayRoot;
        private bool _lastOverlayEnabledState;

        // Mid/High -> Low の瞬間に、オーバーレイがパッと消えないよう 0.5 秒で弱側透明度へ落とす。
        private const float OverlayWeakFadeSec = 0.5f;
        private OverlayBeatZone _lastOverlayBeatZone = OverlayBeatZone.Unknown;
        private bool _overlayWeakFadeActive;
        private float _overlayWeakFadeStartTime;
        private float _overlayWeakFadeStartOpacity = 0.5f;
        private float _lastOverlayResolvedOpacity = 0.5f;

        private void UpdateSubCameraOverlay()
        {
            bool enabled = _settings != null && _settings.OverlayEnabled;

            if (!enabled || _videoRoomRoot == null)
            {
                if (_overlayQuads.Count > 0)
                    HideAllOverlayQuads();
                ResetOverlayBeatFadeState();
                _lastOverlayEnabledState = false;
                return;
            }

            if (!TryGetSubCameraRenderTextureExternal(out RenderTexture rt, out string rtReason))
            {
                if (!_overlayLoggedMissingTexture)
                {
                    LogInfo("[overlay] subcamera render texture unavailable reason=" + rtReason);
                    _overlayLoggedMissingTexture = true;
                }
                if (_overlayQuads.Count > 0)
                    HideAllOverlayQuads();
                ResetOverlayBeatFadeState();
                return;
            }
            _overlayLoggedMissingTexture = false;

            EnsureOverlayMaterial();
            if (_overlayMaterial == null)
                return;

            if (rt != _cachedSubCameraRenderTexture
                || rt.width != _cachedSubCameraTextureWidth
                || rt.height != _cachedSubCameraTextureHeight)
            {
                _cachedSubCameraRenderTexture = rt;
                _cachedSubCameraTextureWidth = rt.width;
                _cachedSubCameraTextureHeight = rt.height;
                _overlayMaterial.mainTexture = rt;
                ApplyCenterSquareCropToOverlayMaterial(rt.width, rt.height);
            }

            string targetSurface = NormalizeLogicalSurfacesCsv(_settings.OverlayTargetSurface);
            string tileMode = NormalizeTileMode(_settings.OverlayTileMode);
            int currentChildCount = _videoRoomRoot.transform.childCount;
            bool surfaceChanged = !string.Equals(targetSurface, _lastBuiltOverlaySurface, StringComparison.Ordinal);
            bool modeChanged = !string.Equals(tileMode, _lastBuiltOverlayMode, StringComparison.Ordinal);
            bool roomChanged = _lastBuiltOverlayRoot != _videoRoomRoot
                || currentChildCount != _lastBuiltOverlayChildCount;
            bool enabledRising = !_lastOverlayEnabledState;

            if (surfaceChanged || modeChanged || roomChanged || enabledRising || _overlayQuads.Count == 0)
            {
                RebuildOverlayQuads(targetSurface, tileMode);
                _lastBuiltOverlaySurface = targetSurface;
                _lastBuiltOverlayMode = tileMode;
                _lastBuiltOverlayChildCount = currentChildCount;
                _lastBuiltOverlayRoot = _videoRoomRoot;
            }

            ApplyOverlayOpacity(ResolveOverlayOpacityWithBeat());

            for (int i = 0; i < _overlayQuads.Count; i++)
            {
                GameObject quad = _overlayQuads[i];
                if (quad != null && !quad.activeSelf)
                    quad.SetActive(true);
            }

            _lastOverlayEnabledState = true;
        }

        private void RebuildOverlayQuads(string targetSurfaceCsv, string tileMode)
        {
            DestroyOverlayQuads();

            if (_videoRoomRoot == null)
                return;

            HashSet<string> selected = ParseLogicalFaceSet(targetSurfaceCsv);
            if (selected.Count == 0)
                return;

            foreach (string logicalName in OverlayLogicalFaceNames)
            {
                if (!selected.Contains(logicalName))
                    continue;

                List<Transform> targets = ResolveTargetVideoSurfaces(logicalName);
                if (targets.Count == 0)
                    continue;

                if (string.Equals(tileMode, "tile", StringComparison.Ordinal))
                {
                    for (int ti = 0; ti < targets.Count; ti++)
                        CreateOverlayQuadForTile(targets[ti]);
                }
                else
                {
                    CreateOverlayQuadForLogicalGroup(targets);
                }
            }

            LogInfo("[overlay] built quads count=" + _overlayQuads.Count + " surfaces=" + targetSurfaceCsv + " mode=" + tileMode);
        }

        private void CreateOverlayQuadForTile(Transform target)
        {
            if (target == null)
                return;

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = OverlayQuadName;
            Collider collider = quad.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            quad.transform.SetParent(_videoRoomRoot.transform, worldPositionStays: false);

            Vector3 normal = target.rotation * Vector3.back;
            Vector3 worldPos = target.position + normal * 0.003f;
            quad.transform.SetPositionAndRotation(worldPos, target.rotation);
            quad.transform.localScale = ConvertWorldScaleToLocalScale(quad.transform.parent, target.lossyScale);

            ApplyOverlayMaterial(quad);
            _overlayQuads.Add(quad);
        }

        private void CreateOverlayQuadForLogicalGroup(List<Transform> targets)
        {
            if (targets.Count == 0)
                return;

            // 全タイルから論理面の中央位置とサイズを計算する。
            // 同じ論理面のQuadは同じ向き(rotation)を持っているので最初のQuadの向きを採用。
            Quaternion rotation = targets[0].rotation;
            Quaternion invRotation = Quaternion.Inverse(rotation);

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            Vector3 sumPos = Vector3.zero;
            int count = 0;
            foreach (Transform t in targets)
            {
                Vector3 local = invRotation * t.position;
                Vector3 size = t.lossyScale;
                float halfX = size.x * 0.5f;
                float halfY = size.y * 0.5f;
                if (local.x - halfX < minX) minX = local.x - halfX;
                if (local.x + halfX > maxX) maxX = local.x + halfX;
                if (local.y - halfY < minY) minY = local.y - halfY;
                if (local.y + halfY > maxY) maxY = local.y + halfY;
                sumPos += t.position;
                count++;
            }
            if (count == 0)
                return;

            Vector3 centerLocal = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (invRotation * (sumPos / count)).z);
            Vector3 centerWorld = rotation * centerLocal;
            float width = maxX - minX;
            float height = maxY - minY;

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = OverlayQuadName;
            Collider collider = quad.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            quad.transform.SetParent(_videoRoomRoot.transform, worldPositionStays: false);

            Vector3 normal = rotation * Vector3.back;
            Vector3 worldPos = centerWorld + normal * 0.003f;
            quad.transform.SetPositionAndRotation(worldPos, rotation);
            quad.transform.localScale = ConvertWorldScaleToLocalScale(quad.transform.parent, new Vector3(width, height, 1f));

            ApplyOverlayMaterial(quad);
            _overlayQuads.Add(quad);
        }

        private void ApplyOverlayMaterial(GameObject quad)
        {
            if (quad == null)
                return;

            MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
            if (renderer == null)
                return;

            renderer.sharedMaterial = _overlayMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void EnsureOverlayMaterial()
        {
            if (_overlayMaterial != null)
                return;

            Shader shader = Shader.Find("Particles/Alpha Blended");
            if (shader == null)
                shader = Shader.Find("Mobile/Particles/Alpha Blended");
            if (shader == null)
                shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (shader == null)
                shader = Shader.Find("Unlit/Transparent");

            _overlayMaterial = new Material(shader)
            {
                name = "SubCameraOverlayMat",
                renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent
            };

            if (_overlayMaterial.HasProperty("_TintColor"))
                _overlayMaterial.SetColor("_TintColor", new Color(1f, 1f, 1f, 0.5f));
            if (_overlayMaterial.HasProperty("_Color"))
                _overlayMaterial.SetColor("_Color", new Color(1f, 1f, 1f, 0.5f));
            if (_overlayMaterial.HasProperty("_ZWrite"))
                _overlayMaterial.SetInt("_ZWrite", 0);
            if (_overlayMaterial.HasProperty("_Cull"))
                _overlayMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _overlayMaterial.SetOverrideTag("RenderType", "Transparent");
        }

        private void ApplyCenterSquareCropToOverlayMaterial(int width, int height)
        {
            if (_overlayMaterial == null || width <= 0 || height <= 0)
                return;

            float scaleX = 1f;
            float scaleY = 1f;
            if (width > height)
            {
                scaleX = (float)height / (float)width;
            }
            else if (height > width)
            {
                scaleY = (float)width / (float)height;
            }

            float offsetX = (1f - scaleX) * 0.5f;
            float offsetY = (1f - scaleY) * 0.5f;
            _overlayMaterial.SetTextureScale("_MainTex", new Vector2(scaleX, scaleY));
            _overlayMaterial.SetTextureOffset("_MainTex", new Vector2(offsetX, offsetY));
        }

        private void EnsureBeatLookup()
        {
            if (_beatLookupDone)
                return;

            _beatLookupDone = true;
            _beatPluginType = Type.GetType("MainGameBeatSyncSpeed.Plugin, MainGameBeatSyncSpeed");
            if (_beatPluginType == null)
                return;

            _beatInstanceProperty = _beatPluginType.GetProperty("Instance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            _beatZoneRawTargetField = _beatPluginType.GetField("_currentZoneRawTarget", BindingFlags.Instance | BindingFlags.NonPublic);
            _beatCfgBpmField = _beatPluginType.GetField("_cfgBpm", BindingFlags.Instance | BindingFlags.NonPublic);
            _beatCfgLowIntensityField = _beatPluginType.GetField("_cfgLowIntensity", BindingFlags.Instance | BindingFlags.NonPublic);
            _beatCfgMidIntensityField = _beatPluginType.GetField("_cfgMidIntensity", BindingFlags.Instance | BindingFlags.NonPublic);
            _beatCfgHighIntensityField = _beatPluginType.GetField("_cfgHighIntensity", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private object GetBeatInstance()
        {
            EnsureBeatLookup();
            if (_beatInstanceProperty == null)
                return null;
            try { return _beatInstanceProperty.GetValue(null, null); }
            catch { return null; }
        }

        private static bool TryReadBeatConfigFloat(object owner, FieldInfo field, out float value)
        {
            value = 0f;
            if (owner == null || field == null) return false;
            try
            {
                object entry = field.GetValue(owner);
                if (entry == null) return false;
                PropertyInfo valueProp = entry.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
                if (valueProp == null) return false;
                object raw = valueProp.GetValue(entry, null);
                if (raw is float f) { value = f; return true; }
                if (raw is double d) { value = (float)d; return true; }
                if (raw is int i) { value = i; return true; }
            }
            catch { }
            return false;
        }

        private static bool TryReadBeatConfigInt(object owner, FieldInfo field, out int value)
        {
            value = 0;
            if (owner == null || field == null) return false;
            try
            {
                object entry = field.GetValue(owner);
                if (entry == null) return false;
                PropertyInfo valueProp = entry.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
                if (valueProp == null) return false;
                object raw = valueProp.GetValue(entry, null);
                if (raw is int i) { value = i; return true; }
                if (raw is float f) { value = Mathf.RoundToInt(f); return true; }
                if (raw is double d) { value = Mathf.RoundToInt((float)d); return true; }
            }
            catch { }
            return false;
        }

        private bool TryGetOverlayBeatZone(out OverlayBeatZone zone, out float lowIntensity, out float midIntensity, out float highIntensity)
        {
            zone = OverlayBeatZone.Unknown;
            lowIntensity = 0f;
            midIntensity = 0.5f;
            highIntensity = 1f;

            object inst = GetBeatInstance();
            if (inst == null || _beatZoneRawTargetField == null)
                return false;

            float rawTarget;
            try
            {
                object raw = _beatZoneRawTargetField.GetValue(inst);
                if (!(raw is float f) || f < 0f)
                    return false;
                rawTarget = f;
            }
            catch
            {
                return false;
            }

            if (!TryReadBeatConfigFloat(inst, _beatCfgLowIntensityField, out float low)) return false;
            if (!TryReadBeatConfigFloat(inst, _beatCfgMidIntensityField, out float mid)) return false;
            if (!TryReadBeatConfigFloat(inst, _beatCfgHighIntensityField, out float high)) return false;

            lowIntensity = low;
            midIntensity = mid;
            highIntensity = high;

            float dLow = Mathf.Abs(rawTarget - low);
            float dMid = Mathf.Abs(rawTarget - mid);
            float dHigh = Mathf.Abs(rawTarget - high);

            if (dLow <= dMid && dLow <= dHigh)
                zone = OverlayBeatZone.Low;
            else if (dHigh <= dLow && dHigh <= dMid)
                zone = OverlayBeatZone.High;
            else
                zone = OverlayBeatZone.Mid;

            return true;
        }

        private bool TryGetBeatLoopHzForOverlay(out float hz)
        {
            hz = 0f;
            object inst = GetBeatInstance();
            if (inst == null) return false;
            if (!TryReadBeatConfigInt(inst, _beatCfgBpmField, out int bpm)) return false;

            // 透明度の周期はBPMだけで決める。Low/Mid/High強度は周期に掛けない。
            hz = Mathf.Max(1, bpm) / 60f;
            return hz > 0f;
        }

        private float GetBeatPulse01ForOverlay()
        {
            if (!TryGetBeatLoopHzForOverlay(out float hz) || hz <= 0f)
                return 0f;
            float phase01 = Mathf.Repeat(Time.unscaledTime * hz, 1f);
            return Mathf.Clamp01(Mathf.Sin(phase01 * Mathf.PI));
        }

        private float ResolveOverlayOpacityWithBeat()
        {
            float baseOpacity = _settings != null ? Mathf.Clamp01(_settings.OverlayOpacity) : 0.5f;
            if (_settings == null || !_settings.OverlayBeatOpacityEnabled)
            {
                ResetOverlayBeatFadeState();
                _lastOverlayResolvedOpacity = baseOpacity;
                return baseOpacity;
            }

            float pulse01 = GetBeatPulse01ForOverlay();
            float min = Mathf.Clamp01(_settings.OverlayBeatOpacityMin);
            float max = Mathf.Clamp01(_settings.OverlayBeatOpacityMax);

            float normalOpacity = _settings.OverlayBeatOpacityInverted
                ? Mathf.Lerp(max, min, pulse01)
                : Mathf.Lerp(min, max, pulse01);

            if (!TryGetOverlayBeatZone(out OverlayBeatZone currentZone, out _, out _, out _))
            {
                _lastOverlayBeatZone = OverlayBeatZone.Unknown;
                _overlayWeakFadeActive = false;
                _lastOverlayResolvedOpacity = normalOpacity;
                return normalOpacity;
            }

            bool fallingToLow =
                currentZone == OverlayBeatZone.Low &&
                (_lastOverlayBeatZone == OverlayBeatZone.Mid ||
                 _lastOverlayBeatZone == OverlayBeatZone.High);

            if (fallingToLow)
            {
                _overlayWeakFadeActive = true;
                _overlayWeakFadeStartTime = Time.unscaledTime;
                _overlayWeakFadeStartOpacity = Mathf.Clamp01(_lastOverlayResolvedOpacity);
            }

            _lastOverlayBeatZone = currentZone;

            float result = normalOpacity;
            if (_overlayWeakFadeActive)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - _overlayWeakFadeStartTime) / OverlayWeakFadeSec);
                float lowOpacity = _settings.OverlayBeatOpacityInverted ? max : min;
                result = Mathf.Lerp(_overlayWeakFadeStartOpacity, lowOpacity, t);

                if (t >= 1f)
                    _overlayWeakFadeActive = false;
            }

            result = Mathf.Clamp01(result);
            _lastOverlayResolvedOpacity = result;
            return result;
        }

        private void ResetOverlayBeatFadeState()
        {
            _lastOverlayBeatZone = OverlayBeatZone.Unknown;
            _overlayWeakFadeActive = false;
            _overlayWeakFadeStartTime = 0f;
            _overlayWeakFadeStartOpacity = _settings != null ? Mathf.Clamp01(_settings.OverlayOpacity) : 0.5f;
            _lastOverlayResolvedOpacity = _overlayWeakFadeStartOpacity;
        }

        private void ApplyOverlayOpacity(float opacity)
        {
            if (_overlayMaterial == null)
                return;

            float clamped = Mathf.Clamp01(opacity);
            // Particles/Alpha Blended 系は _TintColor.a が乗算で効く（半輝度なので2倍補正）
            if (_overlayMaterial.HasProperty("_TintColor"))
            {
                Color color = _overlayMaterial.GetColor("_TintColor");
                color.a = clamped * 0.5f;
                _overlayMaterial.SetColor("_TintColor", color);
            }
            if (_overlayMaterial.HasProperty("_Color"))
            {
                Color color = _overlayMaterial.color;
                color.a = clamped;
                _overlayMaterial.color = color;
            }
        }

        private List<Transform> ResolveTargetVideoSurfaces(string logicalName)
        {
            var result = new List<Transform>();
            if (_videoRoomRoot == null || string.IsNullOrEmpty(logicalName))
                return result;

            string physicalName = LogicalToPhysicalName(logicalName);
            if (string.IsNullOrEmpty(physicalName))
                return result;

            Transform root = _videoRoomRoot.transform;
            int count = root.childCount;
            for (int i = 0; i < count; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null || child.name == null)
                    continue;
                if (!child.name.StartsWith(VideoSurfacePrefix, StringComparison.Ordinal))
                    continue;
                string surfaceName = child.name.Substring(VideoSurfacePrefix.Length);
                if (!IsSamePhysicalSurface(surfaceName, physicalName))
                    continue;
                result.Add(child);
            }
            return result;
        }

        private static string LogicalToPhysicalName(string logicalName)
        {
            switch (logicalName)
            {
                case "Front": return "WallFront";
                case "Back": return "WallBack";
                case "Left": return "WallLeft";
                case "Right": return "WallRight";
                case "Floor": return "Floor";
                case "Ceiling": return "Ceiling";
                default: return string.Empty;
            }
        }

        private static bool IsSamePhysicalSurface(string surfaceName, string physicalPrefix)
        {
            if (string.IsNullOrEmpty(surfaceName) || string.IsNullOrEmpty(physicalPrefix))
                return false;

            // 完全一致 (タイル分割なし) または "physicalPrefix_数字" のパターン
            if (string.Equals(surfaceName, physicalPrefix, StringComparison.Ordinal))
                return true;
            if (surfaceName.Length > physicalPrefix.Length + 1
                && surfaceName.StartsWith(physicalPrefix + "_", StringComparison.Ordinal))
            {
                string rest = surfaceName.Substring(physicalPrefix.Length + 1);
                int dummy;
                return int.TryParse(rest, out dummy);
            }
            return false;
        }

        private static string NormalizeLogicalSurfacesCsv(string raw)
        {
            HashSet<string> set = ParseLogicalFaceSet(raw);
            if (set.Count == 0)
                return "All";
            if (set.Count == OverlayLogicalFaceNames.Length)
                return "All";
            var ordered = new List<string>();
            foreach (string name in OverlayLogicalFaceNames)
                if (set.Contains(name)) ordered.Add(name);
            return string.Join(",", ordered.ToArray());
        }

        private static HashSet<string> ParseLogicalFaceSet(string raw)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(raw))
                return set;

            string[] tokens = raw.Split(',', ';', '|', ' ');
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i] == null ? string.Empty : tokens[i].Trim();
                if (string.IsNullOrEmpty(token))
                    continue;
                if (string.Equals(token, "All", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string name in OverlayLogicalFaceNames)
                        set.Add(name);
                    continue;
                }
                if (string.Equals(token, "None", StringComparison.OrdinalIgnoreCase))
                {
                    set.Clear();
                    continue;
                }
                for (int j = 0; j < OverlayLogicalFaceNames.Length; j++)
                {
                    if (string.Equals(token, OverlayLogicalFaceNames[j], StringComparison.OrdinalIgnoreCase))
                    {
                        set.Add(OverlayLogicalFaceNames[j]);
                        break;
                    }
                }
            }
            return set;
        }

        private static string NormalizeTileMode(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "single";
            string trimmed = raw.Trim().ToLowerInvariant();
            if (trimmed == "tile") return "tile";
            return "single";
        }

        private static Vector3 ConvertWorldScaleToLocalScale(Transform parent, Vector3 worldScale)
        {
            if (parent == null)
                return worldScale;
            Vector3 parentLossy = parent.lossyScale;
            float x = Mathf.Approximately(parentLossy.x, 0f) ? worldScale.x : worldScale.x / parentLossy.x;
            float y = Mathf.Approximately(parentLossy.y, 0f) ? worldScale.y : worldScale.y / parentLossy.y;
            float z = Mathf.Approximately(parentLossy.z, 0f) ? worldScale.z : worldScale.z / parentLossy.z;
            return new Vector3(x, y, z);
        }

        private void HideAllOverlayQuads()
        {
            for (int i = 0; i < _overlayQuads.Count; i++)
            {
                GameObject q = _overlayQuads[i];
                if (q != null && q.activeSelf)
                    q.SetActive(false);
            }
        }

        private void DestroyOverlayQuads()
        {
            for (int i = 0; i < _overlayQuads.Count; i++)
            {
                GameObject q = _overlayQuads[i];
                if (q != null)
                    Destroy(q);
            }
            _overlayQuads.Clear();
        }

        private void DestroyOverlayQuad()
        {
            DestroyOverlayQuads();

            if (_overlayMaterial != null)
            {
                Destroy(_overlayMaterial);
                _overlayMaterial = null;
            }

            _cachedSubCameraRenderTexture = null;
            _cachedSubCameraTextureWidth = 0;
            _cachedSubCameraTextureHeight = 0;
            _overlayLoggedMissingTexture = false;
            _overlayLoggedMissingApi = false;
            _lastBuiltOverlaySurface = string.Empty;
            _lastBuiltOverlayMode = string.Empty;
            _lastBuiltOverlayChildCount = -1;
            _lastBuiltOverlayRoot = null;
            _lastOverlayEnabledState = false;
            ResetOverlayBeatFadeState();
        }

        private bool TryGetSubCameraRenderTextureExternal(out RenderTexture renderTexture, out string reason)
        {
            renderTexture = null;
            reason = string.Empty;
            try
            {
                Type apiType = Type.GetType(
                    "MainGameSubCameraDisplayProbe.MainGameSubCameraDisplayProbeApi, MainGameSubCameraDisplayProbe",
                    throwOnError: false);
                if (apiType == null)
                {
                    reason = "api_type_not_found";
                    if (!_overlayLoggedMissingApi)
                    {
                        LogInfo("[overlay] subcamera api type not found");
                        _overlayLoggedMissingApi = true;
                    }
                    return false;
                }

                MethodInfo method = apiType.GetMethod("TryGetRenderTexture", BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                {
                    reason = "api_method_not_found";
                    if (!_overlayLoggedMissingApi)
                    {
                        LogInfo("[overlay] subcamera TryGetRenderTexture method not found");
                        _overlayLoggedMissingApi = true;
                    }
                    return false;
                }

                _overlayLoggedMissingApi = false;
                object[] args = { null, null };
                object result = method.Invoke(null, args);
                renderTexture = args[0] as RenderTexture;
                reason = args[1] as string ?? string.Empty;
                return result is bool && (bool)result && renderTexture != null;
            }
            catch (Exception ex)
            {
                reason = "exception:" + ex.Message;
                return false;
            }
        }

        private void DrawSubCameraOverlaySection(float boxX, float boxY, float boxW, float boxH)
        {
            GUI.Box(new Rect(boxX, boxY, boxW, boxH), "SUBCAMERA OVERLAY");

            float rowH = 22f;
            float row1Y = boxY + 22f;
            float row2Y = row1Y + rowH + 2f;
            float contentX = boxX + 8f;

            // 1段目: Enabled / Mode[Single/Tile] / Opacity slider / 数値
            float toggleW = 80f;
            bool currentEnabled = _settings != null && _settings.OverlayEnabled;
            bool nextEnabled = GUI.Toggle(new Rect(contentX, row1Y, toggleW, rowH), currentEnabled, "Enabled");
            if (nextEnabled != currentEnabled && _settings != null)
            {
                _settings.OverlayEnabled = nextEnabled;
                LogInfo("[overlay] enabled=" + nextEnabled);
                SaveSubCameraOverlaySettings();
            }

            float modeLabelW = 36f;
            float modeLabelX = contentX + toggleW + 6f;
            GUI.Label(new Rect(modeLabelX, row1Y, modeLabelW, rowH), "Mode");

            float modeBtnW = 56f;
            float modeBtnGap = 2f;
            string currentMode = NormalizeTileMode(_settings != null ? _settings.OverlayTileMode : "single");
            for (int i = 0; i < OverlayTileModeOptions.Length; i++)
            {
                string mode = OverlayTileModeOptions[i];
                bool selected = string.Equals(mode, currentMode, StringComparison.Ordinal);
                Rect modeRect = new Rect(modeLabelX + modeLabelW + i * (modeBtnW + modeBtnGap), row1Y, modeBtnW, rowH);
                Color prevColor = GUI.backgroundColor;
                if (selected)
                    GUI.backgroundColor = Color.cyan;
                if (GUI.Button(modeRect, mode == "single" ? "Single" : "Tile"))
                {
                    if (_settings != null && !selected)
                    {
                        _settings.OverlayTileMode = mode;
                        LogInfo("[overlay] mode=" + mode);
                        SaveSubCameraOverlaySettings();
                    }
                }
                GUI.backgroundColor = prevColor;
            }

            float opacityRowX = modeLabelX + modeLabelW + OverlayTileModeOptions.Length * (modeBtnW + modeBtnGap) + 8f;
            float opacityLabelW = 56f;
            GUI.Label(new Rect(opacityRowX, row1Y, opacityLabelW, rowH), "Opacity");

            float opacityInputW = 50f;
            float opacityInputX = boxX + boxW - 8f - opacityInputW;
            float opacitySliderX = opacityRowX + opacityLabelW + 4f;
            float opacitySliderW = Mathf.Max(40f, opacityInputX - 4f - opacitySliderX);

            float currentOpacity = _settings != null ? Mathf.Clamp01(_settings.OverlayOpacity) : 0.5f;
            float sliderOpacity = GUI.HorizontalSlider(
                new Rect(opacitySliderX, row1Y + 4f, opacitySliderW, rowH),
                currentOpacity, 0f, 1f);
            if (Mathf.Abs(sliderOpacity - currentOpacity) > 0.0001f && _settings != null)
            {
                _settings.OverlayOpacity = Mathf.Clamp01(sliderOpacity);
                _overlayOpacityInput = _settings.OverlayOpacity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                SaveSubCameraOverlaySettings();
                currentOpacity = _settings.OverlayOpacity;
            }

            string opacityCtrlName = "OverlayOpacityInput";
            if (string.IsNullOrEmpty(_overlayOpacityInput) || GUI.GetNameOfFocusedControl() != opacityCtrlName)
            {
                _overlayOpacityInput = currentOpacity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            }
            GUI.SetNextControlName(opacityCtrlName);
            string nextInput = GUI.TextField(new Rect(opacityInputX, row1Y, opacityInputW, rowH), _overlayOpacityInput);
            if (nextInput != _overlayOpacityInput)
            {
                _overlayOpacityInput = nextInput;
                if (float.TryParse(nextInput, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float typed))
                {
                    float clamped = Mathf.Clamp01(typed);
                    if (_settings != null && Mathf.Abs(clamped - _settings.OverlayOpacity) > 0.0001f)
                    {
                        _settings.OverlayOpacity = clamped;
                        SaveSubCameraOverlaySettings();
                    }
                }
            }

            // 2段目: 面チェックボックス6個 + All / None ボタン
            HashSet<string> selected2 = ParseLogicalFaceSet(_settings != null ? _settings.OverlayTargetSurface : "All");
            float faceLabelW = 36f;
            GUI.Label(new Rect(contentX, row2Y, faceLabelW, rowH), "Faces");

            float faceCheckW = 70f;
            float faceCheckGap = 2f;
            float faceX = contentX + faceLabelW;
            bool changed = false;
            for (int i = 0; i < OverlayLogicalFaceNames.Length; i++)
            {
                string face = OverlayLogicalFaceNames[i];
                bool isOn = selected2.Contains(face);
                Rect faceRect = new Rect(faceX, row2Y, faceCheckW, rowH);
                bool nextOn = GUI.Toggle(faceRect, isOn, face);
                if (nextOn != isOn)
                {
                    if (nextOn) selected2.Add(face);
                    else selected2.Remove(face);
                    changed = true;
                }
                faceX += faceCheckW + faceCheckGap;
            }

            float allBtnW = 50f;
            if (GUI.Button(new Rect(faceX, row2Y, allBtnW, rowH), "All"))
            {
                selected2.Clear();
                foreach (string face in OverlayLogicalFaceNames)
                    selected2.Add(face);
                changed = true;
            }
            faceX += allBtnW + faceCheckGap;
            if (GUI.Button(new Rect(faceX, row2Y, allBtnW, rowH), "None"))
            {
                selected2.Clear();
                changed = true;
            }

            if (changed && _settings != null)
            {
                _settings.OverlayTargetSurface = SerializeLogicalFaceSet(selected2);
                LogInfo("[overlay] faces=" + _settings.OverlayTargetSurface);
                SaveSubCameraOverlaySettings();
            }

            // 3段目: 拍透明度連動（BeatOp / Min / Max / Inverted）
            float row3Y = row2Y + rowH + 2f;
            float beatToggleW = 90f;
            bool currentBeatEnabled = _settings != null && _settings.OverlayBeatOpacityEnabled;
            bool nextBeatEnabled = GUI.Toggle(new Rect(contentX, row3Y, beatToggleW, rowH), currentBeatEnabled, "拍Op連動");
            if (nextBeatEnabled != currentBeatEnabled && _settings != null)
            {
                _settings.OverlayBeatOpacityEnabled = nextBeatEnabled;
                LogInfo("[overlay] beat-opacity=" + nextBeatEnabled);
                SaveSubCameraOverlaySettings();
            }

            float minLabelW = 28f;
            float minLabelX = contentX + beatToggleW + 4f;
            GUI.Label(new Rect(minLabelX, row3Y, minLabelW, rowH), "Min");

            float minSliderW = 100f;
            float minSliderX = minLabelX + minLabelW;
            float currentMin = _settings != null ? Mathf.Clamp01(_settings.OverlayBeatOpacityMin) : 0.1f;
            float nextMin = GUI.HorizontalSlider(new Rect(minSliderX, row3Y + 4f, minSliderW, rowH), currentMin, 0f, 1f);
            if (Mathf.Abs(nextMin - currentMin) > 0.0001f && _settings != null)
            {
                _settings.OverlayBeatOpacityMin = Mathf.Clamp01(nextMin);
                SaveSubCameraOverlaySettings();
            }
            GUI.Label(new Rect(minSliderX + minSliderW + 2f, row3Y, 36f, rowH), currentMin.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));

            float maxLabelX = minSliderX + minSliderW + 42f;
            float maxLabelW = 30f;
            GUI.Label(new Rect(maxLabelX, row3Y, maxLabelW, rowH), "Max");
            float maxSliderW = 100f;
            float maxSliderX = maxLabelX + maxLabelW;
            float currentMax = _settings != null ? Mathf.Clamp01(_settings.OverlayBeatOpacityMax) : 1.0f;
            float nextMax = GUI.HorizontalSlider(new Rect(maxSliderX, row3Y + 4f, maxSliderW, rowH), currentMax, 0f, 1f);
            if (Mathf.Abs(nextMax - currentMax) > 0.0001f && _settings != null)
            {
                _settings.OverlayBeatOpacityMax = Mathf.Clamp01(nextMax);
                SaveSubCameraOverlaySettings();
            }
            GUI.Label(new Rect(maxSliderX + maxSliderW + 2f, row3Y, 36f, rowH), currentMax.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));

            float invertedX = maxSliderX + maxSliderW + 42f;
            float invertedW = 70f;
            bool currentInverted = _settings != null && _settings.OverlayBeatOpacityInverted;
            bool nextInverted = GUI.Toggle(new Rect(invertedX, row3Y, invertedW, rowH), currentInverted, "反転");
            if (nextInverted != currentInverted && _settings != null)
            {
                _settings.OverlayBeatOpacityInverted = nextInverted;
                LogInfo("[overlay] beat-opacity-inverted=" + nextInverted);
                SaveSubCameraOverlaySettings();
            }
        }

        private static string SerializeLogicalFaceSet(HashSet<string> set)
        {
            if (set == null || set.Count == 0)
                return "None";
            if (set.Count == OverlayLogicalFaceNames.Length)
                return "All";
            var ordered = new List<string>();
            foreach (string name in OverlayLogicalFaceNames)
                if (set.Contains(name)) ordered.Add(name);
            return string.Join(",", ordered.ToArray());
        }

        private void SaveSubCameraOverlaySettings()
        {
            try
            {
                SettingsStore.Save(System.IO.Path.Combine(_pluginDir, "MapAddSettings.json"), _settings);
            }
            catch (Exception ex)
            {
                LogWarn("[overlay] save settings failed: " + ex.Message);
            }
        }

        internal IEnumerable<string> EnumerateVideoSurfaceNames()
        {
            if (_videoRoomRoot == null)
                yield break;

            Transform root = _videoRoomRoot.transform;
            int count = root.childCount;
            for (int i = 0; i < count; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null || child.name == null)
                    continue;
                if (!child.name.StartsWith(VideoSurfacePrefix, StringComparison.Ordinal))
                    continue;
                string surfaceName = child.name.Substring(VideoSurfacePrefix.Length);
                if (string.IsNullOrWhiteSpace(surfaceName))
                    continue;
                yield return surfaceName;
            }
        }
    }
}
