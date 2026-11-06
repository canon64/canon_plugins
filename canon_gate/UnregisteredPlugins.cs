using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;

namespace CanonGate
{
    internal sealed class UnregisteredPlugin
    {
        internal readonly string Name;
        internal readonly string Guid;
        internal UnregisteredPlugin(string name, string guid) { Name = name; Guid = guid; }
    }

    internal static class UnregisteredPlugins
    {
        internal static List<UnregisteredPlugin> Build(string root, IEnumerable<PluginInfo> loaded, Func<string, bool> registered)
        {
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var result = new List<UnregisteredPlugin>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var info in loaded)
            {
                if (info?.Instance == null || info.Metadata == null || string.IsNullOrEmpty(info.Location)) continue;
                string guid = info.Metadata.GUID;
                if (string.IsNullOrEmpty(guid) || guid == GateApi.PluginGuid || CanonPlugins.Shared.GateScopeExclusions.IsExcludedPlugin(guid) || registered(guid)) continue;
                string location;
                try
                {
                    if (!Path.IsPathRooted(info.Location)) continue;
                    location = Path.GetFullPath(info.Location);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
                { continue; }
                if (!location.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !seen.Add(guid)) continue;
                result.Add(new UnregisteredPlugin(string.IsNullOrEmpty(info.Metadata.Name) ? guid : info.Metadata.Name, guid));
            }
            result.Sort((a, b) =>
            {
                int order = StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
                return order != 0 ? order : StringComparer.Ordinal.Compare(a.Guid, b.Guid);
            });
            return result;
        }
    }
}
