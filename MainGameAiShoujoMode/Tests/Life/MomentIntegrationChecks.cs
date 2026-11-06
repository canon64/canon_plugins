using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using MainGameAiShoujoMode;
using MainGameAiShoujoMode.Life;
using MainGameAiShoujoMode.Conversation;

internal static class MomentIntegrationChecks
{
    private static void Check(bool result, string name)
    { if (!result) throw new Exception(name); Console.WriteLine("PASS " + name); }
    internal static void Run()
    {
        var canonical = "F:/kks/work/tools/KksMapSandbox/parameters.json";
        var directory = Path.Combine(AppContext.BaseDirectory, "moment-integration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var settings = AiLifeRuntimeSettings.Load(canonical); var one = Math.Sign(settings.schema);
            settings.savePath = Path.Combine(directory, "original-life.json");
            var life = new AiLifeRuntime(settings, () => DateTime.Now, null);
            var conversationPath = Path.Combine(directory, "original-conversation.json");
            var conversation = new KksConversationStateStore(conversationPath, settings.schema);
            var resident = conversation.GetOrCreate("pod:checkpoint", () => new KksConversationStateStore.Resident { temperament = Array.Empty<byte>(), favor = one, friend = true });
            life.BindResident(resident.id); life.Store.EnsureResident(life.Rules.Create(resident.id, resident.origin));
            var current = life.Store.Read();
            life.Store.Commit(current, new AiLifeStateStore.Request { id = "checkpoint-item", action = "acquire", destination = life.PlayerInventoryKey,
                count = one, category = one, item = one }, out _, out _);
            var beforeLife = File.ReadAllBytes(settings.savePath); var beforeConversation = File.ReadAllBytes(conversationPath);
            using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "checkpoint.json"));
            var save = (AiMomentSave)new DataContractJsonSerializer(typeof(AiMomentSave)).ReadObject(stream);
            save.savedUtc = DateTimeOffset.UtcNow.ToString("O"); save.girl.identity = resident.origin;
            var snapshotLife = life.Store.Read(); var person = snapshotLife.residents.Single();
            person.hasPosition = true; person.x = save.girl.pose.x; person.y = save.girl.pose.y; person.z = save.girl.pose.z;
            save.life = Encoding.UTF8.GetString(AiMomentSaveStore.Encode(snapshotLife)); save.conversation = conversation.ExportMoment();
            var store = new AiMomentSaveStore(Path.Combine(directory, "moment-save.json")); store.Write(save); store.PrepareWorkingCopy(store.Read());
            var restoredSettings = AiLifeRuntimeSettings.Load(canonical); restoredSettings.savePath = store.WorkingLife;
            var restoredLife = new AiLifeRuntime(restoredSettings, () => DateTime.Now, null);
            var restoredConversation = new KksConversationStateStore(store.WorkingConversation, settings.schema);
            var restoredResident = restoredConversation.GetOrCreate(resident.origin, () => throw new Exception("lost saved identity"));
            Check(restoredResident.id == resident.id && restoredResident.favor == one && restoredResident.friend,
                "production conversation restore preserves identity and relationship");
            var restored = restoredLife.Store.Read();
            Check(restored.residents.Single().x == save.girl.pose.x && restored.residents.Single().hasPosition
                && restored.inventories.Single(i => i.Key == restoredLife.PlayerInventoryKey).items.Single().Count == one,
                "production life serializer restores position, inventories and item operations");
            var controller = new AiLifeController(restoredLife, resident.id, resident.origin); controller.Execute("follow");
            store.PrepareWorkingCopy(store.Read());
            var replay = new AiLifeRuntime(restoredSettings, () => DateTime.Now, null);
            Check(replay.Store.Read().residents.Single().action != "follow", "later life autosaves do not move the manual checkpoint");
            Check(beforeLife.SequenceEqual(File.ReadAllBytes(settings.savePath)) && beforeConversation.SequenceEqual(File.ReadAllBytes(conversationPath)),
                "real legacy life and conversation files remain byte-identical");
        }
        finally { Directory.Delete(directory, true); }
    }
}
