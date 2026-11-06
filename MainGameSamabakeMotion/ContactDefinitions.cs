using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace MainGameSamabakeMotion
{
    // Fields are populated by DataContractJsonSerializer.
#pragma warning disable CS0649
    [DataContract] internal sealed class HeightCalibration
    {
        [DataMember] public string bone;
        [DataMember] public float nativeRest;
        [DataMember] public float[] sourceScales;
        internal float Map(float scale)
        {
            if (sourceScales == null || sourceScales.Length < 2 || nativeRest <= 0) throw new InvalidDataException("身長の実寸対応がありません");
            float value = scale / nativeRest;
            if (value <= sourceScales[0]) return 0;
            for (int i = 1; i < sourceScales.Length; i++)
                if (value <= sourceScales[i]) return (i - 1 + (value - sourceScales[i - 1]) / (sourceScales[i] - sourceScales[i - 1])) / (sourceScales.Length - 1);
            return 1;
        }
    }
    [DataContract] internal sealed class ContactDefinition
    {
        [DataMember] public HeightCalibration height;
        [DataMember] public string[] partners;
        [DataMember] public ContactState[] states;
        [DataMember] public string source;
        [DataMember] public string[] issues;
        [DataMember] public float translationScale = 1;
        internal void Validate(PlaybackPose pose)
        {
            Func<float, bool> finite = v => !float.IsNaN(v) && !float.IsInfinity(v);
            var slots = new HashSet<string>(pose.actors.Select(a => a.slot));
            if (height == null || height.bone != "cf_n_height" || !finite(height.nativeRest) || height.nativeRest <= 0
                || height.sourceScales == null || height.sourceScales.Length < 2 || height.sourceScales.Any(v => !finite(v) || v <= 0)
                || height.sourceScales.Skip(1).Where((v, i) => v <= height.sourceScales[i]).Any()
                || partners == null || !slots.SetEquals(partners) || partners.Distinct().Count() != partners.Length
                || states == null || !new HashSet<string>(pose.states).SetEquals(states.Select(s => s.name)) || states.Length != pose.states.Length
                || !finite(translationScale) || translationScale <= 0) throw new InvalidDataException("体型・接触補正の定義が不正です");
            foreach (var state in states)
            {
                if (state.parts == null || state.parts.Length != 4 || state.frames == null) throw new InvalidDataException("接触補正の部位が不正です");
                foreach (var part in state.parts)
                    if (part == null || !slots.Contains(part.slot) || new[] { part.position, part.rotation, part.bendWeight }.Any(v => !finite(v) || v < 0 || v > 1)) throw new InvalidDataException("IKの対象または重みが不正です");
                foreach (var frame in state.frames)
                {
                    if (!slots.Contains(frame.slot) || frame.index < 0 || frame.shapes == null || !finite(frame.scale) || frame.scale <= 0 || (frame.index >= 8 && string.IsNullOrEmpty(frame.bone))) throw new InvalidDataException("身体補正の対象が不正です");
                    foreach (var shape in frame.shapes)
                    {
                        if (shape.index < 0 || shape.index > 43 || shape.indices == null || shape.weights == null || shape.indices.Length == 0 || shape.indices.Length != shape.weights.Length
                            || shape.indices.Any(i => i < 0 || i > 43) || shape.weights.Any(w => !finite(w) || w <= 0) || Math.Abs(shape.weights.Sum() - 1) > 1e-4
                            || new[] {shape.smallPos,shape.smallAng,shape.mediumPos,shape.mediumAng,shape.largePos,shape.largeAng}.Any(v => v == null || v.Length != 3 || v.Any(n => !finite(n)))) throw new InvalidDataException("体型補正値が不正です");
                    }
                }
            }
        }
    }
    [DataContract] internal sealed class ContactState
    {
        [DataMember] public string name;
        [DataMember] public ContactPart[] parts;
        [DataMember] public ContactFrame[] frames;
    }
    [DataContract] internal sealed class ContactPart
    {
        [DataMember] public string slot, bone, bend;
        [DataMember] public float position, rotation, bendWeight;
    }
    [DataContract] internal sealed class ContactFrame
    {
        [DataMember] public int index;
        [DataMember] public string bone, slot;
        [DataMember] public ContactShape[] shapes;
        [DataMember] public float scale = 1;
    }
    [DataContract] internal sealed class ContactShape
    {
        [DataMember] public int index, sourceIndex;
        [DataMember] public int[] indices;
        [DataMember] public float[] weights;
        [DataMember] public float[] smallPos, smallAng, mediumPos, mediumAng, largePos, largeAng;
        internal static float Lerp(float small, float medium, float large, float value) => value < .5f
            ? small + (medium - small) * Math.Max(0, value * 2) : medium + (large - medium) * Math.Min(1, (value - .5f) * 2);
        internal static float Angle(float small, float medium, float large, float value)
        {
            Func<float, float> signed = a => (a % 360 + 540) % 360 - 180;
            return value < .5f ? small + signed(medium - small) * Math.Max(0, value * 2)
                : medium + signed(large - medium) * Math.Min(1, (value - .5f) * 2);
        }
    }
    internal interface IContactRig
    {
        void Configure(IDictionary<string, IContactRig> rigs);
        void Apply(string state);
        void Restore();
        float Height { get; }
    }
#pragma warning restore CS0649
}
