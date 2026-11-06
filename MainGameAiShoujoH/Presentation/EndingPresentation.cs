using System;
using System.Runtime.Serialization;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace MainGameAiShoujoH.Presentation
{
    public sealed partial class PresentationModule
    {
#pragma warning disable CS0649
        [DataContract] private sealed class EndingExpression { [DataMember] public int personality; [DataMember] public string name; [DataMember] public string[] fields; }
        [DataContract] private sealed class EndingExpressions { [DataMember] public EndingExpression[] rows; }
#pragma warning restore CS0649
        private EndingExpressions endingExpressions;
        private EndingTextPanel endingText;
        public void PresentEndingText(string speaker, string text, Action advanced)
        {
            if (!finalizing || disposed) throw new InvalidOperationException("ADV caption requires an active ending lease.");
            if (endingText == null) endingText = new GameObject("AI H ending caption").AddComponent<EndingTextPanel>();
            endingText.Show("ai-h-ending:" + context.Id.ToString("N"), speaker, text, advanced);
        }
        public void ApplyNativeEndingExpression(int slot, string exactNativeExpression)
        {
            if (disposed || !finalizing || !NativeKks || slot < 0 || slot >= slots.Count) throw new InvalidOperationException("Native KKS ending expression requires its active lease");
            slots[slot].KksFaceLease.ApplyExpression(exactNativeExpression);
        }
        public void HideEndingText() { if (endingText != null) endingText.Hide(); }
        private void ReleaseEndingText()
        { if (endingText != null) { endingText.Hide(); UnityEngine.Object.Destroy(endingText.gameObject); endingText = null; } }

        private void LoadEndingExpressions()
        {
            if (endingExpressions != null) return;
            var loaded = MainGameAiShoujoH.Motion.DedicatedMotionModule.ReadCatalog<EndingExpressions>(Path.Combine(catalogDirectory, "ending-expressions.json"));
            if (loaded?.rows == null) throw new InvalidDataException("Native ending expression tables are unavailable.");
            foreach (var row in loaded.rows)
            {
                if (string.IsNullOrEmpty(row.name) || row.fields == null || row.fields.Length < 11) throw new InvalidDataException("Native ending expression row is invalid.");
                for (int i = 0; i < 8; i++) float.Parse(row.fields[i], CultureInfo.InvariantCulture);
                bool.Parse(row.fields[8]); float.Parse(row.fields[9], CultureInfo.InvariantCulture); bool.Parse(row.fields[10]);
            }
            endingExpressions = loaded;
        }
        public void ApplyEndingExpression(int slot, string name, int? personalityOverride = null)
        {
            if (!finalizing || disposed) throw new InvalidOperationException("ADV expression requires an active ending lease.");
            if (slot < 0 || slot >= slots.Count) throw new ArgumentOutOfRangeException(nameof(slot));
            if (NativeKks) return; // AI ending-face names are not applied to native KKS face data.
            if (endingExpressions == null) throw new InvalidOperationException("Native ending expressions were not prepared under the loading transition.");
            int personality = personalityOverride ?? context.Request.VoiceContexts[slot].Personality;
            foreach (var row in endingExpressions.rows)
            {
                if (row.personality != personality || row.name != name) continue;
                var f = row.fields;
                float Number(int i) => float.Parse(f[i], CultureInfo.InvariantCulture);
                // Manager.Game.Expression.Initialize: eyebrow/eyes/mouth then their opening maxima.
                slots[slot].FaceLease.Apply(new NativeFaceRequest { Brows = int.Parse(f[0]), Eyes = int.Parse(f[1]), Mouth = int.Parse(f[2]),
                    BrowOpen = Number(3), EyeOpen = Number(4), MouthMin = 0, MouthMax = Number(5), Cheek = Number(7), Highlight = bool.Parse(f[8]), Tear = Number(9), Blink = bool.Parse(f[10]) });
                int gaze = int.Parse(f[6]); if (gaze != -1) slots[slot].Actor.ChangeLookEyesPtn(gaze);
                return;
            }
            throw new InvalidDataException("Native ending expression is missing: c" + personality + ":" + name);
        }
    }
}
