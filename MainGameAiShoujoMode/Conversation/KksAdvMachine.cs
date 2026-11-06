using System;
using System.Collections.Generic;
using System.Linq;

namespace MainGameAiShoujoMode.Conversation
{
    // Event-driven counterpart of CommandList.Process and MessageWindowProc.
    // Hosts notify completion once; the machine never polls Unity state or timers.
    internal sealed class KksAdvMachine : IDisposable
    {
        internal interface ICommand
        {
            void Start(Action completed, Action<Exception> failed);
            // Native xData.End uses true; ForceEnd (advance/cancellation) uses false.
            void End(bool processEnd);
        }
        internal interface IHost
        {
            // Create must not start an operation before its ownership is registered.
            ICommand Create(Context context);
            bool IsRegulated { get; }
            bool TextComplete { get; }
            void CompleteText();
            void ClearText();
            void ClearCurrentCharaData();
        }
        internal enum AdvanceSource { Manual, Skip, Auto }
        internal sealed class Context
        {
            private readonly KksAdvMachine _owner;
            private readonly Run _run;
            private readonly Slot _slot;
            internal KksAdvTape.Entry Entry { get; }
            internal int LocalLine { get; }
            internal string LoadBundle => _run.Tape.LoadBundle;
            internal bool IsCurrent => ReferenceEquals(_owner._run, _run) && ReferenceEquals(_run.Batch, _slot.Batch)
                && (!_slot.Ended || _slot.Finalizing);
            internal Context(KksAdvMachine owner, Run run, Slot slot, KksAdvTape.Entry entry, int localLine)
            { _owner = owner; _run = run; _slot = slot; Entry = entry; LocalLine = localLine; }
            internal bool Open(KksAdvScript script, bool clear, bool clearCheck, bool next)
            {
                if (!IsCurrent) return false;
                var loaded = _run.Tape.Load(script, clear, clearCheck);
                if (loaded && next) _run.NextRequested = true;
                return loaded;
            }
            internal void InsertParameters(KksAdvScript script) { if (IsCurrent) _run.Tape.InsertParameters(script); }
            internal bool Jump(string target, Func<string, string> replaceVariable)
            {
                if (!IsCurrent) return false;
                var found = _run.Tape.TryJump(target, LocalLine, replaceVariable);
                if (found && !target.Contains(':')) _run.NextRequested = true;
                return found;
            }
            internal void Close() { if (IsCurrent) _run.CloseRequested = true; }
        }
        internal sealed class Slot
        {
            internal object Batch;
            internal string Name, Location;
            internal ICommand Command;
            internal bool Completed, Ended, Finalizing;
        }
        internal sealed class Run
        {
            internal KksAdvTape Tape;
            internal readonly List<Slot> Slots = new List<Slot>();
            internal object Batch;
            internal bool NeedBatch = true, HasText, NextRequested, CloseRequested;
            internal AdvanceSource? Input;
            internal Action Completed;
            internal Action<Exception> Failed;
            internal Exception Error;
            internal Preparation Preparing;
        }
        internal sealed class Preparation
        {
            internal IDisposable Lease;
            internal bool Ready, Released;
        }
        private readonly IHost _host;
        private readonly int _zero, _one;
        private readonly Action<bool> _processingSuspended;
        private readonly Action _stateChanged;
        private readonly Func<IReadOnlyList<KksAdvTape.Entry>, Action, Action<Exception>, IDisposable> _prepareBatch;
        private Run _run;
        private object _transition;
        private bool _pumping, _disposed;
        internal bool Active => _run != null;
        internal bool HardWaiting => _run != null && (_run.Preparing != null || _run.Slots.Any(slot => !slot.Ended && IsHardWait(slot.Name)));
        internal bool HasText => _run != null && _run.HasText;

