namespace MainGameMagicPaint
{
    // Shared by the VR input handlers and the actual paint permission checks.
    internal sealed class VrPaintState
    {
        internal bool Held { get; private set; }
        internal bool Trigger { get; private set; }
        internal void Grip() { Held = true; Trigger = false; }
        internal bool Press() { if (!Held) return false; Trigger = true; return true; }
        internal void StopPress() { Trigger = false; }
        internal void Release() { Held = Trigger = false; }
        internal bool CanPaint(bool surfaceReady) => Held && Trigger && surfaceReady;
    }

    internal sealed class ContactLatch
    {
        internal bool Attached { get; private set; }
        internal float Limit(float attach, float detach) => Attached ? System.Math.Max(attach, detach) : attach;
        internal void Set(bool attached) { Attached = attached; }
    }

    // No timer, files or sampling here. The plugin starts a single timeout only when armed.
    internal sealed class StrokeInspection
    {
        internal bool Active { get; private set; }
        internal bool Started { get; private set; }
        internal bool RawInput { get; private set; }
        internal void Arm() { Active = true; Started = RawInput = false; }
        internal void Input(bool accepted) { if (!Active) return; RawInput = true; if (accepted) Started = true; }
        internal bool Timeout(double elapsed) => Active && !Started && elapsed >= 5;
        internal bool Finish() { if (!Active) return false; Active = false; return true; }
        internal void Cancel() { Active = Started = RawInput = false; }
    }
}
