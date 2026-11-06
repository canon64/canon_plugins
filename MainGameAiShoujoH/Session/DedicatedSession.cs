using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MainGameAiShoujoH.Contracts;
using UnityEngine;

namespace MainGameAiShoujoH.Session
{
    public sealed class DedicatedSession : IDisposable
    {
        readonly Func<bool> _allowed;
        readonly Func<StartRequest, ISessionModule[]> _modules;
        readonly Action<string> _notify;
        readonly Action<Exception> _traceException;
        readonly List<ISessionModule> _owned = new List<ISessionModule>();
        readonly List<Action> _restore = new List<Action>();
        readonly List<string> _failures = new List<string>();
        StartRequest _request;
        Guid _id;
        long _generation;
        public SessionPhase Phase { get; private set; }
        bool _disposed;
        EndReason? _lastEndReason;
        bool _lastRestored;
        int _resultPending, _resultApplied;
        string _lastMotion, _lastState;
        string[] _lastParticipants = Array.Empty<string>();
        bool _modulesEnded;
        int _beforeCleanup, _resultFailures;
        string _endingScenario;
        bool _endingReceiveSaved, _endingCompleted;
        bool _externalAborted;
        bool _changingMotion;
        bool _initialResumed;
        bool _revealed;
        bool _cleaningModules;
        public bool Busy => Phase != SessionPhase.Idle && Phase != SessionPhase.Failed;
        public DedicatedSession(Func<bool> allowed, Func<StartRequest, ISessionModule[]> modules, Action<string> notify, Action<Exception> traceException = null)
        { _allowed = allowed; _modules = modules; _notify = notify; _traceException = traceException; }
        public void Begin(StartRequest request)
        {
            if (_disposed || Busy) throw new InvalidOperationException("専用Hは開始済みか破棄済みです。");
            if (!_allowed()) throw new InvalidOperationException("専用Hの操作権がありません。");
            if (request?.Owner == null || request.Island == null || request.Participants == null || request.Participants.Length == 0 || request.Cover == null || request.Reveal == null || string.IsNullOrWhiteSpace(request.MotionId))
                throw new ArgumentException("専用Hの参加者・島・ロード契約が不足しています。");
            _request = request; _id = Guid.NewGuid(); ++_generation; _failures.Clear(); _modulesEnded = false; _lastEndReason = null; _lastRestored = false; _resultPending = _resultApplied = 0; Phase = SessionPhase.Loading;
            _endingScenario = null; _endingReceiveSaved = _endingCompleted = false;
            _externalAborted = false;
            _changingMotion = false;
            _initialResumed = false;
            _revealed = false;
            _cleaningModules = false;
            long generation = _generation;
            try { request.Cover(() => { if (Current(generation)) request.Owner.StartCoroutine(Prepare(generation)); }); }
            catch { Phase = SessionPhase.Failed; _request = null; throw; }
        }
        bool Current(long generation) => !_disposed && generation == _generation && _allowed() && _request?.Owner != null && _request.Island != null && Busy;
        IEnumerator Prepare(long generation)
        {
            yield return new WaitForEndOfFrame();
            if (!Current(generation)) { EndInvalidCurrent(generation); yield break; }
            Phase = SessionPhase.Preparing;
            try
            {
                if (_request.ReleaseParticipants != null) _restore.Add(_request.ReleaseParticipants);
                _request.ResolveParticipants?.Invoke();
                foreach (var participant in _request.Participants)
                {
                    if (participant?.Character == null) throw new InvalidOperationException("参加キャラが破棄されています。");
                    Transform t = participant.Character.transform;
                    Transform parent = t.parent; Vector3 position = t.localPosition; Quaternion rotation = t.localRotation; Vector3 scale = t.localScale;
                    _restore.Add(() => { if (t != null) { t.SetParent(parent, false); t.localPosition = position; t.localRotation = rotation; t.localScale = scale; } });
                }
                var control = _request.AcquireControl?.Invoke(); if (control != null) _restore.Add(control);
                _request.Started?.Invoke();
                var context = new SessionContext { Id = _id, Generation = generation, Request = _request, IsCurrent = () => Current(generation), CanInteract = () => Current(generation) && Phase == SessionPhase.Ready && _revealed,
                    IsMotionPreparation = () => Current(generation) && _changingMotion && Phase == SessionPhase.Preparing,
                    RequestMotionChange = (id, automatic) => { if (!Current(generation)) throw new InvalidOperationException("旧世代からの体位変更は無効です。"); Execute(new Command { Name = automatic ? "auto-motion" : "motion", Value = id }); }, ReportFailure = Failure, TraceException = _traceException };
                PrepareModules(generation, context, _modules(_request), default(int));
            }
            catch (Exception ex) { _traceException?.Invoke(ex); Failure("準備: " + ex.Message); End(EndReason.PreparationFailed); }
        }
        void PrepareModules(long generation, SessionContext context, ISessionModule[] modules, int index)
        {
            if (!Current(generation)) { EndInvalidCurrent(generation); return; }
            if (Phase != SessionPhase.Preparing) return;
            try
            {
                for (; index < modules.Length; ++index)
                {
                    var module = modules[index]; _owned.Add(module);
                    if (module is IAsyncSessionPreparation asynchronous)
                    {
                        bool returned = false; int next = index; ++next;
                        asynchronous.PrepareAsync(context,
                            () => { if (returned) return; returned = true; PrepareModules(generation, context, modules, next); },
                            error => { if (returned) return; returned = true; if (!Current(generation)) { EndInvalidCurrent(generation); return; } PreparationFailed(module.Name, error); });
                        return;
                    }
                    try { module.Prepare(context); }
                    catch (Exception error) { throw new InvalidOperationException("専用H準備[" + module.Name + "]: " + error.Message, error); }
                    if (!Current(generation)) { EndInvalidCurrent(generation); return; }
                    if (Phase != SessionPhase.Preparing) return;
                }
                Phase = SessionPhase.Ready;
                _request.Ready?.Invoke();
                if (!Current(generation) || Phase != SessionPhase.Ready) return;
                _request.Reveal(() => ResumeInitial(generation));
            }
            catch (Exception error) { PreparationFailed("準備", error); }
        }
        void PreparationFailed(string stage, Exception error)
        {
            _traceException?.Invoke(error); Failure(stage + ": " + error.Message); End(EndReason.PreparationFailed);
        }
        void ResumeInitial(long generation)
        {
            if (_initialResumed || !Current(generation) || Phase != SessionPhase.Ready) return;
            _initialResumed = true;
            _revealed = true;
            try
            {
                foreach (var transition in _owned.OfType<IMotionSelectionTransition>().ToArray())
                {
                    if (!Current(generation) || Phase != SessionPhase.Ready) return;
                    transition.ResumeSelection();
                }
            }
            catch (Exception ex)
            {
                if (!Current(generation) || Phase != SessionPhase.Ready) return;
                Failure("開始再開: " + ex.Message); End(EndReason.PreparationFailed);
            }
        }
        public void Execute(Command command)
        {
            if (Phase != SessionPhase.Ready || !_revealed || !Current(_generation)) throw new InvalidOperationException("専用Hは操作可能ではありません。");
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (command.Name == "end") { End(EndReason.Completed); return; }
            if (command.Name == "cancel") { End(EndReason.Cancelled); return; }
            if (command.Name == "motion" || command.Name == "auto-motion") { BeginMotionChange(command); return; }
            foreach (var module in _owned) module.Execute(command);
        }
        void EndInvalidCurrent(long generation)
        {
            // A cancelled/replaced coroutine has no authority to end a newer lifecycle.
            if (generation != _generation || _request == null || !Busy || _disposed) return;
            End(_allowed() ? EndReason.ModeDestroyed : EndReason.PermissionRevoked);
        }
        void BeginMotionChange(Command command)
        {
            var observers = _owned.OfType<IMotionSelectionTransition>().ToArray();
            if (!observers.Any()) throw new InvalidOperationException("体位選択の実条件検証がありません。");
            foreach (var observer in observers) observer.ValidateMotionSelection(command.Value, command.Name == "auto-motion");
            long generation = _generation;
            _revealed = false; _changingMotion = true; Phase = SessionPhase.Loading;
            try { _request.Cover(() => { if (Current(generation)) _request.Owner.StartCoroutine(PrepareMotionChange(generation, command, observers)); }); }
            catch (Exception ex) { Failure("体位ロード表示: " + ex.Message); End(EndReason.PreparationFailed); }
        }
        IEnumerator PrepareMotionChange(long generation, Command command, IMotionSelectionTransition[] observers)
        {
            yield return new WaitForEndOfFrame();
            if (!Current(generation)) { EndInvalidCurrent(generation); yield break; }
            Phase = SessionPhase.Preparing;
            try
            {
                foreach (var observer in observers) observer.SuspendSelection();
                foreach (var module in _owned) module.Execute(command);
                if (!Current(generation)) throw new InvalidOperationException("体位準備中に専用Hの世代/許可が失効しました。");
                _request.MotionId = command.Value; _changingMotion = false; Phase = SessionPhase.Ready;
                bool resumed = false;
                _request.Reveal(() => {
                    if (resumed || !Current(generation) || Phase != SessionPhase.Ready) return;
                    resumed = true; _revealed = true;
                    try { foreach (var observer in observers) { if (!Current(generation) || Phase != SessionPhase.Ready) return; observer.ResumeSelection(); } }
                    catch (Exception error) { _traceException?.Invoke(error); Failure("体位再開: " + error.Message); End(EndReason.PreparationFailed); }
                });
            }
            catch (Exception ex) { Failure("体位準備: " + ex.Message); End(EndReason.PreparationFailed); }
        }
        public void End(EndReason reason)
        {
            _revealed = false;
            if (!Busy || Phase == SessionPhase.Ending) return;
            if (Phase == SessionPhase.Restoring)
            {
                if (reason == EndReason.Completed) return;
                if (_owned.Count == 0 && _restore.Count == 0 && reason != EndReason.PermissionRevoked && reason != EndReason.ModeDestroyed) return;
                ++_generation; _lastEndReason = reason;
                if (reason == EndReason.PermissionRevoked || reason == EndReason.ModeDestroyed) AbortExternalControl();
                RestoreNow(reason); return;
            }
            ++_generation; Phase = SessionPhase.Ending;
            var request = _request;
            long endingGeneration = _generation;
            try { request.Cover(() => { if (_generation != endingGeneration || Phase != SessionPhase.Ending) return; if (request.Owner != null) request.Owner.StartCoroutine(Restore(reason, endingGeneration)); else RestoreNow(reason); }); }
            catch (Exception ex) { Failure("復帰ロード: " + ex.Message); RestoreNow(reason); }
        }
        IEnumerator Restore(EndReason reason, long generation)
        {
            yield return new WaitForEndOfFrame();
            if (generation != _generation) yield break;
            EndModules(reason);
            if (reason == EndReason.Completed && _allowed())
            {
                var finalizers = new Queue<ISessionFinalizer>(_owned.OfType<ISessionFinalizer>());
                if (finalizers.Count == 0) RestoreNow(reason);
                else FinalizeNext(finalizers, reason, generation);
                yield break;
            }
            if (generation == _generation) RestoreNow(reason);
        }
        void FinalizeNext(Queue<ISessionFinalizer> pending, EndReason reason, long generation)
        {
            if (generation != _generation || Phase != SessionPhase.Restoring) return;
            if (!_allowed()) { RestoreNow(EndReason.PermissionRevoked); return; }
            if (pending.Count == 0)
            {
                try { _request.Cover(() => { if (generation == _generation && _request?.Owner != null) _request.Owner.StartCoroutine(CleanupAfterFinalization(reason, generation)); }); }
                catch (Exception ex) { Failure("終了後復帰ロード: " + ex.Message); RestoreNow(reason); }
                return;
            }
            bool completed = false;
            Action<Exception> finish = error => {
                if (completed || generation != _generation || Phase != SessionPhase.Restoring) return;
                completed = true;
                if (error != null) { _traceException?.Invoke(error); Failure("終了処理: " + error.Message); }
                FinalizeNext(pending, reason, generation);
            };
            try { pending.Dequeue().BeginFinalization(reason, () => finish(null), finish); } catch (Exception ex) { finish(ex); }
        }
        IEnumerator CleanupAfterFinalization(EndReason reason, long generation)
        { yield return new WaitForEndOfFrame(); if (generation == _generation) RestoreNow(reason); }
        void RestoreNow(EndReason reason)
        {
            if (Phase == SessionPhase.Idle || Phase == SessionPhase.Failed) return;
            if (_cleaningModules) return;
            EndModules(reason);
            int beforeCleanup = _beforeCleanup, resultFailures = _resultFailures;
            var ending = _owned.OfType<ISessionEndingStatus>().FirstOrDefault();
            if (ending != null) { _endingScenario = ending.EndingScenario; _endingReceiveSaved = ending.EndingReceiveSaved; _endingCompleted = ending.EndingCompleted; }
            _cleaningModules = true;
            CleanupModule(_owned.Count - 1, reason, beforeCleanup, resultFailures);
        }
        void CleanupModule(int index, EndReason reason, int beforeCleanup, int resultFailures)
        {
            if (index < 0) { _owned.Clear(); _cleaningModules = false; RestoreControls(reason, beforeCleanup, resultFailures); return; }
            var module = _owned[index];
            if (module is IAsyncSessionCleanup asynchronous)
            {
                bool finished = false;
                Action<Exception> finish = error => {
                    if (finished) return; finished = true;
                    if (error != null) { _traceException?.Invoke(error); Failure(module.Name + "解放: " + error.Message); }
                    CleanupModule(index - 1, reason, beforeCleanup, resultFailures);
                };
                try { asynchronous.DisposeAsync(() => finish(null), finish); } catch (Exception error) { finish(error); }
            }
            else { Attempt(module.Name + "解放", module.Dispose); CleanupModule(index - 1, reason, beforeCleanup, resultFailures); }
        }
        void EndModules(EndReason reason)
        {
            if (_modulesEnded) return;
            _modulesEnded = true;
            Phase = SessionPhase.Restoring;
            _lastEndReason = reason;
            _lastMotion = _owned.OfType<IMotionModule>().FirstOrDefault()?.SelectedMotionId ?? _request?.MotionId;
            _lastState = _owned.OfType<IMotionModule>().FirstOrDefault()?.SelectedState;
            _lastParticipants = _request?.Participants?.Select(p => p.Identity + ":" + p.Role).ToArray() ?? Array.Empty<string>();
            _beforeCleanup = _failures.Count;
            _resultFailures = 0;
            for (int i = _owned.Count - 1; i >= 0; --i)
            {
                var module = _owned[i]; int beforeEnd = _failures.Count; Attempt(module.Name + "終了", () => module.End(reason));
                if (module is Results.ResultModule results) { _resultPending = results.PendingCount; _resultApplied = results.AppliedCount; _resultFailures += _failures.Count - beforeEnd; }
            }
        }
        void RestoreControls(EndReason reason, int beforeCleanup, int resultFailures)
        {
            for (int i = _restore.Count - 1; i >= 0; --i) Attempt("状態復元", _restore[i]);
            _restore.Clear();
            var request = _request;
            if (request.RestoreExternalControl != null && reason != EndReason.ModeDestroyed && reason != EndReason.PermissionRevoked && request.Owner != null && request.Island != null)
            {
                long generation = _generation; bool finished = false;
                Action<Exception> finish = error => {
                    if (finished || generation != _generation || !ReferenceEquals(_request, request)) return;
                    finished = true;
                    if (error != null) { Failure("外部動作復元: " + error.Message); AbortExternalControl(); }
                    FinishRestoration(request, reason, beforeCleanup, resultFailures);
                };
                try { request.RestoreExternalControl(reason, () => finish(null), finish); } catch (Exception ex) { finish(ex); }
                return;
            }
            AbortExternalControl();
            FinishRestoration(request, reason, beforeCleanup, resultFailures);
        }
        void FinishRestoration(StartRequest request, EndReason reason, int beforeCleanup, int resultFailures)
        {
            _request = null;
            if (reason != EndReason.ModeDestroyed && request.Owner != null && request.Island != null) Attempt("歩行復帰", () => request.Ended?.Invoke());
            Phase = _failures.Count == 0 ? SessionPhase.Idle : SessionPhase.Failed;
            Attempt("ロード解除", () => request.Reveal(() => { }));
            _lastRestored = beforeCleanup + resultFailures == _failures.Count;
            if (_failures.Count != 0) _notify(string.Join("\n", _failures));
        }
        void Attempt(string name, Action action) { try { action(); } catch (Exception ex) { Failure(name + ": " + ex.Message); } }
        void AbortExternalControl()
        {
            if (_externalAborted) return;
            _externalAborted = true;
            Attempt("外部動作取消", () => _request?.AbortExternalControl?.Invoke());
        }
        void Failure(string message) { _failures.Add(message); }
        public SessionSnapshot Snapshot() => new SessionSnapshot {
            Id = _id, Generation = _generation, Phase = Phase, Ready = Phase == SessionPhase.Ready && Current(_generation),
            MotionId = _owned.OfType<IMotionModule>().FirstOrDefault()?.SelectedMotionId ?? _request?.MotionId ?? _lastMotion,
            MotionState = _owned.OfType<IMotionModule>().FirstOrDefault()?.SelectedState ?? _lastState,
            BodyBackend = _request?.KksPose != null ? "KKS" : null,
            KksTableBundle = _request?.KksPose?.TableBundle, KksTableAsset = _request?.KksPose?.TableAsset,
            KksMode = _request?.KksPose?.Mode, KksPoseId = _request?.KksPose?.Id,
            Participants = _request?.Participants?.Select(p => p.Identity + ":" + p.Role).ToArray() ?? _lastParticipants,
            LastEndReason = _lastEndReason, Restored = _lastRestored, ResultPendingCount = _resultPending, ResultAppliedCount = _resultApplied, Failures = _failures.ToArray(),
            EndingScenario = _owned.OfType<ISessionEndingStatus>().FirstOrDefault()?.EndingScenario ?? _endingScenario,
            EndingReceiveSaved = _owned.OfType<ISessionEndingStatus>().FirstOrDefault()?.EndingReceiveSaved ?? _endingReceiveSaved,
            EndingCompleted = _owned.OfType<ISessionEndingStatus>().FirstOrDefault()?.EndingCompleted ?? _endingCompleted };
        public void OwnerDestroyed(MonoBehaviour owner)
        {
            if (!Busy || !ReferenceEquals(_request?.Owner, owner)) return;
            ++_generation;
            _lastEndReason = EndReason.ModeDestroyed;
            if (Phase == SessionPhase.Restoring)
            {
                var request = _request;
                AbortExternalControl();
                if (_owned.Count != 0 || _restore.Count != 0) RestoreNow(EndReason.ModeDestroyed);
                else FinishRestoration(request, EndReason.ModeDestroyed, _failures.Count, 0);
            }
            else RestoreNow(EndReason.ModeDestroyed);
        }
        public void Dispose() { if (_disposed) return; if (Busy) OwnerDestroyed(_request.Owner); _disposed = true; }
    }
}
