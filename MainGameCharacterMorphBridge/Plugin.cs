using System;
using System.Collections;
using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace MainGameCharacterMorphBridge
{
    [BepInPlugin(Guid, PluginName, Version)]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.kks.maingame.charactermorphbridge";
        public const string PluginName = "MainGameCharacterMorphBridge";
        public const string Version = "0.1.0";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        internal static Plugin Instance { get; private set; }

        private string _pluginDir;
        private string _logPath;
        private Harmony _harmony;
        private PluginSettings _settings;
        private HSceneProc _currentHScene;
        private MorphSnapshot _originalSnapshot;
        private MorphSnapshot _targetSnapshot;
        private int _originalFemaleIndex = -1;
        private int _targetFemaleIndex = -1;
        private string _targetCardPath = string.Empty;

        private ConfigEntry<bool> _cfgEnabled;
        private ConfigEntry<bool> _cfgEnableLogs;
        private ConfigEntry<bool> _cfgAutoCaptureOriginal;
        private ConfigEntry<bool> _cfgAutoLoadOnHSceneStart;
        private ConfigEntry<bool> _cfgAutoResetBlendOnHSceneStart;
        private ConfigEntry<int> _cfgTargetFemaleIndex;
        private ConfigEntry<string> _cfgSelectedCardWord;
        private ConfigEntry<string> _cfgSelectedCardTriggerWords;
        private ConfigEntry<string> _cfgTargetCardPath;
        private ConfigEntry<bool> _cfgUpsertRegisteredCardButton;
        private ConfigEntry<bool> _cfgCaptureOriginalButton;
        private ConfigEntry<bool> _cfgLoadTargetCardButton;
        private ConfigEntry<bool> _cfgResetOriginalButton;
        private ConfigEntry<float> _cfgBlend;
        private ConfigEntry<float> _cfgHeight;
        private ConfigEntry<float> _cfgBreast;
        private ConfigEntry<int> _cfgBodyShapeIndex;
        private ConfigEntry<float> _cfgBodyShapeValue;
        private ConfigEntry<int> _cfgFaceShapeIndex;
        private ConfigEntry<float> _cfgFaceShapeValue;

        private void Awake()
        {
            Instance = this;
            _pluginDir = Path.GetDirectoryName(Info.Location) ?? string.Empty;
            _logPath = Path.Combine(_pluginDir, PluginName + ".log");
            _settings = SettingsStore.LoadOrCreate(_pluginDir, null, LogWarnAlways, LogErrorAlways);

            BindConfig();

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(HSceneHooks));
            LogExecution("plugin loaded");
        }

        private void OnDisable()
        {
            SaveSettings("plugin disabled");
        }

        private void OnDestroy()
        {
            try
            {
                _harmony?.UnpatchSelf();
                _harmony = null;
                SaveSettings("plugin destroyed");
                LogExecution("plugin destroyed");
            }
            finally
            {
                if (ReferenceEquals(Instance, this))
                    Instance = null;
            }
        }

        private void BindConfig()
        {
            _cfgEnabled = Config.Bind(
                "00 General",
                "00 Enable",
                _settings.Enabled,
                BuildConfigDescription("プラグインの有効/無効。OFFにした時点でJSONへ保存します。", 1000));
            _cfgEnabled.SettingChanged += OnEnabledChanged;

            _cfgEnableLogs = Config.Bind(
                "00 General",
                "01 EnableLogs",
                _settings.EnableLogs,
                BuildConfigDescription("分岐と実行時だけログを出します。毎フレームログは出しません。", 990));
            _cfgEnableLogs.SettingChanged += OnEnableLogsChanged;

            _cfgTargetFemaleIndex = Config.Bind(
                "10 Target",
                "00 Target Female Index",
                _settings.TargetFemaleIndex,
                BuildIntSliderDescription("Hシーン女性Index。通常は0、3P時の2人目は1。", 900, 0, 1, DrawTargetFemaleIndexSlider));

            _cfgAutoLoadOnHSceneStart = Config.Bind(
                "10 Target",
                "01 Auto Load On HScene Start",
                _settings.AutoLoadOnHSceneStart,
                BuildConfigDescription("Hシーン開始時に元キャラ数値と補間先カード数値を自動取得します。", 895));
            _cfgAutoLoadOnHSceneStart.SettingChanged += (_, __) => _settings.AutoLoadOnHSceneStart = _cfgAutoLoadOnHSceneStart.Value;

            _cfgAutoCaptureOriginal = Config.Bind(
                "10 Target",
                "02 Auto Capture Original",
                _settings.AutoCaptureOriginal,
                BuildConfigDescription("カード数値を読み込む前に、現在の元キャラ数値を自動保存します。", 890));
            _cfgAutoCaptureOriginal.SettingChanged += (_, __) => _settings.AutoCaptureOriginal = _cfgAutoCaptureOriginal.Value;

            _cfgAutoResetBlendOnHSceneStart = Config.Bind(
                "10 Target",
                "03 Auto Reset Blend On HScene Start",
                _settings.AutoResetBlendOnHSceneStart,
                BuildConfigDescription("Hシーン開始時の自動取得後、Blendを0に戻して元キャラ状態で待機します。", 885));
            _cfgAutoResetBlendOnHSceneStart.SettingChanged += (_, __) => _settings.AutoResetBlendOnHSceneStart = _cfgAutoResetBlendOnHSceneStart.Value;

            _cfgSelectedCardWord = Config.Bind(
                "10 Target",
                "04 Selected Card Word",
                _settings.SelectedCardWord ?? string.Empty,
                BuildConfigDescription("登録カードの呼び名。例: ルナ。", 882));
            _cfgSelectedCardWord.SettingChanged += (_, __) => _settings.SelectedCardWord = (_cfgSelectedCardWord.Value ?? string.Empty).Trim();

            _cfgTargetCardPath = Config.Bind(
                "10 Target",
                "05 Target Card Path",
                _settings.TargetCardPath ?? string.Empty,
                BuildConfigDescription("補間先キャラカードPNGのフルパス。入力途中では読み込みません。", 880));
            _cfgTargetCardPath.SettingChanged += (_, __) => _settings.TargetCardPath = PluginSettings.NormalizePath(_cfgTargetCardPath.Value);

            _cfgSelectedCardTriggerWords = Config.Bind(
                "10 Target",
                "06 Selected Card Trigger Words",
                _settings.SelectedCardTriggerWords ?? string.Empty,
                BuildConfigDescription("登録カードに紐づく追加ワード。カンマ区切り。例: ルナ,白峰ルナ。", 875));
            _cfgSelectedCardTriggerWords.SettingChanged += (_, __) => _settings.SelectedCardTriggerWords = PluginSettings.NormalizeCsv(_cfgSelectedCardTriggerWords.Value);

            _cfgUpsertRegisteredCardButton = Config.Bind(
                "10 Target",
                "07 Register Or Update Selected Card",
                false,
                BuildButtonDescription("選択カードを登録/更新", 872, _ => UpsertSelectedCardFromConfig()));

            _cfgCaptureOriginalButton = Config.Bind(
                "10 Target",
                "08 Capture Original",
                false,
                BuildButtonDescription("現在の元キャラ数値を記憶", 870, _ => CaptureOriginalFromConfig()));

            _cfgLoadTargetCardButton = Config.Bind(
                "10 Target",
                "09 Load Target Card Values",
                false,
                BuildButtonDescription("カード数値を読み込む", 860, _ => LoadTargetCardFromConfig()));

            _cfgResetOriginalButton = Config.Bind(
                "10 Target",
                "10 Reset To Original",
                false,
                BuildButtonDescription("元キャラ数値へ戻す", 850, _ => ResetToOriginalFromConfig()));

            _cfgBlend = Config.Bind(
                "20 Blend",
                "00 Blend",
                _settings.Blend,
                BuildFloatSliderDescription("0=元キャラ、1=指定カード。ドラッグ中も反映し、ログは離した時だけ出します。", 800, 0f, 1f, "0.00", DrawBlendSlider));

            _cfgHeight = Config.Bind(
                "30 Direct Body",
                "00 Height",
                _settings.Height,
                BuildFloatSliderDescription("shapeValueBody[0] 身長。ドラッグ中もheightとMotionIKを更新します。", 700, 0f, 1f, "0.00", DrawHeightSlider));

            _cfgBreast = Config.Bind(
                "30 Direct Body",
                "01 Breast",
                _settings.Breast,
                BuildFloatSliderDescription("shapeValueBody[4] 胸サイズ。ドラッグ中もBreastとMotionIKを更新します。", 690, 0f, 1f, "0.00", DrawBreastSlider));

            _cfgBodyShapeIndex = Config.Bind(
                "30 Direct Body",
                "10 Body Shape Index",
                _settings.BodyShapeIndex,
                BuildIntSliderDescription("直接変更するbody shape index。0..43。", 680, 0, 43, DrawBodyShapeIndexSlider));

            _cfgBodyShapeValue = Config.Bind(
                "30 Direct Body",
                "11 Body Shape Value",
                _settings.BodyShapeValue,
                BuildFloatSliderDescription("選択中body shape indexへドラッグ中も反映します。", 670, 0f, 1f, "0.00", DrawBodyShapeValueSlider));

            _cfgFaceShapeIndex = Config.Bind(
                "40 Direct Face",
                "00 Face Shape Index",
                _settings.FaceShapeIndex,
                BuildIntSliderDescription("直接変更するface shape index。0..51。", 600, 0, 51, DrawFaceShapeIndexSlider));

            _cfgFaceShapeValue = Config.Bind(
                "40 Direct Face",
                "01 Face Shape Value",
                _settings.FaceShapeValue,
                BuildFloatSliderDescription("選択中face shape indexへドラッグ中も反映します。", 590, 0f, 1f, "0.00", DrawFaceShapeValueSlider));
        }

        private ConfigDescription BuildConfigDescription(string description, int order)
        {
            return new ConfigDescription(
                description,
                null,
                new ConfigurationManager.ConfigurationManagerAttributes
                {
                    Order = order
                });
        }

        private void OnEnabledChanged(object sender, EventArgs e)
        {
            _settings.Enabled = _cfgEnabled.Value;
            LogExecution("Enable changed: " + _settings.Enabled);
            if (!_settings.Enabled)
                SaveSettings("disabled by config");
        }

        private void OnEnableLogsChanged(object sender, EventArgs e)
        {
            _settings.EnableLogs = _cfgEnableLogs.Value;
            LogExecution("EnableLogs changed: " + _settings.EnableLogs);
        }

        private bool IsEnabled()
        {
            return _settings != null && _settings.Enabled;
        }

        private int TargetFemaleIndex()
        {
            return PluginSettings.ClampInt(_settings.TargetFemaleIndex, 0, 1);
        }

        private void SaveSettings(string reason)
        {
            if (_settings == null || string.IsNullOrEmpty(_pluginDir))
                return;

            SettingsStore.Save(_pluginDir, _settings, LogWarnAlways);
            LogExecution("settings saved: " + reason);
        }

        internal void SetCurrentHScene(HSceneProc proc)
        {
            if (proc == null || ReferenceEquals(_currentHScene, proc))
                return;

            _currentHScene = proc;
            _originalSnapshot = null;
            _targetSnapshot = null;
            _originalFemaleIndex = -1;
            _targetFemaleIndex = -1;
            ResetAnimationObservation();
            ClearDirectOverrideState();
            LogExecution("HScene detected");
            StartHSceneAutoInitialize(proc);
        }

        internal void ClearCurrentHScene(HSceneProc proc)
        {
            if (!ReferenceEquals(_currentHScene, proc))
                return;

            _currentHScene = null;
            _originalSnapshot = null;
            _targetSnapshot = null;
            _originalFemaleIndex = -1;
            _targetFemaleIndex = -1;
            StopHSceneAutoInitialize();
            StopBlendTransition();
            StopPostMotionReapply();
            ResetAnimationObservation();
            ClearDirectOverrideState();
            LogExecution("HScene cleared");
        }

        internal void LogExecution(string message)
        {
            if (_settings == null || !_settings.EnableLogs)
                return;

            string line = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + message;
            Logger.LogInfo("[" + PluginName + "] " + message);
            try
            {
                File.AppendAllText(_logPath, line + Environment.NewLine, Utf8NoBom);
            }
            catch
            {
            }
        }

        internal void LogWarnAlways(string message)
        {
            Logger.LogWarning("[" + PluginName + "] " + message);
        }

        internal void LogErrorAlways(string message)
        {
            Logger.LogError("[" + PluginName + "] " + message);
        }

        private static class HSceneHooks
        {
            [HarmonyPatch(typeof(HSceneProc), "Start")]
            [HarmonyPostfix]
            private static void StartPostfix(HSceneProc __instance)
            {
                Instance?.SetCurrentHScene(__instance);
            }

            [HarmonyPatch(typeof(HSceneProc), "OnDestroy")]
            [HarmonyPrefix]
            private static void OnDestroyPrefix(HSceneProc __instance)
            {
                Instance?.ClearCurrentHScene(__instance);
            }
        }
    }
}
