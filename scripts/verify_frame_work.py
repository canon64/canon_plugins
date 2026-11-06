"""Exercise actual cache/mask/scheduler/effect methods with isolated Unity objects."""
from pathlib import Path
import subprocess
from verify_perf_reductions import STUBS, member, read, ROOT

OUT = ROOT / '_diagnostics' / 'perf-reduction-20261027' / 'frame-checks'


def afterimage():
    source = read('SimpleAfterimage/Plugin.Capture.cs')
    methods = '\n'.join(member(source, s) for s in [
        'public static void SetKaleidoscopeThoughtMask(bool active, ChaControl actor,',
        'private void HideKaleidoscopeActorForCapture()', 'private void RestoreKaleidoscopeActorAfterCapture()'])
    return '''using System;using System.Collections.Generic;using UnityEngine;
namespace AfterimageChecks {public sealed class Plugin {
 public static Plugin Instance=new Plugin();
 bool _kaleidoscopeThoughtMaskActive;ChaControl _kaleidoscopeThoughtActor;
 Func<ChaControl,Renderer[]> _kaleidoscopeRendererProvider;
 readonly List<Renderer> _kaleidoscopeHiddenRenderers=new List<Renderer>();
 public static void SetKaleidoscopeThoughtMask(bool active,ChaControl actor)=>SetKaleidoscopeThoughtMask(active,actor,null);
 public void Capture(Action render){try{HideKaleidoscopeActorForCapture();render();}finally{RestoreKaleidoscopeActorAfterCapture();}}
 public static bool IsReleased=>Instance._kaleidoscopeThoughtActor==null&&Instance._kaleidoscopeRendererProvider==null;
''' + methods + '}}'


MASK_TESTS = r'''
using System;using System.Collections;using System.Collections.Generic;using UnityEngine;
namespace MainGameKaleidoscope { public sealed partial class Plugin {
 public const string Guid="checks"; public static Plugin Instance;
 BepInEx.Logging.ManualLogSource Logger=new BepInEx.Logging.ManualLogSource();
 readonly Dictionary<ChaControl,Renderer[]> _rendererCache=new Dictionary<ChaControl,Renderer[]>();
 ChaControl[] _characters=Array.Empty<ChaControl>();Light[] _lights=Array.Empty<Light>();
 GameObject _dildoModel;Renderer[] _dildoRenderers=Array.Empty<Renderer>();
 float _nextCharacterScan,_nextLightScan,_nextDildoScan;
 public static void RunChecks(){
  var p=Instance=new Plugin();p._afterimageRendererProvider=p.GetCharacterRenderers;
  var visible=new Renderer();var disabled=new Renderer{enabled=false};var a=new ChaControl{Renderers=new[]{visible,disabled}};
  var after=AfterimageChecks.Plugin.Instance;
  AfterimageChecks.Plugin.SetKaleidoscopeThoughtMask(true,a,p._afterimageRendererProvider);
  Action draw=()=>Check.That(!visible.enabled&&!disabled.enabled,"hide actor during capture");
  for(int i=0;i<120;i++){after.Capture(draw);Check.That(visible.enabled&&!disabled.enabled,"restore only originally enabled renderers");}
  Check.That(a.Reads==1,"one shared hierarchy query across 120 captures");
  p.GetCharacterRenderers(a);Check.That(a.Reads==1,"owner reuses the very same cache");
  var replacement=new Renderer();a.Renderers=new[]{replacement};RendererChangeCompleted(a);RendererChangeCompleted(a);
  after.Capture(()=>Check.That(!replacement.enabled,"clothes changed after Update are excluded"));
  Check.That(a.Reads==2&&replacement.enabled,"targeted coalesced refresh before capture");
  try{after.Capture(()=>throw new InvalidOperationException());}catch(InvalidOperationException){}
  Check.That(replacement.enabled,"capture exception restores visibility");
  var b=new ChaControl{Renderers=new[]{new Renderer()}};
  AfterimageChecks.Plugin.SetKaleidoscopeThoughtMask(true,b,p._afterimageRendererProvider);after.Capture(()=>{});
  Check.That(a.Reads==2&&b.Reads==1,"actor replacement uses new actor");
  p.ClearRendererCache();Check.That(AfterimageChecks.Plugin.IsReleased,"scene clear releases cross-plugin references");
  AfterimageChecks.Plugin.SetKaleidoscopeThoughtMask(false,null);int before=b.Reads;after.Capture(()=>{});
  Check.That(b.Reads==before,"inactive mask performs no lookup");
  Console.WriteLine("MASK_OK stable/late reload/swap/exception/release; 120 captures -> 1 hierarchy query");
 }
}}
'''


