using System.Collections.Generic;
using MainGameVoiceFaceEventBridge.Game;

namespace UnityEngine
{
    // Native clock is unavailable in the console boundary harness.
    public static class Time { public static float unscaledTime; }
}

namespace Manager
{
    internal static class Voice
    {
        internal static int Stops;
        internal static bool Fade;
        internal static void StopAll(bool fade) { Stops++; Fade = fade; }
    }
}
namespace MainGameVoiceFaceEventBridge
{
    internal static class SessionGate { internal static bool Allowed; }
    internal sealed partial class Plugin
    {
        internal sealed class TestAudio
        {
            internal bool Cue=true, Dialogue=true, Paused=true, Loading=true;
            internal bool PriorityAibuCueActive=>Cue;
            internal bool IsThoughtPlaying {get;set;}
            internal bool ImpactCueBusy=>Cue;
            internal bool IsPlaying=>Dialogue||Loading;
            internal void Stop(string reason) { Cue=Dialogue=Paused=Loading=false; }
        }
        internal sealed class TestSequences
        {
            internal bool Active=true;
            internal void Deactivate() { Active=false; }
        }
        internal readonly TestAudio _externalVoicePlayer=new TestAudio();
        internal readonly object _gateInboundLock=new object();
        internal readonly List<int> _inboundCommandQueue=new List<int> {1,2};
        internal readonly TestSequences _sequenceSessions=new TestSequences();
        internal readonly GameVoiceLoadLedger<object> Native=new GameVoiceLoadLedger<object>();
        internal bool Delayed=true, Responses=true;
        internal static PluginSettings Settings=new PluginSettings {Enabled=false,BlockGameVoiceWhileExternalPlaying=false};
        private readonly GameVoiceGuardState _voiceGuardState=new GameVoiceGuardState();
        private void ResetNativeVoiceAdmission() { Native.Reset(); }
        private void ClearDelayedActions(string reason) { Delayed=false; }
        private void ResetResponseTextQueue(string reason) { Responses=false; }
    }
}
namespace MainGameVoiceFaceEventBridge.RegressionHarness
{
    internal static partial class Program
    {
        private static void CheckSyringeInterruption()
        {
            var plugin=new Plugin();var nativeOwner=new object();plugin.Native.Begin(nativeOwner);
            int stops=Manager.Voice.Stops;
            SessionGate.Allowed=false;
            Assert(!plugin.InterruptAllVoicesForSyringe()&&Manager.Voice.Stops==stops&&plugin.Native.HasPending,
                "denied syringe interruption changed audio");
            SessionGate.Allowed=true;
            Assert(plugin.InterruptAllVoicesForSyringe(),"syringe interruption rejected in active session");
            Assert(Manager.Voice.Stops==stops+1&&!Manager.Voice.Fade,"native voices did not stop immediately");
            Assert(!plugin._externalVoicePlayer.Cue&&!plugin._externalVoicePlayer.Dialogue&&
                !plugin._externalVoicePlayer.Paused&&!plugin._externalVoicePlayer.Loading,"external playback or paused voice retained");
            Assert(plugin.Native.IsCancelled(nativeOwner)&&!plugin.Native.HasPending,"old native load can restart after interruption");
            Assert(plugin._inboundCommandQueue.Count==0&&!plugin._sequenceSessions.Active&&!plugin.Delayed&&!plugin.Responses,
                "queued voice work survived syringe interruption");
            Assert(plugin.InterruptAllVoicesForSyringe(),"repeated interruption was not safe");
            int cuts=0;
            var syringe=plugin.BeginSyringeDialogue(()=>cuts++);
            Assert(syringe!=null&&plugin.SyringeDialogueActive&&plugin.ShouldBlockGameVoiceEvents(),
                "syringe must block native breath/moan even with optional voice guard OFF");
            plugin._externalVoicePlayer.IsThoughtPlaying=true;
            Assert(plugin.ShouldBlockGameVoiceEvents(),"thought audio bypassed syringe native suppression");
            plugin.ExternalDialogueStarted(true);
            Assert(cuts==0&&plugin.SyringeDialogueActive,"thought audio cut syringe dialogue");
            plugin.ExternalDialogueStarted(false);
            Assert(cuts==1&&!plugin.SyringeDialogueActive,"external speech did not cut syringe dialogue");
            var fresh=plugin.BeginSyringeDialogue(()=>cuts++);syringe.Dispose();syringe.Dispose();
            Assert(plugin.SyringeDialogueActive,"stale syringe completion released fresh guard");
            fresh.Dispose();Assert(!plugin.SyringeDialogueActive,"natural completion retained native block");
            plugin.BeginSyringeDialogue(()=>cuts++);
            SessionGate.Allowed=false;
            Assert(!plugin.ShouldBlockGameVoiceEvents(),"native block survived a revoked session");
            Assert(plugin.BeginSyringeDialogue(()=>cuts++)==null,"guard acquisition bypassed gate");
        }
    }
}
