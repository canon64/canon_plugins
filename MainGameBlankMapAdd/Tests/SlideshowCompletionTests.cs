using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using MainGameBlankMapAdd.Media.Images;

namespace MainGameBlankMapAdd.Characterization
{
    internal static class SlideshowCompletionTests
    {
        private static void Check(bool ok,string name) { if(!ok) throw new Exception(name); Console.WriteLine("PASS "+name); }
        internal static void Run()
        {
            var job=new SlideshowBackgroundJob<int>();
            using(var complete=new ManualResetEventSlim())
            {
                job.Request(()=>17,()=>complete.Set());
                Check(complete.Wait(3000),"slideshow completion notification arrives without frame polling");
                Check(job.TryTake(out int value,out var error)&&value==17&&error==null,"completed catalog is immediately consumable on notification");
            }
            using(var started=new ManualResetEventSlim())
            using(var release=new ManualResetEventSlim())
            using(var next=new ManualResetEventSlim())
            {
                job.Request(()=>{started.Set(); release.Wait(3000);return 1;},()=>{});
                Check(started.Wait(3000),"old session worker started");
                job.Reset();
                job.Request(()=>2,()=>next.Set());
                release.Set();
                Check(next.Wait(3000),"new session receives completion even when old dispatcher was discarded");
                next.Reset();
                Check(!job.TryTake(out _,out _),"old session result is discarded and pending worker begins");
                Check(next.Wait(3000)&&job.TryTake(out int value,out _)&&value==2,"new result is delivered without periodic retry");
            }
            string folder=Path.Combine(Path.GetTempPath(),"slideshow-events-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var queue=new ConcurrentQueue<Action>();
                int notifications=0;
                using(var posted=new ManualResetEventSlim())
                {
                    var events=new SlideshowFileEvents(folder,true,a=>{queue.Enqueue(a);posted.Set();},()=>notifications++);
                    File.WriteAllBytes(Path.Combine(folder,"new.png"),new byte[]{1});
                    Check(posted.Wait(3000),"new image raises OS file notification");
                    while(queue.TryDequeue(out var delivered)) delivered();
                    Check(notifications>0,"image notification is delivered on owning thread");
                    int beforeDispose=notifications;
                    events.Dispose();
                    while(queue.TryDequeue(out var action)) action();
                    Check(notifications==beforeDispose,"disposed session rejects queued image notification");
                }
            }
            finally { Directory.Delete(folder,true); }
        }
    }
}
