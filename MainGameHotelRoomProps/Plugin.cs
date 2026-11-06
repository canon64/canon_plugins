using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using CanonGate;
using UnityEngine;
using UnityEngine.SceneManagement;
using Valve.VR;
using VRGIN.Controls;
using VRGIN.Core;

namespace MainGameHotelRoomProps
{
    [BepInPlugin(Guid,"Hotel Room VR Props","1.0.0")]
    [BepInDependency(GateApi.PluginGuid,"2.4.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid="canon.maingamehotelroomprops";
        private const string Marker="HotelRoomProps__",PropPrefix="PROP__";
        private GateApi.ActivityRegistration _activity;
        private GateApi.ActivityPermit _permit;
        private GameObject _room;
        private string _roomId;
        private LayoutStore _store;
        private bool _ready,_writable,_bound;
        private ConfigEntry<bool> _diagnostics;
        private ConfigEntry<float> _radius;
        private SteamVR_Action_Boolean _grip;
        private readonly List<Prop> _props=new List<Prop>();
        private readonly Dictionary<SteamVR_Input_Sources,Hold> _holds=new Dictionary<SteamVR_Input_Sources,Hold>();
        public string LastError {get;private set;}
        private bool CanAct => isActiveAndEnabled && _ready && _permit.IsValid && _room != null && _room.activeInHierarchy && Application.isFocused;
        private sealed class Prop {internal string Id;internal Transform Root;internal Transform Parent;internal Collider[] Colliders;}
        private sealed class Hold
        {
            internal Prop Prop;internal Controller Hand;internal Controller.Lock Focus;internal SteamVR_Behaviour_Pose Pose;
            internal bool[] ColliderStates;
        }
        private void Awake()
        {
            _diagnostics=Config.Bind("Diagnostics","Enabled",false,"追加診断。OFFではログ用の収集と出力を停止します。");
            _radius=Config.Bind("VR","GrabRadius",.14f,new ConfigDescription("グリップで家具を掴む距離（m）。",new AcceptableValueRange<float>(.03f,.4f)));
            _activity=GateApi.RegisterActivity(Guid,GateApi.ActivityKind.Preview,StartSession,StopSession);
            SceneManager.sceneLoaded+=SceneLoaded;SceneManager.sceneUnloaded+=SceneUnloaded;
        }
        private void SceneLoaded(Scene scene,LoadSceneMode mode)
        {
            var roots=scene.GetRootGameObjects().Where(r=>r.name.StartsWith(Marker,StringComparison.Ordinal)).ToArray();
            if (!roots.Any()) return;
            if (roots.Length != 1) {Fail(new InvalidDataException("客室ルートが重複しています。"));return;}
            StopRoom();_room=roots.Single();_roomId=_room.name.Substring(Marker.Length);
            // The activity decision happens once at this explicit room load event.
            if (_activity.IsActive) AttachRoom();else _activity.Begin(this);
        }
        private void SceneUnloaded(Scene scene)
        {if (_room == null || _room.scene == scene) {_activity?.End();StopRoom();_room=null;}}
        private void StartSession() {_permit=_activity.Capture();if (_room != null) AttachRoom();}
        private void StopSession() {StopRoom();_permit=default;}
        private void AttachRoom()
        {
            if (!_permit.IsValid || _room == null || _ready) return;
            try
            {
                _store=new LayoutStore(Path.Combine(Paths.GameRootPath,"UserData","HotelMode","RoomLayouts"),_roomId);
                foreach (var root in _room.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith(PropPrefix,StringComparison.Ordinal)))
                    _props.Add(new Prop{Id=root.name.Substring(PropPrefix.Length),Root=root,Parent=root.parent,Colliders=root.GetComponentsInChildren<Collider>(true)});
                if (_props.Select(p=>p.Id).Distinct().Count()!=_props.Count) throw new InvalidDataException("家具IDが重複しています。");
                var data=_store.Load();
                if (data != null)
                    foreach (var pose in data.props)
                    {
                        var p=_props.SingleOrDefault(v=>v.Id==pose.id);if (p==null) continue;
                        p.Root.localPosition=new Vector3(pose.position[0],pose.position[1],pose.position[2]);
                        p.Root.localRotation=new Quaternion(pose.rotation[0],pose.rotation[1],pose.rotation[2],pose.rotation[3]);
                    }
                var visibility=_room.GetComponent<RoomVisibility>() ?? _room.AddComponent<RoomVisibility>();
                visibility.Disabled=ReleaseHands;
                _writable=true;_ready=true;Physics.SyncTransforms();Connect();
            }
            catch (Exception ex) {Fail(ex);StopRoom();}
        }
        private void Connect()
        {
            if (!VR.Active) return;
            _grip=SteamVR_Actions.legacy_emulate.Grip_Press;
            if (_grip==null) throw new InvalidOperationException("VRグリップ入力がありません。");
            _bound=true;
            foreach (var source in Sources)
            {_grip.AddOnStateDownListener(GripDown,source);_grip.AddOnStateUpListener(GripUp,source);}
        }
        private static readonly SteamVR_Input_Sources[] Sources={SteamVR_Input_Sources.LeftHand,SteamVR_Input_Sources.RightHand};
        private static Controller Hand(SteamVR_Input_Sources source) => !VR.Active || VR.Mode==null ? null : source==SteamVR_Input_Sources.LeftHand ? VR.Mode.Left : source==SteamVR_Input_Sources.RightHand ? VR.Mode.Right : null;
        private void GripDown(SteamVR_Action_Boolean action,SteamVR_Input_Sources source)
        {
            if (!CanAct || _holds.ContainsKey(source)) return;
            var hand=Hand(source);if (hand==null || !hand.IsTracking) return;
            // Search only on a grip edge. Native parenting supplies tracked motion.
            var selected=_props.Where(p=>!string.Equals(p.Id,"bed_furniture",StringComparison.Ordinal)
                && p.Root!=null && p.Root.gameObject.activeInHierarchy && !_holds.Values.Any(h=>h.Prop==p))
                .SelectMany(p=>p.Colliders.Where(c=>c!=null && c.enabled && !c.isTrigger).Select(c=>new {Prop=p,Distance=(c.ClosestPoint(hand.transform.position)-hand.transform.position).sqrMagnitude,Volume=c.bounds.size.sqrMagnitude}))
                .Where(c=>c.Distance<=_radius.Value*_radius.Value).OrderBy(c=>c.Distance).ThenBy(c=>c.Volume).FirstOrDefault();
            if (selected==null || !hand.TryAcquireFocus(out var focus) || focus==null || !focus.IsValid) return;
            var hold=new Hold{Prop=selected.Prop,Hand=hand,Focus=focus,Pose=hand.Tracking,ColliderStates=selected.Prop.Colliders.Select(c=>c!=null && c.enabled).ToArray()};
            _holds.Add(source,hold);
            try
            {
                foreach (var c in hold.Prop.Colliders) if(c!=null)c.enabled=false;
                hold.Prop.Root.SetParent(hand.transform,true);
                if (hold.Pose!=null) {hold.Pose.onConnectedChangedEvent+=Connected;hold.Pose.onTrackingChangedEvent+=Tracking;}
            }
            catch (Exception ex) {Release(source,false);Fail(ex);}
        }
        private void GripUp(SteamVR_Action_Boolean action,SteamVR_Input_Sources source) {Release(source,true);}
        private void Connected(SteamVR_Behaviour_Pose pose,SteamVR_Input_Sources source,bool connected) {if(!connected)Release(source,true);}
        private void Tracking(SteamVR_Behaviour_Pose pose,SteamVR_Input_Sources source,ETrackingResult result) {if(result!=ETrackingResult.Running_OK)Release(source,true);}
        private void Release(SteamVR_Input_Sources source,bool save)
        {
            if (!_holds.TryGetValue(source,out var hold)) return;_holds.Remove(source);
            if(hold.Pose!=null){hold.Pose.onConnectedChangedEvent-=Connected;hold.Pose.onTrackingChangedEvent-=Tracking;}
            if (hold.Prop.Root!=null)hold.Prop.Root.SetParent(hold.Prop.Parent,true);
            for(int i=0;i<hold.Prop.Colliders.Length;i++)if(hold.Prop.Colliders[i]!=null)hold.Prop.Colliders[i].enabled=hold.ColliderStates[i];
            if(hold.Focus!=null && hold.Focus.IsValid)hold.Focus.SafeRelease();
            Physics.SyncTransforms();if(save)SaveLayout();
        }
        public void SaveLayout()
        {
            if (!_writable || _room==null) return;
            try
            {
                // Temporarily held objects still serialize in their authored room parent.
                var poses=_props.Where(p=>p.Root!=null).Select(p=>
                {
                    var pos=p.Parent.InverseTransformPoint(p.Root.position);var rot=Quaternion.Inverse(p.Parent.rotation)*p.Root.rotation;
                    return new PropPose{id=p.Id,position=new[]{pos.x,pos.y,pos.z},rotation=new[]{rot.x,rot.y,rot.z,rot.w}};
                }).ToArray();
                _store.Save(new RoomLayout{roomId=_roomId,props=poses});LastError=null;
            }
            catch(Exception ex){Fail(ex);}
        }
        private void OnApplicationFocus(bool focused){if(!focused)foreach(var source in Sources)Release(source,true);}
        private void ReleaseHands(){foreach(var source in Sources)Release(source,true);}
        private void StopRoom()
        {
            _ready=false;
            if(_room!=null){var visibility=_room.GetComponent<RoomVisibility>();if(visibility!=null)visibility.Disabled=null;}
            if (_bound){_bound=false;foreach(var source in Sources){_grip.RemoveOnStateDownListener(GripDown,source);_grip.RemoveOnStateUpListener(GripUp,source);}}
            foreach(var source in Sources)Release(source,false);
            if(_writable)SaveLayout();
            _grip=null;_props.Clear();_store=null;_writable=false;
        }
        private void Fail(Exception ex){LastError=ex.Message;if(_diagnostics.Value)Logger.LogError(ex);}
        private void OnDisable(){_activity?.End();StopRoom();}
        private void OnDestroy(){SceneManager.sceneLoaded-=SceneLoaded;SceneManager.sceneUnloaded-=SceneUnloaded;_activity?.Dispose();StopRoom();}
    }
    public sealed class RoomVisibility : MonoBehaviour
    {
        internal Action Disabled;
        private void OnDisable(){Disabled?.Invoke();}
    }
}
