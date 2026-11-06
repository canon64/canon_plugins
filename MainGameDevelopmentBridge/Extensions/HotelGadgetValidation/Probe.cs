using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using CanonPlugins.Shared;
using MainGameDevelopmentBridge;
using UnityEngine;

public sealed class HotelGadgetValidation : IDevelopmentCommand
{
    const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    [Serializable] public sealed class Result
    {
        public string guid, mode, stage, error, cleanupError, before, expected, immediate, afterFrame;
        public bool empty, missingSaved, immediatePassed, afterFramePassed, passed, nonemptyPassed, cleanupPassed;
    }
    [Serializable] public sealed class Manifest {public bool running,complete;public string mode,error;public string[] results;}
    [Serializable] sealed class CuffState {public int[] wrists;public Vector3 bend=Vector3.zero;}
    [Serializable] sealed class ComposerState {public string layout,preset=null;}
    [Serializable] sealed class ObjectLayout {public string format="ObjectLayoutV2";public string[] objectsJson;}
    [Serializable] sealed class Source {public string bundle,asset,manifest,name;public bool active;}
    [Serializable] sealed class Sources {public string[] itemRecords;}
    sealed class Item {internal BaseUnityPlugin plugin;internal Result result;internal string before;internal bool dirty;}
    public string Name=>"hotel-gadget-validation";
    readonly List<Item> _items=new List<Item>();
    MonoBehaviour _host;Transform _room;object _gate;Coroutine _routine;
    string _directory,_mode,_error;bool _running,_complete;
    static object Read(object value,string name)=>value==null?null:value.GetType().GetField(name,Flags)?.GetValue(value)??value.GetType().GetProperty(name,Flags)?.GetValue(value,null);
    static MethodInfo Method(object value,string name,int count)=>value.GetType().GetMethods(Flags).Single(m=>m.Name==name&&m.GetParameters().Length==count);
    static object Call(object value,string name,params object[] args)=>Method(value,name,args.Length).Invoke(value,args);
    static string Capture(BaseUnityPlugin plugin,Transform room)=>(string)Call(plugin,"CaptureHotelGadget",room);
    static object CurrentGate()=>ReflectionCommands.ResolveType("CanonGate.GateApi").GetProperty("Current",Flags).GetValue(null,null);
    void CheckSession()
    {
        var current=CurrentGate();
        if (!ReferenceEquals(current,_gate)||Convert.ToString(Read(current,"Kind"))!="H"||!Equals(Read(current,"IsHotelMode"),true)
            ||Convert.ToString(Read(current,"HotelRoomId"))!="201"||!Equals(Read(Read(_host,"_hSession"),"RoomReady"),true))
            throw new InvalidOperationException("Hotel room 201 H session changed or is not ready.");
    }
    public object Execute(Dictionary<string,object> args,DevelopmentContext context)
    {
        string action=ReflectionCommands.Text(args,"action","result");
        if(action=="result") return Status();
        if(action!="start") throw new ArgumentException("action must be start or result");
        if(_running) throw new InvalidOperationException("Validation is already running; use action=result.");
        _mode=ReflectionCommands.Text(args,"mode","saved");
        if(_mode!="saved"&&_mode!="roundtrip"&&_mode!="cuffs-bound"&&_mode!="objects-roundtrip") throw new ArgumentException("mode must be saved, roundtrip, cuffs-bound or objects-roundtrip");
        _host=(MonoBehaviour)Read(Chainloader.PluginInfos["com.kks.maingame.aishoujomode"].Instance,"_hotel");
        _gate=CurrentGate();CheckSession();
        _room=(Transform)Read(_host,"_sessionRoomRoot");if(_room==null) throw new InvalidOperationException("Room root missing");
        _directory=Path.Combine(context.OutputDirectory,"HotelGadgetValidation",DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff")+"-"+_mode);
        Directory.CreateDirectory(_directory);_items.Clear();_error=null;_complete=false;
        var saved=new Dictionary<string,string>();
        foreach(var entry in ((IEnumerable)Read(Read(_host,"_roomState"),"gadgets")??new object[0]))
            saved[Convert.ToString(Read(entry,"guid"))]=Convert.ToString(Read(entry,"payload"));
        string filter=ReflectionCommands.Text(args,"guid");
        foreach(var info in Chainloader.PluginInfos.Values.OrderBy(p=>p.Metadata.GUID))
        {
            var provider=info.Instance;if(provider==null||provider.GetType().GetMethod("CaptureHotelGadget",new[]{typeof(Transform)})==null) continue;
            if(filter!=null&&info.Metadata.GUID!=filter) continue;
            var item=new Item {plugin=provider,result=new Result {guid=info.Metadata.GUID,mode=_mode,stage="capture-before"}};_items.Add(item);
            try
            {
                if(!Equals(Read(provider,"HotelGadgetAllowed"),true)) throw new InvalidOperationException("Provider is not allowed");
                item.before=Capture(provider,_room);GadgetRoomSnapshot.Decode(item.before);
                item.result.before=Write(item,"before",item.before);
                if(saved.TryGetValue(item.result.guid,out var payload)) item.result.expected=Write(item,"saved",payload);
                else item.result.missingSaved=true;
            }
            catch(Exception ex) {Fail(item,ex);}
        }
        if(_items.Count==0) throw new InvalidOperationException("No matching gadget provider found");
        _running=true;var routine=_host.StartCoroutine(Run());_routine=_running?routine:null;return Status();
    }
    object Status()=>new {running=_running,complete=_complete,mode=_mode,error=_error,directory=_directory,
        providers=_items.Select(i=>i.result.guid).ToArray(),results=_items.Select(i=>i.result).ToArray()};
    string Write(Item item,string stage,string text)
    {
        string path=Path.Combine(_directory,item.result.guid+"."+stage+".json");File.WriteAllText(path,text);return path;
    }
    void Fail(Item item,Exception ex) {item.result.error=(item.result.error==null?"":item.result.error+"\n")+item.result.stage+": "+ex.GetBaseException();item.result.passed=false;}
    bool Attempt(Item item,string stage,Action work)
    {
        item.result.stage=stage;
        try {CheckSession();work();return true;}catch(Exception ex) {Fail(item,ex);return false;}
    }
    IEnumerator Run()
    {
        try
        {
            foreach(var item in _items)
            {
                if(item.before==null||item.result.error!=null) continue;
                if(_mode=="cuffs-bound"&&item.result.guid!="com.kks.maingame.vrhandcuffs") {item.result.stage="not-applicable";continue;}
                if(_mode=="objects-roundtrip"&&item.result.guid!="com.kks.main.gameobjectcomposer"&&item.result.guid!="com.kks.maingamestudioitemsummon") {item.result.stage="not-applicable";continue;}
                string expected=null;
                bool prepared=Attempt(item,"prepare",()=>
                {
                    if(_mode=="saved")
                    {
                        if(item.result.missingSaved) {item.result.stage="not-saved";return;}
                        expected=File.ReadAllText(item.result.expected);
                    }
                    else {item.dirty=true;Call(item.plugin,"SetHotelGadgetVisible",true);}
                });
                if(!prepared||(_mode=="saved"&&expected==null)) {Cleanup(item);continue;}
                if(_mode!="saved")
                {
                    yield return null;
                    prepared=Attempt(item,"capture-generated",()=>
                    {
                        expected=Capture(item.plugin,_room);
                        if(_mode=="cuffs-bound") expected=BindCuffs(item,expected);
                        if(_mode=="objects-roundtrip") expected=CreateObjectFixture(item,expected);
                    });
                    if(!prepared) {Cleanup(item);continue;}
                    if(_mode=="cuffs-bound"||_mode=="objects-roundtrip")
                    {
                        yield return null;
                        if(!Attempt(item,"capture-bound-settled",()=>expected=Capture(item.plugin,_room))) {Cleanup(item);continue;}
                    }
                }
                if(!Attempt(item,"restore-expected",()=>
                {
                    var data=GadgetRoomSnapshot.Decode(expected);item.result.empty=!data.visible;
                    item.result.expected=Write(item,"expected",expected);
                    item.dirty=true;
                    if(_mode!="saved") Call(item.plugin,"SetHotelGadgetVisible",false);
                    Call(item.plugin,"RestoreHotelGadget",_room,expected);
                })) {Cleanup(item);continue;}
                Attempt(item,"verify-immediate",()=>item.result.immediatePassed=Verify(item,expected,false));
                yield return new WaitForEndOfFrame();
                Attempt(item,"verify-after-frame",()=>item.result.afterFramePassed=Verify(item,expected,true));
                item.result.passed=item.result.error==null&&item.result.immediatePassed&&item.result.afterFramePassed;
                item.result.nonemptyPassed=item.result.passed&&!item.result.empty;
                Cleanup(item);
            }
        }
        finally
        {
            foreach(var item in _items) Cleanup(item);
            _running=false;_complete=true;_routine=null;
            var report=new Manifest {complete=true,mode=_mode,error=_error,results=_items.Select(i=>JsonUtility.ToJson(i.result)).ToArray()};
            try {File.WriteAllText(Path.Combine(_directory,"result.json"),JsonUtility.ToJson(report));}
            catch(Exception ex) {_error=ex.ToString();}
        }
    }
    bool Verify(Item item,string expected,bool afterFrame)
    {
        string actual=Capture(item.plugin,_room);GadgetRoomSnapshot.Decode(actual);
        string file=Write(item,afterFrame?"after-frame":"immediate",actual);
        if(afterFrame) item.result.afterFrame=file;else item.result.immediate=file;
        bool placement=VerifyOptional(item,"VerifyHotelGadgetPlacement",expected),state=VerifyOptional(item,"VerifyHotelGadgetState",expected);
        var type=ReflectionCommands.ResolveType("MainGameAiShoujoMode.HotelRoomGadgets");
        var method=type.GetMethods(Flags).Single(m=>m.Name=="EquivalentGadget"&&m.GetParameters().Length>=5&&m.GetParameters().Length<=6);
        var values=new List<object> {expected,actual,item.result.guid=="com.kks.maingame.syringe",placement,state};
        if(method.GetParameters().Length==6)
        {
            var extra=method.GetParameters()[5];
            if(!extra.HasDefaultValue) throw new NotSupportedException("Unknown required comparison argument: "+extra.Name);
            values.Add(extra.DefaultValue);
        }
        bool passed=(bool)method.Invoke(null,values.ToArray());
        if(_mode=="objects-roundtrip") VerifyObjectFixture(item,actual);
        if(!passed) throw new InvalidOperationException("Applied gadget does not match saved state");return true;
    }
    bool VerifyOptional(Item item,string name,string expected)
    {
        var method=item.plugin.GetType().GetMethod(name,new[]{typeof(Transform),typeof(string)});
        if(method==null) return false;
        if(!(bool)method.Invoke(item.plugin,new object[]{_room,expected})) throw new InvalidOperationException(name+" returned false");return true;
    }
    string BindCuffs(Item item,string json)
    {
        var data=GadgetRoomSnapshot.Decode(json);
        if(!data.visible||data.poses.Length!=8) throw new InvalidOperationException("Expected visible cuffs with 8 transforms");
        var get=ReflectionCommands.ResolveType("MainGirlHipHijack.Plugin").GetMethod("TryGetRuntimeWrist",Flags);
        for(int i=0;i<2;i++)
        {
            object[] args={i==0?"left_hand":"right_hand",null,null};
            if(!(bool)get.Invoke(null,args)||!(args[2] is Transform wrist)) throw new InvalidOperationException("Runtime wrist unavailable: "+i);
            data.poses[6+i]=GadgetRoomPose.Read(_room,wrist);
        }
        var state=JsonUtility.FromJson<CuffState>(data.runtime);state.wrists=new[]{0,1};data.runtime=JsonUtility.ToJson(state);
        Call(item.plugin,"RestoreHotelGadget",_room,data.Encode());
        return Capture(item.plugin,_room);
    }
    string CreateObjectFixture(Item item,string json)
    {
        var data=GadgetRoomSnapshot.Decode(json);
        if(item.result.guid=="com.kks.main.gameobjectcomposer")
        {
            if(!data.visible||data.poses.Length!=1) throw new InvalidOperationException("Composer root was not created");
            data.runtime=JsonUtility.ToJson(new ComposerState {layout=JsonUtility.ToJson(new ObjectLayout {objectsJson=new[]{
                "{\"id\":\"hotel-validation-a\",\"name\":\"validation A\",\"primitive\":0,\"parentKind\":\"root\",\"localPosition\":{\"x\":0.1,\"y\":1.1,\"z\":0.2},\"localScale\":{\"x\":0.05,\"y\":0.05,\"z\":0.05},\"visible\":true}",
                "{\"id\":\"hotel-validation-b\",\"name\":\"validation B\",\"primitive\":3,\"parentKind\":\"managed\",\"parentRefId\":\"hotel-validation-a\",\"localPosition\":{\"x\":0.2,\"y\":0.1,\"z\":0.3},\"localScale\":{\"x\":0.03,\"y\":0.03,\"z\":0.03},\"visible\":true}"}})});
        }
        else
        {
            // These addresses are the installed chain provider's actual production
            // constants, not fabricated Studio catalogue IDs. The summon provider's
            // real CommonLib Restore loader validates both assets before committing.
            var chain=ReflectionCommands.ResolveType("MainGameVrNeckChain.Plugin");
            string Constant(string name)=>(string)chain.GetField(name,Flags).GetValue(null);
            var sources=new[]{
                new Source {bundle=Constant("BundlePath"),asset=Constant("AssetName"),manifest=string.Empty,name="validation chain",active=true},
                new Source {bundle=Constant("CollarBundlePath"),asset=Constant("CollarAssetName"),manifest=string.Empty,name="validation collar",active=false}};
            data.visible=true;data.poses=Enumerable.Range(0,2).Select(i=>new GadgetRoomPose {position=new Vector3(.1f+i*.2f,1.1f,.2f),rotation=Quaternion.identity,scale=Vector3.one,worldScale=true}).ToArray();
            data.runtime=JsonUtility.ToJson(new Sources {itemRecords=sources.Select(source=>JsonUtility.ToJson(source)).ToArray()});
        }
        Call(item.plugin,"RestoreHotelGadget",_room,data.Encode());
        return Capture(item.plugin,_room);
    }
    void VerifyObjectFixture(Item item,string actual)
    {
        if(item.result.guid=="com.kks.main.gameobjectcomposer")
        {
            var objects=(IDictionary)Read(item.plugin,"_runtimeObjects");
            if(objects.Count!=2) throw new InvalidOperationException("Expected 2 real Composer objects");
            var a=(GameObject)Read(objects["hotel-validation-a"],"GameObject");
            var b=(GameObject)Read(objects["hotel-validation-b"],"GameObject");
            if(a==null||b==null||!b.transform.IsChildOf(a.transform)) throw new InvalidOperationException("Composer generated object/managed parent missing");
        }
        else
        {
            var data=GadgetRoomSnapshot.Decode(actual);
            var state=JsonUtility.FromJson<Sources>(data.runtime);
            var live=((IEnumerable)Read(item.plugin,"_summoned")).Cast<GameObject>().Where(g=>g!=null).ToArray();
            if(data.poses.Length!=2||state?.itemRecords?.Length!=2||live.Length!=2||!live[0].activeSelf||live[1].activeSelf)
                throw new InvalidOperationException("Expected 2 real Studio items with separate active states");
        }
    }
    void Cleanup(Item item)
    {
        if(!item.dirty||item.before==null) return;item.dirty=false;
        try {Call(item.plugin,"RestoreHotelGadget",_room,item.before);item.result.cleanupPassed=true;}
        catch(Exception ex) {item.result.cleanupError=ex.GetBaseException().ToString();item.result.passed=false;item.result.nonemptyPassed=false;}
        item.result.stage="complete";
    }
    public void Dispose()
    {
        if(_routine!=null&&_host!=null) _host.StopCoroutine(_routine);
        foreach(var item in _items) Cleanup(item);
        _routine=null;_running=false;
    }
}
