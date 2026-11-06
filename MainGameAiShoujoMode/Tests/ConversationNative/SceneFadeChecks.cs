using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoMode.Conversation;

internal static class SceneFadeChecks
{
    private sealed class Immediate : KksAdvMachine.ICommand
    {
        public void Start(Action completed, Action<Exception> failed) => completed();
        public void End(bool processEnd) { }
    }
    private sealed class Host : KksAdvMachine.IHost
    {
        internal int Captions;
        public bool IsRegulated => false;
        public bool TextComplete => true;
        public void CompleteText() { }
        public void ClearText() { }
        public void ClearCurrentCharaData() { }
        public KksAdvMachine.ICommand Create(KksAdvMachine.Context context)
        { if (context.Entry.Row.Command == "Text") Captions++; return new Immediate(); }
    }
    private sealed class Backend : KksAdvSceneFadeHost.IBackend
    {
        internal sealed class Operation : KksAdvSceneFadeHost.IOperation
        {
            internal Backend Owner; internal Action Completed; internal bool FadeIn, Ended;
            public void Finish()
            {
                if (Ended) throw new Exception("Fade finalized twice"); Ended = true;
                Owner.Finishes++; Owner.IsFading = FadeIn; Owner.Trace.Add("force:" + FadeIn); Owner.Trace.Add("default-time");
            }
            public void Dispose() { Owner.Releases++; }
        }
        internal readonly List<string> Trace = new List<string>();
        internal readonly List<Operation> Operations = new List<Operation>();
        internal int Finishes, Releases;
        public bool IsFading { get; set; }
        public KksAdvSceneFadeHost.IOperation StartFade(bool fadeIn, Action completed, Action<Exception> failed)
        {
            Trace.Add("start:" + fadeIn); if (fadeIn) IsFading = true;
            var operation = new Operation { Owner = this, Completed = completed, FadeIn = fadeIn }; Operations.Add(operation); return operation;
        }
        public void SetTime(float duration) => Trace.Add("time:" + duration);
        public void SetColor(string color) => Trace.Add("color:" + color);
        internal void Complete(bool covered)
        { IsFading = covered; foreach (var operation in Operations.ToArray()) if (!operation.Ended) operation.Completed(); }
    }
    internal static void Run(string directory, int zero, int one)
    {
        Console.WriteLine("Start native SceneFade host lifecycle checks with mock canvas");
        KksAdvScript.Row Row(string command, bool multi = false, params string[] args) => new KksAdvScript.Row(zero, zero, multi, command, args);
        var host = new Host(); var backend = new Backend { IsFading = true }; var suspended = new List<bool>();
        KksAdvMachine machine = null;
        var fadeHost = new KksAdvSceneFadeHost(host, backend, value => value, () => machine?.NotifyPresentationChanged());
        var core = new KksAdvCoreHost(fadeHost, new EmptyAssets(), value => value, (bundle, asset) => { }, null, zero);
        using (machine = new KksAdvMachine(core, zero, one, value => suspended.Add(value)))
        {
            int closed = zero;
            machine.Start(new KksAdvScript("test", "regulated", new[] { Row("SceneFade", false, "out", one.ToString(), "red", "unused", "unused"), Row("Text"), Row("Close") }), () => closed++, exception => throw exception);
            Require(fadeHost.IsRegulated && host.Captions == zero && backend.Trace.SequenceEqual(new[] { "start:False", "time:" + one, "color:red" }), "SceneFade native start/time/color order changed");
            machine.Advance(); Require(host.Captions == zero && backend.Finishes == zero && suspended.Last(), "Regulated click advanced or finalized fade");
            backend.Complete(false);
            Require(host.Captions == one && backend.Finishes == one && backend.Releases == one && !suspended.Last(), "Scene fade completion did not resume command processing/clock");
            machine.Advance(); Require(closed == one, "Scene fade completion failed to reach Close");
        }
        host = new Host(); backend = new Backend(); machine = null;
        fadeHost = new KksAdvSceneFadeHost(host, backend, value => value == "disabled" ? bool.FalseString : value, () => machine?.NotifyPresentationChanged());
        core = new KksAdvCoreHost(fadeHost, new EmptyAssets(), value => value, (bundle, asset) => { }, null, zero);
        using (machine = new KksAdvMachine(core, zero, one))
        {
            machine.Start(new KksAdvScript("test", "unregulated", new[] { Row("SceneFadeRegulate", true, "disabled"), Row("SceneFade", false, "IN"), Row("Text"), Row("Close") }), () => { }, exception => throw exception);
            var stale = backend.Operations.Single().Completed;
            machine.Advance(); Require(host.Captions == one && backend.Finishes == one && backend.Releases == one && !fadeHost.IsRegulated, "Unregulated skip failed to force fade endpoint");
            machine.Stop(); stale(); Require(!machine.Active && host.Captions == one && backend.Finishes == one, "Stale fade completion resumed stopped run");
        }
        host = new Host(); backend = new Backend();
        fadeHost = new KksAdvSceneFadeHost(host, backend, value => value, null);
        core = new KksAdvCoreHost(fadeHost, new EmptyAssets(), value => value, (bundle, asset) => { }, null, zero);
        using (machine = new KksAdvMachine(core, zero, one))
        {
            Exception error = null;
            machine.Start(new KksAdvScript("test", "malformed", new[] { Row("SceneFade", false, "in", "bad-duration"), Row("Close") }), () => { }, exception => error = exception);
            Require(error is FormatException && !machine.Active && backend.Finishes == one && backend.Releases == one
                && backend.Trace.First() == "start:True", "Bad optional time failed to finalize the already-started native transition");
        }
        File.WriteAllText(Path.Combine(directory, "adv-scene-fade-checks.json"), JsonSerializer.Serialize(new
        {
            scope = "Production SceneFade/SceneFadeRegulate host and machine; native canvas/engine events mocked",
            nativeStartBeforeOptionalArguments = true, globalRegulationStopsInputAndClock = true,
            completionResumes = true, unregulatedAdvanceForcesEndpoint = true,
            staleCompletionRejected = true, optionalParseFailureFinalized = true
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS SceneFade host lifecycle and native argument order; mock canvas=true");
    }
    private sealed class EmptyAssets : KksAdvCoreHost.IAssets
    {
        public string ResolveBundle(string bundle, string loadBundle) => bundle;
        public KksAdvScript Load(string bundle, string asset, string loadBundle) => throw new NotSupportedException();
        public bool TryParameter(string name, out KksAdvScript script) { script = null; return false; }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
