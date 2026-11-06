using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace MainGameFrameTimingProbe
{
    internal static class MethodSelection
    {
        private static readonly HashSet<string> EntryNames = new HashSet<string>(StringComparer.Ordinal) {
            "Update", "LateUpdate", "FixedUpdate", "OnGUI", "OnPreCull", "OnPreRender", "OnPostRender", "OnRenderImage", "OnWillRenderObject",
            "CaptureFrame", "CaptureCharacters", "DrawScreen", "DrawOverlay", "UpdateDisplacementMapIfNeeded",
            "UpdateUvForDisplacement", "BeforeCameraCull", "Upload", "OnAfterHSceneLateUpdate"
        };
        private static readonly HashSet<string> MapKernels = new HashSet<string>(StringComparer.Ordinal) {
            "UpdateImpactBoost", "UpdateBlankRoomGate", "TickRoomOverlay", "TickAiIsland", "TickMapSky",
            "ApplyConfigChangesIfNeeded", "TryStartPendingVideosIfRoomReady", "TickDeferredPlayFromBar",
            "MaintainAstralRoomOffset", "UpdateUiCaptureState", "UpdateWebCamTextures", "UpdateImageSlideshow",
            "UpdateSubCameraOverlay", "UpdateSolidRoomColorMode", "EnsureFemaleCharaRef",
            "SyncReverbZoneToFemale", "SyncVideoAudioSourceToFemale", "EnforceReverbBypassWhileDisabled",
            "SyncActiveVoiceSourcesToVideoRoom", "ReadVmdBar", "EnsureFolderPlayPathListConsistency",
            "DrawEmbeddedPlaybackUi", "DrawClothingTargets", "DrawClubLightsPlaybackControls",
            "DrawRoomOverlayButton", "HideSourceRoomForOverlay"
        };

        internal static bool Selected(MethodInfo method)
        {
            if (method.IsAbstract || method.ContainsGenericParameters || method.GetMethodBody() == null) return false;
            if (EntryNames.Contains(method.Name)) return true;
            if (method.DeclaringType.FullName == "MainGameBlankMapAdd.Plugin" && MapKernels.Contains(method.Name)) return true;
            if (method.Name == "Render" && method.DeclaringType.FullName == "MainGameMilkSpray.LiquidRenderer") return true;
            // Time coroutine work, not its cheap IEnumerator factory. Include late-starting
            // loops without depending on a short list of generated class names.
            return method.Name == "MoveNext" && typeof(IEnumerator).IsAssignableFrom(method.DeclaringType)
                && method.DeclaringType.IsDefined(typeof(CompilerGeneratedAttribute), false);
        }
    }
}
