using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoMode.Conversation;

internal static class PointDisplayChecks
{
    private sealed class Animation : IDisposable
    {
        internal Action Begin, Complete;
        internal Action<Exception> Fail;
        internal bool Ended;
        internal bool ThrowOnDispose;
        public void Dispose() { Ended = true; if (ThrowOnDispose) throw new IOException("animation release"); }
    }
    private sealed class Device : KksPointDisplayPlayback.IDevice
    {
        internal readonly List<Animation> Animations = new List<Animation>();
        internal readonly List<int> Initial = new List<int>(), Targets = new List<int>(), Resets = new List<int>();
        internal Action<Animation> Started;
        public IDisposable Play(int now, int target, Action beganAdding, Action completed, Action<Exception> failed)
        {
            Initial.Add(now); Targets.Add(target);
            var animation = new Animation { Begin = beganAdding, Complete = completed, Fail = failed };
            Animations.Add(animation); Started?.Invoke(animation); return animation;
        }
        public void Reset(int score) { Resets.Add(score); }
    }
    internal static void Run(string directory, int zero, int one)
    {
        Console.WriteLine("Start shared point-display score, award-stage and animation lifetime checks");
        var device = new Device(); var display = new KksPointDisplayPlayback(device, zero);
        int first = zero, second = zero, total = zero;
        var old = display.Present(one, amount => total += amount, () => first++, exception => throw exception);
        var stale = device.Animations.Last();
        var current = display.Present(one + one, amount => total += amount, () => second++, exception => throw exception);
        Require(stale.Ended && device.Initial.Last() == zero && display.DisplayedScore == zero, "Replacement prematurely advanced displayed score");
        stale.Begin(); stale.Complete();
        Require(total == zero && first == zero && second == zero && display.Active, "Stale animation affected latest display");
        device.Animations.Last().Begin(); device.Animations.Last().Begin();
        Require(total == one + one && display.DisplayedScore == one + one, "Catch-up award did not include undrawn previous command or fired twice");
        device.Animations.Last().Complete();
        Require(first == one && second == one && !display.Active, "Shared display waiters did not complete together");
        old.Dispose(); current.Dispose();
        var cancelled = display.Present(one + one + one, amount => total += amount, () => first++, exception => throw exception);
        var cancelledAnimation = device.Animations.Last(); cancelled.Dispose(); cancelledAnimation.Begin(); cancelledAnimation.Complete();
        Require(!display.Active && total == one + one && cancelledAnimation.Ended, "Cancel before add stage awarded or leaked animation");
        display.Present(one + one + one, amount => total += amount, () => first++, exception => throw exception);
        var begun = device.Animations.Last(); begun.Begin();
        display.Present(one + one + one + one, amount => total += amount, () => second++, exception => throw exception);
        Require(device.Initial.Last() == one + one + one, "Replacement lost already begun displayed score");
        device.Animations.Last().Begin(); device.Animations.Last().Complete();
        Require(total == one + one + one + one, "Replacement credited an already begun amount twice");
        var animationCount = device.Animations.Count;
        display.Present(one, amount => throw new Exception("Decrease awarded points"), () => first++, exception => throw exception);
        Require(device.Animations.Count == animationCount && display.DisplayedScore == one && !display.Active, "Decrease did not match native immediate clear path");
        Exception failure = null;
        display.Present(one + one, amount => { }, () => throw new Exception("Failed display succeeded"), exception => failure = exception);
        device.Animations.Last().Fail(new IOException("native view"));
        Require(failure is IOException && !display.Active && device.Animations.Last().Ended, "Animation failure did not release shared wait");
        display.Present(one + one, amount => { }, () => throw new Exception("Bad cleanup succeeded"), exception => failure = exception);
        var bad = device.Animations.Last(); bad.ThrowOnDispose = true; bad.Complete();
        Require(failure is IOException && !display.Active, "Animation cleanup failure was not reported");
        display.Dispose();
        var synchronousDevice = new Device { Started = animation => { animation.Begin(); animation.Complete(); } };
        using (var synchronous = new KksPointDisplayPlayback(synchronousDevice, zero))
        {
            synchronous.Present(one, amount => { }, () => { }, exception => throw exception).Dispose();
            Require(!synchronous.Active && synchronousDevice.Animations.Single().Ended, "Synchronous display leaked returned animation");
        }
        var reentrantDevice = new Device();
        using (var reentrant = new KksPointDisplayPlayback(reentrantDevice, zero))
        {
            reentrant.Present(one, amount => { }, () => reentrant.Present(one + one, amount => { }, () => { }, exception => throw exception), exception => throw exception);
            reentrantDevice.Animations.Last().Begin(); reentrantDevice.Animations.Last().Complete();
            Require(reentrant.Active && !reentrantDevice.Animations.Last().Ended, "Old completion reset a reentrant new display");
        }
        File.WriteAllText(Path.Combine(directory, "point-display-playback-checks.json"), JsonSerializer.Serialize(new
        {
            scope = "Production shared score/wait state and mocked finite animation device; no Unity view, sounds or rendering",
            replacementBeforeAndAfterAward = true, sharedWaits = true, catchUpDelta = true, staleAndDuplicateBeginRejected = true,
            cancelBeforeAward = true, decreaseClearsImmediately = true, failuresReleaseWaits = true, cleanupFailureReported = true,
            synchronousDevice = true, reentrantCompletion = true
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS shared point display replacement catch-up cancel failure synchronous reentrant; view mocked=true");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
