using System;
using System.Collections;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using MainGameAiShoujoMode.Life;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    // Finite trial performance. Unity navigation owns travel; one scheduled completion owns the result.
    internal sealed class AiLifeActivityMotion : IDisposable
    {
        private readonly AiAnimationTrial _actor;
        private readonly AiLifeActivityCatalog _settings;
        private GameObject _marker, _food;
        private Coroutine _timer;
        private bool _disposed;
        private Animator _body;
        private RuntimeAnimatorController _originalController;
        private AnimatorOverrideController _override;
        private readonly Dictionary<string, AssetBundle> _bundles = new Dictionary<string, AssetBundle>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AnimationClip> _clips = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
        private AiLifeAnimationStep[] _steps;
        private Action[] _restoreBones;
        private bool _fireEvents, _rootMotion;
        private float _speed;
        private AnimatorCullingMode _culling;
        internal AiLifeActivityMotion(AiAnimationTrial actor, AiLifeActivityCatalog settings, AiLifeActivity activity, bool marker=true)
        {
            _actor = actor; _settings = settings;
            try
            {
                if(!marker)return;
                _marker = Primitive("仮の" + (activity.kind == "motion" ? activity.animationLabel : settings.Find(activity.kind).label) + "地点", settings.markerSize);
                _marker.transform.position = new Vector3(activity.x, activity.y, activity.z) + Vector3.up * settings.markerHeight;
            }
            catch { Dispose(); throw; }
        }
        private GameObject Primitive(string name, float size)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Cube); value.name = name;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(value, _actor.Root.gameObject.scene);
            value.transform.localScale = Vector3.one * size;
            var collider = value.GetComponent<Collider>(); collider.enabled = false; UnityEngine.Object.Destroy(collider);
            return value;
        }
        internal void Begin(AiLifeActivity activity, Action finished, Action<Exception> failed)
        {
            if (_disposed || _actor.CurrentActor?.animBody == null) throw new InvalidOperationException("生活行動の女の子がいません。");
            _actor.ApplyLifeAction("idle", null);
            var definition = _settings.Find(activity.kind == "motion" ? "rest" : activity.kind);
            var action = activity.animationActionId ?? definition.animationActionId;
            if (action != null) PrepareNativeMotion(action);
            if (activity.kind == "eat")
            {
                var hand = _actor.CurrentActor.animBody.GetComponentsInChildren<Transform>(true).FirstOrDefault(v => v.name == _settings.handBone);
                if (hand == null) throw new InvalidOperationException("食べ物の仮小物を持たせる右手が見つかりません。");
                _food = Primitive("食べ物の仮小物（未変換）", _settings.foodSize);
                _food.transform.SetParent(hand, false); _food.transform.localPosition = Vector3.zero; _food.transform.localRotation = Quaternion.identity;
            }
            _timer = _actor.StartCoroutine(Perform(definition.seconds, finished, failed));
        }
        private void PrepareNativeMotion(string action)
        {
            _steps = _settings.Animations.Sequence(action);
            foreach (var step in _steps)
            {
                if (!_bundles.TryGetValue(step.Clip.bundle, out var bundle))
                {
                    bundle = AssetBundle.LoadFromFile(Path.Combine(_actor.ContentDirectory, "LifeAnimations", step.Clip.bundle));
                    if (bundle == null) throw new InvalidDataException("生活動作のBundleを読み込めません。");
                    _bundles.Add(step.Clip.bundle, bundle);
                }
                var clip = bundle.LoadAsset<AnimationClip>(step.Clip.asset);
                if (clip == null) throw new InvalidDataException("生活動作のクリップがありません。");
                _clips[step.Clip.id] = clip;
            }
            _body = _actor.CurrentActor.animBody; _originalController = _body.runtimeAnimatorController;
            if (_originalController == null || _originalController.animationClips.Count(v => v.name == "Idle") != _settings.one) throw new InvalidDataException("生活動作を差し込む待機クリップがありません。");
            _restoreBones = _body.GetComponentsInChildren<Transform>(true).Select(bone =>
            {
                var position = bone.localPosition; var rotation = bone.localRotation; var scale = bone.localScale;
                return (Action)(() => { if (bone != null) { bone.localPosition = position; bone.localRotation = rotation; bone.localScale = scale; } });
            }).ToArray();
            _fireEvents = _body.fireEvents; _rootMotion = _body.applyRootMotion; _speed = _body.speed; _culling = _body.cullingMode;
            _override = new AnimatorOverrideController(_originalController);
            _body.fireEvents = false; _body.applyRootMotion = false; _body.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _body.speed = _settings.one;
        }
        private void PlayStep(AiLifeAnimationStep step)
        {
            if (_body == null || _actor.CurrentActor?.animBody != _body) throw new InvalidOperationException("生活動作の対象が失われました。");
            _override["Idle"] = _clips[step.Clip.id];
            _body.runtimeAnimatorController = _override; _body.Rebind();
            var layer = default(int); _body.Play(_body.GetLayerName(layer) + ".Idle", layer, default(float)); _body.Update(default(float));
        }
        private IEnumerator Perform(float seconds, Action finished, Action<Exception> failed)
        {
            if (_steps == null) yield return new WaitForSeconds(seconds);
            else foreach (var step in _steps)
            {
                Exception failure = null;
                try { PlayStep(step); } catch (Exception error) { failure = error; }
                if (failure != null) { _timer = null; failed(failure); yield break; }
                yield return new WaitForSeconds(step.Phase == "main" ? Math.Max(seconds, step.Clip.duration) : step.Clip.duration);
                if (_disposed) yield break;
            }
            _timer = null;
            if (!_disposed) finished();
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            if (_timer != null && _actor != null) _actor.StopCoroutine(_timer);
            _timer = null;
            if (_food != null) UnityEngine.Object.Destroy(_food);
            if (_marker != null) UnityEngine.Object.Destroy(_marker);
            _food = null; _marker = null;
            try
            {
                if (_body != null && _override != null && _body.runtimeAnimatorController == _override)
                {
                    _body.fireEvents = false; _body.runtimeAnimatorController = _originalController;
                    _body.applyRootMotion = _rootMotion; _body.speed = _speed; _body.cullingMode = _culling; _body.Rebind();
                    if (_restoreBones != null) foreach (var restore in _restoreBones) restore();
                    _body.Update(default(float)); _body.fireEvents = _fireEvents;
                }
            }
            finally
            {
                if (_override != null) UnityEngine.Object.Destroy(_override);
                foreach (var bundle in _bundles.Values) if (bundle != null) bundle.Unload(true);
                _bundles.Clear(); _clips.Clear(); _steps = null;
                _override = null; _body = null;
            }
        }
    }
}
