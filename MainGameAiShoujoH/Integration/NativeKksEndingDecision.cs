using System.Collections.Generic;

namespace MainGameAiShoujoH.Integration
{
    internal static class NativeKksEndingDecision
    {
        // HSceneProc.EndProc, for an owned normal 2-person scene with no
        // ActionScene NPC witness/affinity (the AI session owns its own results).
        internal static int Select(bool firstInsertion, int orgasms, int finishes, ICollection<int> categories, int experience, int lewdness, bool invited)
        {
            if (firstInsertion) return 0;
            if (orgasms == 0 && !categories.Contains(1200) && !categories.Contains(12) && !categories.Contains(1306)) return experience == 3 ? 7 : 6;
            if (finishes == 0 && !categories.Contains(1002) && !categories.Contains(1003) && !categories.Contains(1305)) return 8;
            if (invited) return 11;
            if (categories.Contains(1003) || categories.Contains(1305)) return lewdness >= 80 && orgasms != 0 ? 10 : 9;
            if (orgasms >= 3) return 4;
            return experience <= 1 ? 1 : experience == 2 ? 2 : 3;
        }
    }
}
