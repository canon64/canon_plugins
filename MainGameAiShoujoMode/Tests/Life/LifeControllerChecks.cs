using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoMode.Life;

internal static class LifeControllerChecks
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    internal static void Run(string root)
    {
        Console.WriteLine("AI_LIFE_CONTROLLER_CHECK_START productionStateAndTransactions=true");
        var settings = AiLifeRuntimeSettings.Load("F:/kks/work/tools/KksMapSandbox/parameters.json");
        var directory = Path.Combine(root, "life-controller-checks", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        settings.savePath = Path.Combine(directory, "world.json");
        var runtime = new AiLifeRuntime(settings, () => DateTime.Now, _ => { }); runtime.LoadItems(root);
        var id = Guid.NewGuid().ToString("N"); var life = new AiLifeController(runtime, id, "controller-check:resident");
        var rules = life.Rules;
        Require(life.State.health == rules.health && life.State.mood == rules.mood && life.State.fullness == rules.fullness
            && life.State.sickness == rules.healthySickness && life.State.flavor.Length == rules.flavorCount, "Native initial state missing");
        life.BeginConversation(); life.Execute("follow");
        Require(life.DesiredAction == "conversation" && life.State.action == "follow", "Conversation did not own presentation while preserving follow intent");
        try { life.BeginConversation(); throw new Exception("Double conversation accepted"); } catch (InvalidOperationException) { }
        life.EndConversation(); Require(life.DesiredAction == "follow", "Conversation restored old action instead of committed follow");
        var reopened = new AiLifeController(new AiLifeRuntime(settings, () => DateTime.Now, _ => { }), id, "controller-check:resident");
        Require(reopened.DesiredAction == "follow" && reopened.State.following == "player" && !reopened.Conversing, "Save/reload lost follow or restored transient conversation");
        reopened.BeginConversation(); reopened.BeginH(); Require(reopened.DesiredAction == "h", "H handoff did not own actor");
        reopened.EndH(); Require(reopened.DesiredAction == "follow", "H failed to restore saved intent");
        life.Execute("test:cold"); Require(life.DesiredAction == "treatment", "Sickness did not override normal navigation");
        life.Advance(rules.restSeconds); Require(life.State.sicknessSeconds == rules.restSeconds, "Clock event did not advance sickness");
        var stock = new AiLifeStateStore.Request { id = Guid.NewGuid().ToString("N"), action = "acquire", destination = runtime.PlayerInventoryKey,
            category = rules.coldMedicineCategory, item = rules.coldMedicineItem, count = settings.schema };
        runtime.Store.Commit(runtime.Store.Read(), stock, out _, out _);
        var confirmation = runtime.Store.Read(); var operation = Guid.NewGuid().ToString("N");
        life.Give(confirmation, operation, stock.category, stock.item, stock.count, true);
        Require(life.State.sickness == rules.healthySickness && life.State.coldLocked && !runtime.Store.Read().inventories.Single(value => value.Key == runtime.PlayerInventoryKey).items.Any(), "Medicine stock/status were not committed together");
        var settled = runtime.Store.Read().revision;
        life.Give(confirmation, operation, stock.category, stock.item, stock.count, true);
        Require(runtime.Store.Read().revision == settled, "Medicine replay published or consumed twice");
        life.Execute("test:cold"); var shortage = runtime.Store.Read();
        try { life.Give(shortage, Guid.NewGuid().ToString("N"), stock.category, stock.item, stock.count, true); throw new Exception("Medicine shortage accepted"); } catch (InvalidOperationException) { }
        Require(life.State.sickness == rules.coldSickness && runtime.Store.Read().revision == shortage.revision, "Failed medicine published health without inventory");
        life.Execute("test:healthy"); life.Execute("test:hungry"); Require(life.DesiredAction == "eat", "Hunger did not affect action selection");
        life.Execute("test:healthy"); life.Execute("test:work"); Require(life.DesiredAction == "work", "Work intent missing");
        life.Block("missing work location"); Require(life.State.blockedReason == "missing work location", "Unavailable work was silently treated as complete");
        var unchanged = runtime.Store.Read().revision; life.Block("missing work location"); Require(runtime.Store.Read().revision == unchanged, "Repeated block status churned save");
        var stale = runtime.Store.Read(); life.Execute("normal");
        try { runtime.Store.Commit(stale, new AiLifeStateStore.Request { id = Guid.NewGuid().ToString("N"), action = "state", resident = stale.residents.Single() }, out _, out _); throw new Exception("Stale state write accepted"); } catch (InvalidOperationException) { }
        var food = runtime.Items.items.First(value => !value.system && value.food && runtime.Items.CanPresent(value.category, value.id));
        stock.id = Guid.NewGuid().ToString("N"); stock.category = food.category; stock.item = food.id;
        runtime.Store.Commit(runtime.Store.Read(), stock, out _, out _);
        life.Give(runtime.Store.Read(), Guid.NewGuid().ToString("N"), food.category, food.id, stock.count, false);
        Require(runtime.Store.Read().inventories.Single(value => value.Key == "agent:" + id).items.Any(value => value.CategoryID == food.category && value.ID == food.id && value.Count == stock.count), "Native food gift did not transfer to NPC inventory");
        var restored = new AiLifeController(runtime, id, "controller-check:resident");
        Require(restored.DesiredAction == "normal" && restored.State.fullness == rules.fullness, "Reentry reset or corrupted status");
        restored.SavePosition(rules.temperature, rules.minimum, rules.fullness, rules.mood);
        var positionReload = new AiLifeController(runtime, id, "controller-check:resident");
        Require(positionReload.State.hasPosition && positionReload.State.x == rules.temperature && positionReload.State.yaw == rules.mood, "Position was not persisted on an event");
        var book = runtime.Items.items.First(value => !value.system && !value.food && !value.drink && value.giftFlavor != null
            && value.giftFlavor.amount > default(int) && runtime.Items.CanPresent(value.category, value.id));
        stock.id = Guid.NewGuid().ToString("N"); stock.category = book.category; stock.item = book.id;
        runtime.Store.Commit(runtime.Store.Read(), stock, out _, out _);
        var giftConfirmation = runtime.Store.Read(); var giftId = Guid.NewGuid().ToString("N");
        life.Give(giftConfirmation, giftId, book.category, book.id, stock.count, false);
        Require(life.State.flavor[book.giftFlavor.flavor] == book.giftFlavor.amount && life.State.totalFlavor == book.giftFlavor.amount
            && life.State.flavorAdditionAmount == book.giftFlavor.amount && runtime.Store.Read().totalAgentFlavorAdditionAmount == book.giftFlavor.amount, "Gift growth did not update individual and world counters together");
        life.Give(giftConfirmation, giftId, book.category, book.id, stock.count, false);
        Require(life.State.totalFlavor == book.giftFlavor.amount, "Gift retry applied growth twice");
        File.WriteAllText(Path.Combine(root, "life-controller-checks.json"), JsonSerializer.Serialize(new
        {
            scope = "Production life controller, catalog and atomic save on isolated files; Unity navigation, menus, audio and live KKS are not executed",
            initialState = true, followPersisted = true, transientOwnership = true, hReturn = true,
            sicknessAndHungerSelection = true, medicineAtomic = true, medicineReplay = true, shortageRollsBackBoth = true,
            foodGiftTransferred = true, blockedWorkExplicit = true, staleWriteRejected = true, runtimeVerified = false
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("AI_LIFE_CONTROLLER_CHECK_PASS state follow reload conversation H medicineAtomic replay shortage foodGift workBlocked staleWrite");
    }
}
