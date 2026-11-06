using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using H;
using Illusion.Component.Correct;
using Illusion.Component.Correct.Process;
using RootMotion.FinalIK;
using UnityEngine;

namespace MainGameAiShoujoH.Integration
{
    // Capture-only counterpart of KksOnePosePlayback's native snapshots. No
    // Animator evaluation, playback, IK creation, asset load or actor creation.
    internal sealed class NativeKksActorBodyLease : IDisposable
    {
        readonly List<Action> restore = new List<Action>();
        readonly List<Action> restoreBones = new List<Action>();
        readonly List<Action> restoreEnabled = new List<Action>();
        readonly List<Behaviour> drivers = new List<Behaviour>();
        bool disposed;

        internal NativeKksActorBodyLease(ChaControl actor)
        {
            if (actor == null || actor.animBody == null) throw new InvalidDataException("Borrowed KKS actor body is unavailable.");
            var animator = actor.animBody;
            var controller = animator.runtimeAnimatorController;
            var events = animator.fireEvents; var rootMotion = animator.applyRootMotion;
            var speed = animator.speed; var culling = animator.cullingMode; var update = animator.updateMode;
            CaptureEnabled(animator);
            restore.Add(() =>
            {
                if (actor == null || actor.animBody != animator) throw new InvalidOperationException("Borrowed KKS Animator identity changed.");
                animator.runtimeAnimatorController = controller;
            });
            foreach (var parameter in animator.parameters)
            {
                int hash = parameter.nameHash;
                if (parameter.type == AnimatorControllerParameterType.Float)
                { float value = animator.GetFloat(hash); restore.Add(() => animator.SetFloat(hash, value)); }
                else if (parameter.type == AnimatorControllerParameterType.Int)
                { int value = animator.GetInteger(hash); restore.Add(() => animator.SetInteger(hash, value)); }
                else if (parameter.type == AnimatorControllerParameterType.Bool)
                { bool value = animator.GetBool(hash); restore.Add(() => animator.SetBool(hash, value)); }
                // Unity has no API to read a pending Trigger. Do not synthesize it.
            }
            for (int index = default(int); index < animator.layerCount; ++index)
            {
                int layer = index; float weight = animator.GetLayerWeight(layer);
                var state = animator.GetCurrentAnimatorStateInfo(layer);
                restore.Add(() => animator.SetLayerWeight(layer, weight));
                restore.Add(() =>
                {
                    if (state.fullPathHash == default(int)) return;
                    if (!animator.HasState(layer, state.fullPathHash)) throw new InvalidOperationException("Borrowed KKS walking state disappeared.");
                    animator.Play(state.fullPathHash, layer, state.normalizedTime);
                });
            }
            restore.Add(() => animator.fireEvents = events);
            restore.Add(() => animator.applyRootMotion = rootMotion);
            restore.Add(() => animator.speed = speed);
            restore.Add(() => animator.cullingMode = culling);
            restore.Add(() => animator.updateMode = update);
            var solver = animator.GetComponent<FullBodyBipedIK>();
            if (solver != null)
            {
                CaptureEnabled(solver);
                float weight = solver.solver.IKPositionWeight;
                restore.Add(() => solver.solver.IKPositionWeight = weight);
                foreach (var pair in MotionIK.IKTargetPair.GetPairs(solver.solver))
                {
                    var effector = pair.effector; var bend = pair.bend;
                    var target = effector.target; var positionWeight = effector.positionWeight; var rotationWeight = effector.rotationWeight;
                    var position = effector.position; var rotation = effector.rotation;
                    var goal = bend.bendGoal; var bendWeight = bend.weight;
                    restore.Add(() => effector.target = target);
                    restore.Add(() => effector.positionWeight = positionWeight);
                    restore.Add(() => effector.rotationWeight = rotationWeight);
                    restore.Add(() => effector.position = position);
                    restore.Add(() => effector.rotation = rotation);
                    restore.Add(() => bend.bendGoal = goal);
                    restore.Add(() => bend.weight = bendWeight);
                }
            }
            foreach (var data in animator.GetComponentsInChildren<BaseData>(true))
            {
                var value = data; var bone = data.bone; var position = data.pos; var rotation = data.rot;
                restore.Add(() => value.bone = bone); restore.Add(() => value.pos = position); restore.Add(() => value.rot = rotation);
            }
            foreach (var process in animator.GetComponentsInChildren<BaseProcess>(true)) CaptureEnabled(process);
            foreach (var correct in animator.GetComponentsInChildren<BaseCorrect>(true))
            {
                CaptureEnabled(correct);
                // Native loaders may replace/clear entries, not just edit them.
                var list = (object)correct.list as IList;
                if (list == null) throw new NotSupportedException("Native correction list snapshot contract differs.");
                var entries = list.Cast<object>().ToArray();
                restore.Add(() =>
                {
                    if (!ReferenceEquals((object)correct.list, list)) throw new InvalidOperationException("Native correction list identity changed.");
                    if (list.IsFixedSize)
                    {
                        if (list.Count != entries.Length) throw new InvalidOperationException("Native correction list size changed.");
                        for (int i = default(int); i < entries.Length; ++i) list[i] = entries[i];
                    }
                    else { list.Clear(); foreach (var entry in entries) list.Add(entry); }
                });
                foreach (var info in correct.list)
                {
                    var value = info; var bone = info.bone; var position = info.pos; var rotation = info.rot; bool active = info.enabled;
                    restore.Add(() => value.bone = bone); restore.Add(() => value.pos = position);
                    restore.Add(() => value.rot = rotation); restore.Add(() => value.enabled = active);
                }
            }
            // Native YureCtrl.Active changes only these drivers' enabled states;
            // YureCtrl.Shape changes the bust mask through DisableShapeBodyID.
            if (actor.dictDynamicBoneBust != null)
                foreach (var bone in actor.dictDynamicBoneBust.Values.Where(value => value != null)) CaptureEnabled(bone);
            foreach (var behaviour in animator.GetComponentsInChildren<Behaviour>(true)
                .Where(value => value.GetType().Name.StartsWith("DynamicBone", StringComparison.Ordinal))) CaptureEnabled(behaviour);
            var masks = (bool[,])actor.fileStatus.disableBustShapeMask.Clone();
            for (int side = default(int); side < masks.GetLength(default(int)); ++side)
                for (int index = default(int); index < masks.GetLength(typeof(bool[]).GetArrayRank()); ++index)
                { int s = side, i = index; restore.Add(() => actor.DisableShapeBodyID(s, i, masks[s, i])); }
            CaptureFace(actor);
            foreach (var bone in actor.GetComponentsInChildren<Transform>(true))
            {
                if (bone == actor.transform) continue; // ActorBinding owns actor root.
                var value = bone; var p = bone.localPosition; var r = bone.localRotation; var s = bone.localScale;
                restoreBones.Add(() => value.localPosition = p);
                restoreBones.Add(() => value.localRotation = r);
                restoreBones.Add(() => value.localScale = s);
            }
        }

