using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MainGameAiShoujoMode;
using SaveData;

internal static class Program
{
    private static readonly List<string> Checks = new();
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Checks.Add(name); }
    private static void Main()
    {
        var original = new WorldData(); Manager.Game.Reset(original, true);
        var temporary = new WorldData();
        var scope = new NativeActionWorldScope(temporary);
        Check(ReferenceEquals(Manager.Game.saveData, temporary), "native Action sees only its temporary world");
        Check(!Manager.Game.isAutoSavePossible, "automatic saving starts disabled");
        Check(NativeActionWorldScope.Owns(temporary) && !NativeActionWorldScope.Owns(original), "ownership uses world identity");
        temporary.Save("manual"); temporary.Save("folder", "manual");
        temporary.AutoSave(default, default); temporary.Delete("slot");
        Check(!temporary.Wrote, "temporary manual save, autosave rotation and deletion blocked");
        Manager.Game.isAutoSavePossible = true;
        temporary.AutoSave(default, default);
        Check(!temporary.Wrote, "native Cycle enabling autosave cannot bypass write guard");
        original.Save("other");
        Check(original.Wrote, "unowned world is untouched");
        bool rejected = false;
        try { using var duplicate = new NativeActionWorldScope(new WorldData()); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && ReferenceEquals(Manager.Game.saveData, temporary), "overlapping native walking sessions rejected");
        scope.Dispose(); scope.Dispose();
        Check(ReferenceEquals(Manager.Game.saveData, original) && Manager.Game.isAutoSavePossible, "exit restores exact previous world and autosave");
        Check(!HarmonyLib.Harmony.Prefixes.Any(), "all temporary hooks removed on exit");
        temporary.Save("after exit");
        Check(temporary.Wrote, "persist guard does not remain globally active after exit");
        Manager.Game.Reset(original, false);
        using (var second = new NativeActionWorldScope(new WorldData())) { }
        Check(!Manager.Game.isAutoSavePossible, "initially disabled autosave remains disabled");
        HarmonyLib.Harmony.FailName = "AutoSave";
        rejected = false;
        try { using var failed = new NativeActionWorldScope(new WorldData()); } catch (InvalidOperationException) { rejected = true; }
        HarmonyLib.Harmony.FailName = null;
        Check(rejected && ReferenceEquals(Manager.Game.saveData, original) && !HarmonyLib.Harmony.Prefixes.Any(), "partial hook failure rolls back without changing world");
        using (var replaced = new NativeActionWorldScope(new WorldData()))
        {
            var foreign = new WorldData(); Manager.Game.Reset(foreign, true);
            replaced.Dispose();
            Check(ReferenceEquals(Manager.Game.saveData, foreign) && Manager.Game.isAutoSavePossible, "exit never overwrites another owner world or autosave state");
        }
        Manager.Game.Reset(original, true);
        rejected = false;
        try { using var unsafeScope = new NativeActionWorldScope(original); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && !HarmonyLib.Harmony.Prefixes.Any(), "existing game world cannot be adopted as temporary world");
        Console.WriteLine("PASS " + Checks.Count + " native Action world ownership checks");
    }
}
namespace Manager
{
    internal static class Game
    {
        public static WorldData saveData { get; private set; }
        public static bool isAutoSavePossible { get; set; }
        public static void Reset(WorldData world, bool autoSave) { saveData = world; isAutoSavePossible = autoSave; }
    }
}
namespace SaveData
{
    internal class WorldData
    {
        public bool Wrote;
        public void Save(string name) { if (HarmonyLib.Harmony.Allows(this, MethodBase.GetCurrentMethod())) Wrote = true; }
        public void Save(string folder, string name) { if (HarmonyLib.Harmony.Allows(this, MethodBase.GetCurrentMethod())) Wrote = true; }
        public void AutoSave(int when, int area) { if (HarmonyLib.Harmony.Allows(this, MethodBase.GetCurrentMethod())) Wrote = true; }
        public void Delete(string name) { if (HarmonyLib.Harmony.Allows(this, MethodBase.GetCurrentMethod())) Wrote = true; }
    }
}
namespace HarmonyLib
{
    internal class Harmony
    {
        internal static readonly Dictionary<MethodBase, MethodInfo> Prefixes = new();
        internal static string FailName;
        public Harmony(string id) { }
        public void Patch(MethodBase original, HarmonyMethod prefix)
        {
            if (original.Name == FailName) throw new InvalidOperationException("injected patch failure");
            Prefixes.Add(original, prefix.Method);
        }
        public void UnpatchSelf() => Prefixes.Clear();
        internal static bool Allows(object instance, MethodBase method) => !Prefixes.TryGetValue(method, out var prefix) || (bool)prefix.Invoke(null, new[] { instance });
    }
    internal class HarmonyMethod
    {
        public readonly MethodInfo Method;
        public HarmonyMethod(Type type, string method) { Method = type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static); }
    }
    internal static class AccessTools
    {
        public static MethodInfo PropertySetter(Type type, string name) => type.GetProperty(name).GetSetMethod(true);
        public static MethodInfo Method(Type type, string name, Type[] args) => type.GetMethod(name, args);
    }
}
