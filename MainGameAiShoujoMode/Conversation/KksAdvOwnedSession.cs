using System;
using System.Collections.Generic;

namespace MainGameAiShoujoMode.Conversation
{
    internal interface IKksAdvSession : IDisposable
    {
        IDisposable Open(string bundle, string asset, Action completed, Action<Exception> failed);
        void Advance();
    }
    // The parent scope owns shared rendering/actors/CG resources. A run scope owns its machine and waits.
    internal sealed class KksAdvOwnedSession : IKksAdvSession
    {
        private sealed class Run : IDisposable
        {
            internal KksAdvOwnedSession Owner;
            internal readonly KksAdvSessionScope Scope = new KksAdvSessionScope();
            internal KksAdvMachine Machine;
            internal object Token;
            internal Action Completed;
            internal Action<Exception> Failed;
            internal bool Ended;
            public void Dispose() => Owner.Cancel(this);
        }
        private readonly Func<string, string, KksAdvScript> _load;
        private readonly Func<KksAdvSessionScope, KksAdvScript, KksAdvMachine> _createMachine;
        private readonly KksAdvSessionScope _lifetime;
        private Run _active;
        private object _transition;
        private bool _disposed;
        internal bool Active => _active != null;
        internal KksAdvMachine CurrentMachine => _active?.Machine;
        // Factory owns dependencies in the supplied run scope; this session adopts its returned machine last.
        internal KksAdvOwnedSession(Func<string, string, KksAdvScript> load, Func<KksAdvSessionScope, KksAdvMachine> createMachine, KksAdvSessionScope lifetime)
            : this(load, (scope, script) => createMachine(scope), lifetime)
        { if (createMachine == null) throw new ArgumentNullException(nameof(createMachine)); }
        internal KksAdvOwnedSession(Func<string, string, KksAdvScript> load, Func<KksAdvSessionScope, KksAdvScript, KksAdvMachine> createMachine, KksAdvSessionScope lifetime)
        {
            _load = load ?? throw new ArgumentNullException(nameof(load)); _createMachine = createMachine ?? throw new ArgumentNullException(nameof(createMachine));
            _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
        }
        public IDisposable Open(string bundle, string asset, Action completed, Action<Exception> failed)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KksAdvOwnedSession));
            var token = new object(); _transition = token;
            var run = new Run { Owner = this, Token = token, Completed = completed, Failed = failed };
            var previous = _active; _active = null;
            try { Close(previous); }
            catch (Exception exception)
            {
                run.Ended = true;
                if (ReferenceEquals(_transition, token) && !_disposed) failed?.Invoke(exception);
                return run;
            }
            if (!ReferenceEquals(_transition, token) || _disposed) { run.Ended = true; return run; }
            _active = run;
            try
            {
                var script = _load(bundle, asset);
                if (!ReferenceEquals(_active, run)) return run;
                var machine = _createMachine(run.Scope, script) ?? throw new InvalidOperationException("ADV runtime factory returned no machine.");
                run.Machine = run.Scope.Own(machine);
                if (!ReferenceEquals(_active, run)) return run;
                machine.Start(script, () => Finish(run, null), exception => Finish(run, exception));
            }
            catch (Exception exception) { Finish(run, exception); }
            return run;
        }
        public void Advance() => Advance(KksAdvMachine.AdvanceSource.Manual);
        internal void Advance(KksAdvMachine.AdvanceSource source) => _active?.Machine?.Advance(source);
        internal void NotifyPresentationChanged() => _active?.Machine?.NotifyPresentationChanged();
        private static void Close(Run run)
        {
            if (run == null || run.Ended) return;
            run.Ended = true; run.Machine = null; run.Scope.Dispose();
        }
        private void Cancel(Run run)
        {
            if (run.Ended) return;
            if (ReferenceEquals(_active, run)) { _active = null; _transition = new object(); }
            Close(run);
        }
        private void Finish(Run run, Exception error)
        {
            if (!ReferenceEquals(_active, run) || run.Ended) return;
            _active = null;
            try { Close(run); } catch (Exception cleanup) { error = error == null ? cleanup : new AggregateException(error, cleanup); }
            if (_disposed || !ReferenceEquals(_transition, run.Token)) return;
            if (error != null) { run.Failed?.Invoke(error); return; }
            try { run.Completed?.Invoke(); }
            catch (Exception exception)
            { if (!_disposed && ReferenceEquals(_transition, run.Token)) run.Failed?.Invoke(exception); }
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true; _transition = new object();
            var run = _active; _active = null; var errors = new List<Exception>();
            try { Close(run); } catch (Exception exception) { errors.Add(exception); }
            try { _lifetime.Dispose(); } catch (Exception exception) { errors.Add(exception); }
            if (errors.Count != default(int)) throw new AggregateException("ADV parent session cleanup failed.", errors);
        }
    }
}
