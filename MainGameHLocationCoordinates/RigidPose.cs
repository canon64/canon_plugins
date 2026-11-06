using System;
using UnityEngine;

namespace MainGameHLocationCoordinates
{
    [Serializable]
    public struct RigidPose
    {
        public Vector3 position;
        public Quaternion rotation;
        public RigidPose(Vector3 position, Quaternion rotation)
        {
            if (!Finite(position.x) || !Finite(position.y) || !Finite(position.z)
                || !Finite(rotation.x) || !Finite(rotation.y) || !Finite(rotation.z) || !Finite(rotation.w))
                throw new ArgumentException("Non-finite pose");
            float n = (float)Math.Sqrt(rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w);
            if (n < 0.000001f) throw new ArgumentException("Zero quaternion");
            this.position = position;
            this.rotation = new Quaternion(rotation.x / n, rotation.y / n, rotation.z / n, rotation.w / n);
        }
        public static RigidPose Identity => new RigidPose(Vector3.zero, Quaternion.identity);
        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        public RigidPose Inverse()
        {
            Quaternion q = Quaternion.Inverse(rotation);
            return new RigidPose(q * -position, q);
        }
        public static RigidPose operator *(RigidPose a, RigidPose b)
            => new RigidPose(a.position + a.rotation * b.position, a.rotation * b.rotation);
        public Vector3 Point(Vector3 p) => position + rotation * p;
        public static RigidPose Rebase(RigidPose source, RigidPose destination, RigidPose original)
            => destination * source.Inverse() * original;
    }

    public sealed class CoordinateChange
    {
        public long Operation { get; }
        public long MapOperation { get; }
        public string Reason { get; }
        public string PlaceId { get; }
        public RigidPose Before { get; }
        public RigidPose Destination { get; }
        internal CoordinateChange(long operation, long mapOperation, string reason, string placeId, RigidPose before, RigidPose destination)
        { Operation = operation; MapOperation = mapOperation; Reason = reason; PlaceId = placeId; Before = before; Destination = destination; }
    }

    // No Transform references: every projection starts from the immutable registration frame.
    public sealed class CoordinateSpace
    {
        private readonly Action _validate;
        private long _operation;
        private bool _closed;
        public RigidPose Source { get; }
        public RigidPose Destination { get; private set; }
        public event Action<CoordinateChange> Changed;
        internal CoordinateSpace(RigidPose source, Action validate)
        { Source = new RigidPose(source.position, source.rotation); Destination = Source; _validate = validate; }
        private void Check() { if (_closed) throw new ObjectDisposedException(nameof(CoordinateSpace)); _validate(); }
        public void UpdateDestination(RigidPose destination, long mapOperation = 0, string reason = "explicit", string placeId = "", RigidPose? before = null)
        {
            Check();
            var previous = before ?? Destination;
            Destination = new RigidPose(destination.position, destination.rotation);
            var change = new CoordinateChange(++_operation, mapOperation, reason, placeId, previous, Destination);
            // State is committed before any observer. Failure must reach the owner, never be silently swallowed.
            Changed?.Invoke(change);
        }
        public RigidPose Transform(RigidPose original) { Check(); return RigidPose.Rebase(Source, Destination, original); }
        public RigidPose InverseTransform(RigidPose current) { Check(); return RigidPose.Rebase(Destination, Source, current); }
        public Vector3 TransformPoint(Vector3 point) => Transform(RigidPose.Identity).Point(point);
        public Quaternion TransformRotation(Quaternion rotation) => Transform(new RigidPose(Vector3.zero, rotation)).rotation;
        public Vector3 InverseTransformPoint(Vector3 point) => InverseTransform(RigidPose.Identity).Point(point);
        public Quaternion InverseTransformRotation(Quaternion rotation) => InverseTransform(new RigidPose(Vector3.zero, rotation)).rotation;
        public void Close() { _closed = true; Changed = null; }
    }
}
