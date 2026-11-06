using System;
using System.Reflection;
using VRGIN.Controls;

namespace MainGirlHipHijack
{
    // Optional connection is resolved once; no plugin discovery or polling during head input.
    internal static class SyringeGripPriority
    {
        internal static readonly Func<Controller, bool> HasPriority = Bind(Type.GetType("MainGameSyringe.Plugin, MainGameSyringe", false));

        internal static Func<Controller, bool> Bind(Type type)
        {
            var method = type?.GetMethod("HasGripPriority", BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(Controller) }, null);
            return method != null && method.ReturnType == typeof(bool)
                ? (Func<Controller, bool>)Delegate.CreateDelegate(typeof(Func<Controller, bool>), method)
                : controller => false;
        }
    }
}
