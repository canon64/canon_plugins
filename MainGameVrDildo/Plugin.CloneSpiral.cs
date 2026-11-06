using System;
using CanonGate;
using HarmonyLib;

namespace MainGameVrDildo
{
    public sealed partial class Plugin
    {
        private GateApi.Permit _cloneReactionPermit;
        private Harmony _cloneReactionHarmony;

        private bool IsClonePose()
        {
            var pose = _sceneFlags?.nowAnimationInfo;
            return pose != null && CloneSpiralPolicy.IsClone((int)pose.mode, pose.id, pose.nameAnimation);
        }

        private bool CanStartCloneOgazm()
        {
            var flags = _sceneFlags;
            return _cloneReactionPermit.IsValid && CanonPlugins.Shared.PluginSessionGate.Allowed
                && !DildoVoiceSuppressed && _settings != null && _settings.Enabled && _settings.OgazmVoiceEnabled && _model != null && IsClonePose()
                && flags.mode == HFlag.EMode.aibu
                && CloneSpiralPolicy.IsReady(flags.gaugeFemale, flags.nowAnimStateName);
        }

        private void BeginCloneReactionSession()
        {
            _cloneReactionPermit = CanonPlugins.Shared.PluginSessionGate.Capture();
            if (!_cloneReactionPermit.IsValid) return;
            try
            {
                _cloneReactionHarmony = new Harmony(Guid + ".clone-reaction");
                PatchOwned(_cloneReactionHarmony, AccessTools.Method(typeof(HSceneProc), "ChangeAnimator"),
                    postfix: new HarmonyMethod(typeof(Plugin), nameof(CloneReactionPoseChanged)));
                InstallCloneClimaxHooks();
            }
            catch (Exception ex)
            {
                EndCloneReactionSession();
                Logger.LogWarning("Clone reaction initialization failed: " + ex.Message);
                throw;
            }
        }

        private static void CloneReactionPoseChanged(HSceneProc __instance)
        {
            var p = Instance;
            if (p == null || !p._cloneReactionPermit.IsValid || __instance != p._scene) return;
            var participants = FemaleListField?.GetValue(__instance) as System.Collections.IList;
            if (!p.IsClonePose() || (p._climaxFemale != null && (participants == null || participants.Count == 0
                || !ReferenceEquals(participants[0], p._climaxFemale))))
            { p.ReleaseCloneClimaxFace(); p.ReleaseGaugeFace(); }
        }

        private void EndCloneReactionSession()
        {
            _cloneReactionPermit = default;
            ReleaseCloneClimaxFace();
            RemoveOwnedHooks(ref _cloneReactionHarmony);
        }

        private void ApplyCloneReactionSettings()
        {
            if (_settings == null || !_settings.Enabled || !_settings.CloneClimaxFaceHoldEnabled) ReleaseCloneClimaxFace();
        }
    }
}
