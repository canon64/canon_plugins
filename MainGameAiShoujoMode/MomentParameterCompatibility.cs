using System;
using System.Collections.Generic;
using System.Linq;
using MapEnvironmentRuntime;

namespace MainGameAiShoujoMode
{
    internal static class MomentParameterCompatibility
    {
        internal static Parameters Restore(string savedJson, Parameters installed)
        {
            var saved = MergeSaved(savedJson, installed);
            saved.Validate();
            return saved;
        }
        internal static Parameters MergeSaved(string savedJson, Parameters installed)
        {
            var saved = JsonCodec.FromJson<Parameters>(savedJson);
            if (saved == null) throw new System.IO.InvalidDataException("保存した環境設定が空です。");
            saved.ValidateDocument();
            // Preserve saved values; copy only keys introduced after this checkpoint.
            var defaults = JsonCodec.FromJson<Parameters>(installed.Json());
            var numbers = new HashSet<string>(saved.numbers.Select(item => item.key), StringComparer.Ordinal);
            var text = new HashSet<string>(saved.text.Select(item => item.key), StringComparer.Ordinal);
            saved.numbers = saved.numbers.Concat(defaults.numbers.Where(item => !numbers.Contains(item.key))).ToArray();
            saved.text = saved.text.Concat(defaults.text.Where(item => !text.Contains(item.key))).ToArray();
            return saved;
        }
    }
}
