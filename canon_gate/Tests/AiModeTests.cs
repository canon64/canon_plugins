using System;
using CanonGate;
using UnityEngine;

internal static class AiModeTests
{
    internal static void Run(Action<bool, string> check)
    {
        int starts = default(int), stops = default(int), reads = default(int);
        bool allow = true;
        GateApi.SetRunning(true);
        GateApi.AllowActivity = (guid, kind) => { ++reads; return allow; };
        var owner = new MonoBehaviour();
        using (var client = GateApi.RegisterActivity("ai.test", GateApi.ActivityKind.AiShoujoMode,
            () => ++starts, () => ++stops))
        using (var blocked = GateApi.Register("unrelated.test", () => { throw new Exception("Unrelated plugin authorized"); }, () => { }))
        {
            client.Begin(owner);
            check(!client.IsActive && reads == default(int), "AI activity requires published owner, not manual Begin");
            using (var mode = GateApi.EnterAiShoujoMode(owner))
            {
                check(client.IsActive && ReferenceEquals(client.Scene, owner), "mode entry publishes exact owner");
                using(var furniture=GateApi.RegisterActivity("ai.furniture",GateApi.ActivityKind.AiShoujoMode,()=>{},()=>{},false))
                {
                    check(!furniture.IsActive,"furniture registration alone cannot create scene objects");
                    furniture.Begin(owner);var furniturePermit=furniture.Capture();
                    check(furniturePermit.IsValid,"furniture ready event captures exact published mode owner");
                    furniture.End();check(!furniturePermit.IsValid,"furniture end invalidates previous work");
                }
                var permit = client.Capture();
                int before = reads;
                client.Begin(owner); mode.Resume();
                check(reads == before && permit.IsValid, "repeated entry does not re-evaluate AI policy");
                var actor = new MonoBehaviour();
                mode.SetActor(actor);
                check(!permit.IsValid && ReferenceEquals(mode.Actor, actor) && client.IsActive, "actor replacement invalidates old work and binds new actor");
                before = reads; mode.SetActor(actor);
                check(reads == before, "same actor notification is idempotent");
                int resourceStarts = default(int), resourceStops = default(int);
                using (var resource = GateApi.RegisterActivity("ai.face-resource", GateApi.ActivityKind.AiShoujoMode, () => ++resourceStarts, () => ++resourceStops, false))
                {
                    resource.Begin(owner); int expectedStarts = resourceStarts; int expectedStops = resourceStops; var oldResourcePermit = resource.Capture(); int decisionReads = reads;
                    var resourceRelease = mode.SuspendActorActivities("ai.test", actor, "ai.face-resource");
                    check(!resource.IsActive && !oldResourcePermit.IsValid && resourceStops == ++expectedStops, "actor resource lease stops only its exact active target and revokes old work");
                    allow = false; resourceRelease(); resourceRelease();
                    check(resource.IsActive && resourceStarts == ++expectedStarts && reads == decisionReads, "resource return keeps its fixed permission without rereading policy and restores once");
                    allow = true;
                    bool deniedLease = false; try { mode.SuspendActorActivities("unknown", actor, "ai.face-resource"); } catch (InvalidOperationException) { deniedLease = true; }
                    check(deniedLease && resource.IsActive, "unregistered requester cannot borrow actor resources");
                    var staleRelease = mode.SuspendActorActivities("ai.test", actor, "ai.face-resource");
                    mode.SetActor(new MonoBehaviour()); int nextStarts = resourceStarts; staleRelease();
                    check(resourceStarts == nextStarts, "old actor resource lease cannot restart a replacement actor generation");
                    mode.SetActor(actor);
                }
                bool paused = false, resumed = false;
                var native = GateApi.AcquireDedicatedNativeEngine("ai.test", owner, actor, client.Id);
                var nativeScene = new HSceneProc(); native.Bind(nativeScene);
                check(native.IsValid && GateApi.IsDedicatedNativeEngine(nativeScene) && GateApi.Current == null,
                    "dedicated native engine binds exact AI generation without granting normal H permissions");
                check(!GateApi.IsDedicatedNativeEngine(new HSceneProc()) && !GateApi.CanRun("unrelated.test"),
                    "native engine identity does not authorize unrelated scenes or H plugins");
                var nativeGatePlugin = new CanonGate.Plugin();
                var hidden = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(CanonGate.Plugin).GetField("_hScene", hidden).SetValue(nativeGatePlugin, nativeScene);
                var activityBeforeNativeEvent = client.Capture(); int readsBeforeNativeEvent = reads;
                typeof(CanonGate.Plugin).GetMethod("OnStartH", hidden).Invoke(nativeGatePlugin, new object[] { null, EventArgs.Empty });
                typeof(CanonGate.Plugin).GetMethod("OnEndH", hidden).Invoke(nativeGatePlugin, new object[] { null, EventArgs.Empty });
                check(activityBeforeNativeEvent.IsValid && GateApi.Current == null && reads == readsBeforeNativeEvent,
                    "owned native start/end events preserve fixed AI permit without starting normal H or rereading policy");
                bool duplicateNative = false; try { GateApi.AcquireDedicatedNativeEngine("ai.test", owner, actor, client.Id); } catch (InvalidOperationException) { duplicateNative = true; }
                check(duplicateNative, "second native engine ownership rejected");
                mode.SetActor(new MonoBehaviour());
                check(!native.IsValid && GateApi.IsDedicatedNativeEngine(nativeScene), "actor invalidation stops native work but retains cleanup identity");
                native.Dispose(); native.Dispose();
                check(!GateApi.IsDedicatedNativeEngine(nativeScene), "native engine disposal clears only its bound identity once");
                mode.SetActor(actor);
                bool staleNative = false; try { GateApi.AcquireDedicatedNativeEngine("ai.test", owner, actor, default(ulong)); } catch (InvalidOperationException) { staleNative = true; }
                check(staleNative, "stale activity generation cannot acquire native engine");
                var cancelledLoad = GateApi.AcquireDedicatedNativeEngine("ai.test", owner, actor, client.Id);
                mode.Suspend(); var lateNativeScene = new HSceneProc(); cancelledLoad.BindCleanupScene(lateNativeScene);
                check(!cancelledLoad.IsValid && GateApi.IsDedicatedNativeEngine(lateNativeScene), "cancelled native load may bind cleanup identity but cannot regain permission");
                cancelledLoad.Dispose(); mode.Resume();
                mode.PauseActorMotion = () => { paused = true; return () => resumed = true; };
                check(mode.AcquireActorMotion(owner) == null && !paused, "motion acquisition rejects another actor");
                var releaseMotion = mode.AcquireActorMotion(actor);
                check(paused && releaseMotion != null, "motion acquisition pauses the exact published actor");
                releaseMotion(); check(resumed, "motion release returns actor control");
                allow = false; permit = client.Capture();
                check(permit.IsValid, "AI permission stays fixed after configuration change");
                mode.Suspend();
                check(mode.AcquireActorMotion(actor) == null, "suspended owner rejects motion acquisition");
                check(!permit.IsValid && !client.IsActive, "H handoff stops and revokes walking activity");
                var policy = new ExceptionPolicy(); policy.Load("", "", "ai.test");
                var h = new HSceneProc();
                GateApi.BeginSession(GateApi.SessionKind.H, false, policy, h, true);
                check(GateApi.Current.IsAiShoujoMode && ReferenceEquals(GateApi.Current.HScene, h), "H captures fixed AI origin and exact H scene");
                check(GateApi.CanRun("ai.test") && !GateApi.CanRun("unrelated.test") && !GateApi.IsAllowed, "AI H allows named clients without enabling every plugin");
                var hPermit = GateApi.Capture("ai.test");
                mode.Resume();
                check(GateApi.Current.HScene == h, "AI Resume cannot bypass an active H session");
                GateApi.EndSession(GateApi.SessionKind.H);
                check(!hPermit.IsValid && !client.IsActive, "H end revokes work before owner resumes");
                allow = true; mode.Resume();
                check(client.IsActive && !permit.IsValid, "explicit return starts fresh AI activity");
                mode.SetActor(null);
                check(mode.Actor == null && client.IsActive, "actor removal is published without ending subtitle lifetime");
                GateApi.SetRunning(false);
                check(!client.IsActive, "gate stop revokes AI clients");
                GateApi.SetRunning(true);
                check(!client.IsActive, "gate re-enable does not silently resume stale work");
            }
            check(GateApi.AiShoujoMode == null, "mode disposal clears owner and actor context");
        }
        GateApi.SetRunning(false); GateApi.AllowActivity = null;
    }
}
