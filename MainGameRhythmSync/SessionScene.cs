using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MainGameRhythmSync
{
    // Captured once at H entry. VRHScene is not derived from HSceneProc;
    // VRHFlag is derived from HFlag. Never search Unity objects to repair a session.
    internal sealed class SessionScene
    {
        internal readonly MonoBehaviour Source;
        internal readonly HFlag flags;
        private readonly Dictionary<string, FieldInfo> _fields = new Dictionary<string, FieldInfo>();
        internal SessionScene(MonoBehaviour source)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            foreach (string name in new[] { "flags", "lstFemale", "male", "male1", "face", "face1", "lstUseAnimInfo" })
                _fields[name] = AccessTools.Field(source.GetType(), name);
            flags = Read<HFlag>("flags") ?? throw new InvalidOperationException("H scene has no HFlag");
        }
        internal T Read<T>(string name) where T : class =>
            _fields.TryGetValue(name, out var field) ? field?.GetValue(Source) as T : null;
        internal static IEnumerable<MethodBase> Methods(string method)
        {
            var normal = AccessTools.Method(typeof(HSceneProc), method);
            if (normal != null) yield return normal;
            var vr = Type.GetType("VRHScene, Assembly-CSharp", false);
            var vrMethod = vr == null ? null : AccessTools.Method(vr, method);
            if (vrMethod != null && vrMethod != normal) yield return vrMethod;
        }
        internal FaceListCtrl face => Read<FaceListCtrl>("face");
        internal FaceListCtrl face1 => Read<FaceListCtrl>("face1");
    }
}
