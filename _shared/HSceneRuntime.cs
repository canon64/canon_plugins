using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace CanonPlugins.Shared
{
    // Bound once at the permitted session start. VRHFlag/VRHVoiceCtrl inherit the native types.
    internal sealed class HSceneRuntime
    {
        internal readonly MonoBehaviour Scene;
        internal readonly HFlag Flags;
        internal readonly HVoiceCtrl Voice;
        internal readonly IList Females;
        internal HSceneRuntime(MonoBehaviour scene)
        {
            Scene = scene;
            if (scene == null) return;
            Flags = Read(scene, "flags") as HFlag;
            Voice = Read(scene, "voice") as HVoiceCtrl;
            Females = Read(scene, "lstFemale") as IList;
        }
        private static object Read(object owner, string name)
        {
            for (Type type = owner.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field.GetValue(owner);
            }
            return null;
        }
    }
}
