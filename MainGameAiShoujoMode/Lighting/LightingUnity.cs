using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MainGameAiShoujoMode.Lighting
{
    internal static class LightingUnity
    {
        private static readonly string[] LightProperties = { "enabled", "type", "color", "intensity", "range", "spotAngle", "shadows", "shadowStrength", "shadowBias", "shadowNormalBias", "shadowNearPlane", "shadowCustomResolution", "renderMode", "cullingMask", "bounceIntensity", "colorTemperature", "useColorTemperature" };
        private static readonly string[] CameraProperties = { "enabled", "allowHDR", "allowMSAA", "renderingPath", "actualRenderingPath", "clearFlags", "backgroundColor", "cullingMask", "depth", "fieldOfView", "nearClipPlane", "farClipPlane", "depthTextureMode", "stereoEnabled", "stereoTargetEye", "targetDisplay", "rect" };
        private static readonly string[] EnvironmentProperties = { "ambientMode", "ambientLight", "ambientIntensity", "ambientSkyColor", "ambientEquatorColor", "ambientGroundColor", "fog", "fogMode", "fogColor", "fogDensity", "fogStartDistance", "fogEndDistance", "reflectionIntensity", "reflectionBounces", "defaultReflectionMode", "customReflection", "skybox", "sun", "ambientProbe" };
        internal static string PathOf(Transform transform)
        {
            if (transform == null) return null;
            var result = new Stack<string>();
            for (var current = transform; current != null; current = current.parent) result.Push(current.name + "[" + current.GetSiblingIndex() + "]");
            return transform.gameObject.scene.name + "/" + string.Join("/", result);
        }
        internal static string Reference(Object value) => value == null ? null : value.GetType().FullName + ":" + value.name;
        internal static LightingValue Encode(string name, object value, Type type)
        {
            var item = new LightingValue { name = name };
            if (typeof(Object).IsAssignableFrom(type)) { item.kind = type == typeof(Shader) || typeof(Texture).IsAssignableFrom(type) ? "reference" : "unsupported"; item.text = Reference(value as Object); return item; }
            if (value == null) { item.kind = "null"; return item; }
            if (type.IsEnum) { item.kind = "enum"; item.text = Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture); return item; }
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
            { item.kind = "scalar"; item.text = value is float single ? single.ToString("R", CultureInfo.InvariantCulture) : value is double real ? real.ToString("R", CultureInfo.InvariantCulture) : Convert.ToString(value, CultureInfo.InvariantCulture); return item; }
            item.kind = type.FullName;
            if (value is Vector2 pair) item.values = new[] { pair.x, pair.y };
            else if (value is Vector3 vector) item.values = new[] { vector.x, vector.y, vector.z };
            else if (value is Vector4 four) item.values = new[] { four.x, four.y, four.z, four.w };
            else if (value is Quaternion rotation) item.values = new[] { rotation.x, rotation.y, rotation.z, rotation.w };
            else if (value is Color color) item.values = new[] { color.r, color.g, color.b, color.a };
            else if (value is Rect rect) item.values = new[] { rect.x, rect.y, rect.width, rect.height };
            else if (value is float[] numbers) item.values = numbers.ToArray();
            else if (value is AnimationCurve curve) item.text = JsonUtility.ToJson(new CurveValue { value = curve });
            else if (type == typeof(UnityEngine.Rendering.SphericalHarmonicsL2)) item.values = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(field => field.FieldType == typeof(float)).OrderBy(field => field.Name, StringComparer.Ordinal).Select(field => (float)field.GetValue(value)).ToArray();
            else { item.kind = "unsupported"; try { item.text = type.FullName + ":" + JsonUtility.ToJson(value); } catch { item.text = type.FullName; } }
            return item;
        }
        [Serializable] private sealed class CurveValue { public AnimationCurve value; }
        internal static object Decode(LightingValue value, Type type, object current)
        {
            if (value.kind == "reference")
            {
                if (Reference(current as Object) != value.text) throw new InvalidOperationException("画面効果の素材参照が一致しません：" + value.name);
                return current;
            }
            if (value.kind == "null" && !type.IsValueType) return null;
            if (type.IsEnum && value.kind == "enum") return Enum.ToObject(type, long.Parse(value.text, CultureInfo.InvariantCulture));
            if ((type.IsPrimitive || type == typeof(string) || type == typeof(decimal)) && value.kind == "scalar")
            {
                var decoded = Convert.ChangeType(value.text, type, CultureInfo.InvariantCulture);
                if ((decoded is float single && (float.IsNaN(single) || float.IsInfinity(single))) || (decoded is double real && (double.IsNaN(real) || double.IsInfinity(real))))
                    throw new InvalidOperationException("有限でない画面設定です：" + value.name);
                return decoded;
            }
            if (type.FullName != value.kind) throw new InvalidOperationException("保存値の型が一致しません：" + value.name);
            var array = value.values ?? Array.Empty<float>(); var queue = new Queue<float>(array);
            if (type == typeof(Vector2)) return new Vector2(queue.Dequeue(), queue.Dequeue());
            if (type == typeof(Vector3)) return new Vector3(queue.Dequeue(), queue.Dequeue(), queue.Dequeue());
            if (type == typeof(Vector4)) return new Vector4(queue.Dequeue(), queue.Dequeue(), queue.Dequeue(), queue.Dequeue());
            if (type == typeof(Quaternion)) return new Quaternion(queue.Dequeue(), queue.Dequeue(), queue.Dequeue(), queue.Dequeue());
            if (type == typeof(Color)) return new Color(queue.Dequeue(), queue.Dequeue(), queue.Dequeue(), queue.Dequeue());
            if (type == typeof(Rect)) return new Rect(queue.Dequeue(), queue.Dequeue(), queue.Dequeue(), queue.Dequeue());
            if (type == typeof(float[])) return array.ToArray();
            if (type == typeof(AnimationCurve)) return JsonUtility.FromJson<CurveValue>(value.text).value;
            throw new InvalidOperationException("再現できない設定です：" + value.name);
        }
        private static LightingValue[] Properties(object owner, Type type, IEnumerable<string> names)
        {
            var result = new List<LightingValue>();
            foreach (var name in names)
            {
                var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                if (property == null || !property.CanRead) continue;
                try { result.Add(Encode(name, property.GetValue(owner, null), property.PropertyType)); }
                catch (Exception error) { result.Add(new LightingValue { name = name, kind = "read-error", text = error.GetBaseException().GetType().Name }); }
            }
            return result.ToArray();
        }
        internal static LightingLight CaptureLight(Light light, Camera camera)
        {
            if (light == null) return null;
            var relative = camera != null && light.transform.IsChildOf(camera.transform);
            var position = relative ? camera.transform.InverseTransformPoint(light.transform.position) : light.transform.position;
            var rotation = relative ? Quaternion.Inverse(camera.transform.rotation) * light.transform.rotation : light.transform.rotation;
            return new LightingLight { path = PathOf(light.transform), anchor = relative ? "camera" : "world", cookie = Reference(light.cookie),
                enabled = light.enabled, active = light.gameObject.activeInHierarchy,
                settings = Properties(light, typeof(Light), LightProperties).Concat(new[] { Encode("position", position, typeof(Vector3)), Encode("rotation", rotation, typeof(Quaternion)) }).ToArray() };
        }
        internal static void ApplyLight(LightingLight value, Light target, Camera camera)
        {
            if (target == null || value == null) return;
            var changes = new List<Action>();
            foreach (var setting in value.settings)
            {
                if (setting.name == "position") { var position = (Vector3)Decode(setting, typeof(Vector3), null); if (value.anchor != "camera" || camera != null) changes.Add(() => target.transform.position = value.anchor == "camera" ? camera.transform.TransformPoint(position) : position); continue; }
                if (setting.name == "rotation") { var rotation = (Quaternion)Decode(setting, typeof(Quaternion), null); if (value.anchor != "camera" || camera != null) changes.Add(() => target.transform.rotation = value.anchor == "camera" ? camera.transform.rotation * rotation : rotation); continue; }
                if (!LightProperties.Contains(setting.name)) throw new InvalidOperationException("未対応のライト設定です。");
                var property = typeof(Light).GetProperty(setting.name);
                if (property == null || !property.CanWrite) throw new InvalidOperationException("ライト設定が利用できません：" + setting.name);
                var decoded = Decode(setting, property.PropertyType, property.GetValue(target, null));
                changes.Add(() => property.SetValue(target, decoded, null));
            }
            foreach (var change in changes) change();
        }
        internal static bool IsImageEffect(Type type) => type != null && type != typeof(MapEnvironmentRuntime.SkyTemporalFilter)
            && (type.Namespace == "UnityStandardAssets.ImageEffects" || type.Name == "AmplifyOcclusionEffect" || type.Name == "AmplifyColorEffect"
                || type.FullName == "UnityEngine.PostProcessing.PostProcessingBehaviour"
                || type.FullName == "UnityEngine.Rendering.PostProcessing.PostProcessLayer");
        private static FieldInfo[] Fields(Type type) => type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(field => !field.IsStatic && !field.IsNotSerialized && (field.IsPublic || field.IsDefined(typeof(SerializeField), true)))
            .OrderBy(field => field.Name, StringComparer.Ordinal).ToArray();
        internal static LightingEffect CaptureEffect(Behaviour component)
        {
            var type = component.GetType();
            return new LightingEffect { type = type.FullName, assembly = type.Assembly.GetName().Name, path = PathOf(component.transform),
                enabled = component.enabled, imageEffect = IsImageEffect(type),
                order = component.gameObject.GetComponents<Behaviour>().Where(item => item != null && (IsImageEffect(item.GetType()) || item is MapEnvironmentRuntime.SkyTemporalFilter)).TakeWhile(item => item != component).Count(),
                fields = Fields(type).Select(field => Encode(field.Name, field.GetValue(component), field.FieldType)).ToArray() };
        }
        internal static void ValidateEffect(LightingEffect value, Behaviour component)
        {
            if (component == null || component.GetType().FullName != value.type || !IsImageEffect(component.GetType())) throw new InvalidOperationException("歩行カメラに対応する画面効果がありません：" + value.type);
            if (CaptureEffect(component).order != value.order) throw new InvalidOperationException("画面効果の適用順が異なります。順序を変える処理は原因確認後に限定して接続してください：" + value.type);
            var fields = Fields(component.GetType()).ToDictionary(field => field.Name, StringComparer.Ordinal);
            foreach (var setting in value.fields)
            {
                if (!fields.TryGetValue(setting.name, out var field) || field.IsInitOnly) throw new InvalidOperationException("画面効果の設定が利用できません：" + setting.name);
                Decode(setting, field.FieldType, field.GetValue(component));
            }
        }
        internal static void ApplyEffect(LightingEffect value, Behaviour component)
        {
            ValidateEffect(value, component);
            foreach (var setting in value.fields)
            {
                var field = Fields(component.GetType()).Single(item => item.Name == setting.name);
                if (setting.kind != "reference") field.SetValue(component, Decode(setting, field.FieldType, field.GetValue(component)));
            }
            component.enabled = value.enabled;
        }
        internal static LightingCamera CaptureCamera(Camera camera, Camera managed)
        {
            return new LightingCamera { path = PathOf(camera.transform), role = camera == managed ? "managed" : camera.stereoEnabled ? "stereo-output" : "output",
                settings = Properties(camera, typeof(Camera), CameraProperties).Concat(new[] { Encode("position", camera.transform.position, typeof(Vector3)), Encode("rotation", camera.transform.rotation, typeof(Quaternion)),
                    new LightingValue { name = "component-order", kind = "text", text = string.Join(" -> ", camera.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().FullName)) },
                    new LightingValue { name = "command-buffers", kind = "text", text = string.Join(";", Enum.GetValues(typeof(UnityEngine.Rendering.CameraEvent)).Cast<UnityEngine.Rendering.CameraEvent>()
                        .SelectMany(phase => camera.GetCommandBuffers(phase).Select(buffer => phase + ":" + buffer.name))) } }).ToArray(),
                components = camera.GetComponents<Behaviour>().Where(c => c != null).Select(CaptureEffect).ToArray() };
        }
        internal static Camera[] OutputCameras(Camera managed)
        {
            return new[] { managed }.Concat(Camera.allCameras.Where(camera => camera.enabled && (camera.stereoEnabled || camera == Camera.main)))
                .Where(camera => camera != null).Distinct().ToArray();
        }
        internal static LightingSnapshot Snapshot(string stage, Camera managed, MapEnvironmentRuntime.EnvironmentWorld world, IEnumerable<ChaControl> characters)
        {
            return new LightingSnapshot { stage = stage, utc = DateTime.UtcNow.ToString("o"), hour = world.Hour, year = world.Year, month = world.Month, day = world.Day, weather = world.Weather,
                viewNote = "比較は同じ時刻・位置・視線で行ってください。VRの管理カメラと実描画カメラを併記。",
                environment = Properties(null, typeof(RenderSettings), EnvironmentProperties).Concat(new[] { Encode("pixelLightCount", QualitySettings.pixelLightCount, typeof(int)), Encode("colorSpace", QualitySettings.activeColorSpace, typeof(ColorSpace)), Encode("shadowDistance", QualitySettings.shadowDistance, typeof(float)) }).ToArray(),
                lights = Resources.FindObjectsOfTypeAll<Light>().Where(light => light != null && light.gameObject.scene.IsValid() && light.gameObject.scene.isLoaded).OrderBy(light => PathOf(light.transform), StringComparer.Ordinal).Select(light => CaptureLight(light, managed)).ToArray(),
                cameras = OutputCameras(managed).Select(camera => CaptureCamera(camera, managed)).ToArray(),
                characters = characters.Where(character => character != null).SelectMany(character => character.GetComponentsInChildren<Renderer>(true)).Select(renderer => new LightingRenderer { path = PathOf(renderer.transform),
                    settings = Properties(renderer, typeof(Renderer), new[] { "enabled", "lightProbeUsage", "reflectionProbeUsage", "shadowCastingMode", "receiveShadows", "lightmapIndex", "lightmapScaleOffset" }).Concat(new[] { Encode("layer", renderer.gameObject.layer, typeof(int)) })
                        .Concat(renderer.sharedMaterials.Where(material => material != null).SelectMany(MaterialValues)).ToArray() }).ToArray() };
        }
        private static IEnumerable<LightingValue> MaterialValues(Material material)
        {
            var prefix = "material/" + material.name + "/";
            yield return new LightingValue { name = prefix + "shader", kind = "reference", text = Reference(material.shader) };
            yield return Encode(prefix + "renderQueue", material.renderQueue, typeof(int));
            yield return new LightingValue { name = prefix + "keywords", kind = "text", text = string.Join(";", material.shaderKeywords.OrderBy(name => name, StringComparer.Ordinal)) };
            var shader = material.shader;
            var countMethod = typeof(Shader).GetMethod("GetPropertyCount", Type.EmptyTypes);
            var nameMethod = typeof(Shader).GetMethod("GetPropertyName", new[] { typeof(int) });
            var typeMethod = typeof(Shader).GetMethod("GetPropertyType", new[] { typeof(int) });
            if (shader != null && countMethod != null && nameMethod != null && typeMethod != null)
            {
                foreach (var index in Enumerable.Range(default(int), (int)countMethod.Invoke(shader, null)))
                {
                    var name = (string)nameMethod.Invoke(shader, new object[] { index });
                    var kind = typeMethod.Invoke(shader, new object[] { index }).ToString();
                    if (kind == "Color") yield return Encode(prefix + "property/" + name, material.GetColor(name), typeof(Color));
                    else if (kind == "Vector") yield return Encode(prefix + "property/" + name, material.GetVector(name), typeof(Vector4));
                    else if (kind == "Texture")
                    {
                        yield return new LightingValue { name = prefix + "property/" + name, kind = "reference", text = Reference(material.GetTexture(name)) };
                        yield return Encode(prefix + "texture-scale/" + name, material.GetTextureScale(name), typeof(Vector2));
                        yield return Encode(prefix + "texture-offset/" + name, material.GetTextureOffset(name), typeof(Vector2));
                    }
                    else yield return Encode(prefix + "property/" + name, material.GetFloat(name), typeof(float));
                }
            }
            foreach (var name in new[] { "_Color", "_ShadowColor", "_SpecColor", "_EmissionColor" }) if (material.HasProperty(name)) yield return Encode(prefix + name, material.GetColor(name), typeof(Color));
            foreach (var name in new[] { "_Glossiness", "_Metallic", "_Cutoff", "_BumpScale" }) if (material.HasProperty(name)) yield return Encode(prefix + name, material.GetFloat(name), typeof(float));
        }
    }
}
