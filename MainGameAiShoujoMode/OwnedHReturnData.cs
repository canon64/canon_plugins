using System.Runtime.CompilerServices;
using FreeH;
using SaveData;
using Object = UnityEngine.Object;

namespace MainGameAiShoujoMode
{
    // Weak ownership survives a room/session object's disposal without retaining actors.
    internal static class OwnedHReturnData
    {
        private static readonly ConditionalWeakTable<Heroine, Player> Owners = new ConditionalWeakTable<Heroine, Player>();
        internal static void Register(string sourceId, Heroine heroine, Player player)
        {
            if (sourceId != "hotel-room-201" && sourceId != "hotel-room-202" && sourceId != "hotel-room-203"
                && sourceId != "hotel-room-204" && sourceId != "hotel-room-205" && sourceId != "hotel-room-206"
                && sourceId != "hotel-room-reception-suite") return;
            Owners.Remove(heroine); Owners.Add(heroine, player);
        }

        internal static void RemoveOwned()
        {
            if (Manager.Scene.commonSpace == null) return;
            foreach (var back in Manager.Scene.commonSpace.GetComponents<FreeHBackData>())
                if (back.heroine != null && Owners.TryGetValue(back.heroine, out _))
                    Object.DestroyImmediate(back);
        }

        internal static FreeHBackData GetNativeReturn(UnityEngine.GameObject space)
        {
            RemoveOwned();
            return space.GetComponent<FreeHBackData>();
        }
    }
}
