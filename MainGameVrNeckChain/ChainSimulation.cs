using System;
using UnityEngine;

namespace MainGameVrNeckChain
{
    // Pure geometry/Verlet step. No scene state or Unity native calls are needed.
    internal static class ChainSimulation
    {
        internal static void Step(Vector3[] points, Vector3[] previous, float[] lengths,
            Vector3 root, Vector3 tip, bool rootPinned, bool tipPinned, float dt,
            float gravity, Action<bool, bool> collide = null, bool preservePinnedTip = false)
        {
            int last = points.Length - 1;
            float total = 0f;
            for (int i = 0; i < lengths.Length; i++) total += lengths[i];
            Vector3 span = tip - root;
            float distance = span.magnitude;
            if (rootPinned && tipPinned && distance >= total - 0.005f)
            {
                Vector3 direction = distance > 0.00001f ? span / distance : Vector3.down;
                float along = 0f;
                for (int i = 0; i <= last; i++)
                {
                    if (i > 0) along += lengths[i - 1];
                    // A released world anchor must not be reeled toward a moving collar.
                    points[i] = previous[i] = preservePinnedTip && total > 0f
                        ? root + span * (along / total) : root + direction * along;
                }
                return;
            }

            // Carry the entire rope with its endpoints. Moving both endpoints by
            // the same amount translates every point equally; it cannot reel the
            // free length into the root or introduce artificial Verlet velocity.
            Vector3 rootDelta = rootPinned ? root - points[0] : Vector3.zero;
            Vector3 tipDelta = tipPinned ? tip - points[last] : Vector3.zero;
            float arc = 0f;
            for (int i = 0; i <= last; i++)
            {
                if (i > 0) arc += lengths[i - 1];
                float t = total > 0f ? arc / total : (float)i / last;
                Vector3 shift = rootDelta * (1f - t) + tipDelta * t;
                points[i] += shift;
                previous[i] += shift;
            }
            Vector3 acceleration = Vector3.down * (gravity * dt * dt);
            for (int i = 0; i <= last; i++)
            {
                if ((i == 0 && rootPinned) || (i == last && tipPinned)) continue;
                Vector3 current = points[i];
                points[i] += (points[i] - previous[i]) * 0.94f + acceleration;
                previous[i] = current;
            }

            // Sweep in both directions so one endpoint does not absorb all the
            // correction. The tolerance limits visible gaps between chain links.
            for (int pass = 0; pass < 64; pass++)
            {
                if (rootPinned) points[0] = root;
                if (tipPinned) points[last] = tip;
                for (int i = 0; i < last; i++) Project(points, lengths, i, rootPinned, tipPinned);
                for (int i = last - 1; i >= 0; i--) Project(points, lengths, i, rootPinned, tipPinned);
                collide?.Invoke(rootPinned, tipPinned);
                if (rootPinned) points[0] = root;
                if (tipPinned) points[last] = tip;
                float error = 0f;
                for (int i = 0; i < last; i++)
                    error = Math.Max(error, Math.Abs((points[i + 1] - points[i]).magnitude - lengths[i]));
                if (error < 0.0005f) break;
            }
            if (rootPinned) points[0] = previous[0] = root;
            if (tipPinned) points[last] = previous[last] = tip;
        }

        private static void Project(Vector3[] points, float[] lengths, int i, bool rootPinned, bool tipPinned)
        {
            Vector3 delta = points[i + 1] - points[i];
            float length = delta.magnitude;
            Vector3 correction = length > 0.000001f
                ? delta * ((lengths[i] - length) / length) : Vector3.down * lengths[i];
            bool leftFixed = i == 0 && rootPinned;
            bool rightFixed = i == lengths.Length - 1 && tipPinned;
            if (leftFixed && rightFixed) return;
            if (leftFixed) points[i + 1] += correction;
            else if (rightFixed) points[i] -= correction;
            else { points[i] -= correction * 0.5f; points[i + 1] += correction * 0.5f; }
        }
    }
}
