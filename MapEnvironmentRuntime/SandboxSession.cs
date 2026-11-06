using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace MapEnvironmentRuntime
{
    public sealed class Change { public string key; public float value; }
    public sealed class ChangeRequest { public string expectedRevision; public Change[] changes; public TextSetting[] textChanges; }
    public sealed class SequenceRequest { public int frames,stride; }
    public sealed class CaptureSequenceRecord { public string id,revision,error; public bool complete; public CaptureRecord[] frames; }
    public sealed class SessionStatus
    {
        public SpecialMovementStatus specialMovement;
        public string revision, error, host, unity, graphics, latestCapture, cameraMode;
        public string inputCapture;
        public float hour, fps, frameMilliseconds;
        public int year, month, day;
        public float declination, cloudCoverage, cloudDensity;
        public string weather, nextWeather;
        public Vector3 cloudAdvection;
        public float cloudFormation;
        public Vector3 windVelocity;
        public string windState;
        public Vector3 sunDirection, moonDirection;
        public float moonIllumination;
        public float temperatureCelsius, temperatureMinimumCelsius, temperatureMaximumCelsius, cumulusMix, summerCloudFactor, cloudDevelopment;
        public bool clockPlaying;
        public Color nightAmbient;
        public int frame;
        public int skyHistoryFrames;
        public CloudLayerState[] cloudLayers;
        public double cloudSeconds;
        public string cloudMode,cloudQuality,cloudHistoryReset,cloudRenderError,captureSequence;
        public bool cloudHistoryValid,cloudShadowActive,captureSequenceRunning;
        public float cloudCpuMilliseconds;
        public int cloudWidth,cloudHeight;
        public double gpuFrameMilliseconds;
        public bool gpuFrameTimingAvailable;
        public IslandVisibilityStatus islandVisibility;
        public WalkingStatus walking;
        public DoorStatus doors;
        public AnimationApplicationStatus animationApplication;
        public NavigationStatus navigation;
    }
    public sealed class CaptureRecord
    {
        public string id, png, utc, revision;
        public Parameters parameters;
        public SessionStatus state;
        public Vector3 cameraPosition, cameraEuler;
        public int width, height;
        public string sequenceId;
        public int sequenceIndex;
    }
    public sealed class SessionConnection { public string endpoint, parameterPath, host; public int pid; }
    public sealed class SpecialMovementStatus
    {
        public string state,reason,commandName;
        public int? candidateId;
        public float distance;
        public Vector3 playerSourcePosition,commandSourcePosition;
    }

    public sealed class SandboxSession : MonoBehaviour, IWalkingSession, IWalkingInputNotifications
    {
        public EnvironmentWorld World { get; private set; }
        public Parameters Settings => World.Settings;
        public bool WalkingReady => World != null;
        public string Error { get; private set; }
        public bool Panel { get; private set; }
        private bool _externalInputBlocked;
        public event Action InputBlockChanged;
        public bool ExternalInputBlocked
        {
            get => _externalInputBlocked;
            set { if (_externalInputBlocked == value) return; _externalInputBlocked = value; InputBlockChanged?.Invoke(); }
        }
        public Func<SpecialMovementStatus> SpecialMovementSnapshot { get; set; }
        public Rect PanelRect => new Rect(Settings.N("ui.x"), Settings.N("ui.y") + Settings.N("ui.infoHeight"), Settings.N("ui.width"), Mathf.Min(Settings.N("ui.height"), Screen.height - Settings.N("ui.infoHeight") - Settings.N("ui.y")));
        public bool PointerOverPanel => Panel && PanelRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));
        public void TogglePanel() { if (Panel && _calendarPending) CommitCalendar(); Panel = !Panel; ReleaseKeyboardFocus(); PanelChanged?.Invoke(Panel); }
        public event Action<bool> PanelChanged;
        public bool EditingParameter { get; private set; }
        public IAnimationApplicationControls AnimationTrial { get; set; }
        public Action ModeMenuControls { get; set; }
        public Func<bool> ModeMenuOwnsWindow { get; set; }
        public NavigationWorld Navigation { get; set; }
        public Func<string> InputCaptureState { get; set; }
        public event Action<Parameters> ParametersApplied;
        private bool _clearKeyboardFocus;
        public void ReleaseKeyboardFocus() { EditingParameter = false; _clearKeyboardFocus = true; }
        private string _path, _share, _previous, _latest;
        private DateTime _lastWrite;
        private float _reloadAt, _statusAt;
        private HttpListener _listener;
        private Thread _thread;
        private readonly ConcurrentQueue<HttpListenerContext> _requests = new ConcurrentQueue<HttpListenerContext>();
        private readonly Dictionary<string, string> _inputs = new Dictionary<string, string>();
        private Vector2 _scroll;
        private enum SettingsPage { Quick, Characters, Capsules, Environment, Stage, Advanced }
        private bool _calendarPending;
        private int _pendingPage;
        private SettingsPage _drawPage;
        private int _detailPage, _detailCount;
        private string _detailFilter = "movement.";
        private NumberSetting[] _detailRows = Array.Empty<NumberSetting>();
        private SandboxHost _uiHost;
        private bool _standalone, _capturing;
        private string _hostName, _sessionFile, _statusFile;
        private SessionConnection _connection;
        private GUIStyle _label;
        private int _dateYearCenter;
        private CaptureSequenceRecord _sequence;
        private bool _sequenceRunning;
        public void Initialize(Transform map, string parameterPath, bool standalone)
            => Initialize(map, parameterPath, standalone, null);
        public void Initialize(Transform map, string parameterPath, bool standalone, string hostOverride)
            => Initialize(map, parameterPath, standalone, hostOverride, null);
        public void Initialize(Transform map, string parameterPath, bool standalone, string hostOverride, Parameters initialSettings)
        {
            _path = parameterPath; _standalone = standalone;
            _hostName = hostOverride ?? (standalone ? "Standalone" : "KKS");
            _uiHost = GetComponent<SandboxHost>();
            _pendingPage = (int)SettingsPage.Quick;
            _sessionFile = hostOverride != null ? "session-ai-mode.json" : standalone ? "session.json" : "session-kks.json";
            _statusFile = hostOverride != null ? "status-ai-mode.json" : standalone ? "status.json" : "status-kks.json";
            var parameters = initialSettings ?? Parameters.Load(_path);
            _share = parameters.T("share.directory"); Directory.CreateDirectory(_share);
            World = gameObject.AddComponent<EnvironmentWorld>();
            try { World.Initialize(map, parameters); }
            catch { DestroyImmediate(World); World = null; throw; }
            _lastWrite = File.GetLastWriteTimeUtc(_path);
            _dateYearCenter = parameters.I("calendar.year");
            StartApi();
            Debug.Log("MAP_SANDBOX_READY " + _connection.endpoint);
        }
        private void StartApi()
        {
            int port = Settings.I("api.port");
            if (port == default(int))
            {
                var probe = new TcpListener(IPAddress.Loopback, port); probe.Start();
                port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
            }
            _listener = new HttpListener();
            string endpoint = "http://" + Settings.T("api.host") + ":" + port.ToString(CultureInfo.InvariantCulture) + "/";
            _listener.Prefixes.Add(endpoint); _listener.Start();
            _connection = new SessionConnection { endpoint = endpoint, parameterPath = _path, host = _hostName, pid = System.Diagnostics.Process.GetCurrentProcess().Id };
            _thread = new Thread(() =>
            {
                try { while (_listener.IsListening) _requests.Enqueue(_listener.GetContext()); }
                catch (HttpListenerException) { }
                catch (ObjectDisposedException) { }
            }) { IsBackground = true, Name = "Map sandbox API" };
            _thread.Start();
            Parameters.AtomicWrite(Path.Combine(_share, _sessionFile), JsonCodec.ToJson(_connection, true));
        }
        private void Update()
        {
            if (World == null || Settings == null) return;
            if (Input.GetKeyDown((KeyCode)Enum.Parse(typeof(KeyCode), Settings.T("input.capture")))) RequestCapture();
            if (_standalone && !ExternalInputBlocked && Input.GetKeyDown((KeyCode)Enum.Parse(typeof(KeyCode), Settings.T("input.panel")))) TogglePanel();
            if (Time.unscaledTime >= _reloadAt)
            {
                _reloadAt = Time.unscaledTime + Settings.N("io.reloadSeconds");
                if (File.GetLastWriteTimeUtc(_path) != _lastWrite)
                {
                    try { Apply(Parameters.Load(_path), false); Error = null; }
                    catch (Exception ex) { Error = ex.Message; }
                    _lastWrite = File.GetLastWriteTimeUtc(_path);
                }
            }
            while (_requests.TryDequeue(out var context)) HandleRequest(context);
            if (Time.unscaledTime >= _statusAt)
            {
                _statusAt = Time.unscaledTime + Settings.N("io.statusSeconds");
                Parameters.AtomicWrite(Path.Combine(_share, _statusFile), JsonCodec.ToJson(Status(), true));
            }
        }
        public SessionStatus Status() => new SessionStatus
        {
            revision = Settings.revision, error = Error, hour = World.Hour,
            specialMovement=SpecialMovementSnapshot?.Invoke(),
            inputCapture = InputCaptureState?.Invoke(),
            islandVisibility = FindObjectOfType<SandboxIslandLoader>()?.Visibility?.Snapshot(),
            walking = FindObjectOfType<SandboxFreeCamera>()?.Snapshot(),
            doors = FindObjectOfType<SandboxIslandDoors>()?.Snapshot(),
            animationApplication = AnimationTrial?.Snapshot(),
            navigation = Navigation?.Status,
            year = World.Year, month = World.Month, day = World.Day, declination = World.Declination,
            weather = World.Weather, nextWeather = World.NextWeather, cloudCoverage = World.CloudCoverage, cloudDensity = World.CloudDensity,
            cloudAdvection = World.CloudAdvection, cloudFormation = World.CloudFormation,
            windVelocity = World.WindVelocity, windState = World.WindState,
            sunDirection = World.SunDirection, moonDirection = World.MoonDirection, moonIllumination = World.MoonIllumination,
            temperatureCelsius = World.Temperature, temperatureMinimumCelsius = World.TemperatureMinimum, temperatureMaximumCelsius = World.TemperatureMaximum,
            cumulusMix = World.CumulusMix, summerCloudFactor = World.SummerCloudFactor, cloudDevelopment = World.CloudDevelopment, clockPlaying = World.Playing,
            nightAmbient = World.NightAmbient,
            skyHistoryFrames = World.SkyHistoryFrames,
            cloudLayers=World.Clouds.Layers,cloudSeconds=World.Clouds.Seconds,cloudMode=Settings.T("cloud.mode"),cloudQuality=Settings.T("cloud.quality"),
            cloudHistoryReset=World.CloudRenderer?.ResetReason,cloudRenderError=World.CloudRenderer?.Error,
            cloudHistoryValid=World.CloudRenderer!=null && World.CloudRenderer.HistoryValid,cloudShadowActive=World.CloudRenderer!=null && World.CloudRenderer.ShadowActive,
            cloudCpuMilliseconds=World.CloudRenderer==null ? Settings.N("math.zero") : World.CloudRenderer.CpuMilliseconds,
            cloudWidth=World.CloudRenderer==null ? default(int) : World.CloudRenderer.CloudWidth,cloudHeight=World.CloudRenderer==null ? default(int) : World.CloudRenderer.CloudHeight,
            captureSequence=_sequence?.id,captureSequenceRunning=_sequenceRunning,
            gpuFrameMilliseconds=World.CloudRenderer==null ? Settings.N("math.zero") : World.CloudRenderer.GpuFrameMilliseconds,
            gpuFrameTimingAvailable=World.CloudRenderer!=null && World.CloudRenderer.GpuFrameTimingAvailable,
            fps = Settings.N("math.one") / Mathf.Max(Settings.N("render.epsilon"), Time.unscaledDeltaTime),
            frameMilliseconds = Time.unscaledDeltaTime * Settings.N("units.millisecondsPerSecond"),
            host = _hostName, frame = Time.frameCount,
            unity = Application.unityVersion, graphics = SystemInfo.graphicsDeviceName, latestCapture = _latest, cameraMode = Settings.T("movement.mode")
        };
        private void HandleRequest(HttpListenerContext context)
        {
            try
            {
                string route = context.Request.Url.AbsolutePath.Trim('/');
                string json;
                if (context.Request.HttpMethod == "GET" && route == "parameters") json = Settings.Json();
                else if (context.Request.HttpMethod == "GET" && route == "status") json = JsonCodec.ToJson(Status(), true);
                else if (context.Request.HttpMethod == "POST" && route == "capture")
                { RequestCapture(); json = JsonCodec.ToJson(Status(), true); }
                else if (context.Request.HttpMethod == "POST" && route == "bring-character" && _standalone)
                { FindObjectOfType<SandboxHost>().BringCharacterFront(); json = JsonCodec.ToJson(Status(), true); }
                else if(context.Request.HttpMethod=="POST" && route=="cloud-diagnostics")
                {
                    if(World.CloudRenderer==null) throw new InvalidOperationException("Cloud renderer is not attached");
                    string directory=Path.Combine(_share,"diagnostics-"+Guid.NewGuid().ToString("N"));
                    json=JsonCodec.ToJson(World.CloudRenderer.ExportDiagnostics(directory),true);Parameters.AtomicWrite(Path.Combine(directory,"diagnostics.json"),json);
                }
                else if(context.Request.HttpMethod=="POST" && route=="capture-sequence")
                {
                    string body;using(var reader=new StreamReader(context.Request.InputStream,Encoding.UTF8)) body=reader.ReadToEnd();
                    var request=JsonCodec.FromJson<SequenceRequest>(body);
                    if(request==null || request.frames<Settings.I("math.two") || request.frames>Settings.I("cloud.test.maximumSequenceFrames") || request.stride<Settings.I("math.one") || request.stride>Settings.I("cloud.test.maximumSequenceFrames")) throw new InvalidOperationException("Invalid capture sequence frames/stride");
                    if(_capturing) throw new InvalidOperationException("A capture is already running");
                    _sequence=new CaptureSequenceRecord{id="sequence-"+Guid.NewGuid().ToString("N"),revision=Settings.revision,frames=new CaptureRecord[request.frames]};
                    _capturing=true;_sequenceRunning=true;StartCoroutine(CaptureSequence(request));json=JsonCodec.ToJson(Status(),true);
                }
                else if (context.Request.HttpMethod == "POST" && route == "randomize-clouds")
                { RandomizeClouds(); json = Settings.Json(); }
                else if (context.Request.HttpMethod == "POST" && route == "toggle-camera-mode" && _standalone)
                { ToggleCameraMode(); if (Error != null) throw new InvalidOperationException(Error); json = Settings.Json(); }
                else if (context.Request.HttpMethod == "POST" && route == "toggle-clock" && _standalone)
                { ToggleClock(); if (Error != null) throw new InvalidOperationException(Error); json = Settings.Json(); }
                else if (context.Request.HttpMethod == "POST" && route == "restore")
                {
                    if (_previous == null) throw new InvalidOperationException("No previous configuration");
                    var restore = JsonCodec.FromJson<Parameters>(_previous); restore.revision = Guid.NewGuid().ToString("N"); Apply(restore, true); json = Settings.Json();
                }
                else if (context.Request.HttpMethod == "POST" && route == "parameters")
                {
                    string body; using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8)) body = reader.ReadToEnd();
                    var request = JsonCodec.FromJson<ChangeRequest>(body);
                    if (request == null || (request.changes == null && request.textChanges==null) || request.expectedRevision != Settings.revision) throw new InvalidOperationException("Stale revision or invalid changes");
                    var changed = Settings.Copy();
                    var keys = new HashSet<string>();
                    foreach (var change in request.changes ?? Array.Empty<Change>())
                    {
                        if (!keys.Add(change.key)) throw new InvalidOperationException("Duplicate change: " + change.key);
                        changed.numbers.Single(item => item.key == change.key).value = change.value;
                    }
                    foreach(var change in request.textChanges ?? Array.Empty<TextSetting>())
                    {
                        if(!keys.Add(change.key)) throw new InvalidOperationException("Duplicate change: "+change.key);
                        changed.text.Single(item=>item.key==change.key).value=change.value;
                    }
                    changed.revision = Guid.NewGuid().ToString("N"); Apply(changed, true); json = Settings.Json();
                }
                else throw new InvalidOperationException("Unsupported API request");
                Send(context, json, HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                try { Send(context, JsonCodec.ToJson(Status(), true), HttpStatusCode.BadRequest); } catch { }
            }
        }
        private static void Send(HttpListenerContext context, string json, HttpStatusCode status)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            context.Response.StatusCode = (int)status; context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = bytes.LongLength;
            using (var output = context.Response.OutputStream) output.Write(bytes, default(int), bytes.Length);
        }
        public void Apply(Parameters next, bool persist)
        {
            next.Validate();
            string before = Settings.Json();
            float hour = World.Hour;
            int year = World.Year, month = World.Month, day = World.Day;
            bool clockChanged = next.N("clock.hours") != Settings.N("clock.hours");
            bool dateChanged = next.N("calendar.year") != Settings.N("calendar.year") || next.N("calendar.month") != Settings.N("calendar.month") || next.N("calendar.day") != Settings.N("calendar.day");
            try
            {
                World.Apply(next);
                if (!dateChanged) World.SetDate(year, month, day, clockChanged ? next.N("clock.hours") : hour);
                if (persist)
                {
                    Parameters.AtomicWrite(Path.Combine(_share, "previous-parameters.json"), before);
                    Parameters.AtomicWrite(_path, next.Json()); _lastWrite = File.GetLastWriteTimeUtc(_path);
                }
                _previous = before; _inputs.Clear(); Error = null;
                if (_standalone && _hostName != "KKS AI Mode") SandboxHost.ApplyDisplay(next);
                ParametersApplied?.Invoke(next);
            }
            catch
            {
                World.Apply(JsonCodec.FromJson<Parameters>(before)); World.SetDate(year, month, day, hour);
                throw;
            }
        }
        public void RequestCapture()
        {
            if (!_capturing) StartCoroutine(Capture());
        }
        private void RandomizeClouds()
        {
            var next = Settings.Copy(); var random = new System.Random();
            foreach (var item in next.numbers)
                if (item.randomize) item.value = Mathf.Lerp(item.randomMin, item.randomMax, (float)random.NextDouble());
            next.revision = Guid.NewGuid().ToString("N"); Apply(next, true);
        }
        public void ToggleCameraMode()
        {
            try
            {
                var next = Settings.Copy();
                next.text.Single(item => item.key == "movement.mode").value = Settings.T("movement.mode") == "free" ? "walk" : "free";
                next.revision = Guid.NewGuid().ToString("N"); Apply(next, true);
            }
            catch (Exception ex) { Error = ex.Message; }
        }
        public void ToggleClock()
        {
            try
            {
                var next = Settings.Copy(); next.text.Single(item => item.key == "clock.playing").value = (!World.Playing).ToString().ToLowerInvariant();
                next.revision = Guid.NewGuid().ToString("N"); Apply(next, true);
            }
            catch (Exception ex) { Error = ex.Message; }
        }
        private IEnumerator Capture()
        {
            _capturing = true;
            yield return new WaitForEndOfFrame();
            Texture2D image = null;
            try
            {
                string id = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ", CultureInfo.InvariantCulture);
                string filename = id + ".png";
                image = ScreenCapture.CaptureScreenshotAsTexture(Settings.I("capture.superSize"));
                byte[] png = ImageConversion.EncodeToPNG(image);
                var camera = Camera.main;
                var record = new CaptureRecord { id = id, png = filename, utc = DateTime.UtcNow.ToString("o"), revision = Settings.revision,
                    parameters = Settings.Copy(), state = Status(), width = image.width, height = image.height,
                    cameraPosition = camera != null ? camera.transform.position : default(Vector3), cameraEuler = camera != null ? camera.transform.eulerAngles : default(Vector3) };
                string temporary = Path.Combine(_share, filename + ".pending");
                File.WriteAllBytes(temporary, png); File.Move(temporary, Path.Combine(_share, filename));
                string metadata = JsonCodec.ToJson(record, true);
                Parameters.AtomicWrite(Path.Combine(_share, id + ".json"), metadata);
                Parameters.AtomicWrite(Path.Combine(_share, "latest.json"), metadata);
                _latest = id;
            }
            catch (Exception ex) { Error = "Capture: " + ex.Message; }
            finally { if (image != null) Destroy(image); _capturing = false; }
        }
        private IEnumerator CaptureSequence(SequenceRequest request)
        {
            for(int index=Settings.I("math.zero");index<request.frames;index++)
            {
                for(int skip=Settings.I("math.zero");skip<request.stride;skip++) yield return new WaitForEndOfFrame();
                Texture2D image=null;
                try
                {
                    if(Settings.revision!=_sequence.revision) throw new InvalidOperationException("Settings changed during capture sequence");
                    string id=_sequence.id+"-"+index.ToString(CultureInfo.InvariantCulture),filename=id+".png";
                    image=ScreenCapture.CaptureScreenshotAsTexture(Settings.I("capture.superSize"));
                    var camera=Camera.main;
                    var record=new CaptureRecord{id=id,png=filename,utc=DateTime.UtcNow.ToString("o"),revision=Settings.revision,parameters=Settings.Copy(),state=Status(),
                        width=image.width,height=image.height,cameraPosition=camera!=null ? camera.transform.position : default(Vector3),cameraEuler=camera!=null ? camera.transform.eulerAngles : default(Vector3),sequenceId=_sequence.id,sequenceIndex=index};
                    record=JsonCodec.FromJson<CaptureRecord>(JsonCodec.ToJson(record));
                    string pending=Path.Combine(_share,filename+".pending");File.WriteAllBytes(pending,ImageConversion.EncodeToPNG(image));File.Move(pending,Path.Combine(_share,filename));
                    Parameters.AtomicWrite(Path.Combine(_share,id+".json"),JsonCodec.ToJson(record,true));
                    _sequence.frames[index]=record;
                }
                catch(Exception ex) { _sequence.error=ex.Message;Error="Capture sequence: "+ex.Message; }
                finally { if(image!=null) Destroy(image); }
                if(_sequence.error!=null) break;
            }
            _sequence.complete=_sequence.error==null;
            string json=JsonCodec.ToJson(_sequence,true);
            Parameters.AtomicWrite(Path.Combine(_share,_sequence.id+".json"),json);
            if(_sequence.complete) Parameters.AtomicWrite(Path.Combine(_share,"latest-sequence.json"),json);
            _capturing=false;_sequenceRunning=false;
        }
        private void OnGUI()
        {
            if (!_standalone || World == null || (_capturing && Settings.B("capture.hidePanel"))) return;
            if (Event.current.type == EventType.Layout) _drawPage = (SettingsPage)_pendingPage;
            if (_clearKeyboardFocus) { GUI.FocusControl(null); _clearKeyboardFocus = false; }
            EditingParameter = Panel && GUI.GetNameOfFocusedControl().StartsWith("parameter:", StringComparison.Ordinal);
            if (_label == null) _label = new GUIStyle(GUI.skin.label);
            _label.fontSize = Settings.I("ui.fontSize");
            GUI.Label(new Rect(Settings.N("ui.x"), Settings.N("ui.y"), Settings.N("ui.width"), Settings.N("ui.infoHeight")),
                (_hostName == "KKS AI Mode" ? "AI少女モード | " + Settings.T("aiMode.returnKey") + " タイトルへ | " : "KKS Map Sandbox | ") + Settings.T("input.panel") + " 設定  " + Settings.T("input.capture") + " 撮影  " + Settings.T("input.cameraMode") + " 歩行/フリーカメラ\n" +
                (Settings.T("movement.mode") == "walk" ? "歩行：マウス視点・WASD移動・" + Settings.T("input.crouch") + "しゃがみ・" + Settings.T("input.jump") + "ジャンプ・" + Settings.T("input.cursorRelease") + "カーソル解除/固定" : "フリーカメラ：右ドラッグ視点・WASD移動・QE上下・Shift高速") + "\n" +
                World.Year + "/" + World.Month + "/" + World.Day + " " + World.Hour.ToString("F2") + "時  " + WeatherLabel(World.Weather) + " → " + WeatherLabel(World.NextWeather) + "  FPS " + (Settings.N("math.one") / Mathf.Max(Settings.N("render.epsilon"), Time.unscaledDeltaTime)).ToString("F1") + "\n" +
                "気温 " + World.Temperature.ToString("F1") + "℃  当日最低 " + World.TemperatureMinimum.ToString("F1") + "℃ / 最高 " + World.TemperatureMaximum.ToString("F1") + "℃\n" + Error, _label);
            if (!Panel) return;
            GUI.Window(Settings.I("ui.windowId"), PanelRect, DrawWindow, "モード設定");
        }
        private void DrawWindow(int id)
        {
            ModeMenuControls?.Invoke();
            if(ModeMenuOwnsWindow?.Invoke()==true)return;
            _pendingPage = GUILayout.Toolbar(_pendingPage, new[] { "よく使う", "女の子", "カプセル", "日時・空", "島の配置", "詳細設定" });
            _scroll = GUILayout.BeginScrollView(_scroll);
            try
            {
                if (_drawPage == SettingsPage.Quick)
                {
                    DrawDateSliders(); AnimationTrial?.DrawQuickControls();
                    if (GUILayout.Button(Settings.T("movement.mode") == "free" ? "歩行へ切り替え" : "フリーカメラへ切り替え")) ToggleCameraMode();
                    GUILayout.Label(Error ?? ""); return;
                }
                if (_drawPage == SettingsPage.Capsules) { AnimationTrial?.DrawCapsuleControls(); return; }
                if (_drawPage == SettingsPage.Stage) { DrawStageControls(); return; }
                if (_drawPage == SettingsPage.Advanced) { DrawAdvancedSettings(); return; }
                if (_drawPage == SettingsPage.Characters)
                {
                    AnimationTrial?.DrawControls();
                    if (_uiHost?.HasReferenceCharacter == true)
                    {
                        if (GUILayout.Button("ルナを目の前へ")) { try { _uiHost.BringCharacterFront(); } catch (Exception ex) { Error = ex.Message; } }
                        var distanceSetting = Settings.numbers.Single(item => item.key == "character.distance");
                        GUILayout.Label("ルナを置く距離：" + distanceSetting.value.ToString("F1") + " m");
                        float distance = GUILayout.HorizontalSlider(distanceSetting.value, distanceSetting.min, distanceSetting.max);
                        if (distance != distanceSetting.value)
                        {
                            try { var next = Settings.Copy(); next.numbers.Single(item => item.key == "character.distance").value = distance; next.revision = Guid.NewGuid().ToString("N"); Apply(next, true); }
                            catch (Exception ex) { Error = ex.Message; }
                        }
                    }
                    if (GUILayout.Button(Settings.T("movement.mode") == "free" ? "歩行へ切り替え" : "フリーカメラへ切り替え")) ToggleCameraMode();
                    return;
                }
                DrawDateSliders(true);
                DrawCloudControls();
                GUILayout.Label("気温 " + World.Temperature.ToString("F1") + "℃　最低 " + World.TemperatureMinimum.ToString("F1") + "℃ / 最高 " + World.TemperatureMaximum.ToString("F1") + "℃");
                if (GUILayout.Button(Settings.T("movement.mode") == "free" ? "歩行へ切り替え" : "フリーカメラへ切り替え")) ToggleCameraMode();
                if (GUILayout.Button("雲をランダム生成（配置・偏り・形・濃さ）"))
                {
                    try { RandomizeClouds(); }
                    catch (Exception ex) { Error = ex.Message; }
                }
                GUILayout.Label(Error ?? "");
            }
            finally { GUILayout.EndScrollView(); }
        }
        private void DrawAdvancedSettings()
        {
                GUILayout.Label("分類・項目名で絞り込み（例：movement. / sky. / cloud.）");
                GUI.SetNextControlName("parameter:filter");
                string filter = GUILayout.TextField(_detailFilter);
                if (filter != _detailFilter) { _detailFilter = filter; _detailPage = Settings.I("math.zero"); }
                if (Event.current.type == EventType.Layout)
                {
                    var selected = Settings.numbers.Where(item => item.key.IndexOf(_detailFilter ?? "", StringComparison.OrdinalIgnoreCase) >= Settings.I("math.zero")).ToArray();
                    _detailCount = selected.Length;
                    _detailRows = selected.Skip(_detailPage * Settings.I("ui.parametersPerPage")).Take(Settings.I("ui.parametersPerPage")).ToArray();
                }
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("前のページ")) _detailPage = Mathf.Max(Settings.I("math.zero"), _detailPage - Settings.I("math.one"));
                GUILayout.Label((_detailPage + Settings.I("math.one")) + "ページ / " + _detailCount + "項目");
                if (GUILayout.Button("次のページ") && (_detailPage + Settings.I("math.one")) * Settings.I("ui.parametersPerPage") < _detailCount) _detailPage += Settings.I("math.one");
                GUILayout.EndHorizontal();
                foreach (var item in _detailRows)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(new GUIContent(item.key, item.description + " [" + item.min + " .. " + item.max + "] " + item.unit), GUILayout.Width(Settings.N("ui.labelWidth")));
                    if (!_inputs.TryGetValue(item.key, out string input)) input = item.value.ToString("R", CultureInfo.InvariantCulture);
                    GUI.SetNextControlName("parameter:" + item.key);
                    _inputs[item.key] = GUILayout.TextField(input, GUILayout.Width(Settings.N("ui.valueWidth")));
                    GUILayout.EndHorizontal();
                }
                if (GUILayout.Button("変更を反映・保存"))
                {
                    try
                    {
                        var next = Settings.Copy();
                        foreach (var number in next.numbers) if (_inputs.TryGetValue(number.key, out string input)) number.value = Parameters.ParseNumber(input);
                        next.revision = Guid.NewGuid().ToString("N"); Apply(next, true);
                    }
                    catch (Exception ex) { Error = ex.Message; }
                }
                GUILayout.Label(Error ?? "");
        }
        private static string WeatherLabel(string key) => key == "clear" ? "晴天" : key == "cloudy" ? "曇" : "時々曇";
        private readonly Dictionary<string, string> _stageDraft = new Dictionary<string, string>();
        private string _stageRevision;
        private void DrawStageControls()
        {
            if (Settings.T("sandbox.map") != "ai-island") return;
            string[] keys = { "island.stage.position.x", "island.stage.position.y", "island.stage.position.z", "island.stage.yaw" };
            if (_stageRevision != Settings.revision)
            {
                _stageDraft.Clear(); foreach (string key in keys) _stageDraft[key] = Settings.N(key).ToString("R", CultureInfo.InvariantCulture);
                _stageRevision = Settings.revision;
            }
            GUILayout.Label("島全体の配置（カメラ位置は固定）");
            foreach (string key in keys)
            {
                var item = Settings.numbers.Single(setting => setting.key == key);
                GUILayout.Label(new GUIContent(key + " [" + item.min + "～" + item.max + "] " + item.unit, item.description));
                GUI.SetNextControlName("parameter:" + key);
                _stageDraft[key] = GUILayout.TextField(_stageDraft[key]);
                if (float.TryParse(_stageDraft[key], NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                {
                    float selected = GUILayout.HorizontalSlider(value, item.min, item.max);
                    if (selected != value) _stageDraft[key] = selected.ToString("R", CultureInfo.InvariantCulture);
                }
                else GUILayout.HorizontalSlider(item.value, item.min, item.max);
            }
            GUILayout.Label("X・Y・Zはm、方位角は度。例：Y=1で1m上、方位角90で直角。回転は地形を再計算するため反映時に待つ場合があります。");
            if (GUILayout.Button("配置を反映・保存"))
            {
                try
                {
                    var next = Settings.Copy(); foreach (string key in keys) next.numbers.Single(item => item.key == key).value = Parameters.ParseNumber(_stageDraft[key]);
                    next.revision = Guid.NewGuid().ToString("N"); Apply(next, true);
                }
                catch (Exception ex) { Error = ex.Message; }
            }
            if (GUILayout.Button("配置を初期値へ戻して保存"))
            {
                try { var next = Settings.Copy(); foreach (string key in keys) { var item = next.numbers.Single(setting => setting.key == key); item.value = item.initial; } next.revision = Guid.NewGuid().ToString("N"); Apply(next, true); }
                catch (Exception ex) { Error = ex.Message; }
            }
        }
        private void SetText(string key,string value)
        {
            try {var next=Settings.Copy();next.text.Single(item=>item.key==key).value=value;next.revision=Guid.NewGuid().ToString("N");Apply(next,true);}
            catch(Exception ex) {Error=ex.Message;}
        }
        private void Choices(string label,string key,string[] values,string[] labels)
        {
            GUILayout.Label(label+"："+Settings.T(key));GUILayout.BeginHorizontal();
            for(int index=Settings.I("math.zero");index<values.Length;index++) if(GUILayout.Button(labels[index])) SetText(key,values[index]);
            GUILayout.EndHorizontal();
        }
        private void DrawCloudControls()
        {
            Choices("雲の生成","cloud.mode",new[]{"automatic","season","type"},new[]{"年月日から自動","季節固定","雲種固定"});
            Choices("固定する季節","cloud.fixedSeason",new[]{"spring","summer","autumn","winter"},new[]{"春","夏","秋","冬"});
            Choices("固定する雲","cloud.fixedType",Settings.cloudTypes,new[]{"積雲","層積雲","ひつじ雲","うろこ雲","筋雲","中層薄雲","上層薄雲"});
            Choices("雲の画質","cloud.quality",new[]{"low","standard","high"},new[]{"低","標準","高"});
            Choices("描画診断","cloud.debug",new[]{"normal","static","passthrough","single"},new[]{"通常","固定描画点","雲なし入出力確認","積雲1個"});
            GUILayout.Label("雲の数値はスライダーで即時保存。単位と範囲は各項目の説明に表示します。");
            foreach(var item in Settings.numbers.Where(item=>item.key.StartsWith("cloud.") && item.min<item.max && !item.key.StartsWith("cloud.test.") && !item.key.StartsWith("cloud.noise.baseResolution") && !item.key.StartsWith("cloud.noise.detailResolution")))
            {
                GUILayout.Label(new GUIContent(item.key+"："+item.value.ToString("G4")+" "+item.unit,item.description+" ["+item.min+" .. "+item.max+"]"));
                float value=GUILayout.HorizontalSlider(item.value,item.min,item.max);
                if(value!=item.value)
                {
                    if(item.key=="cloud.steps" || item.key=="cloud.shadow.steps" || item.key=="cloud.sampling.phaseCycle" || item.key=="cloud.seedWrap") value=Mathf.Round(value);
                    if(item.key=="cloud.shadow.resolution") value=Mathf.ClosestPowerOfTwo(Mathf.RoundToInt(value));
                    try {var next=Settings.Copy();next.numbers.Single(number=>number.key==item.key).value=value;next.revision=Guid.NewGuid().ToString("N");Apply(next,true);}
                    catch(Exception ex){Error=ex.Message;}
                }
            }
        }
        public void DrawEnvironmentSettings()
        {
            if(World==null) return;
            DrawDateSliders();DrawCloudControls();GUILayout.Label(Error??"");
        }
        private float DateSlider(string label, float value, float minimum, float maximum)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + "：" + value.ToString("F0"), GUILayout.Width(Settings.N("ui.valueWidth")));
            float result = Mathf.Round(GUILayout.HorizontalSlider(value, minimum, maximum, GUILayout.Width(Settings.N("ui.dateSliderWidth"))));
            GUILayout.EndHorizontal(); return result;
        }
        private void DrawDateSliders(bool showYear = false)
        {
            int year = showYear ? checked((int)DateSlider("年", World.Year, Mathf.Max(Settings.N("math.one"), _dateYearCenter - Settings.N("calendar.yearSliderPast")), Mathf.Min(Settings.N("calendar.maxYear"), _dateYearCenter + Settings.N("calendar.yearSliderFuture")))) : World.Year;
            int month = World.Month, day = Mathf.Min(World.Day, CalendarClock.DaysInMonth(year, World.Month, Settings));
            int dayOfYear = CalendarClock.DayOfYear(year, month, day, Settings);
            GUILayout.Label("一年：" + year + "/" + month + "/" + day + "　1月1日 ～ 12月31日");
            float selectedDay = CalendarSlider(dayOfYear, Settings.N("math.one"), CalendarClock.DaysInYear(year, Settings), out bool dateChanged);
            int remaining = Mathf.RoundToInt(selectedDay); month = Settings.I("math.one");
            while (month < Settings.I("calendar.months") && remaining > CalendarClock.DaysInMonth(year, month, Settings)) { remaining -= CalendarClock.DaysInMonth(year, month, Settings); month += Settings.I("math.one"); }
            day = remaining;
            int hour = Mathf.FloorToInt(World.Hour), minute = Mathf.FloorToInt((World.Hour - hour) * Settings.N("calendar.minutesPerHour"));
            GUILayout.Label("24時間：" + hour.ToString("D2") + ":" + minute.ToString("D2") + "　00:00 ～ 24:00");
            float selectedHour = CalendarSlider(World.Hour, Settings.N("math.zero"), Settings.N("clock.hoursPerDay"), out bool timeChanged);
            if (dateChanged || timeChanged || year != World.Year)
            {
                try { selectedHour = Mathf.Min(Mathf.Round(selectedHour * Settings.N("calendar.minutesPerHour")) / Settings.N("calendar.minutesPerHour"), Settings.N("clock.hoursPerDay") - Settings.N("math.one") / Settings.N("calendar.minutesPerHour")); World.SetDate(year, month, day, selectedHour); _calendarPending = true; }
                catch (Exception ex) { Error = ex.Message; }
            }
            if (_calendarPending && Event.current.rawType == EventType.MouseUp) CommitCalendar();
            if (GUILayout.Button(World.Playing ? "時間の進行を停止" : "時間の進行を再開")) ToggleClock();
        }
        private float CalendarSlider(float value, float minimum, float maximum, out bool changed)
        {
            bool before = GUI.changed; GUI.changed = false;
            float result = GUILayout.HorizontalSlider(value, minimum, maximum);
            changed = GUI.changed; GUI.changed |= before; return result;
        }
        private void CommitCalendar()
        {
            try
            {
                var next = Settings.Copy();
                next.numbers.Single(item => item.key == "calendar.year").value = World.Year;
                next.numbers.Single(item => item.key == "calendar.month").value = World.Month;
                next.numbers.Single(item => item.key == "calendar.day").value = World.Day;
                next.numbers.Single(item => item.key == "clock.hours").value = World.Hour;
                next.revision = Guid.NewGuid().ToString("N"); Apply(next, true); _calendarPending = false;
            }
            catch (Exception ex) { Error = ex.Message; }
        }
        private void OnDestroy()
        {
            _listener?.Close();
            if (_thread != null && Settings != null) _thread.Join(Settings.I("api.joinMilliseconds"));
            if (_connection != null)
            {
                string file = Path.Combine(_share, _sessionFile);
                if (File.Exists(file) && File.ReadAllText(file).Contains(_connection.endpoint)) File.Delete(file);
            }
        }
    }
}
