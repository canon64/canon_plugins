using MainGameCharacterMorphBridge;
using System;
using UnityEngine;

namespace MainGameVoiceFaceEventBridge
{
    internal sealed partial class Plugin
    {
        private void ScheduleMorphBridgeTriggersFromText(string text, Func<int, string, Action, float> scheduleAction)
        {
            PluginSettings settings = Settings;
            if (settings == null || !settings.EnableMorphBridgeTriggers || string.IsNullOrWhiteSpace(text) || scheduleAction == null)
                return;

            string[] resetKeywords = SplitKeywords((settings.MorphBridgeResetKeywords ?? "元に戻るね").Replace('、', ','));
            string[] transformKeywords = SplitKeywords((settings.MorphBridgeTransformKeywords ?? "変身するね").Replace('、', ','));
            int resetIndex = FindFirstKeywordIndex(text, resetKeywords, StringComparison.Ordinal);
            int transformIndex = FindFirstKeywordIndex(text, transformKeywords, StringComparison.Ordinal);
            float seconds = ResolveMorphBridgeTransitionSeconds();

            if (resetIndex >= 0)
            {
                float delay = scheduleAction(resetIndex, "morph_reset", () => ExecuteMorphBridgeReset(seconds));
                Log("[morph] reset trigger matched pos=" + resetIndex + " delay=" + delay.ToString("F2") + " seconds=" + seconds.ToString("F2"));
            }

            if (transformIndex >= 0)
            {
                string cardWord;
                string cardPath;
                bool hasCardWord = CharacterMorphBridgeApi.TryResolveRegisteredCardFromText(text, out cardWord, out cardPath);
                string selectedWord = hasCardWord ? cardWord : string.Empty;
                float delay = scheduleAction(transformIndex, "morph_transform", () => ExecuteMorphBridgeTransform(selectedWord, seconds));
                Log("[morph] transform trigger matched pos=" + transformIndex
                    + " delay=" + delay.ToString("F2")
                    + " seconds=" + seconds.ToString("F2")
                    + " cardWord=" + (string.IsNullOrWhiteSpace(selectedWord) ? "(active)" : selectedWord)
                    + " cardPath=" + (string.IsNullOrWhiteSpace(cardPath) ? "(active)" : cardPath)
                    + " preview=" + TrimPreview(text, 80));
            }

            if (resetIndex < 0 && transformIndex < 0)
                Log("[morph] no trigger matched");
        }

        private void ExecuteMorphBridgeTransform(string cardWord, float seconds)
        {
            if (!CharacterMorphBridgeApi.IsAvailable)
            {
                LogWarn("[morph] transform failed result=bridge_unavailable");
                return;
            }

            bool ok = !string.IsNullOrWhiteSpace(cardWord)
                ? CharacterMorphBridgeApi.BlendToCardWord(cardWord, 1f, seconds)
                : CharacterMorphBridgeApi.BlendTo(1f, seconds);

            if (ok)
            {
                Log("[morph] transform requested result=ok cardWord=" + (string.IsNullOrWhiteSpace(cardWord) ? "(active)" : cardWord)
                    + " seconds=" + seconds.ToString("F2"));
            }
            else
            {
                LogWarn("[morph] transform failed result=api_false cardWord=" + (string.IsNullOrWhiteSpace(cardWord) ? "(active)" : cardWord)
                    + " seconds=" + seconds.ToString("F2"));
            }
        }

        private void ExecuteMorphBridgeReset(float seconds)
        {
            if (!CharacterMorphBridgeApi.IsAvailable)
            {
                LogWarn("[morph] reset failed result=bridge_unavailable");
                return;
            }

            bool ok = CharacterMorphBridgeApi.BlendTo(0f, seconds);
            if (ok)
            {
                Log("[morph] reset requested result=ok seconds=" + seconds.ToString("F2"));
            }
            else
            {
                LogWarn("[morph] reset failed result=api_false seconds=" + seconds.ToString("F2"));
            }
        }

        private float ResolveMorphBridgeTransitionSeconds()
        {
            float seconds = _cfgMorphBridgeTransitionSeconds != null
                ? _cfgMorphBridgeTransitionSeconds.Value
                : (Settings != null ? Settings.MorphBridgeTransitionSeconds : 5f);
            return Mathf.Clamp(seconds, 0f, 60f);
        }
    }
}
