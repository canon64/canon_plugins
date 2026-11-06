using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoMode.Conversation;

internal static class AdvMachineChecks
{
    private sealed class Operation : KksAdvMachine.ICommand
    {
        internal RecordingHost Host;
        internal KksAdvMachine.Context Context;
        internal Action Complete;
        internal Action<Exception> Fail;
        internal bool? Ended;
        internal string Name => Context.Entry.Row.Command;
        public void Start(Action completed, Action<Exception> failed)
        {
            Complete = completed; Fail = failed; Host.Started.Add(this); Host.Trace.Add(Name + ".start");
            Host.OnStart?.Invoke(this);
            if (!Host.Deferred.Contains(Name)) completed();
        }
        public void End(bool processEnd)
        {
            Require(!Ended.HasValue, "ADV operation ended twice"); Ended = processEnd;
            Host.Trace.Add(Name + ".end=" + processEnd); Host.OnEnd?.Invoke(this);
        }
    }
    private sealed class RecordingHost : KksAdvMachine.IHost
    {
        internal readonly HashSet<string> Deferred = new HashSet<string> { "Text" };
        internal readonly List<Operation> Started = new List<Operation>();
        internal readonly List<string> Trace = new List<string>();
        internal Action<Operation> OnStart, OnEnd;
        internal Action<KksAdvMachine.Context> OnCreate;
        internal bool Regulated, CompleteDisplay = true;
        internal string Unsupported;
        internal int ForcedText, Clears, ClearedText;
        public bool IsRegulated => Regulated;
        public bool TextComplete => CompleteDisplay;
        public void CompleteText() { ForcedText++; CompleteDisplay = true; }
        public void ClearText() { ClearedText++; CompleteDisplay = true; }
        public void ClearCurrentCharaData() { Clears++; }
        public KksAdvMachine.ICommand Create(KksAdvMachine.Context context)
        {
            OnCreate?.Invoke(context);
            if (context.Entry.Row.Command == Unsupported) return null;
            return new Operation { Host = this, Context = context };
        }
    }
    private sealed class Assets : KksAdvCoreHost.IAssets
    {
        internal readonly Dictionary<(string Bundle, string Asset), KksAdvScript> Scripts = new Dictionary<(string, string), KksAdvScript>();
        internal readonly Dictionary<string, KksAdvScript> Parameters = new Dictionary<string, KksAdvScript>(StringComparer.Ordinal);
        public string ResolveBundle(string bundle, string loadBundle)
        {
            bundle = string.IsNullOrEmpty(bundle) ? loadBundle : bundle;
            return Scripts.Keys.Any(key => key.Bundle == bundle) ? bundle : "adv/scenario/" + bundle + ".unity3d";
        }
        public KksAdvScript Load(string bundle, string asset, string loadBundle) => Scripts[(ResolveBundle(bundle, loadBundle), asset)];
        public bool TryParameter(string name, out KksAdvScript script) => Parameters.TryGetValue(name, out script);
        internal KksAdvScript Add(string bundle, string asset, params KksAdvScript.Row[] rows)
        { var script = new KksAdvScript(bundle, asset, rows); Scripts.Add((bundle, asset), script); return script; }
    }
    private sealed class Case : IDisposable
    {
        internal readonly RecordingHost Host = new RecordingHost();
        internal readonly Assets Assets = new Assets();
        internal readonly Dictionary<string, string> Vars = new Dictionary<string, string>();
        internal readonly List<string> Diagnostics = new List<string>();
        internal readonly KksAdvMachine Machine;
        internal int Successes, Failures;
        internal Exception Error;
        internal Case(KksNormalConversationRules.Settings numbers, Action<KksAdvMachine> stateChanged = null)
        {
            var core = new KksAdvCoreHost(Host, Assets, text => Vars.TryGetValue(text, out var value) ? value : text,
                (bundle, asset) => { Vars["BundleFile"] = bundle; Vars["AssetFile"] = asset; }, Diagnostics.Add, numbers.zero);
            Machine = new KksAdvMachine(core, numbers.zero, numbers.one, stateChanged: () => stateChanged?.Invoke(Machine));
        }
        internal void Start(KksAdvScript script) => Machine.Start(script, () => Successes++, exception => { Failures++; Error = exception; });
        public void Dispose() => Machine.Dispose();
    }
    internal static void Run(string directory, KksNormalConversationRules.Settings n)
    {
        Console.WriteLine("Start event-driven ADV batches, native control instructions and lifecycle checks");
        KksAdvScript.Row Row(string name, bool multi = false, params string[] args) => new KksAdvScript.Row(n.one, n.zero, multi, name, args);
        using (var test = new Case(n))
        {
            test.Host.Deferred.UnionWith(new[] { "Voice", "Motion", "Expression" });
            test.Start(test.Assets.Add("root", "multi", Row("Voice", true), Row("Motion", true), Row("Expression", true), Row("Text"), Row("Close")));
            Require(test.Host.Started.Select(op => op.Name).SequenceEqual(new[] { "Voice", "Motion", "Expression", "Text" }), "Multi commands were awaited serially");
            Require(test.Host.Started.All(op => !op.Ended.HasValue), "Pending batch was finalized before advance");
            test.Machine.Advance();
            Require(test.Successes == n.one && test.Host.Started.All(op => op.Ended == false), "Manual advance did not force-end pending operations");
        }
        using (var test = new Case(n))
        {
            test.Host.Deferred.Add("Voice");
            test.Start(test.Assets.Add("root", "natural", Row("Voice", true), Row("Text"), Row("Close")));
            var voice = test.Host.Started.First(); var text = test.Host.Started.Last();
            voice.Complete(); voice.Complete(); text.Complete();
            Require(voice.Ended == true && text.Ended == true && test.Machine.Active, "Natural completion lost its Result(true) or skipped text input");
            test.Machine.Advance(); Require(test.Successes == n.one, "Completed batch failed to advance");
            text.Complete(); text.Fail(new IOException("stale")); Require(test.Failures == n.zero, "Completed command failed a closed run");
        }
        using (var test = new Case(n))
        {
            test.Host.CompleteDisplay = false;
            test.Start(test.Assets.Add("root", "typewriter", Row("Text"), Row("Close")));
            test.Machine.Advance();
            Require(test.Host.ForcedText == n.one && test.Host.Clears == n.zero && test.Machine.Active && test.Host.Started.Single().Ended == false,
                "First click advanced instead of completing text with ForceEnd(false)");
            test.Machine.Advance(); Require(test.Successes == n.one, "Second click did not advance completed text");
        }
        foreach (var name in new[] { "WaitAbs", "Selection", "KoikatsuPointAdd", "GetTopic" })
        {
            using var test = new Case(n); test.Host.Deferred.Add(name);
            test.Start(test.Assets.Add("root", name, Row(name), Row("Text"), Row("Close")));
            test.Machine.Advance(); test.Machine.Advance(KksAdvMachine.AdvanceSource.Skip); test.Machine.Advance(KksAdvMachine.AdvanceSource.Auto);
            Require(test.Machine.HardWaiting && test.Host.Started.Single().Name == name, "Hard wait was bypassed by an input source");
            test.Host.Started.Single().Complete();
            Require(test.Host.Started.Last().Name == "Text" && test.Machine.Active && !test.Machine.HardWaiting, "Hard-wait completion retained blocked input or failed to resume");
            test.Machine.Advance(); Require(test.Successes == n.one, "Hard wait route failed to close");
        }
        using (var test = new Case(n))
        {
            test.Host.Deferred.Add("Wait");
            test.Start(test.Assets.Add("root", "wait", Row("Wait"), Row("Text"), Row("Close")));
            test.Machine.Advance(KksAdvMachine.AdvanceSource.Auto);
            Require(test.Host.Started.Single().Name == "Wait", "Auto bypassed a pending non-text wait");
            test.Machine.Advance();
            Require(test.Host.Started.First().Ended == false && test.Host.Started.Last().Name == "Text", "Ordinary wait was incorrectly unskippable");
        }
        using (var test = new Case(n))
        {
            test.Start(test.Assets.Add("root", "auto", Row("Text"), Row("Close")));
            test.Machine.Advance(KksAdvMachine.AdvanceSource.Auto);
            Require(test.Machine.Active, "Auto ignored ongoing speech/presentation");
            test.Host.Started.Single().Complete(); test.Machine.Advance(KksAdvMachine.AdvanceSource.Auto);
            Require(test.Successes == n.one, "Auto failed after presentation completion");
        }
        using (var test = new Case(n))
        {
            test.Host.Regulated = true;
            test.Start(test.Assets.Add("root", "regulation", Row("Voice", true), Row("Text"), Row("Close")));
            test.Machine.Advance(); Require(test.Host.Started.First().Ended == null, "Global regulation processed commands or retained a click");
            test.Host.Regulated = false; test.Machine.NotifyPresentationChanged();
            Require(test.Host.Started.First().Ended == true && test.Machine.Active, "Regulation release did not process queued completion");
            test.Machine.Advance(); Require(test.Successes == n.one, "Regulated route failed to close");
        }
        using (var test = new Case(n))
        {
            var source = test.Assets.Add("root", "stale", Row("Text"), Row("Close"));
            test.Start(source); var old = test.Host.Started.Single(); test.Machine.Stop(); test.Start(source);
            old.Complete(); old.Fail(new IOException("old failure")); old.Context.Close();
            Require(!old.Context.Open(source, true, true, true) && !old.Context.Jump("ignored", text => text), "Stopped command mutated a new run");
            Require(test.Machine.Active && test.Failures == n.zero && old.Ended == false, "Old callback entered or ended its replacement run");
        }
        using (var test = new Case(n))
        {
            test.Host.Deferred.UnionWith(new[] { "Voice", "Motion" });
            test.Host.OnEnd = op => { if (op.Name == "Voice") throw new IOException("cleanup failure"); };
            test.Start(test.Assets.Add("root", "cleanup", Row("Voice", true), Row("Motion", true), Row("Text"), Row("Close")));
            try { test.Machine.Stop(); throw new Exception("Cleanup error was swallowed"); } catch (AggregateException) { }
            Require(test.Host.Started.All(op => op.Ended == false) && !test.Machine.Active && test.Successes == n.zero, "Cleanup failure leaked later commands or committed a result");
        }
        using (var test = new Case(n))
        {
            test.Host.OnStart = op => { if (op.Name == "Motion") throw new IOException("start failure"); };
            test.Start(test.Assets.Add("root", "failure", Row("Voice", true), Row("Motion", true), Row("Text"), Row("Close")));
            Require(test.Failures == n.one && !test.Machine.Active && test.Host.Started.All(op => op.Ended == false), "Start failure did not clean all registered operations");
            test.Host.OnStart = op => op.Fail(new IOException("inline failure"));
            test.Start(test.Assets.Add("root", "inline", Row("Motion", true), Row("Text"), Row("Close")));
            Require(test.Failures == n.one + n.one && test.Host.Started.Last().Name == "Motion", "Inline failure began subsequent Multi commands");
        }
        using (var test = new Case(n))
        {
            test.Host.Unsupported = "Unknown";
            test.Start(test.Assets.Add("root", "unsupported", Row("Unknown"), Row("Close")));
            Require(test.Failures == n.one && test.Successes == n.zero && test.Error is NotSupportedException, "Unknown command was silently successful");
            Require(test.Error.Message.Contains("sourceRow=") && test.Error.Message.Contains("unsupported"), "Missing handler diagnostics lost source location");
        }
        using (var test = new Case(n))
        {
            test.Host.Deferred.Add("Voice"); test.Host.Deferred.Add("Motion");
            test.Host.OnEnd = op => { if (op.Name == "Voice" && op.Ended == true) throw new IOException("natural result failure"); };
            test.Start(test.Assets.Add("root", "result-failure", Row("Voice", true), Row("Motion", true), Row("Text"), Row("Close")));
            test.Host.Started.First().Complete();
            Require(test.Failures == n.one && !test.Machine.Active && test.Host.Started.Skip(n.one).All(op => op.Ended == false), "Natural result failure leaked pending operations");
        }
        using (var test = new Case(n))
        {
            var replacement = test.Assets.Add("root", "after-error", Row("Text"), Row("Close"));
            test.Host.Unsupported = "Unknown";
            test.Machine.Start(test.Assets.Add("root", "recover", Row("Unknown")), () => throw new Exception("Unexpected success"),
                _ => { test.Failures++; test.Start(replacement); });
            Require(test.Failures == n.one && test.Machine.Active && test.Host.Started.Single().Context.Entry.Asset == replacement.Asset, "Failure-handler replacement did not start");
            test.Machine.Advance(); Require(test.Successes == n.one, "Failure-handler replacement stalled");
            test.Machine.Start(replacement, () => throw new IOException("terminal save failed"), _ => test.Failures++);
            test.Machine.Advance(); Require(test.Failures == n.one + n.one && !test.Machine.Active, "Terminal persistence failure was swallowed");
        }
        using (var test = new Case(n))
        {
            test.Host.OnCreate = _ => test.Machine.Stop();
            test.Start(test.Assets.Add("root", "cancel-create", Row("Text"), Row("Close")));
            Require(!test.Machine.Active && !test.Host.Started.Any() && test.Host.Trace.Single() == "Text.end=False", "Operation created during cancellation was started or leaked");
        }
        using (var test = new Case(n))
        {
            var replacement = test.Assets.Add("root", "replacement", Row("Text", false, "", "replacement"), Row("Close"));
            bool replaced = false;
            test.Host.OnEnd = op => { if (!replaced && op.Ended == true) { replaced = true; test.Start(replacement); } };
            test.Start(test.Assets.Add("root", "reentrant", Row("Text"), Row("Close")));
            test.Host.Started.First().Complete();
            Require(test.Machine.Active && test.Host.Started.Last().Context.Entry.Asset == replacement.Asset, "Natural finalizer failed to start its replacement");
            test.Machine.Advance(); Require(test.Successes == n.one && test.Failures == n.zero, "Replacement from a finalizer failed");
        }
        using (var test = new Case(n))
        {
            var newer = test.Assets.Add("root", "newer", Row("Text"), Row("Close"));
            var older = test.Assets.Add("root", "older-request", Row("Text"), Row("Close"));
            bool replaced = false;
            test.Start(test.Assets.Add("root", "old", Row("Text"), Row("Close")));
            test.Host.OnEnd = _ => { if (!replaced) { replaced = true; test.Start(newer); } };
            test.Start(older);
            Require(test.Host.Started.Last().Context.Entry.Asset == newer.Asset && test.Machine.Active, "An older Start overwrote a newer Start from cleanup");
        }
        using (var test = new Case(n))
        {
            test.Host.Deferred.Add("Voice"); test.Host.Regulated = true;
            test.Start(test.Assets.Add("root", "close", Row("Voice", true), Row("Close", true), Row("Poison")));
            Require(test.Successes == n.one && test.Host.Started.Single().Ended == false, "Close waited on regulation, ran subsequent rows, or leaked a pending operation");
        }
        using (var test = new Case(n))
        {
            test.Start(test.Assets.Add("root", "ignored", Row("None", true), Row("Log", true), Row("Text"), Row("Close")));
            Require(test.Host.Started.Single().Name == "Text", "None/Log were dispatched as active commands");
            test.Machine.Advance(); Require(test.Successes == n.one, "Ignored command boundaries stalled the machine");
        }
        bool nextVisible = false, presentationRegulated = false;
        using (var test = new Case(n, machine => nextVisible = machine.HasText && !machine.HardWaiting && !presentationRegulated))
        {
            test.Host.Deferred.Add("WaitAbs");
            test.Start(test.Assets.Add("root", "text-and-hard-wait", Row("Text", true), Row("WaitAbs"), Row("Close")));
            Require(!nextVisible && test.Machine.HardWaiting, "Next indicator observed an incomplete Multi batch before its hard wait started");
            test.Host.Started.Last().Complete();
            Require(nextVisible && test.Machine.Active, "Hard-wait completion did not publish the settled text-ready state");
            test.Host.Regulated = presentationRegulated = true; test.Machine.NotifyPresentationChanged();
            Require(!nextVisible && test.Machine.Active, "Opening the log retained the next indicator");
            test.Host.Regulated = presentationRegulated = false; test.Machine.NotifyPresentationChanged();
            Require(nextVisible, "Closing the log did not restore the next indicator");
            test.Machine.Advance(); Require(test.Successes == n.one, "Published text-ready state failed to advance");
        }
        using (var test = new Case(n, machine => throw new IOException("display state update")))
        {
            test.Start(test.Assets.Add("root", "state-display-failure", Row("Text"), Row("Close")));
            Require(test.Failures == n.one && !test.Machine.Active, "Display-state observer failure escaped run cleanup");
        }
        CheckControls(n, Row);
        CheckInstalledEntries(directory, n);
        File.WriteAllText(Path.Combine(directory, "adv-machine-checks.json"), JsonSerializer.Serialize(new
        {
            scope = "Production batch/control machine with mocked presentation and in-memory assets; no Unity playback or world effects",
            multiStart = true, naturalVsForcedEnd = true, textTwoClicks = true, hardVsSoftWait = true, autoAndRegulation = true,
            staleCallbacksAndContexts = true, cleanupFailures = true, reentrantReplacement = true, unknownCommandRejected = true,
            settledMultiStatePublished = true, asynchronousHardWaitRefreshesNext = true, stateDisplayFailureCleaned = true,
            nativeControlCommands = new[] { "Tag", "Jump", "Open", "ParameterFile", "Close" }
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS ADV event batches controls lifecycle cancellation reentrancy failures");
    }

    private static void CheckControls(KksNormalConversationRules.Settings n, Func<string, bool, string[], KksAdvScript.Row> row)
    {
        KksAdvScript.Row R(string name, bool multi = false, params string[] args) => row(name, multi, args);
        using (var test = new Case(n))
        {
            var parameter = test.Assets.Add("params", "p", R("Text", false, "", "parameter")); test.Assets.Parameters.Add("p", parameter);
            test.Start(test.Assets.Add("root", "params", R("ParameterFile", false, "p"), R("ParameterFile", false, "missing"), R("Text", false, "", "main"), R("Close")));
            Require(test.Host.Started.Single().Context.Entry.Asset == "p", "ParameterFile did not execute its inserted source first");
            test.Machine.Advance(); Require(test.Host.Started.Last().Context.Entry.Asset == "params", "ParameterFile did not return to remaining caller rows");
            Require(test.Diagnostics.Single().Contains("missing"), "Missing native parameter key was not diagnosed"); test.Machine.Advance();
            Require(test.Successes == n.one, "Parameter route failed");
        }
        using (var test = new Case(n))
        {
            test.Vars.Add("LabelKey", "target");
            test.Start(test.Assets.Add("root", "jump", R("Jump", false, "target"), R("Poison"), R("Tag", true, "LabelKey"), R("Text"), R("Close")));
            Require(test.Host.Started.Single().Name == "Text", "Tag jump did not skip the intervening command");
            test.Machine.Advance(); Require(test.Successes == n.one, "Tag jump failed to close");
        }
        using (var test = new Case(n))
        {
            var target = test.Assets.Add("adv/scenario/other.unity3d", "target", R("Text"), R("Close"));
            test.Start(test.Assets.Add("root", "filejump", R("Jump", false, "other:target"), R("Poison")));
            Require(test.Host.Started.Single().Context.Entry.Bundle == target.Bundle && test.Vars["AssetFile"] == target.Asset
                && test.Host.ClearedText == n.one, "File jump did not resolve and replace the current script");
            test.Machine.Advance(); Require(test.Successes == n.one, "File jump failed to close");
        }
        using (var test = new Case(n))
        {
            test.Assets.Add("child", "extra", R("Text", false, "", "child"));
            test.Start(test.Assets.Add("root", "add", R("Open", false, "child", "extra", "True"),
                R("Open", false, "child", "extra", "True"), R("Text", false, "", "main"), R("Close")));
            Require(test.Host.Started.Single().Context.LoadBundle == "root", "Additive Open changed the root bundle");
            test.Machine.Advance(); Require(test.Host.Started.Last().Context.Entry.Asset == "add" && test.Host.Started.Count == n.one + n.one,
                "Duplicate additive Open replayed inserted commands");
            Require(test.Host.ClearedText == n.zero, "Additive Open cleared the text window"); test.Machine.Advance();
        }
        foreach (var next in new[] { false, true })
        {
            using var test = new Case(n); test.Assets.Add("child", "extra", R("Text"));
            test.Start(test.Assets.Add("root", "next", R("Text", true), R("Open", false, "child", "extra", "True", "True", next.ToString()), R("Close")));
            Require(test.Host.Started.Count == (next ? n.one + n.one : n.one), "Open isNext did not distinguish immediate request from current-text wait");
            if (!next) test.Machine.Advance();
            Require(test.Host.Started.Last().Context.Entry.Bundle == "child", "Open next route did not enter inserted script");
            test.Machine.Advance(); Require(test.Successes == n.one, "Open next route failed to close");
        }
        using (var test = new Case(n))
        {
            test.Start(test.Assets.Add("root", "badopen", R("Open", false, "missing", "asset"), R("Close")));
            Require(test.Failures == n.one && test.Host.ClearedText == n.one && test.Vars["AssetFile"] == "asset", "Open failure changed native variable/clear ordering");
        }
        using (var test = new Case(n))
        {
            var many = Enumerable.Repeat(R("Tag", true, "unused"), n.requiredNicknameEvent * n.temperamentCount).Concat(new[] { R("Text"), R("Close") }).ToArray();
            test.Start(test.Assets.Add("root", "long-multi", many));
            Require(test.Host.Started.Single().Name == "Text" && test.Machine.Active, "Long synchronous batch recursed or failed to reach text");
            test.Machine.Advance(); Require(test.Successes == n.one, "Long batch failed to close");
        }
    }

    private static void CheckInstalledEntries(string directory, KksNormalConversationRules.Settings n)
    {
        using var graph = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "scenario-graph.json")));
        var needed = graph.RootElement.GetProperty("nodes").EnumerateArray().Select(node =>
            (Bundle: node.GetProperty("bundle").GetString(), Asset: node.GetProperty("asset").GetString())).ToHashSet();
        using var test = new Case(n);
        foreach (var bundle in needed.Select(key => key.Bundle).Distinct())
        {
            using var source = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "bundles", bundle.Replace('/', '_') + ".json")));
            foreach (var asset in source.RootElement.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString();
                if (!needed.Contains((bundle, name))) continue;
                test.Assets.Add(bundle, name, asset.GetProperty("data").GetProperty("list").EnumerateArray().Select(raw =>
                    new KksAdvScript.Row(raw.GetProperty("_hash").GetInt32(), raw.GetProperty("_version").GetInt32(), raw.GetProperty("_multi").GetInt32() != n.zero,
                        raw.GetProperty("command").GetString(), raw.GetProperty("_args").EnumerateArray().Select(arg => arg.GetString()))).ToArray());
            }
        }
        foreach (var parameter in graph.RootElement.GetProperty("parameterOverrides").EnumerateObject())
        {
            var key = (parameter.Value.GetProperty("bundle").GetString(), parameter.Value.GetProperty("asset").GetString());
            if (test.Assets.Scripts.TryGetValue(key, out var script)) test.Assets.Parameters.Add(parameter.Name, script);
        }
        int roots = n.zero, commands = n.zero, textRows = n.zero;
        foreach (var root in graph.RootElement.GetProperty("roots").EnumerateArray())
        {
            var script = test.Assets.Scripts[(root.GetProperty("bundle").GetString(), root.GetProperty("asset").GetString())];
            var expanded = script.Rows.ToList();
            for (var index = n.zero; index < expanded.Count; index++)
            {
                var current = expanded[index];
                if (current.Command == "ParameterFile" && test.Assets.Parameters.TryGetValue(current.Arguments[n.zero], out var parameter))
                    expanded.InsertRange(index + n.one, parameter.Rows);
            }
            Require(expanded.Last().Command == "Close" && !expanded.Any(row => row.Command == "Open" || row.Command == "Jump"), "Installed fixture requires a richer reference trace");
            var expected = expanded.Where(row => row.Command != "Close" && row.Command != "ParameterFile").ToArray();
            test.Host.Started.Clear(); test.Start(script);
            // Presentation is mocked as immediate except Text, which gets one input event per caption.
            var advances = n.zero;
            while (test.Machine.Active)
            {
                test.Machine.Advance(); advances++;
                Require(advances <= expanded.Count, "Installed fixture stalled without an external presentation event");
            }
            Require(test.Failures == n.zero && test.Host.Started.Select(op => op.Context.Entry.Row).SequenceEqual(expected), "Installed fixture dispatch order differs from independent parameter expansion");
            roots++; commands += expected.Length; textRows += expected.Count(row => row.Command == "Text");
        }
        Require(roots == graph.RootElement.GetProperty("roots").GetArrayLength() && roots > n.zero && test.Successes == roots, "Not all installed entry points closed");
        File.WriteAllText(Path.Combine(directory, "adv-entry-dispatch-checks.json"), JsonSerializer.Serialize(new
        { scope = "Production control flow and dispatch order; mocked presentation and world commands, not executed Unity effects", roots, dispatched = commands, captions = textRows, parameterExpansionMatched = true }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"PASS ADV installed-entry dispatch roots={roots} commands={commands} captions={textRows} mockedPresentation=true");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
