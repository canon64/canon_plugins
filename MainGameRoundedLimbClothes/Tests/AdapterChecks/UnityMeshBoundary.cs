using System;
using System.Collections.Generic;
using System.Linq;
using N=System.Numerics;
namespace UnityEngine.Rendering {public enum IndexFormat{UInt16,UInt32}}
namespace UnityEngine
{
    public struct Vector3
    {
        public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 zero=>new Vector3();
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator *(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);
    }
    public struct Vector4{public float x,y,z,w;public Vector4(float x,float y,float z,float w){this.x=x;this.y=y;this.z=z;this.w=w;}}
    public struct Color{public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}}
    public struct Bounds{}
    public struct BoneWeight{public int boneIndex0,boneIndex1,boneIndex2,boneIndex3;public float weight0,weight1,weight2,weight3;}
    public struct Matrix4x4
    {
        internal N.Matrix4x4 value;
        public Matrix4x4 inverse{get{N.Matrix4x4.Invert(value,out var result);return new Matrix4x4{value=result};}}
        public Vector3 MultiplyPoint3x4(Vector3 p){var r=N.Vector3.Transform(new N.Vector3(p.x,p.y,p.z),value);return new Vector3(r.X,r.Y,r.Z);}
    }
    public class Transform:Object{}
    public class Mesh:Object
    {
        public bool isReadable=true;public Rendering.IndexFormat indexFormat;
        public Vector3[] vertices=Array.Empty<Vector3>(),normals=Array.Empty<Vector3>();
        public Vector4[] tangents=Array.Empty<Vector4>();public Color[] colors=Array.Empty<Color>();
        public BoneWeight[] boneWeights=Array.Empty<BoneWeight>();public Matrix4x4[] bindposes=Array.Empty<Matrix4x4>();public Bounds bounds;
        readonly List<int[]> submeshes=new List<int[]>();readonly Dictionary<int,Vector4[]> uv=new Dictionary<int,Vector4[]>();
        sealed class Shape{internal string Name;internal float Weight;internal Vector3[] V,N,T;}
        readonly List<Shape> shapes=new List<Shape>();
        public int vertexCount=>vertices.Length;
        public int subMeshCount{get=>submeshes.Count;set{while(submeshes.Count<value)submeshes.Add(Array.Empty<int>());while(submeshes.Count>value)submeshes.RemoveAt(submeshes.Count-1);}}
        public int blendShapeCount=>shapes.Count;
        public void SetVertices(List<Vector3> v)=>vertices=v.ToArray();public void SetNormals(List<Vector3> n)=>normals=n.ToArray();
        public void SetTangents(List<Vector4> t)=>tangents=t.ToArray();public void SetColors(List<Color> c)=>colors=c.ToArray();
        public void GetUVs(int channel,List<Vector4> to){to.Clear();if(uv.TryGetValue(channel,out var data))to.AddRange(data);}
        public void SetUVs(int channel,List<Vector4> from)=>uv[channel]=from.ToArray();
        public int[] GetTriangles(int sub)=>(int[])submeshes[sub].Clone();
        public void SetTriangles(List<int> ids,int sub,bool recalc)=>submeshes[sub]=ids.ToArray();
        public int GetBlendShapeFrameCount(int shape)=>1;
        public string GetBlendShapeName(int shape)=>shapes[shape].Name;
        public float GetBlendShapeFrameWeight(int shape,int frame)=>shapes[shape].Weight;
        public void GetBlendShapeFrameVertices(int shape,int frame,Vector3[] v,Vector3[] n,Vector3[] t){Array.Copy(shapes[shape].V,v,v.Length);Array.Copy(shapes[shape].N,n,n.Length);Array.Copy(shapes[shape].T,t,t.Length);}
        public void AddBlendShapeFrame(string name,float weight,Vector3[] v,Vector3[] n,Vector3[] t)=>shapes.Add(new Shape{Name=name,Weight=weight,V=v,N=n,T=t});
    }
}
