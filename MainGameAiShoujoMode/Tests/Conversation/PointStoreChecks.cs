using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.Json;
using MainGameAiShoujoMode.Conversation;

internal static class PointStoreChecks
{
    [DataContract] private sealed class Legacy
    {
        [DataMember] public int schema;
        [DataMember] public List<KksConversationStateStore.Resident> residents;
    }
    internal static void Run(string directory, int schema, int zero, int one, int maximum)
    {
        Console.WriteLine("Start durable AI-player point ledger checks with production serializer");
        var path = Path.Combine(directory, "save-checks", Guid.NewGuid().ToString("N"), "points.json");
        var store = new KksConversationStateStore(path, schema);
        KksConversationStateStore.PointChange Operation(int delta) => new KksConversationStateStore.PointChange
            { id = Guid.NewGuid().ToString("N"), bundle = "adv/test", asset = "scene", sourceRow = one, delta = delta, applied = int.MaxValue,
                displayed = true, displayDelta = int.MaxValue, awardApplied = int.MaxValue };
        var initial = store.ReadPlayer(); initial.points = one;
        Require(store.ReadPlayer().points == zero, "Point snapshot mutated live state");
        var op = Operation(one); var saved = store.CommitPoints(store.ReadPlayer(), op, zero, maximum, out var applied);
        Require(applied && saved.points == one && saved.pointChanges.Single().applied == one && op.applied == int.MaxValue, "Point commit failed or trusted caller's applied amount");
        var second = new KksConversationStateStore(path, schema); var reloaded = second.ReadPlayer();
        Require(reloaded.points == one && reloaded.pointChanges.Single().id == op.id, "Points/provenance did not survive reload");
        Require(reloaded.totalPoints == zero && !reloaded.pointChanges.Single().displayed && reloaded.pointChanges.Single().displayDelta == zero,
            "Command commit trusted caller display stage or awarded points before display");
        var duplicate = second.CommitPoints(initial, op, zero, maximum, out applied);
        Require(!applied && duplicate.revision == saved.revision && duplicate.points == one, "Duplicate point operation applied again");
        op.delta = one + one;
        try { second.CommitPoints(initial, op, zero, maximum, out _); throw new Exception("Conflicting duplicate was accepted"); } catch (InvalidOperationException) { }
        var next = second.CommitPoints(reloaded, Operation(one), zero, maximum, out _);
        try { store.CommitPoints(reloaded, Operation(one), zero, maximum, out _); throw new Exception("Stale player revision was accepted"); } catch (InvalidOperationException) { }
        var full = second.CommitPoints(next, Operation(maximum), zero, maximum, out _);
        Require(full.points == maximum, "Upper native point limit failed");
        var empty = second.CommitPoints(full, Operation(-maximum - one), zero, maximum, out _);
        Require(empty.points == zero, "Lower native point limit failed");
        full = second.CommitPoints(empty, Operation(maximum), zero, maximum, out _);
        var overflow = second.CommitPoints(full, Operation(int.MaxValue), zero, maximum, out _);
        Require(overflow.points == zero, "Point addition did not match native unchecked Int32 overflow then clamp");
        var bytes = File.ReadAllBytes(path); var invalid = Operation(one); invalid.asset = "";
        try { second.CommitPoints(overflow, invalid, zero, maximum, out _); throw new Exception("Invalid point provenance was saved"); } catch (InvalidDataException) { }
        Require(File.ReadAllBytes(path).SequenceEqual(bytes), "Failed point transaction modified save");
        using (var locked = new FileStream(path + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        { try { second.CommitPoints(overflow, Operation(one), zero, maximum, out _); throw new Exception("Point save bypassed lock"); } catch (IOException) { } }
        Require(File.ReadAllBytes(path).SequenceEqual(bytes), "Busy point lock modified save");
        var awarded = second.MarkPointDisplayed(op.id, one, zero, maximum, out applied);
        Require(applied && awarded.totalPoints == one && awarded.points == zero && awarded.pointChanges.Single(value => value.id == op.id).awardApplied == one,
            "Display stage did not award independently of already committed points");
        var awardedAgain = new KksConversationStateStore(path, schema).MarkPointDisplayed(op.id, one, zero, maximum, out applied);
        Require(!applied && awardedAgain.revision == awarded.revision && awardedAgain.totalPoints == one, "Display stage was not durable/idempotent");
        try { second.MarkPointDisplayed(op.id, one + one, zero, maximum, out _); throw new Exception("Conflicting award accepted"); } catch (InvalidOperationException) { }
        try { second.MarkPointDisplayed("unknown-command", one, zero, maximum, out _); throw new Exception("Uncommitted award accepted"); } catch (InvalidOperationException) { }
        var negative = Operation(-one); second.CommitPoints(second.ReadPlayer(), negative, zero, maximum, out _);
        var afterNegative = second.MarkPointDisplayed(negative.id, -one, zero, maximum, out _);
        Require(afterNegative.totalPoints == one && afterNegative.pointChanges.Last().displayed && afterNegative.pointChanges.Last().awardApplied == zero,
            "Nonpositive display reduced cumulative points");
        var saturated = Operation(one); second.CommitPoints(second.ReadPlayer(), saturated, zero, maximum, out _);
        Require(second.MarkPointDisplayed(saturated.id, maximum, zero, maximum, out _).totalPoints == maximum, "Cumulative native upper clamp failed");
        var totalOverflow = Operation(one); second.CommitPoints(second.ReadPlayer(), totalOverflow, zero, maximum, out _);
        Require(second.MarkPointDisplayed(totalOverflow.id, int.MaxValue, zero, maximum, out _).totalPoints == zero, "Cumulative native unchecked overflow/clamp failed");
        var awardLock = Operation(one); second.CommitPoints(second.ReadPlayer(), awardLock, zero, maximum, out _); bytes = File.ReadAllBytes(path);
        using (var locked = new FileStream(path + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        { try { second.MarkPointDisplayed(awardLock.id, one, zero, maximum, out _); throw new Exception("Award bypassed lock"); } catch (IOException) { } }
        Require(File.ReadAllBytes(path).SequenceEqual(bytes), "Busy display lock modified save");
        var oldPlayerPath = Path.Combine(Path.GetDirectoryName(path), "old-player.json");
        File.WriteAllText(oldPlayerPath, JsonSerializer.Serialize(new
        {
            schema, residents = Array.Empty<object>(), player = new
            {
                revision = "old-player", points = one,
                pointChanges = new[] { new { id = "old-point", bundle = "old-bundle", asset = "old-asset", sourceRow = zero, delta = one, applied = one } }
            }
        }));
        var oldPlayer = new KksConversationStateStore(oldPlayerPath, schema).ReadPlayer();
        Require(oldPlayer.points == one && oldPlayer.totalPoints == zero && !oldPlayer.pointChanges.Single().displayed,
            "Existing player without display ledger was lost or retroactively awarded cumulative points");
        var resident = new KksConversationStateStore.Resident { id = "legacy-resident", origin = "pod:legacy", revision = "resident-revision", temperament = Array.Empty<byte>() };
        var legacyPath = Path.Combine(Path.GetDirectoryName(path), "legacy.json");
        using (var stream = File.Create(legacyPath)) new DataContractJsonSerializer(typeof(Legacy)).WriteObject(stream, new Legacy { schema = schema, residents = new List<KksConversationStateStore.Resident> { resident } });
        var legacy = new KksConversationStateStore(legacyPath, schema);
        var migrated = legacy.CommitPoints(legacy.ReadPlayer(), Operation(one), zero, maximum, out applied);
        var retained = legacy.GetOrCreate(resident.origin, () => throw new Exception("Legacy resident lost"));
        Require(applied && migrated.points == one && retained.id == resident.id, "Legacy player migration lost points or resident identity");
        legacy.Commit(retained, new KksConversationStateStore.Result { id = "resident-result", bundle = "bundle", asset = "asset" }, value => value.favor = one);
        Require(new KksConversationStateStore(legacyPath, schema).ReadPlayer().points == one, "Resident result overwrote player points");
        var corrupt = Path.Combine(Path.GetDirectoryName(path), "null-player.json");
        File.WriteAllText(corrupt, JsonSerializer.Serialize(new { schema, residents = Array.Empty<object>(), player = (object)null }));
        try { new KksConversationStateStore(corrupt, schema); throw new Exception("Explicitly corrupt player was migrated as legacy"); } catch (InvalidDataException) { }
        File.WriteAllText(Path.Combine(directory, "player-point-store-checks.json"), JsonSerializer.Serialize(new
        {
            scope = "Production DataContract player/resident save transactions and filesystem; no KKS UI or game execution",
            detachedRead = true, durableReload = true, duplicateCommit = true, conflictingDuplicateRejected = true,
            staleRevisionRejected = true, nativeClampAndUncheckedOverflow = true, failedCommitRollback = true,
            exclusiveLock = true, legacyMigrationPreservesResident = true, residentWritesPreservePoints = true,
            explicitNullPlayerRejected = true, cumulativeOnlyAtDisplay = true, cumulativeDurableAndIdempotent = true,
            nonpositiveDisplayDoesNotAward = true, cumulativeNativeClampAndOverflow = true, displayRequiresCommittedCommand = true,
            existingPlayerWithoutDisplayStagePreserved = true
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS player point save reload duplicate conflict limits overflow migration corruption lock display-stage cumulative");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
