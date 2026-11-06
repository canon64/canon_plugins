using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MainGameAiShoujoMode.Conversation;

internal static class AdvActorTableChecks
{
    internal static void Run(string directory, int zero, int one, int playerNumber, int heroineOffset)
    {
        Console.WriteLine("Start native ADV actor-number resolution checks");
        object player = new object(); var first = new object(); var second = new object();
        var heroines = new List<object> { first, second };
        var table = new KksAdvActorTable<object>(() => player, () => heroines, zero, playerNumber, heroineOffset);
        Require(table.Resolve(zero) == null, "Uninitialized current actor was invented");
        table.Register(zero, first); Require(table.SetCurrent(zero), "Registered actor failed to become current");
        Require(ReferenceEquals(table.Resolve(playerNumber), player) && ReferenceEquals(table.Resolve(-heroineOffset), first)
            && ReferenceEquals(table.Resolve(-heroineOffset - one), second), "Player or indexed heroine references resolved to a slot number");
        table.Register(one, second); table.SetCurrent(one);
        Require(ReferenceEquals(table.Resolve(zero), first) && ReferenceEquals(table.Resolve(int.MaxValue), second), "Explicit slot or native default-current reference changed");
        Require(ReferenceEquals(table.Resolve(-heroineOffset), first), "Current actor overrode an explicit heroine reference");
        table.Register(int.MaxValue, player);
        Require(ReferenceEquals(table.Resolve(int.MaxValue), player), "Native default index incorrectly bypassed an explicitly registered slot");
        table.Remove(one); Require(ReferenceEquals(table.Current, second), "Removing a slot erased the separate native current-character pointer");
        table.Remove(int.MaxValue); Require(ReferenceEquals(table.Resolve(int.MaxValue), second), "Missing positive slot did not fall back to current");
        var savedPlayer = player; player = null;
        Require(ReferenceEquals(table.Resolve(playerNumber), second), "Missing player did not follow native slot/current fallback");
        table.Register(playerNumber, savedPlayer);
        Require(ReferenceEquals(table.Resolve(playerNumber), savedPlayer), "Negative slot fallback was dropped when player was absent");
        heroines[zero] = null; table.Register(-heroineOffset, first);
        Require(ReferenceEquals(table.Resolve(-heroineOffset), first), "Null heroine did not use its registered-slot fallback");
        try { table.Resolve(-heroineOffset - heroines.Count); throw new Exception("Out-of-range heroine reference was accepted"); }
        catch (ArgumentOutOfRangeException) { }
        var slots = table.SnapshotSlots(); table.Register(zero, second);
        Require(ReferenceEquals(slots[zero], first) && ReferenceEquals(table.SnapshotSlots()[zero], second)
            && !slots.ContainsKey(one), "Registered-slot snapshot was mutable through subsequent table changes or invented a fallback slot");
        table.Clear(); Require(table.Current == null && table.Resolve(int.MaxValue) == null, "Clear retained slot/current state");
        File.WriteAllText(Path.Combine(directory, "adv-actor-table-checks.json"), JsonSerializer.Serialize(new
        {
            scope = "Production actor-number table using managed identity objects; native actor creation, snapshots and restoration are not executed",
            playerNumber, heroineOffset, explicitSlots = true, negativeRoleReferences = true, currentFallback = true,
            nullRoleFallback = true, sharedIdentity = true, independentCurrentPointer = true, missingHeroineRejected = true,
            registeredSlotSnapshotIndependent = true
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS ADV actors explicit slots player/heroines current fallback identity clear");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
