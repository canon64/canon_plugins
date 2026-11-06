using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MainGameAiShoujoMode.Conversation
{
    internal sealed class KksAdvSessionScope : IDisposable
    {
        private sealed class Identity : IEqualityComparer<IDisposable>
        {
            public bool Equals(IDisposable left, IDisposable right) => ReferenceEquals(left, right);
            public int GetHashCode(IDisposable value) => RuntimeHelpers.GetHashCode(value);
        }
        private readonly Stack<IDisposable> _resources = new Stack<IDisposable>();
        private readonly HashSet<IDisposable> _owned = new HashSet<IDisposable>(new Identity());
        private bool _disposed;
        internal T Own<T>(T resource) where T : class, IDisposable
        {
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            if (_disposed)
            {
                if (_owned.Add(resource)) resource.Dispose();
                throw new ObjectDisposedException(nameof(KksAdvSessionScope));
            }
            if (!_owned.Add(resource)) throw new InvalidOperationException("ADV resource already belongs to this scope.");
            _resources.Push(resource); return resource;
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            var errors = new List<Exception>();
            while (_resources.Count != default(int))
                try { _resources.Pop().Dispose(); } catch (Exception exception) { errors.Add(exception); }
            if (errors.Count != default(int)) throw new AggregateException("ADV owned resources failed to close.", errors);
        }
    }
}
