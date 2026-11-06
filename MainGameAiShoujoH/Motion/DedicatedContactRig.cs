using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RootMotion.FinalIK;
using Illusion.Component.Correct;
using Illusion.Component.Correct.Process;
using UnityEngine;

namespace MainGameAiShoujoH.Motion
{
    // The game's existing FinalIK component evaluates contacts. Selection and explicit
    // shape changes configure targets once; this class does not install an update loop.
    internal sealed class DedicatedContactRig : IDisposable
    {
        private readonly ChaControl _character;
        private readonly MotionActor _definition;
        private readonly Dictionary<string, ChaControl> _partners;
        private readonly List<Action> _restore = new List<Action>();
        private readonly List<GameObject> _created = new List<GameObject>();
        private readonly Dictionary<MotionFrame, Transform> _frames = new Dictionary<MotionFrame, Transform>();
        private readonly Dictionary<BodyCorrection, BaseCorrect.Info> _body = new Dictionary<BodyCorrection, BaseCorrect.Info>();
        private readonly Dictionary<BodyCorrection, BaseData> _targetCorrections = new Dictionary<BodyCorrection, BaseData>();
        private readonly List<Component> _addedComponents = new List<Component>();
        private readonly Action<string> _failure;
        private bool _disposed;
        internal DedicatedContactRig(ChaControl character, MotionActor definition, Dictionary<string, ChaControl> partners, Action<string> failure)
        {
            _character = character; _definition = definition; _partners = partners; _failure = failure;
            try
            {
                var correct = character.animBody.GetComponent<FrameCorrect>() ?? throw new InvalidDataException("KKS frame correction driver missing");
                var correctEnabled = correct.enabled; _restore.Add(() => { if (correct != null) correct.enabled = correctEnabled; }); correct.enabled = true;
                foreach (var existing in correct.list)
                {
                    var frame = existing; var active = frame.enabled; var position = frame.pos; var rotation = frame.rot;
                    _restore.Add(() => { frame.pos = position; frame.rot = rotation; frame.enabled = active; });
                    frame.enabled = false; frame.pos = Vector3.zero; frame.rot = Quaternion.identity;
                }
                var ikCorrect = character.animBody.GetComponent<IKCorrect>() ?? throw new InvalidDataException("KKS IK correction driver missing");
                var ikEnabled = ikCorrect.enabled; _restore.Add(() => { if (ikCorrect != null) ikCorrect.enabled = ikEnabled; }); ikCorrect.enabled = true;
                foreach (var correction in definition.corrections ?? new BodyCorrection[0])
                {
                    var bone = Find(character, correction.bone);
                    if (correction.ikTarget)
                    {
                        var targetData = bone.GetComponent<BaseData>() ?? throw new InvalidDataException("Native IK target correction data missing: " + bone.name);
                        var position = targetData.pos; var rotation = targetData.rot;
                        _restore.Add(() => { if (targetData != null) { targetData.pos = position; targetData.rot = rotation; } });
                        _targetCorrections.Add(correction, targetData); continue;
                    }
                    var info = correct.list.FirstOrDefault(v => v.data.transform == bone);
                    if (info == null)
                    {
                        // The native driver can evaluate additional finger/shoulder frames;
                        // its stock 24-name catalog does not limit the list itself.
                        if (bone.GetComponents<BaseProcess>().Length != 0) throw new InvalidDataException("Additional correction frame already has an owned process: " + bone.name);
                        var components = new HashSet<Component>(bone.GetComponents<Component>());
                        info = new BaseCorrect.Info(bone) { type = correction.beforeIK ? BaseCorrect.Info.ProcOrderType.First : BaseCorrect.Info.ProcOrderType.Last, bone = bone };
                        info.process.type = BaseProcess.Type.Target;
                        _addedComponents.AddRange(bone.GetComponents<Component>().Where(c => !components.Contains(c)));
                        correct.list.Add(info);
                        var added = info; _restore.Add(() => { if (correct != null) correct.list.Remove(added); });
                    }
                    else
                    {
                        var owned = info; var enabled = info.enabled; var p = info.pos; var r = info.rot; var previousBone = info.bone;
                        _restore.Add(() => { if (bone != null) { owned.pos = p; owned.rot = r; owned.bone = previousBone; owned.enabled = enabled; } });
                    }
                    info.enabled = true; _body.Add(correction, info);
                }
                foreach (var frame in definition.frames ?? new MotionFrame[0])
                {
                    if (!partners.TryGetValue(frame.parentRole, out var parent)) throw new InvalidDataException("Correction frame partner missing: " + frame.parentRole);
                    var root = Find(parent, frame.parentBone);
                    if (string.IsNullOrEmpty(frame.path) || frame.path.IndexOf('/') >= 0) throw new InvalidDataException("Correction frame path must be a local unique name");
                    var existingAnchors = root.Cast<Transform>().Where(t => t.name == frame.path).ToArray();
                    if (existingAnchors.Length > 1 || existingAnchors.Length == 1 && existingAnchors[0].GetComponent<DedicatedContactProxyTag>() == null)
                        throw new InvalidDataException("Correction frame ownership collision: " + frame.path);
                    Transform anchor;
                    if (existingAnchors.Length == 1) { anchor = existingAnchors[0]; _created.Add(anchor.gameObject); }
                    else
                    {
                        var created = new GameObject(frame.path); _created.Add(created);
                        created.AddComponent<DedicatedContactProxyTag>(); created.transform.SetParent(root, false); anchor = created.transform;
                    }
                    anchor.GetComponent<DedicatedContactProxyTag>().References++;
                    _frames.Add(frame, anchor);
                }
                var contacts = definition.contacts ?? throw new InvalidDataException("H contact table missing");
                var solver = character.animBody.GetComponent<FullBodyBipedIK>() ?? throw new InvalidDataException("KKS body IK missing");
                var solverEnabled = solver.enabled; _restore.Add(() => { if (solver != null) solver.enabled = solverEnabled; }); solver.enabled = true;
                var parts = new[] { solver.solver.leftHandEffector, solver.solver.rightHandEffector, solver.solver.leftFootEffector, solver.solver.rightFootEffector };
                var bends = new[] { solver.solver.leftArmChain.bendConstraint, solver.solver.rightArmChain.bendConstraint, solver.solver.leftLegChain.bendConstraint, solver.solver.rightLegChain.bendConstraint };
                if (contacts.Length != 4 || contacts.Select(c => c.part).Distinct().Count() != 4) throw new InvalidDataException("All four H IK parts must be explicitly defined");
                foreach (var contact in contacts)
                {
                    if (contact.part < 0 || contact.part > 3 || !Weight(contact.positionWeight) || !Weight(contact.rotationWeight) || !Weight(contact.bendWeight)) throw new InvalidDataException("Invalid H IK part/weight");
                    var effector = parts[contact.part]; var bend = bends[contact.part];
                    var previous = effector.target; var pw = effector.positionWeight; var rw = effector.rotationWeight;
                    var previousBend = bend.bendGoal; var bw = bend.weight;
                    _restore.Add(() => { effector.target = previous; effector.positionWeight = pw; effector.rotationWeight = rw; bend.bendGoal = previousBend; bend.weight = bw; });
                    if (!string.IsNullOrEmpty(contact.target))
                    {
                        if (!partners.TryGetValue(contact.partner, out var partner)) throw new InvalidDataException("Contact participant missing: " + contact.partner);
                        Link(effector.target, Find(partner, contact.target));
                    }
                    else if (contact.positionWeight != 0 || contact.rotationWeight != 0) throw new InvalidDataException("Weighted contact target missing");
                    else Unlink(effector.target);
                    if (!string.IsNullOrEmpty(contact.bend)) Link(bend.bendGoal, Find(character, contact.bend));
                    else if (contact.bendWeight != 0) throw new InvalidDataException("Weighted bend target missing");
                    else Unlink(bend.bendGoal);
                    effector.positionWeight = contact.positionWeight; effector.rotationWeight = contact.rotationWeight; bend.weight = contact.bendWeight;
                }
                ReapplyShape();
            }
            catch { Dispose(); throw; }
        }
        private void Link(Transform nativeTarget, Transform bone)
        {
            if (nativeTarget == null) throw new InvalidDataException("Native IK target missing");
            var data = nativeTarget.GetComponent<BaseData>() ?? throw new InvalidDataException("Native IK target data missing");
            var process = nativeTarget.GetComponent<BaseProcess>() ?? throw new InvalidDataException("Native IK target process missing");
            var previous = data.bone; var position = data.pos; var rotation = data.rot; var enabled = process.enabled;
            _restore.Add(() => { if (data != null) { data.bone = previous; data.pos = position; data.rot = rotation; } if (process != null) process.enabled = enabled; });
            data.bone = bone; process.enabled = true;
            data.pos = Vector3.zero; data.rot = Quaternion.identity;
        }
        private void Unlink(Transform nativeTarget)
        {
            if (nativeTarget == null) throw new InvalidDataException("Native IK target missing");
            var data = nativeTarget.GetComponent<BaseData>() ?? throw new InvalidDataException("Native IK target data missing");
            var process = nativeTarget.GetComponent<BaseProcess>() ?? throw new InvalidDataException("Native IK target process missing");
            var previous = data.bone; var position = data.pos; var rotation = data.rot; var enabled = process.enabled;
            _restore.Add(() => { if (data != null) { data.bone = previous; data.pos = position; data.rot = rotation; } if (process != null) process.enabled = enabled; });
            // Original MotionIK.LinkIK(null) explicitly clears target ownership.
            data.bone = null; data.pos = Vector3.zero; data.rot = Quaternion.identity; process.enabled = false;
        }
        private static bool Weight(float value) => !float.IsNaN(value) && value >= 0 && value <= 1;
        private static Transform Find(ChaControl actor, string name)
        {
            if (actor == null || actor.animBody == null) throw new InvalidDataException("Contact actor destroyed");
            var matches = actor.animBody.GetComponentsInChildren<Transform>(true).Where(t => t.name == name).ToArray();
            if (matches.Length != 1) throw new InvalidDataException("Contact bone missing/ambiguous: " + name);
            return matches[0];
        }
        internal void ReapplyShape()
        {
            if (_disposed || _character == null) return;
            float height = _character.GetShapeBodyValue(0);
            foreach (var frame in _frames)
            {
                frame.Value.localPosition = Interpolate(frame.Key.smallPosition, frame.Key.mediumPosition, frame.Key.largePosition, height);
                frame.Value.localRotation = Quaternion.Euler(Interpolate(frame.Key.smallRotation, frame.Key.mediumRotation, frame.Key.largeRotation, height));
            }
            foreach (var definition in _body.Keys.Concat(_targetCorrections.Keys))
            {
                Vector3 position = Vector3.zero; Vector3 angles = Vector3.zero;
                foreach (var shape in definition.shapes ?? new ShapeCorrection[0])
                {
                    if (shape.indices == null || shape.weights == null || shape.indices.Length != shape.weights.Length || shape.indices.Length == 0) throw new InvalidDataException("H shape projection missing");
                    float value = 0;
                    if (!_partners.TryGetValue(definition.shapeRole, out var shapeActor)) throw new InvalidDataException("Correction shape participant missing: " + definition.shapeRole);
                    for (int i = 0; i < shape.indices.Length; ++i) value += shapeActor.GetShapeBodyValue(shape.indices[i]) * shape.weights[i];
                    position += Interpolate(shape.smallPosition, shape.mediumPosition, shape.largePosition, value);
                    angles += InterpolateAngles(shape.smallRotation, shape.mediumRotation, shape.largeRotation, value);
                }
                if (_body.TryGetValue(definition, out var body)) { body.pos = position; body.ang = angles; }
                else { _targetCorrections[definition].pos = position; _targetCorrections[definition].ang = angles; }
            }
        }
        private static Vector3 InterpolateAngles(float[] small, float[] medium, float[] large, float value)
        {
            if (small == null || medium == null || large == null || small.Length != 3 || medium.Length != 3 || large.Length != 3) throw new InvalidDataException("H angle correction coordinates missing");
            var a = value < .5f ? small : medium; var b = value < .5f ? medium : large;
            float t = Mathf.Clamp01(value < .5f ? value * 2 : (value - .5f) * 2);
            return new Vector3(Mathf.LerpAngle(a[0], b[0], t), Mathf.LerpAngle(a[1], b[1], t), Mathf.LerpAngle(a[2], b[2], t));
        }
        private static Vector3 Interpolate(float[] small, float[] medium, float[] large, float value)
        {
            if (small == null || medium == null || large == null || small.Length != 3 || medium.Length != 3 || large.Length != 3) throw new InvalidDataException("H correction S/M/L coordinates missing");
            var a = value < .5f ? small : medium; var b = value < .5f ? medium : large;
            var blend = value < .5f ? Mathf.Clamp01(value * 2) : Mathf.Clamp01((value - .5f) * 2);
            return Vector3.Lerp(new Vector3(a[0], a[1], a[2]), new Vector3(b[0], b[1], b[2]), blend);
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            foreach (var restore in Enumerable.Reverse(_restore)) try { restore(); } catch (Exception e) { _failure?.Invoke("H contact restore: " + e.GetBaseException().Message); }
            _restore.Clear();
            foreach (var component in Enumerable.Reverse(_addedComponents)) if (component != null) UnityEngine.Object.Destroy(component);
            _addedComponents.Clear();
            _body.Clear();
            _targetCorrections.Clear();
            foreach (var created in _created) if (created != null)
            {
                var proxy = created.GetComponent<DedicatedContactProxyTag>();
                if (proxy != null && --proxy.References > 0) continue;
                // Detach before deferred Destroy so a same-event state change
                // cannot capture an anchor already scheduled for destruction.
                created.transform.SetParent(null, false); UnityEngine.Object.Destroy(created);
            }
            _created.Clear(); _frames.Clear();
        }
    }
    internal sealed class DedicatedContactProxyTag : MonoBehaviour { internal int References; }
}
