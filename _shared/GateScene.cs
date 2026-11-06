using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CanonPlugins.Shared
{
    // A view of the exact scene supplied at entry. Never searches Unity scenes or maps.
    internal sealed class GateScene
    {
        internal readonly MonoBehaviour Source;
        private readonly Dictionary<string, FieldInfo> _fields = new Dictionary<string, FieldInfo>();
        private readonly List<ChaControl> _aiActors;
        internal GateScene(MonoBehaviour source, ChaControl aiActor = null)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            if (aiActor != null) _aiActors = new List<ChaControl> { aiActor };
            foreach (var field in source.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                _fields[field.Name] = field;
        }
        internal T Read<T>(string name) where T : class => Source != null && _fields.TryGetValue(name, out var field) ? field.GetValue(Source) as T : null;
        internal HFlag flags => Read<HFlag>("flags");
        internal HVoiceCtrl voice => Read<HVoiceCtrl>("voice");
        internal HandCtrl hand => Read<HandCtrl>("hand");
        internal List<ChaControl> lstFemale => _aiActors ?? Read<List<ChaControl>>("lstFemale");
        internal bool isActiveAndEnabled => Source != null && Source.isActiveAndEnabled;
        internal int GetInstanceID() => Source.GetInstanceID();
        public static implicit operator bool(GateScene scene) => scene != null && scene.Source != null;
    }
}
