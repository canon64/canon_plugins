using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using CanonGate;
using UnityEngine;
namespace CanonPlugins.Local {
 internal static class SessionGate {
  private static IDisposable registration;
  private static GateApi.Permit permit;
  private static bool active;
  private static BaseUnityPlugin owner;
  private static readonly Dictionary<string,FieldInfo> fields=new Dictionary<string,FieldInfo>();
  internal static bool Allowed => active && permit.IsValid && owner!=null && owner.enabled;
  internal static bool IsInBlankRoom => Allowed;
  internal static bool IsInBlankRoomStrict => Allowed;
  internal static HSceneProc HScene {get;private set;}
  internal static MonoBehaviour Scene {get;private set;}
  internal static GateApi.SessionKind Kind {get;private set;}
  internal static ChaControl[] Actors {get;private set;}=new ChaControl[0];
  internal static T ReadScene<T>(string name) where T:class => Scene!=null && fields.TryGetValue(name,out var f)?f?.GetValue(Scene) as T:null;
  internal static GateApi.Permit Capture()=>Allowed?permit:default;
  internal static bool Tick(ref bool previous,Action ignored) {previous=Allowed;return Allowed;}
  internal static void Bind(BaseUnityPlugin instance,string guid,Action start,Action stop,bool maker=false) {
   owner=instance; owner.enabled=false;
   registration=GateApi.Register(guid,()=>{
    var c=GateApi.Current;
    if(c==null || (c.Kind!=GateApi.SessionKind.H && !(maker && c.Kind==GateApi.SessionKind.Maker))) return;
    Scene=c.Scene; HScene=c.HScene; Kind=c.Kind; fields.Clear();
    if(Scene!=null) for(var t=Scene.GetType();t!=null;t=t.BaseType)
     foreach(var f in t.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)) if(!fields.ContainsKey(f.Name)) fields.Add(f.Name,f);
    var actors=new List<ChaControl>();
    var females=ReadScene<System.Collections.IList>("lstFemale"); if(females!=null) foreach(var v in females) if(v is ChaControl a && a!=null) actors.Add(a);
    foreach(var n in new[]{"male","male1"}) {var a=ReadScene<ChaControl>(n);if(a!=null && !actors.Contains(a))actors.Add(a);}
    Actors=actors.ToArray();
    permit=GateApi.Capture(guid); active=true; owner.enabled=true;
    start();
   },()=>{
    bool cleanup=active; active=false; permit=default;
    try {if(cleanup) stop();} finally {owner.enabled=false; HScene=null;Scene=null;fields.Clear();Actors=new ChaControl[0];Kind=GateApi.SessionKind.None;}
   });
  }
  internal static void Cleanup(params Action[] actions){foreach(var action in actions)try{action();}catch(Exception ex){System.Diagnostics.Trace.TraceWarning("Session cleanup: "+ex);}}
  internal static void Dispose(){var r=registration;registration=null;try{r?.Dispose();}finally{active=false;permit=default;owner=null;HScene=null;Scene=null;fields.Clear();Actors=new ChaControl[0];Kind=GateApi.SessionKind.None;}}
 }
}
