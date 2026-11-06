using System;
using System.Collections.Generic;

namespace MainGameObjectComposer
{
    [Serializable]
    internal sealed class PresetEntry
    {
        public string key;
        public string displayName;
        public PresetEditorSnapshot editorView;
        public bool hasConditionMetadata;
        public string modeDisplayName;
        public int animationId = -1;
        public string animationName;
        public int posture = -1;
        public int sysTaii = -1;
        public int kindHoushi = -1;
        public string categorySummary;
        public bool useLoopSplit = true;
        public bool autoLoadEligible = true;
        public bool applyToW = true;
        public bool applyToS = true;
        public bool applyToOther = true;
        public List<string> loop1Json = new List<string>();
        public List<string> loop2Json = new List<string>();
        public List<string> ikLinks = new List<string>();
        public List<string> motionIntensityRulesJson = new List<string>();
        public List<string> motionIntensityTargetIds = new List<string>();
        public List<string> motionPositionRulesJson = new List<string>();
        public int movementVersion;
        public bool movementEnabled;
        public float movementZeroMultiplier, movementFullMultiplier, movementTransitionSeconds;
        public List<string> movementTargetIds = new List<string>();
        public List<string> movementRulesJson = new List<string>();
        public bool betterPenetrationTargetConfigured;
        public bool betterPenetrationTargetEnabled;
        public float betterPenetrationVaginaTarget;
    }

    [Serializable]
    internal sealed class PresetsFile
    {
        public string format = "ObjectComposerPresetsV2";
        public List<string> presetsJson = new List<string>();
    }

    internal static class PresetEndpointCoordinates
    {
        internal static void Capture(PresetEntry preset, Func<List<string>, List<string>> capture)
        {
            // Compute both before assigning: a failed conversion leaves the candidate intact.
            var l1 = capture(preset.loop1Json);
            var l2 = capture(preset.loop2Json);
            preset.loop1Json = l1;
            preset.loop2Json = l2;
        }
    }

    internal static class PresetConditionBinding
    {
        internal static void Bind(PresetEntry candidate, string currentKey, Action<PresetEntry, string> updateMetadata)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            if (string.IsNullOrWhiteSpace(currentKey)) throw new InvalidOperationException("現在の体位を取得できません。編集内容は保持しています。");
            candidate.key = currentKey;
            updateMetadata(candidate, currentKey);
        }
    }
}
