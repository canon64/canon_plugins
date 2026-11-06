using System;
using System.IO;
using MainGameAiShoujoH.Integration;
namespace MainGameAiShoujoH.Presentation { public enum VoiceKind { Breath, ShortBreath, Voice, StartVoice, None } }
namespace MainGameAiShoujoH.Results { public sealed class ResultDelta { public string OperationId, ParticipantId; public int Parameter, Amount; public bool Assignment; public float? AssignedValue; } }
class Program
{
    static int checks;
    static void Check(bool condition, string message) { ++checks; if (!condition) throw new Exception(message); }
    static void Main(string[] args)
    {
        string root = Path.Combine(args[0], Guid.NewGuid().ToString("N"));
        int created = 0;
        var store = new NativeResidentProfileStore(root, () => { ++created; return new NativeResidentProfile { Mood = 50, StartMoodLower = 20, Origin = "test-explicit-new-setting", Evidence = "test fixture" }; });
        string first = Guid.NewGuid().ToString("N"), second = Guid.NewGuid().ToString("N");
        var p = store.ReadOrCreate(first); Check(created == 1 && p.ResidentId == first && p.Mood == 50, "new stable resident is durably initialized");
        p.Personality = 2; p.PersonalPhase = 3; p.Mood = 64; store.Save(p);
        var resumed = store.ReadOrCreate(first); Check(created == 1 && resumed.Personality == 2 && resumed.PersonalPhase == 3 && resumed.Mood == 64, "existing owned values survive next read");
        Check(File.Exists(Path.Combine(root, first + ".json.bak")), "previous valid document retained");
        var other = store.ReadOrCreate(second); Check(created == 2 && other.Personality == 0 && other.PersonalPhase == 0, "residents remain independent");
        var context = resumed.Context(1); Check(context.Slot == 1 && context.Personality == 2 && context.Confirmed && context.Evidence.Contains("test fixture"), "selected native configuration reaches runtime adapter with provenance");
        string checkpoint = store.Export(new[] { first, second }).Json;
        try { store.ReadOrCreate("../../invalid"); throw new Exception("unsafe identity accepted"); } catch (ArgumentException) { ++checks; }
        string path = Path.Combine(root, first + ".json"); File.WriteAllText(path, "corrupted-original");
        try { store.Save(resumed); throw new Exception("corrupt old file overwritten"); } catch (System.Runtime.Serialization.SerializationException) { ++checks; }
        Check(File.ReadAllText(path) == "corrupted-original", "corrupt original preserved on save failure");
        Check(store.ReadOrCreate(second).Mood == 50, "other resident remains readable after independent failure");
        var partial = store.Export(new[] { first, second }, checkpoint);
        Check(partial.Failures.Length == 1, "checkpoint records failed resident and preserves prior profile independently");
        var restoreRoot = Path.Combine(root, "restored"); var restoreStore = new NativeResidentProfileStore(restoreRoot, () => throw new Exception("import must not use defaults"));
        Check(restoreStore.Import(partial.Json).Length == 0 && restoreStore.ReadOrCreate(first).Personality == 2, "checkpoint imports real saved profile values");
        File.WriteAllText(Path.Combine(restoreRoot, first + ".json"), "damaged-current");
        Check(restoreStore.Import(partial.Json).Length == 1 && restoreStore.ReadOrCreate(second).Mood == 50, "failed profile restore preserves damaged target and continues independent residents");
        var result = new MainGameAiShoujoH.Results.ResultDelta { OperationId = "test-dirty", ParticipantId = second, Parameter = 14, Amount = 18 };
        store.ApplyVoiceResult(result); store.ApplyVoiceResult(result);
        Check(store.ReadOrCreate(second).Skill2 == 18, "native Dirty corresponds to HTag14 and applies once");
        store.ApplyVoiceResult(new MainGameAiShoujoH.Results.ResultDelta { OperationId = "test-unlock", ParticipantId = second, Parameter = 14, Amount = 200 });
        Check(store.ReadOrCreate(second).HasAdditionalMotion0 && store.ReadOrCreate(second).HasAdditionalMotion1, "native Dirty thresholds unlock both additional-motion flags");
        store.ApplyVoiceResult(new MainGameAiShoujoH.Results.ResultDelta { OperationId = "test-dirty-loss", ParticipantId = second, Parameter = 14, Amount = -300 });
        Check(store.ReadOrCreate(second).Skill2 == 0 && store.ReadOrCreate(second).HasAdditionalMotion1, "native flavor clamps at zero without revoking an acquired unlock");
        result = new MainGameAiShoujoH.Results.ResultDelta { OperationId = "test-mood", ParticipantId = second, Parameter = 1, Amount = 100 };
        store.ApplyVoiceResult(result);
        Check(store.ReadOrCreate(second).Mood == 50, "native initial phase SetStatus returns mood-bound midpoint before applying the delta");
        var adult = store.ReadOrCreate(second); adult.PersonalPhase = 2; adult.Skills = new[] { 2, 3 }; adult.Settings.Drug5 = true; store.Save(adult);
        result = new MainGameAiShoujoH.Results.ResultDelta { OperationId = "ending-receive-mood", ParticipantId = second, Parameter = 1, Amount = 27, Assignment = true, AssignedValue = 27.5f };
        store.ApplyVoiceResult(result); store.ApplyVoiceResult(result);
        Check(store.ReadOrCreate(second).Mood == 27.5f, "ending Receive preserves Single precision, sets rather than adds native mood and deduplicates");
        var configured = store.ReadOrCreate(second);
        Check(configured.Settings.Drug5 && configured.Context(0).Skills.Length == 2, "dedicated drug and possessed native skills survive save and reach runtime context");
        if (args.Length > 1)
        {
            var installedDefaults = new NativeResidentProfileStore(Path.Combine(root, "native-defaults"), () => NativeResidentProfileStore.LoadDefaults(args[1]));
            var native = installedDefaults.ReadOrCreate(Guid.NewGuid().ToString("N"));
            Check(native.Personality == 0 && native.PersonalPhase == 0 && native.Mood == 50 && native.StartMoodLower == 20 && native.Skill2 == 0, "generated source-backed defaults survive real serialization and durable reread");
            Check(native.Origin.Contains("新規") && native.Evidence.Contains("not an asserted"), "explicit new setting provenance remains distinct from native constructor evidence");
        }
        Console.WriteLine("DEDICATED_H_PROFILE_PASS " + checks);
    }
}
