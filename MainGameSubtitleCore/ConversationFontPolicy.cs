using System;

namespace MainGameSubtitleCore
{
    internal static class ConversationFontPolicy
    {
        // Names verified against KKS's own sharedassets5.assets, not OS fonts.
        internal static int Rank(string name)
        {
            if (string.Equals(name, "SetoFontCustom", StringComparison.OrdinalIgnoreCase)) return 2;
            if (string.Equals(name, "setofont", StringComparison.OrdinalIgnoreCase)) return 1;
            return 0;
        }
    }
}
