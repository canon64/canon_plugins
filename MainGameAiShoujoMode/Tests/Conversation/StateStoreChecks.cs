using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json;
using MainGameAiShoujoMode.Conversation;

internal static class StateStoreChecks
{
    internal static void Run(string directory, int schema, KksNormalConversationRules.Settings settings)
    {
        Console.WriteLine("Start KKS conversation resident save checks using the production serializer and filesystem");
        var path = Path.Combine(directory, "save-checks", Guid.NewGuid().ToString("N"), "conversation-state.json");
        var store = new KksConversationStateStore(path, schema);
        KksConversationStateStore.Result NewOperation() => new KksConversationStateStore.Result
        {
            id = Guid.NewGuid().ToString("N"), bundle = "native bundle", asset = "native asset", blockRow = settings.one,
            nativeFavor = settings.one, nativeLewdness = settings.one, appliedFavor = settings.one, appliedLewdness = settings.zero,
            topic = settings.one, selected = settings.one, correct = settings.zero
        };
        KksConversationStateStore.Resident NewResident() => new KksConversationStateStore.Resident
        {
            personality = settings.zero, temperament = new byte[settings.temperamentCount], callId = settings.zero, friend = true, girlfriend = false
        };
        var first = store.GetOrCreate("pod:test", NewResident); var identity = first.id;
        first.favor = settings.one;
        var reread = store.GetOrCreate("pod:test", () => throw new Exception("Existing resident was recreated"));
        Require(reread.favor == settings.zero && reread.id == identity, "Returned record mutated the live save");
        var operation = NewOperation();
        var saved = store.Commit(reread, operation, resident =>
        {
            resident.favor += settings.one; resident.events.Add(settings.requiredNicknameEvent); resident.temperament[settings.zero] = (byte)settings.one;
        });
        Require(saved.favor == settings.one && saved.events.Contains(settings.requiredNicknameEvent), "Native result was not saved");
        var reloaded = new KksConversationStateStore(path, schema).GetOrCreate("pod:test", NewResident);
        Require(reloaded.id == identity && reloaded.favor == settings.one && reloaded.temperament[settings.zero] == settings.one, "Resident identity/state did not survive reload");
        Require(reloaded.results.Single().bundle == operation.bundle && reloaded.results.Single().nativeFavor == operation.nativeFavor, "Native result provenance was lost");
        Require(reloaded.friend && !reloaded.girlfriend && reloaded.results.Single().selected == operation.selected
            && reloaded.results.Single().appliedFavor == operation.appliedFavor && reloaded.results.Single().topic == operation.topic, "Relation flags or result details were lost");
        var duplicate = store.Commit(reread, operation, _ => throw new Exception("Duplicate operation was applied"));
        Require(duplicate.revision == saved.revision && duplicate.favor == saved.favor, "Duplicate operation changed the save");
        try
        {
            store.Commit(reread, NewOperation(), resident => resident.favor++);
            throw new Exception("Stale revision was accepted");
        }
        catch (InvalidOperationException) { }
        var secondStore = new KksConversationStateStore(path, schema);
        var next = secondStore.Commit(reloaded, NewOperation(), resident => resident.lewdness += settings.one);
        try
        {
            store.Commit(saved, NewOperation(), resident => resident.favor++);
            throw new Exception("Another store's commit was overwritten");
        }
        catch (InvalidOperationException) { }
        var bytes = File.ReadAllBytes(path);
        try
        {
            secondStore.Commit(next, NewOperation(), resident => { resident.favor++; throw new IOException("Simulated domain result failure"); });
            throw new Exception("Failed result update was accepted");
        }
        catch (IOException) { }
        Require(File.ReadAllBytes(path).SequenceEqual(bytes), "Failed domain result changed the saved file");
        using (var locked = new FileStream(path + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            try { secondStore.Commit(next, NewOperation(), resident => resident.favor++); throw new Exception("Busy save lock was ignored"); }
            catch (IOException) { }
        }
        Require(File.ReadAllBytes(path).SequenceEqual(bytes), "Busy save lock changed the saved file");
        Require(File.Exists(path + ".previous"), "Previous complete save was not retained");
        Require(!Directory.EnumerateFiles(Path.GetDirectoryName(path), "*.pending-*").Any(), "Temporary save files leaked");
        var other = store.GetOrCreate("pod:another", NewResident);
        Require(other.id != identity, "Distinct resident origins shared identity");
        var corrupt = Path.Combine(Path.GetDirectoryName(path), "corrupt.json"); File.WriteAllText(corrupt, "{broken");
        try { new KksConversationStateStore(corrupt, schema); throw new Exception("Corrupt save was silently reset"); }
        catch (SerializationException) { }
        Require(File.ReadAllText(corrupt) == "{broken", "Corrupt save was overwritten");
        var report = new { scope = "Production DataContract serializer, resident identity, durable file replacement and transaction ownership; no game behavior proof",
            savePath = path, reload = true, identity = true, detachedReads = true, duplicateCommit = true, staleRevision = true,
            crossStoreConflict = true, failedResultRollback = true, exclusiveLock = true, previousSave = true, corruptionRejected = true };
        File.WriteAllText(Path.Combine(directory, "state-store-checks.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS residentSave reload identity duplicateCommit crossStoreConflict failedResultRollback exclusiveLock previousSave corruptionRejected");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
