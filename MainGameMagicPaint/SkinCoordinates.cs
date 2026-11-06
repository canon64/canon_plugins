using System.Numerics;
namespace MainGameMagicPaint
{
    internal static class SkinCoordinates
    {
        // Legacy BakeMesh already includes scale. Use the current Transform pose, not Renderer graphics state.
        internal static Matrix4x4 BakedToWorld(Vector3 position, Quaternion rotation) =>
            Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(position);
    }
    internal struct SurfaceAnchor
    {
        int _a, _b, _c;
        Vector3 _weights;
        bool _valid;
        internal static SurfaceAnchor Capture(Vector3[] vertices, int[] triangles, int triangle, Vector3 point)
        {
            int a=triangles[triangle],b=triangles[triangle+1],c=triangles[triangle+2];
            var ab=vertices[b]-vertices[a];var ac=vertices[c]-vertices[a];var p=point-vertices[a];
            float aa=Vector3.Dot(ab,ab),bb=Vector3.Dot(ab,ac),cc=Vector3.Dot(ac,ac),d=aa*cc-bb*bb;
            if(d<1e-20f)return default;
            float v=(cc*Vector3.Dot(p,ab)-bb*Vector3.Dot(p,ac))/d;
            float w=(aa*Vector3.Dot(p,ac)-bb*Vector3.Dot(p,ab))/d;
            return new SurfaceAnchor{_a=a,_b=b,_c=c,_weights=new Vector3(1-v-w,v,w),_valid=true};
        }
        internal bool Resolve(Vector3[] vertices,out Vector3 point)
        {
            point=default;
            if(!_valid||vertices==null||_a>=vertices.Length||_b>=vertices.Length||_c>=vertices.Length)return false;
            point=vertices[_a]*_weights.X+vertices[_b]*_weights.Y+vertices[_c]*_weights.Z;return true;
        }
    }
}
