using SessionGate = CanonPlugins.Local.SessionGate;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MainGirlHipHijack.Services;
using RootMotion.FinalIK;
using UnityEngine;

namespace MainGirlHipHijack
{
    public sealed partial class Plugin
    {

        private bool TryResolveRuntimeRefs()
        {
            if (!SessionGate.Allowed || _femaleSession.Character == null) return false;

            TrackPostureBySelectAnimationInfo();
            TrackPostureByNowAnimationInfo();

            if (_femaleSession.Character.animBody != _femaleSession.AnimationBody)
            {
                int prevId = _femaleSession.AnimationBody != null ? _femaleSession.AnimationBody.GetInstanceID() : 0;
                int nextId = _femaleSession.Character.animBody != null ? _femaleSession.Character.animBody.GetInstanceID() : 0;
                LogInfo("animBody changed: " + prevId + " -> " + nextId);
                ResetCachedBodyIkBindings();
                _femaleSession.AnimationBody = _femaleSession.Character.animBody;
                _hSceneSession.HasAnimatorStateHash = false;
                _hSceneSession.AnimatorStateHash = 0;
                _hSceneSession.HasMotionStrength = false;
                _hSceneSession.MotionStrengthTag = PoseMotionStrengthUnknown;
                RequestAbandonAllBodyIKByPostureChange("animBody changed");
                _femaleSession.FullBodyIk = null;
                _femaleSession.BoneCache = null;
                _femaleSession.FullBodyIk = ResolveFbbik(_femaleSession.Character);
                Transform root = _femaleSession.AnimationBody != null ? _femaleSession.AnimationBody.transform : null;
                _femaleSession.BoneCache = root != null ? root.GetComponentsInChildren<Transform>(true) : null;
                PublishShoulderRuntime();
            }

            TrackMotionByAnimatorState();

            if (_femaleSession.FullBodyIk == null) return false;

            if (_settings.AutoEnableAllOnResolve && !_hSceneSession.AutoEnabledInCurrentScene && !_abandonedByPostureChange)
            {
                _hSceneSession.AutoEnabledInCurrentScene = true;
                for (int i = 0; i < BIK_TOTAL; i++)
                    SetBodyIK(i, true, saveSettings: false, reason: "auto-resolve");
                SaveSettings();
                LogInfo("auto enabled all bodyIK");
                LogStateSnapshot("auto-enable-all", force: true);
            }

            return true;
        }

        private void ResetHipQuickSetupOnHSceneEnter()
        {
            if (_settings == null)
                return;

            bool changed = false;

            if (_settings.SpeedHijackEnabled)
            {
                _settings.SpeedHijackEnabled = false;
                changed = true;
            }

            if (_settings.CutFemaleAnimSpeedEnabled)
            {
                _settings.CutFemaleAnimSpeedEnabled = false;
                changed = true;
            }

            if (_settings.FemaleHeadAngleGizmoVisible)
            {
                _settings.FemaleHeadAngleGizmoVisible = false;
                changed = true;
            }
            DestroyFemaleHeadGizmo();

            bool hadBodyIkState = false;
            for (int i = 0; i < BIK_TOTAL; i++)
            {
                bool running = _bikEff[i] != null && _bikEff[i].Running;
                if (_bikWant[i] || running || (_settings.Enabled != null && i < _settings.Enabled.Length && _settings.Enabled[i]))
                    hadBodyIkState = true;

                if (_bikWant[i])
                {
                    _bikWant[i] = false;
                    changed = true;
                }

                if (_settings.Enabled != null && i < _settings.Enabled.Length && _settings.Enabled[i])
                {
                    _settings.Enabled[i] = false;
                    changed = true;
                }
            }

            if (hadBodyIkState)
            {
                DisableAllBodyIK(silent: true);
                changed = true;
            }

            if (_bodyCtrlLinkEnabled || _leftCtrlHidden)
            {
                DisableBodyCtrlLink();
                RestoreLeftControllerVisuals();
                changed = true;
            }

            if (_hipQuickSetupActive)
            {
                _hipQuickSetupActive = false;
                changed = true;
            }
            if (_hipQuickSetupSavedWant != null)
            {
                _hipQuickSetupSavedWant = null;
                changed = true;
            }
            if (_hipQuickSetupTogglePending)
            {
                _hipQuickSetupTogglePending = false;
                changed = true;
            }

            if (changed)
            {
                SaveSettings();
                LogInfo("hip quick setup reset on HScene enter");
            }

            ResetFemaleHandPoseRuntime();
        }

