using System;
using System.Reflection;
using System.IO;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using KKAPI.MainGame;
using KKAPI.Maker;

namespace CanonGate
{
    [BepInPlugin(GateApi.PluginGuid, "canon_gate", "2.4.0")]
    [BepInDependency("marco.kkapi")]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    [BepInProcess("CharaStudio")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private const string RoomProviderGuid = "com.kks.maingameblankmapadd";
        private static Plugin _instance;
        private Harmony _harmony;
        private ConfigEntry<string> _makerExceptions;
        private ConfigEntry<string> _everywhereExceptions;
        private ConfigEntry<string> _activityExceptions;
        private ConfigEntry<string> _aiModeExceptions;
        private ConfigEntry<string> _hotelExceptions;
        private bool _initialized;
        private bool _hObserved;
        private bool _makerObserved;
        private WarningWindow _warning;
        private UnityEngine.MonoBehaviour _hScene;

        private void Awake()
        {
            _instance = this;
            GateApi.ReportError = ex => Logger.LogError("Session callback failed: " + ex);
            try
            {
                _makerExceptions = Config.Bind("Exceptions", "MakerAllowedPluginGuids", "",
                    "キャラメイク開始時に許可するプラグインGUID。カンマ区切り、完全一致。変更は次回開始から反映。");
                _everywhereExceptions = Config.Bind("Exceptions", "EverywhereAllowedPluginGuids", "",
                    "H・キャラメイクの開始時に場所条件を免除するプラグインGUID。変更は次回開始から反映。門番停止中は不許可。");
                GateApi.ReloadSettings = Config.Reload;
                _hotelExceptions = Config.Bind("Exceptions", "HotelModeAllowedPluginGuids",
                    "com.kks.maingame.vrdildo,com.kks.maingame.vrneckchain,com.kks.maingame.iphonecamera,com.kks.maingame.syringe,com.kks.maingame.transformgizmo,com.kks.maingame.unifiedui,com.kks.maingame.uiinputcapture,com.kks.maingame.spiraleyes,com.kks.main.girlbodyikgizmo,com.kks.maingame.voicefaceeventbridge,com.kks.maingame.subtitlecore",
                    "旧版互換用。所有シーンが一致するホテルHは全登録プラグインを許可するため、このリストへの追加は不要です。");
                _aiModeExceptions = Config.Bind("Exceptions", "AiShoujoModeAllowedPluginGuids",
                    "com.kks.maingame.voicefaceeventbridge,com.kks.maingame.subtitlecore,com.kks.main.girlbodyikgizmo,com.kks.main.gameobjectcomposer,com.kks.maingame.unifiedui,com.kks.maingame.transformgizmo,com.kks.maingame.vmdplay,com.kks.maingame.aishoujomode.furniture,com.kks.maingame.aishoujoh,com.kks.maingame.vrdildo,com.kks.maingame.vrneckchain,com.kks.maingame.syringe,com.kks.maingame.iphonecamera,com.kks.maingame.magicpaint,com.kks.maingame.candle,com.kks.maingame.desktopdisplay,com.kks.maingame.vrhandcuffs,com.kks.maingame.uiinputcapture",
                    "AI少女モードの歩行・会話と、そのモードから開始したHで許可するGUID。開始時に一度だけ評価。");
                _activityExceptions = Config.Bind("Exceptions", "ActivityAllowedPluginGuids", "",
                    "非Hのタイトル・Studio・プレビュー・通常画面の実処理を許可するGUID。開始イベントで一度だけ評価。起動API準備は対象外。");
                GateApi.AllowActivity = (guid, kind) =>
                {
                    var policy = new ExceptionPolicy();
                    policy.Load("", kind == GateApi.ActivityKind.AiShoujoMode ? "" : _activityExceptions.Value, _aiModeExceptions.Value);
                    return policy.Allows(guid, false, kind == GateApi.ActivityKind.AiShoujoMode);
                };
                _harmony = new Harmony(GateApi.PluginGuid);
                _harmony.PatchAll(typeof(Plugin));
                // KKSAPI's VR Start UniTask continuation raises the same StartH event.
                // These hooks capture identity only; they never decide permission.
                var vrType = Type.GetType("VRHScene, Assembly-CSharp", false);
                if (vrType != null)
                {
                    _harmony.Patch(AccessTools.Method(vrType, "Start"),
                        prefix: new HarmonyMethod(typeof(Plugin), nameof(VrSceneStarting)));
                    var destroy = AccessTools.Method(vrType, "OnDestroy");
                    if (destroy != null) _harmony.Patch(destroy,
                        prefix: new HarmonyMethod(typeof(Plugin), nameof(VrSceneDestroyed)));
                }
                GameAPI.StartH += OnStartH;
                GameAPI.EndH += OnEndH;
                MakerAPI.MakerFinishedLoading += OnMakerLoaded;
                MakerAPI.MakerExiting += OnMakerExiting;
                _initialized = true;
                GateApi.SetRunning(isActiveAndEnabled);
                Logger.LogInfo("canon_gate: fixed H/Maker context and explicit AI mode activities; no room polling.");
            }
            catch { Shutdown(); throw; }
        }

        private void OnEnable() { if (_initialized) GateApi.SetRunning(true); }
        private void OnDisable() { _warning?.Hide(); GateApi.SetRunning(false); }
        private void OnDestroy() => Shutdown();

        private void Shutdown()
        {
            _warning?.Hide();
            GateApi.SetRunning(false);
            _initialized = false;
            GameAPI.StartH -= OnStartH;
            GameAPI.EndH -= OnEndH;
            MakerAPI.MakerFinishedLoading -= OnMakerLoaded;
            MakerAPI.MakerExiting -= OnMakerExiting;
            _harmony?.UnpatchSelf();
            GateApi.ReloadSettings = null;
            GateApi.AllowActivity = null;
            GateApi.ReportError = null;
            _instance = null;
        }

        private ExceptionPolicy ReadPolicy()
        {
            var policy = new ExceptionPolicy();
            policy.Load(_makerExceptions.Value, _everywhereExceptions.Value, _aiModeExceptions.Value, _hotelExceptions.Value);
            return policy;
        }

        private void OnStartH(object sender, EventArgs args)
        {
            if (GateApi.IsDedicatedNativeEngine(_hScene)) return;
            if (_hObserved) return;
            _hObserved = true;
            if (!_initialized || !isActiveAndEnabled) return;
            // This is the sole call site for room inspection. No retries during this H session.
            bool aiMode = GateApi.AiShoujoMode != null;
            bool hotelMode = GateApi.HotelMode?.Owns(_hScene) == true;
            bool allPoseRoom = !aiMode && !hotelMode && ReadRoomOnce();
            // Snapshot before invoking registered start callbacks; late registrations apply next H.
            var missing = allPoseRoom ? UnregisteredPlugins.Build(
                Path.Combine(Paths.PluginPath, "canon_plugins"), Chainloader.PluginInfos.Values, GateApi.IsRegistered) : null;
            GateApi.BeginSession(GateApi.SessionKind.H, allPoseRoom, ReadPolicy(), _hScene, aiMode, hotelMode);
            if (missing != null && missing.Count > 0 && _initialized && isActiveAndEnabled && GateApi.IsAllowed)
            {
                if (_warning == null)
                {
                    _warning = gameObject.AddComponent<WarningWindow>();
                    _warning.Trace = message => Logger.LogInfo("[WarningUI] " + message);
                }
                _warning.BeginScene((object)_hScene ?? new object());
                _warning.Show(missing);
            }
        }

        private void OnEndH(object sender, EventArgs args)
        {
            if (GateApi.IsDedicatedNativeEngine(_hScene)) return;
            _hObserved = false;
            _warning?.Hide();
            GateApi.EndSession(GateApi.SessionKind.H);
        }

        private void OnMakerLoaded(object sender, EventArgs args)
        {
            if (_makerObserved) return;
            _makerObserved = true;
            if (_initialized && isActiveAndEnabled)
                GateApi.BeginSession(GateApi.SessionKind.Maker, false, ReadPolicy());
        }

        private void OnMakerExiting(object sender, EventArgs args)
        {
            _makerObserved = false;
            GateApi.EndSession(GateApi.SessionKind.Maker);
        }

        private bool ReadRoomOnce()
        {
            try
            {
                if (!Chainloader.PluginInfos.TryGetValue(RoomProviderGuid, out var info)
                    || info.Instance == null || !info.Instance.isActiveAndEnabled) return false;
                var type = info.Instance.GetType().Assembly.GetType("MainGameBlankMapAdd.BlankRoomState", false);
                var getter = type?.GetProperty("IsInBlankRoom", BindingFlags.Public | BindingFlags.Static)?.GetGetMethod();
                if (getter == null || getter.ReturnType != typeof(bool)) return false;
                return ((Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), getter))();
            }
            catch (Exception ex)
            {
                Logger.LogError("H entry room check failed; room permission denied for this session: " + ex.Message);
                return false;
            }
        }

        // Identity only: room inspection remains solely in GameAPI.StartH after loading completes.
        [HarmonyPrefix, HarmonyPatch(typeof(HSceneProc), "Start")]
        private static void HSceneStarting(HSceneProc __instance)
        {
            if (_instance != null) _instance._hScene = __instance;
        }

        private static void VrSceneStarting(UnityEngine.MonoBehaviour __instance)
        { if (_instance != null) _instance._hScene = __instance; }

        private static void VrSceneDestroyed(UnityEngine.MonoBehaviour __instance)
        {
            if (_instance != null && ReferenceEquals(_instance._hScene, __instance))
                _instance.OnEndH(null, EventArgs.Empty);
        }

        // Ignore destruction of an older/unrelated H scene; never inspect the map here.
        [HarmonyPrefix, HarmonyPatch(typeof(HSceneProc), "OnDestroy")]
        private static void HSceneDestroyed(HSceneProc __instance)
        {
            if (GateApi.IsDedicatedNativeEngine(__instance)) return;
            if (_instance == null) return;
            if (_instance._hScene != null && !ReferenceEquals(_instance._hScene, __instance)) return;
            _instance.OnEndH(null, EventArgs.Empty);
        }
    }
}
