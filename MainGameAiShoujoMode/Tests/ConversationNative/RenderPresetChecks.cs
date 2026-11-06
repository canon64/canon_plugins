using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoMode.Conversation;
using UnityStandardAssets.ImageEffects;

internal static class RenderPresetChecks
{
    private sealed class Preset
    {
        public int Scalar;
        public object Reference;
        public readonly object Runtime = new object();
        public int Property { get; set; }
    }
    internal static void Run(string directory, int zero, int one)
    {
        Console.WriteLine("Start installed DOF field coverage and render-preset copy checks; no engine objects");
        var source = new Preset { Scalar = one, Reference = new object(), Property = one };
        var target = new Preset { Scalar = zero, Property = zero }; var original = target.Runtime;
        KksAdvComponentPreset.Copy(source, target);
        Require(target.Scalar == one && ReferenceEquals(target.Reference, source.Reference) && target.Property == zero
            && ReferenceEquals(target.Runtime, original), "Preset copy lost a setting or overwrote runtime/property state");
        try { KksAdvComponentPreset.Copy(source, new object()); throw new Exception("Mismatched effect type was accepted"); } catch (ArgumentException) { }
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "render-source-assets.json")));
        var depth = document.RootElement.GetProperty("components").EnumerateArray().Single(component => component.GetProperty("className").GetString() == nameof(DepthOfField));
        var serialized = depth.GetProperty("data").EnumerateObject().Select(property => property.Name)
            .Where(name => !name.StartsWith("m_", StringComparison.Ordinal)).OrderBy(name => name).ToArray();
        var copied = KksAdvComponentPreset.Fields(typeof(DepthOfField)).Select(field => field.Name).OrderBy(name => name).ToArray();
        Require(serialized.SequenceEqual(copied), "Installed DOF prefab has settings omitted from native field copy");
        var prefab = document.RootElement.GetProperty("assets").EnumerateArray().Single(asset => asset.GetProperty("name").GetString() == "ActionCamera");
        var order = prefab.GetProperty("componentOrder").EnumerateArray().Select(value => value.GetString()).ToArray();
        Require(Array.IndexOf(order, "CrossFade") >= zero && Array.IndexOf(order, nameof(DepthOfField)) >= zero, "Render effect order evidence is missing");
        var light = document.RootElement.GetProperty("components").EnumerateArray().Single(component => component.GetProperty("className").GetString() == "CorrectLightAngle");
        Require(light.GetProperty("data").TryGetProperty("correctRX", out _) && light.GetProperty("data").TryGetProperty("correctRY", out _)
            && light.GetProperty("light").ValueKind == JsonValueKind.Object, "Native light corrections/preset were not extracted");
        Require(light.GetProperty("lightSubtreeComponents").EnumerateArray().All(value => value.GetString() == "Transform" || value.GetString() == "Light"),
            "Installed light contains scripts/components that require a separate lifecycle adapter");
        File.WriteAllText(Path.Combine(directory, "adv-render-preset-checks.json"), JsonSerializer.Serialize(new
        {
            scope = "Installed camera metadata and production preset-field copy with managed test objects; no Unity rig creation/rendering/constraint updates",
            depthFields = copied, effectOrder = order.Where(name => name == "CrossFade" || name == nameof(DepthOfField)).ToArray(),
            allSerializedDepthFieldsCopied = true, runtimeFieldsAndPropertiesPreserved = true,
            nativeLightCorrectionFieldsAvailable = true, nativeLightSubtreeHasNoExtraBehaviour = true, engineRigRuntimeVerified = false
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"PASS DOF preset fields={copied.Length} native effect order/light metadata; engine rig=false");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
