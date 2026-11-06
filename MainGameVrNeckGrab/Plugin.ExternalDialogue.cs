using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace MainGameVrNeckGrab
{
    public sealed partial class Plugin
    {
        Type _externalPlayer;
        PropertyInfo _bridgeInstance, _bridgePlayer;
        void InstallExternalVoiceHooks()
        {
            _externalPlayer = AccessTools.TypeByName("MainGameVoiceFaceEventBridge.ExternalVoicePlayer");
            var bridge = AccessTools.TypeByName("MainGameVoiceFaceEventBridge.Plugin");
            _bridgeInstance = bridge == null ? null : AccessTools.Property(bridge, "Instance");
            _bridgePlayer = bridge == null ? null : AccessTools.Property(bridge, "_externalVoicePlayer");
            if (_externalPlayer == null) return;
            // Do not patch cue/moan entry points. This player owns a separate cue AudioSource.
            foreach (var m in _externalPlayer.GetMethods(AccessTools.allDeclared).Where(m => m.ReturnType == typeof(bool) && new[] { "Play", "PlaySequence", "AppendSequence", "TryPlayAiConversation", "StartLoad" }.Contains(m.Name)))
                Patch(m, nameof(ExternalDialoguePrefix));
        }
        static bool ExternalDialoguePrefix(object __instance, object[] __args, ref bool __result)
        {
            var p = Instance;
            if (p == null || !p.Allowed) return true;
            var actor = __args.OfType<ChaControl>().FirstOrDefault();
            if (actor == null || p.FindActor(actor)?.State.Muted != true) return true;
            __result = false; return false;
        }
        void StopExternalDialogue(ChaControl actor)
        {
            if (_bridgeInstance == null || _bridgePlayer == null) return;
            object bridge = _bridgeInstance.GetValue(null, null);
            object player = bridge == null ? null : _bridgePlayer.GetValue(bridge, null);
            if (player == null) return;
            bool current = ReferenceEquals(ReflectionAccess.Get(player, "_dialogueFemale"), actor) || ReferenceEquals(ReflectionAccess.Get(player, "_aiLipActor"), actor);
            bool pending = Equals(ReflectionAccess.Get(player, "_loadPending"), true) && ReferenceEquals(ReflectionAccess.Get(player, "_pendingFemale"), actor);
            bool queued = ReferenceEquals(ReflectionAccess.Get(player, "_sequenceFemale"), actor);
            // Stop() stops cues as well; StopInternal only stops dialogue. Invalidate background work
            // through the player's own cancellation path, preserving its cue state.
            if (!current && !pending && !queued) return;
            var generation = AccessTools.Field(_externalPlayer, "_loadGeneration");
            if (pending)
            {
                generation.SetValue(player, (int)generation.GetValue(player) + 1);
                AccessTools.Field(_externalPlayer, "_loadPending").SetValue(player, false);
                var sync = ReflectionAccess.Get(player, "_pendingLock");
                lock (sync)
                {
                    AccessTools.Field(_externalPlayer, "_pendingReady").SetValue(player, false);
                    AccessTools.Field(_externalPlayer, "_pendingPcm").SetValue(player, null);
                    AccessTools.Field(_externalPlayer, "_pendingError").SetValue(player, null);
                }
            }
            if (queued) AccessTools.Method(_externalPlayer, "ClearQueuedItems").Invoke(player, null);
            if (current) AccessTools.Method(_externalPlayer, "StopInternal").Invoke(player, new object[] { "neck hold", false });
        }
    }
}
