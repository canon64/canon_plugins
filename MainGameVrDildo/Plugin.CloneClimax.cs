using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MainGameVrDildo
{
    internal struct ClimaxEyePose
    {
        internal float Weight, Inward, Upward, IrisInward, IrisUp;
    }

    public sealed partial class Plugin
    {
        private static readonly FieldInfo ClimaxActionFlags = AccessTools.Field(typeof(HActionBase), "flags");
        private static readonly FieldInfo ClimaxMouthWeights = AccessTools.Field(typeof(FBSBase), "dictFace");
        private ChaControl _climaxFemale, _lastCloneEyeFemale;
        private ClimaxEyePose _climaxEyePose, _lastCloneEyePose;
        private int _climaxMouthPattern, _climaxOriginalMouthPattern;
        private int _climaxEyesPattern, _climaxOriginalEyesPattern;
        private int _climaxEyelidShape = -1;
        private int _climaxEyebrowPattern = -1, _climaxOriginalEyebrowPattern;
        private byte _climaxTongueState, _climaxOriginalTongueState;
        private float _climaxMouthOpen, _climaxOriginalMouthOpen, _climaxOriginalFixedRate;
        private bool _climaxOriginalFixed, _cloneCycleInClimax;
        private bool CloneClimaxFaceHeld => _cloneReactionPermit.IsValid &&
            CanonPlugins.Shared.PluginSessionGate.Allowed && _climaxFemale != null && _climaxFemale == _female;

        private static bool IsClimaxAnimation(string state) => state != null &&
            (state.StartsWith("Orgasm", StringComparison.Ordinal) || state.StartsWith("After", StringComparison.Ordinal));

        private void InstallCloneClimaxHooks()
        {
            PatchOwned(_cloneReactionHarmony,
                AccessTools.Method(typeof(ChaControl), nameof(ChaControl.ChangeEyesOpenMax), new[] { typeof(float) }),
                postfix: new HarmonyMethod(typeof(Plugin), nameof(SelectedEyeOpeningChanged)) { priority = Priority.Last });
            PatchOwned(_cloneReactionHarmony, AccessTools.Method(typeof(HActionBase), "SetPlay", new[] { typeof(string), typeof(bool) }),
                prefix: new HarmonyMethod(typeof(Plugin), nameof(CloneClimaxMotionStarting)) { priority = Priority.Last });
            foreach (string method in new[] { "ChangeMouthPtn", "ChangeMouthOpenMax", "ChangeMouthFixed" })
                PatchOwned(_cloneReactionHarmony, AccessTools.Method(typeof(ChaControl), method),
                    postfix: new HarmonyMethod(typeof(Plugin), nameof(CloneClimaxMouthChanged)) { priority = Priority.Last });
            // SexFaces mixes FBS weights directly rather than calling ChangeMouthPtn.
            PatchOwned(_cloneReactionHarmony, AccessTools.Method(typeof(FBSBase), "ChangeFace", new[] { typeof(bool) }),
                prefix: new HarmonyMethod(typeof(Plugin), nameof(CloneClimaxMouthMixChanging)) { priority = Priority.Last });
            PatchOwned(_cloneReactionHarmony, AccessTools.Method(typeof(ChaControl), "ChangeEyesPtn"),
                prefix: new HarmonyMethod(typeof(Plugin), nameof(CloneClimaxEyesChanging)) { priority = Priority.Last });
            PatchOwned(_cloneReactionHarmony, AccessTools.Method(typeof(ChaControl), "ChangeEyebrowPtn"),
                prefix: new HarmonyMethod(typeof(Plugin), nameof(CloneClimaxEyebrowsChanging)) { priority = Priority.Last });
        }

        private static void CloneClimaxMotionStarting(HActionBase __instance, string _nextAnimation)
        {
            var p = Instance;
            if (p == null || !p._cloneReactionPermit.IsValid || !CanonPlugins.Shared.PluginSessionGate.Allowed ||
                p._settings == null || !p._settings.Enabled || p._model == null || !p.IsClonePose() ||
                !ReferenceEquals(ClimaxActionFlags?.GetValue(__instance), p._sceneFlags)) return;
            if (IsClimaxAnimation(_nextAnimation))
            {
                if (!p._cloneCycleInClimax)
                {
                    // SetPlay has not reset the game's gauge, mouth or gaze yet.
                    p.CaptureCloneClimaxFace();
                    p.ResetCloneVoiceCycle();
                }
                p._cloneCycleInClimax = true;
            }
            else if (p._cloneCycleInClimax)
            {
                // Re-arm sound without releasing the independently held face.
                p.ResetCloneVoiceCycle();
                p._cloneCycleInClimax = false;
            }
        }

        private void CaptureCloneClimaxFace()
        {
            if (!_settings.CloneClimaxFaceHoldEnabled || CloneClimaxFaceHeld || _female == null ||
                _female.fileStatus == null || _female.mouthCtrl == null) return;
            _climaxFemale = _female;
            _climaxEyePose = _lastCloneEyeFemale == _female ? _lastCloneEyePose : default;
            CaptureCloneClimaxEyeTransforms();
            var status = _female.fileStatus;
            _climaxOriginalEyesPattern = _gaugeFaceFemale == _female ? _gaugeFaceOriginalEyes : status.eyesPtn;
            _climaxEyesPattern = status.eyesPtn == _settings.HeartEyesPattern || status.eyesPtn == _settings.StarEyesPattern
                ? status.eyesPtn : ChooseGaugeEyePattern();
            _climaxEyelidShape = _gaugeFaceFemale == _female ? _gaugeFaceEyelidShape : ChooseEyelidShape(_female);
            _climaxOriginalEyebrowPattern = _gaugeFaceFemale == _female ? _gaugeFaceOriginalEyebrows : status.eyebrowPtn;
            _climaxEyebrowPattern = _gaugeFaceFemale == _female ? _gaugeFaceEyebrowPattern : ChooseEyebrowPattern(_female);
            if (_climaxEyebrowPattern >= 0) _female.ChangeEyebrowPtn(_climaxEyebrowPattern, true);
            _female.ChangeEyesPtn(_climaxEyesPattern, true);
            ApplySelectedEyeOpening(_female);
            _climaxOriginalMouthPattern = _gaugeFaceFemale == _female ? _gaugeFaceOriginalMouth : status.mouthPtn;
            _climaxOriginalMouthOpen = status.mouthOpenMax;
            _climaxOriginalFixed = status.mouthFixed;
            _climaxOriginalFixedRate = _female.mouthCtrl.FixedRate;
            _climaxOriginalTongueState = status.tongueState;
            _climaxTongueState = status.tongueState;
            int last = status.mouthPtn;
            _climaxMouthPattern = IsConfiguredGaugeMouth(last) ? last :
                IsConfiguredGaugeMouth(_gaugeFaceTonguePattern) ? _gaugeFaceTonguePattern : ChooseGaugeMouthPattern(-1);
            if (_climaxMouthPattern < 0 || _climaxMouthPattern >= _female.mouthCtrl.GetMaxPtn())
                _climaxMouthPattern = Math.Max(0, Math.Min(24, _female.mouthCtrl.GetMaxPtn() - 1));
            // Preserve a manually fixed opening; otherwise keep the selected mouth's
            // open extent rather than freezing a transient lip-sync closed frame.
            _climaxMouthOpen = Mathf.Clamp01(_climaxOriginalFixedRate >= 0f
                ? _climaxOriginalFixedRate : status.mouthOpenMax);
            ApplyCloneClimaxMouth();
            _female.mouthCtrl.ChangePtn(_climaxMouthPattern, false);
        }

        private bool IsConfiguredGaugeMouth(int pattern) => pattern >= 0 && _settings.MouthPatternSlots != null
            && Array.IndexOf(_settings.MouthPatternSlots, pattern) >= 0;

        private static void CloneClimaxEyesChanging(ChaControl __instance, ref int ptn)
        {
            var p = Instance;
            if (p == null || !p._cloneReactionPermit.IsValid || !CanonPlugins.Shared.PluginSessionGate.Allowed) return;
            if (p.CloneClimaxFaceHeld && __instance == p._climaxFemale) ptn = p._climaxEyesPattern;
            else if (__instance == p._gaugeFaceFemale) ptn = p._gaugeFaceEyePattern;
        }

        private static void CloneClimaxEyebrowsChanging(ChaControl __instance, ref int ptn)
        {
            var p = Instance;
            if (p == null || !p._cloneReactionPermit.IsValid || !CanonPlugins.Shared.PluginSessionGate.Allowed) return;
            if (p.CloneClimaxFaceHeld && __instance == p._climaxFemale && p._climaxEyebrowPattern >= 0) ptn = p._climaxEyebrowPattern;
            else if (__instance == p._gaugeFaceFemale && p._gaugeFaceEyebrowPattern >= 0) ptn = p._gaugeFaceEyebrowPattern;
        }

        private static void CloneClimaxMouthChanged(ChaControl __instance)
        {
            var p = Instance;
            if (p != null && p.CloneClimaxFaceHeld && __instance == p._climaxFemale) p.ApplyCloneClimaxMouth();
        }

        private static void CloneClimaxMouthMixChanging(FBSBase __instance, ref bool blend)
        {
            var p = Instance;
            if (p == null || !p._cloneReactionPermit.IsValid || !CanonPlugins.Shared.PluginSessionGate.Allowed) return;
            int pattern;
            if ((p.CloneClimaxFaceHeld && ReferenceEquals(__instance, p._climaxFemale.eyesCtrl)) ||
                (p._gaugeFaceFemale != null && ReferenceEquals(__instance, p._gaugeFaceFemale.eyesCtrl)))
                p.ApplySelectedEyeOpening(p.CloneClimaxFaceHeld ? p._climaxFemale : p._gaugeFaceFemale);
            if (p.CloneClimaxFaceHeld && ReferenceEquals(__instance, p._climaxFemale.mouthCtrl)) pattern = p._climaxMouthPattern;
            else if (p.CloneClimaxFaceHeld && ReferenceEquals(__instance, p._climaxFemale.eyesCtrl)) pattern = p._climaxEyelidShape;
            else if (p._gaugeFaceFemale != null && ReferenceEquals(__instance, p._gaugeFaceFemale.eyesCtrl)) pattern = p._gaugeFaceEyelidShape;
            else if (p.CloneClimaxFaceHeld && ReferenceEquals(__instance, p._climaxFemale.eyebrowCtrl)) pattern = p._climaxEyebrowPattern;
            else if (p._gaugeFaceFemale != null && ReferenceEquals(__instance, p._gaugeFaceFemale.eyebrowCtrl)) pattern = p._gaugeFaceEyebrowPattern;
            else return;
            if (pattern < 0) return;
            var weights = ClimaxMouthWeights?.GetValue(__instance) as Dictionary<int, float>;
            if (weights == null) return;
            weights.Clear(); weights[pattern] = 1f;
            blend = false;
        }

        private void ApplyCloneClimaxMouth()
        {
            if (!CloneClimaxFaceHeld || _climaxFemale.mouthCtrl == null) return;
            var status = _climaxFemale.fileStatus;
            if (status.mouthPtn != _climaxMouthPattern)
            {
                status.mouthPtn = _climaxMouthPattern;
                _climaxFemale.mouthCtrl.ChangePtn(_climaxMouthPattern, false);
            }
            status.mouthFixed = true; status.mouthOpenMax = _climaxMouthOpen;
            _climaxFemale.mouthCtrl.OpenMax = _climaxMouthOpen;
            _climaxFemale.mouthCtrl.FixedRate = _climaxMouthOpen;
            if (status.tongueState != _climaxTongueState) _climaxFemale.ChangeTongueState(_climaxTongueState);
        }

        private void ReleaseCloneClimaxFace()
        {
            var actor = _climaxFemale;
            _climaxFemale = null;
            if (_gaugeFaceFemale == null) ReleaseSelectedEyeOpening();
            ReleaseCloneClimaxEyeTransforms();
            _lastCloneEyeFemale = null;
            _lastCloneEyePose = _climaxEyePose = default;
            _cloneCycleInClimax = false;
            ResetCloneVoiceCycle();
            if (actor == null || actor.fileStatus == null || actor.mouthCtrl == null) return;
            // Clear ownership first: our Harmony postfixes must not recapture cleanup.
            if (actor.fileStatus.eyesPtn == _climaxEyesPattern) actor.ChangeEyesPtn(_climaxOriginalEyesPattern, true);
            if (_climaxEyebrowPattern >= 0 && actor.fileStatus.eyebrowPtn == _climaxEyebrowPattern)
                actor.ChangeEyebrowPtn(_climaxOriginalEyebrowPattern, true);
            actor.ChangeMouthFixed(_climaxOriginalFixed);
            actor.ChangeMouthOpenMax(_climaxOriginalMouthOpen);
            actor.mouthCtrl.FixedRate = _climaxOriginalFixedRate;
            if (actor.fileStatus.mouthPtn == _climaxMouthPattern) actor.ChangeMouthPtn(_climaxOriginalMouthPattern, true);
            actor.ChangeTongueState(_climaxOriginalTongueState);
        }
    }
}