        private void OnHSceneEnded()
        {
            SessionGate.Cleanup(
                () => { DisableBodyCtrlLink(); },
                () => { if (_vrGrabMode) ExitVRGrabMode(); },
                () => { StopPoseTransitionIfRunning(); },
                () => { DisableAllBodyIK(silent: true); },
                () => { ResetVibeReactionState(); },
                () => { ResetFemaleHandPoseRuntime(); },
                () => { ResetCachedBodyIkBindings(); },
                () => { _abandonedByPostureChange = false; },
                () => { _pendingAbandonByPostureChange = false; },
                () => { _pendingAbandonTrigger = null; },
                () => { _pendingAbandonRequestTime = 0f; },
                () => { _hipQuickSetupActive = false; },
                () => { _hipQuickSetupSavedWant = null; },
                () => { _hipQuickSetupTogglePending = false; },
                () => { ResetBodyIkDiagnosticsState(); },
                () => { _hSceneSessionTracker.Clear(PoseMotionStrengthUnknown); },
                () => { ClearMaleRefs(); },
                () => { ClearExternalFollowTargets(); },
                () => { OnPostureContextCleared(); },
                () => { LogInfo("HScene ended -> cleanup"); },
                () => { LogStateSnapshot("hscene-ended", force: true); });
        }

        private ChaControl ResolveMainFemale(HSceneProc proc)
        {
            foreach (var actor in SessionGate.Actors)
                if (actor != null && actor.sex == 1) return actor;
            return null;
        }

        private static FullBodyBipedIK ResolveFbbik(ChaControl cha)
        {
            if (cha == null || cha.animBody == null)
                return null;

            FullBodyBipedIK ik = cha.animBody.GetComponent<FullBodyBipedIK>();
            if (ik != null)
                return ik;

            return cha.animBody.GetComponentInChildren<FullBodyBipedIK>(true);
        }

        private void TrackPostureByNowAnimationInfo()
        {
            object info = GetNowAnimationInfo(_hSceneSession.Process);
            if (info == null)
            {
                if (_hSceneSession.HasCurrentAnimationInfo)
                {
                    string prev = BuildPostureInfoText(
                        _hSceneSession.CurrentAnimationInfoId,
                        _hSceneSession.CurrentAnimationInfoMode,
                        _hSceneSession.CurrentAnimationInfoName);
                    LogInfo("posture changed: " + prev + " -> (null)");
                    _hSceneSession.HasCurrentAnimationInfo = false;
                    _hSceneSession.CurrentAnimationInfoId = 0;
                    _hSceneSession.CurrentAnimationInfoMode = 0;
                    _hSceneSession.CurrentAnimationInfoName = null;
                    OnPostureContextCleared();
                }
                return;
            }

            int id = GetIntMemberValue(info, "id", int.MinValue);
            int mode = GetIntMemberValue(info, "mode", int.MinValue);
            string name = GetStringMemberValue(info, "nameAnimation");

            if (!_hSceneSession.HasCurrentAnimationInfo)
            {
                _hSceneSession.HasCurrentAnimationInfo = true;
                _hSceneSession.CurrentAnimationInfoId = id;
                _hSceneSession.CurrentAnimationInfoMode = mode;
                _hSceneSession.CurrentAnimationInfoName = name;
                LogInfo("posture init: " + BuildPostureInfoText(id, mode, name));
                OnPostureContextChanged(id, mode, name, "posture-init");
                return;
            }

            bool changed = _hSceneSession.CurrentAnimationInfoId != id
                || _hSceneSession.CurrentAnimationInfoMode != mode
                || !string.Equals(_hSceneSession.CurrentAnimationInfoName ?? string.Empty, name ?? string.Empty, StringComparison.Ordinal);
            if (!changed)
                return;

            string prevText = BuildPostureInfoText(
                _hSceneSession.CurrentAnimationInfoId,
                _hSceneSession.CurrentAnimationInfoMode,
                _hSceneSession.CurrentAnimationInfoName);
            string nextText = BuildPostureInfoText(id, mode, name);
            LogInfo("posture changed: " + prevText + " -> " + nextText);

            _hSceneSession.CurrentAnimationInfoId = id;
            _hSceneSession.CurrentAnimationInfoMode = mode;
            _hSceneSession.CurrentAnimationInfoName = name;
            OnPostureContextChanged(id, mode, name, "posture-changed");
        }

