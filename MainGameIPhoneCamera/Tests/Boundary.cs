using System;
using System.Collections.Generic;

// Boundary doubles: these tests execute the product's edge handlers, not Unity/SteamVR.
namespace UnityEngine
{
    public static class Application { public static bool isFocused = true; }
    public class Object { }
    public class MonoBehaviour { public Transform transform = new Transform(); }
    public class GameObject { public Transform transform = new Transform(); public T AddComponent<T>() where T:new()=>new T(); }
    public class Camera { public GameObject gameObject = new(); }
    public class Transform
    {
        public Vector3 position;
        public Transform parent;
        public void SetParent(Transform value, bool preserve) { if (!preserve) throw new Exception("pose not preserved"); parent = value; }
    }
    public struct Vector2 { public float x,y; public Vector2(float a,float b){x=a;y=b;} }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float a,float b,float c){x=a;y=b;z=c;}
        public float this[int i] => i==0?x:i==1?y:z;
        public float sqrMagnitude => x*x+y*y+z*z;
        public static Vector3 operator -(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>new Vector3(a.x+(b.x-a.x)*t,a.y+(b.y-a.y)*t,a.z+(b.z-a.z)*t);
    }
    public static class Mathf { public static float InverseLerp(float a,float b,float v)=>Math.Clamp((v-a)/(b-a),0,1); }
    public class Mesh
    {
        public string name; public Vector3[] vertices,normals; public Vector2[] uv; public int[] triangles;
        public void SetVertices(List<Vector3> v)=>vertices=v.ToArray();
        public void SetUVs(int channel,List<Vector2> v)=>uv=v.ToArray();
        public void SetTriangles(List<int> v,int submesh)=>triangles=v.ToArray();
        public void RecalculateNormals(){} public void RecalculateBounds(){}
    }
}
namespace Valve.VR
{
    public static class SteamVR_Events { public class Action { public bool enabled; } public static Action InputFocusAction(System.Action<bool> handler) => new Action(); }
    public enum SteamVR_Input_Sources { Any, LeftHand, RightHand }
    public enum ETrackingResult { Running_OK, Uninitialized }
    public class SteamVR_Behaviour_Pose
    {
        public delegate void Connected(SteamVR_Behaviour_Pose p, SteamVR_Input_Sources s, bool c);
        public delegate void Tracking(SteamVR_Behaviour_Pose p, SteamVR_Input_Sources s, ETrackingResult r);
        public Connected onConnectedChangedEvent; public Tracking onTrackingChangedEvent;
        public void Disconnect()=>onConnectedChangedEvent?.Invoke(this,SteamVR_Input_Sources.LeftHand,false);
        public void LoseTracking()=>onTrackingChangedEvent?.Invoke(this,SteamVR_Input_Sources.LeftHand,ETrackingResult.Uninitialized);
    }
    public class SteamVR_Action_Boolean
    {
        public delegate void StateDownHandler(SteamVR_Action_Boolean action,SteamVR_Input_Sources source);
        public delegate void StateUpHandler(SteamVR_Action_Boolean action,SteamVR_Input_Sources source);
        private readonly Dictionary<SteamVR_Input_Sources,StateDownHandler> down=new();
        private readonly Dictionary<SteamVR_Input_Sources,StateUpHandler> up=new();
        public void AddOnStateDownListener(StateDownHandler h,SteamVR_Input_Sources s){down.TryGetValue(s,out var old);down[s]=old+h;}
        public void AddOnStateUpListener(StateUpHandler h,SteamVR_Input_Sources s){up.TryGetValue(s,out var old);up[s]=old+h;}
        public void RemoveOnStateDownListener(StateDownHandler h,SteamVR_Input_Sources s){down.TryGetValue(s,out var old);down[s]=old-h;}
        public void RemoveOnStateUpListener(StateUpHandler h,SteamVR_Input_Sources s){up.TryGetValue(s,out var old);up[s]=old-h;}
        public void Down(SteamVR_Input_Sources s){down.TryGetValue(s,out var h);h?.Invoke(this,s);}
        public void Up(SteamVR_Input_Sources s){up.TryGetValue(s,out var h);h?.Invoke(this,s);}
        public int Count { get {int n=0;foreach(var h in down.Values)n+=h?.GetInvocationList().Length??0;foreach(var h in up.Values)n+=h?.GetInvocationList().Length??0;return n;} }
    }
    public class SteamVR_Action_Vector2
    {
        public delegate void ChangeHandler(SteamVR_Action_Vector2 a, SteamVR_Input_Sources s, UnityEngine.Vector2 axis, UnityEngine.Vector2 delta);
        private readonly Dictionary<SteamVR_Input_Sources, ChangeHandler> handlers = new();
        public void AddOnChangeListener(ChangeHandler h, SteamVR_Input_Sources s) { handlers.TryGetValue(s, out var old); handlers[s] = old + h; }
        public void RemoveOnChangeListener(ChangeHandler h, SteamVR_Input_Sources s) { handlers.TryGetValue(s, out var old); handlers[s] = old - h; }
        public void Change(SteamVR_Input_Sources s, float x, float y) { handlers.TryGetValue(s, out var h); h?.Invoke(this, s, new UnityEngine.Vector2(x, y), default); }
        public int Count { get { int n = 0; foreach (var h in handlers.Values) n += h?.GetInvocationList().Length ?? 0; return n; } }
    }
    public static class SteamVR_Actions
    {
        public static Actions legacy_emulate=new();
        public class Actions { public SteamVR_Action_Boolean Grip_Press=new(),Axis1_Press=new(); public SteamVR_Action_Vector2 Axis0_2D = new(); }
    }
}
namespace VRGIN.Controls
{
    public class Controller
    {
        public object GetComponent(Type type)=>null;
        public class Lock { public bool IsValid=true,IsInvalidating; public int Releases; public void SafeRelease(){IsInvalidating=true;Releases++;} public void Release(){IsValid=false;} }
        public UnityEngine.Transform transform=new();
        public bool IsTracking=true,Available=true;
        public Valve.VR.SteamVR_Behaviour_Pose Tracking=new();
        public Device Input=new();
        private Lock _Lock;
        public Lock LastLock {get=>_Lock;set=>_Lock=value;}
        public bool TryAcquireFocus(out Lock focus){focus=Available&&(_Lock==null||!_Lock.IsValid)?new Lock():null;if(focus!=null)_Lock=focus;return focus!=null;}
        public class Device { public int Pulses; public void TriggerHapticPulse(ushort duration){Pulses++;} }
    }
}
namespace VRGIN.Core
{
    public static class VR
    {
        public static bool Active=true;
        public static CameraClass Camera=new();
        public class CameraClass { public UnityEngine.Transform Head=new(); }
        public static ModeClass Mode=new();
        public class ModeClass { public VRGIN.Controls.Controller Left=new(),Right=new(); }
    }
}
namespace MainGameIPhoneCamera
{
    internal sealed class PhoneZoomMotion
    {
        internal float Input;
        private Action<float> changed;
        internal void Initialize(UnityEngine.Camera camera, Action<float> callback) { changed=callback; }
        internal void Drive(float input, float fov) { Input=input; }
        internal void StopMotion() { Input=0; }
        internal void Advance(float zoom) { changed(zoom); }
    }
    internal sealed class PhoneButtonInput : IDisposable
    {
        private readonly Action _confirm, _back;
        internal bool Enabled;
        internal PhoneButtonInput(Action confirm, Action back) { _confirm = confirm; _back = back; }
        internal void Connect() { }
        internal void Active(bool active) { Enabled = active; }
        internal void A() { if (Enabled) _confirm(); }
        internal void B() { if (Enabled) _back(); }
        public void Dispose() { Enabled = false; }
    }
    public sealed partial class Plugin
    {
        private bool CanAct=true;
        private UnityEngine.GameObject _root=new();
        private UnityEngine.MonoBehaviour _scene=new();
        private Setting _grabRadius=new();
        private string _status;
        private Log Logger=new();
        private class Setting{ public float Value=.14f; }
        private class Log{public void LogInfo(string s){} public void LogWarning(string s){} }
        internal int Photos;
        private readonly CameraAppState _app = new();
        private readonly PhoneState _phone = CameraPhone();
        private static PhoneState CameraPhone() { var state = new PhoneState(); state.Show(PhonePage.Camera, DateTime.UtcNow); return state; }
        internal int Confirms, Backs;
        private void PhoneGrabbed() { _phone.Grab(DateTime.UtcNow); }
        private void PhoneReleased() { _phone.Release(DateTime.UtcNow); }
        private void TriggerPhone() { ShutterAction(); }
        internal bool ViewerPan;
        private void SetViewerPan(bool pressed) { ViewerPan = pressed; }
        private void ConfirmPhone() { Confirms++; }
        private void BackPhone() { Backs++; }
        private void RunPhoneInput(Action action) { action(); }
        private UnityEngine.Camera _phoneCamera = new();
        private Setting _fov = new();
        private string _zoomText;
        private void PhoneAxis(float x, float y) { CameraZoomAxis(x, y); }
        private Ui _cameraUi = null;
        private sealed class Ui { internal void Apply(CameraAppState app) { } internal void Zoom(float zoom) { } }
        private void ApplyCameraZoom() { }
        private void ShutterAction() { if (_app.Mode == CaptureMode.Photo) CapturePhoto(); else if (_app.Phase == CapturePhase.Recording) _app.BeginSaving(); else _app.BeginRecording(); }
        internal int Recalls;
        internal bool FailSpawn;
        private void SpawnPhone() { HidePhoneGizmo(); ReleasePhone(); Recalls++; _root = FailSpawn ? null : _root ?? new UnityEngine.GameObject(); }
        private void DestroyPhone() { ClearPhoneGizmo(); ReleasePhone(); _root = null; }
        private void CapturePhoto(){Photos++;HapticPhoto();}
        private void ReportError(string s,Exception e){throw new Exception(s,e);}
    }
}
