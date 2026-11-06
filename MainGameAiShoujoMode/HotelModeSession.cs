using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MainGameUiInputCapture;
using MapEnvironmentRuntime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MainGameAiShoujoMode
{
    // Owns only hotel assets, UI and input lifetime. Uses the existing walking controller.
    internal sealed partial class HotelModeSession : MonoBehaviour
    {
        internal StartupTrace StartupDiagnostics { get; set; }
        private readonly Dictionary<GameObject, bool> _titleRoots = new Dictionary<GameObject, bool>();
        private readonly Dictionary<Camera, bool> _cameras = new Dictionary<Camera, bool>();
        private readonly Dictionary<AudioListener, bool> _listeners = new Dictionary<AudioListener, bool>();
        private readonly Dictionary<AudioSource, AudioClip> _audio = new Dictionary<AudioSource, AudioClip>();
        private readonly List<HotelFloorRuntime> _floors = new List<HotelFloorRuntime>();
        private HotelFloorRuntime _floor;
        private bool _changingFloor;
        private TitleScene _title;
        private Scene _previous, _titleScene, _ownedScene;
        private Parameters _p;
        private HotelWalkingContext _context;
        private SandboxFreeCamera _walker;
        private NativeActionWalkingSession _nativeWalking;
        private NativeHotelInteractionScope _nativeInteraction;
        private AiModeLoadingScreen _fade;
        private AiModeHelpScreen _help;
        private HotelRoomCardPanel _roomCards;
        private HotelRoomControlSettings _roomControls;
        private bool _walkingMenu;
        private bool _elevatorMenu;
        private HotelFloorDefinition[] _elevatorDestinations;
        private bool _receptionMenu;
        private HotelReceptionist _receptionist;
        private int _walkingDrag;
        private Vector2 _walkingScroll;
        private Rect WalkingMenuRect => new Rect(Mathf.Min(_roomControls.x, Mathf.Max(default(float), Screen.width - _roomControls.width)),
            Mathf.Min(_roomControls.y, Mathf.Max(default(float), Screen.height - _roomControls.height)),
            Mathf.Min(_roomControls.width, Screen.width), Mathf.Min(_roomControls.height, Screen.height));
        private AiModeCursorScope _cursorScope;
        private GameCursor _nativeCursor;
        private bool _nativeCursorEnabled, _nativeLock, _cursorVisible, _loading = true, _closing, _returnRequested, _restored, _collisionIgnored;
        private CursorLockMode _cursorLock;
        private Action<string> _log, _failed;
        private Action<Exception> _error;
        private Action _closed;
        private Func<bool> _diagnostics;
        private string _failure;
        private Material _skybox;
        private Light _sun;
        private AmbientMode _ambientMode;
        private Color _ambientSky, _ambientEquator, _ambientGround, _ambientLight, _fogColor;
        private bool _fog;
        private float _fogDensity, _fogStart, _fogEnd, _reflectionIntensity, _ambientIntensity;
        private FogMode _fogMode;
        private bool _collisionOwned;

        internal void Initialize(TitleScene title, string content, Parameters parameters, AudioSource confirmation,
            Func<bool> diagnostics, Action<string> log, Action<Exception> error, Action closed, Action<string> failed)
        {
            StartupDiagnostics?.Mark("session.initialize.begin");
            _title = title; _titleScene = title.gameObject.scene; _previous = SceneManager.GetActiveScene();
            _p = parameters; _diagnostics = diagnostics; _log = log; _error = error; _closed = closed; _failed = failed;
            _cursorLock = Cursor.lockState; _cursorVisible = Cursor.visible;
            SaveEnvironment();
            _fade = new AiModeLoadingScreen(log, diagnostics);
            _fade.StartupDiagnostics = StartupDiagnostics;
            StartupDiagnostics?.Mark("session.initialize.end");
            _fade.Cover(() => { if (_closing) return; StartCoroutine(GuardedEnter(content, confirmation)); });
        }
        private IEnumerator GuardedEnter(string content, AudioSource confirmation)
        {
            var steps = new Stack<IEnumerator>(); steps.Push(Enter(content, confirmation));
            while (steps.Any())
            {
                bool next;
                object current = null;
                try { next = steps.Peek().MoveNext(); if (next) current = steps.Peek().Current; }
                catch (Exception ex)
                {
                    StartupDiagnostics?.Mark("entry.coroutine.failed", () => ex.ToString());
                    _failure = ex.Message;
                    if (_diagnostics()) _error(ex);
                    _loading = false; BeginReturn(); yield break;
                }
                if (!next) { steps.Pop(); continue; }
                if (current is IEnumerator nested) steps.Push(nested);
                else yield return current;
            }
        }
        private IEnumerator Enter(string content, AudioSource confirmation)
        {
            StartupDiagnostics?.Mark("confirmation.wait.begin");
            if (_returnRequested) { _loading = false; BeginReturn(); yield break; }
            if (confirmation != null && confirmation.isPlaying && confirmation.clip != null)
                yield return new WaitForSecondsRealtime(Mathf.Max(_p.N("math.zero"), (confirmation.clip.length - confirmation.time) / Mathf.Max(_p.N("render.epsilon"), Mathf.Abs(confirmation.pitch))));
            // VR pauses rendering under the loading fade, so do not wait for a rendered frame.
            yield return null;
            StartupDiagnostics?.Mark("confirmation.wait.end");
            if (_returnRequested) { _loading = false; BeginReturn(); yield break; }
            _ownedScene = SceneManager.CreateScene("CanonHotelModeSession");
            SceneManager.MoveGameObjectToScene(gameObject, _ownedScene);
            foreach (var root in _titleScene.GetRootGameObjects()) _titleRoots.Add(root, root.activeSelf);
            foreach (var camera in FindObjectsOfType<Camera>()) { _cameras.Add(camera, camera.enabled); if (!_fade.Protect(camera)) camera.enabled = false; }
            foreach (var listener in FindObjectsOfType<AudioListener>()) { _listeners.Add(listener, listener.enabled); if (!_fade.Protect(listener)) listener.enabled = false; }
            foreach (var source in FindObjectsOfType<AudioSource>()) if (source.isPlaying && source != confirmation) { _audio.Add(source, source.clip); source.Pause(); }
            _cursorScope = new AiModeCursorScope(_log, _diagnostics);
            _nativeCursor = FindObjectOfType<GameCursor>();
            if (_nativeCursor != null) { _nativeCursorEnabled = _nativeCursor.enabled; _nativeLock = GameCursor.isLock; _nativeCursor.UnLockCursor(); _nativeCursor.enabled = false; }
            foreach (var root in _titleRoots.Keys) if (!_fade.Protect(root)) root.SetActive(false);
            StartupDiagnostics?.Snapshot("title.suspended");
            if (_fade.HasVr)
            {
                _fade.CaptureNativeView();
                StartupDiagnostics?.Mark("native.walking.begin");
                _nativeWalking = new NativeActionWalkingSession(_p);
                float deadline = Time.realtimeSinceStartup + _p.N("hSession.timeoutSeconds");
                while (!_nativeWalking.PlayerReady && _nativeWalking.Error == null && !_returnRequested && Time.realtimeSinceStartup < deadline) yield return null;
                if (_returnRequested) { _loading = false; BeginReturn(); yield break; }
                if (_nativeWalking.Error != null) throw _nativeWalking.Error;
                if (!_nativeWalking.PlayerReady) throw new TimeoutException("ホテルの本編歩行プレイヤーを初期化できませんでした。");
                StartupDiagnostics?.Mark("native.player.ready");
            }
            var manifest = JsonCodec.FromJson<HotelMapManifest>(File.ReadAllText(Path.Combine(content, "floors.json")));
            manifest.Validate();
            foreach (var definition in manifest.floors)
            {
                var floor = new HotelFloorRuntime(definition); _floors.Add(floor);
                // Guest rooms are loaded only after that room is explicitly selected.
                if (definition.room != null || definition.lazyLoad) continue;
                StartupDiagnostics?.Mark("floor.load.begin", () => definition.id + " bundle=" + definition.bundle);
                yield return floor.Load(content);
                StartupDiagnostics?.Mark("floor.load.end", () => definition.id);
                if (_returnRequested) { _loading = false; BeginReturn(); yield break; }
            }
            _floor = _floors.Single(f => f.Definition.id == manifest.initialFloor); _floor.SetActive(true);
            _context = new HotelWalkingContext(_p);
            _p.text.Single(item => item.key == "input.cursorRelease").value = nameof(KeyCode.None);
            _roomControls = JsonCodec.FromJson<HotelRoomControlSettings>(File.ReadAllText(Path.Combine(content, "room-controls.json")));
            if (_roomControls == null) throw new InvalidDataException("客室座標UIの設定がありません。");
            _roomControls.Validate();
            _receptionContent = Path.GetDirectoryName(content);
            _roomCards = new HotelRoomCardPanel(_p, new HotelRoomCards(Path.Combine(BepInEx.Paths.GameRootPath,
                "UserData", "HotelMode", "room-characters.json")), RegistrationChanged, StartRoomSession);
            _receptionist = new HotelReceptionist(_floors.Single(f => f.Definition.id == "lobby"), _p, _roomControls);
            _roomCards.Saved = RoomCardSaved;
            InitializeDoorCards();
            try { _receptionist.Load(_roomCards.Registered("reception")); }
            catch (Exception ex) { _roomCards.ShowError(ex); }
            StartupDiagnostics?.Mark("walker.initialize.begin");
            if (_nativeWalking != null)
                _walker = _nativeWalking.CreateWalker(_context, _fade.NativeRenderCamera, _fade.NativeOrigin);
            else
            {
                var legacyPlayer = new GameObject("Hotel Walking Player"); legacyPlayer.transform.SetParent(transform, false);
                _walker = legacyPlayer.AddComponent<SandboxFreeCamera>(); _walker.Initialize(_context);
            }
            var player = _walker.gameObject;
            _receptionist.SetLookTarget(_walker.ViewCamera.transform);
            _walker.WarpFeet(manifest.initialEntry.position, Quaternion.Euler(_p.N("math.zero"), manifest.initialEntry.yaw, _p.N("math.zero")));
            Physics.SyncTransforms();
            if (_nativeWalking != null)
            {
                StartupDiagnostics?.Mark("native.floor.begin");
                yield return _nativeWalking.AttachMap(_floor.WalkingRoot, _p);
                StartupDiagnostics?.Mark("native.floor.ready");
                _nativeInteraction = new NativeHotelInteractionScope(
                    () => !_closing && !_returnRequested && _nativeWalking?.Ready == true && _hSession?.Busy != true,
                    NativeInteract);
            }
            StartupDiagnostics?.Mark("walker.initialize.end");
            StartupDiagnostics?.Mark("view.bind.begin");
            if (_nativeWalking == null && !_fade.BindView(_walker.ViewCamera)) _walker.ViewCamera.gameObject.AddComponent<AudioListener>();
            StartupDiagnostics?.Snapshot("view.bind.end");
            StartupDiagnostics?.RendererInventory("hotel.before-reveal", null);
            _fade.ObserveNextRender("hotel.first-render");
            InitializeAuthoring(_receptionContent);
            _collisionIgnored = Physics.GetIgnoreLayerCollision(player.layer, LayerMask.NameToLayer("Map"));
            Physics.IgnoreLayerCollision(player.layer, LayerMask.NameToLayer("Map"), false);
            _collisionOwned = true;
            _help = new AiModeHelpScreen(_title, transform, _p, HelpChanged, null, "ホテルモード",
                "W / A / S / D　歩く\nマウス　視点を動かす\nShift　早歩き　　Ctrl　しゃがむ　　Space　ジャンプ\nF　エレベーターで階移動／各客室でフリーH\nTab　ホテルメニューを開閉\nF1　操作説明\nEsc　説明を閉じる／タイトルへ戻る\n\nTABメニューの「全客室・受付嬢のカード登録」で、入室前に女の子を登録してください。\n各客室の扉から直接フリーHを開始します。未登録の場合は入室できません。\nH終了後は廊下へ戻ります。\nVR：右トリガーで歩行、右パッド上を押して調べます。");
            Physics.SyncTransforms();
            _fade.Reveal(() =>
            {
                _loading = false;
                StartupDiagnostics?.Snapshot("mode.ready");
                if (_returnRequested) BeginReturn();
                else { _context.WalkingReady = true; _walker.SetCursorReleased(false); if (_diagnostics()) _log("HOTEL_MODE_READY: HS2 lobby; grounded walking; elevator ready"); }
            });
        }
        private void OnGUI()
        {
            if (_authoring?.Occupied == true) return;
            var input = Event.current;
            if (_receptionMenu && !_closing)
            {
                if (input.type == EventType.KeyDown && (input.keyCode == KeyCode.Escape || input.keyCode == KeyCode.Tab || input.keyCode == KeyCode.F))
                { CloseReception(); input.Use(); }
                return;
            }
            if (!_closing && !_loading && !_changingFloor && input.type == EventType.KeyDown && input.keyCode == KeyCode.Tab)
            {
                if (_roomCards?.Visible == true) _roomCards.SetVisible(false);
                else if (_hSession?.RoomReady == true) { _placementPanel?.Toggle(); if (_placementPanel?.Visible != true) _roomObjects?.StopEditing(); }
                else if (_hSession?.Busy != true) { _walkingMenu = !_walkingMenu; _receptionMenu = false; WalkingMenuChanged(); }
                input.Use();
            }
            if (_hSession?.Busy == true)
            {
                if (!_closing && _hSession.RoomReady)
                {
                    if (_roomCards?.Visible == true) _roomCards.Draw();
                    else _placementPanel?.Draw();
                    if (_placementPanel?.Visible == true && _roomCards?.Visible != true) _roomObjects?.HandlePointer(_hSession.ActiveCamera);
                }
                return;
            }
            if (!_closing && !_loading && !_changingFloor && _walkingMenu && _roomCards?.Visible != true) DrawWalkingMenu();
            if (!_closing && !_loading && !_changingFloor && _walkingMenu && _roomCards?.Visible != true) _roomObjects?.HandlePointer(_walker.ViewCamera);
            if (!_closing && !_loading && !_changingFloor && !_walkingMenu && _roomCards?.Visible != true && !string.IsNullOrWhiteSpace(_roomEntryFailure))
                GUI.Box(new Rect(_roomControls.x, _roomControls.y, Mathf.Min(_roomControls.width, Screen.width), _roomControls.buttonHeight * _p.N("math.two")), _roomEntryFailure + "\nTABメニューで登録と再開始を確認してください。");
            if (!_closing && !_loading && !_changingFloor) _roomCards?.Draw();
            var e = Event.current;
            if (_closing || _p == null || e.type != EventType.KeyDown) return;
            if (e.keyCode == KeyCode.Escape)
            {
                if (_roomCards?.Visible == true) _roomCards.SetVisible(false);
                else if (_walkingMenu) { _walkingMenu = false; _receptionMenu = false; WalkingMenuChanged(); }
                else if (_help != null && _help.Visible) _help.SetVisible(false);
                else Cancel();
                e.Use(); return;
            }
            if (_loading || _changingFloor || _context == null || !_context.WalkingReady) return;
            if (_roomCards?.Visible == true) return;
            if (e.keyCode == KeyCode.F1) { _help.SetVisible(!_help.Visible); e.Use(); }
            else if ((e.keyCode == KeyCode.F || (_nativeWalking != null && e.keyCode == KeyCode.F3)) && !_help.Visible && Application.isFocused) { Interact(); e.Use(); }
        }
        private void NativeInteract()
        {
            if (_closing || _returnRequested || _loading || _changingFloor || _context == null || !_context.WalkingReady
                || _context.Panel || _walkingMenu || _roomCards?.Visible == true || _help?.Visible == true || _authoring?.Occupied == true) return;
            InteractCore(true);
        }
        private void Interact() => InteractCore(false);
        private void InteractCore(bool nativeController)
        {
            var camera = _walker.ViewCamera;
            if (_changingFloor || _returnRequested || _context == null) return;
            if (nativeController)
            { if (!_context.WalkingReady || _context.Panel) return; }
            else if (_context.ExternalInputBlocked || Cursor.lockState != CursorLockMode.Locked) return;
            if (_floor.Definition.id == "lobby" && _receptionist?.CanTalk(camera) == true)
            { OpenReception(); return; }
            var portal = _floor.PortalColliders.FirstOrDefault(pair => HotelElevatorInteraction.CanUse(camera.transform.position,
                camera.transform.forward,pair.Key.interaction,_p.N("hotel.interactDistance"),pair.Value)).Key;
            if (portal == null && !HotelElevatorInteraction.CanUse(camera.transform.position, camera.transform.forward, _floor.Definition.elevator,
                _p.N("hotel.interactDistance"), _floor.ElevatorColliders)) return;
            if (HotelElevatorRoutes.OpensSelector(_floor.Definition, portal, _floors.Select(f => f.Definition)))
            {
                _elevatorDestinations = HotelElevatorRoutes.Destinations(_floors.Select(f => f.Definition));
                _elevatorMenu = true; _walkingMenu = true; _receptionMenu = false;
                WalkingMenuChanged(); return;
            }
            var destination = _floors.Single(f => f.Definition.id == (portal == null ? _floor.Definition.destination : portal.destination));
            TravelToFloor(destination, portal == null ? destination.Definition.arrival : portal.arrival,
                portal == null ? destination.Definition.yaw : portal.yaw);
        }
        private void TravelToFloor(HotelFloorRuntime destination, Vector3 arrival, float yaw, Action entered = null)
        {
            if (_closing || _returnRequested || _loading || _changingFloor || _hSession?.Busy == true) return;
            if (destination.Definition.room != null) { OpenDirectRoom(destination); return; }
            _changingFloor = true; _context.WalkingReady = false;
            try
            {
                _fade.Cover(() =>
                {
                    if (_closing) return;
                    if (_returnRequested) { _changingFloor = false; BeginReturn(); return; }
                    StartCoroutine(ChangeFloor(destination, arrival, yaw, entered));
                });
            }
            catch (Exception ex) { FloorFailed(ex); }
        }
        private IEnumerator ChangeFloor(HotelFloorRuntime destination, Vector3 arrival, float yaw, Action entered)
        {
            var steps = new Stack<IEnumerator>(); steps.Push(ChangeFloorCore(destination, arrival, yaw, entered));
            try
            {
                while (steps.Count > default(int))
                {
                    bool next; object current = null;
                    try { next = steps.Peek().MoveNext(); if (next) current = steps.Peek().Current; }
                    catch (Exception ex) { FloorFailed(ex); yield break; }
                    if (!next) { (steps.Pop() as IDisposable)?.Dispose(); continue; }
                    if (current is IEnumerator nested) steps.Push(nested); else yield return current;
                }
            }
            finally { while (steps.Count > default(int)) (steps.Pop() as IDisposable)?.Dispose(); }
        }
        private IEnumerator ChangeFloorCore(HotelFloorRuntime destination, Vector3 arrival, float yaw, Action entered)
        {
            yield return null;
            yield return EnsureFloorLoaded(destination);
            _nativeWalking?.Suspend();
            _roomLighting?.Dispose(); _roomLighting = null;
            _roomObjects?.Dispose(); _roomObjects = null;
            _floor.SetActive(false); _floor = destination; _floor.SetActive(true);
            var rotation = Quaternion.Euler(_p.N("math.zero"), yaw, _p.N("math.zero"));
            Physics.SyncTransforms();
            if (_nativeWalking != null) yield return _nativeWalking.ReplaceMap(_floor.WalkingRoot, _p, arrival, rotation);
            else _walker.WarpFeet(arrival, rotation);
            if (_closing) yield break;
            if (_returnRequested) { _changingFloor = false; BeginReturn(); yield break; }
            _fade.Reveal(() =>
            {
                _changingFloor = false;
                if (_returnRequested) BeginReturn();
                else if (!_closing)
                {
                    _context.WalkingReady = true; _walker.SetCursorReleased(false);
                    entered?.Invoke();
                }
            });
        }
        private void WarpToFloor()
        {
            _walker.WarpFeet(_floor.Definition.arrival, Quaternion.Euler(_p.N("math.zero"), _floor.Definition.yaw, _p.N("math.zero")));
            Physics.SyncTransforms();
        }
        private void FloorFailed(Exception ex)
        {
            _failure = ex.Message; _changingFloor = false;
            if (_diagnostics()) _error(ex);
            BeginReturn();
        }
        private void HelpChanged()
        {
            _context.SetPanel(_help.Visible);
            if (_help.Visible) { UiInputCaptureApi.SetInputBlockRects(Plugin.Guid, new[] { _help.InputRect }); UiInputCaptureApi.SetIdleCursorUnlock(Plugin.Guid, true); }
            else ReleaseInput();
        }
        private void RegistrationChanged()
        {
            if (_hSession?.Busy == true)
            {
                if (_roomCards?.Visible == true) { _placementPanel?.ReleaseInput(); UiInputCaptureApi.SetInputBlockRects(Plugin.Guid, new[] { _roomCards.InputRect }); UiInputCaptureApi.SetIdleCursorUnlock(Plugin.Guid, true); }
                else ReleaseInput();
                return;
            }
            bool shown = _roomCards?.Visible == true;
            if (shown) { UiInputCaptureApi.Sync(Plugin.Guid, "hotel-walking-slider", false); _walkingDrag = _p.I("math.zero"); }
            _context.SetPanel(shown || _help?.Visible == true);
            if (shown)
            {
                UiInputCaptureApi.SetInputBlockRects(Plugin.Guid, new[] { _roomCards.InputRect });
                UiInputCaptureApi.SetIdleCursorUnlock(Plugin.Guid, true);
                _walker?.SetCursorReleased(true);
            }
            else if (_help?.Visible == true) HelpChanged();
            else if (_walkingMenu) WalkingMenuChanged();
            else { ReleaseInput(); _walker?.SetCursorReleased(false); }
        }
        private void WalkingMenuChanged()
        {
            if (!_walkingMenu) { _elevatorMenu = false; _elevatorDestinations = null; }
            if (!_walkingMenu) RestoreDoorCardSettings();
            if (!_walkingMenu) _roomObjects?.StopEditing();
            if (!_walkingMenu) _receptionist?.RestorePlacement();
            _context.SetPanel(_walkingMenu || _roomCards?.Visible == true);
            if (_walkingMenu)
            {
                UiInputCaptureApi.SetInputBlockRects(Plugin.Guid, new[] { WalkingMenuRect });
                UiInputCaptureApi.SetIdleCursorUnlock(Plugin.Guid, true); _walker?.SetCursorReleased(true);
            }
            else { ReleaseInput(); _walker?.SetCursorReleased(false); }
        }
        private void DrawWalkingMenu()
        {
            int previousHot = GUIUtility.hotControl;
            var rect = WalkingMenuRect;
            UiInputCaptureApi.SetInputBlockRects(Plugin.Guid, new[] { rect });
            GUILayout.BeginArea(rect, GUI.skin.box);
            if (_elevatorMenu)
            {
                DrawElevatorMenu();
                GUILayout.EndArea(); return;
            }
            GUILayout.Label(_receptionMenu ? "受付メニュー" : "ホテルメニュー　TABで開閉");
            if (_receptionMenu)
            {
                GUILayout.Label("受付メニューの内容は未設定です。");
                if (GUILayout.Button("閉じる", GUILayout.ExpandWidth(false))) { _receptionMenu = false; _walkingMenu = false; WalkingMenuChanged(); }
                GUILayout.EndArea(); return;
            }
            _walkingScroll = GUILayout.BeginScrollView(_walkingScroll);
            if (GUILayout.Button("全客室・受付嬢のカード登録", GUILayout.ExpandWidth(false))) _roomCards.SetVisible(true);
            DrawDoorCardSettings();
            _receptionist.DrawPoseMenu();
            _floor.Stage?.Draw();
            if (!string.IsNullOrWhiteSpace(_roomCards.ErrorStatus)) GUILayout.Label(_roomCards.ErrorStatus);
            if (GUILayout.Button("201号室でフリーHを開始", GUILayout.ExpandWidth(false))) StartRoomSession("201");
            if (_floor.Definition.room?.id == "reception-suite" && GUILayout.Button("受付裏スイートでフリーHを開始", GUILayout.ExpandWidth(false))) StartRoomSession("reception-suite");
            if (_floor.Definition.room != null)
            {
                _roomLighting?.Draw();
                _roomObjects?.Draw();
            }
            if (GUILayout.Button("操作説明", GUILayout.ExpandWidth(false))) { _walkingMenu = false; WalkingMenuChanged(); _help.SetVisible(true); }
            if (GUILayout.Button("閉じる", GUILayout.ExpandWidth(false))) { _walkingMenu = false; WalkingMenuChanged(); }
            GUILayout.EndScrollView(); GUILayout.EndArea();
            if (_walkingMenu && _roomCards?.Visible != true && _help?.Visible != true && Application.isFocused)
            {
                if (Event.current.rawType == EventType.MouseDown && GUIUtility.hotControl != previousHot) _walkingDrag = GUIUtility.hotControl;
                if (_walkingDrag != _p.I("math.zero") && !Input.GetMouseButton(_p.I("math.zero"))) _walkingDrag = _p.I("math.zero");
                bool typing = (GUI.GetNameOfFocusedControl() ?? "").StartsWith("MainGameAiShoujoMode.Hotel", StringComparison.Ordinal);
                UiInputCaptureApi.Sync(Plugin.Guid, "hotel-walking-slider", _walkingDrag != _p.I("math.zero") || typing);
            }
        }
        private void ReleaseInput()
        {
            if ((GUI.GetNameOfFocusedControl() ?? "").StartsWith("MainGameAiShoujoMode.Hotel", StringComparison.Ordinal)) GUIUtility.keyboardControl = default(int);
            if (_walkingDrag != default(int) && GUIUtility.hotControl == _walkingDrag) GUIUtility.hotControl = default(int);
            _walkingDrag = default(int); UiInputCaptureApi.Sync(Plugin.Guid, "hotel-walking-slider", false);
            UiInputCaptureApi.ClearInputBlockRects(Plugin.Guid); UiInputCaptureApi.SetIdleCursorUnlock(Plugin.Guid, false);
        }
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) _roomObjects?.StopEditing();
            if (_hSession?.Busy == true) { if (!focused) _placementPanel?.ReleaseInput(); return; }
            if (!focused) { ReleaseInput(); _walker?.SetCursorReleased(true); }
            else if (_receptionMenu) CaptureReceptionInput();
            else if (_roomCards?.Visible == true) RegistrationChanged();
            else if (_walkingMenu) WalkingMenuChanged();
            else if (_help != null && _help.Visible) HelpChanged();
            else if (!_loading && !_closing) _walker?.SetCursorReleased(false);
        }
        internal void Cancel()
        {
            _returnRequested = true;
            if (_context != null) _context.WalkingReady = false;
            if (_hSession?.Busy == true) { _hSession.Cancel(); return; }
            if (_loading) foreach (var floor in _floors) floor.Dispose();
            if (!_loading && !_changingFloor) BeginReturn();
        }
        internal void TitleSceneUnloaded(Scene scene) { if (scene == _titleScene) Cancel(); }
        private void OnApplicationQuit() { ReleaseRoomLoadingAudio(); SaveRoomSnapshot(); }
        private void BeginReturn()
        {
            StartupDiagnostics?.Mark("return.requested");
            _authoring?.Shutdown();
            if (_closing) return;
            if (_hSession?.Busy == true) { _returnRequested = true; _hSession.Cancel(); return; }
            _closing = true;
            _nativeInteraction?.Dispose(); _nativeInteraction = null;
            _nativeWalking?.Suspend();
            _hSession?.Dispose(); _hSession = null;
            if (_context != null) _context.WalkingReady = false;
            ReleaseInput(); _walker?.SetCursorReleased(true);
            try { _fade.Cover(() => StartCoroutine(Return())); }
            catch (Exception ex) { if (_diagnostics()) _error(ex); StartCoroutine(Return()); }
        }
        private IEnumerator Return()
        {
            if (_nativeWalking != null)
            {
                _nativeWalking.Dispose();
                while (!_nativeWalking.Released) yield return null;
                _nativeWalking = null; _walker = null;
            }
            _roomObjects?.Dispose(); _roomObjects = null;
            DisposeReception(); _receptionist?.Dispose(); _receptionist = null;
            DisposeDoorCards();
            foreach (var floor in _floors) floor.Dispose(); _floors.Clear();
            _help?.Dispose(); _help = null;
            _roomCards?.Dispose(); _roomCards = null;
            if (_walker != null) { DestroyImmediate(_walker.gameObject); _walker = null; }
            // Restore the title even while VR rendering is paused by the loading fade.
            yield return null;
            Restore();
            if (_fade != null) _fade.Reveal(FinishReturn); else FinishReturn();
        }
        private void FinishReturn()
        {
            StartupDiagnostics?.Mark("return.completed"); StartupDiagnostics?.Dispose();
            string failure = _failure;
            var failed = _failed;
            var scene = _ownedScene;
            if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene); else Destroy(gameObject);
            if (!string.IsNullOrEmpty(failure)) failed(failure);
        }
        private void SaveEnvironment()
        {
            _skybox = RenderSettings.skybox; _sun = RenderSettings.sun; _ambientMode = RenderSettings.ambientMode;
            _ambientSky = RenderSettings.ambientSkyColor; _ambientEquator = RenderSettings.ambientEquatorColor; _ambientGround = RenderSettings.ambientGroundColor; _ambientLight = RenderSettings.ambientLight;
            _fog = RenderSettings.fog; _fogColor = RenderSettings.fogColor; _fogMode = RenderSettings.fogMode; _fogDensity = RenderSettings.fogDensity; _fogStart = RenderSettings.fogStartDistance; _fogEnd = RenderSettings.fogEndDistance;
            _reflectionIntensity = RenderSettings.reflectionIntensity; _ambientIntensity = RenderSettings.ambientIntensity;
        }
        private void Restore()
        {
            if (_restored) return; _restored = true;
            _fade?.RestoreView();
            if (_previous.IsValid() && _previous.isLoaded) SceneManager.SetActiveScene(_previous);
            foreach (var root in _titleRoots) if (root.Key != null) root.Key.SetActive(root.Value);
            foreach (var camera in _cameras) if (camera.Key != null) camera.Key.enabled = camera.Value;
            foreach (var listener in _listeners) if (listener.Key != null) listener.Key.enabled = listener.Value;
            foreach (var audio in _audio) if (audio.Key != null && audio.Key.clip == audio.Value) audio.Key.UnPause();
            _cursorScope?.Dispose(); _cursorScope = null;
            if (_nativeCursor != null) { _nativeCursor.enabled = _nativeCursorEnabled; _nativeCursor.SetCursorLock(_nativeLock); }
            if (_collisionOwned) { Physics.IgnoreLayerCollision(default(int), LayerMask.NameToLayer("Map"), _collisionIgnored); _collisionOwned = false; }
            RenderSettings.skybox = _skybox; RenderSettings.sun = _sun; RenderSettings.ambientMode = _ambientMode; RenderSettings.ambientSkyColor = _ambientSky; RenderSettings.ambientEquatorColor = _ambientEquator; RenderSettings.ambientGroundColor = _ambientGround; RenderSettings.ambientLight = _ambientLight;
            RenderSettings.fog = _fog; RenderSettings.fogColor = _fogColor; RenderSettings.fogMode = _fogMode; RenderSettings.fogDensity = _fogDensity; RenderSettings.fogStartDistance = _fogStart; RenderSettings.fogEndDistance = _fogEnd;
            RenderSettings.reflectionIntensity = _reflectionIntensity; RenderSettings.ambientIntensity = _ambientIntensity;
            Cursor.lockState = _cursorLock; Cursor.visible = _cursorVisible;
        }
        private void OnDestroy()
        {
            _nativeInteraction?.Dispose(); _nativeInteraction = null;
            StartupDiagnostics?.Mark("session.destroy"); StartupDiagnostics?.Dispose();
            _authoring?.Shutdown();
            _roomObjects?.Dispose(); _roomObjects = null;
            ReleaseRoomLoadingAudio();
            SaveRoomSnapshot();
            DisposeReception(); _receptionist?.Dispose(); _receptionist = null;
            _placementPanel?.Dispose(); _placementPanel = null;
            _hSession?.Dispose(); _hSession = null;
            _hotelScope?.Dispose(); _hotelScope = null;
            _nativeWalking?.Dispose(); _nativeWalking = null;
            _roomLighting?.Dispose(); _roomLighting = null;
            if (_context != null) _context.WalkingReady = false;
            DisposeDoorCards();
            foreach (var floor in _floors) floor.Dispose(); _floors.Clear();
            _roomCards?.Dispose(); _help?.Dispose(); ReleaseInput(); Restore(); _fade?.Dispose();
            _closed?.Invoke();
        }
    }
}
