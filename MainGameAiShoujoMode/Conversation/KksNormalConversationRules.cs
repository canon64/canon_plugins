using System;
using System.Collections.Generic;
using System.Linq;

namespace MainGameAiShoujoMode.Conversation
{
    // Normal GetADV rules only. Introduction/event/world conditions have separate owners.
    internal sealed class KksNormalConversationRules
    {
#pragma warning disable CS0649
        [Serializable] internal sealed class AttributeBranch { public int condition, attribute; public int[] matching, other; }
        [Serializable] internal sealed class PreferenceBranch { public int condition, axis, boundary, matched, other; public bool lessOrEqual; }
        [Serializable] internal sealed class ForcedBranch { public int condition, response; }
        [Serializable] internal sealed class FallbackBranch { public int successCount; public bool hasNoInterest; public int[] weights; }
        [Serializable] internal sealed class BodySelection { public int condition; public string category; public int[] answers; }
        [Serializable] internal sealed class Settings
        {
            public int zero, one, listenHistoryCapacity, temperamentCount, disabledSpeakCondition, nicknameCondition, nicknameEventCondition, requiredNicknameEvent;
            public int[] nicknameEvents;
            public string fallbackChoice;
            public AttributeBranch[] attributeBranches;
            public PreferenceBranch[] preferenceBranches;
            public ForcedBranch[] forcedBranches;
            public FallbackBranch[] fallbackBranches;
            public BodySelection[] bodyCategorySelections;
        }
#pragma warning restore CS0649
        internal sealed class Context
        {
            internal int Attribute { get; set; }
            internal bool NormalNickname { get; set; }
            internal ISet<int> Events = new HashSet<int>();
            internal int[] Preferences = Array.Empty<int>();
            internal byte[] Temperament = Array.Empty<byte>();
            internal Func<string, int> BodyCategory { get; set; }
        }
        private readonly Settings _settings;
        internal Settings Configuration => _settings;
        internal KksNormalConversationRules(Settings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (settings.attributeBranches == null || settings.preferenceBranches == null || settings.forcedBranches == null
                || settings.fallbackBranches == null || settings.bodyCategorySelections == null || settings.nicknameEvents == null
                || settings.listenHistoryCapacity <= settings.zero || settings.one <= settings.zero || string.IsNullOrEmpty(settings.fallbackChoice))
                throw new FormatException("KKS normal conversation rule settings are incomplete.");
        }
        internal bool Eligible(KksConversationLayout.Group group, int condition, Context context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (group == KksConversationLayout.Group.Listen) return true;
            if (group != KksConversationLayout.Group.Talk) throw new NotSupportedException("Use the dedicated KKS rule owner for " + group);
            if (condition == _settings.disabledSpeakCondition) return false;
            if (condition == _settings.nicknameCondition) return !context.NormalNickname && !_settings.nicknameEvents.Any(context.Events.Contains);
            if (condition == _settings.nicknameEventCondition) return !context.NormalNickname && context.Events.Contains(_settings.requiredNicknameEvent);
            // Native ConditionDeterminationSpeak defaults to true for the other conditions.
            return true;
        }
        internal int Branch(KksConversationLayout.BranchInfo block, Context context, Func<IReadOnlyList<int>, int> weighted)
        {
            if (block?.success == null || context == null || weighted == null) throw new ArgumentNullException(nameof(block));
            int index;
            var attribute = _settings.attributeBranches.SingleOrDefault(rule => rule.condition == block.conditions);
            var preference = _settings.preferenceBranches.SingleOrDefault(rule => rule.condition == block.conditions);
            var forced = _settings.forcedBranches.SingleOrDefault(rule => rule.condition == block.conditions);
            if (attribute != null) index = weighted(context.Attribute == attribute.attribute ? attribute.matching : attribute.other);
            else if (preference != null)
            {
                if (preference.axis < _settings.zero || preference.axis >= context.Preferences.Length) throw new InvalidOperationException("KKS preference context is incomplete.");
                var value = context.Preferences[preference.axis];
                var matched = preference.lessOrEqual ? value <= preference.boundary : value >= preference.boundary;
                index = matched ? preference.matched : preference.other;
            }
            else if (forced != null) index = forced.response;
            else if (block.conditions >= _settings.one && block.conditions <= _settings.temperamentCount)
            {
                if (context.Temperament.Length != _settings.temperamentCount) throw new InvalidOperationException("KKS temperament context is incomplete.");
                index = context.Temperament[block.conditions - _settings.one];
            }
            else
            {
                var fallback = _settings.fallbackBranches.SingleOrDefault(rule => rule.successCount == block.success.Length && rule.hasNoInterest == (block.noInterested != null));
                if (fallback == null) throw new NotSupportedException("KKS branch response shape has no native fallback rule.");
                index = weighted(fallback.weights);
            }
            var responseCount = block.success.Length + _settings.one + (block.noInterested == null ? _settings.zero : _settings.one);
            if (index < _settings.zero || index >= responseCount) throw new InvalidOperationException("KKS context selected a nonexistent branch response.");
            return index;
        }
        internal int CorrectSelection(KksConversationLayout.SelectInfo block, Context context)
        {
            var rule = _settings.bodyCategorySelections.SingleOrDefault(item => item.condition == block.conditions);
            if (rule == null) return _settings.zero;
            if (context.BodyCategory == null) throw new InvalidOperationException("KKS body category context is missing.");
            var category = context.BodyCategory(rule.category);
            if (category < _settings.zero || category >= rule.answers.Length) throw new InvalidOperationException("KKS body category is outside the native table.");
            return rule.answers[category];
        }
    }
}
