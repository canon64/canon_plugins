using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace MainGameAiShoujoMode.Conversation
{
#pragma warning disable CS0649
    [DataContract] internal sealed class AiPipeNativeSettings
    {
        [DataMember] public uint access, openExisting, overlapped, readMode;
        [DataMember] public int pendingError, moreDataError;
    }
#pragma warning restore CS0649
    // Unity's Mono leaves ConnectAsync and IsMessageComplete unimplemented. Use the Windows
    // message-pipe API directly, with overlapped I/O so cancellation never needs a polling loop.
    internal sealed class AiConversationPipeNative : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] private struct Overlapped
        { internal IntPtr Internal, InternalHigh; internal uint Offset, OffsetHigh; internal IntPtr Event; }
        private readonly AiPipeNativeSettings _settings;
        private readonly CancellationToken _cancel;
        private readonly string _path;
        private readonly object _gate = new object();
        private SafeFileHandle _handle;
        private bool _disposed;
        internal AiConversationPipeNative(AiPipeNativeSettings settings, CancellationToken cancel, string name)
        { _settings = settings; _cancel = cancel; _path = @"\\.\pipe\" + name; }
        internal void Connect(int timeout)
        {
            _cancel.ThrowIfCancellationRequested();
            if (!WaitNamedPipe(_path, timeout)) throw new Win32Exception(Marshal.GetLastWin32Error());
            var handle = CreateFile(_path, _settings.access, default(uint), IntPtr.Zero, _settings.openExisting, _settings.overlapped, IntPtr.Zero);
            if (handle.IsInvalid) { var error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error); }
            lock (_gate)
            {
                if (_disposed || _cancel.IsCancellationRequested) { handle.Dispose(); _cancel.ThrowIfCancellationRequested(); throw new ObjectDisposedException(nameof(AiConversationPipeNative)); }
                _handle = handle;
            }
            var mode = _settings.readMode;
            if (!SetNamedPipeHandleState(handle, ref mode, IntPtr.Zero, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        private int Transfer(byte[] bytes, bool write, out bool more)
        {
            SafeFileHandle handle; bool retained = false;
            lock (_gate)
            {
                _cancel.ThrowIfCancellationRequested();
                if (_disposed || _handle == null) throw new ObjectDisposedException(nameof(AiConversationPipeNative));
                handle = _handle; handle.DangerousAddRef(ref retained);
            }
            var pinned = default(GCHandle); var native = IntPtr.Zero;
            try
            {
                using (var ready = new EventWaitHandle(false, EventResetMode.ManualReset))
                {
                    pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                    native = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Overlapped)));
                    Marshal.StructureToPtr(new Overlapped { Event = ready.SafeWaitHandle.DangerousGetHandle() }, native, false);
                    uint count;
                    var raw = handle.DangerousGetHandle();
                    var ok = write ? WriteFile(raw, pinned.AddrOfPinnedObject(), bytes.Length, out count, native)
                        : ReadFile(raw, pinned.AddrOfPinnedObject(), bytes.Length, out count, native);
                    var error = ok ? default(int) : Marshal.GetLastWin32Error();
                    if (error == _settings.pendingError)
                    {
                        var completed = WaitHandle.WaitAny(new WaitHandle[] { ready, _cancel.WaitHandle }) == default(int);
                        if (!completed || _cancel.IsCancellationRequested)
                        {
                            CancelIoEx(raw, native);
                            // Complete cancellation before unpinning the buffer/OVERLAPPED memory.
                            GetOverlappedResult(raw, native, out count, true);
                            _cancel.ThrowIfCancellationRequested();
                        }
                    }
                    if (ok || error == _settings.pendingError || error == _settings.moreDataError)
                    {
                        ok = GetOverlappedResult(raw, native, out count, false);
                        error = ok ? default(int) : Marshal.GetLastWin32Error();
                    }
                    more = !write && error == _settings.moreDataError;
                    if (error != default(int) && !more) throw new Win32Exception(error);
                    return checked((int)count);
                }
            }
            finally
            {
                if (native != IntPtr.Zero) Marshal.FreeHGlobal(native);
                if (pinned.IsAllocated) pinned.Free();
                if (retained) handle.DangerousRelease();
            }
        }
        internal void Write(byte[] bytes)
        { if (Transfer(bytes, true, out _) != bytes.Length) throw new IOException("会話メッセージを最後まで送信できませんでした。"); }
        internal byte[] Read(int limit)
        {
            using (var result = new MemoryStream())
            {
                var buffer = new byte[limit]; bool more;
                do
                {
                    var count = Transfer(buffer, false, out more);
                    if (count == default(int)) throw new EndOfStreamException("Human_2との接続が閉じられました。");
                    result.Write(buffer, default(int), count);
                    if (result.Length > limit || (more && result.Length == limit)) throw new InvalidDataException("会話データが上限を超えています。");
                } while (more);
                return result.ToArray();
            }
        }
        public void Dispose() { lock (_gate) { if (_disposed) return; _disposed = true; _handle?.Dispose(); } }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool WaitNamedPipe(string name, int timeout);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetNamedPipeHandleState(SafeFileHandle pipe, ref uint mode, IntPtr collectionCount, IntPtr timeout);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ReadFile(IntPtr file, IntPtr bytes, int size, out uint read, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool WriteFile(IntPtr file, IntPtr bytes, int size, out uint written, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetOverlappedResult(IntPtr file, IntPtr overlapped, out uint count, bool wait);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CancelIoEx(IntPtr file, IntPtr overlapped);
    }
}
