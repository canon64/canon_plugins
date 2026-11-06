using System;
using System.IO;
using System.Linq;
using MainGameAiShoujoMode.Conversation;
using MainGameAiShoujoMode.Life;

internal static class AiConversationActionChecks
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    internal static void Run(string root)
    {
        var content = Path.GetFullPath(Path.Combine(root, "../../MainGameAiShoujoMode/Package/Content"));
        var settings = AiLifeRuntimeSettings.LoadContent(content);
        var directory = Path.Combine(root, "ai-conversation-actions", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); settings.savePath = Path.Combine(directory, "state.json");
        var runtime = new AiLifeRuntime(settings, () => DateTime.Now, null); runtime.LoadItems(content); runtime.LoadConnections(content);
        var life = new AiLifeController(runtime, Guid.NewGuid().ToString("N"), "ai-action-test");
        var one = Math.Sign(settings.schema); var item = runtime.Items.items.First(value => !value.system);
        runtime.Store.Commit(runtime.Store.Read(), new AiLifeStateStore.Request { id = Guid.NewGuid().ToString("N"), action = "acquire",
            destination = runtime.PlayerInventoryKey, category = item.category, item = item.id, count = one }, out _, out _);
        var receive = new AiReplyAction { Action = "receive_item", Inventory = runtime.PlayerInventoryKey, Category = item.category, Item = item.id,
            Target = "item:" + runtime.PlayerInventoryKey + ":" + item.category + ":" + item.id };
        var startedH = false; var closed = false;
        string Execute(AiReplyAction action, string operation = null) => AiConversationActionExecution.Execute(action,
            operation ?? Guid.NewGuid().ToString("N"), life, runtime, one, () => startedH = true, () => { closed = true; life.EndConversation(); });
        void Reject(AiReplyAction action) { try { Execute(action); } catch (InvalidOperationException) { return; } throw new Exception("Invalid AI action executed"); }
        Reject(receive);
        life.BeginConversation();
        var operation = Guid.NewGuid().ToString("N"); Execute(receive, operation);
        var world = runtime.Store.Read();
        Require(!world.inventories.Single(value => value.Key == runtime.PlayerInventoryKey).items.Any(), "Player still owns transferred item");
        Require(world.inventories.Single(value => value.Key == "agent:" + life.ResidentId).items.Single().Count == one, "Resident did not receive item");
        Require(life.Conversing && !closed, "Item transfer unnecessarily closed AI conversation");
        Execute(receive, operation);
        Require(runtime.Store.Read().revision == world.revision, "Repeated reply transferred another item");
        Reject(receive); Require(runtime.Store.Read().revision == world.revision, "Shortage changed inventory");
        receive.Target += "wrong"; Reject(receive);
        Execute(new AiReplyAction { Action = "h", Target = "player" }); Require(startedH && !closed, "H did not use existing H callback");
        startedH = false; life.Execute("test:cold");
        Reject(new AiReplyAction { Action = "h", Target = "player" });
        Reject(new AiReplyAction { Action = "follow", Target = "player" }); Require(!startedH && !closed, "Sick actor started H or follow");
        life.Execute("test:healthy"); Execute(new AiReplyAction { Action = "follow", Target = "player" });
        Require(closed && life.State.action == "follow" && life.State.following == "player", "Follow did not commit and close conversation");
        closed = false; life.BeginConversation(); Execute(new AiReplyAction { Action = "normal" });
        Require(closed && life.State.action == "normal" && life.State.following == null, "Normal did not cancel follow");
        Console.WriteLine("AI_CONVERSATION_ACTION_CHECK_PASS receive replay shortage wrongTarget HCallback sickFollow sickH follow normal");
    }
}