def subcamera():
    source = read('MainGameSubCameraDisplayProbe/Plugin.CharactersOnly.cs')
    fields = source[source.index('        private const int CharactersOnlyDedicatedLayer'):source.index('        private void EnableCharactersOnlyHook()')]
    methods = '\n'.join(member(source, s) for s in [
        'private void OnCharactersOnlyPreCull(', 'private void OnCharactersOnlyPostRender(',
        'private void RestoreCharactersOnlySwapped()', 'private void RebuildCharactersOnlyKeepObjects()'])
    return '''using System;using System.Collections;using System.Collections.Generic;using UnityEngine;using Object=UnityEngine.Object;
namespace MainGameSubCameraDisplayProbe {public sealed partial class Plugin {
 public const string Guid="subchecks";public static Plugin Instance;
 class Settings {public bool CharactersOnlyMode=true;}
 Settings _settings=new Settings();Camera _subCamera=new Camera(),_renderScopeCamera;
 bool _runtimeLoggingEnabled;
 void LogInfo(string s){}void LogWarn(string s){throw new Exception(s);}
 void RestoreExternalRenderScope(){if(_charactersOnlyApplying&&_renderScopeCamera!=null)_renderScopeCamera.cullingMask=_charactersOnlySavedCullingMask;RestoreCharactersOnlySwapped();_charactersOnlyApplying=false;_renderScopeCamera=null;}
''' + fields + methods + r'''
 public static void RunChecks(){
  var p=Instance=new Plugin();UnityEngine.Object.Registry.Clear();UnityEngine.Object.Queries=0;
  for(int i=0;i<120;i++){Time.unscaledTime=i/60f;p.OnCharactersOnlyPreCull(p._subCamera);p.OnCharactersOnlyPostRender(p._subCamera);}
  Check.That(UnityEngine.Object.Queries<=10&&p._charactersOnlyKeepObjects.Count==0,"negative actor result respects discovery interval");
  var a=new ChaControl{Renderers=new[]{new Renderer()}};var b=new ChaControl{Renderers=new[]{new Renderer()}};
  a.Renderers[0].gameObject.layer=8;b.Renderers[0].gameObject.layer=9;
  UnityEngine.Object.Registry.AddRange(new UnityEngine.Object[]{a,b});Time.unscaledTime=3;
  p.OnCharactersOnlyPreCull(p._subCamera);p.OnCharactersOnlyPostRender(p._subCamera);
  for(int i=0;i<600;i++){Time.unscaledTime=3+i/60f;p.OnCharactersOnlyPreCull(p._subCamera);p.OnCharactersOnlyPostRender(p._subCamera);}
  Check.That(a.Reads==1&&b.Reads==1,"no periodic renderer cache purge after ten seconds");
  Check.That(a.Renderers[0].gameObject.layer==8&&b.Renderers[0].gameObject.layer==9,"layers restored");
  a.Renderers=new[]{new Renderer()};CharactersOnlyVisualsChanged(a);CharactersOnlyVisualsChanged(a);
  p.OnCharactersOnlyPreCull(p._subCamera);p.OnCharactersOnlyPostRender(p._subCamera);
  Check.That(a.Reads==2&&b.Reads==1,"only changed actor rescanned");
  bool done=false;IEnumerator Load(){yield return null;a.Renderers=new[]{new Renderer()};done=true;}
  var load=CharactersOnlyNotifyAfterCompletion(Load(),a);Check.That(load.MoveNext()&&!done,"async still loading");
  Check.That(!load.MoveNext()&&done,"async finished");p.OnCharactersOnlyPreCull(p._subCamera);p.OnCharactersOnlyPostRender(p._subCamera);
  Check.That(a.Reads==3&&b.Reads==1,"async completion refreshes target only");
  UnityEngine.Object.Registry.Remove(a);p.RefreshCharactersOnlyActors();p.RebuildCharactersOnlyKeepObjects();
  Check.That(!p._charactersOnlyRendererCache.ContainsKey(a),"departed actor released");
  p.ClearCharactersOnlyCache();Check.That(p._charactersOnlyRendererCache.Count==0&&p._charactersOnlyKeepObjects.Count==0,"stop releases cache");
  Console.WriteLine("SUBCAMERA_OK empty=120 frames bounded discovery; stable=600 frames no hierarchy rescan; async/layers/removal passed");
 }
}}
'''


