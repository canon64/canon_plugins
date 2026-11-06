using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

internal sealed class FakeDeadlineTimeline
{
    internal sealed class Alarm : IDisposable
    {
        internal double Due, Delay;
        internal Action Callback;
        internal bool Disposed, Fired;
        internal bool ThrowOnDispose { get; set; }
        public void Dispose()
        {
            if (Disposed) throw new Exception("Deadline lease disposed twice");
            Disposed = true;
            if (ThrowOnDispose) throw new IOException("Synthetic deadline cancellation failed");
        }
    }
    internal readonly List<Alarm> Alarms = new List<Alarm>();
    internal double Now;
    internal bool Synchronous { get; set; }
    internal bool ThrowOnSchedule { get; set; }
    private readonly double _zero;
    internal FakeDeadlineTimeline(double zero) { _zero = zero; Now = zero; }
    internal IDisposable Schedule(double seconds, Action completed)
    {
        if (ThrowOnSchedule) throw new IOException("Synthetic deadline creation failed");
        var alarm = new Alarm { Delay = seconds, Due = Now + seconds, Callback = completed }; Alarms.Add(alarm);
        if (Synchronous) { alarm.Fired = true; completed(); }
        return alarm;
    }
    internal void Advance(double seconds)
    {
        var target = Now + seconds;
        while (true)
        {
            var next = Alarms.Where(alarm => !alarm.Disposed && !alarm.Fired && alarm.Due <= target).OrderBy(alarm => alarm.Due).FirstOrDefault();
            if (next == null) break;
            Now = Math.Max(Now, next.Due); next.Fired = true; next.Callback();
        }
        Now = target;
    }
    internal bool AdvanceNext()
    {
        var next = Alarms.Where(alarm => !alarm.Disposed && !alarm.Fired).OrderBy(alarm => alarm.Due).FirstOrDefault();
        if (next == null) return false;
        Advance(Math.Max(_zero, next.Due - Now)); return true;
    }
}
