using System.Collections.Generic;
using System.Linq;

namespace MainGameAiShoujoMode
{
    internal static class HotelElevatorRoutes
    {
        internal static HotelFloorDefinition[] Destinations(IEnumerable<HotelFloorDefinition> floors)
            => floors.Where(floor => floor.room == null).ToArray();

        internal static string Label(HotelFloorDefinition floor)
        {
            switch (floor.id)
            {
                case "lobby": return "1F　ロビー";
                case "second": return "2F　客室フロア";
                case "third": return "3F　ステージ";
                case "fourth": return "4F　大浴場";
                default: return floor.id;
            }
        }

        internal static bool OpensSelector(HotelFloorDefinition source, HotelPortalDefinition portal,
            IEnumerable<HotelFloorDefinition> floors)
            => source.room == null && (portal == null || floors.Any(floor => floor.id == portal.destination && floor.room == null));
    }
}
