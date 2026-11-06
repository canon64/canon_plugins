using System;
using System.Reflection;
using UnityEngine;

namespace MapEnvironmentRuntime
{
    public static class SandboxStaticBatchBinding
    {
        public static int Bind(Transform root)
        {
            var property = typeof(Renderer).GetProperty("staticBatchRootTransform", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null || property.GetSetMethod(true) == null) throw new InvalidOperationException("Static batch root binding is unavailable");
            int count = default(int);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.isPartOfStaticBatch || property.GetValue(renderer, null) != null) continue;
                property.SetValue(renderer, root, null); count++;
            }
            Debug.Log("SANDBOX_STATIC_BATCH_BOUND renderers=" + count + " root=" + root.name);
            return count;
        }
    }
}
