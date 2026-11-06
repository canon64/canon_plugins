using System;
using System.Collections.Generic;
using System.Linq;
using MainGameDesktopDisplay;
using Valve.VR;
namespace UnityEngine {public class Transform{}public struct Vector2{public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}}
static class Program
{
    static int checks;
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
    static int Main()
    {
        try
        {
            var sentinelSet=new SteamVR_ActionSet();var sentinelAction=new SteamVR_Action_Boolean();
            SteamVR_Input.actionSets=new[]{sentinelSet};SteamVR_Input.actions=new SteamVR_Action[]{sentinelAction};
            var calls=new List<string>();var input=new DesktopButtonInput((right,down)=>calls.Add((right?"B ":"A ")+down),axis=>calls.Add("axis "+axis.x+" "+axis.y));
            input.Connect();input.Connect();
            Check(SteamVR_Input.actionSets.Length==2&&SteamVR_Input.actions.Length==4,"connect is idempotent and preserves preexisting actions");
            var a=SteamVR_Input.actionsBoolean.Single(x=>x.Path.EndsWith("left_click"));
            var b=SteamVR_Input.actionsBoolean.Single(x=>x.Path.EndsWith("right_click"));
            var stick=SteamVR_Input.actionsVector2.Single();var set=SteamVR_Input.actionSets.Last();
            Check(a.Count==3&&b.Count==3&&stick.Count==1,"registers physical A/B down-up-active and dedicated stick change handlers");
            a.Down(SteamVR_Input_Sources.RightHand);a.Up(SteamVR_Input_Sources.RightHand);
            b.Down(SteamVR_Input_Sources.RightHand);b.Up(SteamVR_Input_Sources.RightHand);
            Check(calls.SequenceEqual(new[]{"A True","A False","B True","B False"}),"physical button paths route exact left/right press and release");
            int n=calls.Count;a.Down(SteamVR_Input_Sources.LeftHand);b.Down(SteamVR_Input_Sources.LeftHand);Check(calls.Count==n,"left-hand face buttons do not alias right-hand A/B");
            stick.Move(SteamVR_Input_Sources.RightHand,-1,1);Check(calls.Last()=="axis -1 1","preserves both stick axes");
            input.Active(true);Check(set.Active&&set.Priority==100,"activates right-hand desktop set at explicit priority");
            input.Active(false);Check(!set.Active,"OFF returns native input action set");
            a.SetActive(SteamVR_Input_Sources.RightHand,false);Check(calls.TakeLast(2).SequenceEqual(new[]{"A False","B False"}),"native input deactivation releases both Windows button paths");
            input.Dispose();input.Dispose();
            Check(a.Count==0&&b.Count==0&&stick.Count==0,"dispose removes every button and stick handler");
            Check(SteamVR_Input.actions.SequenceEqual(new SteamVR_Action[]{sentinelAction})&&SteamVR_Input.actionSets.SequenceEqual(new[]{sentinelSet}),"dispose preserves other plugins' actions");
            SteamVR_Input.FailHandles=true;
            try{input.Connect();throw new Exception("missing manifest accepted");}catch(InvalidOperationException){}
            Check(SteamVR_Input.actions.Length==1&&SteamVR_Input.actionSets.Length==1&&SteamVR_Input.actionsBoolean.Length==0&&SteamVR_Input.actionsVector2.Length==0,"missing native handles rolls back action arrays and set");
            SteamVR_Input.FailHandles=false;
            Console.WriteLine(checks+" real DesktopButtonInput boundary checks passed; physical device not exercised.");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
