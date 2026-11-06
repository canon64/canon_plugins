using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using MainGameAiShoujoMode.Life;
namespace MainGameAiShoujoMode.Life { internal sealed class AiLifeActivity { internal void Validate() { } } }
internal static class Program
{
    static AiResidentState Copy(AiResidentState value)
    {
        using (var stream = new MemoryStream()) {
            var serializer = new DataContractJsonSerializer(typeof(AiResidentState));
            serializer.WriteObject(stream, value); stream.Position = 0;
            return (AiResidentState)serializer.ReadObject(stream);
        }
    }
    static void Check(bool value, string text) { if (!value) throw new Exception(text); }
    static void Main()
    {
        var old = new AiResidentState { id = Guid.NewGuid().ToString("N"), origin = "existing-card", flavor = new int[8], mood = 73 };
        var migrated = Copy(old); migrated.Validate();
        Check(!migrated.nativeVitality.HasValue && !migrated.nativeMorality.HasValue && migrated.nativeDesires == null, "legacy save retains absence without resetting old fields");
        migrated.nativeVitality = 83; migrated.nativeMorality = 52;
        AiNativeHResultState.SetNativeMood(migrated, 50);
        migrated.nativeDesires = new Dictionary<int, float> { [1] = 50, [8] = 0 };
        var persisted = Copy(migrated); persisted.Validate();
        Check(persisted.mood == 73 && persisted.nativeMood == 50, "native phase midpoint result persists in the H extension without resetting pre-existing life mood");
        Check(persisted.id == old.id && persisted.nativeVitality == 83 && persisted.nativeMorality == 52 && persisted.nativeDesires[1] == 50 && persisted.nativeDesires[8] == 0, "native state survives actual product DataContract roundtrip with stable resident");
        migrated.nativeDesires[1] = 0;
        Check(persisted.nativeDesires[1] == 50, "checkpoint copy does not alias mutable desires");
        persisted.nativeVitality = float.NaN;
        try { persisted.Validate(); throw new Exception("nonfinite native status accepted"); } catch (SerializationException) { }
        persisted.nativeVitality = 50; persisted.nativeDesires[1] = float.PositiveInfinity;
        try { persisted.Validate(); throw new Exception("nonfinite desire accepted"); } catch (SerializationException) { }
        Console.WriteLine("DEDICATED_H_NATIVE_STATE_PASS 6");
    }
}
