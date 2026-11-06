using CanonPlugins.Shared;
using MainGirlHipHijack.Runtime;
using UnityEngine;
using Valve.VR;
using VRGIN.Controls;
using VRGIN.Core;

namespace MainGirlHipHijack
{
    public sealed partial class Plugin
    {
        private SteamVR_Events.Action _vrWristFocus;
        private readonly SteamVR_Behaviour_Pose[] _vrWristPoses = new SteamVR_Behaviour_Pose[2];

        private static bool IsWristEffector(int index) => index == BIK_LH || index == BIK_RH;
        private bool IsVRWristHeld(int index) => IsWristEffector(index)
            && (_vrLeftGrabIdx == index || _vrRightGrabIdx == index);

        // Candidate discovery is only called on a grip edge.
        private int FindNearestProxy(Vector3 position)
        {
            int best = -1;
            float distance = _settings != null ? _settings.VRGrabDistance : 0.15f;
            for (int i = 0; i < BIK_TOTAL; i++)
            {
                var state = _bikEff[i];
                if (state.ExternalOwner || (!state.Running && !IsWristEffector(i))) continue;
                if (IsWristEffector(i) && state.GizmoDragging) continue;
                if (_vrLeftGrabIdx == i || _vrRightGrabIdx == i) continue;
                if (i == BIK_BODY && _bodyCtrlLinkEnabled) continue;
                Transform candidate = state.Running ? state.Proxy : BIK_GetEffector(i)?.bone;
                if (candidate == null) continue;
                float d = Vector3.Distance(candidate.position, position);
                if (d < distance) { best = i; distance = d; }
            }
            return best;
        }

        private void ConnectVRWristFocus()
        {
            if (_vrWristFocus != null) return;
            _vrInteractionState.InputFocused = true;
            _vrWristFocus = SteamVR_Events.InputFocusAction(focused =>
            {
                _vrInteractionState.InputFocused = focused;
                if (!focused) ReleaseBothVRGrabs();
            });
            _vrWristFocus.enabled = true;
        }

        private void DisconnectVRWristFocus()
        {
            if (_vrWristFocus != null) _vrWristFocus.enabled = false;
            _vrWristFocus = null;
        }

        private void ReleaseBothVRGrabs()
        {
            try { ReleaseVRGrab(ref _vrInteractionState.LeftGrabIndex, 0); }
            finally
            {
                try { ReleaseVRGrab(ref _vrInteractionState.RightGrabIndex, 1); }
                finally
                {
                    _vrGrabLockLeft?.Release(); _vrGrabLockLeft = null;
                    _vrGrabLockRight?.Release(); _vrGrabLockRight = null;
                }
            }
        }

        // Invoked from the existing input entry, before the hip shortcut is evaluated.
        private void ProcessVRWristResetInput()
        {
            if (!_vrGrabMode || !_vrInteractionState.InputFocused || !VR.Active || VR.Mode == null
                || !CanonPlugins.Local.SessionGate.Allowed || !IsPluginEnabled()) return;
            ProcessVRWristReset(VR.Mode.Left, ref _vrInteractionState.LeftGrabIndex, 0);
            ProcessVRWristReset(VR.Mode.Right, ref _vrInteractionState.RightGrabIndex, 1);
        }

        private void ProcessVRWristReset(Controller controller, ref int index, int hand)
        {
            if (controller == null || !IsWristEffector(index) || _bikEff[index].ExternalOwner) return;
            if (!PluginInput.VrDown("MainGirlHipHijack.wristReset", controller.Input, "Axis0")) return;
            int wrist = index;
            _vrInteractionState.WristResetUntilGripUp[hand] = true;
            try { ReleaseVRWrist(ref index, hand); }
            finally
            {
                try { ClearBodyIKFollowBone(wrist); }
                finally
                {
                    try { ResetVRElbowForWrist(wrist); }
                    finally { ResetBodyIKPartToAnimationPose(wrist, stopPoseTransition: false, logSnapshot: false); }
                }
            }
        }

