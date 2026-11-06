using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public struct Vector2{public float x,y;}
    public struct Color{public static Color white=>new Color();}
    public struct Matrix4x4{public static Matrix4x4 identity=>new Matrix4x4();}
    public struct Rect
    {
        public float x,y,width,height;
        public Rect(float x,float y,float width,float height){this.x=x;this.y=y;this.width=width;this.height=height;}
    }
    public static class Screen{public static int width=1920,height=1080;}
    public static class Mathf{public static float Clamp(float v,float min,float max)=>Math.Max(min,Math.Min(max,v));}
    public enum EventType{Layout,Repaint,MouseDown,MouseDrag,MouseUp}
    public class Event{public static Event current=new Event();public EventType type,rawType;}
    public static class GUIUtility{public static int hotControl;}
    public sealed class Skin{public object box=new object();}
    public static class GUI
    {
        public static bool enabled=true;public static Matrix4x4 matrix;public static Color color;public static int depth;public static Skin skin=new Skin();
        public static Rect Window(int id,Rect rect,Action<int> draw,string title){draw(id);return rect;}
        public static void DragWindow(Rect title){}
    }
    public static class GUILayout
    {
        public static float? NextSlider;public static string ClickButton;public static int SliderCalls;
        public static void BeginVertical(params object[] options){}public static void EndVertical(){}
        public static void BeginHorizontal(){}public static void EndHorizontal(){}
        public static void Label(string label,params object[] options){}
        public static bool Toggle(bool value,string label)=>value;
        public static bool Button(string text,params object[] options){if(!GUI.enabled||text!=ClickButton)return false;ClickButton=null;return true;}
        public static string TextField(string text,params object[] options)=>text;
        public static float HorizontalSlider(float value,float min,float max,params object[] options)
        {SliderCalls++;if(!GUI.enabled||NextSlider==null)return value;float next=NextSlider.Value;NextSlider=null;GUIUtility.hotControl=333;return next;}
        public static object Width(float n)=>null;public static object Height(float n)=>null;public static object MinWidth(float n)=>null;public static object MaxWidth(float n)=>null;public static object ExpandWidth(bool b)=>null;
    }
}
namespace MainGameUiInputCapture
{
    public static class UiInputCaptureApi
    {
        public static bool Blocked,Dragging,Unlock;
        public static void Sync(string owner,string source,bool value)=>Dragging=value;
        public static void EndOwner(string owner)=>Dragging=false;
        public static void SetInputBlockRects(string owner,UnityEngine.Rect[] rects)=>Blocked=rects.Length>0;
        public static void ClearInputBlockRects(string owner)=>Blocked=false;
        public static void SetIdleCursorUnlock(string owner,bool value)=>Unlock=value;
    }
}
namespace MainGameRoundedLimbClothes
{
    public sealed partial class Plugin
    {
        public const string Guid="canon.kks.roundedlimbclothes";
        bool applicationFocused=true;
        internal bool Allowed=>Permit;
        public void TestOpen(RoundedLimbController c){BeginEditing(c);makerWindow=true;RegisterWindowInput();}
        public void TestGui(UnityEngine.EventType type){UnityEngine.Event.current.type=type;UnityEngine.Event.current.rawType=type;OnGUI();}
        public bool WindowOpen=>makerWindow;
    }
}
