using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoMode.Conversation;

internal static class AdvClockChecks
{
    private sealed class Empty : KksAdvMachine.ICommand
    {
        internal Action Began;
        public void Start(Action completed, Action<Exception> failed) { Began?.Invoke(); completed(); }
        public void End(bool processEnd) { }
    }
    private sealed class Host : KksAdvMachine.IHost
    {
        internal bool Regulated;
        internal int Captions;
        public bool IsRegulated => Regulated;
        public bool TextComplete => true;
        public void CompleteText() { }
        public void ClearText() { }
        public void ClearCurrentCharaData() { }
        public KksAdvMachine.ICommand Create(KksAdvMachine.Context context)
            => new Empty { Began = () => { if (context.Entry.Row.Command == "Text") Captions++; } };
    }
    internal static void Run(string directory, KksNormalConversationRules.Settings n)
    {
        Console.WriteLine("Start event deadline and native Wait/WaitAbs processing-clock checks");
        var time = new FakeDeadlineTimeline(n.zero);
        using (var clock = new KksAdvProcessClock(() => time.Now, time.Schedule, n.zero))
        {
            var ended = new List<string>();
            clock.Wait(n.one + n.one, () => ended.Add("first"), exception => throw exception);
            clock.Wait(n.one + n.one + n.one, () => ended.Add("second"), exception => throw exception);
            time.Advance(n.one); clock.SetSuspended(true); var old = time.Alarms.ToArray();
            time.Advance(n.requiredNicknameEvent); foreach (var alarm in old) alarm.Callback();
            Require(!ended.Any() && clock.Pending == n.one + n.one, "Suspension or a canceled deadline consumed remaining wait time");
            clock.SetSuspended(false);
            Require(time.Alarms.Skip(old.Length).First().Delay == n.one, "Resume restarted the full duration or lost elapsed time");
            time.Advance(n.one); Require(ended.SequenceEqual(new[] { "first" }), "First resumed deadline fired out of order");
            time.Advance(n.one); Require(ended.SequenceEqual(new[] { "first", "second" }) && clock.Pending == n.zero, "Second resumed deadline failed");
            clock.SetSuspended(true); var count = time.Alarms.Count;
            var canceled = clock.Wait(n.one, () => ended.Add("canceled"), exception => throw exception);
            Require(time.Alarms.Count == count, "Wait created while suspended started its timer");
            time.Advance(n.requiredNicknameEvent); clock.SetSuspended(false); canceled.Dispose(); canceled.Dispose();
            time.Alarms.Last().Callback(); Require(ended.Count == n.one + n.one, "Canceled wait delivered completion");
            clock.Wait(double.NaN, () => throw new Exception("NaN wait completed"), exception => throw exception);
            clock.Wait(double.PositiveInfinity, () => throw new Exception("Infinite wait completed"), exception => throw exception);
            count = time.Alarms.Count; clock.SetSuspended(true); clock.SetSuspended(false);
            Require(time.Alarms.Count == count && clock.Pending == n.one + n.one, "Native non-completing numeric waits scheduled polling deadlines");
        }
        time = new FakeDeadlineTimeline(n.zero) { Synchronous = true };
        using (var clock = new KksAdvProcessClock(() => time.Now, time.Schedule, n.zero))
        {
            int done = n.zero, failures = n.zero;
            clock.Wait(n.zero, () => done++, _ => failures++);
            Require(done == n.one && clock.Pending == n.zero && time.Alarms.Single().Disposed, "Synchronous deadline leaked the returned lease");
            clock.Wait(n.zero, () => throw new IOException("completion failed"), _ => failures++);
            Require(failures == n.one, "Deadline completion exception was swallowed");
            time.ThrowOnSchedule = true; clock.Wait(n.one, () => done++, _ => failures++);
            Require(failures == n.one + n.one && clock.Pending == n.zero, "Deadline creation error leaked a pending wait");
        }
        time = new FakeDeadlineTimeline(n.zero);
        using (var clock = new KksAdvProcessClock(() => throw new IOException("Time source failed"), time.Schedule, n.zero))
        {
            int failed = n.zero; clock.Wait(n.one, () => throw new Exception("Unexpected deadline completion"), _ => failed++);
            Require(failed == n.one && clock.Pending == n.zero && !time.Alarms.Any(), "Clock read failure leaked a pending wait");
        }
        time = new FakeDeadlineTimeline(n.zero);
        using (var clock = new KksAdvProcessClock(() => time.Now, time.Schedule, n.zero))
        {
            clock.Wait(n.one, () => throw new Exception("Disposed wait completed"), exception => throw exception);
            clock.Wait(n.one, () => throw new Exception("Disposed wait completed"), exception => throw exception);
            time.Alarms.First().ThrowOnDispose = true;
            try { clock.Dispose(); throw new Exception("Disposal error was swallowed"); } catch (AggregateException) { }
            Require(time.Alarms.All(alarm => alarm.Disposed) && clock.Pending == n.zero, "Disposal failure skipped later waits");
            foreach (var alarm in time.Alarms) alarm.Callback();
        }
        CheckMachine(directory, n);
        File.WriteAllText(Path.Combine(directory, "adv-clock-checks.json"), JsonSerializer.Serialize(new
        {
            scope = "Production processing clock/timed host/machine with controlled time; Unity timer adapter not executed",
            suspensionRemainingTime = true, waitsCreatedWhileSuspended = true, staleDeadlines = true,
            synchronousLeaseAdoption = true, numericNonCompletion = true, scheduleAndCompletionFailures = true,
            bestEffortCleanup = true, hardAndSoftWaitIntegration = true, liveRegulationEvents = true
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS ADV processing clock suspend resume deadlines cancellation Wait/WaitAbs integration");
    }
    private static void CheckMachine(string directory, KksNormalConversationRules.Settings n)
    {
        KksAdvScript.Row R(string name, params string[] args) => new KksAdvScript.Row(n.zero, n.zero, false, name, args);
        foreach (var kind in new[] { "WaitAbs", "Wait" })
        {
            var time = new FakeDeadlineTimeline(n.zero); var host = new Host();
            using var clock = new KksAdvProcessClock(() => time.Now, time.Schedule, n.zero);
            var timed = new KksAdvTimedHost(host, clock, value => value == "duration" ? (n.one + n.one).ToString() : value, n.zero);
            var core = new KksAdvCoreHost(timed, new NoAssets(), value => value, (bundle, asset) => { }, null, n.zero);
            using var machine = new KksAdvMachine(core, n.zero, n.one, clock.SetSuspended);
            int done = n.zero;
            machine.Start(new KksAdvScript("test", kind, new[] { R(kind, "duration"), R("Text"), R("Close") }), () => done++, exception => throw exception);
            if (kind == "Wait")
            {
                machine.Advance(); Require(host.Captions == n.one && clock.Pending == n.zero, "Manual input did not cancel ordinary Wait");
            }
            else
            {
                machine.Advance(); Require(host.Captions == n.zero, "Manual input bypassed WaitAbs");
                time.Advance(n.one); host.Regulated = true; machine.NotifyPresentationChanged();
                var stale = time.Alarms.Last(); time.Advance(n.requiredNicknameEvent); stale.Callback();
                Require(host.Captions == n.zero && clock.Suspended, "Regulation failed to freeze WaitAbs");
                host.Regulated = false; machine.NotifyPresentationChanged(); time.Advance(n.one);
                Require(host.Captions == n.one && machine.Active, "WaitAbs did not resume into a single caption");
            }
            machine.Advance(); Require(done == n.one && clock.Pending == n.zero && clock.Suspended, "Wait route did not close and pause its clock");
        }
        // A resumed zero deadline must not let the unblocking click skip the newly shown caption.
        {
            var time = new FakeDeadlineTimeline(n.zero) { Synchronous = true }; var host = new Host { Regulated = true };
            using var clock = new KksAdvProcessClock(() => time.Now, time.Schedule, n.zero);
            var timed = new KksAdvTimedHost(host, clock, value => value, n.zero);
            var core = new KksAdvCoreHost(timed, new NoAssets(), value => value, (bundle, asset) => { }, null, n.zero);
            using var machine = new KksAdvMachine(core, n.zero, n.one, clock.SetSuspended);
            int done = n.zero;
            machine.Start(new KksAdvScript("test", "zero", new[] { R("WaitAbs", "not-a-number"), R("Text"), R("Close") }), () => done++, exception => throw exception);
            Require(!time.Alarms.Any(), "Initially regulated zero wait started before processing was allowed");
            host.Regulated = false; machine.Advance();
            Require(host.Captions == n.one && machine.Active && done == n.zero, "Clock resume consumed an unblocking click in a new batch");
            machine.Advance(); Require(done == n.one, "Zero wait route failed to close");
        }
    }
    private sealed class NoAssets : KksAdvCoreHost.IAssets
    {
        public string ResolveBundle(string bundle, string loadBundle) => throw new NotSupportedException();
        public KksAdvScript Load(string bundle, string asset, string loadBundle) => throw new NotSupportedException();
        public bool TryParameter(string name, out KksAdvScript script) { script = null; return false; }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
