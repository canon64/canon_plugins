using System;
using System.Collections.Generic;
using System.Linq;
using MainGameAiShoujoMode;

namespace UnityEngine
{
    public class Object
    {
        internal static readonly List<Object> All = new List<Object>();
        public bool Destroyed;
        public Object() { All.Add(this); }
        public static Object[] FindObjectsOfType(Type type) => All.Where(value => !value.Destroyed && type.IsInstanceOfType(value)).ToArray();
        public static void Destroy(Object value) { if (value != null) value.Destroyed = true; }
    }
    public class GameObject : Object { }
    public class Behaviour : Object { public bool enabled = true; public GameObject gameObject = new GameObject(); }
}
namespace ksPlug
{
    public static class Core { public static Dealer dealer; }
    public class Dealer
    {
        public enum Context { Default, H }
        public HSession.HCtrl HCtrl;
        public Context Ctx = Context.H;
        public readonly List<string> Events = new List<string>();
        public void SetContext(Context context) { Ctx = context; Events.Add("default"); }
    }
}
namespace ksPlug.HSession
{
    public class Actor { public object Cha; }
    public class HCtrl : UnityEngine.Behaviour
    {
        public object HProc;
        public UnityEngine.Behaviour FpvCtrl = new UnityEngine.Behaviour();
        public Actor MainGirl = new Actor { Cha = new object() };
    }
    public class GroundPlane : UnityEngine.Behaviour { public object cha; }
    public static class HManage
    {
        public static bool Fail;
        public static void EndH()
        {
            Core.dealer.Events.Add("end");
            if (Fail) throw new InvalidOperationException("synthetic EndH failure");
        }
    }
}
internal static class Program
{
    private static void Check(bool result, string message)
    { if (!result) throw new Exception(message); Console.WriteLine("PASS " + message); }
    private static void Main()
    {
        Console.WriteLine("KSPLUG_EXIT_CHECKS_START");
        var proc = new object();
        var controller = new ksPlug.HSession.HCtrl { HProc = proc };
        var dealer = ksPlug.Core.dealer = new ksPlug.Dealer { HCtrl = controller };
        var ground = new ksPlug.HSession.GroundPlane { cha = controller.MainGirl.Cha };
        var unrelated = new ksPlug.HSession.GroundPlane { cha = new object() };
        var exit = new KsPlugHSessionExit();
        var assembly = typeof(ksPlug.Core).Assembly;
        exit.End(proc, typeof(string).Assembly);
        exit.End(new object(), assembly);
        Check(!dealer.Events.Any() && controller.enabled, "absent plugin and foreign HProc do nothing");
        exit.End(proc, assembly);
        Check(dealer.Events.SequenceEqual(new[] { "end", "default" }), "EndH precedes context reset while HProc exists");
        Check(!controller.enabled && controller.Destroyed && !controller.FpvCtrl.enabled && controller.FpvCtrl.Destroyed,
            "deferred controller and FPV callbacks stop before native unload");
        Check(!ground.enabled && ground.gameObject.Destroyed && unrelated.enabled && !unrelated.gameObject.Destroyed,
            "only owned persistent ground is removed");
        exit.End(proc, assembly);
        Check(dealer.Events.SequenceEqual(new[] { "end", "default" }), "duplicate exit is harmless");
        new KsPlugHSessionExit().End(proc, assembly);
        Check(dealer.Events.SequenceEqual(new[] { "end", "default" }), "already completed native EndH is not repeated");
        controller = new ksPlug.HSession.HCtrl { HProc = proc };
        dealer = ksPlug.Core.dealer = new ksPlug.Dealer { HCtrl = controller };
        ground = new ksPlug.HSession.GroundPlane { cha = controller.MainGirl.Cha };
        ksPlug.HSession.HManage.Fail = true;
        try { new KsPlugHSessionExit().End(proc, assembly); throw new Exception("expected failure"); }
        catch (System.Reflection.TargetInvocationException) { }
        Check(dealer.Ctx == ksPlug.Dealer.Context.Default && !controller.enabled && ground.gameObject.Destroyed,
            "EndH exception still resets context and removes callbacks and ground");
        Console.WriteLine("KSPLUG_EXIT_CHECKS_PASS");
    }
}
