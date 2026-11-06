using System;
using System.Collections.Generic;
using V = System.Numerics.Vector3;
using U = System.Numerics.Vector2;
namespace MainGameMagicPaint
{
    internal sealed class BrushStats
    {
        internal long Stamps,Candidates,FacingRejected,TooFar,Degenerate,UvInvalid,SelectedPixels,Writes;
        public override string ToString()=>"stamps="+Stamps+" candidates="+Candidates+" facingRejected="+FacingRejected+" tooFar="+TooFar+" degenerate="+Degenerate+" uvInvalid="+UvInvalid+" selectedPixels="+SelectedPixels+" writes="+Writes;
    }
    internal static class BrushRaster
    {
        static int Clamp(int v, int n) => Math.Max(0, Math.Min(n - 1, v));
        static float Smooth(float x) { x = Math.Max(0, Math.Min(1, x)); return x * x * (3 - 2 * x); }
        internal static void Stamp(PaintBuffer buffer, V[] vertices, U[] uv, int[] triangles, V point, V normal, float radius, uint color, float strength, bool erase, float feather, IList<int> candidates = null, BrushStats stats = null)
        {
            if(stats!=null)stats.Stamps++;
            if (radius <= 0 || strength <= 0 || uv.Length != vertices.Length) { if(stats!=null&&uv.Length!=vertices.Length)stats.UvInvalid++;return; }
            long before=stats==null?0:buffer.Revision;
            int size = buffer.Size; float r2 = radius * radius;
            for (int index = 0; index < (candidates == null ? triangles.Length / 3 : candidates.Count); index++)
            {
                int i = candidates == null ? index * 3 : candidates[index]; if(stats!=null)stats.Candidates++;
                int ia = triangles[i], ib = triangles[i+1], ic = triangles[i+2]; V a = vertices[ia], b = vertices[ib], c = vertices[ic];
                V e1 = b - a, e2 = c - a, face = V.Cross(e1, e2); float faceLength = face.Length();
                if(faceLength<1e-10f){if(stats!=null)stats.Degenerate++;continue;}
                if(V.Dot(face/faceLength,normal)<.25f){if(stats!=null)stats.FacingRejected++;continue;}
                if (V.DistanceSquared(SurfaceMath.Closest(point, a, b, c), point) > r2) {if(stats!=null)stats.TooFar++;continue;}
                U u = uv[ia], w = uv[ib] - u, h = uv[ic] - u; float det = w.X * h.Y - w.Y * h.X;
                if (Math.Abs(det) < 1e-12f) {if(stats!=null)stats.UvInvalid++;continue;}
                float g11 = V.Dot(e1,e1), g12 = V.Dot(e1,e2), g22 = V.Dot(e2,e2), gd = g11*g22-g12*g12;
                if (gd < 1e-16f) {if(stats!=null)stats.Degenerate++;continue;}
                V du = (e1*g22-e2*g12)/gd, dv = (e2*g11-e1*g12)/gd;
                V ux = du*w.X+dv*h.X, uy = du*w.Y+dv*h.Y;
                U center = u + w*V.Dot(du,point-a) + h*V.Dot(dv,point-a);
                float rx=radius*ux.Length(), ry=radius*uy.Length();
                int x0=Clamp((int)Math.Floor(Math.Max(Math.Min(u.X,Math.Min(u.X+w.X,u.X+h.X)),center.X-rx)*size),size);
                int x1=Clamp((int)Math.Ceiling(Math.Min(Math.Max(u.X,Math.Max(u.X+w.X,u.X+h.X)),center.X+rx)*size),size);
                int y0=Clamp((int)Math.Floor(Math.Max(Math.Min(u.Y,Math.Min(u.Y+w.Y,u.Y+h.Y)),center.Y-ry)*size),size);
                int y1=Clamp((int)Math.Ceiling(Math.Min(Math.Max(u.Y,Math.Max(u.Y+w.Y,u.Y+h.Y)),center.Y+ry)*size),size);
                bool covered = false;
                for (int y=y0;y<=y1;y++) for(int x=x0;x<=x1;x++)
                {
                    float px=(x+.5f)/size-u.X, py=(y+.5f)/size-u.Y;
                    float s=(px*h.Y-py*h.X)/det, t=(w.X*py-w.Y*px)/det;
                    if(s<-.00001f||t<-.00001f||s+t>1.00001f)continue;
                    float distance=V.Distance(a+e1*s+e2*t,point)/radius; if(distance>1)continue;
                    float edge=feather<=0?1:1-Smooth((distance-(1-feather))/feather);
                    covered = true; if(stats!=null)stats.SelectedPixels++;
                    buffer.Set(x,y,PaintBuffer.Blend(buffer.Pixels[y*size+x],color,strength*edge,erase));
                }
                // A sub-texel spray grain must still contribute ink. Preserve area as alpha.
                if (!covered)
                {
                    float s0=V.Dot(du,point-a),t0=V.Dot(dv,point-a);
                    if(s0<0||t0<0||s0+t0>1)continue;
                    float pixelArea=V.Cross(e1*h.Y-e2*w.Y,e2*w.X-e1*h.X).Length()/(det*det*size*size);
                    float area=(float)Math.PI*r2;
                    if(pixelArea<=0||area>=pixelArea)continue;
                    int x=Clamp((int)Math.Floor(center.X*size),size),y=Clamp((int)Math.Floor(center.Y*size),size);
                    buffer.Set(x,y,PaintBuffer.Blend(buffer.Pixels[y*size+x],color,strength*Math.Min(1,area/pixelArea),erase));
                }
            }
            if(stats!=null)stats.Writes+=buffer.Revision-before;
        }
    }
}
