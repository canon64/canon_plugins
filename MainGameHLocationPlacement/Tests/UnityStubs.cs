using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using N = System.Numerics;

namespace UnityEngine
{
    public class Object { public static void Destroy(Object obj) { if (obj is GameObject go) go.transform.SetParent(null, true); } }
    public class GameObject : Object
    {
        public readonly Transform transform;
        public string name;
        public HideFlags hideFlags;
        public GameObject(string name = "") { this.name = name; transform = new Transform { gameObject = this }; }
    }
    public enum HideFlags { HideAndDontSave }
    public class Transform : Object
    {
        public GameObject gameObject;
        public Vector3 localScale = Vector3.one, localPosition;
        public Quaternion localRotation = Quaternion.identity;
        private Transform _parent;
        private readonly List<Transform> _children = new List<Transform>();
        public Transform parent => _parent;
        public int childCount => _children.Count;
        public Transform GetChild(int index) => _children[index];
        public Vector3 position { get => _parent == null ? localPosition : _parent.position + _parent.rotation * localPosition; set => localPosition = _parent == null ? value : Quaternion.Inverse(_parent.rotation) * (value - _parent.position); }
        public Quaternion rotation { get => _parent == null ? localRotation : _parent.rotation * localRotation; set => localRotation = _parent == null ? value : Quaternion.Inverse(_parent.rotation) * value; }
        public Vector3 localEulerAngles { get => localRotation.eulerAngles; set => localRotation = Quaternion.Euler(value); }
        public void SetPositionAndRotation(Vector3 p, Quaternion q) { position = p; rotation = q; }
        public void SetParent(Transform value, bool keepWorld)
        { var p = position; var q = rotation; _parent?._children.Remove(this); _parent = value; value?._children.Add(this); if (keepWorld) SetPositionAndRotation(p, q); }
    }
    public sealed class GUIContent { public string text, tooltip; public GUIContent(string text,string tooltip) { this.text=text;this.tooltip=tooltip; } }
    public static class GUILayout
    {
        public static string Last; public static bool? NextToggle; public static int ToggleCalls;
        public static void Label(string value) { Last = value; }
        public static bool Toggle(bool value, GUIContent content) { Last=content.text;ToggleCalls++;bool next=NextToggle??value;NextToggle=null;return next; }
    }
    [Serializable] public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(); public static Vector3 one => new Vector3(1, 1, 1);
        [ScriptIgnore] public float sqrMagnitude => x*x+y*y+z*z;
        [ScriptIgnore] public float magnitude => (float)Math.Sqrt(sqrMagnitude);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x,-a.y,-a.z);
    }
    [Serializable] public struct Quaternion
    {
        public float x,y,z,w;
        public Quaternion(float x,float y,float z,float w) { this.x=x;this.y=y;this.z=z;this.w=w; }
        private N.Quaternion Nq() => new N.Quaternion(x,y,z,w);
        private static Quaternion Q(N.Quaternion q) => new Quaternion(q.X,q.Y,q.Z,q.W);
        public static Quaternion identity => new Quaternion(0,0,0,1);
        public static Quaternion Inverse(Quaternion q) => Q(N.Quaternion.Inverse(q.Nq()));
        public static Quaternion operator *(Quaternion a, Quaternion b) => Q(a.Nq()*b.Nq());
        public static Vector3 operator *(Quaternion a, Vector3 b) { var v=N.Vector3.Transform(new N.Vector3(b.x,b.y,b.z),a.Nq());return new Vector3(v.X,v.Y,v.Z); }
        public static Quaternion Euler(Vector3 v) => Q(N.Quaternion.CreateFromYawPitchRoll(v.y*(float)Math.PI/180,v.x*(float)Math.PI/180,v.z*(float)Math.PI/180));
        [ScriptIgnore] public Vector3 eulerAngles => new Vector3(
            (float)Math.Asin(Math.Max(-1,Math.Min(1,2*(w*x-y*z))))*180/(float)Math.PI,
            (float)Math.Atan2(2*(w*y+x*z),1-2*(x*x+y*y))*180/(float)Math.PI,
            (float)Math.Atan2(2*(w*z+x*y),1-2*(x*x+z*z))*180/(float)Math.PI);
        public static float Angle(Quaternion a,Quaternion b)
        { float d=Math.Abs(N.Quaternion.Dot(N.Quaternion.Normalize(a.Nq()),N.Quaternion.Normalize(b.Nq())));return d>0.999999f?0:2*(float)Math.Acos(Math.Min(1,d))*180/(float)Math.PI; }
    }
    public static class JsonUtility
    {
        public static string ToJson(object value) => new JavaScriptSerializer().Serialize(value);
        public static object FromJson(string value, Type type) => new JavaScriptSerializer().Deserialize(value,type);
    }
}
public class ChaControl { public UnityEngine.Transform transform; }
