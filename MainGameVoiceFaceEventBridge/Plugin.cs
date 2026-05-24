
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using UnityEngine;

namespace MainGameVoiceFaceEventBridge
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    internal sealed partial class Plugin : BaseUnityPlugin
    {
        public const string GUID = "com.kks.maingame.voicefaceeventbridge";
        public const string PluginName = "MainGameVoiceFaceEventBridge";
        public const string Version = "0.3.1";

        internal static Plugin Instance { get; private set; }
        internal static new ManualLogSource Logger { get; private set; }
        internal static string PluginDir { get; private set; }
        internal static string LogFilePath { get; private set; }
        internal static PluginSettings Settings { get; private set; }
        internal static HSceneProc CurrentProc { get; private set; }

        private static readonly object FileLogLock = new object();
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private static readonly FieldInfo LstFemaleField = AccessTools.Field(typeof(HSceneProc), "lstFemale");
        private static readonly FieldInfo FaceField = AccessTools.Field(typeof(HSceneProc), "face");
        private static readonly FieldInfo Face1Field = AccessTools.Field(typeof(HSceneProc), "face1");
        private static readonly FieldInfo LstUseAnimInfoField = AccessTools.Field(typeof(HSceneProc), "lstUseAnimInfo");
        private static readonly FieldInfo LstProcField = AccessTools.Field(typeof(HSceneProc), "lstProc");
        private static readonly FieldInfo SpriteField = AccessTools.Field(typeof(HSceneProc), "sprite");
        private static readonly FieldInfo CategorysField = AccessTools.Field(typeof(HSceneProc), "categorys");
        private static readonly FieldInfo ChangeTaiiField = AccessTools.Field(typeof(HSceneProc), "changeTaii");
        private static readonly FieldInfo BChangePointField = AccessTools.Field(typeof(HSceneProc), "bChangePoint");

        private Harmony _harmony;
        private ExternalPipeServer _pipeServer;
        private ExternalVoicePlayer _externalVoicePlayer;
        private float _nextProcProbeTime;
        private float _blockGameVoiceUntil;
        private bool _voiceProcStopOverridden;
        private bool _voiceProcStopOriginal;
        private float _nextVoiceRestorePendingLogTime;
        private string _activeSequenceSessionId = string.Empty;
        private readonly HashSet<string> _sequenceTriggerRegisteredSessions =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _sequenceLineStartTimes =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly List<PendingLineTimedAction> _pendingLineTimedActions =
            new List<PendingLineTimedAction>();

        private ConfigEntry<bool> _cfgEnabled;
        private ConfigEntry<bool> _cfgVerboseLog;
        private ConfigEntry<KeyboardShortcut> _cfgReloadKey;
        private ConfigEntry<KeyboardShortcut> _cfgStateDumpKey;
        private ConfigEntry<float> _cfgPlaybackVolume;
        private ConfigEntry<float> _cfgFemalePlaybackVolume;
        private ConfigEntry<float> _cfgExternalPlaybackPitch;

        // ClothesDetection
        private ConfigEntry<string> _cfgTopKeywords;
        private ConfigEntry<string> _cfgBottomKeywords;
        private ConfigEntry<string> _cfgBraKeywords;
        private ConfigEntry<string> _cfgShortsKeywords;
        private ConfigEntry<string> _cfgGlovesKeywords;
        private ConfigEntry<string> _cfgPanthoseKeywords;
        private ConfigEntry<string> _cfgSocksKeywords;
        private ConfigEntry<string> _cfgShoesKeywords;
        private ConfigEntry<string> _cfgGlassesKeywords;
        private ConfigEntry<string> _cfgRemoveKeywords;
        private ConfigEntry<string> _cfgShiftKeywords;
        private ConfigEntry<string> _cfgPutOnKeywords;
        private ConfigEntry<string> _cfgRemoveAllKeywords;
        private ConfigEntry<string> _cfgPutOnAllKeywords;
        private ConfigEntry<string> _cfgCoordPattern;
        private ConfigEntry<string> _cfgCameraTriggerKeywords;
        private ConfigEntry<bool> _cfgEnableVideoPlaybackByResponseText;
        private ConfigEntry<string> _cfgVideoPlaybackTriggerKeywords;
        private ConfigEntry<bool> _cfgSequenceSubtitleEnabled;
        private ConfigEntry<string> _cfgSequenceSubtitleHost;
        private ConfigEntry<int> _cfgSequenceSubtitlePort;
        private ConfigEntry<string> _cfgSequenceSubtitleEndpointPath;
        private ConfigEntry<string> _cfgSequenceSubtitleDisplayMode;
        private ConfigEntry<string> _cfgSequenceSubtitleSendMode;
        private ConfigEntry<float> _cfgSequenceSubtitleHoldPaddingSeconds;
        private ConfigEntry<bool> _cfgSequenceSubtitleProgressPrefixEnabled;
        private ConfigEntry<bool> _cfgEnableFacePresetApply;
        private ConfigEntry<string> _cfgFacePresetJsonRelativePath;
        private ConfigEntry<bool> _cfgEnableSubCameraPresetForward;
        private ConfigEntry<bool> _cfgPoseChangeEnabled;
        private ConfigEntry<bool> _cfgPoseSimpleModeEnabled;
        private ConfigEntry<string> _cfgPoseSimpleModeTriggerKeywords;
        private ConfigEntry<bool> _cfgPoseRulesEnabled;
        private ConfigEntry<bool> _cfgPoseInferRulesEnabled;
        private ConfigEntry<bool> _cfgPoseCategoriesExpanded;
        private ConfigEntry<bool> _cfgPoseRulesExpanded;
        private ConfigEntry<bool> _cfgPoseInferRulesExpanded;
        private readonly Dictionary<string, ConfigEntry<bool>> _cfgPoseCategoryEnabledEntries = new Dictionary<string, ConfigEntry<bool>>(StringComparer.Ordinal);
        private readonly Dictionary<string, ConfigEntry<bool>> _cfgPoseRuleEnabledEntries = new Dictionary<string, ConfigEntry<bool>>(StringComparer.Ordinal);
        private readonly Dictionary<string, ConfigEntry<bool>> _cfgPoseInferRuleEnabledEntries = new Dictionary<string, ConfigEntry<bool>>(StringComparer.Ordinal);
        private readonly Dictionary<ConfigEntryBase, ConfigurationManager.ConfigurationManagerAttributes> _cfgPoseReadonlyAttributes =
            new Dictionary<ConfigEntryBase, ConfigurationManager.ConfigurationManagerAttributes>();
        private readonly Dictionary<string, List<string>> _poseNameAliasesByCanonical =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private bool _suppressConfigChangeEvent;
        private bool _suppressPoseConfigChangeEvent;

        private void Awake()
        {
            Instance = this;
            Logger = base.Logger;
            PluginDir = Path.GetDirectoryName(Info.Location);
            LogFilePath = Path.Combine(PluginDir, PluginName + ".log");

            Directory.CreateDirectory(PluginDir);
            File.WriteAllText(
                LogFilePath,
                $"[{DateTime.Now:HH:mm:ss}] === {PluginName} {Version} started ==={Environment.NewLine}",
                Utf8NoBom);

            Settings = SettingsStore.LoadOrCreate(PluginDir, Log, LogWarn, LogError);
            LoadScenarioTextRules();
            BindConfigEntries();
            ApplySettingsToConfigEntries(Settings);
            SaveConfigFile(reason: "awake");
            LoadPoseScoreRules();
            LoadPoseCategoryEntries();
            _externalVoicePlayer = new ExternalVoicePlayer(Log, LogWarn, LogError);
            _externalVoicePlayer.PlaybackStarted += OnExternalVoicePlaybackStarted;
            LogGuardSettings("awake");

            _harmony = new Harmony(GUID);
            _harmony.PatchAll(typeof(Plugin));
            VoiceGuardHooks.Apply(_harmony, Log, LogWarn, LogError);
            FacePresetProbeHooks.Apply(_harmony, Log, LogWarn, LogError);

            StartOrRestartPipeServer(forceRestart: true, reason: "awake");
            StartWatchdog();
            Log("awake complete");
        }

        private void StartWatchdog()
        {
        }

        private void OnDestroy()
        {
            StopPipeServer("on_destroy");
            ResetResponseTextQueue("on_destroy");

            try
            {
                _harmony?.UnpatchSelf();
                _harmony = null;
            }
            catch (Exception ex)
            {
                LogWarn("unpatch failed: " + ex.Message);
            }

            if (_externalVoicePlayer != null)
            {
                _externalVoicePlayer.PlaybackStarted -= OnExternalVoicePlaybackStarted;
                _externalVoicePlayer.Dispose();
                _externalVoicePlayer = null;
            }

            CurrentProc = null;
        }

        private float _updateProbeNext;
        private int _updateLastStep;
        private volatile int _mainThreadFrameCount;
        private float _nextStateDumpAllowedTime;
        private string _lastResponseTraceId = "(none)";
        private string _lastResponsePhase = "idle";
        private float _lastResponseStartedAt = -1f;
        private float _lastResponseElapsedMs = -1f;
        private int _lastResponseTextLength;

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            bool extPlaying = _externalVoicePlayer != null && _externalVoicePlayer.IsPlaying;
            if (extPlaying && now >= _updateProbeNext)
            {
                _updateProbeNext = now + 2f;
                if (Settings != null && Settings.VerboseLog)
                {
                    Log($"[update-probe] alive t={now:F1} lastStep={_updateLastStep}");
                }
                _updateLastStep = 0;
            }

            if (Settings != null && Settings.EnableCtrlRReload && IsReloadKeyDown())
            {
                ReloadSettings();
            }

            if (IsStateDumpKeyDown() && now >= _nextStateDumpAllowedTime)
            {
                _nextStateDumpAllowedTime = now + 0.5f;
                DumpRuntimeState("manual_hotkey");
            }

            _mainThreadFrameCount++;
            if (extPlaying) _updateLastStep = 1;
            bool wasExternalPlaying = _externalVoicePlayer != null && _externalVoicePlayer.IsPlaying;
            _externalVoicePlayer?.Update();
            if (extPlaying) _updateLastStep = 2;
            bool isExternalPlaying = _externalVoicePlayer != null && _externalVoicePlayer.IsPlaying;
            if (wasExternalPlaying && !isExternalPlaying)
            {
                _blockGameVoiceUntil = 0f;
                Log("[guard] external play finished -> clear block and try restore");
                RestoreVoiceProcStopIfNeeded();
            }

            if (!ShouldBlockGameVoiceEvents())
            {
                RestoreVoiceProcStopIfNeeded();
            }
            if (extPlaying) _updateLastStep = 3;

            if (Settings == null || !Settings.Enabled)
            {
                if (_responseTextCoroutine != null || _responseTextQueue.Count > 0)
                {
                    ResetResponseTextQueue("settings_disabled");
                }
                return;
            }

            if (extPlaying) _updateLastStep = 4;
            ProcessDelayedActions();
            if (extPlaying) _updateLastStep = 5;
            DrainIncomingCommands(8);
            PumpResponseTextQueue();
            if (extPlaying) _updateLastStep = 6;
            UpdateFacePresetProbe();
            TickScenarioTextSend(now);
            if (extPlaying) _updateLastStep = 7;
        }

        /// <summary>
        /// 外部プラグイン (PublicApi 経由) からの一時的音量ブースト要求を
        /// ExternalVoicePlayer に転送する。
        /// </summary>
        internal bool RequestExternalVolumeBoost(float peakMultiplier, float attackSec, float holdSec, float releaseSec)
        {
            return RequestExternalVolumeBoost(peakMultiplier, attackSec, holdSec, releaseSec, 0f, PublicApi.EasingShape.CosineInOut);
        }

        internal bool RequestExternalVolumeBoost(
            float peakMultiplier,
            float attackSec,
            float holdSec,
            float releaseSec,
            float silenceSec,
            PublicApi.EasingShape easing)
        {
            return RequestExternalVolumeBoost(
                peakMultiplier,
                attackSec,
                holdSec,
                releaseSec,
                silenceSec,
                0.08f,
                0.08f,
                easing);
        }

        internal bool RequestExternalVolumeBoost(
            float peakMultiplier,
            float attackSec,
            float holdSec,
            float releaseSec,
            float silenceSec,
            float silenceFadeOutSec,
            float silenceFadeInSec,
            PublicApi.EasingShape easing)
        {
            if (_externalVoicePlayer == null) return false;
            return _externalVoicePlayer.RequestTransientBoost(
                peakMultiplier,
                attackSec,
                holdSec,
                releaseSec,
                silenceSec,
                silenceFadeOutSec,
                silenceFadeInSec,
                easing);
        }




    }
}
