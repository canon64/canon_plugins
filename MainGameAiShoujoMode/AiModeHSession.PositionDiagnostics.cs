using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MapEnvironmentRuntime;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    public sealed partial class AiModeHSession
    {
        // Called once by HReady, inside the diagnostics enabled guard.
        internal string CaptureEventPositions(SandboxHost host, string characterId)
        {
            var females = (List<ChaControl>)AccessTools.Field(typeof(HSceneProc), "lstFemale").GetValue(_proc);
            var female = females?.FirstOrDefault(actor => actor != null);
            var male = AccessTools.Field(typeof(HSceneProc), "male").GetValue(_proc) as ChaControl;
            return Conversation.EventPositionSnapshot.Capture("h.ready", host, male?.transform,
                female?.transform, characterId, _location);
        }
    }
}
