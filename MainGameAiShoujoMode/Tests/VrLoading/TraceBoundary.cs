using System;
namespace MapEnvironmentRuntime
{
    // Legacy loading/vegetation tests keep diagnostics OFF. The real writer/probe has its own suite.
    public sealed class StartupTrace
    {
        public bool Enabled => false;
        public void Mark(string stage, Func<string> capture = null) { }
        public void Snapshot(string stage, UnityEngine.GameObject root = null) { }
        public void CameraCheckpoint(string stage, UnityEngine.Camera camera) { }
        public void TerrainCheckpoint(string stage, UnityEngine.Terrain terrain) { }
        public void RendererInventory(string stage, UnityEngine.GameObject root) { }
        public void ArmNextRender(string stage, UnityEngine.Camera camera, UnityEngine.GameObject root = null) { }
    }
}
namespace UnityEngine { public class Terrain { } }
