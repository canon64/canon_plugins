using System;
using System.Collections.Generic;
using System.Linq;

namespace MainGameAiShoujoMode.Conversation
{
    // Mirrors TextScenario's mutable commandPacks and cursor, not a call/return stack.
    // The runner must execute each row before reading another: Do() can splice this tape.
    internal sealed class KksAdvTape
    {
        internal sealed class Entry
        {
            internal string Bundle { get; }
            internal string Asset { get; }
            internal int SourceRow { get; }
            internal bool Synthetic { get; }
            internal KksAdvScript.Row Row { get; }
            internal Entry(string bundle, string asset, int sourceRow, bool synthetic, KksAdvScript.Row row)
            { Bundle = bundle; Asset = asset; SourceRow = sourceRow; Synthetic = synthetic; Row = row; }
        }
        private sealed class OpenedFile
        {
            internal string Bundle, Asset;
            internal int Line;
        }
        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<OpenedFile> _opened = new List<OpenedFile>();
        private readonly int _zero, _one;
        private int _next;
        internal string LoadBundle { get; private set; }
        internal string LoadAsset { get; private set; }
        internal int CurrentLine => _next - _one;
        internal int Count => _entries.Count;
        internal IReadOnlyList<Entry> Entries => _entries.AsReadOnly();
        internal IReadOnlyList<Entry> PreviewNextBatch()
        {
            var entries = new List<Entry>();
            for (var index = _next; index < _entries.Count; index++)
            {
                var entry = _entries[index]; entries.Add(entry);
                // These commands can replace/insert the remaining tape while Do executes.
                if (!entry.Row.Multi || entry.Row.Command == "None" || entry.Row.Command == "Log" || entry.Row.Command == "Close"
                    || entry.Row.Command == "Open" || entry.Row.Command == "Jump" || entry.Row.Command == "ParameterFile") break;
            }
            return entries.AsReadOnly();
        }
        internal KksAdvTape(int zero, int one) { _zero = zero; _one = one; _next = zero; }

        internal bool Load(KksAdvScript script, bool clear, bool clearCheck)
        {
            if (script == null) throw new ArgumentNullException(nameof(script));
            if (clear)
            {
                _opened.Clear(); _entries.Clear(); _next = _zero;
                LoadBundle = script.Bundle; LoadAsset = script.Asset;
                // Native LoadFile prepends an Open record and starts AFTER that marker.
                _entries.Add(OpenMarker(script.Bundle, script.Asset,
                    new[] { script.Bundle, script.Asset, bool.FalseString, bool.TrueString, bool.TrueString }));
                _next++;
                _entries.AddRange(Copy(script));
                return true;
            }
            if (clearCheck && _opened.Any(file => file.Bundle == script.Bundle && file.Asset == script.Asset)) return false;
            _entries.InsertRange(_next, Copy(script));
            var line = CurrentLine;
            if (!_opened.Any(file => file.Bundle == script.Bundle && file.Asset == script.Asset && file.Line == line))
                _opened.Add(new OpenedFile { Bundle = script.Bundle, Asset = script.Asset, Line = line });
            // Additive loads deliberately do not replace LoadBundle/LoadAsset.
            return true;
        }
        internal void InsertParameters(KksAdvScript script)
        {
            if (script == null) throw new ArgumentNullException(nameof(script));
            // ParameterFile does NOT participate in LoadFile's duplicate-open check.
            _entries.InsertRange(_next, Copy(script));
        }
        internal bool TryRead(out Entry entry)
        {
            if (_next >= _entries.Count) { entry = null; return false; }
            entry = _entries[_next++]; return true;
        }
        internal bool TryJump(string target, int localLine, Func<string, string> replaceVariable)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (replaceVariable == null) throw new ArgumentNullException(nameof(replaceVariable));
            var parts = target.Split(':');
            if (parts.Length == _one)
            {
                // SearchTag always scans from the beginning, including already executed rows.
                for (var index = _zero; index < _entries.Count; index++)
                    if (_entries[index].Row.Command == "Tag"
                        && replaceVariable(_entries[index].Row.Arguments[_zero]) == target)
                    { _next = index; return true; }
                return false;
            }
            var args = new[] { string.Empty, string.Empty, bool.FalseString, bool.TrueString, bool.TrueString };
            for (var index = _zero; index < parts.Length && index < args.Length; index++) args[index] = replaceVariable(parts[index]);
            // A file jump inserts an Open at localLine+1; it does not jump to a label.
            _entries.Insert(localLine + _one, OpenMarker(LoadBundle, LoadAsset, args));
            return true;
        }
        private Entry OpenMarker(string bundle, string asset, string[] args) => new Entry(bundle, asset, _zero, true,
            new KksAdvScript.Row(_zero, _zero, false, "Open", args));
        private IEnumerable<Entry> Copy(KksAdvScript script) => script.Rows.Select((row, index) => new Entry(script.Bundle, script.Asset, index, false, row));
    }
}
