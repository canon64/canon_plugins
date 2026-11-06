"""Build isolated checks from the actual C# methods; Unity rendering is not emulated."""
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / '_diagnostics' / 'perf-reduction-20261027' / 'checks'


def read(path):
    return (ROOT / path).read_text(encoding='utf-8-sig')


def member(source, signature):
    start = source.index(signature)
    brace = source.index('{', start)
    depth = 1
    end = brace + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]


STUBS = r'''
using System;
using System.Collections.Generic;
using System.Linq;
namespace UnityEngine {
 public class Object {
  static int serial; readonly int id=++serial;
  public int GetInstanceID()=>id;
  public static readonly List<Object> Registry=new List<Object>();
  public static int Queries;
  public static Object[] FindObjectsOfType(Type t) {
   if(t==typeof(MonoBehaviour)) throw new Exception("global MonoBehaviour scan");
   Queries++; return Registry.Where(x=>t.IsInstanceOfType(x)).ToArray();
  }
  public static T[] FindObjectsOfType<T>() where T:Object => FindObjectsOfType(typeof(T)).Cast<T>().ToArray();
 }
 public class GameObject:Object {public bool activeSelf=true; public void SetActive(bool b)=>activeSelf=b;}
 public class MonoBehaviour:Object {public bool enabled=true; public GameObject gameObject=new GameObject(); public T GetComponentInParent<T>() where T:class=>null;}
 public class Renderer:Object {}
 public class Light:Object {}
 public static class Time {public static float time, unscaledTime;}
 public struct Vector2 {
  public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;}
  public float magnitude=>(float)Math.Sqrt(x*x+y*y);
  public static Vector2 right=>new Vector2(1,0);
  public static Vector2 operator +(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
  public static Vector2 operator -(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
  public static Vector2 operator /(Vector2 a,float b)=>new Vector2(a.x/b,a.y/b);
  public static Vector2 operator *(Vector2 a,float b)=>new Vector2(a.x*b,a.y*b);
 }
 public static class Mathf {
  public const float PI=(float)Math.PI;
  public static float Max(float a,float b)=>Math.Max(a,b);
  public static int Max(int a,int b)=>Math.Max(a,b);
  public static float Clamp(float v,float a,float b)=>Math.Min(b,Math.Max(a,v));
  public static float Clamp01(float v)=>Clamp(v,0,1);
  public static float InverseLerp(float a,float b,float v)=>a==b?0:Clamp01((v-a)/(b-a));
  public static float Pow(float a,float b)=>(float)Math.Pow(a,b);
  public static float Sin(float a)=>(float)Math.Sin(a);
  public static float Exp(float a)=>(float)Math.Exp(a);
 }
 public class Mesh {public Vector2[] Last; public Vector2[] uv {get=>throw new Exception("UV getter called"); set=>Last=value;}}
}
public class ChaControl:UnityEngine.MonoBehaviour {
 public UnityEngine.Renderer[] Renderers=Array.Empty<UnityEngine.Renderer>(); public int Reads;
 public T[] GetComponentsInChildren<T>(bool includeInactive) {Reads++;return (T[])(object)Renderers;}
 public void ReloadNoAsync(){}
 public System.Collections.IEnumerator ReloadAsync(){yield return null;}
}
namespace UnityEngine.SceneManagement {
 public struct Scene {}
 public static class SceneManager {
  public static event Action<Scene> sceneUnloaded;
  public static void Unload()=>sceneUnloaded?.Invoke(new Scene());
 }
}
namespace HarmonyLib {
 public class Harmony {public Harmony(string id){} public void Patch(System.Reflection.MethodInfo m,HarmonyMethod postfix=null){} public void UnpatchSelf(){} }
 public class HarmonyMethod {public HarmonyMethod(Type t,string name){} }
}
namespace BepInEx.Configuration {
 public class ConfigEntry<T> {public T Value; public ConfigEntry(T v){Value=v;}}
 public class ConfigFile {public bool? EnabledOverride; public ConfigEntry<T> Bind<T>(string s,string k,T v,string d)=>new ConfigEntry<T>(s=="General"&&k=="Enabled"&&EnabledOverride.HasValue?(T)(object)EnabledOverride.Value:v);}
}
namespace BepInEx.Logging {public class ManualLogSource {public void LogInfo(string s){} public void LogError(string s){throw new Exception(s);}}}
namespace BepInEx {
 public class BaseUnityPlugin:UnityEngine.MonoBehaviour {public BepInEx.Configuration.ConfigFile Config=new BepInEx.Configuration.ConfigFile();public BepInEx.Logging.ManualLogSource Logger=new BepInEx.Logging.ManualLogSource();}
 [AttributeUsage(AttributeTargets.Class)] public class BepInPlugin:Attribute {public BepInPlugin(string a,string b,string c){}}
 [AttributeUsage(AttributeTargets.Class,AllowMultiple=true)] public class BepInProcess:Attribute {public BepInProcess(string s){}}
 [AttributeUsage(AttributeTargets.Class,AllowMultiple=true)] public class BepInDependency:Attribute {public BepInDependency(string s){}}
}
namespace KKAPI.Chara {public static class CharacterApi {public static int Registrations; public static void RegisterExtraBehaviour<T>(string s){Registrations++;}}}
namespace SimpleAfterimage {public static class Plugin {public static void SetKaleidoscopeThoughtMask(bool active,ChaControl actor){}}}
namespace MainGameClothColliderCards {public class ColliderCardController {}}
static class Check {public static int Count;public static void That(bool b,string m){Count++;if(!b)throw new Exception(m);}}
'''


