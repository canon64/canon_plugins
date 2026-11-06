using System;
using System.Collections.Generic;
using UnityEngine;

namespace MainGameMagicPaint
{
    // A thin cap sampled on the actual painted skin. Its bind-space vertices
    // reproduce the sampled deformed surface and use the native skeleton, not a body proxy.
    internal sealed class SkinReliefPatch:IDisposable
    {
        internal const float Thickness=.0012f, Edge=.00002f;
        internal readonly GameObject Object;
        internal readonly Mesh Mesh;
        readonly Vector3[] _baseVertices,_lift,_vertices;
        SkinReliefPatch(GameObject obj,Mesh mesh,Vector3[] basis,Vector3[] lift)
        {Object=obj;Mesh=mesh;_baseVertices=basis;_lift=lift;_vertices=new Vector3[basis.Length];}
        internal static SkinReliefPatch Create(MeshProbe probe,Vector3 point,Vector3 normal,float radius,Material material,float thickness=Thickness)
        {
            var skin=probe.Renderer as SkinnedMeshRenderer;
            if(skin==null||skin.sharedMesh==null||!skin.sharedMesh.isReadable||!probe.Ready())return null;
            var original=skin.sharedMesh;var weights=original.boneWeights;var poses=original.bindposes;var bones=skin.bones;
            if(weights.Length!=original.vertexCount||poses.Length==0||bones.Length!=poses.Length)return null;
            const int segments=12,rings=3,count=1+segments*rings;
            var vertices=new Vector3[count];var basis=new Vector3[count];var lift=new Vector3[count];var assigned=new BoneWeight[count];var triangles=new List<int>();
            var tangent=Vector3.Cross(normal,Vector3.up).normalized;if(tangent.sqrMagnitude<.01f)tangent=Vector3.right;
            var bitangent=Vector3.Cross(normal,tangent).normalized;
            for(int i=0;i<count;i++)
            {
                int ring=i==0?0:(i-1)/segments+1,segment=i==0?0:(i-1)%segments;
                float r=(float)ring/rings,angle=segment*Mathf.PI*2/segments;
                Vector3 sample=point+(tangent*Mathf.Cos(angle)+bitangent*Mathf.Sin(angle))*(radius*r);
                if(!probe.Index.Near(MeshProbe.Num(sample),radius*2,out int triangle,out var contact,false))return null;
                int a=probe.Triangles[triangle],b=probe.Triangles[triangle+1],c=probe.Triangles[triangle+2];
                Vector3 pa=probe.Vertices[a],pb=probe.Vertices[b],pc=probe.Vertices[c],p=MeshProbe.Vec(contact);
                var bary=Barycentric(p,pa,pb,pc);
                assigned[i]=Blend(weights[a],weights[b],weights[c],bary);
                var matrix=WorldMatrix(assigned[i],bones,poses);
                if(Mathf.Abs(matrix.determinant)<1e-10f)return null;
                Vector3 face=Vector3.Cross(pb-pa,pc-pa).normalized;if(Vector3.Dot(face,normal)<0)face=-face;
                float dome=1-r*r;var inverse=matrix.inverse;
                basis[i]=inverse.MultiplyPoint3x4(p+face*Edge);lift[i]=inverse.MultiplyVector(face*(dome*dome));
                vertices[i]=basis[i]+lift[i]*thickness;
            }
            for(int j=0;j<segments;j++)triangles.AddRange(new[]{0,1+j,1+(j+1)%segments});
            for(int ring=1;ring<rings;ring++)for(int j=0;j<segments;j++)
            {
                int a=1+(ring-1)*segments+j,b=1+(ring-1)*segments+(j+1)%segments,c=1+ring*segments+j,d=1+ring*segments+(j+1)%segments;
                triangles.AddRange(new[]{a,c,b,b,c,d});
            }
            var mesh=new Mesh{name="Wax relief on native skin",vertices=vertices,boneWeights=assigned,bindposes=poses,triangles=triangles.ToArray()};
            var colors=new Color[count];for(int i=0;i<count;i++)colors[i]=Color.white;mesh.colors=colors;
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            var obj=new GameObject("Candle thin skin relief",typeof(SkinnedMeshRenderer));
            obj.layer=skin.gameObject.layer;
            try
            {
                obj.transform.SetParent(skin.transform,false);var renderer=obj.GetComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh=mesh;renderer.bones=bones;renderer.rootBone=skin.rootBone;renderer.sharedMaterial=material;
                renderer.localBounds=skin.localBounds;renderer.updateWhenOffscreen=skin.updateWhenOffscreen;
                var patch=new SkinReliefPatch(obj,mesh,basis,lift);patch.SetThickness(thickness);return patch;
            }
            catch{UnityEngine.Object.Destroy(obj);UnityEngine.Object.Destroy(mesh);throw;}
        }
        internal void SetThickness(float thickness)
        {
            if(Object==null||Mesh==null||float.IsNaN(thickness)||float.IsInfinity(thickness))return;
            thickness=Math.Max(0,Math.Min(.005f,thickness));
            for(int i=0;i<_vertices.Length;i++)_vertices[i]=_baseVertices[i]+_lift[i]*thickness;
            Mesh.vertices=_vertices;Mesh.RecalculateNormals();Mesh.RecalculateBounds();
            Object.SetActive(thickness>0);
        }
        static Vector3 Barycentric(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            var x=b-a;var y=c-a;var v=p-a;float xx=Vector3.Dot(x,x),xy=Vector3.Dot(x,y),yy=Vector3.Dot(y,y),vx=Vector3.Dot(v,x),vy=Vector3.Dot(v,y);
            float denominator=xx*yy-xy*xy;
            if(Math.Abs(denominator)<1e-20f)return new Vector3(1,0,0);
            float s=(yy*vx-xy*vy)/denominator,t=(xx*vy-xy*vx)/denominator;return new Vector3(1-s-t,s,t);
        }
        static BoneWeight Blend(BoneWeight a,BoneWeight b,BoneWeight c,Vector3 bary)
        {
            var totals=new Dictionary<int,float>();Add(totals,a,bary.x);Add(totals,b,bary.y);Add(totals,c,bary.z);
            var ordered=new List<KeyValuePair<int,float>>(totals);ordered.Sort((x,y)=>y.Value.CompareTo(x.Value));
            var ids=new int[4];var values=new float[4];float sum=0;
            for(int i=0;i<Math.Min(4,ordered.Count);i++){ids[i]=ordered[i].Key;values[i]=ordered[i].Value;sum+=values[i];}
            if(sum>0)for(int i=0;i<4;i++)values[i]/=sum;
            return new BoneWeight{boneIndex0=ids[0],boneIndex1=ids[1],boneIndex2=ids[2],boneIndex3=ids[3],weight0=values[0],weight1=values[1],weight2=values[2],weight3=values[3]};
        }
        static void Add(Dictionary<int,float> totals,BoneWeight weight,float factor)
        {
            int[] ids={weight.boneIndex0,weight.boneIndex1,weight.boneIndex2,weight.boneIndex3};float[] values={weight.weight0,weight.weight1,weight.weight2,weight.weight3};
            for(int i=0;i<4;i++)if(values[i]*factor>0){totals.TryGetValue(ids[i],out float value);totals[ids[i]]=value+values[i]*factor;}
        }
        internal static Matrix4x4 WorldMatrix(BoneWeight weight,Transform[] bones,Matrix4x4[] poses)
        {
            int[] ids={weight.boneIndex0,weight.boneIndex1,weight.boneIndex2,weight.boneIndex3};float[] values={weight.weight0,weight.weight1,weight.weight2,weight.weight3};
            var result=new Matrix4x4();
            for(int k=0;k<4;k++)
            {
                if(values[k]<=0||ids[k]>=bones.Length||bones[ids[k]]==null)continue;
                var bone=bones[ids[k]].localToWorldMatrix*poses[ids[k]];
                for(int j=0;j<16;j++)result[j]+=bone[j]*values[k];
            }
            return result;
        }
        public void Dispose(){if(Object!=null){Object.SetActive(false);UnityEngine.Object.Destroy(Object);}if(Mesh!=null)UnityEngine.Object.Destroy(Mesh);}
    }
}
