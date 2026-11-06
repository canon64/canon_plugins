using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    public sealed class SpecialMovementKey
    {
        public float time;
        public Vector3 position;
    }
    public static class SpecialMovementTimeline
    {
        // Adjacent segments share endpoints. Unity curves must not receive equal-time keys with NaN tangents.
        public static SpecialMovementKey[] Build(Vector3 start, SpecialMovementSegment[] segments, float seconds)
        {
            if (segments == null || segments.Length == default(int) || seconds <= default(float) || float.IsNaN(seconds) || float.IsInfinity(seconds))
                throw new InvalidDataException("特殊移動の時間・区間が不正です。");
            var keys = new List<SpecialMovementKey>();
            Action<float, Vector3> add = (time, position) =>
            {
                if (float.IsNaN(time) || float.IsInfinity(time) || float.IsNaN(position.x) || float.IsInfinity(position.x)
                    || float.IsNaN(position.y) || float.IsInfinity(position.y) || float.IsNaN(position.z) || float.IsInfinity(position.z))
                    throw new InvalidDataException("特殊移動の座標・時刻が有限値ではありません。");
                if (keys.Count > default(int))
                {
                    var previous = keys[keys.Count - Math.Sign(int.MaxValue)];
                    if (time < previous.time || (time == previous.time && !position.Equals(previous.position)))
                        throw new InvalidDataException("特殊移動の時刻が逆転、または同時刻の位置が競合しています。");
                    if (time == previous.time) return;
                }
                keys.Add(new SpecialMovementKey { time = time, position = position });
            };
            add(default(float), start); var last = start;
            foreach (var segment in segments)
            {
                if (segment.start < default(float) || segment.end <= segment.start || segment.end > Math.Sign(int.MaxValue))
                    throw new InvalidDataException("特殊移動の区間が不正です。");
                add(segment.start * seconds, last); add(segment.end * seconds, segment.position); last = segment.position;
            }
            add(seconds, last); return keys.ToArray();
        }
    }
}
