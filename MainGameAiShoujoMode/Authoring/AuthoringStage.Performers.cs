using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapEnvironmentRuntime;
using UnityEngine;

namespace MainGameAiShoujoMode.Authoring
{
    internal sealed partial class AuthoringStage
    {
        private sealed class Extra
        {
            internal bool visible, animatorEnabled;
            internal byte[] clothes;
            internal RuntimeAnimatorController controller;
            internal AnimatorStateInfo animation;
            internal float speed;
        }
        private readonly Dictionary<ChaControl, Extra> extra = new Dictionary<ChaControl, Extra>();
        private readonly Dictionary<ChaControl, Animation> legacy = new Dictionary<ChaControl, Animation>();
        private readonly List<AnimationClip> clonedClips = new List<AnimationClip>();
        private readonly Dictionary<string, AssetBundle> localBundles = new Dictionary<string, AssetBundle>();
        private readonly Dictionary<Transform, Tuple<Vector3, Quaternion, bool>> itemSnapshots = new Dictionary<Transform, Tuple<Vector3, Quaternion, bool>>();
        private readonly List<GameObject> spawnedItems = new List<GameObject>();
        private readonly Dictionary<string, GameObject> itemInstances = new Dictionary<string, GameObject>();
        private readonly Dictionary<ChaControl, float> playbackSpeeds = new Dictionary<ChaControl, float>();
        private SandboxFreeCamera walking;
        private GameObject walkingRoot;
        private HotelWalkingContext walkingContext;
        partial void CaptureExtra(ChaControl actor)
        {
            if (extra.ContainsKey(actor)) return;
            extra.Add(actor, new Extra { visible = actor.visibleAll, clothes = actor.fileStatus.clothesState.ToArray(), controller = actor.animBody?.runtimeAnimatorController,
                animatorEnabled = actor.animBody != null && actor.animBody.enabled, speed = actor.animBody == null ? Numbers.One : actor.animBody.speed,
                animation = actor.animBody == null || actor.animBody.runtimeAnimatorController == null ? default(AnimatorStateInfo) : actor.animBody.GetCurrentAnimatorStateInfo(default(int)) });
        }
        partial void ApplyCondition(ChaControl actor, ActorCondition condition)
        {
            if (condition == null) return; actor.visibleAll = condition.visible;
            if (condition.clothing == "preserve") return;
            byte value = (byte)(condition.clothing == "nude" ? AuthoringDefaults.Current.two : condition.clothing == "half" ? Numbers.One : default(int));
            for (int i = default(int); i < actor.fileStatus.clothesState.Length; i++) actor.SetClothesState(i, value, true);
        }
        internal void Perform(MotionCue cue) { cue.Validate("animation"); ApplyPlacementMotion(Actor(cue.characterId), cue); }
        partial void ApplyPlacementMotion(ChaControl actor, MotionCue cue)
        {
            if (actor?.animBody == null) throw new InvalidOperationException("この人物にはアニメを適用できません。");
            playbackSpeeds[actor] = cue.speed;
            if (legacy.TryGetValue(actor, out var previous) && previous != null) { previous.Stop(); UnityEngine.Object.Destroy(previous); legacy.Remove(actor); }
            if (cue.source == "controller")
            {
                var controller = new AssetBundleData(cue.bundle, cue.asset).GetAsset<RuntimeAnimatorController>();
                if (controller == null) throw new FileNotFoundException("アニメ用Controllerがありません：" + cue.asset);
                actor.animBody.enabled = true; actor.animBody.runtimeAnimatorController = controller; actor.animBody.speed = cue.speed;
                if (!actor.animBody.HasState(default(int), Animator.StringToHash(cue.state))) throw new InvalidDataException("Controllerに動作名がありません：" + cue.state);
                actor.animBody.CrossFadeInFixedTime(cue.state, cue.blendSeconds, default(int), cue.startSeconds); return;
            }
            AnimationClip clip;
            if (Path.IsPathRooted(cue.bundle))
            {
                if (!localBundles.TryGetValue(cue.bundle, out var bundle)) { bundle = AssetBundle.LoadFromFile(cue.bundle); if (bundle == null) throw new FileNotFoundException("アニメBundleを読めません：" + cue.bundle); localBundles.Add(cue.bundle, bundle); }
                clip = bundle.LoadAsset<AnimationClip>(cue.asset);
            }
            else clip = new AssetBundleData(cue.bundle, cue.asset).GetAsset<AnimationClip>();
            if (clip == null) throw new FileNotFoundException("アニメ素材がありません：" + cue.asset);
            if (clip.isHumanMotion) throw new InvalidDataException("HumanoidクリップはController形式で指定してください。変換済み生活動作はクリップ形式で使えます。");
            var copy = UnityEngine.Object.Instantiate(clip); copy.legacy = true; copy.wrapMode = cue.loop ? WrapMode.Loop : WrapMode.ClampForever; clonedClips.Add(copy);
            actor.animBody.enabled = false;
            var animation = actor.animBody.gameObject.AddComponent<Animation>(); animation.playAutomatically = false; animation.AddClip(copy, cue.asset);
            animation[cue.asset].speed = cue.speed; animation[cue.asset].time = cue.startSeconds;
            animation.CrossFade(cue.asset, cue.blendSeconds); legacy.Add(actor, animation);
        }
        internal void ConfigureWalking(Parameters settings)
        {
            if (walking != null) return;
            var initial = Capture(); walkingRoot = new GameObject("Event walking player"); walkingRoot.transform.SetParent(owner.transform, false);
            walkingContext = new HotelWalkingContext(settings) { WalkingReady = true };
            walking = walkingRoot.AddComponent<SandboxFreeCamera>(); walking.Initialize(walkingContext);
            walking.SetViewPose(Vector(initial.position), Quaternion.Euler(Vector(initial.rotation)), initial.fieldOfView);
            if (camera != null) camera.enabled = false; SpecialCamera = walking.ViewCamera;
        }
        partial void CameraApplied(Camera target) { if (walking != null && walking.enabled && walking.ViewCamera == target) walking.SetViewPose(target.transform.position, target.transform.rotation, target.fieldOfView); }
        internal void SetDialogueInput(bool active) { walkingContext?.SetPanel(active); walking?.SetCursorReleased(active); }
        internal void AdoptNative(Camera native, string femaleId, string maleId, ChaControl[] cast)
        {
            if (walking != null) walking.enabled = false; if (SpecialCamera != null) SpecialCamera.enabled = false; if (camera != null) camera.enabled = false;
            SpecialCamera = native;
            var female = cast.FirstOrDefault(a => a.chaFile.parameter.sex == Numbers.One); var male = cast.FirstOrDefault(a => a.chaFile.parameter.sex == default(int));
            if (female != null) assigned[femaleId] = female; if (!string.IsNullOrEmpty(maleId) && male != null) assigned[maleId] = male;
            foreach (var actor in cast) if (!actors.ContainsKey(actor)) { actors.Add(actor, new ActorState(actor, false)); CaptureExtra(actor); }
        }
        internal void PlaceItems(IEnumerable<ItemPlacement> placements)
        {
            foreach (var item in placements ?? Enumerable.Empty<ItemPlacement>())
            {
                GameObject target;
                if (item.source == "map") { var t = MapRoot == null ? null : MapRoot.transform.Find(item.path); if (t == null) throw new InvalidDataException("マップ内アイテムがありません：" + item.name + " / " + item.path); target = t.gameObject; if (!itemSnapshots.ContainsKey(t)) itemSnapshots.Add(t, Tuple.Create(t.position, t.rotation, target.activeSelf)); }
                else if (!itemInstances.TryGetValue(item.id, out target) || target == null) { var prefab = new AssetBundleData(item.bundle, item.asset).GetAsset<GameObject>(); if (prefab == null) throw new FileNotFoundException("アイテム素材がありません：" + item.name); target = UnityEngine.Object.Instantiate(prefab); target.name = item.name; spawnedItems.Add(target); itemInstances[item.id] = target; }
                target.transform.SetPositionAndRotation(Vector(item.position), Quaternion.Euler(Vector(item.rotation))); target.SetActive(item.visible);
            }
        }
        internal IEnumerable<KeyValuePair<string, string>> MapItems()
        {
            if (MapRoot == null) throw new InvalidOperationException("先に場面を試し再生してください。読み込まれたマップの実物から選べます。");
            foreach (var t in MapRoot.GetComponentsInChildren<Transform>(true).Where(t => t != MapRoot.transform && t.GetComponent<Renderer>() != null && t.GetComponentInParent<ChaControl>() == null))
            {
                var parts = new List<string>(); for (var p = t; p != MapRoot.transform; p = p.parent) parts.Insert(default(int), p.name);
                yield return new KeyValuePair<string, string>(string.Join("/", parts), t.name);
            }
        }
        partial void PauseExtras(bool paused)
        {
            foreach (var actor in extra.Keys) if (actor != null && actor.animBody != null) actor.animBody.speed = paused ? default(float) : playbackSpeeds.TryGetValue(actor, out var speed) ? speed : extra[actor].speed;
            foreach (var pair in legacy) if (pair.Value != null) foreach (AnimationState state in pair.Value) state.speed = paused ? default(float) : playbackSpeeds[pair.Key];
        }
        partial void RestoreExtras()
        {
            foreach (var pair in legacy) if (pair.Value != null) { pair.Value.Stop(); UnityEngine.Object.Destroy(pair.Value); } legacy.Clear();
            foreach (var pair in extra)
            {
                var actor = pair.Key; if (actor == null) continue; var saved = pair.Value; actor.visibleAll = saved.visible;
                for (int i = default(int); i < saved.clothes.Length; i++) actor.SetClothesState(i, saved.clothes[i], true);
                if (actor.animBody != null) { actor.animBody.runtimeAnimatorController = saved.controller; actor.animBody.enabled = saved.animatorEnabled; actor.animBody.speed = saved.speed; if (saved.controller != null) actor.animBody.Play(saved.animation.fullPathHash, default(int), saved.animation.normalizedTime); }
            }
            extra.Clear(); foreach (var clip in clonedClips) if (clip != null) UnityEngine.Object.Destroy(clip); clonedClips.Clear();
            playbackSpeeds.Clear();
            foreach (var bundle in localBundles.Values) bundle.Unload(false); localBundles.Clear();
            foreach (var pair in itemSnapshots) if (pair.Key != null) { pair.Key.SetPositionAndRotation(pair.Value.Item1, pair.Value.Item2); pair.Key.gameObject.SetActive(pair.Value.Item3); } itemSnapshots.Clear();
            foreach (var item in spawnedItems) if (item != null) UnityEngine.Object.Destroy(item); spawnedItems.Clear(); itemInstances.Clear();
            if (walkingRoot != null) UnityEngine.Object.Destroy(walkingRoot); walkingRoot = null; walking = null; walkingContext = null;
        }
    }
}
