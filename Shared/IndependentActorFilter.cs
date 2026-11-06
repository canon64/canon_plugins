using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEngine;
namespace CanonPlugins.Shared
{
    internal static class IndependentActorFilter
    {
        private static Type _markerType;
        private static int _assemblyRevision;
        private static int _resolvedAssemblyRevision = -1;

        static IndependentActorFilter()
        {
            // Assembly notifications may run off the Unity thread: only mark discovery stale.
            AppDomain.CurrentDomain.AssemblyLoad += (_, __) => Interlocked.Increment(ref _assemblyRevision);
        }

        internal static bool IsIndependent(ChaControl c)
        {
            if (c == null) return false;
            if (_markerType == null)
            {
                int revision = Volatile.Read(ref _assemblyRevision);
                if (_resolvedAssemblyRevision != revision)
                {
                    _resolvedAssemblyRevision = revision;
                    foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        Type type;
                        try { type = assembly.GetType("MainGameVMDPlay.IndependentActorMarker", false); }
                        catch { continue; }
                        if (type == null || !typeof(Component).IsAssignableFrom(type)) continue;
                        _markerType = type;
                        break;
                    }
                }
            }
            // The marker is owned by VMDPlay. Its absence is a cached negative result.
            // Read the component each time so marker insertion/removal is visible immediately.
            return _markerType != null && c.GetComponent(_markerType) != null;
        }
        internal static ChaControl[] FindNonIndependent() {
            var result = new List<ChaControl>();
            foreach (var c in UnityEngine.Object.FindObjectsOfType<ChaControl>()) if (c != null && !IsIndependent(c)) result.Add(c);
            return result.ToArray();
        }
        private static readonly FieldInfo[] ParticipantFields = Array.FindAll(typeof(HSceneProc).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public), f => f.Name == "lstFemale" || f.Name == "lstMale" || f.Name == "male" || f.Name == "male1");
        internal static ChaControl[] FindHActors() {
            var scene = UnityEngine.Object.FindObjectOfType<HSceneProc>();
            if (scene == null) return FindNonIndependent();
            var result = new List<ChaControl>();
            foreach (var field in ParticipantFields) {
                object value = field.GetValue(scene);
                if (value is ChaControl actor) { if (actor != null && !IsIndependent(actor) && !result.Contains(actor)) result.Add(actor); }
                else if (value is IEnumerable actors) foreach (object item in actors) {
                    var c = item as ChaControl;
                    if (c != null && !IsIndependent(c) && !result.Contains(c)) result.Add(c);
                }
            }
            return result.ToArray();
        }
    }
}