def circular():
    current = read('MainGameCircularDisplacement/Plugin.cs')
    # Pin the pre-change implementation; replay does not depend on the working HEAD.
    previous = subprocess.check_output(
        ['git', 'cat-file', 'blob', '3c0f1b06e522aee2757f205a945018f5898cb79b'], cwd=ROOT).decode('utf-8-sig')
    methods = '\n'.join(member(current, signature) for signature in [
        'private DistortionParameters CaptureDistortionParameters()',
        'private struct DistortionParameters',
        'private static Vector2 Distort(', 'private void UpdateUv()', 'private void UpdateWarpUv()'])
    methods += '\n' + member(previous, 'private Vector2 Distort(Vector2 uv)').replace('Distort(Vector2 uv)', 'LegacyDistort(Vector2 uv)')
    fields = '\n'.join(line.strip().replace('private ConfigEntry', 'private BepInEx.Configuration.ConfigEntry')
                       for line in current.splitlines() if 'private ConfigEntry<' in line)
    field_names = re.findall(r'(_\w+)', fields)
    setup = []
    for line in fields.splitlines():
        typ = re.search(r'ConfigEntry<([^>]+)>', line).group(1)
        val = {'bool': 'false', 'int': '5', 'float': '.37f', 'Mode': 'Mode.Radial', 'string': '""'}[typ]
        for name in re.findall(r'_\w+', line):
            setup.append(f'{name}=new BepInEx.Configuration.ConfigEntry<{typ}>({val});')
    return 'using System;\nusing UnityEngine;\nclass CircularChecks {\n' + fields + r'''
 enum Mode {Off,Radial,Ripple,Swirl,Ring}
 bool external; int stateReads;
 Mode _externalMode;
 float _externalCenterX,_externalCenterY,_externalRadius,_externalInner,_externalStrength,_externalIntensity;
 bool IsExternalActiveNow(){stateReads++;return external;}
 Mesh _mesh=new Mesh(),_warpMesh=new Mesh(); Vector2[] _uvBuffer,_warpUvBuffer;
 int _meshGX=48,_meshGY=27,_warpMeshGX=48,_warpMeshGY=27;
''' + methods + '\n public static void Run(){new CircularChecks().Verify();}\n void Verify(){' + '\n'.join(setup) + r'''
 var random=new Random(271027); int comparisons=0;
 foreach(bool ext in new[]{false,true}) foreach(Mode m in Enum.GetValues(typeof(Mode)))
 for(int scenario=0;scenario<12;scenario++) {
  external=ext;_mode.Value=_externalMode=m;
  _cx.Value=_externalCenterX=.5f;_cy.Value=_externalCenterY=.5f;
  _radius.Value=.35f;_inner.Value=.05f;_falloff.Value=scenario%3==0?0:1.7f;
  _externalRadius=scenario%3==0?-1:.42f;_externalInner=scenario%2==0?-1:.1f;
  _externalStrength=scenario%2==0?0:-.2f;_externalIntensity=scenario%3==0?0:.8f;
  _ringSpeed.Value=scenario%2==0?0:.8f;_ringWidth.Value=scenario%3==0?0:.03f;
  _waveCount.Value=scenario%2==0?0:5;Time.time=scenario*7.13f;
  var p=CaptureDistortionParameters();
  var points=new[]{new Vector2(.5f,.5f),new Vector2(.5f+p.Radius,.5f),new Vector2(0,0),new Vector2(1,1)};
  foreach(var uv in points) Compare(uv,p,ref comparisons);
  for(int y=0;y<=27;y++) for(int x=0;x<=48;x++) {
   Compare(new Vector2(x/48f,1-y/27f),p,ref comparisons);
   Compare(new Vector2(x/48f,y/27f),p,ref comparisons);
  }
  for(int i=0;i<20;i++) Compare(new Vector2((float)random.NextDouble(),(float)random.NextDouble()),p,ref comparisons);
 }
 _uvBuffer=new Vector2[49*28];_warpUvBuffer=new Vector2[49*28];
 var first=_uvBuffer; var second=_warpUvBuffer;stateReads=0;
 for(int i=0;i<120;i++){UpdateUv();UpdateWarpUv();}
 Check.That(stateReads==240,"state read once per UV update");
 Check.That(ReferenceEquals(first,_mesh.Last)&&ReferenceEquals(second,_warpMesh.Last),"UV buffers retained across renders");
 Console.WriteLine("CIRCULAR_OK comparisons="+comparisons+" renders=240 UV_getter_calls=0");
 }
 void Compare(Vector2 uv,DistortionParameters p,ref int count) {
  var a=LegacyDistort(uv);var b=Distort(uv,p);count++;
  Check.That(BitConverter.SingleToInt32Bits(a.x)==BitConverter.SingleToInt32Bits(b.x)&&BitConverter.SingleToInt32Bits(a.y)==BitConverter.SingleToInt32Bits(b.y),"UV mismatch");
 }
}
'''


