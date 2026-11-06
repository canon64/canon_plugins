using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MainGameAiShoujoMode.Lighting
{
    // Native H settings, not a user-confirmed diagnosis. Owned only by the AI mode session.
    internal sealed class NativeHLightingRenderer : IDisposable
    {
#pragma warning disable CS0649 // Assigned by Unity's JSON deserializer from installed H scene data.
        [DataContract] private sealed class Defaults
        {
            [DataMember] public string cameraBundle, cameraAsset, lightJson, walkingLightJson;
            [DataMember] public Effect[] effects;
        }
        [DataContract] private sealed class Effect
        {
            [DataMember] public string type, settings;
            [DataMember] public bool enabled;
            [DataMember] public Reference[] references;
        }
        [DataContract] private sealed class Reference { [DataMember] public string field, name, resource; }
#pragma warning restore CS0649
        private sealed class Preset { internal Type Type; internal string Json; internal bool Enabled; }
        private sealed class CameraState
        {
            internal Camera Camera;
            internal bool Hdr, Msaa;
            internal DepthTextureMode Depth;
            internal readonly Dictionary<Behaviour, bool> Borrowed = new Dictionary<Behaviour, bool>();
            internal readonly List<Behaviour> Added = new List<Behaviour>();
        }
        private readonly List<Preset> _effects = new List<Preset>();
        private readonly List<Texture2D> _textures = new List<Texture2D>();
        private readonly Dictionary<Camera, CameraState> _cameras = new Dictionary<Camera, CameraState>();
        private readonly Assembly _assembly = typeof(NativeHLightingRenderer).Assembly;
        private readonly Transform _root;
        private readonly AssetBundleData _asset;
        private GameObject _templates;
        private LightingLight _light;
        private readonly LightingLight _walkingPreset;
        private bool _walkingMode;
        private Light _walkingLight;
        internal Light OwnedLight => _walkingLight;
        private bool _disposed;

        internal NativeHLightingRenderer(Transform root)
        {
            _root = root;
            var defaults = LightingJson.Read<Defaults>(ReadText("h-render-defaults.json"));
            _light = LightingJson.Read<LightingLight>(defaults.lightJson);
            _walkingPreset = LightingJson.Read<LightingLight>(defaults.walkingLightJson ?? defaults.lightJson);
            _asset = new AssetBundleData(defaults.cameraBundle, defaults.cameraAsset);
            string initializing = "camera prefab";
            try
            {
                var prefab = _asset.GetAsset<GameObject>();
                if (prefab == null) throw new InvalidOperationException("本編カメラの素材がありません。");
                initializing = "effect templates";
                _templates = new GameObject("AI native H effect settings");
                _templates.SetActive(false); // Never execute native camera/controller scripts.
                if (defaults.effects == null) throw new InvalidDataException("H描画設定のeffects配列がありません。");
                foreach (var effect in defaults.effects)
                {
                    initializing = effect.type + ": template";
                    var source = prefab.GetComponents<Behaviour>().Single(component => component != null && component.GetType().FullName == effect.type);
                    var target = (Behaviour)_templates.AddComponent(source.GetType());
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), target);
                    JsonUtility.FromJsonOverwrite(effect.settings, target);
                    foreach (var reference in effect.references)
                    {
                        initializing = effect.type + ": " + reference.field;
                        var field = HarmonyLib.AccessTools.Field(target.GetType(), reference.field)
                            ?? throw new MissingFieldException(effect.type, reference.field);
                        Object value;
                        if (reference.resource != null)
                        {
                            var original = field.GetValue(source) as Texture;
                            var texture = new Texture2D(original != null ? original.width : Texture2D.whiteTexture.width,
                                original != null ? original.height : Texture2D.whiteTexture.height, TextureFormat.RGBA32, false,
                                reference.field.IndexOf("Lut", StringComparison.OrdinalIgnoreCase) >= default(int));
                            _textures.Add(texture);
                            if (!ImageConversion.LoadImage(texture, ReadBytes(reference.resource))) throw new InvalidDataException("H色補正素材を読み込めません。");
                            texture.name = reference.name;
                            if (original != null) { texture.wrapMode = original.wrapMode; texture.filterMode = original.filterMode; }
                            value = texture;
                        }
                        else value = Shader.Find(reference.name) ?? throw new InvalidOperationException("H描画シェーダーがありません：" + reference.name);
                        field.SetValue(target, value);
                    }
                    // H's focus target is scene-owned. Walking uses the same numeric focus settings.
                    var focus = HarmonyLib.AccessTools.Field(target.GetType(), "focalTransform");
                    if (focus != null) focus.SetValue(target, null);
                    initializing = effect.type + ": graphics configuration";
                    _effects.Add(new Preset { Type = target.GetType(), Json = JsonUtility.ToJson(target), Enabled = ConfigEnabled(effect.type, effect.enabled) });
                }
            }
            catch (Exception error)
            {
                try { Dispose(); } catch (Exception cleanup) { throw new AggregateException("H描画設定の初期化・解放失敗: " + initializing, error, cleanup); }
                throw new InvalidOperationException("H描画設定の初期化失敗: " + initializing, error);
            }
        }
        private static bool ConfigEnabled(string type, bool fallback)
        {
            var graphics = Manager.Config.GraphicData;
            if (type.EndsWith(".BloomAndFlares", StringComparison.Ordinal)) return graphics.Bloom;
            if (type.EndsWith(".DepthOfField", StringComparison.Ordinal)) return graphics.DepthOfField;
            if (type.EndsWith(".GlobalFog", StringComparison.Ordinal)) return graphics.Fog;
            if (type.EndsWith(".VignetteAndChromaticAberration", StringComparison.Ordinal)) return graphics.Vignette;
            if (type == "AmplifyOcclusionEffect") return graphics.AmplifyOcclus;
            if (type.EndsWith(".SunShafts", StringComparison.Ordinal)) return graphics.SunShafts;
            return fallback;
        }
        private string Resource(string suffix) => _assembly.GetManifestResourceNames().Single(name => name.EndsWith(".Lighting.Native." + suffix, StringComparison.Ordinal));
        private byte[] ReadBytes(string suffix)
        {
            using (var stream = _assembly.GetManifestResourceStream(Resource(suffix)))
            using (var output = new MemoryStream()) { stream.CopyTo(output); return output.ToArray(); }
        }
        private string ReadText(string suffix) => System.Text.Encoding.UTF8.GetString(ReadBytes(suffix));

        internal void Walking(Camera camera)
        {
            Suspend();
            if (_disposed || camera == null) return;
            _walkingMode = true;
            try
            {
                var node = new GameObject("AI native H walking light");
                node.transform.SetParent(_walkingPreset.anchor == "camera" ? camera.transform : _root, false);
                _walkingLight = node.AddComponent<Light>();
                LightingUnity.ApplyLight(_walkingPreset, _walkingLight, camera);
                _walkingLight.enabled = _walkingPreset.enabled && _walkingPreset.active;
            }
            catch { Suspend(); throw; }
        }
        internal void ApplyCamera(Camera camera)
        {
            if (_disposed || _walkingMode || camera == null || _cameras.ContainsKey(camera)) return;
            var state = new CameraState { Camera = camera, Hdr = camera.allowHDR, Msaa = camera.allowMSAA, Depth = camera.depthTextureMode };
            _cameras.Add(camera, state);
            foreach (var component in camera.GetComponents<Behaviour>())
                if (component != null && (LightingUnity.IsImageEffect(component.GetType())
                    || component.GetType().FullName == "UnityEngine.Rendering.PostProcessing.PostProcessVolume"
                    || component is CameraEffectorConfig))
                { state.Borrowed.Add(component, component.enabled); component.enabled = false; }
            // Appended in the H scene's actual component order; existing sky/VR components stay owned by their hosts.
            foreach (var preset in _effects)
            {
                var target = (Behaviour)camera.gameObject.AddComponent(preset.Type);
                state.Added.Add(target); target.enabled = false;
                JsonUtility.FromJsonOverwrite(preset.Json, target); target.enabled = preset.Enabled;
            }
            camera.allowHDR = true; camera.allowMSAA = true;
        }
        internal void HReady(Camera camera, HSceneProc scene, Camera output = null)
        {
            Suspend();
            _walkingMode = false;
            if (_disposed || camera == null || scene?.lightCamera == null) return;
            // Read the actual H baseline once; do not rewrite H's native light or copy its controller.
            _light = LightingUnity.CaptureLight(scene.lightCamera, camera);
            foreach (var preset in _effects)
            {
                var component = (output != null ? output : camera).GetComponents<Behaviour>().FirstOrDefault(item => item != null && item.GetType() == preset.Type);
                if (component == null && output != null) component = camera.GetComponents<Behaviour>().FirstOrDefault(item => item != null && item.GetType() == preset.Type);
                if (component == null) continue;
                var template = _templates.GetComponent(preset.Type);
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(component), template);
                var focus = HarmonyLib.AccessTools.Field(preset.Type, "focalTransform");
                if (focus != null) focus.SetValue(template, null);
                preset.Json = JsonUtility.ToJson(template); preset.Enabled = component.enabled;
            }
        }
        internal void Suspend()
        {
            if (_walkingLight != null) { _walkingLight.enabled = false; Object.DestroyImmediate(_walkingLight.gameObject); _walkingLight = null; }
            foreach (var state in _cameras.Values)
            {
                foreach (var component in state.Added) if (component != null) { component.enabled = false; Object.DestroyImmediate(component); }
                foreach (var pair in state.Borrowed) if (pair.Key != null) pair.Key.enabled = pair.Value;
                if (state.Camera != null) { state.Camera.allowHDR = state.Hdr; state.Camera.allowMSAA = state.Msaa; state.Camera.depthTextureMode = state.Depth; }
            }
            _cameras.Clear();
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            Suspend();
            if (_templates != null) Object.DestroyImmediate(_templates);
            foreach (var texture in _textures) if (texture != null) Object.DestroyImmediate(texture);
            _textures.Clear(); _effects.Clear(); _asset?.UnloadBundle();
        }
    }
}
