// Unity identity boundary only; the gate, permits and shape edit transaction are production sources.
namespace UnityEngine { public class MonoBehaviour { } }
public class HSceneProc : UnityEngine.MonoBehaviour { }
namespace CanonGate
{
    public static partial class GateApi
    {
        private sealed class AiBoundary
        {
            internal bool Suspended;
            internal UnityEngine.MonoBehaviour Owner;
        }
        private static AiBoundary _aiMode;
    }
}
