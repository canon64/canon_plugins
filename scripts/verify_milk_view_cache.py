"""Run production LiquidRenderer with engine stubs against forced rebuilds. No Unity runtime/FPS claim."""
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / '_diagnostics/perf-reduction-20261027/milk-view-cache'

STUBS = r'''
using System;using System.Collections.Generic;using System.Numerics;
namespace UnityEngine {
 public class Object {public static void DontDestroyOnLoad(Object o){} public static void Destroy(Object o){} }
 public class GameObject:Object {public int layer;public string name;public GameObject(string n){name=n;}public T AddComponent<T>()where T:new()=>new T();}
 public class Transform {public Vector3 position,forward=new Vector3(0,0,1),right=new Vector3(1,0,0),up=new Vector3(0,1,0);}
 public class Camera:Object {public static event Action<Camera> onPreCull;public int cullingMask=-1;public string name="camera";public Transform transform=new Transform();public int GetInstanceID()=>1;public static void Draw(Camera c)=>onPreCull?.Invoke(c);}
 public class AssetBundle:Object {public static AssetBundle LoadFromMemory(byte[] b)=>new AssetBundle();public T LoadAsset<T>(string s)where T:new()=>new T();public void Unload(bool b){} }
 public class Shader:Object {public bool isSupported=true;}
 public enum HideFlags {HideAndDontSave}
 public class Material:Object {public string name;public HideFlags hideFlags;public Material(Shader s){} }
 public class MeshRenderer:Object {public Material sharedMaterial;public Rendering.ShadowCastingMode shadowCastingMode;public bool receiveShadows,enabled;}
 public class MeshFilter:Object {public Mesh sharedMesh;}
 public class Mesh:Object {
  public string name;public Rendering.IndexFormat indexFormat;public int Uploads,Clears;public bool FailNextUpload;
  public List<Vector3> Positions=new List<Vector3>(),Normals=new List<Vector3>();public List<Color> Colors=new List<Color>();
  public List<Vector2> Uvs=new List<Vector2>();public List<Vector4> Surfaces=new List<Vector4>();public List<int> Indices=new List<int>();
  public void MarkDynamic(){}public void Clear(){Clears++;Positions.Clear();Normals.Clear();Colors.Clear();Uvs.Clear();Surfaces.Clear();Indices.Clear();}
  public void SetVertices(List<Vector3> v){if(FailNextUpload){FailNextUpload=false;throw new InvalidOperationException("upload failed");}Positions.AddRange(v);}
  public void SetNormals(List<Vector3> v)=>Normals.AddRange(v);public void SetColors(List<Color> c)=>Colors.AddRange(c);
  public void SetUVs(int channel,List<Vector2> v)=>Uvs.AddRange(v);public void SetUVs(int channel,List<Vector4> v)=>Surfaces.AddRange(v);
  public void SetTriangles(List<int> v,int sub,bool bounds){Uploads++;Indices.AddRange(v);}public void RecalculateBounds(){}
 }
 public struct Vector2 {public float x,y;public Vector2(float a,float b){x=a;y=b;}public static Vector2 zero=>default;}
 public struct Vector4 {public float x,y,z,w;public Vector4(float a,float b,float c,float d){x=a;y=b;z=c;w=d;}}
 public struct Color {public float r,g,b,a;public Color(float x,float y,float z,float w){r=x;g=y;b=z;a=w;}}
 public struct Vector3 {
  public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}
  public static Vector3 up=>new Vector3(0,1,0);public static Vector3 down=>new Vector3(0,-1,0);
  public Vector3 normalized {get{float n=MathF.Sqrt(x*x+y*y+z*z);return n>1e-12f?this*(1/n):default;}}
  public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
  public static Vector3 operator-(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
  public static Vector3 operator-(Vector3 a)=>new Vector3(-a.x,-a.y,-a.z);
  public static Vector3 operator*(Vector3 a,float s)=>new Vector3(a.x*s,a.y*s,a.z*s);
  public static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
 }
 public struct Quaternion {
  System.Numerics.Quaternion q;
  public static Quaternion FromToRotation(Vector3 a,Vector3 b){
   var x=new System.Numerics.Vector3(a.x,a.y,a.z);var y=System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(b.x,b.y,b.z));
   float dot=System.Numerics.Vector3.Dot(x,y);var cross=System.Numerics.Vector3.Cross(x,y);
   return new Quaternion{q=dot < -.999999f?System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitX,MathF.PI):System.Numerics.Quaternion.Normalize(new System.Numerics.Quaternion(cross,1+dot))};
  }
  public static Vector3 operator*(Quaternion a,Vector3 b){var v=System.Numerics.Vector3.Transform(new System.Numerics.Vector3(b.x,b.y,b.z),a.q);return new Vector3(v.X,v.Y,v.Z);}
 }
 public static class Mathf {public const float PI=MathF.PI;public static float Max(float a,float b)=>Math.Max(a,b);public static float Sin(float f)=>MathF.Sin(f);public static float Cos(float f)=>MathF.Cos(f);public static float Clamp01(float f)=>Math.Clamp(f,0,1);public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);}
}
namespace UnityEngine.Rendering {public enum IndexFormat{UInt32}public enum ShadowCastingMode{Off}}
namespace MainGameMilkSpray {
 internal enum MilkStage {Sort,Geometry,Upload,Ghosts}
 internal class MilkPerformanceLog {
  internal bool Capturing;internal int CameraCalls;
  internal struct CameraSample{} internal struct Scope:IDisposable {public void Dispose(){}}
  internal static Scope Measure(MilkPerformanceLog p,MilkStage s)=>default;internal void TouchFrame(int frame){}
  internal CameraSample BeginCamera()=>default;internal void EndCamera(CameraSample s,int id,string name,int count,int vertices,int triangles){CameraCalls++;}
  internal void SetParticles(int d,int g,int c){}
 }
}
namespace UnityEngine {public static class Time {public static int frameCount;}}
'''

