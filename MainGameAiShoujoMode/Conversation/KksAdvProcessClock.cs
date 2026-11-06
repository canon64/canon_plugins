using System;
using System.Collections.Generic;
using System.Linq;

namespace MainGameAiShoujoMode.Conversation
{
    // Time advances only while the scenario can process commands. Each wait owns one deadline.
    internal sealed class KksAdvProcessClock : IDisposable
    {
        private sealed class Ticket : IDisposable
        {
            internal KksAdvProcessClock Owner;
            internal double Remaining, StartedAt;
            internal object Generation;
            internal IDisposable Timer;
            internal Action Completed;
            internal Action<Exception> Failed;
            internal bool Ended;
            public void Dispose() => Owner.Cancel(this);
        }
        private readonly Func<double> _now;
        private readonly Func<double, Action, IDisposable> _schedule;
        private readonly double _zero;
        private readonly HashSet<Ticket> _tickets = new HashSet<Ticket>();
        private bool _suspended, _disposed;
        internal bool Suspended => _suspended;
        internal int Pending => _tickets.Count;
        internal KksAdvProcessClock(Func<double> now, Func<double, Action, IDisposable> schedule, double zero)
        { _now = now ?? throw new ArgumentNullException(nameof(now)); _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule)); _zero = zero; }
        internal IDisposable Wait(double seconds, Action completed, Action<Exception> failed)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KksAdvProcessClock));
            var ticket = new Ticket { Owner = this, Remaining = seconds, Completed = completed, Failed = failed };
            _tickets.Add(ticket);
            if (!_suspended) Begin(ticket);
            return ticket;
        }
        internal void SetSuspended(bool suspended)
        {
            if (_disposed || _suspended == suspended) return;
            _suspended = suspended;
            var failures = new List<Exception>();
            foreach (var ticket in _tickets.ToArray())
            {
                if (ticket.Ended) continue;
                try
                {
                    if (_suspended)
                    {
                        if (ticket.Generation != null)
                        {
                            ticket.Remaining -= Math.Max(_zero, _now() - ticket.StartedAt);
                            CancelTimer(ticket);
                        }
                    }
                    else if (ticket.Generation == null) Begin(ticket);
                }
                catch (Exception exception) { failures.Add(exception); }
            }
            if (failures.Any()) throw new AggregateException("KKS ADV processing clock state change failed.", failures);
        }
        private void Begin(Ticket ticket)
        {
            if (ticket.Ended || _suspended || _disposed) return;
            // Native timer>=NaN/+Infinity never succeeds. Cancellation still releases these waits.
            if (double.IsNaN(ticket.Remaining) || double.IsPositiveInfinity(ticket.Remaining)) return;
            var generation = new object(); ticket.Generation = generation;
            try
            {
                ticket.StartedAt = _now();
                var timer = _schedule(Math.Max(_zero, ticket.Remaining), () => Due(ticket, generation));
                if (!ticket.Ended && ReferenceEquals(ticket.Generation, generation)) ticket.Timer = timer;
                else timer?.Dispose();
            }
            catch (Exception exception) { Fail(ticket, exception); }
        }
        private void Due(Ticket ticket, object generation)
        {
            if (_disposed || _suspended || ticket.Ended || !ReferenceEquals(ticket.Generation, generation)) return;
            var completed = ticket.Completed; var failed = ticket.Failed;
            try { Cancel(ticket); completed?.Invoke(); } catch (Exception exception) { failed?.Invoke(exception); }
        }
        private void Fail(Ticket ticket, Exception exception)
        {
            if (ticket.Ended) return;
            var failed = ticket.Failed;
            try { Cancel(ticket); } catch (Exception cleanup) { exception = new AggregateException(exception, cleanup); }
            failed?.Invoke(exception);
        }
        private static void CancelTimer(Ticket ticket)
        {
            ticket.Generation = null; var timer = ticket.Timer; ticket.Timer = null; timer?.Dispose();
        }
        private void Cancel(Ticket ticket)
        {
            if (ticket.Ended) return;
            ticket.Ended = true; _tickets.Remove(ticket); ticket.Completed = null; ticket.Failed = null; CancelTimer(ticket);
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            var failures = new List<Exception>();
            foreach (var ticket in _tickets.ToArray()) try { Cancel(ticket); } catch (Exception exception) { failures.Add(exception); }
            if (failures.Any()) throw new AggregateException("KKS ADV processing clock cleanup failed.", failures);
        }
    }
}
