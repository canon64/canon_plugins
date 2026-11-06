using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.Serialization.Json;
using MainGameFrameTimingProbe;

namespace UnityEngine.Profiling
{
    public sealed class Recorder
    {
        internal static readonly Dictionary<string, Recorder> Registry = new Dictionary<string, Recorder>();
        public bool isValid, enabled;
        internal bool ThrowRead;
        internal int Blocks;
        internal long Elapsed;
        public int sampleBlockCount { get { if (ThrowRead) throw new InvalidOperationException(); return Blocks; } }
        public long elapsedNanoseconds => Elapsed;
        public static Recorder Get(string name)
        {
            if (name == "constructor failure") throw new InvalidOperationException("test setup error");
            return Registry[name];
        }
    }
}

internal static class NativeTimingSamplerChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var good = new UnityEngine.Profiling.Recorder { isValid = true, Blocks = 2, Elapsed = 5000000 };
        var external = new UnityEngine.Profiling.Recorder { isValid = true, enabled = true, Blocks = 1, Elapsed = 1000000 };
        var empty = new UnityEngine.Profiling.Recorder { isValid = true };
        var invalid = new UnityEngine.Profiling.Recorder();
        UnityEngine.Profiling.Recorder.Registry.Clear();
        UnityEngine.Profiling.Recorder.Registry.Add("good", good);
        UnityEngine.Profiling.Recorder.Registry.Add("external", external);
        UnityEngine.Profiling.Recorder.Registry.Add("empty", empty);
        UnityEngine.Profiling.Recorder.Registry.Add("invalid", invalid);
        var sampler = new NativeTimingSampler(new[] { "good", "external", "empty", "invalid", "constructor failure" });
        check(good.enabled && external.enabled && !invalid.enabled, "native setup enables only valid recorder");
        sampler.Sample();
        var report = sampler.Snapshot();
        check(report[0].Blocks == 2 && report[0].MeanMsPerFrame == 5 && report[0].MaxMs == 5, "native nanosecond conversion and block count");
        check(report[2].MeanMsPerFrame == null && report[3].MeanMsPerFrame == null && report[3].Error != null, "no native samples remain unavailable, not zero");
        check(report[4].Error.Contains("setup error"), "one invalid native marker does not abort capture");
        good.Blocks = 0; sampler.Sample(); report = sampler.Snapshot();
        check(report[0].ObservedFrames == 2 && report[0].FramesWithSamples == 1 && report[0].MeanMsPerFrame == 2.5, "native mean includes frames without marker work");
        good.Blocks = 2;
        for (int i = 0; i < 100; i++) sampler.Sample();
        var allocated = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread"));
        allocated(); long before = allocated();
        for (int i = 0; i < 10000; i++) sampler.Sample();
        check(allocated() == before, "native sampling has no steady per-frame allocations");
        int beforePause = sampler.Snapshot()[0].ObservedFrames;
        sampler.Pause(); sampler.Pause(); sampler.Sample();
        check(!good.enabled && external.enabled && sampler.Snapshot()[0].ObservedFrames == beforePause, "native pause disables owned collection and excludes pause without changing external recorder");
        sampler.Resume(); sampler.Sample();
        check(good.enabled && sampler.Snapshot()[0].ObservedFrames == beforePause + 1, "native resume continues focused collection");
        good.ThrowRead = true; sampler.Sample();
        check(sampler.Snapshot()[0].Error != null, "native read failure isolated to marker");
        sampler.Dispose();
        check(!good.enabled && external.enabled && !empty.enabled, "native stop restores owned recorder without disabling external recorder");
        int frames = sampler.Snapshot()[0].ObservedFrames;
        sampler.Sample(); sampler.Dispose();
        check(sampler.Snapshot()[0].ObservedFrames == frames, "native stop is idempotent and stops sampling");
        using (var stream = new MemoryStream())
        {
            var serializer = new DataContractJsonSerializer(typeof(NativeMarkerReport[]));
            serializer.WriteObject(stream, sampler.Snapshot()); stream.Position = 0;
            var restored = (NativeMarkerReport[])serializer.ReadObject(stream);
            check(restored[0].Blocks > 0 && restored[3].MeanMsPerFrame == null, "native JSON preserves actual counts and unavailable values");
        }
        using (var nextPhase = new NativeTimingSampler(new[] { "good" }))
            check(nextPhase.Snapshot()[0].ObservedFrames == 0 && nextPhase.Snapshot()[0].Blocks == 0, "native phases do not share totals");
    }
}
