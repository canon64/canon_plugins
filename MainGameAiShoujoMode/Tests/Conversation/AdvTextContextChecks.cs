using System;
using System.IO;
using System.Text.Json;
using MainGameAiShoujoMode.Conversation;

internal static class AdvTextContextChecks
{
    internal static void Run(string directory)
    {
        Console.WriteLine("Start separate native ADV variable/text/name replacement checks");
        var selected = "H名";
        var context = new KksAdvTextContext(new[] { "H名", "P名" }, key => key == "H" ? selected : null);
        context.Variables["amount"] = () => "quantity";
        context.Replacements["H名"] = () => "Resident [literal]";
        context.Replacements["H"] = () => "default-name";
        context.Replacements["P名"] = () => "Player";
        context.Replacements["intro"] = () => "[P名]→[H名]";
        Require(context.ReplaceArgument("amount") == "quantity" && context.ReplaceArgument("[amount]") == "[amount]"
            && context.ReplaceArgument("[H名]") == "[H名]", "Argument lookup rewrote embedded text/color tags");
        Require(context.ReplaceText("[intro] [unknown]", false) == "Player→Resident [literal] [unknown]", "Recursive replace/name literal/unknown tag handling differs");
        Require(context.ReplaceText("[H]", false) == "default-name" && context.ReplaceText("[H]", true) == "Resident [literal]", "Once-name selection was applied to caption names or ignored in body");
        selected = "missing-name";
        Require(context.ReplaceText("[H]", true) == "[H]", "Absent once-name did not preserve original token");
        context.SetFile("first-bundle", "first-asset");
        Require(context.ReplaceArgument("BundleFile") == "first-bundle" && context.ReplaceArgument("AssetFile") == "first-asset", "Initial file variables were not available");
        context.SetFile("second-bundle", "second-asset");
        Require(context.ReplaceArgument("AssetFile") == "second-asset", "Open did not update file context");
        context.Replacements["cycle"] = () => "[cycle]";
        try { context.ReplaceText("[cycle]", false); throw new Exception("Recursive malformed replacement accepted"); } catch (InvalidOperationException) { }
        File.WriteAllText(Path.Combine(directory, "adv-text-context-checks.json"), JsonSerializer.Serialize(new
        {
            scope = "Production detached variable/text/name conversion with explicit values; no game/native parameter getters",
            wholeArgumentLookup = true, colorTagsPreserved = true, recursiveReplacements = true, namesRemainLiteral = true,
            onceNameSelection = true, unknownTagsPreserved = true, fileVariablesUpdated = true, cyclesRejected = true
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS ADV arguments separate from text/color tags; recursive once-name and file context");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
