using CanonPlugins.Shared;
using UnityEngine;
using VRGIN.Controls;

namespace MainGirlHipHijack
{
    public sealed partial class Plugin
    {
        internal static float ClampVRElbowExtension(float meters)
        {
            if (float.IsNaN(meters) || float.IsInfinity(meters)) return 0.2f;
            return Mathf.Clamp(meters, 0f, 1f);
        }

        private void SetVRElbowExtension(float meters)
        {
            if (_settings == null) return;
            _settings.VRElbowExtension = ClampVRElbowExtension(meters);
            ApplyVRElbowExtension(0);
            ApplyVRElbowExtension(1);
            SaveSettings();
        }

        // Button edges create/detach the parent relation. No movement callback or scan.
        private void ProcessVRElbowAimInput(Controller controller, int wrist, int hand)
        {
            if (!IsWristEffector(wrist) || _bikEff[wrist].ExternalOwner) return;
            // Installed Touch/Index bindings send physical A as ApplicationMenu_Press.
            if (PluginInput.VrDown("MainGirlHipHijack.elbowAim", controller.Input, "ApplicationMenu"))
                BeginVRElbowAim(wrist, hand);
            else if (_vrInteractionState.ElbowAims[hand].Index >= 0
                && PluginInput.VrUp("MainGirlHipHijack.elbowAim", controller.Input, "ApplicationMenu"))
                StopVRElbowAim(hand);
        }

        private bool BeginVRElbowAim(int wrist, int hand)
        {
            if (!CanonPlugins.Local.SessionGate.Allowed || !IsPluginEnabled()
                || _pendingAbandonByPostureChange || _poseTransitionCoroutine != null) return false;
            StopVRElbowAim(hand);
            int elbow = wrist == BIK_LH ? BIK_LE : BIK_RE;
            var state = _bikEff[elbow];
            if (state.ExternalOwner || state.GizmoDragging || _vrLeftGrabIdx == elbow || _vrRightGrabIdx == elbow) return false;
            var wristTarget = _bikEff[wrist].Proxy;
            var bend = BIK_GetBendConstraint(elbow);
            if (wristTarget == null || bend == null || bend.bone2 == null) return false;
            Vector3 elbowPosition = bend.bone2.position;
            Vector3 delta = elbowPosition - wristTarget.position;
            float length = delta.magnitude;
            if (length < 0.0001f) return false;
            Vector3 direction = delta / length;

            if (!state.Running) DoEnableBodyIK(elbow);
            if (!state.Running || state.Proxy == null) return false;
            _bikWant[elbow] = true;
            state.VrAutoEnabled = true;
            state.VrElbowFromWrist = true;
            state.SuppressConfiguredFollow = true;
            ClearBodyIKFollowBone(elbow);
            SetBodyIKWeight(elbow, 1f, saveSettings: false, updateSettings: false);
            state.HasPostDragHold = false;
            state.PostDragHoldFrames = 0;
            var aim = _vrInteractionState.ElbowAims[hand];
            aim.Index = elbow;
            aim.PreviousParent = state.Proxy.parent;
            aim.BaseLocalPosition = wristTarget.InverseTransformPoint(elbowPosition);
            aim.ExtensionLocalDirection = wristTarget.InverseTransformPoint(elbowPosition + direction) - aim.BaseLocalPosition;
            state.GizmoDragging = true;
            bool started = false;
            try
            {
                state.Proxy.SetParent(wristTarget, true);
                ApplyVRElbowExtension(hand);
                state.Gizmo?.SetVRGrabState(2);
                state.VRMarkerGo?.SetActive(true);
                if (state.VRMarkerRend != null) state.VRMarkerRend.sharedMaterial.color = Color.yellow;
                RecomputeGizmoDraggingState();
                started = true;
            }
            finally { if (!started) StopVRElbowAim(hand); }
            return true;
        }

        private bool TryBeginRuntimeWristElbowAim(RuntimeWristBinding owner, int hand)
        {
            if (hand < 0 || hand > 1 || !CanonPlugins.Local.SessionGate.Allowed || !IsPluginEnabled()
                || !ReferenceEquals(_runtimeWrists[owner.Index], owner) || !owner.IsValid) return false;
            if ((hand == 0 ? _vrLeftGrabIdx : _vrRightGrabIdx) >= 0) return false;
            var aim = _vrInteractionState.ElbowAims[hand];
            if (aim.Index >= 0 && !ReferenceEquals(aim.WristOwner, owner)) return false;
            if (!BeginVRElbowAim(owner.Index, hand)) return false;
            aim.WristOwner = owner;
            return true;
        }

        private void EndRuntimeWristElbowAim(RuntimeWristBinding owner)
        {
            try
            {
                if (ReferenceEquals(_vrInteractionState.ElbowAims[0].WristOwner, owner)) StopVRElbowAim(0);
            }
            finally
            {
                if (ReferenceEquals(_vrInteractionState.ElbowAims[1].WristOwner, owner)) StopVRElbowAim(1);
            }
        }

        private void ApplyVRElbowExtension(int hand)
        {
            var aim = _vrInteractionState.ElbowAims[hand];
            if (aim.Index < 0) return;
            var target = _bikEff[aim.Index].Proxy;
            if (target == null) return;
            float extension = ClampVRElbowExtension(_settings != null ? _settings.VRElbowExtension : 0.2f);
            target.localPosition = aim.BaseLocalPosition + aim.ExtensionLocalDirection * extension;
        }

        private void StopVRElbowAim(int hand)
        {
            var aim = _vrInteractionState.ElbowAims[hand];
            if (aim.Index < 0) return;
            var state = _bikEff[aim.Index];
            var parent = aim.PreviousParent;
            aim.Index = -1;
            aim.WristOwner = null;
            aim.PreviousParent = null;
            try { if (state.Proxy != null) state.Proxy.SetParent(parent, true); }
            finally
            {
                state.GizmoDragging = false;
                state.Gizmo?.SetVRGrabState(0);
                state.VRMarkerGo?.SetActive(false);
                RecomputeGizmoDraggingState();
            }
        }

        private void ReleaseVRElbowForPart(int index)
        {
            if (_vrInteractionState.ElbowAims[0].Index == index) StopVRElbowAim(0);
            if (_vrInteractionState.ElbowAims[1].Index == index) StopVRElbowAim(1);
        }

        private void ResetVRElbowForWrist(int wrist)
        {
            int elbow = wrist == BIK_LH ? BIK_LE : BIK_RE;
            if (!_bikEff[elbow].VrElbowFromWrist) return;
            _bikEff[elbow].VrElbowFromWrist = false;
            try { ClearBodyIKFollowBone(elbow); }
            finally { ResetBodyIKPartToAnimationPose(elbow, stopPoseTransition: false, logSnapshot: false); }
        }
    }
}