        private bool ProcessVRWristGrab(Controller controller, ref int index, int hand)
        {
            if (!_vrInteractionState.InputFocused) return true;
            var input = controller.Input;
            if (_vrInteractionState.WristResetUntilGripUp[hand])
            {
                if (PluginInput.VrUp("MainGirlHipHijack.grab", input, "Grip"))
                    _vrInteractionState.WristResetUntilGripUp[hand] = false;
                return true;
            }

            bool down = PluginInput.VrDown("MainGirlHipHijack.grab", input, "Grip");
            if (down)
            {
                // Recover a missed Up from this hand without affecting the other hand.
                if (IsWristEffector(index)) ReleaseVRWrist(ref index, hand);
                if (index >= 0) return false;
                int candidate = FindNearestProxy(((Component)controller).transform.position);
                if (!IsWristEffector(candidate)) return false;
                if (!CanonPlugins.Local.SessionGate.Allowed || !IsPluginEnabled() || !controller.IsTracking
                    || _pendingAbandonByPostureChange || _poseTransitionCoroutine != null) return true;
                if (!VrItemFocus.TryAcquire(controller, out var focus)) return true;
                if (hand == 0) _vrGrabLockLeft = focus; else _vrGrabLockRight = focus;
                bool started = false;
                try
                {
                    if (!TryEnableVRWrist(candidate)) return true;
                    var state = _bikEff[candidate];
                    index = candidate;
                    state.SuppressConfiguredFollow = true;
                    _vrInteractionState.WristParents[hand] = state.Proxy.parent;
                    state.GizmoDragging = true;
                    state.HasPostDragHold = false;
                    state.PostDragHoldFrames = 0;
                    state.Proxy.SetParent(((Component)controller).transform, true);
                    state.Gizmo?.SetVRGrabState(2);
                    state.VRMarkerGo?.SetActive(true);
                    if (state.VRMarkerRend != null) state.VRMarkerRend.sharedMaterial.color = Color.yellow;
                    var pose = controller.Tracking;
                    _vrWristPoses[hand] = pose;
                    if (pose != null)
                    {
                        pose.onConnectedChangedEvent += VRWristConnected;
                        pose.onTrackingChangedEvent += VRWristTrackingChanged;
                    }
                    RecomputeGizmoDraggingState();
                    started = true;
                }
                finally
                {
                    if (!started)
                    {
                        try { ReleaseVRWrist(ref index, hand); }
                        finally
                        {
                            focus.Release();
                            if (hand == 0) _vrGrabLockLeft = null; else _vrGrabLockRight = null;
                        }
                    }
                }
                return true;
            }
            if (!IsWristEffector(index)) return false;
            if (PluginInput.VrUp("MainGirlHipHijack.grab", input, "Grip"))
                ReleaseVRWrist(ref index, hand);
            else
            {
                ProcessVRElbowAimInput(controller, index, hand);
                if (PluginInput.VrDown("MainGirlHipHijack.follow", input, "Trigger"))
                {
                    var state = _bikEff[index];
                    if (state.FollowBone != null) ClearBodyIKFollowBone(index);
                    else
                    {
                        state.SuppressConfiguredFollow = true;
                        if (state.CandidateBone == null) state.CandidateBone = FindNearestBone(index, state.Proxy.position);
                        TrySetNearestFollowBone(index);
                        state.HasPostDragHold = false;
                        state.PostDragHoldFrames = 0;
                    }
                }
            }
            return true;
        }

        // Reuses the existing follow-visual pass, only while a wrist is being held.
        private void RefreshVRWristFollowCandidate(int index)
        {
            var state = _bikEff[index];
            bool held = IsVRWristHeld(index) && state.Running && state.GizmoDragging
                && !state.ExternalOwner && state.Proxy != null;
            state.CandidateBone = held && state.FollowBone == null ? FindNearestBone(index, state.Proxy.position) : null;
            if (!held) return;
            bool hasCandidate = state.CandidateBone != null;
            if (state.VRMarkerGo != null) state.VRMarkerGo.SetActive(true);
            if (state.VRMarkerRend != null)
                state.VRMarkerRend.sharedMaterial.color = hasCandidate ? new Color(0.8f, 0.2f, 1f) : Color.yellow;
            state.Gizmo?.SetVRGrabState(hasCandidate ? 1 : 2);
        }

