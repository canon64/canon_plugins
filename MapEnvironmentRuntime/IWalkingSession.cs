using System;

namespace MapEnvironmentRuntime
{
    // The existing input/controller can be hosted without an island or weather world.
    public interface IWalkingSession
    {
        Parameters Settings { get; }
        bool WalkingReady { get; }
        bool Panel { get; }
        bool ExternalInputBlocked { get; }
        bool EditingParameter { get; }
        bool PointerOverPanel { get; }
        event Action<bool> PanelChanged;
        event Action<Parameters> ParametersApplied;
        void ReleaseKeyboardFocus();
        void ToggleCameraMode();
    }
}
