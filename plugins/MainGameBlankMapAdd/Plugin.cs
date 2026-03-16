using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using HarmonyLib;
using MainGameTransformGizmo;
using UnityEngine;
using UnityEngine.Video;

namespace MainGameBlankMapAdd
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInProcess("KoikatsuSunshine")]
    [BepInDependency(MainGameTransformGizmo.Plugin.GUID, BepInDependency.DependencyFlags.HardDependency)]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        private sealed class PendingVideoStart
        {
            public VideoPlayer Player;
            public string SurfaceName;
            public string VideoPath;
            public bool PlayWhenReady;
        }

        private sealed class VideoBinding
        {
            public VideoPlayer Player;
            public RenderTexture Texture;
            public readonly List<Material> Materials = new List<Material>();
            public VideoPlayer.EventHandler PreparedHandler;
            public VideoPlayer.EventHandler LoopPointHandler;
        }

        internal const string GUID = "com.kks.maingameblankmapadd";
        internal const string PluginName = "MainGameBlankMapAdd";
        internal const string Version = "0.1.0";

        internal static Plugin Instance { get; private set; }

        private Harmony _harmony;
        private PluginSettings _settings;
        private string _pluginDir;
        private string _logPath;
        private readonly object _logLock = new object();
        private int _lastBlankifiedRootId = int.MinValue;
        private int _sourceThumbnailId = -1;
        private GameObject _videoRoomRoot;
        private int _videoRoomRootMapId = int.MinValue;
        private readonly List<RenderTexture> _videoTextures = new List<RenderTexture>();
        private readonly List<Mesh> _generatedMeshes = new List<Mesh>();
        private readonly Dictionary<string, VideoBinding> _videoBindings =
            new Dictionary<string, VideoBinding>(StringComparer.OrdinalIgnoreCase);
        private VideoPlayer _mainVideoPlayer;
        private AudioSource _videoRoomAudioSource;
        private ChaControl _femaleChara;
        private bool _hasFemaleBaseY;
        private float _femaleBaseY;
        private float _nextPositionLogTime = 0f;
        private float _nextVoiceSyncLogTime = 0f;
        private float _nextReverbBypassEnforceTime = 0f;
        private bool _editMode = false;
        private TransformGizmo _gizmo;
        private AudioReverbZone _voiceReverbZone;
        private GameObject _reverbZoneObject;
        private BaseMap _lastReservedMap;
        private readonly List<PendingVideoStart> _pendingVideoStarts = new List<PendingVideoStart>();
        private float _nextPendingVideoLogTime = 0f;
        private float _nextBridgeSnapshotLogTime = 0f;
        private float _nextBridgeSnapshotMissingLogTime = 0f;
        private bool _playbackSeekDragging = false;
        private bool _playbackVolumeDragging = false;
        private bool _playbackGainDragging = false;
        private bool _playbackReverbDragging = false;
        private bool _playbackBarHiddenByUser = false;
        private float _playbackSeekNormalized = 0f;
        private float _playbackRoomScale = 1f;
        private bool _playbackRoomControlsExpanded = false;
        private string _roomScaleInput = "";
        private string _videoGainInput = "";
        private string _beatSyncBpmInput = "";
        private string _beatSyncLowPassInput = "";
        private string _beatSyncLowThresholdInput = "";
        private string _beatSyncHighThresholdInput = "";
        private string _beatSyncLowIntensityInput = "";
        private string _beatSyncMidIntensityInput = "";
        private string _beatSyncHighIntensityInput = "";
        private string _beatSyncSmoothTimeInput = "";
        private string _beatSyncStrongBeatsInput = "";
        private string _beatSyncWeakBeatsInput = "";
        private readonly string[] _roomPosInputs = { "", "", "" };
        private readonly string[] _roomRotInputs = { "", "", "" };
        private bool _folderDropdownOpen = false;
        private Vector2 _folderDropdownScroll = Vector2.zero;
        private bool _videoDropdownOpen = false;
        private Vector2 _videoDropdownScroll = Vector2.zero;
        private const float PlaybackBarMinHeightCollapsedPx = 72f;
        private const float PlaybackBarMinHeightExpandedPx = 282f;

        // フォルダ再生
        private static readonly string[] _videoExtensions =
            { ".mp4", ".mkv", ".avi", ".mov", ".webm", ".m4v", ".ts" };
        internal string[] FolderFiles = Array.Empty<string>();
        internal int FolderIndex = -1;
        private string _lastScannedFolder = null;
        internal RoomLayoutProfile FolderBaselineProfile = null;
        private RoomLayoutProfileRepository _roomLayoutProfiles;

        // フォルダ切り替えフェード（IMGUI黒オーバーレイ）
        private enum FadePhase { None, ToBlack, FromBlack }
        private FadePhase _fadePhase = FadePhase.None;
        private float _fadeElapsed;
        private float _fadePhaseDuration;
        private string _fadePendingPath; // ToBlack完了時にURL差し替えするパス
        private Texture2D _blackTex;

        // 外部HTTPコマンドキュー（バックグラウンドスレッドからUnityメインスレッドへ）
        internal readonly System.Collections.Concurrent.ConcurrentQueue<Action> CommandQueue =
            new System.Collections.Concurrent.ConcurrentQueue<Action>();

        private void Awake()
        {
            Instance = this;
            _pluginDir = Path.GetDirectoryName(Info.Location) ?? Paths.PluginPath;

            string logDir = Path.Combine(_pluginDir, "_logs");
            Directory.CreateDirectory(logDir);
            _logPath = Path.Combine(logDir, "info.txt");
            File.AppendAllText(
                _logPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] === {PluginName} {Version} start ==={Environment.NewLine}",
                Encoding.UTF8);

            _settings = SettingsStore.LoadOrCreate(_pluginDir, LogInfo, LogWarn, LogError);
            _roomLayoutProfiles = RoomLayoutProfileStore.LoadOrCreate(_pluginDir, LogInfo, LogWarn, LogError);
            SetupConfigEntries();

            OnVideoEnded += OnFolderVideoEnded;
            StartHttpServer();

            _harmony = new Harmony(GUID);
            _harmony.PatchAll(typeof(Plugin));
            LogAvailableWebCamDevices();

            LogInfo(
                $"settings loaded mapNo={_settings.AddedMapNo} source={_settings.SourceMapNo} " +
                $"blankify={_settings.BlankifySceneOnLoad}");
        }

        private void OnDestroy()
        {
            OnVideoEnded -= OnFolderVideoEnded;
            StopHttpServer();
            try
            {
                DestroyVideoRoom();
                _harmony?.UnpatchSelf();
            }
            catch (Exception ex)
            {
                LogWarn($"unpatch failed: {ex.Message}");
            }
        }

        private void Update()
        {
            ApplyConfigChangesIfNeeded();
            TryStartPendingVideosIfRoomReady();
            TickDeferredPlayFromBar();

            // HTTPコマンドキューをメインスレッドで処理
            while (CommandQueue.TryDequeue(out var cmd))
            {
                try { cmd(); }
                catch (Exception ex) { LogWarn($"command error: {ex.Message}"); }
            }

            // フォルダフェード更新
            if (_fadePhase != FadePhase.None) UpdateFolderFade();
            bool ctrlDown = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            // Ctrl+P: toggle video playback.
            if (ctrlDown && Input.GetKeyDown(KeyCode.P))
            {
                if (_mainVideoPlayer != null)
                {
                    if (_mainVideoPlayer.isPlaying)
                    {
                        _mainVideoPlayer.Pause();
                        LogInfo("video paused");
                    }
                    else
                    {
                        _mainVideoPlayer.Play();
                        LogInfo("video resumed");
                    }
                }
            }

            // Ctrl+R: reload JSON settings.
            if (ctrlDown && Input.GetKeyDown(KeyCode.R))
            {
                ReloadSettingsAndApply();
            }

            // Ctrl+D: toggle gizmo edit mode.
            if (ctrlDown && Input.GetKeyDown(KeyCode.D))
            {
                if (_videoRoomRoot != null)
                {
                    _editMode = !_editMode;
                    _gizmo?.SetVisible(_editMode);
                    LogInfo($"gizmo edit mode: {(_editMode ? "ON" : "OFF")}");
                }
            }

            UpdateUiCaptureState();
            UpdateWebCamTextures();
            if (_videoRoomRoot == null) return;

            EnsureFemaleCharaRef();

            // Edit mode: mouse wheel rotates room around selected axis.
            if (_editMode)
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.001f)
                {
                    float deg = scroll * 900f; // one notch (~0.1) => ~90 deg
                    bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                    bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
                    Vector3 axis = shift ? Vector3.right : alt ? Vector3.forward : Vector3.up;
                    _videoRoomRoot.transform.Rotate(axis, deg, Space.World);
                }
            }

            // Follow female in non-edit mode.
            if (!_editMode && _femaleChara != null)
            {
                var fp = _femaleChara.transform.position;
                float baseY = _hasFemaleBaseY ? _femaleBaseY : fp.y;
                float yOffset = _settings.VideoRoomOffsetY;
                _videoRoomRoot.transform.position = new Vector3(
                    fp.x + _settings.VideoRoomOffsetX,
                    baseY + yOffset,
                    fp.z + _settings.VideoRoomOffsetZ);
            }

            // Reverb origin should follow female position.
            SyncReverbZoneToFemale(false);
            // Video audio origin should follow female position in world space.
            SyncVideoAudioSourceToFemale(false);
            EnforceReverbBypassWhileDisabled();
            TryLogAudioDiagnosticsTick();

            // Position log every 5 seconds (VerboseLog only).
            if (!_settings.VerboseLog) return;
            if (Time.unscaledTime < _nextPositionLogTime) return;
            _nextPositionLogTime = Time.unscaledTime + 5f;

            if (_femaleChara != null)
            {
                LogInfo(
                    $"[pos] female pos={_femaleChara.transform.position} " +
                    $"rot={_femaleChara.transform.eulerAngles}");
            }

            LogInfo(
                $"[pos] video room pos={_videoRoomRoot.transform.position} " +
                $"rot={_videoRoomRoot.transform.eulerAngles}");
            if (_videoRoomAudioSource != null)
            {
                LogInfo($"[pos] video audio pos={_videoRoomAudioSource.transform.position}");
            }
        }

        // ── フォルダ再生 ────────────────────────────────────────────

        internal void ScanFolderIfNeeded()
        {
            string folder = _settings?.FolderPlayPath ?? string.Empty;
            if (string.Equals(folder, _lastScannedFolder, StringComparison.OrdinalIgnoreCase)) return;
            _lastScannedFolder = folder;

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                FolderFiles = Array.Empty<string>();
                FolderIndex = -1;
                LogInfo($"folder play: folder not found path={folder}");
                return;
            }

            bool byDate = string.Equals(_settings?.FolderPlaySortMode, "Date", StringComparison.OrdinalIgnoreCase);
            bool asc = _settings?.FolderPlaySortAscending ?? true;
            var files = Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly)
                .Where(f => _videoExtensions.Contains(
                    Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));
            IOrderedEnumerable<string> sorted = byDate
                ? (asc
                    ? files.OrderBy(f => File.GetLastWriteTime(f))
                    : files.OrderByDescending(f => File.GetLastWriteTime(f)))
                : (asc
                    ? files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    : files.OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase));
            FolderFiles = sorted.ToArray();
            FolderIndex = FolderFiles.Length > 0 ? 0 : -1;
            // フォルダ切り替え時点の全設定をベースラインとして保存（動画個別設定なし時の戻し先）
            FolderBaselineProfile = CaptureCurrentRoomLayoutProfile(includeAudioGain: true, markAsRoomLayout: true);
            if (TryGetBeatSyncSnapshot(out BeatSyncSnapshot baselineBeat))
            {
                FolderBaselineProfile.HasBeatSync = true;
                FolderBaselineProfile.BeatEnabled = baselineBeat.Enabled;
                FolderBaselineProfile.BeatBpm = baselineBeat.Bpm;
                FolderBaselineProfile.BeatAutoMotionSwitch = baselineBeat.AutoMotionSwitch;
                FolderBaselineProfile.BeatAutoThreshold = baselineBeat.AutoThreshold;
                FolderBaselineProfile.BeatLowThreshold = baselineBeat.LowThreshold;
                FolderBaselineProfile.BeatHighThreshold = baselineBeat.HighThreshold;
                FolderBaselineProfile.BeatLowIntensity = baselineBeat.LowIntensity;
                FolderBaselineProfile.BeatMidIntensity = baselineBeat.MidIntensity;
                FolderBaselineProfile.BeatHighIntensity = baselineBeat.HighIntensity;
                FolderBaselineProfile.BeatSmoothTime = baselineBeat.SmoothTime;
                FolderBaselineProfile.BeatStrongMotionBeats = baselineBeat.StrongMotionBeats;
                FolderBaselineProfile.BeatWeakMotionBeats = baselineBeat.WeakMotionBeats;
                FolderBaselineProfile.BeatLowPassHz = baselineBeat.LowPassHz;
                FolderBaselineProfile.BeatVerboseLog = baselineBeat.VerboseLog;
            }
            if (TryGetSpeedLimitBreakSnapshot(out SpeedLimitBreakSnapshot baselineSlb))
            {
                FolderBaselineProfile.HasSpeedLimitBreak = true;
                FolderBaselineProfile.SpeedForceVanilla = baselineSlb.ForceVanillaSpeed;
                FolderBaselineProfile.SpeedEnableVideoTimeSpeedCues = baselineSlb.EnableVideoTimeSpeedCues;
                FolderBaselineProfile.SpeedAppliedBpmMax = baselineSlb.AppliedBpmMax;
            }
            LogInfo($"folder play: scanned path={folder} count={FolderFiles.Length}");
        }

        internal void ForceFolderRescan()
        {
            _lastScannedFolder = null;
            ScanFolderIfNeeded();
            if (_settings != null && _settings.FolderPlayEnabled)
                ApplyFolderVideoPath();
        }

        internal void ApplyFolderVideoPath()
        {
            ScanFolderIfNeeded();
            if (FolderIndex >= 0 && FolderIndex < FolderFiles.Length && !IsWebCamUrl(_settings.VideoPath))
                _settings.VideoPath = FolderFiles[FolderIndex];

            TryApplySavedRoomLayoutForCurrentSelection("apply-folder-video-path");
        }

        internal void PlayFolderEntry(int index)
        {
            if (FolderFiles.Length == 0) return;
            if (index < 0 || index >= FolderFiles.Length) return;
            FolderIndex = index;
            string newPath = FolderFiles[FolderIndex];
            _settings.VideoPath = newPath;
            TryApplySavedRoomLayoutForCurrentSelection("play-folder-entry", newPath);
            LogInfo($"folder play: [{FolderIndex + 1}/{FolderFiles.Length}] {newPath}");

            // VideoRoomとメインPlayerが存在する場合はURL差し替え（Destroy不要）
            if (_videoRoomRoot != null && _mainVideoPlayer != null && !string.IsNullOrEmpty(newPath))
            {
                StartFolderVideoSwap(newPath);
                return;
            }

            // フォールバック: 従来のDestroy→再構築
            if (_lastReservedMap == null || _lastReservedMap.mapRoot == null) return;
            DestroyVideoRoom();
            _lastBlankifiedRootId = int.MinValue;
            TryBlankifyCurrentMap(_lastReservedMap);
        }

        private void StartFolderVideoSwap(string newPath)
        {
            string resolvedPath = ResolveVideoPath(newPath);
            if (string.IsNullOrEmpty(resolvedPath))
            {
                LogWarn($"folder swap: path unresolved path={newPath}, falling back to rebuild");
                if (_lastReservedMap == null || _lastReservedMap.mapRoot == null) return;
                DestroyVideoRoom();
                _lastBlankifiedRootId = int.MinValue;
                TryBlankifyCurrentMap(_lastReservedMap);
                return;
            }

            float fadeDur = _settings?.FolderFadeDuration ?? 1.0f;
            if (fadeDur > 0.01f && _fadePhase == FadePhase.None)
            {
                // フェード開始: まず音量を下げながら黒くする
                // URL差し替えはToBlack完了後に行う
                _fadePendingPath = resolvedPath;
                _fadePhaseDuration = fadeDur * 0.5f;
                _fadeElapsed = 0f;
                _fadePhase = FadePhase.ToBlack;
                ApplyRuntimeVideoAudioLevel(0f);
                LogInfo($"fade to black started duration={fadeDur:F2} path={resolvedPath}");
            }
            else
            {
                SwapVideoUrl(resolvedPath);
            }
        }

        // ── URL差し替え（フェードなし・フェード完了時共用） ─────────────
        private void SwapVideoUrl(string resolvedPath)
        {
            var player = _mainVideoPlayer;
            if (player == null) return;

            // 現在のbindingを特定する
            VideoBinding binding = null;
            string oldKey = null;
            foreach (var kv in _videoBindings)
            {
                if (kv.Value?.Player == player)
                {
                    binding = kv.Value;
                    oldKey = kv.Key;
                    break;
                }
            }
            if (binding == null) return;

            // 古いイベントハンドラを解除
            if (binding.PreparedHandler != null) player.prepareCompleted -= binding.PreparedHandler;
            if (binding.LoopPointHandler != null) player.loopPointReached -= binding.LoopPointHandler;

            // 新しいイベントハンドラを登録
            string capturedPath = resolvedPath;
            binding.PreparedHandler = _ =>
            {
                ApplySquareCropToBinding(binding);
                LogInfo($"folder swap prepared: {capturedPath}");
                FireOnVideoLoaded(capturedPath);
            };
            binding.LoopPointHandler = _ => FireOnVideoEnded(capturedPath);
            player.prepareCompleted += binding.PreparedHandler;
            player.loopPointReached += binding.LoopPointHandler;

            // bindingのキーを更新
            if (oldKey != null && oldKey != resolvedPath)
            {
                _videoBindings.Remove(oldKey);
                _videoBindings[resolvedPath] = binding;
            }

            // URL差し替えてPrepare
            player.Stop();
            player.url = resolvedPath;
            player.isLooping = false;
            ConfigureVideoAudio(player, true);
            player.Prepare();
            QueueVideoPlaybackStart(player, "VideoSurface", resolvedPath);
            LogInfo($"folder swap: URL replaced path={resolvedPath}");
        }

        // ── IMGUIフェード更新 ─────────────────────────────────────────────
        private void UpdateFolderFade()
        {
            _fadeElapsed += Time.unscaledDeltaTime;

            if (_fadePhase == FadePhase.ToBlack)
            {
                if (_fadeElapsed >= _fadePhaseDuration)
                {
                    // 完全に黒くなった: URL差し替え実行
                    if (!string.IsNullOrEmpty(_fadePendingPath))
                    {
                        SwapVideoUrl(_fadePendingPath);
                        _fadePendingPath = null;
                    }
                    _fadeElapsed = 0f;
                    _fadePhase = FadePhase.FromBlack;
                }
            }
            else if (_fadePhase == FadePhase.FromBlack)
            {
                if (_fadeElapsed >= _fadePhaseDuration)
                {
                    // フェード完了: 音量を戻す
                    ApplyRuntimeVideoAudioLevel(_settings?.VideoVolume ?? 0.5f);
                    _fadePhase = FadePhase.None;
                    LogInfo("fade complete");
                }
            }
        }

        private void OnFolderVideoEnded(string path)
        {
            if (!(_settings?.FolderPlayEnabled ?? false)) return;
            if (FolderFiles.Length == 0) return;

            if (_settings.FolderPlaySingleLoop)
            {
                int current = FolderIndex;
                if (current < 0 || current >= FolderFiles.Length) current = 0;
                PlayFolderEntry(current);
                return;
            }

            int next = FolderIndex + 1;
            if (next >= FolderFiles.Length)
            {
                if (_settings.FolderPlayLoop) next = 0;
                else return;
            }
            PlayFolderEntry(next);
        }

        // ── OnGUI ──────────────────────────────────────────────────

        // Playback bar implementation moved to Plugin.PlaybackBar.cs


        private string SaveCurrentStateSnapshot(string trigger)
        {
            if (_videoRoomRoot == null)
            {
                LogWarn($"save ignored trigger={trigger}: video room is null");
                return null;
            }

            // Pull latest values from ConfigManager before snapshot so typed VideoPath is included.
            ApplyConfigToSettings(rebuildRoom: false, reason: $"save-prep:{trigger}");
            EnsureFemaleCharaRef();

            if (_femaleChara != null)
            {
                var off = _videoRoomRoot.transform.position - _femaleChara.transform.position;
                _settings.VideoRoomOffsetX = off.x;
                float baseY = _hasFemaleBaseY ? _femaleBaseY : _femaleChara.transform.position.y;
                _settings.VideoRoomOffsetY = _videoRoomRoot.transform.position.y - baseY;
                _settings.VideoRoomOffsetZ = off.z;
            }
            else
            {
                LogWarn($"save trigger={trigger}: female reference is null, keep previous offset");
            }

            var euler = _videoRoomRoot.transform.eulerAngles;
            _settings.VideoRoomRotationX = euler.x;
            _settings.VideoRoomRotationY = euler.y;
            _settings.VideoRoomRotationZ = euler.z;

            SettingsStore.Save(Path.Combine(_pluginDir, "MapAddSettings.json"), _settings);
            FireOnSettingsApplied();
            string presetSnapshot = SaveCurrentSettingsPresetSnapshot();
            RefreshPresetNameList(presetSnapshot);
            SyncConfigEntriesFromSettings();

            LogInfo(
                $"saved trigger={trigger} video={_settings.VideoPath} " +
                $"offset=({_settings.VideoRoomOffsetX:F3},{_settings.VideoRoomOffsetY:F3},{_settings.VideoRoomOffsetZ:F3}) " +
                $"rot=({euler.x:F1},{euler.y:F1},{euler.z:F1})");

            if (_editMode)
            {
                _editMode = false;
                _gizmo?.SetVisible(false);
            }

            return presetSnapshot;
        }

        private string SaveCurrentSettingsPresetSnapshot()
        {
            try
            {
                string presetDir = Path.Combine(_pluginDir, "MapAddPresets");
                Directory.CreateDirectory(presetDir);

                string videoFileBase = GetCurrentVideoFileNameForPreset();
                string presetName = $"{videoFileBase}_{DateTime.Now:yyyyMMdd_HHmmss_fff}";
                string presetPath = Path.Combine(presetDir, presetName + ".json");
                SettingsStore.Save(presetPath, _settings);
                LogInfo($"preset snapshot saved name={presetName} path={presetPath}");
                return presetName;
            }
            catch (Exception ex)
            {
                LogWarn($"preset snapshot save failed: {ex.Message}");
                return null;
            }
        }

        private string GetCurrentVideoFileNameForPreset()
        {
            string path = _mainVideoPlayer?.url;
            if (string.IsNullOrWhiteSpace(path))
                path = _settings?.VideoPath;
            if (string.IsNullOrWhiteSpace(path))
                return "preset";

            string normalizedPath = NormalizeVideoPathInput(path);
            string fileName = Path.GetFileNameWithoutExtension(normalizedPath);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = "preset";

            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
                fileName = fileName.Replace(invalid[i], '_');

            return fileName;
        }

        private static string NormalizeVideoPathInput(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string normalized = value.Trim();
            while (normalized.Length >= 2)
            {
                char first = normalized[0];
                char last = normalized[normalized.Length - 1];
                bool wrappedByDoubleQuote = first == '"' && last == '"';
                bool wrappedBySingleQuote = first == '\'' && last == '\'';
                if (!wrappedByDoubleQuote && !wrappedBySingleQuote)
                    break;

                normalized = normalized.Substring(1, normalized.Length - 2).Trim();
            }

            return RestoreEscapedControlCharacters(normalized);
        }

        private static string RestoreEscapedControlCharacters(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            // BepInEx cfg parser can decode escape sequences like "\b" into control chars.
            // Convert them back to literal path fragments so Windows path APIs accept them.
            var sb = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\b': sb.Append("\\b"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\v': sb.Append("\\v"); break;
                    case '\0': sb.Append("\\0"); break;
                    default:
                        if (!char.IsControl(c))
                            sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        private void LogCore(string level, string msg)
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {msg}";
            if (level == "ERROR")
                Logger.LogError(msg);
            else if (level == "WARN")
                Logger.LogWarning(msg);
            else
                Logger.LogInfo(msg);

            lock (_logLock)
            {
                File.AppendAllText(_logPath, line + Environment.NewLine, Encoding.UTF8);
            }
        }

        private void LogInfo(string msg) => LogCore("INFO", msg);
        private void LogWarn(string msg) => LogCore("WARN", msg);
        private void LogError(string msg) => LogCore("ERROR", msg);

        public static bool TryGetMainVideoPlaybackSnapshot(
            out double timeSec,
            out double lengthSec,
            out bool isPrepared,
            out bool isPlaying)
        {
            timeSec = 0d;
            lengthSec = 0d;
            isPrepared = false;
            isPlaying = false;

            var inst = Instance;
            if (inst == null)
                return false;

            var player = inst._mainVideoPlayer;
            if (player == null)
            {
                if (Time.unscaledTime >= inst._nextBridgeSnapshotMissingLogTime)
                {
                    inst._nextBridgeSnapshotMissingLogTime = Time.unscaledTime + 2f;
                    inst.LogInfo("[bridge] snapshot requested but main video player is null");
                }
                return false;
            }

            isPrepared = player.isPrepared;
            isPlaying = player.isPlaying;
            timeSec = player.time;
            lengthSec = player.length;

            if (inst._settings.VerboseLog && Time.unscaledTime >= inst._nextBridgeSnapshotLogTime)
            {
                inst._nextBridgeSnapshotLogTime = Time.unscaledTime + 2f;
                inst.LogInfo(
                    $"[bridge] snapshot exported prepared={isPrepared} playing={isPlaying} " +
                    $"time={timeSec:F3} length={lengthSec:F3}");
            }

            return true;
        }

    }
}
