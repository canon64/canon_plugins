using System;
using System.Collections.Generic;
using System.Linq;

namespace MainGameAiShoujoMode.Conversation
{
    // Native Info.Create*ADV composition, with scene-independent presentation steps.
    internal sealed class KksDialogueProgram
    {
        internal enum RowKind { PlayerText, HeroineText, OpenScenario }
        internal abstract class Step { }
        internal sealed class Cue : Step
        {
            internal KksConversationLayout.Row Source { get; }
            internal RowKind Kind { get; }
            internal bool Motion { get; }
            internal Cue(KksConversationLayout.Row source, bool heroine, bool motion = true)
            {
                Source = source ?? throw new ArgumentNullException(nameof(source)); Motion = motion;
                Kind = heroine ? RowKind.HeroineText : string.IsNullOrEmpty(source.file) ? RowKind.PlayerText
                    : string.IsNullOrEmpty(source.text) ? RowKind.OpenScenario : RowKind.HeroineText;
            }
        }
        internal sealed class Outcome : Step
        {
            // Native ChangeValueInfo uses this row's favor/lewdness/topic, not its introduction.
            internal KksConversationLayout.Row Source { get; }
            internal int? SelectedIndex { get; }
            internal int? CorrectIndex { get; }
            internal int Topic { get; }
            internal int Favor => Source.favor;
            internal int Lewdness => Source.lewdness;
            internal bool? IsCorrect => SelectedIndex.HasValue && CorrectIndex.HasValue ? SelectedIndex.Value == CorrectIndex.Value : (bool?)null;
            internal Outcome(KksConversationLayout.Row source, int? index = null, int? correct = null, int? topicOverride = null)
            { Source = source; SelectedIndex = index; CorrectIndex = correct; Topic = topicOverride ?? source.topic; }
        }
        internal sealed class Selection : Step
        {
            internal sealed class Option
            {
                internal string Text { get; }
                internal KksDialogueProgram Route { get; }
                internal Option(string text, KksDialogueProgram route) { Text = text; Route = route; }
            }
            internal IReadOnlyList<Option> Options { get; }
            internal Selection(IEnumerable<Option> options) { Options = Array.AsReadOnly(options.ToArray()); }
        }
        internal IReadOnlyList<Step> Steps { get; }
        private KksDialogueProgram(IEnumerable<Step> steps) { Steps = Array.AsReadOnly(steps.ToArray()); }

        internal static KksDialogueProgram Generic(KksConversationLayout.GenericInfo block, bool motion = true)
        {
            if (block?.talk == null || !block.talk.Any()) throw new ArgumentException("KKS generic block has no rows.", nameof(block));
            // Generic effects are read from the first row by native CreateGenericADV.
            return new KksDialogueProgram(block.talk.Select(row => (Step)new Cue(row, false, motion)).Concat(new[] { new Outcome(block.talk.First()) }));
        }
        internal static KksDialogueProgram Branch(KksConversationLayout.BranchInfo block, int nativeResponseIndex, int zero)
        {
            if (block?.success == null) throw new ArgumentException("KKS branch block is incomplete.", nameof(block));
            var responses = block.success.Concat(new[] { block.failure });
            if (block.noInterested != null) responses = responses.Concat(new[] { block.noInterested });
            var choices = responses.ToArray();
            if (nativeResponseIndex < zero || nativeResponseIndex >= choices.Length) throw new ArgumentOutOfRangeException(nameof(nativeResponseIndex));
            var response = choices[nativeResponseIndex] ?? throw new ArgumentException("KKS branch response is missing.", nameof(block));
            var steps = new List<Step>();
            if (block.introduction != null) steps.Add(new Cue(block.introduction, false));
            steps.Add(new Cue(response, false)); steps.Add(new Outcome(response));
            return new KksDialogueProgram(steps);
        }
        internal static KksDialogueProgram Select(KksConversationLayout.SelectInfo block, IReadOnlyDictionary<int, string[]> labels,
            int nativeCorrectIndex, int zero, string nativeFallbackLabel)
        {
            if (block?.choice == null || !block.choice.Any() || block.introduction == null)
                throw new ArgumentException("KKS selection block is incomplete.", nameof(block));
            if (nativeCorrectIndex < zero || nativeCorrectIndex >= block.choice.Length) throw new ArgumentOutOfRangeException(nameof(nativeCorrectIndex));
            if (string.IsNullOrEmpty(nativeFallbackLabel)) throw new ArgumentException("KKS default ChoiceInfo text is missing.", nameof(nativeFallbackLabel));
            string[] captions = null;
            if (labels != null && labels.TryGetValue(block.conditions, out captions) && captions.Length < block.choice.Length)
                throw new FormatException("KKS option labels do not cover every selection route.");
            var options = new List<Selection.Option>();
            int index = zero;
            foreach (var candidate in block.choice)
            {
                var response = block.failure == null || index == nativeCorrectIndex ? candidate : block.failure;
                if (response == null) throw new ArgumentException("KKS selection response is missing.", nameof(block));
                // Native selection forces nonempty response text to heroine text, even without a voice file.
                // ChangeValueSelectInfo does not assign topic; its native default is zero.
                var route = new KksDialogueProgram(new Step[] { new Cue(response, !string.IsNullOrEmpty(response.text)), new Outcome(response, index, nativeCorrectIndex, zero) });
                options.Add(new Selection.Option(captions == null ? nativeFallbackLabel : captions[index], route)); index++;
            }
            return new KksDialogueProgram(new Step[] { new Cue(block.introduction, true), new Selection(options) });
        }
    }
}
