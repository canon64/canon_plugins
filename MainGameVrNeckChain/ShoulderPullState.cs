using System;

namespace MainGameVrNeckChain
{
    // Distances in metres; independent of Unity so the tension boundaries can be tested.
    internal sealed class ShoulderPullState
    {
        internal bool Active { get; private set; }
        internal bool WaitingForSlack { get; private set; }
        internal int Session { get; private set; }
        internal float RestLength { get; private set; }

        internal void SampleDelta(float x, float y, float z, float length, float maximum,
            out float offsetX, out float offsetY, out float offsetZ)
        {
            float distance = (float)Math.Sqrt((double)x * x + (double)y * y + (double)z * z);
            float amount = Sample(distance, length, maximum);
            float scale = distance > 0.00001f && !float.IsInfinity(distance) ? amount / distance : 0f;
            offsetX = scale > 0f ? x * scale : 0f;
            offsetY = scale > 0f ? y * scale : 0f;
            offsetZ = scale > 0f ? z * scale : 0f;
        }

        internal float Sample(float distance, float length, float maximum)
        {
            if (float.IsNaN(distance) || float.IsInfinity(distance) ||
                float.IsNaN(length) || float.IsInfinity(length) || length <= 0f ||
                float.IsNaN(maximum) || float.IsInfinity(maximum) || maximum <= 0f)
            {
                Reset();
                return 0f;
            }
            if (WaitingForSlack)
            {
                if (distance < RestLength - 0.005f) WaitingForSlack = false;
                return 0f;
            }
            if (!Active)
            {
                if (distance < length) return 0f;
                RestLength = length;
                Active = true;
                unchecked { Session++; }
            }
            if (distance < RestLength - 0.005f)
            {
                Reset();
                return 0f;
            }
            return Math.Min(Math.Max(0f, distance - RestLength), Math.Min(maximum, 0.5f));
        }

        internal void CancelUntilSlack()
        {
            if (!Active) return;
            Active = false;
            WaitingForSlack = true;
        }

        internal void Reset()
        {
            Active = false;
            WaitingForSlack = false;
        }
    }
}