RENDERER_TESTS = r'''
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace MainGameKaleidoscope {
 public sealed partial class Plugin {
  public const string Guid="checks"; public static Plugin Instance;
  BepInEx.Logging.ManualLogSource Logger=new BepInEx.Logging.ManualLogSource();
  readonly Dictionary<ChaControl,Renderer[]> _rendererCache=new Dictionary<ChaControl,Renderer[]>();
  ChaControl[] _characters=Array.Empty<ChaControl>();Light[] _lights=Array.Empty<Light>();
  GameObject _dildoModel;Renderer[] _dildoRenderers=Array.Empty<Renderer>();
  float _nextCharacterScan,_nextLightScan,_nextDildoScan;
  public static void RunChecks() {
   var p=Instance=new Plugin();p.InstallRendererInvalidation();
   var a=new ChaControl{Renderers=new[]{new Renderer()}};var b=new ChaControl{Renderers=new[]{new Renderer()}};
   p._characters=new[]{a,b};p.GetCharacterRenderers(a);p.GetCharacterRenderers(b);
   for(int i=0;i<120;i++){p.GetCharacterRenderers(a);p.GetCharacterRenderers(b);}
   Check.That(a.Reads==1&&b.Reads==1,"unchanged cache reused");
   RendererChangeCompleted(a);RendererChangeCompleted(a);p.GetCharacterRenderers(a);p.GetCharacterRenderers(b);
   Check.That(a.Reads==2&&b.Reads==1,"coalesce and isolate invalidation");
   bool completed=false;
   IEnumerator Operation(){yield return "loading";a.Renderers=new[]{new Renderer()};completed=true;}
   var operation=NotifyRendererChangeOnCompletion(Operation(),a);
   Check.That(operation.MoveNext()&&!completed,"async yields before completion");p.GetCharacterRenderers(a);
   Check.That(a.Reads==2,"no premature async completion notification");
   Check.That(!operation.MoveNext()&&completed,"async completes");p.GetCharacterRenderers(a);
   Check.That(a.Reads==3,"async completion invalidates");
   a.Renderers[0]=null;p.GetCharacterRenderers(a);Check.That(a.Reads==4,"missing renderer recovered");
   p._characters=new[]{b};p.PruneRendererCache();Check.That(!p._rendererCache.ContainsKey(a),"departed actor released");
   UnityEngine.SceneManagement.SceneManager.Unload();Check.That(p._rendererCache.Count==0,"scene unload releases cache");
   p.GetCharacterRenderers(b);Check.That(b.Reads==2,"fresh capture after release");
   p.ClearRendererCache();p.RemoveRendererInvalidation();
   Console.WriteLine("KALEIDOSCOPE_OK stable=120 targeted/coalesced/async/missing/departure/scene checks passed");
  }
 }
}
'''


