using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoMode.Conversation;

internal static class AdvOwnedSessionChecks
{
    private sealed class Resource : IDisposable
    {
        internal Action Close;
        public void Dispose() { var close = Close; Close = null; close?.Invoke(); }
    }
    private sealed class Command : KksAdvMachine.ICommand
    {
        internal Host Host;
        internal KksAdvMachine.Context Context;
        public void Start(Action completed, Action<Exception> failed)
        {
            if (Context.Entry.Row.Command == "WaitAbs") Host.Waits.Add(completed);
            else { if (Context.Entry.Row.Command == "Close") Context.Close(); completed(); }
        }
        public void End(bool processEnd) { Host.Trace.Add("command:" + processEnd); }
    }
    private sealed class Host : KksAdvMachine.IHost
    {
        internal readonly List<Action> Waits = new List<Action>();
        internal readonly List<string> Trace = new List<string>();
        public bool IsRegulated => false;
        public bool TextComplete => true;
        public void CompleteText() { }
        public void ClearText() { }
        public void ClearCurrentCharaData() { }
        public KksAdvMachine.ICommand Create(KksAdvMachine.Context context) => new Command { Host = this, Context = context };
    }
    internal static void Run(string directory, int zero, int one)
    {
        Console.WriteLine("Start parent/run ADV ownership and child lifetime checks with production machine");
        var order = new List<string>(); var scope = new KksAdvSessionScope();
        var first = scope.Own(new Resource { Close = () => order.Add("first") });
        scope.Own(new Resource { Close = () => { order.Add("middle"); throw new IOException("middle"); } });
        scope.Own(new Resource { Close = () => order.Add("last") });
        try { scope.Own(first); throw new Exception("Duplicate ownership accepted"); } catch (InvalidOperationException) { }
        try { scope.Dispose(); throw new Exception("Cleanup failure hidden"); } catch (AggregateException) { }
        scope.Dispose(); Require(order.SequenceEqual(new[] { "last", "middle", "first" }), "Scope did not release every resource in reverse order");
        try { scope.Own(first); throw new Exception("Closed scope accepted old ownership"); } catch (ObjectDisposedException) { }
        int lateReleased = zero;
        try { scope.Own(new Resource { Close = () => lateReleased++ }); throw new Exception("Closed scope adopted new resource"); } catch (ObjectDisposedException) { }
        Require(lateReleased == one && order.Count == one + one + one, "Late adoption leaked or disposed an old resource twice");

        KksAdvScript Script(string bundle, string asset) => new KksAdvScript(bundle, asset, asset == "sync"
            ? new[] { new KksAdvScript.Row(zero, zero, false, "Close", Array.Empty<string>()) }
            : new[] { new KksAdvScript.Row(zero, zero, false, "WaitAbs", Array.Empty<string>()), new KksAdvScript.Row(zero, zero, false, "Close", Array.Empty<string>()) });
        var lifetime = new KksAdvSessionScope(); int parentReleased = zero, runReleased = zero, completed = zero;
        lifetime.Own(new Resource { Close = () => parentReleased++ });
        var host = new Host(); var machines = new List<KksAdvMachine>();
        var owner = new KksAdvOwnedSession(Script, run =>
        {
            run.Own(new Resource { Close = () => { host.Trace.Add("dependency"); runReleased++; } });
            var machine = new KksAdvMachine(host, zero, one); machines.Add(machine); return machine;
        }, lifetime);
        var old = owner.Open("bundle", "old", () => completed++, exception => throw exception); var stale = host.Waits.Last();
        var current = owner.Open("bundle", "new", () => completed++, exception => throw exception);
        old.Dispose(); stale();
        Require(owner.Active && completed == zero && runReleased == one && parentReleased == zero
            && host.Trace.Take(one + one).SequenceEqual(new[] { "command:False", "dependency" }), "Replacing a child run leaked/closed shared resources or advanced from a stale callback");
        host.Waits.Last()(); host.Waits.Last()();
        Require(!owner.Active && completed == one && runReleased == one + one && parentReleased == zero, "Child completion failed ownership cleanup or fired twice");
        current.Dispose();
        owner.Open("bundle", "sync", () => completed++, exception => throw exception).Dispose();
        Require(completed == one + one && !owner.Active, "Synchronous child completion failed lease adoption");
        int callbackFailures = zero;
        owner.Open("bundle", "sync", () => throw new IOException("parent continuation"), exception => callbackFailures++);
        Require(callbackFailures == one, "Parent continuation failure was swallowed");
        owner.Open("bundle", "cancel", () => completed++, exception => throw exception).Dispose();
        Require(!owner.Active, "Child lease did not cancel run");
        owner.Dispose(); owner.Dispose(); Require(parentReleased == one, "Parent scope leaked or released twice");

        var partialScope = new KksAdvSessionScope(); int partialReleased = zero; Exception failure = null;
        using (var partial = new KksAdvOwnedSession(Script, run =>
        { run.Own(new Resource { Close = () => partialReleased++ }); throw new IOException("factory"); }, partialScope))
        { partial.Open("bundle", "partial", () => { }, exception => failure = exception); Require(failure is IOException && partialReleased == one && !partial.Active, "Factory failure leaked partially constructed services"); }
        var failedScope = new KksAdvSessionScope(); int cleanupReleased = zero; failure = null;
        using (var failedOwner = new KksAdvOwnedSession(Script, run =>
        { run.Own(new Resource { Close = () => { cleanupReleased++; throw new IOException("dependency"); } }); return new KksAdvMachine(new Host(), zero, one); }, failedScope))
        { failedOwner.Open("bundle", "sync", () => throw new Exception("Completion ran after cleanup failed"), exception => failure = exception); Require(failure is AggregateException && cleanupReleased == one, "Cleanup failure was lost or success continued"); }

        KksAdvOwnedSession interrupted = null; KksAdvMachine returned = null; int interruptedReleased = zero;
        interrupted = new KksAdvOwnedSession(Script, run =>
        {
            run.Own(new Resource { Close = () => interruptedReleased++ }); interrupted.Dispose();
            returned = new KksAdvMachine(new Host(), zero, one); return returned;
        }, new KksAdvSessionScope());
        interrupted.Open("bundle", "interrupted", () => throw new Exception("Cancelled factory completed"), exception => throw new Exception("Cancellation was reported as run failure", exception));
        bool returnedDisposed = false;
        try { returned.Start(Script("bundle", "sync"), () => { }, exception => { }); } catch (ObjectDisposedException) { returnedDisposed = true; }
        Require(interruptedReleased == one && returnedDisposed, "Late returned machine was not disposed after parent cancellation");

        KksAdvOwnedSession reentrant = null; int oldCompleted = zero, newCompleted = zero; bool replace = true;
        reentrant = new KksAdvOwnedSession(Script, run =>
        {
            run.Own(new Resource { Close = () => { if (replace) { replace = false; reentrant.Open("bundle", "sync", () => newCompleted++, exception => throw exception); } } });
            return new KksAdvMachine(new Host(), zero, one);
        }, new KksAdvSessionScope());
        using (reentrant)
        { reentrant.Open("bundle", "sync", () => oldCompleted++, exception => throw exception); Require(oldCompleted == zero && newCompleted == one, "Older continuation overwrote cleanup's newer Open"); }
        KksAdvScript factoryScript = null; int externalFailures = zero;
        var forwardedHost = new Host();
        using (var forwarded = new KksAdvOwnedSession(Script, (run, script) =>
        { factoryScript = script; return new KksAdvMachine(forwardedHost, zero, one); }, new KksAdvSessionScope()))
        {
            forwarded.Open("bundle", "external-failure", () => throw new Exception("Failed presentation completed"), exception => externalFailures++);
            Require(factoryScript.Bundle == "bundle" && factoryScript.Asset == "external-failure", "Runtime factory did not receive the exact loaded script");
            IKksAdvSession manualInput = forwarded; manualInput.Advance();
            Require(forwarded.Active, "Input bypassed the ADV hard wait");
            var machine = forwarded.CurrentMachine; machine.PresentationFailed(new IOException("render failure"));
            forwardedHost.Waits.Last()(); machine.PresentationFailed(new IOException("late render failure"));
            Require(!forwarded.Active && externalFailures == one, "Asynchronous presentation error did not stop exact run once");
        }
        File.WriteAllText(Path.Combine(directory, "adv-owned-session-checks.json"), JsonSerializer.Serialize(new
        {
            scope = "Production parent/run scopes, owned session and ADV machine with managed resources; no Unity service construction or UI bridge execution",
            reverseCleanupContinuesAfterFailure = true, duplicateAndLateOwnership = true,
            machineStopsBeforeDependencies = true, parentResourcesSurviveChildCompletion = true,
            staleCompletionRejected = true, synchronousOpen = true, continuationFailureReported = true,
            partialFactoryFailureCleaned = true, cleanupFailureSuppressesSuccess = true,
            cancelledFactoryReturnedMachineDisposed = true, reentrantOpenPreserved = true,
            factoryReceivesLoadedScript = true, manualInputHonorsHardWait = true, externalPresentationFailureStopsExactRun = true
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS ADV owned parent/run scopes partial failures stale synchronous reentrant late adoption");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
