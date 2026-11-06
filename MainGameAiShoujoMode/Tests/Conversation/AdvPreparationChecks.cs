using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoMode.Conversation;

internal static class AdvPreparationChecks
{
    private sealed class Lease : IDisposable
    {
        internal Action Cancel;
        public void Dispose() { var cancel = Cancel; Cancel = null; cancel?.Invoke(); }
    }
    private sealed class Command : KksAdvMachine.ICommand
    {
        internal Action Started;
        public void Start(Action completed, Action<Exception> failed) { Started(); completed(); }
        public void End(bool processEnd) { }
    }
    private sealed class Host : KksAdvMachine.IHost
    {
        internal readonly List<string> Started = new List<string>();
        internal Action BeforeCreate;
        public bool IsRegulated => false;
        public bool TextComplete => true;
        public void CompleteText() { }
        public void ClearText() { }
        public void ClearCurrentCharaData() { }
        public KksAdvMachine.ICommand Create(KksAdvMachine.Context context)
        { BeforeCreate?.Invoke(); return new Command { Started = () => { Started.Add(context.Entry.Asset + ":" + context.Entry.Row.Command); if (context.Entry.Row.Command == "Close") context.Close(); } }; }
    }
    private sealed class Ticket
    {
        internal Action Ready, Cancel;
        internal Action<Exception> Failed;
        internal int Releases;
        internal IDisposable Lease() => new Lease { Cancel = () => { Releases++; Cancel?.Invoke(); } };
    }
    internal static void Run(string directory, int zero, int one)
    {
        Console.WriteLine("Start ADV pre-mutation preparation, cancellation and tape-preview checks");
        KksAdvScript.Row Row(string command, bool multi = false) => new KksAdvScript.Row(zero, zero, multi, command, Array.Empty<string>());
        KksAdvScript Script(string asset) => new KksAdvScript("test", asset, new[] { Row("CharaMotion", true), Row("Text"), Row("Close") });
        var tape = new KksAdvTape(zero, one); tape.Load(Script("preview"), true, true); var line = tape.CurrentLine;
        var preview = tape.PreviewNextBatch();
        Require(preview.Count == one + one && preview.Last().Row.Command == "Text" && tape.CurrentLine == line, "Preview consumed/reordered batch or crossed Multi boundary");
        tape.Load(new KksAdvScript("test", "opaque", new[] { Row("ParameterFile", true), Row("Text") }), true, true);
        Require(tape.PreviewNextBatch().Count == one && KksAdvFramePreparation.RequiresCapture(tape.PreviewNextBatch()), "Opaque tape expansion was treated as a fully visible batch");
        Require(!KksAdvFramePreparation.RequiresCapture(new[] { new KksAdvTape.Entry("test", "audio", zero, false, Row("BGMPlay")) }), "Audio-only batch requested image capture");

        var host = new Host(); var tickets = new List<Ticket>(); var clock = new List<bool>(); int closed = zero;
        var prepare = KksAdvFramePreparation.Create((ready, failed) =>
        { var ticket = new Ticket { Ready = ready, Failed = failed }; tickets.Add(ticket); return ticket.Lease(); });
        using (var machine = new KksAdvMachine(host, zero, one, value => clock.Add(value), prepare))
        {
            machine.Start(Script("first"), () => closed++, exception => throw exception);
            Require(machine.HardWaiting && !host.Started.Any() && tickets.Count == one && clock.Last(), "Commands or clock ran before capture readiness");
            machine.Advance(); Require(!host.Started.Any() && clock.Last(), "Click resumed processing while preparation was pending");
            var first = tickets.Single(); first.Ready(); first.Ready();
            Require(host.Started.SequenceEqual(new[] { "first:CharaMotion", "first:Text" }) && first.Releases == one && !clock.Last(), "Ready duplicated mutation or leaked preparation ownership");
            machine.Advance(); Require(closed == one && tickets.Count == one, "Close-only batch unnecessarily captured another image");
            machine.Start(Script("cancelled"), () => closed++, exception => throw exception);
            var cancelled = tickets.Last(); machine.Stop(); cancelled.Ready(); cancelled.Failed(new IOException("stale"));
            Require(cancelled.Releases == one && host.Started.Count == one + one + one && closed == one, "Stopped preparation resumed commands or failed twice");
            Exception error = null;
            machine.Start(Script("failed"), () => closed++, exception => error = exception);
            var failed = tickets.Last(); failed.Failed(new IOException("capture")); failed.Ready();
            Require(error?.InnerException is IOException && !machine.Active && failed.Releases == one && closed == one, "Capture failure did not cancel before mutation");
            machine.Start(Script("old"), () => closed++, exception => throw exception);
            var old = tickets.Last(); old.Cancel = () => machine.Start(Script("reentrant"), () => closed++, exception => throw exception);
            machine.Start(Script("superseded"), () => closed++, exception => throw exception);
            old.Ready(); tickets.Last().Ready();
            Require(host.Started.Last() == "reentrant:Text" && !host.Started.Any(value => value.StartsWith("superseded:", StringComparison.Ordinal)), "Cancellation callback's newer Start was overwritten");
        }
        host = new Host(); int released = zero;
        host.BeforeCreate = () => Require(released > zero, "Mutation began before synchronous preparation lease adoption/release");
        using (var machine = new KksAdvMachine(host, zero, one, null, (entries, ready, failed) =>
        { ready(); ready(); return new Lease { Cancel = () => released++ }; }))
        { machine.Start(Script("sync"), () => { }, exception => throw exception); Require(host.Started.Count == one + one && released == one, "Synchronous readiness duplicated commands"); }
        host = new Host(); KksAdvMachine interrupted = null; int adopted = zero;
        using (interrupted = new KksAdvMachine(host, zero, one, null, (entries, ready, failed) =>
        { interrupted.Stop(); return new Lease { Cancel = () => adopted++ }; }))
        { interrupted.Start(Script("interrupted"), () => { }, exception => throw exception); Require(!host.Started.Any() && adopted == one, "Stop during acquisition leaked the returned lease"); }
        host = new Host(); Exception releaseFailure = null;
        using (var machine = new KksAdvMachine(host, zero, one, null, (entries, ready, failed) =>
        { ready(); return new Lease { Cancel = () => throw new IOException("release") }; }))
        { machine.Start(Script("release-failure"), () => { }, exception => releaseFailure = exception); Require(releaseFailure is IOException && !host.Started.Any() && !machine.Active, "Preparation cleanup failure allowed mutation"); }
        File.WriteAllText(Path.Combine(directory, "adv-preparation-checks.json"), JsonSerializer.Serialize(new
        {
            scope = "Production ADV machine/tape/frame policy with synthetic readiness events; no Unity frame capture",
            previewDoesNotConsume = true, opaqueExpansionRequiresCapture = true, noMutationBeforeReady = true,
            clockAndInputSuspended = true, duplicateReadyRejected = true, stopAndStaleCallbacks = true,
            preparationFailureStopsBeforeMutation = true, reentrantStartPreserved = true, synchronousLeaseAdoption = true,
            stopDuringAcquisitionReleasesReturnedLease = true, leaseReleaseFailureStopsBeforeMutation = true
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS ADV preparation preview readiness cancellation reentrancy clock stale failure");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
