using System.Collections.Generic;
using System.Linq;

namespace MainGameBlankMapAdd
{
    internal static class MapLocationNavigation
    {
        internal static List<string> Ids(MapWarpDocument oldPlaces, HLocationDocument hPlaces, string key) =>
            (oldPlaces?.Points.Where(p => p.MapKey == key && !p.IsOrigin).Select(p => p.Id) ?? Enumerable.Empty<string>())
            .Concat(hPlaces?.Presets.Where(p => HLocationMapIdentity.Same(p.MapKey,key)).Select(p => p.Id) ?? Enumerable.Empty<string>())
            .Distinct().ToList();

        internal static string Next(MapWarpDocument oldPlaces, HLocationDocument hPlaces, string key, string active, int direction)
        {
            var ids = Ids(oldPlaces, hPlaces, key);
            if (ids.Count == 0) return null;
            int index = ids.IndexOf(active);
            index = index < 0 ? (direction < 0 ? ids.Count - 1 : 0) :
                (index + (direction < 0 ? -1 : 1) + ids.Count) % ids.Count;
            return ids[index];
        }
    }
}
