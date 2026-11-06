using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using MapEnvironmentRuntime;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace MainGameAiShoujoMode.Authoring
{
    internal sealed class StageMap
    {
        internal string Id, Name, Bundle, Scene, Error;
        internal GameObject[] Roots;
        internal Func<IEnumerator> Load;
        internal Func<GameObject[]> GetRoots;
        internal Action Release;
        internal bool Pending;
        internal event Action Completed;
        internal void CompletePending() { Pending = false; var action = Completed; Completed = null; action?.Invoke(); }
    }
    internal sealed partial class AuthoringStage : IDisposable
    {
        private readonly MonoBehaviour owner;
        private readonly Func<Camera> sourceCamera;
        private readonly Func<string, ChaControl> resolve;
        private readonly Func<Action> suspendActors;
        private readonly Action<bool> block;
        private readonly AuthoringStore store;
        private readonly List<StageMap> maps;
        private readonly Dictionary<Renderer, bool> renderers = new Dictionary<Renderer, bool>();
        private readonly Dictionary<Behaviour, bool> behaviours = new Dictionary<Behaviour, bool>();
        private readonly Dictionary<Collider, bool> colliders = new Dictionary<Collider, bool>();
        private readonly Dictionary<GameObject, bool> roots = new Dictionary<GameObject, bool>();
        private readonly Dictionary<ChaControl, ActorState> actors = new Dictionary<ChaControl, ActorState>();
        private readonly Dictionary<string, ChaControl> assigned = new Dictionary<string, ChaControl>();
        private readonly Dictionary<string, string> bindings = new Dictionary<string, string>();
        private readonly Dictionary<string, GameObject> spawned = new Dictionary<string, GameObject>();
        private readonly List<GameObject> created = new List<GameObject>();
        private readonly List<Scene> scenes = new List<Scene>();
        private readonly HashSet<StageMap> customLoads = new HashSet<StageMap>();
        private readonly List<Tuple<StageMap, Action>> mapWaiters = new List<Tuple<StageMap, Action>>();
        private Action resume;
        private Camera original, camera;
        private bool originalEnabled, active, paused;
        internal bool Active => active;
        private int generation;
        private AudioSource audio;
        private AudioClip externalClip;
        private UnityWebRequest request;
        private Coroutine loadingAudio;
        private Tween motion, deadline;
        private Material skybox;
        private Color ambient, fogColor;
        private bool fog;
        private float fogDensity;
        internal Camera Camera => SpecialCamera ?? (camera != null ? camera : sourceCamera());
        internal Camera SpecialCamera { get; set; }
        internal Action<Camera> CameraPoseApplied { get; set; }
        partial void CameraApplied(Camera target);
        internal GameObject MapRoot { get; private set; }
        partial void CaptureExtra(ChaControl actor);
        partial void RestoreExtras();
        partial void PauseExtras(bool paused);
        partial void ApplyCondition(ChaControl actor, ActorCondition condition);
        partial void ApplyPlacementMotion(ChaControl actor, MotionCue cue);
        private sealed class ActorState
        {
            internal Transform transform; internal Vector3 position; internal Quaternion rotation;
            internal int eyes, mouth, brows; internal AudioSource lip;
            internal ActorState(ChaControl actor, bool useParent = true) { transform = useParent ? actor.transform.parent ?? actor.transform : actor.transform; position = transform.position; rotation = transform.rotation; eyes = actor.GetEyesPtn(); mouth = actor.GetMouthPtn(); brows = actor.GetEyebrowPtn(); lip = actor.asVoice; }
            internal void Restore(ChaControl actor) { if (transform != null) transform.SetPositionAndRotation(position, rotation); actor.ChangeEyesPtn(eyes); actor.ChangeMouthPtn(mouth); actor.ChangeEyebrowPtn(brows); actor.SetLipSync(lip); }
        }
        internal AuthoringStage(MonoBehaviour owner, Func<Camera> camera, Func<string, ChaControl> resolve, Func<Action> suspendActors, Action<bool> block, AuthoringStore store, List<StageMap> maps)
        { this.owner = owner; sourceCamera = camera; this.resolve = resolve; this.suspendActors = suspendActors; this.block = block; this.store = store; this.maps = maps; }
        internal void Begin(AuthoringProject project)
        {
            if (active) return;
            original = sourceCamera();
            if (original == null) throw new InvalidOperationException("プレイ画面のカメラがありません。");
            active = true; generation++;
            paused = false;
            originalEnabled = original.enabled; skybox = RenderSettings.skybox; ambient = RenderSettings.ambientLight; fog = RenderSettings.fog; fogDensity = RenderSettings.fogDensity; fogColor = RenderSettings.fogColor;
            block(true); resume = suspendActors?.Invoke();
            var node = new GameObject("Event authoring camera"); node.transform.SetParent(owner.transform, false); camera = node.AddComponent<Camera>(); camera.CopyFrom(original);
            node.transform.SetPositionAndRotation(original.transform.position, original.transform.rotation); original.enabled = false;
            RefreshBindings(project);
        }
        internal void RefreshBindings(AuthoringProject project)
        {
            foreach (var id in assigned.Keys.Where(id => !project.characters.characters.Any(c => c.id == id)).ToArray()) { assigned.Remove(id); bindings.Remove(id); }
            foreach (var preset in project.characters.characters)
            {
                var key = preset.binding + "/" + preset.card;
                if (bindings.TryGetValue(preset.id, out var previous) && previous == key) continue;
                if (spawned.TryGetValue(preset.id, out var old) && old != null) { foreach (var a in old.GetComponentsInChildren<ChaControl>(true)) Manager.Character.DeleteChara(a); created.Remove(old); UnityEngine.Object.Destroy(old); spawned.Remove(preset.id); }
                assigned.Remove(preset.id); bindings[preset.id] = key;
                var actor = preset.binding == "card" ? CreateActor(preset) : resolve(preset.binding);
                if (actor == null) continue;
                assigned.Add(preset.id, actor); if (!actors.ContainsKey(actor)) { actors.Add(actor, new ActorState(actor)); CaptureExtra(actor); }
            }
        }
        private ChaControl CreateActor(CharacterPreset preset)
        {
            string path = store.Resolve(preset.card); var file = new ChaFileControl();
            if (!File.Exists(path) || !file.LoadCharaFile(path)) throw new InvalidDataException("カードを読めません：" + path);
            var root = new GameObject("Event character " + preset.id); root.transform.SetParent(owner.transform, false); created.Add(root);
            spawned[preset.id] = root;
            int id = UnityEngine.Object.FindObjectsOfType<ChaControl>().Select(a => a.chaID).DefaultIfEmpty(default(int)).Max() + Numbers.One;
            var actor = file.parameter.sex == Numbers.One ? Manager.Character.CreateFemale(root, id, file, true) : Manager.Character.CreateMale(default(int), root, id, file, true); actor.Load();
            if (!actor.loadEnd) throw new InvalidOperationException("カードのモデル読込が完了していません：" + preset.name);
            actor.SetActiveTop(true); actor.visibleAll = true; return actor;
        }
        internal ChaControl Actor(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (!assigned.TryGetValue(id, out var actor) || actor == null) throw new InvalidOperationException("登場人物を割り当てられません：" + id);
            return actor;
        }
        internal ChaControl CurrentActor(CharacterPreset preset)
        {
            if (assigned.TryGetValue(preset.id, out var actor) && actor != null) return actor;
            actor = preset.binding == "card" ? null : resolve(preset.binding);
            if (actor == null) throw new InvalidOperationException("この人物は未配置です。先にイベントを試し再生してください。");
            return actor;
        }
        internal void Place(IEnumerable<ActorPlacement> placements)
        {
            foreach (var p in placements)
            {
                var actor = Actor(p.characterId); var target = actors[actor].transform;
                target.SetPositionAndRotation(Vector(p.position), Quaternion.Euler(Vector(p.rotation)));
                ApplyCondition(actor, p.condition); if (p.animation != null) ApplyPlacementMotion(actor, p.animation);
            }
            Physics.SyncTransforms();
        }
        internal void Expression(string id, int expression)
        {
            if (expression < default(int) || string.IsNullOrEmpty(id)) return;
            var actor = Actor(id);
            if (expression >= actor.GetEyesPtnNum()) throw new InvalidDataException("目の表情番号が範囲外です：" + expression);
            actor.ChangeEyesPtn(expression);
        }
        internal void CameraTo(CameraCue cue, Action completed)
        {
            motion?.Kill(); var target = Camera;
            if (cue == null) { completed(); return; }
            cue.Validate("camera");
            if (cue.seconds == default(int)) { target.transform.SetPositionAndRotation(Vector(cue.position), Quaternion.Euler(Vector(cue.rotation))); target.fieldOfView = cue.fieldOfView; CameraApplied(target); CameraPoseApplied?.Invoke(target); completed(); return; }
            var from = target.transform.position; var rotation = target.transform.rotation; float fov = target.fieldOfView;
            motion = DOVirtual.Float(default(int), Numbers.One, cue.seconds, t => { if (target == null) return; target.transform.SetPositionAndRotation(Vector3.LerpUnclamped(from, Vector(cue.position), t), Quaternion.SlerpUnclamped(rotation, Quaternion.Euler(Vector(cue.rotation)), t)); target.fieldOfView = Mathf.LerpUnclamped(fov, cue.fieldOfView, t); CameraPoseApplied?.Invoke(target); })
                .SetEase(cue.ease == "linear" ? Ease.Linear : Ease.InOutSine).SetUpdate(true).OnComplete(() => { CameraApplied(target); completed(); });
        }
        internal static Point Point(Vector3 p) => new Point { x = p.x, y = p.y, z = p.z };
        internal static Vector3 Vector(Point p) => p == null ? Vector3.zero : new Vector3(p.x, p.y, p.z);
        internal CameraCue Capture() { var c = Camera; if (c == null) throw new InvalidOperationException("カメラがありません。"); return new CameraCue { position = Point(c.transform.position), rotation = Point(c.transform.eulerAngles), fieldOfView = c.fieldOfView }; }
        internal void Pause(bool paused)
        {
            this.paused = paused;
            PauseExtras(paused);
            if (paused) { motion?.Pause(); deadline?.Pause(); if (audio != null) audio.Pause(); }
            else { motion?.Play(); deadline?.Play(); if (audio != null) audio.UnPause(); }
        }
        internal void Wait(float seconds, Action completed) { deadline?.Kill(); if (seconds == default(int)) { completed(); return; } deadline = DOVirtual.DelayedCall(seconds, () => completed(), true); }
        internal void CancelCue() { motion?.Kill(); deadline?.Kill(); motion = deadline = null; StopVoice(); }
        internal void StopVoice()
        {
            request?.Abort(); request?.Dispose(); request = null;
            if (loadingAudio != null && owner != null) owner.StopCoroutine(loadingAudio); loadingAudio = null;
            if (audio != null) { audio.Stop(); audio.clip = null; }
            foreach (var pair in actors) if (pair.Key != null && pair.Key.asVoice == audio) pair.Key.SetLipSync(pair.Value.lip);
            if (externalClip != null) UnityEngine.Object.Destroy(externalClip); externalClip = null;
        }
        internal void PlayVoice(VoiceReference voice, string characterId, Action<Exception> failed)
        {
            StopVoice(); if (voice == null || voice.source == "none") return;
            voice.Validate("voice");
            if (audio == null) { audio = Manager.Voice.Create(owner.transform); audio.playOnAwake = false; }
            var actor = Actor(characterId);
            void Play(AudioClip clip)
            {
                if (clip == null) throw new FileNotFoundException("音声素材がありません。");
                audio.clip = clip; audio.loop = false; audio.spatialBlend = default(int); audio.volume = voice.volume; audio.pitch = voice.pitch;
                if (actor != null) { audio.transform.SetParent(actor.transform, false); actor.SetLipSync(audio); }
                else audio.transform.SetParent(owner.transform, false);
                audio.Play();
                if (paused) audio.Pause();
            }
            if (voice.source == "game") { Play(new AssetBundleData(voice.bundle, voice.asset).GetAsset<AudioClip>()); return; }
            var path = store.Resolve(voice.path); if (!File.Exists(path)) throw new FileNotFoundException("音声ファイルがありません：" + path);
            var extension = Path.GetExtension(path).ToLowerInvariant();
            AudioType type = extension == ".wav" ? AudioType.WAV : extension == ".ogg" ? AudioType.OGGVORBIS : extension == ".mp3" ? AudioType.MPEG : AudioType.UNKNOWN;
            if (type == AudioType.UNKNOWN) throw new InvalidDataException("外部音声はWAV/OGG/MP3を指定してください。");
            int token = generation;
            request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, type);
            loadingAudio = owner.StartCoroutine(LoadAudio(request, token, Play, failed));
        }
        private IEnumerator LoadAudio(UnityWebRequest load, int token, Action<AudioClip> play, Action<Exception> failed)
        {
            yield return load.SendWebRequest();
            if (!active || generation != token || !ReferenceEquals(load, request)) { load.Dispose(); yield break; }
            try { if (load.isHttpError || load.isNetworkError) throw new IOException(load.error); externalClip = DownloadHandlerAudioClip.GetContent(load); play(externalClip); }
            catch (Exception ex) { failed(ex); }
            finally { if (ReferenceEquals(request, load)) request = null; load.Dispose(); loadingAudio = null; }
        }
        internal void Map(string id, Action completed, Action<Exception> failed)
        {
            if (string.IsNullOrEmpty(id) || id == "current") { RestoreMapVisibility(); MapRoot = ChooseRoot(maps.Where(m => m.Roots != null).SelectMany(m => m.Roots).Where(r => r != null && r.activeSelf)); completed(); return; }
            var map = maps.SingleOrDefault(m => m.Id == id); if (map == null) throw new InvalidDataException("マップIDがありません：" + id);
            if (!string.IsNullOrEmpty(map.Error)) throw new InvalidOperationException(map.Error);
            if (map.Pending)
            {
                int token = generation; Action ready = null;
                ready = () => { mapWaiters.RemoveAll(w => w.Item2 == ready); if (active && generation == token && owner != null) { try { Map(id, completed, failed); } catch (Exception ex) { failed(ex); } } };
                mapWaiters.Add(Tuple.Create(map, ready)); map.Completed += ready; return;
            }
            if (map.Roots != null) { ShowMap(map.Roots); completed(); return; }
            if (map.Load != null) { map.Pending = true; customLoads.Add(map); AuthoringLoadJob.Run(LoadCustom(map, generation, completed, failed)); return; }
            var loaded = SceneManager.GetSceneByName(map.Scene);
            if (loaded.IsValid() && loaded.isLoaded) { map.Roots = loaded.GetRootGameObjects(); ShowMap(map.Roots); completed(); return; }
            map.Pending = true; LoadNative(map, generation, completed, failed);
        }
        private async void LoadNative(StageMap map, int token, Action completed, Action<Exception> failed)
        {
            try
            {
                var operation = await AssetBundleManager.LoadLevelAsync(map.Bundle, map.Scene, true, null);
                if (operation == null) throw new IOException("マップ読込処理を開始できません：" + map.Name);
                void Finish()
                {
                    var scene = SceneManager.GetSceneByName(map.Scene);
                    if (!scene.IsValid() || !scene.isLoaded) { map.CompletePending(); if (active && token == generation) failed(new IOException("マップシーンがありません：" + map.Name)); return; }
                    if (!active || token != generation || owner == null) { Unload(map, scene); return; }
                    try
                    {
                    scenes.Add(scene); map.Roots = scene.GetRootGameObjects();
                    // An imported map's cameras/listeners must never replace the owning mode's camera.
                    foreach (var root in map.Roots) { foreach (var c in root.GetComponentsInChildren<Camera>(true)) c.enabled = false; foreach (var a in root.GetComponentsInChildren<AudioListener>(true)) a.enabled = false; root.SetActive(false); }
                    ShowMap(map.Roots); map.CompletePending(); completed();
                    }
                    catch (Exception ex) { failed(ex); }
                }
                if (operation.isDone) Finish(); else operation.completed += _ => Finish();
            }
            catch (Exception ex) { map.CompletePending(); if (active && token == generation) failed(ex); }
        }
        private static void Unload(StageMap map, Scene scene)
        {
            map.Roots = null; map.Pending = true;
            var operation = SceneManager.UnloadSceneAsync(scene);
            if (operation == null || operation.isDone) map.CompletePending(); else operation.completed += _ => map.CompletePending();
        }
        private IEnumerator LoadCustom(StageMap map, int token, Action completed, Action<Exception> failed)
        {
            var steps = new Stack<IEnumerator>();
            try { steps.Push(map.Load()); } catch (Exception ex) { customLoads.Remove(map); ReleaseCustom(map); if (active && generation == token) failed(ex); yield break; }
            while (steps.Count != default(int))
            {
                bool next; object instruction = null;
                try { next = steps.Peek().MoveNext(); if (next) instruction = steps.Peek().Current; }
                catch (Exception ex) { customLoads.Remove(map); ReleaseCustom(map); if (active && generation == token) failed(ex); yield break; }
                if (!next) { (steps.Pop() as IDisposable)?.Dispose(); continue; }
                if (instruction is IEnumerator nested) steps.Push(nested); else yield return instruction;
            }
            customLoads.Remove(map);
            if (!active || generation != token || owner == null) { ReleaseCustom(map); yield break; }
            try { map.Roots = map.GetRoots(); foreach (var root in map.Roots) if (root != null) root.SetActive(false); ShowMap(map.Roots); map.CompletePending(); completed(); }
            catch (Exception ex) { failed(ex); }
        }
        private static void ReleaseCustom(StageMap map)
        {
            if (map.Roots == null && !map.Pending) return;
            map.Roots = null;
            var scene = string.IsNullOrEmpty(map.Scene) ? default(Scene) : SceneManager.GetSceneByName(map.Scene);
            if (!scene.IsValid() || !scene.isLoaded) { map.Release?.Invoke(); map.CompletePending(); return; }
            map.Pending = true; UnityEngine.Events.UnityAction<Scene> unloaded = null;
            unloaded = value => { if (value.name != map.Scene) return; SceneManager.sceneUnloaded -= unloaded; map.CompletePending(); };
            SceneManager.sceneUnloaded += unloaded;
            try { map.Release?.Invoke(); } catch { SceneManager.sceneUnloaded -= unloaded; map.CompletePending(); throw; }
        }
        private void ShowMap(GameObject[] selected)
        {
            MapRoot = ChooseRoot(selected.Where(r => r != null));
            RestoreMapVisibility();
            var chosen = new HashSet<GameObject>(selected);
            foreach (var map in maps.Where(m => m.Roots != null)) foreach (var root in map.Roots)
            {
                if (root == null || chosen.Contains(root)) continue;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true)) { if (!renderers.ContainsKey(r)) renderers.Add(r, r.enabled); r.enabled = false; }
                foreach (var c in root.GetComponentsInChildren<Collider>(true)) { if (!colliders.ContainsKey(c)) colliders.Add(c, c.enabled); c.enabled = false; }
                foreach (var l in root.GetComponentsInChildren<Light>(true)) { if (!behaviours.ContainsKey(l)) behaviours.Add(l, l.enabled); l.enabled = false; }
                foreach (var t in root.GetComponentsInChildren<Terrain>(true)) { if (!behaviours.ContainsKey(t)) behaviours.Add(t, t.enabled); t.enabled = false; }
            }
            foreach (var root in selected) if (root != null) { if (!roots.ContainsKey(root)) roots.Add(root, root.activeSelf); root.SetActive(true); }
        }
        private void RestoreMapVisibility()
        {
            foreach (var pair in renderers) if (pair.Key != null) pair.Key.enabled = pair.Value; renderers.Clear();
            foreach (var pair in colliders) if (pair.Key != null) pair.Key.enabled = pair.Value; colliders.Clear();
            foreach (var pair in behaviours) if (pair.Key != null) pair.Key.enabled = pair.Value; behaviours.Clear();
            foreach (var pair in roots) if (pair.Key != null) pair.Key.SetActive(pair.Value); roots.Clear();
        }
        private static GameObject ChooseRoot(IEnumerable<GameObject> roots)
        { var choices = roots.ToArray(); return choices.FirstOrDefault(r => r.name == BaseMap.MAP_ROOT_NAME) ?? choices.OrderByDescending(r => r.GetComponentsInChildren<Renderer>(true).Length).FirstOrDefault(); }
        public void Dispose()
        {
            if (!active) return; active = false; generation++;
            motion?.Kill(); deadline?.Kill(); motion = deadline = null; StopVoice();
            foreach (var waiter in mapWaiters) waiter.Item1.Completed -= waiter.Item2; mapWaiters.Clear();
            RestoreExtras(); SpecialCamera = null; CameraPoseApplied = null;
            RestoreMapVisibility();
            foreach (var map in maps.Where(m => m.Release != null && !customLoads.Contains(m))) ReleaseCustom(map);
            foreach (var pair in actors) if (pair.Key != null) pair.Value.Restore(pair.Key); actors.Clear(); assigned.Clear(); bindings.Clear(); spawned.Clear();
            foreach (var node in created) if (node != null) foreach (var actor in node.GetComponentsInChildren<ChaControl>(true)) Manager.Character.DeleteChara(actor);
            foreach (var node in created) if (node != null) UnityEngine.Object.Destroy(node); created.Clear();
            foreach (var scene in scenes) if (scene.IsValid() && scene.isLoaded) { var map = maps.First(m => m.Scene == scene.name); Unload(map, scene); } scenes.Clear();
            if (camera != null) UnityEngine.Object.Destroy(camera.gameObject); camera = null;
            if (original != null) original.enabled = originalEnabled;
            RenderSettings.skybox = skybox; RenderSettings.ambientLight = ambient; RenderSettings.fog = fog; RenderSettings.fogDensity = fogDensity; RenderSettings.fogColor = fogColor;
            if (audio != null) UnityEngine.Object.Destroy(audio.gameObject); audio = null;
            var restore = resume; resume = null; try { restore?.Invoke(); } finally { block(false); }
        }
    }
    // Finite IO jobs outlive a closing mode only until their owned load finishes and can be released.
    internal sealed class AuthoringLoadJob : MonoBehaviour
    {
        internal static void Run(IEnumerator operation)
        {
            var root = new GameObject("Event map load completion"); DontDestroyOnLoad(root);
            root.AddComponent<AuthoringLoadJob>().StartCoroutine(Complete(root, operation));
        }
        private static IEnumerator Complete(GameObject root, IEnumerator operation)
        {
            try { while (operation.MoveNext()) yield return operation.Current; }
            finally { (operation as IDisposable)?.Dispose(); if (root != null) Destroy(root); }
        }
    }
}
