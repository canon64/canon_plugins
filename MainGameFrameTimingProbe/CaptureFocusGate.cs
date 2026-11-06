namespace MainGameFrameTimingProbe
{
    internal enum CaptureFocusStep { Active, Paused, Resumed, TimedOut }

    // A focus change pauses sampling instead of consuming the one requested capture.
    // The total wall-clock deadline still bounds how long timing hooks can remain installed.
    internal sealed class CaptureFocusGate
    {
        private readonly float _deadline;
        private float _pauseStarted = -1;
        internal int Pauses { get; private set; }
        internal float PausedSeconds { get; private set; }

        internal CaptureFocusGate(float started, float timeoutSeconds) { _deadline = started + timeoutSeconds; }

        internal CaptureFocusStep Step(float now, bool focused, out float pauseDuration)
        {
            pauseDuration = 0;
            if (now >= _deadline)
            {
                if (_pauseStarted >= 0) { PausedSeconds += now - _pauseStarted; _pauseStarted = -1; }
                return CaptureFocusStep.TimedOut;
            }
            if (!focused)
            {
                if (_pauseStarted < 0) { _pauseStarted = now; Pauses++; }
                return CaptureFocusStep.Paused;
            }
            if (_pauseStarted < 0) return CaptureFocusStep.Active;
            pauseDuration = now - _pauseStarted;
            PausedSeconds += pauseDuration;
            _pauseStarted = -1;
            return CaptureFocusStep.Resumed;
        }
    }
}
