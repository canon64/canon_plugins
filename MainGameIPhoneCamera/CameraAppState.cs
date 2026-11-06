using System;

namespace MainGameIPhoneCamera
{
    internal enum CaptureMode { Photo, Video }
    internal enum CapturePhase { Ready, Recording, Saving, Saved, Error }

    // Input edges and capture completions own this state; there is no input polling loop.
    internal sealed class CameraAppState
    {
        internal CaptureMode Mode { get; private set; }
        internal CapturePhase Phase { get; private set; }
        internal float Zoom { get; private set; } = 1f;
        private bool _axisLatched;
        internal bool Busy => Phase == CapturePhase.Recording || Phase == CapturePhase.Saving;
        internal bool Select(CaptureMode mode)
        {
            if (Busy || Mode == mode) return false;
            Mode = mode; Phase = CapturePhase.Ready; return true;
        }
        internal bool SetZoom(float zoom)
        {
            if (float.IsNaN(zoom) || float.IsInfinity(zoom)) return false;
            float next = PhoneZoomPlan.Clamp(zoom);
            if (Math.Abs(next - Zoom) < .001f) return false;
            Zoom = next;
            if (Phase == CapturePhase.Saved || Phase == CapturePhase.Error) Phase = CapturePhase.Ready;
            return true;
        }
        internal bool Axis(float x, float y)
        {
            float magnitude = Math.Max(Math.Abs(x), Math.Abs(y));
            if (magnitude < .25f) { _axisLatched = false; return false; }
            if (_axisLatched || magnitude < .65f) return false;
            _axisLatched = true;
            return Math.Abs(y) <= Math.Abs(x) && Select(x > 0 ? CaptureMode.Video : CaptureMode.Photo);
        }
        internal void ReleaseAxis() { _axisLatched = false; }
        internal bool BeginRecording()
        {
            if (Mode != CaptureMode.Video || Busy) return false;
            Phase = CapturePhase.Recording; return true;
        }
        internal void BeginSaving() { Phase = CapturePhase.Saving; }
        internal void Complete(bool success) { Phase = success ? CapturePhase.Saved : CapturePhase.Error; }
        internal void Reset() { Mode = CaptureMode.Photo; Phase = CapturePhase.Ready; Zoom = 1f; ReleaseAxis(); }
    }
}
