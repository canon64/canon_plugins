using System;
using System.Linq;

namespace MainGameAiShoujoMode.Conversation
{
    internal sealed class KksAdvTimedHost : KksAdvMachine.IHost
    {
        private sealed class WaitCommand : KksAdvMachine.ICommand
        {
            private readonly KksAdvProcessClock _clock;
            private readonly double _duration;
            private IDisposable _wait;
            private bool _started, _ended;
            internal WaitCommand(KksAdvProcessClock clock, double duration) { _clock = clock; _duration = duration; }
            public void Start(Action completed, Action<Exception> failed)
            {
                if (_started || _ended) throw new InvalidOperationException("KKS ADV wait cannot be started twice.");
                _started = true;
                var wait = _clock.Wait(_duration, completed, failed);
                if (_ended) wait.Dispose(); else _wait = wait;
            }
            public void End(bool processEnd) { if (_ended) return; _ended = true; var wait = _wait; _wait = null; wait?.Dispose(); }
        }
        private readonly KksAdvMachine.IHost _next;
        private readonly KksAdvProcessClock _clock;
        private readonly Func<string, string> _replaceVariable;
        private readonly int _zero;
        internal KksAdvTimedHost(KksAdvMachine.IHost next, KksAdvProcessClock clock, Func<string, string> replaceVariable, int zero)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next)); _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _replaceVariable = replaceVariable ?? throw new ArgumentNullException(nameof(replaceVariable)); _zero = zero;
        }
        public bool IsRegulated => _next.IsRegulated;
        public bool TextComplete => _next.TextComplete;
        public void CompleteText() => _next.CompleteText();
        public void ClearText() => _next.ClearText();
        public void ClearCurrentCharaData() => _next.ClearCurrentCharaData();
        public KksAdvMachine.ICommand Create(KksAdvMachine.Context context)
        {
            if (context.Entry.Row.Command != "Wait" && context.Entry.Row.Command != "WaitAbs") return _next.Create(context);
            var args = context.Entry.Row.InitializeArguments(new[] { _zero.ToString() }).Select(_replaceVariable).ToArray();
            float.TryParse(args[_zero], out var duration);
            _clock.SetSuspended(_next.IsRegulated);
            return new WaitCommand(_clock, duration);
        }
    }
}
