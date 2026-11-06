using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MainGameAiShoujoMode
{
    [BepInPlugin(Guid, "MainGameAiShoujoMode", "1.0.0")]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    [BepInDependency("com.kks.maingame.mapsandbox")]
    [BepInDependency(MainGameAiShoujoH.Plugin.PluginGuid)]
    [BepInDependency("com.bepis.bepinex.extendedsave")]
    [BepInDependency(MainGameUiInputCapture.Plugin.Guid)]
    [BepInDependency(MainGameNotifications.NotificationApi.PluginGuid)]
    [BepInDependency(CanonGate.GateApi.PluginGuid, "2.4.0")]
    [BepInDependency("com.kks.maingame.vmdplay")]
    [BepInDependency("com.kks.maingame.voicefaceeventbridge", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.kks.maingame.unifiedui")]
    [BepInDependency("com.kks.maingame.transformgizmo")]
    [BepInDependency("keelhauled.titleshortcuts", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.kks.maingame.aishoujomode";
        private const string Label = "AI少女モード";
        private static Plugin _instance;
        private Harmony _harmony;
        internal static bool ModeAllowed => _instance != null && _instance.isActiveAndEnabled;
        private readonly Dictionary<TitleScene, Binding> _bindings = new Dictionary<TitleScene, Binding>();
        private readonly HashSet<TitleScene> _pending = new HashSet<TitleScene>();
        private sealed class Binding { internal TextMeshProUGUI text, hotelText; internal string original, hotelOriginal; }

        private void OnEnable()
        {
            _instance = this;
            InitializeAiConversation();
            InitializeStartupDiagnostics();
            InitializeCharacterLoadDiagnostics();
            InitializeEventPositionDiagnostics();
            InitializeLighting();
            _harmony = new Harmony(Guid); _harmony.PatchAll(typeof(TitlePatches));
            _harmony.PatchAll(typeof(TitleShortcutPatches));
            _harmony.PatchAll(typeof(OwnedHReturnReadPatches));
            SceneManager.sceneUnloaded += SceneUnloaded;
            // Independent title mode: initialize once, outside canon_gate's scope.
            foreach (var title in Resources.FindObjectsOfTypeAll<TitleScene>())
                if (title.gameObject.scene.IsValid() && title.gameObject.scene.isLoaded) Queue(title);
            Logger.LogInfo("AI_SHOUJO_TITLE_ENABLED");
        }
        private void Queue(TitleScene title)
        {
            if (title == null || !isActiveAndEnabled || _bindings.ContainsKey(title) || !_pending.Add(title)) return;
            StartCoroutine(Prepare(title));
        }
        private IEnumerator Prepare(TitleScene title)
        {
            // TitleScene.Start disables the component until its async setup and translation finish.
            yield return null;
            yield return new WaitUntil(() => title == null || !isActiveAndEnabled || title.enabled);
            try
            {
                if (title == null || !isActiveAndEnabled) yield break;
                var field = AccessTools.Field(typeof(TitleScene), "buttons");
                var groups = field?.GetValue(title) as TitleScene.ButtonGroup[];
                if (groups == null) throw new InvalidOperationException("TitleScene.buttons is unavailable");
                var group = groups.Single(item => item != null && IsUploader(item.button, title));
                if (group.text == null) throw new InvalidOperationException("Uploader text is unavailable");
                var hotel = groups.Single(item => item != null && HasAction(item.button, title, nameof(TitleScene.OnDownloader)));
                if (hotel.text == null) throw new InvalidOperationException("Downloader text is unavailable");
                _bindings.Add(title, new Binding { text = group.text, original = group.text.text, hotelText = hotel.text, hotelOriginal = hotel.text.text });
                group.text.text = Label;
                hotel.text.text = HotelLabel;
                Logger.LogInfo("AI_SHOUJO_TITLE_READY button=" + group.button.name);
            }
            catch (Exception ex) { Logger.LogError("AI_SHOUJO_TITLE_SETUP_FAILED " + ex); }
            finally { _pending.Remove(title); }
        }
        private static bool IsUploader(Button button, TitleScene title)
            => HasAction(button, title, nameof(TitleScene.OnUploader));
        private static bool HasAction(Button button, TitleScene title, string method)
        {
            if (button == null) return false;
            for (int index = default(int); index < button.onClick.GetPersistentEventCount(); index++)
                if (button.onClick.GetPersistentTarget(index) == title && button.onClick.GetPersistentMethodName(index) == method) return true;
            return false;
        }
        private bool HandleClick(TitleScene title)
        {
            if (!isActiveAndEnabled || !_bindings.ContainsKey(title)) return true;
            if (TitleModeBusy) return false;
            AudioSource confirmation = null;
            try { confirmation = Illusion.Game.Utils.Sound.Play(Illusion.Game.SystemSE.ok_l); }
            catch (Exception ex) { Logger.LogWarning("AI_SHOUJO_TITLE_SOUND_FAILED " + ex.Message); }
            BeginMode(title, confirmation);
            return false;
        }
        private void SceneUnloaded(Scene scene)
        {
            _hotel?.TitleSceneUnloaded(scene);
            foreach (var title in _bindings.Keys.Where(title => title == null || title.gameObject.scene == scene).ToArray())
            { Restore(_bindings[title]); _bindings.Remove(title); }
            _pending.RemoveWhere(title => title == null || title.gameObject.scene == scene);
        }
        private static void Restore(Binding binding)
        {
            if (binding.text != null && binding.text.text == Label) binding.text.text = binding.original;
            if (binding.hotelText != null && binding.hotelText.text.StartsWith(HotelLabel, StringComparison.Ordinal)) binding.hotelText.text = binding.hotelOriginal;
        }
        private void RestoreTitles()
        {
            StopAllCoroutines(); _pending.Clear();
            foreach (var binding in _bindings.Values) Restore(binding);
            _bindings.Clear();
        }
        private void OnDisable()
        {
            DisposeAiConversation();
            CloseModeImmediately();
            CloseHotelImmediately();
            DisposeStartupDiagnostics();
            DisposeCharacterLoadDiagnostics();
            DisposeEventPositionDiagnostics();
            DisposeLighting();
            SceneManager.sceneUnloaded -= SceneUnloaded;
            RestoreTitles();
            if (_harmony != null)
            {
                foreach (string method in new[] { "Start", nameof(TitleScene.OnUploader), nameof(TitleScene.OnDownloader) })
                    _harmony.Unpatch(AccessTools.Method(typeof(TitleScene), method), HarmonyPatchType.All, Guid);
                var shortcuts = TitleShortcutPatches.FindTarget();
                if (shortcuts != null) _harmony.Unpatch(shortcuts, HarmonyPatchType.All, Guid);
                _harmony = null;
            }
            if (ReferenceEquals(_instance, this)) _instance = null;
        }
        private static class TitlePatches
        {
            [HarmonyPostfix, HarmonyPatch(typeof(TitleScene), "Start")]
            private static void Started(TitleScene __instance) { _instance?.Queue(__instance); }
            [HarmonyPrefix, HarmonyPatch(typeof(TitleScene), nameof(TitleScene.OnUploader))]
            private static bool Uploader(TitleScene __instance) => _instance == null || _instance.HandleClick(__instance);
            [HarmonyPrefix, HarmonyPatch(typeof(TitleScene), nameof(TitleScene.OnDownloader))]
            private static bool Downloader(TitleScene __instance) => _instance == null || _instance.HandleHotelClick(__instance);
        }
        private static class TitleShortcutPatches
        {
            internal static MethodBase FindTarget()
            {
                var plugin = AccessTools.TypeByName("TitleShortcuts.KoikatuSunshine.TitleShortcuts");
                var iterator = plugin?.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                    .SingleOrDefault(type => type.Name.StartsWith("<TitleInput>", StringComparison.Ordinal) && typeof(IEnumerator).IsAssignableFrom(type));
                return iterator == null ? null : AccessTools.Method(iterator, nameof(IEnumerator.MoveNext));
            }
            [HarmonyPrepare] private static bool Available() => FindTarget() != null;
            [HarmonyTargetMethod] private static MethodBase Target() => FindTarget();
            [HarmonyPrefix] private static bool PauseTitleInput(ref bool __result)
            {
                if (_instance == null || !_instance.TitleModeBusy) return true;
                // Keep the original coroutine alive without consuming a key until the mode returns.
                __result = true; return false;
            }
        }
    }
}
