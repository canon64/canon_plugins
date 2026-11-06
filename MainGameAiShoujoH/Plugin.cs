using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using MainGameLogRelay;
using CanonGate;
using MainGameAiShoujoH.Contracts;
using MainGameAiShoujoH.Session;
using MainGameNotifications;

namespace MainGameAiShoujoH
{
    [BepInPlugin(PluginGuid, "AI少女専用H", "0.1.0")]
    [BepInDependency(GateApi.PluginGuid)]
    [BepInDependency(NotificationApi.PluginGuid)]
    [BepInDependency(MainGameUiInputCapture.Plugin.Guid)]
    [BepInDependency(MainGameLogRelay.Plugin.Guid)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.kks.maingame.aishoujoh";
        GateApi.ActivityRegistration _registration;
        GateApi.ActivityPermit _permit;
        bool _active;
        ConfigEntry<bool> _diagnostics;
        DedicatedSession _session;
        Integration.NativeResidentProfileStore _profiles;
        readonly Integration.KksOnePoseRequestScope _kksTrial = new Integration.KksOnePoseRequestScope();
        internal static Plugin Instance;
        void Awake()
        {
            Instance = this;
            _diagnostics = Config.Bind("Diagnostics", "Enabled", false, "専用Hの開始と準備失敗の詳細を共通ログへ記録します。OFF時は診断用収集を行いません。");
            _diagnostics.SettingChanged += DiagnosticsChanged;
            DiagnosticsChanged(this, EventArgs.Empty);
            _profiles = new Integration.NativeResidentProfileStore(Path.Combine(Paths.GameRootPath, "UserData", "AIShoujoMode", "DedicatedH", "Residents"),
                () => Integration.NativeResidentProfileStore.LoadDefaults(Path.Combine(Path.GetDirectoryName(Info.Location), "Content", "HAudio", "default-profile.json")));
            _session = new DedicatedSession(() => _active && _permit.IsValid, CreateModules, Notify, TraceException);
            _registration = GateApi.RegisterActivity(PluginGuid, GateApi.ActivityKind.AiShoujoMode, StartActivity, StopActivity, false);
        }
        void StartActivity() { _permit = _registration.Capture(); _active = true; }
        void StopActivity() { _active = false; _session?.End(EndReason.PermissionRevoked); }
        ISessionModule[] CreateModules(StartRequest request)
        {
            var primaryProfile = _profiles.ReadOrCreate(request.Participants[0].Identity);
            if (request.Settings == null) request.Settings = primaryProfile.Settings;
            request.Settings.HentaiUnlock0 |= primaryProfile.HasAdditionalMotion0;
            request.Settings.HentaiUnlock1 |= primaryProfile.HasAdditionalMotion1;
            request.Settings.Validate();
            if (request.VoiceContexts == null)
                request.VoiceContexts = request.Participants.Where(p => p.Role != "male").Select((p, slot) => _profiles.ReadOrCreate(p.Identity).Context(slot)).ToArray();
            foreach (var voice in request.VoiceContexts)
            {
                voice.NormalStartEvent = request.EventId == (int)NativeHEvent.Normal || request.EventId == (int)NativeHEvent.GyakuYobai || request.EventId == (int)NativeHEvent.FromFemale;
                voice.Yobai = request.EventId == (int)NativeHEvent.Yobai;
                voice.Place = request.NativePlace;
                voice.Force = request.Settings.Force;
            }
            Integration.NativeMerchantAccess.Validate(request, request.MotionId);
            var nativeRuntime = new Integration.NativeKksSessionRuntime(Path.Combine(Path.GetDirectoryName(Info.Location), "Content", "HKks", "serialized-defaults.json"));
            IMotionModule motion = new Integration.NativeKksOwnedScene(nativeRuntime, _registration.Id, reason => _session.End(reason), Path.Combine(Path.GetDirectoryName(Info.Location), "Content", "HKks", "native-engine-policy.json"));
            var presentation = new Presentation.PresentationModule(Path.Combine(Path.GetDirectoryName(Info.Location), "Content", "HAudio"), nativeRuntime);
            var results = new Results.ResultModule(request.Results);
            var progression = new Progression.ProgressionModule(motion, presentation, results, nativeRuntime);
            var ending = new Integration.EndingModule(Path.Combine(Path.GetDirectoryName(Info.Location), "Content", "HAdv"), motion, presentation, progression,
                () => _active && _permit.IsValid && _session.Phase == SessionPhase.Restoring, nativeRuntime);
            return new ISessionModule[] { nativeRuntime, motion, presentation, results, progression, ending };
        }
        void Notify(string body) => NotificationApi.Publish(PluginGuid, "AI専用Hの一部失敗", body, NotificationSeverity.Failure);
        void DiagnosticsChanged(object sender, EventArgs args)
        {
            LogRelayApi.SetOwnerEnabled(PluginGuid, _diagnostics.Value);
            if (_diagnostics.Value) LogRelayApi.SetOwnerOutputMode(PluginGuid, LogRelayOutputMode.FileOnly);
        }
        void TraceException(Exception exception)
        {
            if (_diagnostics?.Value != true) return;
            Logger.LogError(exception);
            LogRelayApi.LogLazy(PluginGuid, LogRelayLevel.Error, () => exception.ToString());
        }
        void OnDisable() { _active = false; _session?.End(EndReason.PermissionRevoked); }
        void OnDestroy() { _active = false; _session?.Dispose(); _registration?.Dispose(); if (_diagnostics != null) _diagnostics.SettingChanged -= DiagnosticsChanged; LogRelayApi.ClearOwnerRuntimeOverrides(PluginGuid); if (ReferenceEquals(Instance, this)) Instance = null; }
        public static bool Busy => Instance?._session?.Busy == true;
        public static SessionSnapshot ReadState() => Instance?._session?.Snapshot();
        public static void Begin(StartRequest request)
        {
            var plugin = Instance;
            if (plugin == null) throw new InvalidOperationException("AI専用Hプラグインが読み込まれていません。");
            if (!plugin._active || !plugin._permit.IsValid)
                throw new InvalidOperationException("AI専用Hの中央許可がありません。許可を変更した場合は、次のAIモード開始時に適用されます。");
            var scope = GateApi.AiShoujoMode;
            if (request == null || scope == null || !ReferenceEquals(scope.Owner, request.Owner) || !ReferenceEquals(plugin._registration.Scene, request.Owner)
                || request.Participants == null || request.Participants.Length == 0 || !ReferenceEquals(scope.Actor, request.Participants[0].Character))
                throw new InvalidOperationException("専用Hは中央AI所有者と、その所有者の現参加住人からだけ開始できます。");
            request.MotionId = Progression.NativeProcCatalog.CanonicalId(request.MotionId);
            if (plugin._kksTrial.Capture(request.Owner, request.MotionId)) request.UseKksOnePoseTrial = true;
            else if (request.UseKksOnePoseTrial)
                throw new InvalidOperationException("KKS一件試験の明示要求scopeがありません。");
            // The dedicated session now owns a native KKS body backend. Keep its
            // table identity separate from the AI result/conversation meaning.
            request.KksPose = request.KksPose ?? Presentation.KksNativePoseDefinition.DefaultIdentity();
            Presentation.KksNativePoseDefinition.ValidateIdentity(request.KksPose);
            request.MotionId = Presentation.KksNativePoseDefinition.ProgressionId(request.KksPose);
            request.UseKksOnePoseTrial = true;
            if (plugin._diagnostics.Value)
            {
                var message = "Begin backend=KKS table=" + request.KksPose.TableBundle + "/" + request.KksPose.TableAsset + " nativeId=" + request.KksPose.Id + " resultMeaning=" + request.MotionId + " owner=" + request.Owner.GetInstanceID();
                plugin.Logger.LogInfo(message); LogRelayApi.Info(PluginGuid, message);
            }
            plugin._session.Begin(request);
        }
        public static void RunKksOnePoseTrial(UnityEngine.MonoBehaviour owner, Action beginNormalRequest)
        {
            var plugin = Instance;
            if (plugin == null || !plugin._active || !plugin._permit.IsValid)
                throw new InvalidOperationException("AI専用Hの中央許可がありません。");
            var scope = GateApi.AiShoujoMode;
            if (owner == null || beginNormalRequest == null || scope == null || !ReferenceEquals(scope.Owner, owner)
                || !ReferenceEquals(plugin._registration.Scene, owner) || Busy || plugin._kksTrial.Active)
                throw new InvalidOperationException("現在のAI所有者による未開始の一件試験要求ではありません。");
            plugin._kksTrial.Run(owner, beginNormalRequest);
        }
        public static void Execute(Command command)
        { if (Instance == null) throw new InvalidOperationException("AI専用Hプラグインが読み込まれていません。"); Instance._session.Execute(command); }
        public static void End(EndReason reason = EndReason.Cancelled) => Instance?._session.End(reason);
        public static void OwnerDestroyed(UnityEngine.MonoBehaviour owner) => Instance?._session.OwnerDestroyed(owner);
        public static Integration.NativeResidentProfile ReadResidentProfile(string residentId)
        {
            var plugin = Instance; if (plugin == null || !plugin._active || !plugin._permit.IsValid) throw new InvalidOperationException("AI専用Hの許可がありません。");
            try { return plugin._profiles.ReadOrCreate(residentId); }
            catch (Exception ex) { plugin.Notify("専用住人プロフィール読込: " + ex.Message); throw; }
        }
        public static void SaveResidentProfile(Integration.NativeResidentProfile profile)
        {
            var plugin = Instance; if (plugin == null || !plugin._active || !plugin._permit.IsValid) throw new InvalidOperationException("AI専用Hの許可がありません。");
            try { plugin._profiles.Save(profile); }
            catch (Exception ex) { plugin.Notify("専用住人プロフィール保存: " + ex.Message); throw; }
        }
        public static void ApplyResidentVoiceResult(Results.ResultDelta delta)
        {
            var plugin = Instance;
            if (plugin == null || !ResultCommitBoundary.CanCommit(plugin._active, plugin._permit.IsValid, plugin._session.Phase))
                throw new InvalidOperationException("AI専用Hの有効な結果確定処理ではありません。");
            plugin._profiles.ApplyVoiceResult(delta);
        }
        public static string ExportResidentProfiles(string[] identities, string previous)
        {
            // This optional extension must not prevent the independent walking-world save.
            // A context without H permission owns no new profile state to export.
            var plugin = Instance;
            if (plugin == null || !plugin._active || !plugin._permit.IsValid) return previous;
            try
            {
                var report = plugin._profiles.Export(identities, previous);
                if (report.Failures.Length != 0) plugin.Notify("専用住人プロフィールの一部保存失敗。取得成功分を保存し、失敗分の以前の値を保持します。\n" + string.Join("\n", report.Failures));
                return report.Json;
            }
            catch (Exception ex) { plugin.Notify("専用住人プロフィールのチェックポイント取得失敗。以前の記録を保持し、他の保存項目は継続します。\n" + ex.Message); return previous; }
        }
        public static void ImportResidentProfiles(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            var plugin = Instance; if (plugin == null || !plugin._active || !plugin._permit.IsValid) throw new InvalidOperationException("AI専用Hの許可がありません。");
            try
            {
                var failures = plugin._profiles.Import(json);
                if (failures.Length != 0) plugin.Notify("専用住人プロフィールの一部復元失敗。独立した成功分は適用済みです。\n" + string.Join("\n", failures));
            }
            catch (Exception ex) { plugin.Notify("専用住人プロフィールの復元失敗。現在の正常な記録を保持し、他の復元項目は継続します。\n" + ex.Message); }
        }
        public static string PlayerProfileIdentity(string worldIdentity)
        {
            if (string.IsNullOrWhiteSpace(worldIdentity)) throw new ArgumentException("AIワールド識別子がありません。");
            using (var hash = SHA256.Create()) return new Guid(hash.ComputeHash(Encoding.UTF8.GetBytes(PluginGuid + ":player:" + worldIdentity)).Take(16).ToArray()).ToString("N");
        }
    }
}
