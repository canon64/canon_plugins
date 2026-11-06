using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CanonGate;
using MapEnvironmentRuntime;
using UnityEngine;
using UnityEngine.AI;
using MainGameAiShoujoMode.Life;

namespace MainGameAiShoujoMode.Furniture
{
    internal sealed partial class AiFurnitureSession:IDisposable
    {
        private sealed class Placed { internal FurnitureInstance Data; internal FurnitureItem Item; internal GameObject Root; }
        internal sealed class BedOption { internal string Target,Name; internal PlacedBed Bed; }
        internal sealed class PlacedBed { internal string Instance; internal Transform Root; internal FurnitureSlot Slot; }
        private readonly SandboxHost _host;
        private readonly AiAnimationTrial _actor;
        private readonly Func<AiLifeController> _resident;
        private readonly FurnitureStore _store;
        private readonly FurnitureCatalog _catalog;
        private readonly Parameters _p;
        private readonly string _content;
        private readonly Dictionary<string,Placed> _placed=new Dictionary<string,Placed>(StringComparer.Ordinal);
        private readonly Dictionary<string,AssetBundle> _bundles=new Dictionary<string,AssetBundle>(StringComparer.Ordinal);
        private readonly FurnitureReservations _reservations=new FurnitureReservations();
        private readonly HashSet<BedRun> _beds=new HashSet<BedRun>();
        private GateApi.ActivityRegistration _activity;
        private GateApi.ActivityPermit _permit;
        private FurnitureLayout _layout;
        private bool _disposed,_broken;
        private string _status;
        private Bounds[] _blocked=Array.Empty<Bounds>();
        internal bool Editing=>_preview!=null;
        internal bool Busy=>Editing||_beds.Any();
        internal event Action Changed;
        internal AiFurnitureSession(SandboxHost host,AiAnimationTrial actor,Func<AiLifeController> resident,string content,FurnitureLayout snapshot)
        {
            _host=host;_actor=actor;_resident=resident;_content=content;
            _p=JsonCodec.FromJson<Parameters>(File.ReadAllText(Path.Combine(content,"Furniture","settings.json")));
            _catalog=FurnitureCatalog.Load(Path.Combine(content,"Furniture","catalog.json"));
            _store=new FurnitureStore(Path.Combine(BepInEx.Paths.GameRootPath,"UserData","AIShoujoMode","Furniture","ai-island.json"));
            _activity=GateApi.RegisterActivity(Plugin.Guid+".furniture",GateApi.ActivityKind.AiShoujoMode,()=>{if(_activity!=null)_permit=_activity.Capture();},Suspend,false);
            _activity.Begin(GateApi.AiShoujoMode?.Owner);
            _permit=_activity.Capture();
            try
            {
                Require();
                var saved=_store.Read();Restore(snapshot??saved);if(snapshot!=null)_store.Write(snapshot);
            }
            catch(Exception error){_broken=true;_status="家具の復元を停止しました："+error.GetBaseException().Message;}
            Application.focusChanged+=FocusChanged;
        }
        private bool Allowed=>!_disposed&&_permit.IsValid;
        private float N(string key)=>_p.N(key.StartsWith("furniture.",StringComparison.Ordinal)||key.StartsWith("math.",StringComparison.Ordinal)?key:"furniture."+key);
        private int I(string key)=>checked((int)N(key));
        private static Vector3 Vector(FurnitureVector v)=>new Vector3(v.x,v.y,v.z);
        private static FurnitureVector Value(Vector3 v)=>new FurnitureVector{x=v.x,y=v.y,z=v.z};
        private Quaternion SourceRotation
        {
            get
            {
                var origin=_host.FromIslandSourcePosition(Vector3.zero);
                return Quaternion.LookRotation(_host.FromIslandSourcePosition(Vector3.forward)-origin,_host.FromIslandSourcePosition(Vector3.up)-origin);
            }
        }
        private float SourceScale=>( _host.FromIslandSourcePosition(Vector3.right)-_host.FromIslandSourcePosition(Vector3.zero)).magnitude;
        private void Require(){if(!Allowed||_broken)throw new InvalidOperationException(_status??"家具を操作できる状態ではありません。");}
        private GameObject Create(FurnitureItem item)
        {
            if(!item.Available)throw new InvalidDataException(item.error??"この家具は変換されていません。");
            if(!_bundles.TryGetValue(item.bundle,out var bundle))
            {
                bundle=AssetBundle.LoadFromFile(Path.Combine(_content,"Furniture",item.bundle));
                if(bundle==null)throw new IOException("家具Bundleを読み込めません："+item.name);
                _bundles.Add(item.bundle,bundle);
            }
            var prefab=bundle.LoadAsset<GameObject>(item.asset);if(prefab==null)throw new IOException("家具Prefabがありません："+item.name);
            var root=UnityEngine.Object.Instantiate(prefab);root.name="配置家具："+item.name;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,_host.Island.LoadedScene);
            root.transform.localScale=Vector3.Scale(root.transform.localScale,Vector3.one*SourceScale);return root;
        }
        private void Pose(GameObject root,FurnitureInstance data)
        {root.transform.SetPositionAndRotation(_host.FromIslandSourcePosition(Vector(data.position)),SourceRotation*Quaternion.Euler(Vector(data.rotation)));}
        private static Bounds BoundsOf(GameObject root)
        {
            var renderers=root.GetComponentsInChildren<Renderer>(true);if(!renderers.Any())throw new InvalidDataException("家具の形状がありません。");
            var bounds=renderers.First().bounds;foreach(var renderer in renderers.Skip(Math.Sign(int.MaxValue)))bounds.Encapsulate(renderer.bounds);return bounds;
        }
        private void AddObstacle(Placed placed)
        {
            var obstacle=placed.Root.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;
            obstacle.center=Vector(placed.Item.center);obstacle.size=Vector(placed.Item.size);obstacle.carving=true;obstacle.carveOnlyStationary=true;
        }
        private void Restore(FurnitureLayout data)
        {
            data.Validate();var staged=new Dictionary<string,Placed>();
            try
            {
                foreach(var value in data.items)
                {
                    var item=_catalog.items.SingleOrDefault(i=>i.id==value.item)??throw new InvalidDataException("保存した家具が一覧にありません。");
                    var root=Create(item);var placed=new Placed{Data=value,Item=item,Root=root};staged.Add(value.id,placed);Pose(root,value);root.SetActive(false);
                }
                foreach(var value in staged.Values)AddObstacle(value);
                foreach(var value in _placed.Values)UnityEngine.Object.Destroy(value.Root);_placed.Clear();
                foreach(var pair in staged){_placed.Add(pair.Key,pair.Value);pair.Value.Root.SetActive(true);}
                _layout=FurnitureJson.Copy(data);Physics.SyncTransforms();CacheBounds();
            }
            catch{foreach(var value in staged.Values)if(!_placed.ContainsKey(value.Data.id))UnityEngine.Object.Destroy(value.Root);throw;}
        }
        internal FurnitureLayout Snapshot(){Require();if(Busy)throw new InvalidOperationException("家具の編集・ベッド利用を終えてから保存してください。");return FurnitureJson.Copy(_layout);}
        internal IReadOnlyList<Bounds> BlockedBounds()=>_blocked;
        private void CacheBounds(){_blocked=_placed.Values.Where(v=>v.Root!=null&&v.Root.activeSelf).Select(v=>BoundsOf(v.Root)).ToArray();}
        private bool Ground(Vector3 point,out RaycastHit hit,GameObject ignore=null)
        {
            hit=Physics.RaycastAll(point+Vector3.up*N("groundProbe")*SourceScale,Vector3.down,N("groundProbe")*SourceScale*N("math.two"),Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
                .Where(v=>v.collider.gameObject.scene==_host.Island.LoadedScene && !v.collider.transform.IsChildOf(ignore==null?_host.transform:ignore.transform)
                    && !_placed.Values.Any(p=>v.collider.transform.IsChildOf(p.Root.transform)))
                .OrderBy(v=>v.distance).FirstOrDefault();
            return hit.collider!=null&&Vector3.Angle(hit.normal,Vector3.up)<=N("maxSlope");
        }
        private string CheckPlacement(GameObject root,FurnitureItem item)
        {
            var bounds=BoundsOf(root);var groundPoint=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            if(!Ground(groundPoint,out var ground,root)||Mathf.Abs(ground.point.y-bounds.min.y)>N("supportTolerance"))return "家具を支える床面が見つかりません。";
            foreach(var x in new[]{bounds.min.x+N("overlapInset"),bounds.max.x-N("overlapInset")})
                foreach(var z in new[]{bounds.min.z+N("overlapInset"),bounds.max.z-N("overlapInset")})
                    if(!Ground(new Vector3(x,bounds.min.y,z),out var support,root)||Mathf.Abs(support.point.y-bounds.min.y)>N("supportTolerance"))return "家具の端を支える床面がありません。";
            foreach(var collider in root.GetComponentsInChildren<Collider>(true).Where(c=>!c.isTrigger))
                foreach(var other in Physics.OverlapBox(bounds.center,bounds.extents,Quaternion.identity,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                {
                    if(other.transform.IsChildOf(root.transform)||(_actor.Root!=null&&other.transform.IsChildOf(_actor.Root))||other.gameObject.scene!=_host.Island.LoadedScene)continue;
                    if(Physics.ComputePenetration(collider,collider.transform.position,collider.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out var distance)
                        &&distance>N("overlapInset"))return "地面・壁または他の家具と重なっています。";
                }
            foreach(var slot in (item.slots??Array.Empty<FurnitureSlot>()).Where(s=>s.status=="ready"))
            {
                var approach=root.transform.TransformPoint(Vector(slot.approach));
                if(_host.Session.Navigation==null||!_host.Session.Navigation.TryProject(approach,out var projected)||Vector3.Distance(projected,approach)>_host.Session.Settings.N("navigation.projectRadius"))return "ベッドの接近位置が歩ける床から離れています。";
                if(_actor.Root!=null&&!_host.Session.Navigation.TryCalculatePath(_actor.Root.position,projected,out _))return "女の子からベッドの接近位置へ到達できません。";
            }
            return null;
        }
        private FurnitureInstance Capture(string id,FurnitureItem item,GameObject root)=>new FurnitureInstance{id=id,item=item.id,
            position=Value(_host.ToIslandSourcePosition(root.transform.position)),rotation=Value((Quaternion.Inverse(SourceRotation)*root.transform.rotation).eulerAngles)};
        private void ChangedLayout(){Physics.SyncTransforms();CacheBounds();Changed?.Invoke();}
        private void FocusChanged(bool focused){if(!focused)CancelEdit();}
        internal void Suspend(){CancelEdit();foreach(var bed in _beds.ToArray())bed.Cancel("ベッド利用を中断しました。");}
        public void Dispose()
        {
            if(_disposed)return;Suspend();_disposed=true;Application.focusChanged-=FocusChanged;_activity?.Dispose();_activity=null;
            ClearThumbnails();
            foreach(var placed in _placed.Values)if(placed.Root!=null)UnityEngine.Object.Destroy(placed.Root);_placed.Clear();
            foreach(var bundle in _bundles.Values)if(bundle!=null)bundle.Unload(true);_bundles.Clear();
        }
    }
}
