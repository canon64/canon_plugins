using System;
using System.Threading;
using System.Threading.Tasks;

namespace MainGameVoiceFaceEventBridge.Infrastructure
{
    // Shared by the plugin and the offline runtime test. Every posted request
    // completes even when acquisition/serialization throws or the session ends.
    internal sealed class ClothingQueryDispatcher
    {
        private readonly SynchronizationContext _context;
        private readonly Func<string, string> _query;

        internal ClothingQueryDispatcher(SynchronizationContext context, Func<string, string> query)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _query = query ?? throw new ArgumentNullException(nameof(query));
        }

        internal async Task<string> Query(string line, CancellationToken cancel)
        {
            var reply = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancel.Register(() => reply.TrySetCanceled()))
            {
                cancel.ThrowIfCancellationRequested();
                _context.Post(_ =>
                {
                    if (cancel.IsCancellationRequested) return;
                    try { reply.TrySetResult(_query(line)); }
                    catch (Exception error) { reply.TrySetException(error); }
                }, null);
                return await reply.Task.ConfigureAwait(false);
            }
        }
    }
}
