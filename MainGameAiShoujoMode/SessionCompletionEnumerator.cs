using System;
using System.Collections;

namespace MainGameAiShoujoMode
{
    internal sealed class SessionCompletionEnumerator : IEnumerator, IDisposable
    {
        private readonly IEnumerator _inner;
        private Action _completed;
        internal SessionCompletionEnumerator(IEnumerator inner, Action completed)
        { _inner = inner; _completed = completed; }
        public object Current => _inner.Current;
        public bool MoveNext()
        {
            try
            {
                if (_inner.MoveNext()) return true;
            }
            catch { Complete(); throw; }
            Complete();
            return false;
        }
        private void Complete()
        { var callback = _completed; _completed = null; callback?.Invoke(); }
        public void Reset() => throw new NotSupportedException();
        public void Dispose()
        { try { (_inner as IDisposable)?.Dispose(); } finally { Complete(); } }
    }
}
