using System;
using UnityEngine;
// Only the game host is substituted. All capture, mesh, material, texture and render callbacks
// are compiled byte-for-byte from the product sources into the real Unity 2019.4 player loop.
namespace BepInEx
{
    public class BepInPlugin : Attribute { public BepInPlugin(string a,string b,string c) { } }
    [AttributeUsage(AttributeTargets.Class,AllowMultiple=true)] public class BepInDependency : Attribute { public BepInDependency(string a,string b=null) { } }
    [AttributeUsage(AttributeTargets.Class,AllowMultiple=true)] public class BepInProcess : Attribute { public BepInProcess(string a) { } }
    public class BaseUnityPlugin : MonoBehaviour { public Configuration.ConfigFile Config = new Configuration.ConfigFile(); }
}
namespace BepInEx.Configuration
{
    public class ConfigEntry<T>
    {
        T value;
        public ConfigEntry(T initial) { value=initial; }
        public T Value { get=>value; set { this.value=value; SettingChanged?.Invoke(this,EventArgs.Empty); } }
        public event EventHandler SettingChanged;
    }
    public class ConfigDescription { public ConfigDescription(string text,object values) { } }
    public class AcceptableValueList<T> { public AcceptableValueList(params T[] values) { } }
    public class AcceptableValueRange<T> { public AcceptableValueRange(T min,T max) { } }
    public class ConfigFile { public ConfigEntry<T> Bind<T>(string group,string key,T value,object desc) => new ConfigEntry<T>(value); }
}
namespace CanonGate
{
    public static class GateApi
    {
        public const string PluginGuid="canon.gate";
        public enum SessionKind { None,H,Maker }
        public class SessionContext { public SessionKind Kind; public MonoBehaviour Scene; public ulong Id; }
        public static SessionContext Current;
        public static Action Start,Stop;
        public static IDisposable Register(string guid,Action start,Action stop) { Start=start; Stop=stop; return new Registration(); }
        class Registration : IDisposable { public void Dispose() { Start=Stop=null; } }
        public static Permit Capture(string guid) => new Permit();
        public struct Permit { public bool IsValid => Current!=null; }
    }
}
namespace MainGameUnifiedUi { public static class UnifiedUiApi { public static void RegisterTab(string id,string title,Action draw,int order) { } public static void UnregisterTab(string id) { } } }
namespace MainGameDesktopDisplay
{
    internal sealed class DesktopButtonInput : IDisposable
    {
        internal DesktopButtonInput(Action<bool,bool> button,Action<Vector2> axis){}
        internal void Connect(){}internal void Active(bool active){}public void Dispose(){}
    }
}
namespace VRGIN.Core
{
    public static class VR { public static bool Active => false; public static HeadCamera Camera => null; public class HeadCamera { public Transform Head; } public static ModeState Mode=>null;public class ModeState{public VRGIN.Controls.Controller Left,Right;} }
}
namespace Valve.VR
{
    public enum SteamVR_Input_Sources { Any,LeftHand,RightHand }
    public enum ETrackingResult { Running_OK,Uninitialized }
    public class SteamVR_Behaviour_Pose
    {
        public Action<SteamVR_Behaviour_Pose,SteamVR_Input_Sources,bool> onConnectedChangedEvent;
        public Action<SteamVR_Behaviour_Pose,SteamVR_Input_Sources,ETrackingResult> onTrackingChangedEvent;
    }
    public class SteamVR_Action_Boolean
    {
        public delegate void Edge(SteamVR_Action_Boolean action,SteamVR_Input_Sources source);
        public delegate void Active(SteamVR_Action_Boolean action,SteamVR_Input_Sources source,bool active);
        public void AddOnStateDownListener(Edge handler,SteamVR_Input_Sources source) { }
        public void RemoveOnStateDownListener(Edge handler,SteamVR_Input_Sources source) { }
        public void AddOnStateUpListener(Edge handler,SteamVR_Input_Sources source) { }
        public void RemoveOnStateUpListener(Edge handler,SteamVR_Input_Sources source) { }
        public void AddOnActiveChangeListener(Active handler,SteamVR_Input_Sources source) { }
        public void RemoveOnActiveChangeListener(Active handler,SteamVR_Input_Sources source) { }
    }
    public class SteamVR_Action_Vector2
    {
        public delegate void Change(SteamVR_Action_Vector2 action,SteamVR_Input_Sources source,Vector2 value,Vector2 delta);
        public void AddOnChangeListener(Change handler,SteamVR_Input_Sources source) { }
        public void RemoveOnChangeListener(Change handler,SteamVR_Input_Sources source) { }
    }
    public static class SteamVR_Actions { public static Actions legacy_emulate=new Actions();public class Actions{public SteamVR_Action_Boolean Grip_Press;public SteamVR_Action_Vector2 Axis0_2D;} }
    public static class SteamVR_Events {public class Action{public bool enabled;}public static Action InputFocusAction(System.Action<bool> handler)=>new Action();}
}
namespace VRGIN.Controls
{
    public class Controller
    {
        public Transform transform;public bool IsTracking;public Valve.VR.SteamVR_Behaviour_Pose Tracking;
        private Lock _Lock;
        public object GetComponent(Type type)=>null;
        public bool CanAcquireFocus()=>false;
        public bool TryAcquireFocus(out Lock focus){focus=null;return false;}
        public class Lock{public bool IsValid,IsInvalidating;public void SafeRelease(){} public void Release(){}}
    }
}
namespace MainGameTransformGizmo
{
    public enum GizmoMode { Move,Rotate }
    public class TransformGizmo : MonoBehaviour { public bool IsVisible; public void SetVisible(bool visible) { IsVisible=visible; } public void SetMode(GizmoMode mode) { } }
    public static class TransformGizmoApi
    {
        public static bool IsAvailable => false;
        public static TransformGizmo Attach(GameObject obj) => null;
        public static bool EnableCameraInputCaptureOnDrag(TransformGizmo gizmo) => false;
        public static bool DisableCameraInputCaptureOnDrag(TransformGizmo gizmo) => true;
    }
}
