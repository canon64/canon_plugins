using System;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using MainGameAiShoujoMode;

internal static class Program
{
    private static int checks;
    private static readonly Dictionary<string, object> Fixture = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixture.json")));
    private static int N(string key) => Convert.ToInt32(Fixture[key]);
    private static byte B(string key) => Convert.ToByte(Fixture[key]);
    private static float F(string key) => Convert.ToSingle(Fixture[key]);
    private static int TearCount => ChaFileDefine.tearsFlags.GetLength(default(int));
    private static readonly MethodInfo CopyPrefix = Assembly.Load("MoreAccessories")
        .GetType("MoreAccessoriesKOI.Patches.ChaFile_Patches+ChaFileControl_Copy_Patches", true)
        .GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);

    private static void Require(bool value, string message)
    { ++checks; if (!value) throw new Exception(message); }

    // Invoke the installed MoreAccessories prefix and the installed native method.
    // This reproduces the copy behaviour without launching Unity or loading a character.
    private static void InstalledCopy(ChaFileStatus live, ChaFileStatus saved)
    {
        CopyPrefix.Invoke(null, new object[] { live, saved });
        live.Copy(saved);
    }

    private static ChaFileStatus Live(int slots)
    {
        var result = new ChaFileStatus { coordinateType = N("two"), backCoordinateType = N("one"), shoesType = B("zero"),
            showAccessory = Enumerable.Range(default(int), slots).Select(i => i % N("two") == default(int)).ToArray(), tearsLv = B("one") };
        return result;
    }

    // Same three array indices as the native/MoreAccessories UpdateVisible accessory loop.
    private static void Display(int parts, object[] objects, bool[] visibility)
    {
        for (int i = default(int); i < parts; ++i) { bool shown = visibility[i]; object obj = objects[i]; }
    }

    private static void Main()
    {
        Console.WriteLine("Status restore: installed native Copy + MoreAccessories prefix");
        var baseline = Live(N("expandedSlots"));
        InstalledCopy(baseline, new ChaFileStatus());
        Require(baseline.showAccessory.Length == N("nativeSlots"), "baseline did not shrink to twenty slots");
        bool reproduced = false;
        try { Display(N("expandedSlots"), new object[N("expandedSlots")], baseline.showAccessory); }
        catch (IndexOutOfRangeException) { reproduced = true; }
        Require(reproduced, "baseline did not reproduce the reported exception type");
        Console.WriteLine("BASELINE_REPRODUCED IndexOutOfRangeException parts=35 visibility=20");

        foreach (int slots in new[] { N("nativeSlots"), N("expandedSlots"), N("largeSlots") })
        foreach (int savedSlots in new[] { N("zero"), N("nativeSlots"), N("expandedSlots"), N("longSavedSlots") })
        foreach (byte sex in new[] { B("zero"), B("one") })
        {
            var live = Live(slots);
            var original = (bool[])live.showAccessory.Clone();
            var saved = new ChaFileStatus { coordinateType = N("otherCoordinate"), backCoordinateType = N("otherBackCoordinate"),
                showAccessory = Enumerable.Repeat(false, savedSlots).ToArray(), tearsLv = B("three"),
                eyebrowPtn = N("two"), mouthPtn = N("mouthPattern"), mouthOpenMax = F("mouthOpen") };
            saved.clothesState[N("one")] = B("three");
            for (int visit = default(int); visit < N("three"); ++visit)
            {
                var merged = CharacterStatusRestore.Merge(live, saved, TearCount);
                Require(!ReferenceEquals(merged.showAccessory, live.showAccessory), "merge aliases live array");
                InstalledCopy(live, merged);
                live.shoesType = merged.shoesType;
                Display(slots, new object[slots], live.showAccessory);
                Require(live.showAccessory.Length == slots, "slot count changed on revisit, sex=" + sex);
                Require(live.coordinateType == N("two") && live.backCoordinateType == N("one") && live.shoesType == B("zero"), "loaded coordinate changed");
                Require(live.tearsLv == B("three") && live.clothesState[N("one")] == B("three") && live.mouthPtn == N("mouthPattern") && live.mouthOpenMax == F("mouthOpen"), "appearance lost");
                for (int i = default(int); i < slots; ++i)
                    Require(live.showAccessory[i] == (i < savedSlots ? false : original[i]), "saved/extra visibility lost");
            }
        }

        var current = Live(N("expandedSlots"));
        var bad = new ChaFileStatus { clothesState = new[] { B("invalidByte"), B("three") }, showAccessory = null,
            tearsLv = B("invalidByte"), mouthOpenMax = float.NaN,
            shapeHandPtn = new int[N("one"), N("one")], disableBustShapeMask = null };
        var repaired = CharacterStatusRestore.Merge(current, bad, TearCount);
        Require(repaired.clothesState.Length == N("clothingCount") && repaired.clothesState[default(int)] == B("zero") && repaired.clothesState[N("one")] == B("three"), "invalid clothing fallback");
        Require(repaired.tearsLv == current.tearsLv, "invalid face fallback");
        Require(repaired.mouthOpenMax == current.mouthOpenMax, "nonfinite scalar fallback");
        Require(repaired.shapeHandPtn.GetLength(default(int)) == N("two") && repaired.disableBustShapeMask != null, "legacy shape fallback");
        Require(repaired.showAccessory.SequenceEqual(current.showAccessory), "missing visibility lost loaded slots");
        InstalledCopy(current, repaired);
        Display(N("expandedSlots"), new object[N("expandedSlots")], current.showAccessory);
        var absent = CharacterStatusRestore.Merge(current, null, TearCount);
        Require(absent.showAccessory.SequenceEqual(current.showAccessory) && !ReferenceEquals(absent.showAccessory, current.showAccessory), "absent snapshot changed state");
        Console.WriteLine("STATUS_RESTORE_PASS checks=" + checks);
    }
}