        private void TrackPostureBySelectAnimationInfo()
        {
            object info = GetSelectAnimationListInfo(_hSceneSession.Process);
            if (info == null)
            {
                if (_hSceneSession.HasSelectedAnimationInfo)
                {
                    if (_settings != null && _settings.DetailLogEnabled)
                    {
                        string prev = BuildPostureInfoText(
                            _hSceneSession.SelectedAnimationInfoId,
                            _hSceneSession.SelectedAnimationInfoMode,
                            _hSceneSession.SelectedAnimationInfoName);
                        LogInfo("posture select cleared: " + prev + " -> (null)");
                    }

                    _hSceneSession.HasSelectedAnimationInfo = false;
                    _hSceneSession.SelectedAnimationInfoId = 0;
                    _hSceneSession.SelectedAnimationInfoMode = 0;
                    _hSceneSession.SelectedAnimationInfoName = null;
                }
                return;
            }

            int id = GetIntMemberValue(info, "id", int.MinValue);
            int mode = GetIntMemberValue(info, "mode", int.MinValue);
            string name = GetStringMemberValue(info, "nameAnimation");

            if (!_hSceneSession.HasSelectedAnimationInfo)
            {
                _hSceneSession.HasSelectedAnimationInfo = true;
                _hSceneSession.SelectedAnimationInfoId = id;
                _hSceneSession.SelectedAnimationInfoMode = mode;
                _hSceneSession.SelectedAnimationInfoName = name;
                LogInfo("posture select set: " + BuildPostureInfoText(id, mode, name));
                RequestAbandonAllBodyIKByPostureChange("selectAnimationListInfo set");
                return;
            }

            bool changed = _hSceneSession.SelectedAnimationInfoId != id
                || _hSceneSession.SelectedAnimationInfoMode != mode
                || !string.Equals(_hSceneSession.SelectedAnimationInfoName ?? string.Empty, name ?? string.Empty, StringComparison.Ordinal);
            if (!changed)
                return;

            string prevText = BuildPostureInfoText(
                _hSceneSession.SelectedAnimationInfoId,
                _hSceneSession.SelectedAnimationInfoMode,
                _hSceneSession.SelectedAnimationInfoName);

            _hSceneSession.SelectedAnimationInfoId = id;
            _hSceneSession.SelectedAnimationInfoMode = mode;
            _hSceneSession.SelectedAnimationInfoName = name;

            string nextText = BuildPostureInfoText(id, mode, name);
            LogInfo("posture select changed: " + prevText + " -> " + nextText);
            RequestAbandonAllBodyIKByPostureChange("selectAnimationListInfo changed");
        }

        private void TrackMotionByAnimatorState()
        {
            Animator anim = _femaleSession.AnimationBody;
            if (anim == null)
                return;

            AnimatorStateInfo stateInfo;
            try
            {
                stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            }
            catch
            {
                return;
            }

            int hash = stateInfo.fullPathHash;
            if (_hSceneSession.HasAnimatorStateHash && _hSceneSession.AnimatorStateHash == hash
                && _hSceneSession.HasMotionStrength)
                return;
            string strengthTag = MotionStrengthClassifier.Classify(stateInfo.IsName);
            if (!_hSceneSession.HasAnimatorStateHash)
            {
                _hSceneSession.HasAnimatorStateHash = true;
                _hSceneSession.AnimatorStateHash = hash;
                if (_settings != null && _settings.DetailLogEnabled)
                    LogInfo("anim state init hash=" + hash);
            }
            else if (_hSceneSession.AnimatorStateHash != hash)
            {
                int prev = _hSceneSession.AnimatorStateHash;
                _hSceneSession.AnimatorStateHash = hash;
                if (!_hotelRoomPoseOwned) HandleFemaleHeadAngleContextChange("anim-state-hash-changed");

                // Animator state hash changes during the same posture are frequent.
                // Treat this as observation only; abandoning IK here can race with ChangeAnimator.
                if (_settings != null && _settings.DetailLogEnabled)
                    LogInfo("anim state changed(observe): " + prev + " -> " + hash);
            }

            if (!_hSceneSession.HasMotionStrength)
            {
                _hSceneSession.HasMotionStrength = true;
                _hSceneSession.MotionStrengthTag = strengthTag;
                if (_settings != null && _settings.DetailLogEnabled)
                    LogInfo("motion strength init: " + strengthTag);
                return;
            }

            string prevStrength = NormalizePoseMotionStrength(_hSceneSession.MotionStrengthTag);
            if (string.Equals(prevStrength, strengthTag, StringComparison.Ordinal))
                return;

            _hSceneSession.MotionStrengthTag = strengthTag;
            if (_settings != null && _settings.DetailLogEnabled)
                LogInfo("motion strength changed: " + prevStrength + " -> " + strengthTag);

            if (!MotionStrengthClassifier.IsStrongWeakSwitch(prevStrength, strengthTag))
                return;

            RequestAbandonAllBodyIKByPostureChange("motion strength changed " + prevStrength + "->" + strengthTag);
            if (_hSceneSession.HasCurrentAnimationInfo)
            {
                OnPostureContextChanged(
                    _hSceneSession.CurrentAnimationInfoId,
                    _hSceneSession.CurrentAnimationInfoMode,
                    _hSceneSession.CurrentAnimationInfoName,
                    "motion-strength-changed");
            }
        }