SCHEDULER_TESTS = r'''
using System;using System.Collections.Generic;using MainGameVoiceFaceEventBridge.Scheduling;
static class SchedulerChecks {
 public static void Run(){
  var s=new StableActionScheduler();var order=new List<int>();s.Schedule(100,()=>order.Add(9));
  s.DrainDue(0,null);long before=GC.GetAllocatedBytesForCurrentThread();
  for(int i=0;i<10000;i++)s.DrainDue(0,null);
  Check.That(GC.GetAllocatedBytesForCurrentThread()==before,"future-only drains allocate nothing");
  s.Schedule(2,()=>order.Add(2));s.Schedule(1,()=>order.Add(1));s.Schedule(2,()=>order.Add(3));s.DrainDue(2,null);
  Check.That(string.Join(",",order)=="1,2,3","chronological stable order");
  s.Clear();order.Clear();s.Schedule(0,()=>{order.Add(1);s.Schedule(0,()=>order.Add(2));s.DrainDue(0,null);});s.Schedule(0,()=>order.Add(3));
  s.DrainDue(0,null);Check.That(string.Join(",",order)=="1,2,3","recursive drain preserves outer snapshot");
  s.Schedule(0,()=>throw new Exception());try{s.DrainDue(0,e=>throw e);}catch(Exception){}
  s.Schedule(0,()=>order.Add(4));Check.That(s.DrainDue(0,null)==1&&order[order.Count-1]==4,"error handler failure does not retain buffer");
  Console.WriteLine("SCHEDULER_OK future=10000 drains 0 bytes; stable order/reentrancy/errors passed");
 }
}
'''


GAUGE_TESTS = r'''
using System;using System.Reflection;using System.Reflection.Emit;using UnityEngine;
class HFlag {public enum EMode{aibu} public EMode mode;}
namespace MainGameVrDildo {public sealed partial class Plugin {
 enum TargetKind {None,Oral}
 class Settings {public bool Enabled=true,OralFluidEnabled=false,MotionGaugeEnabled=true,SweatEnabled=true;public float MotionGaugeFullCm=1,SweatStage1GaugeFraction=.1f,SweatStage2GaugeFraction=.5f,SweatStage3GaugeFraction=.9f,OralTearStepSeconds=1,OralDroolStepSeconds=1;public int SweatStage1Level=1,SweatStage2Level=2,SweatStage3Level=3,OralTearLevel=0,OralDroolLevel=0;}
 class Scene {public bool isActiveAndEnabled=true;public HFlag flags=new HFlag();}
 Settings _settings=new Settings();ChaControl _female=new ChaControl();Scene _scene=new Scene();GameObject _model;
 TargetKind _selectedTarget;bool _oralContact;float _motionGaugeCm=.6f;
 void LogDiagnostic(string s){throw new Exception(s);}
 static Type EmitController(){
  var a=AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("LateSkinEffects"),AssemblyBuilderAccess.Run);
  var t=a.DefineDynamicModule("skin").DefineType("KK_SkinEffects.SkinEffectsController",TypeAttributes.Public,typeof(MonoBehaviour));
  foreach(string name in new[]{"SweatLevel","TearLevel","DroolLevel"}){
   var f=t.DefineField("_"+name,typeof(int),FieldAttributes.Private);var p=t.DefineProperty(name,PropertyAttributes.None,typeof(int),Type.EmptyTypes);
   var get=t.DefineMethod("get_"+name,MethodAttributes.Public|MethodAttributes.SpecialName,typeof(int),Type.EmptyTypes);var il=get.GetILGenerator();il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldfld,f);il.Emit(OpCodes.Ret);p.SetGetMethod(get);
   var set=t.DefineMethod("set_"+name,MethodAttributes.Public|MethodAttributes.SpecialName,typeof(void),new[]{typeof(int)});il=set.GetILGenerator();il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldarg_1);il.Emit(OpCodes.Stfld,f);il.Emit(OpCodes.Ret);p.SetSetMethod(set);
  }return t.CreateType();
 }
 public static void RunChecks(){
  var p=new Plugin();AppDomain.CurrentDomain.AssemblyLoad+=p.GaugeEffectAssemblyLoaded;
  for(int i=0;i<120;i++)p.UpdateGaugeEffects();Check.That(p._effectResolvedAssemblyVersion==-1,"unused effects do not resolve any API");
  p._model=new GameObject();p.UpdateGaugeEffects();int version=p._effectResolvedAssemblyVersion;
  for(int i=0;i<120;i++)p.UpdateGaugeEffects();Check.That(p._effectResolvedAssemblyVersion==version&&p._effectControllerType==null,"missing type cached");
  var type=EmitController();p.UpdateGaugeEffects();
  int queries=p._female.ComponentReads;for(int i=0;i<120;i++)p.UpdateGaugeEffects();Check.That(p._female.ComponentReads==queries,"missing controller not probed every frame");
  var controller=(MonoBehaviour)Activator.CreateInstance(type);var sweat=type.GetProperty("SweatLevel");sweat.SetValue(controller,1);
  p._female.Components[type]=controller;Time.unscaledTime+=1;p.UpdateGaugeEffects();
  Check.That((int)sweat.GetValue(controller)==2,"late controller receives effect");
  var cached=p._effectProperties[0];for(int i=0;i<120;i++)p.UpdateGaugeEffects();Check.That(ReferenceEquals(cached,p._effectProperties[0]),"property metadata retained");
  p._model=null;p.UpdateGaugeEffects();Check.That((int)sweat.GetValue(controller)==1,"original effect restored");
  p._model=new GameObject();p.UpdateGaugeEffects();sweat.SetValue(controller,3);p._model=null;p.UpdateGaugeEffects();
  Check.That((int)sweat.GetValue(controller)==3,"other controller's newer value preserved");
  AppDomain.CurrentDomain.AssemblyLoad-=p.GaugeEffectAssemblyLoaded;
  Console.WriteLine("GAUGE_OK inactive/missing type/missing component/late assembly/properties/restore/external overwrite passed");
 }
}}
'''


