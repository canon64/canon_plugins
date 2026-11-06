using System.Collections;
using System.Collections.Generic;

namespace CanonPlugins.Shared
{
    internal static class SessionActors
    {
        // Participants belong to the scene captured at entry; never discover another H scene.
        internal static ChaControl[] Current()
        {
            var actors = new List<ChaControl>();
            foreach (string name in new[] { "lstFemale", "lstMale", "male", "male1" })
            {
                object value = PluginSessionGate.ReadScene<object>(name);
                if (value is ChaControl actor) Add(actors, actor);
                else if (value is IEnumerable list)
                    foreach (object item in list) if (item is ChaControl cha) Add(actors, cha);
            }
            if (PluginSessionGate.Kind == CanonGate.GateApi.SessionKind.Maker)
                foreach (var cha in IndependentActorFilter.FindNonIndependent()) Add(actors, cha);
            return actors.ToArray();
        }
        private static void Add(List<ChaControl> actors, ChaControl cha)
        {
            if (cha != null && !IndependentActorFilter.IsIndependent(cha) && !actors.Contains(cha)) actors.Add(cha);
        }
    }
}
