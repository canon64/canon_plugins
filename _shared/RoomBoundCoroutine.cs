using System;
using System.Collections;
using System.Collections.Generic;

namespace CanonPlugins.Shared
{
    // Flatten nested routines so cancellation disposes every active finally block.
    internal sealed class RoomBoundCoroutine : IEnumerator, IDisposable
    {
        private readonly Stack<IEnumerator> _stack = new Stack<IEnumerator>();
        private readonly Func<bool> _allowed;
        internal bool IsCompleted => _stack.Count == 0;
        public object Current { get; private set; }

        internal RoomBoundCoroutine(IEnumerator routine, Func<bool> allowed)
        {
            _stack.Push(routine);
            _allowed = allowed;
        }

        public bool MoveNext()
        {
            try
            {
                while (_stack.Count > 0)
                {
                    if (!_allowed()) { Dispose(); return false; }
                    var routine = _stack.Peek();
                    if (!routine.MoveNext())
                    {
                        _stack.Pop();
                        (routine as IDisposable)?.Dispose();
                        continue;
                    }
                    if (routine.Current is IEnumerator nested) { _stack.Push(nested); continue; }
                    Current = routine.Current;
                    return true;
                }
                Current = null;
                return false;
            }
            catch { Dispose(); throw; }
        }

        public void Reset() => throw new NotSupportedException();
        public void Dispose()
        {
            try
            {
                while (_stack.Count > 0) (_stack.Pop() as IDisposable)?.Dispose();
            }
            finally
            {
                Current = null;
                // Even a failing child cleanup must release all parent finally blocks.
                if (_stack.Count > 0) Dispose();
            }
        }
    }
}
