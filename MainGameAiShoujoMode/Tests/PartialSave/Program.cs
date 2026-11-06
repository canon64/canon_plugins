using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MainGameAiShoujoMode;

internal static class Program
{
    private static int _checks;
    private static void Check(bool value, string reason) { _checks++; if (!value) throw new Exception(reason); }
    private static void Main(string[] args)
    {
        var root = Path.Combine(args.Single(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        Console.WriteLine("HOTEL_PARTIAL_SAVE_TEST_START");
        try
        {
            var keys = new[] { "first", "middle", "last" };
            foreach (string failed in keys)
            {
                var store = new HotelRoomStateStore(Path.Combine(root, "case" + failed), "201");
                foreach (string key in new[] { "first", "middle", "last" }) store.SavePreference("card", new HotelRoomPreference { key = key, value = "old" });
                var logs = new List<string>(); var batch = new HotelRoomSaveBatch(() => true, logs.Add, store.FilePath, "test");
                foreach (string key in new[] { "first", "middle", "last" })
                {
                    bool fail = key == failed;
                    batch.Item(key, "appearance.preferences[" + key + "]", () => fail ? throw new InvalidOperationException("injected capture") : new HotelRoomPreference { key = key, value = "new" },
                        value => store.SavePreference("card", value), value => value.value);
                }
                Check(!batch.Finish() && batch.FailureNotice.Contains("injected capture"), "Failure did not reach user result");
                var values = store.Load().appearance.preferences;
                foreach (string key in keys) Check(values.Single(p => p.key == key).value == (key == failed ? "old" : "new"), "Independent parameter write/retention failed");
                Check(logs.Any(x => x.Contains("stage=capture") && x.Contains("injected capture")), "Failure details absent");
                Check(logs.Where(x => x.Contains("result=success")).All(x => x.Contains(store.FilePath) && x.Contains("key=appearance.preferences") && x.Contains("content=new")), "Saved destination/key/content absent");
                Check(logs.Last().Contains("result=complete") && logs.Last().Contains("success=[") && logs.Last().Contains("failed=["), "Final result lists absent");
            }
            var disk = new HotelRoomStateStore(Path.Combine(root, "disk"), "201"); disk.SaveActor("card", true, "old-card");
            Directory.CreateDirectory(disk.FilePath + ".tmp");
            var diskLogs = new List<string>(); var diskBatch = new HotelRoomSaveBatch(() => true, diskLogs.Add, disk.FilePath, "test");
            diskBatch.Item("female", "appearance.female", () => "new-card", value => disk.SaveActor("card", true, value));
            var independent = new HotelRoomStateStore(Path.Combine(root, "other"), "201");
            diskBatch.Item("other-file", "appearance.male", () => "male", value => independent.SaveActor("card", false, value));
            Check(!diskBatch.Finish() && disk.Load().appearance.female == "old-card" && independent.Load().appearance.male == "male", "Disk failure blocked other file or damaged old value");
            Check(!diskLogs.Any(x => x.Contains("result=success") && x.Contains("item=female")) && diskLogs.Any(x => x.Contains("stage=write") && x.Contains("result=failure")), "Disk error misreported success");
            bool enabled = false; int summaries = default(int), output = default(int);
            var off = new HotelRoomSaveBatch(() => enabled, _ => output++, "201.json", "test");
            off.Item("ok", "key", () => true, _ => { }, _ => { summaries++; return "summary"; }); off.Finish();
            Check(summaries == default(int) && output == default(int), "OFF collected diagnostic data");
            enabled = true; off.Item("on", "key", () => true, _ => { }, _ => { summaries++; return "summary"; });
            int onSummaries = summaries, onOutput = output;
            enabled = false; off.Item<int>("failure-off", "key", () => throw new IOException("OFF failure reason"), _ => { }); off.Finish();
            Check(onSummaries > default(int) && onOutput > default(int) && summaries == onSummaries && output == onOutput && off.FailureNotice.Contains("OFF failure reason"), "Toggle or diagnostic OFF failure notification broken");
            var sink = new HotelRoomSaveBatch(() => true, _ => throw new IOException("sink"), "201.json", "test");
            Check(sink.Item("committed", "key", () => true, _ => { }) && sink.Finish() && sink.HasNotice && sink.FailureNotice.Contains("sink"), "Diagnostic sink exception changed committed result or lost notification");
            foreach (bool writeFails in new[] { false, true })
            {
                var store = new HotelRoomStateStore(Path.Combine(root, "group" + writeFails), "201");
                store.SavePreference("card", new HotelRoomPreference { key = "middle", value = "old" });
                var groupedLogs = new List<string>(); int writes = default(int), committed = default(int);
                var group = new HotelRoomSaveBatch(() => true, groupedLogs.Add, store.FilePath, "group-test");
                group.BeginFile(store.BeginSave, () => { writes++; if (writeFails) throw new IOException("injected grouped disk"); store.CommitSave(); }, store.EndSave);
                foreach (string key in keys)
                    group.Item(key, "appearance.preferences[" + key + "]", () => key == "middle" ? throw new InvalidOperationException("middle capture") : new HotelRoomPreference { key = key, value = "new" },
                        value => store.SavePreference("card", value), value => value.value, _ => committed++);
                group.Item("invalid-placement", "placement", () => new HotelRoomPlacement { x = float.NaN }, store.SavePlacement);
                Check(!groupedLogs.Any(x => x.Contains("result=success")), "Success logged before shared file committed");
                Check(!File.ReadAllText(store.FilePath).Contains("new"), "Staging wrote the file early");
                group.Finish();
                var restored = new HotelRoomStateStore(Path.Combine(root, "group" + writeFails), "201").Load();
                Check(writes == keys.Count(key => key == "middle"), "Expected exactly one shared writer call");
                Check(restored.appearance.preferences.Single(p => p.key == "middle").value == "old" && restored.placement == null, "Capture/validation failure damaged previous state");
                if (writeFails)
                    Check(committed == default(int) && !groupedLogs.Any(x => x.Contains("result=success")) && groupedLogs.Count(x => x.Contains("stage=write") && x.Contains("result=failure")) == keys.Count(key => key != "middle"), "Shared write failure reported pending items as saved");
                else
                    Check(committed == keys.Count(key => key != "middle") && restored.appearance.preferences.Count(p => p.value == "new") == committed, "Successful staged items were not persisted together");
            }
            string restorePath = Path.Combine(root, "restore.json");
            File.WriteAllText(restorePath, "{\"version\":\"v1\",\"room\":\"201\",\"selection\":{\"mode\":\"aibu\",\"id\":0,\"name\":\"saved\",\"bundle\":\"b\",\"asset\":\"a\"},\"lighting\":{\"light\":\"invalid\"},\"appearance\":{\"female\":\"card\",\"preferences\":[{\"key\":\"bad\",\"value\":{}},{\"key\":\"good\",\"value\":\"new\"}]},\"placement\":{\"x\":1,\"y\":2,\"z\":3,\"yaw\":4}}");
            var decoded = HotelRoomStateReader.Read(restorePath, "201");
            Check(decoded.Errors.Any(p => p.Key == "lighting") && decoded.State.selection.name == "saved" && decoded.State.placement != null
                && decoded.State.appearance.female == "card" && decoded.State.appearance.preferences.Any(p => p.key == "good"), "A typed JSON member failure erased independent members");
            foreach (string failureStage in new[] { "read", "apply", "verify" })
            {
                string current = "normal", later = "normal"; var restoreLogs = new List<string>();
                var restoration = new HotelRoomRestoreBatch(() => true, restoreLogs.Add, restorePath);
                restoration.Item("middle", "key-middle", "target", () => failureStage == "read" ? throw new InvalidDataException("injected parse") : "saved",
                    value => { current = value; if (failureStage == "apply") throw new InvalidOperationException("injected apply"); },
                    value => failureStage != "verify", value => value, () => current, () => { string before = current; return () => current = before; });
                restoration.Item("later", "key-later", "later-target", () => "saved", value => later = value, value => later == value, value => value, () => later);
                Check(!restoration.Finish() && current == "normal" && later == "saved" && restoration.HasNotice && restoration.Status.Contains("middle"), "Restore failure lost current state, later result or notification");
                Check(!restoreLogs.Any(x => x.Contains("result=success item=middle")) && restoreLogs.Any(x => x.Contains("result=success item=later") && x.Contains("actual=saved")), "Restore success logged before actual application");
                Check(restoreLogs.Last().Contains("partial-failure") && restoreLogs.Any(x => x.Contains("key=key-middle") && x.Contains("reason=")), "Restore source/key/reason/final lists missing");
            }
            int restoreSummaries = default(int), restoreOutput = default(int);
            var restoreOff = new HotelRoomRestoreBatch(() => false, _ => restoreOutput++, restorePath);
            restoreOff.Item("on-target", "key", "target", () => "saved", _ => { }, _ => true,
                _ => { restoreSummaries++; return "summary"; }, () => { restoreSummaries++; return "actual"; });
            restoreOff.Failure("failure", "key", new IOException("always notify")); restoreOff.Finish();
            Check(restoreSummaries == default(int) && restoreOutput == default(int) && restoreOff.HasNotice && restoreOff.Status.Contains("always notify"), "Restore OFF collected diagnostics or suppressed notification");
            var effectsStore = new HotelRoomStateStore(Path.Combine(root, "actor-effects"), "201");
            effectsStore.SaveActorEffect("card", new HotelRoomActorEffect { female = true, key = "controls", value = "manual tears" });
            effectsStore.BeginSave();
            effectsStore.SaveActorEffect("card", new HotelRoomActorEffect { female = true, key = "DroolLevel", value = "visible" });
            effectsStore.SavePreference("card", new HotelRoomPreference { key = "gaugeFemale", value = "raised" });
            effectsStore.CommitSave(); effectsStore.EndSave();
            var effectsRead = HotelRoomStateReader.Read(effectsStore.FilePath, "201");
            Check(effectsRead.Errors.Count == default(int) && effectsRead.State.appearance.effects.Single(e => e.key == "controls").value == "manual tears"
                && effectsRead.State.appearance.effects.Single(e => e.key == "DroolLevel").value == "visible", "Per-actor controls/MOD effects were lost while merging or reading independent parameters");
            var final = new HotelFinalSaveBoundary(); int finalCaptures = default(int); bool permit = true;
            Check(!final.Save(() => false, () => throw new Exception("not-ready capture")), "Startup failure saved unready defaults");
            Check(final.Save(() => true, () => { Check(permit, "Final provider capture ran after revocation"); finalCaptures++; return true; }), "Pre-revoke final save failed");
            permit = false;
            Check(final.Save(() => true, () => throw new Exception("destroy-time recapture")) && finalCaptures == new[] { "one" }.Length, "OnDestroy recaptured or overwrote after final save");
            final.Reset();
            Check(!final.Save(() => true, () => false) && !final.Save(() => true, () => throw new Exception("failed final retry")), "Failed final save reused stale data or recaptured after shutdown");
            Console.WriteLine("HOTEL_PARTIAL_SAVE_PASS checks=" + _checks + " captureFirstMiddleLast=true writeFailure=true priorRetained=true diagnosticOff=true notifications=true");
        }
        finally { Directory.Delete(root, true); }
    }
}
