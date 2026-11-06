using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MainGameAiShoujoMode;

namespace SaveData { public class Heroine { } public class Player { } }
namespace FreeH { public class FreeHScene { } public class FreeHBackData { public SaveData.Heroine heroine; public SaveData.Player player; } }
namespace HarmonyLib
{
    public class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string name) { } }
    public class HarmonyPrefix : Attribute { }
}
namespace UnityEngine
{
    public class GameObject
    {
        public List<FreeH.FreeHBackData> items = new List<FreeH.FreeHBackData>();
        public T[] GetComponents<T>() => items.Cast<T>().ToArray();
        public T GetComponent<T>() => items.Cast<T>().FirstOrDefault();
    }
    public static class Object
    {
        public static void DestroyImmediate(object value) => Manager.Scene.commonSpace.items.Remove((FreeH.FreeHBackData)value);
    }
}
namespace Manager
{
    public class Space : UnityEngine.GameObject { }
    public static class Scene { public static Space commonSpace = new Space(); }
}
namespace MainGameAiShoujoMode
{
    public sealed partial class AiModeHSession
    {
        private static AiModeHSession _active;
        private SaveData.Heroine _heroine = new SaveData.Heroine();
        private SaveData.Player _player = new SaveData.Player();
        public bool Busy = true;
        public static AiModeHSession Begin(string sourceId = "hotel-room-201")
        { _active = new AiModeHSession(); OwnedHReturnData.Register(sourceId, _active._heroine, _active._player); return _active; }
        public static IEnumerator Wrap(IEnumerator routine) { AfterOuterStart(ref routine); return routine; }
        public void FinishFirst() { RemoveOwnedReturnData(_heroine, _player); _active = null; Busy = false; }
        public void NativeReturn() => Manager.Scene.commonSpace.items.Add(new FreeH.FreeHBackData { heroine = _heroine, player = _player });
    }
}
internal static class Program
{
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static IEnumerator Native(AiModeHSession owner)
    {
        yield return null;
        owner.FinishFirst(); // Native unload dispatches cleanup before producing its return data.
        owner.NativeReturn();
    }
    private static IEnumerator Failure(AiModeHSession owner)
    {
        yield return null;
        owner.NativeReturn();
        throw new InvalidOperationException("native failure");
    }
    private static void Main()
    {
        var foreign = new FreeH.FreeHBackData { heroine = new SaveData.Heroine(), player = new SaveData.Player() };
        Manager.Scene.commonSpace.items.Add(foreign);
        var owner = AiModeHSession.Begin();
        var native = AiModeHSession.Wrap(Native(owner));
        Require(native.MoveNext(), "Native yield lost");
        Require(!native.MoveNext(), "Native completion lost");
        Require(Manager.Scene.commonSpace.items.SequenceEqual(new[] { foreign }), "Late return leaked or foreign return was deleted");
        var ordinary = Native(owner);
        Require(ReferenceEquals(ordinary, AiModeHSession.Wrap(ordinary)), "Normal Free H was wrapped");
        owner = AiModeHSession.Begin();
        var failing = AiModeHSession.Wrap(Failure(owner)); failing.MoveNext();
        try { failing.MoveNext(); throw new Exception("Native exception swallowed"); } catch (InvalidOperationException) { }
        Require(Manager.Scene.commonSpace.items.SequenceEqual(new[] { foreign }), "Failure leaked owned data");
        ((IDisposable)failing).Dispose();
        owner.FinishFirst();
        owner = AiModeHSession.Begin();
        var aborted = AiModeHSession.Wrap(Native(owner)); aborted.MoveNext(); owner.NativeReturn();
        ((IDisposable)aborted).Dispose(); ((IDisposable)aborted).Dispose();
        Require(Manager.Scene.commonSpace.items.SequenceEqual(new[] { foreign }), "Cancellation leaked data or repeated cleanup damaged foreign data");
        owner.FinishFirst();
        owner.NativeReturn(); // A producer outlives the disposed session and its enumerator.
        OwnedHReturnData.RemoveOwned();
        Require(Manager.Scene.commonSpace.items.SequenceEqual(new[] { foreign }), "Normal Free H could consume a late hotel return after disposal");
        OwnedHReturnData.RemoveOwned();
        Require(Manager.Scene.commonSpace.items.SequenceEqual(new[] { foreign }), "Native Free H return data was modified");
        owner.NativeReturn();
        Manager.Scene.commonSpace.items.Last().player = new SaveData.Player();
        var consumed = OwnedHReturnData.GetNativeReturn(Manager.Scene.commonSpace);
        Require(ReferenceEquals(consumed, foreign) && Manager.Scene.commonSpace.items.SequenceEqual(new[] { foreign }), "Read boundary leaked hotel data after partner replacement");
        foreach (var source in new[] { "normal-free-h", "ai-island", "hotel-room-unknown" })
        {
            var unrelated = new FreeH.FreeHBackData { heroine = new SaveData.Heroine(), player = new SaveData.Player() };
            OwnedHReturnData.Register(source, unrelated.heroine, unrelated.player);
            Manager.Scene.commonSpace.items.Add(unrelated);
            OwnedHReturnData.GetNativeReturn(Manager.Scene.commonSpace);
            Require(Manager.Scene.commonSpace.items.Contains(unrelated), "201 isolation affected " + source);
        }
        foreach (string room in new[] { "202", "203", "204", "205", "206", "reception-suite" })
        {
            var beforeSuite = Manager.Scene.commonSpace.items.ToArray();
            owner = AiModeHSession.Begin("hotel-room-" + room);
            owner.FinishFirst(); owner.NativeReturn();
            Manager.Scene.commonSpace.items.Last().player = new SaveData.Player();
            OwnedHReturnData.GetNativeReturn(Manager.Scene.commonSpace);
            Require(Manager.Scene.commonSpace.items.SequenceEqual(beforeSuite), room + " leaked a late native return after disposal/partner replacement");
        }
        Console.WriteLine("SESSION_ISOLATION_PASS lateReturn=true normalFreeHUntouched=true foreignPreserved=true failure=true cancellation=true repeatedCleanup=true receptionSuite=true");
    }
}
