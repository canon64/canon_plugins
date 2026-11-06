using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoH.Presentation;
using MainGameAiShoujoH.Progression;

class Program
{
    static int checks;
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    static void Main(string[] args)
    {
        int calls = 0; string requested = null;
        string[] choices = { "aibu:0", "houshi:2" };
        Func<string, bool> apply = id => { calls++; requested = id; return true; };
        Check(MotionSelectionAction.Choose("houshi:2", "aibu:0", choices, apply) && calls == 1 && requested == "houshi:2", "A row click must request a real switch once");
        Check(!MotionSelectionAction.Choose("aibu:0", "aibu:0", choices, apply) && calls == 1, "Current row does not reload");
        bool rejected = false; try { MotionSelectionAction.Choose("sonyu:0", "aibu:0", choices, apply); } catch (ArgumentException) { rejected = true; }
        Check(rejected && calls == 1, "Hidden or unready rows cannot dispatch");
        Check(!MotionSelectionAction.Choose("houshi:2", "aibu:0", choices, id => false), "Rejected request remains rejected");
        using var catalog = JsonDocument.Parse(File.ReadAllText(args[0]));
        var context = new NativeMotionSelectionContext { PlayerSex = 0, Place = 0, Initiative = 0, HasItem = (mode, id) => false };
        var ready = catalog.RootElement.GetProperty("motions").EnumerateArray().Where(row => row.GetProperty("packedVerified").GetBoolean() && row.GetProperty("constraintsVerified").GetBoolean() && row.GetProperty("nativeGraphVerified").GetBoolean() && row.GetProperty("issues").GetArrayLength() == 0).Select(row => row.GetProperty("id").GetString()).ToArray();
        var shown = ready.Where(id => NativeMotionEligibility.Check(NativeProcCatalog.Get(id), context, false)).ToArray();
        Console.WriteLine("SOURCE_LINK_SELECTION_CHECKS_PASS " + checks);
        Console.WriteLine("DEFAULT_MALE_PLACE0_READY " + ready.Length + " ELIGIBLE " + shown.Length + " " + string.Join(",", shown));
        foreach (var id in ready.Except(shown))
        {
            var definition = NativeProcCatalog.Get(id);
            Console.WriteLine("FILTERED " + id + " place=" + string.Join(",", definition.Positions) + " unlock=" + definition.Hentai + " initiative=" + definition.FemaleInitiative + " item=" + definition.NeedItem);
        }
    }
}
