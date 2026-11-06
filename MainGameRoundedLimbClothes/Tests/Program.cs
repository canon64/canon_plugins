using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using MainGameRoundedLimbClothes;

static class Program
{
    static float[] Floats(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetSingle()).ToArray();
    static Vector3 V3(JsonElement e){var f=Floats(e);return new Vector3(f[0],f[1],f[2]);}
    static int assertions,cuts;
    static string previewFolder;
    static void Check(bool value,string message){assertions++;if(!value)throw new Exception(message);}
    static void Main(string[] args)
    {
        Console.WriteLine("START rounded limb geometry tests using actual KKS body assets");
        string folder=args.Length>0?args[0]:"Tests/Fixtures";
        if(args.Length>1){previewFolder=args[1];Directory.CreateDirectory(previewFolder);TestBody(Path.Combine(folder,"o_body_a_8387.json"));return;}
        foreach(string path in Directory.GetFiles(folder,"o_body_a_*.json")) TestBody(path);
        Console.WriteLine("PASS bodies=4 limbs=16 cuts="+cuts+" assertions="+assertions);
    }
    static void TestBody(string path)
    {
        var json=JsonDocument.Parse(File.ReadAllText(path)).RootElement;
        var source=new SkinMesh();var pos=json.GetProperty("positions");var normals=json.GetProperty("normals");
        var bones=json.GetProperty("bones").EnumerateArray().Select(x=>x.GetString()).ToArray();
        for(int i=0;i<pos.GetArrayLength();i++)
        {
            var v=new SkinVertex{Position=V3(pos[i]),Normal=V3(normals[i]),Tangent=new Vector4(1,0,0,1)};
            var uv=Floats(json.GetProperty("uv")[i]);v.UV[0]=new Vector4(uv[0],uv[1],0,0);
            var ws=Floats(json.GetProperty("weights")[i]);var ix=json.GetProperty("indices")[i].EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            for(int j=0;j<ws.Length;++j)if(ws[j]>0)v.Bones[ix[j]]=ws[j];v.Sources[i]=1;
            source.Vertices.Add(v);
        }
        foreach(var sub in json.GetProperty("triangles").EnumerateArray()) source.Submeshes.Add(sub.EnumerateArray().SelectMany(t=>t.EnumerateArray().Select(x=>x.GetInt32())).ToList());
        RoundedGeometry.WeldPositions(source);
        var origins=new Vector3[bones.Length];int k=0;
        foreach(var p in json.GetProperty("bindposes").EnumerateArray())
        {
            var f=p.EnumerateArray().Select(Floats).ToArray();
            var m=new Matrix4x4(f[0][0],f[1][0],f[2][0],f[3][0],f[0][1],f[1][1],f[2][1],f[3][1],f[0][2],f[1][2],f[2][2],f[3][2],f[0][3],f[1][3],f[2][3],f[3][3]);
            Matrix4x4.Invert(m,out var inverse);origins[k++]=Vector3.Transform(Vector3.Zero,inverse);
        }
        var limbs=new List<LimbDefinition>();
        foreach(bool arm in new[]{false,true})foreach(string side in new[]{"L","R"})
        {
            string root=arm?"cf_s_arm01_":"cf_s_thigh01_",end=arm?"cf_s_forearm01_":"cf_s_leg01_";
            int ri=Array.IndexOf(bones,root+side);
            int ei=Array.IndexOf(bones,end+side);
            var limb=new LimbDefinition{Name=(arm?"arm":"leg")+side,Root=ri>=0?origins[ri]:ReferenceRoot(Path.GetDirectoryName(path),root+side),End=origins[ei]};
            for(int i=0;i<bones.Length;++i)if(Belongs(bones[i],arm,side))limb.Bones.Add(i);
            RoundedGeometry.FindRange(source,limb);limbs.Add(limb);
            Console.WriteLine(Path.GetFileName(path)+" "+limb.Name+" length="+limb.Length+" range="+limb.Minimum+".."+limb.Maximum);
            if(previewFolder!=null)
            {
                var mesh=RoundedGeometry.Apply(source,limb,50,source.Submeshes.Count);
                var data=new {limb=limb.Name,percent=50,source="Installed stock oo_base.unity3d / o_body_a_8387",positions=mesh.Vertices.Select(v=>new[]{v.Position.X,v.Position.Y,v.Position.Z}).ToArray(),membership=mesh.Vertices.Select(limb.Membership).ToArray(),submeshes=mesh.Submeshes.Select(s=>s.ToArray()).ToArray(),axis=new[]{limb.Axis.X,limb.Axis.Y,limb.Axis.Z},center=new[]{mesh.Vertices.Last().Position.X,mesh.Vertices.Last().Position.Y,mesh.Vertices.Last().Position.Z}};
                File.WriteAllText(Path.Combine(previewFolder,limb.Name+".json"),JsonSerializer.Serialize(data));continue;
            }
            foreach(float value in Enumerable.Range(0,101).Select(i=>(float)i))
            {
                var result=RoundedGeometry.Apply(source,limb,value,source.Submeshes.Count);
                Validate(source,result,limb,value);
            }
            // Exact source-vertex planes exercise clipping on a vertex / collinear edge.
            foreach(float distance in source.Vertices.Where(v=>limb.Membership(v)>.8f).Select(v=>Vector3.Dot(v.Position-limb.Root,limb.Axis)).Where(d=>d>limb.Minimum&&d<limb.Maximum).Distinct().Take(8))
            {
                var result=RoundedGeometry.Clip(source,limb,distance,source.Submeshes.Count,true);
                Validate(source,result,limb,(distance-limb.Minimum)/(limb.Maximum-limb.Minimum)*100);
            }
        }
        if(previewFolder!=null)return;
        var combined=source;
        foreach(var limb in limbs)combined=RoundedGeometry.Apply(combined,limb,33f,source.Submeshes.Count);
        Check(combined.Submeshes.Last().Count>0,"four cap submesh");
        foreach(var sub in source.Submeshes)Check(sub.Count%3==0,"source unchanged");
    }
    internal static bool Belongs(string name,bool arm,string side)
    {
        if(!name.EndsWith("_"+side))return false;
        return (arm?new[]{"arm","forearm","elbo","wrist","hand","thumb","index","middle","ring","little"}:new[]{"thigh","leg","knee","foot","toes"}).Any(s=>name.Substring(5).StartsWith(s,StringComparison.Ordinal));
    }
    static Vector3 ReferenceRoot(string folder,string bone)
    {
        var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"o_body_a_8387.json"))).RootElement;
        int index=Array.IndexOf(json.GetProperty("bones").EnumerateArray().Select(x=>x.GetString()).ToArray(),bone);
        var f=json.GetProperty("bindposes")[index].EnumerateArray().Select(Floats).ToArray();
        var m=new Matrix4x4(f[0][0],f[1][0],f[2][0],f[3][0],f[0][1],f[1][1],f[2][1],f[3][1],f[0][2],f[1][2],f[2][2],f[3][2],f[0][3],f[1][3],f[2][3],f[3][3]);
        Matrix4x4.Invert(m,out var inverse);return Vector3.Transform(Vector3.Zero,inverse);
    }
    static void Validate(SkinMesh source,SkinMesh result,LimbDefinition limb,float value)
    {
        ++cuts;
        Check(result.Submeshes.Last().Count>=24,"cap missing");
        foreach(var v in result.Vertices.Skip(source.Vertices.Count))
        {
            Check(float.IsFinite(v.Position.X)&&float.IsFinite(v.Normal.X),"finite vertex");
            Check(Math.Abs(v.Bones.Values.Sum()-1)<.00001f&&v.Bones.Count<=4,"weights");
            Check(Math.Abs(v.Sources.Values.Sum()-1)<.0001f,"provenance");
        }
        foreach(var sub in result.Submeshes)for(int i=0;i<sub.Count;i+=3)
        {
            var a=result.Vertices[sub[i]].Position;var b=result.Vertices[sub[i+1]].Position;var c=result.Vertices[sub[i+2]].Position;
            Check(Vector3.Cross(b-a,c-a).LengthSquared()>1e-28f,"degenerate face");
        }
        // Newly created boundary edges must be paired by position, including the original UV seam.
        string Key(Vector3 p)=>RoundedGeometry.PositionKey(p);
        var counts=new Dictionary<string,int>();
        foreach(var sub in result.Submeshes)for(int i=0;i<sub.Count;i+=3)for(int e=0;e<3;++e)
        {
            int a=sub[i+e],b=sub[i+(e+1)%3];
            if(a<source.Vertices.Count&&b<source.Vertices.Count)continue;
            string ka=Key(result.Vertices[a].Position),kb=Key(result.Vertices[b].Position),edge=String.CompareOrdinal(ka,kb)<0?ka+"|"+kb:kb+"|"+ka;
            counts.TryGetValue(edge,out int count);counts[edge]=count+1;
        }
        Check(counts.All(x=>x.Value==2),"open/nonmanifold new edge: "+string.Join(",",counts.Where(x=>x.Value!=2).Take(3)));
        // Every newly generated face must point outwards, not just have paired edges.
        var cap=result.Submeshes.Last();
        for(int i=0;i<cap.Count;i+=3)
        {
            var a=result.Vertices[cap[i]];var b=result.Vertices[cap[i+1]];var c=result.Vertices[cap[i+2]];
            Check(Vector3.Dot(Vector3.Cross(b.Position-a.Position,c.Position-a.Position),a.Normal+b.Normal+c.Normal)>=-1e-12f,"cap faces outward");
        }
        // No distal limb triangles survive. Unrelated triangles keep their exact vertex indices.
        float plane=limb.Minimum+(limb.Maximum-limb.Minimum)*value/100;
        foreach(var sub in result.Submeshes.Take(source.Submeshes.Count))for(int i=0;i<sub.Count;i+=3)
        {
            var v=new[]{result.Vertices[sub[i]],result.Vertices[sub[i+1]],result.Vertices[sub[i+2]]};
            if(v.Max(limb.Membership)<.3f)continue;
            Check(v.All(x=>Vector3.Dot(x.Position-limb.Root,limb.Axis)<=plane+1.1e-6f),"distal body face retained");
        }
    }
}
