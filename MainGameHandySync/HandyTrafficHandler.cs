using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MainGameHandySync
{
    // Observes the existing HTTP exchange, without changing URI, content, ordering or status.
    internal sealed class HandyTrafficHandler : DelegatingHandler
    {
        private static long _sequence;
        internal HandyTrafficHandler(HttpMessageHandler transport) : base(transport) { }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            bool record = HandyTrafficLog.Enabled;
            long id = record ? Interlocked.Increment(ref _sequence) : 0;
            Stopwatch watch = record ? Stopwatch.StartNew() : null;
            string path = record ? request.RequestUri.AbsolutePath : null; // Never record auth query strings.
            if (record)
            {
                string content = request.Content == null ? "" : await request.Content.ReadAsStringAsync();
                HandyTrafficLog.Record("http-send", () => $"id={id} method={request.Method} path={path} headers={Headers(request.Headers)} body={content}");
            }
            try
            {
                HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
                if (HandyTrafficLog.Enabled)
                {
                    // The SSE body never ends. Its complete lines are recorded by the event reader.
                    string body = request.RequestUri.AbsolutePath.EndsWith("/sse", StringComparison.Ordinal)
                        ? "<stream; see sse-line>" : response.Content == null ? "" : await response.Content.ReadAsStringAsync();
                    HandyTrafficLog.Record("http-response", () => $"id={id} method={request.Method} path={request.RequestUri.AbsolutePath} status={(int)response.StatusCode} elapsedMs={watch?.ElapsedMilliseconds} headers={Headers(response.Headers)} contentHeaders={Headers(response.Content?.Headers)} body={body}");
                }
                return response;
            }
            catch (Exception ex)
            {
                HandyTrafficLog.Record("http-exception", () => $"id={id} path={path} elapsedMs={watch?.ElapsedMilliseconds} exception={ex.GetType().Name} cancelled={cancellationToken.IsCancellationRequested}");
                throw;
            }
        }
        private static string Headers(System.Net.Http.Headers.HttpHeaders headers)
        {
            if (headers == null) return "";
            return string.Join(";", headers.Where(h => !h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
                && !h.Key.Equals("X-Api-Key", StringComparison.OrdinalIgnoreCase)
                && !h.Key.Equals("X-Connection-Key", StringComparison.OrdinalIgnoreCase)
                && !h.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase)
                && !h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
                .Select(h => h.Key + ":" + string.Join(",", h.Value)));
        }
    }
}
