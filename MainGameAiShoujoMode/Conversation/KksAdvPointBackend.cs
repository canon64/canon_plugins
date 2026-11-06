using System;
using System.Linq;

namespace MainGameAiShoujoMode.Conversation
{
    internal sealed class KksAdvPointBackend : KksAdvPointHost.IBackend
    {
        private sealed class Presentation : IDisposable
        {
            internal bool Active = true;
            internal IDisposable View;
            internal Action<Exception> Failed;
            internal void Adopt(IDisposable view) { if (Active) View = view; else view?.Dispose(); }
            internal void Finish(Action notification, Exception failure = null)
            {
                if (!Active) return;
                Active = false;
                var view = View; View = null;
                try { view?.Dispose(); }
                catch (Exception exception)
                {
                    if (notification == null && failure == null) throw;
                    failure = failure == null ? exception : new AggregateException(failure, exception);
                }
                if (failure != null) Failed(failure); else notification?.Invoke();
            }
            public void Dispose() => Finish(null);
        }
        private readonly KksConversationStateStore _store;
        private readonly Action<int> _setPlayerPoints;
        private readonly Func<KksConversationStateStore.PointChange, Action<int>, Action, Action<Exception>, IDisposable> _present;
        private readonly int _zero, _maximum;
        internal KksAdvPointBackend(KksConversationStateStore store, Action<int> setPlayerPoints,
            Func<KksConversationStateStore.PointChange, Action<int>, Action, Action<Exception>, IDisposable> present, int zero, int maximum)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store)); _setPlayerPoints = setPlayerPoints ?? throw new ArgumentNullException(nameof(setPlayerPoints));
            _present = present ?? throw new ArgumentNullException(nameof(present)); _zero = zero; _maximum = maximum;
        }
        public IDisposable Add(KksConversationStateStore.PointChange change, Action completed, Action<Exception> failed)
        {
            var saved = _store.CommitPoints(_store.ReadPlayer(), change, _zero, _maximum, out var applied);
            _setPlayerPoints(saved.points);
            if (!applied) { completed(); return null; }
            // Display failure or cancellation never rolls back the command's already committed points.
            var savedChange = saved.pointChanges.Single(operation => operation.id == change.id);
            var operationId = savedChange.id;
            var presentation = new Presentation { Failed = failed };
            try
            {
                presentation.Adopt(_present(savedChange, displayDelta =>
                {
                    if (!presentation.Active) return;
                    try { _store.MarkPointDisplayed(operationId, displayDelta, _zero, _maximum, out _); }
                    catch (Exception exception) { presentation.Finish(null, exception); }
                }, () => presentation.Finish(completed), exception => presentation.Finish(null, exception)));
                return presentation;
            }
            catch { presentation.Dispose(); throw; }
        }
    }
}
