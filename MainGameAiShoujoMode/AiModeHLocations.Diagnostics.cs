using System.Runtime.Serialization;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    public static partial class AiModeHLocations
    {
        [DataContract] internal sealed class SelectionTrace
        {
            [DataMember] public string policy = "nearest_horizontal_to_female; no_bed_priority; no_placed_furniture_search";
            [DataMember] public float radius, verticalTolerance;
            [DataMember] public Vector3 originFloor;
            [DataMember] public CandidateTrace[] candidates;
        }
        [DataContract] internal sealed class CandidateTrace
        {
            [DataMember] public string id, sourceName, result, blockingCollider;
            [DataMember] public int[] categories;
            [DataMember] public Vector3 sourcePosition, worldPosition, femaleToPoint, maleToPoint;
            [DataMember] public bool maleAvailable, groundFound;
            [DataMember] public float femaleHorizontalDistance, maleHorizontalDistance, supportHeightDelta;
            [DataMember] public Vector3 supportWorld, blockingHitWorld;
        }
    }
}
