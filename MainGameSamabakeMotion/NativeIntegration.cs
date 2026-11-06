using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace MainGameSamabakeMotion
{
    internal sealed class NativeSnapshot
    {
        internal ChaControl Actor;
        internal RuntimeAnimatorController Original, Imported;
        internal float Speed;
        internal bool Visible;
        internal bool Changed;
        internal bool AnimatorEnabled;
        internal AnimatorCullingMode CullingMode;
        internal ActorDefinition Definition;
        internal AnimatorStateInfo[] States;
        internal IContactRig Contact;
        internal float[] Layers;
        internal readonly Dictionary<Behaviour, bool> Solvers = new Dictionary<Behaviour, bool>();
        internal readonly Dictionary<Transform, NativeTransform> Bones = new Dictionary<Transform, NativeTransform>();
    }
    internal struct NativeTransform { internal Vector3 Position, Scale; internal Quaternion Rotation; }
    internal sealed class NativeIntegration
    {
        private readonly Func<bool> _allowed;
        private readonly Func<bool> _diagnostics;
        private readonly Action<string> _log;
        private HSceneProc _scene;
        private readonly List<NativeSnapshot> _actors = new List<NativeSnapshot>();
        private bool _active;
        private PlaybackPose _definition;
        private HSceneProc.AnimationListInfo _captured;
        private HandCtrl.MotionEnableState _previousHandState;
        private HandCtrl _hand;
        internal NativeIntegration(Func<bool> allowed, Func<bool> diagnostics, Action<string> log)
        { _allowed = allowed; _diagnostics = diagnostics; _log = log; }
        internal bool Owns(Animator animator) => _active && animator != null && _actors.Any(s => s.Imported != null && s.Actor != null && s.Actor.animBody == animator && animator.runtimeAnimatorController == s.Imported);
        internal bool Owns(ChaControl actor) => actor != null && Owns(actor.animBody);
        internal void BeginSession(HSceneProc scene)
        {
            EndSession();
            if (scene == null || !_allowed()) return;
            _scene = scene;
            try
            {
                // StartH is delivered after scene initialization. Also handle an imported initial pose.
                if (ZipmodPose.IsImported(scene.flags.nowAnimationInfo))
                {
                    CaptureBase(scene, scene.flags.nowAnimationInfo, true);
                    AfterChange(scene, scene.flags.nowAnimationInfo);
                }
                if (_diagnostics()) _log("ZIPMOD_SESSION_READY registration=AnimationLoader");
            }
            catch (Exception ex) { if (_diagnostics()) _log("NATIVE_REGISTER_FAILED " + ex); EndSession(); }
        }
        internal void BeforeChange() { Restore(); }
        internal void CaptureBase(HSceneProc scene, HSceneProc.AnimationListInfo pose, bool alreadySwapped = false)
        {
            if (!_allowed() || scene != _scene || !ZipmodPose.IsImported(pose)) return;
            try
            {
                Restore();
                _definition = ZipmodPose.Find(pose);
                var females = AccessTools.Field(typeof(HSceneProc), "lstFemale").GetValue(scene) as IList;
                foreach (var definition in _definition.actors)
                {
                    bool isMale = definition.slot == "male0";
                    int index = definition.slot == "female1" ? 1 : 0;
                    var actor = isMale ? AccessTools.Field(typeof(HSceneProc), "male").GetValue(scene) as ChaControl
                        : females != null && females.Count > index ? females[index] as ChaControl : null;
                    if (actor == null || actor.sex != (isMale ? 0 : 1)) throw new InvalidOperationException("必要な再生キャラがいません: " + definition.slot);
                    CaptureActor(actor, definition, alreadySwapped);
                }
                _captured = pose;
            }
            catch (Exception ex) { if (_diagnostics()) _log("ZIPMOD_CAPTURE_FAILED " + ex); Restore(); }
        }
        internal void AfterChange(HSceneProc scene, HSceneProc.AnimationListInfo pose)
        {
            if (!_allowed() || scene != _scene || pose != _captured || !ZipmodPose.IsImported(pose)) return;
            try
            {
                foreach (var snapshot in _actors) { snapshot.Imported = snapshot.Actor.animBody.runtimeAnimatorController; snapshot.Changed = true; }
                string state = scene.flags.nowAnimStateName;
                if (!_definition.states.Contains(state)) throw new InvalidDataException("初期状態の対応がありません: " + state);
                // Resolve through Sideloader/AnimationLoader's own AssetBundleManager, never load a second loose copy.
                var controllers = _actors.ToDictionary(a => a.Definition.slot, a => LoadController(a.Definition.path, a.Definition.controller));
                _active = true;
                foreach (var snapshot in _actors)
                    InstallActor(snapshot, _actors.Single(a => a.Definition.slot == snapshot.Definition.partner).Actor, controllers[snapshot.Definition.slot], state);
                var contacts = _actors.Where(a => a.Contact != null).ToDictionary(a => a.Definition.slot, a => a.Contact);
                foreach (var contact in contacts.Values) contact.Configure(contacts);
                ApplyContacts(state);
                _hand = AccessTools.Field(typeof(HSceneProc), "hand").GetValue(scene) as HandCtrl;
                _previousHandState = _hand?.nowMES;
                RestrictTouch(_hand, state);
                foreach (var actor in _actors) actor.Actor.animBody.Update(0f);
                if (_diagnostics()) _log("ZIPMOD_SELECTED studioId=" + _definition.studioId + " state=" + state + " actors=" + _actors.Count);
            }
            catch (Exception ex) { if (_diagnostics()) _log("NATIVE_APPLY_FAILED " + ex); Restore(); }
        }
        private static RuntimeAnimatorController LoadController(string path, string name)
        {
            var controller = AssetBundleManager.LoadAsset(path, name, typeof(RuntimeAnimatorController), null).GetAsset<RuntimeAnimatorController>();
            if (controller == null) throw new InvalidDataException("zipmodのControllerを開けません: " + name);
            return controller;
        }
        private void CaptureActor(ChaControl actor, ActorDefinition definition, bool alreadySwapped)
        {
            var animator = actor.animBody;
            if (animator == null) throw new InvalidOperationException("Animatorがありません");
            var original = animator.runtimeAnimatorController;
            if (alreadySwapped && original is AnimatorOverrideController outer) original = outer.runtimeAnimatorController;
            var snapshot = new NativeSnapshot { Actor = actor, Definition = definition, Original = original, Speed = animator.speed, Visible = actor.visibleAll,
                AnimatorEnabled = animator.enabled, CullingMode = animator.cullingMode };
            snapshot.States = Enumerable.Range(0, animator.layerCount).Select(animator.GetCurrentAnimatorStateInfo).ToArray();
            snapshot.Layers = Enumerable.Range(0, animator.layerCount).Select(animator.GetLayerWeight).ToArray();
            foreach (var bone in animator.GetComponentsInChildren<Transform>(true)) snapshot.Bones.Add(bone, new NativeTransform { Position = bone.localPosition, Rotation = bone.localRotation, Scale = bone.localScale });
            _actors.Add(snapshot);
        }
        private void InstallActor(NativeSnapshot snapshot, ChaControl partner, RuntimeAnimatorController controller, string state)
        {
            var actor = snapshot.Actor;
            var animator = actor.animBody;
            snapshot.Imported = controller;
            snapshot.Changed = true;
            snapshot.Contact = NativeContactRig.Capture(actor, snapshot.Definition.contact);
            foreach (var component in animator.GetComponentsInChildren<Behaviour>(true))
            {
                string type = component.GetType().FullName;
                if (type == "RootMotion.FinalIK.FullBodyBipedIK" || type == "Illusion.Component.Correct.FrameCorrect" || type == "Illusion.Component.Correct.IKCorrect")
                { snapshot.Solvers[component] = component.enabled; component.enabled = false; }
            }
            animator.runtimeAnimatorController = snapshot.Imported;
            // The standing-caress donor does not drive a male animation. Do not inherit
            // a disabled/culled Animator when taking ownership of that actor.
            animator.enabled = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (string required in _definition.states)
                if (!animator.HasState(0, Animator.StringToHash(required))) throw new InvalidDataException("KKSの進行用状態がありません: " + required);
            Set(animator, "height1", actor.GetShapeBodyValue(0));
            Set(animator, "partner_height", partner.GetShapeBodyValue(0));
            Set(animator, "Breast1", (actor.sex == 0 ? partner : actor).GetShapeBodyValue(4));
            Set(animator, "motion", _scene.flags.motion);
            Set(animator, "speed", _scene.flags.speed);
            Set(animator, "speedBody", 1f + _scene.flags.speed);
            animator.speed = 1f;
            actor.visibleAll = true;
            animator.Play(state, 0, 0f);
            if (_diagnostics()) _log("ZIPMOD_ACTOR slot=" + snapshot.Definition.slot + " controller=" + controller.name
                + " state=" + state + " previousEnabled=" + snapshot.AnimatorEnabled + " previousCulling=" + snapshot.CullingMode);
            actor.resetDynamicBoneAll = true;
            ApplyLayers(snapshot, state);
        }
        private static void ApplyLayers(NativeSnapshot snapshot, string state)
        {
            bool corrective = snapshot.Definition.correctiveStates != null && snapshot.Definition.correctiveStates.Contains(state);
            for (int layer = 1; layer < snapshot.Actor.animBody.layerCount; layer++) snapshot.Actor.animBody.SetLayerWeight(layer, corrective ? 1f : 0f);
        }
        private static void Set(Animator animator, string name, float value)
        {
            int hash = Animator.StringToHash(name);
            if (animator.parameters.Any(p => p.nameHash == hash && p.type == AnimatorControllerParameterType.Float)) animator.SetFloat(hash, value);
        }
        internal void AfterPlay(string state)
        {
            if (!_active || !_allowed()) return;
            // HActionBase.SetPlay skips a hidden male. Forward the native state event
            // explicitly to our male controller, independently of the donor's visibility.
            if (_definition.states.Contains(state))
                foreach (var snapshot in _actors)
                    if (snapshot.Definition.slot == "male0" && Owns(snapshot.Actor))
                        snapshot.Actor.animBody.Play(state, 0, 0f);
            RestrictTouch(_hand, state);
            foreach (var actor in _actors) ApplyLayers(actor, state);
            if (_definition.states.Contains(state))
                try { ApplyContacts(state); }
                catch (Exception ex) { if (_diagnostics()) _log("CONTACT_APPLY_FAILED " + ex); Restore(); }
            if (_diagnostics()) _log("NATIVE_STATE " + state);
        }
        private void ApplyContacts(string state)
        {
            foreach (var snapshot in _actors)
            {
                if (snapshot.Contact == null) continue;
                var partner = _actors.Single(a => a.Definition.slot == snapshot.Definition.partner);
                Set(snapshot.Actor.animBody, "height1", snapshot.Contact.Height);
                Set(snapshot.Actor.animBody, "partner_height", partner.Contact.Height);
                Set(snapshot.Actor.animBody, "Breast1", (snapshot.Actor.sex == 0 ? partner.Actor : snapshot.Actor).GetShapeBodyValue(4));
            }
            foreach (var snapshot in _actors) snapshot.Contact?.Apply(state);
        }
        internal void ShapeChanged(ChaControl actor)
        {
            if (!_active || !_allowed() || !Owns(actor)) return;
            try { ApplyContacts(_scene.flags.nowAnimStateName); }
            catch (Exception ex) { if (_diagnostics()) _log("CONTACT_APPLY_FAILED " + ex); Restore(); }
        }
        internal void RestrictTouch(HandCtrl hand, string state)
        {
            if (!_active || !_allowed() || _definition.mode != 0 || hand == null || (hand != _hand && _hand != null)) return;
            var areas = new bool[7];
            if (!state.StartsWith("Orgasm", StringComparison.Ordinal) || state == "Orgasm_A")
                foreach (int area in _definition.touchAreas ?? new int[0]) if (area >= 0 && area < areas.Length) areas[area] = true;
            hand.nowMES = new HandCtrl.MotionEnableState { nameAnimation = state, isTouchAreas = areas, stateMotion = state == "Idle" ? 0 : 1 };
        }
        internal void Restore()
        {
            _active = false;
            _captured = null;
            _definition = null;
            foreach (var snapshot in _actors)
            {
                try { snapshot.Contact?.Restore(); }
                catch (Exception ex) { if (_diagnostics()) _log("CONTACT_RESTORE_FAILED " + ex); }
                snapshot.Contact = null;
                var actor = snapshot.Actor;
                if (actor != null && actor.animBody != null && snapshot.Changed && actor.animBody.runtimeAnimatorController == snapshot.Imported)
                {
                    foreach (var bone in snapshot.Bones) if (bone.Key != null) { bone.Key.localPosition = bone.Value.Position; bone.Key.localRotation = bone.Value.Rotation; bone.Key.localScale = bone.Value.Scale; }
                    var animator = actor.animBody;
                    animator.runtimeAnimatorController = snapshot.Original; animator.speed = snapshot.Speed;
                    if (snapshot.Original != null)
                        for (int layer = 0; layer < Math.Min(animator.layerCount, snapshot.States.Length); layer++)
                        {
                            var state = snapshot.States[layer];
                            animator.SetLayerWeight(layer, snapshot.Layers[layer]);
                            if (state.fullPathHash != 0) animator.Play(state.fullPathHash, layer, Mathf.Repeat(state.normalizedTime, 1f));
                        }
                    actor.visibleAll = snapshot.Visible; actor.resetDynamicBoneAll = true;
                    animator.cullingMode = snapshot.CullingMode;
                    animator.enabled = snapshot.AnimatorEnabled;
                }
                foreach (var solver in snapshot.Solvers) if (solver.Key != null) solver.Key.enabled = solver.Value;
            }
            _actors.Clear();
            if (_hand != null && _previousHandState != null) _hand.nowMES = _previousHandState;
            _hand = null; _previousHandState = null;
        }
        internal void EndSession()
        {
            Restore(); _scene = null;
        }
    }
}
