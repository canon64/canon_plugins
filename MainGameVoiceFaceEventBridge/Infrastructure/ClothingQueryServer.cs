using System;
using System.IO;
using System.IO.Pipes;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace MainGameVoiceFaceEventBridge.Infrastructure
{
    // A blocking IPC worker, woken by connections, never a game-state polling loop.
    internal sealed class ClothingQueryServer : IDisposable
    {
        private readonly string _name;
        private readonly Func<string, CancellationToken, Task<string>> _query;
        private readonly Action<Exception> _failure;
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private readonly object _sync = new object();
        private SafePipeHandle _handle;
        private Thread _thread;
        private int _disposed;

        internal ClothingQueryServer(string name, Func<string, CancellationToken, Task<string>> query, Action<Exception> failure = null)
        { _name = name; _query = query; _failure = failure; }

        // Unity's bundled Mono builds invalid flags for asynchronous server pipes.
        // Own a synchronous Win32 handle and wrap it only after connection. This
        // also handles a client connecting before ConnectNamedPipe (error 535).
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        // KKS Mono's SafePipeHandle has no parameterless constructor. Returning it
        // from P/Invoke fails in the marshaler before the listener can accept.
        private static extern IntPtr CreateNamedPipe(string name, uint access, uint mode, uint instances,
            uint outputSize, uint inputSize, uint timeout, IntPtr security);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ConnectNamedPipe(SafePipeHandle pipe, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CancelIoEx(SafePipeHandle pipe, IntPtr overlapped);

        private void Interrupt()
        {
            lock (_sync)
            {
                if (_handle != null && !_handle.IsClosed)
                {
                    CancelIoEx(_handle, IntPtr.Zero);
                    _handle.Dispose();
                }
            }
        }

        internal void Start()
        {
            _thread = new Thread(Run) { IsBackground = true, Name = "KKS.ClothingQuery" };
            _thread.Start();
        }

        private void Run()
        {
            while (!_stop.IsCancellationRequested)
            {
                bool connected = false;
                bool failureReported = false;
                string stage = "接続準備";
                try
                {
                    using (var handle = new SafePipeHandle(
                        CreateNamedPipe(@"\\.\pipe\" + _name, 3, 0, 1, 1024, 1024, 0, IntPtr.Zero), true))
                    {
                        if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
                        lock (_sync) { if (_stop.IsCancellationRequested) return; _handle = handle; }
                        stage = "接続受付";
                        if (!ConnectNamedPipe(handle, IntPtr.Zero) && Marshal.GetLastWin32Error() != 535)
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                        connected = true;
                        using (var pipe = new NamedPipeServerStream(PipeDirection.InOut, false, true, handle))
                        using (var io = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token))
                        using (var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 1024, true))
                        using (var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, true))
                        {
                            // A silent client cannot hold the only listener indefinitely.
                            io.CancelAfter(3000);
                            using (io.Token.Register(Interrupt))
                            {
                                stage = "要求受信";
                                string line = ReadRequest(reader, io.Token);
                                // Keep the pipe alive long enough to return a query timeout.
                                io.CancelAfter(5000);
                                string reply;
                                stage = "衣装取得・JSON生成";
                                using (var query = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token))
                                {
                                    query.CancelAfter(3000);
                                    try { reply = _query(line, query.Token).GetAwaiter().GetResult(); }
                                    catch (Exception error)
                                    {
                                        if (_stop.IsCancellationRequested) return;
                                        var failure = error is OperationCanceledException
                                            ? (Exception)new TimeoutException("衣装取得が時間切れになりました（メインスレッドの処理待ちを含む）。", error)
                                            : new InvalidOperationException(stage + "に失敗しました：" + error.Message, error);
                                        // The failure response does not depend on Unity dispatch.
                                        reply = ClothingJson.Serialize(null, failure.Message);
                                        failureReported = true;
                                        _failure?.Invoke(failure);
                                    }
                                }
                                stage = "JSON返送";
                                writer.WriteLine(reply);
                                writer.Flush();
                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    if (_stop.IsCancellationRequested) return;
                    if (!failureReported)
                        _failure?.Invoke(new IOException(stage + "に失敗しました：" + error.Message, error));
                    // A rejected/disconnected request must not kill the listener.
                    // A failure before connection is terminal; never busy-retry it.
                    if (!connected) return;
                }
                finally { lock (_sync) _handle = null; }
            }
        }

        private static string ReadRequest(StreamReader reader, CancellationToken token)
        {
            var text = new StringBuilder();
            while (text.Length < 4096)
            {
                token.ThrowIfCancellationRequested();
                int next = reader.Read();
                if (next < 0) throw new IOException("衣装問い合わせの接続が閉じられました。");
                if (next == '\n') return text.ToString().TrimEnd('\r');
                text.Append((char)next);
            }
            throw new IOException("Clothing request too long");
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _stop.Cancel();
            Interrupt();
            if (_thread == null || _thread.Join(1000)) _stop.Dispose();
        }
    }
}
