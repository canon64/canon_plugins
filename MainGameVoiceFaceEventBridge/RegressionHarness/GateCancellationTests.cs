using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using MainGameVoiceFaceEventBridge.External;

namespace MainGameVoiceFaceEventBridge.RegressionHarness
{
    internal static partial class Program
    {
        private static void CharacterizeGateCancellation()
        {
            using (var received = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            using (var finished = new ManualResetEventSlim())
            using (var cancel = new CancellationTokenSource())
            {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                var server = new Thread(() =>
                {
                    try { using (var connection = listener.AcceptTcpClient()) { received.Set(); release.Wait(4000); } }
                    catch (SocketException) { }
                }) { IsBackground = true };
                server.Start();
                var sender = new Thread(() =>
                {
                    try
                    {
                        new SubtitleEventClient(_ => { }, _ => { }).Send(
                            new SubtitleEventClientOptions { Host = "127.0.0.1", Port = port, EndpointPath = "/test" },
                            new SubtitleEventMessage { Text = "cancel test" }, cancel.Token);
                    }
                    finally { finished.Set(); }
                }) { IsBackground = true };
                try
                {
                    sender.Start();
                    Assert(received.Wait(3000), "cancellable subtitle request reached loopback server");
                    cancel.Cancel();
                    Assert(finished.Wait(750), "gate stop aborts in-flight subtitle HTTP immediately");
                }
                finally { release.Set(); listener.Stop(); sender.Join(3000); server.Join(3000); }
            }
        }
    }
}
