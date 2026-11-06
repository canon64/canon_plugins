using System;
using System.Reflection;
using UnityEngine;

namespace MainGameObjectComposer
{
    // Called only for a user gesture. Preserve the other slot's differences when editing both.
    internal static class PresetEditFields
    {
        internal static bool Apply<T>(T before, T after, T target, bool additive) where T : class
        {
            bool changed = false;
            foreach (FieldInfo field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.Name == "id" || field.Name == "phaseContinuityOffsetTurns"
                    || field.Name == "femaleRootPose" || field.Name == "worldPosition" || field.Name == "worldRotation") continue;
                object a = field.GetValue(before), b = field.GetValue(after);
                if (Equals(a, b)) continue;
                if (field.FieldType == typeof(float))
                {
                    float next = (float)b;
                    if (float.IsNaN(next) || float.IsInfinity(next)) continue;
                    if (additive) next = (float)field.GetValue(target) + next - (float)a;
                    if (!Finite(next)) continue;
                    field.SetValue(target, next);
                }
                else if (field.FieldType == typeof(Vector3))
                {
                    Vector3 next = (Vector3)b;
                    if (!Finite(next.x) || !Finite(next.y) || !Finite(next.z)) continue;
                    if (additive) next = (Vector3)field.GetValue(target) + next - (Vector3)a;
                    if (!Finite(next.x) || !Finite(next.y) || !Finite(next.z)) continue;
                    field.SetValue(target, next);
                }
                else field.SetValue(target, b);
                changed = true;
            }
            return changed;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
