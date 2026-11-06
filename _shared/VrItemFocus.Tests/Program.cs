using System;
using CanonPlugins.Shared;
using VRGIN.Controls;

namespace VRGIN.Controls
{
    // Matches the installed VRGIN lock lifetime: SafeRelease cannot be cancelled;
    // TryAcquire rejects any still-valid lock, including retiring native tool locks.
    public class Controller
    {
        public object GetComponent(Type type) => null;
        private Lock _Lock;
        public bool TryAcquireFocus(out Lock focus)
        {
            focus = null;
            if (_Lock != null && _Lock.IsValid) return false;
            focus = _Lock = new Lock(); return true;
        }
        public void NativeButtonsReleased()
        { if (_Lock != null && _Lock.IsInvalidating) _Lock.Release(); }
        public class Lock
        {
            public bool IsValid = true, IsInvalidating;
            public int Releases;
            public void SafeRelease() { IsInvalidating = true; }
            public void Release() { IsValid = false; Releases++; }
        }
    }
}

internal static class Program
{
    private sealed class NativeTouch
    {
        private Controller.Lock _lock;
        internal bool MouseDown = true;
        internal int Cleanups;
        internal NativeTouch(Controller.Lock focus) { _lock=focus; }
        private void ReleaseLock() { MouseDown=false; Cleanups++; _lock.Release(); _lock=null; }
    }
    private static int checks;
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); checks++; }
    private static void Main()
    {
        Console.WriteLine("[VrItemFocus] Production focus acquisition with the native controller boundary replaced.");
        foreach (var hand in new[] { new Controller(), new Controller() })
        {
            hand.TryAcquireFocus(out var nativeTool);
            Check(!VrItemFocus.TryAcquire(hand, out _) && nativeTool.Releases == 0, "live native or item focus must not be stolen");
            nativeTool.SafeRelease();
            Check(VrItemFocus.TryAcquire(hand, out var current) && nativeTool.Releases == 1 && !current.IsInvalidating, "retiring foreign/native focus is completed before a fresh grab");
            for (int i = 0; i < 100; i++)
            {
                Check(!VrItemFocus.TryAcquire(hand, out _) && current.IsValid, "another item cannot steal a held hand");
                var previous = current; previous.SafeRelease();
                Check(VrItemFocus.TryAcquire(hand, out current) && !previous.IsValid && current != previous && !current.IsInvalidating, "rapid cross-item handoff creates a fresh lock");
                hand.NativeButtonsReleased();
                Check(current.IsValid, "native cleanup cannot invalidate a lock reused in retirement");
            }
            current.SafeRelease(); hand.NativeButtonsReleased();
            Check(VrItemFocus.TryAcquire(hand, out current) && current.IsValid, "fully released hand remains reusable");
            current.Release();
        }
        Check(!VrItemFocus.TryAcquire(null, out _), "missing controller is safe");
        var ownerHand=new Controller();ownerHand.TryAcquireFocus(out var hover);
        var touch=new NativeTouch(hover);
        Check(VrItemFocus.YieldTouchOwner(touch,hover)&&!hover.IsValid&&!touch.MouseDown&&touch.Cleanups==1,"body hover yields through native cleanup including mouse up");
        Check(VrItemFocus.TryAcquire(ownerHand,out var item),"selected item can acquire after body hover yields");
        var unrelated=new NativeTouch(new Controller.Lock());
        Check(!VrItemFocus.YieldTouchOwner(unrelated,item)&&item.IsValid&&unrelated.Cleanups==0,"live item lock cannot be released through an unrelated touch owner");
        Console.WriteLine("PASS " + checks + " focus lifetime checks; no game or VR execution.");
    }
}
