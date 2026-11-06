using System;
using UnityEngine;

namespace MainGameAiShoujoH.Motion
{
    public sealed class MotionEventReceiver : MonoBehaviour
    {
        internal Action Cycle;
        internal Action<int> Sound;
        // Invoked by the converted clip's authored terminal AnimationEvent.
        public void OnAiHMotionCycle() { Cycle?.Invoke(); }
        public void OnAiHSeCue(int cueId) { Sound?.Invoke(cueId); }
    }
}
