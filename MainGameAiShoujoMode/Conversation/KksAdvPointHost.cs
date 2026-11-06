using System;
using System.Linq;

namespace MainGameAiShoujoMode.Conversation
{
    internal sealed class KksAdvPointHost : KksAdvMachine.IHost
    {
        internal interface IBackend
        {
            IDisposable Add(KksConversationStateStore.PointChange change, Action completed, Action<Exception> failed);
        }
        private sealed class Command : KksAdvMachine.ICommand
        {
            private readonly KksAdvMachine.Context _context;
            private readonly KksAdvPointHost _owner;
            private readonly string _id = Guid.NewGuid().ToString("N");
            private IDisposable _wait;
            private bool _started, _ended;
            internal Command(KksAdvPointHost owner, KksAdvMachine.Context context) { _owner = owner; _context = context; }
            public void Start(Action completed, Action<Exception> failed)
            {
                if (_started || _ended) throw new InvalidOperationException("Point command cannot start twice."); _started = true;
                var delta = int.Parse(_context.Entry.Row.InitializeArguments(new ADV.Commands.Game.KoikatsuPointAdd().ArgsDefault).Select(_owner._replace).First());
                var wait = _owner._backend.Add(new KksConversationStateStore.PointChange
                {
                    id = _id, bundle = _context.Entry.Bundle, asset = _context.Entry.Asset, sourceRow = _context.Entry.SourceRow, delta = delta
                }, completed, failed);
                if (_ended) wait?.Dispose(); else _wait = wait;
            }
            public void End(bool processEnd) { if (_ended) return; _ended = true; var wait = _wait; _wait = null; wait?.Dispose(); }
        }
        private readonly KksAdvMachine.IHost _next;
        private readonly IBackend _backend;
        private readonly Func<string, string> _replace;
        internal KksAdvPointHost(KksAdvMachine.IHost next, IBackend backend, Func<string, string> replace)
        { _next = next ?? throw new ArgumentNullException(nameof(next)); _backend = backend ?? throw new ArgumentNullException(nameof(backend)); _replace = replace ?? throw new ArgumentNullException(nameof(replace)); }
        public bool IsRegulated => _next.IsRegulated;
        public bool TextComplete => _next.TextComplete;
        public void CompleteText() => _next.CompleteText();
        public void ClearText() => _next.ClearText();
        public void ClearCurrentCharaData() => _next.ClearCurrentCharaData();
        public KksAdvMachine.ICommand Create(KksAdvMachine.Context context) => context.Entry.Row.Command == "KoikatsuPointAdd" ? new Command(this, context) : _next.Create(context);
    }
}
