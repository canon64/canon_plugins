using System;
using System.Collections.Generic;
using CanonGate;
namespace UnityEngine
{
 public class MonoBehaviour
 {
  public Coroutine StartCoroutine(System.Collections.IEnumerator routine){var c=new Coroutine{Routine=routine};routine.MoveNext();return c;}
  public void StopCoroutine(Coroutine c){c.Stopped=true;}
  public static void Destroy(object value){if(value is GameObject g)g.Destroyed=true;if(value is AudioClip clip)clip.Destroyed=true;}
 }
 public class Coroutine {public System.Collections.IEnumerator Routine;public bool Stopped;}
 public class WaitForSecondsRealtime {public float Seconds;public WaitForSecondsRealtime(float seconds){Seconds=seconds;}}
 public class WaitForSeconds {public float Seconds;public WaitForSeconds(float seconds){Seconds=seconds;}}
 public static class Time {public static float realtimeSinceStartup;}
 public class Transform {public Vector3 localPosition,localScale;public void SetParent(Transform parent,bool world){}}
 public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 one=>new Vector3(1,1,1);}
 public class AnimationState {public float speed=1,time,length=3;}
 public class Animation
 {
  readonly AnimationState state=new AnimationState();public bool Playing;
  public AnimationState this[string name]=>state;
  public bool Play(string name){Playing=true;return true;}public void Stop(){Playing=false;}
 }
 public enum WrapMode {ClampForever}
 public struct Quaternion {public float x,y,z,w;public Quaternion(float x,float y,float z,float w){this.x=x;this.y=y;this.z=z;this.w=w;}}
 public struct Keyframe {public float time,value;public Keyframe(float time,float value){this.time=time;this.value=value;}}
 public class AnimationCurve {public Keyframe[] keys;public AnimationCurve(params Keyframe[] keys){this.keys=keys;}}
 public class MeshRenderer {}
 public class AnimationClip
 {
  public bool legacy;public WrapMode wrapMode;
  public readonly Dictionary<string,AnimationCurve> Curves=new Dictionary<string,AnimationCurve>();
  public void SetCurve(string path,Type type,string property,AnimationCurve curve){Curves[path+":"+property]=curve;}
  public void EnsureQuaternionContinuity(){}
 }
 public class GameObject
 {
  public bool Destroyed;public Transform transform=new Transform();
  public GameObject(string name){}
  public T AddComponent<T>() where T:new()=>new T();
 }
 public class AudioClip
 {
  public delegate void PCMReaderCallback(float[] output);
  public float length;public string name;public float[] Samples;public bool Destroyed;public PCMReaderCallback Reader;
  public static AudioClip Create(string name,int count,int channels,int rate,bool stream,PCMReaderCallback reader)
   =>new AudioClip{length=(float)count/rate,Reader=reader};
  public static AudioClip Create(string name,int count,int channels,int rate,bool stream)=>new AudioClip{name=name,length=(float)count/rate};
  public void SetData(float[] values,int offset){Samples=values;}
 }
 public class AudioSource
 {
  public bool playOnAwake,loop,Playing;public float spatialBlend,dopplerLevel,volume;public AudioClip clip;
  public void Play(){Playing=true;}public void Stop(){Playing=false;}
 }
 public struct Vector2 {public float x,y;public Vector2(float a,float b){x=a;y=b;}public static Vector2 zero=>new Vector2();}
 public struct Vector4 {public float x,y,z,w;public Vector4(float x,float y,float z,float w){this.x=x;this.y=y;this.z=z;this.w=w;}public static Vector4 zero=>new Vector4();}
 public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);public static float Clamp(float x,float min,float max)=>Math.Max(min,Math.Min(x,max));public static float Clamp01(float x)=>Clamp(x,0,1);}
 public static class Random {public static int Range(int min,int max)=>min;}
 public static class GUILayout
 {
  public static bool Toggle(bool value,string text)=>value;
  public static bool Button(string text)=>false;
  public static void Label(string text){}
 }
 public class Texture {}
 public class Shader {public string name;public bool isSupported=true;public Shader(string name){this.name=name;}}
 public class Material
 {
  public bool Expression=true;public Shader shader=new Shader("Koikano/main_eye");public int renderQueue=2902;public string[] shaderKeywords=new string[0];
  readonly Dictionary<string,object> values=new Dictionary<string,object>();
  public bool HasProperty(string property)=>property=="_MainTex"||property=="_rotation"||property=="_expression"&&Expression;
  public float GetFloat(string key)=>values.TryGetValue(key,out var value)?(float)value:0;
  public void SetFloat(string key,float value)=>values[key]=value;
  public Texture GetTexture(string key)=>values.TryGetValue(key,out var value)?value as Texture:null;
  public void SetTexture(string key,Texture value)=>values[key]=value;
  public void SetVector(string key,Vector4 value)=>values[key]=value;
 }
 public class MaterialPropertyBlock
 {
  internal readonly Dictionary<string,object> Values=new Dictionary<string,object>();
  public bool isEmpty=>Values.Count==0;
  public void SetTexture(string key,Texture value)=>Values[key]=value;
  public void SetFloat(string key,float value)=>Values[key]=value;
  public Texture GetTexture(string key)=>Values.TryGetValue(key,out var value)?value as Texture:null;
  public float GetFloat(string key)=>Values.TryGetValue(key,out var value)?(float)value:0;
  internal void CopyFrom(MaterialPropertyBlock other){Values.Clear();if(other!=null)foreach(var p in other.Values)Values[p.Key]=p.Value;}
 }
 public class Renderer
 {
  public Material[] sharedMaterials={new Material()};
  readonly Dictionary<int,MaterialPropertyBlock> blocks=new Dictionary<int,MaterialPropertyBlock>();
  public void GetPropertyBlock(MaterialPropertyBlock target,int index=-1){blocks.TryGetValue(index,out var value);target.CopyFrom(value);}
  public void SetPropertyBlock(MaterialPropertyBlock value,int index=-1){if(value==null){blocks.Remove(index);return;}var copy=new MaterialPropertyBlock();copy.CopyFrom(value);blocks[index]=copy;}
  public MaterialPropertyBlock Block(int index=0){var copy=new MaterialPropertyBlock();GetPropertyBlock(copy,index);return copy;}
 }
 public static class Debug {public static void LogWarning(object message){}}
}
public class HSceneProc:UnityEngine.MonoBehaviour {}
public class ChaControl
{
 public UnityEngine.AudioSource LipSource;
 public bool MouthFixed;
 public void SetLipSync(UnityEngine.AudioSource source){LipSource=source;}
 public bool GetMouthFixed()=>MouthFixed;
 public void ChangeMouthFixed(bool value){MouthFixed=value;}
 static int next;readonly int id=++next;
 public int sex=1;public bool Participant=true,IdentityMatches=true,Target=true;
 public int EyeNumber;
 public UnityEngine.Transform transform=new UnityEngine.Transform();
 public UnityEngine.GameObject objHeadBone=new UnityEngine.GameObject("head");
 public UnityEngine.Renderer[] rendEye={new UnityEngine.Renderer(),new UnityEngine.Renderer()};
 public int GetInstanceID()=>id;
}
namespace HarmonyLib
{
 public class HarmonyPrefix:Attribute {}
 public class HarmonyPostfix:Attribute {}
 public class HarmonyPriority:Attribute {public HarmonyPriority(int value){}}
 public class HarmonyPatch:Attribute {public HarmonyPatch(Type type,string method){}}
 public static class Priority {public const int First=800,Last=0;}
 public class HarmonyMethod {public int priority;public HarmonyMethod(Type type,string name){}}
 public class Harmony
 {
  public void Patch(System.Reflection.MethodBase method,HarmonyMethod prefix=null,HarmonyMethod finalizer=null){}
  public void UnpatchSelf(){}
 }
}
namespace CanonPlugins.Local
{
 public static class SessionGate
 {
  public static ChaControl[] Actors=new ChaControl[0];
 }
}
namespace MainGameDollMode
{
 public partial class Plugin
 {
  public const string GUID="canon.maingame.dollmode";
  internal static Plugin Instance;
  internal bool _dollModeApplied=true,Enabled=true;
  internal readonly Dictionary<int,bool> _originalHideHighlightByCha=new Dictionary<int,bool>();
  internal bool IsTargetCharacter(ChaControl actor)=>actor.Target&&actor.sex==1;
  internal bool SavedActorMatches(ChaControl actor)=>actor.IdentityMatches;
  static void LogWarn(string message){}
  public static bool IsDollModeEnabled()=>GateApi.CanRun(GUID)&&Instance!=null&&Instance.Enabled;
  public void SetEnabled(bool enabled){Enabled=enabled;ReleaseExternalDolls(false);}
  public void Stop(){ReleaseExternalDolls(true);}
 }
}
namespace MainGameSpiralEyes
{
 public class ConfigFlag {public bool Value=true;}
 public class TestLogger {public void LogWarning(string message){}}
 public sealed partial class Plugin
 {
  public const string Guid="com.kks.maingame.spiraleyes";
  internal static Plugin Instance;
  internal readonly int _mainThread=System.Threading.Thread.CurrentThread.ManagedThreadId;
  internal readonly ConfigFlag _enabled=new ConfigFlag();
  internal readonly List<ChaControl> _actors=new List<ChaControl>();
  internal readonly HashSet<ChaControl> _selectedActors=new HashSet<ChaControl>();
  internal readonly Dictionary<ChaControl,int> _selectionCache=new Dictionary<ChaControl,int>();
  internal readonly HashSet<ChaControl> Manual=new HashSet<ChaControl>();
  internal readonly PupilFollowBindings _bindings=new PupilFollowBindings(new UnityEngine.Shader("canon/PupilFollow"));
  PupilFollowBindings _followBindings=>_bindings;
  internal bool PrepareOrbit(string owner,ChaControl actor)=>Allowed;
  internal bool SetOrbit(string owner,ChaControl actor,float r,float s)=>Allowed;
  internal void EndOrbit(string owner,ChaControl actor){}
  void ClearOrbits(){}
  void UnbindOrbit(ChaControl actor){}
  internal readonly UnityEngine.Texture OverlayTexture=new UnityEngine.Texture();
  internal readonly HarmonyLib.Harmony _harmony=new HarmonyLib.Harmony();
  internal readonly TestLogger Logger=new TestLogger();
  internal bool Active=true,FailBinding,ThrowBinding;
  bool Allowed=>Active&&GateApi.CanRun(Guid);
  void BindActor(ChaControl actor)
  {
   if(FailBinding||(!HasForcedOverlay(actor)&&!Manual.Contains(actor)))return;
   if(ThrowBinding)throw new Exception("partial failure");
   _selectedActors.Add(actor);for(int side=0;side<actor.rendEye.Length;side++){var renderer=actor.rendEye[side];if(renderer!=null)_bindings.Apply(renderer,OverlayTexture,HasDollLayer(actor)?DollLayerPosition(actor):UnityEngine.Vector2.zero,EffectiveOpacity(actor),side,IsSpiralSelected(actor));}
  }
  bool IsSpiralSelected(ChaControl actor)=>Manual.Contains(actor);
  void ReleaseEffects(){_bindings.Restore();_selectedActors.Clear();}
  public void Stop(){Active=false;ClearForcedOverlays();ReleaseEffects();}
  public bool Forced(ChaControl actor)=>HasForcedOverlay(actor);
  public bool Independent(ChaControl actor)=>!IsSpiralSelected(actor)&&HasForcedOverlay(actor);
 }
}
