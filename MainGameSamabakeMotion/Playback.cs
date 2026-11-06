using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using CanonPlugins.Shared;

namespace MainGameSamabakeMotion
{
    internal sealed class ActorSnapshot
    {
        internal ChaControl Actor;
        internal RuntimeAnimatorController Original, Imported;
        internal float Speed, Time;
        internal int State;
        internal bool Visible;
        internal readonly Dictionary<Behaviour, bool> Solvers = new Dictionary<Behaviour, bool>();
        internal readonly List<KeyValuePair<Transform, PoseTransform>> Bones = new List<KeyValuePair<Transform, PoseTransform>>();
    }
    internal struct PoseTransform
    {
        internal Vector3 Position, Scale;
        internal Quaternion Rotation;
    }
    internal sealed class Playback
    {
        private readonly string _directory;
        private readonly List<AssetBundle> _bundles = new List<AssetBundle>();
        private readonly List<ActorSnapshot> _actors = new List<ActorSnapshot>();
        internal string Status = "停止中";
        internal float Speed = 1f, Pattern = 0.5f;
        internal bool Active => _actors.Count > 0;
        internal Playback(string directory) { _directory = directory; }
        internal bool Owns(ChaControl actor) => _actors.Any(s => s.Actor == actor && actor != null && actor.animBody != null && actor.animBody.runtimeAnimatorController == s.Imported);

