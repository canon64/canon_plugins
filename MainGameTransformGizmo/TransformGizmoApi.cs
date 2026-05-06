using UnityEngine;

namespace MainGameTransformGizmo
{
    /// <summary>
    /// Public entry points for other plugins.
    /// </summary>
    public static class TransformGizmoApi
    {
        public static bool IsAvailable => Plugin.Instance != null;

        public static TransformGizmo Attach(GameObject target)
        {
            if (target == null) return null;

            TransformGizmo gizmo = target.GetComponent<TransformGizmo>();
            if (gizmo == null)
            {
                gizmo = target.AddComponent<TransformGizmo>();
            }

            return gizmo;
        }

        public static bool TryAttach(GameObject target, out TransformGizmo gizmo)
        {
            gizmo = null;
            if (!IsAvailable || target == null) return false;

            gizmo = Attach(target);
            return gizmo != null;
        }

        public static TransformGizmo Get(GameObject target)
        {
            return target == null ? null : target.GetComponent<TransformGizmo>();
        }

        public static bool Detach(GameObject target)
        {
            TransformGizmo gizmo = Get(target);
            if (gizmo == null) return false;

            Object.Destroy(gizmo);
            return true;
        }

        public static float GetSizeMultiplier(TransformGizmo gizmo)
        {
            return gizmo != null ? gizmo.SizeMultiplier : TransformGizmo.DefaultSizeMultiplier;
        }

        public static float GetSizeMultiplier(GameObject target)
        {
            return GetSizeMultiplier(Get(target));
        }

        public static bool SetSizeMultiplier(TransformGizmo gizmo, float sizeMultiplier)
        {
            if (gizmo == null) return false;
            gizmo.SetSizeMultiplier(sizeMultiplier);
            return true;
        }

        public static bool SetSizeMultiplier(GameObject target, float sizeMultiplier)
        {
            TransformGizmo gizmo = Get(target);
            if (gizmo == null) return false;
            gizmo.SetSizeMultiplier(sizeMultiplier);
            return true;
        }
    }
}
