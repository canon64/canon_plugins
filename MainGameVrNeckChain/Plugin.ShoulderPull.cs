using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using RootMotion.FinalIK;
using UnityEngine;

namespace MainGameVrNeckChain
{
    public sealed partial class Plugin
    {
        private readonly ShoulderPullState _shoulderPull = new ShoulderPullState();
        private Vector3 _pullRoot;
        private Vector3 _pullOffset;
        private object _pullSelectedPose, _pullCurrentPose;
        private RuntimeAnimatorController _pullAnimator;
        private Vector3 _pullScale;
        private bool _missingShoulderBonesLogged;
        private bool _pullHasPose;
        private Vector3 _pullLeftPosition, _pullRightPosition;
        private Quaternion _pullLeftRotation, _pullRightRotation;
        private const BindingFlags PoseAccess = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo SelectedPoseField = typeof(HFlag).GetField("selectAnimationListInfo", PoseAccess);
        private static readonly PropertyInfo SelectedPoseProperty = typeof(HFlag).GetProperty("selectAnimationListInfo", PoseAccess);
        private static readonly FieldInfo CurrentPoseField = typeof(HFlag).GetField("nowAnimationInfo", PoseAccess);
        private static readonly PropertyInfo CurrentPoseProperty = typeof(HFlag).GetProperty("nowAnimationInfo", PoseAccess);

        private static Plugin PullInstance
        {
            get
            {
                return Chainloader.PluginInfos.TryGetValue(Guid, out PluginInfo info)
                    ? info.Instance as Plugin : null;
            }
        }

        // Read-only bridge: no dependency from HipHijack to this optional plugin.
        public static bool TryGetShoulderPull(out HSceneProc scene, out ChaControl female,
            out int session, out Vector3 offset)
        {
            Plugin p = PullInstance;
            scene = null;
            female = null;
            session = 0;
            offset = Vector3.zero;
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed || p == null || !p.isActiveAndEnabled) return false;
            p.UpdateShoulderPull();
            if (!p._shoulderPull.Active) return false;
            scene = p._scene as HSceneProc;
            female = p._female;
            session = p._shoulderPull.Session;
            offset = p._pullOffset;
            return true;
        }

        // The pose is sampled by the chain at the tension edge, not one frame later
        // when the IK consumer happens to run. It remains immutable for this session.
        public static bool TryGetShoulderPullPose(int session, out Vector3 leftPosition,
            out Quaternion leftRotation, out Vector3 rightPosition, out Quaternion rightRotation)
        {
            Plugin p = PullInstance;
            leftPosition = rightPosition = Vector3.zero;
            leftRotation = rightRotation = Quaternion.identity;
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed || p == null || !p.isActiveAndEnabled || !p._shoulderPull.Active ||
                p._shoulderPull.Session != session || !p._pullHasPose) return false;
            leftPosition = p._pullLeftPosition;
            leftRotation = p._pullLeftRotation;
            rightPosition = p._pullRightPosition;
            rightRotation = p._pullRightRotation;
            return true;
        }

        // Called after HipHijack finishes the shoulder solve and rotation hold. The
        // frozen pull anchor is for measuring displacement only; drawing must use
        // the collar's final world position, or both chain ends appear stuck at
        // the animated (pre-IK) pose even though the collar has already moved.
        public static void SyncChainAfterShoulderSolve(HSceneProc scene, ChaControl female, int session)
        {
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed) return;
            Plugin p = PullInstance;
            if (p == null || !p.isActiveAndEnabled || p._chain == null || p._collar == null ||
                p._scene != scene || p._female != female || !p._collarAttached || !p._rootAttached ||
                !p._shoulderPull.Active || p._shoulderPull.Session != session ||
                !p.TipPinned) return;

            // Notification only. Final rendering belongs to ChainVisualDriver,
            // after every pose writer has finished; never solve once before IK
            // and then pull the same rope back to another root a second time.
            p._chainVisualPending = true;
        }

        private void CapturePullShoulderPose()
        {
            _pullHasPose = false;
            var body = _female.animBody;
            FullBodyBipedIK ik = body != null ? body.GetComponent<FullBodyBipedIK>() : null;
            if (ik == null && body != null) ik = body.GetComponentInChildren<FullBodyBipedIK>(true);
            Transform left = ik?.solver.GetEffector(FullBodyBipedEffector.LeftShoulder)?.bone;
            Transform right = ik?.solver.GetEffector(FullBodyBipedEffector.RightShoulder)?.bone;
            if (left == null || right == null)
            {
                if (!_missingShoulderBonesLogged)
                {
                    _missingShoulderBonesLogged = true;
                    Logger.LogWarning("Chain shoulder pose could not be captured: target shoulder bones are missing.");
                }
                return;
            }
            _pullLeftPosition = left.position;
            _pullLeftRotation = left.rotation;
            _pullRightPosition = right.position;
            _pullRightRotation = right.rotation;
            _pullHasPose = true;
        }

        private static object PoseInfo(HFlag flags, FieldInfo field, PropertyInfo property)
        {
            if (flags == null) return null;
            return field != null ? field.GetValue(flags) : property?.GetValue(flags, null);
        }

        private void UpdateShoulderPull()
        {
            if (_chain == null || _collar == null || !_collarAttached || !_rootAttached ||
                _scene == null || !_scene.isActiveAndEnabled || _female == null ||
                _female != FirstFemale(_scene) ||
                !TipPinned)
            {
                ResetShoulderPull();
                return;
            }

            Vector3 root = _collar.transform.TransformPoint(_attachedRootLocal);
            Vector3 tip = TipTarget;
            object selected = PoseInfo(_sceneFlags, SelectedPoseField, SelectedPoseProperty);
            object current = PoseInfo(_sceneFlags, CurrentPoseField, CurrentPoseProperty);
            RuntimeAnimatorController animator = _female.animBody != null
                ? _female.animBody.runtimeAnimatorController : null;
            Vector3 scale = ChainScaleVector();
            if (_shoulderPull.Active && (!ReferenceEquals(selected, _pullSelectedPose) ||
                !ReferenceEquals(current, _pullCurrentPose) || animator != _pullAnimator || scale != _pullScale))
                _shoulderPull.CancelUntilSlack();

            bool anchored = _shoulderPull.Active || _shoulderPull.WaitingForSlack;
            if (!anchored) _pullRoot = root;
            Vector3 delta = tip - _pullRoot;
            bool wasActive = _shoulderPull.Active;
            _shoulderPull.SampleDelta(delta.x, delta.y, delta.z, _chainLength, _shoulderPullMaxCm.Value * 0.01f,
                out float x, out float y, out float z);
            _pullOffset = new Vector3(x, y, z);
            if (!wasActive && _shoulderPull.Active)
            {
                CapturePullShoulderPose();
                _pullSelectedPose = selected;
                _pullCurrentPose = current;
                _pullAnimator = animator;
                _pullScale = scale;
            }
        }

        private void ResetShoulderPull()
        {
            _shoulderPull.Reset();
            _pullHasPose = false;
            _pullOffset = Vector3.zero;
            _pullSelectedPose = _pullCurrentPose = null;
            _pullAnimator = null;
        }
    }
}
