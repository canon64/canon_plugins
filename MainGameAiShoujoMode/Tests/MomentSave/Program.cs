using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.Serialization.Json;
using MainGameAiShoujoMode;

internal static class Program
{
    private static void Check(bool result, string message)
    { if (!result) throw new Exception(message); Console.WriteLine("PASS " + message); }
    private static AiMomentSave Create(string life)
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "checkpoint.json"));
        var save = (AiMomentSave)new DataContractJsonSerializer(typeof(AiMomentSave)).ReadObject(stream);
        save.savedUtc = DateTimeOffset.UtcNow.ToString("O"); save.life = life; return save;
    }
    private static void Main()
    {
        MenuChecks.Run();
        var folder = Path.Combine(AppContext.BaseDirectory, "save-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "moment-save.json"); var store = new AiMomentSaveStore(path);
            Check(store.Read() == null, "no checkpoint preserves legacy startup");
            var save = Create("{\"inventory\":[1,2],\"resident\":\"snapshot\"}"); store.Write(save);
            var firstBytes = File.ReadAllBytes(path); var loaded = store.Read();
            Check(loaded.player.x == save.player.x && loaded.view.x == save.view.x && loaded.crouching && loaded.pickup.x == save.pickup.x, "player/view/stance/pickup round trip");
            Check(loaded.girl.identity == "pod:3:stable" && loaded.girl.source == "deleted-source.png" && loaded.girl.animationTime == save.girl.animationTime
                && loaded.girl.card == save.girl.card && loaded.playerCard == save.playerCard, "embedded cards, stable identity and animation survive without source cards");
            Check(loaded.doors.Single().open && loaded.doors.Single().right && loaded.parameters == save.parameters && loaded.conversation == save.conversation, "clock/environment/doors/conversation captured together");
            var legacyPath = Path.Combine(folder, "life-state.json"); File.WriteAllText(legacyPath, "legacy-state-must-survive");
            store.PrepareWorkingCopy(loaded);
            Check(File.ReadAllText(store.WorkingLife) == save.life && File.ReadAllText(store.WorkingConversation) == save.conversation
                && File.ReadAllText(legacyPath) == "legacy-state-must-survive", "resume uses isolated copies and preserves legacy saves");
            File.WriteAllText(store.WorkingLife, "progress after checkpoint");
            Check(store.Read().life == save.life, "subsequent autosaves do not alter the manual checkpoint");
            store.PrepareWorkingCopy(store.Read());
            Check(File.ReadAllText(store.WorkingLife) == save.life, "next resume returns to the saved instant");
            var invalid = Create("invalid"); invalid.player.x = float.NaN;
            bool failed = false; try { store.Write(invalid); } catch (InvalidDataException) { failed = true; }
            Check(failed && firstBytes.SequenceEqual(File.ReadAllBytes(path)), "invalid capture cannot overwrite valid checkpoint");
            invalid = Create("invalid"); invalid.doors = new[] { save.doors.First(), save.doors.First() };
            failed = false; try { store.Write(invalid); } catch (InvalidDataException) { failed = true; }
            Check(failed && firstBytes.SequenceEqual(File.ReadAllBytes(path)), "duplicate door identities rejected before publish");
            var next = Create("new checkpoint"); store.Write(next);
            Check(File.ReadAllBytes(path + ".previous").SequenceEqual(firstBytes) && store.Read().life == "new checkpoint", "atomic replacement keeps one complete previous generation");
            var text = File.ReadAllText(path); var digest = text.IndexOf("sha256\":\"", StringComparison.Ordinal) + "sha256\":\"".Length;
            File.WriteAllText(path, text.Substring(default(int), digest) + (text[digest] == 'A' ? "B" : "A") + text.Substring(digest + Convert.ToInt32(true)));
            failed = false; try { store.Read(); } catch (InvalidDataException) { failed = true; }
            Check(failed && File.ReadAllText(legacyPath) == "legacy-state-must-survive", "checksum corruption fails closed without touching legacy state");
            Check(!Directory.GetFiles(folder, "*.pending-*").Any(), "no pending files remain after publication");
            var furniturePath=Path.Combine(folder,"furniture-checkpoint.json");var furnitureStore=new AiMomentSaveStore(furniturePath);
            var furnitureSave=Create("furniture life");
            Check(furnitureSave.furniture==null,"legacy checkpoint has no furniture override");
            furnitureSave.furniture=new MainGameAiShoujoMode.Furniture.FurnitureLayout();furnitureStore.Write(furnitureSave);
            Check(furnitureStore.Read().furniture.items.Length==default(int),"explicit empty furniture overrides existing island layout");
            var instance=new MainGameAiShoujoMode.Furniture.FurnitureInstance{id=Guid.NewGuid().ToString("N"),position=new MainGameAiShoujoMode.Furniture.FurnitureVector(),rotation=new MainGameAiShoujoMode.Furniture.FurnitureVector()};
            furnitureSave.furniture.items=new[]{instance};furnitureStore.Write(furnitureSave);
            Check(furnitureStore.Read().furniture.items.Single().id==instance.id,"checkpoint captures furniture identity and transform");
        }
        finally { Directory.Delete(folder, true); }
    }
}
