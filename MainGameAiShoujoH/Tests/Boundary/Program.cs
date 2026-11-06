using System;
using System.Collections;
using System.Collections.Generic;
using MainGameAiShoujoH.Contracts;
using MainGameAiShoujoH.Session;
using UnityEngine;
public class ChaControl : MonoBehaviour { }
namespace MainGameAiShoujoH.Presentation { public sealed class NativeVoiceContext { } }
namespace UnityEngine
{
    public struct Color { }
    public struct Vector3 { }
    public struct Quaternion { }
    public class Transform { public Transform parent; public Vector3 localPosition, localScale; public Quaternion localRotation; public void SetParent(Transform p, bool world) { parent = p; } }
    public class GameObject { }
    public class Camera { }
    public class WaitForEndOfFrame { }
    public class MonoBehaviour
    {
        public Transform transform = new Transform();
        public readonly Queue<IEnumerator> Pending = new Queue<IEnumerator>();
        public void StartCoroutine(IEnumerator routine) { Pending.Enqueue(routine); routine.MoveNext(); }
        public void Render() { var count = Pending.Count; while (count-- > 0) { var routine = Pending.Dequeue(); if (routine.MoveNext()) Pending.Enqueue(routine); } }
    }
}
class Module : ISessionModule
{
    public string Name => "test";
    public int Prepared, Ended, Disposed;
    public bool FailPrepare, FailEnd, InteractionDuringPrepare;
    public SessionContext Context;
    public void Prepare(SessionContext context) { ++Prepared; Context = context; InteractionDuringPrepare = context.CanInteract(); if (FailPrepare) throw new Exception("injected prepare"); }
    public virtual void Execute(Command command) { }
    public void End(EndReason reason) { ++Ended; if (FailEnd) throw new Exception("injected cleanup"); }
    public void Dispose() { ++Disposed; }
}
class AsyncModule : ISessionModule, IAsyncSessionPreparation
{
    public string Name => "native-engine";
    public Action Ready;
    public Action<Exception> Failed;
    public bool Disposed;
    public void Prepare(SessionContext context) { throw new Exception("Synchronous preparation was not expected"); }
    public void PrepareAsync(SessionContext context, Action ready, Action<Exception> failed) { Ready = ready; Failed = failed; }
    public void Execute(Command command) { }
    public void End(EndReason reason) { }
    public void Dispose() { Disposed = true; }
}
class Program
{
    static int assertions;
    static void Check(bool value, string name) { ++assertions; if (!value) throw new Exception(name); }
    static void Reject(Action action, string name) { try { action(); } catch (InvalidOperationException) { ++assertions; return; } throw new Exception(name); }
    static StartRequest Request(MonoBehaviour host) => new StartRequest { Owner = host, Island = new GameObject(), MotionId = "0:0", Participants = new[] { new Participant { Identity="girl", Character = new ChaControl() } }, Cover = done => done(), Reveal = done => done() };
    static void Main()
    {
        var unloadHost = new MonoBehaviour(); var unload = new AsyncCleanupModule(); var afterUnload = new Module(); int walkReturns = default(int);
        var unloadRequest = Request(unloadHost); unloadRequest.Ended = () => ++walkReturns;
        var unloadSession = new DedicatedSession(() => true, _ => new ISessionModule[] { afterUnload, unload }, _ => { });
        unloadSession.Begin(unloadRequest); unloadHost.Render(); unloadSession.End(EndReason.Cancelled); unloadHost.Render();
        Check(unloadSession.Phase == SessionPhase.Restoring && walkReturns == default(int) && afterUnload.Disposed == default(int), "native async unload retains cover and actor/control ownership until scene destruction");
        unload.Completed(); unload.Completed();
        Check(!unloadSession.Busy && walkReturns == 1 && afterUnload.Disposed == 1, "native scene unload completion releases remaining modules and walking exactly once");
        unload = new AsyncCleanupModule(); afterUnload = new Module();
        unloadSession = new DedicatedSession(() => true, _ => new ISessionModule[] { afterUnload, unload }, _ => { });
        unloadSession.Begin(unloadRequest); unloadHost.Render(); unloadSession.End(EndReason.Cancelled); unloadHost.Render();
        unload.Failed(new Exception("native unload failure")); unload.Completed();
        Check(unloadSession.Phase == SessionPhase.Failed && !unloadSession.Snapshot().Restored && afterUnload.Disposed == 1 && walkReturns == 2, "native cleanup failure is retained while later ownership cleanup still runs once");
        var nativeOwner = new MonoBehaviour(); var nativeAsync = new AsyncModule(); var nativeNext = new Module();
        var nativeSession = new DedicatedSession(() => true, _ => new ISessionModule[] { nativeAsync, nativeNext }, _ => { });
        nativeSession.Begin(Request(nativeOwner)); nativeOwner.Render();
        Check(nativeSession.Phase == SessionPhase.Preparing && nativeNext.Prepared == default(int), "native scene preparation stays covered without polling or early later-module work");
        nativeAsync.Ready(); nativeAsync.Ready(); Check(nativeSession.Phase == SessionPhase.Ready && nativeNext.Prepared == 1, "native completion resumes preparation exactly once");
        nativeSession.End(EndReason.Cancelled); nativeOwner.Render(); Check(nativeAsync.Disposed, "native asynchronous owner is disposed on cancellation");
        nativeAsync = new AsyncModule(); nativeNext = new Module();
        nativeSession = new DedicatedSession(() => true, _ => new ISessionModule[] { nativeAsync, nativeNext }, _ => { });
        nativeSession.Begin(Request(nativeOwner)); nativeOwner.Render(); nativeSession.End(EndReason.Cancelled); nativeOwner.Render(); nativeAsync.Ready();
        Check(!nativeSession.Busy && nativeNext.Prepared == default(int), "native late scene-ready callback cannot revive a cancelled generation");
        bool allowed = false; var host = new MonoBehaviour(); var module = new Module(); int notifications = 0;
        var session = new DedicatedSession(() => allowed, _ => new ISessionModule[] { module }, x => ++notifications);
        Reject(() => session.Begin(Request(host)), "permission denied"); allowed = true;
        var request = Request(host); int acquired = 0, restored = 0, ended = 0;
        request.KksPose = new KksPoseIdentity { TableBundle = "native-table", TableAsset = "native-list", Mode = default(int), Id = default(int) };
        request.AcquireControl = () => { ++acquired; return () => ++restored; }; request.Ended = () => ++ended;
        session.Begin(request); Check(module.Prepared == 0 && acquired == 0, "nothing before rendered cover");
        Reject(() => session.Begin(request), "duplicate start"); host.Render(); Check(session.Phase == SessionPhase.Ready && acquired == 1, "ready after render");
        Check(!module.InteractionDuringPrepare && module.Context.CanInteract(), "interaction denied during preparation and allowed only at Ready");
        Check(session.Snapshot().BodyBackend == "KKS" && session.Snapshot().KksTableBundle == "native-table" && session.Snapshot().KksPoseId == default(int), "native body identity is reported separately from upper result motion meaning");
        var old = module.Context; session.End(EndReason.Completed); session.End(EndReason.Completed);
        Check(!old.IsCurrent() && !old.CanInteract(), "late completion and UI invalidated before restore"); Check(restored == 0, "restoration waits for render");
        host.Render(); Check(restored == 1 && ended == 1 && module.Ended == 1 && module.Disposed == 1, "exactly once cleanup");
        Check(session.Snapshot().LastEndReason == EndReason.Completed && session.Snapshot().Restored, "API retains actual completion and restoration outcome");
        var failing = new Module { FailPrepare = true, FailEnd = true }; var second = new Module();
        session = new DedicatedSession(() => allowed, _ => new ISessionModule[] { second, failing }, x => ++notifications);
        session.Begin(Request(host)); host.Render(); host.Render();
        Check(second.Ended == 1 && second.Disposed == 1 && failing.Disposed == 1, "partial failure continues all cleanup");
        Check(session.Phase == SessionPhase.Failed && notifications == 1, "failures notify");
        int participantReleases = 0; var partialHost = new MonoBehaviour(); var partialRequest = Request(partialHost);
        partialRequest.ResolveParticipants = () => { throw new Exception("injected resident model load failure"); };
        partialRequest.ReleaseParticipants = () => ++participantReleases;
        session = new DedicatedSession(() => allowed, _ => throw new Exception("modules must not start after resident failure"), x => ++notifications);
        session.Begin(partialRequest); Check(participantReleases == 0, "resident resolution waits for rendered cover"); partialHost.Render(); partialHost.Render();
        Check(participantReleases == 1 && !session.Busy, "partial resident load releases owned participants once");
        session = new DedicatedSession(() => allowed, _ => new ISessionModule[] { new Module() }, x => ++notifications);
        session.Begin(Request(host)); allowed = false; host.Render(); host.Render(); Check(!session.Busy, "revoke during load restores");
        session.Dispose(); Reject(() => session.Begin(Request(host)), "disposed rejects");
        allowed = true; var destroyedModule = new Module(); var owner = new MonoBehaviour(); int forbiddenResume = 0;
        var destroying = Request(owner); destroying.Ended = () => ++forbiddenResume;
        session = new DedicatedSession(() => allowed, _ => new ISessionModule[] { destroyedModule }, x => ++notifications);
        session.Begin(destroying); owner.Render(); var beforeDestroy = session.Snapshot();
        Check(beforeDestroy.Ready && beforeDestroy.Participants.Length == 1, "snapshot reports actual ready participants");
        session.OwnerDestroyed(new MonoBehaviour()); Check(session.Busy, "unrelated owner cannot destroy active session");
        session.OwnerDestroyed(owner);
        Check(!session.Busy && destroyedModule.Disposed == 1 && forbiddenResume == 0, "destroyed owner releases without walking resume");
        Check(session.Snapshot().Generation > beforeDestroy.Generation, "owner destruction invalidates generation");
        int commits = 0;
        foreach (var reason in new[] { EndReason.Completed, EndReason.Cancelled, EndReason.PermissionRevoked })
        {
            allowed = true; var endHost = new MonoBehaviour(); var cleanup = new Module();
            var results = new MainGameAiShoujoH.Results.ResultModule(new MainGameAiShoujoH.Results.ResultServices {
                Apply = delta => { Check(ResultCommitBoundary.CanCommit(allowed, allowed, session.Phase), "real result commit boundary permits normal restoring phase"); ++commits; return new MainGameAiShoujoH.Results.ResultReceipt { Applied = true, Saved = true }; }, EndDialogue = _ => { } });
            session = new DedicatedSession(() => allowed, _ => new ISessionModule[] { cleanup, results }, x => ++notifications);
            session.Begin(Request(endHost)); endHost.Render(); results.AddParameter("girl", 1, 3, "test-award");
            if (reason == EndReason.PermissionRevoked) allowed = false;
            session.End(reason); endHost.Render();
            Check(cleanup.Disposed == 1 && session.Snapshot().Restored, "cleanup remains authorized after external permit revocation");
            Check(session.Snapshot().ResultAppliedCount == (reason == EndReason.Completed ? 1 : 0), "normal, cancellation, and gate revocation report distinct actual persistence");
        }
        Check(commits == 1, "only normal completion persists results exactly once");
        allowed = true; Action externalDone = null; Action<Exception> externalFail = null;
        int externalAborts = 0, walkingReturns = 0; var externalHost = new MonoBehaviour(); var externalModule = new Module();
        var externalRequest = Request(externalHost); externalRequest.Ended = () => ++walkingReturns;
        externalRequest.RestoreExternalControl = (reason, done, failed) => { externalDone = done; externalFail = failed; };
        externalRequest.AbortExternalControl = () => ++externalAborts;
        session = new DedicatedSession(() => allowed, _ => new ISessionModule[] { externalModule }, _ => { });
        session.Begin(externalRequest); externalHost.Render(); session.End(EndReason.Cancelled); externalHost.Render();
        Check(session.Phase == SessionPhase.Restoring && externalModule.Disposed == 1 && walkingReturns == 0, "external restoration waits after H cleanup and before walking return");
        externalDone(); externalDone(); Check(!session.Busy && walkingReturns == 1, "external restoration returns once");
        session.Begin(externalRequest); externalHost.Render(); session.End(EndReason.Cancelled); externalHost.Render();
        var staleDone = externalDone; session.OwnerDestroyed(externalHost); staleDone();
        Check(externalAborts == 1 && walkingReturns == 1 && !session.Busy, "owner destruction cancels external lease and rejects stale completion without walking resume");
        session.Begin(externalRequest); externalHost.Render(); session.End(EndReason.Cancelled); externalHost.Render(); externalFail(new Exception("injected asynchronous restore failure"));
        Check(!session.Busy && externalAborts == 2 && session.Phase == SessionPhase.Failed && !session.Snapshot().Restored, "external restoration failure releases lease and reports actual failure");
        var finalHost = new MonoBehaviour(); var final = new FinalModule(); int finalReturns = 0;
        var finalRequest = Request(finalHost); finalRequest.Ended = () => ++finalReturns;
        session = new DedicatedSession(() => allowed, _ => new ISessionModule[] { final }, _ => { });
        session.Begin(finalRequest); finalHost.Render(); session.End(EndReason.Completed); finalHost.Render();
        Check(final.Started && final.Ended == 1 && final.Disposed == 0 && finalReturns == 0, "ending presentation begins after result capture while module ownership remains alive");
        final.Complete(); Check(final.Finished && final.Disposed == 0 && finalReturns == 0, "finalizer completion covers the rendered view before restoration");
        finalHost.Render(); Check(final.Disposed == 1 && finalReturns == 1, "Receive/finalizer completes before ownership disposal and walking return");
        final = new FinalModule(); session.Begin(finalRequest); finalHost.Render(); session.End(EndReason.Completed); finalHost.Render();
        allowed = false; session.End(EndReason.PermissionRevoked); final.Complete();
        Check(final.Disposed == 1 && !session.Busy && session.Snapshot().LastEndReason == EndReason.PermissionRevoked && finalReturns == 2, "permission revocation cancels pending ending and rejects late completion while releasing resources once");
        var ec = new MainGameAiShoujoH.Integration.EndingConditions { Event = NativeHEvent.Normal, HadControl = true, Phase = 2, FemaleOrgasm = 3, MaleFinish = 1 };
        Check(MainGameAiShoujoH.Integration.EndingSelection.Select(ec, 0).Scenario == "0", "native both-results ending");
        ec.ConstraintSelected = true; ec.DirtySkill = 169;
        Check(MainGameAiShoujoH.Integration.EndingSelection.Select(ec, 0).Scenario == "12", "constraint branch precedes result ending below native Dirty threshold");
        ec.PainSelected = true;
        Check(MainGameAiShoujoH.Integration.EndingSelection.Select(ec, 0).Scenario == "5", "native pain takes priority over constraint");
        ec.Event = NativeHEvent.FromFemale; ec.InvitePattern = -1;
        Check(MainGameAiShoujoH.Integration.EndingSelection.Select(ec, 0).Scenario == "6", "FromFemale outer condition preserved before invite branch");
        ec.Event = NativeHEvent.GyakuYobai; ec.SleepStart = true; ec.Weakness = true;
        Check(MainGameAiShoujoH.Integration.EndingSelection.Select(ec, 0).Scenario == "14", "weak sleeping reverse-night ending preserves distinct scenario14");
        ec.Event = NativeHEvent.Yobai; ec.YobaiBareRate = 50;
        Check(MainGameAiShoujoH.Integration.EndingSelection.Select(ec, 50).CloseWithoutScenario, "night caught threshold excludes equal roll and closes without scenario");
        var selection = new SelectionModule(); var selectionHost = new MonoBehaviour();
        allowed = true; session = new DedicatedSession(() => allowed, _ => new ISessionModule[] { selection }, _ => { });
        session.Begin(Request(selectionHost)); selectionHost.Render(); var initialResumes = selection.Resumed;
        Check(initialResumes == selection.Prepared && initialResumes > selection.Suspended, "initial Ready resumes progression exactly once after preparation");
        Reject(() => session.Execute(new Command { Name = "motion", Value = "blocked" }), "native selection rejected before loading or graph mutation");
        Check(session.Phase == SessionPhase.Ready && selection.Selected == 0, "invalid selection retains current ready graph");
        selection.Context.RequestMotionChange("next", true);
        Check(session.Phase == SessionPhase.Loading && !selection.Context.CanInteract() && selection.Selected == 0 && selection.Suspended == 0, "selection performs no initialization before rendered cover");
        selectionHost.Render();
        Check(selection.PreparedUnderCover && selection.Suspended == 1 && selection.Resumed == initialResumes + selection.Suspended && selection.Selected == 1 && selection.Automatic && session.Phase == SessionPhase.Ready, "automatic selection uses the exact same rendered preparation boundary");
        var oldSelection = selection.Context; session.Execute(new Command { Name = "motion", Value = "later" }); session.End(EndReason.Cancelled); selectionHost.Render();
        Check(session.Snapshot().LastEndReason == EndReason.Cancelled && selection.Selected == 1 && selection.Disposed == 1, "stale selection coroutine cannot convert cancellation into permission revocation");
        session.Begin(Request(selectionHost)); selectionHost.Render();
        Reject(() => oldSelection.RequestMotionChange("stale", false), "old selection callback cannot mutate a new session");
        session.End(EndReason.Cancelled); selectionHost.Render();
        var revealHost = new MonoBehaviour(); var revealModule = new SelectionModule(); Action reveal = null;
        var revealRequest = Request(revealHost); revealRequest.Reveal = done => reveal = done;
        session = new DedicatedSession(() => allowed, _ => new ISessionModule[] { revealModule }, _ => { });
        session.Begin(revealRequest); revealHost.Render();
        Check(session.Phase == SessionPhase.Ready && !revealModule.Context.CanInteract() && revealModule.Resumed == 0, "Ready before reveal cannot accept camera or progression input");
        Reject(() => session.Execute(new Command { Name = "wheel" }), "input rejects while reveal is pending");
        reveal(); reveal();
        Check(revealModule.Context.CanInteract() && revealModule.Resumed == 1, "initial reveal resumes all input once");
        session.Execute(new Command { Name = "motion", Value = "next" }); revealHost.Render();
        Check(!revealModule.Context.CanInteract() && revealModule.Resumed == 1 && revealModule.Suspended == 1, "selection input remains suspended through pending reveal");
        reveal(); reveal();
        Check(revealModule.Context.CanInteract() && revealModule.Resumed == 2, "motion reveal resumes once after preparation");
        session.Execute(new Command { Name = "motion", Value = "last" }); revealHost.Render(); var lateReveal = reveal;
        session.End(EndReason.Cancelled); revealHost.Render(); lateReveal();
        Check(!revealModule.Context.CanInteract() && revealModule.Resumed == 2 && revealModule.Disposed == 1, "late reveal after cancellation cannot restart camera or progression");
        var trial = new MainGameAiShoujoH.Integration.KksOnePoseRequestScope(); var trialOwner = new object();
        Check(!trial.Capture(trialOwner, "houshi:2"), "ordinary Begin cannot inherit a trial stamp");
        trial.Run(trialOwner, () => Check(trial.Capture(trialOwner, "houshi:2"), "explicit matching trial captures once"));
        Check(!trial.Active && !trial.Capture(trialOwner, "houshi:2"), "successful trial scope clears");
        Reject(() => trial.Run(trialOwner, () => { throw new InvalidOperationException("injected"); }), "throwing builder propagates");
        Check(!trial.Active, "throwing builder clears trial scope");
        Reject(() => trial.Run(trialOwner, () => trial.Capture(new object(), "houshi:2")), "other owner cannot consume trial");
        Reject(() => trial.Run(trialOwner, () => trial.Capture(trialOwner, "aibu:0")), "other motion cannot consume trial");
        Reject(() => trial.Run(trialOwner, () => trial.Run(trialOwner, () => { })), "trial scope rejects reentry");
        Reject(() => trial.Run(trialOwner, () => { trial.Capture(trialOwner, "houshi:2"); trial.Capture(trialOwner, "houshi:2"); }), "trial rejects second Begin");
        Check(!trial.Active, "all rejected trial paths clear their stamps");
        Console.WriteLine("DEDICATED_H_BOUNDARY_PASS " + assertions);
    }
}
class AsyncCleanupModule : Module, IAsyncSessionCleanup
{
    public Action Completed;
    public Action<Exception> Failed;
    public void DisposeAsync(Action completed, Action<Exception> failed) { Completed = completed; Failed = failed; }
}
class FinalModule : Module, ISessionFinalizer
{
    public bool Started, Finished;
    Action complete;
    public void BeginFinalization(EndReason reason, Action completed, Action<Exception> failed) { Started = true; complete = completed; }
    public void Complete() { Finished = true; complete(); }
}

class SelectionModule : Module, IMotionSelectionTransition
{
    public int Selected, Suspended, Resumed; public bool PreparedUnderCover, Automatic;
    public void ValidateMotionSelection(string id, bool automatic) { if (id == "blocked") throw new InvalidOperationException("native guard"); }
    public void SuspendSelection() { ++Suspended; }
    public void ResumeSelection() { if (!Context.CanInteract()) throw new Exception("resume before Ready"); ++Resumed; }
    public override void Execute(Command command) { if (command.Name == "motion" || command.Name == "auto-motion") { ++Selected; PreparedUnderCover = Context.IsMotionPreparation() && !Context.CanInteract(); Automatic = command.Name == "auto-motion"; } }
}
