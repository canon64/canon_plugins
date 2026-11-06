using UnityEngine;

namespace MainGameKaleidoscope
{
    internal static class VrPatternCenter
    {
        // A VR eye's texture midpoint is not necessarily straight ahead. Project
        // the head's forward direction at infinity so IPD adds no convergence.
        internal static Vector2 Offset(Matrix4x4 projection, Vector3 eyeForward)
        {
            Vector4 clip = projection * new Vector4(eyeForward.x, eyeForward.y, eyeForward.z, 0f);
            if (!(clip.w > .000001f)) return Vector2.zero;
            float x = .5f * clip.x / clip.w;
            float y = .5f * clip.y / clip.w;
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y))
                return Vector2.zero;
            return new Vector2(x, y);
        }

        internal static Vector2 ForEye(Camera camera, Camera.StereoscopicEye eye)
        {
            Vector3 eyeForward = camera.GetStereoViewMatrix(eye).MultiplyVector(camera.transform.forward);
            return Offset(camera.GetStereoProjectionMatrix(eye), eyeForward);
        }
    }
}
