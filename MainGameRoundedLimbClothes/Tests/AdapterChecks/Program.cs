using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using MainGameRoundedLimbClothes;
using Sideloader.AutoResolver;
using UnityEngine;
using N3=System.Numerics.Vector3;
using NM=System.Numerics.Matrix4x4;

static class Program
{
    static int checks;
    static void Check(bool good,string why){checks++;if(!good)throw new Exception(why);}
    static float[] F(JsonElement e)=>e.EnumerateArray().Select(v=>v.GetSingle()).ToArray();
    static Vector3 V(JsonElement e){var f=F(e);return new Vector3(f[0],f[1],f[2]);}
    static N3 N(Vector3 v)=>new N3(v.x,v.y,v.z);
    static void Main(string[] args)
    {
        string folder=args.Length>0?args[0]:"../Fixtures";
        Console.WriteLine("START actual BodyMeshAdapter -> actual controller -> renderer assignment checks; Unity boundary substituted");
        foreach(string path in Directory.GetFiles(folder,"o_body_a_*.json"))Run(path);
        Console.WriteLine("PASS actual adapter/controller and posed seams checks="+checks);
    }
    static void Run(string path)
    {
        var json=JsonDocument.Parse(File.ReadAllText(path)).RootElement;
        var mesh=new Mesh{name="actual installed body fixture"};
        mesh.vertices=json.GetProperty("positions").EnumerateArray().Select(V).ToArray();
        mesh.normals=json.GetProperty("normals").EnumerateArray().Select(V).ToArray();
        mesh.tangents=Enumerable.Repeat(new Vector4(1,0,0,1),mesh.vertexCount).ToArray();
        var uv=json.GetProperty("uv").EnumerateArray().Select(e=>{var f=F(e);return new Vector4(f[0],f[1],0,0);}).ToList();mesh.SetUVs(0,uv);
        var indices=json.GetProperty("indices");var ws=json.GetProperty("weights");mesh.boneWeights=new BoneWeight[mesh.vertexCount];
        for(int i=0;i<mesh.vertexCount;i++)
        {
            var ids=indices[i].EnumerateArray().Select(v=>v.GetInt32()).ToArray();var w=F(ws[i]);
            mesh.boneWeights[i]=new BoneWeight{boneIndex0=ids[0],boneIndex1=ids[1],boneIndex2=ids[2],boneIndex3=ids[3],weight0=w[0],weight1=w[1],weight2=w[2],weight3=w[3]};
        }
        mesh.bindposes=json.GetProperty("bindposes").EnumerateArray().Select(e=>
        {
            var f=e.EnumerateArray().Select(F).ToArray();return new Matrix4x4{value=new NM(f[0][0],f[1][0],f[2][0],f[3][0],f[0][1],f[1][1],f[2][1],f[3][1],f[0][2],f[1][2],f[2][2],f[3][2],f[0][3],f[1][3],f[2][3],f[3][3])};
        }).ToArray();
        var subs=json.GetProperty("triangles");mesh.subMeshCount=subs.GetArrayLength();
        for(int s=0;s<mesh.subMeshCount;s++)mesh.SetTriangles(subs[s].EnumerateArray().SelectMany(t=>t.EnumerateArray().Select(x=>x.GetInt32())).ToList(),s,false);
        var bones=json.GetProperty("bones").EnumerateArray().Select(e=>new Transform{name=e.GetString()}).ToArray();
        // Exercise the real adapter with a compatible body that does NOT match any stock byte fingerprint.
        for(int i=0;i<mesh.vertexCount;i++){var v=mesh.vertices[i];v.x*=1.025f;mesh.vertices[i]=v;}
        var delta=Enumerable.Repeat(new Vector3(.0001f,0,0),mesh.vertexCount).ToArray();
        mesh.AddBlendShapeFrame("existing body morph",100,delta,new Vector3[mesh.vertexCount],new Vector3[mesh.vertexCount]);
        var adapter=new BodyMeshAdapter(mesh,bones);
        var c=new RoundedLimbController();c.ChaControl.rendBody.sharedMesh=mesh;c.ChaControl.rendBody.bones=bones;
        UniversalAutoResolver.Entries[100000123]=new ResolveInfo{GUID=Plugin.ModGuid,Slot=1};
        c.ChaControl.nowCoordinate.clothes.parts[4].id=100000123;c.ChaControl.nowCoordinate.clothes.parts[6].id=100000123;
        Plugin.Instance.Permit=true;c.Reload();
        var generated=c.ChaControl.rendBody.sharedMesh;
        Check(generated!=mesh&&generated.subMeshCount==mesh.subMeshCount+1,"real controller must assign real adapter result: "+c.Status);
        Check(generated.blendShapeCount==1&&generated.GetBlendShapeName(0)=="existing body morph","morph retained");
        var settings=new PositionSettings();settings.LinkArms=false;settings.LinkLegs=false;
        foreach(float pos in new[]{0f,50f,100f})
        {
            settings.LeftArm=pos;settings.RightArm=100-pos;settings.LeftLeg=pos;settings.RightLeg=100-pos;
            c.Preview(settings);generated=c.ChaControl.rendBody.sharedMesh;
            Check(c.Settings.LeftArm==pos&&generated!=mesh,"actual preview changes position");
            Check(!c.Status.StartsWith("未適用"),"actual adapter accepts compatible body: "+c.Status);
            CheckSeams(generated,mesh.subMeshCount,adapter,bones);
        }
        Check(c.Commit(),"real generated preview can commit");
        c.ChaControl.fileStatus.clothesState[4]=3;c.ChaControl.fileStatus.clothesState[6]=3;c.Refresh();
        Check(c.ChaControl.rendBody.sharedMesh==mesh,"actual body restored on undress");
        c.ChaControl.fileStatus.clothesState[4]=0;c.Refresh();Check(c.ChaControl.rendBody.sharedMesh!=mesh,"real adapter applies again after redress");
        c.StopRuntime();Check(c.ChaControl.rendBody.sharedMesh==mesh,"runtime stop restores actual source");c.Dispose();
        Console.WriteLine("BODY "+Path.GetFileName(path)+" actual assignment, deformed body, blendshape, poses, cancel/restore PASS");
    }
    static void CheckSeams(Mesh mesh,int capSub,BodyMeshAdapter adapter,Transform[] bones)
    {
        var cap=mesh.GetTriangles(capSub);var body=Enumerable.Range(0,capSub).SelectMany(mesh.GetTriangles).ToArray();
        var shared=cap.Intersect(body).ToArray();Check(shared.Length>=32,"body and caps must share actual vertex indices");
        var boundaryGroups=body.Distinct().GroupBy(i=>RoundedGeometry.PositionKey(N(mesh.vertices[i]))).ToDictionary(g=>g.Key,g=>g.ToArray());
        var pairs=cap.Distinct().Where(i=>boundaryGroups.ContainsKey(RoundedGeometry.PositionKey(N(mesh.vertices[i])))).SelectMany(i=>boundaryGroups[RoundedGeometry.PositionKey(N(mesh.vertices[i]))].Select(j=>(i,j))).ToArray();
        foreach(int pose in new[]{0,1,2,3})
        {
            var transforms=new NM[mesh.bindposes.Length];
            for(int i=0;i<mesh.bindposes.Length;i++)
            {
                var origin=N(mesh.bindposes[i].inverse.MultiplyPoint3x4(Vector3.zero));
                bool arm=bones[i].name.Contains("arm")||bones[i].name.Contains("hand")||bones[i].name.Contains("elbo")||bones[i].name.Contains("wrist");
                var rotation=arm?NM.CreateRotationZ(pose*.5f)*NM.CreateRotationX(pose*.7f):NM.CreateRotationX(pose*.3f);
                transforms[i]=NM.CreateTranslation(-origin)*NM.CreateScale(1+pose*.1f,1-pose*.03f,1+pose*.04f)*rotation*NM.CreateTranslation(origin);
            }
            N3 Skin(int id)
            {
                var v=N(mesh.vertices[id]);var w=mesh.boneWeights[id];
                return N3.Transform(v,transforms[w.boneIndex0])*w.weight0+N3.Transform(v,transforms[w.boneIndex1])*w.weight1+N3.Transform(v,transforms[w.boneIndex2])*w.weight2+N3.Transform(v,transforms[w.boneIndex3])*w.weight3;
            }
            foreach(var (a,b) in pairs)Check(N3.Distance(Skin(a),Skin(b))<.00002f,"bent/twisted/scaled body-cap seam opened");
            foreach(int id in cap.Distinct()){var v=Skin(id);Check(float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z),"posed cap finite");}
        }
    }
}
