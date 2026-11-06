using UnityEngine;

namespace MainGameAiShoujoH.Contracts
{
    public static class HPointRange
    {
        public const float Radius = 10f;
        public static bool Contains(Vector3 origin, Vector3 destination)
            => (destination - origin).sqrMagnitude <= Radius * Radius;
    }
}
