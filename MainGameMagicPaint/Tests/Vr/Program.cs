using System;
using MainGameMagicPaint;
using Valve.VR;
using CanonPlugins.Shared;
static class Program
{
    static int checks;static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static void Main()
    {
        Console.WriteLine("MagicPaint real VR-handler boundary tests (no game launch)");
        foreach(var hand in new[]{SteamVR_Input_Sources.LeftHand,SteamVR_Input_Sources.RightHand})
        {
            var other=hand==SteamVR_Input_Sources.LeftHand?SteamVR_Input_Sources.RightHand:SteamVR_Input_Sources.LeftHand;
            var p=new Plugin();p.Connect();p.Down(hand);Check(!p.Paint,"unheld down rejected");p.Grip(hand);Check(p.Held&&!p.Paint,"grip does not paint");
            p.Down(other);Check(!p.Paint,"other hand cannot start painting");p.Down(hand);Check(p.Paint,"held-hand down enables trigger permission");
            p.Ungrip(other);Check(p.Held&&p.Paint,"other hand up cannot release tool");var focus=VrItemFocus.Last;p.Ungrip(hand);
            Check(!p.Held&&!p.Paint&&focus.Releases==1,"release revokes state and returns focus");int queries=p.Queries;p.HeldTick();Check(p.Queries==queries,"no contact work after release");
            p.Grip(hand);Check(!p.Paint,"regrip does not resume old trigger");p.Down(hand);p.Up(hand);Check(!p.Paint&&p.Captures>0,"trigger up ends and requests inspection");
            p.A(hand);Check(p.Settings,"A opens settings");p.Stick(0,0,hand);p.Stick(0,1,hand);Check(p.Selection==-1,"up selects previous item");
            p.Stick(0,0,hand);p.Stick(1,0,hand);p.HeldTick();Check(p.Adjustment>0&&p.RedoCount==0,"right adjusts setting without redo");
            p.A(hand);p.Stick(0,0,hand);p.Stick(-1,0,hand);Check(!p.Settings&&p.UndoCount==1,"closed settings restores undo");
            p.Down(hand);p.LoseTracking();Check(!p.Held&&!p.Paint,"tracking loss releases");
            p.Grip(hand);var last=VrItemFocus.Last;p.ThrowOnEnd();try{p.Ungrip(hand);}catch(InvalidOperationException){}
            Check(!p.Held&&!p.Paint&&last.Releases==1,"flush exception still clears holder/trigger/loop and focus");
        }
        Console.WriteLine("PASS "+checks+" real-handler checks; Unity rendering remains unverified.");
    }
}
