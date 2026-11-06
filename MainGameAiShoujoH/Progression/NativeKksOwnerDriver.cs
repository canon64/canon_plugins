using System;
using System.Collections.Generic;
using MainGameAiShoujoH.Contracts;
using MainGameAiShoujoH.Integration;

namespace MainGameAiShoujoH.Progression
{
    // Observation/command adapter only. Native HSceneProc owns every time step,
    // gauge, voice wait, HHoushi.Proc and continuous animation parameter update.
    internal sealed class NativeKksOwnerDriver : IProgressionDriver
    {
        readonly NativeKksSessionRuntime runtime;
        bool stopped;
        internal NativeKksOwnerDriver(NativeKksSessionRuntime runtime) { this.runtime = runtime; }
        public string State => runtime.Engine.SelectedState ?? string.Empty;
        public float Female => runtime.Flags.gaugeFemale / runtime.Protocol.gaugeMax;
        public float Male => runtime.Flags.gaugeMale / runtime.Protocol.gaugeMax;
        public float Speed => runtime.Flags.speed;
        public bool WaitingForStartVoice => runtime.Flags.voiceWait;
        public bool HasInsideResult => runtime.Flags.count.houshiInside > default(int);
        public bool CanFinish(Finish finish) => !stopped && State == "OLoop" && (finish == Finish.Outside || finish == Finish.Inside);
        public float SecondsToBoundary(DriveSample sample) => float.PositiveInfinity;
        public IReadOnlyList<ProcEffect> Advance(DriveSample sample) => Array.Empty<ProcEffect>();
        public void SetManualSpeed(float value)
        { if (!stopped) runtime.Engine.DispatchNativeCommand(new Command { Name = "speed", Amount = value }); }
        public void Stop() { stopped = true; }
    }
}
