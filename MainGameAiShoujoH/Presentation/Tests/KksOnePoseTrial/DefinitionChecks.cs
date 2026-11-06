using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoH.Presentation;
namespace UnityEngine { public struct Vector3 { } public struct Quaternion { } }
namespace MainGameAiShoujoH.Presentation { internal static class KksOnePosePlayback { internal const string MotionId = "houshi:2"; } }
class DefinitionChecks
{
    static int checks;
    static void Check(bool condition, string label) { checks++; if (!condition) throw new Exception(label); }
    static void Main(string[] args)
    {
        using var evidence = JsonDocument.Parse(File.ReadAllText(args[0]));
        foreach (var actor in evidence.RootElement.GetProperty("actors").EnumerateArray())
        {
            var names = actor.GetProperty("states").EnumerateArray().Select(x => x.GetString()).ToArray();
            Check(names.ToHashSet().SetEquals(KksNativePoseDefinition.NativeStates), "Definition must match both independently preserved KKS native controllers");
        }
        var definition = KksNativePoseDefinition.Create();
        Check(definition.nativeActors == null && !definition.packedVerified && !definition.nativeGraphVerified, "KKS definition must not impersonate converted AI assets");
        Check(definition.states.All(x => x.actors == null), "No AI contact/frame/retarget metadata");
        foreach (var state in KksNativePoseDefinition.NativeStates) Check(KksNativePoseDefinition.NativeState(state) == state, "Exact KKS state preserved");
        Check(KksNativePoseDefinition.NativeState("Orgasm_OUT") == "M_OUT_Start", "Explicit upper action-start adaptation");
        Check(KksNativePoseDefinition.NativeState("Orgasm_OUT_A") == "OUT_A", "Explicit upper after-action adaptation");
        bool rejected = false; try { KksNativePoseDefinition.NativeState("D_WLoop"); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "No guessed D_ stripping");
        foreach (int pose in new[] { 72, 73, 74, 75, 76 }) Check(KksNativePoseDefinition.SupportsEnding(99, pose), "Saved upper ending key accepted for explicit Idle policy");
        Check(!KksNativePoseDefinition.SupportsEnding(99, 71) && !KksNativePoseDefinition.SupportsEnding(0, 72), "Unknown ending must not fall back");
        Console.WriteLine("KKS_NATIVE_DEFINITION_SOURCE_LINK_PASS " + checks);
    }
}
