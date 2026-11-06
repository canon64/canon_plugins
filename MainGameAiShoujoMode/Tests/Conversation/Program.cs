using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoMode.Conversation;

internal static class Program
{
    private static int Main(string[] args)
    {
        var catalogPath = args.Single();
        CharacterVoiceChecks.Run(catalogPath);
        Console.WriteLine("Start KKS native table-layout checks against extracted installed assets");
        int tables = default, blocks = default, branches = default, choices = default, referencedRows = default;
        var results = new List<object>();
        var options = new JsonSerializerOptions { IncludeFields = true };
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(catalogPath, "runtime-manifest.json")));
        var numbers = JsonSerializer.Deserialize<KksConversationLayout.LayoutNumber[]>(manifest.RootElement.GetProperty("layoutNumbers").GetRawText(), options);
        var rules = JsonSerializer.Deserialize<KksNormalConversationRules.Settings>(manifest.RootElement.GetProperty("normalRules").GetRawText(), options);
        var dialogue = new DialogueChecks(rules);
        using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "layout-expectations.json")));
        int Expected(string name) => expected.RootElement.GetProperty(name).GetInt32();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(catalogPath, "bundles"), "communication_info_*.json").OrderBy(x => x))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            foreach (var asset in document.RootElement.EnumerateArray())
            {
                var assetClass = asset.GetProperty("class").GetString();
                if (assetClass != "CommunicationInfo" && assetClass != "CommunicationNPCData") continue;
                bool npc = assetClass == "CommunicationNPCData";
                var rows = JsonSerializer.Deserialize<List<KksConversationLayout.Row>>(asset.GetProperty("data").GetProperty("param").GetRawText(), options);
                var layout = KksConversationLayout.Read(rows, numbers, npc);
                if (!npc) dialogue.CheckSelector(layout);
                if (!npc)
                {
                var first = layout.Groups[Expected("stage")][KksConversationLayout.Group.Talk][Expected("command")];
                var branch0 = (KksConversationLayout.BranchInfo)first[Expected("firstBranch")];
                var branch1 = (KksConversationLayout.BranchInfo)first[Expected("secondBranch")];
                Require(ReferenceEquals(branch0.introduction, rows[Expected("introductionRow")]), "First talk introduction shifted");
                Require(ReferenceEquals(branch0.success[Expected("firstBranch")], rows[Expected("firstSuccessRow")]) && ReferenceEquals(branch0.success[Expected("secondBranch")], rows[Expected("secondSuccessRow")]), "Success alternatives lost");
                Require(ReferenceEquals(branch0.failure, rows[Expected("sharedFailureRow")]) && ReferenceEquals(branch1.failure, branch0.failure), "Shared forward failure reference lost");
                Require(layout.Groups[Expected("stage")][KksConversationLayout.Group.Introduction][Expected("command")].Count == Expected("introductionCountAfterAppend"), "Stage 0 late introduction override missing");
                }
                var validRows = rows.ToHashSet(ReferenceEqualityComparer.Instance);
                var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
                int tableBlocks = default;
                foreach (var block in layout.Groups.Values.SelectMany(group => group.Values).SelectMany(commands => commands.Values).SelectMany(value => value))
                {
                    Require(block.row >= default(int) && block.row < rows.Count, "Block source row out of range");
                    dialogue.Check(block);
                    foreach (var row in References(block).Where(row => row != null))
                    {
                        Require(validRows.Contains(row), "Response does not refer to its own source table");
                        seen.Add(row);
                    }
                    if (block is KksConversationLayout.BranchInfo) branches++;
                    if (block is KksConversationLayout.SelectInfo) choices++;
                    blocks++; tableBlocks++;
                }
                referencedRows += seen.Count;
                tables++;
                results.Add(new { file = Path.GetFileName(file), name = asset.GetProperty("name").GetString(), rows = rows.Count, blocks = tableBlocks, referencedRows = seen.Count,
                    referencedRowIndices = Enumerable.Range(default(int), rows.Count).Where(index => seen.Contains(rows[index])).ToArray() });
            }
        }
        Require(tables > default(int) && branches > default(int) && choices > default(int), "No substantive fixtures exercised");
        try { KksConversationLayout.Read(new List<KksConversationLayout.Row>(), numbers); throw new Exception("Truncated table was accepted"); }
        catch (FormatException) { }
        dialogue.CheckLifecycle();
        dialogue.Report(catalogPath);
        StateStoreChecks.Run(catalogPath, manifest.RootElement.GetProperty("runtime").GetProperty("schema").GetInt32(), rules);
        AdvPreparationChecks.Run(catalogPath, rules.zero, rules.one);
        AdvOwnedSessionChecks.Run(catalogPath, rules.zero, rules.one);
        AdvTextContextChecks.Run(catalogPath);
        PointDisplayChecks.Run(catalogPath, rules.zero, rules.one);
        PointStoreChecks.Run(catalogPath, manifest.RootElement.GetProperty("runtime").GetProperty("schema").GetInt32(), rules.zero, rules.one,
            manifest.RootElement.GetProperty("runtime").GetProperty("koikatsuPointMax").GetInt32());
        AdvTapeChecks.Run(catalogPath, rules);
        AdvMachineChecks.Run(catalogPath, rules);
        AdvTextPlaybackChecks.Run(catalogPath, rules);
        VoiceGroupChecks.Run(catalogPath, rules);
        AdvClockChecks.Run(catalogPath, rules);
        AdvActorTableChecks.Run(catalogPath, rules.zero, rules.one,
            manifest.RootElement.GetProperty("runtime").GetProperty("advPlayerNumber").GetInt32(),
            manifest.RootElement.GetProperty("runtime").GetProperty("advHeroineNumberOffset").GetInt32());
        File.WriteAllText(Path.Combine(catalogPath, "layout-checks.json"), JsonSerializer.Serialize(new { scope = "Pure layout checks; no game/UI test", tables, blocks, branches, choices, referencedRows, results }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"PASS tables={tables} blocks={blocks} branches={branches} choiceBlocks={choices} distinctReferencedRowsAcrossTables={referencedRows}");
        return default;
    }

    private static IEnumerable<KksConversationLayout.Row> References(KksConversationLayout.BasicInfo block)
    {
        if (block is KksConversationLayout.GenericInfo generic) return generic.talk;
        if (block is KksConversationLayout.BranchInfo branch) return new[] { branch.introduction, branch.failure, branch.noInterested }.Concat(branch.success);
        if (block is KksConversationLayout.SelectInfo select) return new[] { select.introduction, select.failure }.Concat(select.choice);
        throw new InvalidOperationException("Unknown block type");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
