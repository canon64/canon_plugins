using System;
using System.IO;
using System.Linq;
namespace MainGameAiShoujoH.Presentation
{
    internal static class FaceChannelLayout
    {
        internal static void Validate(string target, string[] required, string[] actual)
        {
            if (required == null || actual == null) throw new InvalidDataException("FBS channel names are missing: " + target);
            var counts = actual.GroupBy(n => n, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
            foreach (string name in required)
                if (!counts.TryGetValue(name, out int count) || count != Convert.ToInt32(true))
                    throw new InvalidDataException("FBS required channel is missing or ambiguous: " + target + ":" + name + " actualCount=" + actual.Length);
        }
    }
}