        internal KksAdvMachine(IHost host, int zero, int one, Action<bool> processingSuspended = null,
            Func<IReadOnlyList<KksAdvTape.Entry>, Action, Action<Exception>, IDisposable> prepareBatch = null, Action stateChanged = null)
        { _host = host ?? throw new ArgumentNullException(nameof(host)); _zero = zero; _one = one; _processingSuspended = processingSuspended; _prepareBatch = prepareBatch; _stateChanged = stateChanged; }
        internal void Start(KksAdvScript script, Action completed, Action<Exception> failed)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KksAdvMachine));
            if (script == null) throw new ArgumentNullException(nameof(script));
            var transition = new object(); _transition = transition;
            var previous = _run; _run = null;
            EndRemaining(previous);
            // A cleanup callback may have issued a newer Start or Stop.
            if (!ReferenceEquals(_transition, transition) || _disposed) return;
            var tape = new KksAdvTape(_zero, _one); tape.Load(script, true, true);
            _run = new Run { Tape = tape, Completed = completed, Failed = failed };
            Pump();
        }
        internal void Advance(AdvanceSource source = AdvanceSource.Manual)
        {
            var run = _run;
            if (run == null) return;
            try
            {
                var batch = run.Batch; var regulated = run.Preparing != null || _host.IsRegulated;
                _processingSuspended?.Invoke(regulated);
                if (!ReferenceEquals(_run, run) || !ReferenceEquals(run.Batch, batch) || regulated || HardWaiting) return;
                run.Input = source;
            }
            catch (Exception exception) { Fail(run, exception); }
            Pump();
        }
        // Called after text completion or a global presentation regulation changes.
        internal void NotifyPresentationChanged() => Pump();
        internal void PresentationFailed(Exception exception)
        { var run = _run; if (run != null) Fail(run, exception ?? new InvalidOperationException("ADV presentation failed without diagnostics.")); }

        private void Pump()
        {
            if (_pumping) return;
            _pumping = true;
            try
            {
                while (_run != null)
                {
                    var run = _run;
                    try { if (!Step(run)) break; }
                    catch (Exception exception) { Fail(run, exception); }
                }
            }
            finally
            {
                _pumping = false;
                // Command callbacks can arrive while a Multi batch is still being created.
                // Publish only its settled state, including asynchronous hard-wait completion.
                var run = _run;
                if (run != null)
                    try { _stateChanged?.Invoke(); }
                    catch (Exception exception) { Fail(run, exception); }
            }
        }
        private bool Step(Run run)
        {
            if (run.NeedBatch)
            {
                if (!PrepareBatch(run)) return !ReferenceEquals(_run, run);
                if (!ReferenceEquals(_run, run)) return true;
                BeginBatch(run);
            }
            if (!ReferenceEquals(_run, run)) return true;
            if (run.Error != null) throw run.Error;
            if (run.CloseRequested)
            {
                ProcessCompleted(run);
                if (ReferenceEquals(_run, run)) Finish(run);
                return true;
            }
            var regulated = _host.IsRegulated; _processingSuspended?.Invoke(regulated);
            if (!ReferenceEquals(_run, run)) return true;
            if (regulated) { run.Input = null; return false; }
            if (ProcessCompleted(run)) return true;
            if (!ReferenceEquals(_run, run)) return true;
            if (run.CloseRequested) { Finish(run); return true; }
            if (run.NextRequested)
            {
                run.NextRequested = false; EndRemaining(run);
                if (ReferenceEquals(_run, run)) run.NeedBatch = true;
                return true;
            }
            if (HardWaiting) { run.Input = null; return false; }
            var input = run.Input; run.Input = null;
            if (!_host.TextComplete)
            {
                if (input == AdvanceSource.Manual || input == AdvanceSource.Skip)
                {
                    _host.CompleteText();
                    if (ReferenceEquals(_run, run)) EndRemaining(run);
                }
                // Completing the current text never also advances to a new batch.
                return !ReferenceEquals(_run, run);
            }
            bool advance = input == AdvanceSource.Manual || input == AdvanceSource.Skip
                || (input == AdvanceSource.Auto && run.HasText && !run.Slots.Any())
                || (!run.HasText && !run.Slots.Any());
            if (!advance) return false;
            EndRemaining(run);
            if (!ReferenceEquals(_run, run)) return true;
            _host.ClearCurrentCharaData();
            if (ReferenceEquals(_run, run)) run.NeedBatch = true;
            return true;
        }
        private void BeginBatch(Run run)
        {
            run.NeedBatch = false; run.Batch = new object(); run.HasText = false;
            while (ReferenceEquals(_run, run))
            {
                if (!run.Tape.TryRead(out var entry)) throw new FormatException("KKS ADV ended without Close: " + run.Tape.LoadBundle + "/" + run.Tape.LoadAsset);
                // TextScenario treats these as a batch boundary even if serialized Multi is true.
                if (entry.Row.Command == "None" || entry.Row.Command == "Log") break;
                var slot = new Slot { Batch = run.Batch, Name = entry.Row.Command,
                    Location = entry.Bundle + "/" + entry.Asset + " sourceRow=" + entry.SourceRow + " localLine=" + run.Tape.CurrentLine + " command=" + entry.Row.Command };
                run.Slots.Add(slot);
                if (slot.Name == "Text") run.HasText = true;
                var context = new Context(this, run, slot, entry, run.Tape.CurrentLine);
                var command = _host.Create(context) ?? throw new NotSupportedException("KKS ADV has no handler: " + slot.Location);
                if (slot.Ended) { command.End(false); return; }
                slot.Command = command;
                if (!ReferenceEquals(_run, run)) { End(slot, false); return; }
                slot.Command.Start(() => Complete(run, slot), exception => CommandFailed(run, slot, exception));
                if (run.Error != null || run.CloseRequested || run.NextRequested || !entry.Row.Multi) break;
            }
        }
        private bool PrepareBatch(Run run)
        {
            if (_prepareBatch == null) return true;
            var preparation = run.Preparing;
            if (preparation == null)
            {
                preparation = new Preparation(); run.Preparing = preparation; run.Input = null;
                _processingSuspended?.Invoke(true);
                if (!ReferenceEquals(_run, run)) return false;
                var current = preparation;
                var lease = _prepareBatch(run.Tape.PreviewNextBatch(), () =>
                {
                    if (!ReferenceEquals(_run, run) || !ReferenceEquals(run.Preparing, current) || current.Ready || current.Released) return;
                    current.Ready = true; Pump();
                }, exception =>
                {
                    if (!ReferenceEquals(_run, run) || !ReferenceEquals(run.Preparing, current) || current.Ready || current.Released) return;
                    current.Ready = true;
                    run.Error = new InvalidOperationException("KKS ADV batch preparation failed.", exception ?? new InvalidOperationException("Preparation failed without diagnostics."));
                    Pump();
                });
                if (preparation.Released) lease?.Dispose(); else preparation.Lease = lease;
            }
            if (!ReferenceEquals(_run, run)) return false;
            if (run.Error != null) throw run.Error;
            if (!preparation.Ready) return false;
            ReleasePreparation(run);
            return ReferenceEquals(_run, run);
        }
        private static void ReleasePreparation(Run run)
        {
            var preparation = run?.Preparing;
            if (preparation == null) return;
            run.Preparing = null; preparation.Released = true;
            var lease = preparation.Lease; preparation.Lease = null; lease?.Dispose();
        }
        private void Complete(Run run, Slot slot)
        {
            if (!ReferenceEquals(_run, run) || !ReferenceEquals(run.Batch, slot.Batch) || slot.Ended || slot.Completed) return;
            slot.Completed = true; Pump();
        }
        private void CommandFailed(Run run, Slot slot, Exception exception)
        {
            if (!ReferenceEquals(_run, run) || !ReferenceEquals(run.Batch, slot.Batch) || slot.Ended) return;
            if (run.Error == null) run.Error = new InvalidOperationException("KKS ADV command failed: " + slot.Location,
                exception ?? new InvalidOperationException("Command failed without diagnostics."));
            Pump();
        }
        private bool ProcessCompleted(Run run)
        {
            var complete = run.Slots.Where(slot => slot.Completed && !slot.Ended).ToArray();
            foreach (var slot in complete)
            {
                run.Slots.Remove(slot); End(slot, true);
                if (!ReferenceEquals(_run, run)) return true;
            }
            return complete.Any();
        }
        private static void End(Slot slot, bool processEnd)
        {
            if (slot.Ended) return;
            slot.Ended = true; slot.Finalizing = true;
            try { slot.Command?.End(processEnd); }
            finally { slot.Finalizing = false; }
        }
        private static void EndRemaining(Run run)
        {
            if (run == null) return;
            var remaining = run.Slots.ToArray(); run.Slots.Clear();
            var errors = new List<Exception>();
            try { ReleasePreparation(run); } catch (Exception exception) { errors.Add(exception); }
            foreach (var slot in remaining) try { End(slot, false); } catch (Exception exception) { errors.Add(exception); }
            if (errors.Any()) throw new AggregateException("KKS ADV command cleanup failed.", errors);
        }
        private void Finish(Run run)
        {
            var completed = run.Completed; var failed = run.Failed;
            try { Stop(); completed?.Invoke(); }
            catch (Exception exception) { failed?.Invoke(exception); }
        }
        private void Fail(Run run, Exception exception)
        {
            if (!ReferenceEquals(_run, run)) return;
            var failed = run.Failed;
            try { Stop(); } catch (Exception cleanup) { exception = new AggregateException(exception, cleanup); }
            failed?.Invoke(exception);
        }
        internal void Stop()
        {
            _transition = new object(); var run = _run; _run = null;
            var failures = new List<Exception>();
            try { _processingSuspended?.Invoke(true); } catch (Exception exception) { failures.Add(exception); }
            try { EndRemaining(run); } catch (Exception exception) { failures.Add(exception); }
            if (failures.Any()) throw new AggregateException("KKS ADV stop failed.", failures);
        }
        public void Dispose() { if (_disposed) return; _disposed = true; Stop(); }
        private static bool IsHardWait(string command)
        {
            switch (command)
            {
                case "Choice": case "Selection": case "WaitAbs": case "TaskWait": case "FadeWait":
                case "CharaKaraokePlay": case "OpenEntryPlayer": case "OpenWhereLive": case "GetTopic": case "KoikatsuPointAdd": return true;
                default: return false;
            }
        }
    }
}
