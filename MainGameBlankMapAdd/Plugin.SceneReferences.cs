using HarmonyLib;
using MainGameBlankMapAdd.Compatibility;
using UnityEngine.SceneManagement;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private HSceneProc _roomOverlayHScene;


        private HSceneProc GetRoomOverlayHScene()
        {
            return CanonPlugins.Shared.PluginSessionGate.Allowed ? _roomOverlayHScene : null;
        }

        private void InvalidateRoomOverlayHScene()
        {
            _roomOverlayHScene = null;

        }

        private void OnRoomOverlaySceneUnloaded(Scene scene) { if (CanonPlugins.Shared.PluginSessionGate.Scene == null || CanonPlugins.Shared.PluginSessionGate.Scene.gameObject.scene == scene) InvalidateRoomOverlayHScene(); }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HSceneProc), "Start")]
        private static void RoomOverlayHSceneStart(HSceneProc __instance)
        {
            Plugin plugin = PluginCompatibilityResolver.ResolveLoadedPlugin();
            if (plugin == null) return;
            // Start can be a coroutine: presence is known before its first yield.
            plugin._roomOverlayHScene = __instance;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HSceneProc), "OnDestroy")]
        private static void RoomOverlayHSceneDestroyed(HSceneProc __instance)
        {
            Plugin plugin = PluginCompatibilityResolver.ResolveLoadedPlugin();
            if (plugin != null && ReferenceEquals(plugin._roomOverlayHScene, __instance))
            {
                plugin._mapSession?.CancelSourceMapPreparation();
                plugin.InvalidateRoomOverlayHScene();
            }
        }
    }
}
