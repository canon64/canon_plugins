using System;
using System.Globalization;
using System.Text;
using RootMotion.FinalIK;
using UnityEngine;
using V = System.Numerics.Vector3;
using Q = System.Numerics.Quaternion;

namespace MainGirlShoulderIkStabilizer
{
    internal sealed partial class ShoulderRotator
    {
        private struct BonePose
        {
            internal V Position, LocalEuler;
            internal Q Rotation;
            internal static BonePose Read(Transform bone) => new BonePose {
                Position = N(bone.position), Rotation = N(bone.rotation), LocalEuler = N(bone.localEulerAngles) };
        }
        private struct ArmPose
        {
            internal BonePose Clavicle, Upper, Fore, Hand;
            internal static ArmPose Read(Arm arm) => new ArmPose {
                Clavicle = BonePose.Read(arm.Mapping.parentBone), Upper = BonePose.Read(arm.Mapping.bone1),
                Fore = BonePose.Read(arm.Mapping.bone2), Hand = BonePose.Read(arm.Mapping.bone3) };
        }
        private sealed class ArmDiagnostic
        {
            internal ArmPose Solved, Before, After;
            internal bool HasSolved, HasAfter;
        }
        private readonly ArmDiagnostic[] armDiagnostics = { new ArmDiagnostic(), new ArmDiagnostic() };
        private bool diagnosticSample;
        private bool diagnosticBasisReady;
        private long diagnosticSequence;
        private Vector3 diagnosticForwardLocal;
        private Vector3 diagnosticLeftLocal;
        private string bindStatus = "not_bound";

