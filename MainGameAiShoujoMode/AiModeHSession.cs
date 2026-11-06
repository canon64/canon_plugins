using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ActionGame;
using ActionGame.H;
using Cysharp.Threading.Tasks;
using FreeH;
using HarmonyLib;
using H;
using MapEnvironmentRuntime;
using SaveData;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using GameScene = Manager.Scene;

namespace MainGameAiShoujoMode
{
    // Owns only this mode's standard-H boundary. Native H animation/rendering stays native.
    public sealed partial class AiModeHSession : IDisposable
    {
        public sealed class HLocation
        {
            public string SourceId;
            public Vector3 Position;
            public Quaternion Rotation;
            public int[] Categories;
            public bool VerifiedGround;
        }

        private static AiModeHSession _active;
        private MonoBehaviour _host;
        private Parameters _p;
        private Action _started, _ended, _ready;
        private Action<string> _log;
        private Action<Exception> _error;
        private Harmony _harmony;
        private Heroine _heroine;
        private Player _player;
        private WorldData _previousWorld, _temporaryWorld;
        private GameObject _femaleRoot, _maleRoot, _island, _anchor;
        private HLocation _location;
        private HPointData _point;
        private HSceneProc _proc;
        private AiHItemMenu _itemMenu;
        private KsPlugHSessionExit _ksPlugExit;
        private ActionMap _map;
        private Coroutine _deadline;
        private bool _disposed, _finishing, _sceneSeen, _aborting;
        private bool _globalsCaptured, _previousHandCorrection, _islandActive;
        private Transform _previousSoundListener;
        private OpenHData _open;
        private readonly HashSet<string> _loadedScenes = new HashSet<string>();
        public bool Busy { get; private set; }
        internal HSceneProc LightingScene => _proc;
        public Camera ActiveCamera => _proc != null && _proc.flags != null && _proc.flags.ctrlCamera != null ? _proc.flags.ctrlCamera.thisCmaera : null;
        private void BeforeGateSessionEnding(CanonGate.GateApi.SessionContext context)
        { if (AllRoomSelections && RoomReady && context?.Scene == _proc) Cleanup(() => RoomBeforeEnding?.Invoke()); }

        public void Initialize(MonoBehaviour host, Parameters settings, Action onStarted, Action onEnded, Action<string> log, Action<Exception> error, Action onReady = null)
        {
            _host = host; _p = settings; _started = onStarted; _ended = onEnded; _log = log; _error = error; _ready = onReady;
            _harmony = new Harmony("canon.MainGameAiShoujoMode.HSession");
            CanonGate.GateApi.SessionEnding += BeforeGateSessionEnding;
            Patch(typeof(HSceneProc), "Start", nameof(BeforeHStart), nameof(AfterHStart));
            Patch(typeof(HScene), "Start", null, nameof(AfterOuterStart));
            Patch(typeof(HSceneProc), "OnDestroy", nameof(BeforeHDestroy), nameof(AfterHDestroy));
            Patch(typeof(ActionMap), "Awake", null, nameof(AfterMapAwake));
            Patch(typeof(ActionMap), "Start", nameof(BeforeMapStart), null);
            Patch(typeof(BaseMap), "Change", nameof(BeforeMapChange), null, new[] { typeof(int), typeof(FadeCanvas.Fade), typeof(bool) });
            Patch(typeof(HSceneProc), "GetCloseCategory", nameof(BeforeCategories), null);
            Patch(typeof(HSceneProc), "LoadMapDependent", nameof(BeforeMapDependent), null);
            Patch(typeof(HSceneProc), "GotoPointMoveScene", nameof(BeforePointMoveScene), null);
            Patch(typeof(HSceneProc), "ChangeAnimator", null, nameof(AfterRoomSelection));
            Patch(typeof(HSceneProc), "SetLocalPosition", null, nameof(AfterRoomPositionReset));
            Patch(typeof(HSceneProc), "CreateListAnimationFileName", null, nameof(AfterRoomCatalog));
            Patch(typeof(HSprite), "CreateMotionList", nameof(BeforeNativeMotionMenu), null);
            Patch(typeof(HSprite), "CloseMotionSubMenu", nameof(BeforeNativeMotionMenu), null);
            Patch(typeof(GameScene), "UnloadBaseSceneAsync", nameof(BeforeBaseUnload), null, new[] { typeof(string) });
            SceneManager.sceneLoaded += SceneLoaded;
            SceneManager.sceneUnloaded += SceneUnloaded;
        }

