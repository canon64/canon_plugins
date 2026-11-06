using System;
using System.Collections.Generic;
using UnityEngine;
namespace Valve.VR
{
    public interface ISteamVR_Action_In {}
    public class SteamVR_Action
    {
        public string Path;public ulong handle;
        public static T Create<T>(string path) where T:SteamVR_Action,new()=>new T{Path=path};
        public void Initialize(bool force){handle=SteamVR_Input.FailHandles?0ul:1ul;}
    }
    public class SteamVR_ActionSet
    {
        public ulong handle;public bool Active;public int Priority;
        public static T Create<T>(string path) where T:SteamVR_ActionSet,new()=>new T();
        public void FinishPreInitialize(){}public void Initialize(bool force){handle=SteamVR_Input.FailHandles?0ul:1ul;}
        public void Activate(SteamVR_Input_Sources source,int priority,bool disable){Active=true;Priority=priority;}
        public void Deactivate(SteamVR_Input_Sources source){Active=false;}
    }
    public static class SteamVR_Input
    {
        public static bool FailHandles;
        public static SteamVR_ActionSet[] actionSets=Array.Empty<SteamVR_ActionSet>();
        public static SteamVR_Action[] actions=Array.Empty<SteamVR_Action>();
        public static ISteamVR_Action_In[] actionsIn=Array.Empty<ISteamVR_Action_In>(),actionsNonPoseNonSkeletonIn=Array.Empty<ISteamVR_Action_In>();
        public static SteamVR_Action_Boolean[] actionsBoolean=Array.Empty<SteamVR_Action_Boolean>();
        public static SteamVR_Action_Vector2[] actionsVector2=Array.Empty<SteamVR_Action_Vector2>();
        public static void PreinitializeActionSetDictionaries(){} public static void PreinitializeActionDictionaries(){}
    }
    public enum SteamVR_Input_Sources { Any,LeftHand,RightHand }
    public enum ETrackingResult { Running_OK,Uninitialized }
    public class SteamVR_Behaviour_Pose
    {
        public Action<SteamVR_Behaviour_Pose,SteamVR_Input_Sources,bool> onConnectedChangedEvent;
        public Action<SteamVR_Behaviour_Pose,SteamVR_Input_Sources,ETrackingResult> onTrackingChangedEvent;
    }
    public class SteamVR_Action_Boolean : SteamVR_Action,ISteamVR_Action_In
    {
        public delegate void Edge(SteamVR_Action_Boolean a,SteamVR_Input_Sources s);
        public delegate void Active(SteamVR_Action_Boolean a,SteamVR_Input_Sources s,bool active);
        readonly Dictionary<SteamVR_Input_Sources,Edge> down=new(),up=new();
        readonly Dictionary<SteamVR_Input_Sources,Active> active=new();
        public bool FailSecondDown;
        public void AddOnStateDownListener(Edge h,SteamVR_Input_Sources s){if(FailSecondDown&&s==SteamVR_Input_Sources.RightHand)throw new Exception("injected binding failure");down.TryGetValue(s,out var old);down[s]=old+h;}
        public void AddOnStateUpListener(Edge h,SteamVR_Input_Sources s){up.TryGetValue(s,out var old);up[s]=old+h;}
        public void AddOnActiveChangeListener(Active h,SteamVR_Input_Sources s){active.TryGetValue(s,out var old);active[s]=old+h;}
        public void RemoveOnStateDownListener(Edge h,SteamVR_Input_Sources s){if(down.ContainsKey(s))down[s]-=h;}
        public void RemoveOnStateUpListener(Edge h,SteamVR_Input_Sources s){if(up.ContainsKey(s))up[s]-=h;}
        public void RemoveOnActiveChangeListener(Active h,SteamVR_Input_Sources s){if(active.ContainsKey(s))active[s]-=h;}
        public void Down(SteamVR_Input_Sources s){if(down.TryGetValue(s,out var h))h?.Invoke(this,s);}
        public void Up(SteamVR_Input_Sources s){if(up.TryGetValue(s,out var h))h?.Invoke(this,s);}
        public void SetActive(SteamVR_Input_Sources s,bool value){if(active.TryGetValue(s,out var h))h?.Invoke(this,s,value);}
        public int Count { get {int n=0;foreach(var h in down.Values)n+=h?.GetInvocationList().Length??0;foreach(var h in up.Values)n+=h?.GetInvocationList().Length??0;foreach(var h in active.Values)n+=h?.GetInvocationList().Length??0;return n;} }
    }
    public class SteamVR_Action_Vector2 : SteamVR_Action,ISteamVR_Action_In
    {
        public delegate void Change(SteamVR_Action_Vector2 a,SteamVR_Input_Sources s,Vector2 v,Vector2 d);
        readonly Dictionary<SteamVR_Input_Sources,Change> handlers=new();
        public void AddOnChangeListener(Change h,SteamVR_Input_Sources s){handlers.TryGetValue(s,out var old);handlers[s]=old+h;}
        public void RemoveOnChangeListener(Change h,SteamVR_Input_Sources s){if(handlers.ContainsKey(s))handlers[s]-=h;}
        public void Move(SteamVR_Input_Sources s,float y){if(handlers.TryGetValue(s,out var h))h?.Invoke(this,s,new(0,y),default);}
        public void Move(SteamVR_Input_Sources s,float x,float y){if(handlers.TryGetValue(s,out var h))h?.Invoke(this,s,new(x,y),default);}
        public int Count {get{int n=0;foreach(var h in handlers.Values)n+=h?.GetInvocationList().Length??0;return n;}}
    }
    public static class SteamVR_Actions {public static Actions legacy_emulate=new();public class Actions{public SteamVR_Action_Boolean Grip_Press=new();public SteamVR_Action_Vector2 Axis0_2D=new();}}
    public static class SteamVR_Events
    {
        public class Action {public bool enabled;internal System.Action<bool> Handler;public void Emit(bool focused){if(enabled)Handler(focused);}}
        public static Action Latest;
        public static Action InputFocusAction(System.Action<bool> handler)=>Latest=new(){Handler=handler};
    }
}
namespace VRGIN.Controls
{
    public class Controller
    {
        public object GetComponent(Type type)=>null;
        public Transform transform=new();public bool IsTracking=true;public Valve.VR.SteamVR_Behaviour_Pose Tracking=new();
        private Lock _Lock;
        public Lock Current {get=>_Lock;set=>_Lock=value;}
        public bool CanAcquireFocus()=>_Lock==null||!_Lock.IsValid;
        public bool TryAcquireFocus(out Lock focus){focus=null;if(_Lock!=null&&_Lock.IsValid)return false;focus=_Lock=new Lock();return true;}
        public class Lock
        {
            public bool IsValid=true,IsInvalidating;
            public void SafeRelease(){IsInvalidating=true;}
            public void Release(){IsValid=false;}
            public void NativeButtonsReleased(){if(IsInvalidating)Release();}
        }
    }
}
