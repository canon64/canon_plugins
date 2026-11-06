using System;
using System.Collections.Generic;

namespace MainGameHScenePoseInjector
{
    internal sealed class PatternEntry
    {
        public int Id;
        public string Name;
    }

    internal static class ExpressionPatternCatalog
    {
        internal static void AddRegistered(Dictionary<int, PatternEntry> table, int id, string name)
        {
            if (id < 0) return;
            if (string.IsNullOrWhiteSpace(name) && table.ContainsKey(id)) return;
            table[id] = new PatternEntry { Id = id, Name = Label(id, name) };
        }

        internal static void AddControllerPatterns(Dictionary<int, PatternEntry> table,
            int count, Func<int, string> readName)
        {
            for (int id = 0; id < count; id++)
            {
                if (table.ContainsKey(id)) continue;
                table.Add(id, new PatternEntry { Id = id, Name = Label(id, readName(id)) });
            }
        }

        internal static List<PatternEntry> Sorted(Dictionary<int, PatternEntry> table)
        {
            var result = new List<PatternEntry>(table.Values);
            result.Sort((a, b) => a.Id.CompareTo(b.Id));
            return result;
        }

        private static string Label(int id, string name)
        {
            return string.IsNullOrWhiteSpace(name) ? "追加表情 " + id : name;
        }
    }
}