        public void Begin(Heroine heroine, Player player, GameObject island, HLocation location)
        {
            if (_disposed || Busy || _active != null) throw new InvalidOperationException("Hの開始処理中です。");
            if (heroine?.charFile == null || (player != null && player.charFile == null) || island == null) throw new InvalidOperationException("H参加キャラと島の情報が不足しています。");
            if (location == null || !location.VerifiedGround || location.Categories == null || !location.Categories.Any()) throw new InvalidOperationException("この場所のH地点情報は未確認です。");
            if (Object.FindObjectOfType<HSceneProc>() != null) throw new InvalidOperationException("別のHシーンが動作しています。");
            if (GameScene.commonSpace == null || GameScene.commonSpace.GetComponent<OpenHData>() != null) throw new InvalidOperationException("前のH開始データが残っているか、共通シーンが未初期化です。");
            RoomReady = false;
            _ksPlugExit = new KsPlugHSessionExit();
            _heroine = heroine; _femaleRoot = heroine.root;
            _island = island; _location = location; _sceneSeen = false; _aborting = false; _loadedScenes.Clear();
            Busy = true; _active = this;
            try
            {
                _previousHandCorrection = Manager.Character.enableCorrectHandSize;
                _previousSoundListener = Manager.Sound.Listener;
                _islandActive = island.activeSelf;
                _globalsCaptured = true;
                _previousWorld = Manager.Game.saveData;
                if (_previousWorld == null)
                {
                    _temporaryWorld = new WorldData();
                    _temporaryWorld.player.Randomize();
                    Set(null, typeof(Manager.Game), "saveData", _temporaryWorld);
                }
                _player = player ?? (AllRoomSelections
                    ? new Player(HotelRoomAppearanceRuntime.CloneCard(Manager.Game.saveData.player.charFile), false)
                    : Manager.Game.saveData.player);
                _maleRoot = _player.root;
                OwnedHReturnData.Register(location.SourceId, _heroine, _player);
                _anchor = new GameObject("h_free");
                _anchor.transform.SetParent(island.transform, true);
                _anchor.transform.SetPositionAndRotation(location.Position, location.Rotation);
                var pointObject = new GameObject("AiModeHLocation");
                SceneManager.MoveGameObjectToScene(pointObject, _host.gameObject.scene);
                pointObject.transform.SetPositionAndRotation(location.Position, location.Rotation);
                _point = pointObject.AddComponent<HPointData>();
                _point.category = location.Categories.Distinct().ToArray();
                AccessTools.Field(typeof(HPointData), "_targets").SetValue(_point, Array.Empty<string>());
                AccessTools.Field(typeof(HPointData), "_groups").SetValue(_point, Array.Empty<string>());
                AccessTools.Field(typeof(HPointData), "_experience").SetValue(_point, (int)_p.N("math.zero"));
                var zero = Vector3.one * _p.N("math.zero");
                _point.SetKindOffset(zero, zero);
                _point.BackUpPosition();
                _started?.Invoke();
                _open = GameScene.commonSpace.AddComponent<OpenHData>();
                var data = new OpenHData.Data
                {
                    lstFemale = new List<Heroine> { heroine }, player = _player,
                    isFreeH = true, map = island, position = location.Position, rotation = location.Rotation,
                    peepCategory = location.Categories.ToList(), mapNoFreeH = (int)_p.N("hSession.mapNo"),
                    // This scene has its own lighting and no native SunLightInfo.
                    // Preserve the native skip sentinel; owned return data is removed
                    // after HScene finishes, rather than changing startup semantics.
                    timezoneFreeH = _p.I("hSession.timezone"), statusFreeH = _p.I("hSession.timezone"),
                    stageFreeH1 = (int)_p.N("hSession.stage"), stageFreeH2 = (int)_p.N("hSession.stage"),
                    state = (int)_p.N("hSession.state"), appoint = (int)_p.N("hSession.appoint"),
                    maleCoordinateType = _player.charFile.status.coordinateType,
                    lstFemaleCoordinateType = new List<int> { heroine.charFile.status.coordinateType, (int)_p.N("hSession.coordinate") },
                    camera = new OpenData.CameraData { position = location.Position, rotation = location.Rotation }
                };
                data.clothStates.Add(heroine.charFile.status.clothesState.Select(x => (int)x).ToList());
                data.accessoryStates.Add(heroine.charFile.status.showAccessory.ToList());
                _open.data = data;
                _open.isLoad = false; _open.isAsync = true; _open.isFade = false; _open.fadeType = FadeCanvas.Fade.In;
                _deadline = _host.StartCoroutine(Deadline());
                _log?.Invoke("現在位置のH開始: " + location.SourceId);
            }
            catch (Exception ex) { Fail(ex); }
        }

