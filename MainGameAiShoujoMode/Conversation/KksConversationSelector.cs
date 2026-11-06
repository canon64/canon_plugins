using System;
using System.Collections.Generic;
using System.Linq;

namespace MainGameAiShoujoMode.Conversation
{
    internal sealed class KksConversationSelector
    {
        private readonly KksNormalConversationRules _rules;
        private readonly Queue<int> _listen = new Queue<int>();
        private KksConversationLayout.BasicInfo _previous;
        internal KksConversationSelector(KksNormalConversationRules rules) { _rules = rules ?? throw new ArgumentNullException(nameof(rules)); }
        internal void Reset() { _listen.Clear(); _previous = null; }
        internal KksConversationLayout.BasicInfo Select(KksConversationLayout layout, int stage, KksConversationLayout.Group group,
            int? command, KksNormalConversationRules.Context context, Func<int, int> pick)
        {
            if (group != KksConversationLayout.Group.Talk && group != KksConversationLayout.Group.Listen)
                throw new NotSupportedException("This selector owns normal talk/listen only.");
            if (layout == null || pick == null) throw new ArgumentNullException(nameof(layout));
            if (!layout.Groups.TryGetValue(stage, out var groups) || !groups.TryGetValue(group, out var commands))
                throw new InvalidOperationException("KKS conversation stage/group is unavailable.");
            int selected;
            if (command.HasValue) selected = command.Value;
            else
            {
                var keys = commands.Keys.Where(key => group != KksConversationLayout.Group.Listen
                    || (!_listen.Contains(key) && commands[key].Any(block => _rules.Eligible(group, block.conditions, context)))).ToArray();
                if (!keys.Any()) throw new InvalidOperationException("KKS has no eligible unqueued conversation command.");
                selected = keys[Pick(pick, keys.Length)];
                if (group == KksConversationLayout.Group.Listen)
                {
                    _listen.Enqueue(selected);
                    if (_listen.Count > _rules.Configuration.listenHistoryCapacity) _listen.Dequeue();
                }
            }
            if (!commands.TryGetValue(selected, out var list)) throw new KeyNotFoundException("KKS conversation command is unavailable.");
            var eligible = list.OrderByDescending(block => block.priority).Where(block => _rules.Eligible(group, block.conditions, context)).ToArray();
            if (!eligible.Any()) throw new InvalidOperationException("KKS conversation command has no eligible block.");
            var highest = eligible.First().priority;
            var candidates = eligible.TakeWhile(block => block.priority == highest).ToList();
            if (candidates.Count > _rules.Configuration.one && _previous != null) candidates.Remove(_previous);
            return _previous = candidates[Pick(pick, candidates.Count)];
        }
        private int Pick(Func<int, int> pick, int count)
        {
            var result = pick(count);
            if (result < _rules.Configuration.zero || result >= count) throw new InvalidOperationException("KKS candidate picker returned an invalid index.");
            return result;
        }
    }
}