        private bool TryEnableVRWrist(int index)
        {
            if (!IsWristEffector(index) || _bikEff[index].ExternalOwner || _runtimeWrists[index] != null) return false;
            if (_bikEff[index].Running) return _bikEff[index].Proxy != null;
            var effector = BIK_GetEffector(index);
            if (effector == null || effector.bone == null || _femaleSession.FullBodyIk == null) return false;
            var old = _bikEff[index];
            bool wanted = _bikWant[index];
            float weight = _bikWeight[index];
            var target = effector.target;
            float positionWeight = effector.positionWeight, rotationWeight = effector.rotationWeight;
            bool solverEnabled = _femaleSession.FullBodyIk.enabled;
            var next = new BodyIkOperationPointState
            {
                HasOriginalEffectorTarget = old.HasOriginalEffectorTarget,
                OriginalEffectorTarget = old.OriginalEffectorTarget,
                FallbackEffectorTargetGo = old.FallbackEffectorTargetGo,
                SuppressConfiguredFollow = true,
                VrAutoEnabled = true
            };
            _bikEff[index] = next;
            _bikWant[index] = true;
            _bikWeight[index] = 1f;
            bool success = false;
            try
            {
                DoEnableBodyIK(index);
                success = next.Running && next.Proxy != null;
                if (!success) return false;
                _abandonedByPostureChange = false;
                if (old.BoneMarkerGo != null) Destroy(old.BoneMarkerGo);
                if (old.FollowLine != null) Destroy(old.FollowLine.gameObject);
                if (old.ProxyGo != null) Destroy(old.ProxyGo);
                return true;
            }
            finally
            {
                if (!success)
                {
                    try { if (next.ProxyGo != null) Destroy(next.ProxyGo); }
                    finally
                    {
                        _bikEff[index] = old; _bikWant[index] = wanted; _bikWeight[index] = weight;
                        effector.target = target;
                        effector.positionWeight = positionWeight; effector.rotationWeight = rotationWeight;
                        _femaleSession.FullBodyIk.enabled = solverEnabled;
                    }
                }
            }
        }

        private bool ReleaseVRWrist(ref int index, int hand)
        {
            if (!IsWristEffector(index)) return false;
            var state = _bikEff[index];
            int wrist = index;
            var parent = _vrInteractionState.WristParents[hand];
            var pose = _vrWristPoses[hand];
            var focus = hand == 0 ? _vrGrabLockLeft : _vrGrabLockRight;
            index = -1;
            _vrInteractionState.WristParents[hand] = null;
            _vrWristPoses[hand] = null;
            if (hand == 0) _vrGrabLockLeft = null; else _vrGrabLockRight = null;
            try
            {
                try { StopVRElbowAim(hand); }
                finally
                {
                    if (pose != null)
                    {
                        pose.onConnectedChangedEvent -= VRWristConnected;
                        pose.onTrackingChangedEvent -= VRWristTrackingChanged;
                    }
                    if (state.Proxy != null) state.Proxy.SetParent(parent, true);
                    if (state.Running && state.FollowBone != null)
                    {
                        ArmBodyIKPostDragHold(wrist, "vr-wrist-release");
                        QueueBodyIKFollowRebind(wrist, state.FollowBone, false, Vector3.zero, Quaternion.identity, null);
                    }
                }
            }
            finally
            {
                try
                {
                    state.GizmoDragging = false;
                    state.Gizmo?.SetVRGrabState(0);
                    state.VRMarkerGo?.SetActive(false);
                    RecomputeGizmoDraggingState();
                }
                finally { focus?.Release(); }
            }
            return true;
        }

        private void ReleaseVRWristForPart(int index)
        {
            if (!IsWristEffector(index)) return;
            if (_vrLeftGrabIdx == index) ReleaseVRWrist(ref _vrInteractionState.LeftGrabIndex, 0);
            if (_vrRightGrabIdx == index) ReleaseVRWrist(ref _vrInteractionState.RightGrabIndex, 1);
        }

        private bool ReserveGripForVRWrist(Controller controller)
        {
            if (!_vrGrabMode || !_vrInteractionState.InputFocused || controller == null) return false;
            return IsWristEffector(FindNearestProxy(((Component)controller).transform.position));
        }

        private void VRWristConnected(SteamVR_Behaviour_Pose pose, SteamVR_Input_Sources source, bool connected)
        { if (!connected) ReleaseVRWristTracking(source); }

        private void VRWristTrackingChanged(SteamVR_Behaviour_Pose pose, SteamVR_Input_Sources source, ETrackingResult result)
        { if (result != ETrackingResult.Running_OK) ReleaseVRWristTracking(source); }

        private void ReleaseVRWristTracking(SteamVR_Input_Sources source)
        {
            if (source == SteamVR_Input_Sources.LeftHand) ReleaseVRWrist(ref _vrInteractionState.LeftGrabIndex, 0);
            else if (source == SteamVR_Input_Sources.RightHand) ReleaseVRWrist(ref _vrInteractionState.RightGrabIndex, 1);
        }
    }
}
