using System;
using System.Collections.Generic;
using UnityEngine;

namespace MainGameMagicPaint
{
    internal sealed class PaintActor : IDisposable
    {
        internal readonly ChaControl Actor;
        internal readonly PaintHistory History = new PaintHistory();
        internal readonly List<MeshProbe> Probes = new List<MeshProbe>();
        internal SkinSurface Face, Body;
        internal PaintBuffer FaceBuffer, BodyBuffer;
        internal PaintActor(ChaControl actor) { Actor = actor; Refresh(); }
        internal void Refresh()
        {
            if (Actor == null) return;
            if (Face != null && Face.Probe.Renderer != Actor.rendFace) { Face.Dispose(); Face = null; }
            if (Body != null && Body.Probe.Renderer != Actor.rendBody) { Body.Dispose(); Body = null; }
            var next = new List<MeshProbe>();
            // Only native face/body receive paint. No other character mesh participates at all.
            foreach (var renderer in new[] { Actor.rendFace, Actor.rendBody })
                if (renderer != null && !next.Exists(p => p.Renderer == renderer)) next.Add(Probes.Find(p => p.Renderer == renderer) ?? new MeshProbe(renderer));
            foreach (var probe in Probes) if (!next.Contains(probe)) probe.Dispose();
            Probes.Clear(); Probes.AddRange(next); foreach(var probe in Probes) probe.Owner = this;
            Face?.Rebind(); Body?.Rebind();
            foreach (var probe in Probes)
            {
                if (Face == null && FaceBuffer != null && probe.Renderer == Actor.rendFace) Surface(probe);
                if (Body == null && BodyBuffer != null && probe.Renderer == Actor.rendBody) Surface(probe);
            }
        }
        internal bool IsSkin(MeshProbe probe) => Actor != null && (probe.Renderer == Actor.rendBody || probe.Renderer == Actor.rendFace);
        internal SkinSurface Surface(MeshProbe probe)
        {
            if (!IsSkin(probe)) return null;
            bool face = probe.Renderer == Actor.rendFace;
            if (face) return Face ?? (Face = new SkinSurface(probe, true, FaceBuffer ?? (FaceBuffer = new PaintBuffer(1024))));
            return Body ?? (Body = new SkinSurface(probe, false, BodyBuffer ?? (BodyBuffer = new PaintBuffer(2048))));
        }
        internal IEnumerable<PaintBuffer> Buffers()
        { if (FaceBuffer != null) yield return FaceBuffer; if (BodyBuffer != null) yield return BodyBuffer; }
        internal void Commit() => History.Commit(Buffers());
        internal void Flush() { Face?.Flush(); Body?.Flush(); }
        internal void Clear() { foreach (var buffer in Buffers()) buffer.Clear(); Commit(); Flush(); }
        public void Dispose()
        {
            Face?.Dispose(); Body?.Dispose(); Face = Body = null;
            foreach (var probe in Probes) probe.Dispose(); Probes.Clear(); History.Clear(); FaceBuffer = BodyBuffer = null;
        }
    }
}
