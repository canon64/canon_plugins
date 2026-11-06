using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Runtime.Serialization.Json;
using MainGameAiShoujoH.Contracts;
using MainGameAiShoujoH.Presentation;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MainGameAiShoujoH.Motion
{
    // One native playable graph supplies all participants' clocks. No frame callback,
    // transform search loop, timer, or state reapplication is installed.
    public sealed class DedicatedMotionModule : IMotionModule, IEndingMotionModule, IKksEndingMotionModule
    {
        private readonly string _directory;
        private readonly bool _useKksOnePoseTrial;
        private KksOnePosePlayback _kksOnePose;
        private readonly Dictionary<string, NativeKksEndingPlayback> _kksEndingPlayers = new Dictionary<string, NativeKksEndingPlayback>(StringComparer.Ordinal);
        private SessionContext _context;
        private MotionCatalog _catalog;
        private PlayableGraph _graph;
        private bool _disposed, _ending;
        private EndingMotionCatalog _endingCatalog;
        private readonly Dictionary<EndingMotionClip, AnimationClip> _endingClips = new Dictionary<EndingMotionClip, AnimationClip>();
        private readonly HashSet<AssetBundle> _pinnedBundles = new HashSet<AssetBundle>();
        private readonly Dictionary<string, EndingMotionPlayback> _endingPlayers = new Dictionary<string, EndingMotionPlayback>(StringComparer.Ordinal);
        private float _speed = 1;
        private readonly Dictionary<string, AssetBundle> _bundles = new Dictionary<string, AssetBundle>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ActorSnapshot> _actors = new Dictionary<string, ActorSnapshot>(StringComparer.Ordinal);
        private readonly List<AnimationClipPlayable> _players = new List<AnimationClipPlayable>();
        private readonly List<DedicatedContactRig> _contacts = new List<DedicatedContactRig>();
        private readonly List<MotionEventReceiver> _receivers = new List<MotionEventReceiver>();
        private int _cycles;
        public event Action<float> AnimationDriven;
        public event Action<int> SeCue;
        private NativeGraphPlayback _native;
        public string Name => "AI native motion";
        public string SelectedMotionId { get; private set; }
        public string SelectedState { get; private set; }
        public string[] AvailableMotionIds => _useKksOnePoseTrial ? (_catalog == null ? new string[0] : new[] { KksOnePosePlayback.MotionId }) : (_catalog?.motions ?? new MotionDefinition[0]).Where(m => (!_useKksOnePoseTrial || m.id == KksOnePosePlayback.MotionId) && m.sourceDataVerified && m.verificationVersion >= 2 && m.packedVerified && m.constraintsVerified && m.nativeGraphVerified && (m.issues == null || m.issues.Length == 0) && m.nativeActors != null && m.nativeActors.All(a => _actors.ContainsKey(a.role))).Select(m => m.id).ToArray();
        public DedicatedMotionModule(string contentDirectory, bool useKksOnePoseTrial = false)
        { _directory = Path.GetFullPath(contentDirectory); _useKksOnePoseTrial = useKksOnePoseTrial; }
        public void Prepare(SessionContext context)
        {
            if (_disposed || _context != null) throw new InvalidOperationException("Motion module already used");
            _context = context ?? throw new ArgumentNullException(nameof(context));
            RequireCurrent();
            _catalog = _useKksOnePoseTrial ? new MotionCatalog { version = 1, motions = new[] { KksNativePoseDefinition.Create() } } : ReadCatalog<MotionCatalog>(Path.Combine(_directory, "catalog.json"));
            if (_catalog == null || _catalog.version != 1 || _catalog.motions == null) throw new InvalidDataException("H motion catalog version is invalid");
            foreach (var participant in context.Request.Participants)
            {
                if (participant.Character == null || participant.Character.animBody == null || string.IsNullOrEmpty(participant.Role)) throw new InvalidDataException("H participant body/role missing");
                _actors.Add(participant.Role, new ActorSnapshot(participant.Character));
            }
            if (_useKksOnePoseTrial)
            {
                foreach (var actor in _actors.Where(a => a.Key == "female" || a.Key == "female2"))
                    _kksEndingPlayers.Add(actor.Key, new NativeKksEndingPlayback(actor.Value.Character));
            }
            else PrepareEndingMotions();
            Select(context.Request.MotionId);
        }
        private void RequireCurrent()
        {
            if (_disposed || _context == null || !_context.IsCurrent()) throw new InvalidOperationException("H motion session expired");
        }
        internal static T ReadCatalog<T>(string path)
        {
            using (var stream = File.OpenRead(path)) return (T)new DataContractJsonSerializer(typeof(T), new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = int.MaxValue }).ReadObject(stream);
        }
        public void Select(string motionId)
        {
            RequireCurrent();
            if (_useKksOnePoseTrial && motionId != KksOnePosePlayback.MotionId) throw new InvalidDataException("KKS one-pose trial permits only " + KksOnePosePlayback.MotionId);
            var motion = _catalog.motions.SingleOrDefault(m => m.id == motionId) ?? throw new InvalidDataException("Unknown AI H motion: " + motionId);
            if (_useKksOnePoseTrial)
            {
                SelectedMotionId = motionId; Play(motion, motion.initialState); return;
            }
            if (!motion.sourceDataVerified || motion.verificationVersion < 2 || !motion.packedVerified || !motion.constraintsVerified || motion.states == null || motion.states.Length == 0 || (motion.issues != null && motion.issues.Length != 0))
                throw new InvalidDataException("AI H motion conversion is incomplete: " + motionId + " " + string.Join("; ", motion.issues ?? new string[0]));
            if (motion.nativeActors == null || motion.nativeActors.Length == 0 || !motion.nativeGraphVerified) throw new InvalidDataException("Native H controller graph has not passed reload validation");
            if (SelectedMotionId != motionId)
            {
                // Destroy all readers before releasing the previous selected closure.
                // State changes within one motion retain its shared base/pose clips.
                StopPlayback();
                ReleaseBundles();
            }
            SelectedMotionId = motionId;
            Play(motion, motion.initialState);
        }
        private void Play(MotionDefinition motion, string stateName)
        {
            var state = motion.states.SingleOrDefault(s => s.name == stateName) ?? throw new InvalidDataException("AI motion state missing: " + stateName);
            if (_useKksOnePoseTrial)
            {
                if (motion.id != KksOnePosePlayback.MotionId) throw new InvalidDataException("KKS trial motion identity differs");
                if (_kksOnePose == null) _kksOnePose = new KksOnePosePlayback(_context, _actors.ToDictionary(a => a.Key, a => a.Value.Character), value => AnimationDriven?.Invoke(value));
                _kksOnePose.Play(state.name, _speed);
                foreach (var actor in _actors.Values) actor.Character.transform.SetPositionAndRotation(_context.Request.Position, _context.Request.Rotation);
                SelectedState = state.name; return;
            }
            if (state.actors == null || state.actors.Length == 0 || !FinitePositive(state.duration)) throw new InvalidDataException("Invalid AI motion state duration");
            if (state.actors.Select(a => a.role).Distinct().Count() != state.actors.Length || state.actors.Any(a => !_actors.ContainsKey(a.role))) throw new InvalidDataException("AI motion participant role mismatch");
            if (motion.nativeActors != null && motion.nativeActors.Length > 0)
            {
                foreach (var contact in _contacts) Try(contact.Dispose, "contact state restore");
                _contacts.Clear();
                foreach (var actor in state.actors)
                    _contacts.Add(new DedicatedContactRig(_actors[actor.role].Character, actor, _actors.ToDictionary(a => a.Key, a => a.Value.Character), _context.ReportFailure));
                if (_native == null) _native = new NativeGraphPlayback(_directory, motion.nativeActors, _actors.ToDictionary(a => a.Key, a => a.Value.Character), LoadBundle, _context, value => AnimationDriven?.Invoke(value), cue => SeCue?.Invoke(cue));
                _native.Play(state, _speed);
                foreach (var actor in state.actors) _actors[actor.role].Character.transform.SetPositionAndRotation(_context.Request.Position, _context.Request.Rotation);
                SelectedState = state.name; return;
            }
            // Read and validate every clip before replacing the currently playing graph.
            var clips = state.actors.Select(a => a.variants != null && a.variants.Length > 0 ? a.variants.Select(v => Load(v)).ToArray() : new[] { Load(a) }).ToArray();
            RequireCurrent();
            StopPlayback();
            var next = PlayableGraph.Create("AiShoujoH:" + _context.Id);
            var players = new List<AnimationClipPlayable>();
            var rigs = new List<DedicatedContactRig>();
            try
            {
                next.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                foreach (var actor in state.actors)
                    rigs.Add(new DedicatedContactRig(_actors[actor.role].Character, actor, _actors.ToDictionary(a => a.Key, a => a.Value.Character), _context.ReportFailure));
                for (int i = 0; i < state.actors.Length; ++i)
                {
                    var definition = state.actors[i]; var snapshot = _actors[definition.role];
                    var mixer = AnimationMixerPlayable.Create(next, clips[i].Length, false);
                    float height = snapshot.Character.GetShapeBodyValue(0);
                    for (int variant = 0; variant < clips[i].Length; ++variant)
                    {
                        var playable = AnimationClipPlayable.Create(next, clips[i][variant]);
                        playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
                        playable.SetTime(0); playable.SetSpeed(_speed * clips[i][variant].length / state.duration);
                        if (!state.loop) playable.SetDuration(clips[i][variant].length);
                        next.Connect(playable, 0, mixer, variant);
                        float weight = 1;
                        if (definition.variants != null && definition.variants.Length == 3)
                            weight = variant == 0 ? Mathf.Clamp01(1 - height * 2) : variant == 1 ? 1 - Mathf.Abs(Mathf.Clamp01(height) * 2 - 1) : Mathf.Clamp01(height * 2 - 1);
                        mixer.SetInputWeight(variant, weight); players.Add(playable);
                    }
                    var output = AnimationPlayableOutput.Create(next, definition.role, snapshot.Animator);
                    output.SetSourcePlayable(mixer);
                }
                next.Evaluate(0);
            }
            catch { if (next.IsValid()) next.Destroy(); foreach (var rig in rigs) rig.Dispose(); throw; }
            _graph = next; _players.AddRange(players); _contacts.AddRange(rigs);
            _cycles = 0;
            foreach (var actor in state.actors)
            {
                var animator = _actors[actor.role].Animator;
                _actors[actor.role].Character.transform.SetPositionAndRotation(_context.Request.Position, _context.Request.Rotation);
                animator.fireEvents = true; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var receiver = animator.gameObject.AddComponent<MotionEventReceiver>(); _receivers.Add(receiver);
                if (_receivers.Count == 1) receiver.Cycle = () =>
                {
                    if (_disposed || !_context.IsCurrent() || _players.Count == 0) return;
                    // Blended S/M/L clips share a normalized clock and may emit the
                    // same event; count each authored cycle once without polling.
                    float normalized = (float)(_players[0].GetTime() / clips[0][0].length);
                    int completed = Math.Max(1, (int)Math.Floor(normalized + .02f));
                    if (completed > _cycles) { _cycles = completed; AnimationDriven?.Invoke(_cycles); }
                };
            }
            _graph.Play(); SelectedState = state.name;
        }
        private AnimationClip Load(MotionClipReference actor)
        {
            var bundle = LoadBundle(actor.bundle, actor.sha256);
            var clip = bundle.LoadAsset<AnimationClip>(actor.asset);
            if (clip == null || !FinitePositive(clip.length) || Math.Abs(clip.length - actor.duration) > .01f) throw new InvalidDataException("Converted H clip missing/duration mismatch: " + actor.asset);
            return clip;
        }
        private AssetBundle LoadBundle(string relative, string sha256) => LoadBundleIn(_directory, relative, sha256);
        private AssetBundle LoadBundleIn(string directory, string relative, string sha256)
        {
            if (string.IsNullOrEmpty(sha256) || sha256.Length != 64) throw new InvalidDataException("Converted H bundle hash missing");
            var path = Path.GetFullPath(Path.Combine(directory, relative ?? ""));
            if (!path.StartsWith(directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("H bundle escapes content directory");
            if (!_bundles.TryGetValue(path, out var bundle))
            {
                using (var hash = SHA256.Create()) using (var stream = File.OpenRead(path))
                    if (!string.Equals(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""), sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("H bundle hash mismatch: " + relative);
                bundle = AssetBundle.LoadFromFile(path) ?? throw new InvalidDataException("Converted H bundle unreadable: " + relative);
                _bundles.Add(path, bundle);
            }
            return bundle;
        }
        private static bool HashIdentity(string value) => value != null && value.Length == 64 && value.All(character => "0123456789abcdefABCDEF".IndexOf(character) >= 0);
        private static bool FinitePositive(float v) => !float.IsNaN(v) && !float.IsInfinity(v) && v > 0;
        public void SetSpeed(float speed)
        {
            RequireCurrent();
            if (_useKksOnePoseTrial)
            {
                if (float.IsNaN(speed) || float.IsInfinity(speed) || speed < 0 || speed > 4) throw new ArgumentOutOfRangeException(nameof(speed));
                _speed = speed; _kksOnePose?.Parameter("speed", speed); return;
            }
            if (!FinitePositive(speed) || speed > 4) throw new ArgumentOutOfRangeException(nameof(speed));
            float ratio = speed / _speed;
            foreach (var player in _players) player.SetSpeed(player.GetSpeed() * ratio);
            _speed = speed;
            _native?.Parameter("speed", speed);
            _kksOnePose?.Parameter("speed", speed);
        }
        public void Execute(Command command)
        {
            RequireCurrent();
            // Progression validates native eligibility and performs the single Select.
            if (command.Name == "speed") SetSpeed(command.Amount);
            else if (command.Name == "state") Play(_catalog.motions.Single(m => m.id == SelectedMotionId), command.Value);
            else if (command.Name == "shape-changed")
            {
                foreach (var rig in _contacts) rig.ReapplyShape();
                _native?.ReapplyShape();
                _kksOnePose?.ReapplyShape();
            }
            else if (command.Name == "motion-blend") Parameter("motion", command.Amount);
            else if (command.Name.StartsWith("parameter:", StringComparison.Ordinal)) Parameter(command.Name.Substring("parameter:".Length), command.Amount);
        }
        private void Parameter(string name, float value)
        {
            _native?.Parameter(name, value);
            _kksOnePose?.Parameter(name, value);
        }
        private void StopPlayback()
        {
            if (_kksOnePose != null) { Try(_kksOnePose.Dispose, "KKS trial release"); _kksOnePose = null; }
            if (_native != null) { Try(_native.Dispose, "native controller release"); _native = null; }
            foreach (var receiver in _receivers) if (receiver != null) { receiver.Cycle = null; UnityEngine.Object.Destroy(receiver); }
            _receivers.Clear();
            if (_graph.IsValid()) _graph.Destroy();
            _players.Clear();
            foreach (var contact in _contacts) Try(contact.Dispose, "contact restore");
            _contacts.Clear();
        }
        private void Try(Action action, string item) { try { action(); } catch (Exception e) { _context?.ReportFailure?.Invoke(item + ": " + e.GetBaseException().Message); } }
        public void End(EndReason reason)
        {
            if (reason == EndReason.Completed && !_disposed)
            {
                if (_ending) return;
                _ending = true;
                if (_useKksOnePoseTrial)
                {
                    _kksOnePose?.BeginEnding();
                    foreach (var ending in _kksEndingPlayers.Values) ending.Begin();
                    SelectedState = NativeKksEndingPlayback.DefaultState;
                }
                else StopPlayback();
                // Native HScene.EndProc hides the player before its ending ADV.
                // Its body no longer owns an H graph while female ADV motion runs.
                foreach (var participant in _context.Request.Participants.Where(p => p.IsPlayer))
                    if (_actors.TryGetValue(participant.Role, out var player)) Try(player.HideForEnding, "ending player visibility");
            }
            else Dispose();
        }
        public void ValidateEndingMotion(int femaleSlot, int posture, int pose)
        {
            if (_disposed || _context == null) throw new InvalidOperationException("Ending motion preparation ownership expired");
            string role = femaleSlot == 0 ? "female" : femaleSlot == 1 ? "female2" : throw new ArgumentOutOfRangeException(nameof(femaleSlot));
            if (!_actors.TryGetValue(role, out var actor) || actor.Character == null || actor.Animator == null) throw new InvalidDataException("Ending female participant is missing: " + role);
            if (_useKksOnePoseTrial) throw new NotSupportedException("AI posture/pose identifiers cannot select a KKS native ADV motion; use PlayNativeEndingMotion");
            var definition = _endingCatalog?.clips.SingleOrDefault(clip => clip.posture == posture && clip.pose == pose);
            if (definition == null || !_endingClips.ContainsKey(definition)) throw new InvalidDataException("Ending motion was not prepared before reveal: " + posture + "/" + pose);
        }
        public void PlayEndingMotion(int femaleSlot, int posture, int pose)
        {
            if (_disposed || !_ending || _context == null) throw new InvalidOperationException("Ending motion is outside completed-session finalization");
            ValidateEndingMotion(femaleSlot, posture, pose);
            string role = femaleSlot == 0 ? "female" : femaleSlot == 1 ? "female2" : throw new ArgumentOutOfRangeException(nameof(femaleSlot));
            if (!_actors.TryGetValue(role, out var actor)) throw new InvalidDataException("Ending female participant is missing: " + role);
            var definition = _endingCatalog?.clips.SingleOrDefault(c => c.posture == posture && c.pose == pose);
            if (definition == null || !_endingClips.TryGetValue(definition, out var clip)) throw new InvalidDataException("Ending motion was not prepared before reveal: " + posture + "/" + pose);
            if (_endingPlayers.TryGetValue(role, out var previous)) { previous.Dispose(); _endingPlayers.Remove(role); }
            _endingPlayers.Add(role, new EndingMotionPlayback(actor.Animator, clip, _endingCatalog.bindingPaths));
        }
        public void PlayNativeEndingMotion(int femaleSlot, string exactNativeState)
        {
            if (_disposed || !_ending || !_useKksOnePoseTrial) throw new InvalidOperationException("KKS native ending motion is outside its finalization lease");
            string role = femaleSlot == 0 ? "female" : femaleSlot == 1 ? "female2" : throw new ArgumentOutOfRangeException(nameof(femaleSlot));
            if (!_kksEndingPlayers.TryGetValue(role, out var player)) throw new InvalidDataException("KKS native ending participant is missing: " + role);
            player.Play(exactNativeState); SelectedState = player.State;
        }
        private void PrepareEndingMotions()
        {
            string directory = Path.GetFullPath(Path.Combine(_directory, "..", "HAdvMotion"));
            var catalog = ReadCatalog<EndingMotionCatalog>(Path.Combine(directory, "catalog.json"));
            if (catalog == null || catalog.version != 1 || catalog.clips == null || catalog.clips.Length == 0 || catalog.bindingPaths == null) throw new InvalidDataException("Ending ADV motion catalog is invalid");
            if (catalog.clips.GroupBy(c => new { c.posture, c.pose }).Any(group => group.Count() != 1)) throw new InvalidDataException("Ending ADV motion identity is ambiguous");
            foreach (var definition in catalog.clips)
            {
                if (!definition.sourceDataVerified || !definition.sourceCurveVerified || !definition.packedVerified || definition.verificationVersion < 2 || !HashIdentity(definition.sourceHash) || !HashIdentity(definition.sourceRawHash) || !HashIdentity(definition.conversionHash) || !long.TryParse(definition.sourcePathID, out var sourceId) || !FinitePositive(definition.sourceDuration) || Math.Abs(definition.sourceDuration - definition.duration) > .01f) throw new InvalidDataException("Original ending motion source/curve/recipe verification is not ready: " + definition.posture + "/" + definition.pose);
                var bundle = LoadBundleIn(directory, definition.bundle, definition.sha256);
                var clip = bundle.LoadAsset<AnimationClip>(definition.asset);
                if (clip == null || !FinitePositive(clip.length) || Math.Abs(clip.length - definition.duration) > .01f) throw new InvalidDataException("Ending ADV clip is missing or its duration changed: " + definition.state);
                _endingClips.Add(definition, clip); _pinnedBundles.Add(bundle);
            }
            _endingCatalog = catalog;
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            // Release ADV ownership before its enclosing H backend restores the
            // original walking controller and native correction snapshots.
            foreach (var player in _kksEndingPlayers.Values) Try(player.Dispose, "KKS ending motion release");
            _kksEndingPlayers.Clear();
            StopPlayback();
            foreach (var player in _endingPlayers.Values) Try(player.Dispose, "ending motion release");
            _endingPlayers.Clear();
            foreach (var actor in _actors) actor.Value.Restore(message => _context?.ReportFailure?.Invoke(actor.Key + ": " + message));
            _actors.Clear();
            _endingClips.Clear(); _endingCatalog = null; _pinnedBundles.Clear();
            ReleaseBundles(); _context = null;
        }
        private void ReleaseBundles()
        {
            foreach (var entry in _bundles.Where(pair => !_pinnedBundles.Contains(pair.Value)).ToArray())
            {
                var bundle = entry.Value; Try(() => { if (bundle != null) bundle.Unload(true); }, "bundle unload");
                _bundles.Remove(entry.Key);
            }
        }
        private sealed class ActorSnapshot
        {
            internal readonly ChaControl Character;
            internal readonly Animator Animator;
            private readonly bool _events, _root, _visible;
            private bool _endingVisibilityChanged;
            private readonly float _speed;
            private readonly AnimatorCullingMode _culling;
            private readonly Action[] _bones;
            private readonly Vector3 _position;
            private readonly Quaternion _rotation;
            internal ActorSnapshot(ChaControl actor)
            {
                Character = actor; Animator = actor.animBody;
                _visible = actor.visibleAll;
                _position = actor.transform.position; _rotation = actor.transform.rotation;
                _events = Animator.fireEvents; _root = Animator.applyRootMotion; _speed = Animator.speed; _culling = Animator.cullingMode;
                _bones = Animator.GetComponentsInChildren<Transform>(true).Select(b =>
                {
                    var p = b.localPosition; var r = b.localRotation; var s = b.localScale;
                    return (Action)(() => { if (b != null) { b.localPosition = p; b.localRotation = r; b.localScale = s; } });
                }).ToArray();
            }
            internal void HideForEnding()
            {
                if (Character == null || Character.animBody != Animator) throw new InvalidOperationException("Ending player body ownership changed");
                _endingVisibilityChanged = true; Character.visibleAll = false;
            }
            internal void Restore(Action<string> failure)
            {
                if (Character == null || Character.animBody != Animator || Animator == null) return;
                try { Character.transform.SetPositionAndRotation(_position, _rotation); } catch (Exception e) { failure("actor position restore: " + e.Message); }
                foreach (var restore in _bones) try { restore(); } catch (Exception e) { failure("bone restore: " + e.Message); }
                try { Animator.fireEvents = _events; Animator.applyRootMotion = _root; Animator.speed = _speed; Animator.cullingMode = _culling; } catch (Exception e) { failure("Animator restore: " + e.Message); }
                if (_endingVisibilityChanged) try { Character.visibleAll = _visible; _endingVisibilityChanged = false; } catch (Exception e) { failure("ending visibility restore: " + e.Message); }
            }
        }
    }
}
