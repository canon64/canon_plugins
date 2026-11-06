using System;

namespace CanonPlugins.Shared
{
    internal static class GateScopeExclusions
    {
        // User-designated independent title mode; not an H-scene plugin.
        internal const string AiShoujoModeGuid = "com.kks.maingame.aishoujomode";
        internal static bool IsExcludedPlugin(string guid) =>
            string.Equals(guid, AiShoujoModeGuid, StringComparison.Ordinal);
        internal static bool IsIndependentInputOwner(string owner) =>
            IsExcludedPlugin(owner) || owner != null && owner.StartsWith(AiShoujoModeGuid + ".", StringComparison.Ordinal);
    }
}
