using System;
using CanonPlugins.ShoulderPose;
using RootMotion.FinalIK;
using UnityEngine;
using V = System.Numerics.Vector3;
using Q = System.Numerics.Quaternion;

namespace MainGirlShoulderIkStabilizer
{
    internal sealed partial class ShoulderRotator : IDisposable
    {
        private sealed class Arm
        {
            internal IKMappingLimb Mapping;
            internal IKEffector Effector;
            internal float NeutralTilt;
            internal Q Current = Q.Identity;
            internal V BendLocal;
            internal V NeutralDirectionLocal;
            internal bool Running;
            internal readonly OwnedBoneRotation[] Owned = { new OwnedBoneRotation(), new OwnedBoneRotation(), new OwnedBoneRotation(), new OwnedBoneRotation() };
            internal void Restore() { foreach (var bone in Owned) bone.Restore(); }
        }
        private readonly FullBodyBipedIK ik;
        private readonly PluginSettings settings;
        private readonly Action<string> diagnostic;
        private readonly Arm[] arms = { new Arm(), new Arm() };
        private Transform chest;
        private Vector3 chestUpLocal;
        private bool ready, enabled = true, disposed;
        private int lastFrame = -1;
        private float nextLog;
        internal bool NeedsProcessing => !disposed && ((enabled && (arms[0].Running || arms[1].Running)) ||
            ShoulderPoseMath.RotationAngle(arms[0].Current) > 0.01f || ShoulderPoseMath.RotationAngle(arms[1].Current) > 0.01f);
        internal ShoulderRotator(FullBodyBipedIK solver, PluginSettings config, Action<string> log)
        {
            ik = solver; settings = config; diagnostic = log;
            ik.solver.OnPostInitiate += BindBones;
            ik.solver.OnStoreDefaultLocalState += BindBones;
            if (ik.solver.initiated) BindBones();
        }
        internal bool Owns(Component component, int side) => !disposed && ready && component != null &&
            component.gameObject == ik.gameObject && side >= 0 && side < 2 &&
            ((enabled && arms[side].Running) || ShoulderPoseMath.RotationAngle(arms[side].Current) > 0.01f);
        internal void SetState(bool active, bool left, bool right)
        { enabled = active; arms[0].Running = left; arms[1].Running = right; }
        private static V N(Vector3 v) => new V(v.x, v.y, v.z);
        private static Vector3 U(V v) => new Vector3(v.X, v.Y, v.Z);
        private static Q N(Quaternion q) => new Q(q.x, q.y, q.z, q.w);
        private static Quaternion U(Q q) => new Quaternion(q.X, q.Y, q.Z, q.W);

