using UnityEngine;

namespace MainGameBlankMapAdd
{
    internal static class MapWarpGeometry
    {
        internal static Vector3 RootForPoint(Vector3 localPoint, Vector3 actor, Quaternion rotation, Vector3 scale) =>
            actor - rotation * Vector3.Scale(localPoint, scale);

        internal static Vector3 OverlayOffset(Vector3 targetRoot, Vector3 basePosition, Quaternion baseRotation,
            Quaternion rotation, Vector3 actorAnchor, bool aroundActor)
        {
            if (aroundActor)
                targetRoot = actorAnchor + Quaternion.Inverse(rotation * Quaternion.Inverse(baseRotation)) * (targetRoot - actorAnchor);
            return targetRoot - basePosition;
        }
    }
}
