using System;
using System.Collections.Generic;
using System.Linq;

namespace MainGameAiShoujoMode.Conversation
{
    // Parallel channels within a group, sequential groups, without polling sound sources.
    internal sealed class KksVoiceGroupSequence<T> : IDisposable
    {
        private sealed class Slot
        {
            internal T Cue;
            internal IDisposable Lease;
            internal bool Completed;
        }
        private sealed class Run : IDisposable
        {
            internal KksVoiceGroupSequence<T> Owner;
            internal T[][] Groups;
            internal int Index;
            internal Slot[] Slots;
            internal bool Ready;
            internal Exception Error;
            internal Action Changed, Completed, Cleanup;
            internal Action<Exception> Failed;
            public void Dispose() { if (ReferenceEquals(Owner._run, this)) Owner.Stop(); }
        }
        private readonly Func<T, object> _channel;
        private readonly Func<T, Action, Action<Exception>, IDisposable> _play;
        private readonly int _zero;
        private Run _run;
        private object _transition;
        private bool _pumping, _disposed;
        internal KksVoiceGroupSequence(Func<T, object> channel, Func<T, Action, Action<Exception>, IDisposable> play, int zero)
        { _channel = channel ?? throw new ArgumentNullException(nameof(channel)); _play = play ?? throw new ArgumentNullException(nameof(play)); _zero = zero; }
        internal IDisposable Play(IEnumerable<IEnumerable<T>> groups, Action changed, Action completed, Action<Exception> failed, Action cleanup)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KksVoiceGroupSequence<T>));
            var copy = (groups ?? throw new ArgumentNullException(nameof(groups))).Select(group => group.ToArray()).ToArray();
            var transition = new object(); _transition = transition;
            var previous = _run; _run = null; Cleanup(previous);
            if (!ReferenceEquals(_transition, transition) || _disposed) { cleanup?.Invoke(); return new Empty(); }
            var run = new Run { Owner = this, Groups = copy, Index = _zero, Changed = changed, Completed = completed, Failed = failed, Cleanup = cleanup };
            _run = run; Pump(); return run;
        }
        private sealed class Empty : IDisposable { public void Dispose() { } }
        private void Pump()
        {
            if (_pumping) return; _pumping = true;
            try
            {
                while (_run != null)
                {
                    var run = _run;
                    try
                    {
                        if (run.Error != null) throw run.Error;
                        if (run.Slots == null)
                        {
                            if (run.Index >= run.Groups.Length) { Finish(run); continue; }
                            // Native Voice.Play replaces an earlier voice on the same actor/channel.
                            var latest = run.Groups[run.Index].Select((cue, index) => new { cue, index })
                                .GroupBy(item => _channel(item.cue)).Select(group => group.Last()).OrderBy(item => item.index);
                            run.Slots = latest.Select(item => new Slot { Cue = item.cue }).ToArray(); run.Ready = false;
                            foreach (var slot in run.Slots)
                            {
                                var lease = _play(slot.Cue, () => Complete(run, slot), exception => FailCommand(run, slot, exception));
                                if (ReferenceEquals(_run, run) && run.Slots != null && run.Slots.Contains(slot)) slot.Lease = lease;
                                else lease?.Dispose();
                                if (!ReferenceEquals(_run, run) || run.Error != null) break;
                            }
                            if (!ReferenceEquals(_run, run)) continue;
                            if (run.Error != null) throw run.Error;
                            run.Ready = true; run.Changed?.Invoke();
                            if (!ReferenceEquals(_run, run)) continue;
                        }
                        if (!run.Ready || run.Slots.Any(slot => !slot.Completed)) break;
                        EndSlots(run); run.Index++;
                    }
                    catch (Exception exception) { Fail(run, exception); }
                }
            }
            finally { _pumping = false; }
        }
        private void Complete(Run run, Slot slot)
        {
            if (!ReferenceEquals(_run, run) || run.Slots == null || !run.Slots.Contains(slot) || slot.Completed) return;
            slot.Completed = true; Pump();
        }
        private void FailCommand(Run run, Slot slot, Exception exception)
        {
            if (!ReferenceEquals(_run, run) || run.Slots == null || !run.Slots.Contains(slot) || slot.Completed) return;
            if (run.Error == null) run.Error = exception ?? new InvalidOperationException("KKS voice channel failed without diagnostics.");
            Pump();
        }
        private static void EndSlots(Run run)
        {
            var slots = run.Slots; run.Slots = null;
            if (slots == null) return;
            var failures = new List<Exception>();
            foreach (var slot in slots) try { var lease = slot.Lease; slot.Lease = null; lease?.Dispose(); } catch (Exception exception) { failures.Add(exception); }
            if (failures.Any()) throw new AggregateException("KKS voice channel cleanup failed.", failures);
        }
        private static void Cleanup(Run run)
        {
            if (run == null) return;
            var cleanup = run.Cleanup; run.Cleanup = null;
            var failures = new List<Exception>();
            try { EndSlots(run); } catch (Exception exception) { failures.Add(exception); }
            try { cleanup?.Invoke(); } catch (Exception exception) { failures.Add(exception); }
            if (failures.Any()) throw new AggregateException("KKS voice group cleanup failed.", failures);
        }
        private void Finish(Run run)
        {
            var completed = run.Completed; var failed = run.Failed;
            try { Stop(); completed?.Invoke(); } catch (Exception exception) { failed?.Invoke(exception); }
        }
        private void Fail(Run run, Exception exception)
        {
            if (!ReferenceEquals(_run, run)) return;
            var failed = run.Failed;
            try { Stop(); } catch (Exception cleanup) { exception = new AggregateException(exception, cleanup); }
            failed?.Invoke(exception);
        }
        internal void Stop() { _transition = new object(); var run = _run; _run = null; Cleanup(run); }
        public void Dispose() { if (_disposed) return; _disposed = true; Stop(); }
    }
}
