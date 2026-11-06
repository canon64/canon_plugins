using MainGameVoiceFaceEventBridge.Audio;

namespace MainGameVoiceFaceEventBridge
{
    // Only the native player boundary is replaced; CancelImpactCueForMotion is production code.
    internal sealed partial class ExternalVoicePlayer
    {
        private bool _priorityDeepCueActive, _priorityPistonCueActive, _explicitActorCueActive;
        private readonly ImpactCueState _cueState = new ImpactCueState();
        internal int TestStopActions;
        internal void TestStart(bool deep, bool piston, bool contact = false)
        {
            _priorityDeepCueActive = deep; _priorityPistonCueActive = piston;
            _explicitActorCueActive = contact;
            CueAction action;
            _cueState.TryBegin(0, false, out action);
            _cueState.RecordCueStarted(0, 4);
        }
        private void ApplyCueActions(CueAction action)
        { if ((action & (CueAction.FadeCue | CueAction.FinishCue)) != 0) TestStopActions++; }
        internal bool TestPlaying => _cueState.Phase == ImpactCueState.Stage.PlayingCue;
        internal CueAction TestTick(double now, bool playing) => _cueState.Tick(now, false, true, playing, true, 150);
    }
}
namespace MainGameVoiceFaceEventBridge.RegressionHarness
{
    internal static partial class Program
    {
        private static void CheckDildoMotionCancellation()
        {
            foreach (bool deep in new[] { false, true })
            {
                var player = new ExternalVoicePlayer(); player.TestStart(deep, !deep);
                player.CancelImpactCueForMotion();
                Assert(player.TestStopActions == 0 && player.TestPlaying, "foreign motion cancellation cannot fade or stop an accepted dildo line");
                Assert(player.TestTick(1, true) == CueAction.None, "line keeps playing after incoming motion request");
                player.CancelImpactCueForMotion();
                Assert(player.TestStopActions == 0 && player.TestPlaying, "repeated motion requests preserve the same line");
                Assert(player.TestTick(4, false) == CueAction.FinishCue, "protected line finishes only when audio reaches its natural end");
            }
            var ordinary = new ExternalVoicePlayer(); ordinary.TestStart(false, false);
            ordinary.CancelImpactCueForMotion();
            Assert(ordinary.TestStopActions == 1 && !ordinary.TestPlaying, "ordinary impact cue still cancels outside eligible motion");
            var candle = new ExternalVoicePlayer(); candle.TestStart(false, false, true);
            candle.CancelImpactCueForMotion();
            Assert(candle.TestStopActions == 0 && candle.TestPlaying, "candle contact reaction is independent of H motion");
            Assert(candle.TestTick(4, false) == CueAction.FinishCue, "candle reaction finishes naturally");
        }
    }
}
