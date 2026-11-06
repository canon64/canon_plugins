using System;
using System.Collections;
using System.Collections.Generic;
using MainGameAiShoujoMode;

internal static class Program
{
    private static readonly List<string> Checks=new();
    private static void Check(bool ok,string text){if(!ok)throw new Exception(text);Checks.Add(text);}
    private static void Main()
    {
        var session=new HotelModeSession();var room=new HotelFloorRuntime("201");var corridor=session.Corridor;
        session.Enter(room);
        Check(session.Fade.Pending && session.CardReads==default(int),"even registration disk reads wait for loading cover");
        session.Fade.CompleteCover();
        Check(session.CardReads==default(int) && session.PendingFrames==Convert.ToInt32(true),"cover callback starts no disk reads before its first yield");
        session.RenderCoveredFrame();
        Check(session.Error!=null && ReferenceEquals(session.Corridor,corridor) && session.Context.WalkingReady,"unregistered room restores corridor after covered-frame validation");
        session=new HotelModeSession();session.Registered=true;corridor=session.Corridor;room=new HotelFloorRuntime("201");
        session.Enter(room);
        Check(session.Fade.Pending && !session.Context.WalkingReady && session.Walker.enabled && !session.Native.Suspended,"entry blocks walking intent without synchronous suspension before cover");
        session.Enter(room);
        session.Fade.CompleteCover();
        Check(session.Started.Count==default(int) && session.CardReads==default(int) && corridor.Active && !room.Active,"nothing initializes inside loading completion callback");
        session.RenderCoveredFrame();
        Check(session.Started.Count==Convert.ToInt32(true) && session.Started.Contains("201"),"door starts H exactly once after cover");
        Check(ReferenceEquals(session.Corridor,corridor) && !corridor.Active && room.Active,"H borrows room scenery while corridor remains the walking floor");
        Check(session.Fade.Covered && !session.Context.WalkingReady,"room scenery never reveals as an intermediate walking room");
        session.Ready();
        Check(!session.Fade.Covered && !session.Context.WalkingReady && !session.Walker.enabled && session.Native.Suspended,"H readiness reveals H without reenabling walking");
        session.End();
        Check(!room.Active && corridor.Active && ReferenceEquals(session.Corridor,corridor) && session.Context.WalkingReady,"H end returns to the original corridor instead of room walking");
        session.End();
        Check(corridor.Active && session.Context.WalkingReady,"repeated room cleanup preserves corridor");
        session=new HotelModeSession();session.Registered=true;room=new HotelFloorRuntime("201");
        session.Enter(room);session.CancelBeforeCover();session.Fade.CompleteCover();
        session.RenderCoveredFrame();
        Check(!session.Started.Contains("201") && session.Returned,"cancelled fade cannot start a stale H session");
        session=new HotelModeSession();session.Registered=true;room=new HotelFloorRuntime("201");
        session.Enter(room);session.Fade.CompleteCover();session.CancelBeforeCover();session.RenderCoveredFrame();
        Check(session.CardReads==default(int) && session.Returned,"cancel between cover and rendered frame cancels deferred IO");
        session=new HotelModeSession();session.Registered=true;room=new HotelFloorRuntime("201");
        session.Enter(room);session.Fade.CompleteCover();session.DestroyBeforeFrame();session.RenderCoveredFrame();
        Check(session.CardReads==default(int) && session.Started.Count==default(int),"destroyed owner cannot resume room startup");
        session=new HotelModeSession();session.Registered=true;room=new HotelFloorRuntime("201");session.Fade.Covered=true;
        session.Enter(room);
        Check(session.CardReads==default(int) && session.Started.Count==default(int) && session.PendingFrames==Convert.ToInt32(true),"already-covered fade still defers all startup until next frame");
        session.RenderCoveredFrame();
        Check(session.Started.Contains("201"),"already-covered entry resumes after its covered frame");
        session=new HotelModeSession();session.Registered=true;room=new HotelFloorRuntime("201");
        HotelRoomObjectLayout.FailNext=true;session.Enter(room);session.Fade.CompleteCover();
        session.RenderCoveredFrame();
        Check(session.Error!=null && session.Corridor.Active && !room.Active && session.Context.WalkingReady,"room initialization failure restores corridor without room walking");
        session=new HotelModeSession();session.Registered=true;room=new HotelFloorRuntime("reception-suite");
        session.Enter(room);session.Fade.CompleteCover();session.RenderCoveredFrame();session.Ready();
        Check(session.Started.Contains("reception-suite") && !session.Context.WalkingReady,"reception suite also enters H directly without room walking");
        session.End();
        Check(session.Corridor.Active && !room.Active,"reception H returns to its original public area");
        foreach (string id in new[] { "202", "203", "204", "205", "206" })
        {
            session=new HotelModeSession();session.Registered=true;room=new HotelFloorRuntime(id);
            session.Enter(room);session.Fade.CompleteCover();session.RenderCoveredFrame();session.Ready();
            Check(session.Started.Contains(id) && room.Active && !session.Context.WalkingReady,"registered guest room starts its own H: "+id);
            session.End();
            Check(session.Corridor.Active && !room.Active,"guest H returns to its corridor: "+id);
        }
        Console.WriteLine("PASS "+Checks.Count+" direct door-to-H transition checks (Unity and native H boundaries mocked)");
    }
}
namespace MainGameAiShoujoMode
{
    internal sealed partial class HotelModeSession
    {
        private bool _closing,_returnRequested,_loading,_changingFloor;
        private FakeH _hSession;private readonly FakeCards _roomCards=new();
        private readonly FakeContext _context=new();private readonly FakeWalker _walker=new();
        private readonly FakeNative _nativeWalking=new();private readonly FakeFade _fade=new();
        private readonly object _p=new();private HotelFloorRuntime _floor=new("corridor"){Active=true};
        private object _sessionRoomRoot;private HotelRoomObjectLayout _roomObjects;
        public Exception Error;public bool Returned;public readonly List<string> Started=new();
        private readonly List<IEnumerator> _pendingFrames=new();
        public int PendingFrames=>_pendingFrames.Count;public int CardReads=>_roomCards.Reads;
        public bool Registered{set=>_roomCards.RegisteredValue=value;}
        public HotelFloorRuntime Corridor=>_floor;public FakeContext Context=>_context;public FakeWalker Walker=>_walker;public FakeNative Native=>_nativeWalking;public FakeFade Fade=>_fade;
        public void Enter(HotelFloorRuntime room)=>OpenDirectRoom(room);
        public void Ready()=>RevealDirectRoom();
        public void End()=>RoomSessionEnded();
        public void CancelBeforeCover(){_returnRequested=true;}
        public void DestroyBeforeFrame(){_closing=true;}
        private void StartCoroutine(IEnumerator work){if(work.MoveNext())_pendingFrames.Add(work);}
        public void RenderCoveredFrame(){if(!Fade.Covered)throw new Exception("loading frame must be covered");var all=_pendingFrames.ToArray();_pendingFrames.Clear();foreach(var work in all){if(work.MoveNext())throw new Exception("unexpected polling");}}
        private void BeginRoomEntryTrace(string room){}
        private IEnumerator EnsureFloorLoaded(HotelFloorRuntime room){yield break;}
        private void TraceRoom(string stage){}
        private void BeginReturn(){Returned=true;}
        private void SuspendRoomBehaviour(FakeWalker walker){walker.enabled=false;}
        private void RefreshRoomPortalPositions(){}
        private void ReportRoomFailure(Exception error,string stage){Error=error;}
        private void StartRoomSessionAfterLoading(string room){Started.Add(room);_hSession=new FakeH{Busy=true};}
        private void RoomSessionEnded(){RestoreDirectRoom();_floor.SetActive(true);_context.WalkingReady=true;_walker.enabled=true;_nativeWalking.Resume();_loading=false;}
    }
    internal sealed class FakeH{public bool Busy;}
    internal sealed class FakeCards{public bool RegisteredValue;public int Reads;public object Registered(string slot){Reads++;return RegisteredValue?this:null;}}
    internal sealed class FakeContext{public bool WalkingReady=true;}
    internal sealed class FakeWalker{public bool enabled=true;public void SetCursorReleased(bool released){}}
    internal sealed class FakeNative{public bool Suspended;public void Suspend(){Suspended=true;}public void Resume(){Suspended=false;}}
    internal sealed class FakeFade
    {
        private Action _covered;public bool Pending=>_covered!=null;public bool Covered;
        public void Cover(Action covered){if(Covered)covered();else _covered=covered;}
        public void CompleteCover(){Covered=true;var action=_covered;_covered=null;action?.Invoke();}
        public void Reveal(Action revealed){Covered=false;revealed();}
    }
    internal sealed class HotelFloorRuntime
    {
        public readonly FloorDefinition Definition;public readonly FakeRoot[] Roots=new[]{new FakeRoot()};
        public bool Active;
        public HotelFloorRuntime(string id){Definition=new FloorDefinition{room=new RoomDefinition{id=id,cardSlot=id,label=id,rootObject="room-root"}};}
        public void SetActive(bool active){Active=active;}
        internal sealed class FloorDefinition{public RoomDefinition room;}
        internal sealed class RoomDefinition{public string id,cardSlot,label,rootObject;}
    }
    internal sealed class FakeRoot{public string name=>"room-root";public object transform=>this;}
    internal sealed class HotelRoomObjectLayout:IDisposable
    {
        public static bool FailNext;
        public HotelRoomObjectLayout(HotelModeSession owner,object root,string room,object settings,Action refreshed)
        {if(FailNext){FailNext=false;throw new InvalidOperationException("room objects unavailable");}}
        public void Dispose(){}
    }
}
