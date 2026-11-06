using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace MainGameVrNeckGrab
{
    public sealed partial class Plugin
    {
        internal sealed class NativeVoiceTicket { internal ChaControl Actor; internal bool Cancelled; }
        ChaControl _nativeDialogueActor;
        ConditionalWeakTable<Manager.Voice.Loader, NativeVoiceTicket> _nativeVoiceLoads = new ConditionalWeakTable<Manager.Voice.Loader, NativeVoiceTicket>();
        readonly List<NativeVoiceTicket> _nativeVoiceTickets = new List<NativeVoiceTicket>();
        Harmony _pendingVoiceHarmony;
        void InstallNativeLoaderHooks()
        {
            foreach (var method in typeof(Manager.Voice).GetMethods(AccessTools.allDeclared).Where(m => m.GetParameters().Any(p => p.ParameterType == typeof(Manager.Voice.Loader)) && m.Name != "Play_Standby"))
                Patch(method, nameof(NativeLoaderPrefix));
            // Cancelled loads may finish after a gate stop. Keep only this exact-load cleanup
            // hook until plugin destruction; it performs no scene discovery or playback work.
            if (_pendingVoiceHarmony == null)
            {
                _pendingVoiceHarmony = new Harmony(Guid + ".pending");
                _pendingVoiceHarmony.Patch(AccessTools.Method(typeof(Manager.Voice), "Play_Standby"),
                    prefix: new HarmonyMethod(typeof(Plugin), nameof(NativeStandbyPrefix)));
            }
        }
        static void NativeLoaderPrefix(Manager.Voice.Loader loader)
        {
            var p = Instance;
            if (p == null || !p.Allowed || p._nativeDialogueActor == null || loader == null || p._nativeVoiceLoads.TryGetValue(loader, out _)) return;
            var ticket = new NativeVoiceTicket { Actor = p._nativeDialogueActor };
            p._nativeVoiceLoads.Add(loader, ticket); p._nativeVoiceTickets.Add(ticket);
        }
        static bool NativeStandbyPrefix(AudioSource audioSource, Manager.Voice.Loader loader)
        {
            var p = Instance;
            if (p == null || loader == null || !p._nativeVoiceLoads.TryGetValue(loader, out var ticket)) return true;
            p._nativeVoiceTickets.Remove(ticket); p._nativeVoiceLoads.Remove(loader);
            if (!ticket.Cancelled && (!p.Allowed || p.FindActor(ticket.Actor)?.State.Muted != true)) return true;
            // This exact asynchronous dialogue was cancelled. Never revive it after release.
            if (audioSource != null) { audioSource.Stop(); Destroy(audioSource.gameObject); }
            return false;
        }
        static Exception NativeDialogueFinalizer(Exception __exception, ChaControl __state)
        { if (Instance != null) Instance._nativeDialogueActor = __state; return __exception; }
    }
}
