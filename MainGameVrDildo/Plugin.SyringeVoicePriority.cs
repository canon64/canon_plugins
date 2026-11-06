using System;
using System.Collections.Generic;

namespace MainGameVrDildo
{
    public sealed partial class Plugin
    {
        private readonly HashSet<SyringeVoiceLease> _syringeVoiceLeases = new HashSet<SyringeVoiceLease>();
        private bool SyringeVoiceSuppressed => _syringeVoiceLeases.Count != 0;

        // Optional connection from MainGameSyringe. One lease per actual voice playback.
        // Unity main thread; cleanup works after gate invalidation.
        public static IDisposable AcquireSyringeVoicePriority()
        {
            Plugin p = Instance;
            if (p == null || !CanonPlugins.Shared.PluginSessionGate.Allowed) return null;
            var lease = new SyringeVoiceLease(p);
            p._syringeVoiceLeases.Add(lease);
            if (p._syringeVoiceLeases.Count == 1)
            {
                try
                {
                    MainGameVoiceFaceEventBridge.PublicApi.SetImpactCueEnabled(Guid, false, true);
                    p.StopOralAudio();
                    p._pistonVoicePending = p._hardVoicePending = p._ogazmVoicePending = false;
                }
                catch { lease.Dispose(); throw; }
            }
            return lease;
        }

        private sealed class SyringeVoiceLease : IDisposable
        {
            private Plugin _owner;
            internal SyringeVoiceLease(Plugin owner) { _owner = owner; }
            public void Dispose()
            {
                Plugin p = _owner;
                _owner = null;
                if (p == null || !p._syringeVoiceLeases.Remove(this) || p.SyringeVoiceSuppressed) return;
                p.ApplyVoiceSetting();
            }
        }
    }
}
