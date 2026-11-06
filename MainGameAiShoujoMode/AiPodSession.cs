using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapEnvironmentRuntime;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    // The existing settings window owns input capture; this adds no independent GUI window.
    public sealed class AiPodSession : MonoBehaviour
    {
        public sealed class Rotation { public float x, y, z, w; public Quaternion Value => new Quaternion(x, y, z, w); }
        public sealed class Pose { public Vector3 position, scale; public Rotation rotation; }
        public sealed class Descriptor
        {
            public int id; public string animatorPath; public string[] enabledNodes, disabledNodes;
            public Pose[] command, pivot, recover, playerRecover;
        }
        public sealed class Manifest { public string root, controller, closedIdle, open, openIdle, close; public Descriptor[] pods; }
        private sealed class SavedTransform { public Transform transform; public Vector3 position, scale; public Quaternion rotation; }
        private sealed class Assignment { public string card, coordinate; }
        private sealed class Pod
        {
            public Descriptor data; public Animator animator; public RuntimeAnimatorController controller;
            public bool enabled, events, rootMotion; public float speed; public AnimatorCullingMode culling; public AnimatorUpdateMode update;
            public AnimatorStateInfo[] states; public SavedTransform[] transforms;
            public Dictionary<GameObject, bool> active; public Dictionary<Collider, bool> colliders;
        }
        private SandboxHost _host;
        private AiAnimationTrial _characters;
        private Action<string> _log;
        private Manifest _manifest;
        private readonly List<Pod> _pods = new List<Pod>();
        private Transform _root;
        private Pod _selected, _drawPod, _operationPod;
        private Assignment _drawAssignment;
        private readonly Dictionary<int, Assignment> _assignments = new Dictionary<int, Assignment>();
        private bool _busy, _drawSelected, _drawBusy, _listingCoordinates, _drawCoordinates, _drawPicker;
        internal bool Busy => _busy;
        internal AiMomentSave.Pod[] CaptureMoment()
        {
            if (_busy) throw new InvalidOperationException("カプセルの動作が終わってからセーブしてください。");
            return _pods.Select(p => { var state = p.animator.GetCurrentAnimatorStateInfo(P.I("math.zero"));
                return new AiMomentSave.Pod { id = p.data.id, animation = state.fullPathHash, time = state.normalizedTime }; }).ToArray();
        }
        internal void RestoreMoment(AiMomentSave.Pod[] saved)
        {
            int layer = P.I("math.zero");
            if (saved.Length != _pods.Count || saved.Any(s => !_pods.Any(p => p.data.id == s.id && p.animator.HasState(layer, s.animation))))
                throw new InvalidDataException("保存したカプセルと現在の島が一致しません。");
            foreach (var item in saved)
            {
                var pod = _pods.Single(p => p.data.id == item.id);
                pod.animator.Play(item.animation, layer, item.time); pod.animator.Update(P.N("math.zero"));
            }
        }
        private string _status = "各カプセルの女性カードと初期コーデを保存してください。端末の前でFを押すと割当済みの女性が登場します。", _parameterPath;
        private AiCardPicker _assignmentPicker;
        private Parameters P => _host.Session.Settings;

        public void Initialize(SandboxHost host, AiAnimationTrial characters, string content, Action<string> log)
        {
            _host = host; _characters = characters; _log = log; _parameterPath = Path.Combine(content, "parameters.json");
            _manifest = JsonCodec.FromJson<Manifest>(File.ReadAllText(Path.Combine(content, P.T("pod.manifest"))));
            if (_manifest?.pods == null || !_manifest.pods.Any() || new[] { _manifest.root, _manifest.controller, _manifest.closedIdle, _manifest.open, _manifest.openIdle, _manifest.close }.Any(string.IsNullOrEmpty))
                throw new InvalidDataException("カプセルの配置・開閉データが不足しています。");
            _root = FindObjectsOfType<Transform>().Single(node => node.name == _manifest.root && node.gameObject.scene.isLoaded);
            RuntimeAnimatorController controller = null;
            foreach (var bundle in AssetBundle.GetAllLoadedAssetBundles())
            {
                if (bundle.isStreamedSceneAssetBundle || !bundle.GetAllAssetNames().Any(name => string.Equals(Path.GetFileNameWithoutExtension(name), _manifest.controller, StringComparison.OrdinalIgnoreCase))) continue;
                controller = bundle.LoadAsset<RuntimeAnimatorController>(_manifest.controller);
                if (controller != null) break;
            }
            if (controller == null) throw new InvalidDataException("読込済み島バンドルにカプセルのcontrollerがありません。");
            // Borrow the island's controller. The island loader retains bundle ownership.
            foreach (var item in _manifest.pods)
            {
                Validate(item.command); Validate(item.recover); Validate(item.pivot);
                var animator = Find(item.animatorPath).GetComponent<Animator>();
                if (animator == null) throw new InvalidDataException("カプセルAnimatorがありません：" + item.animatorPath);
                var pod = new Pod { data = item, animator = animator, controller = animator.runtimeAnimatorController,
                    enabled = animator.enabled, events = animator.fireEvents, rootMotion = animator.applyRootMotion,
                    speed = animator.speed, culling = animator.cullingMode, update = animator.updateMode,
                    states = animator.runtimeAnimatorController == null ? Array.Empty<AnimatorStateInfo>() : Enumerable.Range(P.I("math.zero"), animator.layerCount).Select(animator.GetCurrentAnimatorStateInfo).ToArray(),
                    transforms = animator.GetComponentsInChildren<Transform>(true).Select(node => new SavedTransform { transform = node, position = node.localPosition, rotation = node.localRotation, scale = node.localScale }).ToArray(),
                    active = animator.GetComponentsInChildren<Transform>(true).ToDictionary(node => node.gameObject, node => node.gameObject.activeSelf),
                    colliders = animator.GetComponentsInChildren<Collider>(true).ToDictionary(collider => collider, collider => collider.enabled) };
                _pods.Add(pod);
                foreach (string name in (item.enabledNodes ?? Array.Empty<string>()).Concat(item.disabledNodes ?? Array.Empty<string>())) Find(name);
                animator.enabled = true; animator.runtimeAnimatorController = controller; animator.fireEvents = false; animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.updateMode = AnimatorUpdateMode.UnscaledTime; animator.Rebind();
                foreach (string state in new[] { _manifest.closedIdle, _manifest.open, _manifest.openIdle, _manifest.close }) State(pod, state);
                Play(pod, _manifest.closedIdle);
            }
            ReadAssignments(); _selected = _pods.OrderBy(pod => pod.data.id).First();
            _host.Session.ParametersApplied += ConfigureCardPicker;
            _log("AI_PODS_READY count=" + _pods.Count + " controller=" + controller.name + " characterEntryAnimation=idle-minimal");
        }
        private void Validate(Pose[] chain)
        {
            if (chain == null || !chain.Any() || chain.Any(pose => pose == null || pose.rotation == null || Mathf.Abs(pose.scale.x * pose.scale.y * pose.scale.z) <= P.N("render.epsilon")))
                throw new InvalidDataException("カプセルの座標変換データが不正です。");
        }
        private Transform Find(string path)
        {
            string prefix = _manifest.root + "/";
            var node = _root.Find(path.StartsWith(prefix, StringComparison.Ordinal) ? path.Substring(prefix.Length) : path);
            if (node == null) throw new InvalidDataException("カプセルの配置物がありません：" + path);
            return node;
        }
        private void World(Pose[] chain, out Vector3 position, out Quaternion rotation)
        {
            Matrix4x4 matrix = Matrix4x4.identity;
            foreach (var pose in chain) matrix *= Matrix4x4.TRS(pose.position, pose.rotation.Value, pose.scale);
            Vector3 source = matrix.MultiplyPoint3x4(Vector3.zero);
            position = _host.FromIslandSourcePosition(source);
            Vector3 forward = _host.FromIslandSourcePosition(source + matrix.MultiplyVector(Vector3.forward)) - position;
            Vector3 up = _host.FromIslandSourcePosition(source + matrix.MultiplyVector(Vector3.up)) - position;
            if (forward.sqrMagnitude <= P.N("render.epsilon") || up.sqrMagnitude <= P.N("render.epsilon")) throw new InvalidDataException("カプセルの方向変換が不正です。");
            rotation = Quaternion.LookRotation(forward.normalized, up.normalized);
        }
        public bool TryInteract()
        {
            if (_busy) return true;
            if (_host?.Session == null || _host.Session.Panel || _host.Session.EditingParameter || P.T("movement.mode") != "walk" || _host.Walker?.ViewCamera == null) return false;
            var camera = _host.Walker.ViewCamera.transform; float nearest = P.N("pod.reach"); Pod candidate = null;
            foreach (var pod in _pods)
            {
                World(pod.data.command, out Vector3 point, out Quaternion unused);
                Vector3 direction = point - camera.position; float distance = direction.magnitude;
                if (distance > nearest || (distance > P.N("render.epsilon") && Vector3.Dot(camera.forward, direction.normalized) < P.N("pod.viewDot"))) continue;
                candidate = pod; nearest = distance;
            }
            if (candidate == null) return false;
            _selected = candidate;
            if (string.IsNullOrWhiteSpace(P.T(Key(candidate, "card"))))
            {
                _status = Label(candidate) + "は女性カード未割当です。この設定画面でカードを指定して保存してください。";
                _host.Session.TogglePanel(); _log("AI_POD_UNASSIGNED id=" + candidate.data.id); return true;
            }
            BeginSpawn(candidate);
            return true;
        }
        private static string Key(Pod pod, string field) => "pod." + pod.data.id + "." + field;
        private static string Label(Pod pod) => "カプセル " + pod.data.id + "（" + (pod.data.animatorPath.Contains("city") ? "市街地" : pod.data.animatorPath.Contains("labo") ? "研究所" : "森") + "）";
        private void ReadAssignments()
        {
            foreach (var pod in _pods) _assignments[pod.data.id] = new Assignment { card = P.T(Key(pod, "card")), coordinate = P.T(Key(pod, "coordinate")) };
        }
        internal bool TryResolveAssignedResident(string origin, out string card, out string coordinate)
        {
            card = coordinate = null;
            if (origin == null || !origin.StartsWith("pod:", StringComparison.Ordinal)) return false;
            if (!int.TryParse(origin.Substring("pod:".Length), out int id) || !_assignments.TryGetValue(id, out var assignment) || string.IsNullOrWhiteSpace(assignment.card)) return false;
            card = assignment.card; coordinate = assignment.coordinate; return true;
        }
        public void DrawControls()
        {
            if (Event.current.type == EventType.Layout)
            {
                _drawSelected = _selected != null; _drawBusy = _busy; _drawCoordinates = _listingCoordinates; _drawPod = _selected;
                _drawPicker = _assignmentPicker != null;
                _drawAssignment = _drawSelected ? _assignments[_drawPod.data.id] : null;
            }
            GUILayout.Label("カプセルごとの女性・初期コーデ設定");
            GUILayout.Label(_status);
            bool enabled = GUI.enabled; GUI.enabled = enabled && !_drawBusy;
            GUILayout.BeginHorizontal();
            foreach (var pod in _pods.OrderBy(pod => pod.data.id)) if (GUILayout.Button(Label(pod))) _selected = pod;
            GUILayout.EndHorizontal();
            GUI.enabled = enabled;
            if (!_drawSelected) return;
            GUI.enabled = enabled && !_drawBusy;
            try
            {
                GUILayout.Label(Label(_drawPod) + "の割り当て。どこからでも設定できます。変更は保存後のF操作から使われます。");
                GUILayout.Label("女性カード：" + (string.IsNullOrWhiteSpace(_drawAssignment.card) ? "未割当（登場しません）" : Path.GetFileName(_drawAssignment.card)));
                GUILayout.Label("初期コーデ：" + (string.IsNullOrWhiteSpace(_drawAssignment.coordinate) ? "女性カード内の服装" : Path.GetFileName(_drawAssignment.coordinate)));
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("このカプセルの割り当てを保存")) SaveAssignment(_drawPod, _drawAssignment);
                if (GUILayout.Button("JSONから割り当てを再読込")) ReloadAssignments();
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("女性の割り当てを解除")) _drawAssignment.card = "";
                if (GUILayout.Button("コーデを女性カード内の服装へ戻す")) _drawAssignment.coordinate = "";
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("女性カード一覧を開く・更新")) ListCards(false);
                if (GUILayout.Button("コーデカード一覧を開く・更新")) ListCards(true);
                GUILayout.EndHorizontal();
                GUILayout.Label(_drawCoordinates ? "初期コーデの画像を選び、割り当てを保存してください。" : "女性の画像を選び、割り当てを保存してください。");
                if (_drawPicker) _assignmentPicker.Draw(_drawCoordinates ? _drawAssignment.coordinate : _drawAssignment.card,
                    path => { if (_drawCoordinates) _drawAssignment.coordinate = path; else _drawAssignment.card = path; });
            }
            finally { GUI.enabled = enabled; }
        }
        private void ListCards(bool coordinates)
        {
            try
            {
                string directory = P.T(coordinates ? "pod.coordinate.directory" : "animation.card.directory");
                if (_assignmentPicker == null) _assignmentPicker = new AiCardPicker(P, directory, Failure); else _assignmentPicker.Refresh(directory);
                _listingCoordinates = coordinates;
            }
            catch (Exception ex) { Failure(ex); }
        }
        private void ConfigureCardPicker(Parameters settings) { _assignmentPicker?.Configure(settings); }
        private string ValidateCard(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            path = Path.GetFullPath(path.Trim());
            if (!File.Exists(path)) throw new FileNotFoundException("女性カードのファイルがありません。", path);
            var card = new ChaFileControl(); if (!card.LoadCharaFile(path) || card.parameter.sex != P.I("math.one")) throw new InvalidDataException("KKSの女性カードではありません。");
            return path;
        }
        private string ValidateCoordinate(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            path = Path.GetFullPath(path.Trim());
            if (!File.Exists(path)) throw new FileNotFoundException("コーデカードのファイルがありません。", path);
            var coordinate = new ChaFileCoordinate(); coordinate.MemberInit();
            if (!coordinate.LoadFile(path)) throw new InvalidDataException("KKSのコーデカードではありません。");
            return path;
        }
        private void SaveAssignment(Pod pod, Assignment assignment)
        {
            try
            {
                string card = ValidateCard(assignment.card), coordinate = ValidateCoordinate(assignment.coordinate);
                var next = P.Copy(); next.text.Single(item => item.key == Key(pod, "card")).value = card; next.text.Single(item => item.key == Key(pod, "coordinate")).value = coordinate;
                next.revision = Guid.NewGuid().ToString("N"); _host.Session.Apply(next, true);
                assignment.card = card; assignment.coordinate = coordinate;
                _status = Label(pod) + "の割り当てを保存しました。"; _log("AI_POD_ASSIGNMENT_SAVED id=" + pod.data.id + " revision=" + next.revision);
            }
            catch (Exception ex) { Failure(ex); }
        }
        private void ReloadAssignments()
        {
            try { _host.Session.Apply(Parameters.Load(_parameterPath), false); ReadAssignments(); _status = "JSONから各カプセルの保存済み割り当てを再読込しました。"; }
            catch (Exception ex) { Failure(ex); }
        }
        private void BeginSpawn(Pod pod)
        {
            if (_busy) return;
            try
            {
                string path = ValidateCard(P.T(Key(pod, "card"))), coordinate = ValidateCoordinate(P.T(Key(pod, "coordinate")));
                if (string.IsNullOrEmpty(path)) throw new InvalidOperationException(Label(pod) + "の女性カードが未割当です。");
                _busy = true; _operationPod = pod; _status = Label(pod) + "を開いています。";
                StartCoroutine(Guard(Spawn(pod, path, coordinate)));
            }
            catch (Exception ex) { _busy = false; Failure(ex); if (!_host.Session.Panel) _host.Session.TogglePanel(); }
        }
        private IEnumerator Guard(IEnumerator operation)
        {
            while (true)
            {
                bool next;
                try { next = operation.MoveNext(); }
                catch (Exception ex)
                {
                    _characters.CancelPodSequence();
                    if (_operationPod != null) { try { Play(_operationPod, _manifest.closedIdle); } catch (Exception restore) { _log("AI_POD_RESTORE_ERROR " + restore); } }
                    _busy = false; _operationPod = null; Failure(ex); if (!_host.Session.Panel) _host.Session.TogglePanel(); yield break;
                }
                if (!next) yield break;
                yield return operation.Current;
            }
        }
        private IEnumerator Spawn(Pod pod, string path, string coordinate)
        {
            World(pod.data.pivot, out Vector3 position, out Quaternion rotation);
            if (!_characters.SpawnAt(path, coordinate, position, rotation, "pod:" + pod.data.id.ToString(System.Globalization.CultureInfo.InvariantCulture))) throw new InvalidOperationException("カプセル内への女性配置に失敗しました。女の子欄のエラーを確認してください。");
            bool opened = false, completed = false, closing = false;
            Exception failure = null;
            Action close = () =>
            {
                if (!opened || !completed || closing || failure != null) return;
                closing = true; StartCoroutine(Guard(CloseAfterAppearance(pod, path, coordinate)));
            };
            int hash = Play(pod, _manifest.open);
            _characters.BeginPodAppearance(() => { completed = true; close(); }, ex =>
            {
                failure = ex;
                if (opened)
                {
                    _characters.CancelPodSequence();
                    Play(pod, _manifest.closedIdle); _busy = false; _operationPod = null; Failure(ex);
                    if (!_host.Session.Panel) _host.Session.TogglePanel();
                }
            });
            yield return new WaitForSecondsRealtime(Duration(pod));
            if (failure != null) throw failure;
            RequireFinished(pod, hash, _manifest.openIdle); Play(pod, _manifest.openIdle);
            opened = true; _status = "女の子がカプセルから出ています。"; close();
        }
        private IEnumerator CloseAfterAppearance(Pod pod, string path, string coordinate)
        {
            foreach (string node in pod.data.enabledNodes ?? Array.Empty<string>()) Find(node).gameObject.SetActive(true);
            foreach (string node in pod.data.disabledNodes ?? Array.Empty<string>()) Find(node).gameObject.SetActive(false);
            _status = "女性が出口へ登場しました。カプセルを閉じます。";
            yield return new WaitForSecondsRealtime(P.N("pod.delayClose"));
            int hash = Play(pod, _manifest.close);
            yield return new WaitForSecondsRealtime(Duration(pod));
            RequireFinished(pod, hash, _manifest.closedIdle); Play(pod, _manifest.closedIdle);
            World(pod.data.recover, out Vector3 recovery, out Quaternion rotation);
            _characters.CompletePodSequence(recovery, rotation);
            _busy = false; _operationPod = null; _status = "登場完了。女の子は状態に応じて自律行動を始めます。";
            _log("AI_POD_SPAWN_COMPLETE id=" + pod.data.id + " card=" + path + " coordinate=" + (string.IsNullOrEmpty(coordinate) ? "character-card" : coordinate) + " entryAnimation=" + P.T("podAnimation.sequence"));
        }
        private int State(Pod pod, string name)
        {
            int layer = P.I("math.zero"), hash = Animator.StringToHash(pod.animator.GetLayerName(layer) + "." + name);
            if (!pod.animator.HasState(layer, hash)) throw new InvalidDataException("カプセルstateがありません：" + name);
            return hash;
        }
        private int Play(Pod pod, string state)
        {
            int hash = State(pod, state);
            pod.animator.speed = P.N("pod.animationSpeed"); pod.animator.Play(hash, P.I("math.zero"), P.N("math.zero")); pod.animator.Update(P.N("math.zero"));
            Physics.SyncTransforms(); return hash;
        }
        private float Duration(Pod pod)
        {
            var state = pod.animator.GetCurrentAnimatorStateInfo(P.I("math.zero"));
            float speed = Mathf.Abs(pod.animator.speed * state.speed * state.speedMultiplier);
            if (state.length <= P.N("render.epsilon") || speed <= P.N("render.epsilon")) throw new InvalidDataException("カプセルの実アニメ長・速度を取得できません。");
            return state.length / speed + P.N("render.epsilon");
        }
        private void RequireFinished(Pod pod, int started, string idle)
        {
            var state = pod.animator.GetCurrentAnimatorStateInfo(P.I("math.zero"));
            if (state.fullPathHash != State(pod, idle) && (state.fullPathHash != started || state.normalizedTime + P.N("render.epsilon") < P.N("math.one")))
                throw new InvalidOperationException("カプセルの開閉が完了しませんでした。");
        }
        private void Failure(Exception ex) { _status = ex.GetBaseException().Message; _log("AI_POD_ERROR " + ex); }
        private void OnDestroy()
        {
            if (_host?.Session != null) _host.Session.ParametersApplied -= ConfigureCardPicker;
            _assignmentPicker?.Dispose(); _assignmentPicker = null;
            StopAllCoroutines();
            _characters?.CancelPodSequence();
            foreach (var pod in _pods)
            {
                if (pod.animator == null) continue;
                try
                {
                    var animator = pod.animator; animator.fireEvents = false; animator.runtimeAnimatorController = pod.controller;
                    if (pod.controller != null)
                    {
                        animator.Rebind();
                        for (int layer = P.I("math.zero"); layer < pod.states.Length; layer++) animator.Play(pod.states[layer].fullPathHash, layer, pod.states[layer].normalizedTime);
                        animator.Update(P.N("math.zero"));
                    }
                    foreach (var saved in pod.transforms) if (saved.transform != null) { saved.transform.localPosition = saved.position; saved.transform.localRotation = saved.rotation; saved.transform.localScale = saved.scale; }
                    foreach (var active in pod.active) if (active.Key != null) active.Key.SetActive(active.Value);
                    foreach (var collider in pod.colliders) if (collider.Key != null) collider.Key.enabled = collider.Value;
                    animator.speed = pod.speed; animator.cullingMode = pod.culling; animator.updateMode = pod.update; animator.applyRootMotion = pod.rootMotion; animator.fireEvents = pod.events; animator.enabled = pod.enabled;
                }
                catch (Exception ex) { _log?.Invoke("AI_POD_CLEANUP_ERROR " + ex); }
            }
            Physics.SyncTransforms(); _pods.Clear();
        }
    }
}
