using System;
using System.Collections.Generic;
using System.Linq;

namespace MainGameAiShoujoMode.Conversation
{
    // The same host is used with native KKS objects and managed test backends.
    // Queued media commands only prepare records; Text starts the corresponding choreography.
    internal sealed class KksAdvMediaHost : KksAdvMachine.IHost
    {
        internal interface IBackend : KksAdvMachine.IHost
        {
            void ResolveVoice(ADV.Commands.Base.Voice.Data voice);
            Manager.Game.Expression FindExpression(int actor, string name);
            string[] FindMotionAlias(string name);
            void ApplyExpressions(IReadOnlyList<ADV.Commands.Base.Expression.Data> expressions);
            void ApplyMotions(IReadOnlyList<ADV.Commands.Base.Motion.Data> motions);
            IDisposable WaitMotions(IReadOnlyList<ADV.Commands.Base.Motion.Data> motions, Action completed, Action<Exception> failed);
            IDisposable PlayVoices(IReadOnlyList<ADV.Commands.Base.Voice.Data[]> voices, Action changed, Action completed, Action<Exception> failed);
            void PresentText(string colorKey, string name, string text, IReadOnlyList<ADV.Commands.Base.Voice.Data[]> voices);
        }
        private sealed class Immediate : KksAdvMachine.ICommand
        {
            private readonly Action _action;
            internal Immediate(Action action) { _action = action; }
            public void Start(Action completed, Action<Exception> failed) { _action(); completed(); }
            public void End(bool processEnd) { }
        }
        private sealed class DeferredText : KksAdvMachine.ICommand
        {
            private readonly Func<KksAdvTextPlayback> _begin;
            private KksAdvTextPlayback _playback;
            private bool _ended;
            internal DeferredText(Func<KksAdvTextPlayback> begin) { _begin = begin; }
            public void Start(Action completed, Action<Exception> failed)
            {
                if (_ended) throw new InvalidOperationException("KKS ADV text was canceled before start.");
                _playback = _begin();
                if (_ended) _playback.End(false); else _playback.Start(completed, failed);
            }
            public void End(bool processEnd) { if (_ended) return; _ended = true; _playback?.End(processEnd); }
        }
        private readonly IBackend _backend;
        private readonly KksAdvMediaArguments _arguments;
        private readonly Func<string, string> _replaceVariable;
        private readonly Func<string, bool, string> _replaceText;
        private readonly List<ADV.Commands.Base.Voice.Data[]> _voices = new List<ADV.Commands.Base.Voice.Data[]>();
        private readonly List<ADV.Commands.Base.Expression.Data[]> _expressions = new List<ADV.Commands.Base.Expression.Data[]>();
        private readonly List<ADV.Commands.Base.Motion.Data[]> _motions = new List<ADV.Commands.Base.Motion.Data[]>();
        private readonly int _zero, _one;
        internal KksAdvMediaHost(IBackend backend, Func<string, string> replaceVariable, Func<string, bool, string> replaceText, int zero, int one)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _replaceVariable = replaceVariable ?? throw new ArgumentNullException(nameof(replaceVariable));
            _replaceText = replaceText ?? throw new ArgumentNullException(nameof(replaceText));
            _zero = zero; _one = one; _arguments = new KksAdvMediaArguments(zero, one);
        }
        public bool IsRegulated => _backend.IsRegulated;
        public bool TextComplete => _backend.TextComplete;
        public void CompleteText() => _backend.CompleteText();
        public void ClearText() => _backend.ClearText();
        public void ClearCurrentCharaData()
        {
            _voices.Clear(); _expressions.Clear(); _motions.Clear();
            // Voice bundle inheritance is deliberately owned by the backend, not cleared here.
            _backend.ClearCurrentCharaData();
        }
        public KksAdvMachine.ICommand Create(KksAdvMachine.Context context)
        {
            var row = context.Entry.Row;
            switch (row.Command)
            {
                case "Voice": return new Immediate(() =>
                {
                    var group = _arguments.Voices(row, _replaceVariable).ToArray();
                    foreach (var voice in group) _backend.ResolveVoice(voice);
                    _voices.Add(group);
                });
                case "Expression": return new Immediate(() => _expressions.Add(ReadExpressions(row)));
                case "Motion": return new Immediate(() => _motions.Add(ReadMotions(row)));
                case "CharaExpression": return new Immediate(() => _backend.ApplyExpressions(ReadExpressions(row)));
                case "CharaMotion": return new Immediate(() => _backend.ApplyMotions(ReadMotions(row)));
                case "TextClear": return new Immediate(_backend.ClearText);
                case "Text": return new DeferredText(() => BeginText(context));
                default: return _backend.Create(context);
            }
        }
        private ADV.Commands.Base.Expression.Data[] ReadExpressions(KksAdvScript.Row row) => _arguments.Expressions(row, _replaceVariable, _backend.FindExpression).ToArray();
        private ADV.Commands.Base.Motion.Data[] ReadMotions(KksAdvScript.Row row) => _arguments.Motions(row, _replaceVariable, _backend.FindMotionAlias).ToArray();
        private KksAdvTextPlayback BeginText(KksAdvMachine.Context context)
        {
            var voices = _voices.ToArray(); var expressions = _expressions.ToArray(); var motions = _motions.ToArray();
            var text = new ADV.Commands.Base.Text.Data(context.Entry.Row.InitializeArguments(null).Select(_replaceVariable).ToArray());
            var performances = new List<KksAdvTextPlayback.Performance>();
            for (var index = _zero; index < Math.Max(expressions.Length, motions.Length); index++)
            {
                var expression = index < expressions.Length ? expressions[index] : null;
                var motion = index < motions.Length ? motions[index] : null;
                var hasMotion = motion != null && motion.Any();
                performances.Add(new KksAdvTextPlayback.Performance(
                    expression == null || !expression.Any() ? null : () => _backend.ApplyExpressions(expression), null,
                    hasMotion ? () => _backend.ApplyMotions(motion) : null,
                    hasMotion ? (completed, failed) => _backend.WaitMotions(motion, completed, failed) : null));
            }
            var playback = new KksAdvTextPlayback(performances, motions.Length,
                voices.Any() ? (changed, completed, failed) => _backend.PlayVoices(voices, changed, completed, failed) : null,
                () => !context.IsCurrent, _zero, _one);
            _backend.PresentText(text.colorKey, _replaceText(text.name, false), _replaceText(text.text, true), voices);
            return playback;
        }
    }
}
