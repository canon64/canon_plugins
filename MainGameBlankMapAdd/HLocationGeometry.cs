using UnityEngine;
namespace MainGameBlankMapAdd
{
    internal static class HLocationGeometry
    {
        internal static Vector3 MoveMember(Vector3 member,Vector3 origin,Vector3 destination,Quaternion delta) => destination+delta*(member-origin);
        internal static Vector3 RotateAround(Vector3 position,Vector3 center,Quaternion delta) => center+delta*(position-center);
    }
}
