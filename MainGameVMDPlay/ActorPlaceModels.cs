using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace MainGameVMDPlay
{
    [DataContract]
    internal sealed class ActorPlace
    {
        [DataMember] public string Name;
        [DataMember] public float[] Position;
        [DataMember] public float[] Rotation;
        [DataMember(EmitDefaultValue = false)] public bool IsInitial;
    }

    [DataContract]
    internal sealed class ActorPlaces
    {
        [DataMember] public List<ActorPlace> Items = new List<ActorPlace>();
        [DataMember] public int Selected = -1;
    }

    internal static class ActorPlaceDefaults
    {
        internal static bool EnsureInitial(ActorPlaces places, float[] position, float[] rotation)
        {
            if (places.Items.Exists(p => p.IsInitial)) return false;
            if (!Valid(position) || !Valid(rotation)) throw new ArgumentException("初回呼び出し場所の座標が不正です。");
            int previous = places.Selected;
            places.Items.Insert(0, new ActorPlace { Name = "初回呼び出し場所", IsInitial = true,
                Position = (float[])position.Clone(), Rotation = (float[])rotation.Clone() });
            places.Selected = previous >= 0 && previous < places.Items.Count - 1 ? previous + 1 : 0;
            return true;
        }

        private static bool Valid(float[] values) => values != null && values.Length == 3
            && Array.TrueForAll(values, value => !float.IsNaN(value) && !float.IsInfinity(value));
    }
}
