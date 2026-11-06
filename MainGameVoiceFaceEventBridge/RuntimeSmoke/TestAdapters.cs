// Only game/VR host adapters are faked. AudioSource, DSP, file decoding, the player,
// detector, settings and both trigger services are the real production code.
using BepInEx.Configuration;
using UnityEngine;

public sealed class ChaControl
{
    public GameObject objBodyBone;
    public AudioSource LipSource;
    private bool _fixed;
    public void SetLipSync(AudioSource source) { LipSource = source; }
    public bool GetMouthFixed() => _fixed;
    public void ChangeMouthFixed(bool value) { _fixed = value; }
}
namespace CanonPlugins.Shared
{
    internal static class BlankRoomGate { internal static bool IsInBlankRoom = true; }
}
namespace CanonPlugins.Local
{
    internal static class SessionGate { internal static bool Allowed => CanonPlugins.Shared.BlankRoomGate.IsInBlankRoom; }
}
namespace MainGameVoiceFaceEventBridge
{
    internal static class SessionGate { internal static bool Allowed => CanonPlugins.Shared.BlankRoomGate.IsInBlankRoom; }
    internal sealed class Plugin
    {
        internal static Plugin Instance;
        internal ExternalVoicePlayer Player;
        internal bool InterruptAllVoicesForSyringe(){Player.Stop("syringe priority");return true;}
        internal System.IDisposable BeginSyringeDialogue(System.Action interrupted)=>null;
        internal ChaControl Female = new ChaControl();
        internal void SetImpactCueEnabled(string owner, bool enabled) => Player.SetImpactCueEnabled(owner, enabled);
        internal bool RequestImpactCue(string owner) => Player.RequestImpactCue(owner, Female);
        internal bool RequestPriorityDeepCue(string owner) => Player.RequestPriorityDeepCue(owner, Female);
        internal bool RequestContinuousPistonCue(string owner) => Player.RequestContinuousPistonCue(owner, Female, 1, 0, out _, out _);
        internal bool RequestPistonCue(string owner, bool hard, float volume, float cooldown) => hard
            ? Player.RequestHardPistonCue(owner, Female, volume, cooldown, out _, out _)
            : Player.RequestContinuousPistonCue(owner, Female, volume, cooldown, out _, out _);
        internal bool RequestOgazmCue(string owner, float volume, float cooldown) => Player.RequestOgazmCue(owner, Female, volume, cooldown, out _, out _);
        internal bool IsHardPistonCuePlaying => Player.HardPistonCuePlaying;
        internal bool IsOgazmCuePlaying => Player.OgazmCuePlaying;
        internal bool RequestOwnedTransientVolumeBoost(string owner, float peak, float attack, float hold, float release,
            float silence, float fadeOut, float fadeIn, PublicApi.EasingShape easing)
            => Player.RequestOwnedTransientBoost(owner, peak, attack, hold, release, silence, fadeOut, fadeIn, easing);
        internal bool CancelTransientVolumeBoost(string owner) => Player.CancelTransientVolumeBoost(owner);
        internal static void LogImpactCueDiagnostic(string text) => Debug.Log(text);
        internal bool RequestExternalVolumeBoost(float p, float a, float h, float r, float s, float so, float si, PublicApi.EasingShape e)
            => Player.RequestTransientBoost(p, a, h, r, s, so, si, e);
    }
}
namespace MainGameVrHipIkVoiceImpactBoost
{
    internal static class Plugin { internal const string GUID = "com.kks.main.vrhipikvoiceimpactboost"; }
    internal sealed class PluginFileLogger { internal void LogInfo(string text) => Debug.Log(text); }
    internal static class MillimeterDistanceDrawer { internal static void Draw(ConfigEntryBase entry) { } }
    internal sealed class HipIkTrackingPositionSource
    {
        internal Vector3 Hip;
        internal bool TryGetHip(out Vector3 hip, out bool running, out string source)
        { hip = Hip; running = true; source = "simulated_hip"; return true; }
        internal bool TryGetHead(out Vector3 head, out string source)
        { head = Vector3.up; source = "simulated_head"; return true; }
    }
}
namespace MainGameVoiceImpactBoost
{
    internal static class SessionGate { internal static bool Allowed => CanonPlugins.Shared.BlankRoomGate.IsInBlankRoom; }
    internal static class Plugin { internal const string GUID = "com.kks.main.voiceimpactboost"; }
    internal sealed class PluginFileLogger { internal void LogInfo(string text) => Debug.Log(text); }
    internal static class BeatSyncIntensityReader { internal static float Read() => 1; }
}
