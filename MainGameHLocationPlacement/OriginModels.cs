using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using MainGameHLocationCoordinates;
using UnityEngine;

namespace MainGameHLocationPlacement
{
    // Explicit numerical wire types: never ask Unity to serialize plugin-defined nested graphs.
    [DataContract] public sealed class PoseNumbers
    {
        [DataMember] public float[] position;
        [DataMember] public float[] rotation;
        public static PoseNumbers From(RigidPose pose) => new PoseNumbers {
            position = new[] { pose.position.x, pose.position.y, pose.position.z },
            rotation = new[] { pose.rotation.x, pose.rotation.y, pose.rotation.z, pose.rotation.w } };
        public RigidPose ToPose()
        {
            if (position == null || position.Length != 3 || rotation == null || rotation.Length != 4)
                throw new SerializationException("保存元の位置・向きが欠落しています。");
            return new RigidPose(new Vector3(position[0], position[1], position[2]), new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]));
        }
    }
    [DataContract] public sealed class ParentOrigin
    {
        [DataMember] public string objectId, kind, reference;
        [DataMember] public PoseNumbers world;
    }
    [DataContract] public sealed class PlaceOrigin
    {
        [DataMember] public string mapKey, mapName, placeId, placeName, poseKey, utc;
        [DataMember] public PoseNumbers femaleRoot, mapRoot;
        [DataMember] public List<ParentOrigin> parents = new List<ParentOrigin>();
        [DataMember] public List<string> composerLinks = new List<string>();
        public void Validate()
        {
            if (string.IsNullOrEmpty(mapKey) || string.IsNullOrEmpty(poseKey)) throw new SerializationException("保存元のマップ・体位がありません。");
            femaleRoot.ToPose();
            if (mapRoot != null) mapRoot.ToPose();
            foreach (var parent in parents) parent.world.ToPose();
        }
    }
    [DataContract] public sealed class PartOrigin
    {
        [DataMember] public string part, fingerprint;
        [DataMember] public string[] savedElements;
        [DataMember] public PlaceOrigin source;
    }
    [DataContract] public sealed class PresetOrigin
    {
        [DataMember] public int version = 1;
        [DataMember] public string owner, document, identity, name, poseKey, replacesIdentity;
        [DataMember] public List<PartOrigin> parts = new List<PartOrigin>();
    }
    internal sealed class SavedPart
    {
        internal string Name, Fingerprint;
        internal string[] Elements;
    }
    internal sealed class SavedPreset
    {
        internal string Owner, Identity, Name, PoseKey;
        internal List<SavedPart> Parts = new List<SavedPart>();
    }
}
