using System;
using System.Collections.Generic;

namespace MainGameDevelopmentBridge
{
    internal static class DevelopmentStartup
    {
        internal const string ArgumentPrefix = "--kks-development-mode=";

        // Only this process's explicit argument can request automatic entry.
        internal static string SelectMode(IEnumerable<string> arguments)
        {
            string selected = null;
            foreach (string argument in arguments)
            {
                if (argument == null || !argument.StartsWith(ArgumentPrefix, StringComparison.Ordinal)) continue;
                if (selected != null) return "none";
                selected = argument.Substring(ArgumentPrefix.Length);
                if (selected != "ai" && selected != "hotel" && selected != "none") return "none";
            }
            return selected ?? "none";
        }
    }
}
