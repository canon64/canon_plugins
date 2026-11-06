using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using MainGameAiShoujoMode;
using MapEnvironmentRuntime;

internal static class Program
{
    [DataContract] private sealed class Envelope { [DataMember] public string payload; }
    [DataContract] private sealed class Checkpoint { [DataMember] public string parameters; }
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
    private static void Main(string[] args)
    {
        Console.WriteLine("MOMENT_PARAMETERS_CHECKS_START");
        // Full Validate includes Unity native math calls; exercise the shipped managed merge outside Unity.
        var installed = JsonCodec.FromJson<Parameters>(File.ReadAllText(args.First()));
        var path = args.Last();
        var bytes = File.ReadAllBytes(path);
        var envelope = JsonCodec.FromJson<Envelope>(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));
        var checkpoint = JsonCodec.FromJson<Checkpoint>(Encoding.UTF8.GetString(Convert.FromBase64String(envelope.payload)));
        var before = JsonCodec.FromJson<Parameters>(checkpoint.parameters);
        var installedJson = installed.Json();
        var restored = MomentParameterCompatibility.MergeSaved(checkpoint.parameters, installed);
        Check(restored.numbers.Any(item => item.key == "hItems.insertionIndex"), "real pre-item checkpoint gains required item key");
        Check(installed.numbers.All(item => restored.numbers.Any(saved => saved.key == item.key)), "all current numeric keys exist after upgrade");
        Check(installed.text.All(item => restored.text.Any(saved => saved.key == item.key)), "all current text keys exist after upgrade");
        Check(before.numbers.All(item => restored.N(item.key) == item.value) && before.text.All(item => restored.T(item.key) == item.value),
            "all saved environment values are preserved");
        Check(installed.Json() == installedJson, "installed defaults are not mutated");
        Check(MomentParameterCompatibility.MergeSaved(restored.Json(), installed).Json() == restored.Json(), "repeated resume stages are idempotent");
        using (var sha = SHA256.Create())
            Check(sha.ComputeHash(bytes).SequenceEqual(sha.ComputeHash(File.ReadAllBytes(path))), "real checkpoint is unchanged");
        Console.WriteLine("MOMENT_PARAMETERS_CHECKS_PASS");
    }
}
