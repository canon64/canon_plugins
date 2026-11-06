using System;

namespace MainGameAiShoujoMode.Conversation
{
    // A ground-plane rebase preserves authored height and tilt instead of flattening the CG pose.
    internal sealed class KksEventCgReferenceFrame
    {
        internal struct Pose
        {
            internal double X, Y, Z, ForwardX, ForwardZ;
        }
        internal struct Point { internal double X, Y, Z; }
        internal readonly double Cos, Sin;
        internal readonly Point Origin;
        private KksEventCgReferenceFrame(double cos, double sin, Point origin) { Cos = cos; Sin = sin; Origin = origin; }
        internal static KksEventCgReferenceFrame Align(Pose source, Pose target)
        {
            Check(source); Check(target);
            var angle = Math.Atan2(target.ForwardX, target.ForwardZ) - Math.Atan2(source.ForwardX, source.ForwardZ);
            var cos = Math.Cos(angle); var sin = Math.Sin(angle);
            return new KksEventCgReferenceFrame(cos, sin, new Point
            { X = target.X - cos * source.X - sin * source.Z, Y = target.Y, Z = target.Z + sin * source.X - cos * source.Z });
        }
        internal Point Transform(Point native) => new Point
        { X = Origin.X + Cos * native.X + Sin * native.Z, Y = Origin.Y + native.Y, Z = Origin.Z - Sin * native.X + Cos * native.Z };
        private static void Check(Pose pose)
        {
            foreach (var value in new[] { pose.X, pose.Y, pose.Z, pose.ForwardX, pose.ForwardZ })
                if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentException("EventCG reference pose is not finite.");
            if (pose.ForwardX == default(double) && pose.ForwardZ == default(double)) throw new ArgumentException("EventCG reference pose has no horizontal heading.");
        }
    }
}
