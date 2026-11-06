using System;
using System.Linq;

namespace MainGameAiShoujoMode.Conversation
{
    internal sealed class KksAdvCrossFadeHost : KksAdvMachine.IHost
    {
        internal interface IOperation : IDisposable { void End(bool processEnd); }
        internal interface IBackend { IOperation Start(float duration, Action completed, Action<Exception> failed); }
        private sealed class Command : KksAdvMachine.ICommand
        {
            private readonly KksAdvCrossFadeHost _owner;
            private readonly KksAdvScript.Row _row;
            private IOperation _operation;
            private bool _ended;
            internal Command(KksAdvCrossFadeHost owner, KksAdvScript.Row row) { _owner = owner; _row = row; }
            public void Start(Action completed, Action<Exception> failed)
            {
                var duration = float.Parse(_row.InitializeArguments(new ADV.Commands.CameraEffect.CrossFade().ArgsDefault).Select(_owner._replace).First());
                var operation = _owner._backend.Start(duration, completed, failed);
                if (_ended) { try { operation.End(false); } finally { operation.Dispose(); } } else _operation = operation;
            }
            public void End(bool processEnd)
            {
                if (_ended) return; _ended = true; var operation = _operation; _operation = null;
                if (operation == null) return;
                try { operation.End(processEnd); } finally { operation.Dispose(); }
            }
        }
        private readonly KksAdvMachine.IHost _next;
        private readonly IBackend _backend;
        private readonly Func<string, string> _replace;
        internal KksAdvCrossFadeHost(KksAdvMachine.IHost next, IBackend backend, Func<string, string> replace)
        { _next = next ?? throw new ArgumentNullException(nameof(next)); _backend = backend ?? throw new ArgumentNullException(nameof(backend)); _replace = replace ?? throw new ArgumentNullException(nameof(replace)); }
        public bool IsRegulated => _next.IsRegulated;
        public bool TextComplete => _next.TextComplete;
        public void CompleteText() => _next.CompleteText();
        public void ClearText() => _next.ClearText();
        public void ClearCurrentCharaData() => _next.ClearCurrentCharaData();
        public KksAdvMachine.ICommand Create(KksAdvMachine.Context context) => context.Entry.Row.Command == "CrossFade" ? new Command(this, context.Entry.Row) : _next.Create(context);
    }
}
