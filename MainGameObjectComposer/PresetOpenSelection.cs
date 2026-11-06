using System;
using System.Collections.Generic;
using System.Linq;

namespace MainGameObjectComposer
{
    internal static class PresetOpenSelection
    {
        internal static PresetEntry Choose(IEnumerable<PresetEntry> presets, string currentKey, PresetEntry active, string rememberedKey, string rememberedName)
        {
            if (!TryKey(currentKey, out string mode, out int animation, out string lane)) return null;
            var matches = presets.Where(p => p != null && TryKey(p.key, out string pm, out int pa, out _) && pm == mode && pa == animation &&
                (lane == "W" ? p.applyToW : lane == "S" ? p.applyToS : p.applyToOther)).ToArray();
            // Exact strength first; an already selected preset only wins within this pose.
            var exact = matches.Where(p => p.key == currentKey).ToArray();
            var candidates = exact.Length > 0 ? exact : matches;
            return candidates.FirstOrDefault(p => ReferenceEquals(p, active))
                ?? candidates.FirstOrDefault(p => p.key == rememberedKey && p.displayName == rememberedName)
                ?? candidates.FirstOrDefault(p => p.autoLoadEligible)
                ?? candidates.FirstOrDefault();
        }
        private static bool TryKey(string key, out string mode, out int animation, out string lane)
        {
            mode = null; animation = -1; lane = null;
            string[] parts = key?.Split('/');
            if (parts == null || parts.Length != 3 || string.IsNullOrEmpty(parts[0]) || !int.TryParse(parts[1], out animation) || animation < 0 || string.IsNullOrEmpty(parts[2])) return false;
            mode = parts[0]; lane = parts[2]; return true;
        }
    }
}
