using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MainGameAiShoujoMode.Conversation;

internal static class CrossFadeChecks
{
    private sealed class Lease : IDisposable
    {
        internal Action Cancel;
        public void Dispose() { var cancel = Cancel; Cancel = null; cancel?.Invoke(); }
    }
    private sealed class Device : KksAdvCrossFadePlayback.IDevice
    {
        internal readonly List<Action> Completed = new List<Action>();
        internal readonly List<Action<Exception>> Failed = new List<Action<Exception>>();
        internal readonly List<float> Durations = new List<float>();
        internal int Cancelled, Forced, Disposals;
        internal bool Synchronous, ThrowStart;
        internal float Zero;
        public bool HasFrame { get; set; } = true;
        public float DefaultDuration { get; set; }
        public bool IsZero(float duration) => duration == Zero;
        public IDisposable Animate(float duration, Action completed, Action<Exception> failed)
        {
            if (ThrowStart) throw new IOException("start");
            Durations.Add(duration); Completed.Add(completed); Failed.Add(failed);
            if (Synchronous) completed(); return new Lease { Cancel = () => Cancelled++ };
        }
        public void ForceEnd() { Forced++; }
        public void Dispose() { Disposals++; }
    }
    private sealed class Host : KksAdvMachine.IHost, KksAdvCoreHost.IAssets
    {
        public bool IsRegulated => false;
        public bool TextComplete => true;
        public void CompleteText() { }
        public void ClearText() { }
        public void ClearCurrentCharaData() { }
        public KksAdvMachine.ICommand Create(KksAdvMachine.Context context) => throw new NotSupportedException(context.Entry.Row.Command);
        public string ResolveBundle(string bundle, string loadBundle) => bundle;
        public KksAdvScript Load(string bundle, string asset, string loadBundle) => throw new NotSupportedException();
        public bool TryParameter(string name, out KksAdvScript script) { script = null; return false; }
    }
    internal static void Run(string directory, int zero, int one)
    {
        Console.WriteLine("Start shared CrossFade lifecycle/default/force checks; frame renderer mocked");
        var device = new Device { Zero = zero, DefaultDuration = one + one }; int completions = zero, failures = zero;
        var playback = new KksAdvCrossFadePlayback(device, zero);
        var first = playback.Start(-one, () => completions++, exception => failures++);
        Require(device.Durations[zero] == device.DefaultDuration && completions == zero, "Negative duration did not use native default");
        var second = playback.Start(one, () => completions++, exception => failures++);
        var waiter = playback.Start(zero, () => completions++, exception => failures++);
        Require(device.Durations.Count == one + one && device.Cancelled == one, "Zero duration restarted an active fade");
        device.Completed[zero](); Require(completions == zero, "Superseded fade completion advanced waiters");
        device.Completed[one](); device.Completed[one]();
        Require(completions == one + one + one, "Current completion did not release all shared native waiters exactly once");
        first.End(true); first.End(false); first.Dispose(); second.End(true); second.Dispose(); waiter.End(true); waiter.Dispose();
        Require(device.Forced == zero && playback.PendingOperations == zero, "Natural completion forced output or leaked operations");
        var idle = playback.Start(zero, () => completions++, exception => failures++); idle.End(true); idle.Dispose();
        var active = playback.Start(one, () => completions++, exception => failures++);
        active.End(false); active.End(false); active.Dispose();
        Require(device.Forced == one, "Forced end did not end shared fade exactly once");
        playback.StartImplicit(one, exception => failures++); device.Completed[device.Completed.Count - one]();
        Require(playback.PendingOperations == zero, "Implicit EventCG fade leaked an operation");
        var failed = playback.Start(one, () => completions++, exception => failures++);
        device.Failed[device.Failed.Count - one](new IOException("renderer")); failed.End(true); failed.Dispose();
        Require(failures == one, "Renderer failure was swallowed");
        device.Synchronous = true;
        var synchronous = playback.Start(one, () => completions++, exception => failures++); synchronous.End(true); synchronous.Dispose();
        Require(playback.PendingOperations == zero, "Synchronous completion failed lease adoption");
        device.Synchronous = false; device.ThrowStart = true;
        try { playback.Start(one, () => completions++, exception => failures++); throw new Exception("Start failure was hidden"); } catch (IOException) { }
        device.ThrowStart = false;
        var recovered = playback.Start(zero, () => completions++, exception => failures++); recovered.End(true); recovered.Dispose();
        Require(playback.PendingOperations == zero, "Failed start left a phantom active fade");
        playback.Dispose(); playback.Dispose(); Require(device.Disposals == one, "Renderer disposed twice");
        device = new Device { Zero = zero, HasFrame = false };
        using (var unprepared = new KksAdvCrossFadePlayback(device, zero))
        { try { unprepared.Start(one, () => { }, exception => { }); throw new Exception("Missing pre-change frame was silently accepted"); } catch (InvalidOperationException) { } }

        device = new Device { Zero = zero, DefaultDuration = one };
        using var owner = new KksAdvCrossFadePlayback(device, zero);
        var host = new Host(); var fadeHost = new KksAdvCrossFadeHost(host, owner, value => value == "time" ? one.ToString() : value);
        var core = new KksAdvCoreHost(fadeHost, host, value => value, (bundle, asset) => { }, null, zero);
        using var machine = new KksAdvMachine(core, zero, one); int closed = zero;
        KksAdvScript Script(string duration) => new KksAdvScript("test", "crossfade", new[]
        { new KksAdvScript.Row(zero, zero, false, "CrossFade", new[] { duration }), new KksAdvScript.Row(zero, zero, false, "Close", Array.Empty<string>()) });
        machine.Start(Script("time"), () => closed++, exception => throw exception);
        Require(machine.Active && !machine.HardWaiting && closed == zero, "Native CrossFade incorrectly became an absolute wait");
        machine.Advance(); Require(!machine.Active && closed == one && device.Forced == one, "Manual advance did not force the fade endpoint");
        device.Completed[zero](); Require(closed == one, "Stale renderer callback restarted a closed script");
        machine.Start(Script("time"), () => closed++, exception => throw exception); device.Completed[one]();
        Require(closed == one + one && device.Forced == one, "Natural command completion forced or failed to close");
        Exception parseFailure = null;
        machine.Start(Script("invalid-duration"), () => { }, exception => parseFailure = exception);
        Require(parseFailure is FormatException && device.Durations.Count == one + one, "Invalid duration mutated renderer");
        File.WriteAllText(Path.Combine(directory, "adv-crossfade-checks.json"), JsonSerializer.Serialize(new
        {
            scope = "Production CrossFade host and shared playback using mock frame device; no capture/shader/Unity clock rendering",
            negativeUsesDefault = true, zeroKeepsActiveFade = true, sharedWaiters = true,
            staleCompletionRejected = true, naturalVsForcedEnd = true, implicitOperationReleased = true,
            synchronousLeaseAdoption = true, startFailureRecovery = true, preChangeCaptureRequired = true,
            rendererFailurePropagated = true, manualAdvanceIsNotHardWait = true
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS shared CrossFade lifecycle and command advance; frame device mocked=true");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
