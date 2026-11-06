using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MainGameAiShoujoMode.Conversation;

internal static class BgmPlaybackChecks
{
    private sealed class Lease : IDisposable
    {
        internal Action Release;
        public void Dispose() { var action = Release; Release = null; action?.Invoke(); }
    }
    private sealed class Track : KksAdvBgmPlayback.ITrack
    {
        internal int Starts, Disposals;
        internal float Fade;
        internal Action End;
        internal bool FailStart, FailDispose, EndImmediately;
        public float Pitch { set { } }
        public float PanStereo { set { } }
        public float Time { set { } }
        public float Volume { set { } }
        public void Start(float fadeSeconds) { if (FailStart) throw new IOException("start"); Starts++; Fade = fadeSeconds; }
        public void FadeOut(float fadeSeconds, Action completed) { Fade = fadeSeconds; End = completed; if (EndImmediately) completed(); }
        public void Dispose() { Disposals++; if (FailDispose) throw new IOException("dispose"); }
    }
    private sealed class Device : KksAdvBgmPlayback.IDevice
    {
        internal readonly Queue<Track> Tracks = new Queue<Track>();
        internal int Creates, Disposals;
        public KksAdvBgmPlayback.ITrack Create(Illusion.Game.Utils.Sound.SettingBGM setting) { Creates++; return Tracks.Dequeue(); }
        public void Dispose() { Disposals++; }
    }
    internal static void Run(string directory, int zero, int one)
    {
        Console.WriteLine("Start owned BGM lifetime checks with mock tracks; no Unity playback");
        var device = new Device(); int suspended = zero, restored = zero;
        IDisposable Suspend() { suspended++; return new Lease { Release = () => restored++ }; }
        var first = new Track(); var second = new Track(); var third = new Track();
        device.Tracks.Enqueue(null); device.Tracks.Enqueue(first); device.Tracks.Enqueue(second); device.Tracks.Enqueue(third);
        var a = new Illusion.Game.Utils.Sound.SettingBGM("a") { loader = { fadeTime = zero } };
        var b = new Illusion.Game.Utils.Sound.SettingBGM("b"); var c = new Illusion.Game.Utils.Sound.SettingBGM("c");
        var playback = new KksAdvBgmPlayback(device, Suspend, one);
        Require(playback.Play(a) == null && suspended == zero, "Missing BGM suspended island music");
        Require(ReferenceEquals(playback.Play(a), first) && first.Fade == one && suspended == one, "First BGM did not acquire suspension/minimum fade");
        var creates = device.Creates;
        Require(ReferenceEquals(playback.Play(a), first) && device.Creates == creates && first.Starts == one, "Identical BGM restarted");
        playback.Play(b); Require(first.End != null && first.Disposals == zero && suspended == one, "Transition failed to fade old BGM or nested suspension");
        playback.Play(c); Require(first.Disposals == one && second.End != null, "Second transition did not retire the previous fading source");
        first.End(); Require(first.Disposals == one && third.Disposals == zero, "Stale fade completion disposed a current source");
        second.End(); Require(second.Disposals == one, "Completed fade did not dispose old source");
        playback.Dispose(); playback.Dispose(); second.End();
        Require(third.Disposals == one && device.Disposals == one && restored == one, "Cleanup leaked/doubled tracks, device or suspension");

        var brokenDevice = new Device(); var broken = new Track { FailStart = true }; brokenDevice.Tracks.Enqueue(broken);
        using (var brokenPlayback = new KksAdvBgmPlayback(brokenDevice, Suspend, one))
        {
            bool failed = false; try { brokenPlayback.Play(a); } catch (IOException) { failed = true; }
            Require(failed && broken.Disposals == one && restored == suspended, "Failed start leaked source or island suspension");
        }
        var failedSuspend = new Device(); var unused = new Track(); failedSuspend.Tracks.Enqueue(unused);
        using (var owner = new KksAdvBgmPlayback(failedSuspend, () => throw new IOException("suspend"), one))
        {
            bool failed = false; try { owner.Play(a); } catch (IOException) { failed = true; }
            Require(failed && unused.Disposals == one && unused.Starts == zero, "Suspension failure leaked or started source");
        }
        var cleanup = new Device(); var unhealthy = new Track { FailDispose = true }; cleanup.Tracks.Enqueue(unhealthy);
        var cleanupOwner = new KksAdvBgmPlayback(cleanup, Suspend, one); cleanupOwner.Play(a);
        bool cleanupFailed = false; try { cleanupOwner.Dispose(); } catch (AggregateException) { cleanupFailed = true; }
        Require(cleanupFailed && cleanup.Disposals == one && restored == suspended, "Track disposal failure skipped remaining cleanup");
        var immediate = new Device(); var old = new Track { EndImmediately = true }; var current = new Track(); immediate.Tracks.Enqueue(old); immediate.Tracks.Enqueue(current);
        using (var owner = new KksAdvBgmPlayback(immediate, Suspend, one))
        { owner.Play(a); owner.Play(b); Require(old.Disposals == one && current.Disposals == zero, "Synchronous fade completion corrupted current track"); }
        Require(restored == suspended, "Island suspension remained held");
        File.WriteAllText(Path.Combine(directory, "adv-bgm-playback-checks.json"), JsonSerializer.Serialize(new
        {
            scope = "Production BGM ownership with mock device/tracks; Unity source, mixer, fades and island audio not executed",
            missingAssetPreservesIslandMusic = true, identicalAssetReused = true, minimumFade = true,
            transitionsRetireOlderFade = true, staleCompletionRejected = true, synchronousCompletion = true,
            startAndSuspendFailureCleanup = true, disposalFailureStillRestoresMusic = true, idempotentDispose = true,
            suspended, restored
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS owned BGM lifetime and failure cleanup; mock tracks=true");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
