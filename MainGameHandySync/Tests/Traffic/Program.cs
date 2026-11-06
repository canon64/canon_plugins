using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MainGameHandySync;

static class Program
{
    static int checks;
    static bool enabled;
    static string path;
    static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static string[] Records() => File.ReadAllLines(path).Select(line => JsonDocument.Parse(line).RootElement.GetProperty("data").GetString()).ToArray();
    static async Task Main()
    {
        path = Path.Combine(Path.GetTempPath(), "handy-all-traffic-" + Guid.NewGuid().ToString("N") + ".jsonl");
        HandyTrafficLog.Configure(path, () => enabled, text => text.Replace("connection-secret", "[redacted]").Replace("application-secret", "[redacted]"));
        try
        {
            int formats = 0;
            HandyTrafficLog.Record("disabled", () => { formats++; return "unused"; });
            Check(formats == 0 && !File.Exists(path), "OFF does not format or create a file");
            enabled = true;
            await HttpObservation();
            await AllEvents();
            await DisableCancelsSubscription();
            Console.WriteLine($"PASS {checks} checks: unchanged HTTP, full responses/all SSE lines, redaction, ON/OFF, subscription cancellation, no reconnect");
        }
        finally
        {
            enabled = false;
            HandyTrafficLog.Configure(null, null, null);
            if (File.Exists(path)) File.Delete(path);
        }
    }
    static async Task HttpObservation()
    {
        int calls = 0;
        var expected = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"new_unknown_property\":123,\"error\":{\"code\":1002},\"token\":\"token-secret\"}") };
        using var client = new HttpClient(new HandyTrafficHandler(new Handler(async (request, cancellation) =>
        {
            calls++;
            Check(request.Method == HttpMethod.Put, "motor HTTP method unchanged");
            Check(request.RequestUri.Query == "?ck=connection-secret", "motor URI unchanged");
            Check(await request.Content.ReadAsStringAsync() == "{\"velocity\":0.43}", "motor payload unchanged");
            return expected;
        })));
        using var command = new HttpRequestMessage(HttpMethod.Put, "https://example.invalid/hamp/velocity?ck=connection-secret") { Content = new StringContent("{\"velocity\":0.43}") };
        command.Headers.TryAddWithoutValidation("Authorization", "Bearer bearer-secret");
        var response = await client.SendAsync(command);
        Check(ReferenceEquals(response, expected) && response.IsSuccessStatusCode && calls == 1, "API error is recorded without changing existing response or issuing extra requests");
        string text = File.ReadAllText(path);
        Check(text.Contains("new_unknown_property") && text.Contains("1002"), "full response preserves unknown fields and API errors");
        Check(!text.Contains("connection-secret") && !text.Contains("token-secret") && !text.Contains("bearer-secret"), "query, body tokens and authorization are redacted");
        enabled = false;
        long before = new FileInfo(path).Length;
        HandyTrafficLog.Record("off", () => throw new Exception("must not format"));
        Check(new FileInfo(path).Length == before, "OFF stops file updates");
        enabled = true;
    }
    static async Task AllEvents()
    {
        var requests = new List<string>();
        const string stream = ":heartbeat\nevent: new_unknown_event\ndata: {\"unknown\":42,\"connection_key\":\"connection-secret\"}\n\nevent: slider_blocked\ndata: {\"arbitrary\":\"kept\"}\n\n";
        var recording = new HandyEventRecording(() => new Handler((request, cancellation) =>
        {
            requests.Add(request.RequestUri.AbsolutePath);
            Check(request.Method == HttpMethod.Get, "observer never sends motor commands");
            if (request.RequestUri.AbsolutePath.EndsWith("/auth/token/issue"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"token\":\"observer-secret\"}") });
            Check(!request.RequestUri.Query.Contains("events="), "SSE does not filter any event types");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(stream))) });
        }));
        recording.Refresh(true, true, "application-secret", "connection-secret");
        await recording.Completion.WaitAsync(TimeSpan.FromSeconds(3));
        string[] data = Records();
        foreach (string line in stream.Split('\n').SkipLast(1)) Check(data.Contains(line.Replace("connection-secret", "[redacted]")), "all SSE lines preserved: " + (line.StartsWith("data:") ? "data" : line));
        Check(requests.Count == 2 && data.Any(x => x.Contains("coverage=ended")), "end of coverage recorded, no automatic polling or reconnect");
        Check(!File.ReadAllText(path).Contains("observer-secret"), "observer auth token redacted");
        enabled = false;
        recording.Refresh(true, true, "application-secret", "connection-secret");
        Check(requests.Count == 2, "OFF creates no extra network subscription");
        enabled = true;
    }
    static async Task DisableCancelsSubscription()
    {
        var input = new BlockingStream();
        int requests = 0;
        var recording = new HandyEventRecording(() => new Handler((request, cancellation) =>
        {
            requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = request.RequestUri.AbsolutePath.EndsWith("/sse")
                ? new StreamContent(input) : new StringContent("{\"token\":\"observer-secret\"}") });
        }));
        recording.Refresh(true, true, "application-secret", "connection-secret");
        await input.Reading.Task.WaitAsync(TimeSpan.FromSeconds(3));
        enabled = false; long before = new FileInfo(path).Length;
        recording.Stop();
        await recording.Completion.WaitAsync(TimeSpan.FromSeconds(3));
        Check(input.Closed, "OFF/stop closes a blocked SSE read");
        Check(requests == 2 && new FileInfo(path).Length == before, "OFF prevents asynchronous trailing output and reconnect");
    }
    sealed class Handler : HttpMessageHandler
    {
        readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send;
        internal Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) { this.send = send; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token);
    }
    sealed class BlockingStream : Stream
    {
        internal readonly TaskCompletionSource<bool> Reading = new(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly TaskCompletionSource<int> end = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Closed;
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) { Reading.TrySetResult(true); return end.Task; }
        protected override void Dispose(bool disposing) { Closed = true; end.TrySetResult(0); base.Dispose(disposing); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { } public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
