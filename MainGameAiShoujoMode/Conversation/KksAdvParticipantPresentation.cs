using System;

namespace MainGameAiShoujoMode.Conversation
{
    // Own after the CG/rig so this subscription is removed before either dependency is restored.
    internal sealed class KksAdvParticipantPresentation : IDisposable
    {
        private readonly KksAdvActorRegistry _actors;
        private readonly KksAdvRenderRig _rig;
        private readonly KksAdvNativeEventCgBackend _cg;
        private bool _disposed;
        internal KksAdvParticipantPresentation(KksAdvActorRegistry actors, KksAdvRenderRig rig, KksAdvNativeEventCgBackend cg)
        {
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _rig = rig ?? throw new ArgumentNullException(nameof(rig)); _cg = cg ?? throw new ArgumentNullException(nameof(cg));
            _actors.PresentationChanged += Refresh;
            try { Refresh(); } catch { Dispose(); throw; }
        }
        private void Refresh()
        {
            if (_disposed) return;
            _rig.RefreshLightTarget();
            if (_cg.Active) _cg.RefreshCameraShape();
        }
        public void Dispose() { if (_disposed) return; _disposed = true; _actors.PresentationChanged -= Refresh; }
    }
}
