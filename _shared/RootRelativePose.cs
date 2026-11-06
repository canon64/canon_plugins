using System;
using System.Runtime.Serialization;
using UnityEngine;

namespace CanonPlugins.Shared
{
    // Primitive fields work with both Unity JsonUtility and DataContractJsonSerializer.
    [Serializable, DataContract]
    public sealed class RootRelativePose
    {
        [DataMember] public float x;
        [DataMember] public float y;
        [DataMember] public float z;
        [DataMember] public float qx;
        [DataMember] public float qy;
        [DataMember] public float qz;
        [DataMember] public float qw;

        public static RootRelativePose Capture(Transform root, Vector3 worldPosition, Quaternion worldRotation)
        {
            if (root == null) return null;
            Vector3 position = root.InverseTransformPoint(worldPosition);
            Quaternion rotation = Quaternion.Inverse(root.rotation) * worldRotation;
            return new RootRelativePose { x = position.x, y = position.y, z = position.z,
                qx = rotation.x, qy = rotation.y, qz = rotation.z, qw = rotation.w };
        }

        public Vector3 LocalPosition => new Vector3(x, y, z);
        public Quaternion LocalRotation => new Quaternion(qx, qy, qz, qw);
        public Vector3 WorldPosition(Transform root) => root.TransformPoint(LocalPosition);
        public Quaternion WorldRotation(Transform root) => root.rotation * LocalRotation;
    }
}
