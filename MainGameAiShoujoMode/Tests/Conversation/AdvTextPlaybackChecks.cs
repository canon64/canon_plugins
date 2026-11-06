using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoMode.Conversation;

internal static class AdvTextPlaybackChecks
{
    private sealed class Ticket : IDisposable
    {
        internal bool Disposed;
        public void Dispose() { Require(!Disposed, "Text playback lease disposed twice"); Disposed = true; }
    }
    private sealed class Motion
    {
        internal Action Completed;
        internal Action<Exception> Failed;
        internal readonly Ticket Ticket = new Ticket();
        internal bool Synchronous;
        internal IDisposable Wait(Action completed, Action<Exception> failed)
        {
            Require(Completed == null, "Motion completion was subscribed twice"); Completed = completed; Failed = failed;
            if (Synchronous) completed(); return Ticket;
        }
    }
    private sealed class Voices
    {
        internal Action Started, Completed;
        internal Action<Exception> Failed;
        internal readonly Ticket Ticket = new Ticket();
        internal Action DuringPlay;
        internal IDisposable Play(Action started, Action completed, Action<Exception> failed)
        { Started = started; Completed = completed; Failed = failed; DuringPlay?.Invoke(); return Ticket; }
    }
    internal static void Run(string directory, KksNormalConversationRules.Settings n)
    {
        Console.WriteLine("Start native ADV text choreography, motion events and voice lifetime checks");
        var trace = new List<string>();
        KksAdvTextPlayback.Performance P(string name, Motion motion = null) => new KksAdvTextPlayback.Performance(
            () => trace.Add(name + ".expression"), () => trace.Add(name + ".icon"), motion == null ? null : () => trace.Add(name + ".motion"), motion == null ? null : motion.Wait);
        var voices = new Voices(); int done = n.zero, errors = n.zero;
        var first = new Motion(); var second = new Motion();
        var playback = new KksAdvTextPlayback(new[] { P("A", first), P("B", second) }, n.one + n.one, voices.Play, () => false, n.zero, n.one);
        playback.Start(() => done++, _ => errors++);
        Require(!trace.Any() && done == n.zero, "Queued performances played before a voice-group start");
        voices.Started();
        Require(trace.SequenceEqual(new[] { "A.expression", "A.icon", "A.motion" }), "Native expression/icon/motion order changed");
        voices.Started(); voices.Completed(); voices.Completed();
        Require(done == n.one && trace.Last() == "B.motion" && first.Completed == null && second.Completed == null, "Voice-driven performance waited for motion timers or completed twice");
        playback.End(true); Require(!voices.Ticket.Disposed, "Ordinary text completion canceled host-owned speech");
        voices.Started(); voices.Failed(new IOException("stale")); Require(errors == n.zero && trace.Count == (n.one + n.one) * (n.one + n.one + n.one), "Ended text accepted late voice callbacks");

        trace.Clear(); done = n.zero;
        voices = new Voices();
        playback = new KksAdvTextPlayback(new[] { P("A"), P("B") }, n.zero, voices.Play, () => false, n.zero, n.one);
        playback.Start(() => done++, _ => errors++); voices.Started(); voices.Completed();
        Require(done == n.zero && trace.SequenceEqual(new[] { "A.expression", "A.icon" }), "Missing voice groups were replaced by unrequested performance timers");
        playback.End(false);
        Require(trace.SequenceEqual(new[] { "A.expression", "A.icon", "B.expression", "B.icon" }) && !voices.Ticket.Disposed,
            "Forced text completion failed to flush remaining performances while retaining speech");

        trace.Clear(); done = n.zero; voices = new Voices();
        playback = new KksAdvTextPlayback(new[] { P("only") }, n.zero, voices.Play, () => false, n.zero, n.one);
        playback.Start(() => done++, _ => errors++); voices.Started(); voices.Started(); voices.Started(); voices.Completed();
        Require(done == n.one && trace.Count == n.one + n.one, "Extra voice groups replayed the last performance"); playback.End(true);

        trace.Clear(); done = n.zero; first = new Motion(); second = new Motion();
        playback = new KksAdvTextPlayback(new[] { P("A", first), P("B", second) }, n.one + n.one, null, () => true, n.zero, n.one);
        playback.Start(() => done++, _ => errors++);
        Require(trace.Last() == "A.motion" && first.Completed != null && second.Completed == null, "No-voice route did not wait for the preceding motion");
        first.Completed(); first.Completed();
        Require(trace.Last() == "B.motion" && second.Completed != null && done == n.zero, "No-voice route failed to wait for its final motion");
        second.Completed(); Require(done == n.one && first.Ticket.Disposed && second.Ticket.Disposed, "Final motion did not complete and release waits"); playback.End(true);

        // The native code advances expression-only trailing rows before awaiting the last motion.
        trace.Clear(); done = n.zero; first = new Motion();
        playback = new KksAdvTextPlayback(new[] { P("motion", first), P("expression-only") }, n.one, null, () => true, n.zero, n.one);
        playback.Start(() => done++, _ => errors++);
        Require(trace.Last() == "expression-only.icon" && done == n.zero && first.Completed != null, "Trailing expression was delayed until after the final motion");
        first.Completed(); Require(done == n.one, "Trailing-expression route did not finish"); playback.End(true);

        trace.Clear(); done = n.zero; first = new Motion(); second = new Motion();
        playback = new KksAdvTextPlayback(new[] { P("A", first), P("empty-motion-row"), P("C", second) }, n.one + n.one + n.one,
            null, () => true, n.zero, n.one);
        playback.Start(() => done++, _ => errors++);
        Require(first.Completed == null && trace.Last() == "C.motion" && second.Completed != null, "Empty next-motion row incorrectly waited for its predecessor");
        playback.End(false); Require(second.Ticket.Disposed && done == n.zero, "Cancellation failed to release a pending motion wait");
        second.Completed(); second.Failed(new IOException("late motion")); Require(done == n.zero && errors == n.zero, "Canceled motion entered completion callbacks");

        done = n.zero; first = new Motion { Synchronous = true }; second = new Motion { Synchronous = true };
        playback = new KksAdvTextPlayback(new[] { P("A", first), P("B", second) }, n.one + n.one, null, () => true, n.zero, n.one);
        playback.Start(() => done++, _ => errors++);
        Require(done == n.one && first.Ticket.Disposed && second.Ticket.Disposed, "Synchronous motion completion lost or leaked its lease"); playback.End(true);

        voices = new Voices(); done = n.zero; errors = n.zero; bool cancel = true;
        playback = new KksAdvTextPlayback(Array.Empty<KksAdvTextPlayback.Performance>(), n.zero, voices.Play, () => cancel, n.zero, n.one);
        playback.Start(() => done++, _ => errors++); playback.End(false);
        Require(voices.Ticket.Disposed, "Whole-session cancellation retained speech"); voices.Completed(); Require(done == n.zero, "Canceled session completed from an old voice callback");

        voices = new Voices(); done = n.zero; errors = n.zero;
        playback = new KksAdvTextPlayback(Array.Empty<KksAdvTextPlayback.Performance>(), n.zero, voices.Play, () => true, n.zero, n.one);
        voices.DuringPlay = () => { voices.Failed(new IOException("inline")); playback.End(false); };
        playback.Start(() => done++, _ => errors++);
        Require(errors == n.one && done == n.zero && voices.Ticket.Disposed, "Inline voice failure leaked the returned lease");

        errors = n.zero;
        playback = new KksAdvTextPlayback(Array.Empty<KksAdvTextPlayback.Performance>(), n.zero, null, () => true, n.zero, n.one);
        playback.Start(() => throw new IOException("completion failed"), _ => errors++);
        Require(errors == n.one, "Completion callback exception was swallowed"); playback.End(true);

        errors = n.zero;
        var broken = new KksAdvTextPlayback.Performance(() => throw new IOException("performance failed"), null, null);
        playback = new KksAdvTextPlayback(new[] { broken }, n.zero, null, () => true, n.zero, n.one);
        playback.Start(() => throw new Exception("Unexpected completion"), _ => errors++);
        Require(errors == n.one, "Performance failure did not reach its owner"); playback.End(false);

        done = n.zero;
        var many = Enumerable.Range(n.zero, n.requiredNicknameEvent * n.temperamentCount).Select(index =>
            new KksAdvTextPlayback.Performance(() => done++, null, null)).ToArray();
        playback = new KksAdvTextPlayback(many, n.zero, null, () => true, n.zero, n.one);
        playback.Start(() => done++, exception => throw exception);
        Require(done == many.Length + n.one, "Long synchronous performance queue recursed or lost rows"); playback.End(true);
        CheckInstalledRecipes(directory, n);

        File.WriteAllText(Path.Combine(directory, "adv-text-playback-checks.json"), JsonSerializer.Serialize(new
        {
            scope = "Production text choreography with controlled voice/motion events; no Unity sound or animation executed",
            nativeOrder = true, voiceGroupPerformanceAlignment = true, missingAndExtraGroups = true,
            noVoiceMotionWaits = true, trailingAndEmptyMotionRows = true, forcedFlushAndVoiceRetention = true,
            cancellationAndStaleEvents = true, synchronousLeaseAdoption = true, failuresAndCompletionErrors = true
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS ADV text voice/motion alignment forced flush retained speech cancellation failures");
    }
    private static void CheckInstalledRecipes(string directory, KksNormalConversationRules.Settings n)
    {
        using var graph = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "scenario-graph.json")));
        var source = new Dictionary<string, JsonDocument>();
        int captions = n.zero, voiceGroups = n.zero, performanceRows = n.zero, noVoiceCaptions = n.zero;
        try
        {
            foreach (var root in graph.RootElement.GetProperty("roots").EnumerateArray())
            {
                var bundle = root.GetProperty("bundle").GetString(); var assetName = root.GetProperty("asset").GetString();
                if (!source.TryGetValue(bundle, out var document))
                {
                    document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "bundles", bundle.Replace('/', '_') + ".json")));
                    source.Add(bundle, document);
                }
                var asset = document.RootElement.EnumerateArray().Single(item => item.GetProperty("name").GetString() == assetName);
                var expressions = new List<string>(); var motions = new List<string>(); int voices = n.zero;
                foreach (var row in asset.GetProperty("data").GetProperty("list").EnumerateArray())
                {
                    var command = row.GetProperty("command").GetString();
                    var key = command + ":" + row.GetProperty("_hash").GetInt32();
                    if (command == "Voice") voices++;
                    if (command == "Expression") expressions.Add(key);
                    if (command == "Motion") motions.Add(key);
                    Require(command != "ExpressionIcon", "Installed fixture gained icon rows; extend the independent recipe expectation");
                    if (command == "Text")
                    {
                        var actual = new List<string>(); var expected = new List<string>();
                        var groups = new List<KksAdvTextPlayback.Performance>();
                        for (var index = n.zero; index < Math.Max(expressions.Count, motions.Count); index++)
                        {
                            var expression = index < expressions.Count ? expressions[index] : null;
                            var motion = index < motions.Count ? motions[index] : null;
                            if (expression != null) expected.Add(expression);
                            if (motion != null) expected.Add(motion);
                            groups.Add(new KksAdvTextPlayback.Performance(expression == null ? null : () => actual.Add(expression), null,
                                motion == null ? null : () => actual.Add(motion), motion == null ? null : (completed, failed) => { completed(); return new Ticket(); }));
                        }
                        var count = voices;
                        Func<Action, Action, Action<Exception>, IDisposable> play = count == n.zero ? null : (started, ended, failed) =>
                        {
                            for (var index = n.zero; index < count; index++) started(); ended(); return new Ticket();
                        };
                        int done = n.zero;
                        var playback = new KksAdvTextPlayback(groups, motions.Count, play, () => false, n.zero, n.one);
                        playback.Start(() => done++, exception => throw exception);
                        Require(done == (count == n.zero || count >= groups.Count ? n.one : n.zero), "Installed voice/performance count changed natural completion");
                        playback.End(done > n.zero);
                        Require(actual.SequenceEqual(expected), "Installed text recipe lost or reordered a performance row");
                        captions++; voiceGroups += count; performanceRows += groups.Count;
                        if (count == n.zero) noVoiceCaptions++;
                    }
                    // Native MessageWindowProc clears currentCharaData before the next batch.
                    if (row.GetProperty("_multi").GetInt32() == n.zero) { voices = n.zero; expressions.Clear(); motions.Clear(); }
                }
            }
        }
        finally { foreach (var document in source.Values) document.Dispose(); }
        Require(captions > n.zero && voiceGroups > n.zero && noVoiceCaptions > n.zero, "No substantive installed text recipes checked");
        File.WriteAllText(Path.Combine(directory, "adv-text-recipe-checks.json"), JsonSerializer.Serialize(new
        {
            scope = "Installed entry-point Text recipes and production choreography with synthetic voice/motion completion events; no native rendering/playback",
            captions, voiceGroups, performanceRows, noVoiceCaptions, performanceOrderMatched = true
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"PASS ADV installed text recipes captions={captions} voiceGroups={voiceGroups} performanceRows={performanceRows} noVoiceCaptions={noVoiceCaptions}");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
