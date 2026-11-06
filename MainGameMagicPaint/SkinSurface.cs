using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using N = System.Numerics.Vector3;

namespace MainGameMagicPaint
{
    internal sealed class MeshProbe : IDisposable
    {
        internal readonly Renderer Renderer;
        internal PaintActor Owner;
        Mesh _baked;
        readonly List<Vector3> _localVertices = new List<Vector3>();
        Mesh _source;
        internal Vector3[] Vertices;
        internal N[] NumericVertices;
        internal System.Numerics.Vector2[] NumericUv;
        Mesh _numericSource;
        internal TriangleIndex Index;
        int[] _indexedTriangles;
        internal Vector2[] Uv;
        internal int[] Triangles;
        int _frame = -1;
        int _boundsFrame = -1;
        Bounds _queryBounds;
        internal Bounds QueryBounds
        {
            get
            {
                if(_boundsFrame==Time.frameCount)return _queryBounds;
                _queryBounds=Renderer.bounds;
                if(Renderer is SkinnedMeshRenderer skin)
                {
                    // Graphics bounds may still describe the previous head pose during LateUpdate.
                    var local=skin.localBounds;var matrix=skin.transform.localToWorldMatrix;var e=local.extents;
                    var x=matrix.MultiplyVector(new Vector3(e.x,0,0));var y=matrix.MultiplyVector(new Vector3(0,e.y,0));var z=matrix.MultiplyVector(new Vector3(0,0,e.z));
                    var extents=new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z));
                    var current=new Bounds(matrix.MultiplyPoint3x4(local.center),extents*2);
                    _queryBounds.Encapsulate(current.min);_queryBounds.Encapsulate(current.max);
                }
                _boundsFrame=Time.frameCount;return _queryBounds;
            }
        }
        internal int SampleFrame => _frame;
        internal IReadOnlyList<Vector3> LocalVertices => _localVertices;
        internal MeshProbe(Renderer renderer) { Renderer = renderer; }
        internal static N Num(Vector3 p) => new N(p.x, p.y, p.z);
        internal static Vector3 Vec(N p) => new Vector3(p.X, p.Y, p.Z);
        internal static bool Participates(Renderer renderer) => renderer != null && PaintVisibility.Participates(
            renderer.enabled, renderer.gameObject.activeInHierarchy, renderer.forceRenderingOff,
            renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly, renderer.gameObject.layer,
            renderer.gameObject.layer == 11 ? renderer.name : null);
        internal bool Ready()
        {
            if (!Participates(Renderer)) return false;
            if (_frame == Time.frameCount) return Vertices != null;
            Mesh mesh;
            if (Renderer is SkinnedMeshRenderer skin)
            {
                if (skin.sharedMesh == null) return false;
                if (_baked == null) _baked = new Mesh { name = "MagicPaint contact cache" };
                skin.BakeMesh(_baked); mesh = _baked;
                if (_source != skin.sharedMesh)
                {
                    _source = skin.sharedMesh;
                    // Imported head meshes can be non-readable. BakeMesh supplies a readable snapshot.
                    var topology = _source.isReadable ? _source : _baked;
                    Triangles = topology.triangles; Uv = topology.uv;
                }
            }
            else
            {
                var mf = Renderer.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) return false;
                mesh = mf.sharedMesh;
                if (_source != mesh) { _source = mesh; Triangles = mesh.triangles; Uv = mesh.uv; }
            }
            mesh.GetVertices(_localVertices); var local = _localVertices;
            if (Vertices == null || Vertices.Length != local.Count) { Vertices = new Vector3[local.Count]; NumericVertices = new N[local.Count]; }
            if(Renderer is SkinnedMeshRenderer)
            {
                var pose=Renderer.transform;var rotation=pose.rotation;
                var matrix=SkinCoordinates.BakedToWorld(Num(pose.position),new System.Numerics.Quaternion(rotation.x,rotation.y,rotation.z,rotation.w));
                for(int i=0;i<local.Count;i++){NumericVertices[i]=N.Transform(Num(local[i]),matrix);Vertices[i]=Vec(NumericVertices[i]);}
            }
            else
            {
                var matrix=Renderer.transform.localToWorldMatrix;
                for(int i=0;i<local.Count;i++){Vertices[i]=matrix.MultiplyPoint3x4(local[i]);NumericVertices[i]=Num(Vertices[i]);}
            }
            if (_numericSource != _source) { _numericSource = _source; NumericUv = new System.Numerics.Vector2[Uv.Length]; for (int i = 0; i < Uv.Length; i++) NumericUv[i] = new System.Numerics.Vector2(Uv[i].x, Uv[i].y); }
            if (Index == null || _indexedTriangles != Triangles) { _indexedTriangles = Triangles; Index = new TriangleIndex(NumericVertices, Triangles); }
            else Index.Refit(NumericVertices);
            _frame = Time.frameCount; return true;
        }
        internal bool Ray(Ray ray, float limit, out SurfaceHit hit)
        {
            hit = default;
            if (Renderer == null || !QueryBounds.IntersectRay(ray, out float boundsDistance) || boundsDistance > limit || !Ready()) return false;
            if (!Index.Ray(Num(ray.origin), Num(ray.direction), limit, out int triangle, out float distance)) return false;
            Vector3 normal = Normal(triangle);
            if (Vector3.Dot(normal, ray.direction) > 0) normal = -normal;
            hit = new SurfaceHit { Probe = this, Point = ray.GetPoint(distance), Normal = normal, Distance = distance, Triangle = triangle };
            return true;
        }
        Vector3 Normal(int triangle) => Vec(SurfaceMath.TriangleNormal(NumericVertices[Triangles[triangle]], NumericVertices[Triangles[triangle+1]], NumericVertices[Triangles[triangle+2]]));
        internal void Stamp(PaintBuffer buffer,Vector3 point,Vector3 normal,float radius,uint color,float strength,bool erase,float feather,List<int> candidates,BrushStats stats=null)
        {
            if(strength<=0||radius<=0||!Ready())return;
            Index.Sphere(Num(point),radius,candidates);
            BrushRaster.Stamp(buffer,NumericVertices,NumericUv,Triangles,Num(point),Num(normal),radius,color,strength,erase,feather,candidates,stats);
        }
        internal bool Near(Vector3 point, float limit, out SurfaceHit hit)
        {
            hit = default;
            if (Renderer == null || QueryBounds.SqrDistance(point) > limit * limit || !Ready()) return false;
            if (!Index.Near(Num(point), limit, out int triangle, out N contact)) return false;
            hit = new SurfaceHit { Probe = this, Point = Vec(contact), Normal = Normal(triangle), Distance = Vector3.Distance(point, Vec(contact)), Triangle = triangle };
            return true;
        }
        public void Dispose() { if (_baked != null) UnityEngine.Object.Destroy(_baked); _baked = null; }
    }
    internal struct SurfaceHit
    {
        internal MeshProbe Probe;
        internal SkinSurface Surface;
        internal Vector3 Point, Normal;
        internal float Distance;
        internal int Triangle;
    }
    internal sealed class SkinSurface : IDisposable
    {
        internal readonly PaintBuffer Buffer;
        internal BrushStats RasterTrace;
        internal readonly MeshProbe Probe;
        internal readonly bool Face;
        internal Texture2D Ink { get; private set; }
        readonly List<Binding> _bindings = new List<Binding>();
        readonly List<int> _candidates = new List<int>();
        Color32[] _upload, _tileUpload;
        bool _fullUpload = true;
        sealed class Binding { internal int Index; internal Texture Source; internal RenderTexture Target; internal MaterialPropertyBlock Saved; }
        static readonly int MainTex = Shader.PropertyToID("_MainTex");
        readonly string _owner;
        readonly int _order;
        internal SkinSurface(MeshProbe probe, bool face, PaintBuffer buffer,string owner=Plugin.Guid,int order=SkinOverlayApi.PaintOrder)
        {
            Probe = probe; Face = face; Buffer = buffer;_owner=owner;_order=order;
            Ink = new Texture2D(buffer.Size, buffer.Size, TextureFormat.RGBA32, false) { name = "MagicPaint " + (face ? "face" : "body"), wrapMode = TextureWrapMode.Clamp };
            _upload = new Color32[buffer.Pixels.Length]; Buffer.Dirty = true;
            try { Rebind(); Flush(); } catch { Dispose(); throw; }
        }
        internal void Rebind()
        {
            Compose();
        }
        void Compose()
        {
            if(!SkinOverlayApi.SetLayer(_owner,Probe.Renderer,Ink,_order))throw new InvalidOperationException("肌テクスチャへの描画を開始できません");
            _bindings.Clear();
            foreach(var binding in SkinOverlayApi.GetBindings(Probe.Renderer))
                _bindings.Add(new Binding{Index=binding.Index,Source=binding.Source,Target=binding.Target,Saved=binding.Saved});
        }
        internal void Flush()
        {
            if (!Buffer.Dirty) return;
            // Convert/upload changed tiles only. The first clear upload initializes the whole texture.
            if (_fullUpload)
            {
                for (int i = 0; i < _upload.Length; i++) { uint p = Buffer.Pixels[i]; _upload[i] = new Color32((byte)p,(byte)(p>>8),(byte)(p>>16),(byte)(p>>24)); }
                Ink.SetPixels32(_upload); _upload = null; _fullUpload = false;
            }
            else
            {
                if (_tileUpload == null) _tileUpload = new Color32[PaintBuffer.Tile * PaintBuffer.Tile];
                foreach (int tile in Buffer.UploadTiles)
                {
                    int x = tile % (Buffer.Size / PaintBuffer.Tile) * PaintBuffer.Tile, y = tile / (Buffer.Size / PaintBuffer.Tile) * PaintBuffer.Tile;
                    for (int row = 0; row < PaintBuffer.Tile; row++) for (int col = 0; col < PaintBuffer.Tile; col++)
                    { uint p = Buffer.Pixels[(y+row)*Buffer.Size+x+col]; _tileUpload[row*PaintBuffer.Tile+col] = new Color32((byte)p,(byte)(p>>8),(byte)(p>>16),(byte)(p>>24)); }
                    Ink.SetPixels32(x, y, PaintBuffer.Tile, PaintBuffer.Tile, _tileUpload);
                }
            }
            Ink.Apply(false, false); Compose(); Buffer.Dirty = false; Buffer.UploadTiles.Clear();
        }
        internal void Stamp(Vector3 point, Vector3 normal, float radius, uint color, float strength, bool erase, float feather)
        {
            Probe.Stamp(Buffer,point,normal,radius,color,strength,erase,feather,_candidates,RasterTrace);
        }
        internal int AssignedBindings
        {
            get
            {
                int count=0;var renderer=Probe.Renderer;if(renderer==null)return 0;
                foreach(var binding in _bindings){var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,binding.Index);if(block.GetTexture(MainTex)==binding.Target)count++;}
                return count;
            }
        }
        internal void CaptureImages(string directory, string name, System.Text.StringBuilder report)
        {
            CaptureTexture(Ink, System.IO.Path.Combine(directory,name+"-gpu-ink.png"));
            var renderer=Probe.Renderer;
            if (renderer == null) { report.AppendLine(name+": renderer is missing"); return; }
            var materials=renderer.sharedMaterials;
            report.AppendLine(name+" bindingCount="+_bindings.Count);
            if (_bindings.Count == 0) report.AppendLine(name+": no skin texture binding; composed/assigned images unavailable");
            report.AppendLine(name+": inkPixels="+Buffer.InkPixels+" renderer="+renderer.name+" layer="+renderer.gameObject.layer+" enabled="+renderer.enabled);
            foreach(var binding in _bindings)
            {
                var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,binding.Index);
                var common=new MaterialPropertyBlock();renderer.GetPropertyBlock(common);
                if (binding.Index >= materials.Length || materials[binding.Index] == null) { report.AppendLine("missing material slot="+binding.Index); continue; }
                var mat=materials[binding.Index];
                Texture displayed=block.GetTexture(MainTex)??common.GetTexture(MainTex)??mat.GetTexture(MainTex);
                string prefix=System.IO.Path.Combine(directory,name+"-slot"+binding.Index);
                CaptureTexture(binding.Source,prefix+"-original.png"); CaptureTexture(binding.Target,prefix+"-composed.png");CaptureTexture(displayed,prefix+"-assigned.png");
                report.AppendLine("slot="+binding.Index+" shader="+mat.shader.name+" renderQueue="+mat.renderQueue+" assigned="+(displayed==binding.Target)+" keywords="+string.Join(",",mat.shaderKeywords));
            }
        }
        static void CaptureTexture(Texture source,string path)
        {
            if(source==null)return;var previous=RenderTexture.active;var temporary=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32);
            var texture=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);
            try { Graphics.Blit(source,temporary);RenderTexture.active=temporary;texture.ReadPixels(new Rect(0,0,source.width,source.height),0,0);texture.Apply(false);System.IO.File.WriteAllBytes(path,texture.EncodeToPNG()); }
            finally { RenderTexture.active=previous;RenderTexture.ReleaseTemporary(temporary);UnityEngine.Object.Destroy(texture); }
        }
        public void Dispose()
        {
            SkinOverlayApi.RemoveLayer(_owner,Probe.Renderer);
            _bindings.Clear(); if (Ink != null) UnityEngine.Object.Destroy(Ink); Ink = null;
            Probe.Dispose();
        }
    }
}
