using System;
using System.Collections.Generic;
using UnityEngine;

namespace MainGameCandle
{
    public sealed partial class Plugin
    {
        readonly List<WaxSurface> _surfaces=new List<WaxSurface>();
        readonly List<FlyingDrop> _flying=new List<FlyingDrop>();
        readonly Queue<WaxMark> _deposits=new Queue<WaxMark>();
        readonly Dictionary<Renderer,WaxTextureLayer> _waxLayers=new Dictionary<Renderer,WaxTextureLayer>();
        Material _depositMaterial;
        Mesh _dropMesh;
        sealed class FlyingDrop {internal GameObject Object;internal Vector3 Position,Velocity;internal float Age,Volume;}
        void RefreshSurfaces()
        {
            if(!CanAct)return;
            ClearSurfaces();
            // One explicit acquisition on recall/refresh. No scene enumeration in the loop.
            foreach(var actor in FindObjectsOfType<ChaControl>())
            {
                foreach(var renderer in new[]{actor.rendFace,actor.rendBody})
                    if(renderer!=null&&!_surfaces.Exists(s=>s.Renderer==renderer))_surfaces.Add(new WaxSurface(renderer,actor));
            }
            _status="付着対象を取得しました。";
        }
        void ClearSurfaces(){foreach(var s in _surfaces)s.Dispose();_surfaces.Clear();}
        void EnsureWaxResources()
        {
            if(_depositMaterial==null)
            {
                _depositMaterial=new Material(_model.Wax){name="Deposited candle wax"};
                WaxMaterial.MakeOpaque(_depositMaterial,WaxMaterial.Red);
            }
            if(_dropMesh==null)_dropMesh=CandleModel.Drop();
        }
        void SpawnDrop(float volume=1)
        {
            EnsureWaxResources();
            if(_flying.Count>=CandleFlow.FlyingLimit){DestroyDrop(_flying[0]);_flying.RemoveAt(0);}
            var o=new GameObject("Falling candle wax",typeof(MeshFilter),typeof(MeshRenderer));o.transform.SetParent(_scene.transform,false);
            // The body rim supplies the drip origin independently of the wick/flame height.
            Vector3 start=_model.WaxDropPosition;
            o.transform.position=start;o.transform.localScale=new Vector3(.0022f,.0035f,.0022f)*(float)Math.Pow(volume,1d/3);
            o.GetComponent<MeshFilter>().sharedMesh=_dropMesh;o.GetComponent<MeshRenderer>().sharedMaterial=_depositMaterial;
            _flying.Add(new FlyingDrop{Object=o,Position=start,Velocity=Vector3.down*.02f,Volume=volume});
        }
        void AdvanceFlying(float dt)
        {
            for(int i=_flying.Count-1;i>=0;i--)
            {
                var drop=_flying[i];Vector3 next=drop.Position+drop.Velocity*dt+Physics.gravity*(.5f*dt*dt);drop.Velocity+=Physics.gravity*dt;drop.Age+=dt;
                Vector3 delta=next-drop.Position;float length=delta.magnitude;
                if(length>1e-7f&&FindWaxHit(new Ray(drop.Position,delta/length),length,out var hit))
                {
                    try{Deposit(hit,drop.Volume);}catch(Exception ex){_status="蝋の付着失敗："+ex.Message;}
                    DestroyDrop(drop);_flying.RemoveAt(i);continue;
                }
                if(drop.Age>=3||drop.Object==null){DestroyDrop(drop);_flying.RemoveAt(i);continue;}
                drop.Position=next;drop.Object.transform.position=next;
            }
        }
        bool FindWaxHit(Ray ray,float distance,out WaxHit hit)
        {
            hit=default;bool found=false;float best=distance;
            foreach(var surface in _surfaces)
                if(surface.Ray(ray,best,out var candidate)){best=candidate.Distance;hit=candidate;found=true;}
            return found;
        }
        void Deposit(WaxHit hit,float volume=1)
        {
            if(hit.Surface==null||!hit.Surface.IsSkin)return;
            var renderer=hit.Surface.Renderer;
            if(!_waxLayers.TryGetValue(renderer,out var layer)){layer=new WaxTextureLayer(renderer,hit.Surface.Actor);layer.SetThickness(_waxThickness.Value*.001f);_waxLayers.Add(renderer,layer);}
            var mark=layer.Stamp(hit.Surface,hit,.0048f*(float)Math.Sqrt(volume));
            while(_deposits.Count>=CandleFlow.DepositLimit){var old=_deposits.Dequeue();old.Layer.Remove(old);}
            _deposits.Enqueue(mark);
            if(hit.Surface!=null&&hit.Surface.IsSkin)
                try{React(hit.Surface.Actor);}catch(Exception ex){_status="蝋は付着しました。反応処理失敗："+ex.Message;}
        }
        static void DestroyDrop(FlyingDrop drop){if(drop.Object!=null){drop.Object.SetActive(false);UnityEngine.Object.Destroy(drop.Object);}}
        void ClearFlying(){foreach(var d in _flying)DestroyDrop(d);_flying.Clear();_flow.Reset();}
        void ClearWax()
        {
            ClearFlying();_deposits.Clear();
            foreach(var layer in _waxLayers.Values)layer.Dispose();_waxLayers.Clear();
            if(_depositMaterial!=null)Destroy(_depositMaterial);_depositMaterial=null;
            if(_dropMesh!=null)Destroy(_dropMesh);_dropMesh=null;
        }
    }
}
