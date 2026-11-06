using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MapEnvironmentRuntime
{
    public sealed class SandboxHost : MonoBehaviour
    {
        private SandboxSession _session;
        private AssetBundle _bundle;
        private AssetBundle _characterBundle;
        private GameObject _referenceCharacter;
        private SandboxIslandLoader _island;
        private string _fatal;
        private string _contentOverride;
        private bool _insideKks;
        private bool _useMeshVegetation;
        private Parameters _loadingSettings;
        private Scene _mapScene;
        private GameObject _player;
        private Func<IWalkingSession, SandboxFreeCamera> _nativeWalkerFactory;
        private SandboxFreeCamera _walker;
        private bool _ownsPlayer;
        public SandboxFreeCamera Walker => _walker;
        public StartupTrace Diagnostics { get; set; }
        public Parameters InitialSettings { get; set; }
        public SandboxSession Session => _session;
        public SandboxIslandLoader Island => _island;
        public bool Ready { get; private set; }
        public string Error => _fatal;
        public string LoadingStatus => _island?.Status ?? "環境データを読み込み中";
        public bool HasReferenceCharacter => _referenceCharacter != null;
        public Vector3 FromIslandSourcePosition(Vector3 position) => _island.FromSourcePosition(position);
        public Vector3 ToIslandSourcePosition(Vector3 position) => _island.ToSourcePosition(position);
        public void SetWalkingNavigation(NavigationWorld world) { _walker?.SetNavigation(world); }
        public void SetNativeWalkerFactory(Func<IWalkingSession, SandboxFreeCamera> factory) { _nativeWalkerFactory = factory; }
        public void ConfigureKksMode(string content) => ConfigureKksMode(content, false);
        public void ConfigureKksMode(string content, bool useMeshVegetation) { _contentOverride = content; _insideKks = true; _useMeshVegetation = useMeshVegetation; }
        private IEnumerator Start()
        {
            string directory = _contentOverride ?? Path.Combine(Application.dataPath, "../Content");
            string path = Path.Combine(directory, "parameters.json");
            Parameters settings = null;
            try
            {
                Diagnostics?.Mark("host.settings.begin", () => path);
                settings = InitialSettings?.Copy() ?? Parameters.Load(path); _loadingSettings = settings; if (!_insideKks) ApplyDisplay(settings);
                Diagnostics?.Mark("host.settings.end");
                Diagnostics?.Mark("host.bundle.begin", () => Path.Combine(directory, settings.T("content.bundle")));
                _bundle = AssetBundle.LoadFromFile(Path.Combine(directory, settings.T("content.bundle")));
                if (_bundle == null) throw new InvalidDataException("Map AssetBundle load failed");
                Diagnostics?.Mark("host.bundle.end");
            }
            catch (Exception ex) { Diagnostics?.Mark("host.failed", () => ex.ToString()); _fatal = ex.ToString(); Debug.LogError(_fatal); }
            if (settings == null || _bundle == null) yield break;
            string scenePath = _bundle.GetAllScenePaths().Single();
            Diagnostics?.Mark("host.scene.begin", () => scenePath);
            yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Diagnostics?.Snapshot("host.scene.end");
            _mapScene = SceneManager.GetSceneByPath(scenePath);
            if (settings.T("sandbox.map") == "ai-island")
            {
                try
                {
                    var bootstrap = _mapScene.GetRootGameObjects().Single(root => root.name == settings.T("content.root"));
                    foreach (var geometry in settings.geometry) bootstrap.transform.Find(geometry.name).gameObject.SetActive(false);
                }
                catch (Exception ex) { _fatal = ex.ToString(); Diagnostics?.Mark("host.bootstrap.failed", () => _fatal); yield break; }
                _island = gameObject.AddComponent<SandboxIslandLoader>(); _island.Diagnostics = Diagnostics;
                yield return _island.Load(settings, _useMeshVegetation);
                if (_island.Error != null) { _fatal = _island.Error; yield break; }
            }
            try
            {
                var scene = SceneManager.GetSceneByPath(scenePath); _mapScene = scene;
                var map = scene.GetRootGameObjects().Single(root => root.name == settings.T("content.root"));
                if (_island != null && InitialSettings == null) { settings.revision = Guid.NewGuid().ToString("N"); Parameters.AtomicWrite(path, JsonCodec.ToJson(settings, true)); }
                Diagnostics?.Mark("host.environment.begin");
                _session = gameObject.AddComponent<SandboxSession>(); _session.Initialize(map.transform, path, true, _insideKks ? "KKS AI Mode" : null, InitialSettings == null ? null : settings);
                Diagnostics?.Mark("host.environment.end");
                if (_island != null && _island.Doors != null) _island.Doors.AttachSession(_session);
                Diagnostics?.Mark("host.walker.begin");
                SandboxFreeCamera controller;
                if (_nativeWalkerFactory != null)
                {
                    controller = _nativeWalkerFactory(_session);
                    if (controller == null || !controller.UsesNativeWalking) throw new InvalidOperationException("The native walking factory did not supply a native driver.");
                    _player = controller.gameObject;
                }
                else
                {
                    var player = new GameObject("Sandbox Free Camera"); _player = player; _ownsPlayer = true;
                    player.transform.SetParent(transform, false);
                    if (_island != null) _island.PrepareWalkingLayer(player.layer);
                    controller = player.AddComponent<SandboxFreeCamera>(); controller.Initialize(_session);
                }
                _walker = controller;
                if (_island != null) controller.PlaceAtIslandStart(_island.InitialWalkPosition(settings.V("spawn")), settings.N("island.stage.yaw"));
                if (_island != null) _island.AttachCamera(controller.ViewCamera, _session.Settings);
                Diagnostics?.Mark("host.walker.end");
                if (_insideKks) { Ready = true; Debug.Log("KKS_AI_MODE_WORLD_READY"); yield break; }
                _characterBundle = AssetBundle.LoadFromFile(Path.Combine(directory, settings.T("character.bundle")));
                if (_characterBundle == null) throw new InvalidDataException("Reference character bundle missing");
                _referenceCharacter = Instantiate(_characterBundle.LoadAllAssets<GameObject>().Single());
                var facing = Quaternion.Euler(settings.N("math.zero"), settings.N("spawn.yaw"), settings.N("math.zero"));
                var position = settings.V("spawn") + facing * Vector3.forward * settings.N("character.distance");
                position.y = map.transform.position.y + settings.N("character.groundOffset");
                _referenceCharacter.transform.position = position;
                _referenceCharacter.transform.rotation = facing;
                BringCharacterFront();
                Debug.Log("SANDBOX_REFERENCE_CHARACTER_READY " + position);
                Ready = true;
            }
            catch (Exception ex) { Diagnostics?.Mark("host.failed", () => ex.ToString()); _fatal = ex.ToString(); Debug.LogError(_fatal); }
        }
        public static void ApplyDisplay(Parameters settings)
        {
            QualitySettings.vSyncCount = settings.I("render.vsync");
            QualitySettings.antiAliasing = settings.I("render.aa");
            Application.targetFrameRate = settings.I("render.targetFps");
            if (Screen.width != settings.I("render.width") || Screen.height != settings.I("render.height"))
                Screen.SetResolution(settings.I("render.width"), settings.I("render.height"), settings.B("render.fullscreen"));
        }
        public void BringCharacterFront()
        {
            if (_referenceCharacter == null || _session == null || Camera.main == null) throw new InvalidOperationException("ルナまたはカメラが準備できていません。");
            var settings = _session.Settings; var camera = Camera.main;
            var forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < settings.N("render.epsilon")) forward = camera.transform.parent != null ? camera.transform.parent.forward : Vector3.forward;
            forward.Normalize(); var position = camera.transform.position + forward * settings.N("character.distance");
            float ground = _session.World.GroundHeight;
            var terrain = FindObjectOfType<Terrain>();
            if (terrain != null) ground = terrain.SampleHeight(position) + terrain.transform.position.y;
            Physics.SyncTransforms();
            if (Physics.Raycast(position + Vector3.up * settings.N("character.groundProbeHeight"), Vector3.down, out RaycastHit hit, settings.N("character.groundProbeDistance"), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) ground = hit.point.y;
            position.y = ground + settings.N("character.groundOffset");
            _referenceCharacter.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward));
            Debug.Log("SANDBOX_REFERENCE_CHARACTER_MOVED " + position);
        }
        public IEnumerator Shutdown()
        {
            Ready = false;
            if (_island != null) _island.ShutdownInteractions();
            if (_session != null)
            {
                if (_session.World != null) DestroyImmediate(_session.World);
                DestroyImmediate(_session); _session = null;
            }
            if (_ownsPlayer && _player != null) DestroyImmediate(_player);
            else if (_walker != null) DestroyImmediate(_walker);
            _player = null; _walker = null;
            if (_referenceCharacter != null) { DestroyImmediate(_referenceCharacter); _referenceCharacter = null; }
            if (_mapScene.IsValid() && _mapScene.isLoaded) yield return SceneManager.UnloadSceneAsync(_mapScene);
            if (_island != null && _island.LoadedScene.IsValid() && _island.LoadedScene.isLoaded) yield return SceneManager.UnloadSceneAsync(_island.LoadedScene);
            if (_island != null) { DestroyImmediate(_island); _island = null; }
            if (_bundle != null) { _bundle.Unload(false); _bundle = null; }
        }
        private void OnGUI()
        {
            if (_insideKks) return;
            string message = _fatal != null ? "起動失敗：" + _fatal : _island != null && _session == null ? _island.Status : null;
            if (message == null) return;
            if (_loadingSettings != null)
                GUI.Label(new Rect(_loadingSettings.N("ui.x"), _loadingSettings.N("ui.y"), _loadingSettings.N("ui.width"), _loadingSettings.N("ui.infoHeight")), message);
        }
        private void OnDestroy() { if (_referenceCharacter != null) Destroy(_referenceCharacter); if (_characterBundle != null) _characterBundle.Unload(false); if (_bundle != null) _bundle.Unload(false); }
    }

    public sealed class WalkingStatus
    {
        public bool grounded, crouching, cursorReleased, cursorVisible, cursorReleaseRequested, applicationFocused;
        public string cursorLockMode;
        public string navigationState;
        public string movementConstraint, movementCollisionFlags;
        public int movementFrame, movementAgeFrames;
        public bool movementCommandActive;
        public bool physicalGrounded;
        public Vector3 requestedMovement, guidedMovement, actualMovement;
        public float height, radius, step, slopeLimit;
        public string blockingCollider;
        public int blockingLayer;
        public Vector3 blockingPoint, blockingNormal, cameraPosition;
    }
    public sealed class SandboxFreeCamera : MonoBehaviour
    {
        private IWalkingSession _session;
        private INativeWalkingDriver _native;
        private IWalkingInputNotifications _inputNotifications;
        public bool UsesNativeWalking => _native != null;
        public Action PrepareNativeTraversal(Transform motion) => _native != null ? _native.PrepareTraversal(motion) : throw new InvalidOperationException("Native walking is not attached.");
        private Camera _camera;
        private CharacterController _body;
        private SandboxIslandWalkSurfaces _walkSurfaces;
        private float _fall;
        private float _pitch, _yaw;
        private bool _looking;
        private bool _cursorReleased, _crouching;
        private Collider[] _standHits;
        private readonly WalkingStatus _walking = new WalkingStatus();
        public bool Crouching => _native != null ? _native.Crouching : _crouching;
        public Camera ViewCamera => _camera;
        public void SetViewPose(Vector3 eyePosition, Quaternion rotation, float fieldOfView)
        {
            if (_native != null) { _native.SetViewPose(eyePosition, rotation, fieldOfView); return; }
            if (_camera == null || _body == null) throw new InvalidOperationException("Walking camera is not initialized.");
            _body.enabled = false; _fall = P.N("math.zero");
            _yaw = rotation.eulerAngles.y; _pitch = Mathf.DeltaAngle(P.N("math.zero"), rotation.eulerAngles.x);
            transform.rotation = Quaternion.Euler(P.N("math.zero"), _yaw, P.N("math.zero"));
            transform.position = eyePosition - transform.rotation * _camera.transform.localPosition;
            _camera.transform.rotation = rotation; _camera.fieldOfView = fieldOfView; _walkSurfaces?.Relocate();
        }
        public Vector3 FeetLocalOffset => _native != null ? _native.FeetLocalOffset : Vector3.up * (_body.radius - _body.height);
        public void RestoreSavedPose(Vector3 feet, Quaternion heading, Vector3 eye, Quaternion view, float fieldOfView, bool crouching)
        {
            if (_native != null) { _native.WarpFeet(feet, heading); return; }
            _crouching = crouching; ConfigureMovement(P);
            WarpFeet(feet, heading);
            SetViewPose(eye, view, fieldOfView);
            ViewGeometryChanged?.Invoke();
        }
        public event Action ViewGeometryChanged;
        public void WarpFeet(Vector3 feet,Quaternion heading)
        {
            if (_native != null) { _native.WarpFeet(feet, heading); return; }
            _body.enabled=false;_fall=P.N("math.zero");
            transform.position=feet-FeetLocalOffset;_yaw=heading.eulerAngles.y;
            transform.rotation=Quaternion.Euler(P.N("math.zero"),_yaw,P.N("math.zero"));
            _walkSurfaces?.Relocate();
        }
        private Vector3 _configuredPosition;
        private float _configuredYaw, _configuredPitch;
        private Parameters P => _session.Settings;
        public void PlaceAtIslandStart(Vector3 position, float heading)
        {
            if (_native != null) { _native.PlaceAtIslandStart(position, heading); return; }
            _body.enabled = false; transform.position = position; _yaw += heading;
            transform.rotation = Quaternion.Euler(P.N("math.zero"), _yaw, P.N("math.zero"));
            _walkSurfaces?.Relocate();
        }
        public void SetNavigation(NavigationWorld world) { _walkSurfaces?.SetNavigation(world); }
        public WalkingStatus Snapshot()
        {
            if (_native != null) return _native.Snapshot();
            _walking.grounded = _walkSurfaces.Grounded || _body.enabled && _body.isGrounded; _walking.crouching = _crouching; _walking.cursorReleased = Cursor.lockState != CursorLockMode.Locked;
            _walking.navigationState = _walkSurfaces.State;
            _walking.physicalGrounded = _body.enabled && _body.isGrounded;
            _walking.movementConstraint = _walkSurfaces.Constraint; _walking.movementCollisionFlags = _walkSurfaces.LastCollisionFlags.ToString();
            _walking.movementFrame = _walkSurfaces.MoveFrame; _walking.movementAgeFrames = Time.frameCount - _walkSurfaces.MoveFrame;
            _walking.movementCommandActive = _walkSurfaces.CommandActive; _walking.requestedMovement = _walkSurfaces.RequestedDelta;
            _walking.guidedMovement = _walkSurfaces.GuidedDelta; _walking.actualMovement = _walkSurfaces.ActualDelta;
            _walking.cursorVisible = Cursor.visible; _walking.cursorLockMode = Cursor.lockState.ToString(); _walking.cursorReleaseRequested = _cursorReleased; _walking.applicationFocused = Application.isFocused;
            _walking.height = _body.height; _walking.radius = _body.radius; _walking.step = _body.stepOffset; _walking.slopeLimit = _body.slopeLimit; _walking.cameraPosition = transform.position;
            return _walking;
        }
        public void Initialize(SandboxSession session) => Initialize((IWalkingSession)session);
        public void InitializeNative(IWalkingSession session, INativeWalkingDriver driver)
        {
            if (_session != null || driver == null) throw new InvalidOperationException("Walking adapter initialization is invalid.");
            _session = session; _native = driver; _camera = driver.ViewCamera;
            if (_camera == null) throw new InvalidOperationException("The native render camera is unavailable.");
            _session.PanelChanged += OnPanelChanged;
            _session.ParametersApplied += ConfigureMovement;
            _inputNotifications = session as IWalkingInputNotifications;
            if (_inputNotifications != null) _inputNotifications.InputBlockChanged += ApplyCursor;
            ConfigureMovement(session.Settings);
            ApplyCursor();
        }
        public void Initialize(IWalkingSession session)
        {
            _session = session;
            _session.PanelChanged += OnPanelChanged;
            _configuredPosition = P.V("spawn"); transform.position = _configuredPosition;
            _configuredYaw = P.N("spawn.yaw"); _yaw = _configuredYaw;
            _configuredPitch = P.N("spawn.pitch"); _pitch = _configuredPitch;
            var camera = new GameObject("Sandbox Camera"); camera.tag = "MainCamera"; camera.transform.SetParent(transform, false);
            _camera = camera.AddComponent<Camera>(); _camera.clearFlags = CameraClearFlags.Skybox;
            _body = gameObject.AddComponent<CharacterController>(); _body.enabled = false;
            _walkSurfaces = new SandboxIslandWalkSurfaces(_body, () => P);
            ConfigureMovement(P);
            _session.ParametersApplied += ConfigureMovement;
            _standHits = new Collider[P.I("movement.standHitCapacity")];
            transform.rotation = Quaternion.Euler(P.N("math.zero"), _yaw, P.N("math.zero"));
            _camera.transform.localRotation = Quaternion.Euler(_pitch, P.N("math.zero"), P.N("math.zero"));
        }
        private bool Held(string key) => Input.GetKey((KeyCode)Enum.Parse(typeof(KeyCode), P.T(key)));
        private void ConfigureMovement(Parameters settings)
        {
            if (_native != null) { _native.Configure(settings); ViewGeometryChanged?.Invoke(); return; }
            if (_configuredPosition != settings.V("spawn"))
            {
                _body.enabled = false; _configuredPosition = settings.V("spawn"); transform.position = _configuredPosition;
                _fall = settings.N("math.zero"); _walkSurfaces.Relocate();
            }
            if (_configuredYaw != settings.N("spawn.yaw")) { _configuredYaw = settings.N("spawn.yaw"); _yaw = _configuredYaw; }
            if (_configuredPitch != settings.N("spawn.pitch")) { _configuredPitch = settings.N("spawn.pitch"); _pitch = _configuredPitch; }
            _camera.fieldOfView = settings.N("render.fov"); _camera.nearClipPlane = settings.N("render.near"); _camera.farClipPlane = settings.N("render.far");
            if (_standHits == null || _standHits.Length != settings.I("movement.standHitCapacity")) _standHits = new Collider[settings.I("movement.standHitCapacity")];
            _body.height = settings.N(_crouching ? "movement.crouchHeight" : "movement.bodyHeight"); _body.radius = settings.N("movement.bodyRadius");
            _body.center = Vector3.up * (_body.radius - _body.height * settings.N("math.half"));
            _body.skinWidth = settings.N("movement.skin"); _body.stepOffset = Mathf.Min(settings.N("movement.step"), _body.height);
            _body.slopeLimit = settings.N("movement.slopeLimit");
            _walkSurfaces.Configure(settings.T("movement.mode") == "walk");
            ViewGeometryChanged?.Invoke();
        }
        private void Update()
        {
            if (_native != null) return;
            if (_session == null || !_session.WalkingReady) return;
            if (_session.ExternalInputBlocked) return;
            if(P.N("cloud.test.lockCamera")>P.N("math.zero"))
            {
                _body.enabled=false;transform.position=P.V("spawn");_yaw=P.N("spawn.yaw");_pitch=P.N("spawn.pitch");
                transform.rotation=Quaternion.Euler(P.N("math.zero"),_yaw,P.N("math.zero"));
                _camera.transform.localRotation=Quaternion.Euler(_pitch,P.N("math.zero"),P.N("math.zero"));return;
            }
            bool walking = P.T("movement.mode") == "walk";
            if (Input.GetKeyDown((KeyCode)Enum.Parse(typeof(KeyCode), P.T("input.cursorRelease")))) _cursorReleased = !_cursorReleased;

            bool wantCrouch = walking && !_session.EditingParameter && Held("input.crouch");
            if (!walking) wantCrouch = false;
            if (_crouching && !wantCrouch && walking)
            {
                float radius = P.N("movement.bodyRadius"), height = P.N("movement.bodyHeight");
                Vector3 feet = transform.position + Vector3.up * (radius - P.N("movement.crouchHeight"));
                // Only the volume swept by the rising head is new. Testing the whole standing
                // capsule also hits floor/low geometry already occupied while crouching.
                SandboxStandClearance.Capsule(P.N("movement.crouchHeight"), height, radius, P.N("movement.skin"), P.N("render.epsilon"), out var fromHeight, out var toHeight, out var queryRadius);
                int count = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * fromHeight, feet + Vector3.up * toHeight, queryRadius, _standHits, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (count == _standHits.Length || _standHits.Take(count).Any(hit => hit != null && hit != _body && !hit.transform.IsChildOf(transform) && !Physics.GetIgnoreLayerCollision(gameObject.layer, hit.gameObject.layer))) wantCrouch = true;
            }
            if (wantCrouch != _crouching)
            {
                _body.enabled = false;
                if (walking) transform.position += Vector3.up * (wantCrouch ? P.N("movement.crouchHeight") - P.N("movement.bodyHeight") : P.N("movement.bodyHeight") - P.N("movement.crouchHeight"));
                _crouching = wantCrouch;
                ConfigureMovement(P);
            }
            bool lookHeld = Held("input.look");
            if (!lookHeld) _looking = false;
            if (Input.GetKeyDown((KeyCode)Enum.Parse(typeof(KeyCode), P.T("input.look"))) && !_session.PointerOverPanel)
            {
                _looking = true; _session.ReleaseKeyboardFocus();
            }
            if (!_session.EditingParameter && Input.GetKeyDown((KeyCode)Enum.Parse(typeof(KeyCode), P.T("input.cameraMode")))) _session.ToggleCameraMode();
            walking = P.T("movement.mode") == "walk";
            bool physicalBody = walking && _walkSurfaces.RequiresPhysicalBody;
            if (_body.enabled != physicalBody) { _body.enabled = physicalBody; _fall = P.N("math.zero"); }
            bool lockedWalk = walking && !_cursorReleased && !_session.Panel && Application.isFocused;
            if (lockedWalk || (!walking && _looking && lookHeld && Application.isFocused))
            {
                _yaw += Input.GetAxis(P.T("input.horizontalAxis")) * P.N("movement.mouse");
                _pitch = Mathf.Clamp(_pitch - Input.GetAxis(P.T("input.verticalAxis")) * P.N("movement.mouse"), -P.N("movement.pitchLimit"), P.N("movement.pitchLimit"));
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            transform.rotation = Quaternion.Euler(P.N("math.zero"), _yaw, P.N("math.zero"));
            _camera.transform.localRotation = Quaternion.Euler(_pitch, P.N("math.zero"), P.N("math.zero"));
            float forward = (Held("input.forward") ? P.N("math.one") : P.N("math.zero")) - (Held("input.backward") ? P.N("math.one") : P.N("math.zero"));
            float right = (Held("input.right") ? P.N("math.one") : P.N("math.zero")) - (Held("input.left") ? P.N("math.one") : P.N("math.zero"));
            var motion = (walking ? transform.forward : _camera.transform.forward) * forward + transform.right * right;
            if (!walking) motion += Vector3.up * ((Held("input.up") ? P.N("math.one") : P.N("math.zero")) - (Held("input.down") ? P.N("math.one") : P.N("math.zero")));
            motion = Vector3.ClampMagnitude(motion, P.N("math.one")) * P.N(walking ? (_crouching ? "movement.crouchSpeed" : "movement.walkSpeed") : "movement.cameraSpeed");
            if (!_crouching && Held("input.sprint")) motion *= P.N("movement.sprint");
            if (_session.EditingParameter || (_session.PointerOverPanel && !_looking)) motion = Vector3.zero;
            if (walking)
            {
                bool grounded = _walkSurfaces.Enabled ? _walkSurfaces.Grounded || _body.isGrounded : _body.isGrounded;
                if (grounded && _fall < P.N("math.zero")) _fall = P.N("math.zero");
                if (grounded && !_crouching && !_session.Panel && !_session.EditingParameter && Input.GetKeyDown((KeyCode)Enum.Parse(typeof(KeyCode), P.T("input.jump"))))
                {
                    _fall = Mathf.Sqrt(P.N("math.two") * P.N("movement.gravity") * P.N("movement.jumpHeight"));
                    if (_walkSurfaces.Enabled) _walkSurfaces.BeginJump();
                }
                _fall -= P.N("movement.gravity") * Time.deltaTime; motion.y = _fall;
                if (_walkSurfaces.WillMove(motion))
                {
                    _walking.blockingCollider = null; _walking.blockingLayer = default(int);
                    _walking.blockingPoint = Vector3.zero; _walking.blockingNormal = Vector3.zero;
                }
                var collisions = _walkSurfaces.Move(motion, Time.deltaTime, _fall > P.N("math.zero"));
                if ((collisions & CollisionFlags.Above) != CollisionFlags.None && _fall > P.N("math.zero")) _fall = P.N("math.zero");
            }
            else transform.position += motion * Time.deltaTime;
        }
        public void SetCursorReleased(bool released) { _cursorReleased = released; _looking = false; ApplyCursor(); }
        private void OnPanelChanged(bool shown)
        {
            _looking = false; _cursorReleased = shown;
            ApplyCursor();
        }
        private void ApplyCursor()
        {
            if (_native != null)
            {
                _native.SetInputBlocked(!isActiveAndEnabled || _session.ExternalInputBlocked || _cursorReleased || _session.Panel || !Application.isFocused);
                return;
            }
            bool locked = _session != null && !_session.ExternalInputBlocked && P.T("movement.mode") == "walk" && !_cursorReleased && !_session.Panel && Application.isFocused;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !locked;
        }
        private void OnApplicationFocus(bool focused)
        {
            _looking = false;
            if (focused && _session != null && !_session.Panel) _cursorReleased = false;
            ApplyCursor();
        }
        private void OnDestroy()
        {
            if (_inputNotifications != null) _inputNotifications.InputBlockChanged -= ApplyCursor;
            _native?.Dispose(); _native = null;
            _walkSurfaces?.Dispose();
            if (_session != null) { _session.PanelChanged -= OnPanelChanged; _session.ParametersApplied -= ConfigureMovement; }
        }
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y >= Mathf.Cos(P.N("movement.slopeLimit") * P.N("math.pi") / P.N("math.degreesHalfTurn"))) return;
            _walking.blockingCollider = hit.collider.name; _walking.blockingLayer = hit.gameObject.layer;
            _walking.blockingPoint = hit.point; _walking.blockingNormal = hit.normal;
        }
        private void OnEnable() { if (_native != null) ApplyCursor(); }
        private void OnDisable() { _native?.SetInputBlocked(true); _looking = false; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
