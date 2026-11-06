using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using MainGameAiShoujoMode;
using ActionGame;
using UnityEngine;

internal static class Program
{
    private static readonly List<string> Checks = new();
    private static void Check(bool ok,string text) { if(!ok)throw new Exception(text); Checks.Add(text); }
    private static object Call(string name,object value) => typeof(AiModeHSession).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new[] {value});
    private static void Main()
    {
        var settings = new Settings(JsonSerializer.Deserialize<Dictionary<string,float>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixture.json"))));
        var owner = new AiModeHSession(settings); AiModeHSession.Activate(owner);
        var proc = new HSceneProc(); var map = new ActionMap { Proc=proc };
        CanonGate.GateApi.HotelMode = new CanonGate.HotelScope {Owner=owner.Host};
        Call("AfterMapAwake",map);
        Check(ReferenceEquals(owner.Proc,proc) && ReferenceEquals(owner.Map,map),"ActionMap.Awake captures HProc before its Start");
        Check(map.isLock && ReferenceEquals(map.mapRoot,owner.Island) && map.no == checked((int)settings.N("hSession.mapNo")),"actual hotel room replaces native map before startup");
        Check(map.infoDic != null && map.mapObjects.Length == default && ReferenceEquals(map.mapObjectGroup,owner.Island.transform),"room metadata and object ownership initialized at Awake");
        Check(!(bool)Call("BeforeMapStart",map) && !(bool)Call("BeforeMapChange",map),"standard room Start and map replacement are suppressed");
        var foreign = new ActionMap {Proc=new HSceneProc()};
        Call("AfterMapAwake",foreign);
        Check(ReferenceEquals(owner.Map,map) && foreign.mapRoot==null && (bool)Call("BeforeMapStart",foreign) && (bool)Call("BeforeMapChange",foreign),"unowned H and story maps remain unchanged");
        Call("BeforeHStart",proc);
        Check(ReferenceEquals(CanonGate.GateApi.HotelMode.Scene,proc),"H Start still binds hotel permissions after early Awake capture");
        Call("BeforeHStart",foreign.Proc);
        Check(ReferenceEquals(CanonGate.GateApi.HotelMode.Scene,proc),"another HProc cannot replace the owned permission scene");
        var deadline=owner.GetDeadline(); deadline.MoveNext(); deadline.MoveNext();
        Check(owner.Failure is TimeoutException,"enabled HProc that never reaches ready now times out");
        owner.ResetFailure(); owner.RoomReady=true; deadline=owner.GetDeadline(); deadline.MoveNext(); deadline.MoveNext();
        Check(owner.Failure==null,"completed native startup does not time out");
        AiModeHSession.Activate(null);
        Check((bool)Call("BeforeMapStart",map) && (bool)Call("BeforeMapChange",map),"normal H startup is unchanged without an owner");
        Console.WriteLine("PASS "+Checks.Count+" H Awake-before-Start ownership and startup deadline checks");
    }
}
namespace UnityEngine
{
    public class Transform {}
    public class GameObject {public readonly Transform transform=new();}
    public class WaitForSecondsRealtime {public WaitForSecondsRealtime(float seconds){}}
}
namespace HarmonyLib {internal static class Placeholder {}}
public class HSceneProc {public bool enabled=true;}
public class BaseMap
{
    public bool isLock; public GameObject mapRoot {get;private set;} public int no {get;private set;}
    public Dictionary<int,object> infoDic {get;private set;}
    public static Dictionary<int,object> LoadMapInfo()=>new();
}
namespace ActionGame
{
    public class ActionMap:BaseMap
    {
        public HSceneProc Proc;
        public MapObject.Kind[] mapObjects {get;private set;} public Transform mapObjectGroup {get;private set;}
        public T GetComponent<T>() where T:class=>Proc as T;
    }
}
namespace ActionGame.MapObject {public class Kind {}}
namespace CanonGate
{
    public static class GateApi {public static HotelScope HotelMode;}
    public class HotelScope {public object Owner;public HSceneProc Scene;public void Bind(object owner,HSceneProc proc){Scene=proc;}}
}
public class Settings
{
    private readonly Dictionary<string,float> _values;public Settings(Dictionary<string,float> values){_values=values;}
    public float N(string key)=>_values[key];
}
namespace MainGameAiShoujoMode
{
    public sealed partial class AiModeHSession
    {
        private static AiModeHSession _active;private HSceneProc _proc;private ActionMap _map;
        private readonly GameObject _island=new();private readonly object _host=new();
        private readonly Settings _p;private object _deadline;private Action<string> _log=delegate{};
        public bool Busy=true;public bool RoomReady;public Exception Failure;
        public object Host=>_host;public GameObject Island=>_island;public HSceneProc Proc=>_proc;public ActionMap Map=>_map;
        public AiModeHSession(Settings settings){_p=settings;}
        public static void Activate(AiModeHSession owner){_active=owner;}
        public IEnumerator GetDeadline()=>Deadline();
        public void ResetFailure(){Failure=null;}
        private void Fail(Exception error){Failure=error;}
        private static void Set(object target,Type declaring,string property,object value)=>declaring.GetProperty(property,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).GetSetMethod(true).Invoke(target,new[]{value});
    }
}
