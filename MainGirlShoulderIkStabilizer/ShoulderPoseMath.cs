using System;
using System.Numerics;

namespace MainGirlShoulderIkStabilizer
{
    // The production geometry is also compiled into the headless regression harness.
    internal static class ShoulderPoseMath
    {
        internal struct SwingTrace
        {
            internal float Stretch, ReachBlend, CurrentTilt, ExistingLift, MissingLift, TargetTilt;
            internal float UpperYaw, ForwardGoal, MissingForward;
            internal float UpperModeBlend;
            internal Vector3 ReachDirection, TargetDirection;
        }
        internal const float Rad = (float)(Math.PI / 180.0);
        internal static float Clamp(float v, float lo, float hi) => Math.Max(lo, Math.Min(hi, v));
        internal static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        internal static bool Finite(Vector3 v) => Finite(v.X) && Finite(v.Y) && Finite(v.Z);
        internal static Vector3 Unit(Vector3 v, Vector3 fallback)
            => Finite(v) && v.LengthSquared() > 1e-12f ? Vector3.Normalize(v) : fallback;
        internal static Vector3 Project(Vector3 v, Vector3 normal) => v - normal * Vector3.Dot(v, normal);
        internal static float Angle(Vector3 a, Vector3 b)
            => (float)Math.Acos(Clamp(Vector3.Dot(Unit(a, Vector3.UnitX), Unit(b, Vector3.UnitX)), -1f, 1f)) / Rad;
        internal static float Tilt(Vector3 direction, Vector3 up)
            => (float)Math.Asin(Clamp(Vector3.Dot(Unit(direction, Vector3.UnitX), up), -1f, 1f)) / Rad;
        internal static float Elevation(float angle, float start, float full, float maximum)
        {
            float t = Clamp((angle - start) / Math.Max(1f, full - start), 0f, 1f);
            return maximum * t * t * (3f - 2f * t);
        }
        internal static Quaternion FromTo(Vector3 from, Vector3 to)
        {
            from = Unit(from, Vector3.UnitX); to = Unit(to, from);
            float d = Clamp(Vector3.Dot(from, to), -1f, 1f);
            Vector3 cross = Vector3.Cross(from, to);
            if (d > 0f && cross.LengthSquared() < 1e-14f) return Quaternion.Identity;
            if (d < -0.999999f)
            {
                Vector3 axis = Vector3.Cross(from, Math.Abs(from.X) < 0.8f ? Vector3.UnitX : Vector3.UnitY);
                return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), (float)Math.PI);
            }
            return Quaternion.Normalize(new Quaternion(cross, 1f + d));
        }
        internal static float RotationAngle(Quaternion q)
            => 2f * (float)Math.Atan2(new Vector3(q.X, q.Y, q.Z).Length(), Math.Abs(q.W)) / Rad;
        internal static Quaternion LimitAngle(Quaternion q, float limit)
        {
            float angle = RotationAngle(q);
            return angle <= limit || angle < 1e-5f ? q : Quaternion.Slerp(Quaternion.Identity, q, limit / angle);
        }

        internal static Quaternion DesiredSwing(Vector3 shoulder, Vector3 upperRoot, Vector3 elbow,
            Vector3 hand, Vector3 up, float neutralTilt, float armLength, float reachWeight,
            float reachOffset, float loweredScale, bool reverseLowered, float elevationWeight,
            float start, float full, float lift, float maxBlend, float solverWeight,
            out float upperAngle, out float requestedLift)
            => DesiredSwing(shoulder, upperRoot, elbow, hand, up, neutralTilt, armLength, reachWeight,
                reachOffset, loweredScale, reverseLowered, elevationWeight, start, full, lift, maxBlend,
                solverWeight, out upperAngle, out requestedLift, out _);

        internal static Quaternion DesiredSwing(Vector3 shoulder, Vector3 upperRoot, Vector3 elbow,
            Vector3 hand, Vector3 up, float neutralTilt, float armLength, float reachWeight,
            float reachOffset, float loweredScale, bool reverseLowered, float elevationWeight,
            float start, float full, float lift, float maxBlend, float solverWeight,
            out float upperAngle, out float requestedLift, out SwingTrace trace,
            Vector3 forward = default, Vector3 neutralDirection = default, float maxForward = 30f)
        {
            Vector3 current = Unit(upperRoot - shoulder, Vector3.UnitX);
            upperAngle = Angle(elbow - upperRoot, -up);
            bool lowered = upperAngle < 90f;
            if (lowered) { reachWeight *= loweredScale; reachOffset *= loweredScale; }
            float stretch = armLength > 1e-6f ? (hand - upperRoot).Length() / armLength : 0f;
            float blend = Clamp((stretch - 1f + reachOffset) * reachWeight, 0f, 1f);
            Quaternion reach = FromTo(current, hand - shoulder);
            if (lowered && reverseLowered) reach = Quaternion.Inverse(reach);
            Vector3 reachDirection = Vector3.Transform(current, Quaternion.Slerp(Quaternion.Identity, reach, blend));
            requestedLift = Elevation(upperAngle, start, full, lift) * elevationWeight;
            float currentTilt = Tilt(current, up);
            float missingLift = Math.Max(0f, requestedLift - Math.Max(0f, currentTilt - neutralTilt));
            // Fade from legacy lowered-arm assistance to an absolute upper-arm-driven goal.
            // Adding lift to a depressed IK shoulder retained that depression; using the
            // wrist's reachDirection also changed the goal when only the wrist moved.
            float upperMode = elevationWeight > 0f
                ? Elevation(upperAngle, start, Math.Min(full, Math.Max(start + 1f, 90f)), 1f) : 0f;
            float legacyTilt = Tilt(reachDirection, up);
            float absoluteTilt = neutralTilt + requestedLift;
            float targetTilt = Clamp(requestedLift > 0f
                ? absoluteTilt + Math.Max(0f, legacyTilt - absoluteTilt) * (1f - upperMode)
                : legacyTilt, -85f, 85f);
            Vector3 horizontal = Unit(Project(reachDirection, up), Unit(Project(current, up), Vector3.UnitX));
            float upperYaw = 0f, forwardGoal = 0f, missingForward = 0f;
            Vector3 neutralHorizontal = Unit(Project(neutralDirection, up), Unit(Project(current, up), Vector3.UnitX));
            Vector3 front = Unit(Project(Project(forward, up), neutralHorizontal), Vector3.Zero);
            Vector3 upperDirection = Unit(elbow - upperRoot, -up);
            float upperFront = Vector3.Dot(upperDirection, front);
            // Shoulder protraction comes from the upper arm, even when a folded elbow puts
            // the hand close to the shoulder. At vertical, the horizontal magnitude fades
            // the goal continuously rather than choosing an unstable azimuth.
            Vector3 upperHorizontal = neutralHorizontal;
            if (front.LengthSquared() > .5f && upperFront > 0f && requestedLift > 0f)
            {
                upperYaw = Clamp((float)Math.Atan2(upperFront, Vector3.Dot(upperDirection, neutralHorizontal)) / Rad, 0f, 90f);
                float raised = Elevation(upperAngle, start, full, 1f) * elevationWeight;
                float horizontalMagnitude = Project(upperDirection, up).Length();
                forwardGoal = maxForward * (upperYaw / 90f) * raised * horizontalMagnitude;
                float currentYaw = (float)Math.Atan2(Vector3.Dot(horizontal, front), Vector3.Dot(horizontal, neutralHorizontal)) / Rad;
                missingForward = Math.Max(0f, forwardGoal - currentYaw);
                upperHorizontal = Unit(neutralHorizontal * (float)Math.Cos(forwardGoal * Rad) + front * (float)Math.Sin(forwardGoal * Rad), neutralHorizontal);
            }
            horizontal = Unit(Vector3.Transform(horizontal, Quaternion.Slerp(Quaternion.Identity,
                FromTo(horizontal, upperHorizontal), upperMode)), upperHorizontal);
            Vector3 target = horizontal * (float)Math.Cos(targetTilt * Rad) + up * (float)Math.Sin(targetTilt * Rad);
            trace = new SwingTrace { Stretch = stretch, ReachBlend = blend, CurrentTilt = currentTilt,
                ExistingLift = Math.Max(0f, currentTilt - neutralTilt), MissingLift = missingLift,
                TargetTilt = targetTilt, ReachDirection = reachDirection, TargetDirection = target,
                UpperYaw = upperYaw, ForwardGoal = forwardGoal, MissingForward = missingForward,
                UpperModeBlend = upperMode };
            return Quaternion.Slerp(Quaternion.Identity, FromTo(current, target), Clamp(solverWeight, 0f, maxBlend));
        }

        internal static Quaternion SmoothRotation(Quaternion current, Quaternion target, float dt, float duration, float speed)
        {
            dt = Clamp(dt, 0f, 0.05f);
            if (dt <= 0f) return current;
            float angle = RotationAngle(Quaternion.Inverse(current) * target);
            if (angle < 0.01f) return target;
            float fraction = duration <= 0f ? 1f : 1f - (float)Math.Exp(-4f * dt / duration);
            fraction = Math.Min(fraction, speed * dt / angle);
            return Quaternion.Normalize(Quaternion.Slerp(current, target, Clamp(fraction, 0f, 1f)));
        }

        internal static bool Reachable(Vector3 root, Vector3 hand, float upperLength, float foreLength)
        {
            float distance = Vector3.Distance(root, hand);
            return distance <= upperLength + foreLength + 1e-6f && distance >= Math.Abs(upperLength - foreLength) - 1e-6f;
        }
        internal static float ReachFraction(Vector3 shoulder, Vector3 root, Vector3 hand,
            float upperLength, float foreLength, Quaternion delta)
        {
            if (!Reachable(root, hand, upperLength, foreLength)) return 0f;
            // Find the first boundary connected to zero, even if the arc re-enters reach.
            float lo = 0f, hi = 1f;
            bool boundary = false;
            for (int i = 1; i <= 8; i++)
            {
                float t = i / 8f;
                Vector3 candidate = shoulder + Vector3.Transform(root - shoulder, Quaternion.Slerp(Quaternion.Identity, delta, t));
                if (!Reachable(candidate, hand, upperLength, foreLength)) { hi = t; boundary = true; break; }
                lo = t;
            }
            if (!boundary) return 1f;
            for (int i = 0; i < 12; i++)
            {
                float t = (lo + hi) * 0.5f;
                Vector3 candidate = shoulder + Vector3.Transform(root - shoulder, Quaternion.Slerp(Quaternion.Identity, delta, t));
                if (Reachable(candidate, hand, upperLength, foreLength)) lo = t; else hi = t;
            }
            return lo;
        }
        internal static bool SolveElbow(Vector3 root, Vector3 hand, Vector3 originalElbow,
            Vector3 fallbackBend, float upperLength, float foreLength, out Vector3 elbow, out Vector3 bend,
            bool preserveBendPlane = false)
        {
            elbow = originalElbow; bend = fallbackBend;
            Vector3 toHand = hand - root;
            float d = toHand.Length();
            if (!Finite(d) || d < 1e-6f || upperLength < 1e-6f || foreLength < 1e-6f) return false;
            if (!Reachable(root, hand, upperLength, foreLength)) return false;
            Vector3 direction = toHand / d;
            Vector3 projected = Project(originalElbow - root, direction);
            Vector3 fallback = Unit(Project(fallbackBend, direction), Unit(Vector3.Cross(direction,
                Math.Abs(direction.X) < 0.8f ? Vector3.UnitX : Vector3.UnitY), Vector3.UnitZ));
            // Near a straight arm, a numerically tiny elbow offset must not flip the plane.
            bend = !preserveBendPlane && projected.LengthSquared() > upperLength * upperLength * 1e-6f ? Unit(projected, fallback) : fallback;
            float x = (upperLength * upperLength - foreLength * foreLength + d * d) / (2f * d);
            float y = (float)Math.Sqrt(Math.Max(0f, upperLength * upperLength - x * x));
            elbow = root + direction * x + bend * y;
            return Finite(elbow);
        }
    }
}
