using System;
using System.IO;
using System.Linq;
using MainGameAiShoujoMode.Life;

internal static class LifeConnectionChecks
{
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    internal static void Run(string root)
    {
        Console.WriteLine("AI_LIFE_CONNECTION_CHECK_START productionDispatcherAndSave=true");
        var content = Path.GetFullPath(Path.Combine(root, "../../MainGameAiShoujoMode/Package/Content"));
        var settings = AiLifeRuntimeSettings.LoadContent(content);
        var folder = Path.Combine(root, "connection-checks", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        settings.savePath = Path.Combine(folder, "world.json");
        var runtime = new AiLifeRuntime(settings, () => DateTime.Now, null); runtime.LoadItems(content); runtime.LoadConnections(content);
        var id = Guid.NewGuid().ToString("N"); var life = new AiLifeController(runtime, id, "connection-test");
        var catalog = runtime.ConnectionCatalog;
        var mood = catalog.entries.Single(value => value.key == "trial:mood");
        var operation = Guid.NewGuid().ToString("N"); var before = life.State.mood;
        Require(life.Connections.Execute(mood, operation).Completed, "Parameter request was not completed");
        Require(life.State.mood == Math.Min(life.Rules.maximum, before + mood.amount), "Wrong parameter delta");
        var revision = runtime.Store.Read().revision;
        Require(life.Connections.Execute(mood, operation).Replayed && runtime.Store.Read().revision == revision, "Parameter replay changed state");
        try { life.Connections.Execute(catalog.entries.First(value => value.key != mood.key && value.kind == "parameter"), operation); throw new Exception("Conflicting intent accepted"); }
        catch (InvalidOperationException) { }
        var pending = catalog.entries.Single(value => value.key == "quest");
        Require(!life.Connections.Execute(pending, Guid.NewGuid().ToString("N")).Completed && runtime.Store.Read().revision == revision, "Placeholder changed state or claimed completion");
        var pickup = catalog.entries.Single(value => value.kind == "pickup");
        life.Connections.Execute(pickup, Guid.NewGuid().ToString("N"));
        Require(life.Connections.Execute(pickup, Guid.NewGuid().ToString("N")).Replayed, "Pickup can be collected repeatedly");
        var transfer = catalog.entries.Single(value => value.kind == "transfer");
        life.Connections.Execute(transfer, Guid.NewGuid().ToString("N"));
        Require(runtime.Store.Read().inventories.Single(value => value.Key == "agent:" + id).items.Single().Count == transfer.count, "Transfer did not reach resident");
        revision = runtime.Store.Read().revision;
        try { life.Connections.Execute(transfer, Guid.NewGuid().ToString("N")); throw new Exception("Shortage accepted"); } catch (InvalidOperationException) { }
        Require(runtime.Store.Read().revision == revision, "Shortage partially changed state");
        var outfit = new AiLifeConnection { key = "outfit:give", kind = "outfit", resource = "test-coordinate-reference.png" };
        life.Connections.Execute(outfit, Guid.NewGuid().ToString("N")); life.Connections.Execute(outfit, Guid.NewGuid().ToString("N"));
        Require(life.State.outfits.Count == Math.Sign(settings.schema), "Duplicate outfit references");
        life.Connections.Execute(catalog.entries.Single(value => value.key == "work"), Guid.NewGuid().ToString("N"));
        var reopenRuntime = new AiLifeRuntime(settings, () => DateTime.Now, null); reopenRuntime.LoadItems(content);
        var reopened = new AiLifeController(reopenRuntime, id, "connection-test");
        Require(reopened.State.action == "work" && reopened.State.outfits.Single() == outfit.resource, "Reload lost action or wardrobe");
        Require(reopened.Connections.Execute(pickup, Guid.NewGuid().ToString("N")).Replayed, "Reload respawned pickup");
        var independentPickup = AiLifeStateStore.Copy(pickup); independentPickup.key = "test:without-resident";
        Require(reopenRuntime.Pickup(independentPickup).Completed && reopenRuntime.Pickup(independentPickup).Replayed, "World pickup needs a bound resident or duplicates stock");
        var upper = new AiLifeConnection { key = "test:clamp", kind = "parameter", parameter = "mood", amount = life.Rules.maximum };
        life.Connections.Execute(upper, Guid.NewGuid().ToString("N")); Require(life.State.mood == life.Rules.maximum, "Parameter exceeded maximum");
        upper.amount = -life.Rules.maximum - life.Rules.maximum;
        life.Connections.Execute(upper, Guid.NewGuid().ToString("N")); Require(life.State.mood == life.Rules.minimum, "Parameter dropped below minimum");
        revision = runtime.Store.Read().revision; upper.amount = float.NaN;
        try { life.Connections.Execute(upper, Guid.NewGuid().ToString("N")); throw new Exception("NaN accepted"); } catch (ArgumentException) { }
        Require(runtime.Store.Read().revision == revision, "Rejected parameter published a change");
        life.BeginH();
        try { life.Connections.Execute(mood, Guid.NewGuid().ToString("N")); throw new Exception("H request accepted"); } catch (InvalidOperationException) { }
        life.EndH();
        life.Connections.Register("quest", (request, token) => new AiLifeConnectionResult { Message = "adapter called" });
        Require(life.Connections.Execute(pending, Guid.NewGuid().ToString("N")).Message == "adapter called", "Adapter not reached");
        life.Connections.Dispose();
        try { life.Connections.Execute(mood, Guid.NewGuid().ToString("N")); throw new Exception("Disposed request accepted"); } catch (ObjectDisposedException) { }
        Console.WriteLine("AI_LIFE_CONNECTION_CHECK_PASS parameter replay conflictingIntent pendingNoEffect pickupOnce transfer shortage wardrobe reload adapter H disposed");
    }
}
