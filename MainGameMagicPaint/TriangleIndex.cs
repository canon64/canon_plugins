using System;
using System.Collections.Generic;
using V=System.Numerics.Vector3;
namespace MainGameMagicPaint
{
    // Fixed topology BVH: rebuild only on mesh replacement; refit once per deformed mesh sample.
    internal sealed class TriangleIndex
    {
        sealed class Node { internal V Min,Max; internal int Start,Count,Left=-1,Right=-1; }
        readonly List<Node> _nodes=new List<Node>(); readonly int[] _triangles,_order; V[] _vertices;
        internal int TriangleVisits {get;private set;}
        internal TriangleIndex(V[] vertices,int[] triangles)
        { _vertices=vertices;_triangles=triangles;_order=new int[triangles.Length/3];for(int i=0;i<_order.Length;i++)_order[i]=i*3;if(_order.Length>0){Build(0,_order.Length);Refit(vertices);} }
        int Build(int start,int count)
        {
            int at=_nodes.Count;var node=new Node{Start=start,Count=count};_nodes.Add(node);
            if(count<=8)return at;
            V min=new V(float.PositiveInfinity),max=new V(float.NegativeInfinity);
            for(int i=start;i<start+count;i++){int t=_order[i];V c=(_vertices[_triangles[t]]+_vertices[_triangles[t+1]]+_vertices[_triangles[t+2]])/3;min=V.Min(min,c);max=V.Max(max,c);}
            V span=max-min;int axis=span.X>=span.Y&&span.X>=span.Z?0:span.Y>=span.Z?1:2;
            Array.Sort(_order,start,count,Comparer<int>.Create((a,b)=>Center(a,axis).CompareTo(Center(b,axis))));
            int half=count/2;node.Left=Build(start,half);node.Right=Build(start+half,count-half);return at;
        }
        float Center(int t,int axis){V p=_vertices[_triangles[t]]+_vertices[_triangles[t+1]]+_vertices[_triangles[t+2]];return Axis(p,axis);}
        static float Axis(V p,int axis)=>axis==0?p.X:axis==1?p.Y:p.Z;
        internal void Refit(V[] vertices){_vertices=vertices;if(_nodes.Count>0)RefitNode(0);}
        void RefitNode(int index)
        {
            var n=_nodes[index];if(n.Left>=0){RefitNode(n.Left);RefitNode(n.Right);n.Min=V.Min(_nodes[n.Left].Min,_nodes[n.Right].Min);n.Max=V.Max(_nodes[n.Left].Max,_nodes[n.Right].Max);return;}
            n.Min=new V(float.PositiveInfinity);n.Max=new V(float.NegativeInfinity);
            for(int i=n.Start;i<n.Start+n.Count;i++)for(int j=0;j<3;j++){V p=_vertices[_triangles[_order[i]+j]];n.Min=V.Min(n.Min,p);n.Max=V.Max(n.Max,p);}
        }
        static float Distance(V point,Node n){V delta=V.Max(V.Zero,V.Max(n.Min-point,point-n.Max));return delta.LengthSquared();}
        static bool Box(V origin,V direction,Node n,float max,out float entry)
        {
            float lo=0,hi=max;
            for(int axis=0;axis<3;axis++)
            {
                float o=Axis(origin,axis),d=Axis(direction,axis),a=Axis(n.Min,axis),b=Axis(n.Max,axis);
                if(Math.Abs(d)<1e-12f){if(o<a||o>b){entry=0;return false;}continue;}
                float t0=(a-o)/d,t1=(b-o)/d;if(t0>t1){float swap=t0;t0=t1;t1=swap;}lo=Math.Max(lo,t0);hi=Math.Min(hi,t1);if(lo>hi){entry=0;return false;}
            }
            entry=lo;return true;
        }
        internal bool Ray(V origin,V direction,float limit,out int triangle,out float distance)
        {TriangleVisits=0;triangle=-1;distance=limit;if(_nodes.Count>0)RayNode(0,origin,direction,ref triangle,ref distance);return triangle>=0;}
        void RayNode(int at,V origin,V direction,ref int triangle,ref float distance)
        {
            var n=_nodes[at];if(!Box(origin,direction,n,distance,out _))return;
            if(n.Left>=0){Box(origin,direction,_nodes[n.Left],distance,out float a);Box(origin,direction,_nodes[n.Right],distance,out float b);int first=a<=b?n.Left:n.Right,second=a<=b?n.Right:n.Left;RayNode(first,origin,direction,ref triangle,ref distance);RayNode(second,origin,direction,ref triangle,ref distance);return;}
            for(int i=n.Start;i<n.Start+n.Count;i++){int t=_order[i];TriangleVisits++;if(SurfaceMath.RayTriangle(origin,direction,_vertices[_triangles[t]],_vertices[_triangles[t+1]],_vertices[_triangles[t+2]],out float d,out _)&&d<=distance){triangle=t;distance=d;}}
        }
        internal bool Near(V point,float limit,out int triangle,out V contact,bool rejectDeepInside=true)
        {TriangleVisits=0;triangle=-1;contact=default;float best=limit*limit;if(_nodes.Count>0)NearNode(0,point,ref triangle,ref contact,ref best,rejectDeepInside);return triangle>=0;}
        void NearNode(int at,V point,ref int triangle,ref V contact,ref float best,bool rejectDeepInside)
        {
            var n=_nodes[at];if(Distance(point,n)>best)return;
            if(n.Left>=0){float a=Distance(point,_nodes[n.Left]),b=Distance(point,_nodes[n.Right]);NearNode(a<=b?n.Left:n.Right,point,ref triangle,ref contact,ref best,rejectDeepInside);NearNode(a<=b?n.Right:n.Left,point,ref triangle,ref contact,ref best,rejectDeepInside);return;}
            for(int i=n.Start;i<n.Start+n.Count;i++)
            {
                int t=_order[i];V a=_vertices[_triangles[t]],b=_vertices[_triangles[t+1]],c=_vertices[_triangles[t+2]];TriangleVisits++;
                V normal=V.Cross(b-a,c-a);float length=normal.Length();if(length<1e-10f||(rejectDeepInside&&V.Dot(point-a,normal/length)<-(float)Math.Sqrt(best)))continue;
                V p=SurfaceMath.Closest(point,a,b,c);float d=V.DistanceSquared(point,p);if(d<=best){best=d;contact=p;triangle=t;}
            }
        }
        internal void Sphere(V point,float radius,List<int> result){TriangleVisits=0;result.Clear();if(_nodes.Count>0)SphereNode(0,point,radius*radius,result);}
        void SphereNode(int at,V point,float r2,List<int> result)
        {var n=_nodes[at];if(Distance(point,n)>r2)return;if(n.Left>=0){SphereNode(n.Left,point,r2,result);SphereNode(n.Right,point,r2,result);return;}for(int i=n.Start;i<n.Start+n.Count;i++){result.Add(_order[i]);TriangleVisits++;}}
    }
}
