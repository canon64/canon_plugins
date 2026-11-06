using UnityEngine;
using Valve.VR;
using VRGIN.Core;

static partial class Program
{
 static void CheckReturnAnchor()
 {
  foreach(var side in new[]{SteamVR_Input_Sources.LeftHand,SteamVR_Input_Sources.RightHand})
  foreach(var axis in new[]{new Vector3(1,0,0),new Vector3(0,1,0),new Vector3(0,0,1)})
  {
   var controller=side==SteamVR_Input_Sources.LeftHand?VR.Mode.Left:VR.Mode.Right;
   var start=new Vector3(2,1,-64);
   controller.transform.position=start;
   var p=new MainGameSyringe.Plugin();
   p.Model.position=start+new Vector3(.07f,.02f,.01f);
   p.Model.rotation=new Quaternion{Angle=37};p.Model.localScale=new Vector3(1.8f,1.8f,1.8f);
   p.InsertionSnap=new Vector3(.07f,.03f,.02f);p.Start();
   try
   {
    SteamVR_Actions.legacy_emulate.Grip_Press.Down(side);
    Check(p.Held!=MainGameSyringe.Hand.None,"offset syringe must be grabbable before insertion");
    SteamVR_Actions.legacy_emulate.Axis1_Press.Down(side);
    SteamVR_Actions.legacy_emulate.Axis1_Press.Up(side);
    Check(Vector3.Distance(start,p.Model.position)>.12f,"regression must exceed the old origin threshold after insertion snap");
    Check(p.Inserted&&p.Doses==0,"stationary hand must not immediately withdraw or dose after insertion snap");
    Check(Vector3.Distance(start,p.ReturnAnchor)<.00001f,"captured anchor must match actual hand despite item offset, rotation and scale");
    Check(Vector3.Distance(p.Model.TransformPoint(p.BoundaryLocal),p.ReturnAnchor)<.00001f,"physics sphere and trigger distance must share the same anchor");
    controller.transform.position=start+Vector3.Scale(axis,new Vector3(.119f,.119f,.119f));
    p.Rearm();p.PhysicsExit();
    Check(p.Inserted,"movement below 12cm and settings rearm must preserve insertion");
    Check(Vector3.Distance(start,p.ReturnAnchor)<.00001f,"settings rearm must not reset distance already moved");
    controller.transform.position=start+Vector3.Scale(axis,new Vector3(.121f,.121f,.121f));
    p.PhysicsExit();
    Check(!p.Inserted&&p.Doses==0,"movement above 12cm must withdraw without dosing on every axis");
   }
   finally
   {
    p.Stop();controller.transform.position=default;controller.transform.rotation=default;
   }
  }
 }
}
