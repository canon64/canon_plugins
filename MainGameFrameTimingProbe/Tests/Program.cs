using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Json;
using HarmonyLib;
using MainGameFrameTimingProbe;

static class Program
{
    static int checks;
    static TimingWindow patched;
    static long ticks;
    static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    static void Main()
    {
        NativeTimingSamplerChecks.Run(Check);
        var focus = new CaptureFocusGate(10, 120);
        Check(focus.Step(11, true, out _) == CaptureFocusStep.Active, "focused capture continues");
        Check(focus.Step(12, false, out _) == CaptureFocusStep.Paused && focus.Pauses == 1, "focus loss pauses");
        Check(focus.Step(13, false, out _) == CaptureFocusStep.Paused && focus.Pauses == 1, "pause counted once");
        Check(focus.Step(20, true, out float gap) == CaptureFocusStep.Resumed && gap == 8 && focus.PausedSeconds == 8, "return excludes focus gap");
        Check(focus.Step(21, true, out _) == CaptureFocusStep.Active, "active after return");
        Check(focus.Step(22, false, out _) == CaptureFocusStep.Paused && focus.Pauses == 2, "second pause");
        Check(focus.Step(130, false, out _) == CaptureFocusStep.TimedOut, "wall timeout applies even while unfocused");
        Check(focus.PausedSeconds == 116, "timeout includes the unfinished pause");
        Check(focus.Step(131, true, out _) == CaptureFocusStep.TimedOut, "focus cannot revive expired capture");
        Check(MethodSelection.Selected(typeof(Program).GetMethod(nameof(Update), BindingFlags.NonPublic | BindingFlags.Static)), "Unity callback selected");
        Check(MethodSelection.Selected(typeof(Program).GetMethod(nameof(OnWillRenderObject), BindingFlags.NonPublic | BindingFlags.Static)), "per-camera object callback includes planar reflection work");
        Check(!MethodSelection.Selected(typeof(Program).GetMethod(nameof(Coroutine), BindingFlags.NonPublic | BindingFlags.Static)), "coroutine factory excluded");
        var coroutine = Coroutine();
        Check(MethodSelection.Selected(coroutine.GetType().GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)), "generated MoveNext selected independently of coroutine name");
        Check(!MethodSelection.Selected(typeof(OrdinaryEnumerator).GetMethod("MoveNext")), "ordinary per-element enumerators excluded");
        Check(!MethodSelection.Selected(typeof(Program).GetMethod(nameof(UpdateGeneric), BindingFlags.NonPublic | BindingFlags.Static)), "open generic excluded");
        Check(MethodSelection.Selected(typeof(MainGameBlankMapAdd.Plugin).GetMethod("ReadVmdBar")), "map playback kernel selected");
        Check(!MethodSelection.Selected(typeof(Program).GetMethod(nameof(ReadVmdBar), BindingFlags.NonPublic | BindingFlags.Static)), "map kernel names do not instrument other plugins");
        var window = new TimingWindow(new[] { "outer", "inner" }, 1000, 2);
        window.Start();
        var outer = window.Begin(0, 0);
        var inner = window.Begin(1, 10);
        window.End(inner, 30);
        window.End(outer, 50);
        window.Frame(10); window.Frame(20); window.Frame(30);
        window.EngineTiming(0, double.NaN);
        window.EngineTiming(4, 8);
        window.Stop();
        var r = window.Snapshot();
        Check(r.Methods[0].ExclusiveMs == 30 && r.Methods[0].InclusiveMs == 50, "nested time must not be counted twice");
        Check(r.Methods[1].ExclusiveMs == 20 && r.Methods[1].Calls == 1, "inner time");
        Check(r.Frames == 3 && r.DroppedFrameSamples == 1 && r.FrameSamplesStored == 2, "fixed capacity");
        Check(r.MeanFps == 50 && r.P95FrameMs == 20 && r.FramesOver60FpsBudget == 2, "frame summary");
        Check(r.EngineCpuTimingSamples == 1 && r.EngineGpuMeanMs == 8, "invalid engine timings excluded");
        Check(r.LongestOverBudgetRun == 2 && r.FramesOver20Ms == 1, "slow-frame counters");
        var empty = new TimingWindow(new string[0], 1000).Snapshot();
        Check(empty.EngineGpuMeanMs == null && empty.EngineCpuMeanMs == null, "missing engine timings are unavailable");
        using (var ms = new MemoryStream())
        {
            var serializer = new DataContractJsonSerializer(typeof(WindowReport));
            serializer.WriteObject(ms, r); ms.Position = 0;
            Check(((WindowReport)serializer.ReadObject(ms)).Methods[0].ExclusiveMs == 30, "JSON roundtrip");
        }
        window = new TimingWindow(new[] { "method" }, 1000);
        window.Start();
        for (int i = 0; i < 100; i++) { var t = window.Begin(0, i); window.End(t, i + 1); window.Frame(1); }
        var allocated = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread"));
        allocated();
        long before = allocated();
        for (int i = 0; i < 100000; i++) { var t = window.Begin(0, i); window.End(t, i + 1); window.Frame(1); }
        Check(allocated() == before, "100000 samples allocate no memory");
        window.Stop(); Check(window.Snapshot().UnbalancedCalls == 0, "balanced calls");
        var spikes = new TimingWindow(new[] { "outer", "inner" }, 1000);
        spikes.Start();
        outer = spikes.Begin(0, 0); inner = spikes.Begin(1, 2);
        spikes.End(inner, 6); spikes.End(outer, 12); spikes.Frame(25);
        spikes.Stop(); var spikeReport = spikes.Snapshot();
        Check(spikeReport.SlowFrames.Length == 1 && spikeReport.SlowFrames[0].Methods[0].ExclusiveMs == 8 && spikeReport.SlowFrames[0].Methods[1].ExclusiveMs == 4, "slow-frame attribution excludes nested child time");
        Check(spikeReport.SlowFrames[0].FrameIndexInWindow == 1 && spikeReport.SlowFrames[0].Methods[0].Calls == 1, "slow interval keeps its frame identity and call count");
        using (var ms = new MemoryStream())
        {
            var serializer = new DataContractJsonSerializer(typeof(WindowReport));
            serializer.WriteObject(ms, spikeReport); ms.Position = 0;
            Check(((WindowReport)serializer.ReadObject(ms)).SlowFrames[0].Methods[0].ExclusiveMs == 8, "slow-frame attribution survives JSON roundtrip");
        }
        spikes.Start();
        spikes.Frame(10); spikes.Frame(30);
        spikes.Stop(); spikeReport = spikes.Snapshot();
        Check(spikeReport.SlowFrames[0].FrameMs == 30 && spikeReport.SlowFrames[0].Methods.Length == 0, "method times do not leak into another frame");
        spikes.Start(); outer = spikes.Begin(0, 0); spikes.End(outer, 5); spikes.Stop();
        spikes.Start(); spikes.Frame(35); spikes.Stop();
        Check(spikes.Snapshot().SlowFrames[0].Methods.Length == 0, "focus stop discards unassigned pending-frame costs");
        spikes.Start();
        for (int i = 0; i < 100; i++) spikes.Frame(40 + i);
        spikes.Stop(); spikeReport = spikes.Snapshot();
        Check(spikeReport.SlowFrames.Length == 8 && spikeReport.SlowFrames[0].FrameMs == 139 && spikeReport.SlowFrames[7].FrameMs == 132, "slow-frame storage is bounded to eight worst intervals");
        spikes.Start();
        before = allocated();
        for (int i = 0; i < 10000; i++) { outer = spikes.Begin(0, i); spikes.End(outer, i + 1); spikes.Frame(140 + i); }
        Check(allocated() == before, "slow-frame replacement also allocates zero bytes");
        spikes.Stop();
        window.Start();
        var tickets = new TimingWindow.Ticket[65];
        for (int i = 0; i < tickets.Length; i++) tickets[i] = window.Begin(0, i);
        for (int i = tickets.Length - 1; i >= 0; i--) window.End(tickets[i], 200 - i);
        window.Stop();
        Check(window.Snapshot().DepthOverflows == 1 && window.Snapshot().UnbalancedCalls == 0, "depth bound does not corrupt parent stack");

