using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace MainGameRoundedLimbClothes
{
    // Pure geometry: no Unity calls, pose baking, frame callbacks, or shared asset mutations.
    public sealed class SkinVertex
    {
        public Vector3 Position, Normal;
        public Vector4 Tangent;
        public Vector4 Color = Vector4.One;
        public Vector4[] UV = new Vector4[8];
        public Dictionary<int, float> Bones = new Dictionary<int, float>();
        public Dictionary<int, float> Sources = new Dictionary<int, float>();
        public SkinVertex Copy()
        {
            var v = (SkinVertex)MemberwiseClone();
            v.UV = (Vector4[])UV.Clone();
            v.Bones = new Dictionary<int, float>(Bones);
            v.Sources = new Dictionary<int, float>(Sources);
            return v;
        }
        public static SkinVertex Mix(SkinVertex a, SkinVertex b, float t)
        {
            var v = new SkinVertex { Position = Vector3.Lerp(a.Position, b.Position, t),
                Normal = SafeNormal(Vector3.Lerp(a.Normal, b.Normal, t)),
                Tangent = Vector4.Lerp(a.Tangent, b.Tangent, t), Color = Vector4.Lerp(a.Color, b.Color, t) };
            for (int i = 0; i < 8; ++i) v.UV[i] = Vector4.Lerp(a.UV[i], b.UV[i], t);
            Add(v.Bones, a.Bones, 1-t); Add(v.Bones, b.Bones, t);
            v.Bones = v.Bones.OrderByDescending(x => x.Value).ThenBy(x => x.Key).Take(4).ToDictionary(x => x.Key, x => x.Value);
            float sum = v.Bones.Values.Sum();
            foreach (int key in v.Bones.Keys.ToArray()) v.Bones[key] /= sum;
            Add(v.Sources, a.Sources, 1-t); Add(v.Sources, b.Sources, t);
            return v;
        }
        static void Add(Dictionary<int,float> to, Dictionary<int,float> from, float scale)
        {
            foreach (var x in from) { if (!to.ContainsKey(x.Key)) to[x.Key] = 0; to[x.Key] += x.Value*scale; }
        }
        internal static Vector3 SafeNormal(Vector3 v) => v.LengthSquared() > 1e-18f ? Vector3.Normalize(v) : Vector3.UnitZ;
    }

    public sealed class SkinMesh
    {
        public readonly List<SkinVertex> Vertices = new List<SkinVertex>();
        public readonly List<List<int>> Submeshes = new List<List<int>>();
        public SkinMesh Copy()
        {
            var m = new SkinMesh(); m.Vertices.AddRange(Vertices); // vertices are immutable during clipping
            foreach (var sub in Submeshes) m.Submeshes.Add(new List<int>(sub));
            return m;
        }
    }

    public sealed class LimbDefinition
    {
        public string Name;
        public Vector3 Root, End;
        public readonly HashSet<int> Bones = new HashSet<int>();
        public Vector3 Axis => Vector3.Normalize(End-Root);
        public float Length => (End-Root).Length();
        public float Minimum, Maximum;
        public float Membership(SkinVertex v) => v.Bones.Where(x => Bones.Contains(x.Key)).Sum(x => x.Value);
    }

    public static class RoundedGeometry
    {
        const float Epsilon = 0.000001f;
        public const int BevelRings = 8;
        sealed class Segment { public int A, B; }
        public static string PositionKey(Vector3 p) => p.X.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+":"+p.Y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+":"+p.Z.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
        static string Key(Vector3 p) => PositionKey(p);
        public static void WeldPositions(SkinMesh mesh)
        {
            // UV-seam duplicates in the stock asset differ by float rounding. Keep attributes separate.
            var buckets=new Dictionary<string,List<Vector3>>();
            foreach(var v in mesh.Vertices)
            {
                int x=(int)Math.Floor(v.Position.X/1e-5f),y=(int)Math.Floor(v.Position.Y/1e-5f),z=(int)Math.Floor(v.Position.Z/1e-5f);
                bool found=false;Vector3 canonical=v.Position;
                for(int a=-1;a<=1&&!found;a++)for(int b=-1;b<=1&&!found;b++)for(int c=-1;c<=1&&!found;c++)
                    if(buckets.TryGetValue((x+a)+":"+(y+b)+":"+(z+c),out var list))foreach(var p in list)
                        if(Vector3.DistanceSquared(p,v.Position)<1e-12f){canonical=p;found=true;break;}
                v.Position=canonical;
                string key=x+":"+y+":"+z;
                if(!buckets.ContainsKey(key))buckets[key]=new List<Vector3>();
                if(!found)buckets[key].Add(canonical);
            }
        }

        // Find a contiguous tubular range in the actual source. Never assume shoulder/groin topology.
        public static void FindRange(SkinMesh source, LimbDefinition limb)
        {
            int bestStart = -1, bestEnd = -1, start = -1;
            for (int i = 0; i <= 32; ++i)
            {
                float t = .1f + i*.025f;
                bool valid;
                try { Clip(source, limb, limb.Length*t, source.Submeshes.Count, false); valid = true; }
                catch (InvalidOperationException) { valid = false; }
                if (valid && start < 0) start = i;
                if (start >= 0 && (!valid || i == 32))
                {
                    int end = valid ? i : i-1;
                    if (end-start > bestEnd-bestStart) { bestStart=start; bestEnd=end; }
                    start=-1;
                }
            }
            if (bestStart < 0 || bestEnd-bestStart < 4) throw new InvalidOperationException(limb.Name+": 丸く閉じられる区間がありません。");
            // Leave one scan step at either end to keep bevels clear of branch transitions.
            limb.Minimum = limb.Length*(.1f+(bestStart+1)*.025f);
            limb.Maximum = limb.Length*(.1f+(bestEnd-1)*.025f);
        }

        public static SkinMesh Apply(SkinMesh source, LimbDefinition limb, float percent, int capSubmesh)
        {
            if (float.IsNaN(percent) || float.IsInfinity(percent) || percent < 0 || percent > 100)
                throw new ArgumentOutOfRangeException(nameof(percent));
            return Clip(source, limb, limb.Minimum+(limb.Maximum-limb.Minimum)*percent/100, capSubmesh, true);
        }

        internal static SkinMesh Clip(SkinMesh source, LimbDefinition limb, float distance, int capSubmesh, bool cap)
        {
            var output = source.Copy();
            foreach (var sub in output.Submeshes) sub.Clear();
            while (output.Submeshes.Count <= capSubmesh) output.Submeshes.Add(new List<int>());
            var axis=limb.Axis;
            var signed=source.Vertices.Select(v => Vector3.Dot(v.Position-limb.Root, axis)-distance).ToArray();
            var member=source.Vertices.Select(limb.Membership).ToArray();
            var cache=new Dictionary<long,int>();var geometricIntersections=new Dictionary<string,Vector3>();
            var segments=new List<Segment>();
            Func<int,int,int> intersect=(a,b) =>
            {
                if (Math.Abs(signed[a]) <= Epsilon) return a;
                if (Math.Abs(signed[b]) <= Epsilon) return b;
                long key=((long)Math.Min(a,b)<<32)|(uint)Math.Max(a,b);
                if (cache.TryGetValue(key,out int found)) return found;
                float t=signed[a]/(signed[a]-signed[b]);
                int index=output.Vertices.Count;
                var vertex=SkinVertex.Mix(source.Vertices[a],source.Vertices[b],t);
                string ka=Key(source.Vertices[a].Position),kb=Key(source.Vertices[b].Position);
                string edge=string.CompareOrdinal(ka,kb)<0?ka+"|"+kb:kb+"|"+ka;
                if(geometricIntersections.TryGetValue(edge,out var position))vertex.Position=position;
                else geometricIntersections[edge]=vertex.Position;
                output.Vertices.Add(vertex); cache[key]=index;
                return index;
            };
            for(int sub=0;sub<source.Submeshes.Count;++sub)
            {
                var triangles=source.Submeshes[sub]; var kept=output.Submeshes[sub];
                for(int i=0;i<triangles.Count;i+=3)
                {
                    var tri=new[]{triangles[i],triangles[i+1],triangles[i+2]};
                    if(tri.Max(v=>member[v]) < .3f) { kept.AddRange(tri); continue; }
                    var polygon=new List<int>(4);
                    for(int e=0;e<3;++e)
                    {
                        int a=tri[e],b=tri[(e+1)%3]; bool insideA=signed[a]<=Epsilon,insideB=signed[b]<=Epsilon;
                        if(insideA) polygon.Add(a);
                        if(insideA!=insideB) polygon.Add(intersect(a,b));
                    }
                    polygon=polygon.Distinct().ToList();
                    for(int j=1;j+1<polygon.Count;++j) AddTriangle(output,kept,polygon[0],polygon[j],polygon[j+1]);
                    if(tri.Any(v=>signed[v]>Epsilon) && polygon.Count>=2)
                    {
                        var boundary=polygon.Where(v=>v>=signed.Length || Math.Abs(signed[v])<=Epsilon).ToArray();
                        if(boundary.Length==2 && Key(output.Vertices[boundary[0]].Position)!=Key(output.Vertices[boundary[1]].Position))
                            segments.Add(new Segment{A=boundary[0],B=boundary[1]});
                    }
                }
            }
            // UV splits have distinct indices but form one geometric boundary. Weld only for traversal.
            var reps=new Dictionary<string,int>(); var edges=new Dictionary<string,HashSet<string>>();
            foreach(var s in segments)
            {
                string a=Key(output.Vertices[s.A].Position),b=Key(output.Vertices[s.B].Position);
                if(!reps.ContainsKey(a)) reps[a]=s.A; if(!reps.ContainsKey(b)) reps[b]=s.B;
                if(!edges.ContainsKey(a)) edges[a]=new HashSet<string>(); if(!edges.ContainsKey(b)) edges[b]=new HashSet<string>();
                edges[a].Add(b);edges[b].Add(a);
            }
            if(edges.Count<8 || edges.Values.Any(x=>x.Count!=2)) throw new InvalidOperationException(limb.Name+": 切り口が閉じた輪郭になりません。 distance="+distance+" nodes="+edges.Count+" degrees="+string.Join(",",edges.Where(x=>x.Value.Count!=2).Select(x=>x.Key+"="+x.Value.Count).Take(8)));
            var loop=new List<int>(); string first=edges.Keys.First(),current=first,previous=null;
            do
            {
                loop.Add(reps[current]); string next=edges[current].First(x=>x!=previous);
                previous=current;current=next;
                if(loop.Count>edges.Count) throw new InvalidOperationException("輪郭の接続が不正です。");
            }while(current!=first);
            if(loop.Count!=edges.Count) throw new InvalidOperationException(limb.Name+": 複数の切り口があります。");
            if(!cap) return output;
            AddCap(output,loop,limb,capSubmesh);
            return output;
        }

        static void AddTriangle(SkinMesh mesh,List<int> triangles,int a,int b,int c)
        {
            if(Vector3.Cross(mesh.Vertices[b].Position-mesh.Vertices[a].Position,mesh.Vertices[c].Position-mesh.Vertices[a].Position).LengthSquared()<1e-28f) return;
            triangles.Add(a);triangles.Add(b);triangles.Add(c);
        }
        static void Face(SkinMesh mesh,List<int> triangles,int a,int b,int c,Vector3 outward)
        {
            if(Vector3.Dot(Vector3.Cross(mesh.Vertices[b].Position-mesh.Vertices[a].Position,mesh.Vertices[c].Position-mesh.Vertices[a].Position),outward)<0)
            {int swap=b;b=c;c=swap;}
            AddTriangle(mesh,triangles,a,b,c);
        }
        static void AddCap(SkinMesh mesh,List<int> loop,LimbDefinition limb,int sub)
        {
            var axis=limb.Axis; var center=Vector3.Zero;
            foreach(int id in loop) center+=mesh.Vertices[id].Position;
            center/=loop.Count;
            float radius=loop.Average(id=>(mesh.Vertices[id].Position-center).Length()),depth=radius*.15f;
            if(radius<Epsilon) throw new InvalidOperationException("切り口が小さすぎます。");
            // Reject a folded/non star-shaped loop instead of manufacturing overlapping cap triangles.
            float winding=0;
            for(int i=0;i<loop.Count;++i)
            {
                float d=Vector3.Dot(Vector3.Cross(mesh.Vertices[loop[i]].Position-center,mesh.Vertices[loop[(i+1)%loop.Count]].Position-center),axis);
                if(Math.Abs(d)<1e-12f) continue;
                if(winding!=0 && d*winding<0) throw new InvalidOperationException("切り口が内側へ折れています。");
                winding=d;
            }
            var centerVertex=mesh.Vertices[loop[0]].Copy();
            for(int i=1;i<loop.Count;++i) centerVertex=SkinVertex.Mix(centerVertex,mesh.Vertices[loop[i]],1f/(i+1));
            centerVertex.Position=center+axis*depth;centerVertex.Normal=axis;
            // Duplicate the boundary in the cap submesh; retain skin normals at the outside edge.
            var outer=loop.ToArray();
            for(int ring=1;ring<=BevelRings;++ring)
            {
                float theta=(float)Math.PI*.5f*ring/BevelRings;
                float cosine=(float)Math.Cos(theta),sine=(float)Math.Sin(theta);
                var inner=new int[loop.Count];
                for(int i=0;i<loop.Count;++i)
                {
                    var source=mesh.Vertices[loop[i]];
                    var v=source.Copy();
                    var radial=source.Position-center;
                    v.Position=center+radial*(.85f+.15f*cosine)+axis*depth*sine;
                    v.Normal=SkinVertex.SafeNormal(source.Normal*cosine+axis*sine);
                    var tangent=new Vector3(v.Tangent.X,v.Tangent.Y,v.Tangent.Z);
                    tangent=SkinVertex.SafeNormal(tangent-v.Normal*Vector3.Dot(tangent,v.Normal));
                    v.Tangent=new Vector4(tangent,v.Tangent.W>=0?1:-1);
                    inner[i]=mesh.Vertices.Count;mesh.Vertices.Add(v);
                }
                for(int i=0;i<loop.Count;++i)
                {
                    int next=(i+1)%loop.Count;
                    var outward=mesh.Vertices[inner[i]].Normal+mesh.Vertices[outer[i]].Normal;
                    Face(mesh,mesh.Submeshes[sub],outer[i],outer[next],inner[next],outward);
                    Face(mesh,mesh.Submeshes[sub],outer[i],inner[next],inner[i],outward);
                }
                outer=inner;
            }
            int pole=mesh.Vertices.Count;mesh.Vertices.Add(centerVertex);
            for(int i=0;i<loop.Count;++i) Face(mesh,mesh.Submeshes[sub],outer[i],outer[(i+1)%loop.Count],pole,axis);
        }
    }
}
