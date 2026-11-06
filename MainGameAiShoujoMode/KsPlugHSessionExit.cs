using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MainGameAiShoujoMode
{
    // Event-only compatibility boundary for the HProc owned by this session.
    internal sealed class KsPlugHSessionExit
    {
        private bool _ended;
        internal void End(object proc, Assembly assembly = null)
        {
            if (_ended || proc == null) return;
            assembly = assembly ?? AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(item => item.GetName().Name == "ksPlug");
            if (assembly == null) return;
            var core = assembly.GetType("ksPlug.Core");
            var dealer = Read(core, null, "dealer");
            var controller = Read(dealer?.GetType(), dealer, "HCtrl") as Behaviour;
            if (controller == null || !ReferenceEquals(Read(controller.GetType(), controller, "HProc"), proc)
                || Convert.ToString(Read(dealer.GetType(), dealer, "Ctx")) != "H") return;
            _ended = true;
            var fpv = Read(controller.GetType(), controller, "FpvCtrl") as Behaviour;
            var actors = new HashSet<object>();
            foreach (var name in new[] { "MainGirl", "SecondGirl", "MainBoy", "SecondBoy" })
            {
                var actor = Read(controller.GetType(), controller, name);
                var cha = Read(actor?.GetType(), actor, "Cha");
                if (cha != null) actors.Add(cha);
            }
            try
            {
                // Run before characters and the H camera are unloaded, while native data is valid.
                assembly.GetType("ksPlug.HSession.HManage")?.GetMethod("EndH", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            }
            finally
            {
                try
                {
                    var setContext = dealer.GetType().GetMethod("SetContext", BindingFlags.Public | BindingFlags.Instance);
                    if (setContext == null) throw new MissingMethodException(dealer.GetType().FullName, "SetContext");
                    var contextType = setContext.GetParameters().Single().ParameterType;
                    setContext.Invoke(dealer, new[] { Enum.Parse(contextType, "Default") });
                }
                finally
                {
                    // Destroy is deferred by Unity; stop callbacks before scene references disappear.
                    if (controller != null) controller.enabled = false;
                    if (fpv != null) { fpv.enabled = false; Object.Destroy(fpv); }
                    if (controller != null) Object.Destroy(controller);
                    var groundType = assembly.GetType("ksPlug.HSession.GroundPlane");
                    if (groundType != null)
                        foreach (var ground in Object.FindObjectsOfType(groundType).OfType<Behaviour>())
                            if (actors.Contains(Read(groundType, ground, "cha")))
                            { ground.enabled = false; Object.Destroy(ground.gameObject); }
                }
            }
        }
        private static object Read(Type type, object instance, string name)
        {
            if (type == null) return null;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            var field = type.GetField(name, flags);
            return field != null ? field.GetValue(instance) : type.GetProperty(name, flags)?.GetValue(instance, null);
        }
    }
}
