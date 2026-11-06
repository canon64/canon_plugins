using System;
using System.Collections.Generic;
using System.Reflection;
using AstralMode;

namespace BepInEx.Configuration
{
    public class ConfigEntry<T> { public T Value; }
    public class ConfigFile
    {
        public ConfigEntry<T> Bind<T>(string section, string key, T value, string description)
            => new ConfigEntry<T> { Value = value };
    }
}
namespace MainGameNotifications
{
    public enum NotificationSeverity { Failure }
    public static class NotificationApi
    {
        public static int Failures;
        public static void Publish(string owner, string title, string body, NotificationSeverity severity) { Failures++; }
    }
}
public class HFlag
{
    public enum EMode { houshi, sonyu }
    public enum ClickKind { none, actionChange }
    public bool voiceWait;
    public HSceneProc.AnimationListInfo nowAnimationInfo, selectAnimationListInfo;
    public ClickKind click;
}
public class HSceneProc
{
    public class AnimationListInfo { public int id; public HFlag.EMode mode; public string nameAnimation; }
    private List<AnimationListInfo>[] lstAnimInfo, lstUseAnimInfo;
    public HFlag flags = new HFlag();
    public bool ThrowChange, IgnoreChange;
    public int Changes;
    public HSceneProc(List<AnimationListInfo>[] available, List<AnimationListInfo>[] all)
    { lstUseAnimInfo = available; lstAnimInfo = all; }
    private void ChangeAnimator(AnimationListInfo next, bool reset)
    {
        if (ThrowChange) throw new InvalidOperationException("animation load failed");
        if (flags.voiceWait) throw new InvalidOperationException("voice wait was retained");
        Changes++;
        if (!IgnoreChange) flags.nowAnimationInfo = next;
    }
}
namespace AstralMode
{
    internal static class SimpleFileLogger
    {
        internal static int Calls;
        internal static void Info(string value) { Calls++; }
        internal static void Warn(string value) { Calls++; }
    }
    public sealed partial class Plugin
    {
        public const string Guid = "test";
        internal static bool DiagnosticsEnabled;
        private static HSceneProc _hSceneProc;
        private static readonly FieldInfo SnapLstUseAnimInfoField = typeof(HSceneProc).GetField("lstUseAnimInfo", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly BepInEx.Configuration.ConfigFile Config = new BepInEx.Configuration.ConfigFile();
        public Plugin(HSceneProc proc) { _hSceneProc = proc; }
    }
}
static class Program
{
    static int checks;
    static void Check(bool result, string name) { if (!result) throw new Exception(name); checks++; }
    static HSceneProc.AnimationListInfo Pose(string name, HFlag.EMode mode = HFlag.EMode.houshi)
        => new HSceneProc.AnimationListInfo { nameAnimation = name, mode = mode };
    static List<HSceneProc.AnimationListInfo>[] Lists(params HSceneProc.AnimationListInfo[] poses)
        => new[] { new List<HSceneProc.AnimationListInfo>(poses) };
    static void Main()
    {
        Console.WriteLine("Astral entry pose regression tests");
        var real = Pose("正常位", HFlag.EMode.sonyu);
        var bench = Pose("ベンチフェラ");
        var proc = new HSceneProc(Lists(real), Lists(real, bench));
        proc.flags.nowAnimationInfo = real; proc.flags.selectAnimationListInfo = real; proc.flags.voiceWait = true;
        Check(new Plugin(proc).ForceAstralPose(), "filtered bench fallback applies during voice wait");
        Check(ReferenceEquals(proc.flags.nowAnimationInfo, bench), "live pose is bench");
        Check(proc.flags.selectAnimationListInfo == null && proc.flags.click == HFlag.ClickKind.none, "no stale real selection");
        Check(proc.Changes == 1 && !proc.flags.voiceWait, "one native change");
        Check(SimpleFileLogger.Calls == 0, "diagnostics off does not log");
        proc = new HSceneProc(Lists(null, Pose("ベンチフェラ追加"), bench), null);
        Check(new Plugin(proc).ForceAstralPose() && ReferenceEquals(proc.flags.nowAnimationInfo, bench), "exact match beats partial");
        proc = new HSceneProc(null, Lists(bench));
        Check(new Plugin(proc).ForceAstralPose(), "missing filtered table can use full table");
        proc = new HSceneProc(Lists(real), Lists(real));
        Check(!new Plugin(proc).ForceAstralPose() && proc.Changes == 0, "missing bench fails");
        proc = new HSceneProc(Lists(bench), null) { ThrowChange = true };
        Check(!new Plugin(proc).ForceAstralPose(), "native exception fails");
        proc = new HSceneProc(Lists(bench), null) { IgnoreChange = true };
        Check(!new Plugin(proc).ForceAstralPose(), "unapplied native change fails");
        Check(!new Plugin(null).ForceAstralPose(), "missing scene fails");
        Check(MainGameNotifications.NotificationApi.Failures == 4, "all failures notify with diagnostics off");
        Check(SimpleFileLogger.Calls == 0, "failure diagnostics remain off");
        Plugin.DiagnosticsEnabled = true;
        Check(new Plugin(new HSceneProc(Lists(bench), null)).ForceAstralPose() && SimpleFileLogger.Calls == 1, "diagnostics on logs applied result");
        Console.WriteLine("PASS checks=" + checks);
    }
}
