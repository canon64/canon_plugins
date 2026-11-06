using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MainGameAiShoujoH.Contracts;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MainGameAiShoujoH.Motion
{
    // The original compiled state/layer/blend graphs run against converted clips.
    // A single Unity graph owns all controller clocks; no extra frame driver exists.
    internal sealed class NativeGraphPlayback : IDisposable
    {
        private PlayableGraph _graph;
        private readonly List<AnimatorControllerPlayable> _controllers = new List<AnimatorControllerPlayable>();
        private readonly List<NativeSourceRotationPlayback> _sourceRotations = new List<NativeSourceRotationPlayback>();
        private readonly List<string> _roles = new List<string>();
        private readonly List<AnimatorOverrideController> _overrides = new List<AnimatorOverrideController>();
        private readonly List<MotionEventReceiver> _receivers = new List<MotionEventReceiver>();
        private readonly List<Action> _restoreAvatars = new List<Action>();
        private readonly List<Avatar> _avatars = new List<Avatar>();
        private readonly SessionContext _context;
        private readonly Dictionary<string, ChaControl> _actors;
        private readonly Action<float> _cycle;
        private readonly Action<int> _sound;
        private string _state;
        private int _cycles;
        private object _playToken;
        private bool _disposed;
        internal NativeGraphPlayback(string contentDirectory, NativeGraphActor[] definitions, Dictionary<string, ChaControl> actors, Func<string, string, AssetBundle> load, SessionContext context, Action<float> cycle, Action<int> sound)
        {
            _context = context; _actors = actors; _cycle = cycle; _sound = sound;
            _graph = PlayableGraph.Create("AiHNativeGraph:" + context.Id);
            try
            {
                _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                foreach (var definition in definitions.OrderBy(d => d.role == "female" ? 0 : d.role == "female2" ? 1 : 2))
                {
                    var actor = actors[definition.role];
                    var animator = actor.animBody; var previousAvatar = animator.avatar;
                    foreach (var path in definition.bindingPaths ?? throw new InvalidDataException("Native clip binding-root manifest missing"))
                        if (animator.transform.Find(path) == null) throw new InvalidDataException("KKS native Animator root differs from converted clip binding root: " + definition.role + "/" + path);
                    var avatar = AvatarBuilder.BuildGenericAvatar(animator.gameObject, string.Empty);
                    if (avatar == null || !avatar.isValid) throw new InvalidDataException("Dedicated KKS body Generic Avatar invalid: " + definition.role);
                    _avatars.Add(avatar); animator.avatar = avatar;
                    _restoreAvatars.Add(() => { if (animator != null && animator.avatar == avatar) animator.avatar = previousAvatar; });
                    var pose = load(definition.bundle, definition.sha256).LoadAsset<RuntimeAnimatorController>(definition.controller);
                    var basis = load(definition.baseBundle, definition.baseSha256).LoadAsset<RuntimeAnimatorController>(definition.baseController);
                    if (pose == null || basis == null) throw new InvalidDataException("Converted native H controller missing: " + definition.role);
                    var replacement = new AnimatorOverrideController(basis); _overrides.Add(replacement);
                    var bindings = (definition.overrides ?? throw new InvalidDataException("Native external override manifest missing")).ToDictionary(c => c.key);
                    var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(); replacement.GetOverrides(overrides);
                    var assigned = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                    foreach (var item in overrides)
                    {
                        if (!bindings.TryGetValue(item.Key.name, out var binding)) throw new InvalidDataException("Native graph clip has no converted external override: " + item.Key.name);
                        var clip = load(binding.bundle, binding.sha256).LoadAsset<AnimationClip>(binding.asset);
                        if (clip == null || Math.Abs(clip.length - binding.duration) > .01f) throw new InvalidDataException("Native external override missing/duration mismatch: " + binding.asset);
                        assigned.Add(new KeyValuePair<AnimationClip, AnimationClip>(item.Key, clip));
                    }
                    if (assigned.Count != bindings.Count) throw new InvalidDataException("Native external override closure differs from compiled base graph");
                    replacement.ApplyOverrides(assigned);
                    var controller = AnimatorControllerPlayable.Create(_graph, replacement); _controllers.Add(controller);
                    _roles.Add(definition.role);
                    var output = AnimationPlayableOutput.Create(_graph, definition.role, actor.animBody); output.SetSourcePlayable(controller);
                    actor.animBody.fireEvents = true; actor.animBody.applyRootMotion = false; actor.animBody.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    var receiver = actor.animBody.gameObject.AddComponent<MotionEventReceiver>(); _receivers.Add(receiver);
                    // State callbacks are assigned with a fresh ownership token in Play.
                    float height = actors.TryGetValue(definition.role == "female2" ? "female2" : "female", out var heightActor) ? heightActor.GetShapeBodyValue(0) : actor.GetShapeBodyValue(0);
                    Set(controller, "height", height); Set(controller, "motion", 0); Set(controller, "breast", actor.GetShapeBodyValue(4)); Set(controller, "speed", 1);
                    if (definition.sourceRotations != null)
                    {
                        var source = new NativeSourceRotationPlayback(definition.role, definition.sourceRotations, actor, _graph, load, context, contentDirectory); _sourceRotations.Add(source);
                        Set(source.Controller, "height", height); Set(source.Controller, "motion", 0); Set(source.Controller, "breast", actor.GetShapeBodyValue(4)); Set(source.Controller, "speed", 1);
                    }
                }
            }
            catch { Dispose(); throw; }
        }
        private static void Set(AnimatorControllerPlayable controller, string name, float value)
        {
            for (int i = 0; i < controller.GetParameterCount(); ++i)
                if (controller.GetParameter(i).name == name && controller.GetParameter(i).type == AnimatorControllerParameterType.Float) { controller.SetFloat(name, value); return; }
        }
        internal void Play(MotionState definition, float speed)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NativeGraphPlayback));
            var token = new object(); _playToken = token;
            for (int receiverIndex = 0; receiverIndex < _receivers.Count; ++receiverIndex)
            {
                var receiver = _receivers[receiverIndex];
                receiver.Sound = cue => { if (!_disposed && ReferenceEquals(token, _playToken) && _context.IsCurrent()) _sound?.Invoke(cue); };
                receiver.Cycle = receiverIndex == 0 ? (Action)(() => { if (!_disposed && ReferenceEquals(token, _playToken)) OnCycle(); }) : null;
            }
            string state = definition.name;
            for (int actor = 0; actor < _controllers.Count; ++actor)
            {
                var controller = _controllers[actor];
                int hash = Animator.StringToHash(state);
                if (!controller.HasState(0, hash)) throw new InvalidDataException("Native compiled graph state missing: " + state);
                controller.Play(hash, 0, 0); Set(controller, "speed", speed);
                for (int layer = 1; layer < controller.GetLayerCount(); ++layer) controller.SetLayerWeight(layer, 0);
                foreach (var layer in definition.actors.Single(a => a.role == _roles[actor]).layers ?? new MotionLayer[0])
                {
                    if (layer.index <= 0 || layer.index >= controller.GetLayerCount()) throw new InvalidDataException("Native synchronized layer missing");
                    controller.SetLayerWeight(layer.index, layer.weight);
                    if (!controller.HasState(layer.index, hash)) throw new InvalidDataException("Native synchronized layer state missing: " + state);
                    controller.Play(hash, layer.index, 0);
                }
            }
            foreach (var source in _sourceRotations)
            {
                source.Play(definition, definition.actors.Single(actor => actor.role == source.Role), speed);
                Set(source.Controller, "speed", speed);
            }
            _state = state; _cycles = 0; _graph.Evaluate(0);
            SynchronizeDurations(); _graph.Play();
        }
        private void SynchronizeDurations()
        {
            float master = _controllers[0].GetCurrentAnimatorStateInfo(0).length;
            if (master <= 0) throw new InvalidDataException("Native graph master state duration invalid");
            foreach (var controller in _controllers.Concat(_sourceRotations.Select(source => source.Controller)))
            {
                float duration = controller.GetCurrentAnimatorStateInfo(0).length;
                if (duration <= 0) throw new InvalidDataException("Native graph participant state duration invalid");
                controller.SetSpeed(duration / master);
            }
        }
        internal void ReapplyShape()
        {
            if (_disposed || !_context.IsCurrent()) throw new InvalidOperationException("Native motion ownership expired");
            for (int actorIndex = 0; actorIndex < _controllers.Count; ++actorIndex)
            {
                var role = _roles[actorIndex]; var actor = _actors[role];
                var heightRole = role == "female2" ? "female2" : "female";
                var heightActor = _actors.TryGetValue(heightRole, out var partner) ? partner : actor;
                Set(_controllers[actorIndex], "height", heightActor.GetShapeBodyValue(0));
                Set(_controllers[actorIndex], "breast", actor.GetShapeBodyValue(4));
                foreach (var source in _sourceRotations.Where(source => source.Role == role))
                {
                    Set(source.Controller, "height", heightActor.GetShapeBodyValue(0)); Set(source.Controller, "breast", actor.GetShapeBodyValue(4));
                }
            }
            _graph.Evaluate(0); SynchronizeDurations();
        }
        internal void Parameter(string name, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            foreach (var controller in _controllers.Concat(_sourceRotations.Select(source => source.Controller))) Set(controller, name, value);
            _graph.Evaluate(0); SynchronizeDurations();
        }
        private void OnCycle()
        {
            if (!_context.IsCurrent() || _controllers.Count == 0) return;
            var info = _controllers[0].GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash != Animator.StringToHash(_state)) return;
            int completed = (int)Math.Floor(info.normalizedTime + .02f);
            if (completed > _cycles) { _cycles = completed; _cycle?.Invoke(completed); }
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true; _playToken = null;
            foreach (var receiver in _receivers) if (receiver != null) { receiver.Cycle = null; receiver.Sound = null; UnityEngine.Object.Destroy(receiver); }
            _receivers.Clear();
            foreach (var source in _sourceRotations) try { source.Dispose(); } catch (Exception error) { _context.ReportFailure?.Invoke("Native source correction release: " + error.Message); }
            _sourceRotations.Clear();
            try { if (_graph.IsValid()) _graph.Destroy(); } catch (Exception error) { _context.ReportFailure?.Invoke("Native graph release: " + error.Message); }
            foreach (var restore in Enumerable.Reverse(_restoreAvatars)) try { restore(); } catch (Exception error) { _context.ReportFailure?.Invoke("Native Avatar restore: " + error.Message); }
            _restoreAvatars.Clear();
            foreach (var avatar in _avatars) if (avatar != null) UnityEngine.Object.Destroy(avatar);
            _avatars.Clear();
            _controllers.Clear();
            _roles.Clear();
            foreach (var replacement in _overrides) if (replacement != null) UnityEngine.Object.Destroy(replacement);
            _overrides.Clear();
        }
    }
}
