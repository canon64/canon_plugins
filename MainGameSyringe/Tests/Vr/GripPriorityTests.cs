using UnityEngine;
using Valve.VR;
using VRGIN.Controls;
using VRGIN.Core;

static partial class Program
{
 static void CheckGripPriority()
 {
  var query=MainGirlHipHijack.SyringeGripPriority.Bind(typeof(MainGameSyringe.Plugin));
  Check(!MainGirlHipHijack.SyringeGripPriority.Bind(null)(VR.Mode.Right),"missing optional syringe must leave head grabbing available");
  Check(!MainGirlHipHijack.SyringeGripPriority.Bind(typeof(object))(VR.Mode.Right),"old syringe without priority API must leave head grabbing available");
  foreach(var side in new[]{SteamVR_Input_Sources.LeftHand,SteamVR_Input_Sources.RightHand})
  {
   var controller=side==SteamVR_Input_Sources.LeftHand?VR.Mode.Left:VR.Mode.Right;
   var other=side==SteamVR_Input_Sources.LeftHand?VR.Mode.Right:VR.Mode.Left;
   controller.transform.position=default;other.transform.position=default;
   var plugin=new MainGameSyringe.Plugin();plugin.Start();
   try
   {
    Check(query(controller),"head input processed before syringe grip must yield within grip range");
    Check(plugin.Held==MainGameSyringe.Hand.None&&plugin.Moves==0&&plugin.Doses==0,"priority query must not grab, insert or inject");
    controller.transform.position=new Vector3(.081f,0,0);
    Check(!query(controller),"head must remain available beyond syringe grip distance");
    controller.transform.position=default;
    plugin.Allowed=false;Check(!query(controller),"denied syringe must not reserve head input");plugin.Allowed=true;
    VR.Active=false;Check(!query(controller),"inactive VR must not reserve head input");VR.Active=true;
    Check(!query(null)&&!query(new Controller()),"unknown controller must not reserve head input");
    SteamVR_Actions.legacy_emulate.Grip_Press.Down(side);
    Check(query(controller)&&!query(other),"head input processed after syringe grip must yield only on the owning hand");
    SteamVR_Actions.legacy_emulate.Axis1_Press.Down(side);
    SteamVR_Actions.legacy_emulate.Axis1_Press.Up(side);
    SteamVR_Actions.legacy_emulate.Grip_Press.Up(side);
    Check(plugin.Inserted&&query(controller),"released inserted syringe must still take priority over head");
    SteamVR_Actions.legacy_emulate.Grip_Press.Down(side);
    Check(query(controller)&&plugin.Inserted&&plugin.Doses==0,"regrabbing inserted syringe must preserve priority without dosing");
    SteamVR_Actions.legacy_emulate.Axis1_Press.Down(side);
    SteamVR_Actions.legacy_emulate.Axis1_Press.Up(side);
    SteamVR_Actions.legacy_emulate.Grip_Press.Up(side);
    Check(query(controller)&&plugin.Doses==1,"spent inserted syringe must still take priority over head");
   }
   finally {plugin.Stop();}
   Check(!query(controller),"despawn/session stop must remove syringe priority");
  }
 }
}
