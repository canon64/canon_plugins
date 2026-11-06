using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MainGameKaleidoscope
{
    public sealed partial class Plugin
    {
        private Harmony _rendererHarmony;
        private Func<ChaControl, Renderer[]> _afterimageRendererProvider;
        private readonly HashSet<ChaControl> _dirtyCharacters = new HashSet<ChaControl>();
        private readonly List<ChaControl> _removedCharacters = new List<ChaControl>();

        private void InstallRendererInvalidation()
        {
            _rendererHarmony = new Harmony(Guid + ".renderers");
            var completed = new HarmonyMethod(typeof(Plugin), nameof(RendererChangeCompleted));
            var asyncCompleted = new HarmonyMethod(typeof(Plugin), nameof(RendererChangeAsync));
            foreach (MethodInfo method in typeof(ChaControl).GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                string name = method.Name;
                // Colour/visibility/parent edits keep the same Renderer components.
                if ((name.StartsWith("ChangeClothes", StringComparison.Ordinal) ||
                     name.StartsWith("ChangeAccessory", StringComparison.Ordinal)) &&
                    (name.IndexOf("Color", StringComparison.Ordinal) >= 0 ||
                     name.IndexOf("Pattern", StringComparison.Ordinal) >= 0 ||
                     name.IndexOf("State", StringComparison.Ordinal) >= 0 ||
                     name.IndexOf("Parent", StringComparison.Ordinal) >= 0)) continue;
                if (!(name.StartsWith("ChangeClothes", StringComparison.Ordinal) ||
                      name.StartsWith("ChangeAccessory", StringComparison.Ordinal) ||
                      name.StartsWith("ReleaseControl", StringComparison.Ordinal) ||
                      name == "ReleaseObject" || name == "ReleaseAll" || name == "OnDestroy" ||
                      name == "Reload" || name == "ReloadAsync" || name == "ReloadNoAsync" ||
                      name.StartsWith("LoadCharaFbxData", StringComparison.Ordinal))) continue;
                if (method.IsAbstract || method.ContainsGenericParameters) continue;
                try
                {
                    _rendererHarmony.Patch(method, postfix:
                        method.ReturnType == typeof(IEnumerator) ? asyncCompleted : completed);
                }
                catch (Exception ex)
                {
                    // A missing invalidation hook must be visible, not silently ignored.
                    Logger.LogError("Renderer invalidation hook failed: " + method + ": " + ex);
                    throw;
                }
            }
            SceneManager.sceneUnloaded += OnRendererSceneUnloaded;
        }

        private void RemoveRendererInvalidation()
        {
            SceneManager.sceneUnloaded -= OnRendererSceneUnloaded;
            _rendererHarmony?.UnpatchSelf();
            _rendererHarmony = null;
        }

        private void OnRendererSceneUnloaded(Scene scene) => ClearRendererCache();

        private static void RendererChangeCompleted(ChaControl __instance)
        {
            Plugin owner = Instance;
            if (owner != null && __instance != null && owner._rendererCache.ContainsKey(__instance))
                owner._dirtyCharacters.Add(__instance);
        }

        private static void RendererChangeAsync(ChaControl __instance, ref IEnumerator __result)
        {
            if (__result != null) __result = NotifyRendererChangeOnCompletion(__result, __instance);
        }

        private static IEnumerator NotifyRendererChangeOnCompletion(IEnumerator operation, ChaControl character)
        {
            try
            {
                while (operation.MoveNext()) yield return operation.Current;
            }
            finally
            {
                try { (operation as IDisposable)?.Dispose(); }
                finally { RendererChangeCompleted(character); }
            }
        }

        private Renderer[] GetCharacterRenderers(ChaControl character)
        {
            if (!_rendererCache.TryGetValue(character, out Renderer[] renderers) ||
                _dirtyCharacters.Contains(character) || HasMissingRenderer(renderers))
            {
                renderers = character.GetComponentsInChildren<Renderer>(true);
                _rendererCache[character] = renderers;
                _dirtyCharacters.Remove(character);
            }
            return renderers;
        }

        private static bool HasMissingRenderer(Renderer[] renderers)
        {
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] == null) return true;
            return false;
        }

        private void PruneRendererCache()
        {
            _removedCharacters.Clear();
            foreach (ChaControl character in _rendererCache.Keys)
                if (character == null || Array.IndexOf(_characters, character) < 0)
                    _removedCharacters.Add(character);
            foreach (ChaControl character in _removedCharacters)
            {
                _rendererCache.Remove(character);
                _dirtyCharacters.Remove(character);
            }
            _removedCharacters.Clear();
        }

        private void ClearRendererCache()
        {
            SimpleAfterimage.Plugin.SetKaleidoscopeThoughtMask(false, null);
            _rendererCache.Clear();
            _dirtyCharacters.Clear();
            _removedCharacters.Clear();
            _characters = Array.Empty<ChaControl>();
            _lights = Array.Empty<Light>();
            _dildoModel = null;
            _dildoRenderers = Array.Empty<Renderer>();
            _nextCharacterScan = _nextLightScan = _nextDildoScan = 0f;
        }
    }
}
