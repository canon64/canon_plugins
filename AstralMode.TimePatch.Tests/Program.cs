using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using AstralMode;
using HarmonyLib;

namespace ksPlug
{
    public static class Settings
    {
        public static bool TimePassesInH = true, Saved, FailSave;
        public static void Save() { if (FailSave) throw new IOException("test save error"); Saved = TimePassesInH; }
    }
    namespace HSession
    {
        public sealed class HCtrl
        {
            public int OtherUpdates, TimeUpdates;
            [MethodImpl(MethodImplOptions.NoInlining)]
            public void LateUpdate()
            {
                OtherUpdates++;
                if (Settings.TimePassesInH) TimeUpdates++;
                OtherUpdates++;
            }
        }
    }
}
namespace ksPlugVR
{
    public static class Settings
    {
        public static bool TimePassesInH = true, Saved;
        public static void Save() { Saved = TimePassesInH; }
    }
    namespace HSession
    {
        public sealed class HCtrl
        {
            public int OtherUpdates, TimeUpdates;
            [MethodImpl(MethodImplOptions.NoInlining)]
            public void LateUpdate()
            {
                OtherUpdates++;
                if (Settings.TimePassesInH) TimeUpdates++;
                OtherUpdates++;
            }
        }
    }
}
internal static class Program
{
    private static int _checks, _otherPatchCalls;
    private static bool _inspectVr;
    private static void Check(bool success, string message)
    {
        if (!success) throw new Exception(message);
        ++_checks;
    }
    private static void OtherPostfix() { ++_otherPatchCalls; }
    private static void ExpectRejected(IEnumerable<CodeInstruction> code, MethodBase target)
    {
        try { KsPlugTimePatch.Transpile(code, target).ToList(); }
        catch (InvalidOperationException) { ++_checks; return; }
        throw new Exception("Unexpected IL was accepted");
    }
    private static int Main(string[] args)
    {
        Console.WriteLine("Testing isolated ksPlug time patch");
        try
        {
            _inspectVr = args.Length > 0 && args[0] == "--installed-vr";
            if (_inspectVr)
            {
                AppDomain.CurrentDomain.AssemblyResolve += ResolveGameAssembly;
                VerifyInstalledIl("ksPlugVR");
                Console.WriteLine("PASS VR installed IL checks=" + _checks + "; no game methods executed");
                return 0;
            }
            bool suppress = false;
            var warnings = new List<string>();
            var target = AccessTools.Method(typeof(ksPlug.HSession.HCtrl), "LateUpdate");
            var other = new Harmony("astralmode.timepatch.tests.other");
            other.Patch(target, postfix: new HarmonyMethod(typeof(Program), nameof(OtherPostfix)));
            KsPlugTimePatch.Install(() => suppress, warnings.Add);
            Check(warnings.Count == 0, string.Join("; ", warnings));
            var normal = new ksPlug.HSession.HCtrl();
            var vr = new ksPlugVR.HSession.HCtrl();
            normal.LateUpdate(); vr.LateUpdate();
            Check(normal.TimeUpdates == 1 && vr.TimeUpdates == 1, "Normal time advances");
            suppress = true;
            normal.LateUpdate(); vr.LateUpdate();
            Check(normal.TimeUpdates == 1 && vr.TimeUpdates == 1, "Astral time is stopped in both versions");
            Check(normal.OtherUpdates == 4 && vr.OtherUpdates == 4, "Other HCtrl work continues");
            Check(ksPlug.Settings.TimePassesInH && ksPlugVR.Settings.TimePassesInH, "Settings stay ON");
            ksPlug.Settings.TimePassesInH = true; ksPlugVR.Settings.TimePassesInH = true;
            normal.LateUpdate(); vr.LateUpdate();
            Check(normal.TimeUpdates == 1 && vr.TimeUpdates == 1, "Reloading an ON setting cannot resume astral time");
            suppress = false;
            normal.LateUpdate(); vr.LateUpdate();
            Check(normal.TimeUpdates == 2 && vr.TimeUpdates == 2, "Return or disabling suppression resumes time");
            ksPlug.Settings.TimePassesInH = false; ksPlugVR.Settings.TimePassesInH = false;
            normal.LateUpdate(); vr.LateUpdate();
            Check(normal.TimeUpdates == 2 && vr.TimeUpdates == 2, "Original OFF remains respected");
            ksPlug.Settings.TimePassesInH = true; ksPlugVR.Settings.TimePassesInH = true;
            suppress = true;
            KsPlugTimePatch.Remove();
            normal.LateUpdate(); vr.LateUpdate();
            Check(normal.TimeUpdates == 3 && vr.TimeUpdates == 3, "Removing this integration restores original work");
            Check(_otherPatchCalls == 6, "Other Harmony patch survives removal");
            Check(Harmony.GetPatchInfo(target).Owners.Contains(other.Id), "Other owner still registered");
            other.UnpatchSelf();

            var desktopControl = new MainGameBlankMapAdd.KsPlugTimeControl(false);
            var vrControl = new MainGameBlankMapAdd.KsPlugTimeControl(true);
            Check(desktopControl.TrySet(false, out string settingError), settingError);
            Check(!ksPlug.Settings.TimePassesInH && !ksPlug.Settings.Saved && ksPlugVR.Settings.TimePassesInH, "Desktop switch saves only desktop setting");
            Check(vrControl.TrySet(false, out settingError), settingError);
            Check(!ksPlugVR.Settings.TimePassesInH && !ksPlugVR.Settings.Saved, "VR switch saves VR setting");
            Check(desktopControl.TrySet(true, out settingError), settingError);
            Check(ksPlug.Settings.TimePassesInH && ksPlug.Settings.Saved && !ksPlugVR.Settings.TimePassesInH, "Desktop ON persists without affecting VR");
            ksPlug.Settings.TimePassesInH = false;
            Check(desktopControl.TryRead(out bool readEnabled, out settingError) && !readEnabled, "Read follows native-menu setting changes");
            ksPlug.Settings.FailSave = true;
            Check(!desktopControl.TrySet(true, out settingError) && settingError.Contains("test save error") && !ksPlug.Settings.TimePassesInH, "Save failure is reported and runtime value rolls back");
            ksPlug.Settings.FailSave = false;

            var field = AccessTools.Field(typeof(ksPlug.Settings), "TimePassesInH");
            ExpectRejected(new CodeInstruction[0], target);
            ExpectRejected(new[] { new CodeInstruction(OpCodes.Ldsfld, field), new CodeInstruction(OpCodes.Nop) }, target);
            ExpectRejected(new[] { new CodeInstruction(OpCodes.Ldsfld, field), new CodeInstruction(OpCodes.Brfalse_S),
                new CodeInstruction(OpCodes.Ldsfld, field), new CodeInstruction(OpCodes.Brfalse_S) }, target);

            // 実ゲームDLLはILを読むだけで、ゲームのメソッドを実行しない。
            AppDomain.CurrentDomain.AssemblyResolve += ResolveGameAssembly;
            VerifyInstalledIl("ksPlug");
            Console.WriteLine("PASS checks=" + _checks + "; normal/VR fixture patches and installed desktop IL validated; no game methods executed");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { KsPlugTimePatch.Remove(); }
    }
    private static Assembly ResolveGameAssembly(object sender, ResolveEventArgs args)
    {
        string name = new AssemblyName(args.Name).Name + ".dll";
        string managed = _inspectVr ? @"F:\kks\KoikatsuSunshine_VR_Data\Managed" : @"F:\kks\KoikatsuSunshine_Data\Managed";
        foreach (string dir in new[] { managed, @"F:\kks\BepInEx\core", @"F:\kks\BepInEx\plugins\ksPlug" })
        {
            string path = Path.Combine(dir, name);
            if (File.Exists(path)) return Assembly.LoadFrom(path);
        }
        return null;
    }
    private static void VerifyInstalledIl(string name)
    {
        string path = @"F:\kks\BepInEx\plugins\ksPlug\" + name + ".dll";
        var assembly = Assembly.LoadFrom(path);
        var settings = assembly.GetType(name + ".Settings", true);
        Check(settings.GetField("TimePassesInH", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.FieldType == typeof(bool), name + " exposes bool time setting");
        Check(settings.GetMethod("Save", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null)?.ReturnType == typeof(void), name + " exposes Save API");
        var target = assembly.GetType(name + ".HSession.HCtrl", true).GetMethod("LateUpdate", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var before = PatchProcessor.GetOriginalInstructions(target);
        var after = KsPlugTimePatch.Transpile(before, target).ToList();
        Check(after.Count == before.Count + 1, name + " adds one condition filter");
        int inserted = after.FindIndex(x => x.opcode == OpCodes.Call && x.operand is MethodInfo method && method.DeclaringType == typeof(KsPlugTimePatch));
        Check(inserted >= 0, name + " has filter call");
        after.RemoveAt(inserted);
        Check(before.Zip(after, (a, b) => a.opcode == b.opcode && Equals(a.operand, b.operand)
            && a.labels.SequenceEqual(b.labels) && a.blocks.SequenceEqual(b.blocks)).All(x => x), name + " preserves every original instruction and label");
        Console.WriteLine("Validated installed " + name + " LateUpdate IL; original instructions=" + before.Count);
    }
}
