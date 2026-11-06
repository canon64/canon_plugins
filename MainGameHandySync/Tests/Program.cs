using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using MainGameHandySync;

static class Program
{
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Near(double actual, double expected, string message) => Check(Math.Abs(actual - expected) < 0.0001, message + $": {actual} != {expected}");
    static HampTarget Target(float width, float bpm = 60) => HampTarget.Create(0, width, bpm, 133, 0, 1);
    static TaskCompletionSource<bool> Signal() => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    static async Task Finish(HampCommandPump pump) => await pump.Completion.WaitAsync(TimeSpan.FromSeconds(3));

    static async Task Main()
    {
        foreach (int divisor in new[] { 1, 2, 4 })
        foreach (float width in new[] { 1f, .5f, .25f })
        {
            var t = Target(width, 60f / divisor);
            Near(t.Period, divisor, "target period");
            Near(60d * t.Width / (t.Velocity * 133d), divisor, "actual command period independent of width");
            Check(!t.Limited, "reachable target");
        }
        Check(Target(0).Velocity == 0, "zero width stops");
        Check(HampTarget.Create(0, 1, 600, 133, .02f, 1).Limited, "upper speed limit detected");
        Check(HampTarget.Create(0, .001f, 1, 133, .02f, 1).Limited, "lower speed limit detected");
        Check(HampTarget.NextDivisor(1) == 2 && HampTarget.NextDivisor(2) == 4 && HampTarget.NextDivisor(4) == 1, "button cycle");
        var source = new RhythmStrokeSource();
        Check(!source.TryGetBpm(true, 1, out _), "no source falls back");
        Check(source.Receive(true, 2, 1), "source activation");
        Check(!source.Receive(true, 2, 1), "identical rate sends nothing");
        foreach (int divisor in new[] { 1, 2, 4 })
        { Check(source.TryGetBpm(true, divisor, out float bpm), "source usable"); Near(bpm, 120d / divisor, "source multiplier"); }
        Check(!source.TryGetBpm(false, 4, out _), "checkbox off uses legacy");
        source.Receive(true, 4, .5f); source.TryGetBpm(true, 1, out float scaled);
        Near(scaled, 120, "convert Unity rate to wall time");
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, -1d, 0d })
        { source.Receive(true, invalid, 1); Check(!source.TryGetBpm(true, 1, out _), "invalid rate fallback"); }
        source.Receive(true, 2, 1); source.Receive(false, 2, 1);
        Check(!source.Available, "source deactivation");
        source.Receive(true, 1, 1); Check(source.TryGetBpm(true, 2, out _), "source recovery");
        source.Reset(); Check(!source.Available, "session clears source");
        await PairLatest();
        await StopDuringPair(false);
        await StopDuringPair(true);
        foreach (string path in new[] { "/mode2", "/slider/stroke", "/hamp/velocity", "/hamp/start" }) await Failure(path);
        await StopFailure();
        Console.WriteLine($"PASS {checks} checks: width/period, divisors, source/fallback, paired queue, cancellation, failure/recovery, session restart");
    }

    static async Task PairLatest()
    {
        var entered = Signal(); var release = Signal(); var sent = new List<(string path, string body)>();
        var pump = new HampCommandPump(async (path, body, token) =>
        {
            sent.Add((path, body));
            if (path == "/slider/stroke" && sent.Count == 2) { entered.SetResult(true); await release.Task; }
            return true;
        });
        pump.Request(Target(1)); await entered.Task;
        pump.Request(Target(.5f)); pump.Request(Target(.25f)); release.SetResult(true); await Finish(pump);
        Check(sent.Select(x => x.path).SequenceEqual(new[] { "/mode2", "/slider/stroke", "/hamp/velocity", "/hamp/start", "/slider/stroke", "/hamp/velocity" }), "complete pairs precede start and never starve");
        Near(JsonDocument.Parse(sent[1].body).RootElement.GetProperty("max").GetDouble(), 1, "first width retained");
        Near(JsonDocument.Parse(sent[2].body).RootElement.GetProperty("velocity").GetDouble(), Target(1).Velocity, "first matching velocity");
        Near(JsonDocument.Parse(sent[4].body).RootElement.GetProperty("max").GetDouble(), .25, "latest queued width wins");
        Near(JsonDocument.Parse(sent[5].body).RootElement.GetProperty("velocity").GetDouble(), Target(.25f).Velocity, "latest matching velocity");
    }

    static async Task StopDuringPair(bool restartBeforeCompletion)
    {
        var entered = Signal(); var release = Signal(); var sent = new List<string>(); bool first = true;
        var pump = new HampCommandPump(async (path, body, token) =>
        {
            sent.Add(path);
            if (path == "/slider/stroke" && first)
            {
                first = false; entered.SetResult(true);
                await release.Task; // Simulate a transport that completes despite cancellation.
                Check(token.IsCancellationRequested, "in-flight motion token cancelled");
            }
            return true;
        });
        pump.Request(Target(1)); await entered.Task;
        pump.Request(Target(.5f)); pump.Stop();
        if (restartBeforeCompletion) pump.Request(Target(.25f));
        release.SetResult(true); await Finish(pump);
        var expected = new List<string> { "/mode2", "/slider/stroke", "/hamp/stop" };
        if (restartBeforeCompletion) expected.AddRange(new[] { "/mode2", "/slider/stroke", "/hamp/velocity", "/hamp/start" });
        Check(sent.SequenceEqual(expected), "no stale velocity or start after stop; next session starts after stop");
        if (!restartBeforeCompletion)
        {
            sent.Clear(); pump.Request(Target(.25f)); await Finish(pump);
            Check(sent.SequenceEqual(new[] { "/mode2", "/slider/stroke", "/hamp/velocity", "/hamp/start" }), "explicit restart reconstructs mode and pair");
        }
    }

    static async Task Failure(string failingPath)
    {
        var sent = new List<string>(); bool fail = true;
        var pump = new HampCommandPump((path, body, token) => { sent.Add(path); return Task.FromResult(!(fail && path == failingPath)); });
        pump.Request(Target(1)); await Finish(pump);
        Check(pump.Failed, "failure remains visible: " + failingPath);
        Check(sent.Last() == "/hamp/stop", "failed pair requests stop: " + failingPath);
        if (failingPath != "/hamp/start") Check(!sent.Contains("/hamp/start"), "do not start incomplete target");
        fail = false; sent.Clear(); pump.Request(Target(.5f)); await Finish(pump);
        Check(!pump.Failed && sent.Last() == "/hamp/start", "explicit change recovers: " + failingPath);
    }

    static async Task StopFailure()
    {
        int calls = 0;
        var pump = new HampCommandPump((path, body, token) => { calls++; throw new Exception("offline"); });
        pump.Stop(); await Finish(pump);
        Check(calls == 1 && pump.Failed, "failed stop does not create retry polling");
    }
}
