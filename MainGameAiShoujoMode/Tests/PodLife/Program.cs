using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoMode;
using MainGameAiShoujoMode.Life;
using UnityEngine;

internal static class Program
{
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Main(string[] args)
    {
        Console.WriteLine("POD_LIFE_BRIDGE_START productionBridge=true stubActor=true");
        using var settings=JsonDocument.Parse(File.ReadAllText(args.Single()));
        var numbers=settings.RootElement.GetProperty("numbers").EnumerateArray().ToDictionary(row=>row.GetProperty("key").GetString(),row=>row.GetProperty("value").GetSingle());
        var zero=numbers["math.zero"]; var one=numbers["math.one"]; var two=numbers["math.two"];
        Vector3.up=new Vector3(zero,one,zero);
        var life = new AiLifeController();
        life.State.x=two;
        var actor = new AiAnimationTrial { PodSequenceActive = true };
        actor.Root.position = new Vector3(one, two, zero);
        using (var bridge = new AiLifeActorBridge(life, actor, () => new Transform(), () => zero, null, new MapEnvironmentRuntime.SandboxHost()))
        {
            Require(actor.Root.position.x == one, "Saved position displaced the girl from the capsule");
            Require(actor.Actions == zero, "Life action began before appearance");
            life.Notify(); actor.Ready();
            Require(actor.Actions == zero, "State/ready notification bypassed capsule ownership");
            actor.Root.position = new Vector3(two, one, zero); actor.Arrived();
            Require(life.State.x == two && actor.Actions == zero, "Exit must save before autonomy starts");
            actor.PodSequenceActive = false; actor.Ready();
            Require(actor.Actions == one && actor.LastAction == "normal", "Healthy resident did not start patrol after release");
            life.Notify(); Require(actor.Actions == one, "Same action was started twice");
            life.DesiredAction = "treatment"; life.Notify();
            Require(actor.LastAction == "treatment", "Ill resident ignored saved life state");
        }
        var actions = actor.Actions; actor.Ready(); life.Notify();
        Require(actor.Actions == actions, "Disposed bridge responded to old completion");
        var restored = new AiAnimationTrial();
        using (var bridge = new AiLifeActorBridge(life, restored, () => new Transform(), () => zero, null, new MapEnvironmentRuntime.SandboxHost()))
            Require(restored.Root.position.x == life.State.x, "Ordinary reentry no longer restores saved position");
        var trialLife=new AiLifeController { DesiredAction="rest" };
        trialLife.Activities.minimumTravel=one;trialLife.Activities.maximumTravel=two+two;
        trialLife.State.activity=new AiLifeActivity { id="same-persisted-job",kind="rest" };
        var trialActor=new AiAnimationTrial();var host=new MapEnvironmentRuntime.SandboxHost();
        host.Session.Navigation.Status.ready=true;
        host.Session.Navigation.Waypoints=new[]{new MapEnvironmentRuntime.NavigationWaypoint { Id="trial-point",Position=new Vector3(two,zero,zero) }};
        using(var bridge=new AiLifeActorBridge(trialLife,trialActor,()=>new Transform(),()=>zero,null,host))
        {
            var oldArrival=trialActor.WalkCompleted;
            trialLife.Conversing=true;trialLife.Notify();trialLife.Conversing=false;trialLife.Notify();
            var newArrival=trialActor.WalkCompleted;
            oldArrival();Require(trialLife.Arrivals==zero,"Old arrival entered restarted activity");
            newArrival();Require(trialLife.Arrivals==one,"Arrival did not begin trial activity");
            newArrival();Require(trialLife.Arrivals==one,"Repeated arrival started two performances");
            var oldFinish=AiLifeActivityMotion.Latest.Finished;
            trialLife.Conversing=true;trialLife.Notify();Require(AiLifeActivityMotion.Latest.Disposed,"Conversation left trial props/timer active");
            trialLife.Conversing=false;trialLife.Notify();trialActor.WalkCompleted();
            oldFinish();Require(trialLife.Completions==zero,"Old completion finished restarted activity");
            AiLifeActivityMotion.Latest.Finished();Require(trialLife.Completions==one&&trialActor.LastAction=="normal","Trial completion did not return to patrol");
            host.Session.Navigation.Status.ready=false;
            trialActor.WalkCompleted=null;
            trialLife.State.activity=new AiLifeActivity { id="source-motion",kind="motion",animationActionId="source-action" };
            trialLife.DesiredAction="motion";trialLife.Notify();
            Require(trialActor.WalkCompleted==null&&trialLife.Arrivals==two+one,"Source motion incorrectly required walking or a prepared navmesh");
            var stale=AiLifeActivityMotion.Latest.Finished;
            trialLife.Conversing=true;trialLife.Notify();
            Require(AiLifeActivityMotion.Latest.Disposed,"Source motion was not released for conversation");
            trialLife.Conversing=false;trialLife.Notify();
            stale();Require(trialLife.Completions==one,"Old source motion completed resumed request");
            AiLifeActivityMotion.Latest.Finished();
            Require(trialLife.Completions==two&&trialActor.LastAction=="normal","Source motion failed return to normal");
        }
        Console.WriteLine("POD_LIFE_BRIDGE_PASS deferredRestore deferredAction savedExit singleResume illness disposal normalReentry");
        Console.WriteLine("AI_LIFE_BRIDGE_ACTIVITY_PASS staleArrival repeatedArrival conversationCleanup staleCompletion returnPatrol sourceMotionWithoutNavigation");
    }
}
namespace UnityEngine
{
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z) { this.x=x;this.y=y;this.z=z; }
        public static Vector3 up;
        public static Vector3 operator *(Vector3 v,float scale) => new Vector3(v.x*scale,v.y*scale,v.z*scale);
        public static Vector3 operator -(Vector3 a,Vector3 b) => new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public float sqrMagnitude => x*x+y*y+z*z;
        public static float Distance(Vector3 a,Vector3 b) => MathF.Sqrt((a-b).sqrMagnitude);
    }
    public struct Quaternion { public static Quaternion Euler(Vector3 value) => default; }
    public class Transform
    {
        public Vector3 position,eulerAngles;
        public void SetPositionAndRotation(Vector3 p,Quaternion r) { position=p; }
    }
}
public sealed class ChaControl { }
namespace MainGameAiShoujoMode
{
    public sealed class AiAnimationTrial
    {
        internal bool PodSequenceActive;
        internal bool LifeMotionOccupied;
        public bool isActiveAndEnabled=true;
        public Transform Root = new Transform();
        public ChaControl CurrentActor = new ChaControl();
        internal Action WalkCompleted;
        internal event Action LifeActionReady,LifeDestinationReached;
        internal int Actions; internal string LastAction;
        internal void ApplyLifeAction(string action,Transform player) { Actions++;LastAction=action; }
        internal void Ready() => LifeActionReady?.Invoke();
        internal void Arrived() => LifeDestinationReached?.Invoke();
        internal void StopSpecialSegment() { }
        internal void WalkSpecialSegment(Vector3 point,Action completed,Action<Exception> failed) { WalkCompleted=completed; }
    }
    internal sealed class AiLifeActivityMotion : IDisposable
    {
        internal static AiLifeActivityMotion Latest;
        internal Action Finished;internal bool Disposed;
        internal AiLifeActivityMotion(AiAnimationTrial a,AiLifeActivityCatalog c,AiLifeActivity p) { Latest=this; }
        internal void Begin(AiLifeActivity p,Action done,Action<Exception> failed) { Finished=done; }
        public void Dispose() { Disposed=true; }
    }
}
namespace MapEnvironmentRuntime
{
    public sealed class SandboxHost { public Session Session=new Session(); }
    public sealed class Session { public NavigationWorld Navigation=new NavigationWorld(); }
    public sealed class NavigationWaypoint { public string Id; public Vector3 Position; }
    public sealed class Status { public bool ready; }
    public sealed class NavigationWorld
    {
        public Status Status=new Status();
        public NavigationWaypoint[] Waypoints=Array.Empty<NavigationWaypoint>();
        public event Action Rebuilding,Rebuilt;
        public bool TryCalculatePath(Vector3 a,Vector3 b,out object p) { p=null;return true; }
        public bool TryProject(Vector3 a,out Vector3 p) { p=a;return true; }
    }
}
namespace MainGameAiShoujoMode.Life
{
    internal sealed class AiResidentState { internal bool hasPosition=true; internal float x,y,z,yaw;internal string id="test",action="normal",lastActivityResult;internal AiLifeActivity activity; }
    internal sealed class AiLifeActivity { internal string id,kind,animationActionId;internal bool hasDestination;internal float x,y,z; }
    internal sealed class AiLifeActivityCatalog { internal float minimumTravel,maximumTravel; }
    internal sealed class AiLifeController
    {
        internal AiResidentState State=new AiResidentState();
        internal bool Conversing,InH;
        internal string DesiredAction="normal";
        internal event Action<AiResidentState> Changed;
        internal AiLifeActivityCatalog Activities=new AiLifeActivityCatalog();
        internal int Arrivals,Completions;
        internal void ActivityDestination(string id,string point,float x,float y,float z) { State.activity.hasDestination=true;State.activity.x=x;State.activity.y=y;State.activity.z=z;Notify(); }
        internal void ActivityArrived(string id) { Arrivals++;Notify(); }
        internal bool CompleteActivity(string id) { Completions++;State.activity=null;DesiredAction="normal";Notify();return true; }
        internal void CancelActivity(string reason,string expectedId=null) { }
        internal void Notify() => Changed?.Invoke(State);
        internal void Advance(double delta) { }
        internal void SavePosition(float x,float y,float z,float yaw) { State.x=x;State.y=y;State.z=z;State.yaw=yaw;Notify(); }
        internal void Block(string reason) { }
    }
}
