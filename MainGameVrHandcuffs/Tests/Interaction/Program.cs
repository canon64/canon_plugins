using System.Reflection;
using UnityEngine;
using Valve.VR;
using VRGIN.Core;
using Hip=MainGirlHipHijack.Plugin;
using Cuffs=MainGameVrHandcuffs.Plugin;

static partial class Program
{
    static int checks;
    static void Check(bool value,string message) { checks++;if(!value)throw new Exception(message); }
    static void Main()
    {
        TestHotelRestore();TestWristFollowFeedback();TestCuffedElbow();TestElbowAim();TestWristGrab();TestIkThenHandcuffs();TestMountPose();TestWristLeases();
        foreach(var hand in new[]{SteamVR_Input_Sources.LeftHand,SteamVR_Input_Sources.RightHand})
            foreach(int first in new[]{0,1}) TestInteraction(hand,first);
        TestHeldConstraint();TestChainOrientation();TestReleaseFailure();TestAsset();TestDistance();
        Console.WriteLine($"PASS {checks} production input/lease/kinematic-constraint/asset checks. Native VR and IK are boundary doubles, not an in-game test.");
    }
    static void TestWristLeases()
    {
        var hip=Hip.Instance=new Hip();
        var previous=hip._bikEff[0];previous.Running=true;previous.FollowBone=new Transform();
        hip._bikWant[0]=true;
        var originalTarget=hip.Effectors[0].target=new Transform();
        hip.Effectors[0].positionWeight=.3f;hip.Effectors[0].rotationWeight=.2f;
        var target=new Transform();var female=hip._femaleSession.Character;
        Check(!Hip.TryBindRuntimeWrist("x",new ChaControl(),"left_hand",target,out _),"reject another female");
        Check(!Hip.TryBindRuntimeWrist("x",female,"left_elbow",target,out _),"only wrists");
        CanonPlugins.Local.SessionGate.Allowed=false;
        Check(!Hip.TryBindRuntimeWrist("x",female,"left_hand",target,out _),"gate denies acquisition");
        CanonPlugins.Local.SessionGate.Allowed=true;
        Check(Hip.TryBindRuntimeWrist("a",female,"left_hand",target,out var a),"acquire wrist");
        Check(hip.Effectors[0].target==target && hip.Effectors[0].positionWeight==1 && hip.Effectors[0].rotationWeight==1,"position and rotation IK 100 percent");
        Check(!Hip.TryBindRuntimeWrist("b",female,"left_hand",new Transform(),out _),"another owner cannot replace lease");
        Check(Hip.TryBindRuntimeWrist("b",female,"right_hand",new Transform(),out var b),"other wrist independent");
        a.Dispose();a.Dispose();
        Check(ReferenceEquals(previous,hip._bikEff[0]) && previous.FollowBone!=null && hip._bikWant[0],"restore entire old runtime point and follow state");
        Check(hip.Effectors[0].target==originalTarget && hip.Effectors[0].positionWeight==.3f && hip.Effectors[0].rotationWeight==.2f,"restore exact native solver target and weights");
        Check(b.IsValid && !a.IsValid,"releasing one owner preserves the other wrist");
        bool invalidated=false;b.Invalidated+=()=>invalidated=true;
        CanonPlugins.Local.SessionGate.Allowed=false;hip.End();
        Check(!b.IsValid && invalidated,"cleanup works after permit revoked");
        CanonPlugins.Local.SessionGate.Allowed=true;
        hip=Hip.Instance=new Hip();
        Check(Hip.TryBindRuntimeWrist("a",hip._femaleSession.Character,"left_hand",new Transform(),out a),"acquire disabled solver");
        Check(hip._femaleSession.FullBodyIk.enabled,"solver enabled while borrowed");a.Dispose();
        Check(!hip._femaleSession.FullBodyIk.enabled,"restore previously disabled solver");
        hip=Hip.Instance=new Hip();
        Hip.TryBindRuntimeWrist("a",hip._femaleSession.Character,"left_hand",new Transform(),out a);
        Hip.TryBindRuntimeWrist("b",hip._femaleSession.Character,"right_hand",new Transform(),out b);
        a.Invalidated+=()=>throw new Exception("owner cleanup fault");
        try { hip.End(); } catch (Exception) { }
        Check(!a.IsValid && !b.IsValid && hip.Effectors.All(e=>e.positionWeight==0),"one failing owner callback cannot strand the other wrist");
    }
    static void TestInteraction(SteamVR_Input_Sources hand,int first)
    {
        var hip=Hip.Instance=new Hip(); VR.Mode=new();SteamVR_Actions.legacy_emulate=new();
        var controller=hand==SteamVR_Input_Sources.LeftHand?VR.Mode.Left:VR.Mode.Right;
        var other=hand==SteamVR_Input_Sources.LeftHand?SteamVR_Input_Sources.RightHand:SteamVR_Input_Sources.LeftHand;
        var grip=SteamVR_Actions.legacy_emulate.Grip_Press;var trigger=SteamVR_Actions.legacy_emulate.Axis1_Press;
        var p=new Cuffs();p.CuffAt(first,new Vector3(0,0,0));p.CuffAt(1-first,new Vector3(.3f,0,0));p.Start();p.InitAudio();
        var sound=p.SoundSource(first);var otherSound=p.SoundSource(1-first);var clip=p.SoundClip;
        Check(clip.Samples.Length==41280 && clip.Rate==48000 && clip.Samples.Any(s=>Math.Abs(s)>.1f),"approved reverb sample decoded from embedded audio");
        Check(!sound.playOnAwake && !sound.loop && sound.spatialBlend==1 && sound.dopplerLevel==0 && sound.reverbZoneMix==0,"one-shot positional audio without extra reverb or Doppler");
        p.CanAct=false;grip.Down(hand);Check(p.Held==-1,"no capture without active permit");p.CanAct=true;
        controller.Current=new VRGIN.Controls.Controller.Lock();grip.Down(hand);
        Check(p.Held==-1,"do not steal a live foreign lock");controller.Current.Release();
        grip.Down(hand);Check(p.Held==first,"grab either cuff from either controller");
        Check(p.Part(1-first).parent!=controller.transform,"free cuff remains independent while held cuff follows controller");
        Check(p.Part(first).transform.parent==controller.transform,"held cuff follows through parent relationship");
        grip.Down(other);grip.Up(other);Check(p.Held==first,"other controller cannot steal or release");
        trigger.Down(other);Check(p.Wrist(first)==-1,"wrong trigger cannot bind");
        hip.Effectors[0].bone.position=new Vector3(.051f,0,0);hip.Effectors[1].bone.position=new Vector3(1,0,0);
        trigger.Down(hand);Check(p.Wrist(first)==-1,"outside five centimetres rejected");
        Check(sound.Plays==0,"failed distance test remains silent");
        hip.Effectors[0].bone.position=new Vector3(.049f,0,0);
        hip._pendingAbandonByPostureChange=true;trigger.Down(hand);
        Check(p.Wrist(first)==-1 && Vector3.Distance(p.Part(first).position,Vector3.zero)==0,"failed IK acquisition rolls back snapped cuff without binding state");
        Check(sound.Plays==0,"failed IK acquisition remains silent");
        hip._pendingAbandonByPostureChange=false;
        trigger.Down(hand);Check(p.Wrist(first)==0,"bind held cuff inside five centimetres");
        Check(sound.Plays==1 && otherSound.Plays==0,"successful attachment plays exactly once at the attached cuff");
        trigger.Down(hand);Check(sound.Plays==1,"already attached trigger cannot replay attachment sound");
        Check(hip.Effectors[0].positionWeight==1 && hip.Effectors[0].rotationWeight==1,"bound wrist stays fully IK driven");
        grip.Up(hand);
        Check(p.Held==-1 && p.Wrist(first)==0,"release keeps attachment");
        var frozen=Enumerable.Range(0,5).Select(i=>p.Part(i).position).ToArray();
        controller.transform.position+=Vector3.up;Hip.EvaluateItems();
        Check(Enumerable.Range(0,5).All(i=>Vector3.Distance(p.Part(i).position,frozen[i])<1e-6f),"released cuffs and links remain exactly fixed even if controller moves");
        controller.transform.position-=Vector3.up;
        var retiring=controller.Current;Application.isFocused=false;grip.Down(hand);
        Check(p.Held==first && !retiring.IsValid && !controller.Current.IsInvalidating,"immediate regrab retires old lock and obtains a fresh one");
        Application.isFocused=true;
        grip.Down(hand);Check(p.Held==first,"fresh Down recovers a missed Up");grip.Up(hand);
        p.CuffAt(first,new Vector3(1,0,0));p.CuffAt(1-first,new Vector3(0,0,0));
        grip.Down(hand);Check(p.Held==1-first,"switch to opposite cuff after release");
        hip.Effectors[0].bone.position=new Vector3(0,0,0);trigger.Down(hand);
        Check(p.Wrist(1-first)==-1,"cannot bind the occupied wrist again");
        hip.Effectors[1].bone.position=new Vector3(.02f,0,0);trigger.Down(hand);
        Check(p.Wrist(0)>=0 && p.Wrist(1)>=0 && p.Wrist(0)!=p.Wrist(1),"bind both distinct wrists");
        Check(sound.Plays==1 && otherSound.Plays==1,"second attachment plays independently on the other cuff");
        var detach=CanonPlugins.Shared.DedicatedVrButton.Current;
        SteamVR_Actions.legacy_emulate.ApplicationMenu_Press.Down(hand);
        SteamVR_Actions.legacy_emulate.Axis0_Press.Down(hand);
        Check(p.Wrist(1-first)==1 && otherSound.Plays==1,"physical A and shared stick action cannot detach cuff");
        detach.Down(other);Check(p.Wrist(1-first)==1 && otherSound.Plays==1,"other hand B cannot detach held cuff");
        p.CanAct=false;detach.Down(hand);p.CanAct=true;
        Check(p.Wrist(1-first)==1 && otherSound.Plays==1,"B detach obeys active permit");
        detach.Down(hand);
        Check(p.Wrist(1-first)==-1 && p.Wrist(first)==0,"B detaches only the held side and preserves opposite wrist binding");
        Check(hip.Effectors[1].positionWeight==0 && hip.Effectors[0].positionWeight==1,"B restores detached wrist IK without affecting other wrist");
        Check(p.Held==1-first && p.Part(1-first).parent==controller.transform && controller.Current.IsValid,"detached cuff stays held with the same focus and parent");
        Check(otherSound.Plays==2 && sound.Plays==1,"detach plays the closure sound once at that cuff");
        detach.Down(hand);Check(otherSound.Plays==2,"unattached B does not replay sound");
        hip.Effectors[1].bone.position=p.Part(1-first).position;
        trigger.Down(hand);
        Check(p.Wrist(1-first)==1 && otherSound.Plays==3,"can reattach after B detach with fresh IK and sound");
        p.FocusLost();Check(p.Held==-1 && p.Wrist(first)==0,"focus loss freezes without detaching wrists");
        detach.Down(hand);Check(p.Wrist(1-first)==1 && otherSound.Plays==3,"B without holding cannot detach or play sound");
        p.Delete();p.Stop();Check(hip.Effectors.All(e=>e.positionWeight==0),"delete restores both prior IK weights");
        p.ClearAudio();p.ClearAudio();
        Check(clip.Destroyed && sound.Stopped && otherSound.Stopped && p.SoundClip==null,"audio cleanup stops both sources and disposes the shared clip idempotently");
        Check(!controller.Current.IsValid,"delete completes owned retiring focus");
        grip.Down(hand);Check(p.Held==-1,"stopped listeners disconnected");
    }
    static void TestAsset()
    {
        using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("Handcuffs.Model");using var r=new BinaryReader(s);
        Check(r.ReadInt32()==0x48434631 && r.ReadInt32()==5,"embedded approved mesh has five bodies");
        int count=r.ReadInt32();s.Position+=5*12;int[] groups=new int[5];
        for(int i=0;i<count;i++)
        {
            int group=r.ReadInt32();int length=r.ReadInt32();s.Position+=length;int n=r.ReadInt32();groups[group]++;
            Check(n>0 && n%3==0,"valid mesh triangles");
            bool finite=true;for(int j=0;j<n*6;j++) finite &= float.IsFinite(r.ReadSingle());
            Check(finite,"finite vertex and normal data");
        }
        Check(groups[0]>1 && groups[1]>1 && groups[2]==1 && groups[3]==1 && groups[4]==1,"two complete cuffs and exactly three chain meshes");
        Check(s.Position==s.Length,"entire resource read without trailing or missing bytes");
    }
    static void TestDistance()
    {
        foreach(var direction in new[]{Vector3.right,Vector3.up,Vector3.forward})
        {
            var origin=new Vector3(.1f,.2f,.3f);
            Check(Vector3.Distance(MainGameVrHandcuffs.HandcuffDistance.Correction(origin,origin+direction*.04f),Vector3.zero)<1e-6f,"slack leaves opposite cuff unchanged");
            var free=origin+direction*.12f;
            free+=MainGameVrHandcuffs.HandcuffDistance.Correction(origin,free);
            Check(MathF.Abs(Vector3.Distance(origin,free)-.05f)<1e-6f,"pull only the excess beyond chain reach");
            var stopped=free;
            for(int i=0;i<100;i++) free+=MainGameVrHandcuffs.HandcuffDistance.Correction(origin,free);
            Check(Vector3.Distance(stopped,free)<1e-6f,"stopped hand produces no inertia or continuing oscillation");
            for(int i=0;i<3;i++)
            {
                var p=MainGameVrHandcuffs.HandcuffDistance.ChainPoint(origin,free,Vector3.forward,.012f+i*.013f);
                Check(float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z),"all three visual links remain finite at any direction");
            }
        }
    }
    static void TestChainOrientation()
    {
        foreach (var axis in new[] {Vector3.right,Vector3.up,Vector3.forward})
        {
            Hip.Instance=new Hip();VR.Mode=new();SteamVR_Actions.legacy_emulate=new();
            var p=new Cuffs();p.CuffAt(0,Vector3.zero);p.CuffAt(1,axis*.05f);p.Start();
            SteamVR_Actions.legacy_emulate.Grip_Press.Down(SteamVR_Input_Sources.LeftHand);
            for(int i=0;i<3;i++)
            {
                Check(Vector3.Distance(p.Part(i+2).right,axis)<1e-4f,"all three link long axes follow the span, including vertical and depth");
                var expected=axis*(.012f+i*.013f);
                Check(Vector3.Distance(p.Part(i+2).position,expected)<1e-5f,"link centres follow the actual chain span");
                if(i<2) Check(Vector3.Distance(p.Part(i+2).position,p.Part(i+3).position)<.0194f,"adjacent rings overlap along their long axes");
            }
            p.Stop();
        }
    }
    static void TestHeldConstraint()
    {
        foreach(int first in new[]{0,1})
        {
            Hip.Instance=new Hip();VR.Mode=new();SteamVR_Actions.legacy_emulate=new();
            var p=new Cuffs();p.CuffAt(first,Vector3.zero);p.CuffAt(1-first,Vector3.right*.04f);p.Start();
            var controller=VR.Mode.Left;var grip=SteamVR_Actions.legacy_emulate.Grip_Press;
            grip.Down(SteamVR_Input_Sources.LeftHand);
            var initialFree=p.Part(1-first).position;
            controller.transform.position=Vector3.right*.01f;Hip.EvaluateItems();
            Check(Vector3.Distance(initialFree,p.Part(1-first).position)<1e-6f,"real held callback leaves slack cuff fixed");
            controller.transform.position=Vector3.right*.12f;Hip.EvaluateItems();
            Check(MathF.Abs(Vector3.Distance(p.Part(first).position,p.Part(1-first).position)-.05f)<1e-6f,"either held cuff pulls the other only to maximum reach");
            var stopped=Enumerable.Range(0,5).Select(i=>p.Part(i).position).ToArray();
            for(int i=0;i<100;i++) Hip.EvaluateItems();
            Check(Enumerable.Range(0,5).All(i=>Vector3.Distance(stopped[i],p.Part(i).position)<1e-6f),"actual callback has no residual movement while hand is stopped");
            grip.Up(SteamVR_Input_Sources.LeftHand);
            stopped=Enumerable.Range(0,5).Select(i=>p.Part(i).position).ToArray();
            controller.transform.position=Vector3.up*2;Hip.EvaluateItems();
            Check(Enumerable.Range(0,5).All(i=>Vector3.Distance(stopped[i],p.Part(i).position)<1e-6f),"release unsubscribes evaluation and fixes all five parts");
            p.Stop();
        }
    }
    static void TestReleaseFailure()
    {
        VR.Mode=new();SteamVR_Actions.legacy_emulate=new();Hip.Instance=new Hip();
        var p=new Cuffs();p.CuffAt(0,Vector3.zero);p.CuffAt(1,new Vector3(1,0,0));p.Start();
        var grip=SteamVR_Actions.legacy_emulate.Grip_Press;var hand=SteamVR_Input_Sources.LeftHand;
        grip.Down(hand);p.ThrowOnDetach(0,true);
        try { grip.Up(hand); } catch (Exception) { }
        Check(p.Held==-1 && VR.Mode.Left.Current.IsInvalidating,"detach exception clears ownership before cleanup and releases focus");
        Check(Enumerable.Range(1,4).All(i=>p.Part(i).parent!=VR.Mode.Left.transform),"detach exception still detaches every other part");
        p.ThrowOnDetach(0,false);p.Stop();
        Check(!VR.Mode.Left.Current.IsValid,"shutdown completes pending focus after exception");
    }
    static void TestMountPose()
    {
        foreach(int hand in new[]{0,1}) foreach(int cuff in new[]{0,1})
        foreach(var pose in new[]{Quaternion.identity,Quaternion.Euler(23,67,-41),Quaternion.LookRotation(Vector3.up,Vector3.right),Quaternion.LookRotation(Vector3.forward*-1,Vector3.up)})
        {
            var wrist=new Vector3(.2f,.4f,.6f);
            MainGameVrHandcuffs.HandcuffMountPose.Calculate(cuff,hand,wrist,pose,out var p,out var q);
            var localP=Quaternion.Inverse(pose)*(p-wrist);
            var localQ=Quaternion.Inverse(pose)*q;
            var expectedPosition=new Vector3(hand==0?.008f:-.008f,.014f,0);
            Check(Vector3.Distance(localP,expectedPosition)<1e-6f,"fixed wrist-local position is exactly the same through all world poses");
            var lockDirection=localQ*MainGameVrHandcuffs.HandcuffMountPose.LockDirection(cuff);
            Check(Vector3.Dot(lockDirection,Vector3.up*-1)>.99999f,"either cuff on either wrist always places lock toward local palm -Y");
            Check(Vector3.Dot(localQ*Vector3.up,Vector3.right*(hand==0?-1:1))>.99999f,"fixed ring normal matches native left/right wrist X axis");
            var innerLock=localP+lockDirection*MainGameVrHandcuffs.HandcuffMountPose.LockInsideRadius;
            Check(Vector3.Distance(innerLock,new Vector3(hand==0?.008f:-.008f,-.018f,0))<1e-6f,"fixed housing edge seats 18mm on palm side and 8mm behind wrist");
            MainGameVrHandcuffs.HandcuffMountPose.Calculate(cuff,hand,wrist,pose,out var againP,out var againQ);
            Check(Vector3.Distance(againP,p)==0 && Vector3.Distance(againQ*Vector3.forward,q*Vector3.forward)==0,"repeated attachment has identical position and angle");
        }
    }
}