        private void BindBones()
        {
            Restore(); ready = false; bindStatus = "solver_unavailable";
            if (disposed || ik == null || ik.solver == null) return;
            var spine = ik.solver.spineMapping.spineBones;
            bindStatus = "spine_mapping_missing";
            if (spine == null || spine.Length < 2) return;
            chest = spine[spine.Length - 1];
            bindStatus = "chest_missing";
            if (chest == null) return;
            Vector3 up = Vector3.zero;
            for (int i = spine.Length - 2; i >= 0; i--)
                if (spine[i] != null && (chest.position - spine[i].position).sqrMagnitude > 1e-8f)
                { up = (chest.position - spine[i].position).normalized; break; }
            bindStatus = "spine_up_degenerate";
            if (up.sqrMagnitude < 0.5f) return;
            chestUpLocal = Quaternion.Inverse(chest.rotation) * up;
            for (int i = 0; i < 2; i++)
            {
                Arm a = arms[i];
                a.Mapping = ik.solver.GetLimbMapping((FullBodyBipedChain)i);
                a.Effector = ik.solver.GetEndEffector((FullBodyBipedChain)i);
                var m = a.Mapping;
                bindStatus = "arm_mapping_missing_side_" + i;
                if (m?.parentBone == null || m.parentBone.parent == null || m.bone1 == null || m.bone2 == null || m.bone3 == null || a.Effector == null) return;
                var map = m.GetBoneMap(IKMappingLimb.BoneMapType.Parent);
                Vector3 neutral = m.parentBone.parent.rotation * map.defaultLocalRotation * map.localSwingAxis;
                a.NeutralTilt = ShoulderPoseMath.Tilt(N(neutral), N(up));
                a.NeutralDirectionLocal = N(Quaternion.Inverse(chest.rotation) * neutral);
                a.Current = Q.Identity;
                a.BendLocal = N(Quaternion.Inverse(chest.rotation) * (m.bone2.position - m.bone1.position));
            }
            diagnosticBasisReady = false;
            ready = true; bindStatus = "ready";
            // Anatomical forward is now used by shoulder geometry, not just diagnostics.
            BindDiagnosticBasis();
        }
        internal void Restore() { foreach (var a in arms) a.Restore(); }
        internal void Apply(float leftConstraint, float rightConstraint)
        {
            if (!diagnosticSample) CaptureDiagnosticSolvedPose();
            if (!ready || disposed || ik == null || chest == null) return;
            float dt = lastFrame == Time.frameCount ? 0f : Time.unscaledDeltaTime;
            lastFrame = Time.frameCount;
            ApplyArm(0, leftConstraint, dt);
            ApplyArm(1, rightConstraint, dt);
        }
        private void ApplyArm(int side, float constraint, float dt)
        {
            Arm a = arms[side]; var m = a.Mapping;
            if (!ValidDiagnosticArm(a)) { EmitArmDiagnostic(side, "missing_bones", constraint); return; }
            BeginArmDiagnostic(side);
            V shoulder = N(m.parentBone.position), root = N(m.bone1.position), elbow = N(m.bone2.position), hand = N(m.bone3.position);
            float upper = V.Distance(root, elbow), lower = V.Distance(elbow, hand);
            if (upper < 1e-5f || lower < 1e-5f || (root - shoulder).LengthSquared() < 1e-10f || !ShoulderPoseMath.Finite(hand))
            { EmitArmDiagnostic(side, "degenerate_geometry", constraint); return; }
            Q chestRotation = N(chest.rotation);
            V up = ShoulderPoseMath.Unit(V.Transform(N(chestUpLocal), chestRotation), V.UnitY);
            float solverWeight = a.Effector.positionWeight * ik.solver.IKPositionWeight * Mathf.Clamp01(m.weight);
            bool independentRight = side == 1 && settings.IndependentShoulders;
            float upperAngle = ShoulderPoseMath.Angle(elbow - root, -up), requestedLift = 0f;
            Q desired = Q.Identity;
            ShoulderPoseMath.SwingTrace trace = default;
            if (enabled && a.Running)
                desired = ShoulderPoseMath.DesiredSwing(shoulder, root, elbow, hand, up, a.NeutralTilt, upper + lower,
                    independentRight ? settings.ShoulderRightWeight : settings.ShoulderWeight,
                    independentRight ? settings.ShoulderRightOffset : settings.ShoulderOffset,
                    settings.LoweredArmScale, side == 0 ? settings.ReverseShoulderL : settings.ReverseShoulderR,
                    independentRight ? settings.ElevationRightWeight : settings.ElevationWeight,
                    independentRight ? settings.ElevationRightStartAngle : settings.ElevationStartAngle,
                    independentRight ? settings.ElevationRightFullAngle : settings.ElevationFullAngle,
                    independentRight ? settings.ElevationRightMaxLift : settings.ElevationMaxLift,
                    settings.MaxSolverBlend, solverWeight * (1f - Mathf.Clamp01(constraint)), out upperAngle, out requestedLift, out trace,
                    V.Transform(N(diagnosticForwardLocal), chestRotation), V.Transform(a.NeutralDirectionLocal, chestRotation),
                    independentRight ? settings.ElevationRightMaxForward : settings.ElevationMaxForward);
            float rawDeg = ShoulderPoseMath.RotationAngle(desired);
            desired = ShoulderPoseMath.LimitAngle(desired, settings.MaxShoulderDeltaAngleDeg);
            float cappedDeg = ShoulderPoseMath.RotationAngle(desired);
            float effectiveWeight = enabled && a.Running ? Mathf.Clamp(solverWeight * (1f - Mathf.Clamp01(constraint)), 0f, settings.MaxSolverBlend) : 0f;
            float targetReach = ShoulderPoseMath.ReachFraction(shoulder, root, hand, upper, lower, desired);
            desired = Q.Slerp(Q.Identity, desired, targetReach);
            Q localDesired = Q.Inverse(chestRotation) * desired * chestRotation;
            a.Current = ShoulderPoseMath.SmoothRotation(a.Current, localDesired, dt, settings.TransitionSeconds, settings.MaxAngularSpeed);
            Q delta = chestRotation * a.Current * Q.Inverse(chestRotation);
            // A changed animated clavicle axis must not turn yesterday's swing into twist.
            delta = ShoulderPoseMath.FromTo(root - shoulder, V.Transform(root - shoulder, delta));
            float reach = ShoulderPoseMath.ReachFraction(shoulder, root, hand, upper, lower, delta);
            delta = Q.Slerp(Q.Identity, delta, reach);
            a.Current = Q.Inverse(chestRotation) * delta * chestRotation;
            if (ShoulderPoseMath.RotationAngle(delta) < 0.01f)
            {
                a.Current = Q.Identity;
                string reason = !enabled || !a.Running ? "inactive" : constraint >= .999f ? "shoulder_constraint" :
                    effectiveWeight <= 0f ? "zero_weight" : targetReach <= 0f || reach <= 0f ? "reach_blocked" : "zero_correction";
                EmitArmDiagnostic(side, reason, constraint, upperAngle, requestedLift, effectiveWeight, rawDeg, cappedDeg,
                    targetReach, reach, 0f, 0f, trace);
                return;
            }
            Quaternion handRotation = m.bone3.rotation;
            V previousBend = V.Transform(a.BendLocal, chestRotation);
            V handAxis = ShoulderPoseMath.Unit(hand - root, ShoulderPoseMath.Unit(elbow - root, V.UnitX));
            V solvedBend = ShoulderPoseMath.Project(elbow - root, handAxis);
            V savedBend = solvedBend.LengthSquared() > upper * upper * 1e-6f
                ? ShoulderPoseMath.Unit(solvedBend, previousBend) : previousBend;
            a.Owned[0].Capture(m.parentBone); a.Owned[1].Capture(m.bone1);
            a.Owned[2].Capture(m.bone2); a.Owned[3].Capture(m.bone3);
            m.parentBone.rotation = U(delta) * m.parentBone.rotation;
            V newRoot = N(m.bone1.position);
            if (!ShoulderPoseMath.SolveElbow(newRoot, hand, elbow, savedBend, upper, lower,
                out V newElbow, out V bend, preserveBendPlane: true))
            {
                foreach (var bone in a.Owned) bone.Commit();
                a.Restore(); a.Current = Q.Identity;
                EmitArmDiagnostic(side, "elbow_solve_failed", constraint, upperAngle, requestedLift, effectiveWeight, rawDeg, cappedDeg,
                    targetReach, reach, ShoulderPoseMath.RotationAngle(delta), 0f, trace);
                return;
            }
            m.bone1.rotation = U(ShoulderPoseMath.FromTo(N(m.bone2.position) - newRoot, newElbow - newRoot)) * m.bone1.rotation;
            m.bone2.rotation = U(ShoulderPoseMath.FromTo(N(m.bone3.position - m.bone2.position), hand - N(m.bone2.position))) * m.bone2.rotation;
            m.bone3.rotation = handRotation;
            a.BendLocal = V.Transform(bend, Q.Inverse(chestRotation));
            foreach (var bone in a.Owned) bone.Commit();
            float error = V.Distance(N(m.bone3.position), hand);
            // Modified rigs/nonuniform scale may invalidate rigid two-bone geometry.
            if (!ShoulderPoseMath.Finite(error) || error > 0.001f)
            { a.Restore(); a.Current = Q.Identity; }
            EmitArmDiagnostic(side, !ShoulderPoseMath.Finite(error) || error > .001f ? "hand_error_rollback" : "applied", constraint,
                upperAngle, requestedLift, effectiveWeight, rawDeg, cappedDeg, targetReach, reach,
                ShoulderPoseMath.RotationAngle(delta), error, trace);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; Restore();
            if (ik != null && ik.solver != null)
            { ik.solver.OnPostInitiate -= BindBones; ik.solver.OnStoreDefaultLocalState -= BindBones; }
        }
    }
}
