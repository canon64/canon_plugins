using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Extends the existing AiLifeAnimationBuild rest/basis conversion to explicit
// H controller states. This is an asset build entrypoint, never a preview/player.
public static class AiHAnimationBuild
{
    private static Func<string, int> I;
    private static Func<string, float> N;
    [Serializable] public sealed class PairInput { public string source, target; }
    [Serializable] public sealed class CueInput { public int id; public float phase; }
    [Serializable] public sealed class PolynomialKey { public float time, a, b, c, value; }
    [Serializable] public sealed class SourceScalar { public string kind; public float begin, rate, value; public float[] values; public PolynomialKey[] keys; }
    [Serializable] public sealed class PositionTrack { public string name; public int bindingScalarIndex; public SourceScalar x, y, z; }
    [Serializable] public sealed class TaskInput { public string key, bundle, controller, state, clipAsset, conditionHash, sourceId; public CueInput[] cues; public float sourceDuration, sourceStartTime; public int sourceBindings; public PositionTrack[] positionTracks; }
    [Serializable] public sealed class Input
    {
        public string output, generated, sourceSkeleton, targetSkeleton, conversionHash;
        public bool exactConstantCurves, batchedCurveBinding, batchAssetEditing, fixedSampleGrid, preserveNativePositionTangents, includeNativePositionKeys, analyticNativePositions;
        public PairInput[] pairs;
        public TaskInput[] tasks;
    }
    [Serializable] public sealed class Result
    {
        public string key, bundle, asset, error, conditionHash, sourceClipName, sourcePathID, worstSourceCurve, worstRetargetRotation, worstRetargetPosition;
        public float duration, rotationError, positionError, boneLengthError, sourceRestRotationDifference, sourceRestPositionDifference, sourceRotationTravel, sourcePositionTravel, retargetPositionError, sourceCurveError, authoredKeyPositionError, authoredMidpointPositionError;
        public int samples, mappedBones, sourceBindings, authoredKeySamples, authoredMidpointSamples;
        public bool loop, packedVerified, sourceDataVerified, sourceCurveVerified;
    }
    [Serializable] public sealed class Report { public Result[] results; public string conversionHash; public bool gameVerified; }
    private static string ConversionHash;
    [Serializable] public sealed class NativeLayer { public int index; public float weight; }
    [Serializable] public sealed class NativeState { public string name; public NativeLayer[] layers; }
    [Serializable] public sealed class NativeBinding { public string key, bundle, asset, sourceName; public float duration; }
    [Serializable] public sealed class NativeRole { public string role, original, patched, controller, baseController, rawBundle; public NativeState[] states; public NativeBinding[] bindings; }
    [Serializable] public sealed class NativeInput { public string conversionInput, output, verificationId; public NativeRole[] roles; public bool diagnostic; }
    [Serializable] public sealed class NativeResult { public string role, error, avatar, worstRotation, worstPosition, sourceClips, targetClips; public CurveControl freshControl, kksControl; public int probes, layers, overrideBindings, externalOverrideBindings; public float rotationError, positionError, standaloneRotationError, standalonePositionError, playableRotationError, playablePositionError; public bool passed; }
    [Serializable] public sealed class NativeReport { public NativeResult[] roles; public bool gameVerified, diagnostic; public CurveControl[] controls; public string verificationId; }
    [Serializable] public sealed class CurveControl { public string root; public float disabled, enabled, expected; public bool passed; }
    private sealed class Probe { public string state; public float height, motion, breast, phase; public Quaternion[] rotations; public Vector3[] positions; public NativeLayer[] layers; }
    private sealed class Rest
    {
        public Transform node;
        public Vector3 localPosition, localScale;
        public Quaternion localRotation, parentRotation;
    }
    public static void Build()
    {
        SandboxBuild.VerifyAudit();
        var policy = SandboxBuild.Settings(); I = key => policy.I(key); N = key => policy.N(key);
        var input = JsonUtility.FromJson<Input>(File.ReadAllText(Environment.GetEnvironmentVariable("AI_H_ANIMATION_INPUT")));
        ConversionHash = input.conversionHash;
        Directory.CreateDirectory(input.output); Directory.CreateDirectory(input.generated);
        if (input.batchAssetEditing) AssetDatabase.Refresh();
        var results = new List<Result>();
        Action<AnimationClip, EditorCurveBinding, AnimationCurve> setCurve = AnimationUtility.SetEditorCurve; Action<AnimationClip> syncCurves = null;
        if (input.batchedCurveBinding)
        {
            var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var noSync = typeof(AnimationUtility).GetMethod("SetEditorCurveNoSync", flags); var sync = typeof(AnimationUtility).GetMethod("SyncEditorCurves", flags);
            if (noSync == null || sync == null) throw new InvalidOperationException("Pinned Unity editor has no batched curve binding API");
            setCurve = (Action<AnimationClip, EditorCurveBinding, AnimationCurve>)Delegate.CreateDelegate(typeof(Action<AnimationClip, EditorCurveBinding, AnimationCurve>), noSync);
            syncCurves = (Action<AnimationClip>)Delegate.CreateDelegate(typeof(Action<AnimationClip>), sync);
        }
        var previousPath = Path.Combine(input.output, "build-report.json");
        var previous = File.Exists(previousPath) ? JsonUtility.FromJson<Report>(File.ReadAllText(previousPath)) : null;
        var cache = previous != null && previous.conversionHash == ConversionHash ? previous.results.ToDictionary(r => r.key) : new Dictionary<string, Result>();
        AssetBundle sourceBundle = null, targetBundle = null;
        GameObject source = null, target = null;
        try
        {
            sourceBundle = AssetBundle.LoadFromFile(input.sourceSkeleton);
            source = UnityEngine.Object.Instantiate(sourceBundle.LoadAsset<GameObject>("p_cf_anim"));
            // Both games call the archive oo_base. Release its file ownership after
            // instantiation, as the established life builder already does.
            sourceBundle.Unload(false); sourceBundle = null;
            targetBundle = AssetBundle.LoadFromFile(input.targetSkeleton);
            target = UnityEngine.Object.Instantiate(targetBundle.LoadAsset<GameObject>("p_cf_body_bone"));
            var src = Bones(source); var dst = Bones(target);
            var animator = source.GetComponent<Animator>() ?? source.AddComponent<Animator>();
            animator.fireEvents = false; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var pairs = input.pairs.Where(p => src.ContainsKey(p.source) && dst.ContainsKey(p.target)).ToArray();
            float ratio = dst["cf_j_hips"].node.position.y / src["cf_J_Hips"].node.position.y;
            foreach (var group in input.tasks.GroupBy(t => t.bundle))
            {
                AssetBundle bundle = null; bool editing = false;
                try
                {
                    bundle = AssetBundle.LoadFromFile(group.Key);
                    var sourceClipLifetime = bundle.LoadAllAssets<AnimationClip>();
                    if (input.batchAssetEditing) { AssetDatabase.StartAssetEditing(); editing = true; }
                    foreach (var task in group)
                    {
                        if (cache.TryGetValue(task.key, out var cached) && cached.conditionHash == task.conditionHash && cached.packedVerified && (string.IsNullOrEmpty(task.clipAsset) || cached.sourceDataVerified && cached.sourcePathID == task.sourceId && cached.sourceClipName == task.state && cached.sourceBindings == task.sourceBindings && Mathf.Abs(cached.duration - task.sourceDuration) <= N("hMotion.sourceDurationTolerance")) && string.IsNullOrEmpty(cached.error) && AssetDatabase.LoadAssetAtPath<AnimationClip>(cached.asset) != null)
                        { results.Add(cached); continue; }
                        var result = new Result { key = task.key, conditionHash = task.conditionHash }; results.Add(result);
                        try
                        {
                            AnimationClip originalClip = null;
                            if (!string.IsNullOrEmpty(task.clipAsset)) { animator.runtimeAnimatorController = null; originalClip = bundle.LoadAsset<AnimationClip>(task.clipAsset); }
                            else animator.runtimeAnimatorController = bundle.LoadAsset<RuntimeAnimatorController>(task.controller);
                            if (originalClip == null && animator.runtimeAnimatorController == null) throw new InvalidDataException("Source controller/clip missing: " + task.controller);
                            if (animator.runtimeAnimatorController != null) animator.Rebind(); Restore(src);
                            int hash = Animator.StringToHash(task.state);
                            if (originalClip == null && !animator.HasState(I("math.zero"), hash)) throw new InvalidDataException("Source state missing: " + task.state);
                            if (originalClip == null) { animator.Play(hash, I("math.zero"), I("math.zero")); animator.Update(I("math.zero")); }
                            var info = originalClip == null ? animator.GetCurrentAnimatorStateInfo(I("math.zero")) : default(AnimatorStateInfo);
                            result.sourcePathID = task.sourceId; result.sourceBindings = task.sourceBindings;
                            result.sourceClipName = originalClip == null ? task.state : originalClip.name; result.duration = originalClip == null ? info.length : originalClip.length;
                            if (originalClip != null && (originalClip.name != task.state || task.sourceBindings <= I("math.zero") || Mathf.Abs(originalClip.length - task.sourceDuration) > N("hMotion.sourceDurationTolerance"))) throw new InvalidDataException("Native source clip identity/duration/binding evidence mismatch: " + task.clipAsset + " " + originalClip.name + " duration=" + originalClip.length + " expected=" + task.sourceDuration);
                            if (result.duration <= I("math.zero") || float.IsNaN(result.duration) || float.IsInfinity(result.duration)) throw new InvalidDataException("Source state duration invalid");
                            result.loop = originalClip == null ? info.loop : originalClip.isLooping; result.samples = Math.Max(I("math.two"), Mathf.CeilToInt(result.duration * I("animation.sampleRate")) + I("math.one")); result.mappedBones = pairs.Length;
                            var sampleTimes = Enumerable.Range(I("math.zero"), result.samples).Select(index => input.fixedSampleGrid ? Mathf.Min(index / (float)I("animation.sampleRate"), result.duration) : index / (float)(result.samples - I("math.one")) * result.duration).ToList();
                            if (input.preserveNativePositionTangents && input.includeNativePositionKeys)
                                foreach (var track in task.positionTracks ?? throw new InvalidDataException("Source position tracks missing"))
                                    foreach (var scalar in new[] { track.x, track.y, track.z })
                                    {
                                        if (scalar.kind == "stream") sampleTimes.AddRange(scalar.keys.Select(key => key.time - task.sourceStartTime));
                                        if (scalar.kind == "dense") sampleTimes.AddRange(Enumerable.Range(I("math.zero"), scalar.values.Length).Select(index => scalar.begin + index / scalar.rate - task.sourceStartTime));
                                    }
                            sampleTimes = sampleTimes.Where(time => time >= N("math.zero") && time <= result.duration).Distinct().OrderBy(time => time).ToList(); result.samples = sampleTimes.Count;
                            var clip = new AnimationClip { name = task.key, frameRate = I("animation.sampleRate") };
                            var curves = new Dictionary<string, List<Keyframe>[]>();
                            var changed = new HashSet<string>();
                            var nativePositions = task.positionTracks?.ToDictionary(track => track.name);
                            var firstRotations = new Dictionary<string, Quaternion>(); var firstPositions = new Dictionary<string, Vector3>();
                            for (int sample = I("math.zero"); sample < result.samples; ++sample)
                            {
                                float time = sampleTimes[sample], normalized = time / result.duration;
                                Restore(src);
                                if (originalClip != null) originalClip.SampleAnimation(source, time);
                                else { animator.Play(hash, I("math.zero"), normalized >= I("math.one") ? N("hMotion.sampleEndpoint") : normalized); animator.Update(I("math.zero")); }
                                foreach (var pair in pairs)
                                {
                                    var s = src[pair.source]; var t = dst[pair.target];
                                    if (input.preserveNativePositionTangents && nativePositions.TryGetValue(pair.source, out var nativePosition))
                                    {
                                        var nativeExpected = new Vector3(EvaluateSource(nativePosition.x, time + task.sourceStartTime), EvaluateSource(nativePosition.y, time + task.sourceStartTime), EvaluateSource(nativePosition.z, time + task.sourceStartTime));
                                        float sourceError = Vector3.Distance(nativeExpected, s.node.localPosition) * ratio;
                                        if (sourceError > result.sourceCurveError) { result.sourceCurveError = sourceError; result.worstSourceCurve = pair.source + " time=" + time + " decoded=" + nativeExpected + " native=" + s.node.localPosition; }
                                    }
                                    if (sample == I("math.zero")) { firstRotations[pair.source] = s.node.localRotation; firstPositions[pair.source] = s.node.localPosition; }
                                    result.sourceRestRotationDifference = Mathf.Max(result.sourceRestRotationDifference, Quaternion.Angle(s.localRotation, s.node.localRotation));
                                    result.sourceRestPositionDifference = Mathf.Max(result.sourceRestPositionDifference, Vector3.Distance(s.localPosition, s.node.localPosition));
                                    result.sourceRotationTravel = Mathf.Max(result.sourceRotationTravel, Quaternion.Angle(firstRotations[pair.source], s.node.localRotation));
                                    result.sourcePositionTravel = Mathf.Max(result.sourcePositionTravel, Vector3.Distance(firstPositions[pair.source], s.node.localPosition));
                                    // Same parent-rest basis equation as AiLifeAnimationBuild.
                                    var basis = Quaternion.Inverse(t.parentRotation) * s.parentRotation;
                                    var q = basis * (s.node.localRotation * Quaternion.Inverse(s.localRotation)) * Quaternion.Inverse(basis) * t.localRotation;
                                    var path = AnimationUtility.CalculateTransformPath(t.node, target.transform);
                                    Write(curves, path + "|m_LocalRotation", time, new[] { q.x, q.y, q.z, q.w });
                                    if (Quaternion.Angle(q, t.localRotation) > N("animation.motionTolerance")) changed.Add(path + "|m_LocalRotation");
                                    // Preserve card bone lengths/scales. Root translation and pelvis
                                    // displacement are the only authored body translations adopted.
                                    if (pair.target == "cf_j_hips" || pair.target == "cf_j_root" || pair.source.StartsWith("f_pv_") || pair.source.StartsWith("f_t_"))
                                    {
                                        var p = t.localPosition + basis * (s.node.localPosition - s.localPosition) * ratio;
                                        Write(curves, path + "|m_LocalPosition", time, new[] { p.x, p.y, p.z });
                                        if (Vector3.Distance(p, t.localPosition) > N("hMotion.positionMotionThreshold")) changed.Add(path + "|m_LocalPosition");
                                    }
                                }
                            }
                            if (input.analyticNativePositions)
                            {
                                foreach (var pair in pairs)
                                {
                                    var path = AnimationUtility.CalculateTransformPath(dst[pair.target].node, target.transform); var property = path + "|m_LocalPosition";
                                    if (!curves.ContainsKey(property) || !nativePositions.TryGetValue(pair.source, out var authored)) continue;
                                    curves.Remove(property); changed.Add(property);
                                    var basis = Quaternion.Inverse(dst[pair.target].parentRotation) * src[pair.source].parentRotation;
                                    foreach (var knot in NativePositionKnots(task, result.duration, authored))
                                    {
                                        float time = knot.time; var value = new Vector3(KnotValue(authored.x, knot, task.sourceStartTime), KnotValue(authored.y, knot, task.sourceStartTime), KnotValue(authored.z, knot, task.sourceStartTime));
                                        var position = dst[pair.target].localPosition + basis * (value - src[pair.source].localPosition) * ratio;
                                        Write(curves, property, time, new[] { position.x, position.y, position.z });
                                    }
                                }
                            }
                            if (input.preserveNativePositionTangents)
                            {
                                if (originalClip == null || task.positionTracks == null) throw new InvalidDataException("Native position derivative provenance missing");
                                foreach (var pair in pairs)
                                {
                                    var path = AnimationUtility.CalculateTransformPath(dst[pair.target].node, target.transform);
                                    if (!curves.TryGetValue(path + "|m_LocalPosition", out var tracks)) continue;
                                    var sourceTrack = task.positionTracks.SingleOrDefault(t => t.name == pair.source);
                                    if (sourceTrack == null) { if (tracks.Any(t => t.Any(k => !k.value.Equals(t.First().value)))) throw new InvalidDataException("Moving source position has no native derivative binding: " + pair.source); continue; }
                                    var basis = Quaternion.Inverse(dst[pair.target].parentRotation) * src[pair.source].parentRotation;
                                    for (int keyIndex = I("math.zero"); keyIndex < tracks[I("math.zero")].Count; ++keyIndex)
                                    {
                                        float localTime = tracks[I("math.zero")][keyIndex].time;
                                        var knot = NativePositionKnots(task, result.duration, sourceTrack).SingleOrDefault(k => k.time.Equals(localTime));
                                        var incoming = basis * new Vector3(KnotDerivative(sourceTrack.x, knot, localTime, task.sourceStartTime, true), KnotDerivative(sourceTrack.y, knot, localTime, task.sourceStartTime, true), KnotDerivative(sourceTrack.z, knot, localTime, task.sourceStartTime, true)) * ratio;
                                        var outgoing = basis * new Vector3(KnotDerivative(sourceTrack.x, knot, localTime, task.sourceStartTime, false), KnotDerivative(sourceTrack.y, knot, localTime, task.sourceStartTime, false), KnotDerivative(sourceTrack.z, knot, localTime, task.sourceStartTime, false)) * ratio;
                                        for (int axis = I("math.zero"); axis < tracks.Length; ++axis) { var key = tracks[axis][keyIndex]; key.inTangent = incoming[axis]; key.outTangent = outgoing[axis]; tracks[axis][keyIndex] = key; }
                                    }
                                }
                            }
                            foreach (var curve in curves)
                            {
                                var split = curve.Key.Split('|');
                                for (int i = I("math.zero"); i < curve.Value.Length; ++i)
                                {
                                    var track = curve.Value[i];
                                    bool exactConstant = track.All(k => k.value.Equals(track.First().value) && k.inTangent.Equals(track.First().inTangent) && k.outTangent.Equals(track.First().outTangent)) && track.First().inTangent.Equals(N("math.zero")) && track.First().outTangent.Equals(N("math.zero"));
                                    var keys = (input.exactConstantCurves ? !exactConstant : changed.Contains(curve.Key)) ? track.ToArray() : new[] { track.First(), track.Last() };
                                    setCurve(clip, EditorCurveBinding.FloatCurve(split[I("math.zero")], typeof(Transform), split[I("math.one")] + "." + "xyzw"[i]), new AnimationCurve(keys));
                                }
                            }
                            syncCurves?.Invoke(clip);
                            clip.EnsureQuaternionContinuity(); var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = result.loop; AnimationUtility.SetAnimationClipSettings(clip, settings);
                            var events = new List<AnimationEvent> { new AnimationEvent { time = Mathf.Max(I("math.zero"), result.duration - N("math.one") / N("hMotion.eventSampleRate")), functionName = "OnAiHMotionCycle" } };
                            if (task.cues != null) foreach (var cue in task.cues) events.Add(new AnimationEvent { time = Mathf.Clamp01(cue.phase) * result.duration, functionName = "OnAiHSeCue", intParameter = cue.id });
                            AnimationUtility.SetAnimationEvents(clip, events.OrderBy(e => e.time).ToArray());
                            var asset = input.generated + "/" + task.key + ".anim";
                            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(asset) != null) AssetDatabase.DeleteAsset(asset);
                            AssetDatabase.CreateAsset(clip, asset); result.asset = asset;
                            // Compare the generated animation against independent formula evaluation.
                            foreach (float normalized in new[] { N("math.zero"), N("hMotion.validationQuarter"), N("math.half"), N("hMotion.validationThreeQuarter"), N("hMotion.sampleEndpoint") })
                            {
                                Restore(src); Restore(dst);
                                if (originalClip != null) originalClip.SampleAnimation(source, normalized * result.duration);
                                else { animator.Play(hash, I("math.zero"), normalized); animator.Update(I("math.zero")); }
                                clip.SampleAnimation(target, normalized * result.duration);
                                foreach (var pair in pairs)
                                {
                                    var s = src[pair.source]; var t = dst[pair.target]; var basis = Quaternion.Inverse(t.parentRotation) * s.parentRotation;
                                    var expected = basis * (s.node.localRotation * Quaternion.Inverse(s.localRotation)) * Quaternion.Inverse(basis) * t.localRotation;
                                    float retargetAngle = Quaternion.Angle(expected, t.node.localRotation);
                                    if (retargetAngle > result.rotationError) { result.rotationError = retargetAngle; result.worstRetargetRotation = pair.source + "->" + pair.target + " phase=" + normalized + " expected=" + expected.ToString("G9") + " actual=" + t.node.localRotation.ToString("G9"); }
                                    if (pair.target == "cf_j_hips" || pair.target == "cf_j_root" || pair.source.StartsWith("f_pv_") || pair.source.StartsWith("f_t_"))
                                    {
                                        var expectedPosition = t.localPosition + basis * (s.node.localPosition - s.localPosition) * ratio; float retargetDistance = Vector3.Distance(expectedPosition, t.node.localPosition);
                                        if (retargetDistance > result.retargetPositionError) { result.retargetPositionError = retargetDistance; result.worstRetargetPosition = pair.source + "->" + pair.target + " phase=" + normalized + " expected=" + expectedPosition.ToString("G9") + " actual=" + t.node.localPosition.ToString("G9"); }
                                    }
                                    if (pair.target.StartsWith("cf_j_") && pair.target != "cf_j_hips" && pair.target != "cf_j_root") result.boneLengthError = Mathf.Max(result.boneLengthError, Vector3.Distance(t.node.localPosition, t.localPosition));
                                }
                            }
                            if (input.analyticNativePositions)
                            {
                                var authoredTimes = AuthoredPositionTimes(task, result.duration, null); result.authoredKeySamples = authoredTimes.Count;
                                var midpointTimes = authoredTimes.Zip(authoredTimes.Skip(I("math.one")), (a, b) => (a + b) * N("math.half")).ToList(); result.authoredMidpointSamples = midpointTimes.Count;
                                foreach (bool midpoint in new[] { false, true })
                                foreach (float time in midpoint ? midpointTimes : authoredTimes)
                                {
                                    Restore(src); Restore(dst); originalClip.SampleAnimation(source, time); clip.SampleAnimation(target, time);
                                    foreach (var pair in pairs)
                                    {
                                        var sourceBone = src[pair.source]; var targetBone = dst[pair.target];
                                        if (pair.target != "cf_j_hips" && pair.target != "cf_j_root" && !pair.source.StartsWith("f_pv_") && !pair.source.StartsWith("f_t_")) continue;
                                        var basis = Quaternion.Inverse(targetBone.parentRotation) * sourceBone.parentRotation;
                                        if (nativePositions.TryGetValue(pair.source, out var authoredCurve))
                                        {
                                            float sourceTime = time + task.sourceStartTime; var decoded = new Vector3(EvaluateSource(authoredCurve.x, sourceTime), EvaluateSource(authoredCurve.y, sourceTime), EvaluateSource(authoredCurve.z, sourceTime));
                                            float sourceDistance = Vector3.Distance(decoded, sourceBone.node.localPosition) * ratio;
                                            if (sourceDistance > result.sourceCurveError) { result.sourceCurveError = sourceDistance; result.worstSourceCurve = pair.source + " time=" + time + " midpoint=" + midpoint + " decoded=" + decoded.ToString("G9") + " native=" + sourceBone.node.localPosition.ToString("G9"); }
                                        }
                                        var expectedPosition = targetBone.localPosition + basis * (sourceBone.node.localPosition - sourceBone.localPosition) * ratio;
                                        float distance = Vector3.Distance(expectedPosition, targetBone.node.localPosition);
                                        if (midpoint) result.authoredMidpointPositionError = Mathf.Max(result.authoredMidpointPositionError, distance); else result.authoredKeyPositionError = Mathf.Max(result.authoredKeyPositionError, distance);
                                        if (distance > result.retargetPositionError) { result.retargetPositionError = distance; result.worstRetargetPosition = pair.source + "->" + pair.target + " time=" + time.ToString("G9") + " midpoint=" + midpoint + " expected=" + expectedPosition.ToString("G9") + " actual=" + targetBone.node.localPosition.ToString("G9"); }
                                    }
                                }
                            }
                            result.sourceDataVerified = originalClip != null; result.sourceCurveVerified = input.preserveNativePositionTangents && result.sourceCurveError <= N("hMotion.positionTolerance");
                            if (result.sourceCurveError > N("hMotion.positionTolerance")) throw new InvalidDataException("Native source scalar binding mismatch: " + result.worstSourceCurve);
                            if (result.retargetPositionError > N("hMotion.positionTolerance") || result.rotationError > I("math.two") || result.boneLengthError > N("hMotion.positionTolerance")) throw new InvalidDataException("Retarget validation tolerance exceeded");
                        }
                        catch (Exception error) { result.error = error.GetBaseException().Message; }
                    }
                    GC.KeepAlive(sourceClipLifetime);
                }
                finally { if (editing) AssetDatabase.StopAssetEditing(); animator.runtimeAnimatorController = null; if (bundle != null) bundle.Unload(true); }
                Save(input.output, results);
                Debug.Log("AI_H_CONTROLLER_GROUP_DONE " + group.Key);
            }
            AssetDatabase.SaveAssets();
            foreach (var chunk in results.Where(r => string.IsNullOrEmpty(r.error)).Select((r, i) => new { r, group = i / I("hMotion.bundleClipLimit") }).GroupBy(v => v.group))
            {
                string name = "ai-h-" + chunk.Key.ToString("D3") + ".bundle";
                var entries = chunk.Select(v => v.r).ToArray();
                BuildPipeline.BuildAssetBundles(input.output, new[] { new AssetBundleBuild { assetBundleName = name, assetNames = entries.Select(r => r.asset).ToArray() } }, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
                var packed = AssetBundle.LoadFromFile(Path.Combine(input.output, name));
                try
                {
                    foreach (var result in entries)
                    {
                        var clip = packed.LoadAsset<AnimationClip>(result.asset);
                        if (clip == null) { result.error = "Packed clip missing"; continue; }
                        var generated = AssetDatabase.LoadAssetAtPath<AnimationClip>(result.asset);
                        foreach (float n in new[] { N("math.zero"), N("math.half"), N("hMotion.sampleEndpoint") })
                        {
                            Restore(dst); generated.SampleAnimation(target, result.duration * n);
                            var rotations = dst.ToDictionary(p => p.Key, p => p.Value.node.localRotation);
                            var positions = dst.ToDictionary(p => p.Key, p => p.Value.node.localPosition);
                            Restore(dst); clip.SampleAnimation(target, result.duration * n);
                            foreach (var bone in dst)
                            {
                                result.rotationError = Mathf.Max(result.rotationError, Quaternion.Angle(rotations[bone.Key], bone.Value.node.localRotation));
                                result.positionError = Mathf.Max(result.positionError, Vector3.Distance(positions[bone.Key], bone.Value.node.localPosition));
                            }
                        }
                        result.bundle = name; result.packedVerified = result.rotationError <= I("math.two") && result.positionError <= N("hMotion.positionTolerance");
                        if (!result.packedVerified) result.error = "Packed pose tolerance exceeded";
                    }
                }
                finally { if (packed != null) packed.Unload(true); }
                Save(input.output, results);
            }
            Debug.Log("AI_H_ANIMATION_BUILD_DONE total=" + results.Count + " verified=" + results.Count(r => r.packedVerified));
        }
        finally
        {
            Save(input.output, results);
            if (source != null) UnityEngine.Object.DestroyImmediate(source); if (target != null) UnityEngine.Object.DestroyImmediate(target);
            if (sourceBundle != null) sourceBundle.Unload(true); if (targetBundle != null) targetBundle.Unload(true);
        }
    }
    private static void Save(string output, List<Result> results) => File.WriteAllText(Path.Combine(output, "build-report.json"), JsonUtility.ToJson(new Report { results = results.ToArray(), conversionHash = ConversionHash, gameVerified = false }, true));
    private static Dictionary<string, Rest> Bones(GameObject root) => root.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => new Rest { node = g.First(), localPosition = g.First().localPosition, localRotation = g.First().localRotation, localScale = g.First().localScale, parentRotation = g.First().parent == null ? Quaternion.identity : g.First().parent.rotation });
    private static void Restore(Dictionary<string, Rest> rest) { foreach (var b in rest.Values) { b.node.localPosition = b.localPosition; b.node.localRotation = b.localRotation; b.node.localScale = b.localScale; } }
    private static void Write(Dictionary<string, List<Keyframe>[]> curves, string key, float time, float[] values)
    {
        if (values.Any(v => float.IsNaN(v) || float.IsInfinity(v))) throw new InvalidDataException("Nonfinite motion sample");
        if (!curves.TryGetValue(key, out var tracks)) curves.Add(key, tracks = values.Select(v => new List<Keyframe>()).ToArray());
        for (int i = I("math.zero"); i < values.Length; ++i) tracks[i].Add(new Keyframe(time, values[i]));
    }
    // Native controller, mask, override, parameter and authored-KKS-Avatar QA.
    // It evaluates assets only; a live card's deformation/contact remains separate.
    [Serializable] public sealed class SourceCurveInfo { public string sourceId, name; public float nativeDuration, storedDuration; public int bindings, editorCurves, keys; }
    [Serializable] public sealed class SourceCurveReport { public SourceCurveInfo[] clips; }
    public static void InspectSourceCurves()
    {
        SandboxBuild.VerifyAudit(); var policy = SandboxBuild.Settings(); I = key => policy.I(key); N = key => policy.N(key);
        var input = JsonUtility.FromJson<Input>(File.ReadAllText(Environment.GetEnvironmentVariable("AI_H_ANIMATION_INPUT"))); var rows = new List<SourceCurveInfo>();
        foreach (var group in input.tasks.GroupBy(t => t.bundle))
        {
            var bundle = AssetBundle.LoadFromFile(group.Key);
            try
            {
                var lifetime = bundle.LoadAllAssets<AnimationClip>();
                foreach (var task in group)
                {
                    var clip = bundle.LoadAsset<AnimationClip>(task.clipAsset); var bindings = AnimationUtility.GetCurveBindings(clip);
                    var row = new SourceCurveInfo { sourceId = task.sourceId, name = clip.name, nativeDuration = clip.length, storedDuration = task.sourceDuration, bindings = bindings.Length };
                    foreach (var binding in bindings) { var curve = AnimationUtility.GetEditorCurve(clip, binding); if (curve != null) { ++row.editorCurves; row.keys += curve.length; } }
                    rows.Add(row);
                }
                GC.KeepAlive(lifetime);
            }
            finally { bundle.Unload(true); }
        }
        File.WriteAllText(Path.Combine(input.output, "native-source-editor-curves.json"), JsonUtility.ToJson(new SourceCurveReport { clips = rows.ToArray() }, true));
    }

    public static void VerifyControls()
    {
        SandboxBuild.VerifyAudit(); var policy = SandboxBuild.Settings(); I = key => policy.I(key); N = key => policy.N(key);
        var input = JsonUtility.FromJson<Input>(File.ReadAllText(Environment.GetEnvironmentVariable("AI_H_ANIMATION_INPUT")));
        var report = new List<CurveControl>(); var fresh = new GameObject("AiH one bone control"); var child = new GameObject("ControlBone"); child.transform.SetParent(fresh.transform, false);
        AssetBundle bundle = null; GameObject target = null;
        try
        {
            report.Add(KnownCurveControl(fresh, child.transform)); bundle = AssetBundle.LoadFromFile(input.targetSkeleton); target = UnityEngine.Object.Instantiate(bundle.LoadAsset<GameObject>("p_cf_body_bone"));
            report.Add(KnownCurveControl(target, Bones(target)["cf_j_root"].node));
            File.WriteAllText(Path.Combine(input.output, "known-curve-control.json"), JsonUtility.ToJson(new NativeReport { controls = report.ToArray(), gameVerified = false, diagnostic = true }, true));
            if (report.Any(r => !r.passed)) throw new InvalidDataException("Known curve controller control failed");
        }
        finally { UnityEngine.Object.DestroyImmediate(fresh); if (target != null) UnityEngine.Object.DestroyImmediate(target); if (bundle != null) bundle.Unload(true); }
    }

    public static void VerifyNative()
    {
        SandboxBuild.VerifyAudit(); var policy = SandboxBuild.Settings(); I = key => policy.I(key); N = key => policy.N(key);
        var input = JsonUtility.FromJson<NativeInput>(File.ReadAllText(Environment.GetEnvironmentVariable("AI_H_NATIVE_INPUT")));
        var conversion = JsonUtility.FromJson<Input>(File.ReadAllText(input.conversionInput));
        var report = new List<NativeResult>();
        foreach (var role in input.roles)
        {
            var result = new NativeResult { role = role.role }; report.Add(result);
            AssetBundle skeleton = null, original = null, patched = null;
            GameObject source = null, target = null;
            AnimatorOverrideController sourceOverride = null, targetOverride = null;
            Avatar targetAvatar = null;
            var convertedBundles = new Dictionary<string, AssetBundle>();
            try
            {
                skeleton = AssetBundle.LoadFromFile(conversion.sourceSkeleton);
                source = UnityEngine.Object.Instantiate(skeleton.LoadAsset<GameObject>("p_cf_anim")); skeleton.Unload(false); skeleton = null;
                var sourceAnimator = source.GetComponent<Animator>() ?? source.AddComponent<Animator>();
                sourceAnimator.fireEvents = false; sourceAnimator.applyRootMotion = false; sourceAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var src = Bones(source);
                skeleton = AssetBundle.LoadFromFile(conversion.targetSkeleton);
                target = UnityEngine.Object.Instantiate(skeleton.LoadAsset<GameObject>("p_cf_body_bone"));
                var targetAnimator = target.GetComponent<Animator>() ?? target.AddComponent<Animator>();
                // The mesh Avatar has cf_01-prefixed paths and no IK-helper nodes.
                // Native H owns a Generic Avatar of the actual body-bone hierarchy,
                // retaining all of the KKS transforms and the original state graph.
                targetAvatar = AvatarBuilder.BuildGenericAvatar(target, string.Empty);
                if (targetAvatar == null || !targetAvatar.isValid) throw new InvalidDataException("KKS body hierarchy Generic Avatar invalid");
                targetAvatar.name = "AiH KKS body hierarchy"; targetAnimator.avatar = targetAvatar;
                if (input.diagnostic) { var avatarPath = "Assets/AiHBinding-" + role.role + ".asset"; AssetDatabase.DeleteAsset(avatarPath); AssetDatabase.CreateAsset(UnityEngine.Object.Instantiate(targetAvatar), avatarPath); AssetDatabase.SaveAssets(); }
                result.avatar = targetAnimator.avatar.name;
                targetAnimator.fireEvents = false; targetAnimator.applyRootMotion = false; targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var dst = Bones(target);
                if (input.diagnostic)
                {
                    var fresh = new GameObject("AiH one bone control"); var child = new GameObject("ControlBone"); child.transform.SetParent(fresh.transform, false);
                    try { result.freshControl = KnownCurveControl(fresh, child.transform); result.kksControl = KnownCurveControl(target, dst["cf_j_root"].node); }
                    finally { UnityEngine.Object.DestroyImmediate(fresh); }
                }
                var pairs = conversion.pairs.Where(p => src.ContainsKey(p.source) && dst.ContainsKey(p.target)).ToArray();
                float ratio = dst["cf_j_hips"].node.position.y / src["cf_J_Hips"].node.position.y;
                original = AssetBundle.LoadFromFile(role.original);
                sourceOverride = Override(original, role.baseController, role.controller, out var sourceBindings);
                sourceAnimator.runtimeAnimatorController = sourceOverride; sourceAnimator.Rebind(); result.layers = sourceAnimator.layerCount; result.overrideBindings = sourceBindings;
                var probes = new List<Probe>();
                foreach (var state in role.states)
                foreach (float height in input.diagnostic ? new[] { N("math.zero") } : new[] { N("math.zero"), N("math.half"), N("math.one") })
                foreach (float motion in input.diagnostic ? new[] { N("math.zero") } : new[] { N("math.zero"), N("math.half"), N("math.one") })
                foreach (float breast in input.diagnostic ? new[] { N("math.zero") } : new[] { N("math.zero"), N("math.half"), N("math.one") })
                foreach (float phase in input.diagnostic ? new[] { N("math.zero") } : new[] { N("math.zero"), N("hMotion.validationQuarter"), N("math.half"), N("hMotion.validationThreeQuarter"), N("hMotion.sampleEndpoint") })
                {
                    Restore(src); Drive(sourceAnimator, state.name, state.layers, height, motion, breast, phase);
                    if (result.sourceClips == null) result.sourceClips = string.Join(";", sourceAnimator.GetCurrentAnimatorClipInfo(I("math.zero")).Select(c => c.clip.name + ":" + c.weight));
                    var probe = new Probe { state = state.name, layers = state.layers, height = height, motion = motion, breast = breast, phase = phase, rotations = new Quaternion[pairs.Length], positions = new Vector3[pairs.Length] };
                    for (int bone = I("math.zero"); bone < pairs.Length; ++bone)
                    {
                        var pair = pairs[bone]; var s = src[pair.source]; var t = dst[pair.target];
                        var basis = Quaternion.Inverse(t.parentRotation) * s.parentRotation;
                        probe.rotations[bone] = basis * (s.node.localRotation * Quaternion.Inverse(s.localRotation)) * Quaternion.Inverse(basis) * t.localRotation;
                        probe.positions[bone] = pair.target == "cf_j_hips" || pair.target == "cf_j_root" || pair.source.StartsWith("f_pv_") || pair.source.StartsWith("f_t_") ? t.localPosition + basis * (s.node.localPosition - s.localPosition) * ratio : t.localPosition;
                    }
                    probes.Add(probe);
                }
                if (input.diagnostic)
                {
                    Drive(sourceAnimator, "Idle", null, N("math.zero"), N("math.zero"), N("math.zero"), N("math.zero")); var active = sourceAnimator.GetCurrentAnimatorClipInfo(I("math.zero"))[I("math.zero")].clip;
                    result.avatar += " sourceGraphRoot=" + src["cf_J_Root"].node.localRotation + " activeLength=" + active.length; Restore(src); active.SampleAnimation(source, N("math.zero")); result.avatar += " sourceDirectRoot=" + src["cf_J_Root"].node.localRotation;
                }
                sourceAnimator.runtimeAnimatorController = null; UnityEngine.Object.DestroyImmediate(sourceOverride); sourceOverride = null; original.Unload(true); original = null;
                if (input.diagnostic)
                {
                    var raw = AssetBundle.LoadFromFile(role.rawBundle);
                    try { var rawTask = conversion.tasks.First(t => t.bundle == role.rawBundle && t.state == "S_Idle"); var rawClip = raw.LoadAsset<AnimationClip>(rawTask.clipAsset); Restore(src); rawClip.SampleAnimation(source, N("math.zero")); result.avatar += " exposed=" + rawClip.name + ":" + rawClip.length + " exposedRoot=" + src["cf_J_Root"].node.localRotation; }
                    finally { raw.Unload(true); }
                }
                patched = AssetBundle.LoadFromFile(role.patched);
                targetOverride = OverrideExternal(patched, role.baseController, role.bindings, convertedBundles, out var targetBindings);
                result.externalOverrideBindings = targetBindings;
                targetAnimator.runtimeAnimatorController = targetOverride; targetAnimator.Rebind();
                result.avatar += " enabled=" + targetAnimator.enabled + " active=" + target.activeInHierarchy + " initialized=" + targetAnimator.isInitialized + " human=" + targetAnimator.isHuman + " hierarchy=" + targetAnimator.hasTransformHierarchy + " bound=" + targetAnimator.hasBoundPlayables;
                if (targetAnimator.layerCount != result.layers) throw new InvalidDataException("Native layer count changed");
                if (input.diagnostic)
                {
                    var binding = role.bindings.First(b => b.sourceName == "S_Idle");
                    var single = convertedBundles[binding.bundle].LoadAsset<AnimationClip>(binding.asset);
                    Restore(dst); single.SampleAnimation(target, N("math.zero")); result.avatar += " sampledRoot=" + dst["cf_j_root"].node.localRotation;
                    var expectedRotations = dst.ToDictionary(b => b.Key, b => b.Value.node.localRotation);
                    var expectedPositions = dst.ToDictionary(b => b.Key, b => b.Value.node.localPosition);
                    Restore(dst); var graph = PlayableGraph.Create("NativeClipBindingDiagnostic");
                    try
                    {
                        var player = AnimationClipPlayable.Create(graph, single); var output = AnimationPlayableOutput.Create(graph, "KKS body", targetAnimator); output.SetSourcePlayable(player); player.SetTime(N("math.zero")); graph.Evaluate(N("math.zero"));
                        foreach (var bone in dst)
                        {
                            result.standaloneRotationError = Mathf.Max(result.standaloneRotationError, Quaternion.Angle(expectedRotations[bone.Key], bone.Value.node.localRotation));
                            result.standalonePositionError = Mathf.Max(result.standalonePositionError, Vector3.Distance(expectedPositions[bone.Key], bone.Value.node.localPosition));
                        }
                    }
                    finally { graph.Destroy(); targetAnimator.Rebind(); }
                    var diagnosticController = new UnityEditor.Animations.AnimatorController();
                    try
                    {
                        diagnosticController.AddLayer("Binding diagnostic"); diagnosticController.AddMotion(single); targetAnimator.avatar = null; targetAnimator.runtimeAnimatorController = diagnosticController; targetAnimator.Rebind(); Restore(dst); targetAnimator.Play(single.name); targetAnimator.Update(N("hMotion.validationQuarter"));
                        result.avatar += " minimalRoot=" + dst["cf_j_root"].node.localRotation;
                    }
                    finally { targetAnimator.avatar = targetAvatar; targetAnimator.runtimeAnimatorController = targetOverride; targetAnimator.Rebind(); UnityEngine.Object.DestroyImmediate(diagnosticController); }
                    var controllerGraph = PlayableGraph.Create("Native controller binding diagnostic");
                    try
                    {
                        var controllerPlayer = AnimatorControllerPlayable.Create(controllerGraph, targetOverride);
                        var controllerOutput = AnimationPlayableOutput.Create(controllerGraph, "KKS native controller", targetAnimator); controllerOutput.SetSourcePlayable(controllerPlayer); controllerOutput.SetWeight(N("math.one")); controllerGraph.Play();
                        controllerPlayer.SetLayerWeight(I("math.zero"), N("math.one")); controllerPlayer.SetFloat("height", N("math.zero")); controllerPlayer.SetFloat("motion", N("math.zero")); controllerPlayer.SetFloat("breast", N("math.zero")); controllerPlayer.SetFloat("speed", N("math.one")); controllerPlayer.Play("Idle", I("math.zero"), N("math.zero")); Restore(dst); controllerGraph.Evaluate(N("hMotion.validationQuarter"));
                        result.avatar += " controllerPlayableRoot=" + dst["cf_j_root"].node.localRotation;
                    }
                    finally { controllerGraph.Destroy(); targetAnimator.Rebind(); }
                }
                foreach (var probe in probes)
                {
                    Restore(dst); Drive(targetAnimator, probe.state, probe.layers, probe.height, probe.motion, probe.breast, probe.phase);
                    if (result.targetClips == null) result.targetClips = string.Join(";", targetAnimator.GetCurrentAnimatorClipInfo(I("math.zero")).Select(c => c.clip.name + ":" + c.weight));
                    for (int bone = I("math.zero"); bone < pairs.Length; ++bone)
                    {
                        var t = dst[pairs[bone].target];
                        float angle = Quaternion.Angle(probe.rotations[bone], t.node.localRotation), distance = Vector3.Distance(probe.positions[bone], t.node.localPosition);
                        if (angle > result.rotationError) { result.rotationError = angle; result.worstRotation = pairs[bone].source + "->" + pairs[bone].target + " state=" + probe.state + " height=" + probe.height + " motion=" + probe.motion + " breast=" + probe.breast + " phase=" + probe.phase + " expected=" + probe.rotations[bone] + " actual=" + t.node.localRotation; }
                        if (distance > result.positionError) { result.positionError = distance; result.worstPosition = pairs[bone].source + "->" + pairs[bone].target + " state=" + probe.state + " height=" + probe.height + " motion=" + probe.motion + " breast=" + probe.breast + " phase=" + probe.phase + " expected=" + probe.positions[bone] + " actual=" + t.node.localPosition; }
                    }
                    ++result.probes;
                }
                var nativeGraph = PlayableGraph.Create("Product controller playable QA");
                try
                {
                    var native = AnimatorControllerPlayable.Create(nativeGraph, targetOverride);
                    var nativeOutput = AnimationPlayableOutput.Create(nativeGraph, "Product KKS Animator", targetAnimator); nativeOutput.SetSourcePlayable(native); nativeOutput.SetWeight(N("math.one"));
                    nativeGraph.Play();
                    foreach (var probe in probes)
                    {
                        Restore(dst); DrivePlayable(native, probe); nativeGraph.Evaluate(N("math.zero"));
                        for (int bone = I("math.zero"); bone < pairs.Length; ++bone)
                        {
                            var t = dst[pairs[bone].target]; float angle = Quaternion.Angle(probe.rotations[bone], t.node.localRotation), distance = Vector3.Distance(probe.positions[bone], t.node.localPosition);
                            result.playableRotationError = Mathf.Max(result.playableRotationError, angle); result.playablePositionError = Mathf.Max(result.playablePositionError, distance);
                        }
                    }
                }
                finally { nativeGraph.Destroy(); targetAnimator.Rebind(); }
                result.passed = result.rotationError <= N("animation.packedAngleTolerance") && result.positionError <= N("hMotion.positionTolerance") && result.playableRotationError <= N("animation.packedAngleTolerance") && result.playablePositionError <= N("hMotion.positionTolerance");
                if (!result.passed) result.error = "Native graph retarget pose tolerance exceeded";
            }
            catch (Exception error) { result.error = error.GetBaseException().Message; }
            finally
            {
                if (source != null) UnityEngine.Object.DestroyImmediate(source); if (target != null) UnityEngine.Object.DestroyImmediate(target);
                if (sourceOverride != null) UnityEngine.Object.DestroyImmediate(sourceOverride); if (targetOverride != null) UnityEngine.Object.DestroyImmediate(targetOverride);
                if (targetAvatar != null) UnityEngine.Object.DestroyImmediate(targetAvatar);
                if (original != null) original.Unload(true); if (patched != null) patched.Unload(true); if (skeleton != null) skeleton.Unload(true);
                foreach (var bundle in convertedBundles.Values) if (bundle != null) bundle.Unload(true);
            }
            File.WriteAllText(input.output, JsonUtility.ToJson(new NativeReport { roles = report.ToArray(), gameVerified = false, diagnostic = input.diagnostic, verificationId = input.verificationId }, true));
            Debug.Log("AI_H_NATIVE_ROLE_CHECK " + role.role + " passed=" + result.passed + " angle=" + result.rotationError + " position=" + result.positionError);
        }
        Debug.Log("AI_H_NATIVE_CHECK_DONE passed=" + report.Count(r => r.passed) + " total=" + report.Count);
    }
    private static AnimatorOverrideController OverrideExternal(AssetBundle bundle, string baseName, NativeBinding[] definitions, Dictionary<string, AssetBundle> loaded, out int assigned)
    {
        var basis = bundle.LoadAsset<RuntimeAnimatorController>(baseName);
        var result = new AnimatorOverrideController(basis); var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(); result.GetOverrides(pairs);
        var bindings = definitions.ToDictionary(d => d.key); assigned = I("math.zero");
        for (int clip = I("math.zero"); clip < pairs.Count; ++clip)
        {
            if (!bindings.TryGetValue(pairs[clip].Key.name, out var binding)) throw new InvalidDataException("Native compiled graph clip lacks external override: " + pairs[clip].Key.name);
            if (!loaded.TryGetValue(binding.bundle, out var converted)) { converted = AssetBundle.LoadFromFile(binding.bundle); loaded.Add(binding.bundle, converted); }
            var replacement = converted.LoadAsset<AnimationClip>(binding.asset);
            if (replacement == null) throw new InvalidDataException("External converted native graph clip missing");
            pairs[clip] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[clip].Key, replacement); ++assigned;
        }
        if (assigned != bindings.Count) throw new InvalidDataException("Native compiled graph override closure count mismatch");
        result.ApplyOverrides(pairs); return result;
    }
    private static AnimatorOverrideController Override(AssetBundle bundle, string baseName, string poseName, out int assigned)
    {
        var basis = bundle.LoadAsset<RuntimeAnimatorController>(baseName); var pose = bundle.LoadAsset<RuntimeAnimatorController>(poseName);
        if (basis == null || pose == null) throw new InvalidDataException("Native base/pose controller missing");
        var result = new AnimatorOverrideController(basis); var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(); result.GetOverrides(pairs);
        var clips = pose.animationClips.GroupBy(c => c.name).ToDictionary(g => g.Key, g => g.First()); assigned = I("math.zero");
        for (int clip = I("math.zero"); clip < pairs.Count; ++clip)
            if (clips.TryGetValue(pairs[clip].Key.name, out var replacement)) { pairs[clip] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[clip].Key, replacement); ++assigned; }
        result.ApplyOverrides(pairs); return result;
    }
    private sealed class NativePositionKnot
    {
        public float time;
        public readonly Dictionary<SourceScalar, int> boundaries = new Dictionary<SourceScalar, int>();
    }
    private static List<NativePositionKnot> NativePositionKnots(TaskInput task, float duration, PositionTrack track)
    {
        var knots = new Dictionary<float, NativePositionKnot>();
        Action<float, SourceScalar, int> add = (time, curve, index) =>
        {
            if (time < N("math.zero") || time > duration) return;
            if (!knots.TryGetValue(time, out var knot)) knots.Add(time, knot = new NativePositionKnot { time = time });
            if (curve != null) knot.boundaries.Add(curve, index);
        };
        add(N("math.zero"), null, I("math.zero")); add(duration, null, I("math.zero"));
        foreach (var curve in new[] { track.x, track.y, track.z })
        {
            if (curve.kind == "dense")
                for (int index = I("math.zero"); index < curve.values.Length; ++index) add(curve.begin + index / curve.rate - task.sourceStartTime, curve, index);
            if (curve.kind == "stream")
                for (int index = I("math.zero"); index < curve.keys.Length; ++index) add(curve.keys[index].time - task.sourceStartTime, curve, index);
        }
        return knots.Values.OrderBy(knot => knot.time).ToList();
    }
    private static float KnotValue(SourceScalar curve, NativePositionKnot knot, float start)
    {
        if (knot.boundaries.TryGetValue(curve, out var index)) return curve.kind == "dense" ? curve.values[index] : curve.keys[index].value;
        return EvaluateSource(curve, knot.time + start);
    }
    private static float KnotDerivative(SourceScalar curve, NativePositionKnot knot, float localTime, float start, bool incoming)
    {
        if (knot == null || !knot.boundaries.TryGetValue(curve, out var boundary)) return Derivative(curve, localTime + start, incoming);
        int segment = incoming ? boundary - I("math.one") : boundary;
        if (segment < I("math.zero")) return N("math.zero");
        if (curve.kind == "dense") return segment >= curve.values.Length - I("math.one") ? N("math.zero") : (curve.values[segment + I("math.one")] - curve.values[segment]) * curve.rate;
        var key = curve.keys[segment]; float delta = curve.keys[boundary].time - key.time;
        return ((N("math.two") + N("math.one")) * key.a * delta + N("math.two") * key.b) * delta + key.c;
    }

    private static List<float> AuthoredPositionTimes(TaskInput task, float duration, PositionTrack only)
    {
        var times = new List<float> { N("math.zero"), duration };
        foreach (var track in only == null ? task.positionTracks : new[] { only })
        foreach (var curve in new[] { track.x, track.y, track.z })
        {
            if (curve.kind == "stream") times.AddRange(curve.keys.Select(key => key.time - task.sourceStartTime));
            if (curve.kind == "dense") times.AddRange(Enumerable.Range(I("math.zero"), curve.values.Length).Select(index => curve.begin + index / curve.rate - task.sourceStartTime));
        }
        return times.Where(time => time >= N("math.zero") && time <= duration).Distinct().OrderBy(time => time).ToList();
    }

    private static float EvaluateSource(SourceScalar curve, float time)
    {
        if (curve.kind == "constant") return curve.value;
        if (curve.kind == "dense")
        {
            float frame = Mathf.Clamp((time - curve.begin) * curve.rate, N("math.zero"), curve.values.Length - I("math.one")); int index = Mathf.FloorToInt(frame);
            return index >= curve.values.Length - I("math.one") ? curve.values[index] : Mathf.LerpUnclamped(curve.values[index], curve.values[index + I("math.one")], frame - index);
        }
        int selected = I("math.zero");
        for (int index = I("math.zero"); index < curve.keys.Length; ++index) { if (curve.keys[index].time > time) break; selected = index; }
        var key = curve.keys[selected]; float delta = time - key.time;
        return ((key.a * delta + key.b) * delta + key.c) * delta + key.value;
    }

    private static float Derivative(SourceScalar curve, float time, bool incoming)
    {
        if (curve.kind == "constant") return N("math.zero");
        if (curve.kind == "dense")
        {
            float frame = (time - curve.begin) * curve.rate; int index = Mathf.FloorToInt(frame);
            if (incoming && frame.Equals((float)index)) --index;
            if (index < I("math.zero") || index >= curve.values.Length - I("math.one")) return N("math.zero");
            return (curve.values[index + I("math.one")] - curve.values[index]) * curve.rate;
        }
        if (curve.kind != "stream" || curve.keys == null || curve.keys.Length == I("math.zero")) throw new InvalidDataException("Native scalar derivative kind is invalid");
        int selected = I("math.zero");
        for (int index = I("math.zero"); index < curve.keys.Length; ++index)
        { if (curve.keys[index].time > time || incoming && curve.keys[index].time.Equals(time)) break; selected = index; }
        var key = curve.keys[selected]; float delta = time - key.time;
        return ((N("math.two") + N("math.one")) * key.a * delta + N("math.two") * key.b) * delta + key.c;
    }

    private static CurveControl KnownCurveControl(GameObject root, Transform bone)
    {
        var result = new CurveControl { root = root.name, expected = N("math.half") };
        var original = bone.localPosition; var animator = root.GetComponent<Animator>(); if (animator == null) animator = root.AddComponent<Animator>();
        var oldController = animator.runtimeAnimatorController; var oldAvatar = animator.avatar;
        var clip = new AnimationClip { name = "AiH known curve" };
        var controller = new UnityEditor.Animations.AnimatorController();
        var graph = PlayableGraph.Create("AiH known curve control");
        try
        {
            var path = AnimationUtility.CalculateTransformPath(bone, root.transform);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.x"), AnimationCurve.Linear(N("math.zero"), N("math.zero"), N("math.one"), N("math.one")));
            controller.AddLayer("Known curve"); controller.AddMotion(clip);
            animator.runtimeAnimatorController = null; animator.avatar = null; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var player = AnimatorControllerPlayable.Create(graph, controller);
            var output = AnimationPlayableOutput.Create(graph, "Known bone", animator); output.SetSourcePlayable(player);
            player.Play(clip.name, I("math.zero"), N("math.half"));
            bone.localPosition = new Vector3(N("math.zero"), original.y, original.z); output.SetWeight(N("math.zero")); graph.Evaluate(N("math.zero")); result.disabled = bone.localPosition.x;
            bone.localPosition = new Vector3(N("math.zero"), original.y, original.z); output.SetWeight(N("math.one")); graph.Evaluate(N("math.zero")); result.enabled = bone.localPosition.x;
            result.passed = Mathf.Abs(result.disabled) <= N("hMotion.positionTolerance") && Mathf.Abs(result.enabled - result.expected) <= N("hMotion.positionTolerance");
        }
        finally { graph.Destroy(); animator.avatar = oldAvatar; animator.runtimeAnimatorController = oldController; animator.Rebind(); bone.localPosition = original; UnityEngine.Object.DestroyImmediate(controller); UnityEngine.Object.DestroyImmediate(clip); }
        return result;
    }

    private static void DrivePlayable(AnimatorControllerPlayable controller, Probe probe)
    {
        controller.SetFloat("height", probe.height); controller.SetFloat("motion", probe.motion); controller.SetFloat("breast", probe.breast); controller.SetFloat("speed", N("math.one"));
        int hash = Animator.StringToHash(probe.state);
        if (!controller.HasState(I("math.zero"), hash)) throw new InvalidDataException("Product native state missing: " + probe.state);
        controller.Play(hash, I("math.zero"), probe.phase);
        for (int layer = I("math.one"); layer < controller.GetLayerCount(); ++layer) controller.SetLayerWeight(layer, N("math.zero"));
        foreach (var layer in probe.layers ?? new NativeLayer[I("math.zero")]) { controller.SetLayerWeight(layer.index, layer.weight); controller.Play(hash, layer.index, probe.phase); }
    }

    private static void Drive(Animator animator, string state, NativeLayer[] layers, float height, float motion, float breast, float phase)
    {
        foreach (var parameter in animator.parameters)
        {
            if (parameter.type != AnimatorControllerParameterType.Float) continue;
            if (parameter.name == "height") animator.SetFloat(parameter.name, height);
            if (parameter.name == "motion") animator.SetFloat(parameter.name, motion);
            if (parameter.name == "breast") animator.SetFloat(parameter.name, breast);
            if (parameter.name == "speed") animator.SetFloat(parameter.name, N("math.one"));
        }
        int hash = Animator.StringToHash(state);
        if (!animator.HasState(I("math.zero"), hash)) throw new InvalidDataException("Native base graph state missing: " + state);
        animator.SetLayerWeight(I("math.zero"), N("math.one")); animator.Play(hash, I("math.zero"), phase);
        for (int layer = I("math.one"); layer < animator.layerCount; ++layer) animator.SetLayerWeight(layer, N("math.zero"));
        foreach (var layer in layers ?? new NativeLayer[I("math.zero")]) { animator.SetLayerWeight(layer.index, layer.weight); animator.Play(hash, layer.index, phase); }
        animator.Update(N("math.zero"));
    }
}
