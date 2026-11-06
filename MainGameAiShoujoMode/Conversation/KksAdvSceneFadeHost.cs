using System;
using System.Linq;

namespace MainGameAiShoujoMode.Conversation
{
    internal sealed class KksAdvSceneFadeHost : KksAdvMachine.IHost
    {
        internal interface IOperation : IDisposable { void Finish(); }
        internal interface IBackend
        {
            bool IsFading { get; }
            IOperation StartFade(bool fadeIn, Action completed, Action<Exception> failed);
            void SetTime(float duration);
            void SetColor(string color);
        }
        private sealed class Command : KksAdvMachine.ICommand
        {
            private readonly KksAdvSceneFadeHost _owner;
            private readonly KksAdvScript.Row _row;
            private IOperation _operation;
            private bool _ended;
            internal Command(KksAdvSceneFadeHost owner, KksAdvScript.Row row) { _owner = owner; _row = row; }
            public void Start(Action completed, Action<Exception> failed)
            {
                using (var cells = _row.InitializeArguments(new ADV.Commands.Effect.SceneFade().ArgsDefault).Select(_owner._replace).GetEnumerator())
                {
                    cells.MoveNext(); var fadeIn = string.Equals(cells.Current, "in", StringComparison.OrdinalIgnoreCase);
                    var operation = _owner._backend.StartFade(fadeIn, completed, failed);
                    if (_ended) { try { operation.Finish(); } finally { operation.Dispose(); } return; }
                    _operation = operation;
                    // Original command starts the transition BEFORE parsing/applying optional time and color.
                    cells.MoveNext(); if (!string.IsNullOrEmpty(cells.Current)) _owner._backend.SetTime(float.Parse(cells.Current));
                    cells.MoveNext(); if (!string.IsNullOrEmpty(cells.Current)) _owner._backend.SetColor(cells.Current);
                    // Bundle/Asset and any extra cells are unused by the native command.
                }
            }
            public void End(bool processEnd)
            {
                if (_ended) return; _ended = true;
                var operation = _operation; _operation = null;
                if (operation == null) return;
                try { operation.Finish(); } finally { operation.Dispose(); }
            }
        }
        private sealed class SetRegulate : KksAdvMachine.ICommand
        {
            private readonly Action _set;
            internal SetRegulate(Action set) { _set = set; }
            public void Start(Action completed, Action<Exception> failed) { _set(); completed(); }
            public void End(bool processEnd) { }
        }
        private readonly KksAdvMachine.IHost _next;
        private readonly IBackend _backend;
        private readonly Func<string, string> _replace;
        private readonly Action _changed;
        private bool _regulate = true;
        internal KksAdvSceneFadeHost(KksAdvMachine.IHost next, IBackend backend, Func<string, string> replace, Action changed)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next)); _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _replace = replace ?? throw new ArgumentNullException(nameof(replace)); _changed = changed;
        }
        public bool IsRegulated => _next.IsRegulated || (_regulate && _backend.IsFading);
        public bool TextComplete => _next.TextComplete;
        public void CompleteText() => _next.CompleteText();
        public void ClearText() => _next.ClearText();
        public void ClearCurrentCharaData() => _next.ClearCurrentCharaData();
        public KksAdvMachine.ICommand Create(KksAdvMachine.Context context)
        {
            switch (context.Entry.Row.Command)
            {
                case "SceneFade": return new Command(this, context.Entry.Row);
                case "SceneFadeRegulate": return new SetRegulate(() =>
                {
                    _regulate = bool.Parse(context.Entry.Row.InitializeArguments(new ADV.Commands.Effect.SceneFadeRegulate().ArgsDefault).Select(_replace).First());
                    _changed?.Invoke();
                });
                default: return _next.Create(context);
            }
        }
    }
}
