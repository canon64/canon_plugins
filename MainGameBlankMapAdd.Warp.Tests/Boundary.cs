using System;
using System.Numerics;
namespace UnityEngine
{
    public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
    public struct Vector4 {public float x,y,z,w;public Vector4(float x,float y,float z,float w){this.x=x;this.y=y;this.z=z;this.w=w;}}
    public struct Rect {public float x,y,width,height;public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;}public float xMax=>x+width;}
    public class GUIContent {public string text;public GUIContent(string text,string tip){this.text=text;}}
    public static class Mathf {public static float Max(float a,float b)=>MathF.Max(a,b);}
    public static class GUI
    {
        public static bool enabled=true;public static string Click;
        public static void Label(Rect rect,string text){}public static void Label(Rect rect,GUIContent text){}
        public static bool Button(Rect rect,GUIContent content){if(!enabled||Click!=content.text)return false;Click=null;return true;}
    }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 zero=>new Vector3(); public static Vector3 one=>new Vector3(1,1,1);
        internal System.Numerics.Vector3 N=>new System.Numerics.Vector3(x,y,z);
        public Vector3 normalized=>From(System.Numerics.Vector3.Normalize(N));
        internal static Vector3 From(System.Numerics.Vector3 v)=>new Vector3(v.X,v.Y,v.Z);
        public static Vector3 operator +(Vector3 a,Vector3 b)=>From(a.N+b.N);
        public static Vector3 operator -(Vector3 a,Vector3 b)=>From(a.N-b.N);
        public static Vector3 operator *(Vector3 a,float b)=>From(a.N*b);
        public static Vector3 Scale(Vector3 a,Vector3 b)=>new Vector3(a.x*b.x,a.y*b.y,a.z*b.z);
    }
    public struct Quaternion
    {
        internal System.Numerics.Quaternion N;
        public static Quaternion identity=>new Quaternion{N=System.Numerics.Quaternion.Identity};
        public static Quaternion Euler(float x,float y,float z)=>new Quaternion{N=System.Numerics.Quaternion.CreateFromYawPitchRoll(y*MathF.PI/180,x*MathF.PI/180,z*MathF.PI/180)};
        public static Quaternion Inverse(Quaternion q)=>new Quaternion{N=System.Numerics.Quaternion.Inverse(q.N)};
        public static Quaternion operator *(Quaternion a,Quaternion b)=>new Quaternion{N=a.N*b.N};
        public static Vector3 operator *(Quaternion q,Vector3 v)=>Vector3.From(System.Numerics.Vector3.Transform(v.N,q.N));
    }
    public class Transform
    {
        public GameObject gameObject;
        public void SetPositionAndRotation(Vector3 p, Quaternion r){position=p;rotation=r;}
        public Vector3 position,localScale=Vector3.one; public Quaternion rotation=Quaternion.identity;
        public Vector3 lossyScale=>localScale;
        public Vector3 TransformPoint(Vector3 v)=>position+rotation*Vector3.Scale(v,localScale);
        public Vector3 InverseTransformPoint(Vector3 v){v=Quaternion.Inverse(rotation)*(v-position);return new Vector3(v.x/localScale.x,v.y/localScale.y,v.z/localScale.z);}
    }
    public class GameObject { public Transform transform; public GameObject(){transform=new Transform{gameObject=this};} }
    public static class GUILayout
    {
        public static void Space(float x){} public static void Label(string s,params object[] p){}
        public static void BeginHorizontal(){} public static void EndHorizontal(){}
        public static void BeginVertical(string s){} public static void EndVertical(){}
        public static bool Toggle(bool value,string label,params object[] p)=>value;
        public static object ExpandWidth(bool value)=>null;
        public static object Width(float f)=>null;public static bool Button(string s,params object[] p)=>false;
        public static string TextField(string s,int length,params object[] p)=>s;
    }
}
namespace BepInEx.Configuration
{
    public class ConfigEntry<T>{public T Value;public ConfigEntry(T value){Value=value;}}
    public class ConfigFile {public bool SaveOnConfigSet=true,Fail;public int Saves;public void Save(){if(Fail)throw new System.IO.IOException("test save failure");Saves++;}}
}
public class Actor {public UnityEngine.Transform transform=new UnityEngine.Transform();}
public class BaseMap {public UnityEngine.GameObject mapRoot=new UnityEngine.GameObject();public int no=42;public System.Collections.Generic.Dictionary<int,MapInfo> infoDic=new System.Collections.Generic.Dictionary<int,MapInfo>();}
public class MapInfo {public string AssetBundleName,AssetName;}
public class CameraControl
{
    public struct Data {public UnityEngine.Vector3 Pos;public float Fov;}
    public Data State;public UnityEngine.Transform transBase;public int Writes;
    public Data GetCameraData()=>State;
    public void SetCameraData(Data data){Writes++;State=data;}
}
public class HScene { public Flags flags=new Flags();public class Flags{public CameraControl ctrlCamera=new CameraControl();}}
namespace CanonPlugins.Shared {public static class PluginSessionGate{public static HScene HScene=new HScene();}}
namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private bool _sessionStarted=true,_roomOverlayLoading,allowed=true;
        private BaseMap LastReservedMap=new BaseMap();
        private Actor _femaleChara=new Actor();private string _roomOverlaySceneName="scene",_roomOverlayBundleName="bundle",_roomOverlayPositionKey="1";
        private UnityEngine.GameObject _roomOverlayPositionRoot=new UnityEngine.GameObject(),_vamHotelPivot=new UnityEngine.GameObject();
        private UnityEngine.Vector3 _roomOverlayBasePosition,_roomOverlayActorAnchor,_vamHotelActorAnchor;
        private UnityEngine.Quaternion _roomOverlayBaseRotation=UnityEngine.Quaternion.identity;
        private const string VamHotelSceneName="hotel",StreetlightRoadSceneKey="road";
        private string _pluginDir,_mapTabInputKey;
        private BepInEx.Configuration.ConfigFile Config=new BepInEx.Configuration.ConfigFile();
        private BepInEx.Configuration.ConfigEntry<float> _vamHotelOffsetX=E(0),_vamHotelOffsetY=E(0),_vamHotelOffsetZ=E(0),
            _vamHotelRotationX=E(0),_vamHotelRotationY=E(0),_vamHotelRotationZ=E(0),_vamHotelScale=E(1);
        private sealed class RoomOverlayPosition
        {
            internal BepInEx.Configuration.ConfigEntry<float> X=E(0),Y=E(0),Z=E(0),RotationX=E(0),RotationY=E(0),RotationZ=E(0);
            internal BepInEx.Configuration.ConfigEntry<bool> RotateAroundActor=new BepInEx.Configuration.ConfigEntry<bool>(false);
        }
        private RoomOverlayPosition settings=new RoomOverlayPosition();
        private static BepInEx.Configuration.ConfigEntry<float> E(float x)=>new BepInEx.Configuration.ConfigEntry<float>(x);
        private bool CanUseRoomOverlay()=>allowed;
        private bool IsAiIslandScene(string s)=>s=="island";private bool IsSandboxMapScene(string s)=>s=="sandbox";
        private RoomOverlayPosition EnsureRoomOverlayPositionConfig(string k)=>settings;
        private void EnsureMapTabInputs(string key,BepInEx.Configuration.ConfigEntry<float>[] entries){_mapTabInputKey=key;}
        private void ApplyMapTabTransform(bool hotel){if(hotel)ApplyVamHotelTransform();else ApplyRoomOverlayPosition();}
        private void SyncMapTabPositionInputs(BepInEx.Configuration.ConfigEntry<float>[] entries){}
        private void ApplyRoomOverlayPosition()
        {
            var rot=_roomOverlayBaseRotation*UnityEngine.Quaternion.Euler(settings.RotationX.Value,settings.RotationY.Value,settings.RotationZ.Value);
            var pos=_roomOverlayBasePosition+new UnityEngine.Vector3(settings.X.Value,settings.Y.Value,settings.Z.Value);
            if(settings.RotateAroundActor.Value)pos=_roomOverlayActorAnchor+(rot*UnityEngine.Quaternion.Inverse(_roomOverlayBaseRotation))*(pos-_roomOverlayActorAnchor);
            _roomOverlayPositionRoot.transform.position=pos;_roomOverlayPositionRoot.transform.rotation=rot;
        }
        private void ApplyVamHotelTransform()
        {
            _vamHotelPivot.transform.position=_vamHotelActorAnchor+new UnityEngine.Vector3(_vamHotelOffsetX.Value,_vamHotelOffsetY.Value,_vamHotelOffsetZ.Value);
            _vamHotelPivot.transform.rotation=UnityEngine.Quaternion.Euler(_vamHotelRotationX.Value,_vamHotelRotationY.Value,_vamHotelRotationZ.Value);
            _vamHotelPivot.transform.localScale=UnityEngine.Vector3.one*_vamHotelScale.Value;
        }
        private static bool IsFiniteMapVector(UnityEngine.Vector3 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z);
    }
}
