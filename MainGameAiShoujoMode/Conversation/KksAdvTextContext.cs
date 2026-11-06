using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MainGameAiShoujoMode.Conversation
{
    internal sealed class KksAdvTextContext
    {
        internal readonly Dictionary<string, Func<string>> Variables = new Dictionary<string, Func<string>>(StringComparer.Ordinal);
        internal readonly Dictionary<string, Func<string>> Replacements = new Dictionary<string, Func<string>>(StringComparer.Ordinal);
        private readonly HashSet<string> _names;
        private readonly Func<string, string> _onceName;
        internal KksAdvTextContext(IEnumerable<string> nameKeys, Func<string, string> onceName)
        { _names = new HashSet<string>(nameKeys, StringComparer.OrdinalIgnoreCase); _onceName = onceName ?? throw new ArgumentNullException(nameof(onceName)); }
        internal string ReplaceArgument(string value) => Variables.TryGetValue(value, out var variable) ? variable() : value;
        internal string ReplaceText(string value, bool once) => ReplaceText(value, once, new HashSet<string>(StringComparer.Ordinal));
        private string ReplaceText(string value, bool once, HashSet<string> stack) => Regex.Replace(value ?? string.Empty, @"\[.*?\]", match =>
        {
            var key = match.Value.TrimStart('[').TrimEnd(']');
            if (!Replacements.TryGetValue(key, out var replacement)) return match.Value;
            var text = replacement();
            if (!string.IsNullOrEmpty(text) && !_names.Contains(key))
            {
                if (!stack.Add(key)) throw new InvalidOperationException("Recursive KKS text replacement: " + key);
                try { text = ReplaceText(text, once, stack); } finally { stack.Remove(key); }
            }
            if (once)
            {
                var selected = _onceName(key);
                if (selected != null) text = Replacements.TryGetValue(selected, out var name) ? name() : null;
            }
            return text ?? match.Value;
        });
        internal void SetFile(string bundle, string asset)
        { Variables["BundleFile"] = () => bundle; Variables["AssetFile"] = () => asset; }
    }
}
