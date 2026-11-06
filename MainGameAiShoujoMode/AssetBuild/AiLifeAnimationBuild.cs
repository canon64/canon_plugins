using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapEnvironmentRuntime;
using UnityEditor;
using UnityEngine;

// Offline asset generation only: no preview, rendering, game launch or gameplay scripts.
public static class AiLifeAnimationBuild
{
    [Serializable] public sealed class ClipInput { public string id, bundle, name, key, family, nativeBundle, asset; }
    [Serializable] public sealed class ActionInput { public string id, bundle, controller, state, outState, description; public int posture, pose; }
    [Serializable] public sealed class Input { public string output, generated; public ClipInput[] clips; public ActionInput[] actions; }
    [Serializable] public sealed class ClipResult { public string id, sourceBundle, sourceName, bundle, asset, status, error; public float duration, replayError, packedRotationError, packedPositionError; public int mappedBones, samples; }
    [Serializable] public sealed class Binding { public string id, phase, state, clip, status, error; public string[] candidates; }
    [Serializable] public sealed class Report { public ClipResult[] clips; public Binding[] bindings; public string[] bonePairs; public bool gameVerified, packedVerified; public float packedRotationError, packedPositionError; }
    private sealed class Rest { public Transform node; public Quaternion local, parentWorld; public Vector3 position; }
    private sealed class Pair { public string source, target, path; public Quaternion basis; }
    public static void Build()
    {
        SandboxBuild.VerifyAudit(); var p = SandboxBuild.Settings();
        var input = JsonCodec.FromJson<Input>(File.ReadAllText(Environment.GetEnvironmentVariable("AI_LIFE_ANIMATION_INPUT")));
        Directory.CreateDirectory(input.output); Directory.CreateDirectory(input.generated);
        var sourceBones = AssetBundle.LoadFromFile(p.T("animation.source.skeleton"));
        AssetBundle targetBones = null;
        GameObject source = null, target = null;
        var results = new List<ClipResult>(); var bindings = new List<Binding>(); var packages = new List<AssetBundleBuild>();
        try
        {
            source = UnityEngine.Object.Instantiate(sourceBones.LoadAsset<GameObject>(p.T("animation.source.prefab")));
            sourceBones.Unload(false); sourceBones = null;
            targetBones = AssetBundle.LoadFromFile(p.T("animation.target.skeleton"));
            target = UnityEngine.Object.Instantiate(targetBones.LoadAsset<GameObject>(p.T("animation.target.prefab")));
            var src = Bones(source); var dst = Bones(target);
            var pairs = p.T("animation.bonePairs").Split(';').Select(v => v.Split('=')).Select(v => new Pair {
                source = v.First(), target = v.Last(), path = AnimationUtility.CalculateTransformPath(dst[v.Last()].node, target.transform),
                basis = Quaternion.Inverse(dst[v.Last()].parentWorld) * src[v.First()].parentWorld }).ToArray();
            var animator = source.GetComponent<Animator>() ?? source.AddComponent<Animator>();
            animator.fireEvents = false; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var ratio = dst[p.T("animation.hips.target")].node.position.y / src[p.T("animation.hips.source")].node.position.y;
            foreach (var group in input.clips.GroupBy(v => v.bundle))
            {
                var bundle = AssetBundle.LoadFromFile(group.First().nativeBundle ?? group.Key); var builds = new List<string>();
                if (bundle == null) throw new IOException("Cannot load native animation bundle: " + group.Key);
                try
                {
                    if (bundle.isStreamedSceneAssetBundle)
                    {
                        foreach (var item in group) results.Add(new ClipResult { id = item.id, sourceBundle = item.bundle, sourceName = item.name, status = "native-scene-motion" });
                        continue;
                    }
                    var clips = bundle.LoadAllAssets<AnimationClip>().Concat(bundle.LoadAllAssets<RuntimeAnimatorController>().SelectMany(v => v.animationClips)).Distinct().GroupBy(v => v.name).ToDictionary(v => v.Key, v => v.ToArray());
                    AssetDatabase.StartAssetEditing();
                    try
                    {
                    foreach (var item in group)
                    {
                        var result = new ClipResult { id = item.id, sourceBundle = item.bundle, sourceName = item.name, status = "unconverted" }; results.Add(result);
                        if (item.family == "object") { result.status = "native-object-motion"; continue; }
                        try
                        {
                            AnimationClip original = null;
                            if (!string.IsNullOrWhiteSpace(item.asset)) original = bundle.LoadAsset<AnimationClip>(item.asset);
                            if (original == null && clips.TryGetValue(item.name, out var candidates)) original = SelectOriginal(candidates, item.id);
                            if (original == null) throw new InvalidDataException("Native clip ambiguous; source path ID not exposed by Unity");
                            var existingPath = input.generated + "/" + item.key + ".anim";
                            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(existingPath);
                            if (existing != null && CheckExisting(original, existing, src, dst, pairs, p, out var cachedError, out var cachedBones))
                            {
                                result.duration = original.length; result.samples = Mathf.Max(p.I("math.two"), Mathf.CeilToInt(original.length * existing.frameRate) + p.I("math.one"));
                                result.replayError = cachedError; result.mappedBones = cachedBones; result.asset = existingPath; result.status = "converted"; builds.Add(existingPath); continue;
                            }
                            var motion = new AnimationClip { name = item.key, frameRate = original.frameRate > p.N("math.zero") ? original.frameRate : p.N("animation.sampleRate") };
                            result.duration = original.length;
                            result.samples = Mathf.Max(p.I("math.two"), Mathf.CeilToInt(original.length * motion.frameRate) + p.I("math.one"));
                            var curves = new Dictionary<string, List<Keyframe>[]>(); var moved = new HashSet<string>();
                            var reference = new Dictionary<string, Quaternion[]>();
                            Restore(src); Restore(dst);
                            foreach (var pair in pairs) reference[pair.target] = new Quaternion[result.samples];
                            for (int frame = p.I("math.zero"); frame < result.samples; frame += p.I("math.one"))
                            {
                                var time = original.length * frame / (result.samples - p.I("math.one")); original.SampleAnimation(source, time);
                                foreach (var pair in pairs)
                                {
                                    var s = src[pair.source]; var t = dst[pair.target];
                                    var q = pair.basis * (s.node.localRotation * Quaternion.Inverse(s.local)) * Quaternion.Inverse(pair.basis) * t.local;
                                    if (new[] { q.x, q.y, q.z, q.w }.Any(v => float.IsNaN(v) || float.IsInfinity(v))) throw new InvalidDataException("Non-finite retargeted rotation");
                                    reference[pair.target][frame] = q;
                                    if (RotationError(q, t.local, p) > p.N("animation.angleTolerance")) moved.Add(pair.target);
                                    Write(curves, pair.path + "|m_LocalRotation", time, new[] { q.x, q.y, q.z, q.w });
                                    if (pair.target == p.T("animation.hips.target"))
                                    {
                                        var position = t.position + (s.node.localPosition - s.position) * ratio;
                                        if (new[] { position.x, position.y, position.z }.Any(v => float.IsNaN(v) || float.IsInfinity(v))) throw new InvalidDataException("Non-finite retargeted position");
                                        Write(curves, pair.path + "|m_LocalPosition", time, new[] { position.x, position.y, position.z });
                                    }
                                }
                            }
                            result.mappedBones = moved.Count;
                            if (!moved.Any()) throw new InvalidDataException("No mapped female body motion; source retained");
                            foreach (var curve in curves)
                            {
                                var parts = curve.Key.Split('|'); var axes = "xyzw";
                                for (int axis = p.I("math.zero"); axis < curve.Value.Length; axis += p.I("math.one"))
                                    AnimationUtility.SetEditorCurve(motion, EditorCurveBinding.FloatCurve(parts.First(), typeof(Transform), parts.Last() + "." + axes[axis]), new AnimationCurve(curve.Value[axis].ToArray()));
                            }
                            motion.EnsureQuaternionContinuity(); var settings = AnimationUtility.GetAnimationClipSettings(motion); settings.loopTime = original.isLooping; AnimationUtility.SetAnimationClipSettings(motion, settings);
                            // Native sampling checks the baked clip against the computed target local rotations.
                            foreach (var frame in new[] { p.I("math.zero"), (result.samples - p.I("math.one")) / p.I("math.two"), result.samples - p.I("math.one") }.Distinct())
                            {
                                Restore(dst); motion.SampleAnimation(target, original.length * frame / (result.samples - p.I("math.one")));
                                foreach (var pair in pairs) result.replayError = Mathf.Max(result.replayError, RotationError(dst[pair.target].node.localRotation, reference[pair.target][frame], p));
                            }
                            if (result.replayError > p.N("animation.angleTolerance")) throw new InvalidDataException("Baked clip replay error: " + result.replayError);
                            result.asset = input.generated + "/" + item.key + ".anim";
                            AssetDatabase.DeleteAsset(result.asset); AssetDatabase.CreateAsset(motion, result.asset); builds.Add(result.asset);
                            result.status = "converted";
                        }
                        catch (Exception error) { result.status = "unconverted"; result.error = error.GetBaseException().Message; }
                    }
                    }
                    finally { AssetDatabase.StopAssetEditing(); }
                    foreach (var action in input.actions.Where(v => v.bundle == group.Key))
                    foreach (var phase in new[] { "main", "out" })
                    {
                        var state = phase == "main" ? action.state : action.outState;
                        if (string.IsNullOrWhiteSpace(state)) continue;
                        var binding = new Binding { id = action.id, phase = phase, state = state, status = "unresolved" }; bindings.Add(binding);
                        try
                        {
                            var controller = bundle.LoadAsset<RuntimeAnimatorController>(action.controller);
                            if (controller == null) throw new InvalidDataException("Native controller unavailable");
                            animator.runtimeAnimatorController = controller; Restore(src); animator.Rebind();
                            bool found = false;
                            for (int layer = p.I("math.zero"); layer < animator.layerCount; layer += p.I("math.one"))
                            {
                                var hash = Animator.StringToHash(animator.GetLayerName(layer) + "." + state);
                                if (!animator.HasState(layer, hash)) { hash = Animator.StringToHash(state); if (!animator.HasState(layer, hash)) continue; }
                                animator.Play(hash, layer, p.N("math.zero")); animator.Update(p.N("math.zero"));
                                var active = animator.GetCurrentAnimatorClipInfo(layer).Where(v => v.clip != null && v.weight > p.N("render.epsilon")).Select(v => v.clip.name).Distinct().ToArray();
                                binding.candidates = active;
                                if (active.Length != p.I("math.one")) throw new InvalidDataException("State uses zero or multiple active clips; retain candidates");
                                var clip = results.SingleOrDefault(v => v.sourceBundle == group.Key && v.sourceName == active.Single());
                                if (clip?.status != "converted") throw new InvalidDataException("State clip is not converted");
                                binding.clip = clip.id; binding.status = "resolved"; found = true; break;
                            }
                            if (!found) throw new InvalidDataException("Native state not found: " + state);
                        }
                        catch (Exception error) { binding.error = error.GetBaseException().Message; }
                    }
                    animator.runtimeAnimatorController = null;
                    if (builds.Any())
                    {
                        AssetDatabase.SaveAssets(); var filename = "life-" + group.First().key + ".bundle";
                        packages.Add(new AssetBundleBuild { assetBundleName = filename, assetNames = builds.ToArray() });
                        foreach (var result in results.Where(v => v.sourceBundle == group.Key && v.status == "converted")) result.bundle = filename;
                    }
                    File.WriteAllText(Path.Combine(input.output, "catalog.json"), JsonCodec.ToJson(new Report { clips = results.ToArray(), bindings = bindings.ToArray(), bonePairs = p.T("animation.bonePairs").Split(';'), gameVerified = false }, true));
                    Debug.Log("AI_LIFE_ANIMATION_BUNDLE_DONE " + group.Key + " converted=" + builds.Count);
                }
                finally { animator.runtimeAnimatorController = null; if (bundle != null) bundle.Unload(true); }
            }
            if (packages.Any()) BuildPipeline.BuildAssetBundles(input.output, packages.ToArray(), BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
            File.WriteAllText(Path.Combine(input.output, "catalog.json"), JsonCodec.ToJson(new Report { clips = results.ToArray(), bindings = bindings.ToArray(), bonePairs = p.T("animation.bonePairs").Split(';'), gameVerified = false }, true));
            Debug.Log("AI_LIFE_ANIMATION_BUILD_PASS converted=" + results.Count(v => v.status == "converted") + " total=" + results.Count);
        }
        finally
        {
            if (source != null) UnityEngine.Object.DestroyImmediate(source); if (target != null) UnityEngine.Object.DestroyImmediate(target);
            if (sourceBones != null) sourceBones.Unload(true); if (targetBones != null) targetBones.Unload(true);
        }
    }
    private static Dictionary<string, Rest> Bones(GameObject root) => root.GetComponentsInChildren<Transform>(true).GroupBy(v => v.name).ToDictionary(v => v.Key, v => new Rest { node = v.First(), local = v.First().localRotation, position = v.First().localPosition, parentWorld = v.First().parent == null ? Quaternion.identity : v.First().parent.rotation });
    public static void BindOnly()
    {
        SandboxBuild.VerifyAudit(); var p = SandboxBuild.Settings();
        var input = JsonCodec.FromJson<Input>(File.ReadAllText(Environment.GetEnvironmentVariable("AI_LIFE_ANIMATION_INPUT")));
        var path = Path.Combine(input.output, "catalog.json"); var report = JsonCodec.FromJson<Report>(File.ReadAllText(path));
        var bones = AssetBundle.LoadFromFile(p.T("animation.source.skeleton"));
        var source = UnityEngine.Object.Instantiate(bones.LoadAsset<GameObject>(p.T("animation.source.prefab"))); bones.Unload(false);
        var targetBundle = AssetBundle.LoadFromFile(p.T("animation.target.skeleton"));
        var target = UnityEngine.Object.Instantiate(targetBundle.LoadAsset<GameObject>(p.T("animation.target.prefab"))); targetBundle.Unload(false);
        var targetRest = Bones(target);
        var targetNames = p.T("animation.bonePairs").Split(';').Select(v => v.Split('=').Last()).ToArray();
        var animator = source.GetComponent<Animator>() ?? source.AddComponent<Animator>();
        animator.fireEvents = false; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var bindings = new List<Binding>();
        try
        {
            foreach (var group in input.actions.GroupBy(v => v.bundle))
            {
                var originals = input.clips.Where(v => v.bundle == group.Key).ToArray();
                var bundle = AssetBundle.LoadFromFile(originals.FirstOrDefault()?.nativeBundle ?? group.Key);
                var nativeIds = new Dictionary<AnimationClip, string>();
                foreach (var item in originals.Where(v => !string.IsNullOrWhiteSpace(v.asset)))
                {
                    var clip = bundle.LoadAsset<AnimationClip>(item.asset);
                    if (clip != null) nativeIds[clip] = item.id;
                }
                try
                {
                    foreach (var action in group)
                    foreach (var raw in new[] { new { phase = "main", state = action.state }, new { phase = "out", state = action.outState } })
                    {
                        var states = (raw.state ?? "").Split('/').Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();
                        foreach (var part in states.Select((state, index) => new { state, index }))
                        {
                            var phase = part.index == states.Length - p.I("math.one") ? raw.phase : raw.phase + ":enter:" + part.index;
                            var binding = new Binding { id = action.id, phase = phase, state = part.state, status = "unresolved" }; bindings.Add(binding);
                            try
                            {
                                var controller = bundle.LoadAsset<RuntimeAnimatorController>(action.controller);
                                if (controller == null) throw new InvalidDataException("Native controller unavailable");
                                animator.runtimeAnimatorController = controller; animator.Rebind();
                                bool found = false;
                                for (int layer = p.I("math.zero"); layer < animator.layerCount; layer += p.I("math.one"))
                                {
                                    var hash = Animator.StringToHash(animator.GetLayerName(layer) + "." + part.state);
                                    if (!animator.HasState(layer, hash)) { hash = Animator.StringToHash(part.state); if (!animator.HasState(layer, hash)) continue; }
                                    animator.Play(hash, layer, p.N("math.zero")); animator.Update(p.N("math.zero"));
                                    var names = animator.GetCurrentAnimatorClipInfo(layer).Where(v => v.clip != null && v.weight > p.N("render.epsilon")).Select(v => v.clip.name).Distinct().ToArray();
                                    binding.candidates = names;
                                    if (names.Length != p.I("math.one")) throw new InvalidDataException("Native blend requires separate selection");
                                    var active = animator.GetCurrentAnimatorClipInfo(layer).Where(v => v.clip != null && v.weight > p.N("render.epsilon")).Select(v => v.clip).Distinct().ToArray();
                                    var matches = report.clips.Where(v => v.sourceBundle == group.Key && v.sourceName == names.Single()).ToArray();
                                    var clip = active.Length == p.I("math.one") && nativeIds.TryGetValue(active.Single(), out var nativeId) ? report.clips.Single(v => v.id == nativeId) : matches.Length == p.I("math.one") ? matches.Single() : matches.SingleOrDefault(v => active.Length == p.I("math.one") && LocalId(active.Single()) == v.id.Split('|').Last());
                                    if (clip?.status != "converted") throw new InvalidDataException("State clip has not been converted");
                                    binding.clip = clip.id; binding.status = "resolved"; found = true; break;
                                }
                                if (!found) throw new InvalidDataException("Native state not found");
                            }
                            catch (Exception error) { binding.error = error.GetBaseException().Message; }
                        }
                    }
                    var groupIds = new HashSet<string>(group.Select(v => v.id));
                    var missingGroups = bindings.Where(v => groupIds.Contains(v.id) && v.status != "resolved").GroupBy(v => v.state).ToArray();
                    var stateCandidates = missingGroups.ToDictionary(v => v.Key, v => new Dictionary<string, string>());
                    foreach (var controller in bundle.LoadAllAssets<RuntimeAnimatorController>())
                    {
                        if (!missingGroups.Any()) break;
                        animator.runtimeAnimatorController = controller; animator.Rebind();
                        foreach (var missing in missingGroups)
                        {
                            // Synchronized layers cannot independently enter a state. Probe the
                            // base layer only; original explicit controllers are checked above.
                            for (int layer = p.I("math.zero"); layer < Mathf.Min(animator.layerCount, p.I("math.one")); layer += p.I("math.one"))
                            {
                                var hash = Animator.StringToHash(animator.GetLayerName(layer) + "." + missing.Key);
                                if (!animator.HasState(layer, hash)) { hash = Animator.StringToHash(missing.Key); if (!animator.HasState(layer, hash)) continue; }
                                animator.Play(hash, layer, p.N("math.zero")); animator.Update(p.N("math.zero"));
                                var active = animator.GetCurrentAnimatorClipInfo(layer).Where(v => v.clip != null && v.weight > p.N("render.epsilon")).Select(v => v.clip).Distinct().ToArray();
                                if (active.Length == p.I("math.one") && nativeIds.TryGetValue(active.Single(), out var id) && report.clips.Single(v => v.id == id).status == "converted") stateCandidates[missing.Key][id] = controller.name;
                            }
                        }
                    }
                    foreach (var missing in missingGroups)
                    {
                        var candidates = stateCandidates[missing.Key];
                        if (candidates.Count != p.I("math.one")) continue;
                        foreach (var entry in missing)
                        {
                            entry.clip = candidates.Keys.Single(); entry.status = "resolved";
                            entry.error = "Exact native state verified in sibling source controller " + candidates.Values.Single();
                        }
                    }
                }
                finally { animator.runtimeAnimatorController = null; bundle.Unload(true); }
            }
            // Stale/debug table rows may name an obsolete controller. Reuse only an exact state
            // already resolved natively to one unique clip by another original table row.
            foreach (var entry in bindings.Where(v => v.status != "resolved").ToArray())
            {
                var matches = bindings.Where(v => v.status == "resolved" && v.state == entry.state).ToArray();
                var sourceBundle = input.actions.Single(v => v.id == entry.id).bundle;
                var local = matches.Where(v => report.clips.Single(c => c.id == v.clip).sourceBundle == sourceBundle).ToArray();
                if (local.Any()) matches = local;
                var ids = matches.Select(v => v.clip).Distinct().ToArray();
                if (ids.Length != p.I("math.one")) continue;
                entry.clip = ids.Single(); entry.status = "resolved";
                entry.error = "Original table controller mismatch; exact native state verified by action " + matches.First().id;
            }
            report.bindings = bindings.ToArray(); report.packedRotationError = p.N("math.zero"); report.packedPositionError = p.N("math.zero");
            File.WriteAllText(path, JsonCodec.ToJson(report, true));
            foreach (var group in report.clips.Where(v => v.status == "converted").GroupBy(v => v.bundle))
            {
                var packed = AssetBundle.LoadFromFile(Path.Combine(input.output, group.Key));
                if (packed == null) throw new IOException("Built life-motion bundle is unavailable");
                try
                {
                    foreach (var entry in group)
                    {
                        var clip = packed.LoadAsset<AnimationClip>(entry.asset);
                        if (clip == null || Mathf.Abs(clip.length - entry.duration) > p.N("animation.lengthTolerance")) throw new InvalidDataException("Packed animation failed native reread: " + entry.sourceName + " expected=" + entry.duration + " actual=" + (clip == null ? "missing" : clip.length.ToString()));
                        var expected = AssetDatabase.LoadAssetAtPath<AnimationClip>(entry.asset);
                        if (expected == null) throw new InvalidDataException("Baked comparison clip missing: " + entry.sourceName);
                        foreach (var phase in new[] { p.N("math.zero"), p.N("math.half"), p.N("math.one") })
                        {
                            Restore(targetRest); expected.SampleAnimation(target, phase * entry.duration);
                            var rotations = targetNames.ToDictionary(v => v, v => targetRest[v].node.localRotation);
                            var positions = targetNames.ToDictionary(v => v, v => targetRest[v].node.localPosition);
                            Restore(targetRest); clip.SampleAnimation(target, phase * entry.duration);
                            var rotationError = targetNames.Max(v => RotationError(rotations[v], targetRest[v].node.localRotation, p));
                            var positionError = targetNames.Max(v => Vector3.Distance(positions[v], targetRest[v].node.localPosition));
                            entry.packedRotationError = Mathf.Max(entry.packedRotationError, rotationError); entry.packedPositionError = Mathf.Max(entry.packedPositionError, positionError);
                            report.packedRotationError = Mathf.Max(report.packedRotationError, rotationError); report.packedPositionError = Mathf.Max(report.packedPositionError, positionError);
                        }
                    }
                }
                finally { packed.Unload(true); }
            }
            File.WriteAllText(path, JsonCodec.ToJson(report, true));
            if (report.packedRotationError > p.N("animation.packedAngleTolerance") || report.packedPositionError > p.N("animation.lengthTolerance")) throw new InvalidDataException("Packed animation pose mismatch; maximum rotation=" + report.packedRotationError + " position=" + report.packedPositionError);
            report.packedVerified = true; File.WriteAllText(path, JsonCodec.ToJson(report, true));
            Debug.Log("AI_LIFE_ANIMATION_BIND_PASS resolved=" + bindings.Count(v => v.status == "resolved") + " total=" + bindings.Count);
        }
        finally { UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(target); }
    }
    private static float RotationError(Quaternion a, Quaternion b, Parameters p)
    {
        var dot = (double)a.x * b.x + (double)a.y * b.y + (double)a.z * b.z + (double)a.w * b.w;
        var norm = Math.Sqrt(((double)a.x * a.x + (double)a.y * a.y + (double)a.z * a.z + (double)a.w * a.w)
            * ((double)b.x * b.x + (double)b.y * b.y + (double)b.z * b.z + (double)b.w * b.w));
        if (norm <= default(double)) return float.PositiveInfinity;
        var angle = Math.Acos(Math.Min(Math.Sign(norm), Math.Abs(dot) / norm));
        return (float)((angle + angle) * p.N("math.degreesHalfTurn") / p.N("math.pi"));
    }
    private static string LocalId(UnityEngine.Object value)
    {
        var method = typeof(Unsupported).GetMethod("GetLocalIdentifierInFileForPersistentObject", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (method == null) return null;
        try { return Convert.ToString(method.Invoke(null, new object[] { value }), System.Globalization.CultureInfo.InvariantCulture); }
        catch { return null; }
    }
    private static AnimationClip SelectOriginal(AnimationClip[] candidates, string id)
    {
        if (candidates.Length == Math.Sign(candidates.Length)) return candidates.Single();
        var selected = candidates.Where(v => LocalId(v) == id.Split('|').Last()).ToArray();
        return selected.Length == Math.Sign(selected.Length) && selected.Any() ? selected.Single() : null;
    }
    private static bool CheckExisting(AnimationClip original, AnimationClip motion, Dictionary<string, Rest> src, Dictionary<string, Rest> dst, Pair[] pairs, Parameters p, out float error, out int moved)
    {
        error = p.N("math.zero"); var bones = new HashSet<string>();
        foreach (var phase in new[] { p.N("math.zero"), p.N("math.half"), p.N("math.one") })
        {
            Restore(src); Restore(dst); original.SampleAnimation(src.Values.First().node.root.gameObject, original.length * phase); motion.SampleAnimation(dst.Values.First().node.root.gameObject, original.length * phase);
            foreach (var pair in pairs)
            {
                var s = src[pair.source]; var t = dst[pair.target];
                var expected = pair.basis * (s.node.localRotation * Quaternion.Inverse(s.local)) * Quaternion.Inverse(pair.basis) * t.local;
                error = Mathf.Max(error, RotationError(t.node.localRotation, expected, p));
                if (RotationError(expected, t.local, p) > p.N("animation.angleTolerance")) bones.Add(pair.target);
            }
        }
        moved = bones.Count;
        return moved > p.I("math.zero") && error <= p.N("animation.angleTolerance") && Mathf.Abs(motion.length - original.length) <= p.N("animation.lengthTolerance");
    }
    private static void Restore(Dictionary<string, Rest> bones) { foreach (var bone in bones.Values) { bone.node.localRotation = bone.local; bone.node.localPosition = bone.position; } }
    private static void Write(Dictionary<string, List<Keyframe>[]> curves, string key, float time, float[] values)
    {
        if (!curves.TryGetValue(key, out var channels)) { channels = values.Select(_ => new List<Keyframe>()).ToArray(); curves.Add(key, channels); }
        foreach (var pair in values.Select((value, index) => new { value, index })) channels[pair.index].Add(new Keyframe(time, pair.value));
    }
}
