using System;
using System.Collections;
using System.Collections.Generic;
using CanonPlugins.Shared;

internal static class CoroutineTests
{
    private static IEnumerator Child(List<string> events, bool failCleanup = false)
    {
        try { events.Add("child-start"); yield return "wait"; events.Add("child-finish"); }
        finally { events.Add("child-cleanup"); if (failCleanup) throw new InvalidOperationException("cleanup"); }
    }
    private static IEnumerator Parent(List<string> events, bool failCleanup = false)
    {
        try { yield return Child(events, failCleanup); events.Add("parent-finish"); }
        finally { events.Add("parent-cleanup"); }
    }
    internal static void Run(Action<bool, string> check)
    {
        var events = new List<string>();
        bool allowed = true;
        var routine = new RoomBoundCoroutine(Parent(events), () => allowed);
        check(routine.MoveNext() && (string)routine.Current == "wait", "nested capture wait is preserved");
        allowed = false;
        check(!routine.MoveNext() && string.Join(",", events) == "child-start,child-cleanup,parent-cleanup",
            "room exit aborts nested work and restores child then parent");
        int count = events.Count;
        routine.Dispose();
        check(events.Count == count && routine.IsCompleted, "repeated cancellation does not restore twice");
        allowed = true;
        events.Clear();
        routine = new RoomBoundCoroutine(Parent(events), () => allowed);
        routine.MoveNext();
        check(!routine.MoveNext() && events.Contains("parent-finish"), "normal in-room work completes");
        events.Clear();
        routine = new RoomBoundCoroutine(Parent(events, true), () => allowed);
        routine.MoveNext();
        try { routine.Dispose(); } catch (InvalidOperationException) { }
        check(events.Contains("parent-cleanup") && routine.IsCompleted, "parent restoration survives child cleanup failure");
        events.Clear();
        int epoch = 1, expectedEpoch = epoch;
        routine = new RoomBoundCoroutine(Parent(events), () => allowed && epoch == expectedEpoch);
        routine.MoveNext();
        epoch++;
        check(!routine.MoveNext() && !events.Contains("child-finish"), "old request cannot resume after room reentry");
        events.Clear();
        routine = new RoomBoundCoroutine(Parent(events), () => false);
        check(!routine.MoveNext() && events.Count == 0, "requests starting outside perform no work");
    }
}
