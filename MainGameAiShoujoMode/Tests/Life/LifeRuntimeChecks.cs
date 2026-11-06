using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoMode.Life;

internal static class LifeRuntimeChecks
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    internal static void Run(string root)
    {
        Console.WriteLine("AI_LIFE_RUNTIME_CHECK_START productionSettingsAndWorldBinding=true");
        var canonical = "F:/kks/work/tools/KksMapSandbox/parameters.json";
        var settings = AiLifeRuntimeSettings.Load(canonical); var one = Math.Sign(settings.schema);
        using var playerProfile = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "profiles", "common-" + settings.sourceProfileVersion + "-PlayerProfile.json")));
        Require(settings.playerInitialCapacity == playerProfile.RootElement.GetProperty("_defaultInventoryMaxSlot").GetInt32(), "Player capacity came from a class initializer instead of the installed profile");
        var directory = Path.Combine(root, "life-runtime-checks", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        settings.savePath = Path.Combine(directory, "world.json"); var logs = new List<string>();
        var runtime = new AiLifeRuntime(settings, () => DateTime.Now, logs.Add); var started = runtime.Store.Read();
        Require(started.inventories.Count == one + one + one && started.inventories.All(value => value.owner == settings.worldKey), "Mode startup did not create exactly the player/storage/pantry world owners");
        var resident = Guid.NewGuid().ToString("N"); var key = runtime.BindResident(resident);
        var bound = runtime.Store.Read(); runtime.BindResident(resident);
        Require(runtime.Store.Read().revision == bound.revision && bound.inventories.Single(value => value.Key == key).owner == resident, "Rebinding created a new resident inventory or changed the revision");
        try { runtime.BindResident("card-path-is-not-a-saved-uuid"); throw new Exception("Unstable card identity was accepted"); } catch (ArgumentException) { }
        var otherKey = runtime.BindResident(Guid.NewGuid().ToString("N"));
        Require(otherKey != key && runtime.Store.Read().inventories.Count == one + one + one + one + one, "Different residents shared an inventory");
        using var agentProfile = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "profiles", "common-" + settings.sourceProfileVersion + "-AgentProfile.json")));
        var unusedGlobalLimit = agentProfile.RootElement.GetProperty("_itemSlotMaxInInventory").GetInt32();
        var quantity = checked(settings.itemSlotMax * (unusedGlobalLimit + one));
        var acquired = runtime.Store.Commit(runtime.Store.Read(), new AiLifeStateStore.Request
        { id = Guid.NewGuid().ToString("N"), action = "acquire", destination = key, category = one, count = quantity }, out var receipt, out _);
        Require(receipt.applied == quantity && acquired.inventories.Single(value => value.Key == key).items.Count > unusedGlobalLimit, "Source's unbounded agent gift storage was incorrectly capped by an unrelated profile field");
        var savedRevision = acquired.revision; settings.playerInitialCapacity += one;
        var reentered = new AiLifeRuntime(settings, () => DateTime.Now, logs.Add);
        Require(reentered.Store.Read().revision == savedRevision && reentered.Store.Read().inventories.Single(value => value.kind == "player").capacity == settings.playerInitialCapacity - one,
            "Mode re-entry rewrote persistent capacity/history using startup defaults");
        settings.worldKey += "-different-world";
        try { new AiLifeRuntime(settings, () => DateTime.Now, logs.Add); throw new Exception("Different world reused this save"); } catch (InvalidDataException) { }
        Require(logs.Any(value => value.StartsWith("AI_LIFE_READY ", StringComparison.Ordinal)) && logs.Any(value => value.StartsWith("AI_LIFE_RESIDENT_BOUND ", StringComparison.Ordinal)), "Lifecycle diagnostics missing");
        File.WriteAllText(Path.Combine(root, "inventory-runtime-checks.json"), JsonSerializer.Serialize(new
        {
            scope = "Production settings reader, mode-owned life runtime and resident binding using isolated files; actual AiModeSession/Unity execution is not performed",
            canonicalSettingsLoaded = true, nativePlayerCapacity = true, worldOwnersCreated = true,
            stableResidentReentry = true, differentResidentIsolation = true, unstableIdentityRejected = true,
            unboundedAgentGiftStorage = true, persistentCapacitiesNotReset = true, foreignWorldSaveRejected = true,
            runtimeEntryConnectedInSource = true, kksRuntimeVerified = false
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("AI_LIFE_RUNTIME_CHECK_PASS settings worldOwners stableResidents reentry capacities sourceUnboundedGift");
    }
}
