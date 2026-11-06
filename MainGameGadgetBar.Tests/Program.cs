using System;
using System.Collections.Generic;
using CanonPlugins.Shared;
using UnityEngine;
namespace UnityEngine
{
 public struct Rect
 {
  public bool Contains(Vector2 p)=>p.x>=0&&p.x<58&&p.y>=0&&p.y<25;
 }
 public struct Vector2 {public float x,y;public Vector2(float a,float b){x=a;y=b;}}
 public enum FocusType {Passive}
 public enum EventType {Repaint,MouseDown,MouseUp,Used}
 public class Event
 {
  public static Event current;public EventType type;public int button;public Vector2 mousePosition;
  public void Use(){type=EventType.Used;}
 }
 public static class GUIUtility {public static int hotControl;public static int GetControlID(int hint,FocusType focus,Rect rect)=>hint;}
 public class GUIStyle {}
 public class GUIContent {public GUIContent(string text,string hint){}}
 public static class GUI
 {
  public class Skin {public GUIStyle button=new();}
  public static bool enabled=true;public static Skin skin=new();
  public static bool Button(Rect rect,GUIContent label,GUIStyle style)=>enabled&&Event.current.type==EventType.MouseUp&&Event.current.button==0&&rect.Contains(Event.current.mousePosition);
 }
}
namespace BepInEx.Bootstrap
{
 public static class Chainloader
 {
  public class Info {public object Instance;}
  public class Registry
  {
   public int Reads;public Dictionary<string,Info> Items=new();
   public bool TryGetValue(string guid,out Info info){Reads++;return Items.TryGetValue(guid,out info);}
  }
  public static Registry PluginInfos=new();
 }
}
namespace MainGameBlankMapAdd
{
 public sealed partial class Plugin
 {
  void RegisterVrGadgetDelete(Rect rect,string label,Action remove) { }
  readonly GadgetBarConnection testConnection=new("test");
  public void Draw()=>DrawGadgetBarButton(new Rect(),"test",testConnection);
  public string Hint=>testConnection.Hint;
 }
}
public sealed class FakeGadget
{
 readonly GadgetBarCycle cycle=new();
 public int Recalls,Gizmos,Deletes,Hides;public bool Exists,FailGizmo,Visible;
 public void PlaybackBarPrimaryAction()=>cycle.Press(Exists,()=>{Recalls++;Exists=true;return true;},()=>{Gizmos++;if(FailGizmo)return false;Visible=true;return true;},()=>{Hides++;Visible=false;},Visible);
 public void PlaybackBarDelete(){Deletes++;Exists=false;Visible=false;cycle.Reset();}
 public string GetPlaybackBarActionHint()=>!Exists?"recall":Visible?"hide":"gizmo";
}
static class Program
{
 static int checks;
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static void Draw(MainGameBlankMapAdd.Plugin plugin,EventType type,int button=0,float x=1)
 {
  Event.current=new Event{type=type,button=button,mousePosition=new Vector2(x,1)};plugin.Draw();
 }
 static void Main()
 {
  var gadget=new FakeGadget();BepInEx.Bootstrap.Chainloader.PluginInfos.Items["test"]=new(){Instance=gadget};
  var p=new MainGameBlankMapAdd.Plugin();
  for(int i=0;i<20;i++)Draw(p,EventType.Repaint);
  Check(BepInEx.Bootstrap.Chainloader.PluginInfos.Reads==0,"drawing polled plugin resolution");
  Draw(p,EventType.MouseUp);Check(gadget.Recalls==1&&gadget.Gizmos==0&&gadget.Deletes==0,"first click did not recall");
  Draw(p,EventType.MouseUp);Check(gadget.Recalls==1&&gadget.Gizmos==1,"second click did not show gizmo");
  Draw(p,EventType.MouseUp);Check(gadget.Recalls==1&&gadget.Hides==1&&!gadget.Visible,"third click must hide without recalling");
  for(int i=0;i<10;i++){Draw(p,EventType.MouseUp);Check(gadget.Visible,"repeat show");Draw(p,EventType.MouseUp);Check(!gadget.Visible&&gadget.Recalls==1,"repeat hide must never recall");}
  Draw(p,EventType.MouseUp);gadget.Visible=false;Draw(p,EventType.MouseUp);
  Check(gadget.Visible&&gadget.Recalls==1,"external hide must show next, never recall");
  Draw(p,EventType.MouseDown,1);Check(gadget.Deletes==0&&GUIUtility.hotControl!=0&&Event.current.type==EventType.Used,"right down was not captured");
  Draw(p,EventType.MouseUp,1,90);Check(gadget.Deletes==0&&GUIUtility.hotControl==0,"right release outside deleted object or retained capture");
  Draw(p,EventType.MouseDown,1);Draw(p,EventType.MouseUp,1);
  Check(gadget.Deletes==1&&!gadget.Exists&&Event.current.type==EventType.Used,"right click did not delete/consume input");
  Draw(p,EventType.MouseUp);Check(gadget.Recalls==2&&gadget.Exists,"after delete, next left click was not recall");
  gadget.FailGizmo=true;Draw(p,EventType.MouseUp);Check(p.Hint=="gizmo","failed gizmo operation advanced phase");
  gadget.FailGizmo=false;Draw(p,EventType.MouseUp);Check(p.Hint=="hide","gizmo retry did not show");
  GUI.enabled=false;Draw(p,EventType.MouseDown,1);Draw(p,EventType.MouseUp,1);
  Check(gadget.Deletes==1,"disabled button deleted object");GUI.enabled=true;
  gadget.Exists=false;Draw(p,EventType.MouseUp);Check(gadget.Recalls==3,"externally deleted object did not reset recall");
  Console.WriteLine("GADGET_BAR_OK checks="+checks+" (production cycle/button routing; Unity/host boundary stubs)");
 }
}
