using System;
using System.Collections;
using System.Collections.Generic;
using MainGameUiInputCapture;
using MainGameAiShoujoMode.Life;
using MapEnvironmentRuntime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MainGameAiShoujoMode
{
    public sealed partial class AiModeSession : MonoBehaviour
    {
        internal StartupTrace StartupDiagnostics { get; set; }
        private readonly Dictionary<GameObject, bool> _titleRoots = new Dictionary<GameObject, bool>();
        private readonly Dictionary<Camera, bool> _cameras = new Dictionary<Camera, bool>();
        private readonly Dictionary<AudioSource, AudioClip> _audio = new Dictionary<AudioSource, AudioClip>();
        private readonly Dictionary<AudioListener, bool> _listeners = new Dictionary<AudioListener, bool>();
        private AiModeCursorScope _cursorScope;
        private TitleScene _title;
        private AiAnimationTrial _animationTrial;
        private AiModePlayerAvatar _playerAvatar;
        private AiSpecialMovement _specialMovement;
        private AiModeNavigation _navigation;
        private AiPodSession _pods;
        private AiGirlConversation _conversation;
        private AiLifeRuntime _life;
        private AiModePickupSession _pickups;
        private AiLifeActorBridge _lifeActor;
        private Furniture.AiFurnitureSession _furniture;
        private AiModeHSession _hSession;
        private CanonGate.GateApi.AiShoujoModeScope _gateScope;
        private readonly Dictionary<Behaviour, bool> _hSuspended = new Dictionary<Behaviour, bool>();
        private Action _hRestoreGirl, _hResume;
        private bool _hSuspending;
        private bool _conversationCursorReleased;
        private bool _inputOwned;
        private GameCursor _nativeCursor;
        private bool _nativeCursorEnabled, _nativeCursorLocked;
        private AiModeHelpScreen _help;
        private AiModePlaybackBar _playback;
        private AiModeAudio _islandAudio;
        private AiModeLoadingScreen _loadingScreen;
        private AudioSource _confirmation;
        private Scene _previousScene, _titleScene, _modeScene;
        private Parameters _settings;
        private SandboxHost _host;
        private NativeActionWalkingSession _nativeWalking;
        private Action<string> _log;
        private Action<Exception> _error;
        private Action _closed;
        private bool _loading = true, _closing, _returnRequested, _drag;
        private CursorLockMode _cursorLock;
        private bool _cursorVisible;
        public void Initialize(TitleScene title, string content, Parameters settings, AudioSource confirmation, Action<string> log, Action<Exception> error, Action closed)
        {
            StartupDiagnostics?.Mark("session.initialize.begin");
            _title = title; _settings = settings; _log = log; _error = error; _closed = closed;
            _previousScene = SceneManager.GetActiveScene(); _titleScene = title.gameObject.scene;
            _cursorLock = Cursor.lockState; _cursorVisible = Cursor.visible;
            _confirmation = confirmation;
            _loadingScreen = new AiModeLoadingScreen(log);
            _loadingScreen.StartupDiagnostics = StartupDiagnostics;
            StartupDiagnostics?.Mark("session.initialize.end");
            _loadingScreen.Cover(() => StartCoroutine(EnterSafely(content)));
        }
        private IEnumerator EnterSafely(string content)
        {
            var steps = Enter(content);
            while (true)
            {
                bool next;
                try { next = steps.MoveNext(); }
                catch (Exception ex) { StartupDiagnostics?.Mark("entry.coroutine.failed", () => ex.ToString()); _loading = false; _error(ex); BeginReturn(); yield break; }
                if (!next) yield break;
                yield return steps.Current;
            }
        }
        private IEnumerator Enter(string content)
        {
            StartupDiagnostics?.Mark("confirmation.wait.begin");
            if (_confirmation != null && _confirmation.isPlaying && _confirmation.clip != null)
                yield return new WaitForSecondsRealtime(Mathf.Max(_settings.N("math.zero"), (_confirmation.clip.length - _confirmation.time) / Mathf.Max(_settings.N("render.epsilon"), Mathf.Abs(_confirmation.pitch))));
            // Give EyeCatch one frame to prepare; VR pauses rendering while the fade covers the scene.
            yield return null;
            StartupDiagnostics?.Mark("confirmation.wait.end");
            // Restore saved cards only after the loading screen has had a frame to render.
            InitializeMomentSave(content);
            _help = new AiModeHelpScreen(_title, transform, _settings, HelpChanged, () => { if (_host?.Session != null && !_host.Session.Panel) _host.Session.TogglePanel(); });
            if (SceneManager.GetSceneByName(_settings.T("aiMode.scene")).IsValid()) throw new InvalidOperationException("AI少女モードのシーンが既に存在します。");
            _modeScene = SceneManager.CreateScene(_settings.T("aiMode.scene"));
            SceneManager.MoveGameObjectToScene(gameObject, _modeScene); SceneManager.SetActiveScene(_modeScene);
            foreach (var root in _titleScene.GetRootGameObjects()) _titleRoots.Add(root, root.activeSelf);
            foreach (var camera in FindObjectsOfType<Camera>()) { _cameras.Add(camera, camera.enabled); if (!_loadingScreen.Protect(camera)) camera.enabled = false; }
            foreach (var audio in FindObjectsOfType<AudioSource>()) if (audio.isPlaying && audio != _confirmation) { _audio.Add(audio, audio.clip); audio.Pause(); }
            foreach (var listener in FindObjectsOfType<AudioListener>()) { _listeners.Add(listener, listener.enabled); if (!_loadingScreen.Protect(listener)) listener.enabled = false; }
            _cursorScope = new AiModeCursorScope(_log);
            _nativeCursor = FindObjectOfType<GameCursor>();
            if (_nativeCursor != null)
            {
                _nativeCursorEnabled = _nativeCursor.enabled; _nativeCursorLocked = GameCursor.isLock;
                _nativeCursor.UnLockCursor(); _nativeCursor.enabled = false;
            }
            foreach (var root in _titleRoots.Keys) if (!_loadingScreen.Protect(root)) root.SetActive(false);
            StartupDiagnostics?.Snapshot("title.suspended");
            if (_loadingScreen.HasVr)
            {
                _loadingScreen.CaptureNativeView();
                StartupDiagnostics?.Mark("native.walking.begin");
                _nativeWalking = new NativeActionWalkingSession(_settings, _resumeMoment?.playerCard == null ? null : HotelRoomAppearanceRuntime.Decode(_resumeMoment.playerCard));
                float nativeDeadline = Time.realtimeSinceStartup + _settings.N("hSession.timeoutSeconds");
                while (!_nativeWalking.PlayerReady && _nativeWalking.Error == null && !_returnRequested)
                {
                    if (Time.realtimeSinceStartup > nativeDeadline) throw new TimeoutException("本編の歩行プレイヤー初期化が制限時間を超えました。");
                    yield return null;
                }
                if (_nativeWalking.Error != null) throw new InvalidOperationException("本編歩行の初期化に失敗しました。", _nativeWalking.Error);
                if (_returnRequested) { _loading = false; BeginReturn(); yield break; }
                StartupDiagnostics?.Mark("native.player.ready");
            }
            StartupDiagnostics?.Mark("host.start.begin");
            _host = gameObject.AddComponent<SandboxHost>(); _host.ConfigureKksMode(content, _loadingScreen.HasVr);
            if (_resumeMoment != null) _host.InitialSettings = _settings;
            if (_nativeWalking != null)
                _host.SetNativeWalkerFactory(session => _nativeWalking.CreateWalker(session, _loadingScreen.NativeRenderCamera, _loadingScreen.NativeOrigin));
            _host.Diagnostics = StartupDiagnostics;
            while (!_host.Ready && _host.Error == null) yield return null;
            if (_host.Error != null) throw new InvalidOperationException(_host.Error);
            if (_nativeWalking != null)
            {
                StartupDiagnostics?.Mark("native.floor.begin");
                _nativeWalking.DoorExclusions = _host.Island.Doors.NavigationExclusions;
                _host.Island.Doors.BindView(_host.Walker.ViewCamera, true);
                var attach = _nativeWalking.AttachMap(_host.Island.Root.transform.root.gameObject, _host.Session.Settings);
                var pending = new Stack<IEnumerator>(); pending.Push(attach);
                try
                {
                    while (pending.Count > default(int))
                    {
                        var iterator = pending.Peek();
                        if (!iterator.MoveNext()) { (pending.Pop() as IDisposable)?.Dispose(); continue; }
                        if (iterator.Current is IEnumerator nested) pending.Push(nested);
                        else yield return iterator.Current;
                    }
                }
                finally { while (pending.Count > default(int)) (pending.Pop() as IDisposable)?.Dispose(); }
                StartupDiagnostics?.Mark("native.floor.ready");
            }
            RestoreMomentEnvironment();
            if (StartupDiagnostics?.Enabled == true)
            {
                var traceRoot = _host.Island.Root.transform.root.gameObject;
                StartupDiagnostics.Snapshot("host.start.end", traceRoot);
                StartupDiagnostics.RendererInventory("host.ready", traceRoot);
            }
            _host.Session.PanelChanged += PanelChanged;
            _host.Session.InputCaptureState = UiInputCaptureApi.GetStateSummary;
            var view = _host.Walker.ViewCamera;
            StartupDiagnostics?.Mark("view.bind.begin");
            if (_nativeWalking == null && !_loadingScreen.BindView(view)) view.gameObject.AddComponent<AudioListener>();
            StartupDiagnostics?.Snapshot("view.bind.end");
            if (StartupDiagnostics?.Enabled == true)
                _loadingScreen.ObserveNextRender("world.first-render", _host.Island.Root.transform.root.gameObject);
            StartupDiagnostics?.Mark("player.initialize.begin");
            _playerAvatar = new AiModePlayerAvatar(_host, _log, _error, _resumeMoment?.playerCard == null ? null : HotelRoomAppearanceRuntime.Decode(_resumeMoment.playerCard));
            StartupDiagnostics?.Mark("player.initialize.end");
            _islandAudio = gameObject.AddComponent<AiModeAudio>();
            var audioSteps = _islandAudio.Initialize(_host, content, _log);
            StartupDiagnostics?.Mark("audio.initialize.begin");
            while (audioSteps.MoveNext()) yield return audioSteps.Current;
            StartupDiagnostics?.Mark("audio.initialize.end");
            StartupDiagnostics?.Mark("animation.initialize.begin");
            _animationTrial = gameObject.AddComponent<AiAnimationTrial>(); _animationTrial.Initialize(_host, content, _log);
            StartupDiagnostics?.Mark("animation.initialize.end");
            _animationTrial.PlayerControls = _playerAvatar.DrawControls;
            _gateScope = CanonGate.GateApi.EnterAiShoujoMode(this);
            _gateScope.PauseActorMotion = _animationTrial.PauseForExternalMotion;
            _animationTrial.ActorChanged += ModeActorChanged;
            ModeActorChanged(_animationTrial.CurrentActor);
            StartupDiagnostics?.Mark("navigation.initialize.begin");
            _navigation = new AiModeNavigation(_host, _animationTrial, content, _log, _error);
            StartupDiagnostics?.Mark("navigation.initialize.end");
            StartupDiagnostics?.Mark("movement.initialize.begin");
            _specialMovement=new AiSpecialMovement(_host,_animationTrial,_playerAvatar,_navigation.World,content,_log,_error);
            StartupDiagnostics?.Mark("movement.initialize.end");
            StartupDiagnostics?.Mark("pods.initialize.begin");
            _pods = gameObject.AddComponent<AiPodSession>(); _pods.Initialize(_host, _animationTrial, content, _log);
            StartupDiagnostics?.Mark("pods.initialize.end");
            var lifeSettings = AiLifeRuntimeSettings.LoadContent(content);
            if (_resumeMoment != null) lifeSettings.savePath = _momentStore.WorkingLife;
            _life = new AiLifeRuntime(lifeSettings, () => DateTime.Now, _log);
            _life.LoadItems(content);
            _life.LoadConnections(content);
            _life.LoadActivities(content);
            _furniture=new Furniture.AiFurnitureSession(_host,_animationTrial,()=>_conversation?.Resident,content,_resumeMoment?.furniture);
            _navigation.FurnitureBounds=()=>_furniture?.BlockedBounds()??Array.Empty<Bounds>();
            _furniture.Changed+=()=>_navigation?.RefreshFurniture();
            _navigation.RefreshFurniture();
            _pickups = new AiModePickupSession(_host, _life);
            _conversation = new AiGirlConversation { Life = _life };
            _conversation.PlacePickup = () => _pickups.Place();
            _conversation.PickupStatus = () => _pickups.LastMessage;
            _conversation.ResidentBound = controller =>
            {
                _lifeActor?.Dispose(); _lifeActor = null;
                if (controller != null) _lifeActor = new AiLifeActorBridge(controller, _animationTrial,
                    () => _host.Walker?.transform,
                    () => CalendarClock.AbsoluteSeconds(_host.Session.World.Year, _host.Session.World.Month, _host.Session.World.Day, _host.Session.World.Hour, _host.Session.Settings), _log, _host,_furniture);
            };
            _conversation.ClockSync = () => _lifeActor?.SynchronizeClock();
            _conversation.Furniture=_furniture;
            StartupDiagnostics?.Mark("conversation.initialize.begin");
            _conversation.Initialize(_host, _animationTrial, () => _playerAvatar.Actor, content, ConversationChanged, _log, () => _islandAudio.SuspendMusic(), _resumeMoment == null ? null : _momentStore.WorkingConversation);
            StartupDiagnostics?.Mark("conversation.initialize.end");
            _conversation.RequestH = BeginH;
            _conversation.CanTradeNativeMerchant = () => { try { return MainGameAiShoujoH.Plugin.ReadResidentProfile(_conversation.Resident.ResidentId).Merchant; } catch { return false; } };
            _conversation.OpenNativeMerchant = OpenDedicatedHMerchant;
            _conversation.TradeNativeMerchant = TradeDedicatedHMerchant;
            _playback = gameObject.AddComponent<AiModePlaybackBar>();
            _playback.Initialize(() => _host.Session.Settings, _host.Walker,
                () => !_loading && !_closing && (_hSession?.Busy == true || (!_help.Visible && !_host.Session.Panel && !_conversation.Active)), _error);
            _lighting = new Lighting.AiLightingSession(_host.Session.World, transform, _host.Walker.ViewCamera);
            _animationTrial.ConversationInvalidated += HInvalidated;
            StartupDiagnostics?.Mark("authoring.initialize.begin");
            InitializeAuthoring(content);
            _momentMenu = new AiMomentSaveMenu(SaveMoment, LoadMoment, () =>
            {
                if (_host.Session.Panel) _host.Session.TogglePanel();
                _help.SetVisible(true);
            });
            _host.Session.ModeMenuControls = ()=>{_momentMenu.Draw();_furniture?.Draw();};
            _host.Session.ModeMenuOwnsWindow=()=>_furniture?.UsesWindow==true;
            StartupDiagnostics?.Mark("authoring.initialize.end");
            _animationTrial.AdditionalControls = _pods.DrawControls;
            _host.Island.Doors.BeforeInteract = () => _hSession?.Busy == true || _specialMovement?.PlayerMoving==true || _help.Visible || _conversation.Active || _pickups.TryInteract() || _conversation.TryInteract() || _pods.TryInteract() || _specialMovement.TryInteract();
            if (_returnRequested) { _loading = false; BeginReturn(); yield break; }
            _loadingScreen.Reveal(() =>
            {
                try { CompleteMomentResume(); }
                catch (Exception ex) { _loading = false; _error(ex); BeginReturn(); return; }
                _loading = false;
                StartupDiagnostics?.Snapshot("mode.ready");
                _lighting?.WalkingReady(_host.Walker.ViewCamera);
                if (_returnRequested) BeginReturn();
                else
                {
                    if (StartupDiagnostics?.Enabled == true) _log("AI_SHOUJO_MODE_READY: map loaded; WASD walk, mouse look, F talk/door/pod, V free camera, I menu, Escape return.");
                    _log("AI_MODE_INPUT_STATE " + UiInputCaptureApi.GetStateSummary());
                }
            });
        }
        private void HelpChanged()
        {
            if (_help.Visible) _playback?.Suspend(); else _playback?.Resume();
            if (_help.Visible)
            {
                var player = _host?.Walker; player?.SetCursorReleased(true);
                _inputOwned = true;
                AiModeNativeUiCapture.Apply(Plugin.Guid, _help.InputRect);
            }
            else { ReleaseInput(); _host?.Walker?.SetCursorReleased(false); }
        }
        private void PanelChanged(bool shown)
        {
            if (shown) _playback?.Suspend(); else _playback?.Resume();
            if (!shown) { _furniture?.CancelEdit();ReleaseInput(); }
            else
            {
                _inputOwned = true;
                UiInputCaptureApi.SetInputBlockRects(Plugin.Guid, new[] { _host.Session.PanelRect });
                UiInputCaptureApi.SetIdleCursorUnlock(Plugin.Guid, true);
            }
        }
        private void ConversationChanged()
        {
            if (_host?.Session == null) return;
            var player = _host?.Walker;
            bool active = _conversation != null && _conversation.Active;
            if(active)_furniture?.CancelEdit();
            if (active) _playback?.Suspend(); else _playback?.Resume();
            _host.Session.ExternalInputBlocked = active;
            if (active)
            {
                _conversationCursorReleased = player != null && player.Snapshot().cursorReleaseRequested;
                player?.SetCursorReleased(true);
                _inputOwned = true;
                AiModeNativeUiCapture.Apply(Plugin.Guid, _conversation.InputRect);
            }
            else
            {
                ReleaseInput();
                player?.SetCursorReleased(_conversationCursorReleased);
            }
        }
        private void BeginH()
        {
            ShowDedicatedHParticipants();
        }
        private void BeginLegacyH()
        {
            if (_hSession?.Busy == true || _closing || _loading) return;
            var target = _animationTrial.ConversationTarget;
            if (target == null) throw new InvalidOperationException("会話中の女の子がいません。");
            Plugin.RecordEventPositions(() => Conversation.EventPositionSnapshot.Capture("h.request", _host,
                _playerAvatar?.Root, target, _animationTrial.ConversationIdentityKey, furniture: _furniture?.CaptureEventPositions()));
            var location = AiModeHLocations.Resolve(_host, target, _playerAvatar?.Root, _animationTrial.ConversationIdentityKey);
            Plugin.RecordEventPositions(() => Conversation.EventPositionSnapshot.Capture("h.location_selected", _host,
                _playerAvatar?.Root, target, _animationTrial.ConversationIdentityKey, location, furniture: _furniture?.CaptureEventPositions()));
            if (_hSession == null)
            {
                var session = new AiModeHSession();
                try { session.Initialize(this, _host.Session.Settings, HStarted, HEnded, _log, _error, HReady); _hSession = session; }
                catch { session.Dispose(); throw; }
            }
            _hSession.Begin(_animationTrial.ExportHParticipant(), _playerAvatar.ExportPlayer(), _host.Island.Root, location);
        }
        private void SuspendForH(Behaviour behaviour)
        {
            if (behaviour == null || _hSuspended.ContainsKey(behaviour)) return;
            _hSuspended.Add(behaviour, behaviour.enabled); behaviour.enabled = false;
        }
        private void HStarted()
        {
            _lighting?.BeforeH();
            _furniture?.Suspend();
            _authoring?.SuspendActivity();
            _specialMovement?.Cancel();
            _nativeWalking?.Suspend();
            _playback?.Suspend();
            _hSuspending = true;
            _gateScope?.Suspend();
            _playerAvatar?.Suspend();
            _hResume = _conversation.SuspendForH();
            _hRestoreGirl = _animationTrial.HideForH();
            if (_help.Visible) _help.SetVisible(false);
            if (_host.Session.Panel) _host.Session.TogglePanel();
            _host.Session.ExternalInputBlocked = true;
            ReleaseInput();
            SuspendForH(_host.Session);
            // The island clock, weather and daylight keep advancing during H.
            _host.Island.Visibility?.BindCamera(null);
            SuspendForH(_host.Island.Doors);
            SuspendForH(_host.Walker);
            foreach (var camera in _host.GetComponentsInChildren<Camera>(true)) SuspendForH(camera);
            foreach (var listener in _host.GetComponentsInChildren<AudioListener>(true)) SuspendForH(listener);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        private void HEnded()
        {
            _playback?.Suspend();
            if (_hSuspending)
            {
                _hSuspending = false;
                if (_modeScene.IsValid() && _modeScene.isLoaded) SceneManager.SetActiveScene(_modeScene);
                foreach (var pair in _hSuspended) if (pair.Key != null) pair.Key.enabled = pair.Value;
                _hSuspended.Clear();
                if (_host?.Session != null)
                {
                    _host.Session.ExternalInputBlocked = false;
                    _host.Session.World.SetHour(_host.Session.World.Hour);
                    _host.Session.World.BindCamera(_host.Walker.ViewCamera);
                    _host.Island.Visibility?.BindCamera(_host.Walker.ViewCamera);
                }
                var showGirl = _hRestoreGirl; _hRestoreGirl = null; showGirl?.Invoke();
                _playerAvatar?.Resume();
                _nativeWalking?.Resume();
                _lighting?.WalkingReady(_host.Walker.ViewCamera);
                var resume = _hResume; _hResume = null; resume?.Invoke();
                _gateScope?.Resume();
                _host?.Walker?.SetCursorReleased(_conversationCursorReleased);
                _loading = true;
                try
                {
                    _loadingScreen.RevealAfterH(() =>
                    {
                        _loading = false;
                        _authoring?.ResumeActivity();
                        _playback?.Resume();
                        _log("AI_GIRL_H_RETURN_VISIBLE");
                        if (_returnRequested && !_closing) BeginReturn();
                    });
                }
                catch (Exception ex) { _loading = false; _error(ex); if (_returnRequested && !_closing) BeginReturn(); }
                return;
            }
            _lighting?.WalkingReady(_host?.Walker?.ViewCamera);
            if (_returnRequested && !_closing) BeginReturn();
        }
        private void HReady()
        {
            if (_host?.Session == null || _hSession?.Busy != true) return;
            _host.Session.World.SetHour(_host.Session.World.Hour);
            _host.Session.World.BindCamera(_hSession.ActiveCamera);
            _host.Island.Visibility?.BindCamera(_hSession.ActiveCamera);
            _lighting?.HReady(_hSession.ActiveCamera, _hSession.LightingScene);
            Plugin.RecordEventPositions(() => _hSession.CaptureEventPositions(_host, _animationTrial.ConversationIdentityKey));
            _playback?.Resume();
            _log("AI_GIRL_H_READY");
        }
        private void HInvalidated() { MainGameAiShoujoH.Plugin.End(MainGameAiShoujoH.Contracts.EndReason.PermissionRevoked); if (_hSession?.Busy == true) _hSession.Cancel(); }
        private void ModeActorChanged(ChaControl actor) => _gateScope?.SetActor(actor);
        private void Update()
        {
            if (!Plugin.ModeAllowed) { ReleaseInput(); return; }
            if (_authoring?.Occupied == true) return;
            if (_settings == null || _closing || _hSession?.Busy == true || MainGameAiShoujoH.Plugin.Busy) return;
            if (_conversation?.Active == true)
            {
                if (Input.GetKeyDown((KeyCode)Enum.Parse(typeof(KeyCode), _settings.T("aiMode.returnKey")))) _conversation.Escape();
                return;
            }
            if (Input.GetKeyDown((KeyCode)Enum.Parse(typeof(KeyCode), _settings.T("aiMode.returnKey"))) || (_titleScene.IsValid() && !_titleScene.isLoaded)) RequestReturn();
            if (_host?.Session == null || _loading) return;
            var session = _host.Session;
            if (_help.Visible) return;
            if (!session.Panel) { ReleaseInput(); return; }
            _inputOwned = true;
            UiInputCaptureApi.SetInputBlockRects(Plugin.Guid, new[] { session.PanelRect });
            UiInputCaptureApi.SetIdleCursorUnlock(Plugin.Guid, true);
            _drag = Input.GetMouseButton(_settings.I("math.zero")) && (_drag || session.PointerOverPanel);
            UiInputCaptureApi.Sync(Plugin.Guid, "ai-mode-panel-drag", _drag);
        }
        public void RequestReturn()
        {
            if (_closing) return;
            _returnRequested = true;
            if (MainGameAiShoujoH.Plugin.Busy) { MainGameAiShoujoH.Plugin.End(); return; }
            if (_hSession?.Busy == true) { _hSession.Cancel(); return; }
            if (!_loading) BeginReturn();
        }
        internal void CancelForGate()
        {
            if (_closing) return;
            StopAllCoroutines();
            _returnRequested = true; _loading = false;
            if (_host?.Session != null) _host.Session.enabled = false;
            BeginReturn();
        }
        private void BeginReturn()
        {
            if (_host?.Session != null) _host.Session.ModeMenuControls = null;
            if (_host?.Session != null) _host.Session.ModeMenuOwnsWindow = null;
            StartupDiagnostics?.Mark("return.requested");
            _authoring?.Shutdown();
            _specialMovement?.Dispose();_specialMovement=null;
            _playback?.Suspend();
            if (_closing) return;
            _lighting?.Dispose(); _lighting = null;
            _nativeWalking?.Suspend();
            if (MainGameAiShoujoH.Plugin.Busy) { _returnRequested = true; MainGameAiShoujoH.Plugin.End(); return; }
            if (_hSession?.Busy == true) { _returnRequested = true; _hSession.Cancel(); return; }
            _hSession?.Dispose(); _hSession = null;
            _playerAvatar?.Dispose(); _playerAvatar = null;
            _gateScope?.Dispose(); _gateScope = null;
            if (_animationTrial != null) _animationTrial.ActorChanged -= ModeActorChanged;
            if (_animationTrial != null) _animationTrial.ConversationInvalidated -= HInvalidated;
            _conversation?.Dispose(); _conversation = null;
            _pickups?.Dispose(); _pickups = null;
            _lifeActor?.Dispose(); _lifeActor = null;_furniture?.Dispose();_furniture=null;
            _closing = true; if (_help != null && _help.Visible) _help.SetVisible(false); ReleaseInput();
            try { _loadingScreen.Cover(() => StartCoroutine(Return())); }
            catch (Exception ex) { _error(ex); StartCoroutine(Return()); }
        }
        private IEnumerator Return()
        {
            if (_host?.Island?.Doors != null) _host.Island.Doors.BeforeInteract = null;
            if (_animationTrial != null) _animationTrial.AdditionalControls = null;
            if (_pods != null) { DestroyImmediate(_pods); _pods = null; }
            _navigation?.Dispose(); _navigation = null;
            if (_animationTrial != null) { DestroyImmediate(_animationTrial); _animationTrial = null; }
            if (_islandAudio != null) { DestroyImmediate(_islandAudio); _islandAudio = null; }
            if (_host?.Session != null) _host.Session.PanelChanged -= PanelChanged;
            if (_nativeWalking != null)
            {
                _nativeWalking.Dispose();
                while (!_nativeWalking.Released) yield return null;
                _nativeWalking = null;
            }
            if (_host != null) yield return _host.Shutdown();
            _loadingScreen.RestoreView();
            if (_previousScene.IsValid() && _previousScene.isLoaded) SceneManager.SetActiveScene(_previousScene);
            foreach (var root in _titleRoots) if (root.Key != null) root.Key.SetActive(root.Value);
            foreach (var camera in _cameras) if (camera.Key != null) camera.Key.enabled = camera.Value;
            foreach (var listener in _listeners) if (listener.Key != null) listener.Key.enabled = listener.Value;
            foreach (var audio in _audio) if (audio.Key != null && audio.Key.clip == audio.Value) audio.Key.UnPause();
            _cursorScope?.Dispose(); _cursorScope = null;
            if (_nativeCursor != null) { _nativeCursor.enabled = _nativeCursorEnabled; _nativeCursor.SetCursorLock(_nativeCursorLocked); }
            Cursor.lockState = _cursorLock; Cursor.visible = _cursorVisible;
            _log("AI_SHOUJO_MODE_RETURNED_TO_TITLE");
            _loadingScreen.Reveal(FinishReturn);
        }
        private void FinishReturn()
        {
            StartupDiagnostics?.Mark("return.completed"); StartupDiagnostics?.Dispose();
            if (_modeScene.IsValid() && _modeScene.isLoaded)
            {
                var restart = _restartFromMoment ? RestartMoment : null;
                var unload = SceneManager.UnloadSceneAsync(_modeScene);
                if (restart != null && unload != null) unload.completed += operation => restart();
            }
            else Destroy(gameObject);
        }
        private void ReleaseInput()
        {
            if (!_inputOwned) return;
            _inputOwned = false;
            _drag = false; UiInputCaptureApi.Sync(Plugin.Guid, "ai-mode-panel-drag", false);
            UiInputCaptureApi.ClearInputBlockRects(Plugin.Guid); UiInputCaptureApi.SetIdleCursorUnlock(Plugin.Guid, false);
        }
        private void OnApplicationFocus(bool focused)
        {
            if (!Plugin.ModeAllowed) { ReleaseInput(); return; }
            if (!focused) ReleaseInput();
            else if (_conversation?.Active == true)
            {
                _inputOwned = true;
                AiModeNativeUiCapture.Apply(Plugin.Guid, _conversation.InputRect);
            }
        }
        private void OnDestroy() { MainGameAiShoujoH.Plugin.OwnerDestroyed(this); _lighting?.Dispose(); _lighting = null; _nativeWalking?.Dispose(); _nativeWalking = null; StartupDiagnostics?.Mark("session.destroy"); StartupDiagnostics?.Dispose(); _pickups?.Dispose(); _pickups = null; _lifeActor?.Dispose(); _lifeActor = null; _furniture?.Dispose();_furniture=null;_specialMovement?.Dispose();_specialMovement=null;_hSession?.Dispose(); _hSession = null; _playerAvatar?.Dispose(); _playerAvatar = null; _gateScope?.Dispose(); _gateScope = null; _conversation?.Dispose(); _conversation = null; _navigation?.Dispose(); _navigation = null; _cursorScope?.Dispose(); _cursorScope = null; _help?.Dispose(); _loadingScreen?.Dispose(); ReleaseInput(); _closed?.Invoke(); }
    }
}
