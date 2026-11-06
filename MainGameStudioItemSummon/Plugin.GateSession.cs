using System;
using System.Threading;
using CanonGate;
namespace MainGameStudioItemSummon
{
    public sealed partial class Plugin
    {
        private void OnDisable() { SessionGate.Stop(); }
        private void BeginGateSession() { BuildStatus = "session ready"; }
        private void EndGateSession() { StopAllCoroutines(); _catalogBuilding = false; ClearSummoned(); }
        private void OnDestroy() { SessionGate.Close(); }
    }
}
