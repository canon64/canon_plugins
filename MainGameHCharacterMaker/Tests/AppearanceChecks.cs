using System;
using CanonGate;
using MainGameHCharacterMaker;

internal static class AppearanceChecks
{
    internal static void Run(Action<bool,string> check)
    {
        bool alive=true, allowed=true;
        int hair=10, pupil=20, untouched=30, writes=0;
        float color=0.2f;
        var edit=new ValueEditSession(()=>alive,()=>allowed);
        Action<int> writeHair=v=> { hair=v; writes++; };
        check(writes==0 && !edit.CanUndo && !edit.CanRedo, "appearance window opens without rewriting parts");
        edit.Preview("hair",()=>hair,writeHair,11);
        edit.Preview("pupil",()=>pupil,v=>pupil=v,21);
        check(hair==11 && pupil==21 && edit.CanUndo, "hair and pupil selections apply to distinct fields");
        edit.Undo();
        check(hair==11 && pupil==20 && edit.CanRedo, "undo affects only most recent selected part");
        edit.Redo();
        check(pupil==21, "redo reapplies selected part");
        edit.Preview("color",()=>color,v=>color=v,0.4f,true);
        edit.Preview("color",()=>color,v=>color=v,0.6f,true);
        edit.Preview("color",()=>color,v=>color=v,0.8f,true);
        edit.BreakMerge(); edit.Undo();
        check(color==0.2f && pupil==21, "one undo restores entire color slider gesture");
        edit.Redo(); edit.BreakMerge();
        edit.Preview("color",()=>color,v=>color=v,0.9f,true);
        edit.Undo();
        check(color==0.8f, "separate color gesture has its own undo step");
        edit.Preview("hair",()=>hair,writeHair,12);
        check(!edit.CanRedo, "new part selection invalidates abandoned redo branch");
        untouched=99; allowed=false;
        int beforeWrites=writes;
        check(!edit.Preview("hair",()=>hair,writeHair,15) && !edit.Undo() && !edit.Redo() && !edit.Commit()
            && beforeWrites==writes, "expired H permission blocks all appearance commands");
        edit.Cancel();
        check(hair==10 && pupil==20 && color==0.2f && untouched==99, "cancel after permit loss restores touched fields and preserves unrelated changes");
        beforeWrites=writes; edit.Cancel();
        check(writes==beforeWrites && edit.Closed, "appearance cleanup is idempotent");
        allowed=true; edit=new ValueEditSession(()=>alive,()=>allowed);
        edit.Preview("hair",()=>hair,writeHair,17); edit.Commit(); edit.Cancel();
        check(hair==17 && edit.Closed, "committed part survives window close");
        edit=new ValueEditSession(()=>alive,()=>allowed);
        bool fail=true;
        Action<int> partial=v=> { hair=v; if(fail) { fail=false; throw new Exception("native setter failed after mutation"); } };
        try { edit.Preview("hair",()=>hair,partial,18); } catch(Exception) { }
        edit.Cancel();
        check(hair==17, "partially failing native part setter remains cancellable");
        edit=new ValueEditSession(()=>alive,()=>allowed);
        bool rejectRestore=false;
        Action<int> retry=v=> { if(rejectRestore && v==17) throw new Exception("temporary restore failure"); hair=v; };
        edit.Preview("hair",()=>hair,retry,19);
        edit.Preview("pupil",()=>pupil,v=>pupil=v,25);
        rejectRestore=true;
        try { edit.Cancel(); } catch(InvalidOperationException) { }
        check(!edit.Closed && pupil==20, "one restore failure does not skip other touched fields");
        rejectRestore=false; edit.Cancel();
        check(edit.Closed && hair==17, "failed appearance restoration can be retried");
        edit=new ValueEditSession(()=>alive,()=>allowed);
        edit.Preview("hair",()=>hair,writeHair,22); alive=false;
        beforeWrites=writes; edit.Cancel();
        check(edit.Closed && writes==beforeWrites && !edit.CanCommit, "destroyed or replaced target is never mutated by stale cleanup");
        alive=true;
        var first=new ValueEditSession(()=>alive,()=>allowed);
        var second=new ValueEditSession(()=>alive,()=>allowed);
        int hair2=40;
        first.Preview("hair",()=>hair,writeHair,23);
        second.Preview("hair",()=>hair2,v=>hair2=v,41);
        first.Cancel();
        check(hair==22 && hair2==41, "female switching keeps independent appearance transactions");
        second.Cancel();
        check(hair2==40, "second female has her own original parts");
        CheckGate(check);
    }
    private static void CheckGate(Action<bool,string> check)
    {
        const string guid="canon.kks.hcharactermaker";
        var policy=new ExceptionPolicy(); policy.Load("", "");
        ValueEditSession edit=null;
        int id=7, starts=0, stops=0;
        GateApi.SetRunning(true);
        using(GateApi.Register(guid,()=> {
            starts++;
            var permit=GateApi.Capture(guid);
            edit=new ValueEditSession(()=>true,()=>permit.IsValid);
        },()=> { stops++; edit?.Cancel(); }))
        {
            GateApi.BeginSession(GateApi.SessionKind.H,false,policy,new HSceneProc());
            check(starts==0 && edit==null,"real gate denial cannot create appearance editor");
            GateApi.EndSession(GateApi.SessionKind.H);
            GateApi.BeginSession(GateApi.SessionKind.H,true,policy,new HSceneProc());
            edit.Preview("hair",()=>id,v=>id=v,8);
            var old=edit;
            GateApi.BeginSession(GateApi.SessionKind.H,true,policy,new HSceneProc());
            check(starts==1 && id==8,"duplicate H start preserves appearance transaction");
            GateApi.EndSession(GateApi.SessionKind.H);
            check(stops==1 && id==7 && old.Closed,"real gate shutdown restores appearance after revocation");
            GateApi.EndSession(GateApi.SessionKind.H);
            check(stops==1,"duplicate H end does not repeat appearance cleanup");
            GateApi.BeginSession(GateApi.SessionKind.H,true,policy,new HSceneProc());
            check(!old.Preview("hair",()=>id,v=>id=v,9) && id==7,"next H cannot revive old appearance commands");
            GateApi.EndSession(GateApi.SessionKind.H);
        }
        GateApi.SetRunning(false);
    }
}
