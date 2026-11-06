using System;
using System.Collections.Generic;
using System.Linq;

namespace MainGameAiShoujoMode.Conversation
{
    // A detached copy of ScenarioData: no ScriptableObject or scene controller is retained.
    internal sealed class KksAdvScript
    {
        internal sealed class Row
        {
            internal int Hash { get; }
            internal int Version { get; }
            internal bool Multi { get; }
            internal string Command { get; }
            internal IReadOnlyList<string> Arguments { get; }
            internal Row(int hash, int version, bool multi, string command, IEnumerable<string> arguments)
            {
                Hash = hash; Version = version; Multi = multi;
                Command = command ?? throw new ArgumentNullException(nameof(command));
                Arguments = Array.AsReadOnly((arguments ?? throw new ArgumentNullException(nameof(arguments))).ToArray());
            }
            // CommandBase.Initialize only replaces defaults with nonempty supplied cells.
            internal string[] InitializeArguments(IEnumerable<string> defaults)
            {
                if (defaults == null) return Arguments.ToArray();
                var result = defaults.ToList();
                using (var supplied = Arguments.GetEnumerator())
                {
                    for (int index = default; supplied.MoveNext(); index++)
                    {
                        if (index >= result.Count) result.Add(supplied.Current);
                        else if (!string.IsNullOrEmpty(supplied.Current)) result[index] = supplied.Current;
                    }
                }
                return result.ToArray();
            }
        }
        internal string Bundle { get; }
        internal string Asset { get; }
        internal IReadOnlyList<Row> Rows { get; }
        internal KksAdvScript(string bundle, string asset, IEnumerable<Row> rows)
        {
            Bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            Asset = asset ?? throw new ArgumentNullException(nameof(asset));
            var copy = (rows ?? throw new ArgumentNullException(nameof(rows))).ToArray();
            if (copy.Any(row => row == null)) throw new ArgumentException("ADV source contains a null row.", nameof(rows));
            Rows = Array.AsReadOnly(copy);
        }
    }
}
