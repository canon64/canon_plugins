using System.Reflection;
using VRGIN.Controls;

namespace CanonPlugins.Shared
{
    // Operates only on a grab edge. Native SafeRelease leaves its lock valid
    // until all buttons are up; a new grip must finish that release first.
    internal static class VrItemFocus
    {
        private static readonly FieldInfo CurrentLock = typeof(Controller).GetField("_Lock", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly System.Type TouchType = System.Type.GetType("KKS_VR.Caress.CaressController, KKS_MainGameVR", false);
        internal static string DescribeCurrent(Controller controller)
        {
            var current = controller != null ? CurrentLock?.GetValue(controller) as Controller.Lock : null;
            if (current == null || !current.IsValid) return "free";
            if (current.IsInvalidating) return "retiring";
            var touch = TouchType != null ? controller.GetComponent(TouchType) : null;
            var owned = touch?.GetType().GetField("_lock", BindingFlags.Instance | BindingFlags.NonPublic);
            return owned != null && object.ReferenceEquals(owned.GetValue(touch), current) ? "native-caress" : "active-other";
        }
        // Called only after the item has passed its own grip-distance/permission checks.
        // Native body hovering owns an active lock before GripDown; let that exact
        // owner release its injected mouse input too, instead of stealing a raw lock.
        internal static bool YieldTouchOwner(object touch, Controller.Lock current)
        {
            if (touch == null || current == null || !current.IsValid) return false;
            var type = touch.GetType();
            var owned = type.GetField("_lock", BindingFlags.Instance | BindingFlags.NonPublic);
            var release = type.GetMethod("ReleaseLock", BindingFlags.Instance | BindingFlags.NonPublic);
            if (owned == null || release == null || !object.ReferenceEquals(owned.GetValue(touch), current)) return false;
            release.Invoke(touch, null);
            return !current.IsValid;
        }
        internal static bool TryAcquire(Controller controller, out Controller.Lock focus)
        {
            focus = null;
            if (controller == null) return false;
            var pending = CurrentLock?.GetValue(controller) as Controller.Lock;
            if (pending != null && pending.IsValid && !pending.IsInvalidating && TouchType != null)
                YieldTouchOwner(controller.GetComponent(TouchType), pending);
            if (pending != null && pending.IsValid && pending.IsInvalidating)
                pending.Release();
            // A fresh lock has IsInvalidating=false; reusing a retiring lock cannot
            // clear that flag and lets the native host unexpectedly release it.
            return controller.TryAcquireFocus(out focus) && focus != null && focus.IsValid;
        }
    }
}
