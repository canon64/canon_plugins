using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoMode.Conversation;

internal sealed class DialogueChecks
{
    private readonly KksNormalConversationRules _rules;
    private readonly KksNormalConversationRules.Settings _settings;
    private readonly KksNormalConversationRules.Context _context;
    private KksConversationLayout.BranchInfo _sampleBranch;
    private KksConversationLayout.SelectInfo _sampleSelect;
    private int _programs, _routes, _cueCount, _selections, _selectors;
    internal DialogueChecks(KksNormalConversationRules.Settings settings)
    {
        _settings = settings; _rules = new KksNormalConversationRules(settings);
        _context = new KksNormalConversationRules.Context
        {
            Temperament = new byte[settings.temperamentCount],
            Preferences = new int[settings.preferenceBranches.Max(rule => rule.axis) + settings.one],
            BodyCategory = _ => settings.zero
        };
    }
    internal void Check(KksConversationLayout.BasicInfo block)
    {
        if (block is KksConversationLayout.GenericInfo generic)
        {
            var program = KksDialogueProgram.Generic(generic);
            Require(program.Steps.OfType<KksDialogueProgram.Cue>().Select(cue => cue.Source).SequenceEqual(generic.talk), "Generic rows reordered");
            var result = Execute(program, _settings.zero);
            Require(ReferenceEquals(result.Source, generic.talk.First()) && result.Topic == generic.talk.First().topic, "Generic effects taken from wrong row");
        }
        else if (block is KksConversationLayout.BranchInfo branch)
        {
            _sampleBranch ??= branch;
            var responses = branch.success.Concat(new[] { branch.failure }).Concat(branch.noInterested == null ? Array.Empty<KksConversationLayout.Row>() : new[] { branch.noInterested }).ToArray();
            int index = _settings.zero;
            foreach (var response in responses)
            {
                if (response != null)
                {
                    var program = KksDialogueProgram.Branch(branch, index, _settings.zero);
                    Require(ReferenceEquals(program.Steps.OfType<KksDialogueProgram.Cue>().Last().Source, response), "Branch lost shared response");
                    Require(ReferenceEquals(Execute(program, _settings.zero).Source, response), "Branch effects taken from introduction");
                    _routes++;
                }
                index++;
            }
            var selected = _rules.Branch(branch, _context, weights => weights.ToList().FindIndex(weight => weight > _settings.zero));
            Require(selected >= _settings.zero && selected < responses.Length && responses[selected] != null, "Native branch rules selected missing response");
        }
        else if (block is KksConversationLayout.SelectInfo select)
        {
            _sampleSelect ??= select;
            int correct = _settings.zero;
            foreach (var ignored in select.choice)
            {
                var labels = select.choice.Select((row, index) => "native option " + index).ToArray();
                var program = KksDialogueProgram.Select(select, new Dictionary<int, string[]> { { select.conditions, labels } }, correct, _settings.zero, _settings.fallbackChoice);
                var selection = program.Steps.OfType<KksDialogueProgram.Selection>().Single();
                Require(selection.Options.Select(option => option.Text).SequenceEqual(labels), "Selection labels lost");
                Require(program.Steps.OfType<KksDialogueProgram.Cue>().Single().Kind == KksDialogueProgram.RowKind.HeroineText, "Selection introduction speaker changed");
                int chosen = _settings.zero;
                foreach (var candidate in select.choice)
                {
                    var expected = select.failure == null || chosen == correct ? candidate : select.failure;
                    var result = Execute(program, chosen);
                    Require(ReferenceEquals(result.Source, expected) && result.SelectedIndex == chosen && result.IsCorrect == (chosen == correct), "Selection route/effects mismatch");
                    Require(result.Topic == _settings.zero, "Native default selection topic changed");
                    _routes++; chosen++;
                }
                correct++;
            }
            var fallback = KksDialogueProgram.Select(select, new Dictionary<int, string[]>(), _settings.zero, _settings.zero, _settings.fallbackChoice);
            Require(fallback.Steps.OfType<KksDialogueProgram.Selection>().Single().Options.All(option => option.Text == _settings.fallbackChoice), "Native fallback label changed");
            var correctIndex = _rules.CorrectSelection(select, _context);
            Require(correctIndex >= _settings.zero && correctIndex < select.choice.Length, "Native body category rule selected missing answer");
        }
    }
    internal void CheckSelector(KksConversationLayout layout)
    {
        foreach (var stage in layout.Groups)
        foreach (var group in stage.Value.Where(pair => pair.Key == KksConversationLayout.Group.Talk || pair.Key == KksConversationLayout.Group.Listen))
        {
            foreach (var command in group.Value)
            {
                var eligible = command.Value.Where(block => _rules.Eligible(group.Key, block.conditions, _context)).ToArray();
                if (!eligible.Any()) continue;
                var highest = eligible.Max(block => block.priority);
                var selector = new KksConversationSelector(_rules);
                var first = selector.Select(layout, stage.Key, group.Key, command.Key, _context, count => _settings.zero);
                var second = selector.Select(layout, stage.Key, group.Key, command.Key, _context, count => _settings.zero);
                Require(first.priority == highest && second.priority == highest, "Selector ignored native priority");
                if (eligible.Count(block => block.priority == highest) > _settings.one) Require(!ReferenceEquals(first, second), "Immediate repeat suppression lost");
                _selectors++;
            }
            if (group.Key == KksConversationLayout.Group.Listen && group.Value.Count > _settings.listenHistoryCapacity)
            {
                var selector = new KksConversationSelector(_rules); var recent = new Queue<int>();
                for (int turn = _settings.zero; turn <= _settings.listenHistoryCapacity; turn++)
                {
                    var block = selector.Select(layout, stage.Key, group.Key, null, _context, count => _settings.zero);
                    var key = group.Value.Single(command => command.Value.Contains(block)).Key;
                    Require(!recent.Contains(key), "Listen command repeated inside native history window");
                    recent.Enqueue(key); if (recent.Count > _settings.listenHistoryCapacity) recent.Dequeue();
                }
            }
        }
    }
    private KksDialogueProgram.Outcome Execute(KksDialogueProgram program, int choice)
    {
        var presentation = new Recorder { Synchronous = true, Choice = choice };
        using var machine = new KksDialogueMachine(presentation, _settings.zero);
        KksDialogueProgram.Outcome result = null; int completions = _settings.zero;
        machine.Start(program, outcome => { result = outcome; completions++; }, exception => throw new Exception("Dialogue execution failed", exception));
        Require(!machine.Active && completions == _settings.one && result != null, "Program did not complete exactly once");
        Require(presentation.Requests.All(request => request.Disposed), "Synchronous presentation lease leaked");
        _programs++; _cueCount += presentation.Cues.Count; _selections += presentation.SelectionCount;
        return result;
    }
    internal void CheckLifecycle()
    {
        Require(_sampleBranch != null && _sampleSelect != null, "Missing lifecycle fixtures");
        var program = KksDialogueProgram.Branch(_sampleBranch, _settings.zero, _settings.zero);
        var recorder = new Recorder(); using var machine = new KksDialogueMachine(recorder, _settings.zero);
        int outcomes = _settings.zero, failures = _settings.zero;
        machine.Start(program, _ => outcomes++, _ => failures++);
        var old = recorder.Cues.Last().Complete; old();
        var requestCount = recorder.Requests.Count; old();
        Require(recorder.Requests.Count == requestCount && outcomes == _settings.zero, "Duplicate row completion advanced twice");
        var interrupted = recorder.Cues.Last().Complete; machine.Stop(); interrupted();
        Require(!machine.Active && outcomes == _settings.zero, "Interrupted dialogue committed effects");
        machine.Start(program, _ => outcomes++, _ => failures++);
        var freshCount = recorder.Requests.Count; interrupted();
        Require(recorder.Requests.Count == freshCount && machine.Waiting, "Old callback entered a new conversation");
        recorder.Cues.Last().Complete(); recorder.Cues.Last().Complete(); recorder.Cues.Last().Complete();
        Require(outcomes == _settings.one && failures == _settings.zero && !machine.Active, "Native outcome was not committed exactly once");
        Require(recorder.Requests.All(request => request.Disposed), "Async presentation lease leaked");

        var selection = KksDialogueProgram.Select(_sampleSelect, null, _settings.zero, _settings.zero, _settings.fallbackChoice);
        machine.Start(selection, _ => outcomes++, _ => failures++); recorder.Cues.Last().Complete();
        var choose = recorder.Selected; choose(_sampleSelect.choice.Length);
        Require(!machine.Active && failures == _settings.one, "Out-of-range answer was accepted");
        choose(_settings.zero); Require(outcomes == _settings.one, "Stale answer committed an outcome");

        var answering = new Recorder(); using var answerMachine = new KksDialogueMachine(answering, _settings.zero);
        int answers = _settings.zero;
        answerMachine.Start(selection, _ => answers++, exception => throw exception);
        answering.Cues.Last().Complete(); var answer = answering.Selected;
        answer(_settings.zero); var afterAnswer = answering.Requests.Count; answer(_settings.zero);
        Require(answering.Requests.Count == afterAnswer && answers == _settings.zero, "Double answer entered its response twice");
        answering.Cues.Last().Complete(); answer(_settings.zero);
        Require(answers == _settings.one && !answerMachine.Active, "Answer committed twice after response completion");

        recorder.ThrowOnce = true;
        machine.Start(program, _ => outcomes++, _ => machine.Start(program, _ => outcomes++, _ => failures++));
        Require(machine.Active && machine.Waiting, "Recovery started from failure callback did not resume");
        machine.Stop();

        var sync = new Recorder { Synchronous = true };
        using var effectFailure = new KksDialogueMachine(sync, _settings.zero);
        var before = failures;
        effectFailure.Start(program, _ => throw new IOException("Simulated save failure"), _ => failures++);
        Require(failures == before + _settings.one, "Outcome persistence exception was swallowed");
        CheckRuleContexts();
        var bundles = new KksVoiceBundleContext();
        Require(bundles.Resolve(_settings.zero, string.Empty) == string.Empty, "Unset voice bundle was invented");
        bundles.Resolve(_settings.zero, "native dialogue"); bundles.Resolve(_settings.one, "another personality");
        Require(bundles.Resolve(_settings.zero, string.Empty) == "native dialogue", "Voice bundle did not inherit per personality");
        bundles.Resolve(_settings.zero, "call name");
        Require(bundles.Resolve(_settings.zero, null) == "call name", "Call-name bundle did not update native voice context");
        Require(bundles.Resolve(_settings.one, string.Empty) == "another personality", "Voice bundle leaked across personalities");
        var nextScenario = new KksVoiceBundleContext();
        Require(nextScenario.Resolve(_settings.zero, string.Empty) == string.Empty, "Voice bundle leaked into a new scenario host");
        bundles.Reset(); Require(bundles.Resolve(_settings.zero, string.Empty) == string.Empty, "Voice bundle survived explicit host disposal");
    }
    private void CheckRuleContexts()
    {
        var context = new KksNormalConversationRules.Context
        {
            Preferences = new int[_context.Preferences.Length], Temperament = new byte[_settings.temperamentCount]
        };
        Require(!_rules.Eligible(KksConversationLayout.Group.Talk, _settings.disabledSpeakCondition, context), "Disabled speak condition became eligible");
        Require(_rules.Eligible(KksConversationLayout.Group.Talk, _settings.nicknameCondition, context), "First nickname route was hidden");
        Require(!_rules.Eligible(KksConversationLayout.Group.Talk, _settings.nicknameEventCondition, context), "Nickname continuation ignored required event");
        context.Events.Add(_settings.requiredNicknameEvent);
        Require(!_rules.Eligible(KksConversationLayout.Group.Talk, _settings.nicknameCondition, context)
            && _rules.Eligible(KksConversationLayout.Group.Talk, _settings.nicknameEventCondition, context), "Nickname event gates changed");
        context.NormalNickname = true;
        Require(!_rules.Eligible(KksConversationLayout.Group.Talk, _settings.nicknameCondition, context)
            && !_rules.Eligible(KksConversationLayout.Group.Talk, _settings.nicknameEventCondition, context), "Normal nickname did not close nickname routes");
        KksConversationLayout.BranchInfo Branch(int condition) => new KksConversationLayout.BranchInfo(_sampleBranch.row)
        {
            conditions = condition, introduction = _sampleBranch.introduction, success = _sampleBranch.success,
            failure = _sampleBranch.failure, noInterested = _sampleBranch.noInterested
        };
        foreach (var rule in _settings.attributeBranches)
        {
            IReadOnlyList<int> weights = null;
            int Weighted(IReadOnlyList<int> input) { weights = input; return _settings.zero; }
            context.Attribute = rule.attribute; _rules.Branch(Branch(rule.condition), context, Weighted);
            Require(weights.SequenceEqual(rule.matching), "Matching card attribute used wrong branch probabilities");
            context.Attribute += _settings.one; _rules.Branch(Branch(rule.condition), context, Weighted);
            Require(weights.SequenceEqual(rule.other), "Other card attribute used matching branch probabilities");
        }
        foreach (var rule in _settings.preferenceBranches)
        {
            context.Preferences[rule.axis] = rule.boundary;
            Require(_rules.Branch(Branch(rule.condition), context, _ => throw new Exception("Preference branch rolled random")) == rule.matched, "Preference boundary lost inclusivity");
            context.Preferences[rule.axis] = rule.boundary + (rule.lessOrEqual ? _settings.one : -_settings.one);
            Require(_rules.Branch(Branch(rule.condition), context, _ => throw new Exception("Preference branch rolled random")) == rule.other, "Preference outer boundary chose wrong response");
        }
        foreach (var rule in _settings.bodyCategorySelections)
        {
            var block = new KksConversationLayout.SelectInfo(_sampleSelect.row, rule.answers.Length) { conditions = rule.condition };
            int category = _settings.zero;
            foreach (var expected in rule.answers)
            {
                context.BodyCategory = name => { Require(name == rule.category, "Selection used wrong body category"); return category; };
                Require(_rules.CorrectSelection(block, context) == expected, "Body category answer order changed"); category++;
            }
        }
    }
    internal void Report(string directory)
    {
        var report = new { scope = "Pure native table composition, rules and callback lifecycle; no Unity playback proof", programs = _programs,
            routes = _routes, cues = _cueCount, selections = _selections, selectorCommands = _selectors,
            callbackLifecyclePassed = true, cardAndPreferenceBoundaryCasesPassed = true };
        File.WriteAllText(Path.Combine(directory, "dialogue-checks.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"PASS dialoguePrograms={_programs} routes={_routes} cues={_cueCount} selections={_selections} selectorCommands={_selectors} callbackLifecycle=true");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private sealed class Ticket : IDisposable
    {
        internal bool Disposed;
        public void Dispose() { Require(!Disposed, "Presentation lease disposed twice"); Disposed = true; }
    }
    private sealed class CueRequest
    { internal KksDialogueProgram.Cue Cue; internal Action Complete; }
    private sealed class Recorder : KksDialogueMachine.IPresentation
    {
        internal bool Synchronous, ThrowOnce;
        internal int Choice, SelectionCount;
        internal Action<int> Selected;
        internal readonly List<Ticket> Requests = new List<Ticket>();
        internal readonly List<CueRequest> Cues = new List<CueRequest>();
        public IDisposable Present(KksDialogueProgram.Cue cue, Action completed)
        {
            if (ThrowOnce) { ThrowOnce = false; throw new IOException("Simulated presentation failure"); }
            var ticket = new Ticket(); Requests.Add(ticket); Cues.Add(new CueRequest { Cue = cue, Complete = completed });
            if (Synchronous) completed(); return ticket;
        }
        public IDisposable Select(IReadOnlyList<string> labels, Action<int> selected)
        {
            var ticket = new Ticket(); Requests.Add(ticket); Selected = selected; SelectionCount++;
            if (Synchronous) selected(Choice); return ticket;
        }
    }
}
