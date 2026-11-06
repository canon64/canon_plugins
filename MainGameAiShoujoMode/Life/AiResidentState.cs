using System;
using System.Runtime.Serialization;
using System.Collections.Generic;

namespace MainGameAiShoujoMode.Life
{
#pragma warning disable CS0649 // DataContract save/settings fields are populated by the serializer.
    [DataContract] internal sealed class AiResidentState
    {
        [DataMember] public string id, origin;
        [DataMember] public float temperature, mood, fullness, health, motivation, erotic;
        [DataMember] public float fatigue;
        // Dedicated native-H extension; absent legacy values are initialized from native defaults only when used.
        [DataMember(EmitDefaultValue = false)] public float? nativeVitality, nativeMorality, nativeMood;
        [DataMember(EmitDefaultValue = false)] public Dictionary<int, float> nativeDesires;
        [DataMember] public int sickness, phase, friendship;
        [DataMember] public double sicknessSeconds, treatmentSeconds;
        [DataMember] public bool usedMedicine, treated;
        [DataMember] public bool coldLocked;
        [DataMember] public int[] flavor;
        [DataMember] public int totalFlavor;
        [DataMember] public int flavorAdditionAmount;
        [DataMember] public string action = "normal", following, destination, blockedReason;
        [DataMember] public double gameSeconds;
        [DataMember] public bool hasPosition;
        [DataMember] public float x, y, z, yaw;
        [DataMember] public List<string> outfits = new List<string>();
        [DataMember(EmitDefaultValue = false)] public AiLifeActivity activity;
        [DataMember(EmitDefaultValue = false)] public string lastActivityResult;
        [OnDeserialized] private void Loaded(StreamingContext context) { if (outfits == null) outfits = new List<string>(); }

        internal void Validate()
        {
            activity?.Validate();
            if (nativeDesires != null) foreach (var value in nativeDesires.Values) if (float.IsNaN(value) || float.IsInfinity(value)) throw new SerializationException("Non-finite native desire.");
            foreach (var value in new[] { nativeVitality, nativeMorality, nativeMood }) if (value.HasValue && (float.IsNaN(value.Value) || float.IsInfinity(value.Value))) throw new SerializationException("Non-finite native status.");
            if (outfits == null || outfits.Exists(string.IsNullOrWhiteSpace)
                || new HashSet<string>(outfits, StringComparer.OrdinalIgnoreCase).Count != outfits.Count) throw new SerializationException("Invalid resident wardrobe references.");
            if (!Guid.TryParseExact(id, "N", out _) || string.IsNullOrWhiteSpace(origin) || flavor == null
                || Array.Exists(flavor, value => value < default(int))
                || !AllowedAction(action) || double.IsNaN(gameSeconds) || double.IsInfinity(gameSeconds) || gameSeconds < default(double))
                throw new SerializationException("Invalid AI resident state.");
            foreach (var value in new[] { temperature, mood, fullness, health, motivation, erotic, fatigue, x, y, z, yaw })
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new SerializationException("Non-finite AI resident status.");
            if (action == "follow" && string.IsNullOrWhiteSpace(following)) throw new SerializationException("Following target is missing.");
        }
        private static bool AllowedAction(string value) => value == "normal" || value == "idle" || value == "move"
            || value == "rest" || value == "eat" || value == "work" || value == "follow" || value == "treatment" || value == "motion";
    }

    [DataContract] internal sealed class AiLifeRules
    {
        [DataMember] public float temperature, mood, fullness, health, motivation, erotic, maximum, minimum;
        [DataMember] public int healthySickness, coldSickness, flavorCount, friendship;
        [DataMember] public float lowNeed, lowHealth;
        [DataMember] public double restSeconds;
        [DataMember] public int coldMedicineCategory, coldMedicineItem;
        [DataMember] public int flavorMaximum;
        internal void Validate()
        {
            if (maximum <= minimum || flavorCount <= default(int) || flavorMaximum <= default(int) || restSeconds <= default(double)
                || double.IsNaN(restSeconds) || double.IsInfinity(restSeconds)
                || float.IsNaN(minimum) || float.IsInfinity(minimum) || float.IsNaN(maximum) || float.IsInfinity(maximum)
                || healthySickness == coldSickness) throw new SerializationException("Invalid AI life rules.");
            foreach (var value in new[] { temperature, mood, fullness, health, motivation, erotic, lowNeed, lowHealth })
                if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum || value > maximum) throw new SerializationException("Invalid life status default/threshold.");
        }
        internal AiResidentState Create(string id, string origin) => new AiResidentState
        {
            id = id, origin = origin, temperature = temperature, mood = mood, fullness = fullness,
            health = health, motivation = motivation, erotic = erotic, sickness = healthySickness,
            friendship = friendship, flavor = new int[flavorCount]
        };
    }
#pragma warning restore CS0649
}
