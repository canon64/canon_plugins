using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MainGameHandySync
{
    // A read-only observer with its own HTTP/auth lifetime; never touches the motion client's queue/token.
    internal sealed class HandyEventRecording
    {
        private const string Base = "https://www.handyfeeling.com/api/handy-rest/v3";
        private CancellationTokenSource _owner;
        private readonly object _gate = new object();
        private readonly Func<HttpMessageHandler> _transport;
        internal Task Completion { get; private set; } = Task.CompletedTask;
        internal HandyEventRecording(Func<HttpMessageHandler> transport = null)
        { _transport = transport ?? (() => new HttpClientHandler()); }

        internal void Refresh(bool sessionAllowed, bool usesV3, string appKey, string connectionKey)
        {
            lock (_gate)
            {
                Stop();
                if (!sessionAllowed || !usesV3 || !HandyTrafficLog.Enabled) return;
                var owner = new CancellationTokenSource();
                _owner = owner;
                Completion = Task.Run(() => Read(appKey, connectionKey, owner));
            }
        }
        internal void Stop()
        {
            lock (_gate)
            {
                var owner = _owner; _owner = null;
                if (owner != null)
                {
                    HandyTrafficLog.Record("sse-stop", () => "reason=session-end-or-recording-setting-change");
                    owner.Cancel();
                }
            }
        }
        private async Task Read(string appKey, string connectionKey, CancellationTokenSource owner)
        {
            var token = owner.Token;
            try
            {
                using (var http = new HttpClient(new HandyTrafficHandler(_transport())) { Timeout = TimeSpan.FromSeconds(15) })
                {
                    string clientToken;
                    using (var auth = new HttpRequestMessage(HttpMethod.Get,
                        Base + "/auth/token/issue?ttl=86400&to=" + Uri.EscapeDataString(connectionKey)))
                    {
                        auth.Headers.TryAddWithoutValidation("X-Api-Key", appKey);
                        using (var response = await http.SendAsync(auth, token))
                        {
                            string body = await response.Content.ReadAsStringAsync();
                            var match = Regex.Match(body, "\"token\"\\s*:\\s*\"([^\"]+)\"");
                            if (!response.IsSuccessStatusCode || !match.Success)
                            {
                                HandyTrafficLog.Record("sse-unavailable", () => "reason=authentication-failed");
                                return;
                            }
                            clientToken = match.Groups[1].Value;
                        }
                    }
                    token.ThrowIfCancellationRequested();
                    // No events filter: subscribe to ALL events exposed by the device API.
                    using (var request = new HttpRequestMessage(HttpMethod.Get,
                        Base + "/sse?apikey=" + Uri.EscapeDataString(clientToken) + "&ck=" + Uri.EscapeDataString(connectionKey)))
                    using (var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            HandyTrafficLog.Record("sse-unavailable", () => "reason=subscription-failed status=" + (int)response.StatusCode);
                            return;
                        }
                        HandyTrafficLog.Record("sse-subscribed", () => "events=ALL filtering=none");
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (token.Register(() => stream.Dispose()))
                        using (var reader = new StreamReader(stream))
                        {
                            while (!token.IsCancellationRequested && HandyTrafficLog.Enabled)
                            {
                                string line = await reader.ReadLineAsync();
                                if (token.IsCancellationRequested || !HandyTrafficLog.Enabled) return;
                                if (line == null) break;
                                // Preserve every event header, data line, comment and frame boundary, including unknown events.
                                HandyTrafficLog.Record("sse-line", () => line.Replace(clientToken, "[redacted]"));
                            }
                        }
                        HandyTrafficLog.Record("sse-ended", () => "coverage=ended; no automatic reconnect; toggle recording or enter a new session to resume");
                    }
                }
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    HandyTrafficLog.Record("sse-unavailable", () => "exception=" + ex.GetType().Name);
            }
            finally
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_owner, owner)) _owner = null;
                    owner.Dispose();
                }
            }
        }
    }
}
