using UnityEngine;
using VRGIN.Core;
using Hip=MainGirlHipHijack.Plugin;

static partial class Program
{
    static Hip StartElbowTest(int controllerHand,int wrist,out VRGIN.Controls.Controller controller,out int elbow)
    {
        var p=NewWristTest();controller=controllerHand==0?VR.Mode.Left:VR.Mode.Right;
        controller.transform.position=p.Effectors[wrist].bone.position;
        elbow=wrist==0?8:9;
        p.Bends[elbow].bone2.position=p.Effectors[wrist].bone.position+new Vector3(0,-.3f,0);
        p.WristStep(controllerHand,"Grip");
        return p;
    }
    static void TestElbowAim()
    {
        using var physicalBindings=System.Text.Json.JsonDocument.Parse(File.ReadAllText(@"F:\kks\KoikatsuSunshine_Data\StreamingAssets\SteamVR\bindings_oculus_touch.json"));
        var rightA=physicalBindings.RootElement.GetProperty("bindings").GetProperty("/actions/legacy_emulate").GetProperty("sources").EnumerateArray()
            .Single(s=>s.GetProperty("path").GetString()=="/user/hand/right/input/a");
        Check(rightA.GetProperty("inputs").GetProperty("click").GetProperty("output").GetString()=="/actions/legacy_emulate/in/applicationmenu_press","installed physical A maps to ApplicationMenu rather than legacy A");
        foreach(int hand in new[]{0,1}) foreach(int wrist in new[]{0,1})
        {
            var p=StartElbowTest(hand,wrist,out var controller,out int elbow);
            var pivot=p._bikEff[wrist].Proxy;
            var actualElbow=p.Bends[elbow].bone2.position;
            p.WristStep(hand,"ApplicationMenu");var target=p._bikEff[elbow].Proxy;
            Check(target!=null,"physical A/ApplicationMenu must begin elbow control");
            Check(p.HeldWrist(hand)==wrist && p.AimingElbow(hand)==elbow,"A selects the elbow of the grabbed wrist, regardless of controller side");
            Check(p._bikWant[elbow] && p._bikEff[elbow].Running && p.Bends[elbow].bendGoal==target && p.Bends[elbow].weight==1,"A auto-enables the corresponding elbow IK at full runtime weight");
            Check(Vector3.Distance(target.position,actualElbow+new Vector3(0,-.2f,0))<1e-6f,"default elbow target lies twenty centimetres past the elbow on wrist-to-elbow ray");
            Check(!p._settings.Enabled[elbow] && p.SavedSettings==0,"elbow auto-enable does not save the runtime IK state");
            var localTarget=target.localPosition;
            controller.transform.rotation=Quaternion.Euler(0,0,90);
            Check(target.parent==pivot && Vector3.Distance(target.position,pivot.TransformPoint(localTarget))<1e-6f,"elbow target rotates about held wrist without a frame or movement notification");
            Check(Vector3.Distance(target.position,pivot.position+new Vector3(.5f,0,0))<1e-5f,"wrist rotation swings the whole forearm goal through the same angle");
            p.ElbowExtension(.35f);
            Check(MathF.Abs(Vector3.Distance(target.position,pivot.position)-.65f)<1e-5f && p.SavedSettings==1,"changing extension updates an active goal once and saves the distance");
            var placed=target.position;p.WristStep(hand,up:"ApplicationMenu");
            controller.transform.position+=Vector3.up;
            Check(p.AimingElbow(hand)==-1 && target.parent==null && Vector3.Distance(target.position,placed)<1e-6f && p._bikWant[elbow],"A release fixes elbow IK in world space while wrist remains held");
            p.WristStep(hand,"ApplicationMenu");Check(target.parent==pivot && p.AimingElbow(hand)==elbow,"A can be pressed again to re-aim existing elbow IK");
            p.WristStep(hand,up:"Grip");placed=target.position;
            Check(target.parent==null && p.AimingElbow(hand)==-1 && p._bikEff[elbow].Running,"grip release during A detaches elbow target and preserves IK");
            controller.transform.position=pivot.position;p.WristStep(hand,"Grip");p.WristStep(hand,"Axis0");
            Check(!p._bikWant[wrist] && !p._bikWant[elbow] && !p._bikEff[elbow].Running,"B restores animation for wrist and elbow created by this wrist gesture");
        }
        var unrelated=StartElbowTest(0,0,out _,out int e);
        unrelated._bikEff[9].Running=true;unrelated._bikWant[9]=true;
        unrelated.WristStep(0,"ApplicationMenu");unrelated.WristStep(0,"Axis0");
        Check(unrelated._bikWant[9] && unrelated._bikEff[9].Running,"B preserves the opposite elbow");
        var busy=StartElbowTest(0,0,out _,out e);busy._bikEff[e].GizmoDragging=true;busy.WristStep(0,"ApplicationMenu");
        Check(busy.AimingElbow(0)==-1 && !busy._bikWant[e],"do not overwrite an elbow already being manipulated");
        var failed=StartElbowTest(0,0,out _,out e);failed.EnableFails=true;failed.WristStep(0,"ApplicationMenu");
        Check(failed.AimingElbow(0)==-1 && !failed._bikWant[e],"failed elbow enable leaves no aiming state");
        var zero=StartElbowTest(0,0,out _,out e);zero.Bends[e].bone2.position=zero._bikEff[0].Proxy.position;zero.WristStep(0,"ApplicationMenu");
        Check(!zero._bikWant[e],"undefined wrist-to-elbow direction is rejected without guessing an axis");
        var denied=StartElbowTest(0,0,out _,out e);CanonPlugins.Local.SessionGate.Allowed=false;denied.WristStep(0,"ApplicationMenu");
        Check(!denied._bikWant[e],"revoked gate forbids creating elbow target");denied.StopWristMode();CanonPlugins.Local.SessionGate.Allowed=true;
        var focus=StartElbowTest(0,0,out var c,out e);focus.ConnectWristFocus();focus.WristStep(0,"ApplicationMenu");focus.LoseWristFocus();
        Check(focus.AimingElbow(0)==-1 && focus._bikEff[e].Proxy.parent==null && focus._bikEff[e].Running && !c.Current.IsValid,"focus loss releases elbow attachment and controller focus while retaining IK pose");
        var disable=StartElbowTest(0,0,out _,out e);disable.WristStep(0,"ApplicationMenu");disable.DisableArmPart(e);
        Check(disable.AimingElbow(0)==-1 && disable._bikEff[e].Proxy.parent==null && !disable._bikEff[e].Running,"manual elbow IK disable detaches active aiming target");
        var mode=StartElbowTest(0,0,out c,out e);mode.WristStep(0,"ApplicationMenu");mode.StopWristMode();
        Check(mode._bikEff[e].Proxy.parent==null && mode._bikEff[e].Running && !c.Current.IsValid,"mode off leaves an independent elbow target");
        Check(Hip.ClampVRElbowExtension(0)==0 && Hip.ClampVRElbowExtension(.2f)==.2f && Hip.ClampVRElbowExtension(float.NaN)==.2f,"distance permits zero and preserves twenty-centimetre default for invalid values");
    }
}
