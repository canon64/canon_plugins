using System;
using System.Collections.Generic;
using UnityEngine;

namespace MainGameBlankMapAdd.Map
{
    // Additional-map construction only: hide the borrowed scenery before its first draw.
    // No room creation, audio, lighting, actor operations, or permission evaluation here.
    internal sealed class SourceMapVisibilityPreparation
    {
        private GameObject _root;
        private readonly List<Action> _restore = new List<Action>();

        internal void Prepare(GameObject root, PluginSettings settings)
        {
            if (root == null || settings == null || !settings.BlankifySceneOnLoad)
            {
                Reset();
                return;
            }
            if (_root == root) return;
            Reset();
            _root = root;
            if (settings.DisableRenderers)
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    bool enabled = renderer.enabled;
                    _restore.Add(() => { if (renderer != null) renderer.enabled = enabled; });
                    renderer.enabled = false;
                }
            if (settings.DisableTerrains)
                foreach (var terrain in root.GetComponentsInChildren<Terrain>(true))
                {
                    bool height = terrain.drawHeightmap, trees = terrain.drawTreesAndFoliage;
                    _restore.Add(() =>
                    {
                        if (terrain == null) return;
                        terrain.drawHeightmap = height;
                        terrain.drawTreesAndFoliage = trees;
                    });
                    terrain.drawHeightmap = false;
                    terrain.drawTreesAndFoliage = false;
                }
        }

        internal void Reset()
        {
            foreach (var restore in _restore) restore();
            _restore.Clear();
            _root = null;
        }
    }
}
