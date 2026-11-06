using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoMode.Life;
using Store = MainGameAiShoujoMode.Life.AiLifeStateStore;

internal static class LifeStoreChecks
{
    private static void Require(bool value, string label) { if (!value) throw new Exception(label); }
    private static void Reject<T>(Action action, string label) where T : Exception
    { try { action(); } catch (T) { return; } throw new Exception(label); }
    internal static void Run(string root, JsonElement profile)
    {
        Console.WriteLine("AI_LIFE_INVENTORY_STORE_START productionSerializerAndFilesystem=true");
        var limit = profile.GetProperty("define").GetProperty("_itemSlotMax").GetInt32(); var zero = default(int); var one = Math.Sign(limit);
        var capacity = profile.GetProperty("agent").GetProperty("_itemSlotMaxInInventory").GetInt32();
        var directory = Path.Combine(root, "life-store-checks", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "world.json"); var now = DateTime.Now.AddTicks(one);
        Store Open() => new Store(path, one, limit, () => now);
        var store = Open();
        store.EnsureInventory("player", "world", capacity); store.EnsureInventory("agent", "stable-resident", capacity);
        store.EnsureInventory("storage", "world", null); var state = store.EnsureInventory("pantry", "world", one);
        Require(state.inventories.Count == one + one + one + one, "Four owner kinds were not stored independently");
        Require(store.EnsureInventory("agent", "stable-resident", capacity).revision == state.revision, "Repeated resident binding changed identity/revision");
        Reject<InvalidOperationException>(() => store.EnsureInventory("agent", "stable-resident", one), "Capacity change silently replaced an owner");
        Store.Request Request(string action, string destination, int count, params string[] sources) => new Store.Request
        { id = Guid.NewGuid().ToString("N"), action = action, destination = destination, sources = sources, category = one, item = zero, count = count };
        var acquire = Request("acquire", "player:world", limit + one);
        state = store.Commit(state, acquire, out var acquisition, out var applied);
        Require(applied && acquisition.applied == limit + one && acquisition.remaining == zero, "Acquisition failed");
        var gift = Request("transfer", "agent:stable-resident", one, "player:world"); var beforeGift = state;
        state = store.Commit(state, gift, out var giftReceipt, out applied);
        Require(applied && giftReceipt.applied == one && Count(state, "player:world") == limit && Count(state, "agent:stable-resident") == one, "Gift did not atomically move one item");
        var replay = Open().Commit(beforeGift, gift, out var replayReceipt, out applied);
        Require(!applied && replay.revision == state.revision && replayReceipt.applied == one && Count(replay, "player:world") == limit, "Reentrant/retried gift consumed twice");
        gift.count += one;
        Reject<InvalidOperationException>(() => store.Commit(state, gift, out _, out _), "Reused ID accepted a changed quantity"); gift.count -= one;
        var other = Open(); var stale = state;
        state = other.Commit(state, Request("transfer", "storage:world", one, "player:world"), out _, out _);
        Reject<InvalidOperationException>(() => store.Commit(stale, Request("consume", null, one, "player:world"), out _, out _), "Stale confirmation overwrote a concurrent owner change");
        var beforeFailure = File.ReadAllBytes(path);
        Reject<InvalidOperationException>(() => store.Commit(state, Request("consume", null, limit + limit, "agent:stable-resident"), out _, out _), "Shortage allowed a full required consumption");
        Require(beforeFailure.SequenceEqual(File.ReadAllBytes(path)), "Failed quantity preflight modified the save");
        state = store.Commit(state, Request("acquire", "pantry:world", limit), out _, out _);
        var noRoom = Request("transfer", "pantry:world", one, "agent:stable-resident"); beforeFailure = File.ReadAllBytes(path);
        Reject<InvalidOperationException>(() => store.Commit(state, noRoom, out _, out _), "Full pantry accepted an all-or-nothing transfer");
        Require(beforeFailure.SequenceEqual(File.ReadAllBytes(path)) && Count(store.Read(), "agent:stable-resident") == one, "Full destination removed source stock or published a receipt");
        noRoom.allowPartial = true; state = store.Commit(state, noRoom, out var partial, out applied);
        Require(applied && partial.applied == zero && partial.remaining == one && Count(state, "agent:stable-resident") == one, "Partial transfer lost items when destination accepted none");
        var eat = Request("consume", null, limit - one, "pantry:world"); state = store.Commit(state, eat, out _, out _);
        var mixed = Request("transfer", "pantry:world", limit + one, "storage:world", "player:world"); mixed.allowPartial = true;
        var totalBefore = state.inventories.Sum(value => value.items.Sum(item => (long)item.Count));
        state = store.Commit(state, mixed, out partial, out _);
        Require(partial.applied == limit - one && partial.remaining == one + one && Count(state, "storage:world") == zero
            && state.inventories.Sum(value => value.items.Sum(item => (long)item.Count)) == totalBefore, "Partial transfer violated source order, destination limit or item conservation");
        var medicine = Request("consume", null, one, "agent:stable-resident"); var beforeMedicine = state;
        state = store.Commit(state, medicine, out _, out _);
        // A failed/cancelled presentation retries the already committed operation.
        state = Open().Commit(beforeMedicine, medicine, out _, out applied);
        Require(!applied && Count(state, "agent:stable-resident") == zero, "Presentation retry consumed medicine twice");
        var detached = store.Read(); detached.inventories.First().items.Clear(); detached.operations.Clear();
        Require(store.Read().operations.Any() && store.Read().inventories.First().items.Any(), "Returned snapshot retained mutable save aliases");
        var restored = Open().Read();
        Require(restored.inventories.SelectMany(value => value.items).All(item => item.LatestDateTime.Ticks == now.Ticks && item.LatestDateTime.Kind == now.Kind), "Inventory save lost acquisition timestamp precision/kind");
        beforeFailure = File.ReadAllBytes(path);
        using (var held = new FileStream(path + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Reject<IOException>(() => store.Read(), "Exclusive save lock was ignored");
        using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            Reject<IOException>(() => store.Commit(restored, Request("acquire", "storage:world", one), out _, out _), "Replace failure was swallowed");
        Require(beforeFailure.SequenceEqual(File.ReadAllBytes(path)) && !Directory.EnumerateFiles(directory, "*.pending-*").Any(), "Failed publication changed the save or leaked a pending file");
        Require(File.Exists(path + ".previous"), "Atomic replacement did not retain the previous save");
        File.WriteAllText(path, "null"); Reject<InvalidDataException>(() => Open(), "Null/corrupt save was silently reinitialized");
        File.WriteAllBytes(path, beforeFailure);
        Reject<InvalidDataException>(() => new Store(path, one + one, limit, () => now), "Unknown schema was accepted");
        File.WriteAllText(Path.Combine(root, "inventory-store-checks.json"), JsonSerializer.Serialize(new
        {
            scope = "Production AI inventory save/transactions with real filesystem; no game entry, UI, illness or growth effect integration",
            fourOwnerKinds = true, stableResidentBinding = true, operationReplay = true, intentCollisionRejected = true,
            concurrentRevisionConflict = true, shortageRollback = true, fullDestinationRollback = true,
            partialTransferConservationAndOrder = true, presentationRetryDoesNotConsumeAgain = true, detachedSnapshots = true,
            timestampPrecision = true, exclusiveLock = true, publicationFailurePreservesSave = true,
            previousSaveRetained = true, corruptAndUnknownSchemaRejected = true,
            runtimeEntryConnected = false, medicineIllnessEffectAtomic = false
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("AI_LIFE_INVENTORY_STORE_PASS fourOwners atomicTransfer consume retry conflict shortage full partial lock publicationFailure corruption");
    }
    private static long Count(Store.Snapshot state, string key) => state.inventories.Single(value => value.Key == key).items.Sum(item => (long)item.Count);
}