def doll():
    source = read('MainGameDollMode/Plugin.cs')
    signatures = [
        'private List<MonoBehaviour> GetAhegaoBehaviours(', 'private void RescanAhegaoBehaviours(',
        'private List<MonoBehaviour> GetFixationalBehaviours(', 'private void RescanFixationalBehaviours(',
        'private static int CollectFixationalByKnownTypes(', 'private static bool IsAhegaoBehaviour(',
        'private void TryDisableFixationalComponents(', 'private void RestoreFixationalComponents(',
        'private void TryDisableAhegaoComponents(', 'private void RestoreAhegaoComponents(']
    methods = '\n'.join(member(source, s) for s in signatures)
    return r'''
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
namespace MainGameDollMode {
 static class DollModeGuardHooks {
  internal static void EnsureFixationalBlocks(HarmonyLib.Harmony h,Type[] t){}
  internal static bool EnsureMainGameAhegaoBlock(HarmonyLib.Harmony h)=>true;
 }
 public partial class Plugin {
  class Settings {public bool DisableAhegaoPlugins=true;}
  Settings _settings=new Settings();HarmonyLib.Harmony _harmony=new HarmonyLib.Harmony("checks");
  string _cachedAhegaoKeywordFingerprint="never_match_271027";
  string _lastAhegaoKeywordFingerprint="";
  float _nextFixationalRescanAt,_nextAhegaoRescanAt;
  int _lastFixationalRescanCount=-1,_lastAhegaoRescanCount=-1;
  const float FixationalRescanIntervalSeconds=2,AhegaoRescanIntervalSeconds=2;
  readonly List<MonoBehaviour> _cachedFixationalBehaviours=new List<MonoBehaviour>(),_cachedAhegaoBehaviours=new List<MonoBehaviour>();
  readonly HashSet<int> _cachedFixationalIds=new HashSet<int>(),_cachedAhegaoIds=new HashSet<int>();
  readonly Dictionary<int,bool> _originalFixationalEnabledByComponent=new Dictionary<int,bool>(),_originalAhegaoEnabledByComponent=new Dictionary<int,bool>(),_originalAhegaoActiveByGameObject=new Dictionary<int,bool>();
  List<string> GetAhegaoKeywordsCached()=>new List<string>{_cachedAhegaoKeywordFingerprint};
  void LogInfo(string s){} void LogWarn(string s){throw new Exception(s);}
  bool IsSceneBehaviourCandidate(MonoBehaviour b)=>b!=null;
  bool IsSceneGameObject(GameObject b)=>b!=null;
  bool ShouldAffectFixationalBehaviour(MonoBehaviour b)=>b!=null;
  bool ShouldAffectAhegaoBehaviour(MonoBehaviour b)=>IsAhegaoBehaviour(b,GetAhegaoKeywordsCached());
  void RecordEyeTraceSignal(string a,string b){} void OnExternalPluginBlocked(string a){}
''' + methods + r'''
  static Type Emit(string assembly,string name) {
   var a=AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assembly),AssemblyBuilderAccess.Run);
   return a.DefineDynamicModule(assembly).DefineType(name,TypeAttributes.Public,typeof(MonoBehaviour)).CreateType();
  }
  public static void RunChecks() {
   var p=new Plugin();AppDomain.CurrentDomain.AssemblyLoad+=p.OnOptionalAssemblyLoaded;
   UnityEngine.Object.Queries=0;
   for(int i=0;i<30;i++){Time.unscaledTime=i*3;p.GetAhegaoBehaviours(false);p.GetFixationalBehaviours(false);}
   Check.That(UnityEngine.Object.Queries==0,"no instance query when optional types absent");
   int version=p._resolvedOptionalAssemblyVersion;
   var fix=Emit("LateEyes271027","LateFixationalEyeMovement");
   var ah=Emit("LateFace271027","LateAhegao271027");
   Check.That(p._resolvedOptionalAssemblyVersion==version,"assembly callback only queues refresh");
   p._cachedAhegaoKeywordFingerprint="lateahegao271027";
   var f1=(MonoBehaviour)Activator.CreateInstance(fix);var f2=(MonoBehaviour)Activator.CreateInstance(fix);f2.enabled=false;
   var a1=(MonoBehaviour)Activator.CreateInstance(ah);var a2=(MonoBehaviour)Activator.CreateInstance(ah);a2.enabled=false;
   UnityEngine.Object.Registry.AddRange(new UnityEngine.Object[]{f1,f2,a1,a2});
   p.TryDisableFixationalComponents(out _,out _,out _,out _,out _);
   p.TryDisableAhegaoComponents(out _,out _,out _,out _,out _);
   Check.That(!f1.enabled&&!f2.enabled&&!a1.enabled&&!a2.enabled,"targets disabled");
   var late=(MonoBehaviour)Activator.CreateInstance(fix);UnityEngine.Object.Registry.Add(late);Time.unscaledTime+=3;
   p.TryDisableFixationalComponents(out _,out _,out _,out _,out _);Check.That(!late.enabled,"late instance detected");
   p.RestoreFixationalComponents(out _,out _,out _,out _);p.RestoreAhegaoComponents(out _,out _,out _,out _);
   Check.That(f1.enabled&&!f2.enabled&&a1.enabled&&!a2.enabled&&late.enabled,"original enabled states restored");
   p._cachedAhegaoKeywordFingerprint="absent_again_271027";p.GetAhegaoBehaviours(false);
   Check.That(p._optionalAhegaoTypes.Length==0&&p._cachedAhegaoBehaviours.Count==0,"keyword changes invalidate type cache");
   AppDomain.CurrentDomain.AssemblyLoad-=p.OnOptionalAssemblyLoaded;
   Console.WriteLine("DOLLMODE_OK absent/late_assembly/late_instance/keyword/disable/restore checks passed");
  }
 }
}
'''


