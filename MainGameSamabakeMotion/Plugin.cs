using System;
using System.IO;
using BepInEx;
using HarmonyLib;
using CanonGate;

namespace MainGameSamabakeMotion
{
    [BepInPlugin(Id, "サマバケ zipmod 再生補助", "0.5.0")]
    [BepInProcess("KoikatsuSunshine")]
    [BepInDependency("canon.gate")]
    [BepInDependency("essuhauled.animationloader", "1.1.3.4")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "canon.maingame.samabake.motion";
        internal static NativeIntegration Runtime;
        private Harmony _harmony;
        private IDisposable _registration;
        private GateApi.Permit _permit;
        private bool _running;
        private void Awake()
        {
            string directory = Path.GetDirectoryName(Info.Location);
            var diagnostics = Config.Bind("Diagnostics", "Enabled", false, "状態切替・登録失敗の診断。OFFではログ専用の処理を行いません。");
            // Existing v0.3 kiss-only installations have no expanded catalog.
            string catalog = Path.Combine(directory, "SamabakeNativeCatalog.json");
            if (File.Exists(catalog)) ZipmodPose.Load(catalog);
            Runtime = new NativeIntegration(() => _running && _permit.IsValid && enabled, () => diagnostics.Value,
                message =>
                {
                    // Diagnostics must never prevent rollback or H-session cleanup.
                    try { Logger.LogInfo(message); File.AppendAllText(Path.Combine(directory, "native-h.log"), message + Environment.NewLine, System.Text.Encoding.UTF8); }
                    catch (Exception) { }
                });
            _harmony = new Harmony(Id);
            NativeHooks.Install(_harmony);
            _registration = GateApi.Register(Id, () =>
            {
                var context = GateApi.Current;
                if (!enabled || context == null || context.Kind != GateApi.SessionKind.H || context.HScene == null) return;
                _permit = GateApi.Capture(Id);
                _running = _permit.IsValid;
                if (_running) Runtime.BeginSession(context.HScene);
            }, EndSession);
        }
        private void EndSession()
        {
            _running = false;
            _permit = default;
            Runtime?.EndSession();
        }
        private void OnDisable() { EndSession(); }
        private void OnDestroy()
        {
            _registration?.Dispose();
            _registration = null;
            EndSession();
            _harmony?.UnpatchSelf();
            Runtime = null;
        }
    }
}
