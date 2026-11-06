using RootMotion.FinalIK;
using UnityEngine;

namespace MainGameVrNeckChain
{
    public sealed partial class Plugin
    {
        private FullBodyBipedIK _pullIk;
        private IKEffector _pullLeft, _pullRight;
        private bool _pullSolverApplied;
        private EffectorSnapshot _leftBeforePull, _rightBeforePull;
        private struct EffectorSnapshot
        {
            internal Transform Target;
            internal Vector3 Position;
            internal Quaternion Rotation;
            internal float PositionWeight, RotationWeight;
            internal EffectorSnapshot(IKEffector effector)
            {
                Target = effector.target; Position = effector.position; Rotation = effector.rotation;
                PositionWeight = effector.positionWeight; RotationWeight = effector.rotationWeight;
            }
            internal void Restore(IKEffector effector)
            {
                effector.target = Target; effector.position = Position; effector.rotation = Rotation;
                effector.positionWeight = PositionWeight; effector.rotationWeight = RotationWeight;
            }
        }
        private void BindPullSolver()
        {
            UnbindPullSolver();
            var body = _female != null ? _female.animBody : null;
            _pullIk = body != null ? body.GetComponent<FullBodyBipedIK>() : null;
            if (_pullIk == null && body != null) _pullIk = body.GetComponentInChildren<FullBodyBipedIK>(true);
            if (_pullIk == null) return;
            _pullLeft = _pullIk.solver.GetEffector(FullBodyBipedEffector.LeftShoulder);
            _pullRight = _pullIk.solver.GetEffector(FullBodyBipedEffector.RightShoulder);
            if (_pullLeft == null || _pullRight == null) { _pullIk = null; return; }
            _pullIk.solver.OnPreUpdate += BeforePullSolve;
            _pullIk.solver.OnPostUpdate += AfterPullSolve;
        }
        private void BeforePullSolve()
        {
            RestorePullEffectors();
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed || !isActiveAndEnabled) return;
            UpdateShoulderPull();
            if (!_shoulderPull.Active || !_pullHasPose) return;
            _leftBeforePull = new EffectorSnapshot(_pullLeft);
            _rightBeforePull = new EffectorSnapshot(_pullRight);
            _pullSolverApplied = true;
            _pullLeft.target = _pullRight.target = null;
            _pullLeft.position = _pullLeftPosition + _pullOffset;
            _pullRight.position = _pullRightPosition + _pullOffset;
            _pullLeft.rotation = _pullLeftRotation; _pullRight.rotation = _pullRightRotation;
            _pullLeft.positionWeight = _pullRight.positionWeight = 1;
            _pullLeft.rotationWeight = _pullRight.rotationWeight = 1;
        }
        private void AfterPullSolve()
        {
            // Restore only our temporary solver inputs; keep the native solved body pose.
            RestorePullEffectors();
            _chainVisualPending = true;
        }
        private void RestorePullEffectors()
        {
            if (!_pullSolverApplied) return;
            _pullSolverApplied = false;
            _leftBeforePull.Restore(_pullLeft); _rightBeforePull.Restore(_pullRight);
        }
        private void UnbindPullSolver()
        {
            RestorePullEffectors();
            if (_pullIk != null)
            {
                _pullIk.solver.OnPreUpdate -= BeforePullSolve;
                _pullIk.solver.OnPostUpdate -= AfterPullSolve;
            }
            _pullIk = null; _pullLeft = _pullRight = null;
        }
    }
}