        private object GetNowAnimationInfo(HSceneProc proc)
        {
            object flags = GetHFlags();
            if (flags == null)
                return null;

            return GetMemberValue(flags, _gameReflection.HFlagCurrentAnimationInfo, _gameReflection.HFlagCurrentAnimationInfoProperty);
        }

        private object GetSelectAnimationListInfo(HSceneProc proc)
        {
            object flags = GetHFlags();
            if (flags == null)
                return null;

            return GetMemberValue(flags, _gameReflection.HFlagSelectedAnimationInfo, _gameReflection.HFlagSelectedAnimationInfoProperty);
        }

        private static object GetMemberValue(object owner, FieldInfo fi, PropertyInfo pi)
        {
            if (owner == null)
                return null;

            try
            {
                if (fi != null)
                    return fi.GetValue(owner);
                if (pi != null)
                    return pi.GetValue(owner, null);
            }
            catch
            {
                // ignore
            }

            return null;
        }

        private static int GetIntMemberValue(object owner, string memberName, int fallback)
        {
            object value = GetMemberValueByName(owner, memberName);
            if (value == null)
                return fallback;

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return fallback;
            }
        }

        private static string GetStringMemberValue(object owner, string memberName)
        {
            object value = GetMemberValueByName(owner, memberName);
            return value != null ? value.ToString() : string.Empty;
        }

        private struct MemberAccessor
        {
            internal FieldInfo Field;
            internal PropertyInfo Property;
        }

        private static readonly Dictionary<Type, Dictionary<string, MemberAccessor>> MemberAccessors =
            new Dictionary<Type, Dictionary<string, MemberAccessor>>();

        private static object GetMemberValueByName(object owner, string memberName)
        {
            if (owner == null || string.IsNullOrEmpty(memberName))
                return null;

            Type type = owner.GetType();
            if (!MemberAccessors.TryGetValue(type, out Dictionary<string, MemberAccessor> byName))
            {
                byName = new Dictionary<string, MemberAccessor>(StringComparer.Ordinal);
                MemberAccessors.Add(type, byName);
            }
            if (!byName.TryGetValue(memberName, out MemberAccessor accessor))
            {
                accessor = new MemberAccessor
                {
                    Field = AccessTools.Field(type, memberName),
                    Property = AccessTools.Property(type, memberName)
                };
                byName.Add(memberName, accessor);
            }

            FieldInfo fi = accessor.Field;
            if (fi != null)
            {
                try
                {
                    return fi.GetValue(owner);
                }
                catch
                {
                    // ignore
                }
            }

            PropertyInfo pi = accessor.Property;
            if (pi != null)
            {
                try
                {
                    return pi.GetValue(owner, null);
                }
                catch
                {
                    // ignore
                }
            }

            return null;
        }

        private static string BuildPostureInfoText(int id, int mode, string name)
        {
            return "id=" + id + ", mode=" + mode + ", name=" + (string.IsNullOrEmpty(name) ? "(empty)" : name);
        }

        private void LogResolveMissing(string key)
        {
            float now = Time.unscaledTime;
            if (string.Equals(_lastResolveMissing, key, StringComparison.Ordinal) && now < _nextResolveMissingLogTime)
                return;

            _lastResolveMissing = key;
            _nextResolveMissingLogTime = now + 1f;
            LogDebug("resolve missing: " + key);
        }

        private static string GetFemaleName(ChaControl cha)
        {
            if (cha == null)
                return "(null)";

            try
            {
                if (cha.chaFile != null && cha.chaFile.parameter != null && !string.IsNullOrEmpty(cha.chaFile.parameter.fullname))
                    return cha.chaFile.parameter.fullname;
            }
            catch
            {
                // ignore
            }

            return cha.name ?? "(unnamed)";
        }
    }
}
