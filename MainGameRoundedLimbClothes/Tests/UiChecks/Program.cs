using System;
using MainGameRoundedLimbClothes;
using UnityEngine;
using MainGameUiInputCapture;
using Sideloader.AutoResolver;

static class Program
{
    static int checks;
    static void Check(bool good,string why){checks++;if(!good)throw new Exception(why);}
    static void Main()
    {
        Console.WriteLine("START production Plugin.UI event checks with IMGUI boundary substitution");
        var p=Plugin.Instance;p.Permit=true;var c=new RoundedLimbController();c.Reload();
        p.TestOpen(c);Check(p.WindowOpen&&UiInputCaptureApi.Blocked,"opening immediately registers the window input rect");
        GUI.enabled=false;
        GUILayout.NextSlider=64;p.TestGui(EventType.MouseDown);
        Check(c.Settings.LeftArm==64&&c.Settings.RightArm==64,"slider input is accepted despite a previous plugin leaving GUI disabled");
        Check(!GUI.enabled,"foreign GUI enabled state restored");Check(UiInputCaptureApi.Dragging,"drag explicitly captured");
        GUILayout.NextSlider=72;p.TestGui(EventType.MouseDrag);
        Check(c.Settings.LeftArm==72&&UiInputCaptureApi.Dragging,"continued drag updates the same preview");
        p.TestGui(EventType.MouseUp);Check(!UiInputCaptureApi.Dragging,"mouse release clears capture");
        GUI.enabled=true;
        GUILayout.ClickButton="閉じる";p.TestGui(EventType.MouseUp);
        Check(p.WindowOpen,"layout-changing close waits for Layout");p.TestGui(EventType.Layout);
        Check(!p.WindowOpen&&!UiInputCaptureApi.Blocked&&!UiInputCaptureApi.Unlock,"close removes blocker and idle cursor ownership");
        for(int i=0;i<3;i++){p.TestOpen(c);Check(p.WindowOpen,"can reopen after close");GUILayout.ClickButton="閉じる";p.TestGui(EventType.MouseUp);p.TestGui(EventType.Layout);}
        // Geometry rejection must leave the slider at the requested value and the editor open.
        UniversalAutoResolver.Entries[100000123]=new ResolveInfo{GUID=Plugin.ModGuid,Slot=1};c.ChaControl.nowCoordinate.clothes.parts[4].id=100000123;c.Refresh();
        p.TestOpen(c);GUILayout.NextSlider=13;p.TestGui(EventType.MouseDrag);
        Check(c.Settings.LeftArm==13&&c.Status.StartsWith("未適用"),"failed geometry reports failure without snapping slider back");
        GUILayout.ClickButton="決定";p.TestGui(EventType.MouseUp);p.TestGui(EventType.Layout);
        Check(p.WindowOpen&&c.Editing,"failed preview does not close the editor on commit");
        GUILayout.NextSlider=40;p.TestGui(EventType.MouseDrag);GUILayout.ClickButton="決定";p.TestGui(EventType.MouseUp);p.TestGui(EventType.Layout);
        Check(!p.WindowOpen&&c.Settings.LeftArm==40,"successful preview commits and closes");
        Console.WriteLine("PASS production UI state/event checks="+checks);
    }
}