        private void BindDiagnosticBasis()
        {
            V up = N(chestUpLocal);
            Q inverse = Q.Inverse(N(chest.rotation));
            V left = V.Transform(N(arms[0].Mapping.parentBone.position - arms[1].Mapping.parentBone.position), inverse);
            left = ShoulderPoseMath.Unit(ShoulderPoseMath.Project(left, up), V.UnitX);
            V forward = ShoulderPoseMath.Unit(V.Cross(left, up), V.UnitZ);
            // Pick the anatomical plane's sign from the solver root, not a guessed bone Euler axis.
            if (ik.references != null && ik.references.root != null &&
                V.Dot(V.Transform(forward, N(chest.rotation)), N(ik.references.root.forward)) < 0f) forward = -forward;
            diagnosticLeftLocal = U(left); diagnosticForwardLocal = U(forward);
            diagnosticBasisReady = true;
        }
        internal string DescribeDiagnosticBinding()
        {
            if (ready && !diagnosticBasisReady) BindDiagnosticBasis();
            var b = new StringBuilder("BIND ready=" + ready + " status=" + bindStatus + " chest=\"" + BonePath(chest) + "\"");
            b.Append(" upLocal=").Append(F(N(chestUpLocal))).Append(" leftLocal=").Append(F(N(diagnosticLeftLocal)))
                .Append(" forwardLocal=").Append(F(N(diagnosticForwardLocal)));
            for (int side = 0; side < 2; side++)
            {
                var a = arms[side]; var m = a.Mapping;
                b.Append("\nBONES side=").Append(side == 0 ? "L" : "R").Append(" neutralTilt=").Append(F(a.NeutralTilt));
                b.Append(" clavicle=\"").Append(BonePath(m?.parentBone)).Append("\" upper=\"").Append(BonePath(m?.bone1))
                    .Append("\" fore=\"").Append(BonePath(m?.bone2)).Append("\" hand=\"").Append(BonePath(m?.bone3)).Append('"');
            }
            return b.ToString();
        }
        private static string BonePath(Transform bone)
        {
            if (bone == null) return "null";
            string result = bone.name;
            for (int i = 0; bone.parent != null && i < 64; i++) { bone = bone.parent; result = bone.name + "/" + result; }
            return result;
        }
        internal void CaptureDiagnosticSolvedPose()
        {
            diagnosticSample = false;
            if (!settings.ShoulderDiagnosticLog || Time.unscaledTime < nextLog) return;
            nextLog = Time.unscaledTime + settings.ShoulderDiagnosticLogInterval;
            diagnosticSequence++;
            if (!ready || disposed || ik == null || chest == null)
            {
                diagnostic?.Invoke("BLOCK frame=" + Time.frameCount + " reason=" + bindStatus + " ready=" + ready);
                return;
            }
            diagnosticSample = true;
            if (!diagnosticBasisReady) BindDiagnosticBasis();
            for (int i = 0; i < 2; i++)
            {
                armDiagnostics[i].HasAfter = false;
                armDiagnostics[i].HasSolved = ValidDiagnosticArm(arms[i]);
                if (armDiagnostics[i].HasSolved) armDiagnostics[i].Solved = ArmPose.Read(arms[i]);
            }
        }
        internal void CancelDiagnosticPose()
        {
            diagnosticSample = false;
            nextLog = 0f;
        }
        private static bool ValidDiagnosticArm(Arm arm) => arm.Mapping?.parentBone != null && arm.Mapping.bone1 != null && arm.Mapping.bone2 != null && arm.Mapping.bone3 != null;
        private void BeginArmDiagnostic(int side)
        {
            if (diagnosticSample && ValidDiagnosticArm(arms[side])) armDiagnostics[side].Before = ArmPose.Read(arms[side]);
        }
        private void EmitArmDiagnostic(int side, string status, float constraint, float upperAngle = 0f,
            float liftGoal = 0f, float effectiveWeight = 0f, float rawDeg = 0f, float cappedDeg = 0f,
            float goalReach = 0f, float appliedReach = 0f, float appliedDeg = 0f, float handError = 0f,
            ShoulderPoseMath.SwingTrace trace = default)
        {
            if (!settings.ShoulderDiagnosticLog || !diagnosticSample) return;
            string prefix = "frame=" + Time.frameCount + " sample=" + diagnosticSequence + " side=" + (side == 0 ? "L" : "R");
            var a = arms[side]; var d = armDiagnostics[side];
            Q chestRotation = N(chest.rotation);
            V forward = V.Transform(N(diagnosticForwardLocal), chestRotation);
            V outward = V.Transform(N(diagnosticLeftLocal), chestRotation) * (side == 0 ? 1f : -1f);
            var b = new StringBuilder("CALC " + prefix + " status=" + status + " active=" + enabled + " armIk=" + a.Running);
            b.Append(" upperDeg=").Append(F(upperAngle)).Append(" liftGoal=").Append(F(liftGoal))
                .Append(" neutralTilt=").Append(F(a.NeutralTilt)).Append(" existingLift=").Append(F(trace.ExistingLift))
                .Append(" missingLift=").Append(F(trace.MissingLift)).Append(" targetTilt=").Append(F(trace.TargetTilt))
                .Append(" upperYawInput=").Append(F(trace.UpperYaw)).Append(" forwardGoal=").Append(F(trace.ForwardGoal))
                .Append(" missingForward=").Append(F(trace.MissingForward))
                .Append(" upperModeBlend=").Append(F(trace.UpperModeBlend))
                .Append(" reachYaw=").Append(F(Azimuth(trace.ReachDirection, outward, forward)))
                .Append(" targetYaw=").Append(F(Azimuth(trace.TargetDirection, outward, forward)))
                .Append(" stretch=").Append(F(trace.Stretch)).Append(" reachBlend=").Append(F(trace.ReachBlend))
                .Append(" constraint=").Append(F(constraint)).Append(" effectiveWeight=").Append(F(effectiveWeight))
                .Append(" rawDeg=").Append(F(rawDeg)).Append(" cappedDeg=").Append(F(cappedDeg))
                .Append(" goalReach=").Append(F(goalReach)).Append(" appliedReach=").Append(F(appliedReach))
                .Append(" appliedDeg=").Append(F(appliedDeg)).Append(" handErrorMm=").Append(F(handError * 1000f));
            if (ValidDiagnosticArm(a))
            {
                d.After = ArmPose.Read(a); d.HasAfter = true;
                var chain = ik.solver.GetChain((FullBodyBipedChain)side);
                b.Append("\nWEIGHTS ").Append(prefix).Append(" solver=").Append(F(ik.solver.IKPositionWeight))
                    .Append(" mapping=").Append(F(a.Mapping.weight)).Append(" handPos=").Append(F(a.Effector.positionWeight))
                    .Append(" handRot=").Append(F(a.Effector.rotationWeight)).Append(" bend=").Append(F(chain.bendConstraint.weight))
                    .Append(" handTarget=\"").Append(BonePath(a.Effector.target)).Append("\" elbowTarget=\"").Append(BonePath(chain.bendConstraint.bendGoal)).Append('"');
                if (a.Effector.target != null) b.Append(" handTargetWorld=").Append(F(N(a.Effector.target.position)))
                    .Append(" finalToTargetMm=").Append(F(V.Distance(d.After.Hand.Position, N(a.Effector.target.position)) * 1000f));
                if (chain.bendConstraint.bendGoal != null) b.Append(" elbowTargetWorld=").Append(F(N(chain.bendConstraint.bendGoal.position)));
                if (d.HasSolved) AppendPose(b, prefix, "solved", side, d.Solved);
                AppendPose(b, prefix, "held", side, d.Before);
                AppendPose(b, prefix, "auto", side, d.After);
                if (d.HasSolved) b.Append("\nDELTA ").Append(prefix).Append(" chainClavDeg=").Append(F(RotationDifference(d.Solved.Clavicle, d.Before.Clavicle)))
                    .Append(" chainUpperDeg=").Append(F(RotationDifference(d.Solved.Upper, d.Before.Upper)));
                b.Append("\nDELTA ").Append(prefix).Append(" autoClavDeg=").Append(F(RotationDifference(d.Before.Clavicle, d.After.Clavicle)))
                    .Append(" autoUpperDeg=").Append(F(RotationDifference(d.Before.Upper, d.After.Upper)));
            }
            diagnostic?.Invoke(b.ToString());
        }
        internal void CompleteDiagnosticPose()
        {
            if (!settings.ShoulderDiagnosticLog) { CancelDiagnosticPose(); return; }
            if (!diagnosticSample) return;
            for (int i = 0; i < 2; i++)
            {
                var d = armDiagnostics[i]; if (!d.HasAfter || !ValidDiagnosticArm(arms[i])) continue;
                var final = ArmPose.Read(arms[i]);
                string prefix = "frame=" + Time.frameCount + " sample=" + diagnosticSequence + " side=" + (i == 0 ? "L" : "R");
                var b = new StringBuilder("FINAL_DELTA " + prefix + " clavDeg=" + F(RotationDifference(d.After.Clavicle, final.Clavicle)) +
                    " upperDeg=" + F(RotationDifference(d.After.Upper, final.Upper)) + " handMm=" + F(V.Distance(d.After.Hand.Position, final.Hand.Position) * 1000f));
                AppendPose(b, prefix, "final", i, final); diagnostic?.Invoke(b.ToString());
            }
            diagnosticSample = false;
        }
        private void AppendPose(StringBuilder b, string prefix, string stage, int side, ArmPose pose)
        {
            Q rotation = N(chest.rotation); V up = V.Transform(N(chestUpLocal), rotation);
            V forward = V.Transform(N(diagnosticForwardLocal), rotation);
            V outward = V.Transform(N(diagnosticLeftLocal), rotation) * (side == 0 ? 1f : -1f);
            V clavicle = ShoulderPoseMath.Unit(pose.Upper.Position - pose.Clavicle.Position, outward);
            V upper = ShoulderPoseMath.Unit(pose.Fore.Position - pose.Upper.Position, -up);
            float elbowFlex = 180f - ShoulderPoseMath.Angle(pose.Upper.Position - pose.Fore.Position, pose.Hand.Position - pose.Fore.Position);
            b.Append("\nPOSE ").Append(prefix).Append(" stage=").Append(stage)
                .Append(" shoulderTilt=").Append(F(ShoulderPoseMath.Tilt(clavicle, up)))
                .Append(" shoulderYaw=").Append(F(Azimuth(clavicle, outward, forward)))
                .Append(" upperElevation=").Append(F(ShoulderPoseMath.Angle(upper, -up)))
                .Append(" upperYaw=").Append(F(Azimuth(upper, outward, forward)))
                .Append(" upperForward=").Append(F((float)Math.Atan2(V.Dot(upper, forward), V.Dot(upper, -up)) / ShoulderPoseMath.Rad))
                .Append(" clavToUpper=").Append(F(ShoulderPoseMath.Angle(clavicle, upper)))
                .Append(" elbowFlex=").Append(F(elbowFlex)).Append(" upperWorld=").Append(F(upper));
            AppendBone(b, prefix, stage, "clavicle", pose.Clavicle);
            AppendBone(b, prefix, stage, "upper", pose.Upper);
            AppendBone(b, prefix, stage, "fore", pose.Fore);
            AppendBone(b, prefix, stage, "hand", pose.Hand);
        }
        private void AppendBone(StringBuilder b, string prefix, string stage, string label, BonePose pose)
        {
            Q inverse = Q.Inverse(N(chest.rotation));
            b.Append("\nBONE ").Append(prefix).Append(" stage=").Append(stage).Append(" bone=").Append(label)
                .Append(" localEuler=").Append(F(pose.LocalEuler)).Append(" chestQ=").Append(F(inverse * pose.Rotation))
                .Append(" chestPos=").Append(F(V.Transform(pose.Position - N(chest.position), inverse)));
        }
        private static float Azimuth(V direction, V outward, V forward)
        {
            float x = V.Dot(direction, outward), y = V.Dot(direction, forward);
            return x * x + y * y < 1e-8f ? float.NaN : (float)Math.Atan2(y, x) / ShoulderPoseMath.Rad;
        }
        private static float RotationDifference(BonePose a, BonePose b) => ShoulderPoseMath.RotationAngle(Q.Inverse(a.Rotation) * b.Rotation);
        private static string F(float n) => n.ToString("F4", CultureInfo.InvariantCulture);
        private static string F(V v) => "(" + F(v.X) + "," + F(v.Y) + "," + F(v.Z) + ")";
        private static string F(Q q) => "(" + F(q.X) + "," + F(q.Y) + "," + F(q.Z) + "," + F(q.W) + ")";
    }
}
