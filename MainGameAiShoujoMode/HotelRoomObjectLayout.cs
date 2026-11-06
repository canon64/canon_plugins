using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapEnvironmentRuntime;
using MainGameTransformGizmo;
using MainGameUiInputCapture;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    public sealed class HotelObjectTransform
    {
        public string id;
        public Vector3 position, rotation, scale;
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(id) || new[] {position.x,position.y,position.z,rotation.x,rotation.y,rotation.z,scale.x,scale.y,scale.z}
                .Any(v=>float.IsNaN(v)||float.IsInfinity(v)) || scale.x<=default(float)||scale.y<=default(float)||scale.z<=default(float))
                throw new InvalidDataException("家具の配置データが不正です。");
        }
    }
    public sealed class HotelObjectLayoutData
    {
        public string version = "v1", room;
        public HotelObjectTransform[] objects;
        public void Validate(string expected)
        {
            if(version!="v1"||room!=expected||objects==null||objects.Any(o=>o==null)||objects.Select(o=>o.id).Distinct().Count()!=objects.Length)
                throw new InvalidDataException("この部屋の配置データを読み込めません。元の保存ファイルは保持します。");
            foreach(var obj in objects)obj.Validate();
        }
    }
    public sealed class HotelObjectLayoutStore
    {
        private readonly string _path,_room;
        public HotelObjectLayoutStore(string directory,string room)
        {
            if(room!="reception-suite"&&!HotelRoomCards.Rooms.Contains(room))throw new ArgumentException("客室の識別子が不正です。");
            _room=room;_path=Path.Combine(directory,room+".json");
        }
        public HotelObjectLayoutData Load()
        {
            if(!File.Exists(_path))return new HotelObjectLayoutData{room=_room,objects=Array.Empty<HotelObjectTransform>()};
            var data=JsonCodec.FromJson<HotelObjectLayoutData>(File.ReadAllText(_path));
            if(data==null)throw new InvalidDataException("家具配置の保存データが空です。");
            data.Validate(_room);return data;
        }
        public void Save(HotelObjectLayoutData data)
        {
            data.Validate(_room);Load(); // Preserve externally-corrupted saves as well.
            Directory.CreateDirectory(Path.GetDirectoryName(_path));var temporary=_path+".tmp";
            try
            {
                using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
                using(var writer=new StreamWriter(stream))
                {writer.Write(JsonCodec.ToJson(data,true));writer.Flush();stream.Flush(true);}
                JsonCodec.FromJson<HotelObjectLayoutData>(File.ReadAllText(temporary)).Validate(_room);
                if(File.Exists(_path))File.Replace(temporary,_path,_path+".bak");else File.Move(temporary,_path);
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
    }
    // Queries only on a click. Persistence runs at drag completion, never by transform polling.
    internal sealed class HotelRoomObjectLayout : IDisposable
    {
        private const string Prefix="EDIT__";
        private const string ActivityGuid="com.kks.maingame.aishoujomode.hotel-layout";
        private readonly Dictionary<string,Transform> _objects;
        private readonly CanonGate.GateApi.ActivityRegistration _activity;
        private readonly MonoBehaviour _owner;
        private readonly Parameters _p;
        private readonly string _room;
        private readonly HotelObjectLayoutStore _store;
        private readonly Action _changed;
        private TransformGizmo _gizmo;
        private Transform _selected;
        private bool _broken,_disposed;
        internal bool Editing { get; private set; }
        private string _status;
        internal HotelRoomObjectLayout(MonoBehaviour owner,Transform root,string room,Parameters p,Action changed)
        {
            _owner=owner;_p=p;_room=room;_changed=changed;
            _store=new HotelObjectLayoutStore(Path.Combine(BepInEx.Paths.GameRootPath,"UserData","HotelMode","FurnitureLayouts"),room);
            _objects=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith(Prefix,StringComparison.Ordinal)).ToDictionary(t=>t.name,t=>t);
            _activity=CanonGate.GateApi.RegisterActivity(ActivityGuid,CanonGate.GateApi.ActivityKind.Preview,()=>{},StopEditing,false);
            try
            {
                    var saved=_store.Load();
                    foreach(var value in saved.objects)
                        if(_objects.TryGetValue(value.id,out var target))
                        {target.localPosition=value.position;target.localEulerAngles=value.rotation;target.localScale=value.scale;}
                    Physics.SyncTransforms();_changed?.Invoke();
            }
            catch(Exception ex){_broken=true;_status=ex.GetBaseException().Message;}
        }
        internal void Draw()
        {
            if(!_objects.Any())return;
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("家具・小物の配置");
            if(GUILayout.Button(Editing?"配置編集を終了":"クリックとギズモで配置編集",GUILayout.ExpandWidth(false)))
            {
                if(Editing)StopEditing();
                else if(!_broken)
                {
                    if(CanonGate.GateApi.Current==null)_activity.Begin(_owner);
                    if(CanonGate.GateApi.Current?.IsHotelMode==true||_activity.Capture().IsValid)Editing=true;
                    else _status="配置編集の権限がありません。canon_gate のホテル配置編集許可を確認してください。";
                }
            }
            if(Editing)
            {
                GUILayout.Label("画面の家具をクリック。矢印で移動、円で回転。離すと即時保存。");
                if(_selected!=null)
                {
                    GUILayout.Label("選択中："+_selected.name.Substring(Prefix.Length));
                    GUILayout.BeginHorizontal();
                    foreach(var mode in new[]{GizmoMode.Move,GizmoMode.Rotate,GizmoMode.Scale})
                        if(GUILayout.Button(mode==GizmoMode.Move?"移動":mode==GizmoMode.Rotate?"回転":"拡大縮小",GUILayout.ExpandWidth(false)))_gizmo.SetMode(mode);
                    GUILayout.EndHorizontal();
                    if(GUILayout.Button("現在の配置を保存",GUILayout.ExpandWidth(false)))Save();
                }
            }
            if(!string.IsNullOrWhiteSpace(_status))GUILayout.Label(_status);
            GUILayout.EndVertical();
        }
        internal void HandlePointer(Camera camera)
        {
            var e=Event.current;
            if(!Editing||_disposed||!Application.isFocused||camera==null||e.type!=EventType.MouseDown||e.button!=_p.I("math.zero")
                ||_gizmo?.IsPointerOverHandle==true||UiInputCaptureApi.IsMouseInputBlocked())return;
            var mouse=new Vector3(e.mousePosition.x,Screen.height-e.mousePosition.y,default(float));
            var ray=camera.ScreenPointToRay(mouse);
            var hit=Physics.RaycastAll(ray,_p.N("hotel.layoutReach"),~(_p.I("math.one")<<_p.I("hotel.layoutGizmoLayer")),QueryTriggerInteraction.Collide)
                .OrderBy(h=>h.distance).FirstOrDefault(h=>FindEditable(h.collider.transform)!=null);
            if(hit.collider==null)return;
            var target=FindEditable(hit.collider.transform);
            if(target==_selected)return;
            ReleaseSelection();
            var permit=_activity.Capture();
            _gizmo=permit.IsValid?TransformGizmoApi.AttachForActivity(target.gameObject,permit):TransformGizmoApi.Attach(target.gameObject);
            if(_gizmo==null){_status="ギズモを開始できません。";return;}
            _selected=target;_gizmo.DragStateChanged+=DragChanged;
            _gizmo.TransformCommitted+=TransformChanged;
            TransformGizmoApi.EnableCameraInputCaptureOnDrag(_gizmo);_gizmo.SetVisible(true);e.Use();
        }
        private Transform FindEditable(Transform target)
        {
            for(var t=target;t!=null;t=t.parent)
                if(_objects.TryGetValue(t.name,out var known)&&known==t)return t;
            return null;
        }
        private void DragChanged(bool dragging){if(!dragging){Physics.SyncTransforms();Save();}}
        private void TransformChanged(){Physics.SyncTransforms();Save();}
        private bool Save()
        {
            if(_broken||_disposed)return false;
            try
            {
                var data=new HotelObjectLayoutData{room=_room,objects=_objects.Select(p=>new HotelObjectTransform{
                    id=p.Key,position=p.Value.localPosition,rotation=p.Value.localEulerAngles,scale=p.Value.localScale}).ToArray()};
                _store.Save(data);_changed?.Invoke();
                _status="配置を保存しました。次回の入室時にも復元します。";return true;
            }
            catch(Exception ex){_status="配置を保存できません："+ex.GetBaseException().Message;return false;}
        }
        private void ReleaseSelection()
        {
            if(_gizmo!=null)
            {
                _gizmo.SetVisible(false);_gizmo.DragStateChanged-=DragChanged;_gizmo.TransformCommitted-=TransformChanged;
                TransformGizmoApi.DisableCameraInputCaptureOnDrag(_gizmo);UnityEngine.Object.Destroy(_gizmo);
            }
            _gizmo=null;_selected=null;
        }
        internal void StopEditing(){ReleaseSelection();Editing=false;}
        internal void Suspend(){StopEditing();_activity.End();}
        public void Dispose()
        {
            if(_disposed)return;StopEditing();_activity.Dispose();_disposed=true;
        }
    }
}