def main():
    print('Preparing performance reduction checks', flush=True)
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / 'Checks.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><EnableDefaultCompileItems>true</EnableDefaultCompileItems><NoWarn>CS0169;CS0414;CS0649</NoWarn></PropertyGroup></Project>', encoding='utf-8')
    sources = {'Stubs.cs': STUBS, 'CircularChecks.cs': circular(), 'RendererTests.cs': RENDERER_TESTS,
               'RendererCache.cs': read('MainGameKaleidoscope/Plugin.RendererCache.cs'),
               'DollTests.cs': doll(), 'DollOptionalTargets.cs': read('MainGameDollMode/Plugin.OptionalTargets.cs'),
               'ClothStartup.cs': read('MainGameClothColliderCards/Plugin.cs')}
    sources['Program.cs'] = r'''
using System;
class Program {static void Main(){
 CircularChecks.Run();MainGameKaleidoscope.Plugin.RunChecks();MainGameDollMode.Plugin.RunChecks();
 var p=new MainGameClothColliderCards.Plugin();
 var awake=typeof(MainGameClothColliderCards.Plugin).GetMethod("Awake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
 awake.Invoke(p,null);
 Check.That(!MainGameClothColliderCards.Plugin.IsRunning&&!p.enabled&&KKAPI.Chara.CharacterApi.Registrations==0,"cloth disabled at startup without registration");
 var enabled=new MainGameClothColliderCards.Plugin();enabled.Config.EnabledOverride=true;awake.Invoke(enabled,null);
 Check.That(MainGameClothColliderCards.Plugin.IsRunning&&KKAPI.Chara.CharacterApi.Registrations==1,"cloth opt-in on restart");
 Console.WriteLine("CLOTH_OK disabled default and explicit opt-in");Console.WriteLine("ALL_OK checks="+Check.Count);
}}
'''
    for name, source in sources.items():
        (OUT / name).write_text(source, encoding='utf-8')
    # The HTTP guard must precede JSON processing and main-thread dispatch.
    http = read('MainGameBlankMapAdd/Plugin.HttpCloth.cs')
    assert http.index('if (!MainGameClothColliderCards.Plugin.IsRunning)') < http.index('ClothParameters input = null;')
    assert 'LocalHttpResponse.Message(503, "cloth test plugin disabled' in http
    rendering = read('MainGameKaleidoscope/Plugin.Rendering.cs')
    assert '_nextRendererRefresh' not in rendering
    assert 'FindObjectsOfType<MonoBehaviour>()' not in read('MainGameDollMode/Plugin.cs')
    print('Running extracted production methods with isolated engine stubs', flush=True)
    subprocess.run(['dotnet', 'run', '--project', str(OUT / 'Checks.csproj'), '-c', 'Release', '--nologo'], check=True, cwd=ROOT)


if __name__ == '__main__':
    main()
