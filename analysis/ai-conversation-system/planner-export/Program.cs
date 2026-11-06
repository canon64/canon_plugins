using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoMode.Conversation;

Console.WriteLine("PLANNER_LAYOUT_EXPORT_START productionParser=true");
var root = Path.GetFullPath(args[0]);
var options = new JsonSerializerOptions { IncludeFields = true };
using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "runtime-manifest.json")));
var numbers = JsonSerializer.Deserialize<KksConversationLayout.LayoutNumber[]>(manifest.RootElement.GetProperty("layoutNumbers").GetRawText(), options);
var bundles = new Dictionary<string, JsonDocument>();
var output = new List<object>();
foreach (var profile in manifest.RootElement.GetProperty("personalities").EnumerateArray())
{
    var source = profile.GetProperty("conversation");
    var bundle = source.GetProperty("bundle").GetString(); var assetName = source.GetProperty("asset").GetString();
    var filename = bundle.Replace('/', '_') + ".json";
    if (!bundles.TryGetValue(filename, out var data)) bundles.Add(filename, data = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "bundles", filename))));
    var asset = data.RootElement.EnumerateArray().Single(value => value.GetProperty("name").GetString() == assetName);
    var rows = JsonSerializer.Deserialize<List<KksConversationLayout.Row>>(asset.GetProperty("data").GetProperty("param").GetRawText(), options);
    var layout = KksConversationLayout.Read(rows, numbers, source.GetProperty("className").GetString() == "CommunicationNPCData");
    var personality = profile.GetProperty("personality").GetInt32();
    foreach (var stage in layout.Groups)
    foreach (var group in stage.Value)
    foreach (var category in group.Value)
    foreach (var block in category.Value)
    {
        var parts = new List<object>();
        void Add(string role, KksConversationLayout.Row row)
        {
            if (row == null) return;
            parts.Add(new { role, row = rows.IndexOf(row), row.text, bundle = row.assetbundle, asset = row.file, row.pose, expression = row.facialExpression, row.favor, row.lewdness, row.topic });
        }
        if (block is KksConversationLayout.GenericInfo generic) foreach (var row in generic.talk) Add("本文", row);
        if (block is KksConversationLayout.BranchInfo branch)
        {
            Add("導入", branch.introduction); foreach (var row in branch.success) Add("成功候補", row);
            Add("失敗", branch.failure); Add("興味なし", branch.noInterested);
        }
        if (block is KksConversationLayout.SelectInfo select)
        {
            Add("導入", select.introduction); foreach (var row in select.choice) Add("選択先", row); Add("失敗", select.failure);
        }
        output.Add(new { id = $"kks:{personality}:{stage.Key}:{group.Key}:{category.Key}:{block.row}:{output.Count}", personality,
            stage = stage.Key, group = group.Key.ToString(), category = category.Key, sourceBundle = bundle, sourceAsset = assetName,
            sourceRow = block.row, block.priority, block.conditions, kind = block.GetType().Name, parts });
    }
}
File.WriteAllText(args[1], JsonSerializer.Serialize(output, options));
Console.WriteLine("PLANNER_LAYOUT_EXPORT_PASS profiles=" + manifest.RootElement.GetProperty("personalities").GetArrayLength() + " blocks=" + output.Count);
