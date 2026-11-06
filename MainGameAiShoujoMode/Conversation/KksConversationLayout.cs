using System;
using System.Collections.Generic;

namespace MainGameAiShoujoMode.Conversation
{
    // Serialized KKS table structure. Selection and AI-world effects belong to the session controller.
    internal sealed partial class KksConversationLayout
    {
        internal enum Group { Encounter, Introduction, Talk, Listen, Apologize, See, Touch, Interlude, LeaveAlone, EndConversation, RankUp, Event, Tips }

        // Populated from native records or JSON fixtures rather than assignments in this file.
#pragma warning disable CS0649
        [Serializable]
        internal sealed class Row
        {
            public int priority, favor, lewdness, anger, eyeLook, neckLook, conditions, topic;
            public string assetbundle = "", file = "", text = "", pose = "", facialExpression = "";
        }
#pragma warning restore CS0649

        internal abstract class BasicInfo
        {
            public int priority, conditions;
            public int row { get; }
            protected BasicInfo(int sourceRow) { row = sourceRow; }
        }
        internal sealed class GenericInfo : BasicInfo
        {
            public Row[] talk;
            public GenericInfo(int row, int count) : base(row) { talk = new Row[count]; }
            public GenericInfo(int row, Row data) : base(row) { talk = new[] { data }; }
        }
        internal sealed class BranchInfo : BasicInfo
        {
            public Row introduction, failure, noInterested;
            public Row[] success;
            public BranchInfo(int row) : base(row) { }
        }
        internal sealed class SelectInfo : BasicInfo
        {
            public Row introduction, failure;
            public Row[] choice;
            public SelectInfo(int row, int count) : base(row) { choice = new Row[count]; }
        }

        private Dictionary<int, Dictionary<Group, Dictionary<int, List<BasicInfo>>>> dicTalkInfo;
#pragma warning disable CS0649
        [Serializable]
        internal sealed class LayoutNumber { public string key; public int value; }
#pragma warning restore CS0649
        private readonly Dictionary<string, int> _numbers = new Dictionary<string, int>(StringComparer.Ordinal);
        private int N(string key) => _numbers.TryGetValue(key, out var value) ? value : throw new FormatException("Missing KKS layout constant: " + key);
        internal IReadOnlyDictionary<int, Dictionary<Group, Dictionary<int, List<BasicInfo>>>> Groups => dicTalkInfo;

        internal static KksConversationLayout Read(List<Row> rows, IEnumerable<LayoutNumber> numbers, bool npc = false)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            if (rows.Exists(row => row == null)) throw new ArgumentException("Conversation table contains a null row.", nameof(rows));
            var layout = new KksConversationLayout();
            if (numbers == null) throw new ArgumentNullException(nameof(numbers));
            foreach (var number in numbers)
            {
                if (number == null || string.IsNullOrEmpty(number.key) || layout._numbers.ContainsKey(number.key)) throw new FormatException("Invalid or duplicate KKS layout constant.");
                layout._numbers.Add(number.key, number.value);
            }
            try { if (npc) layout.ParseNpc(rows); else layout.Parse(rows); }
            catch (ArgumentOutOfRangeException ex) { throw new FormatException("KKS conversation table does not match the installed row layout.", ex); }
            return layout;
        }

        private Row LoadData(Row source) => source;
        private Row LoadData(Row source, ref int priority, ref int conditions)
        {
            priority = source.priority; conditions = source.conditions; return source;
        }
        private Row LoadChoiceData(Row source) => source;
        private BranchInfo CreateTalk(List<Row> rows, ref int startRow) => CreateTalk(rows, ref startRow, N("native.layout.-1"));
        private SelectInfo CreateSelect(List<Row> rows, ref int startRow, int count) => CreateSelect(rows, ref startRow, count, N("native.layout.-1"));
    }
}
