using System;
using System.Collections.Generic;
using System.Linq;

namespace MainGameAiShoujoMode.Conversation
{
    // Each presentation request owns one wait. Completion is accepted once for that exact request.
    internal sealed class KksDialogueMachine : IDisposable
    {
        internal interface IPresentation
        {
            IDisposable Present(KksDialogueProgram.Cue cue, Action completed);
            IDisposable Select(IReadOnlyList<string> labels, Action<int> selected);
        }
        private readonly LinkedList<KksDialogueProgram.Step> _pending = new LinkedList<KksDialogueProgram.Step>();
        private readonly IPresentation _presentation;
        private readonly int _zero;
        private Action<KksDialogueProgram.Outcome> _completed;
        private Action<Exception> _failed;
        private object _run, _wait;
        private IDisposable _lease;
        private bool _pumping, _disposed;
        internal bool Active => _run != null;
        internal bool Waiting => _wait != null;
        internal KksDialogueMachine(IPresentation presentation, int zero)
        { _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation)); _zero = zero; }

        internal void Start(KksDialogueProgram program, Action<KksDialogueProgram.Outcome> completed, Action<Exception> failed)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KksDialogueMachine));
            if (program == null) throw new ArgumentNullException(nameof(program));
            Stop(); _run = new object(); _completed = completed; _failed = failed;
            foreach (var step in program.Steps) _pending.AddLast(step);
            Pump();
        }
        private void Pump()
        {
            if (_pumping) return;
            _pumping = true;
            try
            {
                while (Active && _wait == null)
                {
                    if (_pending.First == null) throw new InvalidOperationException("KKS dialogue ended without a native outcome.");
                    var step = _pending.First.Value; _pending.RemoveFirst();
                    if (step is KksDialogueProgram.Outcome outcome)
                    {
                        var completed = _completed; var failed = _failed;
                        try { Stop(); completed?.Invoke(outcome); }
                        catch (Exception exception) { failed?.Invoke(exception); }
                    }
                    else if (step is KksDialogueProgram.Cue cue)
                    {
                        var wait = new object(); var run = _run; _wait = wait;
                        var lease = _presentation.Present(cue, () => Resume(run, wait, null));
                        Adopt(run, wait, lease);
                    }
                    else if (step is KksDialogueProgram.Selection selection)
                    {
                        var wait = new object(); var run = _run; _wait = wait;
                        var lease = _presentation.Select(selection.Options.Select(option => option.Text).ToArray(), index =>
                        {
                            if (!ReferenceEquals(_run, run) || !ReferenceEquals(_wait, wait)) return;
                            if (index < _zero || index >= selection.Options.Count) { Fail(new ArgumentOutOfRangeException(nameof(index))); return; }
                            Resume(run, wait, selection.Options[index].Route);
                        });
                        Adopt(run, wait, lease);
                    }
                    else throw new NotSupportedException("Unknown KKS dialogue step.");
                }
            }
            catch (Exception exception) { Fail(exception); }
            finally { _pumping = false; }
            // A failure handler may have started a replacement run during the pump.
            if (Active && _wait == null) Pump();
        }
        private void Adopt(object run, object wait, IDisposable lease)
        {
            // A synchronous completion may already have moved to the next step.
            if (ReferenceEquals(_run, run) && ReferenceEquals(_wait, wait)) _lease = lease;
            else lease?.Dispose();
        }
        private void Resume(object run, object wait, KksDialogueProgram route)
        {
            if (!ReferenceEquals(_run, run) || !ReferenceEquals(_wait, wait)) return;
            _wait = null;
            var lease = _lease; _lease = null;
            try
            {
                lease?.Dispose();
                if (!ReferenceEquals(_run, run)) return;
                if (route != null) foreach (var step in route.Steps.Reverse()) _pending.AddFirst(step);
                Pump();
            }
            catch (Exception exception) { Fail(exception); }
        }
        private void Fail(Exception exception)
        {
            var failed = _failed;
            try { Stop(); }
            catch (Exception cleanup) { exception = new AggregateException(exception, cleanup); }
            failed?.Invoke(exception);
        }
        internal void Stop()
        {
            _run = null; _wait = null; _pending.Clear(); _completed = null; _failed = null;
            var lease = _lease; _lease = null; lease?.Dispose();
        }
        public void Dispose() { if (_disposed) return; _disposed = true; Stop(); }
    }
}
