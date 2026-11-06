using UnityEngine;
using Valve.VR;
using VRGIN.Core;
using Hip=MainGirlHipHijack.Plugin;
using Cuffs=MainGameVrHandcuffs.Plugin;

static partial class Program
{
    static Hip NewWristTest()
    {
        var p=Hip.Instance=new Hip();VR.Mode=new();SteamVR_Actions.legacy_emulate=new();
        CanonPlugins.Local.SessionGate.Allowed=true;
        p.Effectors[0].bone.position=new Vector3(.04f,0,0);
        p.Effectors[1].bone.position=new Vector3(1,0,0);
        p.Effectors[2].bone.position=Vector3.zero;
        VR.Mode.Right.transform.position=new Vector3(1,0,0);
        return p;
    }
    static void TestWristGrab()
    {
        foreach(int hand in new[]{0,1})
        {
            var p=NewWristTest();
            var controller=hand==0?VR.Mode.Left:VR.Mode.Right;
            var wrist=p.Effectors[hand].bone;
            wrist.position=controller.transform.position+new Vector3(.04f,0,0);
            wrist.rotation=Quaternion.Euler(12,30,20);
            var before=p._bikEff[hand];before.Proxy=new Transform{position=new Vector3(50,0,0)};
            before.FollowBone=new Transform();
            var position=wrist.position;var forward=wrist.forward;
            p.WristStep(hand,"Grip");var s=p._bikEff[hand];
            Check(p.HeldWrist(hand)==hand && s.Running && p._bikWant[hand],"off IK wrist auto-enabled on grip");
            Check(Vector3.Distance(position,s.Proxy.position)<1e-6f && Vector3.Distance(forward,s.Proxy.forward)<1e-6f,"preserve world position and angle at initial grab");
            Check(s.FollowBone==null && s.VrAutoEnabled && s.SuppressConfiguredFollow && p._bikWeight[hand]==1,"discard stale follow and force runtime weight one");
            Check(!p._settings.Enabled[hand] && p._settings.Weights[hand]==(hand==0?0f:.6f),"auto-enable leaves saved settings unchanged");
            Check(!Hip.TryBindRuntimeWrist("cuff",p._femaleSession.Character,hand==0?"left_hand":"right_hand",new Transform(),out _),"cannot attach cuffs while wrist is being dragged");
            controller.transform.position+=Vector3.up;
            controller.transform.rotation=Quaternion.Euler(0,90,0);
            Check(Vector3.Distance(position,s.Proxy.position)>.5f && Vector3.Distance(forward,s.Proxy.forward)>.5f,"parent moves and rotates wrist without a frame callback");
            var moved=s.Proxy.position;var direction=s.Proxy.forward;
            p.WristStep(hand,up:"Grip");
            Check(p.HeldWrist(hand)==-1 && s.Proxy.parent==null && s.Running && p._bikWant[hand],"normal release retains standalone IK and detaches parent");
            controller.transform.position+=Vector3.up;
            Check(Vector3.Distance(moved,s.Proxy.position)<1e-6f && Vector3.Distance(direction,s.Proxy.forward)<1e-6f,"released standalone pose remains fixed");
            controller.transform.position=s.Proxy.position;
            p.WristStep(hand,"Grip");
            p.NearestFollow=new Transform();p.WristStep(hand,"Trigger");
            Check(s.FollowBone==p.NearestFollow && !s.HasPostDragHold,"trigger sets follow without freezing the held controller target");
            p.WristStep(hand,up:"Grip");
            Check(s.FollowBone==p.NearestFollow && p.FollowRebinds>0,"release preserves follow and queues new release offset");
            p.WristStep(hand,"Grip");p.WristStep(hand,"Axis0");
            Check(!s.Running && !p._bikWant[hand] && s.FollowBone==null && s.Proxy.parent==null,"same-hand B clears follow and IK");
            Check(p.AwaitingGripUp(hand) && !controller.Current.IsValid,"B ends ownership and blocks until grip up");
            p.WristStep(hand,"Grip");Check(p.HeldWrist(hand)==-1,"no reacquire before grip up after B");
            p.WristStep(hand,up:"Grip");controller.transform.position=wrist.position;
            p.WristStep(hand,"Grip");Check(p.HeldWrist(hand)==hand,"reacquire after grip up");
            p.StopWristMode();Check(p._bikEff[hand].Running && p._bikEff[hand].Proxy.parent==null && !controller.Current.IsValid,"mode off preserves IK and releases focus");
        }
        var both=NewWristTest();both.WristStep(0,"Grip");both.WristStep(1,"Grip");
        Check(both.HeldWrist(0)==0 && both.HeldWrist(1)==1,"both controllers can hold separate wrists");
        both.WristStep(0,"Axis0");Check(both._bikEff[1].Running && both.HeldWrist(1)==1,"B on one controller preserves opposite wrist");
        both.StopWristMode();
        var exclusive=NewWristTest();exclusive.WristStep(0,"Grip");VR.Mode.Right.transform.position=exclusive._bikEff[0].Proxy.position;
        exclusive.WristStep(1,"Grip");Check(exclusive.HeldWrist(0)==0 && exclusive.HeldWrist(1)==-1,"same wrist cannot be grabbed by opposite controller");
        var previousFocus=VR.Mode.Left.Current;exclusive.WristStep(0,"Grip");
        Check(exclusive.HeldWrist(0)==0 && !previousFocus.IsValid && VR.Mode.Left.Current.IsValid,"new grip down recovers missed up with fresh ownership");
        VR.Mode.Left.Tracking.onTrackingChangedEvent(VR.Mode.Left.Tracking,SteamVR_Input_Sources.LeftHand,ETrackingResult.Uninitialized);
        Check(exclusive.HeldWrist(0)==-1 && exclusive._bikEff[0].Running && exclusive._bikEff[0].Proxy.parent==null && !VR.Mode.Left.Current.IsValid,"tracking loss releases ownership and retains pose");
        var manual=NewWristTest();var manualPoint=manual._bikEff[0];manualPoint.Running=true;manualPoint.Proxy=new Transform{position=new Vector3(.02f,0,0)};
        manualPoint.FollowBone=new Transform();manual._bikWant[0]=true;
        manual.WristStep(0,"Grip");manual.WristStep(0,up:"Grip");
        Check(ReferenceEquals(manualPoint,manual._bikEff[0]) && manualPoint.FollowBone!=null && manual._bikWeight[0]==.3f,"grabbing enabled IK preserves prior runtime point, follow, and weight");
        var revoked=NewWristTest();revoked.WristStep(0,"Grip");CanonPlugins.Local.SessionGate.Allowed=false;revoked.StopWristMode();
        Check(revoked.HeldWrist(0)==-1 && revoked._bikEff[0].Proxy.parent==null && !VR.Mode.Left.Current.IsValid,"cleanup detaches and releases after permit revocation");
        CanonPlugins.Local.SessionGate.Allowed=true;
        var locked=NewWristTest();VR.Mode.Left.Current=new();locked.WristStep(0,"Grip");
        Check(!locked._bikEff[0].Running && locked.HeldWrist(0)==-1 && VR.Mode.Left.Current.IsValid,"foreign focus refuses acquisition without changing IK");
        foreach(bool throws in new[]{false,true})
        {
            var fail=NewWristTest();var old=fail._bikEff[0];fail.EnableFails=!throws;fail.EnableThrows=throws;
            try { fail.WristStep(0,"Grip"); } catch(Exception) { }
            Check(ReferenceEquals(old,fail._bikEff[0]) && !fail._bikWant[0] && fail._bikWeight[0]==.3f && !VR.Mode.Left.Current.IsValid,"failed enable restores state and returns focus");
        }
        var far=NewWristTest();far.Effectors[0].bone.position=new Vector3(2,0,0);
        Check(far.Nearest(Vector3.zero)==-1,"disabled non-wrist is never auto-enabled");
        var denied=NewWristTest();CanonPlugins.Local.SessionGate.Allowed=false;denied.WristStep(0,"Grip");
        Check(!denied._bikEff[0].Running && denied.HeldWrist(0)==-1,"gate rejects automatic wrist grab");
        CanonPlugins.Local.SessionGate.Allowed=true;
        var focus=NewWristTest();focus.ConnectWristFocus();focus.WristStep(0,"Grip");focus.LoseWristFocus();
        Check(focus.HeldWrist(0)==-1 && focus._bikEff[0].Proxy.parent==null && !VR.Mode.Left.Current.IsValid,"focus loss releases wrist and focus without disabling IK");
    }
    static void TestIkThenHandcuffs()
    {
        var hip=NewWristTest();hip.WristStep(0,"Grip");
        var original=hip._bikEff[0];VR.Mode.Left.transform.position=new Vector3(.3f,0,0);
        hip.WristStep(0,up:"Grip");
        original.FollowBone=new Transform();
        hip.Effectors[0].bone.SetPositionAndRotation(original.Proxy.position,original.Proxy.rotation);
        var position=original.Proxy.position;var rotationDirection=original.Proxy.forward;
        var cuffs=new Cuffs();cuffs.CuffAt(0,position);cuffs.CuffAt(1,new Vector3(2,0,0));
        VR.Mode.Left.transform.position=position;cuffs.Start();
        SteamVR_Actions.legacy_emulate.Grip_Press.Down(SteamVR_Input_Sources.LeftHand);
        SteamVR_Actions.legacy_emulate.Axis1_Press.Down(SteamVR_Input_Sources.LeftHand);
        Check(cuffs.Wrist(0)==0 && hip._bikEff[0].ExternalOwner,"mount cuff on released IK-held wrist");
        Check(Vector3.Distance(position,hip.Effectors[0].target.position)<1e-6f && Vector3.Distance(rotationDirection,hip.Effectors[0].target.forward)<1e-6f,"cuff adopts exact wrist pose without snapping");
        Check(hip.Nearest(position)!=0,"cuffed wrist excluded from ordinary wrist grab");
        hip.WristStep(0,"Axis0");Check(cuffs.Wrist(0)==0 && hip._bikEff[0].ExternalOwner,"ordinary wrist B cannot reset cuff ownership");
        SteamVR_Actions.legacy_emulate.Grip_Press.Up(SteamVR_Input_Sources.LeftHand);
        cuffs.Delete();cuffs.Stop();
        Check(ReferenceEquals(original,hip._bikEff[0]) && hip._bikWant[0] && original.Running && original.FollowBone!=null,"deleting cuffs restores prior IK point and follow");
        Check(hip._bikWeight[0]==1 && hip.Effectors[0].target==original.Proxy && original.Proxy.parent==null,"restore runtime weight and independent original target");
        Check(Vector3.Distance(position,original.Proxy.position)<1e-6f,"cuff deletion restores prior IK pose");
        hip.WristStep(0,"Grip");Check(hip.HeldWrist(0)==0,"ordinary wrist grab works again after deleting cuffs");
        hip.WristStep(0,"Axis0");Check(!original.Running,"B returns restored uncuffed wrist to animation");
    }
}
