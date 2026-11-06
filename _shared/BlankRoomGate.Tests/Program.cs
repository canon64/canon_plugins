using System;
using System.IO;
using System.Reflection;
using CanonPlugins.Shared;

internal static class Program
{
    private static int _passed;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("PASS " + name);
        _passed++;
    }

    private static void Main()
    {
        Console.WriteLine("BlankRoomGate regression tests starting");
        string config = Path.Combine(AppContext.BaseDirectory, "BlankRoomGate.json");
        string previous = File.Exists(config) ? File.ReadAllText(config) : null;
        try
        {
            File.WriteAllText(config, "{\"RestrictToBlankRoom\":false}");
            UnityEngine.Time.frameCount = 1;
            typeof(BlankRoomGate).GetField("_nextResolveFrame", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, 2);
            Check(!BlankRoomGate.IsInBlankRoomStrict, "unresolved room state blocks execution");
            Check(BlankRoomGate.IsInBlankRoom, "legacy configurable gate preserves explicit override");
            UnityEngine.Time.frameCount++;
            Check(!BlankRoomGate.IsInBlankRoomStrict, "ordinary room remains blocked with override false");
            MainGameBlankMapAdd.BlankRoomState.InRoom = true;
            UnityEngine.Time.frameCount++;
            Check(BlankRoomGate.IsInBlankRoomStrict, "entering AllPoseRoom permits execution");
            int reads = MainGameBlankMapAdd.BlankRoomState.Reads;
            Check(BlankRoomGate.IsInBlankRoomStrict && MainGameBlankMapAdd.BlankRoomState.Reads == reads,
                "multiple hooks share one state read per frame");
            MainGameBlankMapAdd.BlankRoomState.InRoom = false;
            UnityEngine.Time.frameCount++;
            Check(!BlankRoomGate.IsInBlankRoomStrict, "leaving AllPoseRoom blocks next frame");
            MainGameBlankMapAdd.BlankRoomState.InRoom = true;
            UnityEngine.Time.frameCount++;
            Check(BlankRoomGate.IsInBlankRoomStrict, "reentering AllPoseRoom resumes execution");
            CoroutineTests.Run(Check);
            Console.WriteLine("Passed " + _passed + " tests");
        }
        finally
        {
            if (previous == null) File.Delete(config);
            else File.WriteAllText(config, previous);
        }
    }
}

namespace MainGameBlankMapAdd
{
    public static class BlankRoomState
    {
        public static bool InRoom;
        public static int Reads;
        public static bool IsInBlankRoom { get { Reads++; return InRoom; } }
    }
}

namespace UnityEngine
{
    internal static class Time { public static int frameCount; }
    internal static class Debug
    {
        public static void Log(object message) => Console.WriteLine(message);
        public static void LogWarning(object message) => Console.WriteLine(message);
    }
}
