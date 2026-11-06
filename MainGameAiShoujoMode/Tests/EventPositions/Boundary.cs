using UnityEngine;
namespace MapEnvironmentRuntime
{
    internal sealed class SandboxHost
    {
        public GameObject gameObject;
        public Walker Walker;
        public Vector3 ToIslandSourcePosition(Vector3 value) => value;
    }
    internal sealed class Walker { public Camera ViewCamera; }
}
namespace MainGameAiShoujoMode
{
    internal sealed class AiModeHSession
    {
        internal sealed class HLocation { public string SourceId; public Vector3 Position; public int[] Categories; }
    }
}