TESTS = r'''
using System;using System.Linq;using System.Reflection;using System.Text.Json;using UnityEngine;using MainGameMilkSpray;
class Program {
 static int checks;
 static void Check(bool b,string text){checks++;if(!b)throw new Exception(text);}
 static Mesh MeshOf(object renderer)=>(Mesh)renderer.GetType().GetField("_mesh",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(renderer);
 static Action<Camera> Draw(object renderer)=>(Action<Camera>)Delegate.CreateDelegate(typeof(Action<Camera>),renderer,renderer.GetType().GetMethod("BeforeCameraCull",BindingFlags.Instance|BindingFlags.NonPublic));
 static void Equal(Mesh a,Mesh b){
  Check(a.Positions.SequenceEqual(b.Positions),"positions identical");Check(a.Normals.SequenceEqual(b.Normals),"normals identical");
  Check(a.Colors.SequenceEqual(b.Colors),"colors identical");Check(a.Uvs.SequenceEqual(b.Uvs),"glow UV identical");
  Check(a.Surfaces.SequenceEqual(b.Surfaces),"surfaces identical");Check(a.Indices.SequenceEqual(b.Indices),"transparency sort/indices identical");
 }
 static void Main(){
  var log=new MilkPerformanceLog{Capturing=true};using var actual=new LiquidRenderer("actual",log);using var expected=new ReferenceLiquidRenderer("reference");
  var draw=Draw(actual);var reference=Draw(expected);var mesh=MeshOf(actual);var old=MeshOf(expected);
  var sim=new LiquidSimulation(41729);var spec=new LiquidSpec{Drops=49,Duration=.05f,Speed=1,SpeedVariation=.2f,SpreadRadians=.1f,
   SizeMin=.003f,SizeMax=.005f,StretchRatio=3,DropLife=3,Gravity=9.81f,Afterimages=true,GhostCount=4,GhostLife=.5f,
   GhostOpacity=.5f,GhostEndScale=.3f,GhostDistance=.02f,Gloss=.5f,EdgeSoftness=.2f,Emission=1,Red=.9f,Green=.8f,Blue=.7f,Alpha=.7f};
  var pose=new EmitterPose(default,new V3(0,.7660444f,.6427876f),new V3(1,0,0));sim.Fire(spec,pose,pose,true,false);
  var camera=new Camera();var copy=new Camera();
  int draws=0;
  for(int frame=0;frame<120;frame++){
   Time.frameCount=frame;sim.Advance(1.0/120,pose,pose);if(frame%30==0)sim.Fire(spec,pose,pose,true,false);
   actual.Render(sim);expected.Render(sim);
   for(int capture=0;capture<4;capture++){
    var c=capture%2==0?camera:copy;draw(c);reference(c);Equal(mesh,old);draws++;
   }
  }
  Check(mesh.Uploads>0&&old.Uploads==mesh.Uploads*4,"identical views reduce four builds/uploads to one per particle update");
  int optimized=mesh.Uploads,baseline=old.Uploads;
  void Compare(){draw(camera);reference(camera);Equal(mesh,old);}
  int uploads=mesh.Uploads;
  camera.transform.position=new Vector3(0.0000001f,0,0);Compare();Check(mesh.Uploads==++uploads,"tiny camera position change rebuilds");
  camera.transform.forward=new Vector3(.0000001f,0,1);Compare();Check(mesh.Uploads==++uploads,"tiny forward change rebuilds");
  camera.transform.right=new Vector3(1,.0000001f,0);Compare();Check(mesh.Uploads==++uploads,"tiny right change rebuilds");
  camera.transform.up=new Vector3(.0000001f,1,0);Compare();Check(mesh.Uploads==++uploads,"tiny up change rebuilds");
  camera.transform.position=new Vector3(0,0,-1);Compare();Check(mesh.Uploads==++uploads,"different view rebuilds");
  draw(copy);reference(copy);Equal(mesh,old);Check(mesh.Uploads==++uploads,"return after different view rebuilds current mesh");
  camera.cullingMask=0;draw(camera);Check(mesh.Uploads==uploads,"excluded layer skips work without changing cache");camera.cullingMask=-1;
  mesh.FailNextUpload=true;
  try{draw(camera);throw new Exception("missing upload failure");}catch(InvalidOperationException e){Check(e.Message=="upload failed","upload failure propagates");}
  draw(copy);reference(copy);Equal(mesh,old);Check(mesh.Uploads==++uploads,"failed upload invalidates previous matching view");
  actual.Render(sim);expected.Render(sim);draw(copy);reference(copy);Equal(mesh,old);Check(mesh.Uploads==++uploads,"particle replacement with same count invalidates");
  var quiet=new LiquidRenderer("quiet");var quietDraw=Draw(quiet);quiet.Render(sim);quietDraw(copy);
  for(int i=0;i<100;i++)quietDraw(copy);
  long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++)quietDraw(copy);
  Check(GC.GetAllocatedBytesForCurrentThread()==before,"10000 repeated view callbacks allocate zero bytes");quiet.Dispose();
  actual.Render(sim);expected.Render(sim);mesh.FailNextUpload=true;
  try{draw(copy);throw new Exception("missing second upload failure");}catch(InvalidOperationException){}
  int afterFailedClear=mesh.Clears;actual.Clear();expected.Clear();
  Check(mesh.Clears==afterFailedClear+1,"clear also cleans a partially uploaded mesh");
  draw(copy);reference(copy);Equal(mesh,old);Check(mesh.Positions.Count==0,"clear leaves no stale visible geometry");
  int clears=mesh.Clears;for(int i=0;i<1000;i++)actual.Clear();Check(mesh.Clears==clears,"already empty mesh is not cleared again");
  actual.Render(sim);expected.Render(sim);draw(copy);reference(copy);Equal(mesh,old);Check(mesh.Uploads==++uploads,"restart after clear rebuilds");
  Check(log.CameraCalls>draws,"diagnostics still count cache-hit camera calls and exceptions");
  int finalUploads=mesh.Uploads;actual.Dispose();draw(copy);Check(mesh.Uploads==finalUploads,"disposed renderer does no work");
  ClearChecks(spec,pose);
  SortChecks();
  Console.WriteLine($"MILK_VIEW_CACHE_OK checks={checks}; same-view builds/uploads {baseline}->{optimized}; 10000 hits=0 allocation bytes; engine stubs only, not Unity FPS");
 }
 static object Field(object o,string name)=>o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
 static void SortChecks(){
  using var a=new LiquidRenderer("sort actual");using var b=new ReferenceLiquidRenderer("sort baseline");
  var sort=(Action)Delegate.CreateDelegate(typeof(Action),a,typeof(LiquidRenderer).GetMethod("SortByDepth",BindingFlags.NonPublic|BindingFlags.Instance));
  var oldSort=(Action)Delegate.CreateDelegate(typeof(Action),b,typeof(ReferenceLiquidRenderer).GetMethod("SortByDepth",BindingFlags.NonPublic|BindingFlags.Instance));
  var slots=(Array)Field(a,"_particles");var oldSlots=(Array)Field(b,"_particles");
  var type=slots.GetType().GetElementType();var oldType=oldSlots.GetType().GetElementType();
  var depth=type.GetField("Depth");var oldDepth=oldType.GetField("Depth");var tie=type.GetField("Order");var oldTie=oldType.GetField("Order");
  var count=typeof(LiquidRenderer).GetField("_count",BindingFlags.NonPublic|BindingFlags.Instance);
  var oldCount=typeof(ReferenceLiquidRenderer).GetField("_count",BindingFlags.NonPublic|BindingFlags.Instance);
  var random=new Random(41729);
  foreach(int length in new[]{0,1,2,49,500,3500})foreach(int pattern in new[]{0,1,2,3,4}){
   count.SetValue(a,length);oldCount.SetValue(b,length);
   for(int i=0;i<length;i++){
    float value=pattern==0?0:pattern==1?i:pattern==2?-i:pattern==3?(float)(random.NextDouble()*20-10):
     i%4==0?float.NaN:i%4==1?float.NegativeInfinity:i%4==2?float.PositiveInfinity:0;
    object p=Activator.CreateInstance(type),q=Activator.CreateInstance(oldType);depth.SetValue(p,value);oldDepth.SetValue(q,value);
    tie.SetValue(p,i);oldTie.SetValue(q,i);slots.SetValue(p,i);oldSlots.SetValue(q,i);
   }
   sort();oldSort();
   for(int i=0;i<length;i++){
    Check((int)tie.GetValue(slots.GetValue(i))==(int)oldTie.GetValue(oldSlots.GetValue(i)),"sort matches original order including ties/nonfinite depth");
   }
  }
  // Full-capacity sorting must also remain allocation-free.
  for(int i=0;i<3500;i++){
   float value=(float)(random.NextDouble()*20-10);object p=slots.GetValue(i),q=oldSlots.GetValue(i);
   depth.SetValue(p,value);oldDepth.SetValue(q,value);tie.SetValue(p,i);oldTie.SetValue(q,i);slots.SetValue(p,i);oldSlots.SetValue(q,i);
  }
  for(int i=0;i<200;i++)sort();
  long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++)sort();
  Check(GC.GetAllocatedBytesForCurrentThread()==before,"1000 full-capacity sorts allocate zero bytes");
 }
 static void ClearChecks(LiquidSpec spec,EmitterPose pose){
  var a=new LiquidSimulation(200);var b=new ReferenceLiquidSimulation(200);
  var options=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};
  void EqualState(){
   Check(JsonSerializer.Serialize(a,options)==JsonSerializer.Serialize(b,options),"bursts/drops/ghosts/time remain identical to unconditional reset");
   Check(a.Now==b.Now,"simulation time remains identical");
   foreach(string field in new[]{"_ownedGhosts","_ghostHeap","_heapIndex","_freeGhosts"})
    Check(((Array)Field(a,field)).Cast<int>().SequenceEqual(((Array)Field(b,field)).Cast<int>()),"pool state "+field);
   foreach(string field in new[]{"_hasPose","_heapCount","_freeCount","_left","_right"})Check(Field(a,field).Equals(Field(b,field)),"state "+field);
  }
  for(int i=0;i<1000;i++){a.Clear();b.Clear();}EqualState();
  Check(a.TestClearWork==1&&b.TestClearWork==1001,"1000 disabled frames skip all repeated pool resets");
  for(int cycle=0;cycle<8;cycle++){
   a.Fire(spec,pose,pose,true,cycle%2==0);b.Fire(spec,pose,pose,true,cycle%2==0);
   for(int i=0;i<20;i++){a.Advance(.017,pose,pose);b.Advance(.017,pose,pose);}EqualState();
   int work=a.TestClearWork;a.Clear();b.Clear();EqualState();Check(a.TestClearWork==work+1,"activity re-arms the reset");
   a.Clear();b.Clear();EqualState();Check(a.TestClearWork==work+1,"repeat clear is idle");
  }
  a.SetPoses(pose,pose);b.SetPoses(pose,pose);a.Clear();b.Clear();EqualState();
  a.Advance(0,pose,pose);b.Advance(0,pose,pose);a.Clear();b.Clear();EqualState();
  int last=a.TestClearWork;a.Fire(spec,pose,pose,false,false);b.Fire(spec,pose,pose,false,false);a.Clear();b.Clear();EqualState();
  Check(a.TestClearWork==last,"no-emitter fire does not dirty a cleared pool");
  long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++)a.Clear();
  Check(GC.GetAllocatedBytesForCurrentThread()==before,"idle resets allocate zero bytes");
 }
}
'''


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    source = (ROOT / 'MainGameMilkSpray/LiquidRenderer.cs').read_text(encoding='utf-8-sig')
    # Preserve the original struct-moving sorter and unconditional mesh generation.
    reference = subprocess.check_output(['git', 'show', 'HEAD:MainGameMilkSpray/LiquidRenderer.cs'], cwd=ROOT).decode('utf-8-sig')
    reference = reference.replace('LiquidRenderer', 'ReferenceLiquidRenderer')
    assert 'DrawParticle first = _particles[0];' in reference and '_particleOrder' not in reference
    for name, content in {'LiquidRenderer.cs': source, 'Reference.cs': reference, 'Stubs.cs': STUBS, 'Program.cs': TESTS}.items():
        (OUT / name).write_text(content, encoding='utf-8')
    (OUT / 'LiquidMath.cs').write_text((ROOT / 'MainGameMilkSpray/LiquidMath.cs').read_text(encoding='utf-8-sig'), encoding='utf-8')
    simulation = (ROOT / 'MainGameMilkSpray/LiquidSimulation.cs').read_text(encoding='utf-8-sig')
    # Count the actual expensive branch only in the isolated copy; production adds no counters.
    simulation = simulation.replace('public void Clear()', '[System.Text.Json.Serialization.JsonIgnore] public int TestClearWork;\n        public void Clear()')
    simulation = simulation.replace('foreach (var b in Bursts)', 'TestClearWork++; foreach (var b in Bursts)')
    (OUT / 'LiquidSimulation.cs').write_text(simulation, encoding='utf-8')
    old_simulation = simulation.replace('LiquidSimulation', 'ReferenceLiquidSimulation').replace('if (_clearStateValid) return;', '')
    (OUT / 'ReferenceSimulation.cs').write_text(old_simulation, encoding='utf-8')
    (OUT / 'dummy.bundle').write_bytes(b'0')
    (OUT / 'Checks.csproj').write_text('''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><NoWarn>CS0649;CS0414</NoWarn></PropertyGroup><ItemGroup><EmbeddedResource Include="dummy.bundle" LogicalName="MainGameMilkSpray.milkliquid.bundle" /></ItemGroup></Project>''', encoding='utf-8')
    subprocess.run(['dotnet', 'run', '--project', str(OUT / 'Checks.csproj'), '-c', 'Release', '--nologo'], cwd=ROOT, check=True)


if __name__ == '__main__':
    main()
