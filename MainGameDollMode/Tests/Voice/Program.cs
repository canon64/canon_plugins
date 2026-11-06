using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace MainGameDollMode
{
    internal static partial class DollModeGuardHooks
    {
        internal static bool Block;
        private static bool ShouldBlock() => Block;
        private static bool IsAlreadyPatchedByOwner(MethodInfo method, Harmony harmony)
            => Harmony.GetPatchInfo(method)?.Owners.Contains(harmony.Id) == true;
        internal static void Install(Harmony harmony) => ApplyExternalVoiceHooks(harmony);
    }
}
namespace Manager
{
    internal static class Voice
    {
        internal static int Stops;
        internal static void StopAll(bool fade) { if (fade) throw new Exception("must stop immediately"); Stops++; }
    }
}
namespace MainGameVoiceFaceEventBridge
{
    internal sealed class Plugin
    {
        internal static Plugin Instance { get; } = new Plugin();
        private PlayerService _externalVoicePlayer { get; } = new PlayerService();
        internal PlayerService Service => _externalVoicePlayer;
    }
    internal sealed class PlayerService
    {
        internal bool Pending = true, Playing = true;
        public void Stop(string reason) { Pending = Playing = false; }
    }
    internal sealed class ExternalVoicePlayer
    {
        internal int Calls;
        [MethodImpl(MethodImplOptions.NoInlining)] internal bool Play() { Calls++; return true; }
        [MethodImpl(MethodImplOptions.NoInlining)] internal bool PlaySequence() { Calls++; return true; }
        [MethodImpl(MethodImplOptions.NoInlining)] internal bool AppendSequence() { Calls++; return true; }
        [MethodImpl(MethodImplOptions.NoInlining)] internal bool TryPlayAiConversation() { Calls++; return true; }
        [MethodImpl(MethodImplOptions.NoInlining)] internal bool RequestImpactCue() { Calls++; return true; }
        [MethodImpl(MethodImplOptions.NoInlining)] internal bool RequestPriorityDeepCue() { Calls++; return true; }
        [MethodImpl(MethodImplOptions.NoInlining)] internal bool RequestContinuousPistonCue(out string subtitle, out float duration)
        { Calls++; subtitle = "voice"; duration = 1; return true; }
        [MethodImpl(MethodImplOptions.NoInlining)] internal bool RequestHardPistonCue(out string subtitle, out float duration)
        { Calls++; subtitle = "voice"; duration = 1; return true; }
        [MethodImpl(MethodImplOptions.NoInlining)] internal bool RequestOgazmCue(out string subtitle, out float duration)
        { Calls++; subtitle = "voice"; duration = 1; return true; }
    }
}
internal static class Program
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Main()
    {
        var harmony = new Harmony("doll.voice.tests");
        MainGameDollMode.DollModeGuardHooks.Install(harmony);
        MainGameDollMode.DollModeGuardHooks.Install(harmony);
        var player = new MainGameVoiceFaceEventBridge.ExternalVoicePlayer();
        foreach (var method in player.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            if (method.ReturnType != typeof(bool)) continue;
            object[] args = method.GetParameters().Length == 0 ? new object[0] : new object[] { "stale", 99f };
            MainGameDollMode.DollModeGuardHooks.Block = true;
            int before = player.Calls;
            Check(!(bool)method.Invoke(player, args) && player.Calls == before, "doll leaked: " + method.Name);
            if (args.Length != 0) Check(args[0] == null && (float)args[1] == 0, "stale outputs: " + method.Name);
            MainGameDollMode.DollModeGuardHooks.Block = false;
            Check((bool)method.Invoke(player, args) && player.Calls == before + 1, "release failed: " + method.Name);
        }
        MainGameDollMode.DollModeGuardHooks.StopPlayingVoices();
        var service = MainGameVoiceFaceEventBridge.Plugin.Instance.Service;
        Check(!service.Playing && !service.Pending && Manager.Voice.Stops == 1, "entry left audio playing/pending");
        harmony.UnpatchSelf();
        Console.WriteLine("PASS: all 9 entry points blocked/released, out values cleared, native and external stop");
    }
}
