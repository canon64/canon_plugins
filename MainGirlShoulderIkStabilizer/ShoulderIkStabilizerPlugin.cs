using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using MainGameLogRelay;
using RootMotion.FinalIK;
using UnityEngine;
using Hip = MainGirlHipHijack.Plugin;
using SessionGate = CanonPlugins.Local.SessionGate;

namespace MainGirlShoulderIkStabilizer
{
    [BepInDependency("canon.gate", "2.2.0")]
    [BepInPlugin(Guid, PluginName, Version)]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    [BepInDependency(MainGameLogRelay.Plugin.Guid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.kks.main.girlbodyikgizmo", "1.0.0")]
    public sealed partial class ShoulderIkStabilizerPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.kks.main.girlshoulderikstabilizer";
        public const string PluginName = "MainGirlShoulderIkStabilizer";
        public const string Version = "1.1.7";
        internal static ShoulderIkStabilizerPlugin Instance { get; private set; }
        private PluginSettings settings;
        private ShoulderRotator rotator;
        private FullBodyBipedIK currentIk;
        private Harmony harmony;
        private bool hipEnabled = true, session, poseSubscribed, diagnosticPoseSubscribed, relayEnabled;
        private readonly List<Action> unbindConfig = new List<Action>();

        private void Awake()
        {
            Instance = this;
            // Open diagnostics before settings I/O, compatibility patches or gate registration.
            // Otherwise an exception in any of those paths leaves no evidence in the file.
            settings = new PluginSettings();
            settings.ShoulderDiagnosticLog = Config.Bind("Logging", "ShoulderDiagnosticLog", false,
                "実骨の角度・初期化段階・例外を専用log/ShoulderPoseDiagnostic.logへ記録。Relay全体OFFでも出力。").Value;
            DiagnosticStartup.Run(Initialize, () => settings.ShoulderDiagnosticLog, WriteDiagnostic);
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void Initialize()
        {
            LogRelayApi.SetOwnerLogKey(Guid, "main/" + PluginName);
            bool diagnosticEnabled = settings.ShoulderDiagnosticLog;
            settings = SettingsStore.LoadOrCreate(Path.GetDirectoryName(Info.Location), LogInfo, LogWarn, LogError);
            settings.ShoulderDiagnosticLog = diagnosticEnabled;
            WriteDiagnostic("BOOT stage=settings_loaded");
            BindSettings();
            WriteDiagnostic("BOOT stage=config_bound");
            harmony = new Harmony(Guid);
            harmony.Patch(AccessTools.Method(typeof(ChaControl), "InitializeExpression"),
                postfix: new HarmonyMethod(typeof(ShoulderIkStabilizerPlugin), nameof(NativeExpressionInitialized)));
            var adv = AccessTools.TypeByName("AdvIKPlugin.AdvIKShoulderRotator");
            var method = adv == null ? null : AccessTools.Method(adv, "RotateShoulder");
            if (method != null)
            {
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(ShoulderIkStabilizerPlugin), nameof(BeforeAdvShoulder)));
                advShoulderHooked = true;
            }
            WriteDiagnostic("BOOT stage=patches_ready");
            SessionGate.Bind(this, Guid, BeginGateSession, EndGateSession);
            WriteDiagnostic("BOOT stage=gate_registered waiting_for_permitted_H=true");
            ReportDiagnosticState("startup");
            LogInfo("肩補正 " + Version + ": 上腕挙上・手先保持・IK最外周合成を登録");
        }
        public static bool SetEnabledFromHipHijack(bool enabled, string reason = null)
        {
            var p = Instance;
            if (p == null) return false;
            p.hipEnabled = enabled;
            p.RefreshState();
            p.ReportDiagnosticState("hip_toggle:" + reason);
            return true;
        }
        private static bool BeforeAdvShoulder(Component __instance, FullBodyBipedChain chain)
        {
            var p = Instance;
            return p == null || !SessionGate.Allowed || p.rotator == null || !p.rotator.Owns(__instance, (int)chain);
        }
        private void OnRuntimeChanged(ChaControl actor, FullBodyBipedIK ik)
        {
            if (!session || !SessionGate.Allowed) return;
            if (ReferenceEquals(currentIk, ik) && rotator != null) return;
            UnsubscribePose();
            rotator?.Dispose(); rotator = null; currentIk = ik;
            diagnosticActor = actor;
            BindNativeCorrection();
            RefreshNativeDiagnostics();
            if (actor != null && ik != null && ik.solver != null)
            {
                rotator = new ShoulderRotator(ik, settings, WriteDiagnostic);
                if (settings.ShoulderDiagnosticLog) WriteDiagnostic("RUNTIME actor=" + actor.GetInstanceID() + " solver=" + ik.GetInstanceID());
                LogInfo("肩補正接続: actor=" + actor.GetInstanceID() + " solver=" + ik.GetInstanceID());
            }
            RefreshState();
            ReportDiagnosticState("runtime_changed");
        }
        private void OnArmChanged(int side, bool running) { RefreshState(); ReportDiagnosticState("arm_changed:" + side + ":" + running); }
        private bool NeedsPoseCallbacks => rotator != null && (rotator.NeedsProcessing ||
            settings.ShoulderDiagnosticLog && (Hip.IsArmIkRunning(0) || Hip.IsArmIkRunning(1)));
        private void RefreshState()
        {
            if (!session || !SessionGate.Allowed || rotator == null) return;
            rotator.SetState(settings.Enabled && settings.ShoulderRotationEnabled && hipEnabled,
                Hip.IsArmIkRunning(0), Hip.IsArmIkRunning(1));
            if (!settings.Enabled || !settings.ShoulderRotationEnabled || !settings.NativeAuxiliaryCorrection || !hipEnabled ||
                !Hip.IsArmIkRunning(0) && !Hip.IsArmIkRunning(1)) nativeCorrection?.Restore();
            if (NeedsPoseCallbacks && !poseSubscribed)
            {
                Hip.ShoulderPosePreparing += PreparePose;
                Hip.ShoulderPoseFinalizing += FinishPose;
                poseSubscribed = true;
            }
            else if (!NeedsPoseCallbacks && poseSubscribed) { rotator.Restore(); UnsubscribePose(); }
            bool observe = poseSubscribed && settings.ShoulderDiagnosticLog;
            if (observe && !diagnosticPoseSubscribed)
            {
                Hip.ShoulderPoseSolved += CaptureSolvedPose;
                Hip.ShoulderPoseCompleted += CaptureCompletedPose;
                diagnosticPoseSubscribed = true;
            }
            else if (!observe && diagnosticPoseSubscribed)
            {
                Hip.ShoulderPoseSolved -= CaptureSolvedPose;
                Hip.ShoulderPoseCompleted -= CaptureCompletedPose;
                diagnosticPoseSubscribed = false;
            }
        }
        private void PreparePose(ChaControl actor, FullBodyBipedIK ik)
        {
            if (ReferenceEquals(currentIk, ik)) nativeCorrection?.Restore();
            if (ReferenceEquals(currentIk, ik)) rotator?.Restore();
            if (ReferenceEquals(currentIk, ik)) RecordNativeOrder("ik_prepared");
        }
        private void FinishPose(ChaControl actor, FullBodyBipedIK ik, float leftConstraint, float rightConstraint)
        {
            if (!SessionGate.Allowed || !ReferenceEquals(currentIk, ik) || rotator == null) return;
            RecordNativeOrder("auto_before");
            rotator.Apply(leftConstraint, rightConstraint);
            RecordNativeOrder("auto_after");
            ApplyNativeCorrection();
            if (!NeedsPoseCallbacks) { rotator.CompleteDiagnosticPose(); UnsubscribePose(); }
        }
        private void UnsubscribePose()
        {
            if (!poseSubscribed) return;
            Hip.ShoulderPosePreparing -= PreparePose;
            Hip.ShoulderPoseSolved -= CaptureSolvedPose;
            Hip.ShoulderPoseFinalizing -= FinishPose;
            Hip.ShoulderPoseCompleted -= CaptureCompletedPose;
            poseSubscribed = false;
            diagnosticPoseSubscribed = false;
        }
        private void Bind<T>(string section, string key, T initial, string description, Action<T> assign,
            AcceptableValueBase range = null)
        {
            var entry = Config.Bind(section, key, initial, new ConfigDescription(description, range));
            assign(entry.Value);
            EventHandler handler = (sender, args) => { assign(entry.Value); SettingsStore.Normalize(settings); RefreshState(); ReportDiagnosticState("config:" + section + "." + key); };
            entry.SettingChanged += handler;
            unbindConfig.Add(() => entry.SettingChanged -= handler);
        }
        private void Number(string section, string key, float initial, string description, float min, float max, Action<float> assign)
            => Bind(section, key, initial, description, assign, new AcceptableValueRange<float>(min, max));
        private void BindSettings()
        {
            Bind("General", "Enabled", settings.Enabled, "プラグイン全体のON/OFF。終了時は補正を滑らかに戻す。", v => settings.Enabled = v);
            Bind("General", "VerboseLog", settings.VerboseLog, "詳細ログ。", v => settings.VerboseLog = v);
            Bind("General", "ShoulderRotationEnabled", settings.ShoulderRotationEnabled, "腕IKに連動する肩補正。", v => settings.ShoulderRotationEnabled = v);
            Bind("Shoulder", "NativeAuxiliaryCorrection", settings.NativeAuxiliaryCorrection,
                "腕IKの最終姿勢からKKS本来の肩・上腕補助骨を再計算。元のExpressionBone設定は保持。", v => settings.NativeAuxiliaryCorrection = v);
            Bind("Logging", "EnableLogs", false, "MainGameLogRelay経由ログ。", v => { relayEnabled = v; ApplyLogging(); });
            Bind("Logging", "ShoulderDiagnosticLog", settings.ShoulderDiagnosticLog, "実骨の角度・初期化段階・例外を専用log/ShoulderPoseDiagnostic.logへ記録。Relay全体OFFでも出力。", v => {
                settings.ShoulderDiagnosticLog = v;
                if (!v) { diagnosticWriter = null; diagnosticIdentityWritten = false; rotator?.CancelDiagnosticPose(); }
                Hip.ShoulderPoseFaulted -= OnShoulderPoseFaulted;
                if (v && session) Hip.ShoulderPoseFaulted += OnShoulderPoseFaulted;
                RefreshNativeDiagnostics();
            });
            Number("Logging", "ShoulderDiagnosticLogInterval", settings.ShoulderDiagnosticLogInterval, "診断ログの最小間隔（秒）。", .05f, 2f, v => { settings.ShoulderDiagnosticLogInterval = v; RefreshNativeDiagnostics(); });
            Bind("Shoulder", "IndependentShoulders", settings.IndependentShoulders, "左右のリーチ補助と挙上補正を別々に調整。", v => settings.IndependentShoulders = v);
            Bind("Shoulder", "ReverseShoulderL", settings.ReverseShoulderL, "腕下げ時の左リーチ補助を反転。", v => settings.ReverseShoulderL = v);
            Bind("Shoulder", "ReverseShoulderR", settings.ReverseShoulderR, "腕下げ時の右リーチ補助を反転。", v => settings.ReverseShoulderR = v);
            Number("Shoulder", "ShoulderWeight", settings.ShoulderWeight, "左肩のリーチ補助強度。", 0f, 5f, v => settings.ShoulderWeight = v);
            Number("Shoulder", "ShoulderRightWeight", settings.ShoulderRightWeight, "右肩のリーチ補助強度。", 0f, 5f, v => settings.ShoulderRightWeight = v);
            Number("Shoulder", "ShoulderOffset", settings.ShoulderOffset, "左肩のリーチ補助が働く閾値。", -1f, 1f, v => settings.ShoulderOffset = v);
            Number("Shoulder", "ShoulderRightOffset", settings.ShoulderRightOffset, "右肩のリーチ補助が働く閾値。", -1f, 1f, v => settings.ShoulderRightOffset = v);
            Number("ArmState", "LoweredArmScale", settings.LoweredArmScale, "上腕が水平より下にある時のリーチ補助倍率。", 0f, 1f, v => settings.LoweredArmScale = v);
            Bind("ArmState", "RaisedArmStartY", settings.RaisedArmStartY, "旧設定の互換読込専用。挙上角には使用しない。", v => settings.RaisedArmStartY = v);
            Bind("ArmState", "RaisedArmFullY", settings.RaisedArmFullY, "旧設定の互換読込専用。挙上角には使用しない。", v => settings.RaisedArmFullY = v);
            Bind("ArmState", "RaisedArmScaleMin", settings.RaisedArmScaleMin, "旧設定の互換読込専用。挙上補正には使用しない。", v => settings.RaisedArmScaleMin = v);
            Number("Elevation", "Weight", settings.ElevationWeight, "左肩の腕上げ追従強度。左右独立OFFでは両肩。", 0f, 1f, v => settings.ElevationWeight = v);
            Number("Elevation", "RightWeight", settings.ElevationRightWeight, "右肩の腕上げ追従強度。", 0f, 1f, v => settings.ElevationRightWeight = v);
            Number("Elevation", "StartAngle", settings.ElevationStartAngle, "腕を下げた姿勢を0度とする補正開始角度。", 0f, 160f, v => settings.ElevationStartAngle = v);
            Number("Elevation", "FullAngle", settings.ElevationFullAngle, "持ち上げ量が最大になる上腕角度。", 1f, 180f, v => settings.ElevationFullAngle = v);
            Number("Elevation", "MaxLift", settings.ElevationMaxLift, "初期姿勢からの肩の持ち上げ量（度）。", 0f, 60f, v => settings.ElevationMaxLift = v);
            Number("Elevation", "MaxForward", settings.ElevationMaxForward, "肘を上げて前へ出す時の肩の前方追従上限（度）。0で前方補正OFF。", 0f, 60f, v => settings.ElevationMaxForward = v);
            Number("Elevation", "RightMaxForward", settings.ElevationRightMaxForward, "左右独立時の右肩の前方追従上限（度）。", 0f, 60f, v => settings.ElevationRightMaxForward = v);
            Number("Elevation", "RightStartAngle", settings.ElevationRightStartAngle, "左右独立時の右肩の補正開始角度。", 0f, 160f, v => settings.ElevationRightStartAngle = v);
            Number("Elevation", "RightFullAngle", settings.ElevationRightFullAngle, "左右独立時の右肩の最大到達角度。", 1f, 180f, v => settings.ElevationRightFullAngle = v);
            Number("Elevation", "RightMaxLift", settings.ElevationRightMaxLift, "左右独立時の右肩の持ち上げ量（度）。", 0f, 60f, v => settings.ElevationRightMaxLift = v);
            Number("Motion", "TransitionSeconds", settings.TransitionSeconds, "補正と肩操作の受け渡し時間（秒）。", .05f, 1f, v => settings.TransitionSeconds = v);
            Number("Motion", "MaxAngularSpeed", settings.MaxAngularSpeed, "追加補正の角速度上限（度/秒）。", 10f, 360f, v => settings.MaxAngularSpeed = v);
            Number("Safety", "MaxShoulderDeltaAngleDeg", settings.MaxShoulderDeltaAngleDeg, "追加する肩回転の上限（度）。", 0f, 180f, v => settings.MaxShoulderDeltaAngleDeg = v);
            Number("Safety", "MaxSolverBlend", settings.MaxSolverBlend, "補正の最大ブレンド。", 0f, 1f, v => settings.MaxSolverBlend = v);
            SettingsStore.Normalize(settings);
        }
        private void ApplyLogging()
        {
            if (!LogRelayApi.IsAvailable) return;
            LogRelayApi.SetOwnerLogKey(Guid, "main/" + PluginName);
            LogRelayApi.SetOwnerEnabled(Guid, relayEnabled);
        }
        private void LogInfo(string s) { if (LogRelayApi.IsAvailable) LogRelayApi.Info(Guid, s); else Logger.LogInfo(s); }
        private void LogWarn(string s) { if (LogRelayApi.IsAvailable) LogRelayApi.Warn(Guid, s); else Logger.LogWarning(s); }
        private void LogError(string s) { if (LogRelayApi.IsAvailable) LogRelayApi.Error(Guid, s); else Logger.LogError(s); }
        private void OnDestroy()
        {
            SessionGate.Dispose(); EndGateSession();
            foreach (var unbind in unbindConfig) unbind();
            harmony?.UnpatchSelf(); Instance = null;
        }
        private void OnDisable() => EndGateSession();
    }
}