def ui_help():
    source = read('MainGameUiHelp/Plugin.cs')
    methods = '\n'.join(member(source, signature) for signature in [
        'private static void ReportObservationError(', 'private static void ObserveLabelSafely(',
        'private static void ObserveFieldSafely(', 'private static void LabelPrefix(',
        'private static void ButtonPrefix(', 'private static void TogglePrefix(',
        'private static void TextPrefix(', 'private static void SliderPrefix(', 'private static void GridPrefix('])
    return r'''
using System;using UnityEngine;
namespace HelpChecks {
 struct Rect {public float x,y,width,height;public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;}public bool Contains(Vector2 p)=>p.x>=x&&p.x<x+width&&p.y>=y&&p.y<y+height;}
 class GUIContent {public string text="label";}
 class Event {public static Event current=new Event();public Vector2 mousePosition;}
 static class HelpCatalog {public static string Range(float a,float b,float c)=>"range";}
 class Plugin {
  class Log {public int Errors;public void LogError(string s){Errors++;}}
  static Plugin _instance=new Plugin();bool _reportedError;Log Logger=new Log();
  bool Observing=true,Fail;int Calls;
  bool CanObserve(Rect r)=>Observing;
  void ObserveLabel(Rect r,GUIContent c,string k,bool? t=null){if(Fail)throw new Exception();Calls++;}
  void ObserveField(Rect r,string v,string range){if(Fail)throw new Exception();Calls++;}
  private static bool Relevant(Rect rect)=>_instance!=null&&_instance.CanObserve(rect);
''' + methods + r'''
  public static void Run(){
   var r=new Rect(0,0,100,100);var content=new GUIContent();LabelPrefix(r,content);
   long before=GC.GetAllocatedBytesForCurrentThread();
   for(int i=0;i<10000;i++)LabelPrefix(r,content);
   Check.That(GC.GetAllocatedBytesForCurrentThread()==before,"help prefix creates no per-control closure");
   _instance.Fail=true;LabelPrefix(r,content);ButtonPrefix(r,content);
   Check.That(_instance.Logger.Errors==1,"help errors reported once and original UI continues");
   _instance.Fail=false;_instance.Observing=false;var grid=new[]{content};before=GC.GetAllocatedBytesForCurrentThread();
   for(int i=0;i<10000;i++)GridPrefix(r,grid,1);
   Check.That(GC.GetAllocatedBytesForCurrentThread()==before,"unobserved selection grids allocate no closure");
   Console.WriteLine("HELP_OK 10000 observed prefixes and 10000 unobserved grids: 0 closure allocation bytes; error isolation passed");
  }
 }
 public static class Run {public static void Checks()=>Plugin.Run();}
}
'''


