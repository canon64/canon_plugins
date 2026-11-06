using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text.Json;
using MainGameAiShoujoMode.Life;
using Item = MainGameAiShoujoMode.Life.AiInventoryOperations.Item;
using Native = AIProject.SaveData.StuffItem;

internal static class Program
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static int Main(string[] args)
    {
        if (args.Length == new[] { "mode", "content", "quantity" }.Length && args.First() == "--seed-test-stock")
        { TestStockSeed.Run(args.Skip(Math.Sign(args.Length)).First(), int.Parse(args.Last(), System.Globalization.CultureInfo.InvariantCulture)); return default; }
        Console.WriteLine("AI_INVENTORY_SOURCE_DIFFERENTIAL_START noUnityEngine=true");
        var root = Path.GetFullPath(args.Single()); int cases = default;
        using var evidence = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "state-evidence.json")));
        foreach (var profile in evidence.RootElement.GetProperty("profileVersions").EnumerateObject())
        {
            var limit = profile.Value.GetProperty("define").GetProperty("_itemSlotMax").GetInt32();
            var agentCapacity = profile.Value.GetProperty("agent").GetProperty("_itemSlotMaxInInventory").GetInt32();
            var zero = default(int); var one = Math.Sign(limit); var now = DateTime.Now;
            Singleton<Manager.Resources>.Instance.DefinePack.MapDefines.ItemSlotMax = limit;
            var operations = new AiInventoryOperations(limit, () => now);
            var counts = new[] { zero, one, limit - one, limit };
            var requests = new[] { zero, one, limit - one, limit, limit + one, limit + limit + one };
            foreach (var left in counts)
            foreach (var right in counts)
            foreach (var request in requests)
            {
                List<Item> Initial() => new List<Item>
                {
                    new Item { CategoryID = one, ID = zero, Count = left },
                    new Item { CategoryID = zero, ID = zero, Count = one },
                    new Item { CategoryID = one, ID = zero, Count = right }
                }.Where(item => item.Count > zero).ToList();
                Native Convert(Item value) => new Native { CategoryID = value.CategoryID, ID = value.ID, Count = value.Count, LatestDateTime = value.LatestDateTime };
                Item Key() => new Item { CategoryID = one, ID = zero, Count = request };
                foreach (int? capacity in new int?[] { null, zero, one, one + one, agentCapacity })
                {
                    var inventory = Initial(); var native = inventory.Select(Convert).ToList(); var item = Key(); var nativeItem = Convert(item);
                    var actual = operations.AddItem(inventory, item, request, capacity);
                    var expected = capacity.HasValue ? AIProject.StuffItemExtensions.AddItem(native, nativeItem, request, capacity.Value)
                        : AIProject.StuffItemExtensions.AddItem(native, nativeItem, request);
                    Require(actual == expected && Snapshot(inventory) == Snapshot(native) && item.Count == nativeItem.Count,
                        "Add differs: profile=" + profile.Name + " left=" + left + " right=" + right + " count=" + request + " capacity=" + capacity);
                    Require(inventory.Any(value => ReferenceEquals(value, item)) == native.Any(value => ReferenceEquals(value, nativeItem)), "Final-stack identity differs");
                    if (capacity.HasValue)
                    {
                        var after = Key(); var afterNative = Convert(after);
                        var canAdd = operations.CanAddItem(inventory, capacity.Value, after, request, out var possible);
                        var nativeCanAdd = AIProject.StuffItemExtensions.CanAddItem(native, capacity.Value, afterNative, request, out var nativePossible);
                        Require(canAdd == nativeCanAdd && possible == nativePossible, "CanAdd/possible differs after partial/full add");
                        canAdd = operations.CanAddItem(inventory, capacity.Value, null, request, out possible);
                        nativeCanAdd = AIProject.StuffItemExtensions.CanAddItem(native, capacity.Value, null, request, out nativePossible);
                        Require(canAdd == nativeCanAdd && possible == nativePossible, "Untyped inventory-space probe differs from the source InventoryCheck path");
                    }
                    cases++;
                }
                var removal = Initial(); var nativeRemoval = removal.Select(Convert).ToList(); var remaining = request; var nativeRemaining = request;
                var removed = operations.RemoveItem(removal, Key(), ref remaining);
                var nativeRemoved = AIProject.StuffItemExtensions.RemoveItem(nativeRemoval, Convert(Key()), ref nativeRemaining);
                Require(removed == nativeRemoved && remaining == nativeRemaining && Snapshot(removal) == Snapshot(nativeRemoval), "Remove order, remainder or result differs"); cases++;
            }
            var first = new List<Item> { new Item { CategoryID = one, Count = one } };
            var second = new List<Item> { new Item { CategoryID = one, Count = limit } };
            var missing = operations.RemoveStorages(new[] { first, second }, new Item { CategoryID = one }, limit + one + one);
            Require(missing == one && !first.Any() && !second.Any(), "Storage traversal lost order or shortage remainder");
            var persisted = new Item { CategoryID = one, ID = one, Count = limit, LatestDateTime = now };
            using var stream = new MemoryStream(); var codec = new DataContractJsonSerializer(typeof(Item));
            codec.WriteObject(stream, persisted); stream.Position = zero; var reloaded = (Item)codec.ReadObject(stream);
            Require(reloaded.CategoryID == persisted.CategoryID && reloaded.ID == persisted.ID && reloaded.Count == limit && reloaded.LatestDateTime == now, "Item identity/count/acquisition time did not round-trip");
        }
        LifeStoreChecks.Run(root, evidence.RootElement.GetProperty("profileVersions").EnumerateObject().Last().Value);
        LifeRuntimeChecks.Run(root);
        LifeControllerChecks.Run(root);
        LifeConnectionChecks.Run(root);
        AiConversationActionChecks.Run(root);
        LifeActivityChecks.Run(root);
        LifeAnimationChecks.Run(root);
        MomentIntegrationChecks.Run();
        var source = Path.Combine(root, "ai", "AIProject.StuffItemExtensions.cs");
        var sourceHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant();
        File.WriteAllText(Path.Combine(root, "inventory-operation-checks.json"), JsonSerializer.Serialize(new
        {
            scope = "Production inventory arithmetic vs compiled extracted AIProject source; resource dependencies stubbed; no live inventory/UI/Unity execution",
            cases, referenceSource = source, referenceSha256 = sourceHash,
            addBoundedAndUnbounded = true, nativeBooleanMeanings = true, stackIdentityAndTouchTime = true,
            partialRemovalAndShortage = true, orderedStorageRemoval = true, canAddPossibleParity = true, itemDataRoundTrip = true,
            fourOwnerPersistenceConnected = false, menuGiftMedicineQuestConnected = false
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("AI_INVENTORY_SOURCE_DIFFERENTIAL_PASS cases=" + cases + " referenceSha256=" + sourceHash);
        return default;
    }
    private static string Snapshot(IEnumerable<Item> items) => string.Join(";", items.Select(item => item.CategoryID + ":" + item.ID + ":" + item.Count + ":" + (item.LatestDateTime != default)));
    private static string Snapshot(IEnumerable<Native> items) => string.Join(";", items.Select(item => item.CategoryID + ":" + item.ID + ":" + item.Count + ":" + (item.LatestDateTime != default)));
}
