using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using N3=System.Numerics.Vector3;
using N4=System.Numerics.Vector4;
using U3=UnityEngine.Vector3;

namespace MainGameRoundedLimbClothes
{
    internal sealed class BodyMeshAdapter
    {
        internal readonly Mesh Source;
        internal readonly SkinMesh Geometry;
        internal readonly LimbDefinition[] Limbs;
        internal BodyMeshAdapter(Mesh source,Transform[] bones)
        {
            Source=source;
            if(source==null)throw new InvalidOperationException("表示中の身体メッシュがありません。");
            if(!source.isReadable)throw new InvalidOperationException("表示中の身体メッシュは頂点の読み出しが禁止されています："+source.name);
            if(bones==null||bones.Length==0||source.bindposes.Length==0)
                throw new InvalidOperationException("表示中の身体にボーンまたはバインド姿勢がありません。");
            var positions=source.vertices;var normals=source.normals;var tangents=source.tangents;var colors=source.colors;var weights=source.boneWeights;
            if(normals.Length!=positions.Length||weights.Length!=positions.Length)throw new InvalidOperationException("身体の頂点属性が不足しています。");
            var uvs=new List<Vector4>[8];for(int channel=0;channel<8;++channel){uvs[channel]=new List<Vector4>();source.GetUVs(channel,uvs[channel]);}
            Geometry=new SkinMesh();
            for(int i=0;i<positions.Length;++i)
            {
                var v=new SkinVertex{Position=ToN(positions[i]),Normal=ToN(normals[i]),Tangent=tangents.Length==positions.Length?ToN(tangents[i]):new N4(1,0,0,1)};
                if(colors.Length==positions.Length)v.Color=new N4(colors[i].r,colors[i].g,colors[i].b,colors[i].a);
                for(int c=0;c<8;++c)if(uvs[c].Count==positions.Length)v.UV[c]=ToN(uvs[c][i]);
                var w=weights[i];int[] ids={w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};float[] ws={w.weight0,w.weight1,w.weight2,w.weight3};
                for(int j=0;j<4;++j)if(ws[j]>0)
                {
                    if(ids[j]<0||ids[j]>=bones.Length||ids[j]>=source.bindposes.Length||float.IsNaN(ws[j])||float.IsInfinity(ws[j]))throw new InvalidOperationException("身体のボーンウェイトが不正です。");
                    if(!v.Bones.ContainsKey(ids[j]))v.Bones[ids[j]]=0;v.Bones[ids[j]]+=ws[j];
                }
                if(v.Bones.Count==0)throw new InvalidOperationException("身体に変形情報のない頂点があります。");
                v.Sources[i]=1;Geometry.Vertices.Add(v);
            }
            for(int sub=0;sub<source.subMeshCount;++sub)
            {
                var triangles=source.GetTriangles(sub);
                if(triangles.Length%3!=0||triangles.Any(id=>id<0||id>=positions.Length))throw new InvalidOperationException("身体の三角形データが不正です。");
                Geometry.Submeshes.Add(triangles.ToList());
            }
            RoundedGeometry.WeldPositions(Geometry);
            // Some stock renderers append unused helper bones without adding bind poses.
            Limbs=new LimbDefinition[4];var names=bones.Take(source.bindposes.Length).Select(b=>b==null?"":b.name).ToArray();
            for(int index=0;index<4;++index)
            {
                bool arm=index<2;string side=index%2==0?"L":"R";
                int root=Array.IndexOf(names,(arm?"cf_s_arm01_":"cf_s_thigh01_")+side);
                int end=Array.IndexOf(names,(arm?"cf_s_forearm01_":"cf_s_leg01_")+side);
                if(end<0)continue;
                var endPoint=ToN(source.bindposes[end].inverse.MultiplyPoint3x4(U3.zero));
                var rootPoint=root>=0?ToN(source.bindposes[root].inverse.MultiplyPoint3x4(U3.zero)):N3.Zero;
                if(root<0)
                {
                    int middle=Array.IndexOf(names,(arm?"cf_s_arm02_":"cf_s_thigh02_")+side);
                    if(middle<0)continue;
                    var middlePoint=ToN(source.bindposes[middle].inverse.MultiplyPoint3x4(U3.zero));
                    rootPoint=endPoint+2*(middlePoint-endPoint);
                }
                var limb=new LimbDefinition{Name=new[]{"左腕","右腕","左脚","右脚"}[index],Root=rootPoint,End=endPoint};
                for(int j=0;j<names.Length;++j)if(Belongs(names[j],arm,side))limb.Bones.Add(j);
                if(limb.Length<.0001f||limb.Bones.Count==0)continue;
                Limbs[index]=limb;
            }
        }
        internal static bool Belongs(string name,bool arm,string side)
        {
            if(name.Length<6||!name.EndsWith("_"+side,StringComparison.Ordinal))return false;
            return (arm?new[]{"arm","forearm","elbo","wrist","hand","thumb","index","middle","ring","little"}:new[]{"thigh","leg","knee","foot","toes"}).Any(p=>name.Substring(5).StartsWith(p,StringComparison.Ordinal));
        }
        internal Mesh Generate(PositionSettings settings,bool arms,bool legs)
        {
            var geometry=Geometry;int capSubmesh=Source.subMeshCount;
            for(int i=0;i<4;++i)if(i<2?arms:legs)
            {
                if(Limbs[i]==null)throw new InvalidOperationException(new[]{"左腕","右腕","左脚","右脚"}[i]+"に必要なKKSボーンがありません。");
                if(Limbs[i].Maximum==0)RoundedGeometry.FindRange(Geometry,Limbs[i]);
                geometry=RoundedGeometry.Apply(geometry,Limbs[i],settings.Get(i),capSubmesh);
            }
            Mesh result=null;
            try
            {
                result=new Mesh{name="RoundedLimbClothes (character owned)",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
                result.SetVertices(geometry.Vertices.Select(v=>ToU(v.Position)).ToList());
                result.SetNormals(geometry.Vertices.Select(v=>ToU(v.Normal)).ToList());
                result.SetTangents(geometry.Vertices.Select(v=>ToU(v.Tangent)).ToList());
                if(Source.colors.Length>0)result.SetColors(geometry.Vertices.Select(v=>new Color(v.Color.X,v.Color.Y,v.Color.Z,v.Color.W)).ToList());
                var originalUV=new List<Vector4>();
                for(int c=0;c<8;++c){Source.GetUVs(c,originalUV);if(originalUV.Count>0)result.SetUVs(c,geometry.Vertices.Select(v=>ToU(v.UV[c])).ToList());}
                result.bindposes=Source.bindposes;
                result.boneWeights=geometry.Vertices.Select(v=>Weight(v.Bones)).ToArray();
                result.subMeshCount=geometry.Submeshes.Count;
                for(int s=0;s<geometry.Submeshes.Count;++s)result.SetTriangles(geometry.Submeshes[s],s,false);
                CopyBlendShapes(result,geometry);
                result.bounds=Source.bounds;return result;
            }
            catch{if(result!=null)UnityEngine.Object.Destroy(result);throw;}
        }
        void CopyBlendShapes(Mesh result,SkinMesh geometry)
        {
            var dv=new U3[Source.vertexCount];var dn=new U3[dv.Length];var dt=new U3[dv.Length];
            for(int s=0;s<Source.blendShapeCount;++s)for(int f=0;f<Source.GetBlendShapeFrameCount(s);++f)
            {
                Source.GetBlendShapeFrameVertices(s,f,dv,dn,dt);
                result.AddBlendShapeFrame(Source.GetBlendShapeName(s),Source.GetBlendShapeFrameWeight(s,f),Interpolate(dv,geometry),Interpolate(dn,geometry),Interpolate(dt,geometry));
            }
        }
        static U3[] Interpolate(U3[] original,SkinMesh mesh)=>mesh.Vertices.Select(v=>{var sum=U3.zero;foreach(var p in v.Sources)sum+=original[p.Key]*p.Value;return sum;}).ToArray();
        static BoneWeight Weight(Dictionary<int,float> weights)
        {
            var entries=weights.OrderByDescending(x=>x.Value).Take(4).ToArray();var ids=new int[4];var ws=new float[4];
            for(int i=0;i<entries.Length;++i){ids[i]=entries[i].Key;ws[i]=entries[i].Value;}
            return new BoneWeight{boneIndex0=ids[0],boneIndex1=ids[1],boneIndex2=ids[2],boneIndex3=ids[3],weight0=ws[0],weight1=ws[1],weight2=ws[2],weight3=ws[3]};
        }
        static N3 ToN(U3 v)=>new N3(v.x,v.y,v.z);
        static N4 ToN(Vector4 v)=>new N4(v.x,v.y,v.z,v.w);
        static U3 ToU(N3 v)=>new U3(v.X,v.Y,v.Z);
        static Vector4 ToU(N4 v)=>new Vector4(v.X,v.Y,v.Z,v.W);
    }
}
