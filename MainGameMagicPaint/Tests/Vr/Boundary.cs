using System;
using UnityEngine;
using Valve.VR;
using VRGIN.Controls;
using VRGIN.Core;
namespace UnityEngine
{
    public sealed class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int order){} }
    public class Object { public static void Destroy(object value) { } }
    public class MonoBehaviour : Object { public bool enabled=true; public Transform transform=new Transform(); }
    public class GameObject { public Transform transform=new Transform(); public T AddComponent<T>() where T:new()=>new T(); }
    public class Transform { public Vector3 position; public void SetParent(Transform parent,bool world) { } }
    public struct Vector3 { public float x,y,z; public static float Distance(Vector3 a,Vector3 b)=>(float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)+(a.z-b.z)*(a.z-b.z)); }
    public struct Vector2 { public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} }
    public static class Mathf { public static float Abs(float x)=>Math.Abs(x);public static float Sign(float x)=>Math.Sign(x);public static float InverseLerp(float a,float b,float x)=>Math.Clamp((x-a)/(b-a),0,1); }
    public static class Time { public static float unscaledDeltaTime=.01f; }
}
namespace Valve.VR
{
    public enum SteamVR_Input_Sources { Any,LeftHand,RightHand }
    public enum ETrackingResult { Running_OK,Uninitialized }
    public class SteamVR_Action_Boolean
    {
        public void AddOnStateDownListener(Action<SteamVR_Action_Boolean,SteamVR_Input_Sources> a,SteamVR_Input_Sources s) { }
        public void AddOnStateUpListener(Action<SteamVR_Action_Boolean,SteamVR_Input_Sources> a,SteamVR_Input_Sources s) { }
        public void AddOnActiveChangeListener(Action<SteamVR_Action_Boolean,SteamVR_Input_Sources,bool> a,SteamVR_Input_Sources s) { }
        public void RemoveOnStateDownListener(Action<SteamVR_Action_Boolean,SteamVR_Input_Sources> a,SteamVR_Input_Sources s) { }
        public void RemoveOnStateUpListener(Action<SteamVR_Action_Boolean,SteamVR_Input_Sources> a,SteamVR_Input_Sources s) { }
        public void RemoveOnActiveChangeListener(Action<SteamVR_Action_Boolean,SteamVR_Input_Sources,bool> a,SteamVR_Input_Sources s) { }
    }
    public class SteamVR_Action_Vector2
    {
        public void AddOnChangeListener(Action<SteamVR_Action_Vector2,SteamVR_Input_Sources,Vector2,Vector2> a,SteamVR_Input_Sources s) { }
        public void RemoveOnChangeListener(Action<SteamVR_Action_Vector2,SteamVR_Input_Sources,Vector2,Vector2> a,SteamVR_Input_Sources s) { }
        public Vector2 GetAxis(SteamVR_Input_Sources s)=>new Vector2();
    }
    public static class SteamVR_Actions { public static class legacy_emulate { public static SteamVR_Action_Boolean Grip_Press=new SteamVR_Action_Boolean(),Axis1_Press=new SteamVR_Action_Boolean(); } }
    public static class SteamVR_Events { public class Action { public bool enabled; } public static Action InputFocusAction(System.Action<bool> callback)=>new Action(); }
    public class SteamVR_Behaviour_Pose
    {
        public event Action<SteamVR_Behaviour_Pose,SteamVR_Input_Sources,bool> onConnectedChangedEvent;
        public event Action<SteamVR_Behaviour_Pose,SteamVR_Input_Sources,ETrackingResult> onTrackingChangedEvent;
    }
}
namespace VRGIN.Controls
{
    public class Controller
    {
        public Transform transform=new Transform(); public bool IsTracking=true; public SteamVR_Behaviour_Pose Tracking=new SteamVR_Behaviour_Pose();
        public class Lock { public bool IsValid=true;public int Releases;public void SafeRelease(){Releases++;IsValid=false;} }
    }
}
namespace VRGIN.Core { public static class VR { public static bool Active=true; public static Rig Mode=new Rig();public class Rig {public Controller Left=new Controller(),Right=new Controller();} } }
namespace CanonPlugins.Shared { public static class VrItemFocus { public static Controller.Lock Last;public static bool TryAcquire(Controller hand,out Controller.Lock focus){Last=focus=new Controller.Lock();return true;} } }
namespace MainGameMagicPaint
{
    internal sealed class PaintButtons : IDisposable
    {
        readonly Action<SteamVR_Input_Sources> _setting;internal SteamVR_Action_Vector2 Axis=new SteamVR_Action_Vector2();
        internal PaintButtons(Action<SteamVR_Input_Sources> setting,Action<SteamVR_Input_Sources> clear,Action<SteamVR_Input_Sources> down,Action<SteamVR_Input_Sources> up){_setting=setting;}
        internal void Connect(){}internal void Activate(SteamVR_Input_Sources s,bool value){}public void Dispose(){}internal void Setting(SteamVR_Input_Sources s)=>_setting(s);
    }
    sealed class TestModel { internal GameObject Root=new GameObject(); }
    sealed class TestHud { internal void Hide(){} }
    public sealed partial class Plugin : MonoBehaviour
    {
        bool CanAct=true,_trigger,_contact,_settingsVisible,_inspectionPending;int _selected,_undo,_redo,_colors,_ends,_captures,_queries;float _adjusted;
        bool _throwEnd;readonly VrPaintState _vrPaint=new VrPaintState();readonly StickState _stick=new StickState();PaintSetting _setting=PaintSetting.Width;
        TestModel _model=new TestModel(); TestHud _hud=new TestHud();MonoBehaviour _scene=new MonoBehaviour();string _status;
        bool TraceEnabled=>false; DiagnosticTrace _trace; void TraceInput(string a,SteamVR_Input_Sources s,bool value){}void TraceGeometry(string reason){}void TraceSample(){}
        void HideGizmo(){}void SetDesktop(bool value){}void SaveConfig(){}void Fail(string message,Exception ex){}void UpdateHud(){}
        void StopPaintPress(){_trigger=false;_vrPaint.StopPress();}void ResetContacts(){_contact=false;}
        void EndStroke(){_ends++;if(_throwEnd)throw new InvalidOperationException("flush failure");}
        void RequestInspectionCapture(string reason){_captures++;}void InspectionInput(bool accepted){}
        void Undo(bool redo){if(redo)_redo++;else _undo++;}void ClearDrawing(){}void ChangeTool(PaintTool tool){StopPaintPress();ResetContacts();EndStroke();}
        void ToggleSettings(){_settingsVisible=!_settingsVisible;_stick.BlockUntilNeutral();}
        void SelectSetting(int direction){_selected+=direction;}void ChangeColor(int direction){_colors+=direction;}
        void AdjustSetting(float seconds){_adjusted+=seconds;}void TickTool(){_queries++;}
        internal void Connect()=>ConnectVr();internal void Grip(SteamVR_Input_Sources s)=>GripDown(null,s);internal void Ungrip(SteamVR_Input_Sources s)=>GripUp(null,s);
        internal void Down(SteamVR_Input_Sources s)=>TriggerDown(null,s);internal void Up(SteamVR_Input_Sources s)=>TriggerUp(null,s);
        internal void Stick(float x,float y,SteamVR_Input_Sources s)=>Axis(null,s,new Vector2(x,y),new Vector2());
        internal void A(SteamVR_Input_Sources s)=>_buttons.Setting(s);
        internal void LoseTracking()=>Tracking(null,_handSource,ETrackingResult.Uninitialized);
        internal bool Held=>_hand!=null&&_vrPaint.Held;internal bool Paint=>_trigger&&_vrPaint.CanPaint(true);
        internal bool Settings=>_settingsVisible;internal int Selection=>_selected;internal int UndoCount=>_undo;internal int RedoCount=>_redo;internal float Adjustment=>_adjusted;
        internal void ThrowOnEnd()=>_throwEnd=true;internal int Captures=>_captures;internal int Queries=>_queries;
    }
}
