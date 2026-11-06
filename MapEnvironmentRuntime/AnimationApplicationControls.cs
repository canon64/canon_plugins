using System;

namespace MapEnvironmentRuntime
{
    public sealed class AnimationApplicationStatus
    {
        public string state,card,sourceClip,controller,report,error,autonomyState,autonomyError,destinationId;
        public int mappedBones;
        public bool characterReady,nativeSamplingPassed,kksVisualVerified,rootMotion;
    }
    public interface IAnimationApplicationControls
    {
        void DrawControls();
        void DrawQuickControls();
        void DrawCapsuleControls();
        AnimationApplicationStatus Snapshot();
    }
}
