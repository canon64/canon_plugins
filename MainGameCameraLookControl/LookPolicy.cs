using System;

namespace MainGameCameraLookControl
{
    internal sealed class BonePolicy
    {
        internal float Up = 0f, Down = 0f, Left = 0f, Right = 0f, Roll = 0f, RollFromYaw = 0f;
        internal static readonly BonePolicy Neutral = new BonePolicy();
    }

    internal sealed class LookPolicy
    {
        internal BonePolicy Neck = new BonePolicy(), Head = new BonePolicy();
        internal float PitchGain = 1f, YawGain = 1f, UpBias, RightBias;
        internal float UpField = 30f, DownField = 0f, LeftField = 0f, RightField = 0f, Speed = 1f;

        internal BonePolicy Bone(int index, int count)
        {
            if (index == count - 1) return Head;
            return index == 0 ? Neck : BonePolicy.Neutral;
        }

        internal static float Clamp(float value, float min, float max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return min;
            return Math.Max(min, Math.Min(max, value));
        }

        internal static float Sanitize(float value, float fallback, float min, float max)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Clamp(value, min, max);
        }

        internal static float Upper(float native, float extra) => extra == 0f ? native : Clamp(native - extra, -100f, 0f);
        internal static float Lower(float native, float extra) => extra == 0f ? native : Clamp(native + extra, 0f, 100f);
        internal float Pitch(float native) => Clamp(native * PitchGain - UpBias, -180f, 180f);
        internal float Yaw(float native) => Clamp(native * YawGain + RightBias, -180f, 180f);
        internal float VerticalField(float native, float pitch)
        {
            float extra = pitch <= 0 ? UpField : DownField;
            return extra == 0f ? native : Clamp(native + extra, 0f, 180f);
        }
        internal float HorizontalField(float native, float yaw)
        {
            float extra = yaw <= 0 ? LeftField : RightField;
            return extra == 0f ? native : Clamp(native + extra, 0f, 180f);
        }
        internal static float Roll(BonePolicy bone, float yaw) => Clamp(bone.Roll + yaw * bone.RollFromYaw, -60f, 60f);
    }
}
