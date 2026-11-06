using System;
using System.Threading;
using CanonGate;
namespace MainGameSubtitleEventBridge
{
    internal sealed partial class Plugin
    {
        private void OnDisable() { SessionGate.Stop(); }
        private void BeginGateSession() { StartServer(); }
        private void EndGateSession() { StopServer(); lock (_chimeQueueLock) _chimeQueue.Clear(); _audioPlayer?.Dispose(); _audioPlayer = null; }
        
    }
}