def handy():
    source = read('MainGameHandySync/Plugin.HampBpm.cs')
    method = member(source, 'private static float ResolveBpmFromGauge(')
    return r'''
using System;using System.Reflection;
namespace MainGameSpeedLimitBreak {class Plugin {private static float BpmFromGauge(float value)=>80+120*value;}}
namespace MainGameHandySync {
 static class SimpleFileLogger {public static void Info(string s){}}
 class Plugin {
  static Plugin Instance;bool ShouldLogNormal()=>false;
  static Type _slbType;static MethodInfo _slbBpmFromGauge;static Func<float,float> _slbBpmFromGaugeCall;static bool _slbResolved;
''' + method + r'''
  public static void Run(){
   Check.That(ResolveBpmFromGauge(.5f)==140&&_slbBpmFromGaugeCall!=null,"typed BPM delegate resolved");
   long before=GC.GetAllocatedBytesForCurrentThread();float sum=0;
   for(int i=0;i<10000;i++)sum+=ResolveBpmFromGauge(.5f);
   Check.That(sum==1400000&&GC.GetAllocatedBytesForCurrentThread()==before,"BPM calls do not allocate reflection argument arrays or box values");
   Console.WriteLine("HANDY_OK typed private-method binding; 10000 BPM calls: 0 allocation bytes");
  }
 }
 public static class Run {public static void Checks()=>Plugin.Run();}
}
'''


def main():
    print('Preparing frame-work regression checks', flush=True)
    OUT.mkdir(parents=True, exist_ok=True)
    stubs = STUBS.replace('public class Renderer:Object {}', 'public class Renderer:MonoBehaviour {}')
    stubs = stubs.replace('public class Light:Object {}', 'public class Light:MonoBehaviour {public bool isActiveAndEnabled=true;public int cullingMask=-1;} public class Camera:Object {public int cullingMask=-1;public string name="camera";}')
    stubs = stubs.replace('public bool activeSelf=true;', 'public bool activeSelf=true;public bool activeInHierarchy=true;public int layer;')
    stubs = stubs.replace('public static float time, unscaledTime;', 'public static float time, unscaledTime,deltaTime=1f/60;')
    stubs = stubs.replace('public static float Exp(float a)', 'public static int Min(int a,int b)=>Math.Min(a,b);public static int Clamp(int v,int a,int b)=>Math.Min(b,Math.Max(a,v));public static int FloorToInt(float f)=>(int)Math.Floor(f);public static float Exp(float a)')
    stubs = stubs.replace('public UnityEngine.Renderer[] Renderers=', 'public int ComponentReads;public Dictionary<Type,UnityEngine.Object> Components=new Dictionary<Type,UnityEngine.Object>();public UnityEngine.Object GetComponent(Type t){ComponentReads++;return Components.TryGetValue(t,out var o)?o:null;}public UnityEngine.Renderer[] Renderers=')
    sources = {'Stubs.cs': stubs, 'Afterimage.cs': afterimage(), 'MaskTests.cs': MASK_TESTS,
               'RendererCache.cs': read('MainGameKaleidoscope/Plugin.RendererCache.cs').replace('SimpleAfterimage.Plugin.', 'AfterimageChecks.Plugin.'),
               'SubCameraTests.cs': subcamera(), 'SubCameraCache.cs': read('MainGameSubCameraDisplayProbe/Plugin.CharactersOnlyCache.cs'),
               'Scheduler.cs': read('MainGameVoiceFaceEventBridge/Scheduling/StableActionScheduler.cs'), 'SchedulerTests.cs': SCHEDULER_TESTS,
               'GaugeEffects.cs': read('MainGameVrDildo/Plugin.GaugeEffects.cs'), 'GaugeTests.cs': GAUGE_TESTS,
               'UiHelpTests.cs': ui_help(), 'HandyTests.cs': handy(),
               'Program.cs': 'using System;class Program {static void Main(){MainGameKaleidoscope.Plugin.RunChecks();MainGameSubCameraDisplayProbe.Plugin.RunChecks();SchedulerChecks.Run();MainGameVrDildo.Plugin.RunChecks();HelpChecks.Run.Checks();MainGameHandySync.Run.Checks();Console.WriteLine("FRAME_CHECKS_OK checks="+Check.Count);}}'}
    for name, source in sources.items(): (OUT / name).write_text(source, encoding='utf-8')
    (OUT / 'Checks.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><NoWarn>CS0169;CS0414;CS0649</NoWarn></PropertyGroup></Project>', encoding='utf-8')
    shortcuts = read('MainGameVMDPlay/Plugin.ClothingShortcuts.cs')
    prefix = member(shortcuts, 'private static bool ClothesUpdatePrefix(')
    assert '.ToString()' not in prefix and '_clothesFields' not in shortcuts
    print('Executing production methods with isolated engine stubs', flush=True)
    subprocess.run(['dotnet', 'run', '--project', str(OUT / 'Checks.csproj'), '-c', 'Release', '--nologo'], cwd=ROOT, check=True)


if __name__ == '__main__': main()
