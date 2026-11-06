using System.Collections.Generic;
using MainGameVoiceFaceEventBridge.Audio;

namespace MainGameVoiceFaceEventBridge.RegressionHarness
{
    internal static partial class Program
    {
        private static void CheckAiConversationOwnership()
        {
            var owner = new OwnedConversation(); var events = new List<string>();
            Assert(!owner.Active, "AI ownership absent on normal path");
            owner.Started(); owner.Finish("completed"); Assert(events.Count == 0, "ordinary completion cannot signal AI action");
            Assert(owner.Begin("conversation", "request", (c, r, status) => events.Add(c + "/" + r + "/" + status)), "owner accepted");
            Assert(!owner.Begin("another", "request", (c, r, status) => { }), "cannot steal owner");
            Assert(!owner.Matches("other", null) && !owner.Matches("conversation", "stale"), "scoped cancel rejects other request");
            owner.Started(); owner.Started(); Assert(events.Count == 1 && events[0].EndsWith("/started"), "start emitted once with identities");
            owner.Finish("cancelled"); owner.Finish("completed");
            Assert(events.Count == 2 && events[1].EndsWith("/cancelled"), "cancel cannot later report completion");
            Assert(owner.Begin("next", "next-request", (c, r, status) => Assert(!owner.Active, "owner invalid before completion callback")), "next request independent");
            owner.Finish("completed");
        }
    }
}
