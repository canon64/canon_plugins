using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MapEnvironmentRuntime
{
    internal sealed class SandboxTerrainRenderPreparation : IDisposable
    {
        private readonly string _name;
        internal SandboxTerrainRenderPreparation(string name) { _name = name; SceneManager.sceneLoaded += Prepare; }
        private void Prepare(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != _name) return;
            Dispose();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var terrain in root.GetComponentsInChildren<Terrain>(true))
                {
                    terrain.drawHeightmap = false;
                    terrain.drawTreesAndFoliage = false;
                    terrain.enabled = false;
                }
        }
        public void Dispose() => SceneManager.sceneLoaded -= Prepare;
    }
}
