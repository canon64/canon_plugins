using System;
using MapEnvironmentRuntime;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    internal sealed class HotelWalkingContext : IWalkingSession, IWalkingInputNotifications
    {
        public Parameters Settings { get; }
        private bool _walkingReady;
        public event Action InputBlockChanged;
        public bool WalkingReady
        {
            get => _walkingReady;
            set { if (_walkingReady == value) return; _walkingReady = value; InputBlockChanged?.Invoke(); }
        }
        public bool Panel { get; private set; }
        public bool ExternalInputBlocked => !WalkingReady || Panel || !Application.isFocused;
        public bool EditingParameter => false;
        public bool PointerOverPanel => Panel;
        public event Action<bool> PanelChanged;
        public event Action<Parameters> ParametersApplied;
        internal HotelWalkingContext(Parameters settings) { Settings = settings; }
        internal void SetPanel(bool shown) { Panel = shown; PanelChanged?.Invoke(shown); }
        public void ReleaseKeyboardFocus() { }
        // Hotel mode is always grounded walking; V never enables a flying camera.
        public void ToggleCameraMode() { }
        internal void Apply() => ParametersApplied?.Invoke(Settings);
    }
}
