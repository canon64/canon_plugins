using System;
using MainGameAiShoujoH.Presentation;
namespace UnityEngine { public static class JsonUtility { public static T FromJson<T>(string text) => System.Text.Json.JsonSerializer.Deserialize<T>(text, new System.Text.Json.JsonSerializerOptions { IncludeFields = true }); } }
namespace MainGameAiShoujoH.Presentation
{
    public enum VoiceKind { Breath, ShortBreath, Voice, StartVoice, None }
    public sealed class VoiceRequest { public int Slot, HowTalk; public string Bundle, Asset, LocalFile, Group, State; public VoiceKind Kind; public bool Protect, Force, OnlyOnce; public NativeFaceRequest NativeFace; }
    public sealed class NativeFaceRequest { public int Eyes, Brows, Mouth; public float EyeOpen, MouthMin, MouthMax, Tear, Cheek; public bool Highlight, Blink; }
}
internal static class Program
{
    private static int checks;
    private static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    private static void Main(string[] args)
    {
        Check(NativeVoiceConditions.Motion(Array.Empty<int>(), 9, VoiceKind.Breath), "empty breath motion filter is wildcard");
        Check(!NativeVoiceConditions.Motion(Array.Empty<int>(), 9, VoiceKind.Voice), "empty voice motion filter must reject");
        var context = new NativeVoiceContext { Personality = 0, PersonalPhase = 0, Mood = 50, StartMoodLower = 20, Bust = 1, NormalStartEvent = true, Confirmed = true, Evidence = "native new resident defaults and dedicated voice choice" };
        Check(NativeVoiceConditions.VoiceCharacter(0, context), "low personal phase category");
        Check(!NativeVoiceConditions.VoiceCharacter(1, context), "high phase category rejected");
        context.Faint = true; Check(!NativeVoiceConditions.VoiceCharacter(0, context) && NativeVoiceConditions.VoiceCharacter(4, context), "faint category only"); context.Faint = false;
        context.Skill2 = 170; Check(NativeVoiceConditions.Breath(new[] { 3 }, context) && !NativeVoiceConditions.Breath(new[] { 2 }, context), "skill170 exact boundary");
        Check(NativeVoiceConditions.StartSpecific(new[] { 2, 3, 5 }, context), "start bust1/skill170/slot0");
        context.Skill2 = 0;
        var catalog = new NativeAudioCatalog(args[0]);
        var request = catalog.SelectStart(context, 2, n => 0);
        Check(request != null && request.Kind == VoiceKind.StartVoice && request.Bundle.Contains("c00/"), "real native table initial voice selection");
        var next = catalog.SelectStart(context, 2, n => 0); Check(next != null && next.Asset != request.Asset, "native unplayed-first history");
        var breath = catalog.SelectBreath(context, 0, 0, "WLoop", n => 0); Check(breath != null && breath.Kind == VoiceKind.Breath && !string.IsNullOrEmpty(breath.LocalFile), "real native breath table converted file binding");
        Check(System.IO.File.Exists(catalog.EffectFile("hse_00_00_00")), "original unit u3d SE uses converted exact clip");
        var ordinary = catalog.SelectVoice(context, "WLoop", true, false, false, false, n => 0);
        Check(ordinary != null, "actual ordinary voice representative");
        context.MotionId = 2; // Native Aibu pattern explicitly includes2; kiss0 deliberately has no short.
        var shortVoice = catalog.SelectShort(context, 0, 0, 0, n => 0);
        Check(shortVoice != null, "actual short voice representative");
        if (args.Length > 1) System.IO.File.WriteAllText(args[1], System.Text.Json.JsonSerializer.Serialize(new[] {
            new { kind = "start", bundle = request.Bundle, asset = request.Asset, file = request.LocalFile },
            new { kind = "ordinary", bundle = ordinary.Bundle, asset = ordinary.Asset, file = ordinary.LocalFile },
            new { kind = "breath", bundle = breath.Bundle, asset = breath.Asset, file = breath.LocalFile },
            new { kind = "short", bundle = shortVoice.Bundle, asset = shortVoice.Asset, file = shortVoice.LocalFile }
        }));
        context.Yobai = true; Check(catalog.SelectStart(context, 2, n => 0) == null, "Yobai suppresses start voice");
        Console.WriteLine("NATIVE_VOICE_CHECKS_PASS " + checks);
    }
}
