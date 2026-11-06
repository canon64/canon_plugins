using System;
using System.Collections.Generic;

namespace CanonGate
{
    internal sealed class ExceptionPolicy
    {
        private HashSet<string> _maker = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> _everywhere = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> _aiMode = new HashSet<string>(StringComparer.Ordinal);

        private HashSet<string> _hotel = new HashSet<string>(StringComparer.Ordinal);
        internal void Load(string maker, string everywhere, string aiMode = "", string hotel = "")
        {
            _maker = Parse(maker);
            _everywhere = Parse(everywhere);
            _aiMode = Parse(aiMode);
            _hotel = Parse(hotel);
        }

        internal bool Allows(string guid, bool insideMaker, bool aiMode = false, bool hotel = false) =>
            _everywhere.Contains(guid) || (insideMaker && _maker.Contains(guid)) || (aiMode && _aiMode.Contains(guid)) || (hotel && _hotel.Contains(guid));

        private static HashSet<string> Parse(string text)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (string item in (text ?? "").Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string guid = item.Trim();
                if (guid.Length != 0) result.Add(guid);
            }
            return result;
        }
    }
}
