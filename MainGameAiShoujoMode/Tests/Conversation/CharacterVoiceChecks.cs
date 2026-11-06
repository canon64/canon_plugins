using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoMode.Conversation;

internal static class CharacterVoiceChecks
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    internal static void Run(string evidence)
    {
        Console.WriteLine("CHARACTER_VOICE_CHECK_START productionCatalog=true liveAudio=false");
        var content = Path.GetFullPath(Path.Combine(evidence, "../../MainGameAiShoujoMode/Package/Content"));
        var catalog = KksCharacterVoiceCatalog.Load(Path.Combine(content, "kks-character-voices.json"));
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(evidence, "voice-asset-index.json")));
        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var bundle in json.RootElement.EnumerateObject().Where(bundle => bundle.Name.Contains("/c" + catalog.personality.ToString("00") + "/")))
            foreach (var asset in bundle.Value.EnumerateArray()) expected.Add(bundle.Name + ":" + asset.GetString());
        Require(expected.SetEquals(catalog.entries.Select(entry => entry.id)), "Voice catalog omits or adds source clips");
        foreach (var entry in catalog.entries)
        {
            Require(catalog.Find(entry.id) == entry, "Voice ID cannot be reached");
            Require(catalog.Resolve(catalog.personality, null, entry.asset) == entry.bundle, "Blank bundle cannot resolve source voice");
        }
        var one = Math.Sign(catalog.pageSize);
        foreach (var binding in catalog.bindings)
        {
            var entry = catalog.Find(binding.entryId);
            Require(catalog.Slot(catalog.personality, binding.stage, binding.key) == binding, "Scene binding unreachable");
            Require(entry.sources.Any(source => source.row == binding.sourceRow && source.stage == binding.stage && source.text == binding.text), "Scene binding changed source text or speaker");
            Require(catalog.Slot(catalog.personality + one, binding.stage, binding.key) == null, "Different personality leaked into scene");
        }
        var first = catalog.entries.First();
        Require(catalog.Resolve(catalog.personality + one, null, first.asset) == null, "Other personality used Sexy fallback");
        Require(catalog.Resolve(catalog.personality, first.bundle, first.asset) == first.bundle, "Explicit native bundle changed");
        Require(catalog.Resolve(catalog.personality, null, "missing-test-asset") == null, "Unknown clip was guessed");
        var grouped = catalog.Groups().SelectMany(catalog.InGroup).Concat(catalog.InGroup("用途未確認")).Select(entry => entry.id).ToHashSet();
        Require(expected.SetEquals(grouped), "Some clips cannot be reached from the source-use menu");
        foreach (var slot in catalog.unassignedSlots) Require(!catalog.bindings.Any(binding => binding.key == slot), "Unassigned slot claims a voice");
        File.WriteAllText(Path.Combine(evidence, "character-voice-checks.json"), JsonSerializer.Serialize(new
        {
            clips = expected.Count, bindings = catalog.bindings.Length, fullMenuCoverage = true,
            exactSourceText = true, noCrossPersonality = true, omittedBundleResolved = true,
            unassignedSlots = catalog.unassignedSlots, liveAudioVerified = false
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("CHARACTER_VOICE_CHECK_PASS clips=" + expected.Count + " bindings=" + catalog.bindings.Length + " fullMenuCoverage exactText noCrossPersonality unknownRejected");
    }
}
