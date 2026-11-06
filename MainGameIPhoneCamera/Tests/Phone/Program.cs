using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MainGameIPhoneCamera;
using Valve.VR;

internal static class Program
{
    private sealed class ConfigWindow
    {
        public string SearchString { get; set; }
        public bool DisplayingWindow { get; set; }
    }
    private sealed class ColdConfigWindow
    {
        private bool _visible, _built;
        private string _search = "";
        public bool DisplayingWindow { get => _visible; set { _visible = value; if (value) _built = true; } }
        public string SearchString
        {
            get => _search;
            private set { if (!_built) throw new InvalidOperationException("native settings list has not been built"); _search = value; }
        }
    }
    private static int checks;
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
    private sealed class QueueContext : SynchronizationContext
    {
        internal readonly ConcurrentQueue<Action> Queue = new();
        public override void Post(SendOrPostCallback callback, object state) => Queue.Enqueue(() => callback(state));
        internal void Drain() { while (Queue.TryDequeue(out var callback)) callback(); }
        internal async Task Ready()
        {
            for (int i = 0; i < 100 && Queue.IsEmpty; i++) await Task.Delay(10);
            Check(!Queue.IsEmpty, "deadline dispatched to main context");
        }
    }
    private sealed class ExtraApp : IPhoneApp
    {
        public string Id { get; set; }
        public string Title => Id;
        public UnityEngine.Sprite Icon => null;
        public void Open(UnityEngine.RectTransform viewport) { }
        public void Close() { }
        public void Navigate(int x, int y) { }
        public void Confirm() { }
        public void Trigger() { }
    }
    private static void StateTests()
    {
        DateTime time = new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
        var p = new PhoneState();
        Check(p.Page == PhonePage.Locked, "initial screen is locked");
        p.Grab(time); p.Show(PhonePage.Camera, time); p.Release(time);
        Check(p.Deadline == time.AddSeconds(60), "release schedules 60-second lock");
        Check(!p.Expire(time.AddSeconds(59.999)) && p.Page == PhonePage.Camera, "no early lock");
        p.Grab(time.AddSeconds(59)); Check(p.Page == PhonePage.Camera && !p.Deadline.HasValue, "pickup before 60s preserves app and cancels idle");
        p.Release(time.AddSeconds(60));
        Check(p.Expire(time.AddSeconds(120)) && p.Page == PhonePage.Locked && p.Deadline == time.AddSeconds(150), "lock at 60s, screen-off deadline 30s later");
        Check(!p.Expire(time.AddSeconds(149.999)), "no early screen off");
        Check(p.Expire(time.AddSeconds(150)) && p.Page == PhonePage.Off && !p.Deadline.HasValue, "screen off at 90s");
        p.Grab(time.AddSeconds(151)); Check(p.Page == PhonePage.Locked && p.Held, "pickup of sleeping phone wakes locked screen");
        p.Show(PhonePage.Home, time); p.SetMediaActive(true, time); p.Release(time);
        Check(!p.Deadline.HasValue && !p.Expire(time.AddHours(2)) && p.Page == PhonePage.Home, "recording/playback exempts idle lock");
        p.SetMediaActive(false, time.AddHours(2));
        Check(p.Deadline == time.AddHours(2).AddSeconds(60), "media stop while unheld starts a fresh 60 seconds");
        p.Grab(time.AddHours(2).AddSeconds(10)); p.Show(PhonePage.Gallery, time);
        Check(!p.Expire(time.AddDays(1)) && p.Page == PhonePage.Gallery, "holding phone does not time out");
        p.Release(time); Check(p.Expire(time.AddSeconds(120)) && p.Page == PhonePage.Off, "late deadline dispatch still follows original release time");
        p.Grab(time.AddSeconds(121)); p.Release(time.AddSeconds(122));
        Check(p.Deadline == time.AddSeconds(152), "already locked phone sleeps after 30 seconds");
        Check(PhoneGrid.Move(2, 20, 1, 0, 3) == 2 && PhoneGrid.Move(1, 20, 0, 1, 3) == 1, "D-pad respects grid edges");
        Check(PhoneGrid.Move(6, 20, 0, -1, 3) == 9 && PhoneGrid.Move(9, 20, 0, 1, 3) == 6, "grid navigation supports additional app/media pages");
        Check(PhoneGrid.Move(4, 5, 1, 0, 3) == 4, "partial grid row has no invisible selection");
        foreach (var size in new[] { (4000, 3000), (1080, 1920), (1125, 2436), (300, 300) })
        foreach (float zoom in new[] { 1f, 2f, 8f })
        foreach (float position in new[] { 0f, .5f, 1f })
        {
            var draft = new WallpaperDraft { X = position, Y = position, Zoom = zoom, ForLock = true };
            var crop = draft.Crop(size.Item1, size.Item2);
            Check(crop.X >= 0 && crop.Y >= 0 && crop.X + crop.Width <= 1.00001f && crop.Y + crop.Height <= 1.00001f, "crop stays inside source");
            Check(Math.Abs(size.Item1 * crop.Width / (size.Item2 * crop.Height) - 1125f / 2436) < .0001f, "crop matches phone portrait aspect");
        }
        var editor = new WallpaperDraft { Edit = 1 };
        for (int i = 0; i < 100; i++) editor.Move(-1, 1);
        Check(editor.X == 0 && editor.Y == 1, "wallpaper position boundaries");
        editor.Edit = 2; for (int i = 0; i < 100; i++) editor.Move(0, 1);
        Check(editor.Zoom == 8, "wallpaper zoom limit");
        editor.Reset(); Check(editor.X == .5f && editor.Y == .5f && editor.Zoom == 1 && editor.Edit == 0, "wallpaper reset");
    }
    private static void NativeInputTests()
    {
        var cold = new ColdConfigWindow(); var coldBridge = new PhoneConfigWindow();
        coldBridge.Open(cold);
        Check(cold.DisplayingWindow && cold.SearchString == PhoneConfigWindow.Query, "cold F1 open builds settings before invoking the native private search setter");
        coldBridge.Close();
        Check(!cold.DisplayingWindow && cold.SearchString == "", "cold F1 open restores original closed state and search");
        var manager = new ConfigWindow { SearchString = "old query", DisplayingWindow = false };
        var window = new PhoneConfigWindow(); window.Open(manager); window.Open(manager);
        Check(manager.DisplayingWindow && manager.SearchString == "MainGameIPhoneCamera", "settings opens actual window/search API at phone plugin name");
        window.Close(); window.Close();
        Check(!manager.DisplayingWindow && manager.SearchString == "old query", "settings restores prior UI without changing config");
        manager.DisplayingWindow = true; window.Open(manager); manager.SearchString = "user changed search"; window.Close();
        Check(manager.DisplayingWindow && manager.SearchString == "user changed search", "settings cleanup does not overwrite independent user changes");
        var stockSet = new SteamVR_ActionSet { Path = "/actions/legacy_emulate", Active = true };
        var stock = new SteamVR_Action_Boolean { Path = "/actions/legacy_emulate/in/axis0_press" };
        SteamVR_Input.actionSets = new[] { stockSet }; SteamVR_Input.actions = new SteamVR_Action[] { stock };
        SteamVR_Input.actionsBoolean = new[] { stock }; SteamVR_Input.actionsIn = new ISteamVR_Action_In[] { stock };
        SteamVR_Input.actionsNonPoseNonSkeletonIn = new ISteamVR_Action_In[] { stock };
        int a = 0, b = 0;
        var phone = new PhoneButtonInput(() => a++, () => b++); phone.Connect(); phone.Connect();
        Check(SteamVR_Input.actionSets.Length == 2 && SteamVR_Input.actions.Length == 3, "dedicated action registration is idempotent");
        var confirm = SteamVR_Input.actionsBoolean.Single(x => x.Path == "/actions/iphone/in/confirm");
        var back = SteamVR_Input.actionsBoolean.Single(x => x.Path == "/actions/iphone/in/back");
        confirm.Down(SteamVR_Input_Sources.RightHand); Check(a == 0, "phone actions inactive before grab");
        phone.Active(true);
        confirm.Down(SteamVR_Input_Sources.LeftHand); back.Down(SteamVR_Input_Sources.LeftHand);
        Check(a == 0 && b == 0, "left A/B are not subscribed");
        confirm.Down(SteamVR_Input_Sources.RightHand); back.Down(SteamVR_Input_Sources.RightHand);
        Check(a == 1 && b == 1, "dedicated right A/B dispatch separately");
        stock.Down(SteamVR_Input_Sources.RightHand); Check(b == 1, "stock stick click cannot trigger back");
        phone.Active(false); back.Down(SteamVR_Input_Sources.RightHand); Check(b == 1, "release disables actions");
        var another = new SteamVR_Action_Boolean { Path = "/actions/other/in/test" };
        SteamVR_Input.actions = SteamVR_Input.actions.Concat(new[] { another }).ToArray();
        phone.Dispose(); phone.Dispose();
        Check(confirm.Listeners == 0 && back.Listeners == 0, "action listeners removed");
        Check(SteamVR_Input.actions.Length == 2 && SteamVR_Input.actions.Contains(stock) && SteamVR_Input.actions.Contains(another), "cleanup preserves stock and other-plugin actions");
        Check(SteamVR_Input.actionSets.Single() == stockSet && stockSet.Active, "cleanup preserves stock action set");
        SteamVR_Input.FailHandles = true; bool failed = false;
        try { new PhoneButtonInput(() => { }, () => { }).Connect(); } catch (InvalidOperationException) { failed = true; }
        Check(failed && SteamVR_Input.actionSets.Length == 1 && SteamVR_Input.actions.Length == 2, "partial action initialization failure cleans registration");
        SteamVR_Input.FailHandles = false;
        var registrations = Enumerable.Range(0, 20).Select(i => IPhoneAppApi.Register(new ExtraApp { Id = "extra" + i })).ToArray();
        Check(IPhoneAppApi.Snapshot().Length == 20, "app registry has no two-app limit");
        bool duplicate = false; try { IPhoneAppApi.Register(new ExtraApp { Id = "camera" }); } catch (ArgumentException) { duplicate = true; }
        Check(duplicate, "extensions cannot replace built-in apps");
        foreach (var r in registrations) { r.Dispose(); r.Dispose(); }
        Check(IPhoneAppApi.Snapshot().Length == 0, "extension unregister is idempotent");
    }
    private static async Task DeadlineTests()
    {
        var context = new QueueContext(); int calls = 0;
        using var deadline = new PhoneDeadline(context);
        deadline.Arm(TimeSpan.FromMilliseconds(5), () => calls++);
        await context.Ready(); Check(calls == 0, "timer never executes Unity callback on its worker");
        context.Drain(); Check(calls == 1, "deadline executes once on main context");
        deadline.Arm(TimeSpan.FromMilliseconds(5), () => calls++);
        await context.Ready(); deadline.Cancel(); context.Drain(); Check(calls == 1, "cancel rejects already-posted stale deadline");
        deadline.Arm(TimeSpan.FromMilliseconds(5), () => calls += 10);
        await context.Ready(); deadline.Arm(TimeSpan.FromMilliseconds(5), () => calls++); context.Drain();
        await context.Ready(); context.Drain(); Check(calls == 2, "rearm supersedes old generation");
        deadline.Arm(TimeSpan.FromMilliseconds(5), () => calls++);
        await context.Ready(); deadline.Dispose(); context.Drain(); Check(calls == 2, "destroy rejects pending callback");
    }
    private static async Task Run(string executable, string arguments)
    {
        using var p = new Process { StartInfo = new ProcessStartInfo(executable, arguments) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true } };
        p.Start(); var error = p.StandardError.ReadToEndAsync(); await p.WaitForExitAsync();
        if (p.ExitCode != 0) throw new Exception(await error);
    }
    private static async Task LibraryTests(string ffmpeg, string root)
    {
        string capture = Path.Combine(root, "cap"), imported = Path.Combine(root, "import");
        Directory.CreateDirectory(capture); Directory.CreateDirectory(imported);
        string own = Path.Combine(capture, "iPhone_test.png"), other = Path.Combine(capture, "other.png");
        await Run(ffmpeg, "-v error -y -f lavfi -i color=c=red:s=64x128 -frames:v 1 -update 1 " + MovieWriter.Quote(own));
        File.Copy(own, other, true); File.Copy(own, Path.Combine(imported, "portrait.jpg"), true);
        string movie = Path.Combine(imported, "movie.mp4");
        await Run(ffmpeg, "-v error -y -f lavfi -i color=c=blue:s=64x128:r=10 -t 0.3 -an -c:v libx264 -pix_fmt yuv420p " + MovieWriter.Quote(movie));
        var scan = GalleryLibrary.Scan(capture, imported + ";" + imported + ";" + Path.Combine(root, "missing"), CancellationToken.None);
        Check(scan.Items.Length == 3 && scan.Items.Count(x => x.Video) == 1, "gallery combines phone captures and imported images/video without duplicates");
        Check(!scan.Items.Any(x => x.Path == other) && !string.IsNullOrEmpty(scan.Error), "default capture filter and missing-folder feedback");
        var photoBytes = await GalleryLibrary.Thumbnail(ffmpeg, Path.Combine(root, "cache"), scan.Items.First(x => !x.Video), CancellationToken.None);
        var movieBytes = await GalleryLibrary.Thumbnail(ffmpeg, Path.Combine(root, "cache"), scan.Items.Single(x => x.Video), CancellationToken.None);
        Check(photoBytes.Length > 0 && movieBytes.Length > 0 && photoBytes[0] == 0xff && movieBytes[0] == 0xff, "actual image/video JPEG thumbnails generated");
        var repeat = await GalleryLibrary.Thumbnail(ffmpeg, Path.Combine(root, "cache"), scan.Items.Single(x => x.Video), CancellationToken.None);
        Check(repeat.SequenceEqual(movieBytes), "thumbnail cache reused");
        var compatible = await GalleryLibrary.CompatibleVideo(ffmpeg, Path.Combine(root, "playback"), movie, CancellationToken.None);
        Check(File.Exists(compatible) && new FileInfo(compatible).Length > 0 && compatible != movie, "actual H264 compatibility playback copy generated without altering original");
        var reused = await GalleryLibrary.CompatibleVideo(ffmpeg, Path.Combine(root, "playback"), movie, CancellationToken.None);
        Check(reused == compatible, "playback compatibility cache reused");
        var canceled = new CancellationToken(true); bool rejected = false;
        try { GalleryLibrary.Scan(capture, imported, canceled); } catch (OperationCanceledException) { rejected = true; }
        Check(rejected, "canceled old gallery scan rejected");
    }
    private static async Task Main(string[] args)
    {
        Console.WriteLine("[PhoneTests] Production state/deadline/crop/gallery/registry/native-action boundary tests; no game launch.");
        StateTests(); NativeInputTests(); await DeadlineTests();
        await LibraryTests(args[0], Path.Combine(args[1], Guid.NewGuid().ToString("N")));
        Console.WriteLine("PASS " + checks + " checks (Unity/SteamVR dispatch boundary doubles; real FFmpeg thumbnails).");
    }
}