        patched = new TimingWindow(new[] { "patched" }, 1000);
        var harmony = new Harmony("canon.timingprobe.isolated.tests");
        harmony.Patch(AccessTools.Method(typeof(Program), nameof(Target)),
            prefix: new HarmonyMethod(typeof(Program), nameof(Prefix)), finalizer: new HarmonyMethod(typeof(Program), nameof(Finalizer)));
        patched.Start();
        Check(Target(false) == 123, "patch preserves return");
        try { Target(true); throw new Exception("exception suppressed"); }
        catch (InvalidOperationException ex) { Check(ex.Message == "expected", "original exception preserved"); }
        patched.Stop();
        Check(patched.Snapshot().Methods[0].Calls == 2 && patched.Snapshot().UnbalancedCalls == 0, "finalizer records success and exception");
        harmony.UnpatchSelf();
        Check(Target(false) == 123, "unpatch restores target");
        // Wrap pre-existing observers without changing their order or exception behavior.
        var observer = new Harmony("canon.timingprobe.preexisting.observer");
        observer.Patch(AccessTools.Method(typeof(Program), nameof(Target)),
            prefix: new HarmonyMethod(typeof(Program), nameof(ExistingPrefix)), postfix: new HarmonyMethod(typeof(Program), nameof(ExistingPostfix)));
        var begin = new HarmonyMethod(typeof(Program), nameof(Prefix)) { priority = Priority.First };
        var end = new HarmonyMethod(typeof(Program), nameof(Finalizer)) { priority = Priority.Last };
        harmony.Patch(AccessTools.Method(typeof(Program), nameof(Target)), prefix: begin, finalizer: end);
        harmony.Patch(AccessTools.Method(typeof(Program), nameof(TargetOther)), prefix: begin, finalizer: end);
        patched = new TimingWindow(new[] { "game inclusive" }, 1000); ticks = 0; patched.Start();
        Check(Target(false) == 123, "preexisting patches preserve target return");
        patched.Stop(); Check(patched.Snapshot().Methods[0].InclusiveMs == 31, "outer timing includes existing prefix and postfix");
        int cleanupFailures = 0;
        HookCleanup.Remove(harmony, new[] { AccessTools.Method(typeof(Program), nameof(Target)) },
            harmony.Id, (method, error) => cleanupFailures++);
        Check(cleanupFailures == 0 && !Harmony.GetPatchInfo(AccessTools.Method(typeof(Program), nameof(Target))).Owners.Contains(harmony.Id), "targeted cleanup removes profiler owner");
        Check(Harmony.GetPatchInfo(AccessTools.Method(typeof(Program), nameof(TargetOther))).Owners.Contains(harmony.Id), "cleanup does not enumerate unrelated methods");
        HookCleanup.Remove(harmony, new[] { AccessTools.Method(typeof(Program), nameof(TargetOther)) }, harmony.Id,
            (method, error) => cleanupFailures++);
        long previous = ticks; Check(Target(false) == 123 && ticks == previous + 30, "targeted cleanup preserves existing observer hooks");
        observer.UnpatchSelf();
        Console.WriteLine("TIMING_PROBE_OK checks=" + checks + "; 100000 samples=0 allocation bytes; Harmony finalizer preserves exceptions");
    }
    [MethodImpl(MethodImplOptions.NoInlining)] static int Target(bool fail)
    { if (fail) throw new InvalidOperationException("expected"); return 123; }
    static void Prefix(out TimingWindow.Ticket __state) { __state = patched.Begin(0, ++ticks); }
    static void Finalizer(TimingWindow.Ticket __state) { patched.End(__state, ++ticks); }
    static void ExistingPrefix() { ticks += 10; }
    static void ExistingPostfix() { ticks += 20; }
    static void Update() { }
    static void OnWillRenderObject() { }
    static void ReadVmdBar() { }
    [MethodImpl(MethodImplOptions.NoInlining)] static int TargetOther() => 77;
    static void UpdateGeneric<T>() { }
    static IEnumerator Coroutine() { while (true) yield return null; }
    sealed class OrdinaryEnumerator : IEnumerator
    { public bool MoveNext() => false; public object Current => null; public void Reset() { } }
}
namespace MainGameBlankMapAdd { public sealed class Plugin { public static void ReadVmdBar() { } } }
