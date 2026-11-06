using System;
using System.Collections.Generic;
using System.Linq;

namespace MainGameHLocationPlacement
{
    internal static class IkEligibility
    {
        internal static bool Linked(int index, string[] keys, object settings, IEnumerable<string> activeLinks)
        {
            var enabled = R.Get<bool[]>(settings, "FollowComposerEnabled"); var ids = R.Get<string[]>(settings, "FollowComposerObjectIds");
            return index < enabled.Length && enabled[index] && index < ids.Length && !string.IsNullOrEmpty(ids[index])
                || activeLinks != null && activeLinks.Any(s => s != null && s.StartsWith(keys[index] + "\t", StringComparison.Ordinal));
        }
        internal static bool Independent(object state, int index, bool linked, Array wrists, bool allowSavedFollow)
        {
            if (state == null || linked || R.Get<bool>(state, "ExternalOwner")) return false;
            if (index < 2 && index < wrists.Length && wrists.GetValue(index) != null) return false;
            return allowSavedFollow || R.Get(state, "FollowBone") == null && R.Get(state, "PendingFollowBone") == null;
        }
    }
}