        private void Patch(Type type, string method, string prefix, string postfix, Type[] args = null)
        {
            var original = args == null ? AccessTools.Method(type, method) : AccessTools.Method(type, method, args);
            if (original == null) throw new MissingMethodException(type.FullName, method);
            _harmony.Patch(original,
                prefix == null ? null : new HarmonyMethod(typeof(AiModeHSession), prefix),
                postfix == null ? null : new HarmonyMethod(typeof(AiModeHSession), postfix));
        }

        private static void AfterHStart(HSceneProc __instance, ref IEnumerator __result)
        {
            var owner = _active;
            if (owner != null && owner._proc == __instance) __result = owner.Guard(__result, true);
        }
        private IEnumerator Guard(IEnumerator routine, bool startup = false)
        {
            while (Busy)
            {
                bool next; object value;
                try { next = routine.MoveNext(); value = next ? routine.Current : null; }
                catch (Exception ex) { Fail(ex); yield break; }
                if (!next)
                {
                    if (startup)
                    {
                        try
                        {
                            if (_deadline != null && _host != null) _host.StopCoroutine(_deadline);
                            _deadline = null;
                            HStartupCompletion.Complete(
                                () => { RoomReady = true; _ready?.Invoke(); },
                                AllRoomSelections ? null : (Action)(() => _itemMenu = new AiHItemMenu(_proc.sprite, _p, () => Busy && RoomReady && !_disposed)),
                                ItemMenuFailed);
                        }
                        catch (Exception ex) { Fail(ex); }
                    }
                    yield break;
                }
                var nested = value as IEnumerator;
                yield return nested == null ? value : Guard(nested);
            }
        }
        private void ItemMenuFailed(Exception error)
        {
            // Existing opt-in startup diagnostics; OFF does not format or write anything.
            (_host as AiModeSession)?.StartupDiagnostics?.Mark("h.items.failed", () => error.ToString());
        }
        private static bool BeforeMapDependent(HSceneProc __instance) => _active == null || _active._proc != __instance;
        private static void BeforeNativeMotionMenu(HSprite __instance)
        {
            var owner = _active;
            if (owner != null && owner._proc != null && owner._proc.sprite == __instance) owner._itemMenu?.Hide();
        }
        private static bool BeforePointMoveScene(HSceneProc __instance)
        {
            if (_active == null || _active._proc != __instance) return true;
            _active._log?.Invoke("H中の地点移動は現在の島の選択地点に限定しています。別の場所はH終了後に選択してください。");
            return false;
        }
        private static bool BeforeBaseUnload(string levelName, ref UniTask __result)
        {
            // FreeH normally owns a separate base map. This mode owns no such native map.
            if (_active == null || !_active.Busy || levelName != "HProc") return true;
            __result = UniTask.CompletedTask;
            return false;
        }
        [HarmonyPriority(Priority.First)]
        private static void BeforeHDestroy(HSceneProc __instance)
        {
            var owner = _active;
            if (owner != null && owner._proc == __instance) owner.Cleanup(() => owner.RoomBeforeEnding?.Invoke());
            if (owner != null && owner._proc == __instance) owner.Cleanup(() => owner._ksPlugExit?.End(__instance));
            if (owner != null && owner._proc == __instance) { owner._itemMenu?.Dispose(); owner._itemMenu = null; }
            if (owner != null && owner._proc == __instance) { owner.RoomEnding?.Invoke(); owner.RoomReady = false; }
            if (owner != null && owner._proc == __instance && owner._map != null)
                Set(owner._map, typeof(BaseMap), "mapRoot", null);
        }
        private static void AfterHDestroy(HSceneProc __instance)
        {
            // H's outer scene still reads the participant data after HProc is unloaded.
            // Restore only when the outer H scene has finished its native exit sequence.
            if (_active != null && _active._proc == __instance) _active._proc = null;
        }
        private static void Set(object target, Type declaring, string property, object value)
        {
            var setter = AccessTools.PropertySetter(declaring, property);
            if (setter == null) throw new MissingMemberException(declaring.FullName, property);
            setter.Invoke(target, new[] { value });
        }
        private void SceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            if (!Busy || (scene.name != "H" && scene.name != "HProc")) return;
            _loadedScenes.Add(scene.name); _sceneSeen = true;
        }
        private void SceneUnloaded(UnityEngine.SceneManagement.Scene scene)
        {
            if (Busy && !_aborting && _sceneSeen && scene.name == "H") Finish();
        }
        private void Fail(Exception ex)
        {
            if (_aborting || !Busy) return;
            _aborting = true;
            Cleanup(() => RoomBeforeEnding?.Invoke());
            Cleanup(() => _ksPlugExit?.End(_proc));
            (_host as AiModeSession)?.StartupDiagnostics?.Mark("h.start.failed", () => ex.ToString());
            _error?.Invoke(ex);
            _log?.Invoke("H開始失敗: " + ex.Message);
            if (_proc != null) _proc.StopAllCoroutines();
            for (int index = _p.I("math.zero"); index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.name != "H") continue;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var outer in root.GetComponentsInChildren<HScene>(true)) outer.StopAllCoroutines();
            }
            _host.StartCoroutine(Unwind());
        }
        private IEnumerator Unwind()
        {
            foreach (var name in new[] { "HProc", "H" })
                if (GameScene.NowSceneNames.FirstOrDefault() == name)
                    yield return GameScene.UnloadAsync(false).ToCoroutine();
            Finish();
        }
        public void Cancel()
        {
            if (Busy) Fail(new OperationCanceledException("Hの開始または実行を取り消しました。"));
        }
        private void Finish()
        {
            if (!Busy || _finishing) return;
            _finishing = true;
            RoomReady = false;
            try
            {
                Cleanup(() => _ksPlugExit?.End(_proc));
                Cleanup(() => _itemMenu?.Dispose()); _itemMenu = null;
                Cleanup(() => { if (_deadline != null && _host != null) _host.StopCoroutine(_deadline); });
                _deadline = null;
                Cleanup(() => _heroine?.SetRoot(_femaleRoot));
                Cleanup(() => _player?.SetRoot(_maleRoot));
                Cleanup(() =>
                {
                    if (_temporaryWorld != null && ReferenceEquals(Manager.Game.saveData, _temporaryWorld))
                        Set(null, typeof(Manager.Game), "saveData", _previousWorld);
                });
                _temporaryWorld = null; _previousWorld = null;
                if (_globalsCaptured)
                {
                    Cleanup(() => Manager.Character.enableCorrectHandSize = _previousHandCorrection);
                    Cleanup(() => Manager.Sound.Listener = _previousSoundListener != null ? _previousSoundListener : null);
                    Cleanup(() => { if (_island != null) _island.SetActive(_islandActive); });
                    _globalsCaptured = false; _previousSoundListener = null;
                }
                if (_borrowedMap != null)
                {
                    Cleanup(() => _borrowedMap.Dispose());
                    _borrowedMap = null;
                }
                else Cleanup(() => { if (_map != null) Set(_map, typeof(BaseMap), "mapRoot", null); });
                Cleanup(() => { if (_anchor != null) Object.Destroy(_anchor); });
                Cleanup(() => { if (_point != null) Object.Destroy(_point.gameObject); });
                Cleanup(() => { if (_open != null) Object.Destroy(_open); });
                Cleanup(() => RemoveOwnedReturnData(_heroine, _player));
                _map = null; _proc = null; _open = null; _anchor = null; _point = null;
                Busy = false;
                if (ReferenceEquals(_active, this)) _active = null;
                Cleanup(() => _ended?.Invoke());
                Cleanup(() => _log?.Invoke("島の操作へ復帰しました。"));
            }
            finally { _finishing = false; }
        }
        private void Cleanup(Action restore)
        {
            try { restore(); }
            catch (Exception ex)
            {
                try { _log?.Invoke("H終了時の復元エラー: " + ex); }
                catch { }
            }
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (Busy) Fail(new OperationCanceledException("AI少女モードのH接続を終了しました。"));
            }
            finally
            {
                // Native scene unloading may outlive this host. Restore owned state synchronously.
                Finish();
                CanonGate.GateApi.SessionEnding -= BeforeGateSessionEnding;
                SceneManager.sceneLoaded -= SceneLoaded; SceneManager.sceneUnloaded -= SceneUnloaded;
                _harmony?.UnpatchSelf();
            }
        }
    }
}
