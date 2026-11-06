using System;
using System.Collections.Generic;
using MainGameIPhoneCamera;

namespace UnityEngine
{
    public class Object { public static void Destroy(Object value) { } }
    public class MonoBehaviour : Object { public GameObject gameObject; }
    public struct Vector2 { public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} }
    public struct Vector3
    {
        public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 zero => new(0,0,0); public static Vector3 one => new(1,1,1);
    }
    public class Transform { public Vector3 localScale=Vector3.one, localPosition; }
    public class RectTransform : Transform { public Vector2 sizeDelta; }
    public class GameObject
    {
        public Transform transform = new Transform();
        private readonly Dictionary<Type,object> parts = new();
        public T AddComponent<T>() where T:new()
        { var part = new T(); if(part is MonoBehaviour m)m.gameObject=this; parts[typeof(T)]=part; return part; }
        public T GetComponent<T>() => (T)parts[typeof(T)];
    }
    public enum WrapMode { ClampForever }
    public enum AnimationCullingType { AlwaysAnimate }
    public struct Keyframe
    {
        public float time,value,inTangent,outTangent;
        public Keyframe(float t,float v,float i=0,float o=0){time=t;value=v;inTangent=i;outTangent=o;}
    }
    public class AnimationCurve
    {
        public Keyframe[] keys; public AnimationCurve(params Keyframe[] keys){this.keys=keys;}
        public static AnimationCurve Linear(float a,float b,float c,float d)=>new(new Keyframe(a,b),new Keyframe(c,d));
        public float Evaluate(float t)
        {
            for(int i=1;i<keys.Length;i++) if(t<keys[i].time)
            { var a=keys[i-1];var b=keys[i];return a.value+(b.value-a.value)*(t-a.time)/(b.time-a.time); }
            return keys[keys.Length-1].value;
        }
    }
    public class AnimationClip : Object
    {
        public bool legacy; public WrapMode wrapMode;
        public readonly Dictionary<string,AnimationCurve> Curves=new();
        public void SetCurve(string path,Type type,string property,AnimationCurve curve)
        { if(path!=""||type!=typeof(Transform))throw new Exception("Motion must bind only its ordinary Transform parent");Curves[property]=curve; }
    }
    public class Animation : MonoBehaviour
    {
        public bool playAutomatically; public AnimationCullingType cullingType;
        private AnimationClip clip;
        public void AddClip(AnimationClip value,string name){clip=value;}
        public bool Play(string name)=>clip!=null;
        public void Stop(){gameObject.transform.localScale=Vector3.one;gameObject.transform.localPosition=Vector3.zero;}
        public void RemoveClip(string name){clip=null;}
        public void SampleAt(float seconds)
        {
            foreach(var pair in clip.Curves)
            {
                float v=pair.Value.Evaluate(seconds);var t=gameObject.transform;
                switch(pair.Key)
                {
                    case "localScale.x":t.localScale.x=v;break; case "localScale.y":t.localScale.y=v;break;
                    case "localPosition.x":t.localPosition.x=v;break;case "localPosition.y":t.localPosition.y=v;break;
                    default:throw new Exception("Unexpected animation binding: "+pair.Key);
                }
            }
        }
    }
}
internal static class Program
{
    private static int checks;
    private static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
    private static void Main()
    {
        Console.WriteLine("[ViewerMotion] Production motion clips, ordinary-transform binding and sampled presentation; native Unity rendering not executed.");
        for (int q = 0; q < 4; q++)
        {
            float angle = PhoneOrientationPlan.TargetAngle(0, q);
            Check(Math.Abs(angle) <= 180 && Math.Abs((angle + 90 * q) % 360) < .001, "all four orientations target the matching shortest rotation");
            Check(PhoneOrientationPlan.Columns(q) == ((q & 1) == 0 ? 3 : 5), "portrait and landscape grids follow orientation");
        }
        Check(PhoneOrientationPlan.TargetAngle(270, 0) == 360 && PhoneOrientationPlan.TargetAngle(350, 3) == 450, "angle wrap keeps animation continuous");
        Check(PhoneOrientationPlan.TargetAngle(135, 2) == 180, "interrupted animation continues from current angle");
        Check(PhoneGrid.Move(8, 20, 0, -1, 5) == 12 && PhoneGrid.Move(12, 20, 0, 1, 5) == 8, "five-column navigation preserves visible column across nine-item pages");
        Check(PhoneGrid.Move(8, 20, 1, 0, 5) == 8 && PhoneGrid.Move(13, 20, 0, 1, 5) == 13, "incomplete landscape row rejects empty cells");
        foreach (bool landscape in new[] { false, true })
        foreach (int count in new[] { 4, 5, 6 })
        {
            float width = landscape ? 1166 : 560, height = PhoneControlLayout.Height(landscape);
            float hintTop = PhoneControlLayout.HintY(landscape) + PhoneControlLayout.HintHeight / 2;
            float messageBottom = PhoneControlLayout.MessageY(landscape, count) - PhoneControlLayout.MessageHeight / 2;
            for (int i = 0; i < count; i++)
            {
                float x = PhoneControlLayout.ButtonX(landscape, count, i), y = PhoneControlLayout.ButtonY(landscape, count, i);
                Check(y - 30 > hintTop && y + 30 < messageBottom, "photo/video/editor buttons never overlap instructions or filename");
                Check(Math.Abs(x) + 80 < width / 2 && Math.Abs(y) + 30 < height / 2, "all controls fit inside portrait and landscape screens");
            }
        }
        var host=new UnityEngine.GameObject();var image=new UnityEngine.RectTransform();var motion=host.AddComponent<ViewerMotion>();
        motion.Initialize(host.transform,image,1920,886,560,1166);var animation=host.GetComponent<UnityEngine.Animation>();
        Check(Math.Abs(image.sizeDelta.x-560)<.01,"landscape image fits full portrait width");
        motion.Drive(0,1,false);animation.SampleAt(.5f);
        Check(host.transform.localScale.x>1.7f&&image.sizeDelta.x*host.transform.localScale.x>950,"upward stick changes displayed image scale");
        float zoom=host.transform.localScale.x;motion.StopMotion();
        Check(host.transform.localScale.x==zoom,"stop preserves sampled zoom despite native animation restoring defaults");
        motion.Drive(1,1,true);animation.SampleAt(.25f);
        Check(host.transform.localPosition.x>100&&host.transform.localPosition.y==0&&host.transform.localScale.x==zoom,"pan moves image without zoom and clamps non-overflow axis");
        motion.Resize(1166,560);
        Check(image.sizeDelta.x>1100&&host.transform.localScale.x==zoom,"horizontal viewport refits media and preserves zoom");
        motion.Drive(0,1,false);animation.SampleAt(100);
        Check(Math.Abs(host.transform.localScale.x-20)<.001,"zoom reaches bounded maximum");
        motion.Drive(-1,-1,true);animation.SampleAt(1000);
        Check(Math.Abs(host.transform.localPosition.x+ViewerLayout.Limit(image.sizeDelta.x,1166,20))<.01,"pan reaches image edge");
        motion.Drive(0,-1,false);animation.SampleAt(100);motion.StopMotion();
        Check(Math.Abs(host.transform.localScale.x-1)<.001&&Math.Abs(host.transform.localPosition.x)<.01&&Math.Abs(host.transform.localPosition.y)<.01,"zoom out re-centers and clamps both axes");
        Console.WriteLine("PASS "+checks+" production presentation checks.");
    }
}
