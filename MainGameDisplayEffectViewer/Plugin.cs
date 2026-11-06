using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ActionGame;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace MainGameDisplayEffectViewer
{
    [BepInDependency("canon.gate", "2.3.0")]
    [BepInPlugin(Guid, PluginName, Version)]
    [BepInProcess("KoikatsuSunshine")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.kks.main.displayeffectviewer";
        public const string PluginName = "MainGameDisplayEffectViewer";
        public const string Version = "1.0.2";

        private sealed class EffectEntry
        {
            public int Id;
            public string Category = string.Empty;
            public string Name = string.Empty;
            public string Path = string.Empty;
            public string Detail = string.Empty;

            public GameObject Go;
            public Canvas Canvas;
            public CanvasGroup CanvasGroup;
            public Animator Animator;
            public ParticleSystem Particle;

            public bool IsAlive => Go != null;
            public bool IsActiveInHierarchy => Go != null && Go.activeInHierarchy;
            public bool IsActiveSelf => Go != null && Go.activeSelf;
        }

        private sealed class EntrySnapshot
        {
            public bool HasGo;
            public bool GoActiveSelf;

            public bool HasCanvas;
            public bool CanvasEnabled;
            public int CanvasSortingOrder;

            public bool HasCanvasGroup;
            public float CanvasGroupAlpha;

            public bool HasAnimator;
            public bool AnimatorEnabled;

            public bool HasParticle;
            public bool ParticleWasPlaying;
        }

        private string _pluginDir = string.Empty;
        private MainGameDisplayEffectViewerSettings _settings;
        private SimpleFileLogger _fileLogger;

        private ActionScene _actionScene;
        private int _actionSceneInstanceId;



        private ConfigEntry<bool> _showWindowConfig;
        private ConfigEntry<bool> _safeModeConfig;

        private bool _windowVisible;
        private bool _safeMode = true;
        private Rect _windowRect = new Rect(28f, 28f, 1360f, 760f);
        private Vector2 _scroll;

        private string _search = string.Empty;
        private bool _showOnlyActive;
        private bool _infoOnly;

        private bool _showCanvas = true;
        private bool _showCanvasGroup = true;
        private bool _showAnimator = true;
        private bool _showParticle = true;
        private bool _showMarker = true;

        private int _maxRows = 300;
        private string _lastError = string.Empty;

        private readonly List<EffectEntry> _entries = new List<EffectEntry>(256);
        private readonly Dictionary<int, EntrySnapshot> _allPlaySnapshot = new Dictionary<int, EntrySnapshot>(256);
        private int _entrySeq = 1;
        private bool _hasAllPlaySnapshot;
        private int _lastAllPlayTargetCount;

        private static readonly string[] MarkerKeywords =
        {
            "information",
            "topic",
            "popup",
            "notice",
            "infomation" // game asset typo
        };

        private void Awake()
        {
            _pluginDir = Path.GetDirectoryName(Info.Location) ?? string.Empty;

            _logEnabled = Config.Bind("Logging", "LogEnabled", false,
                "通常ログの出力。OFFでは起動ログ・専用ファイルの作成も停止。変更は即時反映。");

            _fileLogger = new SimpleFileLogger();

            _settings = SettingsStore.LoadOrCreate(_pluginDir, RawInfo, RawWarn);
            _logEnabled.SettingChanged += (_, __) => ApplyLogging();
            ApplyLogging();
            _fileLogger.SetAutoFlush(_settings.AutoFlushFileLog);

            _windowVisible = _settings.StartWindowVisible;
            _showOnlyActive = _settings.ShowOnlyActiveByDefault;
            _infoOnly = _settings.InfoOnlyByDefault;
            _search = _settings.InitialSearch ?? string.Empty;
            _maxRows = _settings.MaxRows;
            _showWindowConfig = Config.Bind(
                "UI",
                "ShowWindow",
                _windowVisible,
                "OnGUI window visible state.");
            _windowVisible = _showWindowConfig.Value;

            _safeModeConfig = Config.Bind(
                "UI",
                "SafeMode",
                true,
                "Prevent destructive hide operations in row controls.");
            _safeMode = _safeModeConfig.Value;

            RawInfo("loaded");
            RawInfo("settings=" + Path.Combine(_pluginDir, SettingsStore.FileName));
            RawInfo("config.UI.ShowWindow=" + _windowVisible);
            RawInfo("config.UI.SafeMode=" + _safeMode);
            CanonPlugins.Shared.PluginActivityGate.Bind(this, Guid, CanonGate.GateApi.ActivityKind.ActionScene,
                () => { if (_actionScene != null) RebuildEntries("scene-start"); }, () => { if (_hasAllPlaySnapshot) ExecuteResetAll(); ClearRuntimeContext(); });

            UnityEngine.SceneManagement.SceneManager.sceneLoaded += ActivitySceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += ActivitySceneUnloaded;
            ActivitySceneLoaded(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), UnityEngine.SceneManagement.LoadSceneMode.Single);
        }

        private void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= ActivitySceneLoaded; UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= ActivitySceneUnloaded;
            CanonPlugins.Shared.PluginActivityGate.Dispose();
            LogInfo("destroyed");
            _fileLogger?.Dispose();
            _fileLogger = null;
        }

        private void Update()
        {
            // Refresh is explicit from the window; no scene or interval polling.
        }

        private void OnGUI()
        {
            if (!CanonPlugins.Shared.PluginActivityGate.Allowed) return;
            if (_settings == null || !_settings.EnablePlugin || !_windowVisible)
                return;

            GUI.depth = 0;
            _windowRect = GUILayout.Window(724198, _windowRect, DrawWindow, PluginName + " v" + Version);
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginVertical();

            GUILayout.Label("Config(UI.ShowWindow): " + _windowVisible + " | ActionScene: " + GetActionSceneStatus());
            GUILayout.Label("Entries: " + _entries.Count + " | MaxRows: " + _maxRows + " | SafeMode: " + (_safeMode ? "ON" : "OFF"));
            GUILayout.Label(BuildGuideText());

            if (!string.IsNullOrEmpty(_lastError))
                GUILayout.Label("ERROR: " + _lastError);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Width(120)))
                RebuildEntries("manual-refresh");
            if (GUILayout.Button("SetTopic", GUILayout.Width(120)))
                TriggerSetTopic();
            if (GUILayout.Button("SetTopicFull", GUILayout.Width(120)))
                TriggerSetTopicFull();
            if (GUILayout.Button("SetNormal", GUILayout.Width(120)))
                TriggerSetNormal();
            if (GUILayout.Button("SetTopicMode", GUILayout.Width(120)))
                TriggerSetTopicMode();
            if (GUILayout.Button("All Play(filtered)", GUILayout.Width(140)))
                ExecuteAllPlayForFilteredEntries();
            if (GUILayout.Button("Reset All", GUILayout.Width(110)))
                ExecuteResetAll();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            bool safeModeNext = GUILayout.Toggle(_safeMode, "SafeMode(no Hide GO)", GUILayout.Width(180));
            if (safeModeNext != _safeMode)
            {
                _safeMode = safeModeNext;
                if (_safeModeConfig != null)
                    _safeModeConfig.Value = _safeMode;
            }
            if (GUILayout.Button("Preset: InfoUI", GUILayout.Width(125)))
                ApplyPresetInfoUi();
            if (GUILayout.Button("Preset: Particle", GUILayout.Width(130)))
                ApplyPresetParticleOnly();
            if (GUILayout.Button("Preset: Full", GUILayout.Width(110)))
                ApplyPresetFull();
            GUILayout.Label("Snapshot: " + (_hasAllPlaySnapshot ? ("yes(" + _lastAllPlayTargetCount + ")") : "none"), GUILayout.Width(130));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", GUILayout.Width(52));
            _search = GUILayout.TextField(_search ?? string.Empty, GUILayout.Width(320));
            _showOnlyActive = GUILayout.Toggle(_showOnlyActive, "OnlyActive", GUILayout.Width(95));
            _infoOnly = GUILayout.Toggle(_infoOnly, "InfoOnly", GUILayout.Width(80));
            GUILayout.Label("Rows", GUILayout.Width(35));
            string rowsText = GUILayout.TextField(_maxRows.ToString(), GUILayout.Width(68));
            if (int.TryParse(rowsText, out int parsedRows))
                _maxRows = Mathf.Clamp(parsedRows, 50, 2000);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            _showCanvas = GUILayout.Toggle(_showCanvas, "Canvas", GUILayout.Width(80));
            _showCanvasGroup = GUILayout.Toggle(_showCanvasGroup, "CanvasGroup", GUILayout.Width(105));
            _showAnimator = GUILayout.Toggle(_showAnimator, "Animator", GUILayout.Width(85));
            _showParticle = GUILayout.Toggle(_showParticle, "Particle", GUILayout.Width(82));
            _showMarker = GUILayout.Toggle(_showMarker, "Marker", GUILayout.Width(80));
            GUILayout.EndHorizontal();

            List<EffectEntry> filteredEntries = CollectFilteredEntries();
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

            int shown = 0;
            foreach (EffectEntry entry in filteredEntries)
            {
                if (shown >= _maxRows)
                    break;

                DrawEntryRow(entry);
                shown++;
            }

            GUILayout.EndScrollView();

            GUILayout.Label("Shown: " + Mathf.Min(_maxRows, filteredEntries.Count));

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private void DrawEntryRow(EffectEntry entry)
        {
            GUILayout.BeginHorizontal("box");

            GUILayout.Label("#" + entry.Id, GUILayout.Width(44));
            GUILayout.Label(entry.Category, GUILayout.Width(84));
            GUILayout.Label(entry.IsActiveInHierarchy ? "ACTIVE" : "INACTIVE", GUILayout.Width(72));
            GUILayout.Label(entry.Name, GUILayout.Width(220));
            GUILayout.Label(entry.Path, GUILayout.Width(520));

            if (entry.IsActiveSelf)
            {
                bool canHide = !_safeMode;
                if (!canHide)
                    GUI.enabled = false;
                if (GUILayout.Button(canHide ? "Hide GO" : "Hide GO(Lock)", GUILayout.Width(90)) && canHide)
                    ToggleGameObjectActive(entry.Go);
                if (!canHide)
                    GUI.enabled = true;
            }
            else
            {
                if (GUILayout.Button("Show GO", GUILayout.Width(90)))
                    ToggleGameObjectActive(entry.Go);
            }

            if (entry.Canvas != null)
            {
                if (GUILayout.Button(entry.Canvas.enabled ? "Canvas Off" : "Canvas On", GUILayout.Width(80)))
                    entry.Canvas.enabled = !entry.Canvas.enabled;
                GUILayout.Label("order=" + entry.Canvas.sortingOrder, GUILayout.Width(70));
                if (GUILayout.Button("+", GUILayout.Width(24))) entry.Canvas.sortingOrder += 1;
                if (GUILayout.Button("-", GUILayout.Width(24))) entry.Canvas.sortingOrder -= 1;
            }
            else if (entry.CanvasGroup != null)
            {
                if (GUILayout.Button("Alpha 0", GUILayout.Width(60))) entry.CanvasGroup.alpha = 0f;
                if (GUILayout.Button("Alpha 1", GUILayout.Width(60))) entry.CanvasGroup.alpha = 1f;
                GUILayout.Label("a=" + entry.CanvasGroup.alpha.ToString("0.00"), GUILayout.Width(62));
            }
            else if (entry.Animator != null)
            {
                if (GUILayout.Button("Play", GUILayout.Width(52))) ReplayAnimator(entry.Animator);
                if (GUILayout.Button(entry.Animator.enabled ? "Anim Off" : "Anim On", GUILayout.Width(72)))
                    entry.Animator.enabled = !entry.Animator.enabled;
            }
            else if (entry.Particle != null)
            {
                if (GUILayout.Button("Play", GUILayout.Width(52))) entry.Particle.Play(true);
                if (GUILayout.Button("Stop", GUILayout.Width(52))) entry.Particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (GUILayout.Button("Clear", GUILayout.Width(52))) entry.Particle.Clear(true);
            }
            else
            {
                GUILayout.Label(entry.Detail, GUILayout.Width(170));
            }

            if (GUILayout.Button("Log", GUILayout.Width(44)))
                LogInfo("entry=" + entry.Id + " category=" + entry.Category + " path=" + entry.Path + " detail=" + entry.Detail);

            GUILayout.EndHorizontal();
        }

        private string BuildGuideText()
        {
            if (_hasAllPlaySnapshot)
                return "Guide: SetTopic -> All Play(filtered) -> check -> Reset All";
            return "Guide: Preset:InfoUI -> SetTopic -> All Play(filtered)";
        }

        private List<EffectEntry> CollectFilteredEntries()
        {
            var list = new List<EffectEntry>(Mathf.Min(_entries.Count, 512));
            foreach (EffectEntry e in FilterEntries())
                list.Add(e);
            return list;
        }

        private void ApplyPresetInfoUi()
        {
            _showOnlyActive = true;
            _infoOnly = true;
            _showCanvas = false;
            _showCanvasGroup = true;
            _showAnimator = true;
            _showParticle = true;
            _showMarker = false;
            _search = "info";
            _maxRows = Mathf.Clamp(60, 50, 2000);
            LogInfo("[PRESET] InfoUI");
        }

        private void ApplyPresetParticleOnly()
        {
            _showOnlyActive = true;
            _infoOnly = false;
            _showCanvas = false;
            _showCanvasGroup = false;
            _showAnimator = false;
            _showParticle = true;
            _showMarker = false;
            _search = string.Empty;
            _maxRows = Mathf.Clamp(120, 50, 2000);
            LogInfo("[PRESET] ParticleOnly");
        }

        private void ApplyPresetFull()
        {
            _showOnlyActive = false;
            _infoOnly = false;
            _showCanvas = true;
            _showCanvasGroup = true;
            _showAnimator = true;
            _showParticle = true;
            _showMarker = true;
            _search = string.Empty;
            _maxRows = Mathf.Clamp(_settings != null ? _settings.MaxRows : 300, 50, 2000);
            LogInfo("[PRESET] Full");
        }

        private void ExecuteAllPlayForFilteredEntries()
        {
            try
            {
                List<EffectEntry> targets = CollectFilteredEntries();
                if (targets.Count == 0)
                {
                    _lastError = "no filtered entries";
                    return;
                }

                _allPlaySnapshot.Clear();
                int changed = 0;
                for (int i = 0; i < targets.Count; i++)
                {
                    EffectEntry e = targets[i];
                    int key = GetRuntimeKey(e);
                    if (key != 0 && !_allPlaySnapshot.ContainsKey(key))
                        _allPlaySnapshot[key] = CaptureState(e);
                    changed += ForceVisibleAndPlay(e);
                }

                _hasAllPlaySnapshot = _allPlaySnapshot.Count > 0;
                _lastAllPlayTargetCount = targets.Count;
                _lastError = string.Empty;
                LogInfo("[ALLPLAY] targets=" + targets.Count + " changed=" + changed + " snapshot=" + _allPlaySnapshot.Count);
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogWarn("[ALLPLAY] failed: " + ex.Message);
            }
        }

        private void ExecuteResetAll()
        {
            if (!_hasAllPlaySnapshot || _allPlaySnapshot.Count == 0)
            {
                _lastError = "no snapshot for reset";
                return;
            }

            try
            {
                Dictionary<int, EffectEntry> map = BuildEntryMap();
                int restored = 0;
                int missing = 0;

                foreach (KeyValuePair<int, EntrySnapshot> kv in _allPlaySnapshot)
                {
                    EffectEntry entry;
                    if (!map.TryGetValue(kv.Key, out entry) || entry == null || !entry.IsAlive)
                    {
                        missing++;
                        continue;
                    }

                    restored += RestoreFromSnapshot(entry, kv.Value);
                }

                _lastError = string.Empty;
                LogInfo("[RESET] restored=" + restored + " missing=" + missing + " snapshot=" + _allPlaySnapshot.Count);
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogWarn("[RESET] failed: " + ex.Message);
            }
        }

        private Dictionary<int, EffectEntry> BuildEntryMap()
        {
            var map = new Dictionary<int, EffectEntry>(_entries.Count);
            for (int i = 0; i < _entries.Count; i++)
            {
                EffectEntry e = _entries[i];
                if (e == null || !e.IsAlive)
                    continue;

                int key = GetRuntimeKey(e);
                if (key == 0 || map.ContainsKey(key))
                    continue;

                map[key] = e;
            }
            return map;
        }

        private static int GetRuntimeKey(EffectEntry e)
        {
            if (e == null)
                return 0;
            if (e.Canvas != null)
                return e.Canvas.GetInstanceID();
            if (e.CanvasGroup != null)
                return e.CanvasGroup.GetInstanceID();
            if (e.Animator != null)
                return e.Animator.GetInstanceID();
            if (e.Particle != null)
                return e.Particle.GetInstanceID();
            if (e.Go != null)
                return e.Go.GetInstanceID();
            return 0;
        }

        private static EntrySnapshot CaptureState(EffectEntry e)
        {
            var s = new EntrySnapshot();
            if (e == null)
                return s;

            if (e.Go != null)
            {
                s.HasGo = true;
                s.GoActiveSelf = e.Go.activeSelf;
            }
            if (e.Canvas != null)
            {
                s.HasCanvas = true;
                s.CanvasEnabled = e.Canvas.enabled;
                s.CanvasSortingOrder = e.Canvas.sortingOrder;
            }
            if (e.CanvasGroup != null)
            {
                s.HasCanvasGroup = true;
                s.CanvasGroupAlpha = e.CanvasGroup.alpha;
            }
            if (e.Animator != null)
            {
                s.HasAnimator = true;
                s.AnimatorEnabled = e.Animator.enabled;
            }
            if (e.Particle != null)
            {
                s.HasParticle = true;
                s.ParticleWasPlaying = e.Particle.isPlaying;
            }
            return s;
        }

        private int ForceVisibleAndPlay(EffectEntry e)
        {
            int changed = 0;
            if (e == null)
                return changed;

            try
            {
                if (e.Go != null && !e.Go.activeSelf)
                {
                    e.Go.SetActive(true);
                    changed++;
                }
                if (e.Canvas != null && !e.Canvas.enabled)
                {
                    e.Canvas.enabled = true;
                    changed++;
                }
                if (e.CanvasGroup != null && e.CanvasGroup.alpha < 0.999f)
                {
                    e.CanvasGroup.alpha = 1f;
                    changed++;
                }
                if (e.Animator != null)
                {
                    if (!e.Animator.enabled)
                    {
                        e.Animator.enabled = true;
                        changed++;
                    }
                    ReplayAnimator(e.Animator);
                    changed++;
                }
                if (e.Particle != null)
                {
                    e.Particle.Play(true);
                    changed++;
                }
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogWarn("all-play failed for entry=" + (e != null ? e.Id.ToString() : "null") + " msg=" + ex.Message);
            }

            return changed;
        }

        private int RestoreFromSnapshot(EffectEntry e, EntrySnapshot s)
        {
            int restored = 0;
            if (e == null || s == null)
                return restored;

            if (s.HasCanvas && e.Canvas != null)
            {
                e.Canvas.enabled = s.CanvasEnabled;
                e.Canvas.sortingOrder = s.CanvasSortingOrder;
                restored++;
            }
            if (s.HasCanvasGroup && e.CanvasGroup != null)
            {
                e.CanvasGroup.alpha = s.CanvasGroupAlpha;
                restored++;
            }
            if (s.HasAnimator && e.Animator != null)
            {
                e.Animator.enabled = s.AnimatorEnabled;
                restored++;
            }
            if (s.HasParticle && e.Particle != null)
            {
                if (s.ParticleWasPlaying)
                    e.Particle.Play(true);
                else
                    e.Particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                restored++;
            }
            if (s.HasGo && e.Go != null)
            {
                e.Go.SetActive(s.GoActiveSelf);
                restored++;
            }

            return restored;
        }

        private IEnumerable<EffectEntry> FilterEntries()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                EffectEntry e = _entries[i];
                if (e == null || !e.IsAlive)
                    continue;

                if (_showOnlyActive && !e.IsActiveInHierarchy)
                    continue;

                if (!PassCategoryFilter(e.Category))
                    continue;

                if (_infoOnly && !ContainsMarkerKeyword(e.Path) && !ContainsMarkerKeyword(e.Name))
                    continue;

                if (!PassSearchFilter(e))
                    continue;

                yield return e;
            }
        }

        private bool PassCategoryFilter(string category)
        {
            switch (category)
            {
                case "Canvas": return _showCanvas;
                case "CanvasGroup": return _showCanvasGroup;
                case "Animator": return _showAnimator;
                case "Particle": return _showParticle;
                case "Marker": return _showMarker;
                default: return true;
            }
        }

        private bool PassSearchFilter(EffectEntry e)
        {
            if (string.IsNullOrWhiteSpace(_search))
                return true;

            string s = _search.Trim();
            return ContainsIgnoreCase(e.Name, s)
                || ContainsIgnoreCase(e.Path, s)
                || ContainsIgnoreCase(e.Category, s)
                || ContainsIgnoreCase(e.Detail, s);
        }

        private bool TryResolveActionScene()
        {
            return CanonPlugins.Shared.PluginActivityGate.Allowed && _actionScene != null;
        }

        private void ActivitySceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (!scene.isLoaded) return;
            foreach (var root in scene.GetRootGameObjects())
            {
                var candidate = root.GetComponentInChildren<ActionScene>(true);
                if (candidate == null) continue;
                CanonPlugins.Shared.PluginActivityGate.End(); _actionScene = candidate;
                _actionSceneInstanceId = candidate.GetInstanceID();
                CanonPlugins.Shared.PluginActivityGate.Begin(candidate); break;
            }
        }
        private void ActivitySceneUnloaded(UnityEngine.SceneManagement.Scene scene)
        {
            if (_actionScene == null || _actionScene.gameObject.scene == scene) CanonPlugins.Shared.PluginActivityGate.End();
        }
        private void ClearRuntimeContext()
        {
            _actionScene = null;
            _actionSceneInstanceId = 0;
            _entries.Clear();
            _allPlaySnapshot.Clear();
            _hasAllPlaySnapshot = false;
            _lastAllPlayTargetCount = 0;
            _entrySeq = 1;


        }

        private void RebuildEntries(string reason)
        {
            _lastError = string.Empty;
            _entries.Clear();
            _entrySeq = 1;

            if (_actionScene == null)
                return;

            try
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                Traverse(_actionScene.transform, seen);
                _entries.Sort((a, b) =>
                {
                    int p = string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
                    if (p != 0) return p;
                    return string.Compare(a.Category, b.Category, StringComparison.OrdinalIgnoreCase);
                });

                LogInfo("[SCAN] reason=" + reason + " entries=" + _entries.Count);
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogWarn("[SCAN] failed: " + ex.Message);
            }
        }

        private void Traverse(Transform node, HashSet<string> seen)
        {
            if (node == null)
                return;

            GameObject go = node.gameObject;
            string path = BuildPath(node);

            bool marker = ContainsMarkerKeyword(go.name) || ContainsMarkerKeyword(path);
            if (marker)
            {
                AddEntry(seen, "Marker", go, path, "marker keyword", null, null, null, null);
            }

            Canvas canvas = go.GetComponent<Canvas>();
            if (canvas != null)
            {
                string detail = "order=" + canvas.sortingOrder + ", mode=" + canvas.renderMode;
                AddEntry(seen, "Canvas", go, path, detail, canvas, null, null, null);
            }

            CanvasGroup canvasGroup = go.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                string detail = "alpha=" + canvasGroup.alpha.ToString("0.00");
                AddEntry(seen, "CanvasGroup", go, path, detail, null, canvasGroup, null, null);
            }

            Animator animator = go.GetComponent<Animator>();
            if (animator != null)
            {
                string detail = "enabled=" + animator.enabled;
                AddEntry(seen, "Animator", go, path, detail, null, null, animator, null);
            }

            ParticleSystem particle = go.GetComponent<ParticleSystem>();
            if (particle != null)
            {
                string detail = "playing=" + particle.isPlaying;
                AddEntry(seen, "Particle", go, path, detail, null, null, null, particle);
            }

            for (int i = 0; i < node.childCount; i++)
                Traverse(node.GetChild(i), seen);
        }

        private void AddEntry(
            HashSet<string> seen,
            string category,
            GameObject go,
            string path,
            string detail,
            Canvas canvas,
            CanvasGroup canvasGroup,
            Animator animator,
            ParticleSystem particle)
        {
            if (go == null)
                return;

            int componentId = 0;
            if (canvas != null) componentId = canvas.GetInstanceID();
            else if (canvasGroup != null) componentId = canvasGroup.GetInstanceID();
            else if (animator != null) componentId = animator.GetInstanceID();
            else if (particle != null) componentId = particle.GetInstanceID();
            else componentId = go.GetInstanceID();

            string key = category + ":" + componentId;
            if (!seen.Add(key))
                return;

            var e = new EffectEntry
            {
                Id = _entrySeq++,
                Category = category,
                Name = go.name,
                Path = path,
                Detail = detail ?? string.Empty,
                Go = go,
                Canvas = canvas,
                CanvasGroup = canvasGroup,
                Animator = animator,
                Particle = particle
            };
            _entries.Add(e);
        }

        private static string BuildPath(Transform t)
        {
            if (t == null)
                return string.Empty;

            var stack = new Stack<string>();
            Transform cur = t;
            while (cur != null)
            {
                stack.Push(cur.name);
                cur = cur.parent;
            }

            var sb = new StringBuilder(128);
            while (stack.Count > 0)
            {
                if (sb.Length > 0)
                    sb.Append('/');
                sb.Append(stack.Pop());
            }
            return sb.ToString();
        }

        private void ToggleGameObjectActive(GameObject go)
        {
            if (go == null)
                return;

            try
            {
                go.SetActive(!go.activeSelf);
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogWarn("toggle active failed: " + ex.Message);
            }
        }

        private static void ReplayAnimator(Animator animator)
        {
            if (animator == null)
                return;

            try
            {
                animator.Rebind();
                animator.Update(0f);
                animator.Play(0, 0, 0f);
                animator.Update(0f);
            }
            catch
            {
                // ignore preview failures
            }
        }

        private void TriggerSetTopic()
        {
            try
            {
                InformationUI.SetTopic(_settings.TestTopicName);
                LogInfo("trigger SetTopic: " + _settings.TestTopicName);
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogWarn("SetTopic failed: " + ex.Message);
            }
        }

        private void TriggerSetTopicFull()
        {
            try
            {
                InformationUI.SetTopicFull();
                LogInfo("trigger SetTopicFull");
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogWarn("SetTopicFull failed: " + ex.Message);
            }
        }

        private void TriggerSetNormal()
        {
            try
            {
                InformationUI.Set(_settings.TestNormalMessage, InformationUI.Mode.Normal);
                LogInfo("trigger Set normal: " + _settings.TestNormalMessage);
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogWarn("Set normal failed: " + ex.Message);
            }
        }

        private void TriggerSetTopicMode()
        {
            try
            {
                InformationUI.Set(_settings.TestTopicName, InformationUI.Mode.Topic);
                LogInfo("trigger Set topic-mode: " + _settings.TestTopicName);
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogWarn("Set topic-mode failed: " + ex.Message);
            }
        }

        private string GetActionSceneStatus()
        {
            if (_actionScene == null)
                return "not found";
            return "captured(id=" + _actionSceneInstanceId + ")";
        }

        private static bool ContainsIgnoreCase(string value, string keyword)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(keyword))
                return false;
            return value.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsMarkerKeyword(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            string lower = value.ToLowerInvariant();
            for (int i = 0; i < MarkerKeywords.Length; i++)
            {
                if (lower.Contains(MarkerKeywords[i]))
                    return true;
            }
            return false;
        }

        private void LogInfo(string message)
        {
            if (_logEnabled == null || !_logEnabled.Value) return;
            if (_settings != null && _settings.EnableFileLog)
                _fileLogger?.Write("INFO", message);
            if (_settings != null && _settings.EnableUnityLog)
                Logger.LogInfo(message);
        }

        private void LogWarn(string message)
        {
            if (_settings != null && _settings.EnableFileLog)
                _fileLogger?.Write("WARN", message);
            Logger.LogWarning(message);
        }

        private void RawInfo(string message)
        {
            LogInfo(message);
        }

        private void RawWarn(string message)
        {
            LogWarn(message);
        }

        private ConfigEntry<bool> _logEnabled;

        private void ApplyLogging()
        {
            _fileLogger?.Dispose();
            if (_logEnabled.Value && _settings.EnableFileLog)
                _fileLogger.Initialize(Path.Combine(_pluginDir, PluginName + ".log"), _settings.AutoFlushFileLog);
        }
        private void OnDisable() => CanonPlugins.Shared.PluginActivityGate.End();
    }
}
