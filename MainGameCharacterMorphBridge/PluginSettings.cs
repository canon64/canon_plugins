using System;
using System.Runtime.Serialization;

namespace MainGameCharacterMorphBridge
{
    [DataContract]
    internal sealed class MorphCardRegistration
    {
        [DataMember(Order = 0)] public bool Enabled = true;
        [DataMember(Order = 1)] public string Word = string.Empty;
        [DataMember(Order = 2)] public string TriggerWords = string.Empty;
        [DataMember(Order = 3)] public string CardPath = string.Empty;

        internal MorphCardRegistration Normalize()
        {
            Word = (Word ?? string.Empty).Trim();
            TriggerWords = PluginSettings.NormalizeCsv(TriggerWords);
            CardPath = PluginSettings.NormalizePath(CardPath);
            return this;
        }
    }

    [DataContract]
    internal sealed class PluginSettings
    {
        [DataMember(Order = 0)] public bool Enabled = true;
        [DataMember(Order = 1)] public bool EnableLogs;
        [DataMember(Order = 2)] public int TargetFemaleIndex;
        [DataMember(Order = 3)] public string TargetCardPath = string.Empty;
        [DataMember(Order = 4)] public bool AutoCaptureOriginal = true;
        [DataMember(Order = 5)] public bool AutoLoadOnHSceneStart = true;
        [DataMember(Order = 6)] public bool AutoResetBlendOnHSceneStart = true;
        [DataMember(Order = 7)] public string SelectedCardWord = string.Empty;
        [DataMember(Order = 8)] public string SelectedCardTriggerWords = string.Empty;
        [DataMember(Order = 9)] public MorphCardRegistration[] RegisteredCards = new MorphCardRegistration[0];
        [DataMember(Order = 10)] public float Blend;
        [DataMember(Order = 11)] public float Height = 0.5f;
        [DataMember(Order = 12)] public float Breast = 0.5f;
        [DataMember(Order = 13)] public int BodyShapeIndex;
        [DataMember(Order = 14)] public float BodyShapeValue = 0.5f;
        [DataMember(Order = 15)] public int FaceShapeIndex;
        [DataMember(Order = 16)] public float FaceShapeValue = 0.5f;

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Normalize();
        }

        internal PluginSettings Normalize()
        {
            TargetFemaleIndex = ClampInt(TargetFemaleIndex, 0, 1);
            TargetCardPath = NormalizePath(TargetCardPath);
            SelectedCardWord = (SelectedCardWord ?? string.Empty).Trim();
            SelectedCardTriggerWords = NormalizeCsv(SelectedCardTriggerWords);
            RegisteredCards = NormalizeRegisteredCards(RegisteredCards);
            Blend = Round2(Clamp01(Blend));
            Height = Round2(Clamp01(Height));
            Breast = Round2(Clamp01(Breast));
            BodyShapeIndex = ClampInt(BodyShapeIndex, 0, 43);
            BodyShapeValue = Round2(Clamp01(BodyShapeValue));
            FaceShapeIndex = ClampInt(FaceShapeIndex, 0, 51);
            FaceShapeValue = Round2(Clamp01(FaceShapeValue));
            return this;
        }

        internal static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        internal static int ClampInt(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        internal static float Round2(float value)
        {
            return (float)Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        internal static string NormalizePath(string value)
        {
            return (value ?? string.Empty).Trim().Trim('"');
        }

        internal static string NormalizeCsv(string csv)
        {
            string source = (csv ?? string.Empty).Replace('、', ',');
            string[] parts = source.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 0)
                return string.Empty;

            var results = new System.Collections.Generic.List<string>();
            foreach (string part in parts)
            {
                string item = (part ?? string.Empty).Trim();
                if (item.Length <= 0)
                    continue;
                if (!results.Contains(item))
                    results.Add(item);
            }

            return string.Join(",", results.ToArray());
        }

        private static MorphCardRegistration[] NormalizeRegisteredCards(MorphCardRegistration[] cards)
        {
            if (cards == null || cards.Length <= 0)
                return new MorphCardRegistration[0];

            var results = new System.Collections.Generic.List<MorphCardRegistration>();
            foreach (MorphCardRegistration card in cards)
            {
                if (card == null)
                    continue;

                card.Normalize();
                if (string.IsNullOrWhiteSpace(card.Word) && string.IsNullOrWhiteSpace(card.CardPath))
                    continue;

                results.Add(card);
            }

            return results.ToArray();
        }
    }
}
