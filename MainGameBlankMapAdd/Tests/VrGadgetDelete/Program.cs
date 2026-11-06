using UnityEngine;
using Valve.VR;
using VRGIN.Core;
using P=MainGameBlankMapAdd.Plugin;

class Program
{
    static int checks;
    static void Check(bool value,string message) { checks++;if(!value)throw new Exception(message); }
    static void Main()
    {
        foreach(var hand in new[]{SteamVR_Input_Sources.LeftHand,SteamVR_Input_Sources.RightHand})
        {
            var p=new P();p.Start();
            var action=CanonPlugins.Shared.DedicatedVrButton.Current;
            var other=hand==SteamVR_Input_Sources.LeftHand?SteamVR_Input_Sources.RightHand:SteamVR_Input_Sources.LeftHand;
            int deleted=0;Action delete=()=>deleted++;
            var rect=new Rect(10,10,60,24);Input.mousePosition=new Vector2(20,Screen.height-20);
            p.Draw(rect,"item",delete);
            SteamVR_Actions.legacy_emulate.ApplicationMenu_Press.Down(hand);
            SteamVR_Actions.legacy_emulate.ApplicationMenu_Press.Up(hand);
            Check(deleted==0,"legacy physical A cannot delete an item");
            action.Down(hand);action.Up(hand);action.Up(hand);
            Check(deleted==1,"same item B down/up deletes once on either hand");
            action.Down(hand);action.Up(other);Check(deleted==1,"other hand cannot release capture");
            Input.mousePosition=new Vector2(200,Screen.height-20);action.Up(hand);
            Check(deleted==1,"release outside cancels");Input.mousePosition=new Vector2(20,Screen.height-20);
            action.Down(hand);p.Hide();action.Up(hand);Check(deleted==1,"hide clears captured delete");
            Time.frameCount++;p.Draw(rect,"item",delete);action.Down(hand);
            Time.frameCount++;p.Draw(rect,"replacement",delete);action.Up(hand);
            Check(deleted==1,"different item at same coordinates cannot be deleted");
            Time.frameCount++;p.Draw(rect,"item",delete);action.Down(hand);Time.frameCount+=2;action.Up(hand);
            Check(deleted==1,"stale GUI target is rejected");
            p.Draw(rect,"item",delete);action.Down(hand);p.Focus(false);p.Draw(rect,"item",delete);action.Up(hand);
            Check(deleted==1,"VR focus loss cancels even if GUI keeps repainting");p.Focus(true);
            p.Draw(rect,"item",delete);action.Down(hand);action.Active(hand,false);action.Up(hand);
            Check(deleted==1,"controller deactivation cancels capture");
            p.Draw(rect,"item",delete);VR.Mode.Left.Menu.LaserVisible=VR.Mode.Right.Menu.LaserVisible=false;
            action.Down(hand);VR.Mode.Left.Menu.LaserVisible=VR.Mode.Right.Menu.LaserVisible=true;action.Up(hand);
            Check(deleted==1,"requires pointing controller laser");
            p.Draw(rect,"item",delete);action.Down(hand);p.Stop();action.Up(hand);
            Check(deleted==1 && action.ListenerCount==0,"session end removes listeners and pending action");
            p.Start();action=CanonPlugins.Shared.DedicatedVrButton.Current;Time.frameCount++;p.Draw(rect,"syringe",delete);action.Down(hand);action.Up(hand);
            Check(deleted==2,"new session and syringe-specific callback work");p.Stop();
        }
        Console.WriteLine($"PASS {checks} production VR item-delete boundary checks; native VR UI requires in-game confirmation.");
    }
}
namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        bool _sessionCommandsOpen;
        public void Start(){_sessionCommandsOpen=true;ConnectVrGadgetDelete();}
        public void Stop(){_sessionCommandsOpen=false;DisconnectVrGadgetDelete();}
        public void Hide()=>ClearVrGadgetDeleteTargets();
        public void Draw(Rect rect,string item,Action delete)=>RegisterVrGadgetDelete(rect,item,delete);
        public void Focus(bool focus)=>_gadgetDeleteFocus.Handler(focus);
    }
}
namespace UnityEngine
{
    public record struct Vector2(float x,float y);
    public record struct Rect(float x,float y,float width,float height)
    { public bool Contains(Vector2 p)=>p.x>=x && p.x<x+width && p.y>=y && p.y<y+height; }
    public static class Input {public static Vector2 mousePosition;}
    public static class Screen {public static int height=1080;}
    public static class Time {public static int frameCount=100;}
    public static class GUI {public static bool enabled=true;}
    public enum EventType {Repaint}
    public class Event {public static Event current=new();public EventType type=EventType.Repaint;}
}
namespace Valve.VR
{
    public enum SteamVR_Input_Sources {LeftHand,RightHand}
    public class SteamVR_Action_Boolean
    {
        public delegate void State(SteamVR_Action_Boolean a,SteamVR_Input_Sources s);
        public delegate void ActiveChange(SteamVR_Action_Boolean a,SteamVR_Input_Sources s,bool active);
        readonly Dictionary<SteamVR_Input_Sources,State> down=new(),up=new();
        readonly Dictionary<SteamVR_Input_Sources,ActiveChange> active=new();
        public int ListenerCount=>down.Count+up.Count+active.Count;
        public void AddOnStateDownListener(State h,SteamVR_Input_Sources s)=>down[s]=h;
        public void AddOnStateUpListener(State h,SteamVR_Input_Sources s)=>up[s]=h;
        public void AddOnActiveChangeListener(ActiveChange h,SteamVR_Input_Sources s)=>active[s]=h;
        public void RemoveOnStateDownListener(State h,SteamVR_Input_Sources s)=>down.Remove(s);
        public void RemoveOnStateUpListener(State h,SteamVR_Input_Sources s)=>up.Remove(s);
        public void RemoveOnActiveChangeListener(ActiveChange h,SteamVR_Input_Sources s)=>active.Remove(s);
        public void Down(SteamVR_Input_Sources s){if(down.TryGetValue(s,out var h))h(this,s);}
        public void Up(SteamVR_Input_Sources s){if(up.TryGetValue(s,out var h))h(this,s);}
        public void Active(SteamVR_Input_Sources s,bool value){if(active.TryGetValue(s,out var h))h(this,s,value);}
    }
    public static class SteamVR_Actions
    { public static class legacy_emulate {public static SteamVR_Action_Boolean ApplicationMenu_Press=new();} }
    public static class SteamVR_Events
    {
        public class Action {public bool enabled;public System.Action<bool> Handler;}
        public static Action InputFocusAction(System.Action<bool> h)=>new(){Handler=h};
    }
}
namespace VRGIN.Controls.Handlers {public class MenuHandler {public bool LaserVisible=true;}}
namespace VRGIN.Core
{
    public static class VR {public static bool Active=true;public static ModeState Mode=new();}
    public class ModeState {public Controller Left=new(),Right=new();}
    public class Controller
    {
        public VRGIN.Controls.Handlers.MenuHandler Menu=new();
        public T GetComponent<T>() where T:class=>Menu as T;
    }
}