        void CaptureEnabled(Behaviour driver)
        {
            if (drivers.Contains(driver)) return;
            drivers.Add(driver); bool enabled = driver.enabled;
            restoreEnabled.Add(() => driver.enabled = enabled);
        }
        void CaptureFace(ChaControl actor)
        {
            var status = actor.fileStatus;
            int eyes = status.eyesPtn, brows = status.eyebrowPtn, mouth = status.mouthPtn;
            bool blink = status.eyesBlink, highlight = status.hideEyesHighlight, shaking = status.eyesYure, mask = status.disableMouthShapeMask;
            float cheek = status.hohoAkaRate; byte tears = actor.tearsLv;
            restore.Add(() => actor.ChangeEyesPtn(eyes)); restore.Add(() => actor.ChangeEyebrowPtn(brows)); restore.Add(() => actor.ChangeMouthPtn(mouth));
            restore.Add(() => actor.ChangeEyesBlinkFlag(blink)); restore.Add(() => actor.HideEyeHighlight(highlight));
            restore.Add(() => actor.ChangeEyesShaking(shaking)); restore.Add(() => actor.DisableShapeMouth(mask));
            restore.Add(() => actor.ChangeHohoAkaRate(cheek)); restore.Add(() => actor.tearsLv = tears);
            var face = actor.objHead == null ? null : actor.objHead.GetComponent<FaceBlendShape>();
            if (face != null) CaptureEnabled(face);
            if (face != null)
                foreach (var control in new FBSBase[] { face.EyebrowCtrl, face.EyesCtrl, face.MouthCtrl }.Where(c => c != null))
                {
                    var value = control; float min = control.OpenMin, max = control.OpenMax, rate = control.FixedRate;
                    restore.Add(() => value.OpenMin = min); restore.Add(() => value.OpenMax = max); restore.Add(() => value.FixedRate = rate);
                }
            int eyesLook = status.eyesLookPtn, neckLook = status.neckLookPtn;
            var eyesCtrl = actor.eyeLookCtrl; var eyesTarget = eyesCtrl == null ? null : eyesCtrl.target;
            var neckCtrl = actor.neckLookCtrl; var neckTarget = neckCtrl == null ? null : neckCtrl.target;
            if (eyesCtrl != null) CaptureEnabled(eyesCtrl);
            if (neckCtrl != null) CaptureEnabled(neckCtrl);
            float neckRate = neckCtrl == null ? default(float) : neckCtrl.rate;
            restore.Add(() => actor.ChangeLookEyesPtn(eyesLook));
            restore.Add(() => actor.ChangeLookNeckPtn(neckLook, neckRate));
            restore.Add(() => { if (eyesCtrl != null) eyesCtrl.target = eyesTarget; });
            restore.Add(() => { if (neckCtrl != null) neckCtrl.target = neckTarget; });
            var lip = actor.asVoice; restore.Add(() => actor.SetLipSync(lip));
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            var errors = new List<Exception>();
            void Attempt(Action action) { try { action(); } catch (Exception error) { errors.Add(error); } }
            // Stop writers before restoring data, then restore exact local pose,
            // and finally restore the walking drivers' original enabled states.
            foreach (var driver in drivers) Attempt(() => driver.enabled = false);
            foreach (var action in restore) Attempt(action);
            foreach (var action in restoreBones) Attempt(action);
            foreach (var action in restoreEnabled) Attempt(action);
            restore.Clear(); restoreBones.Clear(); restoreEnabled.Clear(); drivers.Clear();
            if (errors.Count != default(int)) throw new AggregateException("Native KKS actor body restoration failed", errors);
        }
    }
}
