using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using MainGameVoiceFaceEventBridge.Infrastructure;
using MainGameNotifications;

namespace MainGameVoiceFaceEventBridge.ClothingRuntime
{
    internal static class Program
    {
        // Reproduce the previous ABI on the actual KKS runtime, in a separate
        // process which neither loads Unity nor connects to a game pipe.
        [DllImport("kernel32.dll", EntryPoint = "CreateNamedPipeW", CharSet = CharSet.Unicode)]
        private static extern SafePipeHandle LegacyCreate(string name, uint access, uint mode,
            uint instances, uint output, uint input, uint timeout, IntPtr security);

        private sealed class MainContext : SynchronizationContext, IDisposable
        {
            internal readonly BlockingCollection<Action> Work = new BlockingCollection<Action>();
            private int _delay;
            internal MainContext(int delay) { _delay = delay; }
            public override void Post(SendOrPostCallback callback, object state)
            {
                Work.Add(() =>
                {
                    int delay = Interlocked.Exchange(ref _delay, 0);
                    if (delay > 0) Thread.Sleep(delay);
                    callback(state);
                });
            }
            public void Dispose() { Work.Dispose(); }
        }

        private static int Main(string[] args)
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            if (args[0] == "--legacy")
            {
                try
                {
                    using (LegacyCreate(@"\\.\pipe\clothing_legacy_" + Guid.NewGuid().ToString("N"), 3, 0, 1, 1024, 1024, 0, IntPtr.Zero)) { }
                    Console.WriteLine("LEGACY_UNEXPECTED_SUCCESS");
                    return 1;
                }
                catch (Exception error)
                {
                    Console.WriteLine("LEGACY_FAILURE " + error.Message);
                    return error.Message.Contains("constructor required") ? 0 : 1;
                }
            }
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            using (var context = new MainContext(args.Length > 1 ? int.Parse(args[1]) : 0))
            {
                NotificationApi.Initialize(new NotificationStore());
                var dispatcher = new ClothingQueryDispatcher(context, line =>
                {
                    if (Thread.CurrentThread.ManagedThreadId != mainThread) throw new Exception("Wrong acquisition thread");
                    int main = ClothingJson.ReadMain(line);
                    if (main == 99) throw new InvalidOperationException("衣装メタデータがありません。");
                    var snapshot = new ClothingSnapshot { Token = "fixture-only", CoordinateName = "私服" };
                    for (int i = 0; i < 9; i++) snapshot.Add(i, i + 100, i == 0, "MODの\"青い服\"", "半脱２");
                    snapshot.AddAccessory("黒縁眼鏡", true);
                    return ClothingJson.Serialize(snapshot);
                });
                using (var server = new ClothingQueryServer(args[0], dispatcher.Query, error => context.Post(_ =>
                {
                    NotificationApi.Publish("clothing-test", "衣装情報の取得に失敗", error.Message, NotificationSeverity.Failure);
                    Console.WriteLine("NOTIFICATION " + NotificationApi.GetNotifications()[0].Body);
                    Console.Out.Flush();
                }, null)))
                {
                    bool stop = false;
                    new Thread(() => { Console.ReadLine(); context.Work.Add(() => stop = true); }) { IsBackground = true }.Start();
                    server.Start();
                    Console.WriteLine("READY"); Console.Out.Flush();
                    while (!stop) context.Work.Take()();
                }
                NotificationApi.Shutdown();
            }
            return 0;
        }
    }
}
