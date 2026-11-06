using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace MainGameDevelopmentBridge
{
    internal sealed class PipeServer : IDisposable
    {
        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();
        private readonly object _sync = new object();
        private SafePipeHandle _handle;
        private readonly string _name;
        private readonly Func<Dictionary<string, object>, Task<object>> _dispatch;
        private readonly TaskCompletionSource<bool> _completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task Completion => _completion.Task;
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateNamedPipe(string name, uint access, uint mode, uint instances, uint outputSize, uint inputSize, uint timeout, IntPtr security);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ConnectNamedPipe(SafePipeHandle handle, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CancelIoEx(SafePipeHandle handle, IntPtr overlapped);
        internal PipeServer(string name, Func<Dictionary<string, object>, Task<object>> dispatch)
        {
            _name = name; _dispatch = dispatch;
            new Thread(Listen) { IsBackground = true, Name = "KKS.DevelopmentBridge.Pipe" }.Start();
        }
        private void Listen()
        {
            try
            {
            var json = new BridgeJson();
            while (!_cancel.IsCancellationRequested)
            {
                try
                {
                    // Unity Mono rejects asynchronous pipe flags; own a synchronous Win32 handle.
                    using (var handle = new SafePipeHandle(CreateNamedPipe(@"\\.\pipe\" + _name, 3, 0, 1, 4096, 4096, 0, IntPtr.Zero), true))
                    {
                        if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
                        lock (_sync) { if (_cancel.IsCancellationRequested) break; _handle = handle; }
                        if (!ConnectNamedPipe(handle, IntPtr.Zero) && Marshal.GetLastWin32Error() != 535)
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                        using (var pipe = new NamedPipeServerStream(PipeDirection.InOut, false, true, handle))
                        using (var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 4096, true))
                        using (var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, true) { AutoFlush = true })
                        {
                            string line = reader.ReadLine();
                            if (line == null) continue;
                            object response;
                            try
                            {
                                var request = json.Deserialize<Dictionary<string, object>>(line);
                                var result = _dispatch(request).GetAwaiter().GetResult();
                                response = new { ok = true, result };
                            }
                            catch (Exception ex) { response = new { ok = false, error = ex.GetBaseException().ToString() }; }
                            writer.WriteLine(json.Serialize(response));
                        }
                    }
                }
                catch (Exception) when (_cancel.IsCancellationRequested) { break; }
                catch (IOException) { /* A disconnected client does not stop the next request. */ }
                finally { lock (_sync) _handle = null; }
            }
            _completion.TrySetResult(true);
            }
            catch (Exception ex) { _completion.TrySetException(ex); }
        }
        public void Dispose()
        {
            _cancel.Cancel();
            lock (_sync) { if (_handle != null && !_handle.IsClosed) { CancelIoEx(_handle, IntPtr.Zero); _handle.Dispose(); } }
        }
    }
}
