using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MainGameVrNeckGrab
{
    internal static class ReflectionAccess
    {
        internal static object Get(object owner, string member)
        {
            if (owner == null) return null;
            Type t = owner.GetType();
            var f = AccessTools.Field(t, member);
            return f != null ? f.GetValue(owner) : AccessTools.Property(t, member)?.GetValue(owner, null);
        }
        internal static T Get<T>(object owner, string member) where T : class => Get(owner, member) as T;
        internal static Transform Bone(ChaControl actor, string name)
        {
            if (actor == null || actor.objBodyBone == null) return null;
            foreach (var t in actor.objBodyBone.GetComponentsInChildren<Transform>(true))
                if (string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }
        internal static ChaControl Actor(object owner) => Get<ChaControl>(owner, "female");
        internal static IEnumerable Items(object owner) => Get(owner, "dicItem") is IDictionary d ? d.Values : null;
    }
}
