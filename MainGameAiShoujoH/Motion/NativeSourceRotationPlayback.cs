using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using MainGameAiShoujoH.Contracts;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MainGameAiShoujoH.Motion
{
    // Only manifest-selected rotations are supplied by the original native graph.
    // Native constraints first align its parent with the actual KKS parent after IK,
    // then apply the authored local rotation relative to both rigs' rest axes.
    // Target position/scale and the KKS shape/physics components remain owned by KKS.
    internal sealed class NativeSourceRotationPlayback : IDisposable
    {
        internal const string VerifiedScope = "rest-relative-local-rotation-parent-anchor-v1";
        [Serializable] public sealed class Verification
        {
            public bool passed, parentAnchorVerified, nonzeroRestVerified, ikShapeOwnershipVerified, nativeControllerSyncVerified, positionCompositionVerified;
            public string scope, sourceBundleSha256, definitionHash; public string[] targetBones;
            public Verification() { passed = parentAnchorVerified = nonzeroRestVerified = ikShapeOwnershipVerified = nativeControllerSyncVerified = positionCompositionVerified = false; scope = sourceBundleSha256 = definitionHash = string.Empty; targetBones = Array.Empty<string>(); }
        }
        private static string Digest(Stream stream) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant(); }
        private static void Fields(StringBuilder text, params string[] fields) { foreach (var value in fields) text.Append(value).Append('|'); text.AppendLine(); }
        private static string Float(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static string Vector(Vector3 value) => string.Join(",", Float(value.x), Float(value.y), Float(value.z));
        private static string Rotation(Quaternion value) => string.Join(",", Float(value.x), Float(value.y), Float(value.z), Float(value.w));
        internal static string DefinitionFingerprint(NativeSourceRotationDriver definition)
        {
            var text = new StringBuilder(); Fields(text, VerifiedScope, definition.bundle, definition.sha256, definition.baseController, definition.controller);
            foreach (var node in definition.nodes.OrderBy(node => node.path, StringComparer.Ordinal)) Fields(text, node.path, Vector(node.position), Rotation(node.rotation), Vector(node.scale));
            foreach (var clip in definition.baseClips.OrderBy(clip => clip.sourceId, StringComparer.Ordinal)) Fields(text, "base", clip.sourceId, clip.asset, clip.name, Float(clip.duration));
            foreach (var clip in definition.poseClips.OrderBy(clip => clip.sourceId, StringComparer.Ordinal)) Fields(text, "pose", clip.sourceId, clip.asset, clip.name, Float(clip.duration));
            foreach (var rotation in definition.rotations.OrderBy(rotation => rotation.targetBone, StringComparer.Ordinal)) Fields(text, rotation.sourceBone, rotation.sourceParent, rotation.targetBone, rotation.targetParent, Rotation(rotation.targetRestParent), Rotation(rotation.targetRestBone));
            foreach (var position in (definition.positions ?? Array.Empty<NativeSourcePosition>()).OrderBy(position => position.targetBone, StringComparer.Ordinal)) Fields(text, "position", position.sourceBone, position.sourceParent, position.targetBone, position.targetParent, Float(position.unitScale));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(text.ToString()))) return Digest(stream);
        }
        private static void RequireVerification(NativeSourceRotationDriver definition, string directory)
        {
            if (!definition.nativeDriverVerified || definition.coverageIncomplete || definition.verificationScope != VerifiedScope || string.IsNullOrEmpty(definition.verificationReceipt) || string.IsNullOrEmpty(definition.verificationReceiptSha256)) throw new InvalidDataException("Native source rotation driver is unverified or its scope is incomplete");
            var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var path = Path.GetFullPath(Path.Combine(root, definition.verificationReceipt));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Native correction receipt escapes content directory");
            using (var stream = File.OpenRead(path)) if (!string.Equals(Digest(stream), definition.verificationReceiptSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Native correction receipt hash differs");
            Verification receipt; using (var stream = File.OpenRead(path)) receipt = (Verification)new DataContractJsonSerializer(typeof(Verification)).ReadObject(stream);
            if ((definition.positions?.Length ?? 0) > 0 && (receipt == null || !receipt.positionCompositionVerified)) throw new InvalidDataException("Native source position composition is not verified");
            if (receipt == null || !receipt.passed || !receipt.parentAnchorVerified || !receipt.nonzeroRestVerified || !receipt.ikShapeOwnershipVerified || !receipt.nativeControllerSyncVerified || receipt.scope != VerifiedScope || receipt.sourceBundleSha256 != definition.sha256 || receipt.definitionHash != DefinitionFingerprint(definition) || receipt.targetBones == null || !new HashSet<string>(receipt.targetBones, StringComparer.Ordinal).SetEquals(definition.rotations.Select(rotation => rotation.targetBone))) throw new InvalidDataException("Native source rotation receipt does not verify this exact graph/bone/ownership scope");
        }

        private readonly List<RotationConstraint> _constraints = new List<RotationConstraint>();
        private readonly List<PositionConstraint> _positionConstraints = new List<PositionConstraint>();
        private readonly List<GameObject> _positionAnchors = new List<GameObject>();
        private readonly List<Action> _restore = new List<Action>();
        private readonly Action<string> _failure;
        private GameObject _root;
        private Avatar _avatar;
        private AnimatorOverrideController _override;
        private AnimationClip[] _sourceLifetime;
        private bool _disposed;
        internal AnimatorControllerPlayable Controller { get; private set; }
        internal string Role { get; }
        internal NativeSourceRotationPlayback(string role, NativeSourceRotationDriver definition, ChaControl actor, PlayableGraph graph, Func<string, string, AssetBundle> load, SessionContext context, string contentDirectory)
        {
            Role = role; _failure = context.ReportFailure;
            try
            {
                if (definition.nodes == null || definition.baseClips == null || definition.poseClips == null || definition.rotations == null || !definition.rotations.Any()) throw new InvalidDataException("Native source rotation closure is missing");
#if !AI_H_NATIVE_VERIFICATION
                RequireVerification(definition, contentDirectory);
#endif
                _root = new GameObject("AI H native source rotations: " + role);
                _root.transform.SetParent(actor.animBody.transform, false);
                if ((definition.positions?.Length ?? 0) > 0)
                {
                    if (definition.positions.Length != 1) throw new InvalidDataException("Only the verified single position composition is supported");
                    var position = definition.positions.Single();
                    var targetParent = actor.animBody.transform.Find(position.targetParent);
                    if (targetParent == null || !PositiveUniform(targetParent.lossyScale) || !PositiveUniform(actor.animBody.transform.lossyScale) || !(position.unitScale > 0) || float.IsInfinity(position.unitScale)) throw new InvalidDataException("Position composition needs finite positive uniform actual parent/body scales");
                    _root.transform.localScale = Vector3.one * (position.unitScale * targetParent.lossyScale.x / actor.animBody.transform.lossyScale.x);
                }
                var nodes = new Dictionary<string, Transform>(StringComparer.Ordinal) { { string.Empty, _root.transform } };
                foreach (var node in definition.nodes.OrderBy(n => n.path.Count(character => character == '/')))
                {
                    if (string.IsNullOrEmpty(node.path) || node.path.Split('/').Any(part => part == ".." || part.Length == 0)) throw new InvalidDataException("Invalid native source bone path");
                    var slash = node.path.LastIndexOf('/'); var parent = slash < 0 ? string.Empty : node.path.Substring(0, slash);
                    var bone = new GameObject(slash < 0 ? node.path : node.path.Substring(slash + 1)).transform;
                    bone.SetParent(nodes[parent], false); bone.localPosition = node.position; bone.localRotation = node.rotation; bone.localScale = node.scale; nodes.Add(node.path, bone);
                }
                var animator = _root.AddComponent<Animator>(); animator.fireEvents = false; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                _avatar = AvatarBuilder.BuildGenericAvatar(_root, string.Empty);
                if (_avatar == null || !_avatar.isValid) throw new InvalidDataException("Native source correction Avatar invalid");
                animator.avatar = _avatar;
                var bundle = load(definition.bundle, definition.sha256);
                // Preserve the complete native object lifetime: loading just one raw
                // clip allowed subsequent lazy clip objects to collapse to rest.
                _sourceLifetime = bundle.LoadAllAssets<AnimationClip>();
                var basis = bundle.LoadAsset<RuntimeAnimatorController>(definition.baseController);
                var pose = bundle.LoadAsset<RuntimeAnimatorController>(definition.controller);
                ValidateClips(bundle, basis, definition.baseClips); ValidateClips(bundle, pose, definition.poseClips);
                var poseClips = pose.animationClips.GroupBy(clip => clip.name).ToDictionary(group => group.Key, group => group.Distinct().Single());
                _override = new AnimatorOverrideController(basis);
                var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(); _override.GetOverrides(overrides);
                foreach (var binding in overrides.ToArray())
                    if (poseClips.TryGetValue(binding.Key.name, out var replacement)) overrides[overrides.IndexOf(binding)] = new KeyValuePair<AnimationClip, AnimationClip>(binding.Key, replacement);
                _override.ApplyOverrides(overrides);
                Controller = AnimatorControllerPlayable.Create(graph, _override);
                var output = AnimationPlayableOutput.Create(graph, "Native source rotations: " + role, animator); output.SetSourcePlayable(Controller);
                var parents = new Dictionary<Transform, Transform>();
                foreach (var rotation in definition.rotations)
                {
                    var source = nodes[rotation.sourceBone]; var sourceParent = nodes[rotation.sourceParent];
                    if (source.parent != sourceParent) throw new InvalidDataException("Source correction bone is not a direct child of its declared parent");
                    var target = actor.animBody.transform.Find(rotation.targetBone); var targetParent = actor.animBody.transform.Find(rotation.targetParent);
                    if (target == null || targetParent == null || target.parent != targetParent || targetParent.IsChildOf(target)) throw new InvalidDataException("Target correction hierarchy differs from its verified parent contract");
                    if (target.GetComponents<Component>().OfType<IConstraint>().Any(constraint => constraint.constraintActive)) throw new InvalidDataException("Target rotation already has a constraint owner: " + target.name);
                    // This first small set excludes shape-owned nodes. Shape's child
                    // roots and every position/scale stay untouched.
                    if (Enum.GetNames(typeof(ShapeBodyInfoFemale.DstName)).Contains(target.name)) throw new InvalidDataException("A shape-owned target requires a separate composition contract: " + target.name);
                    var parentRest = actor.animBody.transform.rotation * rotation.targetRestParent;
                    var boneRest = actor.animBody.transform.rotation * rotation.targetRestBone;
                    var sourceParentRest = sourceParent.rotation; var sourceBoneRest = source.rotation;
                    if (parents.TryGetValue(sourceParent, out var previousParent))
                    {
                        if (previousParent != targetParent) throw new InvalidDataException("One source parent cannot follow two target parents");
                    }
                    else
                    {
                        parents.Add(sourceParent, targetParent);
                        AddConstraint(sourceParent, targetParent, Quaternion.Inverse(parentRest) * sourceParentRest);
                    }
                    var before = target.localRotation; _restore.Add(() => { if (target != null) target.localRotation = before; });
                    AddConstraint(target, source, Quaternion.Inverse(sourceBoneRest) * boneRest);
                }
                foreach (var position in definition.positions ?? Array.Empty<NativeSourcePosition>())
                {
                    var rotation = definition.rotations.SingleOrDefault(r => r.sourceBone == position.sourceBone && r.sourceParent == position.sourceParent && r.targetBone == position.targetBone && r.targetParent == position.targetParent);
                    if (rotation == null) throw new InvalidDataException("Position composition requires its exact existing rotation/parent contract");
                    var source = nodes[position.sourceBone]; var sourceParent = nodes[position.sourceParent];
                    var target = actor.animBody.transform.Find(position.targetBone); var targetParent = target.parent;
                    if (!PositiveUniform(sourceParent.lossyScale)) throw new InvalidDataException("Source position parent has unsupported rest scale");
                    var targetRestParent = actor.animBody.transform.rotation * rotation.targetRestParent;
                    var positionBasis = Quaternion.Inverse(targetRestParent) * sourceParent.rotation;
                    var baseline = target.localPosition; _restore.Add(() => { if (target != null) target.localPosition = baseline; });
                    var anchor = new GameObject("AI H position rest anchor: " + target.name); _positionAnchors.Add(anchor);
                    anchor.transform.SetParent(targetParent, false); anchor.transform.localPosition = baseline - positionBasis * source.localPosition * position.unitScale;
                    AddPositionConstraint(sourceParent, anchor.transform); AddPositionConstraint(target, source);
                }
            }
            catch { Dispose(); throw; }
        }
        private static void ValidateClips(AssetBundle bundle, RuntimeAnimatorController controller, NativeSourceClip[] expected)
        {
            if (controller == null || expected == null) throw new InvalidDataException("Original native source controller/clip closure missing");
            var actual = new HashSet<AnimationClip>(controller.animationClips);
            var checkedClips = new HashSet<AnimationClip>();
            foreach (var definition in expected)
            {
                if (definition.asset != "canon/animations/" + definition.sourceId + ".anim") throw new InvalidDataException("Native correction clip ID locator differs");
                var clip = bundle.LoadAsset<AnimationClip>(definition.asset);
                if (clip == null || clip.name != definition.name || !(clip.length > 0) || Math.Abs(clip.length - definition.duration) > .01f || !actual.Contains(clip)) throw new InvalidDataException("Original native correction clip ID/name/duration/reference guard failed: " + definition.sourceId);
                checkedClips.Add(clip);
            }
            if (!actual.SetEquals(checkedClips)) throw new InvalidDataException("Original correction controller clip closure differs");
        }
        private void AddConstraint(Transform target, Transform source, Quaternion offset)
        {
            var constraint = target.gameObject.AddComponent<RotationConstraint>(); _constraints.Add(constraint);
            constraint.rotationAtRest = target.localRotation.eulerAngles; constraint.rotationOffset = offset.eulerAngles;
            constraint.rotationAxis = Axis.X | Axis.Y | Axis.Z; constraint.AddSource(new ConstraintSource { sourceTransform = source, weight = 1 });
            constraint.weight = 1; constraint.locked = true; constraint.constraintActive = true;
        }
        private static bool PositiveUniform(Vector3 scale) => scale.x > 0 && !float.IsInfinity(scale.x) && !float.IsNaN(scale.x) && Mathf.Approximately(scale.x, scale.y) && Mathf.Approximately(scale.x, scale.z);
        private void AddPositionConstraint(Transform target, Transform source)
        {
            var constraint = target.gameObject.AddComponent<PositionConstraint>(); _positionConstraints.Add(constraint);
            constraint.translationAtRest = target.localPosition; constraint.translationOffset = Vector3.zero;
            constraint.translationAxis = Axis.X | Axis.Y | Axis.Z; constraint.AddSource(new ConstraintSource { sourceTransform = source, weight = 1 });
            constraint.weight = 1; constraint.locked = true; constraint.constraintActive = true;
        }
        internal void Play(MotionState state, MotionActor actor, float speed)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NativeSourceRotationPlayback));
            int hash = Animator.StringToHash(state.name);
            if (!Controller.HasState(0, hash)) throw new InvalidDataException("Original correction graph state missing: " + state.name);
            Controller.Play(hash, 0, 0);
            for (int layer = 1; layer < Controller.GetLayerCount(); ++layer) Controller.SetLayerWeight(layer, 0);
            foreach (var layer in actor.layers ?? new MotionLayer[0])
            {
                if (layer.index <= 0 || layer.index >= Controller.GetLayerCount() || !Controller.HasState(layer.index, hash)) throw new InvalidDataException("Original correction graph layer/state missing");
                Controller.SetLayerWeight(layer.index, layer.weight); Controller.Play(hash, layer.index, 0);
            }
        }
