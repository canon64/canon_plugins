using UnityEngine;
using Valve.VR;
using VRGIN.Core;
using Cuffs=MainGameVrHandcuffs.Plugin;

static partial class Program
{
    static void TestCuffedElbow()
    {
        foreach(int hand in new[]{0,1}) foreach(int wrist in new[]{0,1})
        {
            var hip=NewWristTest();var c=hand==0?VR.Mode.Left:VR.Mode.Right;
            var source=hand==0?SteamVR_Input_Sources.LeftHand:SteamVR_Input_Sources.RightHand;
            var other=hand==0?SteamVR_Input_Sources.RightHand:SteamVR_Input_Sources.LeftHand;
            var actions=SteamVR_Actions.legacy_emulate;
            var position=hip.Effectors[wrist].bone.position;
            hip.Effectors[1-wrist].bone.position=position+new Vector3(2,0,0);
            int elbow=wrist==0?8:9;
            hip.Bends[elbow].bone2.position=position+new Vector3(0,-.3f,0);
            c.transform.position=position;
            var cuffs=new Cuffs();cuffs.CuffAt(0,position);cuffs.CuffAt(1,position+new Vector3(2,0,0));cuffs.Start();
            actions.Grip_Press.Down(source);actions.ApplicationMenu_Press.Down(source);
            Check(hip.AimingElbow(hand)==-1,"unattached cuff A cannot create an elbow target");
            actions.Axis1_Press.Down(source);Check(cuffs.Wrist(0)==wrist,"attach cuff before aiming elbow");
            actions.ApplicationMenu_Press.Down(other);Check(hip.AimingElbow(hand)==-1,"opposite controller A cannot aim held cuff elbow");
            actions.ApplicationMenu_Press.Down(source);
            var target=hip._bikEff[elbow].Proxy;
            Check(hip.AimingElbow(hand)==elbow && target!=null && hip._bikWant[elbow],"physical A on held cuff begins matching elbow IK");
            Check(hip._bikEff[wrist].ExternalOwner && cuffs.Wrist(0)==wrist,"elbow aim preserves cuff ownership and attachment");
            Check(Vector3.Distance(target.position,position+new Vector3(0,-.5f,0))<1e-5f,"cuff elbow target starts twenty centimetres beyond actual elbow");
            var local=target.localPosition;var wristTarget=hip._bikEff[wrist].Proxy;
            c.transform.rotation=Quaternion.Euler(0,0,75);
            Check(target.parent==wristTarget && Vector3.Distance(target.position,wristTarget.TransformPoint(local))<1e-5f,"cuff rotation changes elbow direction through existing target hierarchy");
            Check(cuffs.Held==0 && hip.Effectors[wrist].target==wristTarget,"aiming does not replace cuff wrist IK or release grip");
            actions.ApplicationMenu_Press.Up(other);Check(hip.AimingElbow(hand)==elbow,"opposite A up cannot end this aim");
            actions.ApplicationMenu_Press.Up(source);var placed=target.position;
            c.transform.position+=Vector3.up;
            Check(hip.AimingElbow(hand)==-1 && target.parent==null && Vector3.Distance(target.position,placed)<1e-6f && hip._bikEff[elbow].Running,"A up holds cuff elbow IK in world space");
            actions.ApplicationMenu_Press.Down(source);actions.Grip_Press.Up(source);
            Check(target.parent==null && hip.AimingElbow(hand)==-1 && hip._bikEff[elbow].Running,"releasing cuff grip also ends aiming and retains elbow pose");
            c.transform.position=cuffs.Part(0).position;
            cuffs.CuffAt(0,cuffs.Part(0).position); // Boundary bounds do not update with hierarchy movement.
            actions.Grip_Press.Down(source);actions.ApplicationMenu_Press.Down(source);
            Check(hip.AimingElbow(hand)==elbow,"cuffed elbow can be re-aimed after regrab");
            CanonPlugins.Shared.DedicatedVrButton.Current.Down(source);
            Check(cuffs.Wrist(0)==-1 && hip.AimingElbow(hand)==-1 && target.parent==null,"detaching wrist while aiming clears elbow parenting before restoring wrist IK");
            cuffs.Part(0).position=hip.Effectors[wrist].bone.position;
            actions.Axis1_Press.Down(source);actions.ApplicationMenu_Press.Down(source);
            Check(cuffs.Wrist(0)==wrist && hip.AimingElbow(hand)==elbow,"reattach and aim before testing focus loss");
            cuffs.FocusLost();Check(hip.AimingElbow(hand)==-1 && target.parent==null,"cuff focus loss ends elbow aim");
            cuffs.Delete();cuffs.Stop();
            actions.ApplicationMenu_Press.Down(source);Check(hip.AimingElbow(hand)==-1,"stopped cuff A listeners cannot create stale targets");
        }
        var ownerHip=NewWristTest();ownerHip.Bends[8].bone2.position=new Vector3(.04f,-.3f,0);
        var ownedTarget=new Transform{position=new Vector3(.04f,0,0)};
        Check(MainGirlHipHijack.Plugin.TryBindRuntimeWrist("cuff-test",ownerHip._femaleSession.Character,"left_hand",ownedTarget,out var owner),"acquire wrist for external elbow API");
        Check(owner.TryBeginElbowAim(false) && ownerHip.AimingElbow(0)==8,"valid wrist owner may start elbow aiming");
        CanonPlugins.Local.SessionGate.Allowed=false;owner.Dispose();
        Check(ownerHip.AimingElbow(0)==-1 && ownerHip._bikEff[8].Proxy.parent==null && !owner.IsValid,"owner disposal after gate revocation detaches elbow before restoring wrist");
        Check(!owner.TryBeginElbowAim(false),"disposed wrist lease cannot revive elbow aiming");
        CanonPlugins.Local.SessionGate.Allowed=true;
    }
}
