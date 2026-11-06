using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoMode.Conversation;

internal static class PointHostChecks
{
    private sealed class Lease : IDisposable
    {
        internal Action Cancel;
        public void Dispose() { var cancel = Cancel; Cancel = null; cancel?.Invoke(); }
    }
    private sealed class Host : KksAdvMachine.IHost
    {
        public bool IsRegulated => false;
        public bool TextComplete => true;
        public void CompleteText() { }
        public void ClearText() { }
        public void ClearCurrentCharaData() { }
        public KksAdvMachine.ICommand Create(KksAdvMachine.Context context) => throw new NotSupportedException(context.Entry.Row.Command);
    }
    private sealed class Assets : KksAdvCoreHost.IAssets
    {
        public string ResolveBundle(string bundle, string loadBundle) => bundle;
        public KksAdvScript Load(string bundle, string asset, string loadBundle) => throw new NotSupportedException();
        public bool TryParameter(string name, out KksAdvScript script) { script = null; return false; }
    }
    private sealed class PointDevice : KksPointDisplayPlayback.IDevice
    {
        internal sealed class Frame { internal Action Begin, Complete; internal bool Ended; }
        internal readonly List<Frame> Frames = new List<Frame>();
        public IDisposable Play(int now, int target, Action beganAdding, Action completed, Action<Exception> failed)
        {
            var frame = new Frame { Begin = beganAdding, Complete = completed }; Frames.Add(frame);
            return new Lease { Cancel = () => frame.Ended = true };
        }
        public void Reset(int score) { }
    }
    internal static void Run(string directory, int schema, int zero, int one, int maximum)
    {
        Console.WriteLine("Start point command with production durable backend and mocked point UI");
        var savePath = Path.Combine(directory, "save-checks", Guid.NewGuid().ToString("N"), "command-points.json");
        var store = new KksConversationStateStore(savePath, schema);
        int shown = zero, cancelled = zero, playerPoints = zero; Action displayComplete = null; Action<int> displayAward = null;
        KksConversationStateStore.PointChange last = null;
        var backend = new KksAdvPointBackend(store, value => playerPoints = value, (change, award, completed, failed) =>
        { shown++; last = change; displayAward = award; displayComplete = completed; return new Lease { Cancel = () => cancelled++ }; }, zero, maximum);
        var point = new KksAdvPointHost(new Host(), backend, value => value == "amount" ? one.ToString() : value);
        var core = new KksAdvCoreHost(point, new Assets(), value => value, (bundle, asset) => { }, null, zero);
        using var machine = new KksAdvMachine(core, zero, one); int closed = zero;
        KksAdvScript Script(string amount) => new KksAdvScript("bundle", "point-scene", new[]
        { new KksAdvScript.Row(zero, zero, false, "KoikatsuPointAdd", new[] { amount }), new KksAdvScript.Row(zero, zero, false, "Close", Array.Empty<string>()) });
        machine.Start(Script("amount"), () => closed++, exception => throw exception);
        Require(machine.HardWaiting && playerPoints == one && store.ReadPlayer().points == one && shown == one && closed == zero,
            "Points were not committed before display wait");
        machine.Advance(); Require(closed == zero, "Point display hard wait was skipped by click");
        Require(store.ReadPlayer().totalPoints == zero, "Cumulative points awarded before AddPoint display stage");
        displayAward(one); displayAward(one);
        Require(store.ReadPlayer().totalPoints == one, "Display stage did not award once");
        var stale = displayComplete; var staleAward = displayAward; displayComplete(); displayComplete(); staleAward(one + one);
        Require(closed == one && cancelled == one && store.ReadPlayer().pointChanges.Count == one, "Display completion duplicated points or leaked wait");
        backend.Add(last, () => { }, exception => throw exception);
        Require(shown == one && store.ReadPlayer().points == one, "Duplicate backend retry replayed credit display or points");
        machine.Start(Script(one.ToString()), () => closed++, exception => throw exception);
        stale(); Require(machine.Active && closed == one, "Stale display completion closed a newer command");
        machine.Stop(); displayComplete(); displayAward(one);
        Require(!machine.Active && cancelled == one + one && store.ReadPlayer().points == one + one, "Cancellation rolled back points or kept display wait");
        Require(store.ReadPlayer().totalPoints == one, "Cancelled/late display callback awarded cumulative points");
        machine.Start(Script(one.ToString()), () => closed++, exception => throw exception);
        displayAward(one + one); displayComplete();
        Require(store.ReadPlayer().totalPoints == one + one + one, "Display catch-up amount was incorrectly replaced with one command's delta");
        var failBackend = new KksAdvPointBackend(store, value => playerPoints = value, (change, award, completed, failed) => throw new IOException("display failed"), zero, maximum);
        try { failBackend.Add(new KksConversationStateStore.PointChange { id = "failed-display", bundle = "bundle", asset = "asset", delta = one }, () => { }, exception => throw exception); throw new Exception("Display failure was hidden"); } catch (IOException) { }
        Require(store.ReadPlayer().pointChanges.Any(change => change.id == "failed-display" && !change.displayed), "Display failure rolled back points or awarded before display");
        int synchronousReleased = zero;
        var synchronous = new KksAdvPointBackend(store, value => playerPoints = value, (change, award, completed, failed) =>
        { award(one); completed(); return new Lease { Cancel = () => synchronousReleased++ }; }, zero, maximum);
        synchronous.Add(new KksConversationStateStore.PointChange { id = "synchronous", bundle = "bundle", asset = "asset", delta = one }, () => { }, exception => throw exception).Dispose();
        Require(synchronousReleased == one, "Synchronous display resurrected its returned lease");
        Action<int> lockedAward = null; Exception displayFailure = null; int lockedReleased = zero;
        var lockedBackend = new KksAdvPointBackend(store, value => playerPoints = value, (change, award, completed, failed) =>
        { lockedAward = award; return new Lease { Cancel = () => lockedReleased++ }; }, zero, maximum);
        using (lockedBackend.Add(new KksConversationStateStore.PointChange { id = "locked-award", bundle = "bundle", asset = "asset", delta = one },
            () => throw new Exception("Failed award reported success"), exception => displayFailure = exception))
        {
            using (var locked = new FileStream(savePath + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None)) lockedAward(one);
            lockedAward(one);
        }
        Require(displayFailure is IOException && lockedReleased == one && !store.ReadPlayer().pointChanges.Single(change => change.id == "locked-award").displayed,
            "Display-stage save failure did not stop its UI and report failure once");
        Action cleanupComplete = null; Exception cleanupFailure = null;
        var badCleanup = new KksAdvPointBackend(store, value => playerPoints = value, (change, award, completed, failed) =>
        { cleanupComplete = completed; return new Lease { Cancel = () => throw new IOException("point UI release") }; }, zero, maximum);
        using (badCleanup.Add(new KksConversationStateStore.PointChange { id = "cleanup-failure", bundle = "bundle", asset = "asset", delta = one },
            () => throw new Exception("Cleanup failure reported success"), exception => cleanupFailure = exception)) cleanupComplete();
        Require(cleanupFailure is IOException, "Display cleanup failure was not delivered to the command");
        var multiStore = new KksConversationStateStore(Path.Combine(Path.GetDirectoryName(savePath), "multi-points.json"), schema);
        int multiPlayer = zero, multiCompleted = zero; var pointDevice = new PointDevice();
        using (var playback = new KksPointDisplayPlayback(pointDevice, zero))
        {
            var multiBackend = new KksAdvPointBackend(multiStore, value => multiPlayer = value,
                (change, award, completed, failed) => playback.Present(multiPlayer, award, completed, failed), zero, maximum);
            var multiPoint = new KksAdvPointHost(new Host(), multiBackend, value => value);
            var multiCore = new KksAdvCoreHost(multiPoint, new Assets(), value => value, (bundle, asset) => { }, null, zero);
            using var multiMachine = new KksAdvMachine(multiCore, zero, one);
            multiMachine.Start(new KksAdvScript("bundle", "multi-points", new[]
            {
                new KksAdvScript.Row(zero, zero, true, "KoikatsuPointAdd", new[] { one.ToString() }),
                new KksAdvScript.Row(one, zero, false, "KoikatsuPointAdd", new[] { one.ToString() }),
                new KksAdvScript.Row(one + one, zero, false, "Close", Array.Empty<string>())
            }), () => multiCompleted++, exception => throw exception);
            Require(multiMachine.HardWaiting && multiPlayer == one + one && pointDevice.Frames.First().Ended, "Multi commands did not share/replace one point animation");
            pointDevice.Frames.First().Begin(); pointDevice.Frames.First().Complete();
            Require(multiStore.ReadPlayer().totalPoints == zero && multiCompleted == zero, "Superseded multi animation awarded or closed machine");
            pointDevice.Frames.Last().Begin(); pointDevice.Frames.Last().Complete();
            var multiSaved = multiStore.ReadPlayer();
            Require(multiCompleted == one && multiSaved.points == one + one && multiSaved.totalPoints == one + one
                && multiSaved.pointChanges.Count(change => change.displayed) == one,
                "Multi point host/display/save barrier lost or duplicated cumulative awards");
        }
        Require(maximum == SaveData.WorldData.KoikatsuPointMax, "Configured point maximum differs from installed KKS");
        File.WriteAllText(Path.Combine(directory, "adv-point-host-checks.json"), JsonSerializer.Serialize(new
        {
            scope = "Production point host and durable backend; detached player setter and point display mocked",
            immediateDurableCommit = true, hardWait = true, variableReplacement = true,
            duplicateDisplayCompletionRejected = true, duplicateBackendRetryDoesNotCreditOrDisplay = true,
            cancellationPreservesPoints = true, staleDisplayRejected = true, displayFailurePreservesCommit = true,
            installedPointLimitVerified = true, nativePointUiExercised = false, cumulativeAwardAtDisplayStage = true,
            cancelledDisplayDoesNotAward = true, catchUpDisplayAmountPreserved = true, synchronousDisplayLeaseReleased = true,
            failedDisplayStageReleasesUi = true, cleanupFailureSuppressesSuccess = true, multiCommandsShareProductionDisplayAndLedger = true
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS point command immediate save display wait cancel duplicate stale failure; UI mocked=true");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
