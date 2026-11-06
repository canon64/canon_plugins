using System;
using System.Linq;
using MainGameHLocationCoordinates;
using UnityEngine;

namespace MainGameHLocationPlacement
{
    internal static class RuntimeTests
    {
        internal static void Run(){HipLoad();Movement();NestedMapMovement();NestedMapRollback();Composer();DifferentSlotOrigins();}
        private static void NestedMapMovement()
        {
            using(var e=new TestEnvironment("category-during-native"))
            {
                var a=Program.Pose(1,0,2,20);var native=Program.Pose(5,1,-3,70);var target=Program.Pose(-8,2,4,160);var handPose=Program.Pose(2,1,3,40);
                e.Bridge.Female.SetPositionAndRotation(a.position,a.rotation);
                var actor=new ChaControl{transform=e.Bridge.Female};
                var hand=e.Hip.States[1];hand.Proxy.SetParent(e.Bridge.Female,false);hand.Proxy.SetPositionAndRotation(handPose.position,handPose.rotation);
                hand.HasPostDragHold=true;hand.PostDragHoldPos=handPose.position;hand.PostDragHoldRot=handPose.rotation;
                var point=new Point{Index=1,StartPos=handPose.position,StartRot=handPose.rotation,TargetPos=new Vector3(3,2,4),TargetRot=Quaternion.Euler(new Vector3(0,90,0))};
                var pointTarget=new RigidPose(point.TargetPos,point.TargetRot);e.Hip._activeTransitionPoints.Add(point);
                var independent=e.Hip.States[2];independent.Proxy.SetPositionAndRotation(handPose.position,handPose.rotation);
                e.Scene.Move=()=>{
                    e.Bridge.Female.SetPositionAndRotation(native.position,native.rotation);
                    R.Call(e.Session,"Moving",actor,1L,"category-recall","tag-4");
                    e.Bridge.Female.SetPositionAndRotation(target.position,target.rotation);
                    R.Call(e.Session,"Moved",actor,1L,"category-recall","tag-4");
                };
                e.Scene.ChangeAnimator();
                Program.Near(RuntimeBridge.Pose(hand.Proxy),RigidPose.Rebase(a,target,handPose),"native and category move apply exactly once");
                Program.Near(new RigidPose(hand.PostDragHoldPos,hand.PostDragHoldRot),RigidPose.Rebase(a,target,handPose),"hold receives native plus category delta once");
                Program.Near(RuntimeBridge.Pose(independent.Proxy),RigidPose.Rebase(a,target,handPose),"world independent IK receives combined delta once");
                Program.Near(new RigidPose(point.StartPos,point.StartRot),RigidPose.Rebase(a,target,handPose),"nested move rebases transition start once");
                Program.Near(new RigidPose(point.TargetPos,point.TargetRot),RigidPose.Rebase(a,target,pointTarget),"nested move rebases transition target once");
                e.Scene.Move=()=>e.Bridge.Female.SetPositionAndRotation(a.position,a.rotation);e.Scene.ChangeAnimator();
                Program.Near(RuntimeBridge.Pose(hand.Proxy),handPose,"next native move recovers original without drift");
            }
        }
        private static void NestedMapRollback()
        {
            using(var e=new TestEnvironment("category-rollback"))
            {
                var a=Program.Pose(2,0,-1,25);var native=Program.Pose(-3,2,7,115);var p=Program.Pose(4,1,2,50);
                e.Bridge.Female.SetPositionAndRotation(a.position,a.rotation);var hand=e.Hip.States[1];hand.Proxy.SetPositionAndRotation(p.position,p.rotation);
                var actor=new ChaControl{transform=e.Bridge.Female};
                e.Scene.Move=()=>{
                    e.Bridge.Female.SetPositionAndRotation(native.position,native.rotation);
                    R.Call(e.Session,"Moving",actor,7L,"category-recall","tag-3");
                    // A failed map placement rolls the root back before emitting its completion.
                    R.Call(e.Session,"Moved",actor,7L,"rollback","native");
                    throw new InvalidOperationException("outer native failed after rollback");
                };
                Program.Throws(()=>e.Scene.ChangeAnimator(),"nested rollback retains outer exception");
                Program.Near(RuntimeBridge.Pose(hand.Proxy),RigidPose.Rebase(a,native,p),"rollback preserves only committed native movement");
                Program.Assert(R.Get(e.Session,"_pendingNativeMove")==null&&R.Get<int>(e.Session,"_nativeDepth")==0,"exception clears pending native snapshot and nesting");
                e.Scene.Move=()=>e.Bridge.Female.SetPositionAndRotation(a.position,a.rotation);e.Scene.ChangeAnimator();
                Program.Near(RuntimeBridge.Pose(hand.Proxy),p,"move after nested rollback has no stale delta");
                var pending=e.Session.StartNativeMove(e.Scene);e.Session.Stop();
                Program.Assert(R.Get(e.Session,"_pendingNativeMove")==null,"session end releases in-flight snapshot");
                e.Session.EndNativeMove(e.Scene,pending,null);
                Program.Near(RuntimeBridge.Pose(hand.Proxy),p,"late native finalizer cannot move stopped session");
            }
            using(var e=new TestEnvironment("category-next-session"))
                Program.Assert(R.Get(e.Session,"_pendingNativeMove")==null&&R.Get<int>(e.Session,"_nativeDepth")==0,"next session starts without old movement state");
        }
        private static void HipLoad()
        {
            using(var e=new TestEnvironment("hip-load"))
            {
                var a=Program.Pose(5,1,-7,30);var b=Program.Pose(-9,3,2,150);var p=Program.Pose(7,2,-8,70);
                e.Bridge.Female.SetPositionAndRotation(a.position,a.rotation);
                foreach(var entry in e.Hip.NextPreset.entries){entry.proxyPosition=p.position;entry.proxyRotation=p.rotation;}
                e.Hip.SaveCurrentPosePresetWithScreenshot("hands");var original=e.Hip._posePresets.Single();
                e.Bridge.Female.SetPositionAndRotation(b.position,b.rotation);
                e.Hip._settings.FollowComposerEnabled[0]=true;e.Hip._settings.FollowComposerObjectIds[0]="hook";
                e.Hip.States[3].ExternalOwner=true;
                Program.Assert(e.Hip.ApplyPosePreset(original,true,"auto"),"normal automatic entry executes");
                Program.Assert(!ReferenceEquals(original,e.Hip.LastApplied),"load transforms a copy");
                Program.Near(new RigidPose(original.entries[1].proxyPosition,original.entries[1].proxyRotation),p,"saved Hip original unchanged");
                Program.Near(new RigidPose(e.Hip.LastApplied.entries[1].proxyPosition,e.Hip.LastApplied.entries[1].proxyRotation),RigidPose.Rebase(a,b,p),"individual hand transformed through shared coordinates");
                Program.Assert(e.Hip.Touched.SequenceEqual(new[]{1}),"real Harmony filter skips linked hand, native body exclusion, and external owner");
                Program.Near(new RigidPose(e.Hip.LastApplied.entries[0].proxyPosition,e.Hip.LastApplied.entries[0].proxyRotation),p,"linked target not transformed twice");
                e.Hip._settings.FollowComposerEnabled[0]=false;e.Hip._settings.FollowComposerEnabled[2]=true;e.Hip._settings.FollowComposerObjectIds[2]="cylinder";
                e.Hip.ApplyPosePreset(original,true,"manual");Program.Assert(e.Hip.Touched.Contains(0)&&e.Hip.Touched.Contains(1),"waist-only linked config leaves hands independent");
                var unknown=new HipPreset();unknown.entries[1].proxyPosition=p.position;e.Hip.ApplyPosePreset(unknown,true,"legacy");
                Program.Assert(e.Hip.LastApplied.entries[1].proxyPosition.x==p.position.x&&Plugin.Instance.Status.Contains("保存元未登録"),"legacy no-origin loads without guessed conversion and reports status");
                e.Hip.NextPreset=new HipPreset();e.Hip.NextPreset.entries[1].proxyPosition=p.position;e.Hip.NextPreset.entries[1].hasFollowBone=true;
                e.Bridge.Female.SetPositionAndRotation(a.position,a.rotation);e.Hip.SaveCurrentPosePresetWithScreenshot("bone");
                e.Bridge.Female.SetPositionAndRotation(b.position,b.rotation);e.Hip.ApplyPosePreset(e.Hip._posePresets[0],true,"bone");
                Program.Assert(e.Hip.LastApplied.entries[1].proxyPosition.x==p.position.x,"bone-local follow is not given world-fixed correction");
                e.Hip.States[1].FollowBone=new GameObject().transform;Program.Assert(!e.Bridge.Independent(1),"live bone follow excluded from H-place movement");
                e.Hip.States[1].FollowBone=null;e.Hip._runtimeWrists[1]=new object();Program.Assert(!e.Bridge.Independent(1),"external wrist excluded");
                e.Hip._runtimeWrists[1]=null;e.Bridge.Female.localScale=new Vector3(2,2,2);
                e.Hip.ApplyPosePreset(original,true,"unsupported-scale");
                Program.Assert(ReferenceEquals(e.Hip.LastApplied,original)&&Plugin.Instance.Status.Contains("scale"),"unsupported destination scale cannot produce a partial coordinate conversion");
            }
        }
        private static void Movement()
        {
            using(var e=new TestEnvironment("movement"))
            {
                var a=Program.Pose(1,0,2,20);var b=Program.Pose(-10,3,5,110);var p=Program.Pose(3,1,4,50);
                e.Bridge.Female.SetPositionAndRotation(a.position,a.rotation);
                e.Hip._settings.FollowComposerEnabled[0]=true;e.Hip._settings.FollowComposerObjectIds[0]="hook";
                e.Hip.States[3].FollowBone=new GameObject().transform;
                e.Hip.States[0].FollowBone=new GameObject().transform;e.Hip.States[0].HasFollowDistanceBaseline=true;
                var hand=e.Hip.States[1];hand.Proxy.SetParent(e.Bridge.Female,false);hand.Proxy.SetPositionAndRotation(p.position,p.rotation);
                hand.HasPostDragHold=true;hand.PostDragHoldPos=p.position;hand.PostDragHoldRot=p.rotation;
                var body=e.Hip.States[2];body.Proxy.SetPositionAndRotation(p.position,p.rotation);
                var point=new Point{Index=1,StartPos=p.position,StartRot=p.rotation,TargetPos=new Vector3(4,2,5),TargetRot=Quaternion.Euler(new Vector3(0,80,0))};
                e.Hip._activeTransitionPoints.Add(point);
                var linked=new Point{Index=0,StartPos=p.position,TargetPos=p.position};e.Hip._activeTransitionPoints.Add(linked);
                var target=new RigidPose(point.TargetPos,point.TargetRot);
                e.Scene.Move=()=>e.Bridge.Female.SetPositionAndRotation(b.position,b.rotation);e.Scene.ChangeAnimator();
                Program.Near(RuntimeBridge.Pose(hand.Proxy),RigidPose.Rebase(a,b,p),"parented hand receives exactly one movement");
                Program.Near(RuntimeBridge.Pose(body.Proxy),RigidPose.Rebase(a,b,p),"nonlinked body participates without changing native enable behavior");
                Program.Near(new RigidPose(hand.PostDragHoldPos,hand.PostDragHoldRot),RigidPose.Rebase(a,b,p),"post-drag hold follows H place");
                Program.Near(new RigidPose(point.StartPos,point.StartRot),RigidPose.Rebase(a,b,p),"active transition start rebased");
                Program.Near(new RigidPose(point.TargetPos,point.TargetRot),RigidPose.Rebase(a,b,target),"active transition target rebased");
                Program.Assert(linked.TargetPos.x==p.position.x,"linked transition is untouched");
                Program.Assert(!e.Hip.States[0].HasFollowDistanceBaseline&&e.Hip.States[0].Running,"place move resets native distance baseline without changing linked IK enable state");
                for(int i=0;i<20;i++)
                {
                    e.Scene.Move=()=>e.Bridge.Female.SetPositionAndRotation(a.position,a.rotation);e.Scene.SetLocalPosition();
                    Program.Near(RuntimeBridge.Pose(hand.Proxy),p,"move cancel / return");
                    e.Scene.Move=()=>e.Bridge.Female.SetPositionAndRotation(b.position,b.rotation);e.Scene.SetLocalPosition();
                    Program.Near(RuntimeBridge.Pose(hand.Proxy),RigidPose.Rebase(a,b,p),"repeated moves do not accumulate");
                }
                e.Scene.Move=()=>throw new InvalidOperationException("native fail");Program.Throws(()=>e.Scene.ChangeAnimator(),"native exception retained");
                e.Scene.Move=()=>e.Bridge.Female.SetPositionAndRotation(a.position,a.rotation);e.Scene.ChangeAnimator();Program.Near(RuntimeBridge.Pose(hand.Proxy),p,"native nesting recovers after exception");
                e.Session.Stop();e.Scene.Move=()=>e.Bridge.Female.SetPositionAndRotation(b.position,b.rotation);var oldBody=RuntimeBridge.Pose(body.Proxy);e.Scene.ChangeAnimator();
                Program.Near(RuntimeBridge.Pose(body.Proxy),oldBody,"expired session does not act on old proxies");
            }
        }
        private static void Composer()
        {
            using(var e=new TestEnvironment("composer"))
            {
                var a=Program.Pose(2,0,3,30);var b=Program.Pose(-8,2,10,120);e.Bridge.Female.SetPositionAndRotation(a.position,a.rotation);
                e.Composer.NextData.localPosition=new Vector3(.2f,.9f,.1f);e.Composer.NextData.localEulerAngles=new Vector3(0,40,0);
                e.Composer.SaveCurrentToPreset(null,"rotations",0);var preset=e.Composer._presets.Single();e.Composer.ActivatePreset(preset);
                var data=(Data)JsonUtility.FromJson(preset.loop1Json[0],typeof(Data));var parent=e.Composer.ResolveConfiguredParentTransform(data,data.id,out var kind,out var reference);
                Program.Assert(parent!=e.Bridge.Female&&kind=="external"&&reference=="char:root","hidden parent keeps logical original parent");
                var go=new GameObject("parent-rotation");go.transform.SetParent(parent,false);go.transform.localPosition=data.localPosition;go.transform.localEulerAngles=data.localEulerAngles;
                var child=new GameObject("child-rotation");child.transform.SetParent(go.transform,false);child.transform.localPosition=new Vector3(.1f,.2f,.3f);child.transform.localEulerAngles=new Vector3(0,55,0);
                var rr=new RuntimeObject{Data=data,GameObject=go};e.Composer._runtimeObjects[data.id]=rr;
                var parentBefore=RuntimeBridge.Pose(go.transform);var childBefore=RuntimeBridge.Pose(child.transform);var localBefore=new RigidPose(child.transform.localPosition,child.transform.localRotation);
                var actor=new ChaControl{transform=e.Bridge.Female};var native=Program.Pose(3,1,-5,65);
                e.Scene.Move=()=>{
                    e.Bridge.Female.SetPositionAndRotation(native.position,native.rotation);
                    R.Call(e.Session,"Moving",actor,11L,"category-recall","tag-2");
                    e.Bridge.Female.SetPositionAndRotation(b.position,b.rotation);
                    R.Call(e.Session,"Moved",actor,11L,"category-recall","tag-2");
                };e.Scene.ChangeAnimator();
                Program.Near(RuntimeBridge.Pose(go.transform),RigidPose.Rebase(a,b,parentBefore),"Composer parent placed at nested category H location once");
                Program.Near(RuntimeBridge.Pose(child.transform),RigidPose.Rebase(a,b,childBefore),"child receives one whole-hierarchy transform");
                Program.Near(new RigidPose(child.transform.localPosition,child.transform.localRotation),localBefore,"child local rotation preserved");
                e.Composer.SyncDataFromRuntime(rr);Program.Assert(data.parentKind=="external"&&data.parentRefId=="char:root","save synchronization cannot persist private parent identity");
                e.Composer.NextData=data;e.Composer.SaveCurrentToPreset(preset,null,0);
                Program.Assert(go.transform.parent==parent,"saving an active preset preserves the live anchor and rotation phase");
                var direct=Program.Pose(15,1,-8,150);e.Bridge.Female.SetPositionAndRotation(direct.position,direct.rotation);
                e.Composer.SaveCurrentToPreset(preset,null,0);
                var directSnapshot=e.Bridge.ComposerSnapshot(preset);var directOrigin=e.Store.Match(e.Bridge.ComposerPath,directSnapshot,directSnapshot.Parts[0]);
                Program.Near(directOrigin.source.femaleRoot.ToPose(),direct,"save captures current root without requiring a prior movement notification");
                Program.Near(directOrigin.source.parents[0].world.ToPose(),direct,"unchanged endpoint parent uses the freshly captured destination");
                var world=RuntimeBridge.Pose(go.transform);e.Composer.DeactivatePreset();Program.Assert(go.transform.parent==e.Bridge.Female,"deactivate detaches real object before destroying anchor");Program.Near(RuntimeBridge.Pose(go.transform),world,"anchor removal does not jump");
            }
        }
        private static void DifferentSlotOrigins()
        {
            using(var e=new TestEnvironment("slot-origins"))
            {
                var a=Program.Pose(2,0,1,0);var b=Program.Pose(20,1,-7,90);var c=Program.Pose(-4,3,8,180);
                var data1=new Data{parentKind="root",parentRefId="",localPosition=new Vector3(.2f,.8f,.1f)};
                var data2=new Data{parentKind="root",parentRefId="",localPosition=new Vector3(-.1f,.3f,.7f),localEulerAngles=new Vector3(0,25,0)};
                var preset=new ComposerPreset{loop1Json=new System.Collections.Generic.List<string>{JsonUtility.ToJson(data1)},loop2Json=new System.Collections.Generic.List<string>{JsonUtility.ToJson(data2)}};
                preset.motionIntensityTargetIds.Add("root");
                preset.motionPositionRulesJson.Add(JsonUtility.ToJson(new{objectId="root",level=2,splitLoops=true,l1Offset=new Vector3(0,0,1),l2Offset=new Vector3(1,0,0)}));
                var movement=new MovementRule{objectId="root",field="py",l1ZeroSet=true,l2ZeroSet=true,l1Zero=.9f,l2Zero=1.7f};
                preset.movementRulesJson.Add(JsonUtility.ToJson(movement));
                e.Composer._presets.Add(preset);var snapshot=e.Bridge.ComposerSnapshot(preset);
                var source1=Program.Origin(a);var source2=Program.Origin(b);
                foreach(var s in new[]{source1,source2}){s.parents[0].kind="root";s.parents[0].reference="";s.parents[0].world=PoseNumbers.From(RigidPose.Identity);}
                using(var pending=e.Store.Prepare(e.Bridge.ComposerPath,snapshot,source1,new[]{"loop1Json"}))pending.Commit();
                using(var pending=e.Store.Prepare(e.Bridge.ComposerPath,snapshot,source2,new[]{"loop2Json"}))pending.Commit();
                e.Bridge.Female.SetPositionAndRotation(c.position,c.rotation);e.Composer.NativeParent=new GameObject().transform;e.Composer.ActivatePreset(preset);
                var anchor=e.Composer.ResolveConfiguredParentTransform(data1,"root",out _,out _);var normalized=e.Composer._activeL2ById["root"];
                var world=RuntimeBridge.Pose(anchor)*new RigidPose(normalized.localPosition,Quaternion.Euler(normalized.localEulerAngles));
                Program.Near(world,RigidPose.Rebase(b,c,new RigidPose(data2.localPosition,Quaternion.Euler(data2.localEulerAngles))),"L2 uses its own source map frame");
                e.Composer.RebuildActivePresetCache();normalized=e.Composer._activeL2ById["root"];
                Program.Near(RuntimeBridge.Pose(anchor)*new RigidPose(normalized.localPosition,Quaternion.Euler(normalized.localEulerAngles)),world,"cache rebuild does not double-transform endpoint");
                Program.Assert(preset.loop2Json[0]==JsonUtility.ToJson(data2),"saved endpoint remains authored original");
                var relative=RigidPose.Rebase(a,c,RigidPose.Identity).Inverse()*RigidPose.Rebase(b,c,RigidPose.Identity);
                var expectedOffset=relative.rotation*new Vector3(1,0,0);
                Program.Assert((e.Composer.GetEffectiveMotionPositionOffset("root")-expectedOffset).magnitude<.001f,"strength offset uses the L2 parent basis");
                var rr=new RuntimeObject{Data=new Data(),GameObject=new GameObject()};
                e.Composer.ApplyMovementPose(rr,data1,data2,1);
                Program.Assert((rr.Data.localPosition-relative.Point(new Vector3(data2.localPosition.x,1.7f,data2.localPosition.z))).magnitude<.001f,"gauge endpoint transforms all coupled position axes");
                Program.Assert(e.Composer.FindMovementField(preset,"root","py").l2Zero==1.7f,"UI still edits authored rule rather than projected cache");
                string originalRules=preset.movementRulesJson[0];
                e.Composer.ApplyMovementPose(rr,data1,data2,1);Program.Assert(preset.movementRulesJson[0]==originalRules,"gauge evaluation never rewrites saved rules");
                preset.motionPositionRulesJson[0]=JsonUtility.ToJson(new{objectId="root",level=2,splitLoops=true,l1Offset=new Vector3(0,0,1),l2Offset=new Vector3(2,0,0)});
                e.Composer.SaveMotionPresetMutation();
                Program.Assert((e.Composer.GetEffectiveMotionPositionOffset("root")-relative.rotation*new Vector3(2,0,0)).magnitude<.001f,"coordinate rule edit refreshes event cache");
            }
        }
    }
}