        private static ChaControl Resolve(bool female)
        {
            var scene = PluginSessionGate.HScene;
            if (scene == null) return null;
            if (!female) return AccessTools.Field(typeof(HSceneProc), "male")?.GetValue(scene) as ChaControl;
            var actors = AccessTools.Field(typeof(HSceneProc), "lstFemale")?.GetValue(scene) as IList;
            return actors != null && actors.Count > 0 ? actors[0] as ChaControl : null;
        }
        internal void Play(MotionEntry entry, MotionState selected, bool female, bool pair)
        {
            Stop();
            if (!PluginSessionGate.Allowed || PluginSessionGate.HScene == null) { Status = "本編のHシーンで使用してください"; return; }
            try
            {
                var primary = female ? entry.female : entry.male;
                var partner = female ? entry.male : entry.female;
                var target = Resolve(female);
                var other = Resolve(!female);
                if (target == null || target.animBody == null) throw new InvalidOperationException("対象キャラがいません");
                Add(target, other, primary, selected);
                var corresponding = pair && partner != null ? partner.states.FirstOrDefault(s => s.name == selected.name && s.composite) : null;
                if (corresponding != null && selected.composite)
                {
                    if (other == null || other.animBody == null) throw new InvalidOperationException("同期する相手がいません");
                    Add(other, target, partner, corresponding);
                }
                foreach (var s in _actors) s.Actor.animBody.Update(0f);
                Status = entry.title + " ／ " + selected.label + (_actors.Count == 2 ? " ／ 男女同期" : female ? " ／ 女性" : " ／ 男性");
            }
            catch (Exception ex) { Stop(); Status = "再生失敗: " + ex.Message; }
        }
        private void Add(ChaControl actor, ChaControl partner, MotionActor source, MotionState state)
        {
            string path = Path.GetFullPath(Path.Combine(_directory, "Motions", source.file));
            string root = Path.GetFullPath(Path.Combine(_directory, "Motions")) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("モーションのパスが不正です");
            var bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null) throw new InvalidDataException("読み込み失敗: " + source.file);
            _bundles.Add(bundle);
            var controller = bundle.LoadAsset<RuntimeAnimatorController>(source.controller);
            if (controller == null) throw new InvalidDataException("Controllerがありません: " + source.controller);
            Animator animator = actor.animBody;
            var si = animator.GetCurrentAnimatorStateInfo(0);
            var snapshot = new ActorSnapshot { Actor = actor, Original = animator.runtimeAnimatorController, Imported = controller, Speed = animator.speed, State = si.fullPathHash, Time = si.normalizedTime, Visible = actor.visibleAll };
            _actors.Add(snapshot);
            // Disable the stock pose's IK/correction components once, retaining KKS shape and hand-size owners.
            foreach (var component in animator.GetComponentsInChildren<Behaviour>(true))
            {
                string type = component.GetType().FullName;
                if (type == "RootMotion.FinalIK.FullBodyBipedIK" || type == "Illusion.Component.Correct.FrameCorrect" || type == "Illusion.Component.Correct.IKCorrect")
                { snapshot.Solvers[component] = component.enabled; component.enabled = false; }
            }
            foreach (var bone in animator.GetComponentsInChildren<Transform>(true))
                snapshot.Bones.Add(new KeyValuePair<Transform, PoseTransform>(bone, new PoseTransform { Position = bone.localPosition, Rotation = bone.localRotation, Scale = bone.localScale }));
            actor.visibleAll = true;
            animator.runtimeAnimatorController = controller;
            Set(animator, "height1", actor.GetShapeBodyValue(0));
            Set(animator, "partner_height", partner != null ? partner.GetShapeBodyValue(0) : 0.5f);
            Set(animator, "Breast1", actor.GetShapeBodyValue(4));
            Set(animator, "speed", 1f);
            Set(animator, "hspeed", 1f);
            Set(animator, "motion", Pattern);
            animator.speed = Speed;
            if (!animator.HasState(0, Animator.StringToHash(state.name))) throw new InvalidDataException("Stateがありません: " + state.name);
            for (int layer = 1; layer < animator.layerCount; layer++)
            {
                animator.SetLayerWeight(layer, state.corrective ? 1f : 0f);
                if (state.corrective) animator.Play(state.name, layer, 0f);
            }
            animator.Play(state.name, 0, 0f);
            actor.resetDynamicBoneAll = true;
        }
        private static void Set(Animator animator, string name, float value)
        {
            int hash = Animator.StringToHash(name);
            if (animator.parameters.Any(p => p.nameHash == hash && p.type == AnimatorControllerParameterType.Float)) animator.SetFloat(hash, value);
        }
        internal void Adjust(float speed, float pattern)
        {
            Speed = speed; Pattern = pattern;
            foreach (var s in _actors)
                if (Owns(s.Actor)) { s.Actor.animBody.speed = speed; Set(s.Actor.animBody, "motion", pattern); }
        }
        internal void Stop()
        {
            foreach (var s in _actors)
            {
                if (s.Actor != null && s.Actor.animBody != null && s.Actor.animBody.runtimeAnimatorController == s.Imported)
                {
                    foreach (var b in s.Bones) if (b.Key != null) { b.Key.localPosition = b.Value.Position; b.Key.localRotation = b.Value.Rotation; b.Key.localScale = b.Value.Scale; }
                    var animator = s.Actor.animBody;
                    animator.runtimeAnimatorController = s.Original;
                    animator.speed = s.Speed;
                    if (s.Original != null && s.State != 0) animator.Play(s.State, 0, Mathf.Repeat(s.Time, 1f));
                    s.Actor.visibleAll = s.Visible;
                    s.Actor.resetDynamicBoneAll = true;
                }
                foreach (var solver in s.Solvers) if (solver.Key != null) solver.Key.enabled = solver.Value;
            }
            _actors.Clear();
            foreach (var bundle in _bundles) if (bundle != null) bundle.Unload(true);
            _bundles.Clear();
            Status = "停止中";
        }
    }
    internal static class PlaybackHooks
    {
        internal static void Install(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(HSceneProc), "ChangeAnimator"), prefix: new HarmonyMethod(typeof(PlaybackHooks), nameof(BeforeChange)));
            harmony.Patch(AccessTools.Method(typeof(HActionBase), "SetPlay"), prefix: new HarmonyMethod(typeof(PlaybackHooks), nameof(BeforePlay)));
            // Suspend the native H action while explicitly playing an imported motion.
            // This is a branch in existing callbacks, with no new Update/polling loop.
            foreach (var type in typeof(HActionBase).Assembly.GetTypes().Where(t => typeof(HActionBase).IsAssignableFrom(t)))
                foreach (var method in AccessTools.GetDeclaredMethods(type).Where(m => m.Name == "Proc" || m.Name == "LateProc"))
                    harmony.Patch(method, prefix: new HarmonyMethod(typeof(PlaybackHooks), nameof(BeforePlay)));
            foreach (var method in AccessTools.GetDeclaredMethods(typeof(ChaControl)).Where(m => m.Name == "setAnimatorParamFloat" || m.Name == "syncPlay" || m.Name == "AnimPlay"))
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(PlaybackHooks), nameof(BeforeActorWrite)));
        }
        private static void BeforeChange() { Plugin.Player?.Stop(); }
        private static bool BeforePlay() => Plugin.Player == null || !Plugin.Player.Active;
        private static bool BeforeActorWrite(ChaControl __instance) => Plugin.Player == null || !Plugin.Player.Owns(__instance);
    }
}
