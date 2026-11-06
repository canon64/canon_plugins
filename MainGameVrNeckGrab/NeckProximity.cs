using System;

namespace MainGameVrNeckGrab
{
    internal static class NeckProximity
    {
        // Distances are sampled only on a trigger-down edge. Collider callbacks are not required.
        internal static int Closest(double[] squaredDistances, double radiusMetres)
        {
            if (squaredDistances == null || radiusMetres <= 0 || double.IsNaN(radiusMetres) || double.IsInfinity(radiusMetres)) return -1;
            double best = radiusMetres * radiusMetres;
            int index = -1;
            for (int i = 0; i < squaredDistances.Length; ++i)
            {
                double distance = squaredDistances[i];
                if (double.IsNaN(distance) || double.IsInfinity(distance) || distance < 0 || distance > best) continue;
                if (index >= 0 && distance == best) continue;
                best = distance; index = i;
            }
            return index;
        }
    }
    internal sealed class NeckPressRouter
    {
        readonly int[] _actors = { -1, -1 };
        readonly double[] _stamps = { double.NaN, double.NaN };
        internal int Actor(int hand) => hand >= 0 && hand < 2 ? _actors[hand] : -1;
        internal bool Captured(int hand, double stamp) => Actor(hand) >= 0 && _stamps[hand] == stamp;
        internal bool Capture(int hand, int actor, Action start, double stamp = 0)
        {
            if (hand < 0 || hand >= 2) return false;
            if (Captured(hand, stamp)) return true;
            _actors[hand] = -1; _stamps[hand] = stamp;
            if (actor < 0) return false;
            // Claim BEFORE attempting the hold. Even a rejected/failed neck operation must not
            // fall through to a breast/kiss click, and duplicate event delivery must not restart it.
            _actors[hand] = actor;
            start(); return true;
        }
        internal void Release(int hand) { if (hand >= 0 && hand < 2) { _actors[hand] = -1; _stamps[hand] = double.NaN; } }
        internal void Clear() { Release(0); Release(1); }
    }
}
