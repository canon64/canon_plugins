using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace MapEnvironmentRuntime
{
    public sealed class DoorStatus { public int count; public string candidate, state, error; public bool busy; }
    public sealed class SandboxIslandDoors : MonoBehaviour
    {
        public sealed class SavedDoor { public string path; public bool open, right; }
        public SavedDoor[] CaptureMoment()
        {
            if (_doors.Any(d => d.busy)) throw new InvalidOperationException("ドアの開閉が終わってからセーブしてください。");
            return _doors.Select(d => new SavedDoor { path = d.data.animatorPath, open = d.open, right = d.right }).ToArray();
        }
        public void RestoreMoment(SavedDoor[] saved)
        {
            if (saved == null || saved.Length != _doors.Count || saved.Any(s => s == null || !_doors.Any(d => d.data.animatorPath == s.path))
                || saved.Select(s => s.path).Distinct(StringComparer.Ordinal).Count() != saved.Length)
                throw new InvalidDataException("保存したドア構成と現在の島が一致しません。");
            foreach (var item in saved) Commit(_doors.Single(d => d.data.animatorPath == item.path), item.open, item.right);
        }
        public sealed class Obstacles { public string openRight, openLeft, closed; }
        public sealed class Descriptor
        {
            public int id; public string kind, animatorPath, controllerAsset;
            public string openRightState, openLeftState, closeRightState, closeLeftState, closedIdleState, openRightIdleState, openLeftIdleState;
            public Obstacles obstacles; public string[] extraBlockers;
        }
        public sealed class Manifest { public string bundle, root; public Descriptor[] doors; public string[] sessionHiddenObjects, sessionShownObjects; }
        private sealed class Door
        {
            public Descriptor data; public Animator animator; public RuntimeAnimatorController originalController;
            public bool originalEvents, originalRootMotion, originalEnabled, open, right, busy;
            public AnimatorCullingMode originalCulling; public AnimatorUpdateMode originalUpdate;
            public GameObject openRight, openLeft, closed; public GameObject[] extras;
            public Collider[] colliders;
        }
        private sealed class NativeObstacleState
        {
            public bool enabled, carving, onlyStationary;
        }
        private readonly List<Door> _doors = new List<Door>();
        private readonly HashSet<Transform> _navigationRoots = new HashSet<Transform>();
        public IEnumerable<Transform> NavigationExclusions => _navigationRoots;
        private Camera _view;
        private bool _nativeWalkingInput;
        public void BindView(Camera camera, bool nativeWalkingInput) { _view = camera; _nativeWalkingInput = nativeWalkingInput; }
        private readonly Dictionary<NavMeshObstacle, NativeObstacleState> _nativeObstacles = new Dictionary<NavMeshObstacle, NativeObstacleState>();
        public event Action PassageChanged;
        public Func<bool> BeforeInteract { get; set; }
        public IReadOnlyList<Bounds> ClosedBounds() => _doors.Where(door => !door.open).SelectMany(door => door.colliders)
            .Where(collider => collider != null && collider.enabled && collider.gameObject.activeInHierarchy && !collider.isTrigger).Select(collider => collider.bounds).ToArray();
        private readonly Dictionary<GameObject, bool> _active = new Dictionary<GameObject, bool>();
        private SandboxSession _session;
        private Door _candidate;
        private Parameters _settings;
        private readonly DoorStatus _status = new DoorStatus();
        private Parameters P => _session == null ? _settings : _session.Settings;
        public static Manifest Read(Parameters p) => JsonCodec.FromJson<Manifest>(File.ReadAllText(p.T("island.doors.manifest")));
        public void Initialize(Manifest manifest, Transform root, AssetBundle bundle, Parameters p)
        {
            _settings = p;
            // Original EventPoint.Awake hides the merchant ADV safeguard during ordinary exploration.
            // That script was removed from the imported island; restore only its exact initialization target.
            foreach (string path in manifest.sessionHiddenObjects ?? Array.Empty<string>())
            {
                var node = Resolve(root, manifest.root, path);
                if (!_active.ContainsKey(node)) _active.Add(node, node.activeSelf);
                node.SetActive(false);
            }
            foreach (string path in manifest.sessionShownObjects ?? Array.Empty<string>())
            {
                var node = Resolve(root, manifest.root, path);
                if (!_active.ContainsKey(node)) _active.Add(node, node.activeSelf);
                node.SetActive(true);
            }
            foreach (var item in manifest.doors)
            {
                var animator = Find(root, manifest.root, item.animatorPath).GetComponent<Animator>();
                if (animator == null) throw new InvalidDataException("Door Animator missing: " + item.animatorPath);
                var controller = bundle.LoadAsset<RuntimeAnimatorController>(item.controllerAsset);
                if (controller == null) throw new InvalidDataException("Door controller missing: " + item.controllerAsset);
                var door = new Door { data = item, animator = animator, originalController = animator.runtimeAnimatorController,
                    originalEvents = animator.fireEvents, originalRootMotion = animator.applyRootMotion, originalEnabled = animator.enabled, originalCulling = animator.cullingMode, originalUpdate = animator.updateMode };
                _doors.Add(door);
                door.openRight = Resolve(root, manifest.root, item.obstacles?.openRight);
                door.openLeft = Resolve(root, manifest.root, item.obstacles?.openLeft);
                door.closed = Resolve(root, manifest.root, item.obstacles?.closed);
                door.extras = (item.extraBlockers ?? Array.Empty<string>()).Select(path => Resolve(root, manifest.root, path)).ToArray();
                foreach (var node in new[] { door.openRight, door.openLeft, door.closed }.Concat(door.extras).Where(node => node != null)) if (!_active.ContainsKey(node)) _active.Add(node, node.activeSelf);
                var ownedBranches = new[] { animator.gameObject, door.openRight, door.openLeft, door.closed }.Concat(door.extras).Where(node => node != null).Distinct().ToArray();
                foreach (var branch in ownedBranches) _navigationRoots.Add(branch.transform);
                door.colliders = ownedBranches.SelectMany(node => node.GetComponentsInChildren<Collider>(true)).Distinct().ToArray();
                // These original shapes already follow the island's scale and orientation.
                // Only our door branches are owned here; unrelated island obstacles are untouched.
                foreach (var obstacle in ownedBranches.SelectMany(node => node.GetComponentsInChildren<NavMeshObstacle>(true)).Distinct())
                {
                    if (!_nativeObstacles.ContainsKey(obstacle)) _nativeObstacles.Add(obstacle, new NativeObstacleState { enabled = obstacle.enabled, carving = obstacle.carving, onlyStationary = obstacle.carveOnlyStationary });
                    obstacle.enabled = true; obstacle.carving = true; obstacle.carveOnlyStationary = false;
                }
                animator.enabled = true; animator.runtimeAnimatorController = controller; animator.fireEvents = false; animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                animator.Rebind(); animator.Update(p.N("math.zero"));
                foreach (string state in new[] { item.closedIdleState, item.openRightState, item.openRightIdleState, item.openLeftState, item.openLeftIdleState, item.closeRightState, item.closeLeftState }.Where(name => !string.IsNullOrEmpty(name))) State(door, state);
                Commit(door, false, true);
            }
            _status.count = _doors.Count; Physics.SyncTransforms(); Debug.Log("SANDBOX_DOORS_READY count=" + _doors.Count + " nativeCarvingObstacles=" + _nativeObstacles.Count);
        }
        private static Transform Find(Transform root, string name, string path)
        {
            string relative = path.StartsWith(name + "/", StringComparison.Ordinal) ? path.Substring((name + "/").Length) : path;
            var result = root.Find(relative); if (result == null) throw new InvalidDataException("Door path missing: " + path); return result;
        }
        private GameObject Resolve(Transform root, string name, string path) => string.IsNullOrEmpty(path) ? null : Find(root, name, path).gameObject;
        private int State(Door door, string name)
        {
            int layer = P.I("math.zero"), hash = Animator.StringToHash(door.animator.GetLayerName(layer) + "." + name);
            if (!door.animator.HasState(layer, hash)) throw new InvalidDataException("Door state missing: " + door.data.animatorPath + " " + name);
            return hash;
        }
        private void Commit(Door door, bool open, bool right)
        {
            door.open = open; door.right = right;
            if (door.closed != null) door.closed.SetActive(!open);
            if (door.openRight != null) door.openRight.SetActive(open && right);
            if (door.openLeft != null) door.openLeft.SetActive(open && !right);
            foreach (var node in door.extras) if (node != null) node.SetActive(!open);
            string idle = !open ? door.data.closedIdleState : right ? door.data.openRightIdleState : door.data.openLeftIdleState;
            door.animator.Play(State(door, idle), P.I("math.zero"), P.N("math.zero")); door.animator.Update(P.N("math.zero"));
            Physics.SyncTransforms();
            PassageChanged?.Invoke();
        }
        public void AttachSession(SandboxSession session) { _session = session; }
        public void Interact()
        {
            if (_session == null || _session.World == null || _view == null) return;
            _candidate = null; float nearest = P.N("island.doors.reach"); var camera = _view;
            if (!_session.Panel && !_session.EditingParameter && P.T("movement.mode") == "walk") foreach (var door in _doors)
            {
                Vector3 point = door.animator.transform.position;
                float closest = float.PositiveInfinity;
                foreach (var collider in door.colliders)
                {
                    if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy || collider.isTrigger) continue;
                    var candidate = collider.bounds.ClosestPoint(camera.transform.position);
                    float squared = (candidate - camera.transform.position).sqrMagnitude;
                    if (squared < closest) { closest = squared; point = candidate; }
                }
                float distance = Vector3.Distance(point, camera.transform.position);
                var direction = point - camera.transform.position;
                if (distance > nearest || (direction.sqrMagnitude > P.N("render.epsilon") && Vector3.Dot(camera.transform.forward, direction.normalized) < P.N("island.doors.viewDot"))) continue;
                _candidate = door; nearest = distance;
            }
            if (_candidate == null) return;
            if (!_candidate.busy && !(_candidate.open && _candidate.data.kind == "harbor"))
                StartCoroutine(Change(_candidate));
        }
        private IEnumerator Change(Door door)
        {
            bool wasOpen = door.open, oldRight = door.right;
            bool right = door.data.kind == "harbor" || (wasOpen ? door.right : Vector3.Dot(door.animator.transform.forward, _view.transform.position - door.animator.transform.position) < P.N("math.zero"));
            string state = wasOpen ? (right ? door.data.closeRightState : door.data.closeLeftState) : (right ? door.data.openRightState : door.data.openLeftState);
            int hash;
            try { hash = State(door, state); }
            catch (Exception ex) { _status.error = ex.Message; Debug.LogError(ex); yield break; }
            door.busy = true; _status.error = null;
            door.animator.Play(hash, P.I("math.zero"), P.N("math.zero"));
            if (!wasOpen) foreach (var node in door.extras) if (node != null) node.SetActive(false);
            door.animator.Update(P.N("math.zero"));
            var started = door.animator.GetCurrentAnimatorStateInfo(P.I("math.zero"));
            float duration = started.length / Mathf.Max(P.N("render.epsilon"), Mathf.Abs(door.animator.speed * started.speed * started.speedMultiplier));
            string targetIdle = wasOpen ? door.data.closedIdleState : right ? door.data.openRightIdleState : door.data.openLeftIdleState;
            int idleHash = State(door, targetIdle);
            yield return new WaitForSecondsRealtime(Mathf.Min(duration + P.N("render.epsilon"), P.N("island.doors.timeout")));
            var current = door.animator.GetCurrentAnimatorStateInfo(P.I("math.zero"));
            bool complete = (current.fullPathHash == hash && current.normalizedTime + P.N("render.epsilon") >= P.N("math.one")) || current.fullPathHash == idleHash;
            if (complete) Commit(door, !wasOpen, right);
            else { Commit(door, wasOpen, oldRight); _status.error = "扉の開閉が時間内に完了しませんでした：" + door.data.animatorPath; Debug.LogError(_status.error); }
            door.busy = false; Debug.Log("SANDBOX_DOOR_STATE path=" + door.data.animatorPath + " open=" + door.open);
        }
        public DoorStatus Snapshot()
        {
            _status.candidate = _candidate?.data.animatorPath; _status.busy = _candidate != null && _candidate.busy; _status.state = _candidate == null ? null : _candidate.open ? "open" : "closed"; return _status;
        }
        private void OnGUI()
        {
            if (_session == null || _session.Panel || _session.EditingParameter || Event.current.type != EventType.KeyDown) return;
            if (Event.current.keyCode != (KeyCode)Enum.Parse(typeof(KeyCode), P.T("input.door")) && !(_nativeWalkingInput && Event.current.keyCode == KeyCode.F3)) return;
            if (BeforeInteract?.Invoke() == true) { Event.current.Use(); return; }
            Interact(); Event.current.Use();
        }
        private void OnDestroy()
        {
            StopAllCoroutines();
            foreach (var saved in _nativeObstacles) if (saved.Key != null)
            {
                saved.Key.carving = saved.Value.carving; saved.Key.carveOnlyStationary = saved.Value.onlyStationary; saved.Key.enabled = saved.Value.enabled;
            }
            _nativeObstacles.Clear();
            foreach (var door in _doors) if (door.animator != null)
            {
                door.animator.runtimeAnimatorController = door.originalController; door.animator.fireEvents = door.originalEvents; door.animator.applyRootMotion = door.originalRootMotion;
                door.animator.cullingMode = door.originalCulling; door.animator.updateMode = door.originalUpdate; door.animator.enabled = door.originalEnabled;
            }
            foreach (var node in _active) if (node.Key != null) node.Key.SetActive(node.Value);
        }
    }
}
