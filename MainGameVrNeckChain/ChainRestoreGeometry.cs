using UnityEngine;

namespace MainGameVrNeckChain
{
    internal static class ChainRestoreGeometry
    {
        internal static Vector3 ToLocal(Vector3 point, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            // Explicit inverse keeps this geometry usable in finite managed tests
            // as well as Unity (Quaternion.Inverse itself is a native call).
            float norm = rotation.x * rotation.x + rotation.y * rotation.y +
                         rotation.z * rotation.z + rotation.w * rotation.w;
            var inverse = new Quaternion(-rotation.x / norm, -rotation.y / norm,
                -rotation.z / norm, rotation.w / norm);
            var local = inverse * (point - position);
            return new Vector3(local.x / scale.x, local.y / scale.y, local.z / scale.z);
        }

        internal static bool Finite(Vector3 value) =>
            !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) ||
              float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));

        internal static Vector3 ResolveLegacyRoot(Vector3 savedLocal, Vector3 savedWorld,
            Vector3 anchorLocal, Vector3 anchorWorld, float attachRadius, bool attached) =>
            attached && (savedWorld - anchorWorld).magnitude > attachRadius * 1.5f
                ? anchorLocal : savedLocal;
    }
}
