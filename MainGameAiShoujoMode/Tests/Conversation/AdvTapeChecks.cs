using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoMode.Conversation;

internal static class AdvTapeChecks
{
    internal static void Run(string directory, KksNormalConversationRules.Settings numbers)
    {
        Console.WriteLine("Start native ADV tape, source provenance and splice checks");
        int zero = numbers.zero, one = numbers.one;
        KksAdvScript.Row Row(string command, params string[] args) => new KksAdvScript.Row(one, zero, true, command, args);
        var sourceArgs = new[] { "name", "caption" };
        var row = Row("Text", sourceArgs); sourceArgs[zero] = "mutated";
        Require(row.Arguments[zero] == "name", "Source argument array was retained by reference");
        var init = Row("Open", "", "override", "", "", "", "extra", "");
        var defaults = new[] { "bundle", "asset", bool.FalseString, bool.TrueString, bool.TrueString };
        var args = init.InitializeArguments(defaults);
        Require(args.SequenceEqual(new[] { "bundle", "override", bool.FalseString, bool.TrueString, bool.TrueString, "extra", "" }), "Empty/default/extra arguments changed native Initialize semantics");
        args[zero] = "mutated";
        Require(defaults[zero] == "bundle" && init.Arguments[zero] == "", "Argument initialization mutated shared state");
        Require(init.InitializeArguments(null).SequenceEqual(init.Arguments), "Null defaults did not preserve supplied arguments");

        var original = new KksAdvScript("root", "main", new[] { Row("Text", "", "start"), Row("Close") });
        var inserted = new KksAdvScript("inserted", "extra", new[] { Row("Tag", "LabelToken"), Row("Text", "", "inserted") });
        var tape = new KksAdvTape(zero, one);
        Require(tape.Load(original, true, true), "Initial load failed");
        Require(tape.Entries.First().Synthetic && tape.Entries.First().Row.Command == "Open", "Native root Open marker missing");
        Require(tape.TryRead(out var first) && first.Row.Command == "Text" && tape.CurrentLine == one, "Root Open marker executed or CurrentLine is off by one");
        Require(tape.Load(inserted, false, true), "Additive load failed");
        Require(tape.LoadBundle == original.Bundle && tape.LoadAsset == original.Asset, "Additive load changed current root context");
        var count = tape.Count;
        Require(!tape.Load(inserted, false, true) && tape.Count == count, "Duplicate checked load mutated tape");
        Require(tape.TryRead(out var tag) && tag.Bundle == inserted.Bundle && tag.SourceRow == zero, "Inserted source provenance or insertion point changed");
        Require(tape.TryRead(out var text) && text.Row.Arguments.Last() == "inserted", "Inserted command order changed");
        Require(tape.TryRead(out var close) && close.Asset == original.Asset && close.Row.Command == "Close", "Insertion lost the caller's remaining rows");
        Require(!tape.TryRead(out _), "Tape did not end");
        string Replace(string value) => value == "LabelToken" ? "target" : value;
        Require(tape.TryJump("target", tape.CurrentLine, Replace) && tape.TryRead(out var jumped) && ReferenceEquals(jumped, tag), "Tag jump did not search executed rows with whole-variable replacement");
        var cursor = tape.CurrentLine;
        Require(!tape.TryJump("missing", cursor, Replace) && tape.CurrentLine == cursor, "Missing tag moved the cursor");
        Require(tape.Load(inserted, false, false), "Unchecked repeated load was rejected");
        Require(tape.Load(original, true, true) && tape.Load(inserted, false, true), "Clear load did not reset duplicate-open history");

        // Same label in inserted rows must not override an earlier tag in the tape.
        var repeated = new KksAdvScript("root", "tags", new[] { Row("Tag", "target"), Row("Tag", "target") });
        tape.Load(repeated, true, true); tape.TryRead(out var earlier); tape.TryRead(out _);
        Require(tape.TryJump("target", tape.CurrentLine, Replace) && tape.TryRead(out var earliest) && ReferenceEquals(earlier, earliest), "Duplicate tag selected the last match");
        tape.Load(original, true, true); tape.TryRead(out _);
        tape.InsertParameters(inserted); tape.InsertParameters(inserted);
        Require(tape.Entries.Count(entry => entry.Bundle == inserted.Bundle) == inserted.Rows.Count + inserted.Rows.Count, "ParameterFile incorrectly deduplicated repeated inserts");
        Require(tape.TryRead(out var param) && param.Row.Command == "Tag", "ParameterFile inserted after the next command instead of before it");
        tape.Load(original, true, true); tape.TryRead(out _);
        Require(tape.TryJump("LabelToken:asset:True:False:False", tape.CurrentLine, Replace), "File jump failed");
        Require(tape.TryRead(out var open) && open.Synthetic && open.Row.Command == "Open"
            && open.Row.Arguments.SequenceEqual(new[] { "target", "asset", "True", "False", "False" }), "File jump did not insert a variable-resolved Open at localLine+1");
        Require(tape.TryRead(out var afterOpen) && afterOpen.Row.Command == "Close", "File jump unexpectedly replaced the command tape");

        int scripts = zero, rows = zero, multi = zero;
        foreach (var path in Directory.EnumerateFiles(Path.Combine(directory, "bundles"), "adv_scenario_*.json"))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var asset in document.RootElement.EnumerateArray())
            {
                if (asset.GetProperty("class").GetString() != "ScenarioData") continue;
                var raw = asset.GetProperty("data").GetProperty("list").EnumerateArray().ToArray();
                var script = new KksAdvScript(path, asset.GetProperty("name").GetString(), raw.Select(item =>
                    new KksAdvScript.Row(item.GetProperty("_hash").GetInt32(), item.GetProperty("_version").GetInt32(),
                        item.GetProperty("_multi").GetInt32() != zero, item.GetProperty("command").GetString(),
                        item.GetProperty("_args").EnumerateArray().Select(arg => arg.GetString()))));
                tape.Load(script, true, true);
                var index = zero;
                while (tape.TryRead(out var entry))
                {
                    var expected = raw[index];
                    Require(!entry.Synthetic && entry.SourceRow == index && entry.Bundle == script.Bundle && entry.Asset == script.Asset, "Fixture source location lost");
                    Require(entry.Row.Hash == expected.GetProperty("_hash").GetInt32()
                        && entry.Row.Version == expected.GetProperty("_version").GetInt32()
                        && entry.Row.Command == expected.GetProperty("command").GetString()
                        && entry.Row.Arguments.SequenceEqual(expected.GetProperty("_args").EnumerateArray().Select(arg => arg.GetString())), "Fixture metadata or arguments changed");
                    Require(entry.Row.Multi == (expected.GetProperty("_multi").GetInt32() != zero), "Serialized Multi flag was recomputed instead of preserved");
                    if (entry.Row.Multi) multi++;
                    index++; rows++;
                }
                Require(index == raw.Length, "Fixture rows were skipped"); scripts++;
            }
        }
        Require(scripts > zero && rows > scripts && multi > zero, "No substantive ADV fixtures exercised");
        File.WriteAllText(Path.Combine(directory, "adv-tape-checks.json"), JsonSerializer.Serialize(new
        {
            scope = "Pure command-tape and source-data checks; commands, asset loader, presentation and game are not executed",
            scripts, rows, multi, checks = new[] { "detached args", "native defaults", "root marker/cursor", "additive root context", "checked and unchecked inserts",
                "reset history", "tag variables/earliest/missing", "parameter splice/repeat", "file jump splice", "all fixture metadata/arguments/Multi" }
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"PASS ADV tape scripts={scripts} rows={rows} multi={multi}");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
