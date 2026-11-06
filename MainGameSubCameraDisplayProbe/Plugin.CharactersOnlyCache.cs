using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MainGameSubCameraDisplayProbe
{
    public sealed partial class Plugin
    {
        private Harmony _charactersOnlyHarmony;
        private ChaControl[] _charactersOnlyActors = Array.Empty<ChaControl>();
        private bool _charactersOnlyKeepSetDirty = true;
        private readonly HashSet<ChaControl> _charactersOnlyDirtyActors = new HashSet<ChaControl>();
        private readonly List<ChaControl> _charactersOnlyRemovedActors = new List<ChaControl>();

        private void InstallCharactersOnlyInvalidation()
        {
            _charactersOnlyHarmony = new Harmony(Guid + ".renderer-cache");
            var complete = new HarmonyMethod(typeof(Plugin), nameof(CharactersOnlyVisualsChanged));
            var asyncComplete = new HarmonyMethod(typeof(Plugin), nameof(CharactersOnlyAsyncVisualsChanged));
            foreach (MethodInfo method in typeof(ChaControl).GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                string name = method.Name;
                // Material/visibility edits do not change the cached component list.
                if ((name.StartsWith("ChangeClothes", StringComparison.Ordinal) ||
                     name.StartsWith("ChangeAccessory", StringComparison.Ordinal)) &&
                    (name.IndexOf("Color", StringComparison.Ordinal) >= 0 ||
                     name.IndexOf("Pattern", StringComparison.Ordinal) >= 0 ||
                     name.IndexOf("State", StringComparison.Ordinal) >= 0 ||
                     name.IndexOf("Parent", StringComparison.Ordinal) >= 0)) continue;
                if (!(name.StartsWith("ChangeClothes", StringComparison.Ordinal) ||
                      name.StartsWith("ChangeAccessory", StringComparison.Ordinal) ||
                      name.StartsWith("ReleaseControl", StringComparison.Ordinal) ||
                      name == "Reload" || name == "ReloadAsync" || name == "ReloadNoAsync" ||
                      name == "Awake" || name == "Initialize" || name == "OnDestroy" ||
                      name == "ReleaseAll" || name == "ReleaseObject" ||
                      name.StartsWith("LoadCharaFbxData", StringComparison.Ordinal))) continue;
                if (method.IsAbstract || method.ContainsGenericParameters) continue;
                try
                {
                    _charactersOnlyHarmony.Patch(method, postfix:
                        method.ReturnType == typeof(IEnumerator) ? asyncComplete : complete);
                }
                catch (Exception ex)
                {
                    LogWarn("Character renderer invalidation hook failed: " + method + ": " + ex);
                    throw;
                }
            }
            SceneManager.sceneUnloaded += CharactersOnlySceneUnloaded;
        }

        private void RemoveCharactersOnlyInvalidation()
        {
            SceneManager.sceneUnloaded -= CharactersOnlySceneUnloaded;
            _charactersOnlyHarmony?.UnpatchSelf();
            _charactersOnlyHarmony = null;
        }

        private void CharactersOnlySceneUnloaded(Scene scene)
        {
            RestoreExternalRenderScope();
            ClearCharactersOnlyCache();
        }

        private static void CharactersOnlyVisualsChanged(ChaControl __instance)
        {
            Plugin owner = Instance;
            if (owner == null || owner._subCamera == null || !owner._settings.CharactersOnlyMode) return;
            owner._charactersOnlyKeepSetDirty = true;
            if (__instance != null && owner._charactersOnlyRendererCache.ContainsKey(__instance))
                owner._charactersOnlyDirtyActors.Add(__instance);
            else
                owner._charactersOnlyCharacterNextRescan = 0f;
        }

        private static void CharactersOnlyAsyncVisualsChanged(ChaControl __instance, ref IEnumerator __result)
        {
            if (__result != null) __result = CharactersOnlyNotifyAfterCompletion(__result, __instance);
        }

        private static IEnumerator CharactersOnlyNotifyAfterCompletion(IEnumerator operation, ChaControl actor)
        {
            try { while (operation.MoveNext()) yield return operation.Current; }
            finally
            {
                try { (operation as IDisposable)?.Dispose(); }
                finally { CharactersOnlyVisualsChanged(actor); }
            }
        }

        private void RefreshCharactersOnlyActors()
        {
            ChaControl[] actors = UnityEngine.Object.FindObjectsOfType<ChaControl>();
            bool changed = actors.Length != _charactersOnlyActors.Length;
            for (int i = 0; !changed && i < actors.Length; i++)
                changed = actors[i] != _charactersOnlyActors[i];
            if (!changed) return;
            _charactersOnlyActors = actors;
            _charactersOnlyKeepSetDirty = true;
            _charactersOnlyRemovedActors.Clear();
            foreach (ChaControl actor in _charactersOnlyRendererCache.Keys)
                if (actor == null || Array.IndexOf(actors, actor) < 0) _charactersOnlyRemovedActors.Add(actor);
            foreach (ChaControl actor in _charactersOnlyRemovedActors)
            {
                _charactersOnlyRendererCache.Remove(actor);
                _charactersOnlyDirtyActors.Remove(actor);
            }
            _charactersOnlyRemovedActors.Clear();
        }

        private static bool CharactersOnlyHasMissingRenderer(Renderer[] renderers)
        {
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] == null) return true;
            return false;
        }

        private void ClearCharactersOnlyCache()
        {
            _charactersOnlyKeepObjects.Clear();
            _charactersOnlyRendererCache.Clear();
            _charactersOnlyDirtyActors.Clear();
            _charactersOnlyRemovedActors.Clear();
            _charactersOnlyActors = Array.Empty<ChaControl>();
            _charactersOnlyLights = Array.Empty<Light>();
            _charactersOnlyLightNextRescan = 0f;
            _charactersOnlyKeepSetDirty = true;
            _charactersOnlyCharacterNextRescan = 0f;
        }
    }
}