#if AI_H_NATIVE_VERIFICATION
        // Included only by the finite diagnostic assembly, never the product project.
        internal Transform VerificationRoot => _root.transform;
        internal void VerificationConstraintEnabled(bool enabled) { foreach (var constraint in _constraints) constraint.constraintActive = enabled; foreach (var constraint in _positionConstraints) constraint.constraintActive = enabled; }
#endif
        private void Release(Action action, string part) { try { action(); } catch (Exception error) { _failure?.Invoke("Native source rotation " + part + ": " + error.GetBaseException().Message); } }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            foreach (var constraint in Enumerable.Reverse(_constraints)) Release(() => { if (constraint != null) { constraint.constraintActive = false; constraint.enabled = false; UnityEngine.Object.Destroy(constraint); } }, "constraint release");
            _constraints.Clear();
            foreach (var constraint in Enumerable.Reverse(_positionConstraints)) Release(() => { if (constraint != null) { constraint.constraintActive = false; constraint.enabled = false; UnityEngine.Object.Destroy(constraint); } }, "position constraint release");
            _positionConstraints.Clear();
            foreach (var anchor in _positionAnchors) Release(() => { if (anchor != null) { anchor.SetActive(false); anchor.transform.SetParent(null, false); UnityEngine.Object.Destroy(anchor); } }, "position anchor release");
            _positionAnchors.Clear();
            foreach (var restore in Enumerable.Reverse(_restore)) Release(restore, "target restore"); _restore.Clear();
            Release(() => { if (_root != null) { _root.SetActive(false); _root.transform.SetParent(null, false); UnityEngine.Object.Destroy(_root); } }, "source hierarchy release");
            Release(() => { if (_avatar != null) UnityEngine.Object.Destroy(_avatar); }, "Avatar release");
            Release(() => { if (_override != null) UnityEngine.Object.Destroy(_override); }, "override release");
            GC.KeepAlive(_sourceLifetime); _sourceLifetime = null;
        }
    }
}
