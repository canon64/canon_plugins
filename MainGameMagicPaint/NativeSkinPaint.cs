using System;
using System.Collections.Generic;
using UnityEngine;

namespace MainGameMagicPaint
{
    // External items use the actual MagicPaint contact, raster and upload implementation.
    public sealed class NativeSkinContact:IDisposable
    {
        readonly MeshProbe _probe;
        readonly ChaControl _actor;
        public Renderer Renderer=>_probe.Renderer;
        public ChaControl Actor=>_actor;
        public bool IsSkin=>_actor!=null&&Renderer!=null&&(Renderer==_actor.rendFace||Renderer==_actor.rendBody);
        public NativeSkinContact(ChaControl actor,Renderer renderer){_probe=new MeshProbe(renderer);_actor=actor;}
        public bool Ray(Ray ray,float distance,out Vector3 point,out Vector3 normal,out float hitDistance)
        {
            point=normal=default;hitDistance=0;
            if(!IsSkin||!_probe.Ray(ray,distance,out var hit))return false;
            point=hit.Point;normal=hit.Normal;hitDistance=hit.Distance;return true;
        }
        public void Dispose()=>_probe.Dispose();
    }
    public sealed class NativeSkinMark
    {
        internal readonly List<KeyValuePair<int,uint>> Pixels=new List<KeyValuePair<int,uint>>();
        internal SkinReliefPatch Relief;
    }
    public sealed class NativeSkinLayer:IDisposable
    {
        readonly string _owner;
        readonly SkinSurface _surface;
        readonly PaintBuffer _mask;
        readonly List<int> _candidates=new List<int>();
        readonly List<NativeSkinMark> _marks=new List<NativeSkinMark>();
        readonly HashSet<int> _filled=new HashSet<int>();
        Material _reliefMaterial;
        float _reliefThickness=SkinReliefPatch.Thickness;
        public int InkPixels=>_surface.Buffer.InkPixels;
        public NativeSkinLayer(string owner,ChaControl actor,Renderer renderer)
        {
            if(!CanonGate.GateApi.CanRun(owner))throw new InvalidOperationException("肌への描画許可がありません");
            if(actor==null||(renderer!=actor.rendBody&&renderer!=actor.rendFace))throw new InvalidOperationException("肌以外には描画しません");
            _owner=owner;int size=renderer==actor.rendFace?1024:2048;
            _mask=new PaintBuffer(size);
            _surface=new SkinSurface(new MeshProbe(renderer),renderer==actor.rendFace,new PaintBuffer(size),owner,SkinOverlayApi.WaxOrder);
        }
        public NativeSkinMark Stamp(Vector3 point,Vector3 normal,float radius)
        {
            if(!CanonGate.GateApi.CanRun(_owner))throw new InvalidOperationException("肌への描画許可が失効しました");
            _surface.Probe.Stamp(_mask,point,normal,radius,PaintBuffer.Pack(184,5,7,255),1,false,.1f,_candidates);
            var mark=new NativeSkinMark();int rowTiles=_mask.Size/PaintBuffer.Tile;
            foreach(int tile in _mask.UploadTiles)
            {
                int x=tile%rowTiles*PaintBuffer.Tile,y=tile/rowTiles*PaintBuffer.Tile;
                for(int row=0;row<PaintBuffer.Tile;row++)for(int col=0;col<PaintBuffer.Tile;col++)
                {int at=(y+row)*_mask.Size+x+col;uint pixel=_mask.Pixels[at];if(pixel!=0)mark.Pixels.Add(new KeyValuePair<int,uint>(at,pixel));}
            }
            foreach(var pixel in mark.Pixels)_mask.Set(pixel.Key%_mask.Size,pixel.Key/_mask.Size,0);
            _mask.Commit();_mask.UploadTiles.Clear();_mask.Dirty=false;
            if(mark.Pixels.Count==0)throw new InvalidOperationException("接触点を肌へ描き込めません");
            _marks.Add(mark);Apply(mark);
            try
            {
                Flush();
                if(_reliefMaterial==null)
                {
                    var shader=Shader.Find("Standard")??Shader.Find("Legacy Shaders/Diffuse");
                    if(shader!=null)
                    {
                        _reliefMaterial=new Material(shader){name="Candle thin red wax",color=new Color(184/255f,5/255f,7/255f,1),renderQueue=2000};
                        if(_reliefMaterial.HasProperty("_Glossiness"))_reliefMaterial.SetFloat("_Glossiness",.42f);
                    }
                }
                if(_reliefMaterial!=null)mark.Relief=SkinReliefPatch.Create(_surface.Probe,point,normal,radius,_reliefMaterial,_reliefThickness);
                return mark;
            }
            catch{_marks.Remove(mark);Rebuild();try{Flush();}catch{}throw;}
        }
        void Apply(NativeSkinMark mark)
        {
            var buffer=_surface.Buffer;
            foreach(var p in mark.Pixels){_filled.Add(p.Key);buffer.Set(p.Key%buffer.Size,p.Key/buffer.Size,PaintBuffer.Blend(buffer.Pixels[p.Key],p.Value,(p.Value>>24)/255f,false));}
        }
        void Rebuild()
        {
            var buffer=_surface.Buffer;
            foreach(int at in _filled)buffer.Set(at%buffer.Size,at/buffer.Size,0);
            _filled.Clear();foreach(var mark in _marks)Apply(mark);
        }
        void Flush(){_surface.Flush();_surface.Buffer.Commit();}
        public void SetReliefThickness(float meters)
        {
            if(!CanonGate.GateApi.CanRun(_owner)||float.IsNaN(meters)||float.IsInfinity(meters))return;
            _reliefThickness=Math.Max(0,Math.Min(.005f,meters));
            foreach(var mark in _marks)mark.Relief?.SetThickness(_reliefThickness);
        }
        public void Remove(NativeSkinMark mark){if(!_marks.Remove(mark))return;mark.Relief?.Dispose();Rebuild();Flush();}
        public void Dispose(){foreach(var mark in _marks)mark.Relief?.Dispose();_surface.Dispose();if(_reliefMaterial!=null)UnityEngine.Object.Destroy(_reliefMaterial);_marks.Clear();_filled.Clear();}
    }
}
